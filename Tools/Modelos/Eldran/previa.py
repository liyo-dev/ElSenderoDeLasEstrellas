# Previa de un personaje con vestuario propio: junta el cuerpo nuevo con su cabeza y su pelo del pack.
#   args: blend del traje, salida.blend, color de piel r,g,b, color de pelo r,g,b, fbx de cabeza, fbx de pelo...
#   (una pieza con color propio: «ruta.fbx@r,g,b»)
import bpy, sys
from mathutils import Matrix
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
piel = tuple(float(x) for x in a[2].split(",")) + (1,); pelo = tuple(float(x) for x in a[3].split(",")) + (1,)
def mat(n, c):
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c; m.diffuse_color = c; return m
MP, MH = mat("PielPrevia", piel), mat("PeloPrevia", pelo)
for o in bpy.data.objects:
    if o.type == 'MESH':
        for i, m in enumerate(o.data.materials):
            if m and m.name.startswith("lambert"): o.data.materials[i] = MP
for k, fbx in enumerate(a[4:]):
    propio = None   # «ruta@r,g,b»: esa pieza con su color (una corona, unas gafas)
    if "@" in fbx: fbx, c = fbx.rsplit("@", 1); propio = mat("Pieza%d" % k, tuple(float(x) for x in c.split(",")) + (1,))
    antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=fbx)
    for o in [o for o in bpy.data.objects if o not in antes]:
        if o.type != 'MESH': bpy.data.objects.remove(o); continue
        bpy.context.view_layer.update(); o.parent = None; o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)
        o.data.transform(Matrix.Translation((0, -0.0129, 0.914)))
        o.data.materials.clear(); o.data.materials.append(propio or (MP if k == 0 else MH))
bpy.ops.wm.save_as_mainfile(filepath=a[1])
