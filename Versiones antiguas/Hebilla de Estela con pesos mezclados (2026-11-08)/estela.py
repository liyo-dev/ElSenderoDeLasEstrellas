# Estela, aspecto nuevo: pelo naranja con flequillo y coleta alta (con su lazo azul) y traje de hechicera
# de aventuras (túnica corta, hombreras grandes con gema, capa, cinturón de bolsitas, guantes y botas).
# Sobre el esqueleto de RPG Tiny Hero (base.blend). Mira a -Y; su izquierda es +X.
#   Hair14_Coleta   → malla pesada a «head»; la coleta, a sus huesos ColetaBone01-02 (pieza de pelo del modelo modular).
#   Body21_Hechicera → el cuerpo Body09 (manos, torso y piernas) con el traje unido encima, pesado como él.
#   Cloak04_Hechicera → la capa (forma y pesos de Cloak01, en crema), pieza de capa del modelo modular.
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body09"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name not in ("Body09", "Cloak01"): bpy.data.objects.remove(o)

COLORES = {
    "Pelo": (0.9, 0.2, 0.05, 1), "PeloClaro": (1.0, 0.34, 0.1, 1), "LazoAzul": (0.2, 0.45, 0.95, 1),
    "Tunica": (0.18, 0.32, 0.72, 1), "Capa": (0.93, 0.86, 0.7, 1), "Hombrera": (0.32, 0.34, 0.42, 1),
    "Gema": (0.9, 0.08, 0.15, 1), "Cuero": (0.48, 0.28, 0.14, 1), "Dorado": (1.0, 0.8, 0.3, 1),
    "Medias": (0.1, 0.12, 0.28, 1), "Botas": (0.09, 0.11, 0.3, 1),
}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

GRUPOS = {"pelo": [], "traje": []}
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
            bm.faces.new((filas[f][k], filas[f][(k+1) % n], filas[f+1][(k+1) % n], filas[f+1][k]))
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

# ── Pelo ──────────────────────────────────────────────────────────────────────────────────
CAB = Vector((0, -0.015, 0.99)); H = {"head": 1.0}
# Base: un casquete algo menor, que solo tapa la cabeza por debajo de los mechones
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=20, radius=1)
transformar(bm, CAB + Vector((0, 0.012, 0.025)), (0.362, 0.337, 0.325), (0, 0, 0))
quitar = [v for v in bm.verts if (v.co.y - CAB.y < -0.1 and v.co.z - CAB.z < 0.17) or v.co.z - CAB.z < -0.24
          or (v.co.y - CAB.y < 0.05 and v.co.z - CAB.z < -0.05 and abs(v.co.x) < 0.3)]
bmesh.ops.delete(bm, geom=quitar, context='VERTS')
nueva("pelo", "Casquete", bm, "Pelo", H)

# Todo el pelo sale de la coronilla: flequillo, laterales y nuca son el mismo tipo de mechón, que baja
# pegado a la cabeza (con algo de volumen arriba) y, pasada la sien, cae recto. Así el flequillo y los
# laterales nacen del mismo sitio que el resto y no parecen piezas pegadas.
CORONA = CAB + Vector((0, 0.04, 0.0))
RX, RY, RZ = 0.385, 0.36, 0.35
TH_CAIDA = 1.62   # a partir de aquí el mechón deja la cabeza y cae recto
def sobre_cabeza(theta, phi, extra=1.0):
    th = min(theta, TH_CAIDA)
    p = CORONA + Vector((RX * math.sin(th) * math.cos(phi), RY * math.sin(th) * math.sin(phi), RZ * math.cos(th))) * extra
    if theta > TH_CAIDA: p = p + Vector((0, 0, -(theta - TH_CAIDA) * RZ))
    return p
