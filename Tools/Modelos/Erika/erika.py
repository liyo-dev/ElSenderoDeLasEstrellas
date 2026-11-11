# Erika, vestuario propio: guerrera ligera. Camisa verde, peto y brazales de cuero, una hombrera, cinturón
# con bolsa, capa corta a la espalda, pañuelo azul claro al cuello (el que luego lleva Will), pantalón y
# botas altas. Propuesta aprobada por Raúl el 11 nov 2026.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body28_GuerreraLigera → las manos y el torso de Body05 con la ropa encima, pesado como él.
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Camisa": (0.30, 0.52, 0.30, 1), "CamisaOscura": (0.18, 0.34, 0.2, 1), "Peto": (0.45, 0.28, 0.15, 1), "Cuero": (0.33, 0.2, 0.11, 1),
    "CueroOscuro": (0.2, 0.12, 0.07, 1), "Metal": (0.72, 0.74, 0.78, 1), "Capa": (0.2, 0.36, 0.24, 1), "CapaForro": (0.14, 0.24, 0.17, 1),
    "Panuelo": (0.55, 0.78, 0.95, 1), "Pantalon": (0.25, 0.27, 0.22, 1), "Laton": (0.8, 0.62, 0.25, 1),
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
# ── Camisa y peto de cuero ──
torno("traje", "Camisa", [(0.725, 0.14), (0.67, 0.192), (0.58, 0.2), (0.5, 0.19), (0.45, 0.19), (0.4, 0.2)], "Camisa", grosor=0.008)
toro("traje", "BajoCamisa", (0, -0.01, 0.402), 0.2, 0.01, "CamisaOscura", None, escala=(1, 0.92, 1))
torno("traje", "Peto", [(0.69, 0.2), (0.6, 0.214), (0.52, 0.206), (0.47, 0.202)], "Peto", grosor=0.012)
toro("traje", "PetoArriba", (0, -0.01, 0.69), 0.199, 0.011, "CueroOscuro", None, escala=(1, 0.92, 1))
for s in (1, -1):   # correas del peto con su hebilla
    tubo("traje", f"CorreaPeto{s}", curva((0.11 * s, -0.19, 0.69), (0.14 * s, -0.02, 0.795), (0.11 * s, 0.17, 0.69), 9), [0.016] * 9, "CueroOscuro", None, seg=8)
    esfera("traje", f"HebillaPeto{s}", (0.115 * s, -0.2, 0.685), (0.02, 0.01, 0.02), "Laton", None)
# Cinturón ancho con hebilla y bolsa
toro("traje", "Cinturon", (0, -0.01, 0.45), 0.207, 0.024, "CueroOscuro", None, escala=(1, 0.92, 0.9))
esfera("traje", "Hebilla", (0.0, -0.21, 0.45), (0.034, 0.014, 0.03), "Laton", None)
caja("traje", "Bolsa", (-0.2, -0.06, 0.4), (0.07, 0.1, 0.09), "Cuero", {"pelvis": 1.0}, rot=(0, R(8), 0))
caja("traje", "TapaBolsa", (-0.238, -0.06, 0.425), (0.012, 0.106, 0.05), "CueroOscuro", {"pelvis": 1.0}, rot=(0, R(8), 0))
# Pañuelo azul claro al cuello, con el pico por delante (el que luego lleva Will)
toro("traje", "Panuelo", (0, -0.012, 0.728), 0.165, 0.045, "Panuelo", {"spine_03": 1.0}, escala=(1, 0.98, 0.7))
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=0.11, radius2=0.0, depth=0.15)
transformar(bm, (0, -0.2, 0.655), (1, 0.3, 1), (R(180 + 12), 0, R(45)))
nueva("traje", "PicoPanuelo", bm, "Panuelo", {"spine_03": 1.0})
# Capa corta: de los hombros a la cintura, solo por la espalda
torno("traje", "Capa", [(0.745, 0.2), (0.68, 0.245), (0.58, 0.262), (0.47, 0.275), (0.4, 0.285)], "Capa", abierto=(R(8), R(172)), grosor=0.01, onda=0.03, n_onda=7,
      pesos={"spine_03": 0.6, "spine_02": 0.4})
for lado, s in (("l", 1), ("r", -1)):
    tubo("traje", f"Manga_{lado}", [(0.16 * s, -0.003, 0.636), (0.25 * s, -0.003, 0.635), (0.34 * s, -0.003, 0.634)], [0.08, 0.074, 0.07], "Camisa", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.168 * s, -0.003, 0.64), (0.083, 0.081, 0.081), "Camisa", None)
    # brazal de cuero del codo a la muñeca, con dos correas
    tubo("traje", f"Brazal_{lado}", [(0.32 * s, -0.003, 0.634), (0.38 * s, -0.003, 0.633), (0.445 * s, -0.003, 0.632)], [0.076, 0.074, 0.075], "Peto", {f"lowerarm_{lado}": 1.0}, cerrar=False)
    for x in (0.34, 0.425):
        toro("traje", f"CorreaBrazal_{lado}{int(x*1000)}", (x * s, -0.003, 0.633), 0.076, 0.009, "CueroOscuro", {f"lowerarm_{lado}": 1.0}, rot=(0, R(90), 0))
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.24)], [0.092, 0.086, 0.08], "Pantalon", None, cerrar=False)
    # bota alta con vuelta
    o = torno("traje", f"Bota_{lado}", [(0.0, 0.082), (0.04, 0.087), (0.12, 0.076), (0.2, 0.082), (0.25, 0.09)], "Cuero", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.116 * s
    toro("traje", f"VueltaBota_{lado}", (0.112 * s, 0.0, 0.25), 0.088, 0.016, "CueroOscuro", {f"calf_{lado}": 1.0})
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "Cuero", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# Una hombrera de cuero con remaches, en el hombro izquierdo
esfera("traje", "Hombrera", (0.185, -0.003, 0.672), (0.105, 0.1, 0.07), "Peto", {"clavicle_l": 0.5, "upperarm_l": 0.5})
for a in (-50, 0, 50):
    esfera("traje", f"Remache{a}", (0.185 + 0.07 * math.cos(R(a)) * 0.6, -0.003 + 0.09 * math.sin(R(a)), 0.715 - 0.012 * abs(a) / 50), (0.012, 0.012, 0.012), "Metal", {"clavicle_l": 0.5, "upperarm_l": 0.5})
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
for g in GRUPOS.values(): pesar(g)
# Lo del cuerpo de debajo que queda tapado por la ropa se quita (después de copiar los pesos: la ropa los
# toma del cuerpo entero). Del Body05 solo quedan las manos y el torso.
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
fuera = [v for v in bm.verts if (mw @ v.co).z < 0.42 or (0.19 < abs((mw @ v.co).x) < 0.455 and (mw @ v.co).z > 0.55)]
bmesh.ops.delete(bm, geom=fuera, context='VERTS')
bm.to_mesh(cuerpo.data); bm.free()
cuerpo.name = "Body28_GuerreraLigera"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body28_GuerreraLigera", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
