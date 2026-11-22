# Detalle de piedra para las piezas del Tenor (cara, mandíbula, cejas, manos): grietas de tinta,
# motas, desconchones claros y musgo en la parte de abajo. Solo pinta sobre el gris de la piedra,
# así que no toca ojos, bigote, boca ni la tinta del contorno. Mismo lienzo y pivote.
# Uso: python3 detalle_de_piedra.py <carpeta origen> <carpeta salida>
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ORIGEN, DESTINO = sys.argv[1], sys.argv[2]
PIEZAS = ['tenor_cara', 'tenor_mandibula', 'tenor_ceja_izq', 'tenor_ceja_der', 'tenor_mano_izq', 'tenor_mano_der', 'organo', 'atrezo_columna']
TINTA = (42, 34, 48)
MUSGO = [(96, 128, 78), (122, 152, 92), (78, 106, 66)]

def mascara_de_piedra(rgb, a):
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    gris = (abs(r - g) < 22) & (abs(b - g) < 30) & (g > 85) & (g < 215)
    return gris & (a > 200)

for nombre in PIEZAS:
    im = Image.open(os.path.join(ORIGEN, nombre + '.png')).convert('RGBA')
    w, h = im.size
    arr = np.asarray(im)
    piedra = mascara_de_piedra(arr[..., :3], arr[..., 3])
    if not piedra.any():
        im.save(os.path.join(DESTINO, nombre + '.png')); continue
    rng = np.random.default_rng(sum(map(ord, nombre)))
    capa = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(capa)
    ys, xs = np.nonzero(piedra)
    area = len(xs)
    esc = math.sqrt(area) / 300  # tamaño relativo de la pieza
    # Grietas: caminos quebrados que se ramifican.
    for _ in range(max(2, int(area / 200000)) + 1):
        i = rng.integers(area); x, y = float(xs[i]), float(ys[i])
        ang = rng.uniform(0, math.tau); grosor = max(2, round(5 * esc))
        for paso in range(rng.integers(4, 9)):
            l = rng.uniform(14, 40) * max(esc, 0.5)
            ang += rng.uniform(-0.8, 0.8)
            nx, ny = x + math.cos(ang) * l, y + math.sin(ang) * l
            d.line([(x, y), (nx, ny)], fill=TINTA + (230,), width=grosor)
            if rng.random() < 0.3:
                a2 = ang + rng.choice([-1, 1]) * 0.9
                d.line([(nx, ny), (nx + math.cos(a2) * l * 0.6, ny + math.sin(a2) * l * 0.6)], fill=TINTA + (200,), width=max(1, grosor - 1))
            x, y = nx, ny
            grosor = max(1, grosor - (1 if rng.random() < 0.4 else 0))
    # Desconchones claros con su sombra.
    for _ in range(int(area / 60000) + 2):
        i = rng.integers(area); x, y = xs[i], ys[i]; r = rng.uniform(6, 16) * max(esc, 0.6)
        pts = [(x + math.cos(t) * r * rng.uniform(0.6, 1.1), y + math.sin(t) * r * 0.7 * rng.uniform(0.6, 1.1)) for t in np.linspace(0, math.tau, 7)[:-1]]
        d.polygon([(px + 3, py + 3) for px, py in pts], fill=(70, 62, 80, 150))
        d.polygon(pts, fill=(205, 198, 212, 230))
    # Motas.
    for _ in range(int(area / 900)):
        i = rng.integers(area)
        c = (60, 52, 70, 120) if rng.random() < 0.6 else (225, 220, 230, 120)
        r = rng.uniform(1, 3)
        d.ellipse([xs[i] - r, ys[i] - r, xs[i] + r, ys[i] + r], fill=c)
    # Musgo en la parte de abajo de la piedra: manchas redondeadas en racimo.
    fondo = np.percentile(ys, 88)
    abajo = np.nonzero(ys > fondo)[0]
    for _ in range(int(area / 90000) + 2):
        i = abajo[rng.integers(len(abajo))]; cx, cy = xs[i], ys[i]
        for _ in range(rng.integers(3, 6)):
            r = rng.uniform(5, 14) * max(esc, 0.6)
            ox, oy = rng.normal(0, r * 1.2), rng.normal(0, r * 0.5)
            col = MUSGO[rng.integers(3)]
            d.ellipse([cx + ox - r, cy + oy - r * 0.7, cx + ox + r, cy + oy + r * 0.7], fill=col + (235,))
    # Solo donde hay piedra.
    capa_arr = np.asarray(capa).copy()
    m = np.asarray(Image.fromarray((piedra * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(5))) > 0
    capa_arr[..., 3] = np.where(m, capa_arr[..., 3], 0)
    salida = im.copy(); salida.alpha_composite(Image.fromarray(capa_arr))
    salida.save(os.path.join(DESTINO, nombre + '.png'))
print('hecho')
