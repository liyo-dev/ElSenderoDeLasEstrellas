#!/usr/bin/env python3
"""Mockup del tramo del hechizo del prólogo con el tema del conjuro y la voz.

Monta la ventana real del guion horneado con:
  - la voz del Archimago (preset Conjuro del juego: 9 s de sala + slap) y la
    subida de +6 dB del conjuro;
  - el tema "Ultimo Incantazione" entrando en el negro, con su pico justo en
    el grito;
  - el ducking de la voz sobre la música (el del juego: -8 dB);
  - los efectos del guion (truenos, campana, protección, explosión).

Uso:
    python Herramientas/Audio/mockup_hechizo.py
"""
import os
import sys
import subprocess

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import (SR, decodificar, escribir, lufs, true_peak, db_a_ganancia,
                       eco_slap, reverberacion)

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
VOCES = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es")
SFX_PRO = os.path.join(RAIZ, "Assets", "Audio", "SFX_Prologo")
SALIDA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Piloto", "HechizoFinal")
TEMA = os.path.join(RAIZ, "Assets", "Audio", "Music", "Cinematicas", "Ultimo Incantazione.mp3")

# Tiempos reales del guion horneado
T_PLEGARIA_01 = 162.60
T_PLEGARIA_02 = 172.70
T_PLEGARIA_03 = 180.92
T_PLEGARIA_04 = 190.04
T_PLEGARIA_05 = 197.04
T_PLEGARIA_06 = 203.18
T_GRITO = 208.65
T_ESTALLIDO = 212.91
T_FIN_MUSICA = 213.81
T_BLACK = 171.30

# El pico global del tema original está en 152,45 s
PICO_TEMA = 152.45

INICIO = 160.0
FIN = 220.0

# Ducking de voz en cinemáticas (AudioGraphProfile.cinematicVoiceDuck)
DUCK_MUSICA_DB = -8.0
DUCK_ATAQUE = 0.08
DUCK_RELEASE = 0.65
DUCK_HOLD = 0.5

def ffmpeg_bin():
    return os.environ.get("FFMPEG", "ffmpeg")

def colocar(destino, fuente, t, db=0.0):
    i = int((t - INICIO) * SR)
    if i >= len(destino) or i + len(fuente) <= 0:
        return
    a = max(i, 0)
    b = min(i + len(fuente), len(destino))
    if fuente.ndim == 1:
        fuente = fuente[:, None]
    if fuente.shape[1] == 1:
        fuente = np.repeat(fuente, 2, axis=1)
    destino[a:b] += fuente[a - i:b - i] * db_a_ganancia(db)

def preparar_voz(ruta, preset="conjuro", ganancia_db=0.0):
    """Voz como en el juego: pico a -1 dBFS, preset de sala/slap, bus a -18 LUFS."""
    x = decodificar(ruta, SR, mono=True)
    p = float(np.max(np.abs(x))) if len(x) else 0.0
    if p > 1e-9:
        x = x * (db_a_ganancia(-1.0) / p)
    if preset == "conjuro":
        sala = reverberacion(x, sr=SR, decay=9.0, predelay=0.04, wet=1.0)
        x = 0.1 * x + (10 ** (-30.0 / 20.0)) * sala
        x = x + 0.38 * eco_slap(x, retardo_ms=400.0, decaimiento=0.45, humedo=1.0)
    actual = lufs(x)
    if np.isfinite(actual) and actual > -80:
        x = x * db_a_ganancia(-18.0 - actual)
    return x * db_a_ganancia(ganancia_db)

def duck_voz(frases, n):
    """Multiplicador del bus de música: -8 dB mientras hay voz."""
    mult = np.ones(n, dtype=np.float32)
    for t0, t1 in frases:
        a = int((t0 - 0.05 - INICIO) * SR)
        b = int((t1 + DUCK_HOLD - INICIO) * SR)
        a = max(a, 0)
        b = min(b, n)
        if b <= a:
            continue
        curva = np.full(b - a, db_a_ganancia(DUCK_MUSICA_DB), dtype=np.float32)
        k = min(int(DUCK_ATAQUE * SR), b - a)
        if k > 1:
            curva[:k] *= np.linspace(1.0, db_a_ganancia(DUCK_MUSICA_DB), k)
        k = min(int(DUCK_RELEASE * SR), b - a)
        if k > 1:
            curva[-k:] *= np.linspace(db_a_ganancia(DUCK_MUSICA_DB), 1.0, k)
        mult[a:b] = np.minimum(mult[a:b], curva)
    return mult

def escribir_mp3(ruta, x):
    base = os.path.splitext(ruta)[0]
    wav = base + ".wav"
    escribir(wav, x, SR)
    subprocess.run([ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
                    "-i", wav, "-codec:a", "libmp3lame", "-b:a", "192k", ruta], check=True)
    os.remove(wav)

