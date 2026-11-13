# El Archimago, vestuario propio (12 nov 2026): maestro sanador del valle, adulto maduro, sin sombrero ni bastón.
# Túnica larga abierta por delante (sobretúnica crema con ribetes de oro sobre una túnica interior más cálida), faja celeste con cabo,
# capa corta con forro celeste, mangas anchas con puño de oro y una estrella de oro que cierra el cuello. Botas de cuero.
# Paleta: crema y oro cálido (luz del amanecer), con toque celeste; opuesto claro del Mago Oscuro (violeta/negro) y distinto de Will (azul marino y dorado).
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body34_Archimago → las manos y el torso de Body05 con la ropa encima, pesado como él.
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
R = math.radians
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Crema": (0.96, 0.90, 0.72, 1), "CremaCalida": (0.88, 0.74, 0.50, 1), "Oro": (0.88, 0.67, 0.20, 1), "Celeste": (0.55, 0.74, 0.88, 1),
    "Pantalon": (0.52, 0.44, 0.34, 1), "Cuero": (0.40, 0.26, 0.15, 1), "CueroOscuro": (0.27, 0.17, 0.10, 1),
}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

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

CY, EL = -0.01, 0.92
def borde(perfil, ang, extra=0.0):   # puntos (x, y, z) del borde de un torno abierto, a un ángulo dado
    return [(math.cos(ang) * (r + extra), CY + math.sin(ang) * (r + extra) * EL, z) for z, r in perfil]
