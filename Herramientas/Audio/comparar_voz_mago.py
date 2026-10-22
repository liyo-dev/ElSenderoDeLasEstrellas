#!/usr/bin/env python3
"""Comparación de la voz del Mago Oscuro: original vs efecto B (Darth Vader).

Genera dos MP3 con los 13 clips del personaje, en el mismo orden y con una
pausa entre ellos, para poder escuchar la diferencia sin entrar en Play:

  voz_mago_1_original.mp3   los clips tal cual se grabaron
  voz_mago_2_efecto_B.mp3   los clips con el efecto horneado

Salida: Herramientas/Audio/Piloto/VozMagoOscuro/
"""
import os
import sys
import subprocess

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import SR, decodificar, escribir, lufs, db_a_ganancia

ORIG = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Versiones antiguas/Voces del Mago Oscuro antes de efecto (2026-10-22)"
DEST = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Assets/Resources/Voices/es"
SALIDA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Piloto", "VozMagoOscuro")

# El orden en el que aparecen en el juego
CLIPS = [
    "PROLOGO_DUELO_MAGO.mp3",
    "PROLOGO_DUELO_MAGO_2.mp3",
    "PROLOGO_MAGO_VALLE.mp3",
    "PROLOGO_MAGO_VACIO.mp3",
    "BF_INTRO_MAGO_01.mp3",
    "BF_INTRO_MAGO_02.mp3",
    "BF_INTRO_MAGO_03.mp3",
    "BF_INTRO_MAGO_04.mp3",
    "BF_MOMENT_MAGO_REMEMBER.mp3",
    "FINAL_MAGO_DESHACE.mp3",
    "FINAL_MAGO_MAREA.mp3",
    "FINAL_MAGO_TRAICION.mp3",
    "FINAL_MAGO_VENCIDO.mp3",
]

PAUSA = 0.7   # segundos entre clips

def ffmpeg_bin():
    return os.environ.get("FFMPEG", "ffmpeg")

def montar(carpeta, nombre):
    piezas = []
    for c in CLIPS:
        ruta = os.path.join(carpeta, c)
        if not os.path.exists(ruta):
            print("  falta:", c)
            continue
        x = decodificar(ruta, SR, mono=True)
        # A -18 LUFS y pico a -1 dBFS, como deja el import de Unity
        p = float(np.max(np.abs(x))) if len(x) else 0.0
        if p > 1e-9:
            x = x * (db_a_ganancia(-1.0) / p)
        piezas.append(x)
        piezas.append(np.zeros(int(PAUSA * SR), dtype=np.float32))
    if not piezas:
        print("  no hay clips")
        return
    y = np.concatenate(piezas)
    ruta = os.path.join(SALIDA, nombre)
    escribir(ruta.replace(".mp3", ".wav"), y, SR)
    subprocess.run([ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
                    "-i", ruta.replace(".mp3", ".wav"),
                    "-codec:a", "libmp3lame", "-b:a", "192k", ruta], check=True)
    os.remove(ruta.replace(".mp3", ".wav"))
    print(f"  {nombre}: {len(y)/SR:.1f} s  ({lufs(y):.1f} LUFS)")

if __name__ == "__main__":
    os.makedirs(SALIDA, exist_ok=True)
    print("1) Original:")
    montar(ORIG, "voz_mago_1_original.mp3")
    print("2) Con efecto B (Darth Vader):")
    montar(DEST, "voz_mago_2_efecto_B.mp3")
    print(f"\nEn {SALIDA}")
