# Iconos de objetos y habilidades con el aspecto de dibujo (INC-1137). Cada función dibuja un icono
# en el lienzo de 512; main() los escribe todos al tamaño del original para no cambiar cómo se ven.
# Uso: python iconos_objetos.py <carpeta_salida>
import math, sys, os
from dibujo import *

ORO, ORO_S = 'FFD45E', 'E39B2E'
MADERA, MADERA_S = 'D9A066', 'AD7340'
SACO, SACO_S = 'C48A57', '93603A'
VERDE, VERDE_S = '74C77F', '3F9A5A'
AZUL, AZUL_S = '7CC0F2', '4685CC'
LILA, LILA_S = 'A081F5', '6F55C9'
ROSA, ROSA_S = 'F7A3B9', 'D9708F'
ROJO, ROJO_S = 'EE5E4F', 'B53B39'
CREMA, CREMA_S = 'FFF4DC', 'EAD9BB'
GRIS, GRIS_S = 'B4B0C2', '827D96'
OSCURO, OSCURO_S = '4A3D56', '2B2234'

# ---------------------------------------------------------------- objetos
def coin(L):
    L.parte(circulo(256, 262, 178), ORO, ORO_S)
    L.linea(circulo(256, 262, 128), 9)
    L.parte(estrella(256, 266, 78, 34), 'FFE9A6', ORO_S, desp=(-10, -10), brillo=False, trazo=10)
    L.destello(420, 92, 34, CREMA); L.destello(92, 420, 22, CREMA)

def moneda_antigua(L):
    L.parte(circulo(256, 262, 178), 'D9A05F', 'A9703A')
    L.linea(circulo(256, 262, 130), 9)
    corona = poli([(170, 320), (160, 205), (212, 250), (256, 190), (300, 250), (352, 205), (342, 320)])
    L.parte(corona, 'F2C27A', 'B98042', desp=(-10, -10), brillo=False, trazo=11)
    for x in (212, 256, 300): L.plano(circulo(x, 290, 12), 'A9703A', 7)

def _saco(L, cx, base_y, ancho, alto):
    w = ancho / 2; top = base_y - alto
    def cuerpo(c):
        c.move_to(cx - w * 0.38, top + alto * 0.22)
        c.curve_to(cx - w * 1.05, top + alto * 0.45, cx - w * 1.15, base_y, cx, base_y)
        c.curve_to(cx + w * 1.15, base_y, cx + w * 1.05, top + alto * 0.45, cx + w * 0.38, top + alto * 0.22)
        c.close_path()
    L.parte(cuerpo, SACO, SACO_S, desp=(-26, -22))
    boca = poli([(cx - w * 0.42, top + alto * 0.18), (cx - w * 0.62, top), (cx - w * 0.2, top + alto * 0.08),
                 (cx, top - alto * 0.04), (cx + w * 0.2, top + alto * 0.08), (cx + w * 0.62, top), (cx + w * 0.42, top + alto * 0.18)])
    L.parte(boca, SACO, SACO_S, desp=(-8, -8), brillo=False)
    L.parte(rect(cx - w * 0.5, top + alto * 0.15, w, alto * 0.11, alto * 0.05), ROJO, ROJO_S, desp=(-6, -6), brillo=False, trazo=12)

def bolsa_monedas_grande(L):
    for x, y, r in ((120, 210, 54), (395, 230, 50), (360, 150, 42)):
        L.parte(circulo(x, y, r), ORO, ORO_S, desp=(-10, -10), brillo=False)
        L.linea(circulo(x, y, r * 0.6), 7)
    _saco(L, 256, 470, 330, 360)
    L.parte(circulo(256, 360, 46), ORO, ORO_S, desp=(-8, -8), brillo=False, trazo=12)
    L.linea(circulo(256, 360, 26), 7)
    L.destello(440, 80, 26, CREMA)

