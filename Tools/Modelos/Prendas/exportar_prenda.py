# Exporta una prenda (malla pesada) con el esqueleto de RPG Tiny Hero, sin animaciones: en Unity sus
# huesos se cambian por los del personaje (por nombre), así que solo importa su pose de atado.
import bpy, sys
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
bpy.ops.object.select_all(action='DESELECT')
bpy.data.objects["Armature"].select_set(True); bpy.data.objects[a[2]].select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects["Armature"]
bpy.ops.export_scene.fbx(filepath=a[1], use_selection=True, object_types={'ARMATURE','MESH'}, add_leaf_bones=False,
    bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=False, axis_forward='-Z', axis_up='Y')
