# Foto de la nuca: el pelo de un blend sobre su cabeza, de lado, por detrás y desde abajo.
#   args: blend, pelo, cabeza (objeto del blend o FBX de cabeza suelta del pack), color de pelo r,g,b, salida.png
import bpy, sys, math
from mathutils import Matrix, Vector
from PIL import Image
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
pelo = bpy.data.objects[a[1]]
if a[2] in bpy.data.objects: cab = bpy.data.objects[a[2]]
else:
    antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=a[2]); bpy.context.view_layer.update()
    nuevos = [o for o in bpy.data.objects if o not in antes]; cab = [o for o in nuevos if o.type == 'MESH'][0]
    mw = cab.matrix_world.copy(); cab.parent = None
    for m in list(cab.modifiers): cab.modifiers.remove(m)
    cab.data.transform(mw); cab.matrix_world = Matrix.Identity(4); cab.data.transform(Matrix.Translation((0, -0.0129, 0.914)))
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_render = o not in (pelo, cab)
mp = bpy.data.materials.new("p"); mp.diffuse_color = (0.96, 0.75, 0.59, 1); cab.data.materials.clear(); cab.data.materials.append(mp)
mh = bpy.data.materials.new("h"); mh.diffuse_color = tuple(float(x) for x in a[3].split(",")) + (1,)
for i in range(len(pelo.data.materials)): pelo.data.materials[i] = mh
vs = [cab.matrix_world @ v.co for v in cab.data.vertices]; C = Vector(tuple((min(p[i] for p in vs) + max(p[i] for p in vs)) / 2 for i in range(3)))
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'; sc.world = bpy.data.worlds.new("w")
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.25; sc.render.resolution_x = 400; sc.render.resolution_y = 400
tiles = []
for ang, elev in ((90, 0), (135, 0), (180, 0), (180, -28)):
    t = math.radians(ang); e = math.radians(elev)
    d = Vector((math.sin(t) * math.cos(e), -math.cos(t) * math.cos(e), math.sin(e)))
    cam.location = C + Vector((0, 0, 0.02)) + d * 6
    cam.rotation_euler = (math.radians(90) - e, 0, t)
    sc.render.filepath = a[4] + f"_{len(tiles)}.png"; bpy.ops.render.render(write_still=True); tiles.append(sc.render.filepath)
ims = [Image.open(p) for p in tiles]; out = Image.new("RGB", (sum(i.width for i in ims), ims[0].height)); x = 0
for im in ims: out.paste(im, (x, 0)); x += im.width
out.save(a[4])