def bolsa_monedas_pequena(L):
    L.parte(circulo(370, 190, 48), ORO, ORO_S, desp=(-10, -10), brillo=False)
    L.linea(circulo(370, 190, 28), 7)
    _saco(L, 250, 450, 260, 290)
    L.destello(120, 140, 24, CREMA)

def key(L):
    c = L.c
    c.save(); c.translate(256, 256); c.rotate(-math.pi / 4); c.translate(-256, -256)
    vastago = varios(rect(250, 228, 210, 52, 18), rect(380, 270, 34, 64, 8), rect(428, 270, 30, 48, 8))
    L.parte(vastago, ORO, ORO_S, desp=(-8, -10), brillo=False)
    anillo = varios(circulo(170, 254, 110), circulo(170, 254, 50))
    L.parte(anillo, ORO, ORO_S, parpar=True, desp=(-16, -16))
    c.restore()
    L.destello(410, 110, 30, CREMA)

def _pocion(L, liquido, liquido_s):
    cuerpo = varios(circulo(256, 320, 150), rect(214, 112, 84, 110, 10))
    def vidrio(c): cuerpo(c)
    L.parte(vidrio, 'EAF6F6', 'C9E1E4', brillo=False)
    c = L.c
    c.save(); c.new_path(); circulo(256, 320, 150)(c); c.clip()
    def nivel(cc):
        cc.move_to(80, 300)
        for i in range(9):
            x = 80 + i * 45
            cc.curve_to(x + 12, 285, x + 33, 285, x + 45, 300)
        cc.line_to(440, 500); cc.line_to(80, 500); cc.close_path()
    L.parte(nivel, liquido, liquido_s, desp=(-24, -10), brillo=False, trazo=0)
    c.restore()
    L.linea(circulo(256, 320, 150), TRAZO)
    L.linea(lambda cc: (cc.move_to(214, 222), cc.line_to(214, 120), cc.line_to(298, 120), cc.line_to(298, 222)), TRAZO)
    L.parte(rect(200, 70, 112, 66, 14), MADERA, MADERA_S, desp=(-8, -8), brillo=False)
    L.plano(elipse(190, 270, 22, 46, 0.5), 'FFFFFF', 0)
    for x, y, r in ((236, 380, 16), (300, 350, 11), (270, 420, 9)):
        L.plano(circulo(x, y, r), 'FFFFFF', 0)

def lifePotion(L):
    _pocion(L, ROJO, ROJO_S)
    L.parte(lambda c: _corazon(c, 256, 372, 1.0), 'FFC2CC', None, brillo=False, trazo=9)

def magicPotion(L):
    _pocion(L, AZUL, AZUL_S)
    L.destello(256, 375, 36, CREMA)

def _corazon(c, cx, cy, k):
    c.move_to(cx, cy + 42 * k)
    c.curve_to(cx - 70 * k, cy - 4 * k, cx - 40 * k, cy - 62 * k, cx, cy - 28 * k)
    c.curve_to(cx + 40 * k, cy - 62 * k, cx + 70 * k, cy - 4 * k, cx, cy + 42 * k)
    c.close_path()

def mermelada(L):
    L.parte(rect(130, 170, 252, 300, 50), 'E5455B', 'B02E44', desp=(-24, -18))
    L.parte(rect(150, 120, 212, 70, 18), 'F2EEE8', 'D6CFC4', brillo=False)
    def tela(c):
        c.move_to(110, 150); c.line_to(402, 150)
        for i in range(6):
            x = 402 - i * 48.7
            c.curve_to(x - 8, 210, x - 40, 210, x - 48.7, 170)
        c.close_path()
    L.parte(tela, 'F7F2EA', 'DDD4C6', brillo=False)
    c = L.c
    c.save(); c.new_path(); tela(c); c.clip()
    for i in range(7):
        for j in range(3):
            if (i + j) % 2 == 0:
                c.rectangle(110 + i * 42, 112 + j * 34, 42, 34)
    c.set_source_rgba(*hexa(ROJO, 0.9)); c.fill(); c.restore()
    L.linea(tela, TRAZO)
    L.linea(lambda c: (c.move_to(146, 176), c.line_to(366, 176)), 12, 'C48A57')
    L.parte(elipse(256, 330, 92, 70), CREMA, CREMA_S, desp=(-10, -10), brillo=False, trazo=11)
    L.parte(lambda c: _corazon(c, 256, 338, 0.85), ROJO, ROJO_S, desp=(-6, -6), brillo=False, trazo=9)
    L.parte(estrella(256, 300, 22, 9, 5), VERDE, None, brillo=False, trazo=7)

