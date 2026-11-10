# Kit para hacer pelos y ropa sobre el esqueleto de RPG Tiny Hero (base_adulto.blend, ver base_adulto.py):
# formas (esfera, toro, torno, tubo, casquete y picos de pelo), pesos y unión de piezas. Se usa con
#     exec(open(os.path.join(os.path.dirname(__file__), "kit.py")).read())
# después de abrir la base. El personaje mira a -Y; su izquierda es +X; medidas del adulto (cadera 0,33,
# hombros 0,63, cuello 0,71, centro de la cabeza 0,99): la talla de niño se aplica al final con
# cuerpo_nino.reproporcionar, y la ropa la sigue porque va pesada como el cuerpo.
import bpy, bmesh, math
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

MAT = {}
def materiales(colores):
    for n, c in colores.items():
        m = bpy.data.materials.new(n); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
        m.diffuse_color = c; MAT[n] = m

GRUPOS = {}
def nueva(grupo, nombre, bm, mat, pesos=None, grosor=0.0, liso=True):
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(MAT[mat])
    for p in o.data.polygons: p.use_smooth = liso
    if grosor:
        s = o.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = grosor; s.offset = 1
    GRUPOS.setdefault(grupo, []).append((o, pesos)); return o

def transformar(bm, centro, escala, rot=(0, 0, 0)):
    M = Matrix.Translation(centro) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*escala, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)

def esfera(g, nombre, centro, escala, mat, pesos, rot=(0, 0, 0), u=24, v=14):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=1)
    transformar(bm, centro, escala, rot); return nueva(g, nombre, bm, mat, pesos)

def toro(g, nombre, centro, mayor, menor, mat, pesos, rot=(0, 0, 0), escala=(1, 1, 1)):
    bm = bmesh.new(); M, N = 28, 10
    filas = [[bm.verts.new(((mayor + menor * math.cos(2*math.pi*j/N)) * math.cos(2*math.pi*i/M),
                            (mayor + menor * math.cos(2*math.pi*j/N)) * math.sin(2*math.pi*i/M),
                            menor * math.sin(2*math.pi*j/N))) for j in range(N)] for i in range(M)]
    for i in range(M):
        for j in range(N):
            bm.faces.new((filas[i][j], filas[(i+1) % M][j], filas[(i+1) % M][(j+1) % N], filas[i][(j+1) % N]))
    transformar(bm, centro, escala, rot); return nueva(g, nombre, bm, mat, pesos)

def torno(g, nombre, perfil, mat, pesos=None, seg=40, elip=0.92, cy=-0.01, onda=0.0, n_onda=8, grosor=0.0, cx=0.0, cerrar_arriba=False, arco=None):
    """Superficie de revolución: perfil = [(z, radio)…] de arriba abajo. «onda» ondula el borde de abajo (faldas).
    arco=(a0, a1): solo ese trozo, abierto (radianes; -π/2 = delante): delantales, paneles."""
    bm = bmesh.new(); filas = []
    n = seg + 1 if arco else seg
    ang = (lambda k: arco[0] + (arco[1] - arco[0]) * k / seg) if arco else (lambda k: 2*math.pi*k/seg)
    for f, (z, r) in enumerate(perfil):
        u = f / max(1, len(perfil) - 1)
        filas.append([bm.verts.new((cx + math.cos(ang(k)) * r * (1 + onda * u * math.sin(n_onda * ang(k))),
                                    cy + math.sin(ang(k)) * r * elip * (1 + onda * u * math.sin(n_onda * ang(k))), z)) for k in range(n)])
    for f in range(len(filas) - 1):
        for k in range(n if not arco else n - 1):
            bm.faces.new((filas[f][k], filas[f][(k+1) % n], filas[f+1][(k+1) % n], filas[f+1][k]))
    if cerrar_arriba and not arco:
        c = bm.verts.new((cx, cy, perfil[0][0]))
        for k in range(seg): bm.faces.new((c, filas[0][(k+1) % seg], filas[0][k]))
    return nueva(g, nombre, bm, mat, pesos, grosor)

