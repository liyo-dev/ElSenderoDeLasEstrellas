# Liora, vestuario propio (13 nov 2026): la vida que el Archimago dejó atrás en el valle (prólogo y final). Su relación
# con él queda abierta (amiga). Paleta elegida por Raúl: verde salvia y crema (casa del valle; combina con el crema y
# oro del Archimago sin calcarlo). Vestido largo salvia con el bajo crema, cuello ancho de crema con una flor, faja
# tejida de salvia oscuro con el nudo a un lado, mangas recogidas en la muñeca con puño crema, medias crema y botines bajos de cuero.
# Distinta de la boticaria (vestido verde con delantal ámbar) y de Victoria (lila con delantal): sin delantal.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body35_Liora → las manos y el torso de Body05 con la ropa encima, pesado como él.
import bpy, bmesh, math, sys, os
from mathutils import Vector
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Prendas"))
import ropa_lib as rl, taller as tl
args = sys.argv[sys.argv.index("--") + 1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Salvia": (0.56, 0.68, 0.52, 1), "SalviaOscura": (0.34, 0.45, 0.33, 1), "Crema": (0.96, 0.92, 0.80, 1),
    "Flor": (0.98, 0.80, 0.36, 1), "Cuero": (0.42, 0.27, 0.16, 1), "CueroOscuro": (0.26, 0.16, 0.09, 1),
}
T = tl.Taller(COLORES, grupos=("traje",))   # funciones de modelado comunes (../Prendas/taller.py)
GRUPOS = T.grupos
R = math.radians; CY = -0.01
def curva(p0, p1, p2, n):
    return [tuple((1 - u) ** 2 * Vector(p0) + 2 * (1 - u) * u * Vector(p1) + u * u * Vector(p2)) for u in (i / (n - 1) for i in range(n))]

# El vestido cuelga como faldón desde la cintura y copia del tronco y las piernas (no del brazo: no se rasga en la axila)
FALDON = rl.Faldon(z_cintura=0.42, z_bajo=0.12, muslo=0.65)
ZT = rl.Zona(rl.TRONCO + rl.PIERNAS, FALDON)
# ── Vestido largo hasta el tobillo, con algo de vuelo y ondas suaves en el bajo ──
VESTIDO = [(0.745, 0.155), (0.69, 0.19), (0.58, 0.196), (0.5, 0.19), (0.45, 0.195), (0.34, 0.226), (0.22, 0.252), (0.12, 0.268)]
vestido = T.torno("traje", "Vestido", VESTIDO, ("Salvia", "SalviaOscura"), ZT, grosor=0.008, onda=0.02, n_onda=9)
T.toro("traje", "Bajo", (0, CY, 0.124), 0.268, 0.012, "Crema", ZT, escala=(1, 0.92, 1))
# ── Faja tejida a la cintura, con el nudo y dos cabos a su izquierda ──
T.toro("traje", "Faja", (0, CY, 0.45), 0.2, 0.024, "SalviaOscura", ZT, escala=(1, 0.92, 0.9))
T.esfera("traje", "Nudo", (0.1, -0.19, 0.45), (0.032, 0.022, 0.032), "SalviaOscura", {"pelvis": 1.0})
for k, dx in enumerate((0.1, 0.13)):
    T.tubo("traje", f"CaboFaja{k}", curva((0.1, -0.2, 0.44), (dx + 0.02, -0.23, 0.36), (dx, -0.22, 0.27 + 0.03 * k), 7),
           [0.014, 0.013, 0.01], "SalviaOscura", {"pelvis": 1.0}, seg=8)
# ── Cuello ancho de crema (como una esclavina corta que no llega a los brazos) y una flor que lo cierra ──
T.torno("traje", "Cuello", [(0.79, 0.15), (0.765, 0.18), (0.725, 0.205), (0.70, 0.208)], "Crema", rl.Zona(sobre=vestido), seg=40, grosor=0.012)
T.toro("traje", "CuelloRibete", (0, CY, 0.70), 0.21, 0.006, "SalviaOscura", rl.Zona(sobre=vestido), escala=(1, 0.92, 1))
for k in range(5):   # flor de cinco pétalos mirando hacia delante
    a = R(90 + 72 * k)
    T.esfera("traje", f"Petalo{k}", (0.022 * math.cos(a), -0.226, 0.69 + 0.022 * math.sin(a)), (0.017, 0.008, 0.017), "Flor", {"spine_03": 1.0})
T.esfera("traje", "CentroFlor", (0.0, -0.232, 0.69), (0.011, 0.008, 0.011), "Cuero", {"spine_03": 1.0})
# ── Mangas recogidas en la muñeca con puño crema (taller), zapatos de cuero bajo el vestido ──
for lado, s in (("l", 1), ("r", -1)):
    T.manga_con_puno("traje", lado, cuerpo, arm, "Salvia", "SalviaOscura", mat_puno="Crema", boca=0.44, vuelo=0.098)
    T.esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.085, 0.125, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    # medias crema metidas en unos botines bajos de cuero (taller): bajo el vestido largo solo asoman al andar
    T.pernera("traje", lado, 0.118, "Crema", alto_bota=0.13, radio_bota=0.088, nombre="Media")
    T.bota("traje", lado, 0.118, "Cuero", alto=0.13, radio=0.088, nombre="Botin")

# ── Pesos, revisión, cuerpo tapado y unión ──
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
rl.pesar_piezas(GRUPOS["traje"], cuerpo, arm)
tl.revisar_antes_de_exportar(GRUPOS["traje"], cuerpo, arm)
# Los cortes se tapan con el salvia oscuro del forro: es lo que se ve desde abajo por el bajo del vestido.
T.quitar_lo_tapado(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.455 and p.z > 0.55), "SalviaOscura")
cuerpo.name = "Body35_Liora"
unir(GRUPOS["traje"], "Body35_Liora", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("LIORA", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
