# Will, aspecto nuevo: pelo rubio miel de héroe (su Hair01 de siempre con los picos hacia arriba y atrás y el
# flequillo barrido de lado, de una pieza) y ropa de aventurero clásico sin bufanda: chaqueta corta roja abierta con ribete dorado y
# mangas remangadas, camisa crema, cinturón con hebilla de estrella y bolsitas, guantes sin dedos, pantalón
# y botas altas con vuelta.
# Sobre el esqueleto de RPG Tiny Hero (base.blend). Mira a -Y; su izquierda es +X.
#   Hair16_Heroe       → malla pesada a «head» (pieza de pelo del modelo modular).
#   Body23_Aventurero  → las manos y el torso de Body05 con la ropa nueva encima, pesado como él.
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
import os
MANGA_LARGA = os.environ.get("WILL_MANGA", "corta") == "larga"   # Raúl se quedó con la manga corta remangada
PANTALON_CLARO = os.environ.get("WILL_PANTALON", "") == "claro"
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Pelo": (0.72, 0.44, 0.16, 1), "PeloClaro": (0.86, 0.6, 0.28, 1),
    "Chaqueta": (0.1, 0.16, 0.38, 1), "Camisa": (0.95, 0.9, 0.78, 1), "Pantalon": (0.62, 0.52, 0.4, 1) if PANTALON_CLARO else (0.3, 0.24, 0.18, 1),
    "Cuero": (0.42, 0.25, 0.12, 1), "Dorado": (1.0, 0.8, 0.3, 1), "Guante": (0.25, 0.17, 0.12, 1),
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


# ── Pelo (D): el Hair01 del pack —su pelo de siempre, de una pieza y pegado a la cabeza— deformado: los picos
#    se levantan hacia arriba y atrás y el flequillo se barre de lado. Todo sigue unido (nada de piezas sueltas).
from mathutils.bvhtree import BVHTree
SRC_MOD = BASE.replace("base.blend", "modular.blend")
with bpy.data.libraries.load(SRC_MOD) as (fr, to): to.objects = ["Hair01", "Head01_Male", "Armature"]
for o in to.objects: bpy.context.scene.collection.objects.link(o)
bpy.context.view_layer.update()            # que calcule su sitio con el esqueleto del modelo modular
for o in to.objects[:2]:
    mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    for m in list(o.modifiers): o.modifiers.remove(m)
bpy.data.objects.remove(to.objects[2])
pelo, cab = bpy.data.objects["Hair01"], bpy.data.objects["Head01_Male"]
# todo a coordenadas de mundo
for o in (pelo, cab):
    o.data.transform(o.matrix_world); o.matrix_world = o.matrix_world.Identity(4)
bm_c = bmesh.new(); bm_c.from_mesh(cab.data); arbol = BVHTree.FromBMesh(bm_c)
C = Vector((0, -0.013, 0.99)); ARRIBA = Vector((0, 0, 1))
PEGADO = 0.022                                   # lo que el casquete se separa de la piel
bm = bmesh.new(); bm.from_mesh(pelo.data)
# fuera las cejas que trae el Hair01 (islas pequeñas delante de la frente): las cejas ya van en las caras nuevas
bm.verts.ensure_lookup_table(); vistos = set(); quitar = []
for v0 in bm.verts:
    if v0.index in vistos: continue
    isla = [v0]; vistos.add(v0.index); k = 0
    while k < len(isla):
        for e in isla[k].link_edges:
            w = e.other_vert(isla[k])
            if w.index not in vistos: vistos.add(w.index); isla.append(w)
        k += 1
    if len(isla) < 60 and max(v.co.y for v in isla) < -0.2: quitar += isla
bmesh.ops.delete(bm, geom=quitar, context='VERTS')
print("CEJAS FUERA", len(quitar))
borde = {v for v in bm.verts if any(len(e.link_faces) < 2 for e in v.link_edges)}
def suave(a, b, x):
    u = min(1.0, max(0.0, (x - a) / (b - a))); return u * u * (3 - 2 * u)
def radio(t):
    h = arbol.ray_cast(C, t); return (h[0] - C).length if h[0] else 0.33
