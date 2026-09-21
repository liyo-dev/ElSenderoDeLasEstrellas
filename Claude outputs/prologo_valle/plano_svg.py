# -*- coding: utf-8 -*-
"""Dibuja el plató en planta, con los 17 planos de cámara y su cono de visión. Es la forma de
juzgar la composición sin abrir Unity: si un cono apunta a un hueco, ahí falta decorado."""
import math, re, os, sys
sys.path.insert(0,'.')
import layout as L, unitylib as u
from shotmath import Actor, resolver

CX, CZ, ESC = 6000, 6000, 13.0          # centro del mundo y píxeles por metro
W, H = 1180, 1180
def px(x, z): return ((x - CX) * ESC + W/2, H/2 - (z - CZ) * ESC)

TAM = {"Church01_a01.prefab":(4.9,7.2,"#8c6f57"), "Building01_a01.prefab":(4.7,6.4,"#a07f63"),
       "Building01_a02.prefab":(4.7,6.4,"#a07f63"), "Building03_a01.prefab":(4.9,6.5,"#a07f63"),
       "Building03_a02.prefab":(4.9,6.5,"#a07f63"), "Building06_a01.prefab":(5.6,6.3,"#a07f63"),
       "Building12_a01.prefab":(5.7,6.3,"#a07f63"), "Building12_a02.prefab":(5.7,6.3,"#a07f63")}

o = ['<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">' % (W,H,W,H)]
o.append('<rect width="100%%" height="100%%" fill="#20301f"/>')
o.append('<g font-family="Segoe UI, sans-serif">')

# rejilla de 5 m
for m in range(-40, 45, 5):
    x1,y1 = px(CX+m, CZ-40); x2,y2 = px(CX+m, CZ+40)
    o.append('<line x1="%.0f" y1="%.0f" x2="%.0f" y2="%.0f" stroke="#2b3d29" stroke-width="1"/>'%(x1,y1,x2,y2))
    x1,y1 = px(CX-40, CZ+m); x2,y2 = px(CX+40, CZ+m)
    o.append('<line x1="%.0f" y1="%.0f" x2="%.0f" y2="%.0f" stroke="#2b3d29" stroke-width="1"/>'%(x1,y1,x2,y2))

# agua
ax, az, ex, ez, _ = L.AGUA
p1 = px(ax-ex*5, az+ez*5); p2 = px(ax+ex*5, az-ez*5)
o.append('<rect x="%.0f" y="%.0f" width="%.0f" height="%.0f" fill="#14506b" opacity="0.8"/>'%(p1[0],p1[1],p2[0]-p1[0],p2[1]-p1[1]))

# decorado
for g,pref,nombre,x,y,z,rot,esc in L.D:
    cx, cy = px(x, z)
    if pref in TAM:
        w,d,col = TAM[pref]; ry = u.euler_de(rot)[1]
        o.append('<g transform="translate(%.1f,%.1f) rotate(%.1f)"><rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="%s" stroke="#5c4634"/></g>'
                 % (cx, cy, -ry, -w*ESC/2, -d*ESC/2, w*ESC, d*ESC, col))
        o.append('<text x="%.0f" y="%.0f" fill="#e8ddcc" font-size="10" text-anchor="middle">%s</text>'%(cx,cy+4,nombre.replace("_"," ")))
    elif nombre.startswith("Arbol"):
        o.append('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="#2f6b2c" opacity="0.85"/>'%(cx,cy,1.6*ESC/2))
    elif nombre.startswith("Loma"):
        o.append('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="#3a5c33" opacity="0.45"/>'%(cx,cy,5*esc*ESC/2 if not isinstance(esc,tuple) else 20))
    elif nombre.startswith("Camino"):
        o.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#6b5a3e" opacity="0.55"/>'%(cx-4.3*ESC/2,cy-4.3*ESC/2,4.3*ESC,4.3*ESC))
    elif nombre.startswith(("Hierba","Flores","Ribera")):
        o.append('<circle cx="%.1f" cy="%.1f" r="3" fill="#3f7a38" opacity="0.7"/>'%(cx,cy))
    else:
        o.append('<circle cx="%.1f" cy="%.1f" r="4" fill="#c9a227"/>'%(cx,cy))

# marcas
for nombre,x,z,rot in L.MARCAS:
    if nombre == "M_Espera_Oscuro": continue
    cx, cy = px(x, z)
    o.append('<circle cx="%.1f" cy="%.1f" r="6" fill="none" stroke="#ffd27f" stroke-width="2"/>'%(cx,cy))
    o.append('<text x="%.0f" y="%.0f" fill="#ffd27f" font-size="11" text-anchor="middle">%s</text>'%(cx,cy-10,nombre[2:].replace("_"," ")))

# los planos: cono de visión de cada cámara
import verifica as V
COLOR_FASE = {"1":"#ffd27f","2":"#9ad0ff","3":"#ff9d6e","4":"#c9a6ff","5":"#ff7b7b","6":"#ff7b7b"}
for n, fase, tipo, pos, yaw, fov in V.recorrido(verboso=False):
    col = COLOR_FASE.get(fase.strip()[0], "#ffffff")
    cx, cy = px(pos[0], pos[2])
    hh = math.degrees(math.atan(math.tan(math.radians(fov)*0.5)*16/9))
    largo_cono = 26 * ESC
    p = [(cx, cy)]
    for a in (yaw - hh, yaw + hh):
        r = math.radians(a)
        p.append((cx + math.sin(r)*largo_cono, cy - math.cos(r)*largo_cono))
    o.append('<polygon points="%s" fill="%s" opacity="0.10"/>' % (" ".join("%.0f,%.0f"%q for q in p), col))
    o.append('<circle cx="%.1f" cy="%.1f" r="7" fill="%s" opacity="0.9"/>'%(cx,cy,col))
    o.append('<text x="%.0f" y="%.0f" fill="#101810" font-size="10" font-weight="bold" text-anchor="middle">%d</text>'%(cx,cy+3,n))

# leyenda
o.append('<rect x="14" y="14" width="330" height="128" fill="#101810" opacity="0.85" rx="6"/>')
o.append('<text x="28" y="40" fill="#e8ddcc" font-size="15" font-weight="bold">El valle del prólogo — planta del plató</text>')
o.append('<text x="28" y="62" fill="#b8c4b0" font-size="12">Los números son los 17 planos; el cono, lo que ve cada cámara.</text>')
o.append('<text x="28" y="80" fill="#b8c4b0" font-size="12">Los círculos amarillos son las marcas donde se coloca cada actor.</text>')
o.append('<text x="28" y="98" fill="#b8c4b0" font-size="12">Norte arriba. Rejilla de 5 m. Todo a escala 1:1 del juego.</text>')
o.append('<text x="28" y="122" fill="#ffd27f" font-size="12">mañana ■</text>')
o.append('<text x="108" y="122" fill="#9ad0ff" font-size="12">carta ■</text>')
o.append('<text x="175" y="122" fill="#ff9d6e" font-size="12">la hora ■</text>')
o.append('<text x="248" y="122" fill="#c9a6ff" font-size="12">adiós ■</text>')
o.append('<text x="310" y="122" fill="#ff7b7b" font-size="12">duelo ■</text>')
o.append('</g></svg>')
open("plano_del_plato.svg","w",encoding="utf-8").write("\n".join(o))
print("escrito plano_del_plato.svg")
