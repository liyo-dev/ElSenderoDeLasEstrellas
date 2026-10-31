"""Dobla el conjuro con el que Will abre el portal al Sendero en la Piedra Ancestral.

Uso (desde la raíz del proyecto):

    python Tools/Audio/doblar_conjuro_sendero.py            # tres tomas de cada frase; la 1 se coloca
    python Tools/Audio/doblar_conjuro_sendero.py --solo SENDERO_CONJURO_03

La toma 1 de cada frase va a Assets/Resources/Voices/es; todas las tomas, normalizadas con ffmpeg, quedan en
«Versiones antiguas/Voces conjuro del Sendero raw/» para escucharlas y cambiar a mano.
La clave de ElevenLabs y la llamada son las de Tools/Audio/redoblar_final.py.
"""
import argparse
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(__file__))
import redoblar_final as base  # noqa: E402

TOMAS = os.path.join(base.RAIZ, "Versiones antiguas", "Voces conjuro del Sendero raw")

LINEAS = [
    {"clave": "SENDERO_CONJURO_01",
     "texto": "[mystical] [chanting] [slowly] «Tú que tendiste el camino entre las estrellas cuando aún no había pies que lo pisaran...\n"
              "¡Despierta de tu sueño de piedra y recuerda tu senda!»"},
    {"clave": "SENDERO_CONJURO_02",
     "texto": "[intense] [building] [chanting] «No traigo coronas ni deudas. Solo un corazón que no pide nada para sí...\n"
              "¡Que se quiebre el sello y se encienda el umbral!»"},
    {"clave": "SENDERO_CONJURO_03",
     "texto": "[powerful] [epic] [chanting] «Tres pasos, tres pruebas, tres corazones.\n"
              "¡Por quien camina en la luz, abre la puerta de las estrellas!»"},
    {"clave": "SENDERO_CONJURO_04",
     "texto": "[climax] [shouts] [epic] ¡¡LLÉVANOS A LAS ESTRELLAS!!"},
]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--tomas", type=int, default=3)
    p.add_argument("--solo", nargs="*", default=None)
    args = p.parse_args()
    os.makedirs(TOMAS, exist_ok=True)
    api = base.clave_de_api()
    colocadas = []
    for linea in LINEAS:
        clave = linea["clave"]
        if args.solo and clave not in args.solo:
            continue
        print(clave)
        for n in range(1, args.tomas + 1):
            audio = base.generar(api, base.VOZ_WILL, linea["texto"], base.AJUSTES_WILL)
            if audio is None:
                print(f"  toma {n}: FALLÓ")
                continue
            toma = os.path.join(TOMAS, f"{clave}_toma{n}.mp3")
            with open(toma, "wb") as f:
                f.write(audio)
            print(f"  toma {n}: {len(audio) // 1024} KB")
            if n == 1:
                colocadas.append(clave)
            time.sleep(1.0)
    if not colocadas:
        return
    # Mismo estándar que el resto de voces: -18 LUFS, techo -2 dBTP.
    for nombre in os.listdir(TOMAS):
        if not nombre.endswith(".mp3"):
            continue
        ruta = os.path.join(TOMAS, nombre)
        tmp = ruta + ".norm.mp3"
        if subprocess.call(["ffmpeg", "-nostdin", "-loglevel", "error", "-y", "-i", ruta,
                            "-af", "loudnorm=I=-18:TP=-2:LRA=11", "-ar", "44100", "-b:a", "192k", tmp]) == 0:
            os.replace(tmp, ruta)
    for clave in colocadas:
        shutil.copy2(os.path.join(TOMAS, f"{clave}_toma1.mp3"), os.path.join(base.VOCES, clave + ".mp3"))
    print("Listo.")


if __name__ == "__main__":
    main()
