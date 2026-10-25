#!/usr/bin/env python3
"""Sube la primera frase de las plegarias que se grabaron demasiado bajas.

- PROLOGO_PLEGARIA_01: «No daré ni un paso atrás...» (0-3,5 s), +10 dB.
- PROLOGO_PLEGARIA_03: «Ningún poder de este mundo...» (0-2,9 s), +8 dB.

Las dos tomas están normalizadas a -18 LUFS de media, pero la primera frase se grabó
mucho más baja que el grito que viene después: al oírla sobre la música se pierde. Aquí
se sube solo ese tramo y se deja el grito como estaba.

Lee las copias sin tocar de `Versiones antiguas/Voices/es/` y escribe los recursos del
juego. Es idempotente: repetirla no acumula ganancia.

Uso:
    python Herramientas/Audio/subir_frases_plegaria.py
"""
import os
import subprocess
import sys

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", ".."))
sys.path.insert(0, AQUI)
from dsp_audio import decodificar, escribir, lufs, true_peak, db_a_ganancia, ffmpeg_bin, SR

# (clave, ganancia dB, fin de la frase en s, rampa en s)
TRABAJOS = [
    ("PROLOGO_PLEGARIA_01", 10.0, 3.5, 0.6),
    ("PROLOGO_PLEGARIA_03", 8.0, 2.9, 0.5),
]


def envolvente(n, ganancia_db, hasta, rampa, sr=SR):
    """+ganancia hasta «hasta», rampa lineal a 0 dB en «hasta+rampa», 0 después."""
    t = np.arange(n, dtype=np.float32) / sr
    ganancia = np.ones(n, dtype=np.float32)
    subida = t < hasta
    ganancia[subida] = db_a_ganancia(ganancia_db)
    tramo = (t >= hasta) & (t < hasta + rampa)
    u = (t[tramo] - hasta) / rampa
    ganancia[tramo] = db_a_ganancia(ganancia_db * (1.0 - u))
    return ganancia


def procesar(clave, ganancia_db, hasta, rampa):
    entrada = os.path.join(RAIZ, "Versiones antiguas", "Voices", "es",
                           f"{clave}_antes_de_subir_la_frase.mp3")
    salida = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es", f"{clave}.mp3")
    if not os.path.exists(entrada):
        raise SystemExit(f"Falta la copia sin tocar: {entrada}")

    x = decodificar(entrada, SR, mono=True)
    antes = lufs(x)
    pico_antes = true_peak(x)
    x = x * envolvente(len(x), ganancia_db, hasta, rampa)
    despues = lufs(x)
    pico_despues = true_peak(x)

    wav = os.path.join(AQUI, f"_{clave}_tmp.wav")
    escribir(wav, x, SR)
    subprocess.run([ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
                    "-i", wav, "-codec:a", "libmp3lame", "-b:a", "192k", salida],
                   check=True)
    os.remove(wav)
    print(f"{clave}: +{ganancia_db:.0f} dB hasta {hasta:.1f} s | "
          f"LUFS {antes:.1f} -> {despues:.1f} | pico {pico_antes:.1f} -> {pico_despues:.1f} dBTP")


def main():
    for clave, ganancia_db, hasta, rampa in TRABAJOS:
        procesar(clave, ganancia_db, hasta, rampa)


if __name__ == "__main__":
    main()