def algas(L):
    hojas = [
        ([(180, 470), (165, 390), (195, 300), (160, 210), (175, 120)], 76, 18, VERDE, VERDE_S),
        ([(320, 470), (345, 390), (315, 300), (350, 220), (335, 150)], 72, 16, '5DB36E', '357F4B'),
        ([(250, 470), (238, 380), (268, 280), (240, 180), (258, 70)], 80, 16, '8FD88F', VERDE_S),
    ]
    for centro, a0, a1, col, sh in hojas:
        suave = muestrear(centro, 40)
        L.parte(cinta(suave, a0, a1), col, sh, desp=(-14, -6))
        L.linea(curva(centro[:-2]), 6, sh)
    for x, y, r in ((390, 120, 22), (420, 200, 14), (380, 260, 10)):
        L.parte(circulo(x, y, r), 'DDF3FF', None, brillo=False, trazo=8)

def _bota(L, giro=0.0, cx=256, cy=256, escala=1.0):
    c = L.c
    c.save(); c.translate(cx, cy); c.rotate(giro); c.scale(escala, escala); c.translate(-256, -256)
    bota = poli([(170, 120), (320, 120), (322, 300), (408, 330), (430, 380), (430, 420), (150, 420), (160, 300)])
    def suave(cc):
        cc.move_to(170, 120); cc.line_to(320, 120); cc.line_to(322, 290)
        cc.curve_to(380, 300, 432, 330, 432, 392); cc.line_to(432, 420); cc.line_to(150, 420)
        cc.line_to(150, 360); cc.curve_to(150, 300, 168, 260, 170, 120); cc.close_path()
    L.parte(suave, SACO, SACO_S, desp=(-24, -12))
    L.parte(rect(140, 405, 300, 46, 18), OSCURO, OSCURO_S, desp=(-6, -6), brillo=False)
    L.parte(rect(150, 92, 184, 64, 22), 'E6B47A', MADERA_S, desp=(-8, -8), brillo=False)
    for y in (220, 262): L.linea(lambda cc, y=y: (cc.move_to(250, y), cc.line_to(300, y - 6)), 9)
    c.restore()

def boots(L):
    _bota(L)
    L.destello(90, 110, 26, CREMA)

def campanilla_viento(L):
    L.linea(lambda c: (c.move_to(256, 30), c.line_to(256, 92)), 9)
    L.parte(rect(120, 88, 272, 40, 18), MADERA, MADERA_S, desp=(-6, -6), brillo=False)
    tubos = [(160, 300), (215, 360), (297, 340), (352, 280)]
    for x, largo in tubos:
        L.linea(lambda c, x=x: (c.move_to(x, 128), c.line_to(x, 170)), 6)
        L.parte(rect(x - 20, 168, 40, largo - 168, 14), 'C9E6F2', '8FB8CF', desp=(-8, -6), brillo=False, trazo=11)
    L.linea(lambda c: (c.move_to(256, 128), c.line_to(256, 330)), 6)
    L.parte(circulo(256, 340, 22), ORO, ORO_S, desp=(-5, -5), brillo=False, trazo=10)
    L.linea(lambda c: (c.move_to(256, 362), c.line_to(256, 392)), 6)
    L.parte(poli([(222, 392), (290, 392), (300, 470), (256, 450), (212, 470)]), CREMA, CREMA_S, desp=(-6, -6), brillo=False, trazo=11)
    L.linea(curva([(60, 220), (90, 205), (110, 225)]), 9, LILA)
    L.linea(curva([(410, 410), (440, 395), (465, 415)]), 9, LILA)

