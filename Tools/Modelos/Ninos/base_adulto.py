# Base del modelo modular de RPG Tiny Hero en una sola escena: el esqueleto con todos los cuerpos y capas
# (AllBodiesCloaks) y las piezas de cabeza de ModularCharacterPolyart (cabezas, pelos, ojos, bocas), colgadas
# del hueso «head» del primer esqueleto. args: AllBodiesCloaks.fbx, ModularCharacterPolyart.fbx, salida.blend
import bpy, sys
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a[0])
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; arm.name = "Armature"
antes = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=a[1])
nuevos = [o for o in bpy.data.objects if o not in antes]
for o in nuevos:
    if o.type == 'MESH' and o.parent_type == 'BONE':
        mw = o.matrix_world.copy()
        o.parent = arm; o.parent_type = 'BONE'; o.parent_bone = o.parent_bone or "head"
        bpy.context.view_layer.update(); o.matrix_world = mw
for o in nuevos:
    if o.type == 'ARMATURE' or (o.type == 'MESH' and o.parent_type != 'BONE'): bpy.data.objects.remove(o)
for o in bpy.data.objects:
    if o.type == 'MESH' and "." in o.name: o.name = o.name.split(".")[0]
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=a[2])
print("BASE", sorted(o.name for o in bpy.data.objects))
