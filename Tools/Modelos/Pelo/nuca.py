# La nuca del pelo. Los pelos hechos aquí acaban en la base del cráneo (más abajo se metían en el cuello de
# la ropa y, al girar la cabeza, la punta parecía un trozo suelto), y entre ese borde y la ropa quedaba piel
# a la vista: de lado y de espaldas parecía una calva detrás de la oreja. Esto le añade al pelo una capa
# pegada a la cabeza (la propia superficie de la cabeza, separada unos milímetros) por detrás de las orejas
# y hasta abajo: gira con la cabeza, no sobresale y lo que queda bajo el pelo de verdad no se ve.
#   args: blend, nombre del pelo, cabeza (nombre de un objeto del blend, o un FBX de cabeza suelta del pack), salida
#   Entorno: NUCA_SEPARACION (0.008), NUCA_DESDE (0.1: cuánto por detrás de las orejas empieza, 0 = en ellas),
#            NUCA_ALTO (0.06: hasta qué altura sobre el centro de la cabeza sube),
#            NUCA_CAIDA (0.22: cuánto se va hacia atrás la línea del pelo al bajar de la oreja).
import bpy, bmesh, sys, os
from mathutils import Vector, Matrix
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
pelo = bpy.data.objects[a[1]]
SEP = float(os.environ.get("NUCA_SEPARACION", "0.008")); DESDE = float(os.environ.get("NUCA_DESDE", "0.1")); ALTO = float(os.environ.get("NUCA_ALTO", "0.06")); CAIDA = float(os.environ.get("NUCA_CAIDA", "0.22"))
if a[2] in bpy.data.objects: cab = bpy.data.objects[a[2]]; bm = bmesh.new(); bm.from_mesh(cab.data); bm.transform(cab.matrix_world); prestada = None
else:
    antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=a[2]); bpy.context.view_layer.update()
    nuevos = [o for o in bpy.data.objects if o not in antes]; cab = [o for o in nuevos if o.type == 'MESH'][0]
    bm = bmesh.new(); bm.from_mesh(cab.data); bm.transform(cab.matrix_world)
    bm.transform(Matrix.Translation((0, -0.0129, 0.914)))   # las piezas sueltas de cabeza vienen sin el sitio del esqueleto
    prestada = nuevos
bm.normal_update()
xs = [v.co for v in bm.verts]
C = Vector(((min(p.x for p in xs) + max(p.x for p in xs)) / 2, (min(p.y for p in xs) + max(p.y for p in xs)) / 2, (min(p.z for p in xs) + max(p.z for p in xs)) / 2))
ANCHO = max(abs(p.x - C.x) for p in xs)
quitar = []
for f in bm.faces:
    c = f.calc_center_median(); t = (c - C).normalized()
    oreja = any(abs(v.co.x - C.x) > ANCHO * 0.93 for v in f.verts)
    # la línea del pelo no baja recta: por debajo de la oreja se va hacia atrás, como en una nuca de verdad
    borde = DESDE + CAIDA * max(0.0, (C.z - 0.05 - c.z) / 0.25)
    if not (t.y > borde and c.z < C.z + ALTO and not oreja): quitar.append(f)
bmesh.ops.delete(bm, geom=quitar, context='FACES')
for v in bm.verts: v.co += v.normal * SEP
me = bpy.data.meshes.new("Nuca"); bm.to_mesh(me); n = len(bm.verts); bm.free()
if prestada:
    for o in prestada: bpy.data.objects.remove(o)
capa = bpy.data.objects.new("Nuca", me); bpy.context.scene.collection.objects.link(capa)
capa.data.materials.append(pelo.data.materials[0])
for p in capa.data.polygons: p.use_smooth = True
inv = pelo.matrix_world.inverted(); capa.data.transform(inv); capa.matrix_world = pelo.matrix_world.copy(); capa.parent = pelo.parent; capa.matrix_parent_inverse = pelo.matrix_parent_inverse.copy()
vg = capa.vertex_groups.new(name="head"); vg.add(list(range(n)), 1.0, 'REPLACE')
bpy.ops.object.select_all(action='DESELECT'); capa.select_set(True); pelo.select_set(True); bpy.context.view_layer.objects.active = pelo
bpy.ops.object.join(); bpy.context.object.name = a[1]
bpy.ops.wm.save_as_mainfile(filepath=a[3]); print("NUCA", a[1], n, "vértices añadidos; centro de la cabeza", tuple(round(x, 3) for x in C))
