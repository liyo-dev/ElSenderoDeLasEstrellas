#!/usr/bin/env python3
"""Re-procesa los 13 clips del Mago: efecto B + normalización LUFS integrada con true peak."""
import os
import sys
import subprocess

sys.path.insert(0, os.path.join(os.path.dirname(__file__)))

import numpy as np
from scipy.signal import butter, filtfilt

from dsp_audio import (SR, decodificar, escribir, lufs, true_peak, db_a_ganancia,
                       compresor, limitador)

ORIG = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Versiones antiguas/Voces del Mago Oscuro antes de efecto (2026-10-22)"
DEST = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Assets/Resources/Voices/es"

CLIPS = [
    "BF_INTRO_MAGO_01.mp3", "BF_INTRO_MAGO_02.mp3", "BF_INTRO_MAGO_03.mp3", "BF_INTRO_MAGO_04.mp3",
    "BF_MOMENT_MAGO_REMEMBER.mp3",
    "FINAL_MAGO_DESHACE.mp3", "FINAL_MAGO_MAREA.mp3", "FINAL_MAGO_TRAICION.mp3",
    "FINAL_MAGO_VENCIDO.mp3",
    "PROLOGO_DUELO_MAGO.mp3", "PROLOGO_DUELO_MAGO_2.mp3", "PROLOGO_MAGO_VALLE.mp3",
]

def ffmpeg_bin():
    return os.environ.get("FFMPEG", "ffmpeg")

def normalizar_lufs_iterativo(x, objetivo=-18.0, techo_tp=-1.0, max_iter=5):
    """Normaliza a LUFS objetivo con true peak limiting iterativo (estilo EBU R128)."""
    for i in range(max_iter):
        actual = lufs(x)
        if not np.isfinite(actual) or actual < -80:
            break
        # Ganancia para llegar al objetivo
        ganancia_db = objetivo - actual
        if abs(ganancia_db) < 0.1:
            break
        x = x * db_a_ganancia(ganancia_db)
        
        # True peak check
        p_db = true_peak(x)
        if p_db > techo_tp:
            # Reducir ganancia para que el pico caiga en techo_tp
            reduccion_db = p_db - techo_tp
            x = x * db_a_ganancia(-reduccion_db)
            # Recalcular LUFS y ajustar objetivo para siguiente iteración
            objetivo = objetivo - reduccion_db
        else:
            break
    return x

def procesar_efecto_b(x):
    """Efecto B: -3st, low-pass 5kHz, chorus, comp, eco corto, limitador suave."""
    # 1) Pitch -3 semitonos
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
    
    # 4) Compresión fuerte para controlar crest factor
    x = compresor(x, umbral_db=-18, ratio=5.0, ataque=0.003, release=0.08)
    
    # 5) Eco corto (slap 80ms)
    delay = int(0.08 * SR)
    if delay > 0:
        y = x.copy()
        y[delay:] += 0.2 * x[:-delay]
        x = y
    
    # 6) Limitador a -3 dB para forzar crest factor conocido
    x = limitador(x, techo_db=-3.0)
    
    return x

def procesar_clip(nombre):
    src = os.path.join(ORIG, nombre)
    dst = os.path.join(DEST, nombre)
    
    x = decodificar(src, SR, mono=True)
    print(f"{nombre:30s} original: LUFS {lufs(x):7.2f}  pico {true_peak(x):7.2f}")
    
    y = procesar_efecto_b(x)
    
    # Normalización LUFS integrada con true peak limiting
    y = normalizar_lufs_iterativo(y, objetivo=-18.0, techo_tp=-1.0, max_iter=5)
    
    print(f"{nombre:30s} final: LUFS {lufs(y):7.2f}  pico {true_peak(y):7.2f}")
    
    # Guardar
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
        src = os.path.join(ORIG, clip)
        if not os.path.exists(src):
            print(f"SALTADO (no existe original): {clip}")
            continue
        try:
            procesar_clip(clip)
        except Exception as e:
            print(f"ERROR en {clip}: {e}")
    print("\nListo! Unity reimportara automaticamente.")