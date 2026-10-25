"""Simulación en Python del papel que dibuja SpeechBubbleUI por código.

Es una traducción literal de PintarBocadillo() (Assets/Scripts/UI/SpeechBubbleUI.cs)
para poder ver el resultado sin abrir Unity: si cambia el C#, hay que cambiar esto.

Ojo con el eje vertical: la textura de Unity crece hacia arriba (la fila 0 es la de
abajo). Al guardar con PIL hay que voltear la imagen.

Uso:
    python Tools/UI/bocadillo_papel.py
"""

import os
import random

import numpy as np
from PIL import Image, ImageFont

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FUENTE = os.path.join(RAIZ, "Assets", "Plugins", "Fonts", "Nunito-Bold.ttf")

# Valores por defecto de los campos serializados de SpeechBubbleUI.
BORDE = 3.0
MARGEN_TEXTO = 14.0
RADIO_ESQUINA = 26.0
COLA = (52.0, 26.0)
SOLAPE_COLA = 8.0
DESDIBUJO = 1.6
COLOR_PAPEL = (0.965, 0.914, 0.82)
COLOR_BORDE = (0.38, 0.30, 0.23)
COLOR_SOMBRA = (0.10, 0.08, 0.06)
RADIO_SOMBRA = 9.0
SOMBRA_DESPLAZAMIENTO = (2.0, -6.0)
SOMBRA_OPACIDAD = 0.3
TAMANO_TEXTO = 26
ANCHO_MIN_PAPEL = 220.0
ANCHO_MAX_PAPEL = 560.0
PASO_DE_ANCHO = 8.0
TOPE_DE_SPRITES = 64
LADO_RUIDO = 32


def margen_de_sombra():
    return RADIO_SOMBRA + max(abs(SOMBRA_DESPLAZAMIENTO[0]), abs(SOMBRA_DESPLAZAMIENTO[1]))


def tabla_ruido(semilla=20261022):
    """Mismo papel que la tabla del C#: valores al azar con semilla fija."""
    rnd = random.Random(semilla)
    return np.array([rnd.random() * 2.0 - 1.0 for _ in range(LADO_RUIDO * LADO_RUIDO)],
                    dtype=np.float32)


RUIDO = tabla_ruido()


