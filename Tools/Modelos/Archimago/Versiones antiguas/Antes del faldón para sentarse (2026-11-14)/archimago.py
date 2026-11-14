# El Archimago, vestuario propio (12 nov 2026): maestro sanador del valle, adulto maduro, sin sombrero ni bastón.
# Túnica larga abierta por delante (sobretúnica crema con ribetes de oro sobre una túnica interior más cálida), faja celeste con cabo,
# capa corta con forro celeste, mangas anchas con puño de oro y una estrella de oro que cierra el cuello. Botas de cuero.
# Paleta: crema y oro cálido (luz del amanecer), con toque celeste; opuesto claro del Mago Oscuro (violeta/negro) y distinto de Will (azul marino y dorado).
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body34_Archimago → las manos y el torso de Body05 con la ropa encima, pesado como él.
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Euler
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Prendas"))
import ropa_lib as rl, taller as tl
R = math.radians
args = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = args[0], args[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Body05": bpy.data.objects.remove(o)

COLORES = {
    "Crema": (0.96, 0.90, 0.72, 1), "CremaCalida": (0.88, 0.74, 0.50, 1), "Oro": (0.88, 0.67, 0.20, 1), "Celeste": (0.55, 0.74, 0.88, 1),
    "Pantalon": (0.52, 0.44, 0.34, 1), "Cuero": (0.40, 0.26, 0.15, 1), "CueroOscuro": (0.27, 0.17, 0.10, 1),
}
T = tl.Taller(COLORES, grupos=("traje", "velo"))
GRUPOS = T.grupos
nueva, esfera, toro, torno, tubo, caja, estrella = T.nueva, T.esfera, T.toro, T.torno, T.tubo, T.caja, T.estrella
def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]

CY, EL = -0.01, 0.92
def borde(perfil, ang, extra=0.0):   # puntos (x, y, z) del borde de un torno abierto, a un ángulo dado
    return [(math.cos(ang) * (r + extra), CY + math.sin(ang) * (r + extra) * EL, z) for z, r in perfil]
# Pesos (ropa_lib): el tronco solo copia del tronco y las piernas (no del brazo: así no se rasga en la axila);
# lo que cuelga por debajo de la cintura es faldón, que reparte entre pelvis y muslos sin saltos.
FALDON = rl.Faldon(z_cintura=0.40, z_bajo=0.11, muslo=0.65)
ZT = rl.Zona(rl.TRONCO + rl.PIERNAS, FALDON)
# ── Túnica interior (más cálida): larga hasta media pantorrilla, se ve por la abertura de delante ──
# Bajo el brazo no pasa de 0,195 (el cuerpo mide 0,18 y el brazo arranca en 0,20): si no, la túnica envuelve el arranque del brazo.
INT = [(0.745, 0.155), (0.69, 0.19), (0.58, 0.195), (0.5, 0.198), (0.45, 0.2), (0.34, 0.225), (0.24, 0.245), (0.14, 0.262)]
torno("traje", "Tunica", INT, "CremaCalida", ZT, grosor=0.008)
# ── Sobretúnica crema, abierta por delante (60°), con ribete de oro en los bordes y en el bajo ──
EXT = [(0.74, 0.168), (0.69, 0.203), (0.58, 0.208), (0.5, 0.212), (0.45, 0.216), (0.34, 0.25), (0.22, 0.278), (0.11, 0.298)]
ABIERTO = (R(-62), R(242))
torno("traje", "Sobretunica", EXT, "Crema", ZT, abierto=ABIERTO, grosor=0.009, seg=44)
for nombre, ang in (("RibeteI", ABIERTO[0]), ("RibeteD", ABIERTO[1])):
    pts = borde(EXT, ang, 0.006); tubo("traje", nombre, pts, [0.0115] * len(pts), "Oro", ZT, seg=8)
arco_bajo = []
for k in range(0, 45):
    a = ABIERTO[0] + (ABIERTO[1] - ABIERTO[0]) * k / 44; r = EXT[-1][1] + 0.006
    arco_bajo.append((math.cos(a) * r, CY + math.sin(a) * r * EL, EXT[-1][0] + 0.002))
