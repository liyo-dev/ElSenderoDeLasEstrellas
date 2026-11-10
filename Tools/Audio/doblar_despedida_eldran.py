"""Dobla la despedida de Eldran en la salida del pueblo (Cap. 3, Cap3_00_LaDespedida, INC-995).

Eldran con la voz y los ajustes de doblar_regalo_eldran.py; Will con los de redoblar_final.py.

Uso (desde la raíz del proyecto):
    python Tools/Audio/doblar_despedida_eldran.py             # dos tomas por frase; la 1 se coloca
    python Tools/Audio/doblar_despedida_eldran.py --solo CAP3_DESPEDIDA_04 --tomas 3

Todas las tomas quedan en «Versiones antiguas/Voces despedida de Eldran raw (2026-11-08)/».
"""
import argparse
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from redoblar_final import AJUSTES_WILL, NORMALIZADOR, RAIZ, VOCES, VOZ_WILL, clave_de_api, generar  # noqa: E402
from doblar_regalo_eldran import AJUSTES_ELDRAN, VOZ_ELDRAN  # noqa: E402

TOMAS = os.path.join(RAIZ, "Versiones antiguas", "Voces despedida de Eldran raw (2026-11-08)")
E_ = (VOZ_ELDRAN, AJUSTES_ELDRAN)
W_ = (VOZ_WILL, AJUSTES_WILL)

LINEAS = [
    {"clave": "CAP3_DESPEDIDA_01", "quien": E_, "texto": "[warmly] Ya estás aquí. ¿Te has despedido de todos?"},
    {"clave": "CAP3_DESPEDIDA_02", "quien": W_, "texto": "[amused] De casi todos. Tomasa quería venderme otra cosa inútil."},
    {"clave": "CAP3_DESPEDIDA_03", "quien": E_, "texto": "[chuckles] Esa mujer no descansa. [serious] Escucha, que esto es importante."},
    {"clave": "CAP3_DESPEDIDA_04", "quien": E_, "texto": "[serious] Estela no acepta aprendices. Si te presentas con un trozo de hierro y buena cara, te cerrará la puerta."},
    {"clave": "CAP3_DESPEDIDA_05", "quien": E_, "texto": "[thoughtful] Pero en la aldea del mar han visto más animales con aros como ese. Ve allí primero, pregunta y apúntalo todo."},
    {"clave": "CAP3_DESPEDIDA_06", "quien": E_, "texto": "[firmly] Llévale algo que no pueda ignorar. Así te escuchará."},
    {"clave": "CAP3_DESPEDIDA_07", "quien": W_, "texto": "[worried] ¿Y tú?"},
    {"clave": "CAP3_DESPEDIDA_08", "quien": E_, "texto": "[warmly] Yo me voy al Reino, a contarle al Rey lo de los aros. Cuando tengas algo, búscame allí."},
    {"clave": "CAP3_DESPEDIDA_09", "quien": W_, "texto": "[thoughtful] Entonces... no es una despedida."},
    {"clave": "CAP3_DESPEDIDA_10", "quien": E_, "texto": "[tender] Claro que no. Es un «hasta pronto». [playful] Anda, tira, que se te va el día."},
    {"clave": "CAP3_DESPEDIDA_11", "quien": W_, "texto": "[shouts happily] ¡Hasta pronto, Eldran!"},
]


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--tomas", type=int, default=2, help="tomas por frase (la 1 se coloca)")
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
