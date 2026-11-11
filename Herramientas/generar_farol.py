"""Genera el farol de estrellas (OBJ, metros, Y arriba) y una vista previa.
Cada primitiva es convexa (o estrellada respecto a su centro): sus caras se orientan hacia fuera
respecto a su propio centro."""
import math, random
import numpy as np

class Parte:
    def __init__(s, nombre): s.nombre = nombre; s.caras = []
    def pieza(s, caras):
        caras = [[np.array(p, float) for p in c] for c in caras]
        todos = [v for c in caras for v in c]
        cb = sum(todos)/len(todos)
        for c in caras:
            cen = sum(c)/len(c)
            if np.dot(normal(c), cen-cb) < 0: c = c[::-1]
            s.caras.append(c)

def normal(c):
    n = np.zeros(3)
    for k in range(1, len(c)-1): n += np.cross(c[k]-c[0], c[k+1]-c[0])
    return n/(np.linalg.norm(n)+1e-12)

def anillo(n, r, y, rot):
    return [(r*math.cos(rot+2*math.pi*i/n), y, r*math.sin(rot+2*math.pi*i/n)) for i in range(n)]

def prisma(n, rb, rt, y0, y1, rot=None):
    if rot is None: rot = math.pi/n
    a = anillo(n, rb, y0, rot); b = anillo(n, rt, y1, rot)
    return [[a[i], b[i], b[(i+1)%n], a[(i+1)%n]] for i in range(n)] + [a, b]

def caja(cx, cz, hx, hz, y0, y1):
    P = lambda y: [(cx-hx, y, cz-hz), (cx+hx, y, cz-hz), (cx+hx, y, cz+hz), (cx-hx, y, cz+hz)]
    a, b = P(y0), P(y1)
    return [[a[i], b[i], b[(i+1)%4], a[(i+1)%4]] for i in range(4)] + [a, b]

def piramide(n, r, y0, y1, rot):
    a = anillo(n, r, y0, rot)
    return [[a[i], (0, y1, 0), a[(i+1)%n]] for i in range(n)] + [a]

def construir(semilla=7):
    piedra, tejado, losas, estrella = Parte("Piedra"), Parte("Tejado"), Parte("Losas"), Parte("Estrella")
    y = 0.0
    piedra.pieza(prisma(8, 0.46, 0.42, y, y+0.10)); y += 0.10    # zócalo
    piedra.pieza(prisma(8, 0.36, 0.33, y, y+0.10)); y += 0.10    # escalón
    piedra.pieza(prisma(8, 0.20, 0.16, y, y+0.50)); y += 0.50    # fuste
    piedra.pieza(prisma(8, 0.22, 0.35, y, y+0.09)); y += 0.09    # capitel
    piedra.pieza(caja(0, 0, 0.32, 0.32, y, y+0.07)); y += 0.07   # suelo de la linterna
    suelo = y; hueco = 0.46
    for sx in (-1, 1):
        for sz in (-1, 1):
            piedra.pieza(caja(sx*0.255, sz*0.255, 0.065, 0.065, y, y+hueco))
    for (cx, cz, hx, hz) in ((0, .255, .19, .035), (0, -.255, .19, .035), (.255, 0, .035, .19), (-.255, 0, .035, .19)):
        piedra.pieza(caja(cx, cz, hx, hz, y, y+0.06))
    y += hueco
    centro_estrella = suelo + 0.06 + (hueco-0.06)/2
    piedra.pieza(caja(0, 0, 0.34, 0.34, y, y+0.06)); y += 0.06   # techo de la linterna
    tejado.pieza(caja(0, 0, 0.52, 0.52, y, y+0.07)); y += 0.07   # alero
    r45 = math.pi/4
    tejado.pieza(prisma(4, 0.52*math.sqrt(2), 0.30*math.sqrt(2), y, y+0.10, rot=r45)); y += 0.10
    tejado.pieza(piramide(4, 0.30*math.sqrt(2), y, y+0.24, r45)); y += 0.24
    yb, rb = y + 0.01, 0.075
    ecu = anillo(6, rb, yb+rb, 0)
    bola = []
    for i in range(6):
        j = (i+1) % 6
        bola += [[ecu[i], (0, yb+2*rb, 0), ecu[j]], [ecu[j], (0, yb, 0), ecu[i]]]
    tejado.pieza(bola)
    alto = yb + 2*rb

    # Estrella en su origen (el prefab la coloca); plano XY, gruesa y abombada.
    R, r, gr, gc = 0.19, 0.088, 0.038, 0.08
    borde = []
    for i in range(10):
        a = math.pi/2 + i*math.pi/5
        rad = R if i % 2 == 0 else r
        borde.append((rad*math.cos(a), rad*math.sin(a)))
    caras = []
    for i in range(10):
        (x0, y0), (x1, y1) = borde[i], borde[(i+1) % 10]
        caras += [[(x0, y0, gr), (x1, y1, gr), (0, 0, gc)],
                  [(x1, y1, -gr), (x0, y0, -gr), (0, 0, -gc)],
                  [(x0, y0, -gr), (x1, y1, -gr), (x1, y1, gr), (x0, y0, gr)]]
    # la estrella no es convexa: se orienta cara a cara respecto al origen
    for c in caras:
        c = [np.array(p, float) for p in c]
        cen = sum(c)/len(c)
        if np.dot(normal(c), cen) < 0: c = c[::-1]
        estrella.caras.append(c)

    random.seed(semilla)
    N = 9
    for k in range(N):
        a = 2*math.pi*k/N + random.uniform(-0.12, 0.12)
        d = random.uniform(0.74, 0.86)
        cx, cz = d*math.cos(a), d*math.sin(a)
        rr = random.uniform(0.15, 0.2); lados = random.choice((5, 6)); rot0 = random.uniform(0, 6.28)
        top, bot = [], []
        for i in range(lados):
            t = rot0 + 2*math.pi*i/lados; q = rr*random.uniform(0.8, 1.1)
            top.append((cx+q*math.cos(t), 0.035, cz+q*math.sin(t)))
            bot.append((cx+q*1.08*math.cos(t), -0.04, cz+q*1.08*math.sin(t)))
        losas.pieza([[bot[i], top[i], top[(i+1) % lados], bot[(i+1) % lados]] for i in range(lados)] + [top, bot])
    return [piedra, tejado, losas, estrella], centro_estrella, alto