SALE = {}                                        # por vértice: cuánto sale del casquete (0 en lo pegado) y su dirección
for v in bm.verts:
    p = v.co - C; t = p.normalized()
    # Raúl: «baja un pelín el pelo» — que no asome cabeza por los lados, la nuca ni la frente: lo de abajo del
    # pelo se desliza hacia abajo sobre la cabeza (más el borde).
    peso = suave(0.85, 0.25, t.z) * (1.0 - 0.6 * suave(0.5, 0.8, -t.y))   # por delante menos: ahí manda el flequillo
    bajar = (0.24 if v in borde else 0.13) * peso                          # patillas y nuca hasta la altura de la oreja
    if bajar > 0: p = p.length * (t + Vector((0, 0, -bajar))).normalized()
    d = p.length; t = p.normalized(); R = radio(t)
    sobra = d - R - PEGADO
    # a) atrás, arriba y lados: los picos se levantan hacia arriba y atrás
    if sobra < 0.05:
        normal = C + t * (R + PEGADO + max(0.0, sobra) * 0.6)
    else:
        s2 = sobra - 0.05
        if t.z > -0.15:
            dirc = (t + ARRIBA * (1.3 if t.z > 0.4 else 0.8) + Vector((0, 0.25, 0))).normalized(); largo = 1.15
        else:
            dirc = (t + Vector((0, 0.3, -0.2))).normalized(); largo = 0.55
        normal = C + t * (R + PEGADO + 0.05) + dirc * s2 * largo
        SALE[v.index] = (s2, t.copy())
    # b) delante: el flequillo de siempre (cae por la frente) aplastado sobre ella y corrido un poco hacia su
    #    izquierda. Se aplasta a lo largo del radio, así que ninguna cara se da la vuelta (no hay agujeros).
    w = suave(0.35, 0.6, -t.y) * suave(0.85, 0.6, t.z)
    if w > 0:
        lado = suave(0.75, 0.2, t.z)                       # más corrido cuanto más abajo
        tf = (p + Vector((0.07 * lado, 0, 0.01 * lado))).normalized()
        frente = C + tf * (radio(tf) + PEGADO * 0.8 + min(max(sobra, 0.0), 0.2) * 0.3)
        if frente.z < 1.07: frente.z = 1.07 + (frente.z - 1.07) * 0.25   # se queda encima de las cejas
        v.co = normal.lerp(frente, w)
    else:
        v.co = normal
# Raúl (4 nov): «lo hacemos homogéneo»: los picos se quedan como estaban, pero se les reparte un poco la forma
# (sus vértices intermedios se alisan hacia sus vecinos; raíces y puntas no se mueven) para que no queden caras
# torcidas sueltas. PELO_ALISAR=0 lo apaga.
ALISAR = float(os.environ.get("PELO_ALISAR", "0.35"))
# Los picos muy cortos, con el sombreado suave, se veían como bultos redondos (sobre todo en la coronilla):
# se aplanan contra el casquete; los picos de verdad no se tocan.
CORTO = 0.075
cortos = 0
for v in bm.verts:
    if v.index in SALE and SALE[v.index][0] < CORTO:
        s2, t = SALE[v.index]
        if all(SALE.get(e.other_vert(v).index, (0,))[0] <= s2 for e in v.link_edges):   # la punta de un pico corto
            v.co = C + (v.co - C).normalized() * (radio((v.co - C).normalized()) + PEGADO + 0.03); cortos += 1
print("PICOS CORTOS APLANADOS", cortos)
if ALISAR > 0:
    puntas = {v for v in bm.verts if v.index in SALE and all(SALE.get(e.other_vert(v).index, (0,))[0] < SALE[v.index][0] for e in v.link_edges)}
    medio = [v for v in bm.verts if v.index in SALE and v not in puntas and v not in borde]
    bmesh.ops.smooth_vert(bm, verts=medio, factor=ALISAR, use_axis_x=True, use_axis_y=True, use_axis_z=True)
