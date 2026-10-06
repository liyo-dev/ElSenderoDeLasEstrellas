# Genera la mercancía del puesto de Tomasa y la exporta a Assets/Art/Models/PuestoDeTomasa.
# Uso: blender --background --python generar_mercancia.py
# Coordenadas: locales del mostrador Counter01 del Fantasy Kingdom Pack (Blender, Z arriba, -Y = cliente).
import bpy, os, sys
AQUI=os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0,AQUI)
import mercancia
SALIDA=os.path.normpath(os.path.join(AQUI,"..","..","..","Assets","Art","Models","PuestoDeTomasa"))
os.makedirs(SALIDA,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
img=mercancia.crear_paleta(os.path.join(SALIDA,"PaletaTomasa.png"))
raiz,objs=mercancia.montar()
mat=bpy.data.materials.new("PaletaTomasa"); mat.use_nodes=True
t=mat.node_tree.nodes.new("ShaderNodeTexImage"); t.image=img; t.interpolation='Closest'
mat.node_tree.links.new(t.outputs[0],mat.node_tree.nodes["Principled BSDF"].inputs[0])
for o in objs: o.data.materials.clear(); o.data.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(AQUI,"MercanciaTomasa.blend"))
bpy.ops.object.select_all(action='DESELECT'); raiz.select_set(True)
for o in objs: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(SALIDA,"MercanciaTomasa.fbx"),use_selection=True,
    object_types={'EMPTY','MESH'},apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,
    axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',path_mode='STRIP',embed_textures=False,add_leaf_bones=False)
