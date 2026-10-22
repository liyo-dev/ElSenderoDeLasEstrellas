#!/usr/bin/env python3
"""Re-procesa los 13 clips del Mago: efecto B + normalización LUFS+TP en un solo paso."""
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

def normalizar_lufs_tp(x, objetivo_lufs=-18.0, techo_tp=-1.0):
    """Normalización en un solo paso: ganancia única que cumple LUFS y true peak."""
    L = lufs(x)
    TP = true_peak(x)
    print(f"    normalizar: L={L:.2f} LUFS, TP={TP:.2f} dBTP")
    
    if not np.isfinite(L) or L < -80:
        return x
    
    # Ganancia para cumplir LUFS objetivo
    ganancia_lufs_db = objetivo_lufs - L
    # Ganancia para cumplir true peak ≤ -1 dBTP
    ganancia_tp_db = techo_tp - true_peak(x)
    
    print(f"    ganancia_lufs={ganancia_lufs_db:.2f} dB, ganancia_tp={ganancia_tp_db:.2f} dB")
    
    # La ganancia final es la más restrictiva (la más negativa)
    ganancia_db = min(ganancia_lufs_db, ganancia_tp_db)
    
    print(f"    ganancia_final={ganancia_db:.2f} dB")
    
    # Aplicar ganancia
    x = x * (10 ** (ganancia_db / 20.0))
    
    print(f"    tras ganancia: LUFS={lufs(x):.2f}, TP={true_peak(x):.2f}")
    return x

def procesar_efecto_b(x):
    """Efecto B: -3st, low-pass 5kHz, chorus, eco corto, limitador BRUTO."""
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
    
    # 4) Eco corto (slap 80ms)
    delay = int(0.08 * SR)
    if delay > 0:
        y = x.copy()
        y[delay:] += 0.15 * x[:-delay]
        x = y
    
    # 5) Limitador BRUTO a -6 dB para forzar crest factor bajo
    x = limitador(x, techo_db=-6.0)
    
    return x

def procesar_clip(nombre):
    src = os.path.join(ORIG, nombre)
    dst = os.path.join(DEST, nombre)
    
    x = decodificar(src, SR, mono=True)
    print(f"{nombre:30s} original: LUFS {lufs(x):7.2f}  pico {true_peak(x):7.2f}")
    
    y = procesar_efecto_b(x)
    
    # Normalización LUFS + True Peak en un solo paso (estilo EBU R128)
    y = normalizar_lufs_tp(y, objetivo_lufs=-18.0, techo_tp=-1.0)
    
    print(f"{nombre:30s} final: LUFS {lufs(y):7.2f}  pico {true_peak(y):7.2f}")
    
    # Guardar
    wav = os.path.join(DEST, nombre.replace(".mp3", "_tmp.wav"))
    escribir(wav, y, SR)
    subprocess.run([
        ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
        "-i", os.path.join(DEST, nombre.replace(".mp3", "_tmp.wav")),
        "-ar", "44100", "-ac", "1", "-codec:a", "libmp3lame", "-b:a", "192k",
        os.path.join(DEST, nombre)
    ], check=True)
    os.remove(os.path.join(DEST, nombre.replace(".mp3", "_tmp.wav")))
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