def estrella(g, nombre, centro, R_, r_, grosor, mat, pesos):   # estrella de cinco puntas mirando hacia delante (-Y)
    bm = bmesh.new(); cx, cy_, cz = centro
    fr = [bm.verts.new((cx + (R_ if k % 2 == 0 else r_) * math.sin(math.pi * k / 5), cy_ - grosor / 2, cz + (R_ if k % 2 == 0 else r_) * math.cos(math.pi * k / 5))) for k in range(10)]
    at = [bm.verts.new((v.co.x, cy_ + grosor / 2, v.co.z)) for v in fr]
    bm.faces.new(fr); bm.faces.new(at[::-1])
    for k in range(10): bm.faces.new((fr[k], fr[(k + 1) % 10], at[(k + 1) % 10], at[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = nueva(g, nombre, bm, mat, pesos)
    for p in o.data.polygons: p.use_smooth = False
    return o

# ── Túnica interior (más cálida): larga hasta media pantorrilla, se ve por la abertura de delante ──
INT = [(0.745, 0.155), (0.69, 0.2), (0.58, 0.212), (0.5, 0.2), (0.45, 0.2), (0.34, 0.222), (0.24, 0.24), (0.14, 0.255)]
torno("traje", "Tunica", INT, "CremaCalida", grosor=0.008)
# ── Sobretúnica crema, abierta por delante (60°), con ribete de oro en los bordes y en el bajo ──
EXT = [(0.74, 0.168), (0.69, 0.214), (0.58, 0.227), (0.5, 0.216), (0.45, 0.216), (0.34, 0.245), (0.22, 0.272), (0.11, 0.292)]
ABIERTO = (R(-62), R(242))
torno("traje", "Sobretunica", EXT, "Crema", abierto=ABIERTO, grosor=0.009, seg=44)
for nombre, ang in (("RibeteI", ABIERTO[0]), ("RibeteD", ABIERTO[1])):
    pts = borde(EXT, ang, 0.006); tubo("traje", nombre, pts, [0.0115] * len(pts), "Oro", None, seg=8)
arco_bajo = []
for k in range(0, 45):
    a = ABIERTO[0] + (ABIERTO[1] - ABIERTO[0]) * k / 44; r = EXT[-1][1] + 0.006
    arco_bajo.append((math.cos(a) * r, CY + math.sin(a) * r * EL, EXT[-1][0] + 0.002))
tubo("traje", "RibeteBajo", arco_bajo, [0.012] * len(arco_bajo), "Oro", None, seg=8, cerrar=True)
# ── Faja celeste con cabo; hebilla-nudo y estrella de oro al cuello ──
toro("traje", "Faja", (0, CY, 0.45), 0.207, 0.027, "Celeste", None, escala=(1, 0.92, 0.9))
esfera("traje", "Nudo", (0.09, -0.205, 0.45), (0.04, 0.026, 0.04), "Celeste", {"pelvis": 1.0})
tubo("traje", "CaboFaja", curva((0.1, -0.215, 0.43), (0.15, -0.245, 0.3), (0.13, -0.225, 0.16), 8), [0.022, 0.022, 0.019], "Celeste", {"pelvis": 1.0}, seg=10)
toro("traje", "FajaOro", (0, CY, 0.405), 0.213, 0.009, "Oro", None, escala=(1, 0.92, 1))
# ── Cuello suave con ribete de oro, y la estrella que lo cierra ──
torno("traje", "Cuello", [(0.79, 0.15), (0.755, 0.185), (0.715, 0.205)], "Crema", seg=40, grosor=0.012)
toro("traje", "CuelloOro", (0, CY, 0.797), 0.152, 0.01, "Oro", {"spine_03": 0.5, "neck_01": 0.5}, escala=(1, 0.92, 1))
estrella("traje", "EstrellaOro", (0.0, -0.21, 0.69), 0.07, 0.031, 0.022, "Oro", {"spine_03": 1.0})
# ── Capa corta a la espalda (hasta la cintura), crema por fuera y celeste por dentro, sujeta a los hombros ──
CAPA = [(0.765, 0.2), (0.72, 0.262), (0.62, 0.285), (0.5, 0.305), (0.38, 0.33)]
torno("traje", "Capa", CAPA, ("Crema", "Celeste"), abierto=(R(-12), R(192)), grosor=0.01, seg=30)
pts = [(math.cos(R(-12)) * (r + 0.004), CY + math.sin(R(-12)) * (r + 0.004) * EL, z) for z, r in CAPA]
tubo("traje", "CapaOroI", pts, [0.009] * len(pts), "Oro", None, seg=8, cerrar=True)
pts = [(math.cos(R(192)) * (r + 0.004), CY + math.sin(R(192)) * (r + 0.004) * EL, z) for z, r in CAPA]
tubo("traje", "CapaOroD", pts, [0.009] * len(pts), "Oro", None, seg=8, cerrar=True)
arco = [(math.cos(R(-12 + 204 * k / 22)) * (CAPA[-1][1] + 0.004), CY + math.sin(R(-12 + 204 * k / 22)) * (CAPA[-1][1] + 0.004) * EL, CAPA[-1][0] + 0.002) for k in range(23)]
tubo("traje", "CapaOroBajo", arco, [0.01] * len(arco), "Oro", None, seg=8, cerrar=True)
for lado, s in (("l", 1), ("r", -1)):
    # manga ancha que se abre hacia el puño, con forro celeste y puño de oro; hombro redondo
    tubo("traje", f"Manga_{lado}", [(0.17 * s, -0.003, 0.636), (0.26 * s, -0.003, 0.635), (0.35 * s, -0.003, 0.634), (0.44 * s, -0.003, 0.633)],
         [0.086, 0.082, 0.094, 0.118], "Crema", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.175 * s, -0.003, 0.64), (0.09, 0.086, 0.086), "Crema", None)
    toro("traje", f"PunoOro_{lado}", (0.445 * s, -0.003, 0.633), 0.118, 0.014, "Oro", {f"lowerarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
    toro("traje", f"PunoCeleste_{lado}", (0.425 * s, -0.003, 0.633), 0.112, 0.018, "Celeste", {f"lowerarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.22), (0.116 * s, 0.004, 0.11)],
         [0.092, 0.086, 0.078, 0.074], "Pantalon", None, cerrar=False)
    bota = [(0.0, 0.082), (0.04, 0.087), (0.1, 0.074), (0.13, 0.076), (0.15, 0.084)]
    o = torno("traje", f"Bota_{lado}", bota, "Cuero", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "Cuero", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})


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
# Lo del cuerpo de debajo que queda tapado se quita (después de copiar los pesos: la ropa los toma del cuerpo entero)
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
fuera = [v for v in bm.verts if (mw @ v.co).z < 0.42 or (0.19 < abs((mw @ v.co).x) < 0.455 and (mw @ v.co).z > 0.55)]
bmesh.ops.delete(bm, geom=fuera, context='VERTS')
bm.to_mesh(cuerpo.data); bm.free()
cuerpo.name = "Body34_Archimago"
unir(GRUPOS["traje"], "Body34_Archimago", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("ARCHIMAGO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
