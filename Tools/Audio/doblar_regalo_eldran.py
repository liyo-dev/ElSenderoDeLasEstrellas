"""Dobla la charla del regalo de Eldran en sus ocho frases nuevas (INC-946).

Eldran con su voz y ajustes de INC-945 («Will» se escribe «Güil» para la voz; el subtítulo dice
«Will»); Will con la voz y ajustes de redoblar_final.py.

Uso (desde la raíz del proyecto):
    python Tools/Audio/doblar_regalo_eldran.py             # dos tomas por frase; la 1 se coloca
    python Tools/Audio/doblar_regalo_eldran.py --solo DLG_ELDRAN_REGALO_03 --tomas 3

El audio que había se guarda en «Versiones antiguas/Voces regalo de Eldran antes (2026-11-08)/» y
todas las tomas en «Versiones antiguas/Voces regalo de Eldran raw (2026-11-08)/».
"""
import argparse
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from redoblar_final import AJUSTES_WILL, NORMALIZADOR, RAIZ, VOCES, VOZ_WILL, clave_de_api, generar  # noqa: E402

ORIGINALES = os.path.join(RAIZ, "Versiones antiguas", "Voces regalo de Eldran antes (2026-11-08)")
TOMAS = os.path.join(RAIZ, "Versiones antiguas", "Voces regalo de Eldran raw (2026-11-08)")

VOZ_ELDRAN = "RL0oxYTUxBdVxuByUCtW"  # Eldran - Anime candidata 2
AJUSTES_ELDRAN = {"stability": 0.34, "similarity_boost": 0.71, "style": 0.0,
                  "use_speaker_boost": True, "speed": 0.92}
E_ = (VOZ_ELDRAN, AJUSTES_ELDRAN)
W_ = (VOZ_WILL, AJUSTES_WILL)

LINEAS = [
    {"clave": "DLG_ELDRAN_REGALO_01", "quien": E_,
     "texto": "[warmly] Ya mismo es tu cumpleaños. ¿Qué quieres que te regale?"},
    {"clave": "DLG_ELDRAN_REGALO_02", "quien": W_,
     "texto": "[shy] Nada, de verdad. No necesito nada."},
    {"clave": "DLG_ELDRAN_REGALO_03", "quien": E_,
     "texto": "[amused sigh] Ya sabía yo que dirías eso. [gently] A ver, Güil, tienes que pensar un poco en ti. "
              "Te pasas el día ayudando a los demás y apenas te queda tiempo para hacer algo que te guste."},
    {"clave": "DLG_ELDRAN_REGALO_04", "quien": W_,
     "texto": "[softly] Bueno... pero es que me gusta ayudar a la gente."},
    {"clave": "DLG_ELDRAN_REGALO_05", "quien": E_,
     "texto": "[tender] Lo sé, y eso es lo que más me gusta de ti. [warmly] Pero toma, esta moneda es mi regalo "
              "por adelantado. [playful] Y también un ejercicio."},
    {"clave": "DLG_ELDRAN_REGALO_06", "quien": W_,
     "texto": "[curious] ¿Un ejercicio?"},
    {"clave": "DLG_ELDRAN_REGALO_07", "quien": E_,
     "texto": "[playful] Quiero que te compres algo que no sirva para nada. Para nada, ¿eh? "
              "[chuckles] Que ya te veo gastándotela en algo para ayudar a alguien."},
    {"clave": "DLG_ELDRAN_REGALO_08", "quien": W_,
     "texto": "[laughs softly] Vale, vale... algo que no sirva para nada. A ver qué encuentro."},
]


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--tomas", type=int, default=2, help="tomas por frase (la 1 se coloca)")
    p.add_argument("--solo", nargs="*", default=None, help="claves a doblar (por defecto, todas)")
    args = p.parse_args()

    os.makedirs(ORIGINALES, exist_ok=True)
    os.makedirs(TOMAS, exist_ok=True)
    clave_api = clave_de_api()

    colocadas = []
    for linea in LINEAS:
        clave = linea["clave"]
        if args.solo and clave not in args.solo:
            continue
        print(f"\n{clave}")
        destino = os.path.join(VOCES, clave + ".mp3")
        copia = os.path.join(ORIGINALES, clave + ".mp3")
        if os.path.exists(destino) and not os.path.exists(copia):
            shutil.copy2(destino, copia)
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
