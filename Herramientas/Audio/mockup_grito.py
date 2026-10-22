#!/usr/bin/env python3
"""Mockup de la plegaria y el grito del prólogo: la música y la voz montadas.

Monta el tramo real del guion horneado con:
  - la voz del Archimago tal y como suena en el juego (preset `Ninguno` en la
    primera frase, preset `Conjuro` —9 s de sala + slap— en el cántico, y +6 dB
    de ganancia en el conjuro);
  - el tema del conjuro entrando en el negro (su propio primer segundo) y
    quedarse hasta que acaba el prólogo;
  - los ducks reales: el bus de música baja 6 dB mientras suena la voz con
    ganancia, el respiro aparta la música 10 dB y el golpe musical la aparta 6 dB;
  - los efectos del guion (truenos, cargas, campana, protección, explosión).

Salida en `Herramientas/Audio/Piloto/Grito/`.

Uso:
    python Herramientas/Audio/mockup_grito.py
"""
import os
import subprocess
import sys

import numpy as np

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import (SR, decodificar, escribir, lufs, true_peak, db_a_ganancia,
                       eco_slap, reverberacion)

VOCES = os.path.join(RAIZ, "Assets", "Resources", "Voices", "es")
SFX_PRO = os.path.join(RAIZ, "Assets", "Audio", "SFX_Prologo")
TEMA = os.path.join(RAIZ, "Assets", "Audio", "Music", "Cinematicas", "Ultimo Incantazione.mp3")
SALIDA = os.path.join(RAIZ, "Herramientas", "Audio", "Piloto", "Grito")

# Tiempos reales del guion horneado (el parte del horneado, en segundos).
T_PLEGARIA_01 = 167.90
T_PLEGARIA_02 = 178.00
T_PLEGARIA_03 = 186.20
T_PLEGARIA_04 = 196.00
T_PLEGARIA_05 = 202.30
T_PLEGARIA_06 = 208.50
T_GRITO = 213.90          # el grito, el respiro y la campana
T_ESTALLIDO = 218.20      # la explosión
T_FIN_MUSICA = 217.30     # «Música: parar (0,8s)»: la música se va antes de la explosión
T_NEGRO = 176.60          # el negro: el tema del duelo se corta y la voz se abre en el eco
T_TEMA = 178.00           # «¡Luz que naciste antes que el cielo...!»: entra el tema del conjuro
T_MEZCLA_GRITO = 212.80   # la mezcla «grito» (SFX -3 dB)

# La voz con el conjuro: +6 dB en el bus de diálogo, y el resto de buses (música,
# SFX, ambiente) bajan 6 dB para que la voz siga siendo la protagonista.
GANANCIA_VOZ_CONJURO = 6.0
DUCK_BUSES_POR_VOZ = -6.0
FUNDIDO_ENTRADA_TEMA = 1.5     # el fundido que pone el perfil de audio
DUCK_RESPIRO_MUSICA = -10.0
DUCK_GOLPE_MUSICA = -6.0
DURACION_GOLPE = 0.35
DURACION_RESPIRO = 0.9
ATAQUE_RESPIRO = 0.25
FUNDIDO_RESPIRO = 0.35
DUCK_RESPIRO_SFX = -8.0
FUNDIDO_MUSICA_FINAL = 1.2


def ffmpeg_bin():
    return os.environ.get("FFMPEG", "ffmpeg")


def colocar(destino, fuente, t, db=0.0):
    i = int(t * SR)
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
    """La voz como en el juego: pico a -1 dBFS, sala y slap del preset, bus a -18 LUFS."""
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


