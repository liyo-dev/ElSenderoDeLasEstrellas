# -*- coding: utf-8 -*-
"""
validar.py -- comprueba que lo que dice la secuencia existe de verdad.

Tres preguntas, y ninguna la contesta el compilador:

  1. Cada CAMPO que se escribe en un beat, ¿existe en esa clase? (esta si la pilla
     el compilador, pero es gratis comprobarla aqui y ahorra un ciclo de Unity).
  2. Cada MARCA que se nombra, ¿esta dada de alta en la escena?
  3. Cada GESTO que se pide, ¿existe como estado del Animator DE ESE ACTOR?

Un nombre mal escrito en cualquiera de las tres no da error: falla en silencio en
partida y solo se ve grabando.
"""
import os, re, sys
import p3

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BEATS = os.path.join(RAIZ, "Assets", "Scripts", "Sequences")
ESCENA = os.path.join(RAIZ, "Assets", "Scenes", "Worlds", "Prologo_Valle.unity")
# Cada actor tiene su propio controller y NO son el mismo. Comparten casi todo el
# vocabulario, que es justo lo que hace facil el fallo de INC-214: un beat le pedia a Will
# `Attack2`, que existe -- en el controller de los NPCs, no en el suyo.
ID_JUGADOR = "Player"
CONTROLLERS = {
    "NPC_NoWeapon": os.path.join(RAIZ, "Assets", "Art", "Characters", "Animator",
                                 "NPC_NoWeapon.controller"),
    "Invector@BasicLocomotion": os.path.join(RAIZ, "Assets", "Plugins",
                                             "Invector-3rdPersonController_LITE", "Animator",
                                             "Invector@BasicLocomotion.controller"),
}
WIRING = os.path.join(RAIZ, "Assets", "Editor", "PrologoValleMultitudWiring.cs")

def campos_por_clase():
    campos = {}
    for raiz, _, ficheros in os.walk(BEATS):
        for n in ficheros:
            if not n.endswith(".cs"): continue
            src = open(os.path.join(raiz, n), encoding="utf-8", errors="replace").read()
            trozos = re.split(r"\npublic (?:sealed )?class ", src)
            for t in trozos[1:]:
                m = re.match(r"(\w+Beat)\b", t)
                if not m: continue
                cls = m.group(1)
                propios = set(re.findall(r"public\s+[\w<>\[\].]+\s+(\w+)\s*(?:=|;)", t))
                campos[cls] = propios | {"note"}
    return campos

def marcas_de_la_escena():
    nombres = set(re.findall(r"m_Name: (M_[\w]+)", open(ESCENA, encoding="utf-8", errors="replace").read()))
    # Las que el wiring crea y que pueden no estar todavia en el .unity.
    w = open(WIRING, encoding="utf-8", errors="replace").read()
    nombres |= set(re.findall(r'"(M_[\w]+)"', w))
    # Las que el wiring genera con un contador ($"M_Huida_{i+1:00}") no salen del regex de
    # arriba, porque en el C# no aparecen escritas enteras en ningun sitio.
    for patron, cuantos in (("M_Aldeano_%02d", 10), ("M_Puente_%02d", 10),
                            ("M_Huida_%02d", 10), ("M_Lejos_%02d", 10)):
        for i in range(1, cuantos + 1):
            nombres.add(patron % i)
    return nombres

def _yaml(ruta):
    return open(ruta, encoding="utf-8", errors="replace").read()

def estados_del_animator(ruta):
    """Solo los objetos de clase 1102 (AnimatorState) son estados de verdad.

    Un .controller tiene `m_Name:` tambien en los parametros, en las capas y en los blend
    trees, asi que un `re.findall("m_Name: ...")` sobre el fichero entero mete todo eso en
    el saco de "estados" -- y entonces un parametro pasa por estado. Es exactamente lo que
    dejo pasar `Fidget` en INC-264: diez beats por fase pidiendo un gesto que no existia, y
    el comprobador diciendo que todo bien.
    """
    estados = set()
    for bloque in _yaml(ruta).split("--- !u!1102 ")[1:]:
        m = re.search(r"^  m_Name: (.+)$", bloque, re.M)
        if m:
            estados.add(m.group(1).strip())
    return estados

