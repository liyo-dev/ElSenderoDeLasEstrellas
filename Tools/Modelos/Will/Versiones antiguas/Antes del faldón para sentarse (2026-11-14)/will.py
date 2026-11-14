# Will, aspecto nuevo: pelo rubio miel de héroe (su Hair01 de siempre con los picos hacia arriba y atrás y el
# flequillo barrido de lado, de una pieza) y ropa de aventurero: camiseta de manga corta azul marino con ribete dorado,
# pañuelo azul claro al cuello, cinturón con hebilla de estrella, pantalón y botas altas con vuelta. La chaqueta corta
# (azul marino, abierta, ribete dorado, mangas remangadas) es una prenda aparte del armario.
# Sobre el esqueleto de RPG Tiny Hero (base.blend). Mira a -Y; su izquierda es +X.
#   Hair16_Heroe       → malla pesada a «head» (pieza de pelo del modelo modular).
#   Body23_Aventurero  → las manos de Body05 con la ropa nueva (camiseta), pesado como él.
#   Body24_AventureroChaqueta → lo mismo con la chaqueta encima (otro cuerpo del armario: así se puede llevar capa).
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Euler
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Prendas"))
import ropa_lib as rl
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
import os
PANTALON_CLARO = os.environ.get("WILL_PANTALON", "") == "claro"
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Pelo": (0.72, 0.44, 0.16, 1), "PeloClaro": (0.86, 0.6, 0.28, 1),
    "Chaqueta": (0.1, 0.16, 0.38, 1), "Camiseta": (0.1, 0.16, 0.38, 1), "Piel": (0.894, 0.659, 0.388, 1), "Pantalon": (0.62, 0.52, 0.4, 1) if PANTALON_CLARO else (0.3, 0.24, 0.18, 1),
    "Cuero": (0.42, 0.25, 0.12, 1), "Dorado": (1.0, 0.8, 0.3, 1), "Guante": (0.25, 0.17, 0.12, 1), "Panuelo": (0.3, 0.62, 0.92, 1),
}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

GRUPOS = {"pelo": [], "traje": [], "chaqueta": []}
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
# La nuca no baja de la base del cráneo: si colgara más, se mete en el cuello de la ropa y el pañuelo y,
# al girar la cabeza, solo asoma la punta y parece un trozo de pelo suelto (ver INC-943). Lo que queda por
# debajo de PELO_NUCA_Z sube con un tope suave (exponencial, sin escalón) y se vuelve a pegar por fuera de la cabeza.
NUCA_Z = float(os.environ.get("PELO_NUCA_Z", "0.78")); NUCA_B = 0.04
for v in bm.verts:
    d = v.co.z - NUCA_Z
    if d < NUCA_B and (v.co - C).normalized().y > -0.2:
        v.co.z = NUCA_Z + NUCA_B * math.exp(d / NUCA_B - 1.0)
        t = (v.co - C).normalized(); R = radio(t) + PEGADO
        if (v.co - C).length < R: v.co = C + t * R
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

# ── Ropa de aventurero (8 nov 2026, Raúl: «demasiado gordo», «la camiseta se mete en la piel» en las manos;
#    «prefiero una camiseta chula corta con su pañuelito» y la chaqueta como prenda del armario) ─────────
# Proporciones del cuerpo de siempre (Body05): torso de ~0,17 de radio, brazo de ~0,07 y muñeca de ~0,068.
CAMISETA = [(0.75, 0.0), (0.745, 0.07), (0.735, 0.11), (0.72, 0.14), (0.69, 0.163), (0.63, 0.17), (0.56, 0.165),
            (0.49, 0.16), (0.43, 0.172), (0.375, 0.188)]
ELIP = 0.9
def radio_camiseta(z):
    p = sorted(CAMISETA)
    if z <= p[0][0]: return p[0][1]
    for (z0, r0), (z1, r1) in zip(p, p[1:]):
        if z <= z1: return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return p[-1][1]
