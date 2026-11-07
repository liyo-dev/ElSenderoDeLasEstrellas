# Exporta a FBX el esqueleto y las mallas pedidas (o ninguna: solo el esqueleto, para la talla), sin animaciones,
# con los mismos ajustes que el resto de piezas hechas en Blender (Prendas/exportar_prenda.py).
#   args: entrada.blend, salida.fbx, mallas separadas por comas («-» = solo el esqueleto; «*» = todas menos las cabezas del pack)
import bpy, sys
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
bpy.ops.object.select_all(action='DESELECT')
arm = bpy.data.objects["Armature"]; arm.select_set(True)
if a[2] == "*": mallas = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith("Head0")]
elif a[2] == "-": mallas = []
else: mallas = [bpy.data.objects[n] for n in a[2].split(",")]
for o in mallas: o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=a[1], use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
    bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=False, axis_forward='-Z', axis_up='Y')
print("FBX", a[1], [o.name for o in mallas])