def escribir_obj(partes, ruta):
    """Un objeto por parte, normales planas (vértices repetidos por cara)."""
    L = ["# Farol de estrellas — El Sendero de las Estrellas (generado). Metros, Y arriba."]
    vi = ni = 1
    for p in partes:
        L.append(f"o {p.nombre}")
        L.append(f"g {p.nombre}")
        L.append(f"usemtl {p.nombre}")
        for c in p.caras:
            n = normal(c)
            for v in c: L.append(f"v {v[0]:.5f} {v[1]:.5f} {v[2]:.5f}")
            L.append(f"vn {n[0]:.5f} {n[1]:.5f} {n[2]:.5f}")
            L.append("f " + " ".join(f"{vi+k}//{ni}" for k in range(len(c))))
            vi += len(c); ni += 1
    open(ruta, "w", newline="\n").write("\n".join(L) + "\n")

COLORES = {"Piedra": (0.78, 0.74, 0.66), "Tejado": (0.33, 0.40, 0.56), "Losas": (0.70, 0.67, 0.60), "Estrella": (1.0, 0.85, 0.30)}

def vista(partes, centro_estrella, ruta, encendida=True, elev=14, azim=-58):
    import matplotlib; matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection
    fig = plt.figure(figsize=(6, 7), dpi=110)
    ax = fig.add_subplot(111, projection="3d"); ax.set_proj_type("persp", focal_length=0.6)
    luz = np.array([0.4, 0.8, 0.45]); luz /= np.linalg.norm(luz)
    polys, cols = [], []
    for p in partes:
        off = np.array([0, centro_estrella, 0]) if p.nombre == "Estrella" else 0
        base = np.array(COLORES[p.nombre] if (p.nombre != "Estrella" or encendida) else (0.55, 0.55, 0.58))
        for c in p.caras:
            pts = [v + off for v in c]
            # matplotlib: Z arriba
            polys.append([(v[0], v[2], v[1]) for v in pts])
            k = 0.55 + 0.45*max(0, np.dot(normal(c), luz))
            if p.nombre == "Estrella" and encendida: k = 1.0
            cols.append(np.clip(base*k, 0, 1))
    ax.add_collection3d(Poly3DCollection(polys, facecolors=cols, edgecolors=(0.15, 0.12, 0.1, 0.9), linewidths=0.6))
    ax.set_xlim(-0.9, 0.9); ax.set_ylim(-0.9, 0.9); ax.set_zlim(0, 1.8)
    ax.set_box_aspect((1.8, 1.8, 1.8)); ax.view_init(elev=elev, azim=azim); ax.axis("off")
    fig.patch.set_facecolor((0.45, 0.75, 0.40)); plt.tight_layout(); fig.savefig(ruta); plt.close(fig)

if __name__ == "__main__":
    import sys
    partes, ce, alto = construir()
    print("centro_estrella", round(ce, 3), "alto", round(alto, 3), "caras", sum(len(p.caras) for p in partes))
    escribir_obj(partes, "FarolDeEstrellas.obj")
    vista(partes, ce, "vista_encendida.png", True)
    vista(partes, ce, "vista_apagada.png", False, elev=28, azim=-30)