def canica_cristal(L):
    L.parte(circulo(256, 256, 180), AZUL, AZUL_S, desp=(-28, -28), brillo=False)
    c = L.c
    c.save(); c.new_path(); circulo(256, 256, 180)(c); c.clip()
    L.trazo_grueso(curva([(90, 330), (170, 360), (240, 300), (270, 220), (340, 170), (430, 190)]), 'FFE38A', 44, 10)
    c.restore()
    L.linea(circulo(256, 256, 180), TRAZO)
    L.plano(elipse(180, 170, 48, 24, -0.6), 'FFFFFF', 0)
    L.destello(410, 90, 28, CREMA)

def caracola(L):
    L.parte(poli([(330, 120), (420, 40), (390, 150)]), ROSA, ROSA_S, desp=(-6, -6), brillo=False)
    L.parte(circulo(250, 270, 175), ROSA, ROSA_S, desp=(-26, -22))
    puntos = []
    for i in range(120):
        t = i / 119 * 3.2 * math.pi
        r = 14 + t * 15
        puntos.append((262 + r * math.cos(t + 1.0), 262 + r * math.sin(t + 1.0)))
    L.linea(lambda c: (c.move_to(*puntos[0]), [c.line_to(*p) for p in puntos[1:]]), 11, ROSA_S)
    L.parte(elipse(150, 380, 85, 55, -0.5), 'FFE3EA', 'F2BCCA', desp=(-8, -8), brillo=False)
    L.linea(elipse(150, 380, 45, 22, -0.5), 8, ROSA_S)

def esencia(L):
    L.parte(circulo(256, 256, 170), LILA, LILA_S, desp=(-24, -24))
    L.parte(circulo(256, 256, 110), 'C9B6FF', None, brillo=False, trazo=0)
    L.destello(256, 256, 80, CREMA, 9)
    L.linea(circulo(256, 256, 170), TRAZO)
    for x, y, r in ((420, 90, 34), (100, 120, 22), (430, 400, 20)): L.destello(x, y, r)

def estrella_torcida(L):
    L.parte(estrella(256, 270, 205, 92, irregular=(1.0, 0.95, 0.92, 1.1, 1.08, 0.9, 0.97, 1.05, 0.85, 1.0), giro=-math.pi / 2 + 0.18),
            MADERA, MADERA_S, desp=(-22, -18))
    for p in ([(190, 250), (240, 245), (290, 262)], [(210, 320), (260, 312), (300, 330)]):
        L.linea(curva(p), 8, MADERA_S)
    L.plano(circulo(330, 228, 9), MADERA_S, 0)

def farolillo_papel(L):
    L.linea(lambda c: (c.move_to(256, 28), c.line_to(256, 80)), 9)
    L.parte(rect(196, 74, 120, 40, 12), OSCURO, OSCURO_S, desp=(-5, -5), brillo=False)
    L.parte(elipse(256, 265, 170, 160), ROJO, ROJO_S, desp=(-28, -18))
    for k in (-0.62, -0.25, 0.25, 0.62):
        L.linea(lambda c, k=k: (c.move_to(256 + 170 * k * 0.95, 140), c.curve_to(256 + 170 * k * 1.25, 230, 256 + 170 * k * 1.25, 300, 256 + 170 * k * 0.95, 392)), 8, ROJO_S)
    L.linea(elipse(256, 265, 170, 160), TRAZO)
    L.parte(rect(196, 405, 120, 40, 12), OSCURO, OSCURO_S, desp=(-5, -5), brillo=False)
    L.parte(poli([(236, 445), (276, 445), (290, 500), (222, 500)]), ORO, ORO_S, desp=(-5, -5), brillo=False, trazo=10)

