import bpy, bmesh, math
from mathutils import Vector
def limpiar():
    bpy.ops.wm.read_factory_settings(use_empty=True)
def exportar(obj, nombre):
    for o in bpy.context.scene.objects: o.select_set(False)
    obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.object.shade_smooth() if nombre in ('Bolita','Petalo') else bpy.ops.object.shade_flat()
    bpy.ops.export_scene.fbx(filepath=f'mallas/VFX_{nombre}.fbx', use_selection=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', bake_space_transform=True)
def estrella(n, nombre, interior=0.42, grosor=0.22):
    limpiar()
    bm=bmesh.new()
    pts=[]
    for i in range(2*n):
        a=math.pi/2+i*math.pi/n; r=0.5 if i%2==0 else 0.5*interior
        pts.append((r*math.cos(a), r*math.sin(a)))
    # cara delantera y trasera abombadas: centro adelantado
    cf=bm.verts.new((0,0,grosor)); cb=bm.verts.new((0,0,-grosor))
    ring=[bm.verts.new((x,y,0)) for x,y in pts]
    for i in range(len(ring)):
        a=ring[i]; b=ring[(i+1)%len(ring)]
        bm.faces.new((cf,a,b)); bm.faces.new((cb,b,a))
    me=bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    ob=bpy.data.objects.new(nombre,me); bpy.context.scene.collection.objects.link(ob)
    exportar(ob,nombre)
def bolita():
    limpiar()
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.5)
    ob=bpy.context.active_object; ob.name='Bolita'; exportar(ob,'Bolita')
def anillo():
    limpiar()
    bpy.ops.mesh.primitive_torus_add(major_radius=0.5, minor_radius=0.06, major_segments=40, minor_segments=8)
    ob=bpy.context.active_object; ob.name='Anillo'; exportar(ob,'Anillo')
def petalo():
    # pétalo curvo: alto 1, ancho 0.45, vuelto hacia dentro (eje -Y es el centro de la flor)
    limpiar()
    bm=bmesh.new(); filas=10; cols=6; grosor=0.035
    def pos(u,v):  # u 0..1 alto, v -1..1 ancho
        ancho=0.24*math.sin(math.pi*min(1,u*1.05))**0.8*(1-0.25*u)
        x=v*ancho; z=u*1.0
        y=-0.28*u*u - 0.08*(1-v*v)*math.sin(math.pi*u)   # se curva hacia el centro y hace cuchara
        return Vector((x,y,z))
    caras=[]
    for capa,off in ((0,grosor),(1,-grosor)):
        g=[[bm.verts.new(pos(i/filas, -1+2*j/cols)+Vector((0,off,0))) for j in range(cols+1)] for i in range(filas+1)]
        for i in range(filas):
            for j in range(cols):
                q=(g[i][j],g[i][j+1],g[i+1][j+1],g[i+1][j])
                bm.faces.new(q if capa==0 else q[::-1])
        caras.append(g)
    a,b=caras
    for i in range(filas):  # bordes
        for j in (0,cols):
            q=(a[i][j],a[i+1][j],b[i+1][j],b[i][j]); bm.faces.new(q if j==0 else q[::-1])
    for j in range(cols):
        bm.faces.new((a[0][j+1],a[0][j],b[0][j],b[0][j+1]))
        bm.faces.new((a[filas][j],a[filas][j+1],b[filas][j+1],b[filas][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me=bpy.data.meshes.new('Petalo'); bm.to_mesh(me); bm.free()
    ob=bpy.data.objects.new('Petalo',me); bpy.context.scene.collection.objects.link(ob)
    exportar(ob,'Petalo')
estrella(4,'Estrella4'); estrella(5,'Estrella5',0.45,0.2); bolita(); anillo(); petalo()
print('ok')
