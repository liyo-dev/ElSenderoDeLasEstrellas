# -*- coding: utf-8 -*-
"""Pasada en seco de la secuencia, igual que hace SequenceShotCapture pero sin Unity: recorre los
beats, coloca a los actores en sus marcas y resuelve cada plano con el port de ShotComposer. Dice
dónde cae la cámara, hacia dónde mira y QUÉ HAY DETRÁS. Sirve para componer el decorado sin gastar
una captura en cada intento — y para dibujar el plano del plató (plano_svg.py)."""
import os, re, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unitylib as u, layout as L
from shotmath import Actor, resolver, largo, sub, norm

ASSET = os.path.join(u.RAIZ, "Assets/_SEQUENCES/SEQ_Prologo_UltimaNoche.asset")
TIPOS = {0: "Wide", 1: "TwoShot", 2: "OTS", 3: "Medium", 4: "CloseUp", 5: "Reaction", 6: "Tracking"}
MARCAS = {n: (x, L.S, z, r) for n, x, z, r in L.MARCAS}

def _props():
    d = {}
    for pid, obj, alt in [("PROP_Horno", "Horno_Mostrador", 1.10), ("PROP_Carreta", "Carreta", 0.90),
                          ("PROP_Globo", "Globo", 1.20), ("PROP_Campanario", "Iglesia", 7.0),
                          ("PROP_Mesa", "Mesa", 0.95)]:
        for g, pref, nombre, x, y, z, rot, esc in L.D:
            if nombre == obj:
                d[pid] = Actor(pid, (x, y, z), eye=alt, radio=1.0)
    d["PROP_Viga"] = Actor("PROP_Viga", L.MIRA_VIGA, eye=0.0, radio=1.2)
    d["PROP_Incendio"] = Actor("PROP_Incendio", (6002.6, L.S, 6005.2), eye=2.5, radio=3.0)
    return d

# decorado que cuenta para decir "qué se ve" (la hierba y el camino no son fondo)
DECORADO = [(n, (x, y, z)) for g, pref, n, x, y, z, rot, esc in L.D
            if not n.startswith(("Hierba", "Flores", "Camino", "Ribera")) and g != "08_Incendio"]

def _fondo(cam, yaw, fov, aspect=16/9):
    hh = math.degrees(math.atan(math.tan(math.radians(fov) * 0.5) * aspect))
    v = []
    for nombre, pos in DECORADO:
        d = math.hypot(pos[0] - cam[0], pos[2] - cam[2])
        if d > 60 or d < 1.0: continue
        b = math.degrees(math.atan2(pos[0] - cam[0], pos[2] - cam[2])) % 360
        if abs((b - yaw + 540) % 360 - 180) <= hh: v.append((d, nombre))
    v.sort()
    return v

def recorrido(verboso=True):
    actores = {"NPC_Archimago": Actor("NPC_Archimago", (6006.2, L.S, 6001.4), eye=0.95),
               "NPC_Liora": Actor("NPC_Liora", (6012.5, L.S, 6003.7), eye=0.95),
               "NPC_MagoOscuro": Actor("NPC_MagoOscuro", (6000, L.S, 6068), eye=0.95)}
    props = _props()
    def actor(i): return actores.get(i) or props.get(i)

    t = open(ASSET, encoding="utf-8").read()
    fase_de = {}
    for m in re.finditer(r"  - name: (.*?)\n    onlyIfFlag:.*\n    skipIfFlag:.*\n    beats:\n((?:    - rid: \d+\n)+)", t):
        for r in re.findall(r"- rid: (\d+)", m.group(2)): fase_de[r] = m.group(1)

    partes = re.split(r"    - rid: (\d+)\n      type: \{class: (\w+), ns: , asm: [\w-]+\}\n      data:\n", t)
    lado = (1, 0, 0)
    planos, n, anterior = [], 0, None

    if verboso:
        print("%-3s %-22s %-9s %-26s %s" % ("#", "fase", "tipo", "cámara", "qué hay en el fondo"))

    for i in range(1, len(partes), 3):
        rid, cls, cuerpo = partes[i], partes[i + 1], partes[i + 2]
        fase = fase_de.get(rid, "?")
        def campo(k, d=""):
            m = re.search(r"^\s+%s: (.*)$" % re.escape(k), cuerpo, flags=re.M)
            return m.group(1).strip() if m else d

        if cls == "SetActionAxisBeat":
            g = math.radians(float(campo("sideDegrees")))
            lado = (math.sin(g), 0, math.cos(g))

        elif cls == "PlaceAtMarkBeat":
            a, mk = actor(campo("actorId")), MARCAS.get(campo("markName"))
            if a and mk:
                a.pos = (mk[0], mk[1], mk[2])
                hacia = actor(campo("faceTowardsActor"))
                if hacia:
                    a.fwd = norm((hacia.pos[0] - a.pos[0], 0, hacia.pos[2] - a.pos[2]))
                else:
                    r = math.radians(mk[3]); a.fwd = (math.sin(r), 0, math.cos(r))

        elif cls == "ShotBeat":
            tipo = TIPOS[int(re.search(r"type: (\d+)", cuerpo).group(1))]
            suj, sec = actor(campo("subjectId")), actor(campo("secondaryId"))
            if suj is None:
                if verboso: print("  !! plano sin sujeto:", campo("subjectId"))
                continue
            pos, yaw, pitch, fov, mirar, _ = resolver(
                tipo, suj, sec, lado_fijado=lado,
                escala_dist=float(campo("distanceScale") or 1),
                alto_camara=float(campo("heightBias") or 0))
            n += 1
            yaw %= 360
            planos.append((n, fase, tipo, pos, yaw, fov))
            if verboso:
                v = _fondo(pos, yaw, fov)
                detras = [x for x in v if x[0] > largo(sub(mirar, pos))][:4]
                aviso = ""
                if anterior:
                    dif = abs((yaw - anterior[0] + 540) % 360 - 180)
                    if dif < 30 and 0.75 < fov / anterior[1] < 1.33: aviso = "  ⚠ corte parecido al anterior"
                cerca = [x for x in v if x[0] < 2.0]
                if cerca: aviso += "  ⚠ cerca de la cámara: " + cerca[0][1]
                print("%-3d %-22s %-9s (%7.1f,%5.1f,%7.1f) %3.0f° │ %s%s" % (
                    n, fase[:22], tipo, pos[0], pos[1], pos[2], yaw,
                    ", ".join("%s(%.0fm)" % (x[1], x[0]) for x in detras) or "— nada —", aviso))
            anterior = (yaw, fov)

    return planos

if __name__ == "__main__":
    recorrido()
