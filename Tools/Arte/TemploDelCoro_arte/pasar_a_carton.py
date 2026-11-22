# Pase de cartón para los recortes del Templo del Coro: mismo lienzo y mismo pivote que el blockout,
# con canto de papel crema cortado a tijera, sombra dura desplazada, grano de cartón y luz de arriba.
# Uso: python3 pasar_a_carton.py <carpeta de recortes> <carpeta de salida>
import sys, glob, os
import numpy as np
from PIL import Image, ImageFilter

ORIGEN, DESTINO = sys.argv[1], sys.argv[2]
CANTO = (243, 230, 200)
SOMBRA = (18, 10, 24)
SIN_CANTO = ('corista_burbuja_', 'sombra_de_nota', 'corista_boca')   # efectos y piezas que van dentro de otra

def ruido(w, h, escala, semilla):
    rng = np.random.default_rng(semilla)
    pw, ph = max(2, w // escala + 2), max(2, h // escala + 2)
    base = Image.fromarray((rng.random((ph, pw)) * 255).astype(np.uint8))
    return np.asarray(base.resize((w, h), Image.BICUBIC), dtype=np.float32) / 255.0

def dilatar(alfa, r):
    return alfa.filter(ImageFilter.MaxFilter(2 * r + 1))

for ruta in sorted(glob.glob(os.path.join(ORIGEN, '*.png'))):
    nombre = os.path.basename(ruta)
    im = Image.open(ruta).convert('RGBA')
    w, h = im.size
    semilla = sum(map(ord, nombre))
    rgb = np.asarray(im, dtype=np.float32)
    a = rgb[..., 3] / 255.0

    # Grano de cartón (dos escalas) y fibras horizontales suaves.
    g = 0.6 * ruido(w, h, 6, semilla) + 0.4 * ruido(w, h, 2, semilla + 1)
    fibra = ruido(w, h // 1, 1, semilla + 2)
    fibra = np.asarray(Image.fromarray((fibra * 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(0)).resize((w, max(2, h // 3))).resize((w, h)), dtype=np.float32) / 255
    factor = 1.0 + (g - 0.5) * 0.10 + (fibra - 0.5) * 0.05
    # Luz de arriba: arriba un pelo más clara que abajo, muy suave.
    ys = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    factor *= 1.06 - 0.11 * ys
    color = np.clip(rgb[..., :3] * factor[..., None], 0, 255)
    pieza = np.dstack([color, rgb[..., 3]]).astype(np.uint8)
    pieza = Image.fromarray(pieza, 'RGBA')

    if nombre.startswith(SIN_CANTO):
        pieza.save(os.path.join(DESTINO, nombre)); continue

    alfa = im.getchannel('A').point(lambda v: 255 if v > 40 else 0)
    r = max(5, min(9, round(min(w, h) / 60)))
    canto = dilatar(alfa, r)
    # Borde de tijera: el canto se come a trozos con ruido de baja frecuencia.
    borde = np.asarray(canto.filter(ImageFilter.GaussianBlur(r * 0.6)), dtype=np.float32) / 255
    corte = borde + (ruido(w, h, 18, semilla + 3) - 0.5) * 0.35
    canto_a = ((corte > 0.5) | (np.asarray(dilatar(alfa, 2)) > 0)).astype(np.uint8) * 255
    canto_img = Image.new('RGBA', (w, h), CANTO + (0,))
    canto_img.putalpha(Image.fromarray(canto_a))
    # Sombra dura desplazada abajo a la derecha.
    dx, dy = round(r * 0.8), round(r * 0.9)
    sombra_a = Image.new('L', (w, h), 0)
    sombra_a.paste(Image.fromarray(canto_a), (dx, dy))
    sombra = Image.new('RGBA', (w, h), SOMBRA + (0,))
    sombra.putalpha(sombra_a.point(lambda v: v * 0.55))
    salida = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    salida.alpha_composite(sombra); salida.alpha_composite(canto_img); salida.alpha_composite(pieza)
    salida.save(os.path.join(DESTINO, nombre))
print('hecho')
