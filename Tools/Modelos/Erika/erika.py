# Erika, vestuario propio: guerrera ligera. Camisa verde, peto y brazales de cuero, una hombrera, cinturón
# capa corta a la espalda, pañuelo azul claro al cuello (el que luego lleva Will), pantalón y
# botas altas. Propuesta aprobada por Raúl el 11 nov 2026.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body28_GuerreraLigera → las manos y el torso de Body05 con la ropa encima, pesado como él.
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
    "Camisa": (0.30, 0.52, 0.30, 1), "CamisaOscura": (0.18, 0.34, 0.2, 1), "Peto": (0.45, 0.28, 0.15, 1), "Cuero": (0.33, 0.2, 0.11, 1),
    "CueroOscuro": (0.2, 0.12, 0.07, 1), "Metal": (0.72, 0.74, 0.78, 1), "Capa": (0.2, 0.36, 0.24, 1), "CapaForro": (0.14, 0.24, 0.17, 1),
    "Panuelo": (0.55, 0.78, 0.95, 1), "Pantalon": (0.25, 0.27, 0.22, 1), "Laton": (0.8, 0.62, 0.25, 1),
}







def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]




T = tl.Taller(COLORES, grupos=('traje',))   # funciones de modelado comunes (../Prendas/taller.py)
MAT, GRUPOS = T.mat, T.grupos
nueva, transformar, esfera, toro, torno, tubo, caja = T.nueva, T.transformar, T.esfera, T.toro, T.torno, T.tubo, T.caja
R = math.radians
# ── Camisa y peto de cuero ──
torno("traje", "Camisa", [(0.725, 0.14), (0.67, 0.192), (0.58, 0.2), (0.5, 0.19), (0.45, 0.19), (0.4, 0.2)], "Camisa", grosor=0.008)
toro("traje", "BajoCamisa", (0, -0.01, 0.402), 0.2, 0.01, "CamisaOscura", None, escala=(1, 0.92, 1))
torno("traje", "Peto", [(0.69, 0.2), (0.6, 0.214), (0.52, 0.206), (0.47, 0.202)], "Peto", grosor=0.012)
toro("traje", "PetoArriba", (0, -0.01, 0.69), 0.199, 0.011, "CueroOscuro", None, escala=(1, 0.92, 1))
for s in (1, -1):   # correas del peto con su hebilla
    tubo("traje", f"CorreaPeto{s}", curva((0.11 * s, -0.19, 0.69), (0.14 * s, -0.02, 0.795), (0.11 * s, 0.17, 0.69), 9), [0.016] * 9, "CueroOscuro", None, seg=8)
    esfera("traje", f"HebillaPeto{s}", (0.115 * s, -0.2, 0.685), (0.02, 0.01, 0.02), "Laton", None)
# Cinturón ancho con hebilla (sin bolsa: en el costado se la comía la mano al andar)
toro("traje", "Cinturon", (0, -0.01, 0.45), 0.207, 0.024, "CueroOscuro", None, escala=(1, 0.92, 0.9))
esfera("traje", "Hebilla", (0.0, -0.21, 0.45), (0.034, 0.014, 0.03), "Laton", None)
# Pañuelo azul claro al cuello, con el pico por delante (el que luego lleva Will)
toro("traje", "Panuelo", (0, -0.012, 0.728), 0.165, 0.045, "Panuelo", {"spine_03": 1.0}, escala=(1, 0.98, 0.7))
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=0.11, radius2=0.0, depth=0.15)
transformar(bm, (0, -0.2, 0.655), (1, 0.3, 1), (R(180 + 12), 0, R(45)))
nueva("traje", "PicoPanuelo", bm, "Panuelo", {"spine_03": 1.0})
# Capa corta: de los hombros a la cintura, solo por la espalda
torno("traje", "Capa", [(0.745, 0.2), (0.68, 0.245), (0.58, 0.262), (0.47, 0.275), (0.4, 0.285)], "Capa", abierto=(R(8), R(172)), grosor=0.01, onda=0.03, n_onda=7,
      pesos={"spine_03": 0.6, "spine_02": 0.4})
for lado, s in (("l", 1), ("r", -1)):
    tubo("traje", f"Manga_{lado}", [(0.16 * s, -0.003, 0.636), (0.25 * s, -0.003, 0.635), (0.325 * s, -0.003, 0.634)], [0.08, 0.074, 0.07], "Camisa", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.168 * s, -0.003, 0.64), (0.083, 0.081, 0.081), "Camisa", None)
    # brazal de cuero del codo a la muñeca, con dos correas
    tubo("traje", f"Brazal_{lado}", [(0.345 * s, -0.003, 0.634), (0.38 * s, -0.003, 0.633), (0.445 * s, -0.003, 0.632)], [0.076, 0.074, 0.075], "Peto", None, cerrar=False)   # copia del brazo, como la manga que tapa
    for x in (0.34, 0.425):
        toro("traje", f"CorreaBrazal_{lado}{int(x*1000)}", (x * s, -0.003, 0.633), 0.083, 0.009, "CueroOscuro", None, rot=(0, R(90), 0))
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.24)], [0.092, 0.086, 0.08], "Pantalon", None, cerrar=False)
    # bota alta con vuelta
    o = torno("traje", f"Bota_{lado}", [(0.0, 0.082), (0.04, 0.087), (0.12, 0.076), (0.2, 0.082), (0.25, 0.09)], "Cuero", None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.116 * s
    toro("traje", f"VueltaBota_{lado}", (0.112 * s, 0.0, 0.25), 0.088, 0.016, "CueroOscuro", {f"calf_{lado}": 1.0})
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "Cuero", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# Una hombrera de cuero con remaches, en el hombro izquierdo
esfera("traje", "Hombrera", (0.185, -0.003, 0.672), (0.105, 0.1, 0.07), "Peto", {"clavicle_l": 0.5, "upperarm_l": 0.5})
for a in (-50, 0, 50):
    # sobre la superficie de la hombrera (antes quedaban hundidos dentro de ella)
    dx, dy = 0.058, 0.06 * math.sin(R(a))   # en fila por el lado de fuera: junto al cuello se metían en el pañuelo al subir los brazos
    dz = 0.07 * math.sqrt(max(0.0, 1 - (dx / 0.105) ** 2 - (dy / 0.1) ** 2))
    esfera("traje", f"Remache{a}", (0.185 + dx, -0.003 + dy, 0.672 + dz + 0.004), (0.012, 0.012, 0.012), "Metal", {"clavicle_l": 0.5, "upperarm_l": 0.5})
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
cuerpo.name = "Body28_GuerreraLigera"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body28_GuerreraLigera", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