def tubo(g, nombre, puntos, radios, mat, pesos, seg=14, cerrar=True, aplastar=1.0):
    """Tubo a lo largo de una curva con su radio en cada punto (mangas, perneras, coletas, cordones)."""
    bm = bmesh.new(); filas = []
    pts = [Vector(p) for p in puntos]
    if len(radios) != len(pts):
        radios = [radios[min(len(radios)-1, round(i * (len(radios)-1) / max(1, len(pts)-1)))] for i in range(len(pts))]
    t_ant = None; a = None
    for i, p in enumerate(pts):
        t = (pts[min(i+1, len(pts)-1)] - pts[max(i-1, 0)]).normalized()
        if a is None: a = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0)); a = (a - t * a.dot(t)).normalized()
        else: a = (t_ant.rotation_difference(t) @ a); a = (a - t * a.dot(t)).normalized()
        t_ant = t; b = t.cross(a)
        filas.append([bm.verts.new(p + (a * math.cos(2*math.pi*k/seg) * aplastar + b * math.sin(2*math.pi*k/seg)) * radios[i]) for k in range(seg)])
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

# ── Pelo al estilo del pack: un casquete con volumen y pocos picos grandes de caras planas ──
CAB = Vector((0, -0.015, 0.99)); H = {"head": 1.0}
CENTRO_PELO = CAB + Vector((0, 0.03, 0.03))
RAD = Vector((0.375, 0.355, 0.355))
def punto(theta_g, phi_g, extra=1.0):
    """Punto del casquete: theta desde arriba (0) hacia abajo (90 = ecuador); phi 0 = +X (izquierda), -90 = delante."""
    t, p = math.radians(theta_g), math.radians(phi_g)
    return CENTRO_PELO + Vector((RAD.x * math.sin(t) * math.cos(p), RAD.y * math.sin(t) * math.sin(p), RAD.z * math.cos(t))) * extra
def casquete(nombre, corte_cara, corte_nuca, mat, g="pelo", rad=None):
    rad = rad or RAD
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1)
    transformar(bm, CENTRO_PELO, tuple(rad))
    quitar = []
    for v in bm.verts:
        d = v.co - CENTRO_PELO
        delante = -d.y / rad.y
        limite = corte_nuca + (corte_cara - corte_nuca) * max(0.0, delante)
        if d.z < limite: quitar.append(v)
    bmesh.ops.delete(bm, geom=quitar, context='VERTS')
    return nueva(g, nombre, bm, mat, H, liso=False)
def pico(nombre, theta_g, phi_g, direccion, largo, ancho, mat, lados=4, hundir=0.06, g="pelo", curvar=0.0):
    """Mechón en pico que nace en el casquete y apunta a «direccion»; «curvar» lo dobla hacia abajo a mitad."""
    base = punto(theta_g, phi_g, 1.0 - hundir)
    d = Vector(direccion).normalized()
    a = d.orthogonal().normalized(); b = d.cross(a)
    bm = bmesh.new()
    anillo = [bm.verts.new(base + (a * math.cos(2*math.pi*k/lados + 0.4) + b * math.sin(2*math.pi*k/lados + 0.4)) * ancho) for k in range(lados)]
    if curvar:
        medio = base + d * largo * 0.55
        anillo2 = [bm.verts.new(medio + (v.co - base) * 0.55) for v in anillo]
        d2 = (d + Vector((0, 0, -curvar))).normalized()
        punta = bm.verts.new(medio + d2 * largo * 0.5)
        for k in range(lados):
            bm.faces.new((anillo[k], anillo[(k+1) % lados], anillo2[(k+1) % lados], anillo2[k]))
            bm.faces.new((anillo2[k], anillo2[(k+1) % lados], punta))
    else:
        punta = bm.verts.new(base + d * largo)
        for k in range(lados): bm.faces.new((anillo[k], anillo[(k+1) % lados], punta))
    bm.faces.new(anillo[::-1])
    return nueva(g, nombre, bm, mat, H, liso=False)
def hacia(theta_g, phi_g, levantar=0.0, girar=(0, 0, 0)):
    n = (punto(theta_g, phi_g) - CENTRO_PELO).normalized()
    return (n + Vector((0, 0, levantar)) + Vector(girar)).normalized()

