# Pelo de Selene joven (Hair24_Selene, 19 nov 2026): largo, rubio claro (el tono más claro del recuerdo; ni blanco ni plata, que leen a mayor), raya al medio con dos mechones
# que enmarcan la cara hasta la mejilla, y una trenza que baja por la espalda por fuera de la capa. Distinto de Eldran joven
# (pinchos oscuros) y de Silas joven (castaño cobrizo, corto): en el recuerdo en blanco y negro es el pelo más claro de los tres.
# Receta de pelo_liora.py y pelo_archimago.py (caras_lib): casquete liso a 4,5 cm con faldón, mechones por encima del casquete y
# con grosor; la trenza son eslabones que se solapan. Después hay que pasarle coleta_a_la_espalda.py (la trenza sigue a la
# espalda, no solo a la cabeza: así no se mete en la capa).
#   args: modular.blend, salida.blend    Variables: NOMBRE_PELO, SOLO_PELO=1 (quita la cabeza para exportar)
import bpy, bmesh, math, sys, os
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
sys.argv = ["x", "--", a[0], "Head02_Female", "normal", "neutra", "/dev/null", "chicaL", "x", "gris"]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "caras_lib.txt")).read())
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
PLATA = mat("PeloRubio", (0.93, 0.83, 0.58, 1)); PLATA_O = mat("PeloRubioOscuro", (0.78, 0.64, 0.40, 1)); CINTA = mat("CintaNoche", (0.16, 0.19, 0.36, 1))
def tubo(nombre, puntos, radios, material, seg=12, aplastar=1.0):
    bm = bmesh.new(); pts = [Vector(p) for p in puntos]; filas = []; t_ant = None; ax = None
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if ax is None: ax = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0)); ax = (ax - t * ax.dot(t)).normalized()
        else: ax = (t_ant.rotation_difference(t) @ ax); ax = (ax - t * ax.dot(t)).normalized()
        t_ant = t; b = t.cross(ax)
        filas.append([bm.verts.new(p + (ax * math.cos(2 * math.pi * k / seg) + b * aplastar * math.sin(2 * math.pi * k / seg)) * radios[i]) for k in range(seg)])
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
# 1) Casquete: frente despejada en la raya, orejas tapadas, por detrás hasta la nuca.
C_PELO = Vector((0, -0.02, 0.99))
def linea_pelo(w):
    ang = math.atan2(-(w.y + 0.013), max(abs(w.x), 0.001))
    return 0.90 + (0.26 * math.sin(ang) ** 2 if ang > 0 else -0.19 * min(1.0, 2.0 * abs(math.sin(ang))))
def volumen(w):
    d = (w - C_PELO).normalized()
    orejas = 0.035 * max(math.exp(-(math.degrees(d.angle(Vector((s, 0.0, -0.05)).normalized())) / 24) ** 2) for s in (1, -1))
    lomas = 0.028 * max(0.0, math.cos(6 * math.atan2(d.x, -d.y))) ** 3 * max(0.0, d.z + 0.2)   # mechones peinados hacia atrás
    return orejas + lomas + 0.01 * max(0.0, d.y)
cas = casquete_liso("Pelo", PLATA, C_PELO, linea_pelo, 0.045, volumen=volumen)
# 2) Raya al medio: dos mechones que bajan enmarcando la cara hasta la mejilla, con una capa más oscura debajo.
mech = []
for s in (1, -1):
    mech.append(forma(f"Mechon_{s}", [(0.004 * s, 1.215), (0.1 * s, 1.2), (0.2 * s, 1.14), (0.27 * s, 1.04), (0.29 * s, 0.94), (0.275 * s, 0.86), (0.235 * s, 0.9), (0.215 * s, 1.0), (0.16 * s, 1.1), (0.04 * s, 1.165)], PLATA, 24))
    mech.append(forma(f"MechonSombra_{s}", [(0.04 * s, 1.165), (0.16 * s, 1.1), (0.215 * s, 1.0), (0.235 * s, 0.9), (0.2 * s, 0.94), (0.14 * s, 1.06), (0.03 * s, 1.13)], PLATA_O, 23))
con_grosor(mech)
# 3) Trenza: nace en la nuca con una cinta azul noche y baja por la espalda por fuera de la capa (la capa, a ~0,22 de Y).
eje = [Vector(p) for p in [(0, 0.27, 0.93), (0, 0.29, 0.86), (0, 0.285, 0.79), (0, 0.275, 0.72), (0, 0.268, 0.65), (0, 0.262, 0.59)]]
def en_eje(u):
    x = u * (len(eje) - 1); i = min(int(x), len(eje) - 2); f = x - i
    return eje[i] * (1 - f) + eje[i + 1] * f
trenza = []
N = 8
for k in range(N):   # eslabones alternos a un lado y a otro, solapados
    u0, u1 = k / N, (k + 1.25) / N
    p0, p1 = en_eje(min(u0, 1)), en_eje(min(u1, 1)); lado = 0.012 * (1 if k % 2 else -1)
    r = 0.042 * (1 - 0.45 * k / N)
    pts = [p0 + Vector((lado, 0, 0)), (p0 + p1) / 2 + Vector((-lado * 0.3, 0.004, 0)), p1 + Vector((-lado, 0, 0))]
    trenza.append(tubo(f"Trenza{k}", [tuple(p) for p in pts], [r * 0.7, r, r * 0.6], PLATA if k % 2 else PLATA_O, 12, aplastar=0.75))
punta = en_eje(1.0)
trenza.append(tubo("PuntaTrenza", [tuple(punta + Vector((0, 0, 0.01))), tuple(punta + Vector((0, 0.004, -0.04))), tuple(punta + Vector((0, 0.006, -0.065)))], [0.02, 0.024, 0.006], PLATA, 10))
trenza.append(tubo("CintaTrenza", [tuple(punta + Vector((0, 0.0, 0.016))), tuple(punta + Vector((0, 0.0, -0.002)))], [0.03, 0.03], CINTA, 14))
trenza.append(tubo("CintaNuca", [(0, 0.26, 0.945), (0, 0.272, 0.92)], [0.05, 0.05], CINTA, 14))
piezas = [cas] + mech + trenza
bpy.ops.object.select_all(action='DESELECT')
for o in piezas:
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
for o in piezas: o.select_set(True)
bpy.context.view_layer.objects.active = cas
bpy.ops.object.join(); pelo = bpy.context.object; pelo.name = os.environ.get("NOMBRE_PELO", "Hair24_Selene")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for p in pelo.data.polygons: p.use_smooth = True
vg = pelo.vertex_groups.new(name="head"); vg.add(list(range(len(pelo.data.vertices))), 1.0, 'REPLACE')
pelo.parent = arm; pelo.matrix_parent_inverse = arm.matrix_world.inverted()
pelo.modifiers.new("Esqueleto", 'ARMATURE').object = arm
if os.environ.get("SOLO_PELO") == "1": bpy.data.objects.remove(cabeza)
bpy.ops.wm.save_as_mainfile(filepath=a[1])
print("PELO", pelo.name, len(pelo.data.vertices), [m.name for m in pelo.data.materials])
