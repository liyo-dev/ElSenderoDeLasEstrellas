# Texturas que repiten (512 px) para el decorado del Templo del Coro: tablas del escenario,
# muro de cartón pintado, terciopelo del telón y madera oscura de la tramoya.
# Uso: python3 texturas_del_teatro.py <carpeta de salida>
import sys, os
import numpy as np
from PIL import Image

N = 512
SAL = sys.argv[1]
rng = np.random.default_rng(7)

def ruido(periodo):
    # Ruido que repite: rejilla aleatoria periódica interpolada (coseno).
    g = rng.random((periodo, periodo))
    t = np.arange(N) * periodo / N
    i0 = np.floor(t).astype(int); f = t - i0; f = (1 - np.cos(f * np.pi)) / 2
    i1 = (i0 + 1) % periodo
    fil = g[i0][:, i0] * (1 - f)[None, :] + g[i0][:, i1] * f[None, :]
    fil2 = g[i1][:, i0] * (1 - f)[None, :] + g[i1][:, i1] * f[None, :]
    return fil * (1 - f)[:, None] + fil2 * f[:, None]

def fbm(*periodos):
    s = sum(ruido(p) / (k + 1) for k, p in enumerate(periodos))
    s -= s.min(); return s / s.max()

def guardar(nombre, rgb):
    Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), 'RGB').save(os.path.join(SAL, nombre))

y, x = np.mgrid[0:N, 0:N] / N

# Tablas: 4 tablas a lo largo de X, veta a lo largo de Y, juntas oscuras, clavos y tono distinto por tabla.
tabla = np.floor(x * 4).astype(int)
tono = np.array([1.0, 0.93, 1.05, 0.97])[tabla]
veta = np.sin((x * 4 * 6 + fbm(4, 8) * 3) * np.pi * 2) * 0.5 + 0.5
base = np.array([156, 104, 62]) * (0.88 + 0.12 * veta[..., None]) * tono[..., None]
base *= (0.92 + 0.16 * fbm(8, 32, 64))[..., None]
junta = np.minimum((x * 4) % 1, 1 - (x * 4) % 1) < 0.012
base[junta] = [58, 34, 22]
for cx in (0.125, 0.375, 0.625, 0.875):
    for cy in (0.06, 0.56):
        m = ((x - cx) ** 2 + (y - cy) ** 2) < 0.006 ** 2
        base[m] = [70, 60, 58]
guardar('tablas_escenario.png', base)

# Madera oscura (tramoya, practicables).
veta2 = np.sin((y * 10 + fbm(4, 16) * 2.5) * np.pi * 2) * 0.5 + 0.5
guardar('madera_oscura.png', np.array([98, 62, 40]) * (0.85 + 0.15 * veta2[..., None]) * (0.9 + 0.2 * fbm(8, 64))[..., None])

# Muro de cartón pintado: malva noche con pinceladas horizontales y la onda del cartón ondulado.
pincel = fbm(2, 16, 128)
pincel = np.asarray(Image.fromarray((pincel * 255).astype(np.uint8)).resize((N, N // 8)).resize((N, N), Image.BICUBIC)) / 255
onda = np.sin(x * np.pi * 2 * 24) * 0.5 + 0.5
muro = np.array([84, 70, 102]) * (0.86 + 0.18 * pincel[..., None]) * (0.97 + 0.03 * onda[..., None])
guardar('muro_de_carton.png', muro)

# Terciopelo: pliegues verticales (8 por textura) con brillo en la cresta.
pliegue = np.sin(x * np.pi * 2 * 8 + fbm(2, 4) * 1.5)
luz = 0.55 + 0.35 * pliegue + 0.25 * np.clip(pliegue, 0.75, 1) * 4 - 0.75
tela = np.array([150, 28, 46]) * luz[..., None] * (0.94 + 0.08 * fbm(32, 128))[..., None]
guardar('terciopelo.png', tela)
print('hecho')
