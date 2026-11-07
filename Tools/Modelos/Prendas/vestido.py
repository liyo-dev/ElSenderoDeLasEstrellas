# Vestido de candidata de Estela (Chuchelandia): siete volantes, corpiño con corazón, mangas de farol,
# lazo gigante a la espalda y zapatos de caramelo. Se modela sobre el esqueleto de RPG Tiny Hero
# (importado de AllBodiesCloaks.fbx) y se pesa copiando los pesos del cuerpo Body09 (el que lleva
# Estela siempre, que queda debajo), así se dobla igual que él. Mira a -Y; su izquierda es +X. Unidades: las del
# esqueleto en la escena (≈1,2 m de alto).
import bpy, bmesh, math, sys
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=args[0])
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body09"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body09": bpy.data.objects.remove(o)

COLORES = {"Vestido": (1.0, 0.5, 0.72, 1), "Volante": (1.0, 0.93, 0.96, 1), "Lazo": (0.95, 0.22, 0.48, 1),
           "Caramelo": (0.86, 0.12, 0.2, 1), "Dorado": (1.0, 0.8, 0.3, 1)}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.diffuse_color = c; MAT[n] = m

PIEZAS = []   # (obj, pesos) pesos: None = copiar de Body11; o dict hueso->peso fijo
def nueva(nombre, bm, mat, pesos=None):
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(MAT[mat])
    for p in o.data.polygons: p.use_smooth = True
    PIEZAS.append((o, pesos)); return o

def anillo(nombre, z_arriba, z_abajo, r_arriba, r_abajo, mat, onda=0.0, n_onda=12, seg=48, filas=4, elip=0.92, cy=-0.015, grosor=0.006):
    bm = bmesh.new(); filas_v = []
    for f in range(filas + 1):
        u = f / filas; z = z_arriba + (z_abajo - z_arriba) * u
        r = r_arriba + (r_abajo - r_arriba) * (u ** 1.4)
        fila = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            rr = r * (1 + onda * (u ** 2) * math.sin(n_onda * a))
            fila.append(bm.verts.new((math.cos(a) * rr, cy + math.sin(a) * rr * elip, z)))
        filas_v.append(fila)
    for f in range(filas):
        for k in range(seg):
            a, b = filas_v[f][k], filas_v[f][(k + 1) % seg]
            c, d = filas_v[f + 1][(k + 1) % seg], filas_v[f + 1][k]
            bm.faces.new((a, b, c, d))
    o = nueva(nombre, bm, mat)
    if grosor:
        s = o.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = grosor; s.offset = 1
    return o

def esfera(nombre, centro, escala, mat, pesos, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1)
    from mathutils import Matrix, Euler
    M = Matrix.Translation(centro) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*escala, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
    return nueva(nombre, bm, mat, pesos)

def toro(nombre, centro, mayor, menor, mat, pesos, rot=(0, 0, 0), escala=(1, 1, 1)):
    bm = bmesh.new()
    bpy.ops.mesh.primitive_torus_add(major_radius=mayor, minor_radius=menor, major_segments=28, minor_segments=10)
    t = bpy.context.object; bm.from_mesh(t.data); bpy.data.objects.remove(t)
    from mathutils import Matrix, Euler
    M = Matrix.Translation(centro) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*escala, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
    return nueva(nombre, bm, mat, pesos)

# Radio del cuerpo de Estela (Body09; por debajo de 0,2 m, lo que tapa las piernas) a cada altura, medido en el modelo
PERFIL = [(0.0, 0.215), (0.1, 0.212), (0.2, 0.205), (0.3, 0.19), (0.4, 0.178), (0.47, 0.168)]
def radio_cuerpo(z):
    for (z0, r0), (z1, r1) in zip(PERFIL, PERFIL[1:]):
        if z0 <= z <= z1: return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return PERFIL[-1][1] if z > 0.47 else PERFIL[0][1]