def guirnalda_banderines(L):
    cuerda = [(30, 120), (140, 195), (256, 225), (372, 195), (482, 120)]
    L.linea(curva(cuerda), 10)
    colores = [(ROJO, ROJO_S), (ORO, ORO_S), (AZUL, AZUL_S), (VERDE, VERDE_S), (ROSA, ROSA_S)]
    def en_cuerda(t):
        # punto aproximado sobre la parábola de la cuerda
        x = 30 + t * 452
        y = 120 + 105 * (1 - ((x - 256) / 226) ** 2)
        return x, y
    for i in range(5):
        t0, t1 = 0.09 + i * 0.18, 0.09 + i * 0.18 + 0.13
        a, b = en_cuerda(t0), en_cuerda(t1)
        m = ((a[0] + b[0]) / 2, max(a[1], b[1]) + 130)
        col, sh = colores[i]
        L.parte(poli([a, b, m]), col, sh, desp=(-10, -10), brillo=False, trazo=12)
    L.linea(curva(cuerda), 10)

def pajaro_madera(L):
    L.parte(poli([(110, 250), (30, 200), (40, 290)]), MADERA, MADERA_S, desp=(-6, -6), brillo=False)
    L.parte(elipse(220, 300, 140, 100, -0.15), MADERA, MADERA_S, desp=(-24, -18))
    L.parte(circulo(345, 200, 78), MADERA, MADERA_S, desp=(-14, -14))
    L.parte(poli([(410, 190), (480, 215), (408, 236)]), ORO, ORO_S, desp=(-5, -5), brillo=False, trazo=12)
    L.plano(circulo(360, 190, 14), '33263A', 0)
    L.plano(circulo(364, 185, 5), 'FFFFFF', 0)
    L.linea(curva([(160, 270), (220, 300), (280, 290)]), 10, MADERA_S)
    L.parte(rect(180, 390, 100, 34, 12), MADERA_S, None, brillo=False, trazo=12)

def panuelo_erika(L):
    tri = poli([(60, 120), (452, 120), (256, 450)])
    L.parte(tri, VERDE, VERDE_S, desp=(-20, -16))
    c = L.c
    c.save(); c.set_dash([18, 14]); L.linea(poli([(110, 148), (402, 148), (256, 395)]), 7, ORO); c.restore()
    for i in range(5):
        a = i * 2 * math.pi / 5 - math.pi / 2
        L.plano(circulo(256 + 26 * math.cos(a), 210 + 26 * math.sin(a), 18), 'FFFFFF', 7)
    L.plano(circulo(256, 210, 14), ORO, 7)

def piedra_con_agujero(L):
    def roca(c):
        curva([(120, 160), (240, 90), (380, 130), (440, 260), (390, 390), (230, 430), (100, 360), (80, 250), (120, 160)])(c)
        c.close_path()
        c.new_sub_path(); c.arc(265, 240, 52, 0, 2 * math.pi)
    L.parte(roca, GRIS, GRIS_S, parpar=True, desp=(-26, -22))
    for x, y, r in ((170, 330, 9), (340, 350, 7), (330, 180, 6)): L.plano(circulo(x, y, r), GRIS_S, 0)