def mechon(nombre, phi, theta_fin, ancho, mat, n=11, giro=0.0, volumen=0.07, punta_fuera=0.0):
    bm = bmesh.new(); seg = 10; filas = []
    for i in range(n):
        u = i / (n - 1); th = 0.05 + (theta_fin - 0.05) * u
        ph = phi + giro * u
        p = sobre_cabeza(th, ph, 1.0 + volumen * math.sin(math.pi * min(1.0, u * 1.6)) * (1 - u) + punta_fuera * u ** 3)
        q1, q0 = sobre_cabeza(th + 0.02, ph), sobre_cabeza(max(0.0, th - 0.02), ph)
        tang = (q1 - q0).normalized()
        radial = (p - CORONA); radial.z *= 0.6; radial = radial.normalized()
        lado = tang.cross(radial).normalized()
        a = ancho * (0.7 + 0.3 * math.sin(math.pi * min(1, u * 1.3))) * (1 - u ** 2.5) + 0.004
        g = 0.024 * (1 - u) + 0.006
        filas.append([bm.verts.new(p + lado * math.cos(2*math.pi*k/seg) * a + radial * math.sin(2*math.pi*k/seg) * g) for k in range(seg)])
    for f in range(n - 1):
        for k in range(seg):
            bm.faces.new((filas[f][k], filas[f][(k+1) % seg], filas[f+1][(k+1) % seg], filas[f+1][k]))
    return nueva("pelo", nombre, bm, mat, H)

MECHONES = []
# Nuca y lados de atrás
for i in range(14):
    g = -10 + i * (200 / 13)
    MECHONES.append((g, 2.0 + 0.08 * (i % 2), 0.14, 6 * (1 if i % 2 else -1), 0.0))
# Laterales de la cara: largos, caen por delante de las orejas hasta la mandíbula
for g in (-158, -150, -30, -22):
    MECHONES.append((g, 2.45 if g in (-150, -30) else 2.25, 0.12, 0, 0.05))
# Sienes: unen los laterales con el flequillo
for g in (-140, -40):
    MECHONES.append((g, 1.8, 0.13, 0, 0.03))
# Flequillo: picos que se solapan y acaban justo encima de los ojos, abriéndose un poco
for g, fin, giro in ((-128, 1.42, -10), (-114, 1.5, -6), (-100, 1.47, -3), (-90, 1.52, 0), (-80, 1.47, 3), (-66, 1.5, 6), (-52, 1.42, 10)):
    MECHONES.append((g, fin, 0.12, giro, 0.04))
for i, (g, fin, ancho, giro, fuera) in enumerate(MECHONES):
    mechon(f"Mechon{i}", math.radians(g), fin, ancho, "Pelo" if i % 3 else "PeloClaro", giro=math.radians(giro), punta_fuera=fuera)

# Coleta alta con su lazo azul
nacimiento = CAB + Vector((0, 0.24, 0.3))
toro("pelo", "Goma", nacimiento + Vector((0, 0.035, 0.01)), 0.065, 0.028, "LazoAzul", H, rot=(math.radians(60), 0, 0))
for lado, s in (("l", 1), ("r", -1)):
    esfera("pelo", f"LazoColeta_{lado}", nacimiento + Vector((0.09 * s, 0.05, 0.05)), (0.08, 0.03, 0.05), "LazoAzul", H, rot=(0, math.radians(25 * s), 0))
cola = curva(nacimiento + Vector((0, 0.05, 0.03)), nacimiento + Vector((0, 0.4, 0.16)), nacimiento + Vector((0, 0.32, -0.85)), 12)
tubo("pelo", "Coleta", cola, [0.09, 0.14, 0.16, 0.16, 0.155, 0.145, 0.13, 0.11, 0.09, 0.065, 0.035, 0.004], "Pelo", H)
tubo("pelo", "ColetaMechon", [tuple(Vector(p) + Vector((0.03, 0.02, -0.02))) for p in cola[3:]], [0.05, 0.05, 0.045, 0.038, 0.03, 0.02, 0.004], "PeloClaro", H)
# Huesos de la coleta, bajo «head»: en Unity los mueve MuelleDeHuesos (al correr se levanta hacia atrás y
# no se mete en la capa). ColetaBone01 nace en la goma y llega a media coleta; ColetaBone02 sigue hasta la punta.
COLETA = [Vector(p) for p in cola]
HUESOS_COLETA = (("ColetaBone01", "head", COLETA[0], COLETA[5]), ("ColetaBone02", "ColetaBone01", COLETA[5], COLETA[11]))

# ── Traje de hechicera ───────────────────────────────────────────────────────────────────
# Túnica corta (cuerpo y falda hasta medio muslo), con ribete dorado abajo
torno("traje", "Tunica", [(0.715, 0.13), (0.68, 0.185), (0.6, 0.205), (0.52, 0.19), (0.46, 0.185), (0.36, 0.215), (0.27, 0.245)],
      "Tunica", grosor=0.008)
