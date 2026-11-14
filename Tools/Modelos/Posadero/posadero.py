# El posadero de la Aldea del Mar, vestuario propio: camisa blanca con las mangas remangadas (el brazo, de
# piel), chaleco verde azulado, delantal blanco de cintura, paño al hombro, pantalón y zapatos. Colores de
# costa. Propuesta aprobada por Raúl el 11 nov 2026.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body31_Posadero → las manos y el torso de Body05 con la ropa encima, pesado como él.
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
    "Camisa": (0.96, 0.95, 0.92, 1), "CamisaSombra": (0.82, 0.84, 0.86, 1), "Chaleco": (0.13, 0.5, 0.52, 1), "ChalecoOscuro": (0.08, 0.34, 0.37, 1),
    "Delantal": (0.97, 0.96, 0.93, 1), "Pano": (0.93, 0.93, 0.95, 1), "PanoRaya": (0.2, 0.55, 0.6, 1), "Pantalon": (0.36, 0.27, 0.2, 1),
    "Zapato": (0.24, 0.15, 0.1, 1), "Laton": (0.8, 0.62, 0.25, 1), "Piel": (0.96, 0.75, 0.59, 1),
}







def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]




T = tl.Taller(COLORES, grupos=('traje',))   # funciones de modelado comunes (../Prendas/taller.py)
MAT, GRUPOS = T.mat, T.grupos
nueva, transformar, esfera, toro, torno, tubo, caja = T.nueva, T.transformar, T.esfera, T.toro, T.torno, T.tubo, T.caja
R = math.radians
ZAPATO = "Zapato"
# ── Camisa y chaleco abierto por delante ──
torno("traje", "Camisa", [(0.725, 0.14), (0.67, 0.19), (0.58, 0.2), (0.5, 0.196), (0.45, 0.196)], "Camisa", grosor=0.008)
torno("traje", "Cuello", [(0.755, 0.15), (0.735, 0.19), (0.712, 0.2)], "Camisa", seg=40, grosor=0.012)
torno("traje", "Chaleco", [(0.71, 0.17), (0.66, 0.204), (0.58, 0.213), (0.5, 0.208), (0.44, 0.208)], "Chaleco", abierto=(R(-68), R(248)), grosor=0.01)
for s in (1, -1):
    tubo("traje", f"BordeChaleco{s}", curva((0.065 * s, -0.16, 0.71), (0.085 * s, -0.2, 0.58), (0.082 * s, -0.196, 0.44), 8), [0.011] * 8, "ChalecoOscuro", None, seg=6)
for z in (0.6, 0.52):
    esfera("traje", f"Boton{int(z*100)}", (0.0, -0.2, z), (0.013, 0.007, 0.013), "CamisaSombra", None)
# ── Delantal de cintura, largo hasta media pierna, con su cinta y el nudo a un lado ──
torno("traje", "Delantal", [(0.455, 0.212), (0.4, 0.222), (0.3, 0.238), (0.2, 0.25)], "Delantal", abierto=(R(-165), R(-15)), grosor=0.008)
toro("traje", "CintaDelantal", (0, -0.01, 0.452), 0.213, 0.016, "Delantal", None, escala=(1, 0.92, 1))
esfera("traje", "NudoDelantal", (0.19, -0.09, 0.452), (0.024, 0.024, 0.022), "Delantal", None)
tubo("traje", "CaboDelantal", curva((0.19, -0.09, 0.45), (0.22, -0.11, 0.4), (0.215, -0.11, 0.34), 6), [0.012] * 6, "Delantal", {"pelvis": 1.0}, seg=6)
# ── Paño al hombro izquierdo: cae por delante y por detrás, con una raya ──
# Es una tira plana (una tela doblada), no un tubo: dos filas de vértices a lo ancho siguiendo el hombro.
pano = [(0.135, -0.222, 0.54), (0.135, -0.218, 0.6), (0.137, -0.205, 0.67), (0.14, -0.14, 0.742), (0.14, -0.03, 0.772), (0.14, 0.08, 0.765),
        (0.138, 0.17, 0.72), (0.136, 0.212, 0.65), (0.135, 0.222, 0.58)]
