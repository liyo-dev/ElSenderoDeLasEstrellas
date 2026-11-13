"""SFX de Amanecer, el hechizo final de Will (INC-724).

Riman con el final del prólogo: se montan sobre los sonidos estilo DBZ del choque (carga de ki,
disparo de luz e impacto, en Assets/Audio/SFX_Prologo/Disenados/) y les suman una capa propia
del amanecer: un acorde que sube como un coro y campanas claras que se abren.

  - SFX_Hechizo_Amanecer.wav: suena al empezar el gesto. Carga hasta que sale el hechizo
    (castDelaySeconds + chargeTime = 1,1 s) y después el disparo con el acorde abierto.
  - SFX_Impacto_Amanecer.wav: el golpe del choque, un chasquido de cristal (la protección que
    se agrieta) y campanas que se quedan sonando.
Salen en mono, como los demás hechizos. Uso: python3 disenar_amanecer.py (desde cualquier sitio).
"""
import os, sys
import numpy as np
from scipy.io import wavfile

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
from dsp import *

RAIZ = os.path.abspath(os.path.join(AQUI, '..', '..'))
PROLOGO = os.path.join(RAIZ, 'Assets', 'Audio', 'SFX_Prologo', 'Disenados')
SALIDA = os.path.join(RAIZ, 'Assets', 'Audio', 'SFX_Hechizos')
SALE = 1.1  # castDelaySeconds + chargeTime de Amanecer.asset


def leer(nombre):
    sr, x = wavfile.read(os.path.join(PROLOGO, nombre + '.wav'))
    x = x.astype(np.float64) / 32768.0
    assert sr == SR, (nombre, sr)
    return x if x.ndim == 2 else np.stack([x, x], 1)


def mono(y):
    return y.mean(1) if y.ndim == 2 else y


def chispazos(d, dens, seed, banda=(2000, 9000)):
    return bp(crackle(d, dens, seed), *banda) * 4


def coro(notas, d, seed=0):
    """Coro en «a»: sierras desafinadas por tres formantes."""
    t = t_(d); src = np.zeros(len(t))
    for i, f in enumerate(notas):
        for det in (-0.004, 0, 0.005):
            vib = 1 + 0.006 * np.sin(2 * np.pi * (5 + i * 0.3) * t + i + seed)
            src += osc(lambda tt, f=f, det=det, vib=vib: f * (1 + det) * vib, d, 'saw')
    y = np.zeros(len(t))
    for fc, bw in [(700, 130), (1220, 70), (2600, 160)]:
        y += bp(src, fc - bw, fc + bw, 2)
    return y


def campanas(notas, d, tau=1.4, retraso=0.06):
    y = np.zeros(int(d * SR))
    for i, f in enumerate(notas):
        b = fm_bell(f, d - i * retraso, ratio=3.01, index=2.5, tau=tau)
        o = int(i * retraso * SR); y[o:o + len(b)] += b
    return y


# Re mayor abierto: el mismo color luminoso en los dos sonidos.
ACORDE = [146.8, 220.0, 293.7, 370.0]
CAMPANAS = [587.3, 880.0, 1174.7, 1480.0]

# ── Lanzamiento ───────────────────────────────────────────────────────────────────────────
d = SALE + 1.8; n = int(d * SR); t = np.arange(n) / SR
carga = leer('SFX_Ki_Carga')
disparo = leer('SFX_Ki_Disparo_Luz')
sube = np.clip(t / SALE, 0, 1) ** 1.6
cae = np.where(t < SALE, 1.0, np.exp(-(t - SALE) / 0.9))
amanecer = coro(ACORDE, d) * sube * cae * env_adsr(n, 0.3, 0.1, 1, 0.4)
brillo = osc(lambda tt: 1200 * (1 + 0.5 * np.clip(tt / SALE, 0, 1)), d) * 0.08 * sube * cae
lanzamiento = mix((0, carga, 0.9),
                  (0, stereo(amanecer, 0.6), 0.55),
                  (0, stereo(brillo, 0.3), 1.0),
                  (SALE - 0.05, disparo, 1.0),
                  (SALE, stereo(campanas(CAMPANAS, 1.6, 1.0), 0.5), 0.35),
                  (SALE, stereo(chispazos(0.8, 300, 724) * expdec(int(0.8 * SR), 0.3), 0.7), 0.4))
lanzamiento = mono(reverb(lanzamiento, 0.22, 1.8, seed=724))
write(os.path.join(SALIDA, 'SFX_Hechizo_Amanecer.wav'), lanzamiento, -1.0)

# ── Impacto ───────────────────────────────────────────────────────────────────────────────
d = 2.6; n = int(d * SR)
golpe = leer('SFX_Impacto_DBZ')
cristal = bp(crackle(0.35, 900, 725), 3000, 12000) * 6 * expdec(int(0.35 * SR), 0.08, 0.001)
impacto = mix((0, golpe, 0.8),
              (0, stereo(cristal, 0.7), 0.6),
              (0.02, stereo(campanas(CAMPANAS, d, 1.2, 0.04), 0.5), 0.45),
              (0.0, stereo(coro(ACORDE, d, 3) * expdec(n, 0.7, 0.02), 0.6), 0.35))
impacto = mono(reverb(impacto, 0.3, 2.4, seed=725))
write(os.path.join(SALIDA, 'SFX_Impacto_Amanecer.wav'), impacto, -1.0)

for nombre in ('SFX_Hechizo_Amanecer', 'SFX_Impacto_Amanecer'):
    sr, x = wavfile.read(os.path.join(SALIDA, nombre + '.wav'))
    print(nombre, round(len(x) / sr, 2), 's')