torno("traje", "Camiseta", CAMISETA, "Camiseta", elip=ELIP)
toro("traje", "BajoCamiseta", (0, -0.01, 0.378), 0.188, 0.011, "Dorado", None, escala=(1, ELIP, 1))   # ribete del bajo
Z_BRAZO = 0.633
for lado, s in (("l", 1), ("r", -1)):
    # Manga corta: del hombro a medio brazo, con ribete dorado
    tubo("traje", f"Manga_{lado}", [(0.15 * s, -0.003, Z_BRAZO + 0.004), (0.22 * s, -0.003, Z_BRAZO + 0.002), (0.275 * s, -0.003, Z_BRAZO)],
         [0.08, 0.079, 0.077], "Camiseta", None, cerrar=False)
    toro("traje", f"RibeteManga_{lado}", (0.274 * s, -0.003, Z_BRAZO), 0.077, 0.011, "Dorado", None, rot=(0, math.radians(90), 0))
    esfera("traje", f"Hombro_{lado}", (0.16 * s, -0.003, Z_BRAZO + 0.006), (0.082, 0.08, 0.08), "Camiseta", None)
    # Brazo de piel: nace dentro de la manga y entra en la mano con el radio del borde de la mano (~0,07), para que
    # la muñeca no haga escalón; el tubo es de piel y la manga queda por fuera, así la ropa no se mete en la piel.
    tubo("traje", f"Brazo_{lado}", [(0.24 * s, -0.003, Z_BRAZO), (0.3 * s, -0.002, Z_BRAZO), (0.36 * s, 0.0, Z_BRAZO), (0.42 * s, -0.002, Z_BRAZO - 0.003), (0.475 * s, -0.003, Z_BRAZO - 0.005)],
         [0.068, 0.067, 0.067, 0.069, 0.07], "Piel", None, cerrar=False)
    # Pantalón y botas altas con vuelta
    tubo("traje", f"Pernera_{lado}", [(0.096 * s, 0.003, 0.41), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.24), (0.115 * s, 0.003, 0.2)],
         [0.082, 0.082, 0.076, 0.074], "Pantalon", None, cerrar=False)
    bota = [(0.0, 0.082), (0.04, 0.087), (0.1, 0.074), (0.18, 0.072), (0.21, 0.08), (0.235, 0.088)]
    o = torno("traje", f"Bota_{lado}", bota, "Cuero", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "Cuero", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "Cuero", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# Cinturón con hebilla de estrella
CINTURA = radio_camiseta(0.45) + 0.012
toro("traje", "Cinturon", (0, -0.01, 0.45), CINTURA, 0.018, "Cuero", None, escala=(1, ELIP, 1))
Y_HEBILLA = -0.01 - CINTURA * ELIP - 0.018
estrella = [(0.033 * (1 if k % 2 == 0 else 0.45) * math.sin(k * math.pi / 5), 0.45 + 0.033 * (1 if k % 2 == 0 else 0.45) * math.cos(k * math.pi / 5)) for k in range(10)]
bm = bmesh.new(); vs = [bm.verts.new((x, Y_HEBILLA, z)) for x, z in estrella]; bm.faces.new(vs)
r = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
bmesh.ops.translate(bm, vec=(0, 0.012, 0), verts=[g for g in r["geom"] if isinstance(g, bmesh.types.BMVert)])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
nueva("traje", "Hebilla", bm, "Dorado", {"pelvis": 1.0})
# Pañuelo al cuello: banda alrededor del cuello y el pico por delante, sobre la camiseta
toro("traje", "PanueloCuello", (0, -0.01, 0.738), 0.14, 0.022, "Panuelo", {"spine_03": 0.5, "neck_01": 0.5}, escala=(1, ELIP, 1))
bm = bmesh.new(); filas = []
for i in range(7):
    u = i / 6; z = 0.735 - 0.17 * u; ancho = 0.12 * (1 - u) ** 0.8
    fila = []
    for k in range(9):
        x = ancho * (k / 4 - 1)
        r0 = max(radio_camiseta(z), 0.14)
        y = -0.01 - math.sqrt(max(r0 * r0 - x * x, 0.0)) * ELIP - 0.012
        fila.append(bm.verts.new((x, y, z)))
    filas.append(fila)
for i in range(6):
    for k in range(8):
        bm.faces.new((filas[i][k], filas[i+1][k], filas[i+1][k+1], filas[i][k+1]))
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
nueva("traje", "PanueloPico", bm, "Panuelo", None, grosor=0.008)

# ── Chaqueta (va en Body24_AventureroChaqueta: la camiseta con la chaqueta encima) ──────────────────────────────────────────────
# Corta, azul marino, abierta por delante (deja ver la camiseta, el pañuelo y la hebilla), con ribete dorado y
# las mangas remangadas hasta el codo. Va por encima de la camiseta y del cinturón, sin engordar la silueta.
PERFIL_CHAQUETA = [(0.725, 0.16), (0.69, 0.185), (0.62, 0.19), (0.54, 0.185), (0.47, 0.192), (0.42, 0.2), (0.37, 0.212)]
ABIERTO = (math.radians(-55), math.radians(235))
torno("chaqueta", "Chaqueta", PERFIL_CHAQUETA, "Chaqueta", seg=40, elip=ELIP, grosor=0.008, abierto=ABIERTO)
def radio_chaqueta(z):
    p = sorted(PERFIL_CHAQUETA)
    if z <= p[0][0]: return p[0][1]
    for (z0, r0), (z1, r1) in zip(p, p[1:]):
        if z <= z1: return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return p[-1][1]
def sobre_chaqueta(a, z, fuera):
    r = radio_chaqueta(z) + 0.008 + fuera
    return (math.cos(a) * r, -0.01 + math.sin(a) * r * ELIP, z)
for lado, a in (("l", ABIERTO[0]), ("r", ABIERTO[1])):
    tubo("chaqueta", f"Solapa_{lado}", [sobre_chaqueta(a, 0.72 - i * (0.72 - 0.375) / 11, 0.0) for i in range(12)], [0.01] * 12, "Dorado", None)
tubo("chaqueta", "BajoChaqueta", [sobre_chaqueta(ABIERTO[0] + (ABIERTO[1] - ABIERTO[0]) * k / 40, 0.372, 0.0) for k in range(41)], [0.01] * 41, "Dorado", None)
for lado, s in (("l", 1), ("r", -1)):
    tubo("chaqueta", f"MangaChaqueta_{lado}", [(0.15 * s, -0.003, Z_BRAZO + 0.004), (0.24 * s, -0.003, Z_BRAZO + 0.002), (0.32 * s, -0.003, Z_BRAZO)],
         [0.1, 0.098, 0.096], "Chaqueta", None, cerrar=False)
    toro("chaqueta", f"Remangado_{lado}", (0.322 * s, -0.003, Z_BRAZO), 0.096, 0.016, "Chaqueta", {f"upperarm_{lado}": 0.5, f"lowerarm_{lado}": 0.5}, rot=(0, math.radians(90), 0))
    esfera("chaqueta", f"HombroChaqueta_{lado}", (0.16 * s, -0.003, Z_BRAZO + 0.008), (0.1, 0.098, 0.098), "Chaqueta", None)

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
rl.pesar_piezas(GRUPOS["pelo"], cuerpo, arm, con_cuerpo=False); rl.pesar_piezas(rl.completar_zonas(GRUPOS["traje"]), cuerpo, arm); rl.pesar_piezas(rl.completar_zonas(GRUPOS["chaqueta"]), cuerpo, arm, con_cuerpo=False)
# Primero los pesos (la ropa los copia del cuerpo entero, brazos y piernas incluidos); después
# del cuerpo de Body05 solo se quedan las manos: su torso lleva pintada la ropa vieja y asomaba por la camisa
bm = bmesh.new(); bm.from_mesh(cuerpo.data)
mw = cuerpo.matrix_world
fuera = [v for v in bm.verts if abs((mw @ v.co).x) < 0.445]
bmesh.ops.delete(bm, geom=fuera, context='VERTS')
bm.to_mesh(cuerpo.data); bm.free()
unir(GRUPOS["pelo"], "Hair16_Heroe")
cuerpo.name = "Body23_Aventurero"   # el cuerpo de debajo va dentro: las manos
camiseta = unir(GRUPOS["traje"], "Body23_Aventurero", extra=cuerpo)
# La versión con chaqueta es otro cuerpo entero (la misma ropa con la chaqueta encima), no una pieza suelta: así va en
# el hueco de cuerpo del armario y deja libre el de la capa (Raúl, 8 nov: chaqueta y capa a la vez).
copia = camiseta.copy(); copia.data = camiseta.data.copy(); bpy.context.scene.collection.objects.link(copia)
unir(GRUPOS["chaqueta"], "Body24_AventureroChaqueta", extra=copia)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("WILL", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
