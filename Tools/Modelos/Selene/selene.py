# Selene joven, vestuario propio (19 nov 2026): la amiga de juventud de Eldran y Silas que buscó la Piedra con ellos en el barco
# de Silas y murió al cruzar el umbral (GDD § 3 y § 5.11; recuerdo de «La mañana después»). Antes llevaba un modelo del pack con
# gorro puntiagudo y túnica verde; aquí, sin gorro y sin verde.
# Viajera: túnica plateada pálida hasta medio muslo con ribete azul noche, cinturón ancho de cuero con hebilla de luna creciente
# (su nombre), capa corta azul noche a la espalda con forro plata, un broche de luna en el pecho, mangas recogidas en la
# muñeca, mallas gris azulado y botas altas de cuero claro con la vuelta.
# Pensada por valores para el recuerdo en blanco y negro: figura clara (túnica y pelo) con una mancha oscura detrás (capa) y
# las piernas oscuras; Eldran es todo de tono medio-oscuro con el pelo oscuro y Silas, chaleco oscuro sobre camisa clara.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body37_Viajera → las manos y el torso de Body05 con la ropa encima, pesado como él.
import bpy, bmesh, math, sys, os
from mathutils import Vector
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
    "Luna": (0.80, 0.84, 0.90, 1), "LunaSombra": (0.62, 0.67, 0.76, 1),
    "Noche": (0.16, 0.19, 0.36, 1), "NocheOscura": (0.10, 0.11, 0.22, 1),
    "Plata": (0.86, 0.87, 0.92, 1), "Malla": (0.34, 0.36, 0.48, 1),
    "Cuero": (0.55, 0.38, 0.22, 1), "CueroClaro": (0.74, 0.58, 0.38, 1), "CueroOscuro": (0.24, 0.15, 0.09, 1),
}
T = tl.Taller(COLORES, grupos=("traje",))
GRUPOS = T.grupos
CY, EL = -0.01, 0.92
def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]
def luna(nombre, centro, r, mat, pesos):
    """Luna creciente mirando hacia delante: un disco con otro restado (plano grueso, de perfil se ve como una chapa)."""
    bm = bmesh.new(); cx, cy_, cz = centro; n = 18; g = 0.006
    fuera = [(cx + r * math.cos(R(60 + 240 * k / n)), cz + r * math.sin(R(60 + 240 * k / n))) for k in range(n + 1)]
    dentro = [(cx + r * 0.38 + r * 0.78 * math.cos(R(300 - 240 * k / n)), cz + r * 0.78 * math.sin(R(300 - 240 * k / n))) for k in range(n + 1)]
    contorno = fuera + dentro
    fr = [bm.verts.new((x, cy_ - g / 2, z)) for x, z in contorno]; at = [bm.verts.new((x, cy_ + g / 2, z)) for x, z in contorno]
    m = len(contorno)
    for k in range(n):   # tiras entre el borde de fuera y el de dentro (sin n-gonos cóncavos)
        a, b, c, d = k, k + 1, m - 1 - (k + 1), m - 1 - k
        bm.faces.new((fr[a], fr[b], fr[c], fr[d])); bm.faces.new((at[d], at[c], at[b], at[a]))
    for k in range(m): bm.faces.new((fr[k], fr[(k + 1) % m], at[(k + 1) % m], at[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = T.nueva("traje", nombre, bm, mat, pesos)
    for p in o.data.polygons: p.use_smooth = False
    return o

# ── Túnica plateada hasta medio muslo (cuelga como faldón desde la cintura), ribete azul noche en el bajo ──
FALDON = rl.Faldon(z_cintura=0.42, z_bajo=0.27, muslo=0.65)
ZT = rl.Zona(rl.TRONCO + rl.PIERNAS, FALDON)
TUNICA = [(0.745, 0.155), (0.69, 0.188), (0.58, 0.194), (0.5, 0.188), (0.45, 0.193), (0.38, 0.215), (0.32, 0.232), (0.27, 0.242)]
tunica = T.torno("traje", "Tunica", TUNICA, ("Luna", "LunaSombra"), ZT, grosor=0.008)
T.torno("traje", "Bajo", [(0.266, 0.25), (0.30, 0.243), (0.345, 0.233)], "Noche", ZT, seg=40, grosor=0.006)   # franja ancha en el bajo, por fuera de la túnica
# cuello redondo con ribete azul noche
T.torno("traje", "Cuello", [(0.79, 0.15), (0.765, 0.165), (0.735, 0.178)], "Luna", rl.Zona(sobre=tunica), seg=40, grosor=0.01)
T.toro("traje", "CuelloRibete", (0, CY, 0.79), 0.15, 0.008, "Noche", {"spine_03": 0.5, "neck_01": 0.5}, escala=(1, 0.92, 1))
# ── Cinturón ancho de cuero con hebilla de luna creciente (plata), por delante ──
T.torno("traje", "Cinturon", [(0.425, 0.203), (0.465, 0.199)], "Cuero", ZT, seg=40, grosor=0.006)
luna("HebillaLuna", (0.0, -0.207, 0.445), 0.034, "Plata", None)
# ── Capa corta a la espalda (hasta la cintura), azul noche por fuera y plata por dentro, pegada a la espalda ──
# Como la del Archimago: solo cubre la espalda (de 32° a 148°) y va pesada al tronco, así no se mete en las mangas.
CAPA = [(0.765, 0.2), (0.72, 0.232), (0.62, 0.242), (0.5, 0.248), (0.4, 0.26)]
PC = rl.Zona(rl.TRONCO, suavizar=8)
T.torno("traje", "Capa", CAPA, ("Noche", "LunaSombra"), pesos=PC, abierto=(R(32), R(148)), grosor=0.01, seg=30)
arco = [(math.cos(R(32 + 116 * k / 22)) * (CAPA[-1][1] + 0.004), CY + math.sin(R(32 + 116 * k / 22)) * (CAPA[-1][1] + 0.004) * EL, CAPA[-1][0] + 0.002) for k in range(23)]
T.tubo("traje", "CapaBajo", arco, [0.009] * len(arco), "NocheOscura", PC, seg=8, cerrar=True)
# broche de luna creciente sobre el pecho, a su izquierda (de frente, el cuello lo tapa la cabeza del pack)
luna("BrocheLuna", (0.075, -0.192, 0.6), 0.03, "Plata", None)
# ── Mangas recogidas en la muñeca con puño azul noche; mallas oscuras y botas altas de cuero claro ──
for lado, s in (("l", 1), ("r", -1)):
    T.manga_con_puno("traje", lado, cuerpo, arm, "Luna", "LunaSombra", mat_puno="Noche", vuelo=0.096)
    T.pernera("traje", lado, 0.118, "Malla", alto_bota=0.2, nombre="Malla")
    T.bota("traje", lado, 0.118, "Cuero", "CueroClaro", alto=0.2)
    T.esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.086, 0.126, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    T.esfera("traje", f"Puntera_{lado}", (0.122 * s, -0.083, 0.035), (0.068, 0.088, 0.043), "Cuero", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})

def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
rl.pesar_piezas(rl.completar_zonas(GRUPOS["traje"]), cuerpo, arm)
tl.revisar_antes_de_exportar(GRUPOS["traje"], cuerpo, arm)
# los cortes se tapan con las mallas: es lo que se ve desde abajo por el bajo de la túnica
T.quitar_lo_tapado(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.455 and p.z > 0.55), "Malla")
cuerpo.name = "Body37_Viajera"
unir(GRUPOS["traje"], "Body37_Viajera", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("SELENE", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
