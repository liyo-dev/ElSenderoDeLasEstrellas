# El Mago Oscuro, vestuario propio: figura imponente de sombra (túnica hasta el suelo, cuello alto, capa rota con
# estrellas apagadas, cuernos de hombro, hebilla de agujero negro, corona de fragmentos). El rostro va tapado por un
# VELO (accesorio aparte, AC14_VeloDeSombra: capucha + gorjal; con él puesto la cabeza va en negro con ojos de luz) que se quita al final para ver la cara.
# Dirección elegida por Raúl el 12 nov 2026 (figura de sombra, sin cara hasta el final).
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body33_MagoOscuro → las manos y el torso de Body05 con la ropa encima, pesado como él.
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Euler
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Prendas"))
import ropa_lib as rl
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

GRUPOS = {"traje": [], "velo": []}
def nueva(grupo, nombre, bm, mat, pesos=None, grosor=0.0):
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(MAT[mat[0] if isinstance(mat, tuple) else mat])
    if isinstance(mat, tuple): o.data.materials.append(MAT[mat[1]])
    for p in o.data.polygons: p.use_smooth = True
    if grosor:
        s = o.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = grosor; s.offset = 1; s.material_offset = 1 if isinstance(mat, tuple) else 0
    GRUPOS[grupo].append((o, pesos)); return o

def transformar(bm, centro, escala, rot):
    M = Matrix.Translation(centro) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*escala, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)

def esfera(g, nombre, centro, escala, mat, pesos, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=1)
    transformar(bm, centro, escala, rot); return nueva(g, nombre, bm, mat, pesos)

def toro(g, nombre, centro, mayor, menor, mat, pesos, rot=(0, 0, 0), escala=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=mayor, minor_radius=menor, major_segments=28, minor_segments=10)
    t = bpy.context.object; bm = bmesh.new(); bm.from_mesh(t.data); bpy.data.objects.remove(t)
    transformar(bm, centro, escala, rot); return nueva(g, nombre, bm, mat, pesos)

def torno(g, nombre, perfil, mat, pesos=None, seg=40, elip=0.92, cy=-0.01, onda=0.0, n_onda=8, grosor=0.0, abierto=None):
    """abierto=(a0, a1): deja sin cerrar ese arco (radianes, 0 = +X, π/2 = +Y atrás) — para la capa."""
    bm = bmesh.new(); filas = []
    for f, (z, r) in enumerate(perfil):
        u = f / max(1, len(perfil) - 1)
        fila = []
        for k in range(seg + (1 if abierto else 0)):
            if abierto: a = abierto[0] + (abierto[1] - abierto[0]) * k / seg
            else: a = 2*math.pi*k/seg
            rr = r * (1 + onda * u * math.sin(n_onda * a))
            fila.append(bm.verts.new((math.cos(a) * rr, cy + math.sin(a) * rr * elip, z)))
        filas.append(fila)
    n = len(filas[0])
    for f in range(len(filas) - 1):
        for k in range(n if not abierto else n - 1):
            cara = (filas[f][k], filas[f][(k+1) % n], filas[f+1][(k+1) % n], filas[f+1][k])
            bm.faces.new(cara if perfil[0][0] < perfil[-1][0] else cara[::-1])   # siempre hacia fuera (en Unity no se pinta el revés)
    return nueva(g, nombre, bm, mat, pesos, grosor)

