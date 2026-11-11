"""Mide las dependencias entre carpetas de código (INC-1049, TDD.md § 31).

Uso: python3 Tools/Kits/dependencias_kits.py [raíz del proyecto]

Por cada módulo (Kits/<Kit>, ElSendero/<x>, Scripts/<carpeta>, NarrativeGraph/<x>, Editor)
lista los tipos de otros módulos que usa, los ciclos entre módulos y, para Assets/Kits,
cualquier uso de tipos que no sean de un kit (regla 8.9 de CLAUDE.md).
Es una medida por nombres de tipo: quita comentarios y textos, y descarta nombres
demasiado comunes. Sirve para orientar; manda el compilador con los asmdef.
"""
import collections, os, re, sys

raiz = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
assets = os.path.join(raiz, "Assets")
BASES = ["Kits", "ElSendero", "Scripts", "NarrativeGraph", "Editor"]
COMUNES = {"Mode", "Entry", "Result", "Field", "Host", "Config", "Phase", "Estado", "Request",
           "Copia", "Runner", "State", "Data", "Settings", "Type", "Kind", "Item", "Info", "Step", "Slot"}
QUITAR = re.compile(r'//[^\n]*|/\*.*?\*/|@?"(?:\\.|[^"\\])*"', re.S)
DECL = re.compile(r'\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)')
IDENT = re.compile(r'\b[A-Za-z_]\w*\b')

def modulo(rel):
    p = rel.replace("\\", "/").split("/")
    if p[0] == "Editor":
        return "Editor"
    return p[0] + "/" + (p[1] if len(p) > 2 else "(raíz)")

codigo = {}
for base in BASES:
    for dp, _, fs in os.walk(os.path.join(assets, base)):
        if "~" in dp:
            continue
        for f in fs:
            if f.endswith(".cs"):
                ruta = os.path.join(dp, f)
                with open(ruta, encoding="utf-8", errors="ignore") as h:
                    codigo[os.path.relpath(ruta, assets)] = QUITAR.sub(" ", h.read())

dueno = {}
for rel, c in codigo.items():
    for n in DECL.findall(c):
        dueno.setdefault(n, modulo(rel))

usa = collections.defaultdict(lambda: collections.defaultdict(set))
lineas = collections.Counter()
for rel, c in codigo.items():
    m = modulo(rel)
    lineas[m] += c.count("\n")
    for i in set(IDENT.findall(c)):
        o = dueno.get(i)
        if o and o != m and i not in COMUNES:
            usa[m][o].add(i)

print(f"{len(codigo)} archivos .cs\n")
for m in sorted(lineas, key=lambda x: -lineas[x]):
    total = sum(len(s) for s in usa[m].values())
    top = ", ".join(f"{o}:{len(s)}" for o, s in sorted(usa[m].items(), key=lambda x: -len(x[1]))[:6])
    print(f"{m:34} {lineas[m]:7} líneas  {total:4} tipos de fuera | {top}")

# Ciclos (aristas con 2 o más tipos), sin contar herramientas de editor
grafo = {m: {o for o, s in d.items() if len(s) >= 2 and "Editor" not in o}
         for m, d in usa.items() if "Editor" not in m}
indice, bajo, pila, en_pila, comps, cont = {}, {}, [], set(), [], [0]
sys.setrecursionlimit(10000)
def tarjan(v):
    indice[v] = bajo[v] = cont[0]; cont[0] += 1; pila.append(v); en_pila.add(v)
    for w in grafo.get(v, ()):
        if w not in indice:
            tarjan(w); bajo[v] = min(bajo[v], bajo[w])
        elif w in en_pila:
            bajo[v] = min(bajo[v], indice[w])
    if bajo[v] == indice[v]:
        comp = []
        while True:
            w = pila.pop(); en_pila.discard(w); comp.append(w)
            if w == v:
                break
        comps.append(comp)
for v in list(grafo):
    if v not in indice:
        tarjan(v)
print("\nCiclos:")
for c in comps:
    if len(c) > 1:
        print(f"  {len(c)} módulos: {', '.join(sorted(c))}")

print("\nKits que usan algo que no es un kit (debe estar vacío):")
malos = 0
for m, d in usa.items():
    if m.startswith("Kits/"):
        for o, s in d.items():
            if not o.startswith("Kits/"):
                malos += 1
                print(f"  {m} -> {o}: {', '.join(sorted(s))[:300]}")
print("  ninguno" if not malos else "")
