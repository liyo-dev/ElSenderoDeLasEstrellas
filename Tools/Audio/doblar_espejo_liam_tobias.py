"""Dobla el espejo de Liam y Tobías (Cap. 3, Cap3_01_ElEspejo, INC-1008).

Liam y Tobías con las voces y ajustes de redoblar_final.py (las de la cinemática final).

Uso (desde la raíz del proyecto):
    python Tools/Audio/doblar_espejo_liam_tobias.py             # una toma por frase, que se coloca
    python Tools/Audio/doblar_espejo_liam_tobias.py --solo CAP3_ESPEJO_08 --tomas 3

Todas las tomas quedan en «Versiones antiguas/Voces espejo de Liam y Tobías raw (2026-11-08)/».
"""
import argparse
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from redoblar_final import AJUSTES_LIAM, AJUSTES_TOBIAS, NORMALIZADOR, RAIZ, VOCES, VOZ_LIAM, VOZ_TOBIAS, clave_de_api, generar  # noqa: E402

TOMAS = os.path.join(RAIZ, "Versiones antiguas", "Voces espejo de Liam y Tobías raw (2026-11-08)")
L_ = (VOZ_LIAM, AJUSTES_LIAM)
T_ = (VOZ_TOBIAS, AJUSTES_TOBIAS)

LINEAS = [
    {"clave": "CAP3_ESPEJO_01", "quien": T_, "texto": "[proudly] Hoy me he tomado media sopa."},
    {"clave": "CAP3_ESPEJO_02", "quien": L_, "texto": "[softly, teasing] ¿Y la otra media?"},
    {"clave": "CAP3_ESPEJO_03", "quien": T_, "texto": "[giggles] La otra media ha ganado."},
    {"clave": "CAP3_ESPEJO_04", "quien": L_, "texto": "[worried, softly] Tobi... esa tos."},
    {"clave": "CAP3_ESPEJO_05", "quien": L_, "texto": "[warmly, determined] Cuando vuelva iremos a pescar. Te lo prometo."},
    {"clave": "CAP3_ESPEJO_06", "quien": T_, "texto": "[teasing] No sabes pescar."},
    {"clave": "CAP3_ESPEJO_07", "quien": L_, "texto": "[laughs softly] Aprenderé. Dicen que el truco es tener paciencia."},
    {"clave": "CAP3_ESPEJO_08", "quien": T_, "texto": "[excited, then thoughtful] Te he hecho un dibujo. Somos nosotros pescando. Al cuarto no le he puesto cara: todavía no sé quién es."},
    {"clave": "CAP3_ESPEJO_09", "quien": L_, "texto": "[sad, gently] Ya lo sabrás."},
    {"clave": "CAP3_ESPEJO_10", "quien": L_, "texto": "[determined, quietly] La llave del aro. Se calentará cada vez que el chico esté cerca."},
    {"clave": "CAP3_ESPEJO_11", "quien": L_, "texto": "[sad, whispering] Aguanta un poco más, Tobi."},
]


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--tomas", type=int, default=1, help="tomas por frase (la 1 se coloca)")
    p.add_argument("--solo", nargs="*", default=None, help="claves a doblar (por defecto, todas)")
    args = p.parse_args()

    os.makedirs(TOMAS, exist_ok=True)
    clave_api = clave_de_api()
    colocadas = []
    for linea in LINEAS:
        clave = linea["clave"]
        if args.solo and clave not in args.solo:
            continue
        print(f"\n{clave}")
        for n in range(1, max(1, args.tomas) + 1):
            audio = generar(clave_api, linea["quien"][0], linea["texto"], linea["quien"][1])
            if audio is None:
                print(f"  toma {n}: FALLÓ")
                continue
            with open(os.path.join(TOMAS, f"{clave}_toma{n}.mp3"), "wb") as f:
                f.write(audio)
            print(f"  toma {n}: {len(audio) // 1024} KB")
            if n == 1:
                colocadas.append(clave)
            time.sleep(1.0)
    if not colocadas:
        print("\nNo se ha colocado ninguna toma.")
        return
    # Mismo estándar que el resto de voces: -18 LUFS, techo -2 dBTP.
    entorno = dict(os.environ)
    if "FFMPEG" not in entorno and shutil.which("ffmpeg") and os.name != "nt":
        entorno["FFMPEG"] = shutil.which("ffmpeg")
    subprocess.call([sys.executable, NORMALIZADOR, "--aplicar", "--carpeta",
                     os.path.relpath(TOMAS, RAIZ), "--objetivo", "-18", "--techo", "-2"], cwd=RAIZ, env=entorno)
    for clave in colocadas:
        shutil.copy2(os.path.join(TOMAS, f"{clave}_toma1.mp3"), os.path.join(VOCES, clave + ".mp3"))
    print(f"\nListo: {len(colocadas)} voces colocadas. Vuelve a Unity para que las reimporte.")


if __name__ == "__main__":
    main()
