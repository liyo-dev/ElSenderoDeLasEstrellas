# Liam, aspecto nuevo: pelo castaño, juvenil, con un mechón que le cae entre los ojos (sin tapárselos), y
# túnica marrón corta y juvenil con cinturón, pantalón y botas; la capucha (como en los dibujos de Raúl) va
# aparte como accesorio, para que el jugador se la pueda quitar.
# Sobre el esqueleto de RPG Tiny Hero (base.blend). Mira a -Y; su izquierda es +X.
#   Hair15_Flequillo → malla pesada a «head» (pieza de pelo del modelo modular).
#   Body22_Tunica    → las manos y el torso de Body05 con la ropa encima, pesado como él.
#   AC12_Capucha     → la capucha, pesada a «head» (accesorio del modelo modular).
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Pelo": (0.24, 0.12, 0.06, 1), "PeloClaro": (0.36, 0.2, 0.1, 1),
    "Tunica": (0.52, 0.33, 0.17, 1), "TunicaOscura": (0.27, 0.17, 0.09, 1), "Camisa": (0.93, 0.88, 0.76, 1),
    "Pantalon": (0.27, 0.27, 0.3, 1), "Cuero": (0.2, 0.12, 0.07, 1), "Dorado": (1.0, 0.8, 0.3, 1), "Gema": (0.9, 0.08, 0.15, 1),
}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

GRUPOS = {"pelo": [], "traje": [], "capucha": []}
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

# ── Pelo (estilo del pack: un casquete con volumen y pocos mechones grandes en forma de pico, con caras
#    planas; nada de capas de mechones finos) ──────────────────────────────────────────────────────────
CAB = Vector((0, -0.015, 0.99)); H = {"head": 1.0}
CENTRO_PELO = CAB + Vector((0, 0.03, 0.03))
RAD = Vector((0.375, 0.355, 0.355))      # el casquete: algo mayor que la cabeza
def punto(theta_g, phi_g, extra=1.0):
    t, p = math.radians(theta_g), math.radians(phi_g)
    return CENTRO_PELO + Vector((RAD.x * math.sin(t) * math.cos(p), RAD.y * math.sin(t) * math.sin(p), RAD.z * math.cos(t))) * extra
def casquete(nombre, corte_cara, corte_nuca, mat):
    """corte_cara: hasta qué altura (z relativa) llega por delante; corte_nuca: por detrás."""
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1)
    transformar(bm, CENTRO_PELO, tuple(RAD), (0, 0, 0))
    quitar = []
    for v in bm.verts:
        d = v.co - CENTRO_PELO
        delante = -d.y / RAD.y                      # 1 delante, -1 detrás
        limite = corte_nuca + (corte_cara - corte_nuca) * max(0.0, delante)
        if d.z < limite: quitar.append(v)
    bmesh.ops.delete(bm, geom=quitar, context='VERTS')
    o = nueva("pelo", nombre, bm, mat, H)
    for p in o.data.polygons: p.use_smooth = False
    return o
def pico(nombre, theta_g, phi_g, direccion, largo, ancho, mat, lados=4, hundir=0.06):
    """Un mechón en pico: pirámide que nace en la superficie del casquete y apunta a «direccion»."""
    base = punto(theta_g, phi_g, 1.0 - hundir)
    d = Vector(direccion).normalized()
    a = d.orthogonal().normalized(); b = d.cross(a)
    bm = bmesh.new()
    anillo = [bm.verts.new(base + (a * math.cos(2*math.pi*k/lados + 0.4) + b * math.sin(2*math.pi*k/lados + 0.4)) * ancho) for k in range(lados)]
    punta = bm.verts.new(base + d * largo)
    for k in range(lados): bm.faces.new((anillo[k], anillo[(k+1) % lados], punta))
    bm.faces.new(anillo[::-1])
    o = nueva("pelo", nombre, bm, mat, H)
    for p in o.data.polygons: p.use_smooth = False
    return o
def hacia(theta_g, phi_g, levantar=0.0, girar=(0, 0, 0)):
    """Dirección «hacia fuera» del casquete en ese punto, levantada hacia arriba y desviada."""
    n = (punto(theta_g, phi_g) - CENTRO_PELO).normalized()
    return (n + Vector((0, 0, levantar)) + Vector(girar)).normalized()


