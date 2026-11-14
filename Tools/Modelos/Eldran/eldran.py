# Eldran, vestuario propio: abrigo largo de mago viejo, verde musgo apagado, con coderas de cuero y
# bufanda gris con un cabo a la espalda; cinturón, zurrón cruzado con libros, pantalón y botas. La cabeza, la barba, el pelo y los
# ojos siguen siendo los suyos (Head05_Santa, Hair13). Propuesta aprobada por Raúl el 11 nov 2026.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body26_AbrigoDeMago → las manos y el torso de Body05 con la ropa encima, pesado como él.
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Euler
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Prendas"))
import ropa_lib as rl, taller as tl
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Abrigo": (0.25, 0.33, 0.19, 1), "AbrigoOscuro": (0.15, 0.21, 0.12, 1), "Forro": (0.55, 0.47, 0.30, 1),
    "Bufanda": (0.62, 0.62, 0.64, 1), "BufandaOscura": (0.45, 0.45, 0.48, 1),
    "Pantalon": (0.22, 0.18, 0.15, 1), "Cuero": (0.32, 0.19, 0.10, 1), "CueroOscuro": (0.2, 0.12, 0.07, 1),
    "Laton": (0.80, 0.62, 0.25, 1), "LibroRojo": (0.55, 0.13, 0.12, 1), "LibroAzul": (0.16, 0.25, 0.45, 1), "Papel": (0.93, 0.89, 0.78, 1),
}







def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]


T = tl.Taller(COLORES, grupos=('traje',))   # funciones de modelado comunes (../Prendas/taller.py)
MAT, GRUPOS = T.mat, T.grupos
nueva, transformar, esfera, toro, torno, tubo, caja = T.nueva, T.transformar, T.esfera, T.toro, T.torno, T.tubo, T.caja

# ── Abrigo: largo hasta la rodilla y con vuelo, para que las piernas se muevan dentro sin estirarlo ──
torno("traje", "Abrigo", [(0.725, 0.15), (0.67, 0.2), (0.58, 0.212), (0.5, 0.203), (0.45, 0.203), (0.36, 0.228), (0.27, 0.25), (0.19, 0.268)],
      "Abrigo", grosor=0.009)
toro("traje", "Bajo", (0, -0.01, 0.192), 0.266, 0.011, "AbrigoOscuro", None, escala=(1, 0.92, 1))
# Solapas: el borde del abrigo baja por delante, del cuello al bajo, y tres botones de latón
tubo("traje", "Solapa", curva((0.04, -0.165, 0.72), (0.03, -0.225, 0.5), (0.03, -0.262, 0.2), 10), [0.013] * 10, "AbrigoOscuro", None)
for z, y in ((0.62, -0.212), (0.54, -0.21), (0.38, -0.225)):
    esfera("traje", f"Boton{int(z*100)}", (0.075, y, z), (0.017, 0.008, 0.017), "Laton", None)
