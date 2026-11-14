# Uso: python grosor_pelo.py -- pelo.fbx salida.fbx [metros=0.006]
# Da grosor hacia dentro a un pelo ya exportado (FBX con su esqueleto) y lo vuelve a exportar.
# Así el borde y los mechones se pintan por dentro: desde abajo no se ve a través (ver Prendas/chequeo_huecos.py).
import bpy, sys
a = sys.argv[sys.argv.index("--") + 1:]
ENTRADA, SALIDA, METROS = a[0], a[1], float(a[2]) if len(a) > 2 else 0.006
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=ENTRADA)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
pelo = next(o for o in bpy.data.objects if o.type == 'MESH')
esc = pelo.matrix_world.to_scale()[0]
s = pelo.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = METROS / esc; s.offset = -1; s.use_rim = True
bpy.context.view_layer.objects.active = pelo
# el grosor va antes que el esqueleto
while pelo.modifiers[0].name != "Grosor": bpy.ops.object.modifier_move_up(modifier="Grosor")
bpy.ops.object.modifier_apply(modifier="Grosor")
print("PELO", pelo.name, len(pelo.data.vertices), esc)
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); pelo.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=SALIDA, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
    bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=False, axis_forward='-Z', axis_up='Y')