toro("traje", "Ribete", (0, -0.01, 0.272), 0.243, 0.013, "Dorado", None, escala=(1, 0.92, 1))
# Cinturón con hebilla-gema y bolsitas
toro("traje", "Cinturon", (0, -0.01, 0.46), 0.19, 0.022, "Cuero", None, escala=(1, 0.92, 1))
esfera("traje", "Hebilla", (0, -0.19, 0.46), (0.045, 0.02, 0.04), "Dorado", None)
esfera("traje", "GemaHebilla", (0, -0.205, 0.46), (0.025, 0.012, 0.025), "Gema", None)
for lado, s in (("l", 1), ("r", -1)):
    esfera("traje", f"Bolsita_{lado}", (0.17 * s, -0.08, 0.43), (0.045, 0.035, 0.05), "Cuero", {"pelvis": 1.0})
    esfera("traje", f"GemaBolsita_{lado}", (0.18 * s, -0.115, 0.44), (0.016, 0.01, 0.016), "Gema", {"pelvis": 1.0})
    # Hombreras grandes y redondas con gema
    esfera("traje", f"Hombrera_{lado}", (0.24 * s, 0.0, 0.69), (0.14, 0.13, 0.085), "Hombrera", {f"upperarm_{lado}": 0.7, "spine_03": 0.3}, rot=(0, math.radians(-20 * s), 0))
    esfera("traje", f"GemaHombrera_{lado}", (0.27 * s, -0.08, 0.72), (0.035, 0.02, 0.035), "Gema", {f"upperarm_{lado}": 0.7, "spine_03": 0.3})
    # Mangas del traje (tapan las de la ropa de debajo) y puño dorado; las manos son sus guantes oscuros de siempre
    tubo("traje", f"Manga_{lado}", [(0.17 * s, -0.003, 0.636), (0.26 * s, -0.003, 0.635), (0.36 * s, -0.003, 0.634), (0.455 * s, -0.003, 0.633)],
         [0.075, 0.07, 0.062, 0.06], "Tunica", None, cerrar=False)
    toro("traje", f"Puno_{lado}", (0.45 * s, -0.003, 0.633), 0.058, 0.018, "Dorado", {f"lowerarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
    # Botas de hechicera, a juego con los guantes: azul oscuro, finas en el tobillo, con la vuelta dorada
    # acabada en pico por delante (no son las botas marrones de los chicos)
    bota = [(0.0, 0.08), (0.035, 0.084), (0.08, 0.064), (0.15, 0.07), (0.215, 0.083)]
    o = torno("traje", f"Bota_{lado}", bota, "Botas", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.115 * s
    toro("traje", f"VueltaBota_{lado}", (0.115 * s, 0.0, 0.214), 0.084, 0.013, "Dorado", None)
    bm = bmesh.new()   # el pico de la vuelta, por delante de la caña
    pts = [(0.115 * s - 0.045, -0.09, 0.222), (0.115 * s + 0.045, -0.09, 0.222), (0.115 * s, -0.078, 0.16)]
    bm.faces.new([bm.verts.new(p) for p in (pts if s > 0 else pts[::-1])])
    nueva("traje", f"PicoBota_{lado}", bm, "Dorado", None, grosor=0.01)
    esfera("traje", f"Suela_{lado}", (0.115 * s, -0.035, 0.012), (0.08, 0.125, 0.013), "Botas", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.12 * s, -0.095, 0.032), (0.06, 0.095, 0.04), "Botas", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
esfera("traje", "Broche", (0, -0.165, 0.705), (0.04, 0.02, 0.04), "Dorado", {"spine_03": 1.0})
esfera("traje", "GemaBroche", (0, -0.18, 0.705), (0.022, 0.012, 0.022), "Gema", {"spine_03": 1.0})
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
# Primero los pesos (la ropa los copia del cuerpo entero); después se quita lo del cuerpo de debajo
# que queda dentro de las botas (si no, asoma por la caña y parecen manchas)
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='EDIT')
for nombre, padre, a0, a1 in HUESOS_COLETA:
    eb = arm.data.edit_bones.new(nombre); eb.head = arm.matrix_world.inverted() @ a0; eb.tail = arm.matrix_world.inverted() @ a1
    eb.parent = arm.data.edit_bones[padre]; eb.use_deform = True
bpy.ops.object.mode_set(mode='OBJECT')
pesar(GRUPOS["pelo"]); pesar(GRUPOS["traje"])
# La coleta (y su mechón claro) pasa de la cabeza a sus huesos a lo largo del tubo: la base con la cabeza,
# el tramo de arriba con ColetaBone01 y la punta con ColetaBone02. La goma y el lazo siguen con la cabeza.
def suave(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)
for o, _ in GRUPOS["pelo"]:
    if not o.name.startswith("Coleta"): continue
    g = {n: o.vertex_groups.get(n) or o.vertex_groups.new(name=n) for n in ("head", "ColetaBone01", "ColetaBone02")}
    for v in o.data.vertices:
        i = min(range(len(COLETA)), key=lambda k: (COLETA[k] - v.co).length)
        u = i / (len(COLETA) - 1)
        w_cab = 1 - suave(0.0, 0.25, u); w_2 = suave(0.35, 0.65, u); w_1 = max(0.0, 1 - w_cab - w_2)
        for n, w in (("head", w_cab), ("ColetaBone01", w_1), ("ColetaBone02", w_2)):
            if w > 0: g[n].add([v.index], w, 'REPLACE')
            else: g[n].remove([v.index])
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
# ... y lo de los brazos que tapan las mangas nuevas (las mangas abullonadas de la ropa de antes)
# ... y todo el torso y la falda: Body09 lleva pintado el vestido de antes y asomaba por la túnica (manchas).
# Del cuerpo de debajo solo quedan las manos (sus guantes) y el trozo de pierna entre la túnica y las botas,
# que pasa a llevar medias lisas.
bmesh.ops.delete(bm, geom=[v for v in bm.verts if (mw @ v.co).z < 0.205 or (abs((mw @ v.co).x) < 0.45 and (mw @ v.co).z > 0.31)], context='VERTS')
cuerpo.data.materials.append(MAT["Medias"]); i_medias = len(cuerpo.data.materials) - 1
for f in bm.faces:
    if all(abs((mw @ v.co).x) < 0.45 for v in f.verts): f.material_index = i_medias
bm.to_mesh(cuerpo.data); bm.free()
_pelo = unir(GRUPOS["pelo"], "Hair14_Coleta")
_bm = bmesh.new(); _bm.from_mesh(_pelo.data); bmesh.ops.recalc_face_normals(_bm, faces=_bm.faces[:]); _bm.to_mesh(_pelo.data); _bm.free()   # caras hacia fuera: en Unity el revés no se pinta
cuerpo.name = "Body21_Hechicera"   # el cuerpo de debajo va dentro: brazos, manos y piernas
unir(GRUPOS["traje"], "Body21_Hechicera", extra=cuerpo)
# La capa va aparte, como las del pack (categoría Cloak, con sus huesos CloakBone), pero con el diseño
# de antes: crema, desde los hombros hasta las rodillas, abierta por delante. Para que no se meta en los
# brazos: por detrás se mueve con la cadena de la capa (pesos copiados de Cloak01) y los lados van con
# los brazos (sube con ellos como un ala), con la mezcla según lo cerca que estén del brazo.
molde = bpy.data.objects["Cloak01"]
GRUPOS["capa"] = []
o = torno("capa", "Capa", [(0.745, 0.2), (0.69, 0.26), (0.52, 0.31), (0.32, 0.34), (0.15, 0.35)], "Capa",
          None, seg=36, cy=0.02, grosor=0.012, abierto=(math.radians(-10), math.radians(190)))
for m in [m for m in o.modifiers if m.type == 'SOLIDIFY']:
    bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
for g in molde.vertex_groups: o.vertex_groups.new(name=g.name)
dt = o.modifiers.new("Pesos", 'DATA_TRANSFER'); dt.object = molde
dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'NEAREST'
dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=dt.name)
brazo = {1: o.vertex_groups.new(name="upperarm_l"), -1: o.vertex_groups.new(name="upperarm_r")}
for v in o.data.vertices:
    x, z = v.co.x, v.co.z
    lado = 1 if x > 0 else -1
    t = min(1.0, max(0.0, (abs(x) - 0.17) / 0.15)) * min(1.0, max(0.0, (z - 0.25) / 0.35))
    w_brazo = 0.75 * t * t * (3 - 2 * t)
    if w_brazo <= 0: continue
    for g in v.groups:
        g.weight *= (1 - w_brazo)
    brazo[lado].add([v.index], w_brazo, 'REPLACE')
o.modifiers.new("Esqueleto", 'ARMATURE').object = arm
o.name = "Cloak04_Hechicera"
bpy.data.objects.remove(molde)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("ESTELA", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