def parametros_del_animator(ruta):
    """Los nombres que son PARAMETRO y no estado, para poder decirlo en el aviso."""
    m = re.search(r"\n  m_AnimatorParameters:\n(.*?)\n  m_AnimatorLayers:", _yaml(ruta), re.S)
    return set(re.findall(r"^  - m_Name: (.+)$", m.group(1), re.M)) if m else set()

def main():
    campos = campos_por_clase()
    marcas = marcas_de_la_escena()
    # Sin listas blancas: los estados de salto y de vuelo ya estan dados de alta de verdad
    # en los dos controllers (NpcAnimatorSaltoYVueloWiring, 20 sep 2026). Si alguno faltara,
    # que se vea aqui en vez de taparlo.
    estados = {k: estados_del_animator(v) for k, v in CONTROLLERS.items()}
    params  = {k: parametros_del_animator(v) for k, v in CONTROLLERS.items()}

    _, fases, _ = p3.parsea()
    problemas = []
    n_beats = 0

    def revisa(b, donde):
        nonlocal n_beats
        n_beats += 1
        conocidos = campos.get(b.tipo)
        if conocidos is None:
            problemas.append(f"{donde}: clase desconocida '{b.tipo}'")
        else:
            for k, _ in b.campos:
                if k not in conocidos:
                    problemas.append(f"{donde}: {b.tipo} no tiene el campo '{k}'")
        for campo in ("markName", "faceTowardsMark", "objetivoMarca"):
            v = (b.get(campo) or '""').strip('"')
            if v and v not in marcas:
                problemas.append(f"{donde}: {b.tipo}.{campo} apunta a '{v}', que no existe")
        lista = b.get("markNames")
        if lista:
            for v in re.findall(r'"([^"]+)"', lista):
                if v not in marcas:
                    problemas.append(f"{donde}: WalkPathBeat pasa por '{v}', que no existe")
        # Todo campo que lleve el NOMBRE DE UN ESTADO del Animator. No solo `gesture`:
        # PoseBeat y SaltoBeat traen los suyos, y un nombre mal escrito ahi no da error
        # -- el personaje simplemente no hace la animacion, en silencio.
        for campo_pose in ("gesture", "pose", "poseSubida", "poseAire", "poseCaida"):
          g = (b.get(campo_pose) or '""').strip('"')
          if g:
              actor = (b.get("actorId") or '""').strip('"')
              cual  = "Invector@BasicLocomotion" if actor == ID_JUGADOR else "NPC_NoWeapon"
              otro  = "NPC_NoWeapon" if cual != "NPC_NoWeapon" else "Invector@BasicLocomotion"
              if g not in estados[cual]:
                  if g in params[cual]:
                      pista = f" -- OJO: '{g}' es un PARAMETRO de ese controller, no un estado (INC-264)"
                  elif g in estados[otro]:
                      pista = (f" -- existe, pero en {otro} (INC-214): o el actor esta equivocado, "
                               "o hay que dar de alta ese estado aqui tambien")
                  else:
                      pista = ""
                  problemas.append(f"{donde}: gesto '{g}' no existe en {cual}{pista}")
        for h in (b.hijos or []):
            revisa(h, donde)

    for f in fases:
        for i, b in enumerate(f.beats):
            revisa(b, f"{f.nombre} #{i}")

    if problemas:
        print(f"{len(problemas)} problema(s):")
        for p in problemas: print("  -", p)
        return 1
    print(f"OK - {len(fases)} fases, {n_beats} beats (contando los hijos). "
          "Campos, marcas y gestos: todo existe.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