def tubo(g, nombre, puntos, radios, mat, pesos, seg=14, cerrar=True):
    """Tubo a lo largo de una curva de puntos, con su radio en cada punto (mechones, coleta)."""
    bm = bmesh.new(); filas = []
    pts = [Vector(p) for p in puntos]
    if len(radios) != len(pts):   # se reparten los radios a lo largo del tubo
        radios = [radios[min(len(radios)-1, round(i * (len(radios)-1) / max(1, len(pts)-1)))] for i in range(len(pts))]
    # Marco que se transporta a lo largo de la curva (sin giros bruscos que retuerzan el tubo)
    t_ant = None; a = None
    for i, p in enumerate(pts):
        t = (pts[min(i+1, len(pts)-1)] - pts[max(i-1, 0)]).normalized()
        if a is None: a = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0)); a = (a - t * a.dot(t)).normalized()
        else: a = (t_ant.rotation_difference(t) @ a); a = (a - t * a.dot(t)).normalized()
        t_ant = t; b = t.cross(a)
        filas.append([bm.verts.new(p + (a * math.cos(2*math.pi*k/seg) + b * math.sin(2*math.pi*k/seg)) * radios[i]) for k in range(seg)])
    for f in range(len(filas) - 1):
        for k in range(seg):
            bm.faces.new((filas[f][k], filas[f][(k+1) % seg], filas[f+1][(k+1) % seg], filas[f+1][k]))
    if cerrar:
        for fila, inv in ((filas[0], True), (filas[-1], False)):
            c = bm.verts.new(sum((v.co for v in fila), Vector()) / len(fila))
            for k in range(seg):
                tri = (c, fila[k], fila[(k+1) % seg]); bm.faces.new(tri if not inv else tri[::-1])
    return nueva(g, nombre, bm, mat, pesos)

def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]