def trompetilla(L):
    L.parte(poli([(330, 236), (470, 140), (470, 380), (330, 290)]), ORO, ORO_S, desp=(-10, -14))
    L.parte(elipse(470, 260, 30, 120), 'FFE9A6', ORO_S, desp=(-6, -6), brillo=False)
    L.parte(rect(60, 236, 280, 54, 22), ORO, ORO_S, desp=(-6, -10), brillo=False)
    L.parte(rect(40, 226, 30, 74, 10), ORO_S, None, brillo=False, trazo=11)
    for x in (150, 205, 260):
        L.parte(rect(x - 14, 160, 28, 80, 8), ORO, ORO_S, desp=(-4, -4), brillo=False, trazo=11)
        L.parte(rect(x - 22, 146, 44, 20, 8), 'FFE9A6', None, brillo=False, trazo=10)
    L.parte(lambda c: (c.move_to(205, 300), c.line_to(170, 400), c.line_to(205, 380), c.line_to(240, 400), c.close_path()), ROSA, ROSA_S, desp=(-5, -5), brillo=False, trazo=10)
    L.parte(circulo(205, 300, 20), ROSA, ROSA_S, desp=(-4, -4), brillo=False, trazo=10)

def trozo_del_aro(L):
    def trozo(c):
        c.move_to(70, 190); c.curve_to(190, 120, 330, 120, 450, 170)
        c.line_to(440, 360); c.curve_to(330, 320, 190, 320, 80, 380); c.close_path()
    L.parte(trozo, OSCURO, OSCURO_S, desp=(-18, -18))
    L.linea(lambda c: (c.move_to(450, 170), c.line_to(470, 210), c.line_to(445, 250), c.line_to(462, 300), c.line_to(440, 360)), 10)
    L.trazo_grueso(lambda c: (c.move_to(240, 300), c.line_to(250, 190), c.line_to(300, 240)), ROJO, 20, 9)
    for x, y, r in ((300, 180, 18), (200, 230, 12)): L.destello(x, y, r, ROJO, 5)

# ---------------------------------------------------------------- habilidades
def climb(L):
    def roca(c):
        c.move_to(40, 470); c.line_to(90, 300); c.line_to(170, 250); c.line_to(250, 290)
        c.line_to(330, 200); c.line_to(460, 230); c.line_to(480, 470); c.close_path()
    L.parte(roca, GRIS, GRIS_S, desp=(-22, -16), brillo=False)
    L.linea(lambda c: (c.move_to(150, 380), c.line_to(210, 350), c.move_to(360, 330), c.line_to(420, 380)), 9, GRIS_S)
    mano = varios(rect(250, 150, 150, 120, 46), rect(240, 120, 42, 80, 20), rect(285, 108, 42, 84, 20), rect(330, 112, 42, 84, 20), rect(372, 128, 38, 76, 18))
    L.parte(mano, SACO, SACO_S, desp=(-10, -10), brillo=False)
    for x in (282, 326, 370): L.linea(lambda c, x=x: (c.move_to(x, 165), c.line_to(x, 195)), 8, SACO_S)
    L.parte(rect(270, 260, 110, 46, 14), OSCURO, OSCURO_S, desp=(-5, -5), brillo=False)

def fly(L):
    def ala(c, lado):
        s = lado
        cx = 256
        c.move_to(cx + s * 20, 300)
        c.curve_to(cx + s * 80, 140, cx + s * 180, 70, cx + s * 230, 100)
        c.curve_to(cx + s * 245, 160, cx + s * 230, 190, cx + s * 205, 205)
        c.curve_to(cx + s * 225, 230, cx + s * 215, 260, cx + s * 185, 270)
        c.curve_to(cx + s * 200, 300, cx + s * 180, 330, cx + s * 150, 335)
        c.curve_to(cx + s * 150, 370, cx + s * 110, 390, cx + s * 80, 380)
        c.curve_to(cx + s * 60, 380, cx + s * 30, 350, cx + s * 20, 300)
        c.close_path()
    for lado in (-1, 1):
        L.parte(lambda c, lado=lado: ala(c, lado), 'FFFFFF', 'D7CCF5', desp=(-14 * lado, -14))
        for k in (0.45, 0.65, 0.82):
            L.linea(lambda c, k=k, lado=lado: (c.move_to(256 + lado * 40, 300), c.line_to(256 + lado * (40 + 170 * k), 300 - 160 * k + 60 * k * k)), 7, 'B8A8EA')
    L.destello(256, 430, 30)