# Liam: su Hair02 del pack (el pelo de siempre, de una pieza y pegado a la cabeza) con más volumen para que no
# se vea aplastado, el flequillo por encima de los ojos salvo el mechón del centro, que cae entre ellos, y dos
# pelillos de punta arriba. Mismo tratamiento que el pelo de Will: sombreado suave y duras solo las crestas.
import os
from mathutils.bvhtree import BVHTree
HAIR02 = os.environ.get("LIAM_HAIR02") or os.path.join(os.path.dirname(os.path.abspath(__file__)),
    "../../../Assets/Art/Characters/RPG Tiny Hero Duo/Mesh/HeadParts/Hair02.fbx")
_antes = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=HAIR02)
_nuevos = [o for o in bpy.data.objects if o not in _antes]
pelo = [o for o in _nuevos if o.type == 'MESH'][0]
for o in _nuevos:
    if o is not pelo: bpy.data.objects.remove(o)
bpy.context.view_layer.update()
pelo.parent = None; pelo.data.transform(pelo.matrix_world); pelo.matrix_world = Matrix.Identity(4)
pelo.data.transform(Matrix.Translation((0, -0.0129, 0.914)))   # las piezas sueltas de cabeza vienen sin el sitio del esqueleto
for m in list(pelo.modifiers): pelo.modifiers.remove(m)
for g in list(pelo.vertex_groups): pelo.vertex_groups.remove(g)
with bpy.data.libraries.load(BASE.replace("base.blend", "modular.blend")) as (fr, to): to.objects = ["Head01_Male", "Armature"]
for o in to.objects: bpy.context.scene.collection.objects.link(o)
bpy.context.view_layer.update()
cab = to.objects[0]; bm_c = bmesh.new(); bm_c.from_mesh(cab.data); bm_c.transform(cab.matrix_world); arbol = BVHTree.FromBMesh(bm_c)
C = Vector((0, -0.013, 0.99)); PEGADO = 0.02
VOLUMEN = float(os.environ.get("LIAM_VOLUMEN", "1.35"))   # cuánto se esponja lo que sale de la cabeza
def suave(a, b, x):
    u = min(1.0, max(0.0, (x - a) / (b - a))); return u * u * (3 - 2 * u)
def radio(t):
    h = arbol.ray_cast(C, t); return (h[0] - C).length if h[0] else 0.33
bm = bmesh.new(); bm.from_mesh(pelo.data); bm.verts.ensure_lookup_table()
# fuera las cejas que trae el pelo (islas pequeñas delante de la frente): las cejas van en las caras nuevas
vistos = set(); quitar = []
for v0 in bm.verts:
    if v0.index in vistos: continue
    isla = [v0]; vistos.add(v0.index); k = 0
    while k < len(isla):
        for e in isla[k].link_edges:
            w = e.other_vert(isla[k])
            if w.index not in vistos: vistos.add(w.index); isla.append(w)
        k += 1
    if len(isla) < 100 and max(v.co.y for v in isla) < -0.18: quitar += isla
bmesh.ops.delete(bm, geom=quitar, context='VERTS')
print("CEJAS FUERA", len(quitar))
for v in bm.verts:
    p = v.co - C; t = p.normalized(); R = radio(t)
    sobra = max(0.0, p.length - R)
    # volumen: lo que sale de la cabeza se esponja (más arriba y atrás que en la cara)
    esponja = 1.0 + (VOLUMEN - 1.0) * suave(0.1, 0.8, t.z) * (1.0 - 0.7 * suave(0.4, 0.8, -t.y))   # arriba, que es donde se aplastaba
    base = C + t * (R + max(PEGADO, sobra * esponja))
    # flequillo: por encima de los ojos; solo el mechón del centro baja entre ellos
    delante = suave(0.45, 0.7, -t.y)
    if delante > 0:
        f = base.copy()
        centro = 1.0 - suave(0.035, 0.085, abs(f.x))
        tope = 1.07 - 0.085 * centro
        if f.z < tope: f.z = tope + (f.z - tope) * (0.25 + 0.35 * centro)
        tf = (f - C).normalized(); rf = radio(tf)       # que al subirlo no se meta en la cara
        if (f - C).length < rf + PEGADO: f = C + tf * (rf + PEGADO)
        base = base.lerp(f, delante)
    v.co = base
bm.to_mesh(pelo.data); bm.free(); bm_c.free()
bpy.data.objects.remove(to.objects[0]); bpy.data.objects.remove(to.objects[1])
bpy.context.view_layer.objects.active = pelo
if pelo.data.has_custom_normals: bpy.ops.mesh.customdata_custom_splitnormals_clear()
if "sharp_edge" in pelo.data.attributes: pelo.data.attributes.remove(pelo.data.attributes["sharp_edge"])
bm2 = bmesh.new(); bm2.from_mesh(pelo.data)
for e in bm2.edges: e.smooth = not (len(e.link_faces) == 2 and math.degrees(e.calc_face_angle()) > 45)
bm2.to_mesh(pelo.data); bm2.free()
pelo.data.materials.clear(); pelo.data.materials.append(MAT["Pelo"]); pelo.data.materials.append(MAT["PeloClaro"])
for f in pelo.data.polygons:
    f.use_smooth = True; f.material_index = 1 if f.normal.z > 0.6 else 0
