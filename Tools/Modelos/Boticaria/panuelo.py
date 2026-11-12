# La boticaria: pañuelo verde salvia anudado en la cabeza, por encima de la diadema de su Hair09 (la tapa) con el
# nudo arriba delante, donde la diadema lleva la piedra, y una ramita de hierbas metida al lado. Ver INC-1111.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = la del vestuario de la boticaria). Mira a -Y; su izquierda es +X.
#   AC14_Panuelo → pesado a «head». Args: base.blend, Hair09.fbx, salida.blend
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
a = sys.argv[sys.argv.index("--")+1:]
BASE, PELO, SALIDA = a[0], a[1], a[2]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]
for o in list(bpy.data.objects):
    if o.type == 'MESH': bpy.data.objects.remove(o)
COLORES = {"Panuelo": (0.5, 0.66, 0.42, 1), "PanueloOscuro": (0.36, 0.5, 0.3, 1), "Hoja": (0.36, 0.68, 0.3, 1), "Tallo": (0.3, 0.42, 0.2, 1)}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c; m.diffuse_color = c; MAT[n] = m
R = math.radians
PIEZAS = []
def nueva(nombre, bm, mat):
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(MAT[mat])
    for p in o.data.polygons: p.use_smooth = True
    PIEZAS.append(o); return o
def esfera(nombre, centro, escala, mat, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1)
    M = Matrix.Translation(centro) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*escala, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts); return nueva(nombre, bm, mat)

# La diadema del pelo: las caras de Hair09 con el gris de la diadema en la paleta (UV ≈ 0,28 · 0,39) y la piedra (0,81 · 0,59)
antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=PELO)
pelo = next(o for o in bpy.data.objects if o not in antes and o.type == 'MESH')
for o in [o for o in bpy.data.objects if o not in antes and o != pelo]: bpy.data.objects.remove(o)
bpy.context.view_layer.update(); pelo.parent = None; pelo.data.transform(pelo.matrix_world); pelo.matrix_world = Matrix.Identity(4)
pelo.data.transform(Matrix.Translation((0, -0.0129, 0.914)))
uvl = pelo.data.uv_layers.active
def uv_de(p): return uvl.data[p.loop_indices[0]].uv
def en(p, u0, u1, v0, v1): return u0 < uv_de(p).x < u1 and v0 < uv_de(p).y < v1
# la diadema, el trozo de delante bajo la piedra (UV ≈ 0,15 · 0,30) y la piedra
banda = [p.index for p in pelo.data.polygons if en(p, 0.26, 0.30, 0.36, 0.42) or (en(p, 0.13, 0.17, 0.28, 0.32) and pelo.data.vertices[p.vertices[0]].co.z > 1.3) or en(p, 0.79, 0.83, 0.57, 0.61)]
piedra = [Vector(pelo.data.vertices[v].co) for p in pelo.data.polygons if 0.79 < uv_de(p).x < 0.83 and 0.57 < uv_de(p).y < 0.61 for v in p.vertices]
nudo = sum(piedra, Vector()) / len(piedra)
bm = bmesh.new(); bm.from_mesh(pelo.data)
bm.faces.ensure_lookup_table()
quitar = [f for f in bm.faces if f.index not in set(banda)]
bmesh.ops.delete(bm, geom=quitar, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.normal_update()
for v in bm.verts: v.co += v.normal * 0.012   # por fuera de la diadema, que no asome
bpy.data.objects.remove(pelo)
tela = nueva("Banda", bm, "Panuelo")
s = tela.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = 0.012; s.offset = 1

# Nudo arriba delante (tapa la piedra) con sus dos orejas, y una ramita al lado derecho
haciafuera = (nudo - Vector((0, -0.0129, 1.2))).normalized()
c = nudo + haciafuera * 0.025
esfera("Nudo", c, (0.06, 0.05, 0.05), "PanueloOscuro")
for lado in (1, -1):
    esfera(f"Oreja{lado}", c + Vector((0.085 * lado, 0.0, 0.04)), (0.08, 0.026, 0.045), "Panuelo", rot=(R(-20), R(-35 * lado), R(10 * lado)))
base = c + Vector((-0.13, 0.04, -0.05))
for k, (gx, gz) in enumerate(((-0.02, 0.07), (-0.05, 0.05), (0.0, 0.085))):
    esfera(f"Hoja{k}", base + Vector((gx, -0.01, gz)), (0.017, 0.007, 0.036), "Hoja", rot=(R(10), R(-25 + 20 * k), R(-30 + 25 * k)))
esfera("Tallo", base + Vector((-0.015, -0.005, 0.03)), (0.006, 0.006, 0.05), "Tallo", rot=(0, R(-20), 0))

for o in PIEZAS:
    for m in [m for m in o.modifiers if m.type == 'SOLIDIFY']:
        bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
    o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
    vg = o.vertex_groups.new(name="head"); vg.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    md = o.modifiers.new("Esqueleto", 'ARMATURE'); md.object = arm
bpy.ops.object.select_all(action='DESELECT')
for o in PIEZAS: o.select_set(True)
bpy.context.view_layer.objects.active = PIEZAS[0]
bpy.ops.object.join(); bpy.context.object.name = "AC14_Panuelo"
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", len(banda), "caras de banda; nudo en", tuple(round(x, 3) for x in nudo))
