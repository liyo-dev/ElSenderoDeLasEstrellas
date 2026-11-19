"""Texturas del follaje de dibujo (shader «El Sendero/Dibujo», palabra _FOLLAJE).

Genera dos texturas en Assets/Art/World/Follaje/:
- GolpesDeHoja.png: golpes de hoja que se repiten sin costura (R = campo con signo de los golpes, 0,5 en el
  borde; el shader corta ahí para sacar manchas de borde duro). Cada golpe es una mata: media luna clara
  arriba con el borde de abajo festoneado, como las copas pintadas a mano.
- MascaraDeFollaje_AtlasDeArboles.png: en las UV del atlas de árboles del Fantasy Kingdom Pack (Tree_D y sus
  copias recoloreadas: CopaRoja_D, Tree_Cerezo_D, ArbolEnFlor_D), blanco donde la isla es follaje y negro en
  la corteza, las setas y los tallos, que no deben llevar golpes de hoja.

Uso: python follaje_de_dibujo.py <carpeta del proyecto> [--vistas carpeta]
"""
import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image

LADO = 512
SEMILLA = 7


def golpes_de_hoja(lado=LADO, semilla=SEMILLA):
    rnd = random.Random(semilla)
    # Campo con signo: positivo dentro de algún golpe. Se guarda como 0,5 + campo, así que el corte del
    # shader en 0,5 cae justo en el borde y los mips difuminan el borde sin mover la forma.
    campo = np.full((lado, lado), -1.0, np.float32)
    yy, xx = np.mgrid[0:lado, 0:lado].astype(np.float32)
    # Muestreo de disco de Poisson sencillo: matas repartidas sin amontonarse ni dejar calvas grandes.
    centros = []
    radio_min = lado * 0.17
    intentos = 0
    while intentos < 8000 and len(centros) < 30:
        intentos += 1
        c = (rnd.uniform(0, lado), rnd.uniform(0, lado))
        ok = True
        for d in centros:
            dx = min(abs(c[0] - d[0]), lado - abs(c[0] - d[0]))
            dy = min(abs(c[1] - d[1]), lado - abs(c[1] - d[1]))
            if dx * dx + dy * dy < radio_min * radio_min:
                ok = False
                break
        if ok:
            centros.append(c)

    def pegar(f_local, cx, cy, alcance):
        nonlocal campo
        for ox in (-lado, 0, lado):
            for oy in (-lado, 0, lado):
                x0, y0 = cx + ox, cy + oy
                if x0 < -alcance or x0 > lado + alcance or y0 < -alcance or y0 > lado + alcance:
                    continue
                campo = np.maximum(campo, f_local(xx - x0, yy - y0))

    for cx, cy in centros:
        # Cada mata: una media luna clara arriba (la luz que pilla el borde de un grupo de hojas), con el
        # borde de abajo festoneado por dos o tres bocados.
        r = rnd.uniform(0.085, 0.125) * lado
        grosor = rnd.uniform(0.32, 0.5)
        inclinacion = math.radians(rnd.uniform(-18, 18))
        n = rnd.randint(2, 3)
        bocados = [((i - (n - 1) / 2) * 0.9 + rnd.uniform(-0.1, 0.1), rnd.uniform(0.25, 0.35), rnd.uniform(0.6, 0.7)) for i in range(n)]

        def mata(x, y, r=r, grosor=grosor, inc=inclinacion, bocados=bocados):
            c, s_ = math.cos(inc), math.sin(inc)
            u = (x * c - y * s_) / r
            v = (x * s_ + y * c) / r
            dentro = 1.0 - np.sqrt(u * u + (v * 1.15) ** 2)
            fuera = np.full_like(u, 9.0)
            for bx, by, br in bocados:
                fuera = np.minimum(fuera, np.sqrt((u - bx) ** 2 + (v - (by + grosor)) ** 2) - br)
            return np.minimum(dentro, fuera) * r / (lado * 0.02)

        pegar(mata, cx, cy, r * 2)
    img = (np.clip(0.5 + campo * 0.5, 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(img, "L").convert("RGB")


# Zonas del atlas de árboles que no son follaje, en píxeles de una vista de 1024 (x0, y0, x1, y1), origen arriba.
SIN_FOLLAJE_ATLAS_DE_ARBOLES = [
    (0, 0, 314, 186),       # corteza
    (0, 186, 216, 224),     # corteza (sin la punta de la copa de debajo)
    (306, 0, 384, 76),      # seta roja
    (412, 0, 492, 60),      # seta morada
    (462, 0, 536, 62),      # sombrerito verde
    (460, 62, 532, 136),    # seta amarilla
    (114, 228, 166, 316),   # tallo claro
    (162, 228, 202, 302),   # tallo azul
    (0, 434, 40, 502),      # tira naranja
    (72, 450, 118, 494),    # punto rojo
    (226, 450, 272, 498),   # punto morado
    (396, 488, 444, 562),   # tallo amarillo
    (576, 276, 610, 344),   # tallito amarillo
    (374, 652, 428, 738),   # tallo turquesa
    (0, 664, 34, 884),      # tiras de colores
    (112, 656, 132, 792),   # tira naranja
    (126, 560, 162, 702),   # tiras verdes y amarillas
    (142, 876, 342, 956),   # setas de abajo
    (156, 956, 200, 1024),  # tallo gris
]


def mascara_atlas_de_arboles(lado=1024):
    m = np.full((lado, lado), 255, np.uint8)
    k = lado / 1024.0
    for x0, y0, x1, y1 in SIN_FOLLAJE_ATLAS_DE_ARBOLES:
        m[int(y0 * k):int(y1 * k), int(x0 * k):int(x1 * k)] = 0
    return Image.fromarray(m, "L").convert("RGB")


def main():
    raiz = Path(sys.argv[1])
    destino = raiz / "Assets/Art/World/Follaje"
    destino.mkdir(parents=True, exist_ok=True)
    golpes = golpes_de_hoja()
    golpes.save(destino / "GolpesDeHoja.png")
    mascara = mascara_atlas_de_arboles()
    mascara.save(destino / "MascaraDeFollaje_AtlasDeArboles.png")
    if "--vistas" in sys.argv:
        vistas = Path(sys.argv[sys.argv.index("--vistas") + 1])
        vistas.mkdir(parents=True, exist_ok=True)
        atlas = Image.open(raiz / "Assets/Art/World/Fantasy_Kingdom_Pack/Textures/Tree_D.tga").convert("RGB").resize((1024, 1024))
        a = np.asarray(atlas).astype(np.float32)
        mk = np.asarray(mascara)[..., :1].astype(np.float32) / 255
        rosa = np.array([255, 0, 255], np.float32)
        vista = a * mk + (a * 0.3 + rosa * 0.7) * (1 - mk)
        Image.fromarray(vista.astype(np.uint8)).save(vistas / "mascara_sobre_atlas.png")
        g = np.asarray(golpes)[..., 0]
        corte = np.where(g > 127, 255, 0).astype(np.uint8)
        Image.fromarray(np.tile(corte, (2, 2))).save(vistas / "golpes_cortados_2x2.png")
        base = np.array([47, 122, 52], np.float32)
        claro = np.minimum(base * 1.3, 255)
        k = (np.tile(corte, (2, 2)) / 255.0)[..., None]
        Image.fromarray((base * (1 - k) + claro * k).astype(np.uint8)).save(vistas / "golpes_en_verde.png")


if __name__ == "__main__":
    main()
