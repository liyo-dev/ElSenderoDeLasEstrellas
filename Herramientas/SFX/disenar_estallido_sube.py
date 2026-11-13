"""Estallido final del prólogo que SUBE (13 nov 2026, INC-1136).

Raúl: el estallido de antes «se cae en lugar de ir hacia arriba» y «tarda mucho en explotar».
Referencia: el final del Dragon Slave de Slayers (`Escritorio/lina_conjuro.mp4`): golpe, cúpula que
se abre y una columna de energía que sube con un rugido que crece. Aquí:
  - Previa_Estallido (1,4 s): aspiración que sube de tono y se corta en seco en el golpe; se lanza
    1,4 s antes del estallido para que el choque «llegue» en vez de hacerse esperar.
  - Estallido_Sube (~8 s): golpe seco + pared de energía cuyo tono SUBE (110 → 520 Hz) y se abre de
    brillo hacia arriba, chisporroteo que se espesa, coro en lo alto y cola larga.
Usa las capas de disenar_dbz.py y disenar_dbz3.py (zumbido, chisporroteo, master, boom_dbz…).
"""
import numpy as np, os, sys
sys.path.insert(0, '/work/sfx')
from dsp import *
src = open('/work/sfx/disenar_dbz.py').read()
exec(src[src.index('def zap('):src.index('S={}')])
exec(src[src.index('def forcejeo('):src.index('dch=3.2')])
src3 = open('/work/sfx/disenar_dbz3.py').read()
exec(src3[src3.index('def chisporroteo('):src3.index('# ── Carga')] if '# ── Carga' in src3 else src3[src3.index('def chisporroteo('):src3.index('def pared(')])
OUT = '/work/sfx/out_sube'; os.makedirs(OUT, exist_ok=True)


def curva(y, g):
    g = np.asarray(g, float); g = np.pad(g, (0, max(0, len(y)-len(g))), mode='edge')[:len(y)]
    return y*g[:, None] if y.ndim == 2 else y*g


# ── Previa: aspiración que sube y se corta en el golpe ─────────────────────────────────
dp = 1.4; n = int(dp*SR); t = np.arange(n)/SR
k = (t/dp)**2.2
tono = zumbido(lambda tt: 160*(1+5.5*(tt/dp)**2.2), dp, 5, 0.015, 400, 8000, 3.5)
aire = sweep_filter(noise(dp, 601), 300, 9000, 'low', curve=0.6)
aire = hp(aire, 250)
chis = chisporroteo(dp, 602, 2500, 9500, (20, 40))
previa = (tono*0.7 + aire*1.0 + chis*0.6)*(0.05 + 0.95*k)
previa = master(reverb(stereo(previa, 0.7), 0.15, 1.0, seed=603), graves=0.4)
previa = curva(previa[:n], 0.05 + 0.95*k)
# corte en seco al final (el golpe del estallido entra justo aquí)
previa[-int(0.004*SR):] *= np.linspace(1, 0, int(0.004*SR))[:, None]

# ── Estallido que sube ─────────────────────────────────────────────────────────────────
de = 8.5; n = int(de*SR); t = np.arange(n)/SR
golpe = boom_dbz(1.6, 611, 150, 45, 0.5)
rafaga = bp(noise(0.6, 612), 800, 11000, 2)*expdec(int(0.6*SR), 0.18, 0.0005)
sube = lambda tt: 110*(520/110)**np.clip(tt/3.2, 0, 1)**0.8
pared_ = zumbido(sube, de, 7, 0.02, 250, 8000, 4.0)
pared2 = zumbido(lambda tt: sube(tt)*1.5*(1+0.02*np.sin(2*np.pi*6*tt)), de, 5, 0.025, 600, 9000, 3.2)
brillo = sweep_filter(noise(de, 613), 500, 10000, 'low', curve=0.5)
brillo = hp(brillo, 300)
chis = chisporroteo(de, 614, 1500, 9500, (14, 38))*np.clip(t/2.5, 0.3, 1)
coro_ = coro([440, 587.3, 880], de, 'a', 615)*np.clip((t-1.2)/1.6, 0, 1)
cuerpo = (pared_*0.9 + pared2*0.55 + brillo*0.9 + chis*0.8 + coro_*0.35)
# entra a la vez que el golpe, crece hasta los 2,5 s y se sostiene antes de irse
env = np.clip(t/0.05, 0, 1)*(0.7 + 0.3*np.clip(t/2.5, 0, 1))*np.where(t < 4.2, 1.0, np.exp(-(t-4.2)/1.8))
cuerpo = flanger(cuerpo*env, 0.35, 0.0005, 0.004, mix_=0.35)
y = mix((0, stereo(golpe, 0.4), 1.0), (0, stereo(rafaga, 0.8), 0.9), (0, stereo(cuerpo, 0.75), 1.0))
y = reverb(eco(y, 0.4, 0.3, 3, 3000), 0.35, 5.0, damp_end=1200, seed=616)
y = master(y, graves=0.7, corte=150, umbral_db=-24)
y = curva(y, np.where(t < 4.2, 0.85 + 0.15*np.clip(t/2.5, 0, 1), np.exp(-(t-4.2)/1.8)))

write(f'{OUT}/SFX_Previa_Estallido.wav', previa, -1.0)
write(f'{OUT}/SFX_Estallido_Sube.wav', y, -0.8)
print('ok')