ANCHO = 0.055
bm = bmesh.new(); filas = [(bm.verts.new((x - ANCHO, y, z)), bm.verts.new((x + ANCHO, y, z))) for x, y, z in pano]
for a, b in zip(filas, filas[1:]): bm.faces.new((a[0], a[1], b[1], b[0]))
nueva("traje", "Pano", bm, "Pano", {"spine_03": 0.7, "clavicle_l": 0.3}, grosor=0.014)
caja("traje", "RayaPano", (0.135, -0.228, 0.565), (ANCHO * 2 + 0.006, 0.02, 0.016), "PanoRaya", {"spine_03": 1.0})
for lado, s in (("l", 1), ("r", -1)):
    tubo("traje", f"Manga_{lado}", [(0.15 * s, -0.003, 0.636), (0.22 * s, -0.003, 0.635), (0.295 * s, -0.003, 0.634)], [0.082, 0.08, 0.079], "Camisa", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.165 * s, -0.003, 0.64), (0.084, 0.082, 0.082), "Camisa", None)
    toro("traje", f"Vuelta_{lado}", (0.295 * s, -0.003, 0.634), 0.078, 0.021, "CamisaSombra", None, rot=(0, R(90), 0))
    tubo("traje", f"Brazo_{lado}", [(0.26 * s, -0.003, 0.634), (0.33 * s, -0.002, 0.634), (0.4 * s, -0.002, 0.632), (0.475 * s, -0.003, 0.629)],
         [0.068, 0.067, 0.068, 0.07], "Piel", None, cerrar=False)
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.2), (0.116 * s, 0.004, 0.09)],
         [0.092, 0.086, 0.078, 0.073], "Pantalon", None, cerrar=False)
    o = torno("traje", f"Zapato_{lado}", [(0.0, 0.08), (0.04, 0.085), (0.085, 0.074), (0.1, 0.078)], ZAPATO, None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.086, 0.128, 0.014), ZAPATO, {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.068, 0.088, 0.042), ZAPATO, {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# ── Pesos y unión ─────────────────────────────────────────────────────────────────────────
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
# La piel de los brazos usa el material del propio cuerpo (el de las manos y la cabeza) con sus UV puestas en
# el color de piel de la textura: así es exactamente el mismo tono que las manos.
uv_cuerpo = cuerpo.data.uv_layers.active
piel_uv = None
for p in cuerpo.data.polygons:
    if abs((cuerpo.matrix_world @ p.center).x) > 0.5:
        piel_uv = tuple(uv_cuerpo.data[p.loop_indices[0]].uv); break
for o, _ in GRUPOS["traje"]:
    if o.data.materials[0].name.startswith("Piel"):
        o.data.materials.clear(); o.data.materials.append(cuerpo.data.materials[0])
        uv = o.data.uv_layers.new(name=uv_cuerpo.name)
        for d in uv.data: d.uv = piel_uv
rl.pesar_piezas(rl.completar_zonas(GRUPOS["traje"]), cuerpo, arm)
tl.revisar_antes_de_exportar([x for g in GRUPOS.values() for x in g], cuerpo, arm)
# Lo del cuerpo de debajo que queda tapado por la ropa se quita (después de copiar los pesos: la ropa los
# toma del cuerpo entero). Del Body05 solo quedan las manos y el torso.
# Los cortes se tapan con «Pantalon»: es lo que se ve desde abajo por el bajo de la ropa (ver chequeo_huecos.py).
T.quitar_lo_tapado(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.455 and p.z > 0.55), "Pantalon")
cuerpo.name = "Body31_Posadero"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body31_Posadero", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
