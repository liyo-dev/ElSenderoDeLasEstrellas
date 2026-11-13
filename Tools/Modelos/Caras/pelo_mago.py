# Pelo del Mago Oscuro (Hair19_MagoOscuro): casquete negro sobre la cabeza del pack, con flequillo que cae en dos picos sobre la frente
# y un mechón gris en uno de ellos. Asoma bajo la capucha. Usa caras_lib (forma, cabeza) para pegar el flequillo a la cabeza.
#   args: modular.blend, salida.blend
import bpy, bmesh, math, sys, os
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
sys.argv = ["x", "--", a[0], "Head01_Male", "normal", "neutra", "/dev/null", "chicoL", "mantener" if len(a) > 2 and a[2] == "mantener" else "x", "violeta"]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "caras_lib.txt")).read())
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
PELO = mat("PeloMago", (0.04, 0.035, 0.06, 1)); GRIS = mat("PeloGris", (0.62, 0.6, 0.7, 1))
mw = cabeza.matrix_world
# 1) Casquete: las caras de la cabeza por encima de una línea de nacimiento del pelo, separadas 1 cm y con el borde cerrado.
bm = bmesh.new(); bm.from_mesh(cabeza.data)
def linea_pelo(w):   # altura mínima del pelo: baja a la frente por delante, a las patillas por los lados y a la nuca por detrás
    ang = math.atan2(-(w.y + 0.013), max(abs(w.x), 0.001))
    return 0.97 + (0.13 * math.sin(ang) ** 2.5 if ang > 0 else 0.12 * math.sin(ang))
quitar = [f for f in bm.faces if (mw @ f.calc_center_median()).z < linea_pelo(mw @ f.calc_center_median())]
bmesh.ops.delete(bm, geom=quitar, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
hinchar_casquete(bm, mw, 0.010)
me = bpy.data.meshes.new("Casquete"); bm.to_mesh(me); bm.free()
cas = bpy.data.objects.new("Casquete", me); bpy.context.scene.collection.objects.link(cas); cas.matrix_world = mw
cas.data.materials.append(PELO)
for p in me.polygons: p.use_smooth = True
# 2) Flequillo: tres mechones negros (dos picos largos sobre la frente) y un mechón gris en el derecho.
def mechon(x0, x1, xp, zp, alto=1.17, curva=0.0):
    return [(x0, alto), (x1, alto), (x1 + (xp - x1) * 0.55 + curva, (alto + zp) / 2 + 0.01), (xp, zp), (x0 + (xp - x0) * 0.5, (alto + zp) / 2 - 0.0)]
bang = []
bang.append(forma("MechonI", mechon(-0.115, 0.005, -0.050, 1.035), PELO, 6))     # pico izquierdo (en la cara: a la derecha del que mira)
bang.append(forma("MechonD", mechon(0.005, 0.135, 0.080, 1.025), PELO, 6))       # pico derecho
bang.append(forma("FlecoI", mechon(-0.235, -0.10, -0.19, 1.07, curva=-0.01), PELO, 6))
bang.append(forma("FlecoD", mechon(0.12, 0.235, 0.20, 1.075, curva=0.01), PELO, 6))
bang.append(forma("MechonGris", [(0.060, 1.17), (0.100, 1.17), (0.098, 1.10), (0.090, 1.015), (0.076, 1.10)], GRIS, 8))
# 3) Aplicar, unir y pesar a la cabeza.
bpy.ops.object.select_all(action='DESELECT')
piezas = [cas] + bang
for o in piezas:
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
for o in piezas:
    o.select_set(True)
bpy.context.view_layer.objects.active = cas
bpy.ops.object.join(); pelo = bpy.context.object; pelo.name = os.environ.get("NOMBRE_PELO", "Hair19")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for p in pelo.data.polygons: p.use_smooth = True
vg = pelo.vertex_groups.new(name="head"); vg.add(list(range(len(pelo.data.vertices))), 1.0, 'REPLACE')
pelo.parent = arm; pelo.matrix_parent_inverse = arm.matrix_world.inverted()
pelo.modifiers.new("Esqueleto", 'ARMATURE').object = arm
if os.environ.get("SOLO_PELO") == "1": bpy.data.objects.remove(cabeza)   # para exportar solo el pelo
bpy.ops.wm.save_as_mainfile(filepath=a[1])
print("PELO", pelo.name, len(pelo.data.vertices), [m.name for m in pelo.data.materials])
