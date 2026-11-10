"""Vuelve a doblar las frases de Eldran que dicen «Will» (INC-945).

Dos arreglos a la vez:
  · «Will» a la española: el texto que va a la voz lo escribe «Güil» y la petición fuerza el español
    (language_code "es"); el subtítulo sigue diciendo «Will».
  · Menos «Will» seguidos del bosque al demonio: las frases que no llaman de verdad a Will lo pierden
    (el subtítulo se cambia a la vez en Localization/*_es.json).

Voz y ajustes de Eldran de siempre (registro_doblaje.csv): Eldran - Anime candidata 2, estab 34,
simil 71, velocidad 0.92, Eleven v4.

Uso (desde la raíz del proyecto):
    python Tools/Audio/doblar_eldran_will.py             # dos tomas por frase; la 1 se coloca
    python Tools/Audio/doblar_eldran_will.py --solo EVT_08 --tomas 3

El audio que había se guarda en «Versiones antiguas/Voces Eldran antes de Güil (2026-11-08)/» y
todas las tomas en «Versiones antiguas/Voces Eldran Güil raw (2026-11-08)/» para cambiarlas a mano.
"""
import argparse
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from redoblar_final import NORMALIZADOR, RAIZ, VOCES, clave_de_api, generar  # noqa: E402

ORIGINALES = os.path.join(RAIZ, "Versiones antiguas", "Voces Eldran antes de Güil (2026-11-08)")
TOMAS = os.path.join(RAIZ, "Versiones antiguas", "Voces Eldran Güil raw (2026-11-08)")

VOZ_ELDRAN = "RL0oxYTUxBdVxuByUCtW"  # Eldran - Anime candidata 2
AJUSTES_ELDRAN = {"stability": 0.34, "similarity_boost": 0.71, "style": 0.0,
                  "use_speaker_boost": True, "speed": 0.92}

LINEAS = [
    # ── Siguen llamándole: solo cambia la pronunciación ──
    {"clave": "DLG_ELDRAN_CAJA_LLAMADA", "texto": "[shouts] ¡Güil! ¡Espera!"},
    {"clave": "EVT_AWAKEN_01", "texto": "[shouts] ¡Güil, cuidado!"},
    {"clave": "DLG_ELDRAN_WILL_MISSION1_INPROGRESS_01",
     "texto": "¿Todavía no te la has gastado? Una tontería, Güil, lo que tú quieras."},
    {"clave": "PERAS_05", "texto": "[laughs] ¡Güil! Ya ves, perdiendo cajas a mi edad. Ya aparecerá."},
    {"clave": "EVT_ELDRAN_CHEER_01", "texto": "[excited] ¡Así se hace, Güil! ¡Lo tienes!"},
    {"clave": "DLG_ELDRAN_MISSION3_INPROGRESS_01",
     "texto": "[shouts] ¡Ahora no, Güil! ¡Atento al aro de su cuello!"},
    # ── Pierden el nombre ──
    {"clave": "EVT_ELDRAN_GUIA_LLAMADA", "texto": "[shouts] ¡Por aquí!"},
    {"clave": "DLG_ELDRAN_SAVEPOINT_01", "texto": "Mira, esto es un Punto de Guardado."},
    {"clave": "DLG_ELDRAN_SIGUEME_INPROGRESS_01", "texto": "No te quedes atrás. Ya casi estamos."},
    {"clave": "EVT_AWAKEN_06", "texto": "[shouts] ¡Apártate!"},
    {"clave": "EVT_08", "texto": "[shouts] ¡Usa tu magia y derrota al demonio!"},
    {"clave": "EVT_ELDRAN_AHORA_01", "texto": "[shouts] ¡Ahora! ¡El aro brilla!"},
    {"clave": "EVT_ELDRAN_CASI", "texto": "[excited] ¡Ya casi es tuyo! ¡Un último esfuerzo, muchacho!"},
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
            audio = generar(clave_api, VOZ_ELDRAN, linea["texto"], AJUSTES_ELDRAN)
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
