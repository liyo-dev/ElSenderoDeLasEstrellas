#!/usr/bin/env python3
"""Re-procesa los 13 clips del Mago Oscuro: aplica efecto B + normaliza a -18 LUFS."""
import os
import sys
import subprocess

sys.path.insert(0, os.path.join(os.path.dirname(__file__)))

import numpy as np
from dsp_audio import (SR, decodificar, escribir, lufs, true_peak, normalizar_lufs,
                       compresor, limitador)

def normalizar_rms(x, objetivo_dbfs=-18.0):
    """Normaliza por RMS a un nivel dBFS objetivo (más robusto que LUFS para clips cortos)."""
    rms = np.sqrt(np.mean(x**2))
    if rms < 1e-10:
        return x
    actual_dbfs = 20 * np.log10(rms)
    ganancia = 10 ** ((objetivo_dbfs - actual_dbfs) / 20)
    return x * ganancia

ORIG = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Versiones antiguas/Voces del Mago Oscuro antes de efecto (2026-10-22)"
DEST = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Assets/Resources/Voices/es"

CLIPS = [
    "BF_INTRO_MAGO_01.mp3", "BF_INTRO_MAGO_02.mp3", "BF_INTRO_MAGO_03.mp3", "BF_INTRO_MAGO_04.mp3",
    "BF_MOMENT_MAGO_REMEMBER.mp3",
    "FINAL_MAGO_DESHACE.mp3", "FINAL_MAGO_MAREA.mp3", "FINAL_MAGO_TRAICION.mp3",
    "FINAL_MAGO_VENCIDO.mp3", "FINAL_MAGO_VACIO.mp3",
    "PROLOGO_DUELO_MAGO.mp3", "PROLOGO_DUELO_MAGO_2.mp3", "PROLOGO_MAGO_VALLE.mp3",
]

def ffmpeg_bin():
    return os.environ.get("FFMPEG", "ffmpeg")

def procesar_efecto_b(x):
    """Efecto B: -3st, low-pass 5kHz, chorus, comp, eco corto, limitador suave."""
    # 1) Pitch -3 semitonos (resample)
    ratio = 2 ** (-3.0 / 12.0)
    n_new = int(len(x) * ratio)
    x = np.interp(np.linspace(0, len(x)-1, n_new), np.arange(len(x)), x)
    
    # 2) Low-pass 5 kHz
    from scipy.signal import butter, filtfilt
    b, a = butter(4, 5000 / (SR/2), btype='low')
    x = filtfilt(b, a, x)
    
    # 3) Chorus ligero
    chorus_depth = int(0.003 * SR)
    chorus_rate = 0.8
    t = np.arange(len(x)) / SR
    mod = chorus_depth * np.sin(2*np.pi*chorus_rate*t)
    idx = np.arange(len(x)) + mod
    idx = np.clip(idx, 0, len(x)-1)
    x_chorus = np.interp(idx, np.arange(len(x)), x)
    x = 0.7 * x + 0.3 * x_chorus
    
    # 4) Compresión para controlar crest factor
    x = compresor(x, umbral_db=-20, ratio=4.0, ataque=0.005, release=0.1)
    
    # 5) Eco corto (slap 80ms)
    delay = int(0.08 * SR)
    if delay > 0:
        y = x.copy()
        y[delay:] += 0.25 * x[:-delay]
        x = y
    
    # 6) Limitador suave para controlar picos antes de normalizar
    x = limitador(x, techo_db=-3.0)
    
    return x

def normalizar_lufs_o_rms(x, objetivo_lufs=-18.0):
    """Normaliza a LUFS objetivo; si falla, usa RMS."""
    actual = lufs(x)
    if np.isfinite(actual) and actual > -80:
        ganancia = 10 ** ((objetivo_lufs - actual) / 20)
        return x * ganancia
    # Fallback: RMS
    rms = np.sqrt(np.mean(x**2))
    if rms < 1e-10:
        return x
    actual_dbfs = 20 * np.log10(rms)
    ganancia = 10 ** ((objetivo_lufs - actual_dbfs) / 20)
    return x * ganancia

def procesar_clip(nombre):
    src = os.path.join(ORIG, nombre)
    dst = os.path.join(DEST, nombre)
    
    x = decodificar(src, SR, mono=True)
    print(f"{nombre:30s} original: LUFS {lufs(x):7.2f}  pico {true_peak(x):7.2f}")
    
    y = procesar_efecto_b(x)
    print(f"  despues efecto B: LUFS {lufs(y):7.2f}  pico {true_peak(y):7.2f}")
    
    # Normalizar a -15 LUFS (deja headroom para true peak limiter a -1 dBTP)
    y = normalizar_lufs_o_rms(y, -15.0)
    print(f"  despues normalizar: LUFS {lufs(y):7.2f}  pico {true_peak(y):7.2f}")
    
    # True peak limit a -1 dBFS
    p_db = true_peak(y)
    if p_db > -1.0:
        ganancia_db = -1.0 - p_db
        y = y * (10 ** (ganancia_db / 20))
    
    print(f"{nombre:30s} final: LUFS {lufs(y):7.2f}  pico {true_peak(y):7.2f}")
    
    wav = dst.replace(".mp3", "_tmp.wav")
    escribir(wav, y, SR)
    subprocess.run([
        ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
        "-i", wav, "-ar", "44100", "-ac", "1", "-codec:a", "libmp3lame",
        "-b:a", "192k", dst
    ], check=True)
    os.remove(wav)
    return dst

if __name__ == "__main__":
    for clip in CLIPS:
        try:
            procesar_clip(clip)
        except Exception as e:
            print(f"ERROR en {clip}: {e}")
    print("\nListo! Unity reimportara automaticamente.")