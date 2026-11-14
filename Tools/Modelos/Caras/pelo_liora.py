# Pelo de Liora (Hair21_Liora): castaño cálido, raya a su izquierda con un mechón que barre la frente hacia la derecha y otro
# más corto al otro lado, orejas tapadas, y coleta baja a la espalda atada con una cinta crema (la de su cuello).
# Distinto de Victoria (moño rosa) y de Estela (coleta alta naranja). Misma receta que pelo_archimago.py (caras_lib):
# casquete liso a 4,5 cm, mechones con grosor (con_grosor) y coleta pegada a la espalda.
#   args: modular.blend, salida.blend    Variables: NOMBRE_PELO, SOLO_PELO=1 (quita la cabeza para exportar)
import bpy, bmesh, math, sys, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index("--") + 1:]
sys.argv = ["x", "--", a[0], "Head02_Female", "normal", "neutra", "/dev/null", "chicoL", "x", "gris"]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "caras_lib.txt")).read())
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
CASTANO = mat("PeloCastano", (0.42, 0.24, 0.13, 1)); CASTANO_O = mat("PeloCastanoOscuro", (0.30, 0.16, 0.08, 1)); CINTA = mat("CintaCrema", (0.96, 0.92, 0.80, 1))
mw = cabeza.matrix_world
def tubo(nombre, puntos, radios, material, seg=12):
    bm = bmesh.new(); pts = [Vector(p) for p in puntos]; filas = []; t_ant = None; ax = None
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if ax is None: ax = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0)); ax = (ax - t * ax.dot(t)).normalized()
        else: ax = (t_ant.rotation_difference(t) @ ax); ax = (ax - t * ax.dot(t)).normalized()
        t_ant = t; b = t.cross(ax)
        filas.append([bm.verts.new(p + (ax * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * radios[i]) for k in range(seg)])
    for f in range(len(filas) - 1):
        for k in range(seg): bm.faces.new((filas[f][k], filas[f][(k + 1) % seg], filas[f + 1][(k + 1) % seg], filas[f + 1][k]))
    for fila, inv in ((filas[0], True), (filas[-1], False)):
        c = bm.verts.new(sum((v.co for v in fila), Vector()) / len(fila))
        for k in range(seg):
            tri = (c, fila[k], fila[(k + 1) % seg]); bm.faces.new(tri if not inv else tri[::-1])
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o); o.data.materials.append(material)
    for p in me.polygons: p.use_smooth = True
    return o
def curva(p0, p1, p2, n):
    return [tuple((1 - u) ** 2 * Vector(p0) + 2 * (1 - u) * u * Vector(p1) + u * u * Vector(p2)) for u in (i / (n - 1) for i in range(n))]
# 1) Casquete: frente despejada, orejas tapadas, y por detrás baja a la nuca.
C_PELO = Vector((0, -0.02, 0.99))
def linea_pelo(w):
    ang = math.atan2(-(w.y + 0.013), max(abs(w.x), 0.001))
    return 0.92 + (0.24 * math.sin(ang) ** 2 if ang > 0 else -0.21 * min(1.0, 2.0 * abs(math.sin(ang))))
def sobre_las_orejas(w):
    d = (w - C_PELO).normalized()
    return 0.035 * max(math.exp(-(math.degrees(d.angle(Vector((s, 0.0, -0.05)).normalized())) / 24) ** 2) for s in (1, -1))
cas = casquete_liso("Pelo", CASTANO, C_PELO, linea_pelo, 0.045, volumen=sobre_las_orejas)
# 2) Raya a su izquierda (+X): el mechón grande barre la frente hacia su derecha y cae por la sien; el corto, al otro lado.
mech = [
    forma("MechonGrande", [(0.07, 1.215), (0.0, 1.205), (-0.12, 1.17), (-0.22, 1.10), (-0.27, 1.0), (-0.255, 0.93), (-0.215, 1.02), (-0.13, 1.10), (0.0, 1.15), (0.06, 1.17)], CASTANO, 24),
    forma("MechonGrandeSombra", [(0.0, 1.15), (-0.13, 1.10), (-0.215, 1.02), (-0.25, 0.95), (-0.2, 0.99), (-0.12, 1.06), (-0.01, 1.11)], CASTANO_O, 23),
    forma("MechonCorto", [(0.09, 1.215), (0.19, 1.16), (0.26, 1.07), (0.255, 1.0), (0.215, 1.06), (0.14, 1.13), (0.09, 1.17)], CASTANO, 24),
]
con_grosor(mech)   # desde abajo se ve su revés: con grosor, se pinta
# 3) Coleta baja a la espalda, atada con una cinta crema con dos cabos.
# pegada a la espalda: sin capa detrás, si cuelga recta se separa del cuerpo y parece suelta
coleta = tubo("Coleta", [(0, 0.29, 0.98), (0, 0.312, 0.9), (0, 0.302, 0.8), (0, 0.272, 0.7), (0, 0.245, 0.62), (0, 0.232, 0.56)], [0.05, 0.058, 0.058, 0.05, 0.038, 0.012], CASTANO, 12)
nudo = tubo("Cinta", [(0, 0.31, 0.93), (0, 0.322, 0.905)], [0.07, 0.07], CINTA, 14)
cabos = [tubo(f"CaboCinta{s}", curva((0.02 * s, 0.37, 0.92), (0.05 * s, 0.39, 0.86), (0.045 * s, 0.38, 0.80), 6), [0.013, 0.013, 0.012, 0.011, 0.01, 0.008], CINTA, 8) for s in (1, -1)]
piezas = [cas, coleta, nudo] + cabos + mech
bpy.ops.object.select_all(action='DESELECT')
for o in piezas:
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
for o in piezas: o.select_set(True)
bpy.context.view_layer.objects.active = cas
bpy.ops.object.join(); pelo = bpy.context.object; pelo.name = os.environ.get("NOMBRE_PELO", "Hair21_Liora")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for p in pelo.data.polygons: p.use_smooth = True
vg = pelo.vertex_groups.new(name="head"); vg.add(list(range(len(pelo.data.vertices))), 1.0, 'REPLACE')
pelo.parent = arm; pelo.matrix_parent_inverse = arm.matrix_world.inverted()
pelo.modifiers.new("Esqueleto", 'ARMATURE').object = arm
if os.environ.get("SOLO_PELO") == "1": bpy.data.objects.remove(cabeza)
bpy.ops.wm.save_as_mainfile(filepath=a[1])
print("PELO", pelo.name, len(pelo.data.vertices), [m.name for m in pelo.data.materials])
