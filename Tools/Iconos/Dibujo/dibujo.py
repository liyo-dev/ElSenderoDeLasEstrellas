# Piezas comunes para dibujar iconos con el aspecto de dibujo del juego (INC-1127 / INC-1137):
# colores planos, banda de sombra de color abajo a la derecha, brillo pequeño arriba a la izquierda,
# contorno de tinta #33263A y destellos de cuatro puntas. Todo en un lienzo de 512 unidades que se
# escala al tamaño de salida.
import cairo, math

def hexa(h, a=1.0):
    h = h.lstrip('#')
    return (int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, a)

TINTA = hexa('33263A')
TRAZO = 15

class Lienzo:
    def __init__(self, tam=512):
        self.tam = tam
        self.sup = cairo.ImageSurface(cairo.FORMAT_ARGB32, tam, tam)
        self.c = cairo.Context(self.sup)
        self.c.scale(tam / 512, tam / 512)
        self.c.set_line_join(cairo.LINE_JOIN_ROUND)
        self.c.set_line_cap(cairo.LINE_CAP_ROUND)

    def guardar(self, ruta):
        self.sup.write_to_png(ruta)

    # --- piezas -------------------------------------------------------------------------------
    def parte(self, camino, base, sombra=None, desp=(-20, -20), brillo=True, trazo=TRAZO,
              parpar=False, brillo_pos=(0.27, 0.24)):
        """Rellena una forma con su color, la banda de sombra (lo que queda fuera de la forma
        desplazada arriba-izquierda) y un brillo; luego el contorno de tinta."""
        c = self.c
        regla = cairo.FILL_RULE_EVEN_ODD if parpar else cairo.FILL_RULE_WINDING
        c.save()
        c.new_path(); camino(c); c.set_fill_rule(regla)
        x0, y0, x1, y1 = c.fill_extents()
        c.clip()
        if sombra:
            c.set_source_rgba(*hexa(sombra)); c.paint()
            c.save(); c.translate(*desp); c.new_path(); camino(c); c.set_fill_rule(regla)
            c.set_source_rgba(*hexa(base)); c.fill(); c.restore()
        else:
            c.set_source_rgba(*hexa(base)); c.paint()
        if brillo:
            w, h = x1 - x0, y1 - y0
            bx, by = x0 + w * brillo_pos[0], y0 + h * brillo_pos[1]
            c.save(); c.translate(bx, by); c.rotate(-0.6)
            c.scale(max(w * 0.11, 8), max(min(w, h) * 0.045, 5))
            c.arc(0, 0, 1, 0, 2 * math.pi); c.restore()
            c.set_source_rgba(1, 1, 1, 0.75); c.fill()
        c.restore()
        if trazo:
            c.new_path(); camino(c); c.set_fill_rule(regla)
            c.set_source_rgba(*TINTA); c.set_line_width(trazo); c.stroke()

    def plano(self, camino, color, trazo=TRAZO, parpar=False):
        self.parte(camino, color, None, brillo=False, trazo=trazo, parpar=parpar)

    def linea(self, camino, ancho=10, color=TINTA):
        c = self.c
        c.new_path(); camino(c)
        c.set_source_rgba(*(color if isinstance(color, tuple) else hexa(color)))
        c.set_line_width(ancho); c.stroke()

    def trazo_grueso(self, camino, color, ancho=40, borde=TRAZO):
        """Una línea gruesa de color con contorno de tinta (espirales, cuerdas, interrogaciones)."""
        self.linea(camino, ancho + borde * 2, TINTA)
        self.linea(camino, ancho, color)

    def destello(self, x, y, r, color='FFD45E', trazo=6):
        def cam(c):
            k = r * 0.28
            c.move_to(x, y - r)
            c.curve_to(x + k * 0.3, y - k, x + k, y - k * 0.3, x + r, y)
            c.curve_to(x + k, y + k * 0.3, x + k * 0.3, y + k, x, y + r)
            c.curve_to(x - k * 0.3, y + k, x - k, y + k * 0.3, x - r, y)
            c.curve_to(x - k, y - k * 0.3, x - k * 0.3, y - k, x, y - r)
            c.close_path()
        self.plano(cam, color, trazo)

