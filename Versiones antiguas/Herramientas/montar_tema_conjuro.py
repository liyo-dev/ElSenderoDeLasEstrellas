#!/usr/bin/env python3
# RETIRADO (24 oct 2026): el montaje se descartó; el juego vuelve al clip original de 38,66 s.
"""Monta el tema del conjuro del prólogo: la entrada de la pieza + la cola del tema.

El corte anterior (38,66 s) empezaba demasiado tarde: su fundido natural caía en el
grito y la música se apagaba antes de la explosión. Este montaje le añade delante el
tramo 111-115 s de la pieza (la subida que precede al clímax) para que el fundido
natural caiga ya en el blanco final, sin tocar el resto del tema.

Lee la entrada y el cuerpo de `Versiones antiguas/Audio/` y escribe el recurso del juego.
Es reproducible: repetirlo da el mismo archivo.

Uso:
    python Herramientas/Audio/montar_tema_conjuro.py
"""
import os
import subprocess
import sys

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", ".."))
sys.path.insert(0, AQUI)
from dsp_audio import decodificar, escribir, lufs, true_peak, ffmpeg_bin, SR

ENTRADA = os.path.join(RAIZ, "Versiones antiguas", "Audio",
                       "Ultimo Incantazione - entrada 111-115 s.mp3")
CUERPO = os.path.join(RAIZ, "Versiones antiguas", "Audio",
                      "Ultimo Incantazione - lecho del negro (2026-10-22).mp3")
SALIDA = os.path.join(RAIZ, "Assets", "Audio", "Music", "Cinematicas",
                      "Ultimo Incantazione.mp3")

MEZCLA = 0.05     # crossfade en la costura, en segundos


def rms(x):
    return float(np.sqrt(np.mean(x.astype(np.float64) ** 2)))


def main():
    for ruta in (ENTRADA, CUERPO):
        if not os.path.exists(ruta):
            raise SystemExit(f"Falta {ruta}")
    entrada = decodificar(ENTRADA, SR, mono=False)
    cuerpo = decodificar(CUERPO, SR, mono=False)

    # El cuerpo está 0,7 dB por encima de la pieza completa: igualar la entrada a su nivel
    # en la costura para que el empalme no se oiga.
    n = int(1.0 * SR)
    ganancia = rms(cuerpo[:n]) / max(1e-9, rms(entrada[-n:]))
    entrada = entrada * ganancia

    f = int(MEZCLA * SR)
    u = np.linspace(0.0, 1.0, f, dtype=np.float32)[:, None]
    cuerpo[:f] = entrada[-f:] * (1.0 - u) + cuerpo[:f] * u
    tema = np.concatenate([entrada[:-f], cuerpo], axis=0)

    wav = os.path.join(AQUI, "_tema_conjuro_tmp.wav")
    escribir(wav, tema, SR)
    subprocess.run([ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
                    "-i", wav, "-codec:a", "libmp3lame", "-b:a", "192k", SALIDA],
                   check=True)
    os.remove(wav)
    print(f"Escrito {SALIDA}")
    print(f"  {len(tema)/SR:.2f} s | LUFS {lufs(tema):.1f} | pico {true_peak(tema):.1f} dBTP")


if __name__ == "__main__":
    main()