# Cuello alto y vuelto
torno("traje", "Cuello", [(0.77, 0.165), (0.745, 0.2), (0.71, 0.218)], "AbrigoOscuro", seg=40, grosor=0.014)
# Cinturón de cuero con hebilla, por fuera del abrigo
toro("traje", "Cinturon", (0, -0.01, 0.45), 0.208, 0.019, "CueroOscuro", None, escala=(1, 0.92, 1))
esfera("traje", "Hebilla", (0.0, -0.208, 0.45), (0.03, 0.013, 0.027), "Laton", None)
# Bufanda gris: una vuelta gorda al cuello y un cabo echado a la espalda. El cabo de delante se quitó (11 nov):
# salía por debajo de la barba de Head05_Santa y la atravesaba.
toro("traje", "Bufanda", (0, -0.012, 0.722), 0.185, 0.058, "Bufanda", {"spine_03": 1.0}, escala=(1, 0.98, 0.75))
tubo("traje", "CaboEspalda", curva((0.1, 0.17, 0.72), (0.14, 0.25, 0.66), (0.13, 0.235, 0.54), 7), [0.056, 0.054, 0.05], "Bufanda", {"spine_03": 0.6, "spine_02": 0.4}, seg=10)
# Zurrón cruzado: la correa del hombro derecho a la cadera izquierda, y la bolsa con dos libros asomando
correa = curva((-0.15, -0.09, 0.7), (-0.02, -0.37, 0.6), (0.22, -0.13, 0.44), 12)
tubo("traje", "Correa", correa, [0.017] * 12, "Cuero", None, seg=8)
correa_atras = curva((-0.15, 0.06, 0.7), (-0.02, 0.35, 0.6), (0.22, 0.1, 0.44), 12)
tubo("traje", "CorreaAtras", correa_atras, [0.017] * 12, "Cuero", None, seg=8)
P = {"pelvis": 1.0}
caja("traje", "Zurron", (0.255, -0.015, 0.36), (0.085, 0.2, 0.17), "Cuero", P, rot=(0, math.radians(-6), 0))
caja("traje", "Tapa", (0.302, -0.015, 0.405), (0.014, 0.21, 0.1), "CueroOscuro", P, rot=(0, math.radians(-6), 0))
esfera("traje", "Cierre", (0.312, -0.015, 0.365), (0.008, 0.02, 0.02), "Laton", P)
caja("traje", "LibroRojo", (0.245, -0.055, 0.465), (0.05, 0.075, 0.1), "LibroRojo", P, rot=(math.radians(8), 0, 0))
caja("traje", "HojasRojo", (0.245, -0.055, 0.468), (0.04, 0.066, 0.1), "Papel", P, rot=(math.radians(8), 0, 0))
caja("traje", "LibroAzul", (0.25, 0.04, 0.455), (0.045, 0.08, 0.1), "LibroAzul", P, rot=(math.radians(-10), 0, 0))
caja("traje", "HojasAzul", (0.25, 0.04, 0.458), (0.036, 0.07, 0.1), "Papel", P, rot=(math.radians(-10), 0, 0))
for lado, s in (("l", 1), ("r", -1)):
    # manga de abrigo, ancha, con el hombro redondo, la codera de cuero por detrás y la vuelta del puño
    tubo("traje", f"Manga_{lado}", [(0.17 * s, -0.003, 0.636), (0.26 * s, -0.003, 0.635), (0.35 * s, -0.003, 0.634), (0.44 * s, -0.003, 0.633)],
         [0.086, 0.08, 0.076, 0.078], "Abrigo", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.175 * s, -0.003, 0.64), (0.09, 0.086, 0.086), "Abrigo", None)
    esfera("traje", f"Codera_{lado}", (0.305 * s, 0.05, 0.632), (0.055, 0.04, 0.062), "Cuero", {f"upperarm_{lado}": 0.5, f"lowerarm_{lado}": 0.5})
    toro("traje", f"Puno_{lado}", (0.44 * s, -0.003, 0.633), 0.078, 0.017, "AbrigoOscuro", {f"lowerarm_{lado}": 1.0}, rot=(0, math.radians(90), 0))
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.22), (0.116 * s, 0.004, 0.11)],
         [0.092, 0.086, 0.078, 0.074], "Pantalon", None, cerrar=False)
    bota = [(0.0, 0.082), (0.04, 0.087), (0.1, 0.074), (0.13, 0.076), (0.15, 0.084)]
    o = torno("traje", f"Bota_{lado}", bota, "CueroOscuro", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "CueroOscuro", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})

# ── Pesos y unión ─────────────────────────────────────────────────────────────────────────
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
rl.pesar_piezas(rl.completar_zonas(GRUPOS["traje"]), cuerpo, arm)
tl.revisar_antes_de_exportar([x for g in GRUPOS.values() for x in g], cuerpo, arm)
# Lo del cuerpo de debajo que queda tapado por el pantalón, las botas y las mangas se quita (después de
# copiar los pesos: la ropa los toma del cuerpo entero)
# Los cortes se tapan con «Pantalon»: es lo que se ve desde abajo por el bajo de la ropa (ver chequeo_huecos.py).
T.quitar_lo_tapado(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.455 and p.z > 0.55), "Pantalon")
cuerpo.name = "Body26_AbrigoDeMago"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body26_AbrigoDeMago", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("ELDRAN", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
