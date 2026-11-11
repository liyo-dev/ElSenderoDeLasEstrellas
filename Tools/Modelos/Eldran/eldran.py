# Eldran, vestuario propio: abrigo largo de mago viejo, verde musgo apagado, con coderas de cuero y
# bufanda gris; cinturón, zurrón cruzado con libros, pantalón y botas. La cabeza, la barba, el pelo y los
# ojos siguen siendo los suyos (Head05_Santa, Hair13). Propuesta aprobada por Raúl el 11 nov 2026.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body26_AbrigoDeMago → las manos y el torso de Body05 con la ropa encima, pesado como él.
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Abrigo": (0.25, 0.33, 0.19, 1), "AbrigoOscuro": (0.15, 0.21, 0.12, 1), "Forro": (0.55, 0.47, 0.30, 1),
    "Bufanda": (0.62, 0.62, 0.64, 1), "BufandaOscura": (0.45, 0.45, 0.48, 1),
    "Pantalon": (0.22, 0.18, 0.15, 1), "Cuero": (0.32, 0.19, 0.10, 1), "CueroOscuro": (0.2, 0.12, 0.07, 1),
    "Laton": (0.80, 0.62, 0.25, 1), "LibroRojo": (0.55, 0.13, 0.12, 1), "LibroAzul": (0.16, 0.25, 0.45, 1), "Papel": (0.93, 0.89, 0.78, 1),
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

# ── Abrigo: largo hasta la rodilla y con vuelo, para que las piernas se muevan dentro sin estirarlo ──
torno("traje", "Abrigo", [(0.725, 0.15), (0.67, 0.2), (0.58, 0.212), (0.5, 0.203), (0.45, 0.203), (0.36, 0.228), (0.27, 0.25), (0.19, 0.268)],
      "Abrigo", grosor=0.009)
toro("traje", "Bajo", (0, -0.01, 0.192), 0.266, 0.011, "AbrigoOscuro", None, escala=(1, 0.92, 1))
# Solapas: el borde del abrigo baja por delante, del cuello al bajo, y tres botones de latón
tubo("traje", "Solapa", curva((0.04, -0.165, 0.72), (0.03, -0.225, 0.5), (0.03, -0.262, 0.2), 10), [0.013] * 10, "AbrigoOscuro", None)
for z, y in ((0.62, -0.212), (0.54, -0.21), (0.38, -0.225)):
    esfera("traje", f"Boton{int(z*100)}", (0.075, y, z), (0.017, 0.008, 0.017), "Laton", None)