def construir(nombre_salida, inicio, fin):
    n = int((fin - inicio) * SR)

    def t_local(t):
        return t - inicio

    # --- La voz ---------------------------------------------------
    lineas = (
        ("PROLOGO_PLEGARIA_01", T_PLEGARIA_01, "ninguno", 0.0),
        ("PROLOGO_PLEGARIA_02", T_PLEGARIA_02, "conjuro", GANANCIA_VOZ_CONJURO),
        ("PROLOGO_PLEGARIA_03", T_PLEGARIA_03, "conjuro", GANANCIA_VOZ_CONJURO),
        ("PROLOGO_PLEGARIA_04", T_PLEGARIA_04, "conjuro", GANANCIA_VOZ_CONJURO),
        ("PROLOGO_PLEGARIA_05", T_PLEGARIA_05, "conjuro", GANANCIA_VOZ_CONJURO),
        ("PROLOGO_PLEGARIA_06", T_PLEGARIA_06, "conjuro", GANANCIA_VOZ_CONJURO),
        ("PROLOGO_HECHIZO", T_GRITO, "conjuro", GANANCIA_VOZ_CONJURO),
    )
    voz = np.zeros((n, 2), dtype=np.float32)
    for clave, t, preset, gan in lineas:
        ruta = os.path.join(VOCES, clave + ".mp3")
        if not os.path.exists(ruta):
            print(f"  (falta la voz {clave})")
            continue
        x = preparar_voz(ruta, preset, gan)
        colocar(voz, x, t_local(t), 0.0)

    # --- El tema: entra con la frase de la Luz y se queda hasta el final --------
    musica = np.zeros((n, 2), dtype=np.float32)
    tema = decodificar(TEMA, SR, mono=False)
    f_in = max(1, int(FUNDIDO_ENTRADA_TEMA * SR))
    tema[:f_in] *= np.linspace(0.0, 1.0, f_in)[:, None]
    colocar(musica, tema, t_local(T_TEMA), 0.0)

    # --- Los efectos del guion --------------------------------------
    efectos = np.zeros((n, 2), dtype=np.float32)
    for archivo, t, db in (
        ("SFX_Prologo_Trueno_Lejano.wav", 178.80, -14.0),
        ("SFX_Prologo_Trueno_1.wav", 189.40, -9.1),
        ("SFX_Prologo_Trueno_2.wav", 205.70, -9.1),
        ("SFX_Prologo_Trueno_2.wav", 205.90, -9.1),
        ("SFX_Prologo_Trueno_2.wav", 214.10, 0.0),
        ("Prologo_Carga.wav", 189.70, -6.0),
        ("Prologo_Carga.wav", 205.80, -6.0),
        ("SFX_Golpe_Final_Campana.wav", T_GRITO, 0.0),
        ("Prologo_ProteccionAbsoluta.wav", 214.40, -0.4),
        ("SFX_Lucha_Final.wav", 214.40, -6.0),
        ("SFX_Explosion_Final.wav", 216.60, -6.0),
        ("SFX_Golpe_Final_Cola.wav", T_ESTALLIDO, -3.0),
        ("SFX_Prologo_Silencio_Blanco.wav", 219.20, -4.4),
    ):
        x = cargar_sfx(archivo)
        if x is not None:
            colocar(efectos, x, t_local(t), db)

    # --- Los buses como en el juego ---------------------------------
    # La música: -6 dB desde que la voz con ganancia está puesta (el negro), el
    # respiro del grito la aparta 10 dB y el golpe musical 6 dB.
    musica_bus = np.ones(n, dtype=np.float32)
    i_negro = int(t_local(T_NEGRO) * SR)
    if i_negro < n:
        musica_bus[i_negro:] *= db_a_ganancia(DUCK_BUSES_POR_VOZ)
    r0 = int(t_local(T_GRITO) * SR)
    r1 = min(int((t_local(T_GRITO) + DURACION_RESPIRO) * SR), n)
    if 0 <= r0 < n and r1 > r0:
        factor = np.ones(r1 - r0, dtype=np.float32)
        k = min(int(ATAQUE_RESPIRO * SR), r1 - r0)
        if k > 1:
            factor[:k] *= np.linspace(1.0, db_a_ganancia(DUCK_RESPIRO_MUSICA), k, dtype=np.float32)
        k = min(int(FUNDIDO_RESPIRO * SR), r1 - r0)
        if k > 1:
            factor[-k:] *= np.linspace(db_a_ganancia(DUCK_RESPIRO_MUSICA), 1.0, k, dtype=np.float32)
        musica_bus[r0:r1] *= factor
    g0 = int(t_local(T_GRITO) * SR)
    g1 = min(int((t_local(T_GRITO) + DURACION_GOLPE) * SR), n)
    if 0 <= g0 < n:
        musica_bus[g0:g1] *= db_a_ganancia(DUCK_GOLPE_MUSICA)
    # Y se apaga sola con la explosión.
    f0 = int(t_local(T_FIN_MUSICA) * SR)
    f1 = min(int((t_local(T_FIN_MUSICA) + FUNDIDO_MUSICA_FINAL) * SR), n)
    if 0 <= f0 < n:
        musica_bus[f0:f1] *= np.linspace(1.0, 0.0, f1 - f0, dtype=np.float32)
    musica *= musica_bus[:, None]

    # Los SFX: -6 dB por la voz con ganancia, -3 dB por la mezcla «grito» y el
    # respiro del grito (0,9 s) los aparta 8 dB más.
    sfx_bus = np.ones(n, dtype=np.float32)
    if i_negro < n:
        sfx_bus[i_negro:] *= db_a_ganancia(DUCK_BUSES_POR_VOZ)
    g0 = int(t_local(T_MEZCLA_GRITO) * SR)
    if 0 <= g0 < n:
        sfx_bus[g0:] *= db_a_ganancia(-3.0)
    if 0 <= r0 < n and r1 > r0:
        factor = np.ones(r1 - r0, dtype=np.float32)
        k = min(int(ATAQUE_RESPIRO * SR), r1 - r0)
        if k > 1:
            factor[:k] *= np.linspace(1.0, db_a_ganancia(DUCK_RESPIRO_SFX), k, dtype=np.float32)
        k = min(int(FUNDIDO_RESPIRO * SR), r1 - r0)
        if k > 1:
            factor[-k:] *= np.linspace(db_a_ganancia(DUCK_RESPIRO_SFX), 1.0, k, dtype=np.float32)
        sfx_bus[r0:r1] *= factor
    efectos *= sfx_bus[:, None]

    total = musica + voz + efectos
    p = float(np.max(np.abs(total)))
    if p > db_a_ganancia(-1.0):
        total = total * (db_a_ganancia(-1.0) / p)

    ruta = os.path.join(SALIDA, nombre_salida)
    escribir_mp3(ruta, total)
    print(f"  {nombre_salida}: {inicio:.1f}-{fin:.1f} s de la secuencia | "
          f"LUFS {lufs(total.mean(axis=1)):.1f} | pico {true_peak(total):.1f} dBTP")


def main():
    os.makedirs(SALIDA, exist_ok=True)
    tema = decodificar(TEMA, SR, mono=True)
    print(f"Tema: {TEMA}")
    print(f"  {len(tema)/SR:.2f} s, entra con la frase de la Luz ({T_TEMA:.2f} s) y suena hasta el final")
    print(f"Plegaria: {T_PLEGARIA_01:.2f} -> {T_PLEGARIA_06 + 3.8:.2f} s")
    print(f"Estallido: {T_ESTALLIDO:.2f} s | la música se apaga en {T_FIN_MUSICA:.2f} s\n")

    # 1) La plegaria entera y el grito, tal y como queda en el juego.
    construir("01_plegaria_y_grito.mp3", 165.0, 226.0)
    # 2) Solo el final, para escuchar el golpe sin esperar un minuto.
    construir("02_solo_el_grito.mp3", 205.0, 226.0)
    # 3) La plegaria desde el principio (el negro y el cántico).
    construir("03_desde_el_negro.mp3", 175.0, 226.0)
    print(f"\nEn {SALIDA}")


if __name__ == "__main__":
    main()
