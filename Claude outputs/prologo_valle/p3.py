# -*- coding: utf-8 -*-
"""
p3.py -- lee y reescribe ConstruirPrologoUltimaNoche.cs.

Por que existe
--------------
El montaje del prologo son ~4.000 lineas de C# muy repetitivo: cada beat es un
inicializador de objeto con sus campos. Editarlo a mano (o con regex) es como se
cuelan los fallos que luego solo se ven grabando. Aqui el archivo se PARSEA a una
estructura de Python, se toca esa estructura, y se vuelve a EMITIR.

La red de seguridad es la ida y vuelta: parsear y emitir sin tocar nada tiene que
dar el archivo byte a byte identico. Si no lo da, el parser no ha entendido algo y
no se escribe nada. `python3 p3.py --check` hace exactamente eso.

Estructura
----------
    fases = [Fase(nombre, only, skip, beats)]
    beat  = Beat(tipo, [(campo, valor_literal_en_C#), ...], hijos)

Los valores se guardan como el TEXTO del C# tal cual ("1.4f", '"NPC_Liora"',
"ShotType.Wide", 'Prefab("895c...")'). Asi no hay conversiones que puedan perder
precision ni formatos que cambien solos; lo que no se toca sale igual que entro.
"""

import re
import sys
import os

RUTA_CS = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "Assets", "Editor", "ConstruirPrologoUltimaNoche.cs")

INICIO = "    private static List<SequencePhase> Fases() => new()\n    {\n"
FIN = "\n    };\n"


class Beat:
    def __init__(self, tipo, campos, hijos=None, envoltura=None):
        self.tipo = tipo              # "ShotBeat", "ParallelBeat", ...
        self.campos = campos          # [(nombre, texto_del_valor)]
        self.hijos = hijos            # lista de Beat, solo en ParallelBeat
        self.envoltura = envoltura    # ("ConModo", ", PanicInputMode.Mash") o None

    def get(self, nombre, por_defecto=None):
        for k, v in self.campos:
            if k == nombre:
                return v
        return por_defecto

    def set(self, nombre, valor):
        for i, (k, _) in enumerate(self.campos):
            if k == nombre:
                self.campos[i] = (nombre, valor)
                return
        self.campos.append((nombre, valor))

    def framing(self, nombre, por_defecto=None):
        """Lee un campo de dentro del ShotFraming, que va anidado en 'framing'."""
        f = self.get("framing")
        if f is None:
            return por_defecto
        m = re.search(r"\b" + re.escape(nombre) + r" = ([^,\n]+),", f)
        return m.group(1) if m else por_defecto

    def set_framing(self, nombre, valor):
        """
        Cambia un campo del ShotFraming. Si no existe, lo ANADE.

        Lo de anadir hace falta desde que ShotFraming tiene campos nuevos (`encara`):
        los encuadres que vienen de base_construir.cs no los mencionan, porque se
        escribieron antes de que existieran, y heredan el valor por defecto de C#.
        Cambiar uno de esos es legitimo, asi que se anade la linea respetando la
        sangria de las que ya hay.
        """
        f = self.get("framing")
        if f is None:
            raise KeyError("este beat no tiene framing")

        nuevo, n = re.subn(r"(\b" + re.escape(nombre) + r" = )[^,\n]+,",
                           lambda m: m.group(1) + valor + ",", f)
        if n == 1:
            self.set("framing", nuevo)
            return
        if n > 1:
            raise KeyError(f"'{nombre}' aparece {n} veces en el framing")

        # No estaba: se anade delante de la llave de cierre, con su misma sangria
        # interior (la de la ultima linea de campo).
        lineas = f.rstrip().split("\n")
        if not lineas[-1].strip().startswith("}"):
            raise KeyError(f"no se de donde colgar '{nombre}': el framing no acaba en '}}'")
        sangria = None
        for l in reversed(lineas[:-1]):
            if " = " in l:
                sangria = l[:len(l) - len(l.lstrip())]
                break
        if sangria is None:
            raise KeyError(f"el framing no tiene ningun campo del que copiar la sangria")
        lineas.insert(len(lineas) - 1, f"{sangria}{nombre} = {valor},")
        self.set("framing", "\n".join(lineas))

    def __repr__(self):
        return f"<{self.tipo} {self.get('note', '')[:40]}>"


class Fase:
    def __init__(self, nombre, only, skip, beats):
        self.nombre = nombre
        self.only = only
        self.skip = skip
        self.beats = beats

    def __repr__(self):
        return f"<Fase {self.nombre} ({len(self.beats)} beats)>"


# -- Parser ------------------------------------------------------------------

class _Lector:
    def __init__(self, texto):
        self.t = texto
        self.i = 0

    def saltar_blancos(self):
        while self.i < len(self.t) and self.t[self.i] in " \t\r\n":
            self.i += 1

    def mira(self, s):
        self.saltar_blancos()
        return self.t.startswith(s, self.i)

    def come(self, s):
        self.saltar_blancos()
        if not self.t.startswith(s, self.i):
            ctx = self.t[max(0, self.i - 60):self.i + 60]
            raise SyntaxError(f"esperaba {s!r} en {self.i}:\n...{ctx}...")
        self.i += len(s)

    def cadena(self):
        """Lee un literal de cadena de C# y devuelve su texto con comillas."""
        self.saltar_blancos()
        if self.t[self.i] != '"':
            raise SyntaxError(f"esperaba una cadena en {self.i}")
        j = self.i + 1
        while True:
            if self.t[j] == '\\':
                j += 2
                continue
            if self.t[j] == '"':
                j += 1
                break
            j += 1
        s = self.t[self.i:j]
        self.i = j
        return s

    def valor(self):
        """
        Lee el valor de un campo: todo hasta la coma que lo cierra, respetando
        parentesis, llaves y cadenas.
        """
        self.saltar_blancos()
        ini = self.i
        prof = 0
        while self.i < len(self.t):
            c = self.t[self.i]
            if c == '"':
                self.cadena()
                continue
            if c in "({[":
                prof += 1
            elif c in ")}]":
                if prof == 0:
                    break
                prof -= 1
            elif c == ',' and prof == 0:
                break
            self.i += 1
        return self.t[ini:self.i].strip()