# El casquete (lo pegado a la cabeza) liso: se suaviza su forma y se sombrea suave; los picos siguen con
# caras planas. Raúl: «por detrás se ve como con cuadraditos en lugar de liso».
def pegado(v):
    t = (v.co - C).normalized(); return (v.co - C).length < radio(t) + PEGADO + 0.03
casco = [v for v in bm.verts if pegado(v) and (v.co - C).normalized().y > -0.35]   # sin tocar el flequillo
for _ in range(3):
    bmesh.ops.smooth_vert(bm, verts=casco, factor=0.6, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    for v in casco:
        t = (v.co - C).normalized(); v.co = C + t * (radio(t) + PEGADO)
# Raúl (4 nov): «que no se vean como pirámides, algo más homogéneo»: todo el pelo con sombreado suave (sin las
# aristas duras ni las normales partidas que trae el Hair01 del pack); los picos conservan su forma.
bm.to_mesh(pelo.data); bm.free()
# Las normales que trae el Hair01 del FBX (partidas por caras) se quitan para que el sombreado suave valga
bpy.context.view_layer.objects.active = pelo
if pelo.data.has_custom_normals: bpy.ops.mesh.customdata_custom_splitnormals_clear()
if "sharp_edge" in pelo.data.attributes: pelo.data.attributes.remove(pelo.data.attributes["sharp_edge"])   # aristas «duras» del pack
# Solo las aristas muy marcadas (las crestas de los picos) siguen duras: así los picos no se ven como bultos y
# desaparecen las caras planas sueltas. PELO_ANGULO = grados a partir de los que una arista es dura.
ANGULO = float(os.environ.get("PELO_ANGULO", "45"))
if ANGULO < 180:
    bm2 = bmesh.new(); bm2.from_mesh(pelo.data)
    duras = [e for e in bm2.edges if len(e.link_faces) == 2 and math.degrees(e.calc_face_angle()) > ANGULO]
    for e in bm2.edges: e.smooth = e not in duras
    bm2.to_mesh(pelo.data); bm2.free()
def pegado_co(co):
    t = (co - C).normalized(); return (co - C).length < radio(t) + PEGADO + 0.035
CASCO = {v.index for v in pelo.data.vertices if pegado_co(v.co)}
bm_c.free(); bpy.data.objects.remove(cab)
pelo.data.materials.clear(); pelo.data.materials.append(MAT["Pelo"]); pelo.data.materials.append(MAT["PeloClaro"])
for f in pelo.data.polygons:
    en_casco = all(i in CASCO for i in f.vertices)
    f.use_smooth = True; f.material_index = 1 if f.normal.z > 0.55 and not en_casco else 0
for g in list(pelo.vertex_groups): pelo.vertex_groups.remove(g)
GRUPOS["pelo"].append((pelo, H))

# ── Ropa de aventurero ───────────────────────────────────────────────────────────────────
for lado, s in (("l", 1), ("r", -1)):
    # Mangas de la camisa (hasta la muñeca) y las de la chaqueta, remangadas hasta el codo
    if MANGA_LARGA:   # chaqueta con manga entera hasta la muñeca (sin nada en la muñeca)
        tubo("traje", f"MangaChaqueta_{lado}", [(0.17 * s, -0.003, 0.637), (0.26 * s, -0.003, 0.636), (0.35 * s, -0.003, 0.634), (0.455 * s, -0.003, 0.633)],
             [0.084, 0.078, 0.072, 0.068], "Chaqueta", None, cerrar=False)
    else:
        tubo("traje", f"Camisa_{lado}", [(0.3 * s, -0.003, 0.634), (0.42 * s, -0.003, 0.633), (0.455 * s, -0.003, 0.633)],
             [0.062, 0.058, 0.058], "Camisa", None, cerrar=False)   # empieza dentro del remangado: bajo la chaqueta no hay crema
        tubo("traje", f"MangaChaqueta_{lado}", [(0.17 * s, -0.003, 0.637), (0.26 * s, -0.003, 0.636), (0.33 * s, -0.003, 0.635)],
             [0.082, 0.078, 0.075], "Chaqueta", None, cerrar=False)
        toro("traje", f"Remangado_{lado}", (0.335 * s, -0.003, 0.635), 0.075, 0.018, "Chaqueta", {f"upperarm_{lado}": 0.5, f"lowerarm_{lado}": 0.5}, rot=(0, math.radians(90), 0))
    # Pantalón y botas altas con vuelta
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.24), (0.115 * s, 0.003, 0.2)],
         [0.092, 0.086, 0.078, 0.075], "Pantalon", None, cerrar=False)
    bota = [(0.0, 0.082), (0.04, 0.087), (0.1, 0.074), (0.18, 0.072), (0.21, 0.08), (0.235, 0.088)]
    o = torno("traje", f"Bota_{lado}", bota, "Cuero", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "Cuero", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "Cuero", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# Camisa crema por dentro (asoma por delante)
torno("traje", "Camisa", [(0.75, 0.0), (0.745, 0.075), (0.73, 0.1), (0.715, 0.12), (0.68, 0.17), (0.6, 0.186), (0.52, 0.178), (0.46, 0.176), (0.4, 0.165), (0.37, 0.0)], "Camisa")
for lado, s in (("l", 1), ("r", -1)):   # hombros: cierran el hueco entre la camisa y las mangas
    esfera("traje", f"Hombro_{lado}", (0.17 * s, -0.003, 0.637), (0.07, 0.07, 0.07), "Chaqueta", None)   # azul y por dentro de la manga: si asoma, no mancha
# Chaqueta corta roja, abierta por delante (deja ver la camisa), hasta la cadera, con ribete dorado
torno("traje", "Chaqueta", [(0.73, 0.15), (0.69, 0.2), (0.6, 0.214), (0.52, 0.205), (0.44, 0.205), (0.36, 0.222)], "Chaqueta",
      seg=40, grosor=0.01, abierto=(math.radians(-62), math.radians(242)))
for lado, s in (("l", 1), ("r", -1)):
    tubo("traje", f"Solapa_{lado}", curva((0.06 * s, -0.19, 0.72), (0.1 * s, -0.215, 0.56), (0.1 * s, -0.215, 0.36), 8), [0.016] * 8, "Dorado", None)
toro("traje", "Bajo", (0, -0.01, 0.362), 0.222, 0.011, "Dorado", None, escala=(1, 0.92, 1))
# Cinturón con hebilla de estrella y bolsitas
toro("traje", "Cinturon", (0, -0.01, 0.45), 0.198, 0.022, "Cuero", None, escala=(1, 0.92, 1))
estrella = [(0.035 * (1 if k % 2 == 0 else 0.45) * math.sin(k * math.pi / 5), 0.45 + 0.035 * (1 if k % 2 == 0 else 0.45) * math.cos(k * math.pi / 5)) for k in range(10)]
bm = bmesh.new(); vs = [bm.verts.new((x, -0.205, z)) for x, z in estrella]; bm.faces.new(vs)
bmesh.ops.extrude_face_region(bm, geom=bm.faces[:]); bmesh.ops.translate(bm, vec=(0, 0.012, 0), verts=[v for v in bm.verts if v.co.y < -0.2])
nueva("traje", "Hebilla", bm, "Dorado", {"pelvis": 1.0})
for lado, s in (("l", 1), ("r", -1)):
    esfera("traje", f"Bolsita_{lado}", (0.17 * s, -0.09, 0.42), (0.045, 0.035, 0.05), "Cuero", {"pelvis": 1.0})

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
pesar(GRUPOS["pelo"]); pesar(GRUPOS["traje"])
# Primero los pesos (la ropa los copia del cuerpo entero, brazos y piernas incluidos); después
# lo del cuerpo de debajo que queda tapado por el pantalón, las botas y las mangas se quita
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
# del cuerpo de Body05 solo se quedan las manos: su torso lleva pintada la ropa vieja y asomaba por la camisa
fuera = [v for v in bm.verts if abs((mw @ v.co).x) < 0.445]
bmesh.ops.delete(bm, geom=fuera, context='VERTS')
bm.to_mesh(cuerpo.data); bm.free()
unir(GRUPOS["pelo"], "Hair16_Heroe")
cuerpo.name = "Body23_Aventurero"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body23_Aventurero", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("WILL", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
