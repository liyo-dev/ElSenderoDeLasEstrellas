# Mete bajo un gorro o capucha lo que del pelo sale por fuera (19 nov 2026, Liam: «sigo viendo un pincho salir por
# encima del gorro»). Mirando desde el centro de la cabeza hacia cada punto del pelo: si el gorro está antes que el
# punto, el punto se acerca hasta quedar a MARGEN por dentro de la cara de dentro del gorro. Solo se mueven esos
# puntos (los pelillos de punta), el resto del pelo no cambia. Lo mismo hace `chequeo_choques.py` para detectarlo
# («Pelo que sale por fuera del gorro o sombrero»).
#   Uso: python pelo_bajo_el_gorro.py -- pelo.fbx gorro.fbx salida.fbx [margen=0.012] [centro=0,-0.013,0.99]
import bpy, sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index("--") + 1:]
PELO, GORRO, SALIDA = a[0], a[1], a[2]
MARGEN = float(a[3]) if len(a) > 3 else 0.012
CENTRO = Vector(map(float, a[4].split(","))) if len(a) > 4 else Vector((0, -0.013, 0.99))
bpy.ops.wm.read_factory_settings(use_empty=True)
def importar(ruta):
    antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=ruta)
    return [o for o in bpy.data.objects if o not in antes]
nuevos = importar(PELO)
arm = next(o for o in nuevos if o.type == 'ARMATURE'); pelo = next(o for o in nuevos if o.type == 'MESH')
g = [o for o in importar(GORRO) if o.type == 'MESH'][0]
dg = bpy.context.evaluated_depsgraph_get(); ge = g.evaluated_get(dg); me = ge.to_mesh()
vs = [g.matrix_world @ v.co for v in me.vertices]; fs = [list(p.vertices) for p in me.polygons]
gorro = BVHTree.FromPolygons(vs, fs); ge.to_mesh_clear()
mw = pelo.matrix_world; inv = mw.inverted(); n = 0
for v in pelo.data.vertices:
    p = mw @ v.co; d = p - CENTRO; L = d.length
    if L < 1e-6: continue
    t = d / L
    hit = gorro.ray_cast(CENTRO, t, L + 0.5)
    if hit[0] is None: continue
    # el primer choque es la cara de dentro del gorro (el gorro tiene grosor: la de fuera queda más lejos)
    tope = hit[3] - MARGEN
    if L > tope: v.co = inv @ (CENTRO + t * tope); n += 1
print("PELO METIDO BAJO EL GORRO", n, "puntos")
for o in bpy.data.objects:
    if o not in (arm, pelo): bpy.data.objects.remove(o)
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); pelo.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=SALIDA, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
    bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=False, axis_forward='-Z', axis_up='Y')
