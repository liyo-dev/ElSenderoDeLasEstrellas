# Disfraces de Chuchelandia para Will y Liam, sobre el esqueleto de RPG Tiny Hero (base.blend, importado
# de AllBodiesCloaks.fbx). Mira a -Y; su izquierda es +X. Cabeza (Head01_Male): centro ≈ (0, -0.013, 0.99),
# radio ≈ 0.33. Las piezas del cuerpo se pesan copiando los pesos del cuerpo de debajo; las de la cabeza y
# los brazos, rígidas a su hueso.
#   armadura → Armadura_Gominola (Will, sobre Body05): peto y barriga de osito de gominola, hombreras,
#              faldón de gajos, brazaletes y orejitas de osito.
#   paje     → Traje_Paje (Liam, sobre Body12): tabardo de paje con un gran corazón, gorguera, mangas
#              abullonadas, boina y cuerno de unicornio dorado.
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
args = sys.argv[sys.argv.index("--")+1:]
BASE, QUE, SALIDA = args[0], args[1], args[2]
CUERPO = {"armadura": "Body05", "paje": "Body12"}[QUE]
NOMBRE = {"armadura": "Armadura_Gominola", "paje": "Traje_Paje"}[QUE]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects[CUERPO]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != CUERPO: bpy.data.objects.remove(o)

COLORES = {
    "armadura": {"Gominola": (0.35, 0.9, 0.3, 1), "GominolaClara": (0.7, 1.0, 0.55, 1), "GominolaOscura": (0.15, 0.65, 0.2, 1)},
    "paje": {"Paje": (0.55, 0.78, 1.0, 1), "Corazon": (1.0, 0.35, 0.6, 1), "Blanco": (1.0, 0.97, 0.95, 1),
             "Dorado": (1.0, 0.8, 0.3, 1), "Boina": (0.95, 0.45, 0.7, 1)},
}[QUE]
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

PIEZAS = []
def nueva(nombre, bm, mat, pesos=None, grosor=0.0):
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(MAT[mat])
    for p in o.data.polygons: p.use_smooth = True
    if grosor:
        s = o.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = grosor; s.offset = 1
    PIEZAS.append((o, pesos)); return o

def transformar(bm, centro, escala, rot):
    M = Matrix.Translation(centro) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*escala, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)

def torno(nombre, perfil, mat, pesos=None, seg=40, elip=0.92, cy=-0.01, onda=0.0, n_onda=8, grosor=0.0):
    bm = bmesh.new(); filas = []
    for f, (z, r) in enumerate(perfil):
        u = f / max(1, len(perfil) - 1)
        filas.append([bm.verts.new((math.cos(2*math.pi*k/seg) * r * (1 + onda * u * math.sin(n_onda * 2*math.pi*k/seg)),
                                    cy + math.sin(2*math.pi*k/seg) * r * elip * (1 + onda * u * math.sin(n_onda * 2*math.pi*k/seg)), z))
                      for k in range(seg)])
    for f in range(len(filas) - 1):
        for k in range(seg):
            bm.faces.new((filas[f][k], filas[f][(k+1) % seg], filas[f+1][(k+1) % seg], filas[f+1][k]))
    return nueva(nombre, bm, mat, pesos, grosor)

def esfera(nombre, centro, escala, mat, pesos, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1)
    transformar(bm, centro, escala, rot); return nueva(nombre, bm, mat, pesos)

def toro(nombre, centro, mayor, menor, mat, pesos, rot=(0, 0, 0), escala=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=mayor, minor_radius=menor, major_segments=28, minor_segments=10)
    t = bpy.context.object; bm = bmesh.new(); bm.from_mesh(t.data); bpy.data.objects.remove(t)
    transformar(bm, centro, escala, rot); return nueva(nombre, bm, mat, pesos)