def caja(g, nombre, centro, medidas, mat, pesos, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    transformar(bm, centro, medidas, rot); o = nueva(g, nombre, bm, mat, pesos)
    for p in o.data.polygons: p.use_smooth = False
    return o


def caja(g, nombre, centro, medidas, mat, pesos, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    transformar(bm, centro, medidas, rot); o = nueva(g, nombre, bm, mat, pesos)
    for p in o.data.polygons: p.use_smooth = False
    return o
R = math.radians
import random
random.seed(7)
# ── Colores: negro violáceo, forro violeta que "tiene luz", hueso frío ────────────────────────
COLORES = {
    "Tunica": (0.075, 0.055, 0.13, 1), "Capa": (0.05, 0.038, 0.095, 1), "Forro": (0.30, 0.12, 0.52, 1),
    "Luz": (0.74, 0.45, 1.0, 1), "Hueso": (0.66, 0.62, 0.78, 1), "Vacio": (0.012, 0.008, 0.025, 1),
    "Estrella": (0.40, 0.24, 0.70, 1), "Cinto": (0.12, 0.09, 0.19, 1), "Piel": (0.80, 0.74, 0.78, 1),
}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

# ── Túnica hasta el suelo, con el bajo ondulado (esconde las piernas: sirve cuando levita) ──
# Pesos (ropa_lib): el tronco copia solo del tronco y las piernas; por debajo de la cintura es faldón (pelvis y muslos
# repartidos sin saltos: antes el bajo se inclinaba entero hacia una pierna, como un plato).
FALDON = rl.Faldon(z_cintura=0.40, z_bajo=0.06, muslo=0.55)
ZT = rl.Zona(rl.TRONCO + rl.PIERNAS, FALDON)
# Bajo el brazo no pasa de 0,195 (el cuerpo mide 0,18 y el brazo arranca en 0,20).
PERFIL_TUNICA = [(0.72, 0.15), (0.66, 0.19), (0.55, 0.198), (0.45, 0.2), (0.32, 0.25), (0.18, 0.33), (0.06, 0.42)]
torno("traje", "Tunica", PERFIL_TUNICA, ("Tunica", "Forro"), ZT, grosor=0.01, onda=0.09, n_onda=9)
# Franja violeta que "sangra luz" por el bajo
torno("traje", "BajoLuz", [(0.075, 0.425), (0.035, 0.442)], "Luz", ZT, grosor=0.006, onda=0.09, n_onda=9)
# ── Cinto con el agujero negro como hebilla ──
toro("traje", "Cinto", (0, -0.01, 0.45), 0.205, 0.024, "Cinto", ZT, escala=(1, 0.92, 1))
toro("traje", "AroLuz", (0.0, -0.215, 0.45), 0.052, 0.011, "Luz", {"pelvis": 1.0}, rot=(R(90), 0, 0))   # hebilla rígida
esfera("traje", "Vacio", (0.0, -0.2, 0.45), (0.046, 0.022, 0.046), "Vacio", {"pelvis": 1.0})
# ── Cuello alto en abanico por la espalda, con la punta central más alta (enmarca la cabeza sin taparla) ──
ARCO_CUELLO = (R(26), R(154))
o_cuello = torno("traje", "Cuello", [(0.67, 0.25), (0.8, 0.34), (0.95, 0.42), (1.1, 0.47), (1.24, 0.5)], ("Capa", "Forro"), abierto=ARCO_CUELLO, grosor=0.012,
      pesos={"spine_03": 0.55, "neck_01": 0.45}, cy=0.03)
for v in o_cuello.data.vertices:
    if v.co.z > 1.2:
        ang = math.degrees(math.atan2((v.co.y - 0.03) / 0.92, v.co.x))
        v.co.z += 0.34 * max(0.0, 1 - abs(ang - 90) / 64) ** 1.3
# ── Capa larga y rota por la espalda, con estrellas apagadas ──
# Solo por la espalda (antes de -18° a 198°: llegaba a los costados y los brazos la atravesaban al andar).
ARCO = (R(28), R(152))
PERFIL_CAPA = [(0.74, 0.215), (0.66, 0.262), (0.52, 0.29), (0.36, 0.33), (0.2, 0.39), (0.05, 0.45)]
# Sigue a la espalda y por abajo cuelga como faldón (muslos poco: es una capa, no una falda)
ZC = rl.Zona(rl.TRONCO + rl.PIERNAS, rl.Faldon(z_cintura=0.40, z_bajo=0.05, muslo=0.3), suavizar=8)
o_capa = torno("traje", "Capa", PERFIL_CAPA, ("Capa", "Forro"), ZC, abierto=ARCO, grosor=0.012, onda=0.07, n_onda=7)
def radio_capa(z):
    for (z0, r0), (z1, r1) in zip(PERFIL_CAPA, PERFIL_CAPA[1:]):
        if z1 <= z <= z0: return r1 + (r0 - r1) * (z - z1) / (z0 - z1)
    return PERFIL_CAPA[-1][1]
for k in range(16):   # estrellas apagadas: puntitos de un cielo que ya no brilla
    z = random.uniform(0.14, 0.62); a = R(random.uniform(38, 142)); r = radio_capa(z) * 0.92 + 0.013
    esfera("traje", f"Estrella{k}", (math.cos(a) * r, -0.01 + math.sin(a) * r, z), (0.011, 0.007, 0.011), "Estrella", rl.Zona(sobre=o_capa))
# ── Cuernos de hombro que curvan hacia arriba (silueta) ──
for s in (1, -1):
    tubo("traje", f"Cuerno{s}", curva((0.17 * s, 0.02, 0.69), (0.40 * s, 0.03, 0.74), (0.43 * s, 0.03, 1.02), 9),
         [0.062, 0.056, 0.05, 0.043, 0.035, 0.027, 0.018, 0.01, 0.003], "Hueso", {"spine_03": 1.0}, seg=10)
    esfera("traje", f"Hombro{s}", (0.185 * s, 0.0, 0.655), (0.1, 0.09, 0.085), "Tunica", {"spine_03": 1.0})
# ── Mangas anchas que caen, con puño de luz ──
for lado, s in (("l", 1), ("r", -1)):
    tubo("traje", f"Manga_{lado}", [(0.17 * s, -0.003, 0.636), (0.26 * s, -0.003, 0.635), (0.35 * s, -0.003, 0.634), (0.45 * s, -0.003, 0.633)],
         [0.09, 0.088, 0.092, 0.1], ("Tunica", "Forro"), rl.Zona(rl.BRAZO(lado)), cerrar=False)
    # puño ceñido a la boca de la manga (antes 0,115 para un brazo de 0,08: el aro bailaba alrededor de la mano)
    toro("traje", f"Puno_{lado}", (0.448 * s, -0.003, 0.633), 0.101, 0.012, "Luz", {f"lowerarm_{lado}": 1.0}, rot=(0, R(90), 0))
# ── Cresta de pinchos que nace del borde del cuello (pegados a él, no flotando; siguen al cuello) ──
for k in range(7):
    a = R(42 + 96 * k / 6); r = 0.5
    cima = 0.34 * max(0.0, 1 - abs(math.degrees(a) - 90) / 64) ** 1.3   # altura del borde en ese punto (la punta central es la más alta)
    cx, cy, z = math.cos(a) * r, 0.03 + math.sin(a) * r * 0.92, 1.24 + cima - 0.05
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=5, radius1=0.034, radius2=0.0, depth=0.2 + 0.07 * (1 - abs(k - 3) / 3))
    transformar(bm, (cx, cy, z + 0.1), (1, 0.7, 1), (0, R(26) * math.cos(a), 0))
    nueva("traje", f"Pincho{k}", bm, "Hueso", {"spine_03": 0.55, "neck_01": 0.45})
# ── EL VELO (accesorio aparte: «AC14_VeloDeSombra»; sin él se ve la cara, para el final) ──
# Capucha alta y apuntada que cae a la espalda, con gorjal que tapa la mandíbula del pack.
CC = (0.0, -0.013, 0.99)   # centro de la cabeza del pack
torno("velo", "Capucha", [(0.8, 0.30), (0.9, 0.385), (1.0, 0.415), (1.12, 0.405), (1.24, 0.345), (1.34, 0.23), (1.41, 0.1)], ("Capa", "Forro"),
      abierto=(R(-58), R(238)), grosor=0.012, pesos={"head": 1.0}, cy=0.02, elip=0.95)
tubo("velo", "PuntaCapucha", curva((0.0, 0.03, 1.38), (0.0, 0.2, 1.62), (0.0, 0.5, 1.42), 8), [0.1, 0.085, 0.07, 0.056, 0.042, 0.028, 0.016, 0.006], "Capa", {"head": 1.0}, seg=10)
torno("velo", "Gorjal", [(0.60, 0.2), (0.66, 0.27), (0.725, 0.31)], "Vacio", pesos={"head": 1.0}, seg=32, elip=1.0, cy=-0.013)   # bajo la barbilla: deja a la vista la boca cuando la cara va descubierta
# (La cara negra y los ojos de luz ya no son malla del velo: la cabeza va en negro mientras lleva el velo y los ojos salen
#  del sistema de caras, juego «Caras_mago_oscuro_sombra» Eye300-309; al final se quita el velo y se vuelve a la cabeza de piel
#  con «Caras_mago_oscuro» Eye290-299.)

# ── Pesos y unión ─────────────────────────────────────────────────────────────────────────
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
# La piel de los brazos usa el material del propio cuerpo (manos y cabeza), con sus UV en el color de piel.
uv_cuerpo = cuerpo.data.uv_layers.active
piel_uv = None
for p in cuerpo.data.polygons:
    if abs((cuerpo.matrix_world @ p.center).x) > 0.5:
        piel_uv = tuple(uv_cuerpo.data[p.loop_indices[0]].uv); break
rl.pesar_piezas(GRUPOS["velo"], cuerpo, arm, con_cuerpo=False)
rl.pesar_piezas(GRUPOS["traje"], cuerpo, arm)
# Del Body05 solo quedan las manos y el torso (lo demás lo tapa la ropa), después de copiar los pesos.
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
fuera = [v for v in bm.verts if (mw @ v.co).z < 0.42 or (0.19 < abs((mw @ v.co).x) < 0.455 and (mw @ v.co).z > 0.55)]
bmesh.ops.delete(bm, geom=fuera, context='VERTS')
bm.to_mesh(cuerpo.data); bm.free()
cuerpo.name = "Body33_MagoOscuro"
unir(GRUPOS["traje"], "Body33_MagoOscuro", extra=cuerpo)
unir(GRUPOS["velo"], "AC14_VeloDeSombra")
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