def _parsea_beat(r):
    r.saltar_blancos()
    envoltura = None
    if r.mira("ConModo("):
        r.come("ConModo(")
        envoltura = "ConModo"
    r.come("new ")
    m = re.match(r"[A-Za-z_][A-Za-z0-9_]*", r.t[r.i:])
    tipo = m.group(0)
    r.i += len(tipo)
    r.come("{")

    campos = []
    hijos = None
    while not r.mira("}"):
        m = re.match(r"\s*([A-Za-z_][A-Za-z0-9_]*)\s*=", r.t[r.i:])
        if not m:
            raise SyntaxError(f"esperaba un campo en {r.i}: ...{r.t[r.i:r.i+80]}...")
        nombre = m.group(1)
        r.i += m.end()
        if nombre == "beats":
            r.come("new List<SequenceBeat>")
            r.come("{")
            hijos = []
            while not r.mira("}"):
                hijos.append(_parsea_beat(r))
                r.come(",")
            r.come("}")
        else:
            campos.append((nombre, r.valor()))
        r.come(",")
    r.come("}")

    cola = None
    if envoltura == "ConModo":
        r.come(",")
        # El argumento del modo puede llevar sus propios parentesis --
        # "(PanicInputMode)4" es un cast -- asi que se cuentan, no se busca el
        # primer ')'.
        r.saltar_blancos()
        ini, prof = r.i, 0
        while r.i < len(r.t):
            c = r.t[r.i]
            if c == "(":
                prof += 1
            elif c == ")":
                if prof == 0:
                    break
                prof -= 1
            r.i += 1
        cola = ", " + r.t[ini:r.i].strip()
        r.come(")")
    return Beat(tipo, campos, hijos, (envoltura, cola) if envoltura else None)


def parsea(ruta=RUTA_CS):
    src = open(ruta, encoding="utf-8").read()
    a = src.index(INICIO) + len(INICIO)
    b = src.index(FIN, a)
    cuerpo = src[a:b]
    cabecera, pie = src[:a], src[b:]

    r = _Lector(cuerpo)
    fases = []
    while True:
        r.saltar_blancos()
        if r.i >= len(r.t):
            break
        r.come("Fase(")
        nombre = r.cadena()
        r.come(",")
        only = r.cadena()
        r.come(",")
        skip = r.cadena()
        r.come(",")
        r.come("new List<SequenceBeat>")
        r.come("{")
        beats = []
        while not r.mira("}"):
            beats.append(_parsea_beat(r))
            r.come(",")
        r.come("}")
        r.come(")")
        r.come(",")
        fases.append(Fase(nombre, only, skip, beats))
    return cabecera, fases, pie


# -- Emisor ------------------------------------------------------------------

def _emite_beat(b, ind):
    p = " " * ind
    out = []
    abre = f"{p}ConModo(new {b.tipo}" if b.envoltura else f"{p}new {b.tipo}"
    out.append(abre)
    out.append(f"{p}{{")
    for k, v in b.campos:
        if "\n" in v:
            # valor multilinea (el ShotFraming): ya viene con su sangria
            out.append(f"{p}    {k} = {v},")
        else:
            out.append(f"{p}    {k} = {v},")
        if k == "waitForAll" and b.hijos is not None:
            out.append(f"{p}    beats = new List<SequenceBeat>")
            out.append(f"{p}    {{")
            for h in b.hijos:
                out.append(_emite_beat(h, ind + 8) + ",")
            out.append(f"{p}    }},")
    cierre = f"{p}}}"
    if b.envoltura:
        cierre += b.envoltura[1] + ")"
    out.append(cierre)
    return "\n".join(out)


def emite(cabecera, fases, pie):
    out = [cabecera.rstrip("\n") + "\n"] if False else []
    cuerpo = []
    for f in fases:
        cuerpo.append(f"        Fase({f.nombre}, {f.only}, {f.skip}, new List<SequenceBeat>")
        cuerpo.append("        {")
        for b in f.beats:
            cuerpo.append(_emite_beat(b, 12) + ",")
        cuerpo.append("        }),")
    return cabecera + "\n".join(cuerpo) + pie


def comprueba_ida_y_vuelta(ruta=RUTA_CS):
    original = open(ruta, encoding="utf-8").read()
    cab, fases, pie = parsea(ruta)
    regenerado = emite(cab, fases, pie)
    if original == regenerado:
        total = sum(len(f.beats) for f in fases)
        print(f"OK - ida y vuelta identica. {len(fases)} fases, {total} beats de primer nivel.")
        return True
    # informe util del primer punto de divergencia
    for n, (x, y) in enumerate(zip(original.split("\n"), regenerado.split("\n")), 1):
        if x != y:
            print(f"DIFIERE en la linea {n}:\n  orig: {x!r}\n  gen : {y!r}")
            break
    else:
        print(f"Mismo contenido pero distinta longitud: {len(original)} vs {len(regenerado)}")
    return False


if __name__ == "__main__":
    if "--check" in sys.argv:
        sys.exit(0 if comprueba_ida_y_vuelta() else 1)
    cab, fases, pie = parsea()
    for f in fases:
        print(f"{f.nombre:38s} {len(f.beats):3d} beats")