# Siete volantes, de la cintura al suelo, alternando rosa y nata; cada uno sale un poco más que el anterior
for i in range(7):
    zt = 0.47 - i * 0.066; zb = max(0.012, zt - 0.1)
    rt = radio_cuerpo(zt) + 0.018 + i * 0.004; rb = radio_cuerpo(zb) + 0.045 + i * 0.006
    anillo(f"Volante{i+1}", zt, zb, rt, rb, "Vestido" if i % 2 == 0 else "Volante", onda=0.07, n_onda=14 + i)
# Corpiño, cuello de volante y cinturón dorado
def torno(nombre, perfil, mat, seg=48, elip=0.9, cy=-0.005):
    bm = bmesh.new(); filas = []
    for z, r in perfil:
        filas.append([bm.verts.new((math.cos(2*math.pi*k/seg)*r, cy + math.sin(2*math.pi*k/seg)*r*elip, z)) for k in range(seg)])
    for f in range(len(filas)-1):
        for k in range(seg):
            bm.faces.new((filas[f][k], filas[f][(k+1)%seg], filas[f+1][(k+1)%seg], filas[f+1][k]))
    return nueva(nombre, bm, mat)
torno("Corpino", [(0.715, 0.12), (0.68, 0.17), (0.62, 0.195), (0.55, 0.188), (0.5, 0.178), (0.45, 0.174)], "Vestido")
anillo("Cuello", 0.73, 0.69, 0.09, 0.15, "Volante", onda=0.1, n_onda=16, filas=2, grosor=0.004)
toro("Cinturon", (0, -0.008, 0.47), 0.178, 0.012, "Dorado", {"pelvis": 1.0}, escala=(1, 0.92, 1))
esfera("Corazon", (0, -0.182, 0.585), (0.035, 0.012, 0.032), "Dorado", {"spine_03": 1.0})
# Mangas de farol
for lado, s in (("l", 1), ("r", -1)):
    esfera(f"Manga_{lado}", (0.24 * s, -0.002, 0.64), (0.105, 0.095, 0.095), "Volante", {f"upperarm_{lado}": 1.0})
    toro(f"Puno_{lado}", (0.33 * s, -0.002, 0.633), 0.05, 0.01, "Vestido", {f"upperarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
    # Zapatos de caramelo (asoman por delante del vestido)
    esfera(f"Zapato_{lado}", (0.123 * s, -0.2, 0.03), (0.065, 0.09, 0.038), "Caramelo", {f"foot_{lado}": 0.4, f"ball_{lado}": 0.6})
    esfera(f"Brillo_{lado}", (0.123 * s, -0.26, 0.055), (0.022, 0.03, 0.01), "Volante", {f"ball_{lado}": 1.0})
# El lazo gigante a la espalda (el «lazo-escudo»): dos lazadas, nudo y dos cintas
for lado, s in (("l", 1), ("r", -1)):
    toro(f"Lazada_{lado}", (0.14 * s, 0.215, 0.63), 0.1, 0.045, "Lazo", {"spine_03": 1.0}, rot=(math.radians(90), 0, 0), escala=(1.3, 1, 0.8))
    esfera(f"Cinta_{lado}", (0.06 * s, 0.215, 0.48), (0.035, 0.012, 0.12), "Lazo", {"spine_03": 0.6, "pelvis": 0.4}, rot=(0, math.radians(-14 * s), 0))
esfera("Nudo", (0, 0.22, 0.63), (0.06, 0.045, 0.055), "Lazo", {"spine_03": 1.0})

# ── Pesos ─────────────────────────────────────────────────────────────────────────────────
vacios = []
for o, pesos in PIEZAS:
    for m in [m for m in o.modifiers if m.type == 'SOLIDIFY']:
        bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
    o.parent = arm
    o.matrix_parent_inverse = arm.matrix_world.inverted()
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

# Una sola malla
bpy.ops.object.select_all(action='DESELECT')
for o, _ in PIEZAS: o.select_set(True)
bpy.context.view_layer.objects.active = PIEZAS[0][0]
bpy.ops.object.join()
v = bpy.context.object; v.name = "Vestido_Candidata"
bpy.ops.wm.save_as_mainfile(filepath=args[1])
print("VESTIDO", len(v.data.vertices), [m.name for m in v.data.materials], [g.name for g in v.vertex_groups])
