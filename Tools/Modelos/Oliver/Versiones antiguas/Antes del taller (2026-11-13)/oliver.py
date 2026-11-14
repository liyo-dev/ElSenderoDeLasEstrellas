# Oliver, vestuario propio: aprendiz. Túnica de estudiante azul cielo, algo grande (mangas anchas que le
# llegan a la mano, bajo por el muslo), con cuello y ribetes naranjas, cordón a la cintura, zurrón cruzado
# con un pergamino, pantalón y botas. Propuesta aprobada por Raúl el 11 nov 2026.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body29_Aprendiz → las manos y el torso de Body05 con la ropa encima, pesado como él.
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

COLORES = {
    "Tunica": (0.50, 0.74, 0.93, 1), "TunicaOscura": (0.3, 0.52, 0.75, 1), "Naranja": (0.96, 0.55, 0.16, 1), "Cordon": (0.93, 0.87, 0.7, 1),
    "Cuero": (0.42, 0.26, 0.14, 1), "CueroOscuro": (0.25, 0.15, 0.08, 1), "Papel": (0.95, 0.91, 0.8, 1), "Pantalon": (0.36, 0.36, 0.42, 1), "Laton": (0.8, 0.62, 0.25, 1),
}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

GRUPOS = {"traje": []}
def nueva(grupo, nombre, bm, mat, pesos=None, grosor=0.0):
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(MAT[mat])
    for p in o.data.polygons: p.use_smooth = True
    if grosor:
        s = o.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = grosor; s.offset = 1
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
ZAPATO = "CueroOscuro"
# ── Túnica grande: le sobra por todas partes ──
tunica = torno("traje", "Tunica", [(0.725, 0.15), (0.67, 0.205), (0.58, 0.216), (0.5, 0.208), (0.45, 0.21), (0.36, 0.232), (0.29, 0.25)], "Tunica", grosor=0.009)
toro("traje", "Bajo", (0, -0.01, 0.292), 0.249, 0.013, "Naranja", None, escala=(1, 0.92, 1))
# el cuello copia los pesos de la túnica en la que se apoya: con los del cuerpo se separaba de ella al bajar los brazos
torno("traje", "Cuello", [(0.775, 0.17), (0.75, 0.2), (0.715, 0.21)], "Naranja", rl.Zona(sobre=tunica), seg=40, grosor=0.014)
tubo("traje", "Tapeta", curva((0.0, -0.17, 0.72), (0.0, -0.228, 0.6), (0.0, -0.21, 0.47), 8), [0.015] * 8, "Naranja", None, seg=8)
# Cordón a la cintura con el nudo y dos cabos
toro("traje", "Cordon", (0, -0.01, 0.455), 0.211, 0.013, "Cordon", None, escala=(1, 0.92, 1))
esfera("traje", "Nudo", (-0.07, -0.212, 0.455), (0.022, 0.016, 0.022), "Cordon", None)
for k, dx in enumerate((-0.085, -0.055)):
    tubo("traje", f"CaboCordon{k}", curva((-0.07, -0.212, 0.45), (dx, -0.245, 0.4), (dx, -0.25, 0.34 + 0.03 * k), 6), [0.01] * 6, "Cordon", {"pelvis": 1.0}, seg=6)
# Zurrón cruzado, pequeño, con un pergamino enrollado que asoma
tubo("traje", "Correa", curva((-0.15, -0.09, 0.7), (-0.02, -0.38, 0.6), (0.23, -0.13, 0.43), 12), [0.016] * 12, "Cuero", None, seg=8)
tubo("traje", "CorreaAtras", curva((-0.15, 0.06, 0.7), (-0.02, 0.36, 0.6), (0.23, 0.1, 0.43), 12), [0.016] * 12, "Cuero", None, seg=8)
P = {"pelvis": 1.0}
caja("traje", "Zurron", (0.265, -0.015, 0.36), (0.075, 0.17, 0.14), "Cuero", P, rot=(0, R(-6), 0))
caja("traje", "Tapa", (0.306, -0.015, 0.395), (0.013, 0.18, 0.08), "CueroOscuro", P, rot=(0, R(-6), 0))
esfera("traje", "Cierre", (0.315, -0.015, 0.365), (0.008, 0.018, 0.018), "Laton", P)
tubo("traje", "Pergamino", [(0.25, -0.03, 0.4), (0.245, -0.045, 0.52)], [0.024, 0.024], "Papel", P, seg=10)
for lado, s in (("l", 1), ("r", -1)):
    # manga ancha que se abre hacia la mano y casi la tapa
    tubo("traje", f"Manga_{lado}", [(0.17 * s, -0.003, 0.636), (0.27 * s, -0.003, 0.634), (0.38 * s, -0.003, 0.63), (0.48 * s, -0.003, 0.625)],
         [0.088, 0.086, 0.092, 0.104], "Tunica", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.175 * s, -0.003, 0.64), (0.092, 0.088, 0.088), "Tunica", None)
    toro("traje", f"Puno_{lado}", (0.48 * s, -0.003, 0.625), 0.104, 0.014, "Naranja", {f"lowerarm_{lado}": 0.6, f"hand_{lado}": 0.4}, rot=(0, R(90), 0))
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.22), (0.116 * s, 0.004, 0.11)],
         [0.092, 0.086, 0.078, 0.074], "Pantalon", None, cerrar=False)
    o = torno("traje", f"Bota_{lado}", [(0.0, 0.082), (0.04, 0.087), (0.1, 0.074), (0.13, 0.076), (0.15, 0.084)], ZAPATO, None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), ZAPATO, {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), ZAPATO, {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# ── Pesos y unión ─────────────────────────────────────────────────────────────────────────
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
# La piel de los brazos usa el material del propio cuerpo (el de las manos y la cabeza) con sus UV puestas en
# el color de piel de la textura: así es exactamente el mismo tono que las manos.
uv_cuerpo = cuerpo.data.uv_layers.active
piel_uv = None
for p in cuerpo.data.polygons:
    if abs((cuerpo.matrix_world @ p.center).x) > 0.5:
        piel_uv = tuple(uv_cuerpo.data[p.loop_indices[0]].uv); break
for o, _ in GRUPOS["traje"]:
    if o.data.materials[0].name.startswith("Piel"):
        o.data.materials.clear(); o.data.materials.append(cuerpo.data.materials[0])
        uv = o.data.uv_layers.new(name=uv_cuerpo.name)
        for d in uv.data: d.uv = piel_uv
rl.pesar_piezas(rl.completar_zonas(GRUPOS["traje"]), cuerpo, arm)
# Lo del cuerpo de debajo que queda tapado por la ropa se quita (después de copiar los pesos: la ropa los
# toma del cuerpo entero). Del Body05 solo quedan las manos y el torso.
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
fuera = [v for v in bm.verts if (mw @ v.co).z < 0.42 or (0.19 < abs((mw @ v.co).x) < 0.455 and (mw @ v.co).z > 0.55)]
bmesh.ops.delete(bm, geom=fuera, context='VERTS')
bm.to_mesh(cuerpo.data); bm.free()
cuerpo.name = "Body29_Aprendiz"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body29_Aprendiz", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
