"""Comparativa del bocadillo de diálogo (SpeechBubbleUI): el de ahora y el nuevo.

El bocadillo nuevo lo dibuja el propio juego por código (Assets/Scripts/UI/SpeechBubbleUI.cs,
método PintarBocadillo). Tools/UI/bocadillo_papel.py es una traducción literal de ese
método a Python, para poder ver el resultado sin abrir Unity: si cambias el C#, cambia
ahí también.

Uso:
    python Tools/UI/bocadillo_preview.py [carpeta_destino]
"""

import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bocadillo_papel as papel  # noqa: E402

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FUENTE = os.path.join(RAIZ, "Assets", "Plugins", "Fonts", "Nunito-Bold.ttf")
SPRITE_ACTUAL = os.path.join(
    RAIZ, "Assets", "Art", "2D Atlas Speechbubbles Alphabet Numbers",
    "Assets", "Sprites", "Speechbubble_0006.png")

TEXTO = "Ya están otra vez...\nVoy a ver qué pasa."
ESCENA_W, ESCENA_H = 1920, 1080


# ── El bocadillo de ahora: sprite del atlas en 9-slice, como lo monta Unity ───

def nueve_slice(origen, bordes, destino_wh):
    """Renderiza un sprite con bordes de 9-slice al tamaño pedido, como Unity."""
    izq, aba, der, arr = bordes
    sw, sh = origen.size
    w, h = destino_wh

    sx = np.where(np.arange(w) < izq, np.arange(w),
                   np.where(np.arange(w) < w - der,
                            izq + (np.arange(w) - izq) * (sw - izq - der) / max(1, w - izq - der),
                            sw - (w - np.arange(w))))
    sy = np.where(np.arange(h) < arr, np.arange(h),
                   np.where(np.arange(h) < h - aba,
                            arr + (np.arange(h) - arr) * (sh - arr - aba) / max(1, h - arr - aba),
                            sh - (h - np.arange(h))))
    sx = np.clip(sx, 0, sw - 1).astype(np.int32)
    sy = np.clip(sy, 0, sh - 1).astype(np.int32)

    # Unity importa con alphaIsTransparency: el RGB de los píxeles casi transparentes se
    # dilata hacia el color opaco para que no salga un borde negro.
    rgb = np.asarray(origen.convert("RGBA"))[..., :3]
    alpha = np.asarray(origen.convert("RGBA"))[..., 3]
    rgb = np.asarray(Image.fromarray(rgb).filter(ImageFilter.MaxFilter(5)))
    src = np.concatenate([rgb, alpha[..., None]], axis=2)
    return Image.fromarray(src[sy[:, None], sx[None, :]])


def bocadillo_actual(fuente):
    """El bocadillo de hoy: Image de 420x306 con el sprite 0006 y el padding del prefab."""
    sprite = Image.open(SPRITE_ACTUAL)
    # Bordes del import: izquierda 56, abajo 96, derecha 56, arriba 36.
    ancho_img, alto_img = 420, 306
    relleno = nueve_slice(sprite, (56, 96, 56, 36), (ancho_img, alto_img))

    texto = Image.new("RGBA", (ancho_img, alto_img), (0, 0, 0, 0))
    d = ImageDraw.Draw(texto)
    for i, linea in enumerate(TEXTO.split("\n")):
        w = d.textlength(linea, font=fuente)
        d.text(((ancho_img - w) / 2, 150 + i * 30), linea, font=fuente, fill=(31, 31, 31))
    relleno.alpha_composite(texto)
    return relleno


# ── Fondo de la maqueta ─────────────────────────────────────────────────────

def escena():
    """Sustituto de la habitación: colores del juego (pared teal, madera, farol)."""
    img = Image.new("RGB", (ESCENA_W, ESCENA_H), (38, 44, 46))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 120, ESCENA_W, 700], fill=(46, 122, 118))
    d.rectangle([0, 700, ESCENA_W, ESCENA_H], fill=(122, 78, 40))
    for y in range(700, ESCENA_H, 64):
        d.rectangle([0, y, ESCENA_W, y + 3], fill=(96, 60, 30))
    d.rectangle([0, 0, ESCENA_W, 120], fill=(38, 44, 46))
    halo = Image.new("RGB", (ESCENA_W, ESCENA_H), (0, 0, 0))
    ImageDraw.Draw(halo).ellipse([1520, 200, 1880, 540], fill=(255, 210, 120))
    halo = halo.filter(ImageFilter.GaussianBlur(150))
    return Image.composite(Image.blend(img, halo, 0.35), img,
                           halo.convert("L").point(lambda v: 255 if v > 12 else 0))


# ── Composición ─────────────────────────────────────────────────────────────

def bocadillo_nuevo(texto, con_cola=True):
    """El papel tal y como lo dibuja el juego, con el texto dentro."""
    img, (_, _, ancho, alto) = papel.montar_bocadillo(texto, con_cola=con_cola)
    d = ImageDraw.Draw(img)
    fuente = ImageFont.truetype(FUENTE, papel.TAMANO_TEXTO)
    lineas = texto.split("\n")
    alto_linea = papel.TAMANO_TEXTO * 1.28
    for i, linea in enumerate(lineas):
        w = d.textlength(linea, font=fuente)
        d.text(((ancho - w) / 2, (alto - alto_linea * len(lineas)) / 2 + i * alto_linea),
               linea, font=fuente, fill=(36, 31, 26))
    return img


def main():
    destino = sys.argv[1] if len(sys.argv) > 1 else os.path.join(RAIZ, "Temp", "UI")
    os.makedirs(destino, exist_ok=True)
    fondo = escena()
    fuente_chico = ImageFont.truetype(FUENTE, 22)

    # 1) El de ahora, tal cual sale en el juego.
    img = fondo.convert("RGBA")
    viejo = bocadillo_actual(fuente_chico)
    img.alpha_composite(viejo, (960 - viejo.width // 2, 620 - 306 + 96))
    antes = img.crop((620, 340, 1300, 700))

    # 2) El nuevo.
    img = fondo.convert("RGBA")
    nuevo = bocadillo_nuevo(TEXTO)
    img.alpha_composite(nuevo, (960 - nuevo.width // 2, 620 - nuevo.height))
    despues = img.crop((620, 340, 1300, 700))

    ancho_col, alto_col = 680, 360
    hoja = Image.new("RGB", (ancho_col * 2, alto_col), (24, 26, 28))
    d = ImageDraw.Draw(hoja)
    etiquetas = ImageFont.truetype(FUENTE, 26)
    for i, (titulo, recorte) in enumerate((("AHORA", antes), ("NUEVO", despues))):
        hoja.paste(recorte.resize((ancho_col, alto_col), Image.LANCZOS), (i * ancho_col, 0))
        d.text((i * ancho_col + 12, 10), titulo, font=etiquetas, fill=(240, 220, 150))

    ruta = os.path.join(destino, "bocadillo_comparativa.png")
    hoja.save(ruta)
    print("Comparativa:", ruta)

    # Y las medidas del papel nuevo, para tenerlas a mano.
    for caso in (TEXTO, "¡Ese es! ¡Así se le hace daño!", "Sí.",
                 "Eldran guía la SHA"):
        img, (_, _, ancho, alto) = papel.montar_bocadillo(caso)
        print(f"  {ancho}x{alto} px · «{caso.splitlines()[0][:40]}»")


if __name__ == "__main__":
    main()