def caja(nombre, centro, tam, mat, pesos=None, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.bevel(bm, geom=bm.edges[:] + bm.verts[:], offset=0.012, segments=3, affect='EDGES')
    transformar(bm, centro, tam, rot); return nueva(nombre, bm, mat, pesos)

def cuerno(nombre, base, direccion, largo, radio, mat, pesos, vueltas=4):
    """Cono en espiral (cuerno de unicornio): anillos que se estrechan con un surco que gira."""
    d = Vector(direccion).normalized(); bm = bmesh.new(); seg = 16; filas = []; n = 14
    q = Vector((0, 0, 1)).rotation_difference(d)
    for f in range(n + 1):
        u = f / n; r = radio * (1 - u) + 0.002
        fila = []
        for k in range(seg):
            a = 2*math.pi*k/seg
            surco = 1 - 0.18 * max(0, math.cos(a * 2 - u * vueltas * 2*math.pi))
            p = Vector((math.cos(a) * r * surco, math.sin(a) * r * surco, u * largo))
            fila.append(bm.verts.new(Vector(base) + q @ p))
        filas.append(fila)
    for f in range(n):
        for k in range(seg):
            bm.faces.new((filas[f][k], filas[f][(k+1) % seg], filas[f+1][(k+1) % seg], filas[f+1][k]))
    return nueva(nombre, bm, mat, pesos)

CAB = Vector((0, -0.013, 0.99))
if QUE == "armadura":
    # Peto redondo de gominola con la barriga clara de osito; faldón de gajos; hombreras; brazaletes
    torno("Peto", [(0.73, 0.14), (0.68, 0.205), (0.6, 0.222), (0.52, 0.205), (0.45, 0.192)], "Gominola", grosor=0.012)
    esfera("Barriga", (0, -0.2, 0.57), (0.11, 0.035, 0.12), "GominolaClara", {"spine_03": 0.7, "pelvis": 0.3})
    torno("Faldon", [(0.47, 0.195), (0.38, 0.215), (0.3, 0.235)], "GominolaOscura", onda=0.12, n_onda=7, grosor=0.012)
    for lado, s in (("l", 1), ("r", -1)):
        esfera(f"Hombrera_{lado}", (0.235 * s, -0.005, 0.67), (0.11, 0.105, 0.075), "Gominola", {f"upperarm_{lado}": 1.0}, rot=(0, math.radians(-18 * s), 0))
        toro(f"Brazal_{lado}", (0.43 * s, -0.003, 0.633), 0.052, 0.022, "GominolaClara", {f"lowerarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
        # Orejitas de osito de gominola (asoman entre los pinchos del pelo)
        o = CAB + Vector((0.27 * s, 0.03, 0.44))
        esfera(f"Oreja_{lado}", o, (0.12, 0.06, 0.12), "Gominola", {"head": 1.0}, rot=(0, math.radians(-25 * s), 0))
        esfera(f"OrejaDentro_{lado}", o + Vector((0, -0.035, 0)), (0.07, 0.02, 0.07), "GominolaClara", {"head": 1.0}, rot=(0, math.radians(-25 * s), 0))
else:
    # Tabardo de paje delante y detrás (con ribete dorado), gorguera, mangas abullonadas, boina y cuerno
    caja("TabardoDelante", (0, -0.2, 0.43), (0.27, 0.025, 0.5), "Paje")
    caja("TabardoDetras", (0, 0.19, 0.43), (0.27, 0.025, 0.5), "Paje")
    caja("RibeteDelante", (0, -0.21, 0.18), (0.29, 0.03, 0.035), "Dorado")
    caja("RibeteDetras", (0, 0.2, 0.18), (0.29, 0.03, 0.035), "Dorado")
    # El gran corazón del pecho («el corazoncito»)
    for lado, s in (("l", 1), ("r", -1)):
        esfera(f"Corazon_{lado}", (0.05 * s, -0.222, 0.57), (0.07, 0.015, 0.07), "Corazon", {"spine_03": 1.0})
    esfera("CorazonPunta", (0, -0.222, 0.51), (0.065, 0.015, 0.07), "Corazon", {"spine_03": 1.0}, rot=(0, math.radians(45), 0))
    torno("Gorguera", [(0.72, 0.12), (0.7, 0.24), (0.68, 0.12)], "Blanco", {"spine_03": 1.0}, seg=64, onda=0.0, elip=0.95)
    for lado, s in (("l", 1), ("r", -1)):
        esfera(f"Manga_{lado}", (0.24 * s, -0.002, 0.637), (0.105, 0.095, 0.095), "Paje", {f"upperarm_{lado}": 1.0})
        toro(f"Raya_{lado}", (0.24 * s, -0.002, 0.637), 0.098, 0.012, "Corazon", {f"upperarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
    # Boina un poco ladeada sobre el pelo, con la pluma
    esfera("Boina", CAB + Vector((0.04, 0.03, 0.36)), (0.27, 0.25, 0.075), "Boina", {"head": 1.0}, rot=(math.radians(-8), math.radians(10), 0))
    esfera("BotonBoina", CAB + Vector((0.05, 0.03, 0.44)), (0.03, 0.03, 0.025), "Dorado", {"head": 1.0})
    # Cuerno de unicornio: sale de la frente, hacia arriba y un poco al frente
    base = CAB + Vector((0, -0.28, 0.2))
    cuerno("Cuerno", base, (0, -0.32, 1), 0.34, 0.05, "Dorado", {"head": 1.0})

for o, pesos in PIEZAS:
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
bpy.ops.object.select_all(action='DESELECT')
for o, _ in PIEZAS: o.select_set(True)
bpy.context.view_layer.objects.active = PIEZAS[0][0]
bpy.ops.object.join()
bpy.context.object.name = NOMBRE
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("PRENDA", NOMBRE, len(bpy.context.object.data.vertices))
