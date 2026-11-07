# Para las fotos: añade a un personaje piezas de otro .blend con el mismo esqueleto. args: personaje.blend, piezas.blend, nombres,coma, salida
import bpy, sys
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0]); arm = bpy.data.objects["Armature"]
with bpy.data.libraries.load(a[1]) as (fr, to): to.objects = a[2].split(",")
for o in to.objects:
    bpy.context.scene.collection.objects.link(o)
bpy.context.view_layer.update()
for o in to.objects:
    mw = o.matrix_world.copy(); o.parent = arm; o.matrix_world = mw
    for m in o.modifiers:
        if m.type == 'ARMATURE': m.object = arm
bpy.ops.wm.save_as_mainfile(filepath=a[3])
