# Pelo y perilla del Archimago (Hair20_Archimago): pelo largo plateado recogido con un aro de oro, raya al medio con dos mechones que
# barren hacia las sienes, y una perilla corta bajo la boca. Usa caras_lib (forma, cabeza) para pegar los mechones a la cabeza.
#   args: modular.blend, salida.blend    Variables: NOMBRE_PELO, SOLO_PELO=1 (quita la cabeza para exportar)
import bpy, bmesh, math, sys, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index("--") + 1:]
sys.argv = ["x", "--", a[0], "Head01_Male", "normal", "neutra", "/dev/null", "chicoL", "x", "gris"]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "caras_lib.txt")).read())
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
PLATA = mat("PeloPlata", (0.74, 0.74, 0.80, 1)); PLATA_O = mat("PeloPlataOscuro", (0.55, 0.55, 0.62, 1)); ORO = mat("AroOro", (0.88, 0.67, 0.20, 1))
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
# 1) Pelo de arriba y de atrás: frente despejada (1,17 en el centro), sienes y patillas cubiertas hasta media oreja (0,94 a
#    los lados; la oreja queda bajo el pelo con un bulto suave) y, por detrás, baja deprisa a la nuca (0,71).
#    Casquete liso: el borde no va en escalera por las caras de la cabeza.
C_PELO = Vector((0, -0.02, 0.99))
def linea_pelo(w):
    ang = math.atan2(-(w.y + 0.013), max(abs(w.x), 0.001))
    return 0.94 + (0.23 * math.sin(ang) ** 2 if ang > 0 else -0.23 * min(1.0, 2.0 * abs(math.sin(ang))))
def sobre_las_orejas(w):   # más volumen sobre cada oreja, para que el pelo pase por encima sin que asome
    d = (w - C_PELO).normalized()
    return 0.035 * max(math.exp(-(math.degrees(d.angle(Vector((s, 0.0, -0.05)).normalized())) / 24) ** 2) for s in (1, -1))
cas = casquete_liso("Pelo", PLATA, C_PELO, linea_pelo, 0.045, volumen=sobre_las_orejas)
# 2) Raya al medio y dos mechones que barren hacia las sienes, con una segunda capa más oscura debajo para dar volumen. Van por
#    encima del casquete (a 5,2 y 5,0 cm de la cabeza; el casquete, a 4,5): si quedan a la par se pelean con él y parpadean.
mech = []
for s in (1, -1):
    mech.append(forma(f"Mechon_{s}", [(0.004 * s, 1.215), (0.09 * s, 1.205), (0.19 * s, 1.15), (0.265 * s, 1.07), (0.255 * s, 1.0), (0.215 * s, 1.07), (0.13 * s, 1.14), (0.03 * s, 1.17)], PLATA, 24))
    mech.append(forma(f"MechonSombra_{s}", [(0.03 * s, 1.17), (0.13 * s, 1.14), (0.215 * s, 1.07), (0.25 * s, 1.0), (0.2 * s, 1.03), (0.12 * s, 1.1), (0.02 * s, 1.14)], PLATA_O, 23))
con_grosor(mech)   # desde abajo se ve su revés: con grosor, se pinta
# 3) Coleta baja a la espalda, atada con un aro de oro.
coleta = tubo("Coleta", [(0, 0.29, 0.98), (0, 0.325, 0.9), (0, 0.342, 0.8), (0, 0.345, 0.68), (0, 0.343, 0.56), (0, 0.338, 0.46)], [0.045, 0.052, 0.054, 0.05, 0.04, 0.012], PLATA, 12)
nudo = tubo("AroOro", [(0, 0.31, 0.93), (0, 0.322, 0.91)], [0.066, 0.066], ORO, 14)
# 4) Perilla: un copete bajo la boca (la boca queda libre), pegado a la barbilla y con grosor.
perilla = forma("Perilla", [(-0.042, 0.768), (0.042, 0.768), (0.037, 0.74), (0.022, 0.72), (0.0, 0.712), (-0.022, 0.72), (-0.037, 0.74)], PLATA_O, -1)
# Pegada a la barbilla de verdad: la proyección de frente la dejaba separada donde la barbilla se curva hacia abajo
# (de perfil flotaba en el aire). Cada vértice va al punto más cercano de la piel, a 2 mm, y la pieza lleva grosor
# hacia dentro y hacia fuera, así que no deja hueco.
bpy.context.view_layer.objects.active = perilla
for m in list(perilla.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
_bmc = bmesh.new(); _bmc.from_mesh(cabeza.data); _bmc.transform(cabeza.matrix_world); _piel = BVHTree.FromBMesh(_bmc); _bmc.free()
for v in perilla.data.vertices:
    w = perilla.matrix_world @ v.co; cerca = _piel.find_nearest(w)
    if cerca[0] is not None: v.co = perilla.matrix_world.inverted() @ (cerca[0] + cerca[1] * 0.002)
grosor = perilla.modifiers.new("Grosor", 'SOLIDIFY'); grosor.thickness = 0.014; grosor.offset = 0.0
piezas = [cas, coleta, nudo, perilla] + mech
bpy.ops.object.select_all(action='DESELECT')
for o in piezas:
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
for o in piezas: o.select_set(True)
bpy.context.view_layer.objects.active = cas
bpy.ops.object.join(); pelo = bpy.context.object; pelo.name = os.environ.get("NOMBRE_PELO", "Hair20")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for p in pelo.data.polygons: p.use_smooth = True
vg = pelo.vertex_groups.new(name="head"); vg.add(list(range(len(pelo.data.vertices))), 1.0, 'REPLACE')
pelo.parent = arm; pelo.matrix_parent_inverse = arm.matrix_world.inverted()
pelo.modifiers.new("Esqueleto", 'ARMATURE').object = arm
if os.environ.get("SOLO_PELO") == "1": bpy.data.objects.remove(cabeza)
bpy.ops.wm.save_as_mainfile(filepath=a[1])
print("PELO", pelo.name, len(pelo.data.vertices), [m.name for m in pelo.data.materials])
