# Papeles del tablón de avisos de la plaza del prólogo (INC-1012): un aviso de la siega, el dibujo
# del globo que ha hecho Nina y un aviso de una gallina perdida. Salen en Assets/Art/World/Pueblo/Tablon/.
#   python3 Tools/Texturas/tablon_de_avisos.py
import math, os, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

RAIZ = os.path.join(os.path.dirname(__file__), "..", "..")
SALIDA = os.path.join(RAIZ, "Assets/Art/World/Pueblo/Tablon")
FUENTE = os.path.join(RAIZ, "Assets/Plugins/Fonts/Nunito-ExtraBold.ttf")
FUENTE_FINA = os.path.join(RAIZ, "Assets/Plugins/Fonts/Nunito-SemiBold.ttf")
TINTA = (70, 46, 30, 255)
os.makedirs(SALIDA, exist_ok=True)


def papel(w, h, color, semilla):
    """Papel con el borde algo irregular, manchitas y una chincheta arriba."""
    rnd = random.Random(semilla)
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    m = 10
    pts = []
    for i in range(24):
        t = i / 24
        if t < 0.25: x, y = m + (w - 2 * m) * t / 0.25, m
        elif t < 0.5: x, y = w - m, m + (h - 2 * m) * (t - 0.25) / 0.25
        elif t < 0.75: x, y = w - m - (w - 2 * m) * (t - 0.5) / 0.25, h - m
        else: x, y = m, h - m - (h - 2 * m) * (t - 0.75) / 0.25
        pts.append((x + rnd.uniform(-4, 4), y + rnd.uniform(-4, 4)))
    d.polygon(pts, fill=color)
    manchas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dm = ImageDraw.Draw(manchas)
    for _ in range(14):
        x, y, r = rnd.uniform(m + 10, w - m - 10), rnd.uniform(m + 10, h - m - 10), rnd.uniform(6, 16)
        dm.ellipse((x - r, y - r, x + r, y + r), fill=(120, 90, 50, 22))
    im.alpha_composite(manchas)
    im = Image.alpha_composite(Image.new("RGBA", (w, h), (0, 0, 0, 0)), im.filter(ImageFilter.GaussianBlur(0.6)))
    d = ImageDraw.Draw(im)
    d.ellipse((w / 2 - 14, 18, w / 2 + 14, 46), fill=(190, 50, 45, 255), outline=(110, 25, 20, 255), width=3)
    d.ellipse((w / 2 - 6, 24, w / 2 + 2, 32), fill=(240, 150, 140, 255))
    return im


def centrado(d, w, y, texto, fuente, color=TINTA):
    ancho = d.textlength(texto, font=fuente)
    d.text(((w - ancho) / 2, y), texto, font=fuente, fill=color)


def aviso_siega():
    w, h = 512, 640
    im = papel(w, h, (238, 226, 196, 255), 1)
    d = ImageDraw.Draw(im)
    centrado(d, w, 80, "SE BUSCA", ImageFont.truetype(FUENTE, 78))
    centrado(d, w, 175, "AYUDA", ImageFont.truetype(FUENTE, 78))
    f = ImageFont.truetype(FUENTE_FINA, 44)
    centrado(d, w, 300, "para la siega", f)
    centrado(d, w, 360, "del trigo", f)
    # una gavilla de trigo dibujada
    for k in range(-3, 4):
        x0, y0 = w / 2 + k * 6, 590
        x1, y1 = w / 2 + k * 22, 450
        d.line((x0, y0, x1, y1), fill=(170, 120, 40, 255), width=6)
        d.ellipse((x1 - 12, y1 - 26, x1 + 12, y1 + 8), fill=(215, 165, 60, 255))
    d.rectangle((w / 2 - 30, 520, w / 2 + 30, 536), fill=(150, 90, 40, 255))
    im.save(os.path.join(SALIDA, "Aviso_Siega.png"))


