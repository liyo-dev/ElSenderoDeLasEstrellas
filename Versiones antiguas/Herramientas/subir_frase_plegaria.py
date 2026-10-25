#!/usr/bin/env python3
# RETIRADO (24 oct 2026): lo sustituye «subir_frases_plegaria.py» (procesa 01 y 03).
"""Sube la primera frase de PROLOGO_PLEGARIA_03 sin tocar el grito.

La toma está normalizada a -18 LUFS de media, pero la primera frase («Ningún poder de
este mundo puede detenerlo...») se grabó mucho más baja que el resto del cántico: unos
-30 dBFS de RMS frente a -22 dBFS de las otras estrofas. Al oírla sobre el tema del
conjuro se pierde. Aquí se sube solo ese tramo y se deja el grito como estaba.

Lee la copia sin tocar de `Versiones antiguas/Voices/es/` y escribe el recurso del juego.
Es idempotente: repetirla no acumula ganancia.

Uso:
    python Herramientas/Audio/subir_frase_plegaria.py
"""
import os
import subprocess
import sys

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", ".."))
sys.path.insert(0, AQUI)
from dsp_audio import decodificar, escribir, lufs, true_peak, db_a_ganancia, ffmpeg_bin, SR

ENTRADA = os.path.join(RAIZ, "Versiones antiguas", "Voices", "es",
                       "PROLOGO_PLEGARIA_03_antes_de_subir_la_frase.mp3")
SALIDA = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es",
                      "PROLOGO_PLEGARIA_03.mp3")

GANANCIA_DB = 8.0     # deja la frase a la altura de las otras estrofas (-22 dBFS)
HASTA = 2.9           # fin de la frase; después viene el silencio antes del grito
RAMPA = 0.5           # el grito entra sin escalón


def envolvente(n, sr=SR):
    """+GANANCIA_DB hasta HASTA, rampa lineal a 0 dB en HASTA+RAMPA, 0 después."""
    t = np.arange(n, dtype=np.float32) / sr
    ganancia = np.ones(n, dtype=np.float32)
    subida = t < HASTA
    ganancia[subida] = db_a_ganancia(GANANCIA_DB)
    rampa = (t >= HASTA) & (t < HASTA + RAMPA)
    u = (t[rampa] - HASTA) / RAMPA
    ganancia[rampa] = db_a_ganancia(GANANCIA_DB * (1.0 - u))
    return ganancia


def main():
    if not os.path.exists(ENTRADA):
        raise SystemExit(f"Falta la copia sin tocar: {ENTRADA}")
    x = decodificar(ENTRADA, SR, mono=True)
    antes = lufs(x)
    pico_antes = true_peak(x)
    x = x * envolvente(len(x))
    despues = lufs(x)
    pico_despues = true_peak(x)

    wav = os.path.join(AQUI, "_plegaria03_tmp.wav")
    escribir(wav, x, SR)
    subprocess.run([ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
                    "-i", wav, "-codec:a", "libmp3lame", "-b:a", "192k", SALIDA],
                   check=True)
    os.remove(wav)
    print(f"Escrito {SALIDA}")
    print(f"  LUFS {antes:.1f} -> {despues:.1f} | pico {pico_antes:.1f} -> {pico_despues:.1f} dBTP")


if __name__ == "__main__":
    main()
