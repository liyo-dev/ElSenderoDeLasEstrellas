"""Genera las capas que faltan para que un impacto suene a golpe (INC-801).

Los impactos del proyecto son un clip por elemento y tamaño: suenan a "una nota", no a
golpe. Un golpe de cine se construye por capas:

  1. transitorio  -> la grieta fina de 2-8 kHz (10-40 ms)
  2. cuerpo       -> el clip que ya existe (el sonido del impacto)
  3. sub          -> 30-70 Hz que entra TARDE y crece (es lo que da peso)
  4. cola         -> 2-3 s de尾巴 (rumor, chispas, aire) que se apaga

Este script genera las tres capas que no existen (transitorio, sub y cola) con el
carácter de cada elemento. Los cuerpos son los clips que ya hay.

Uso:
    python Herramientas/Audio/disenar_capas_de_hechizo.py
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp_audio import (  # noqa: E402
    SR, banda, db_a_ganancia, escribir, paso_alto, paso_bajo, normalizar_pico,
)

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SALIDA = os.path.join(RAIZ, "Assets", "Audio", "SFX_Hechizos", "Capas")

# elemento: (frecuencia del transitorio, sub inicial, sub final, tipo de cola, brillo de la cola)
ELEMENTOS = {
    "Fuego":   dict(transitorio=(3600, 7200), sub=(58, 36), cola="chispa", brillo=5200),
    "Viento":  dict(transitorio=(1900, 4600), sub=(72, 44), cola="aire",    brillo=3200),
    "Luz":     dict(transitorio=(4800, 9200), sub=(62, 40), cola="cristal", brillo=7200),
    "Mente":   dict(transitorio=(2800, 5600), sub=(52, 33), cola="metal",  brillo=4200),
    "Oscuro":  dict(transitorio=(700, 2400),  sub=(46, 26), cola="rumor",  brillo=900),
}


def transitorio(lo, hi, dur=0.05, semilla=3):
    """La grieta: ruido de banda con caída rapidísima."""
    rng = np.random.default_rng(semilla)
    n = int(dur * SR)
    x = banda(rng.standard_normal(n), lo, hi)
    x *= np.exp(-np.arange(n) / (0.008 * SR))
    return normalizar_pico(x, -6.0)


def sub(ini, fin, dur=1.5, semilla=5):
    """El peso: seno que baja de tono y crece (entra tarde, como en la referencia)."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    fase = 2 * np.pi * np.cumsum(np.linspace(ini, fin, n)) / SR
    x = np.sin(fase) * (1 - np.exp(-t / 0.06)) * np.exp(-t / (dur / 2.6))
    x = np.tanh(x * 2.2) * 0.8
    return normalizar_pico(x, -6.0)


def cola(tipo, brillo, dur=2.6, semilla=7):
    """La cola: lo que queda después del golpe."""
    rng = np.random.default_rng(semilla)
    n = int(dur * SR)
    t = np.arange(n) / SR
    if tipo == "chispa":
        x = banda(rng.standard_normal(n), 1200, 14000)
        # chispas sueltas que caen
        for _ in range(9):
            i = int(rng.uniform(0.05, 0.9) * SR)
            largo = int(rng.uniform(0.004, 0.02) * SR)
            if i + largo >= n:
                continue
            x[i:i + largo] += rng.standard_normal(largo) * 0.8
        x *= np.exp(-t / 0.5)
    elif tipo == "aire":
        x = banda(rng.standard_normal(n), 400, 9000)
        x *= np.exp(-t / 0.8)
        x *= 1 + 0.25 * np.sin(2 * np.pi * 3.1 * t)
    elif tipo == "cristal":
        x = banda(rng.standard_normal(n), 3000, 14000) * 0.5
        for f in (5200, 7100, 9300):
            x += 0.25 * np.sin(2 * np.pi * f * t) * np.exp(-t / 0.55)
        x *= np.exp(-t / 0.6)
    elif tipo == "metal":
        x = banda(rng.standard_normal(n), 600, 6000) * 0.4
        for f in (830, 1240, 1810):
            x += 0.3 * np.sin(2 * np.pi * f * t) * np.exp(-t / 0.45)
        x *= np.exp(-t / 0.55)
    else:  # rumor
        x = banda(rng.standard_normal(n), 40, 420)
        x *= np.exp(-t / 1.1)
    x = paso_bajo(x, min(brillo * 1.6, 15000))
    x = paso_alto(x, 35)
    return normalizar_pico(x, -9.0)


def main():
    os.makedirs(SALIDA, exist_ok=True)
    for nombre, p in ELEMENTOS.items():
        capas = {
            "Transitorio": transitorio(*p["transitorio"]),
            "Sub": sub(*p["sub"]),
            "Cola": cola(p["cola"], p["brillo"]),
        }
        for capa, x in capas.items():
            ruta = os.path.join(SALIDA, f"Capa_{capa}_{nombre}.wav")
            stereo = np.column_stack([x, x])
            escribir(ruta, stereo)
            print(f"  {os.path.basename(ruta):<38} {len(x) / SR:5.2f}s  pico -6 dBFS")
    print(f"\nCapas en {SALIDA}")


if __name__ == "__main__":
    main()
