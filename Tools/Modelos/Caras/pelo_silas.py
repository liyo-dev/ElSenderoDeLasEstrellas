# Pelo de Silas joven (Hair22_Silas, 19 nov 2026, INC-1338): castaño cobrizo, algo despeinado y con volumen arriba, raya a su
# derecha y un flequillo que barre la frente hacia su izquierda y cae por la sien; tapa media oreja y por detrás llega a la
# nuca, sin coleta. Distinto de Eldran joven (pinchos oscuros, Hair13), del Archimago y de Liora (pelo recogido en coleta) y
# de Will (rubio de picos). En el recuerdo (blanco y negro) se lee como un tono medio, más claro que el de Eldran.
# Misma receta que pelo_liora.py y pelo_archimago.py (caras_lib): casquete liso a 4,5 cm con faldón hasta la cabeza, línea
# del pelo continua, mechones por encima del casquete (a más distancia) y con grosor.
#   args: modular.blend, salida.blend    Variables: NOMBRE_PELO, SOLO_PELO=1 (quita la cabeza para exportar)
import bpy, bmesh, math, sys, os
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
sys.argv = ["x", "--", a[0], "Head01_Male", "normal", "neutra", "/dev/null", "chicoL", "x", "gris"]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "caras_lib.txt")).read())
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
COBRE = mat("PeloCobre", (0.50, 0.27, 0.14, 1)); COBRE_O = mat("PeloCobreOscuro", (0.36, 0.18, 0.09, 1))
# 1) Casquete: frente con el nacimiento algo alto, media oreja tapada a los lados y por detrás hasta la nuca.
C_PELO = Vector((0, -0.02, 0.99))
def linea_pelo(w):
    ang = math.atan2(-(w.y + 0.013), max(abs(w.x), 0.001))
    base = 0.93 + (0.24 * math.sin(ang) ** 2 if ang > 0 else -0.20 * min(1.0, 2.0 * abs(math.sin(ang))))
    # el borde de los lados y de atrás acaba en puntas de mechón (no en una línea de casco); la frente queda limpia
    puntas = 0.085 * max(0.0, math.sin(7 * math.atan2(w.y + 0.013, w.x))) ** 2 * min(1.0, max(0.0, (w.y + 0.06) / 0.1))
    return base - puntas
def volumen(w):   # tupé arriba y algo más por detrás de la coronilla; sobre las orejas, para pasar por encima
    d = (w - C_PELO).normalized()
    arriba = 0.022 * max(0.0, d.z) ** 2 * (1.0 + 0.4 * max(0.0, d.y))
    orejas = 0.035 * max(math.exp(-(math.degrees(d.angle(Vector((s, 0.0, -0.05)).normalized())) / 24) ** 2) for s in (1, -1))
    # mechones gordos peinados hacia atrás: lomas que van de la frente a la nuca (despeinado, no un casco liso)
    rumbo = math.atan2(d.x, -d.y)
    lomas = 0.03 * max(0.0, math.cos(6 * rumbo)) ** 3 * max(0.0, d.z + 0.2)
    return arriba + orejas + lomas
cas = casquete_liso("Pelo", COBRE, C_PELO, linea_pelo, 0.045, volumen=volumen)
# 2) Raya a su derecha (-X): el flequillo grande barre hacia su izquierda y cae por la sien; uno corto al otro lado,
#    y una capa más oscura debajo del grande para darle volumen.
mech = [
    forma("Flequillo", [(-0.07, 1.225), (0.0, 1.215), (0.12, 1.18), (0.22, 1.11), (0.27, 1.02), (0.25, 0.95), (0.21, 1.03), (0.12, 1.1), (0.01, 1.14), (-0.06, 1.165)], COBRE, 26),
    forma("FlequilloSombra", [(0.01, 1.14), (0.12, 1.1), (0.21, 1.03), (0.245, 0.965), (0.19, 1.0), (0.11, 1.07), (0.0, 1.11)], COBRE_O, 25),
    forma("MechonCorto", [(-0.09, 1.215), (-0.18, 1.17), (-0.25, 1.09), (-0.255, 1.01), (-0.215, 1.07), (-0.14, 1.13), (-0.09, 1.165)], COBRE, 26),
]
con_grosor(mech)   # desde abajo se ve su revés: con grosor, se pinta
piezas = [cas] + mech
bpy.ops.object.select_all(action='DESELECT')
for o in piezas:
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
for o in piezas: o.select_set(True)
bpy.context.view_layer.objects.active = cas
bpy.ops.object.join(); pelo = bpy.context.object; pelo.name = os.environ.get("NOMBRE_PELO", "Hair22_Silas")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for p in pelo.data.polygons: p.use_smooth = True
vg = pelo.vertex_groups.new(name="head"); vg.add(list(range(len(pelo.data.vertices))), 1.0, 'REPLACE')
pelo.parent = arm; pelo.matrix_parent_inverse = arm.matrix_world.inverted()
pelo.modifiers.new("Esqueleto", 'ARMATURE').object = arm
if os.environ.get("SOLO_PELO") == "1": bpy.data.objects.remove(cabeza)
bpy.ops.wm.save_as_mainfile(filepath=a[1])
print("PELO", pelo.name, len(pelo.data.vertices), [m.name for m in pelo.data.materials])
