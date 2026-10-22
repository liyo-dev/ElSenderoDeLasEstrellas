#!/usr/bin/env python3
"""Voz del Mago Oscuro: efecto B (tipo Darth Vader) sobre los originales.

Cadena:
  1. Pitch -3 semitonos con rubberband (phase vocoder: conserva la duración)
  2. Paso bajo a 5 kHz (la voz se hunde)
  3. Chorus suave
  4. Eco corto (slap 80 ms)
  5. Limitador a -6 dB (aplasta el crest factor)
  6. Normalización LUFS + true peak en un solo paso (estilo EBU R128)

Los originales están en "Versiones antiguas/Voces del Mago Oscuro antes de efecto".
"""
import os
import sys
import subprocess

sys.path.insert(0, os.path.join(os.path.dirname(__file__)))

import numpy as np
from scipy.signal import butter, filtfilt

from dsp_audio import (SR, decodificar, escribir, lufs, true_peak, db_a_ganancia,
                       limitador)

ORIG = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Versiones antiguas/Voces del Mago Oscuro antes de efecto (2026-10-22)"
DEST = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Assets/Resources/Voices/es"
TMP = os.path.join(os.environ.get("TEMP", "."), "mago_pitch")

CLIPS = [
    "BF_INTRO_MAGO_01.mp3", "BF_INTRO_MAGO_02.mp3", "BF_INTRO_MAGO_03.mp3", "BF_INTRO_MAGO_04.mp3",
    "BF_MOMENT_MAGO_REMEMBER.mp3",
    "FINAL_MAGO_DESHACE.mp3", "FINAL_MAGO_MAREA.mp3", "FINAL_MAGO_TRAICION.mp3",
    "FINAL_MAGO_VENCIDO.mp3",
    "PROLOGO_DUELO_MAGO.mp3", "PROLOGO_DUELO_MAGO_2.mp3", "PROLOGO_MAGO_VALLE.mp3",
    "PROLOGO_MAGO_VACIO.mp3",
]

SEMITONOS = -3.0
PICO_OBJETIVO_DBFS = -18.0
TP_OBJETIVO = -1.0

def ffmpeg_bin():
    return os.environ.get("FFMPEG", "ffmpeg")

def bajar_pitch(ruta, semitonos, salida):
    """Baja el tono N semitonos conservando la duración (rubberband)."""
    ratio = 2.0 ** (semitonos / 12.0)
    subprocess.run([
        ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
        "-i", ruta,
        "-af", f"rubberband=pitch={ratio:.6f},aresample={SR}",
        "-ar", str(SR), "-ac", "1", "-c:a", "pcm_f32le", salida,
    ], check=True)

def efecto_b(x):
    """Low-pass 5 kHz, chorus, eco corto y limitador (la voz se hunde)."""
    # Paso bajo 5 kHz
    b, a = butter(4, 5000 / (SR / 2), btype="low")
    x = filtfilt(b, a, x)

    # Chorus suave
    depth = int(0.003 * SR)
    t = np.arange(len(x)) / SR
    mod = depth * np.sin(2 * np.pi * 0.8 * t)
    idx = np.clip(np.arange(len(x)) + mod, 0, len(x) - 1)
    x = 0.7 * x + 0.3 * np.interp(idx, np.arange(len(x)), x)

    # Eco corto (slap 80 ms)
    d = int(0.08 * SR)
    if d > 0:
        y = x.copy()
        y[d:] += 0.15 * x[:-d]
        x = y

    # Limitador a -6 dB: así el pico no se lleva el nivel al normalizar
    y = limitador(x, techo_db=-6.0)
    # limitador devuelve (n, 1): hay que aplanarlo o al escribir el WAV
    # se interpretaría como dos canales y el clip saldría a la mitad de largo
    return y[:, 0] if y.ndim == 2 else y

def normalizar_lufs_tp(x, objetivo=PICO_OBJETIVO_DBFS, techo_tp=TP_OBJETIVO):
    """Ganancia única que cumple LUFS objetivo y true peak (estilo EBU R128)."""
    L = lufs(x)
    if not np.isfinite(L) or L < -80:
        return x
    g_lufs = objetivo - L
    g_tp = techo_tp - true_peak(x)
    g = min(g_lufs, g_tp)
    return x * db_a_ganancia(g)

def procesar(nombre):
    src = os.path.join(ORIG, nombre)
    dst = os.path.join(DEST, nombre)
    if not os.path.exists(src):
        print(f"SALTADO (no hay original): {nombre}")
        return

    original = decodificar(src, SR, mono=True)
    wav_pitch = os.path.join(TMP, nombre.replace(".mp3", "_pitch.wav"))
    bajar_pitch(src, SEMITONOS, wav_pitch)
    x = decodificar(wav_pitch, SR, mono=True)
    os.remove(wav_pitch)

    y = efecto_b(x)
    y = normalizar_lufs_tp(y)

    dur_o = len(original) / SR
    dur_y = len(y) / SR
    print(f"{nombre:30s} dur {dur_o:5.2f}->{dur_y:5.2f}s  "
          f"LUFS {lufs(original):6.2f}->{lufs(y):6.2f}  TP {true_peak(y):6.2f}")

    wav = os.path.join(TMP, nombre.replace(".mp3", "_out.wav"))
    escribir(wav, y, SR)
    subprocess.run([
        ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
        "-i", wav, "-ar", "44100", "-ac", "1", "-codec:a", "libmp3lame", "-b:a", "192k", dst,
    ], check=True)
    os.remove(wav)

if __name__ == "__main__":
    os.makedirs(TMP, exist_ok=True)
    for c in CLIPS:
        try:
            procesar(c)
        except Exception as e:
            print(f"ERROR en {c}: {e}")
    print("\nListo! Unity reimportara automaticamente.")
