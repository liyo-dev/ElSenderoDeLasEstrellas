import bpy, sys
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
    if o.type in ('ARMATURE','MESH'): o.select_set(True)
bpy.context.view_layer.objects.active=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
# Comprobación: todas las caras de las piezas miran hacia delante (-Y); Unity no pinta el revés
reves = [o.name for o in bpy.data.objects if o.type == 'MESH'
         for p in o.data.polygons if (o.matrix_world.to_3x3() @ p.normal).y > 0 and p.area > 1e-8]
if reves: print("AVISO: caras del revés en", sorted(set(reves)), len(reves))
print("OBJ", sorted(o.name for o in bpy.data.objects if o.type=='MESH'))
bpy.ops.export_scene.fbx(filepath=a[1], use_selection=True, object_types={'ARMATURE','MESH'}, add_leaf_bones=False,
    bake_anim=False, mesh_smooth_type='FACE', axis_forward='-Z', axis_up='Y')