def cargar_sfx(nombre):
    for carpeta in (SFX_PRO, os.path.join(SFX_PRO, "Disenados")):
        ruta = os.path.join(carpeta, nombre)
        if os.path.exists(ruta):
            return decodificar(ruta, SR, mono=False)
    return None

def construir(nombre_salida, tema_entra, tema_offset, tema_db=-6.0, solo_hechizo=False):
    n = int((FIN - INICIO) * SR)
    musica = np.zeros((n, 2), dtype=np.float32)

    # --- Tema ------------------------------------------------------
    if tema_entra is not None:
        tema = decodificar(TEMA, SR, mono=False)
        trozo = tema[int(tema_offset * SR):]
        f_in = int(1.5 * SR)
        trozo[:f_in] *= np.linspace(0.0, 1.0, f_in)[:, None]
        colocar(musica, trozo, tema_entra, tema_db)

    # --- Voz -------------------------------------------------------
    lineas = (
        ("PROLOGO_PLEGARIA_01", T_PLEGARIA_01, "ninguno", 0.0),
        ("PROLOGO_PLEGARIA_02", T_PLEGARIA_02, "conjuro", 0.0),
        ("PROLOGO_PLEGARIA_03", T_PLEGARIA_03, "conjuro", 0.0),
        ("PROLOGO_PLEGARIA_04", T_PLEGARIA_04, "conjuro", 0.0),
        ("PROLOGO_PLEGARIA_05", T_PLEGARIA_05, "conjuro", 0.0),
        ("PROLOGO_PLEGARIA_06", T_PLEGARIA_06, "conjuro", 0.0),
        ("PROLOGO_HECHIZO", T_GRITO, "conjuro", 6.0),
    )
    voz = np.zeros((n, 2), dtype=np.float32)
    voz_frases = []
    for clave, t, preset, gan in lineas:
        ruta = os.path.join(VOCES, clave + ".mp3")
        if not os.path.exists(ruta):
            continue
        x = preparar_voz(ruta, preset, gan)
        colocar(voz, x, t, 0.0)
        voz_frases.append((t, t + len(x) / SR))

    # --- Efectos ---------------------------------------------------
    efectos = np.zeros((n, 2), dtype=np.float32)
    for archivo, t, db in (
        ("SFX_Prologo_Trueno_Lejano.wav", 172.8, -14.0),
        ("SFX_Prologo_Trueno_1.wav", 184.1, -9.0),
        ("SFX_Prologo_Trueno_2.wav", 200.4, -9.0),
        ("SFX_Prologo_Trueno_2.wav", 200.9, -9.0),
        ("SFX_Prologo_Trueno_2.wav", 208.9, 0.0),
        ("Prologo_Carga.wav", 184.6, -6.0),
        ("Prologo_Carga.wav", 200.7, -6.0),
        ("Prologo_Carga.wav", 208.6, -8.0),
        ("SFX_Golpe_Final_Campana.wav", 208.8, -3.0),
        ("Prologue_WarClashStinger_A.wav", 155.5, -6.0),
        ("Prologo_ProteccionAbsoluta.wav", 209.2, -0.5),
        ("SFX_Prologo_ExplosionFinal.wav", 212.9, 0.0),
        ("SFX_Golpe_Final_Cola.wav", 212.9, -3.0),
    ):
        x = cargar_sfx(archivo)
        if x is not None:
            colocar(efectos, x, t, db)

    # --- Ducking de la voz sobre la música --------------------------
    musica *= duck_voz(voz_frases, n)[:, None]

    total = musica + voz + efectos
    p = float(np.max(np.abs(total)))
    if p > db_a_ganancia(-1.0):
        total = total * (db_a_ganancia(-1.0) / p)

    escribir_mp3(os.path.join(SALIDA, nombre_salida), total)
    print(f"escrito {nombre_salida}  (LUFS {lufs(total.mean(axis=1)):.1f})")

def main():
    os.makedirs(SALIDA, exist_ok=True)
    # El tema ya está recortado en el proyecto: su pico está al principio.
    # Para que el pico caiga en el grito, el offset es 0.
    print(f"Negro {T_BLACK:.2f} s, grito {T_GRITO:.2f} s "
          f"(separación {T_GRITO - T_BLACK:.2f} s)")
    print(f"El tema recortado mide {len(decodificar(TEMA, SR, mono=True))/SR:.2f} s "
          f"y su pico está en {PICO_TEMA - 115.10:.2f} s de su propio inicio\n")

    # 1) El tema entra en el negro y culmina en el grito (recomendado)
    construir("01_conjuro_completo.mp3", T_BLACK, 0.0, -6.0)

    # 2) Solo el tramo del hechizo
    global INICIO, FIN
    INICIO, FIN = 196.0, 220.0
    construir("02_solo_hechizo.mp3", T_BLACK, 0.0, -6.0)
    INICIO, FIN = 160.0, 220.0

if __name__ == "__main__":
    main()