GRUPOS["pelo"].append((pelo, H))
# pelillos de punta, desde la coronilla esponjada (el casquete de referencia se ajusta al pelo nuevo)
_vs = [v.co for v in pelo.data.vertices]
RAD = Vector((max(abs(p.x) for p in _vs), max(abs(p.y - CENTRO_PELO.y) for p in _vs), max(p.z for p in _vs) - CENTRO_PELO.z)) * 0.97
print("PELO", tuple(round(x, 3) for x in RAD))
pico("Pelillo0", 10, -100, (-0.35, 0.1, 1), 0.12, 0.03, "Pelo", lados=3, hundir=0.0)
pico("Pelillo1", 12, -72, (0.4, 0.15, 1), 0.1, 0.028, "Pelo", lados=3, hundir=0.0)

# ── Capucha (accesorio aparte: el jugador se la puede quitar en el armario) ──
# Capucha puesta: una cáscara más grande que la cabeza y el pelo, algo alta y con un pico suave atrás;
# abierta por delante con un corte limpio (la cara queda a la vista) y por abajo (el cuello). Va con la cabeza.
CENTRO = CAB + Vector((0, 0.03, 0.04))
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=28, radius=1)
# la cáscara envuelve el pelo esponjado (RAD es su tamaño) con un margen
CAPUCHA = (max(0.45, RAD.x + 0.035), max(0.445, RAD.y + 0.035), max(0.465, RAD.z + 0.03))
transformar(bm, CENTRO, CAPUCHA, (0, 0, 0))
for v in bm.verts:   # el pico: lo de arriba y atrás se estira hacia arriba y atrás
    d = v.co - CENTRO
    k = max(0.0, (d.y / CAPUCHA[1]) * 0.6 + (d.z / CAPUCHA[2]) * 0.8) ** 3
    v.co += Vector((0, 0.09, 0.07)) * k