tubo("traje", "RibeteBajo", arco_bajo, [0.012] * len(arco_bajo), "Oro", ZT, seg=8, cerrar=True)
# ── Faja celeste con cabo; hebilla-nudo y estrella de oro al cuello ──
toro("traje", "Faja", (0, CY, 0.45), 0.215, 0.027, "Celeste", ZT, escala=(1, 0.92, 0.9))
esfera("traje", "Nudo", (0.09, -0.205, 0.45), (0.04, 0.026, 0.04), "Celeste", {"pelvis": 1.0})
tubo("traje", "CaboFaja", curva((0.1, -0.215, 0.43), (0.15, -0.245, 0.3), (0.13, -0.225, 0.16), 8), [0.022, 0.022, 0.019], "Celeste", {"pelvis": 1.0}, seg=10)
toro("traje", "FajaOro", (0, CY, 0.405), 0.222, 0.009, "Oro", ZT, escala=(1, 0.92, 1))
# ── Cuello suave con ribete de oro, y la estrella que lo cierra ──
torno("traje", "Cuello", [(0.79, 0.15), (0.755, 0.18), (0.715, 0.198)], "Crema", ZT, seg=40, grosor=0.012)
toro("traje", "CuelloOro", (0, CY, 0.797), 0.152, 0.01, "Oro", {"spine_03": 0.5, "neck_01": 0.5}, escala=(1, 0.92, 1))
estrella("traje", "EstrellaOro", (0.0, -0.21, 0.69), 0.07, 0.031, 0.022, "Oro", {"spine_03": 1.0})
# ── Capa corta a la espalda (hasta la cintura), crema por fuera y celeste por dentro, sujeta a los hombros ──
# Pegada a la espalda (2,5-4 cm por fuera de la sobretúnica) y pesada al tronco copiando, para que siga la espalda
# al andar en vez de ir como una tabla; no toma nada de los brazos, así no se mete en las mangas.
CAPA = [(0.765, 0.2), (0.72, 0.235), (0.62, 0.245), (0.5, 0.25), (0.4, 0.262)]
PC = rl.Zona(rl.TRONCO, suavizar=8)
torno("traje", "Capa", CAPA, ("Crema", "Celeste"), pesos=PC, abierto=(R(32), R(148)), grosor=0.01, seg=30)
pts = [(math.cos(R(32)) * (r + 0.004), CY + math.sin(R(32)) * (r + 0.004) * EL, z) for z, r in CAPA]
tubo("traje", "CapaOroI", pts, [0.009] * len(pts), "Oro", PC, seg=8, cerrar=True)
pts = [(math.cos(R(148)) * (r + 0.004), CY + math.sin(R(148)) * (r + 0.004) * EL, z) for z, r in CAPA]
tubo("traje", "CapaOroD", pts, [0.009] * len(pts), "Oro", PC, seg=8, cerrar=True)
arco = [(math.cos(R(32 + 116 * k / 22)) * (CAPA[-1][1] + 0.004), CY + math.sin(R(32 + 116 * k / 22)) * (CAPA[-1][1] + 0.004) * EL, CAPA[-1][0] + 0.002) for k in range(23)]
tubo("traje", "CapaOroBajo", arco, [0.01] * len(arco), "Oro", PC, seg=8, cerrar=True)
for lado, s in (("l", 1), ("r", -1)):
    # manga ancha en el antebrazo, recogida en la muñeca con puño celeste y ribete de oro (pieza del taller):
    # el puño va ceñido al brazo y copia sus pesos, así no se mete en la tripa al juntar las manos delante
    T.manga_con_puno("traje", lado, cuerpo, arm, "Crema", "Celeste", mat_ribete="Oro", vuelo=0.10)
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.22), (0.116 * s, 0.004, 0.11)],
         [0.092, 0.086, 0.078, 0.074], "Pantalon", rl.Zona(rl.TRONCO + rl.PIERNAS), cerrar=False)
    bota = [(0.0, 0.082), (0.04, 0.087), (0.1, 0.074), (0.13, 0.076), (0.15, 0.084)]
    o = torno("traje", f"Bota_{lado}", bota, "Cuero", rl.Zona(rl.PIERNAS), seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "Cuero", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})


# ── Pesos y unión ─────────────────────────────────────────────────────────────────────────
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
rl.pesar_piezas(GRUPOS["traje"], cuerpo, arm)
tl.revisar_antes_de_exportar(GRUPOS["traje"], cuerpo, arm)
# Lo del cuerpo de debajo que queda tapado se quita, y los cortes se tapan (después de copiar los pesos: la ropa
# los toma del cuerpo entero). La tapa de la cintura, del color del pantalón: es lo que se ve desde abajo.
T.quitar_lo_tapado(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.455 and p.z > 0.55), "Pantalon")
cuerpo.name = "Body34_Archimago"
unir(GRUPOS["traje"], "Body34_Archimago", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("ARCHIMAGO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
