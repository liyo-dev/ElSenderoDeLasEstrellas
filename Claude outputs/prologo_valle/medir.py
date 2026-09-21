# -*- coding: utf-8 -*-
"""Mide una lista de prefabs del pack buscando su FBX del mismo nombre."""
import sys, os, json
sys.path.insert(0,'.')
import unitylib as u, fbxsize

CARPETAS = [
    os.path.join(u.RAIZ, "Assets/Art/World/Fantasy_Kingdom_Pack/Meshes"),
]
# índice de FBX disponibles (minúsculas -> ruta)
INDICE = {}
for c in CARPETAS:
    for f in os.listdir(c):
        if f.lower().endswith((".fbx",)) and not f.endswith(".meta"):
            INDICE[os.path.splitext(f)[0].lower()] = os.path.join(c, f)

import re
def medir(nombre):
    base = os.path.splitext(nombre)[0].lower()
    candidatos = [base, re.sub(r"_[a-z]\d+$", "", base), re.sub(r"_[a-z]\d+$", "_a01", base),
                  re.sub(r"\d+$", "", base)]
    ruta = None
    for c in candidatos:
        if c in INDICE: ruta = INDICE[c]; break
    if not ruta: return None
    r = fbxsize.medir_fbx(ruta)
    if not r: return None
    mn, mx = r
    k = fbxsize.factor_escala(ruta)
    return dict(x=round((mx[0]-mn[0])*k,2), y=round((mx[1]-mn[1])*k,2), z=round((mx[2]-mn[2])*k,2),
                base=round(mn[1]*k,2))

if __name__ == "__main__":
    salida = {}
    for n in sys.argv[1:]:
        m = medir(n)
        salida[n] = m
        print("%-26s %s" % (n, m if m else "— sin FBX propio (prefab compuesto)"))
    json.dump(salida, open("medidas.json","w"), indent=1)
