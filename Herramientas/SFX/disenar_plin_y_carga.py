"""Plin del choque y carga de la plegaria (13 nov 2026, INC-1136).

Raúl: al chocar los dos hechizos, «un destello (plin) que invade la pantalla»; y durante el conjuro
del Archimago «falta un sfx de carga, pero que no pise el audio y la voz».
  - Plin (1,8 s): campanada brillante muy aguda con un brillo que se abre hacia arriba.
  - Carga_Plegaria (35 s): cama que crece despacio durante todo el conjuro. Deja libre la zona de la
    voz (300 Hz–4 kHz): solo graves profundos que laten y aire brillante por encima de 5 kHz, con
    chispitas sueltas. Sube hasta la última estrofa y se aparta en la despedida («Perdóname, Liora»).
"""
import numpy as np, os, sys
sys.path.insert(0, '/work/sfx')
from dsp import *
OUT = '/work/sfx/out_sube'; os.makedirs(OUT, exist_ok=True)

def curva(y, g):
    g = np.asarray(g, float); g = np.pad(g, (0, max(0, len(y)-len(g))), mode='edge')[:len(y)]
    return y*g[:, None] if y.ndim == 2 else y*g

# ── Plin ────────────────────────────────────────────────────────────────────────────────
d = 1.8; n = int(d*SR); t = np.arange(n)/SR
campana = (fm_bell(2637, d, 3.5, 3.0, 0.9)*0.8 + fm_bell(3951, d, 2.0, 2.5, 0.7)*0.6 + fm_bell(5274, d, 1.5, 1.5, 0.5)*0.35)
brillo = sweep_filter(hp(noise(d, 701), 3000), 4000, 14000, 'low', curve=0.4)*expdec(n, 0.35, 0.002)
ataque = bp(noise(0.05, 702), 3000, 12000)*expdec(int(0.05*SR), 0.01, 0.0005)
y = campana + brillo*0.5
y[:len(ataque)] += ataque*0.8
plin = reverb(stereo(y, 0.6), 0.35, 2.2, seed=703)
write(f'{OUT}/SFX_Plin.wav', plin, -1.0)

# ── Carga de la plegaria ────────────────────────────────────────────────────────────────
d = 35.0; n = int(d*SR); t = np.arange(n)/SR
sube = np.clip(t/30.0, 0, 1)**1.3
latido = 0.6 + 0.4*np.sin(2*np.pi*(0.35 + 0.5*sube)*t)**2          # respira cada vez más deprisa
grave = (osc(lambda tt: 42 + 14*np.clip(tt/30, 0, 1), d)*0.8 + osc(lambda tt: 84 + 28*np.clip(tt/30, 0, 1), d)*0.25)
grave = lp(grave, 160)*latido
aire = hp(noise(d, 711), 5500)
aire = sweep_filter(aire, 7000, 13000, 'low', curve=1.0)*(0.5 + 0.5*np.sin(2*np.pi*0.18*t + 1.0)**2)
chispas = hp(crackle(d, 10, 712), 5000)*4
y = grave*0.9 + aire*0.35*(0.3 + 0.7*sube) + chispas*0.25*(0.2 + 0.8*sube)
# envolvente: entra suave, crece hasta la última estrofa, se aparta en la despedida y se va antes del grito
env = np.clip(t/3.0, 0, 1)*(0.35 + 0.65*sube)
env = np.where(t < 30.3, env, env*np.clip(1 - (t-30.3)/0.6, 0.18, 1))
env = np.where(t < 33.5, env, env*np.clip(1 - (t-33.5)/1.0, 0, 1))
y = y*env
y = reverb(stereo(y, 0.8), 0.3, 3.0, seed=713)[:n]
y = curva(y, np.where(t < 34.0, 1.0, np.clip(1 - (t-34.0)/1.0, 0, 1)))
# deja libre la voz: corta lo que quede entre 300 Hz y 4,5 kHz
for c in range(y.shape[1]):
    medio = bp(y[:, c], 300, 4500, 2)
    y[:, c] = y[:, c] - medio*0.85
write(f'{OUT}/SFX_Carga_Plegaria.wav', y, -3.0)
print('ok')