def jump(L):
    _bota(L, giro=-0.35, cx=270, cy=210, escala=0.78)
    for x, y, l in ((150, 400, 70), (230, 430, 90), (320, 410, 70)):
        L.trazo_grueso(lambda c, x=x, y=y, l=l: (c.move_to(x, y), c.line_to(x - 28, y + l * 0.6)), CREMA, 16, 8)

def magic(L):
    puntos = []
    for i in range(140):
        t = i / 139 * 4.2 * math.pi
        r = 18 + t * 13.5
        puntos.append((256 + r * math.cos(t), 262 + r * math.sin(t)))
    def esp(c):
        c.move_to(*puntos[0])
        for p in puntos[1:]: c.line_to(*p)
    L.trazo_grueso(esp, LILA, 40, TRAZO)
    L.linea(esp, 10, 'C9B6FF')
    for x, y, r in ((420, 90, 34), (90, 420, 26), (430, 420, 18)): L.destello(x, y, r)

def proteccion(L):
    def escudo(c):
        c.move_to(256, 50)
        c.curve_to(320, 90, 380, 100, 430, 96)
        c.curve_to(440, 260, 380, 400, 256, 470)
        c.curve_to(132, 400, 72, 260, 82, 96)
        c.curve_to(132, 100, 192, 90, 256, 50); c.close_path()
    L.parte(escudo, AZUL, AZUL_S, desp=(-24, -20))
    L.parte(estrella(256, 250, 96, 42), ORO, ORO_S, desp=(-10, -10), brillo=False, trazo=12)
    for x, y, r in ((440, 50, 26), (60, 60, 20), (450, 420, 18)): L.destello(x, y, r, CREMA)

def swim(L):
    def ola(c):
        c.move_to(40, 420)
        c.curve_to(60, 250, 200, 140, 320, 170)
        c.curve_to(420, 195, 440, 300, 360, 320)
        c.curve_to(300, 335, 270, 290, 300, 260)
        c.curve_to(250, 260, 200, 330, 240, 420)
        c.close_path()
    L.parte(ola, AZUL, AZUL_S, desp=(-20, -14))
    L.parte(lambda c: (c.move_to(300, 260), c.curve_to(330, 230, 380, 250, 360, 290), c.curve_to(345, 315, 300, 300, 300, 260), c.close_path()), CREMA, CREMA_S, desp=(-5, -5), brillo=False, trazo=11)
    L.trazo_grueso(curva([(30, 470), (120, 440), (220, 470), (320, 440), (440, 470)]), AZUL, 22, 10)
    for x, y, r in ((420, 120, 20), (460, 180, 13)):
        L.parte(circulo(x, y, r), 'DDF3FF', None, brillo=False, trazo=8)

OBJETOS = ['algas', 'bolsa_monedas_grande', 'bolsa_monedas_pequena', 'boots', 'campanilla_viento', 'canica_cristal',
           'caracola', 'coin', 'esencia', 'estrella_torcida', 'farolillo_papel', 'guirnalda_banderines', 'key',
           'lifePotion', 'magicPotion', 'mermelada', 'moneda_antigua', 'pajaro_madera', 'panuelo_erika',
           'piedra_con_agujero', 'trompetilla', 'trozo_del_aro']
HABILIDADES = ['climb', 'fly', 'jump', 'magic', 'proteccion', 'swim']

def main(salida, tamanos=None):
    tamanos = tamanos or {}
    for grupo, nombres in (('Items', OBJETOS), ('Abilities', HABILIDADES)):
        os.makedirs(os.path.join(salida, grupo), exist_ok=True)
        for n in nombres:
            L = Lienzo(tamanos.get(n, 512))
            globals()[n](L)
            L.guardar(os.path.join(salida, grupo, n + '.png'))

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'salida')