def muestra_ruido(x, y):
    """Muestreo bilineal con suavizado, como MuestraRuido() del C#."""
    x0 = np.floor(x).astype(np.int32)
    y0 = np.floor(y).astype(np.int32)
    fx = x - x0
    fy = y - y0
    fx = fx * fx * (3.0 - 2.0 * fx)
    fy = fy * fy * (3.0 - 2.0 * fy)
    m = LADO_RUIDO - 1
    x0m, x1m = x0 & m, (x0 + 1) & m
    y0m, y1m = y0 & m, (y0 + 1) & m
    v00 = RUIDO[y0m * LADO_RUIDO + x0m]
    v10 = RUIDO[y0m * LADO_RUIDO + x1m]
    v01 = RUIDO[y1m * LADO_RUIDO + x0m]
    v11 = RUIDO[y1m * LADO_RUIDO + x1m]
    return (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy


def distancia_rect_redondeado(x, y, cx, cy, hw, hh, r):
    qx = np.abs(x - cx) - (hw - r)
    qy = np.abs(y - cy) - (hh - r)
    dx = np.maximum(qx, 0.0)
    dy = np.maximum(qy, 0.0)
    return np.hypot(dx, dy) + np.minimum(np.maximum(qx, qy), 0.0) - r


def distancia_medio_plano(x, y, p, q):
    ex, ey = q[0] - p[0], q[1] - p[1]
    largo = max(1e-5, (ex * ex + ey * ey) ** 0.5)
    return (ex * (y - p[1]) - ey * (x - p[0])) / largo


def distancia_triangulo(x, y, a, b, c):
    area = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    signo = 1.0 if area >= 0 else -1.0
    h1 = signo * distancia_medio_plano(x, y, a, b)
    h2 = signo * distancia_medio_plano(x, y, b, c)
    h3 = signo * distancia_medio_plano(x, y, c, a)
    return -np.minimum(np.minimum(h1, h2), h3)


def distancia_papel(fx, fy, cx, cy, hw, hh, radio,
                     cola_izq, cola_der, cola_base, cola_punta, con_cola):
    d = distancia_rect_redondeado(fx, fy, cx, cy, hw, hh, radio)
    if con_cola:
        dt = distancia_triangulo(fx, fy, (cola_izq, cola_base), (cx, cola_punta),
                                  (cola_der, cola_base))
        d = np.minimum(d, dt)
    return d + muestra_ruido(fx * 0.055, fy * 0.055) * DESDIBUJO


def pintar_bocadillo(ancho, alto, con_cola=True):
    """Traducción de PintarBocadillo() del C#: devuelve una imagen RGBA (origen abajo)."""
    x, y = np.meshgrid(np.arange(ancho, dtype=np.float32) + 0.5,
                       np.arange(alto, dtype=np.float32) + 0.5)

    borde = BORDE
    margen = margen_de_sombra()
    ancho_papel = max(4.0, ancho - 2.0 * margen)
    alto_papel = max(4.0, alto - 2.0 * margen - (COLA[1] if con_cola else 0.0))

    x0, x1 = margen, margen + ancho_papel
    y1 = alto - margen
    y0 = y1 - alto_papel
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    hw, hh = ancho_papel * 0.5, alto_papel * 0.5
    radio = min(RADIO_ESQUINA, hw, hh)

    cola_punta = margen if con_cola else -1000.0
    cola_base = y0 + (SOLAPE_COLA if con_cola else 0.0)
    cola_izq, cola_der = cx - COLA[0] * 0.5, cx + COLA[0] * 0.5

    d = distancia_papel(x, y, cx, cy, hw, hh, radio,
                        cola_izq, cola_der, cola_base, cola_punta, con_cola)
    ds = distancia_papel(x - SOMBRA_DESPLAZAMIENTO[0], y - SOMBRA_DESPLAZAMIENTO[1],
                         cx, cy, hw, hh, radio,
                         cola_izq, cola_der, cola_base, cola_punta, con_cola)
    a_sombra = np.clip((RADIO_SOMBRA - ds) / max(0.001, RADIO_SOMBRA), 0.0, 1.0)
    a_sombra = a_sombra * a_sombra * SOMBRA_OPACIDAD

    a_relleno = np.clip(0.5 - d, 0.0, 1.0)
    a_borde = np.clip((borde - d) / borde, 0.0, 1.0)
    a_papel = np.maximum(a_relleno, a_borde)

    rgba = np.zeros((alto, ancho, 4), dtype=np.float32)
    for i in range(3):
        color = COLOR_BORDE[i] * (1.0 - a_relleno) + COLOR_PAPEL[i] * a_relleno
        rgba[..., i] = COLOR_SOMBRA[i] * (1.0 - a_papel) + color * a_papel
    rgba[..., 3] = a_papel + a_sombra * (1.0 - a_papel)

    return Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA")


def medida_texto(texto, tam=TAMANO_TEXTO):
    """Ancho y alto que TMP daría al texto (Nunito Bold)."""
    fuente = ImageFont.truetype(FUENTE, tam)
    lineas = texto.split("\n")
    ancho = max(fuente.getlength(l) for l in lineas)
    alto = int(round(tam * 1.28 * len(lineas)))
    return ancho, alto, lineas


def montar_bocadillo(texto, tam=TAMANO_TEXTO, con_cola=True):
    """Devuelve (imagen del papel ya volteada, medidas) como los calcula el juego."""
    ancho_texto, alto_texto, _ = medida_texto(texto, tam)
    margen = margen_de_sombra() + BORDE + MARGEN_TEXTO
    ancho = int(np.ceil(min(max(ancho_texto + 2 * margen, ANCHO_MIN_PAPEL), ANCHO_MAX_PAPEL) / PASO_DE_ANCHO) * PASO_DE_ANCHO)
    alto = int(round(alto_texto + margen + margen + (COLA[1] if con_cola else 0.0)))
    img = pintar_bocadillo(ancho, alto, con_cola)
    # La textura de Unity crece hacia arriba; PIL crece hacia abajo.
    return img.transpose(Image.FLIP_TOP_BOTTOM), (ancho_texto, alto_texto, ancho, alto)
