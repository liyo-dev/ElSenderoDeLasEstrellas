import bpy, sys, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
MOD, PELO, CARAS, SALIDA = a[:4]
bpy.ops.wm.open_mainfile(filepath=MOD)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name not in ("Head01_Male", "Body05"): bpy.data.objects.remove(o)
piel = bpy.data.materials.new("Piel"); piel.diffuse_color = (0.98, 0.78, 0.64, 1)
ropa = bpy.data.materials.new("Ropa"); ropa.diffuse_color = (0.12, 0.16, 0.36, 1)
for n, m in (("Head01_Male", piel), ("Body05", ropa)):
    o = bpy.data.objects[n]; o.data.materials.clear(); o.data.materials.append(m)
if PELO.endswith(".blend"):
    with bpy.data.libraries.load(PELO, link=False) as (fr, to): to.objects = [n for n in fr.objects if n.startswith("Hair")]
    for o in to.objects:
        bpy.context.scene.collection.objects.link(o)   # trae consigo su esqueleto (el mismo, en reposo)
else:
    bpy.ops.import_scene.fbx(filepath=PELO)
    for o in bpy.context.selected_objects:
        if o.type == 'MESH':
            for m in o.modifiers:
                if m.type == 'ARMATURE': m.object = bpy.data.objects["Armature"]
            o.data.materials.clear(); mm = bpy.data.materials.new("R"); mm.diffuse_color = (0.98, 0.80, 0.36, 1); o.data.materials.append(mm)
antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=CARAS)
for o in [o for o in bpy.data.objects if o not in antes]:
    if o.type == 'MESH' and not o.name.startswith("Eye30"): bpy.data.objects.remove(o)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'; sc.display.shading.show_cavity = True
sc.display.shading.show_object_outline = True
sc.render.resolution_x = 520; sc.render.resolution_y = 560
sc.world = bpy.data.worlds.new("w")
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.05
for i, (ang, alto) in enumerate(((0, 0.15), (35, 0.25), (90, 0.1), (160, 0.3))):
    r = math.radians(ang); t = Vector((0, 0, 1.0))
    cam.location = t + Vector((math.sin(r) * 3, -math.cos(r) * 3, alto * 3))
    cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f"{SALIDA}_{i}.png"; bpy.ops.render.render(write_still=True)