def dibujo_nina():
    """Dibujo de niña pequeña: cera, trazo temblón, el globo rojo, Nina y su padre, el sol."""
    w, h = 560, 480
    im = papel(w, h, (250, 248, 240, 255), 2)
    d = ImageDraw.Draw(im)
    rnd = random.Random(7)

    def trazo(puntos, color, ancho):
        tem = [(x + rnd.uniform(-2.5, 2.5), y + rnd.uniform(-2.5, 2.5)) for x, y in puntos]
        d.line(tem, fill=color, width=ancho, joint="curve")

    def circulo(cx, cy, r, color, ancho, relleno=None):
        pts = [(cx + r * math.cos(a / 30 * math.tau) * rnd.uniform(0.95, 1.05),
                cy + r * math.sin(a / 30 * math.tau) * rnd.uniform(0.95, 1.05)) for a in range(31)]
        if relleno: d.polygon(pts, fill=relleno)
        trazo(pts, color, ancho)

    # sol
    circulo(90, 95, 38, (240, 180, 30, 255), 6, (250, 215, 80, 255))
    for k in range(10):
        a = k / 10 * math.tau
        trazo([(90 + 50 * math.cos(a), 95 + 50 * math.sin(a)), (90 + 72 * math.cos(a), 95 + 72 * math.sin(a))], (240, 180, 30, 255), 5)
    # globo rojo con su cuerda
    circulo(380, 130, 62, (200, 40, 40, 255), 6, (230, 70, 60, 255))
    trazo([(380, 192), (372, 230), (388, 270), (376, 318)], (60, 60, 60, 255), 3)
    # suelo
    trazo([(30, 420), (200, 412), (380, 424), (530, 414)], (60, 160, 60, 255), 8)
    # Nina (pequeña, coletas) cogiendo el globo
    circulo(372, 340, 24, (60, 40, 30, 255), 4, (255, 215, 180, 255))
    trazo([(350, 330), (330, 350)], (170, 90, 40, 255), 6)
    trazo([(394, 330), (414, 350)], (170, 90, 40, 255), 6)
    d.polygon([(372, 362), (348, 412), (396, 412)], fill=(225, 120, 130, 255))
    trazo([(372, 368), (376, 318)], (60, 40, 30, 255), 4)
    # el maestro (alto, pelo de pinchos) a su lado
    circulo(220, 270, 30, (60, 40, 30, 255), 4, (255, 215, 180, 255))
    for k in range(-3, 4):
        trazo([(220 + k * 9, 244), (220 + k * 13, 222)], (30, 30, 30, 255), 4)
    d.rectangle((200, 300, 240, 370), fill=(60, 90, 200, 255))
    trazo([(205, 370), (198, 412)], (60, 40, 30, 255), 5)
    trazo([(235, 370), (242, 412)], (60, 40, 30, 255), 5)
    trazo([(240, 315), (300, 340), (350, 360)], (60, 40, 30, 255), 5)
    # «NINA» con letras de niña
    f = ImageFont.truetype(FUENTE, 46)
    x = 168
    for i, letra in enumerate("NINA"):
        capa = Image.new("RGBA", (80, 80), (0, 0, 0, 0))
        ImageDraw.Draw(capa).text((10, 0), letra, font=f, fill=(120, 60, 170, 255))
        capa = capa.rotate(rnd.uniform(-14, 14), resample=Image.BICUBIC)
        im.alpha_composite(capa, (x, 40 + int(rnd.uniform(-8, 8))))
        x += 38
    im.save(os.path.join(SALIDA, "Dibujo_Nina.png"))


def aviso_gallina():
    w, h = 448, 512
    im = papel(w, h, (232, 220, 186, 255), 3)
    d = ImageDraw.Draw(im)
    centrado(d, w, 70, "¿HAS VISTO", ImageFont.truetype(FUENTE, 54))
    centrado(d, w, 135, "A PECOSA?", ImageFont.truetype(FUENTE, 54))
    # la gallina
    d.ellipse((150, 260, 300, 390), fill=(250, 280, 245, 285), outline=TINTA, width=5)
    d.ellipse((260, 230, 330, 300), fill=(250, 280, 245, 285), outline=TINTA, width=5)
    d.polygon([(326, 262), (356, 272), (326, 282)], fill=(240, 200, 40, 285))
    d.polygon([(280, 230), (292, 206), (302, 230), (312, 210), (318, 236)], fill=(210, 70, 40, 285))
    d.ellipse((300, 250, 310, 260), fill=TINTA)
    for x, y in ((190, 300), (230, 330), (200, 350), (250, 290)):
        d.ellipse((x - 7, y - 7, x + 7, y + 7), fill=(150, 130, 60, 285))
    d.line((210, 390, 205, 430), fill=(240, 200, 40, 285), width=6)
    d.line((250, 390, 255, 430), fill=(240, 200, 40, 285), width=6)
    centrado(d, w, 440, "Razón: la panadería", ImageFont.truetype(FUENTE_FINA, 34))
    im.save(os.path.join(SALIDA, "Aviso_Gallina.png"))


aviso_siega(); dibujo_nina(); aviso_gallina()
print("PAPELES", sorted(os.listdir(SALIDA)))
