# Victoria, vestuario propio: costurera. Blusa lila con las mangas remangadas, falda lila oscuro con vuelo,
# delantal crema con peto y lazo a la espalda, cinta de medir al cuello, acerico con alfileres en el
# bolsillo, medias y zapatos. El moño va aparte, como accesorio de cabeza. Propuesta aprobada por Raúl
# el 11 nov 2026. Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y.
#   Body27_Costurera → las manos y el torso de Body05 con la ropa encima, pesado como él.
#   AC13_Mono        → el moño con su cinta, pesado a «head» (del color del pelo en Unity).
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Blusa": (0.70, 0.58, 0.86, 1), "BlusaClara": (0.86, 0.78, 0.95, 1), "Falda": (0.46, 0.34, 0.62, 1), "FaldaOscura": (0.33, 0.24, 0.46, 1),
    "Delantal": (0.96, 0.91, 0.79, 1), "DelantalOscuro": (0.82, 0.74, 0.58, 1), "Cinta": (0.98, 0.82, 0.25, 1),
    "Acerico": (0.80, 0.18, 0.2, 1), "Alfiler": (0.92, 0.92, 0.95, 1), "Media": (0.95, 0.93, 0.9, 1), "Zapato": (0.36, 0.2, 0.12, 1),
    "Piel": (0.96, 0.75, 0.59, 1), "Pelo": (0.95, 0.36, 0.9, 1),
}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

GRUPOS = {"traje": [], "mono": []}
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
ZAPATO = "Zapato"; H = {"head": 1.0}
# ── Blusa y falda ──
torno("traje", "Blusa", [(0.725, 0.14), (0.67, 0.192), (0.58, 0.2), (0.5, 0.19), (0.45, 0.188)], "Blusa", grosor=0.008)
torno("traje", "Cuello", [(0.755, 0.15), (0.735, 0.19), (0.712, 0.2)], "BlusaClara", seg=40, grosor=0.012)
torno("traje", "Falda", [(0.47, 0.186), (0.42, 0.21), (0.32, 0.243), (0.21, 0.27)], "Falda", grosor=0.008, onda=0.035, n_onda=10)
toro("traje", "Bajo", (0, -0.01, 0.212), 0.268, 0.01, "FaldaOscura", None, escala=(1, 0.92, 1))
# ── Delantal: el peto delante del pecho, la falda del delantal por delante, la cinta a la cintura y el lazo detrás ──
torno("traje", "Peto", [(0.67, 0.203), (0.6, 0.212), (0.52, 0.205), (0.46, 0.2)], "Delantal", abierto=(R(-125), R(-55)), grosor=0.007)
torno("traje", "DelantalFalda", [(0.47, 0.2), (0.42, 0.222), (0.33, 0.252), (0.25, 0.272)], "Delantal", abierto=(R(-160), R(-20)), grosor=0.007)
toro("traje", "CintaDelantal", (0, -0.01, 0.462), 0.203, 0.014, "DelantalOscuro", None, escala=(1, 0.92, 1))
for s in (1, -1):
    # tirantes del peto, por encima del hombro hasta la cintura por detrás
    tubo("traje", f"Tirante{s}", curva((0.1 * s, -0.19, 0.67), (0.13 * s, -0.02, 0.8), (0.09 * s, 0.17, 0.47), 10), [0.013] * 10, "Delantal", None, seg=8)
    esfera("traje", f"Lazo{s}", (0.07 * s, 0.215, 0.47), (0.065, 0.02, 0.04), "Delantal", {"spine_01": 1.0}, rot=(0, R(18 * s), 0))
    tubo("traje", f"CaboLazo{s}", curva((0.02 * s, 0.215, 0.46), (0.05 * s, 0.25, 0.4), (0.06 * s, 0.262, 0.33), 6), [0.014] * 6, "Delantal", {"pelvis": 1.0}, seg=8)
esfera("traje", "NudoLazo", (0, 0.218, 0.468), (0.026, 0.022, 0.026), "DelantalOscuro", {"spine_01": 1.0})
# Bolsillo con el acerico y sus alfileres
caja("traje", "Bolsillo", (0.09, -0.262, 0.345), (0.1, 0.012, 0.075), "DelantalOscuro", {"pelvis": 1.0}, rot=(R(-14), 0, 0))
esfera("traje", "Acerico", (0.09, -0.262, 0.39), (0.036, 0.026, 0.028), "Acerico", {"pelvis": 1.0})
for k, (dx, dz) in enumerate(((-0.018, 0.018), (0.0, 0.026), (0.02, 0.018))):
    esfera("traje", f"Alfiler{k}", (0.09 + dx, -0.272, 0.39 + dz), (0.007, 0.007, 0.007), "Alfiler", {"pelvis": 1.0})
# Cinta de medir: sobre los hombros, por detrás del cuello, con los dos cabos colgando por delante
tubo("traje", "CintaDeMedir", [(-0.085, -0.215, 0.5), (-0.1, -0.215, 0.62), (-0.125, -0.15, 0.725), (-0.11, 0.0, 0.762), (0.0, 0.1, 0.755),
     (0.11, 0.0, 0.762), (0.125, -0.15, 0.725), (0.1, -0.215, 0.6), (0.085, -0.218, 0.44)], [0.013] * 9, "Cinta", {"spine_03": 0.7, "spine_02": 0.3}, seg=8)
for lado, s in (("l", 1), ("r", -1)):
    # manga remangada hasta el codo, con la vuelta más clara; el brazo, de piel, entra en la mano
    tubo("traje", f"Manga_{lado}", [(0.15 * s, -0.003, 0.636), (0.22 * s, -0.003, 0.635), (0.3 * s, -0.003, 0.634)], [0.082, 0.08, 0.079], "Blusa", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.165 * s, -0.003, 0.64), (0.084, 0.082, 0.082), "Blusa", None)
    toro("traje", f"Vuelta_{lado}", (0.3 * s, -0.003, 0.634), 0.079, 0.02, "BlusaClara", None, rot=(0, R(90), 0))
    tubo("traje", f"Brazo_{lado}", [(0.26 * s, -0.003, 0.634), (0.33 * s, -0.002, 0.634), (0.4 * s, -0.002, 0.632), (0.475 * s, -0.003, 0.629)],
         [0.068, 0.067, 0.068, 0.07], "Piel", None, cerrar=False)
    tubo("traje", f"Media_{lado}", [(0.1 * s, 0.002, 0.3), (0.108 * s, 0.0, 0.2), (0.116 * s, 0.003, 0.09)], [0.078, 0.074, 0.07], "Media", None, cerrar=False)
    o = torno("traje", f"Zapato_{lado}", [(0.0, 0.08), (0.04, 0.085), (0.085, 0.074), (0.1, 0.078)], ZAPATO, None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.086, 0.128, 0.014), ZAPATO, {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.068, 0.088, 0.042), ZAPATO, {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# ── Moño (accesorio de cabeza) ──
# Va por fuera del pelo (Hair06 llega a 0,38 por detrás): arriba y atrás, con su cinta en la base
esfera("mono", "Mono", (0, 0.37, 1.2), (0.125, 0.12, 0.118), "Pelo", H)
toro("mono", "CintaMono", (0, 0.3, 1.17), 0.085, 0.018, "Delantal", H, rot=(R(-70), 0, 0))
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
cuerpo.name = "Body27_Costurera"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body27_Costurera", extra=cuerpo)
unir(GRUPOS["mono"], "AC13_Mono")
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
