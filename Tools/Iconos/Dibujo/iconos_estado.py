# Iconos sobre la cabeza de los NPC (Assets/Art/UI/NpcStatus) con el aspecto de dibujo: ficha de
# papel crema con contorno de tinta y el símbolo plano encima; y los 12 fotogramas de la charla
# (dos bocadillos que se turnan con puntos que saltan). INC-1137.
# Uso: python iconos_estado.py <carpeta_salida>
import math, sys, os
from dibujo import *

PAPEL, PAPEL_S = 'FFF8E8', 'EAD9BB'
ORO, ORO_S = 'FFD45E', 'E39B2E'
ROJO, ROJO_S = 'EE5E4F', 'B53B39'
LILA, LILA_S = 'A081F5', '6F55C9'
GRIS, GRIS_S = 'D9DCE6', 'A3A7B8'

def ficha(L):
    L.parte(circulo(256, 256, 200), PAPEL, PAPEL_S, desp=(-18, -18), brillo=False)

def exclamacion(L, col, sh):
    L.parte(rect(222, 110, 68, 190, 34), col, sh, desp=(-10, -10), brillo=False, trazo=14)
    L.parte(circulo(256, 360, 38), col, sh, desp=(-8, -8), brillo=False, trazo=14)

def over_here(L):
    ficha(L); exclamacion(L, ORO, ORO_S)

def admiration(L):
    ficha(L); exclamacion(L, ROJO, ROJO_S)

def what(L):
    ficha(L)
    L.trazo_grueso(lambda c: (c.new_sub_path(), c.arc(256, 200, 66, math.pi * 1.05, math.pi * 2.25), c.curve_to(300, 270, 256, 270, 256, 310)), LILA, 46, 14)
    L.parte(circulo(256, 368, 34), LILA, LILA_S, desp=(-7, -7), brillo=False, trazo=14)

def talk(L):
    ficha(L)
    for x in (176, 256, 336):
        L.parte(circulo(x, 262, 34), LILA, LILA_S, desp=(-7, -7), brillo=False, trazo=12)

def angry(L):
    ficha(L)
    # La «vena» de enfado del manga: cuatro esquinas curvas.
    for ang in (0, 90, 180, 270):
        c = L.c
        c.save(); c.translate(256, 256); c.rotate(math.radians(ang)); c.translate(-256, -256)
        L.trazo_grueso(lambda cc: (cc.move_to(150, 222), cc.curve_to(200, 226, 222, 230, 228, 228), cc.curve_to(230, 222, 226, 200, 222, 150)), ROJO, 30, 12)
        c.restore()

def want_fight(L):
    ficha(L)
    for lado in (-1, 1):
        c = L.c
        c.save(); c.translate(256, 256); c.rotate(lado * math.radians(45)); c.translate(-256, -256)
        L.parte(poli([(238, 380), (238, 140), (256, 100), (274, 140), (274, 380)]), 'E9ECF5', 'AEB3C7', desp=(-6, -6), brillo=False, trazo=12)
        L.parte(rect(196, 360, 120, 30, 12), ORO, ORO_S, desp=(-5, -5), brillo=False, trazo=12)
        L.parte(rect(240, 388, 32, 60, 10), '8A5A33', None, brillo=False, trazo=12)
        c.restore()

# ---------------------------------------------------------------- charla animada
def bocadillo(L, cx, cy, w, h, lado, col, sh, escala):
    w *= escala; h *= escala
    def cam(c):
        rect(cx - w / 2, cy - h / 2, w, h, h * 0.45)(c)
        c.move_to(cx + lado * w * 0.05, cy + h * 0.38)
        c.line_to(cx + lado * w * 0.38, cy + h * 0.78)
        c.line_to(cx + lado * w * 0.28, cy + h * 0.30)
        c.close_path()
    L.parte(cam, col, sh, desp=(-12, -12), brillo=False, trazo=14)

def charla(L, f, n=12):
    ficha(L)
    t = f / n
    habla_izq = t < 0.5
    pulso = math.sin((t % 0.5) / 0.5 * math.pi)
    e_izq = 1.0 + (0.08 * pulso if habla_izq else 0)
    e_der = 1.0 + (0.08 * pulso if not habla_izq else 0)
    bocadillo(L, 300, 200, 190, 120, 1, 'E3DAFF', 'C2B3F2', e_der)
    bocadillo(L, 215, 290, 220, 140, -1, PAPEL, PAPEL_S, e_izq)
    for i in range(3):
        fase = (t * 2 + i * 0.15) % 1.0
        salto = max(0.0, math.sin(fase * math.pi * 2)) * 12 if habla_izq else 0
        L.plano(circulo(165 + i * 50, 292 - salto, 15), LILA, 0)
    for i in range(3):
        fase = (t * 2 + i * 0.15) % 1.0
        salto = max(0.0, math.sin(fase * math.pi * 2)) * 10 if not habla_izq else 0
        L.plano(circulo(260 + i * 42, 202 - salto, 12), LILA_S, 0)

ESTADOS = ['admiration', 'angry', 'over_here', 'talk', 'want_fight', 'what']

def main(salida):
    os.makedirs(os.path.join(salida, 'NpcStatus', 'Charla'), exist_ok=True)
    for n in ESTADOS:
        L = Lienzo(1024); globals()[n](L); L.guardar(os.path.join(salida, 'NpcStatus', n + '.png'))
    for f in range(12):
        L = Lienzo(512); charla(L, f); L.guardar(os.path.join(salida, 'NpcStatus', 'Charla', 'charla_%02d.png' % f))

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'salida')
