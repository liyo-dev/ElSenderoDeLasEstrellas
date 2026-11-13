# Iconos del armario para las prendas hechas en Blender (Body21-24, Cloak04, AC12): la pieza en
# 3/4 de frente, brazos bajados, colores planos y contorno de tinta, como los iconos del pack. La
# herramienta de Unity (Iconos del armario desde 3D) las fotografía desde arriba porque su raíz va
# girada; para estas piezas se usan estos. Uso: python iconos_de_ropa.py [-- Pieza ...]. Ver INC-1098
import bpy, sys, math, mathutils
import os
d=os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../Assets/Art/Characters/Protagonistas/')
items=[('Will/Body23_Aventurero.fbx','Body23_Aventurero'),('Will/Body24_AventureroChaqueta.fbx','Body24_AventureroChaqueta'),
 ('Estela/Body21_Hechicera.fbx','Body21_Hechicera'),('Estela/Cloak04_Hechicera.fbx','Cloak04_Hechicera'),
 ('Liam/Body22_Tunica.fbx','Body22_Tunica'),('Liam/AC12_Capucha.fbx','AC12_Capucha')]
sel=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
PIEL=(0.95,0.68,0.45,1)
for f,name in items:
    if sel and name not in sel: continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=d+f)
    sc=bpy.context.scene
    arms=[o for o in bpy.data.objects if o.type=='ARMATURE']
    for a in arms:
        for lado,sig in (('l',1),('r',1)):
            pb=a.pose.bones.get('upperarm_'+lado)
            if pb: pb.rotation_mode='XYZ'; pb.rotation_euler=(0,math.radians(62*sig),0)
    bpy.context.view_layer.update()
    meshes=[o for o in bpy.data.objects if o.type=='MESH']
    for o in meshes:
        for m in o.data.materials:
            nt=m.node_tree; b=nt.nodes.get('Principled BSDF')
            if not b: continue
            for l in list(b.inputs['Base Color'].links): nt.links.remove(l)
            for l in list(b.inputs['Normal'].links): nt.links.remove(l)
            if m.name.lower().startswith('lambert') or 'piel' in m.name.lower() or 'skin' in m.name.lower():
                b.inputs['Base Color'].default_value=PIEL
            b.inputs['Roughness'].default_value=1.0
            b.inputs['Specular IOR Level'].default_value=0.0
    # bounds (evaluated)
    dg=bpy.context.evaluated_depsgraph_get()
    pts=[]
    for o in meshes:
        eo=o.evaluated_get(dg); me=eo.to_mesh()
        pts+= [o.matrix_world@v.co for v in me.vertices]
        eo.to_mesh_clear()
    mn=mathutils.Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)))
    mx=mathutils.Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
    c=(mn+mx)/2; size=max(mx-mn)
    cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
    cam.data.type='ORTHO'; cam.data.ortho_scale=size*1.25
    # FBX: frente hacia -Y en Blender
    dirv=mathutils.Vector((0.55,-1.0,0.25)).normalized()
    cam.location=c+dirv*size*4
    cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler()
    sun=bpy.data.objects.new('sun',bpy.data.lights.new('sun','SUN')); sc.collection.objects.link(sun)
    sun.data.energy=2.2; sun.rotation_euler=(math.radians(50),math.radians(15),math.radians(30))
    w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=0.55
    w.node_tree.nodes['Background'].inputs[0].default_value=(1,1,1,1)
    sc.render.engine='CYCLES'; sc.cycles.samples=24; sc.cycles.device='CPU'
    sc.render.film_transparent=True; sc.render.resolution_x=sc.render.resolution_y=512
    sc.view_settings.view_transform='Standard'
    sc.render.use_freestyle=True; sc.render.line_thickness_mode='ABSOLUTE'; sc.render.line_thickness=2.6
    ls=sc.view_layers[0].freestyle_settings.linesets[0] if sc.view_layers[0].freestyle_settings.linesets else sc.view_layers[0].freestyle_settings.linesets.new('l')
    ls.linestyle = ls.linestyle or bpy.data.linestyles.new('ls')
    ls.linestyle.color=(0.16,0.1,0.07); ls.select_by_visibility=True; ls.select_silhouette=True; ls.select_border=True; ls.select_crease=False
    sc.render.filepath=os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../Assets/Art/UI/Wardrobe/'+name+'.png')
    bpy.ops.render.render(write_still=True)
    print('hecho',name)
