#!/usr/bin/env python3
"""Variantes del mockup del conjuro: dónde y desde dónde entra el tema.

  A  tema desde el clímax (115 s) entrando en el negro  -> era el que oíste
  B  tema desde el principio entrando en el negro
  C  tema desde el principio entrando en la primera plegaria (más sitio)

El tema entra siempre con 1,5 s de fade y la voz con el ducking real del juego.
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
TEMA_ORIGINAL = r"C:/Users/luarb/Downloads/Ultimo Incantazione.mp3"

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

DUCK_MUSICA_DB = -8.0
DUCK_ATAQUE = 0.08
DUCK_RELEASE = 0.65
DUCK_HOLD = 0.5

INICIO = 160.0
FIN = 220.0

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
    mult = np.ones(n, dtype=np.float32)
    for t0, t1 in frases:
        a = max(int((t0 - 0.05 - INICIO) * SR), 0)
        b = min(int((t1 + DUCK_HOLD - INICIO) * SR), n)
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
    escribir(base + ".wav", x, SR)
    subprocess.run([ffmpeg_bin(), "-hide_banner", "-loglevel", "error", "-y",
                    "-i", base + ".wav", "-codec:a", "libmp3lame", "-b:a", "192k", ruta], check=True)
    os.remove(base + ".wav")

def cargar_sfx(nombre):
    for carpeta in (SFX_PRO, os.path.join(SFX_PRO, "Disenados")):
        ruta = os.path.join(carpeta, nombre)
        if os.path.exists(ruta):
            return decodificar(ruta, SR, mono=False)
    return None

def construir(nombre_salida, tema_entra, tema_offset, tema_db=-6.0, hasta=None):
    n = int((FIN - INICIO) * SR)
    musica = np.zeros((n, 2), dtype=np.float32)

    tema = decodificar(TEMA_ORIGINAL, SR, mono=False)
    trozo = tema[int(tema_offset * SR):]
    f_in = int(1.5 * SR)
    trozo[:f_in] *= np.linspace(0.0, 1.0, f_in)[:, None]
    if hasta:
        fin = int((hasta - tema_entra) * SR)
        if 0 < fin < len(trozo):
            f_out = min(int(1.2 * SR), fin)
            trozo[fin - f_out:fin] *= np.linspace(1.0, 0.0, f_out)[:, None]
            trozo = trozo[:fin]
    colocar(musica, trozo, tema_entra, tema_db)

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
    frases = []
    for clave, t, preset, gan in lineas:
        ruta = os.path.join(VOCES, clave + ".mp3")
        if not os.path.exists(ruta):
            continue
        x = preparar_voz(ruta, preset, gan)
        colocar(voz, x, t, 0.0)
        frases.append((t, t + len(x) / SR))

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

    musica *= duck_voz(frases, n)[:, None]
    total = musica + voz + efectos
    p = float(np.max(np.abs(total)))
    if p > db_a_ganancia(-1.0):
        total = total * (db_a_ganancia(-1.0) / p)
    escribir_mp3(os.path.join(SALIDA, nombre_salida), total)
    print(f"escrito {nombre_salida}")

if __name__ == "__main__":
    os.makedirs(SALIDA, exist_ok=True)
    # A: clímax (115 s) entrando en el negro (lo que oíste)
    construir("01A_climax_desde_el_negro.mp3", T_BLACK, 115.10, -6.0, hasta=T_FIN_MUSICA)
    # B: desde el principio, entrando en el negro
    construir("01B_desde_el_inicio_en_el_negro.mp3", T_BLACK, 0.0, -6.0, hasta=T_FIN_MUSICA)
    # C: desde el principio, entrando en la primera plegaria (más sitio)
    construir("01C_desde_el_inicio_en_plegaria.mp3", T_PLEGARIA_01, 0.0, -6.0, hasta=T_FIN_MUSICA)