NORMAL_CARA = Vector((0, -1, -0.25)).normalized()      # la abertura mira al frente y un poco abajo
PUNTO_CARA = CENTRO + NORMAL_CARA * 0.29
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=PUNTO_CARA, plane_no=NORMAL_CARA, clear_outer=True)
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
bmesh.ops.bisect_plane(bm, geom=geom, plane_co=CENTRO + Vector((0, 0, -0.39)), plane_no=Vector((0, 0, -1)), clear_outer=True)
# Por debajo de la barbilla no queda nada: la capucha se abre por delante hasta el cuello
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
bmesh.ops.bisect_plane(bm, geom=geom, plane_co=CENTRO + Vector((0, -0.12, -0.22)), plane_no=Vector((0, -1, -1.2)).normalized(), clear_outer=True)
borde = [v.co.copy() for v in bm.verts if abs((v.co - PUNTO_CARA).dot(NORMAL_CARA)) < 1e-4]
nueva("capucha", "Capucha", bm, "Tunica", H, grosor=0.03)
# Borde grueso alrededor de la cara, siguiendo el corte
if borde:
    c = sum(borde, Vector()) / len(borde)
    eje_x = Vector((1, 0, 0)); eje_y = NORMAL_CARA.cross(eje_x).normalized()
    ang = lambda p: math.atan2((p - c).dot(eje_y), (p - c).dot(eje_x))
    pts = sorted(borde, key=ang)
    # El borde ya no es un aro cerrado (abajo está abierto): empieza en el hueco más grande y no se cierra
    huecos = [(ang(pts[(i + 1) % len(pts)]) - ang(pts[i])) % (2 * math.pi) for i in range(len(pts))]
    k = max(range(len(pts)), key=lambda i: huecos[i])
    pts = pts[k + 1:] + pts[:k + 1]
    pts = [p + NORMAL_CARA * 0.005 for p in pts[::max(1, len(pts) // 40)]]
    tubo("capucha", "BordeCapucha", [tuple(p) for p in pts], [0.026] * len(pts), "TunicaOscura", H, seg=10, cerrar=False)
# ── Túnica juvenil (cuerpo): corta, a medio muslo, con cuello vuelto, cinturón de cuero y bolsita;
# pantalón y botas altas a la vista
torno("traje", "Cuello", [(0.755, 0.15), (0.73, 0.2), (0.7, 0.215)], "TunicaOscura", seg=40, grosor=0.014)   # del color de los ribetes: el crema destacaba
torno("traje", "Tunica", [(0.72, 0.15), (0.66, 0.198), (0.58, 0.206), (0.5, 0.194), (0.45, 0.19), (0.36, 0.21), (0.27, 0.228)],
      "Tunica", grosor=0.008)
toro("traje", "Bajo", (0, -0.01, 0.272), 0.226, 0.012, "TunicaOscura", None, escala=(1, 0.92, 1))
cruce = curva((0.1, -0.17, 0.72), (-0.02, -0.215, 0.6), (-0.12, -0.195, 0.47), 8)
tubo("traje", "Cruce", cruce, [0.014] * 8, "TunicaOscura", None)
toro("traje", "Cinturon", (0, -0.01, 0.45), 0.197, 0.02, "Cuero", None, escala=(1, 0.92, 1))
esfera("traje", "Hebilla", (0.05, -0.198, 0.45), (0.03, 0.014, 0.026), "Dorado", None)
esfera("traje", "Bolsita", (-0.16, -0.1, 0.42), (0.045, 0.035, 0.05), "Cuero", {"pelvis": 1.0})
for lado, s in (("l", 1), ("r", -1)):
    # manga algo más ancha en el hombro que en la muñeca, con un puño oscuro que la separa de la mano, y el
    # hombro redondo tapando la unión con la túnica (si no, asoma el cuerpo de debajo)
    tubo("traje", f"Manga_{lado}", [(0.17 * s, -0.003, 0.636), (0.26 * s, -0.003, 0.635), (0.35 * s, -0.003, 0.634), (0.43 * s, -0.003, 0.633)],
         [0.08, 0.072, 0.066, 0.064], "Tunica", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.175 * s, -0.003, 0.637), (0.082, 0.08, 0.08), "Tunica", None)
    toro("traje", f"Puno_{lado}", (0.43 * s, -0.003, 0.633), 0.066, 0.015, "TunicaOscura", {f"lowerarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.22), (0.116 * s, 0.004, 0.11)],
         [0.092, 0.086, 0.078, 0.074], "Pantalon", None, cerrar=False)
    bota = [(0.0, 0.082), (0.04, 0.087), (0.1, 0.074), (0.13, 0.076), (0.15, 0.084)]
    o = torno("traje", f"Bota_{lado}", bota, "Cuero", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "Cuero", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
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
pesar(GRUPOS["pelo"]); pesar(GRUPOS["traje"]); pesar(GRUPOS["capucha"])
# Primero los pesos (la ropa los copia del cuerpo entero, brazos y piernas incluidos); después
# lo del cuerpo de debajo que queda tapado por el pantalón, las botas y las mangas se quita
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
fuera = [v for v in bm.verts if (mw @ v.co).z < 0.42 or (0.19 < abs((mw @ v.co).x) < 0.455 and (mw @ v.co).z > 0.55)]
bmesh.ops.delete(bm, geom=fuera, context='VERTS')
bm.to_mesh(cuerpo.data); bm.free()
# Nada del pelo puede quedar dentro de la cara: lo que se meta en la cabeza se saca a 1,4 cm de la piel
from mathutils.bvhtree import BVHTree
with bpy.data.libraries.load(BASE.replace("base.blend", "modular.blend")) as (fr, to): to.objects = ["Head01_Male", "Armature"]
for o in to.objects: bpy.context.scene.collection.objects.link(o)
bpy.context.view_layer.update()
_cab = to.objects[0]; _bm = bmesh.new(); _bm.from_mesh(_cab.data); _bm.transform(_cab.matrix_world); _arbol = BVHTree.FromBMesh(_bm)
_C = Vector((0, -0.013, 0.99))
for o, _ in GRUPOS["pelo"]:
    mw = o.matrix_world; inv = mw.inverted(); n = 0
    for v in o.data.vertices:
        p = mw @ v.co; t = (p - _C).normalized()
        hit = _arbol.ray_cast(_C, t)
        if hit[0] is not None and (p - _C).length < (hit[0] - _C).length + 0.014:
            v.co = inv @ (hit[0] + t * 0.014); n += 1
print("PELO SACADO DE LA CARA", n)
_bm.free(); bpy.data.objects.remove(to.objects[0]); bpy.data.objects.remove(to.objects[1])
unir(GRUPOS["pelo"], "Hair15_Flequillo")
cuerpo.name = "Body22_Tunica"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body22_Tunica", extra=cuerpo)
unir(GRUPOS["capucha"], "AC12_Capucha")
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("LIAM", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