# --- caminos ---------------------------------------------------------------------------------
def circulo(cx, cy, r):
    return lambda c: (c.new_sub_path(), c.arc(cx, cy, r, 0, 2 * math.pi))

def elipse(cx, cy, rx, ry, giro=0.0):
    def cam(c):
        c.save(); c.translate(cx, cy); c.rotate(giro); c.scale(rx, ry)
        c.new_sub_path(); c.arc(0, 0, 1, 0, 2 * math.pi); c.restore()
    return cam

def rect(x, y, w, h, r=0):
    def cam(c):
        if r <= 0:
            c.rectangle(x, y, w, h); return
        c.new_sub_path()
        c.arc(x + w - r, y + r, r, -math.pi / 2, 0)
        c.arc(x + w - r, y + h - r, r, 0, math.pi / 2)
        c.arc(x + r, y + h - r, r, math.pi / 2, math.pi)
        c.arc(x + r, y + r, r, math.pi, 3 * math.pi / 2)
        c.close_path()
    return cam

def poli(puntos):
    def cam(c):
        c.move_to(*puntos[0])
        for p in puntos[1:]: c.line_to(*p)
        c.close_path()
    return cam

def varios(*caminos):
    def cam(c):
        for k in caminos: k(c)
    return cam

def estrella(cx, cy, r_ext, r_int, puntas=5, giro=-math.pi / 2, irregular=None):
    pts = []
    for i in range(puntas * 2):
        a = giro + i * math.pi / puntas
        r = r_ext if i % 2 == 0 else r_int
        if irregular: r *= irregular[i % len(irregular)]
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return poli(pts)

def curva(puntos):
    """Camino abierto de Catmull-Rom por los puntos."""
    def cam(c):
        p = puntos
        c.move_to(*p[0])
        for i in range(len(p) - 1):
            p0 = p[i - 1] if i > 0 else p[i]
            p1, p2 = p[i], p[i + 1]
            p3 = p[i + 2] if i + 2 < len(p) else p2
            c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
            c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
            c.curve_to(*c1, *c2, *p2)
    return cam

def cinta(centro, ancho_ini, ancho_fin):
    """Forma alargada que sigue una línea y se estrecha (hojas, algas, plumas)."""
    n = len(centro); izq, der = [], []
    for i, (x, y) in enumerate(centro):
        a = centro[min(i + 1, n - 1)]; b = centro[max(i - 1, 0)]
        dx, dy = a[0] - b[0], a[1] - b[1]; l = math.hypot(dx, dy) or 1
        nx, ny = -dy / l, dx / l
        w = (ancho_ini + (ancho_fin - ancho_ini) * i / (n - 1)) / 2
        izq.append((x + nx * w, y + ny * w)); der.append((x - nx * w, y - ny * w))
    return poli(izq + der[::-1])

def muestrear(puntos, n):
    """Puntos repartidos por la curva de Catmull-Rom que pasa por los dados (para formas suaves)."""
    out = []
    m = len(puntos)
    for k in range(n):
        u = k / (n - 1) * (m - 1)
        i = min(int(u), m - 2); t = u - i
        p0 = puntos[i - 1] if i > 0 else puntos[i]
        p1, p2 = puntos[i], puntos[i + 1]
        p3 = puntos[i + 2] if i + 2 < m else p2
        f = lambda a, b, cc, dd: 0.5 * ((2 * b) + (-a + cc) * t + (2 * a - 5 * b + 4 * cc - dd) * t * t + (-a + 3 * b - 3 * cc + dd) * t ** 3)
        out.append((f(p0[0], p1[0], p2[0], p3[0]), f(p0[1], p1[1], p2[1], p3[1])))
    return out
