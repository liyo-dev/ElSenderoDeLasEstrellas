"""Lanzamiento y estallido del choque final con el carácter del Dragon Slave (13 nov 2026, INC-1136).

Medido en la referencia de Raúl (`Escritorio/lina_conjuro.mp4`), solo como regla de medir; no se usa
ni un trozo de su audio:
  - el disparo es una pared con tono que SUBE: el pico pasa de ~1,2 kHz a ~1,65 kHz en 3 s, con el
    centro del espectro en ~3,3 kHz y el máximo de volumen hacia el final;
  - el impacto es un chasquido de banda ancha (centro ~4,4 kHz) con golpe grave en 170–280 Hz;
  - después, un rugido que NO decae durante ~10 s (nivel casi plano) y que se va aclarando:
    centro del espectro de ~1,1 kHz a ~1,9 kHz, con graves en 200–280 Hz por debajo.
Salen:
  - SFX_Lanzamiento_Conjuro (1,8 s): los dos orbes salen disparados; se corta al chocar (con el plin).
  - SFX_Estallido_Sube (v2, 9 s): chasquido + golpe + rugido sostenido que se aclara y sube.
"""
import numpy as np, os, sys
sys.path.insert(0, '/work/sfx')
from dsp import *
src = open('/work/sfx/disenar_dbz.py').read()
exec(src[src.index('def zap('):src.index('S={}')])
exec(src[src.index('def forcejeo('):src.index('dch=3.2')])
src3 = open('/work/sfx/disenar_dbz3.py').read()
exec(src3[src3.index('def chisporroteo('):src3.index('# ── Carga')])
OUT = '/work/sfx/out_sube'; os.makedirs(OUT, exist_ok=True)

def curva(y, g):
    g = np.asarray(g, float); g = np.pad(g, (0, max(0, len(y)-len(g))), mode='edge')[:len(y)]
    return y*g[:, None] if y.ndim == 2 else y*g

# ── Lanzamiento: pared tonal que sube ───────────────────────────────────────────────────
d = 1.8; n = int(d*SR); t = np.arange(n)/SR
k = np.clip(t/d, 0, 1)
tono = lambda tt: 1180*(1 + 0.4*np.clip(tt/d, 0, 1)**0.8)
pared_ = zumbido(lambda tt: tono(tt)/4, d, 7, 0.02, 600, 9000, 4.0)          # fundamental ~300 Hz, armónicos hasta arriba
canto = zumbido(tono, d, 3, 0.006, 900, 6000, 2.0)*0.5                        # el pico que se oye subir
cuerpo_g = osc(lambda tt: 210 + 50*np.clip(tt/d, 0, 1), d)*0.35
chis = chisporroteo(d, 801, 2500, 10000, (18, 40))
y = (pared_*0.9 + canto + cuerpo_g + chis*0.6)
env = np.clip(t/0.06, 0, 1)*(0.65 + 0.35*k**1.5)
y = flanger(y*env, 0.8, 0.0004, 0.003, mix_=0.4)
lanz = master(reverb(stereo(y, 0.7), 0.2, 1.5, seed=802)[:n], graves=0.5, corte=160)
lanz = curva(lanz, np.clip(t/0.06, 0, 1)*(0.65 + 0.35*k**1.5))
lanz[-int(0.006*SR):] *= np.linspace(1, 0, int(0.006*SR))[:, None]
write(f'{OUT}/SFX_Lanzamiento_Conjuro.wav', lanz, -1.0)

# ── Estallido v2: chasquido, golpe y rugido sostenido que se aclara ─────────────────────
de = 9.0; n = int(de*SR); t = np.arange(n)/SR
chasquido = bp(noise(0.7, 811), 1500, 14000, 2)*expdec(int(0.7*SR), 0.12, 0.0004)
golpe = boom_dbz(2.0, 812, 260, 120, 0.7)                                     # golpe en la zona de 170-280 Hz
abre = lambda tt: 1100 + 800*np.clip(tt/8.0, 0, 1)                            # el rugido se aclara
rugido_ = rugido(de, 90, 3000, (20, 34), 813, 1.0)
rugido_ = sweep_filter(rugido_, 1400, 5000, 'low', curve=0.8)
pared_ = zumbido(lambda tt: 240 + 60*np.clip(tt/8.0, 0, 1), de, 7, 0.025, 300, 7000, 4.0)
canto = zumbido(lambda tt: abre(tt), de, 3, 0.008, 900, 6000, 2.0)*np.clip((t-0.4)/1.2, 0, 1)
brillo = sweep_filter(hp(noise(de, 814), 800), 2500, 9000, 'low', curve=0.7)
chis = chisporroteo(de, 815, 1500, 9500, (14, 34))
cuerpo = rugido_*0.8 + pared_*0.8 + canto*0.35 + brillo*0.5 + chis*0.6
cuerpo = cuerpo*np.clip((t-0.05)/0.25, 0, 1)
y = mix((0, stereo(chasquido, 0.8), 1.0), (0, stereo(golpe, 0.4), 1.0), (0, stereo(cuerpo, 0.75), 1.0))
y = reverb(eco(y, 0.35, 0.3, 3, 3000), 0.35, 4.5, damp_end=1200, seed=816)[:n]
y = master(y, graves=0.75, corte=150, umbral_db=-24)
# nivel casi plano 7 s (como la referencia) y se va al final, debajo del fundido a blanco
y = curva(y, np.where(t < 7.0, 1.0, np.clip(1 - (t-7.0)/2.0, 0, 1)))
write(f'{OUT}/SFX_Estallido_Sube.wav', y, -0.8)
print('ok')
