"""Deja la lista de doblaje de Cap. 3 y 4 solo con las frases que siguen en los guiones.

Las de «Los Pájaros» y «Las Jaulas» (Cap3_05 y Cap3_06) se quedaron sin cinemática el 14 nov.
Se guardan aparte, en Versiones antiguas, para no doblarlas por error.
"""
import csv
import os

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LISTA = os.path.join(RAIZ, "docs", "doblaje-sendero", "lineas_cap3_cap4.csv")
GUIONES = os.path.join(RAIZ, "Assets", "_SEQUENCES", "Guiones")
FUERA = os.path.join(RAIZ, "Versiones antiguas",
                     "Lineas de doblaje quitadas (Cap3 pajaros y jaulas, 2026-11-15)")
retiradas = os.path.join(FUERA, "lineas_cap3_cap4_quitadas.csv")

texto = ""
for nombre in sorted(os.listdir(GUIONES)):
    if nombre.endswith(".guion.txt"):
        with open(os.path.join(GUIONES, nombre), encoding="utf-8-sig") as f:
            texto += f.read() + "\n"

with open(LISTA, encoding="utf-8-sig", newline="") as f:
    lector = csv.DictReader(f)
    campos = lector.fieldnames
    filas = list(lector)

dentro = [r for r in filas if r["clave"] in texto]
fuera = [r for r in filas if r["clave"] not in texto]

os.makedirs(FUERA, exist_ok=True)
with open(retiradas, "w", encoding="utf-8", newline="") as f:
    w = csv.DictWriter(f, fieldnames=campos)
    w.writeheader()
    w.writerows(fuera)
with open(LISTA, "w", encoding="utf-8", newline="") as f:
    w = csv.DictWriter(f, fieldnames=campos)
    w.writeheader()
    w.writerows(dentro)

print(f"{len(dentro)} frases en guiones; {len(fuera)} retiradas a {os.path.relpath(retiradas, RAIZ)}")