# Cuello alto y vuelto
torno("traje", "Cuello", [(0.77, 0.165), (0.745, 0.2), (0.71, 0.218)], "AbrigoOscuro", seg=40, grosor=0.014)
# Cinturón de cuero con hebilla, por fuera del abrigo
toro("traje", "Cinturon", (0, -0.01, 0.45), 0.208, 0.019, "CueroOscuro", None, escala=(1, 0.92, 1))
esfera("traje", "Hebilla", (0.0, -0.208, 0.45), (0.03, 0.013, 0.027), "Laton", None)
# Bufanda gris: una vuelta gorda al cuello y dos cabos, uno por delante y otro echado a la espalda
toro("traje", "Bufanda", (0, -0.012, 0.722), 0.185, 0.058, "Bufanda", {"spine_03": 1.0}, escala=(1, 0.98, 0.75))
tubo("traje", "CaboDelante", curva((-0.1, -0.19, 0.72), (-0.125, -0.27, 0.64), (-0.11, -0.245, 0.49), 8), [0.058, 0.056, 0.054, 0.052], "Bufanda", {"spine_03": 0.6, "spine_02": 0.4}, seg=10)
toro("traje", "Fleco", (-0.11, -0.245, 0.49), 0.04, 0.014, "BufandaOscura", {"spine_02": 1.0}, escala=(1, 0.7, 1))
tubo("traje", "CaboEspalda", curva((0.1, 0.17, 0.72), (0.14, 0.25, 0.66), (0.13, 0.235, 0.54), 7), [0.056, 0.054, 0.05], "Bufanda", {"spine_03": 0.6, "spine_02": 0.4}, seg=10)
# Zurrón cruzado: la correa del hombro derecho a la cadera izquierda, y la bolsa con dos libros asomando
correa = curva((-0.15, -0.09, 0.7), (-0.02, -0.37, 0.6), (0.22, -0.13, 0.44), 12)
tubo("traje", "Correa", correa, [0.017] * 12, "Cuero", None, seg=8)
correa_atras = curva((-0.15, 0.06, 0.7), (-0.02, 0.35, 0.6), (0.22, 0.1, 0.44), 12)
tubo("traje", "CorreaAtras", correa_atras, [0.017] * 12, "Cuero", None, seg=8)
P = {"pelvis": 1.0}
caja("traje", "Zurron", (0.255, -0.015, 0.36), (0.085, 0.2, 0.17), "Cuero", P, rot=(0, math.radians(-6), 0))
caja("traje", "Tapa", (0.302, -0.015, 0.405), (0.014, 0.21, 0.1), "CueroOscuro", P, rot=(0, math.radians(-6), 0))
esfera("traje", "Cierre", (0.312, -0.015, 0.365), (0.008, 0.02, 0.02), "Laton", P)
caja("traje", "LibroRojo", (0.245, -0.055, 0.465), (0.05, 0.075, 0.1), "LibroRojo", P, rot=(math.radians(8), 0, 0))
caja("traje", "HojasRojo", (0.245, -0.055, 0.468), (0.04, 0.066, 0.1), "Papel", P, rot=(math.radians(8), 0, 0))
caja("traje", "LibroAzul", (0.25, 0.04, 0.455), (0.045, 0.08, 0.1), "LibroAzul", P, rot=(math.radians(-10), 0, 0))
caja("traje", "HojasAzul", (0.25, 0.04, 0.458), (0.036, 0.07, 0.1), "Papel", P, rot=(math.radians(-10), 0, 0))
for lado, s in (("l", 1), ("r", -1)):
    # manga de abrigo, ancha, con el hombro redondo, la codera de cuero por detrás y la vuelta del puño
    tubo("traje", f"Manga_{lado}", [(0.17 * s, -0.003, 0.636), (0.26 * s, -0.003, 0.635), (0.35 * s, -0.003, 0.634), (0.44 * s, -0.003, 0.633)],
         [0.086, 0.08, 0.076, 0.078], "Abrigo", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.175 * s, -0.003, 0.64), (0.09, 0.086, 0.086), "Abrigo", None)
    esfera("traje", f"Codera_{lado}", (0.305 * s, 0.05, 0.632), (0.055, 0.04, 0.062), "Cuero", {f"upperarm_{lado}": 0.5, f"lowerarm_{lado}": 0.5})
    toro("traje", f"Puno_{lado}", (0.44 * s, -0.003, 0.633), 0.078, 0.017, "AbrigoOscuro", {f"lowerarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.22), (0.116 * s, 0.004, 0.11)],
         [0.092, 0.086, 0.078, 0.074], "Pantalon", None, cerrar=False)
    bota = [(0.0, 0.082), (0.04, 0.087), (0.1, 0.074), (0.13, 0.076), (0.15, 0.084)]
    o = torno("traje", f"Bota_{lado}", bota, "CueroOscuro", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "CueroOscuro", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})

# ── Pesos y unión ─────────────────────────────────────────────────────────────────────────
def pesar(lista):
    for o, pesos in lista:
        for m in [m for m in o.modifiers if m.type == 'SOLIDIFY']:
            bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
        o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
        if pesos is None:
            for g in cuerpo.vertex_groups: o.vertex_groups.new(name=g.name)
            dt = o.modifiers.new("Pesos", 'DATA_TRANSFER'); dt.object = cuerpo
            dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
            dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
            bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=dt.name)
        else:
            for h, w in pesos.items():
                vg = o.vertex_groups.new(name=h); vg.add(list(range(len(o.data.vertices))), w, 'REPLACE')
        md = o.modifiers.new("Esqueleto", 'ARMATURE'); md.object = arm
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
pesar(GRUPOS["traje"])
# Lo del cuerpo de debajo que queda tapado por el pantalón, las botas y las mangas se quita (después de
# copiar los pesos: la ropa los toma del cuerpo entero)
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
fuera = [v for v in bm.verts if (mw @ v.co).z < 0.42 or (0.19 < abs((mw @ v.co).x) < 0.455 and (mw @ v.co).z > 0.55)]
bmesh.ops.delete(bm, geom=fuera, context='VERTS')
bm.to_mesh(cuerpo.data); bm.free()
cuerpo.name = "Body26_AbrigoDeMago"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body26_AbrigoDeMago", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("ELDRAN", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