# ── Pesos y unión ──
def pesar(lista, arm, cuerpo):
    """La ropa sin pesos propios copia los del cuerpo (antes de recortarlo); el resto, los que se le den."""
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

def recortar_cuerpo(cuerpo, quitar):
    """Quita del cuerpo de debajo los vértices tapados por la ropa (quitar(p) con p en el mundo)."""
    bm = bmesh.new(); bm.from_mesh(cuerpo.data); mw = cuerpo.matrix_world
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if quitar(mw @ v.co)], context='VERTS')
    bm.to_mesh(cuerpo.data); bm.free()

def sacar_de_la_cara(lista, cabeza, margen=0.014):
    """Nada del pelo puede quedar dentro de la cabeza: lo que se meta se saca a «margen» de la piel."""
    bm = bmesh.new(); bm.from_mesh(cabeza.data); bm.transform(cabeza.matrix_world); arbol = BVHTree.FromBMesh(bm)
    C = Vector((0, -0.013, 0.99)); n = 0
    for o, _ in lista:
        mw = o.matrix_world; inv = mw.inverted()
        for v in o.data.vertices:
            p = mw @ v.co; t = (p - C).normalized()
            hit = arbol.ray_cast(C, t)
            if hit[0] is not None and (p - C).length < (hit[0] - C).length + margen:
                v.co = inv @ (hit[0] + t * margen); n += 1
    bm.free(); return n

def pelo_del_pack(fbx, nombre, mat, g="pelo"):
    """Un pelo del pack (HeadParts/HairNN.fbx, en el espacio del hueso de la cabeza) como malla del kit, colocado
    sobre la cabeza del adulto y pesado a «head», para deformarlo a gusto."""
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    nuevos = [o for o in bpy.data.objects if o not in antes]
    m = [o for o in nuevos if o.type == 'MESH'][0]
    bm = bmesh.new(); bm.from_mesh(m.data); bm.transform(m.matrix_world)
    bmesh.ops.translate(bm, vec=CABEZA_HUESO, verts=bm.verts)
    # fuera las cejas que traen los pelos del pack (islas pequeñas delante de la frente): van en las caras nuevas
    bm.verts.ensure_lookup_table(); vistos = set(); quitar = []
    for v0 in bm.verts:
        if v0.index in vistos: continue
        isla = [v0]; vistos.add(v0.index); k = 0
        while k < len(isla):
            for e in isla[k].link_edges:
                w = e.other_vert(isla[k])
                if w.index not in vistos: vistos.add(w.index); isla.append(w)
            k += 1
        if len(isla) < 100 and max(v.co.y for v in isla) < -0.19: quitar += isla
    bmesh.ops.delete(bm, geom=quitar, context='VERTS')
    print("CEJAS DEL PACK FUERA", len(quitar))
    for o in nuevos: bpy.data.objects.remove(o)
    return nueva(g, nombre, bm, mat, H, liso=False)
CABEZA_HUESO = Vector((0, -0.013, 0.914))   # el hueso «head» del adulto en el mundo

def lazo(g, nombre, centro, mat, tam=1.0, girar=0.0, pesos=None):
    """Lazo: dos lazadas (óvalos aplanados), el nudo y dos cintas cortas que caen."""
    c = Vector(centro); R = Euler((0, 0, girar)).to_matrix()
    for i, s in enumerate((1, -1)):
        esfera(g, f"{nombre}_Lazada{i}", c + R @ Vector((0.05 * s * tam, 0, 0.012 * tam)), (0.05 * tam, 0.02 * tam, 0.032 * tam), mat, pesos,
               rot=(0, math.radians(-18 * s), girar), u=12, v=8)
        tubo(g, f"{nombre}_Cinta{i}", [tuple(c + R @ Vector((0.006 * s, 0, -0.01) ) * 1), tuple(c + R @ Vector((0.028 * s * tam, -0.004, -0.06 * tam)))],
             [0.012 * tam, 0.01 * tam], mat, pesos, seg=6, aplastar=0.4)
    esfera(g, f"{nombre}_Nudo", c, (0.02 * tam, 0.02 * tam, 0.02 * tam), mat, pesos, u=10, v=6)
