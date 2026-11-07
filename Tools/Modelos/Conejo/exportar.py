import bpy, sys
a=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
rig=bpy.data.objects["Conejo_Rig"]
rig.animation_data_create(); rig.animation_data.action=None
for o in bpy.data.objects: o.select_set(o.name in ("Conejo_Rig","Conejo"))
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=a[1], use_selection=True, object_types={'ARMATURE','MESH'},
    apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
    mesh_smooth_type='FACE', use_armature_deform_only=False)
