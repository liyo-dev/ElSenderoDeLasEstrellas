# Silas joven, vestuario propio (19 nov 2026, INC-1338): el hechicero de los relojes, amigo de juventud de Eldran y Selene,
# en el recuerdo de «La mañana después» (hace cuarenta años, cuando buscaron la Piedra en su barco). GDD § 3 (Silas) y § 5.11.
# Camisa clara de manga larga recogida en la muñeca, chaleco ciruela abierto con botones de latón, reloj de bolsillo con su
# cadena (su objeto: la colección de relojes), pañuelo azul claro al cuello, pantalón gris y botas altas con la vuelta clara.
# Sin abrigo largo ni sombrero: la silueta se distingue de Eldran joven (abrigo largo cerrado) también en el recuerdo, que
# va en blanco y negro: camisa clara, chaleco oscuro, pantalón de tono medio (la paleta se pensó por valores, no por color).
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body36_Relojero → las manos y el torso de Body05 con la ropa encima, pesado como él.
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
    "Camisa": (0.93, 0.90, 0.82, 1), "CamisaSombra": (0.78, 0.74, 0.66, 1),
    "Chaleco": (0.36, 0.14, 0.24, 1), "ChalecoOscuro": (0.24, 0.08, 0.15, 1),
    "Panuelo": (0.56, 0.72, 0.80, 1), "Laton": (0.80, 0.62, 0.25, 1), "Esfera": (0.96, 0.93, 0.84, 1),
    "Pantalon": (0.33, 0.35, 0.41, 1), "Cuero": (0.36, 0.22, 0.12, 1), "CueroClaro": (0.58, 0.40, 0.24, 1), "CueroOscuro": (0.20, 0.12, 0.07, 1),
}
T = tl.Taller(COLORES, grupos=("traje",))
GRUPOS = T.grupos
esfera, toro, torno, tubo = T.esfera, T.toro, T.torno, T.tubo
def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]
CY, EL = -0.01, 0.92

# ── Camisa: el torso (se ve por la abertura del chaleco), con su cuello ──
CAMISA = [(0.75, 0.15), (0.72, 0.172), (0.66, 0.182), (0.58, 0.185), (0.5, 0.182), (0.44, 0.188), (0.39, 0.196)]
torno("traje", "Camisa", CAMISA, "Camisa")
torno("traje", "Cuello", [(0.785, 0.152), (0.755, 0.168), (0.725, 0.178)], "Camisa", seg=40, grosor=0.01)
# ── Chaleco ciruela, abierto por delante (la camisa asoma en una V estrecha), por encima de la camisa ──
CHALECO = [(0.735, 0.17), (0.70, 0.194), (0.62, 0.2), (0.54, 0.199), (0.47, 0.198), (0.42, 0.205), (0.37, 0.214)]
ABIERTO = (R(-72), R(252))
torno("traje", "Chaleco", CHALECO, ("Chaleco", "ChalecoOscuro"), abierto=ABIERTO, grosor=0.008, seg=44)
def borde(perfil, ang, extra=0.0):
    return [(math.cos(ang) * (r + extra), CY + math.sin(ang) * (r + extra) * EL, z) for z, r in perfil]
for nombre, ang in (("RibeteI", ABIERTO[0]), ("RibeteD", ABIERTO[1])):
    pts = borde(CHALECO, ang, 0.005); tubo("traje", nombre, pts, [0.009] * len(pts), "ChalecoOscuro", None, seg=8)
arco_bajo = [(math.cos(a) * (CHALECO[-1][1] + 0.005), CY + math.sin(a) * (CHALECO[-1][1] + 0.005) * EL, CHALECO[-1][0] + 0.002)
             for a in (ABIERTO[0] + (ABIERTO[1] - ABIERTO[0]) * k / 44 for k in range(45))]
tubo("traje", "RibeteBajo", arco_bajo, [0.01] * len(arco_bajo), "ChalecoOscuro", None, seg=8)
# botones de latón en el borde de su izquierda (+X); van rígidos (ropa_lib: un adorno no se estira)
for z in (0.64, 0.56, 0.48, 0.41):
    r = 0.2 + 0.008; a = ABIERTO[0] + R(6)
    esfera("traje", f"Boton{int(z*100)}", (math.cos(a) * r, CY + math.sin(a) * r * EL - 0.004, z), (0.014, 0.007, 0.014), "Laton", None)
# ── Reloj de bolsillo: asoma del bolsillo de su derecha (-X), delante; la cadena va al segundo botón ──
# Nada en el costado (14 nov 2026, INC-1183: la mano se mete): bolsillo y reloj, en la cara delantera del chaleco.
a_r = R(-118); rr = 0.199 + 0.006
CR = (math.cos(a_r) * rr, CY + math.sin(a_r) * rr * EL, 0.50)
esfera("traje", "Reloj", (CR[0], CR[1] - 0.006, CR[2] + 0.012), (0.026, 0.009, 0.026), "Laton", None)
# la esfera, como un cristal abombado que sobresale de la caja (a 5 mm en su borde): a la par de la caja parpadeaba
esfera("traje", "RelojEsfera", (CR[0], CR[1] - 0.017, CR[2] + 0.012), (0.019, 0.003, 0.019), "Esfera", None)
esfera("traje", "Bolsillo", (CR[0], CR[1] + 0.001, CR[2] - 0.01), (0.044, 0.008, 0.022), "ChalecoOscuro", None)
# cadena: del reloj al botón de 0,56, combada hacia abajo y apoyada sobre el chaleco (copia sus pesos: se mueven juntos)
chal = next(o for o, _ in GRUPOS["traje"] if o.name == "Chaleco")
a_b = ABIERTO[0] + R(6); B = (math.cos(a_b) * 0.208, CY + math.sin(a_b) * 0.208 * EL - 0.004, 0.56)
cad = [Vector(p) for p in curva((CR[0] + 0.02, CR[1] - 0.004, CR[2] + 0.02), (0.0, -0.215, 0.47), B, 12)]
tubo("traje", "Cadena", [tuple(p) for p in cad], [0.0045], "Laton", rl.Zona(sobre=chal), seg=6, cerrar=True, grosor=0)
# ── Pañuelo azul claro anudado al cuello, con dos cabos cortos que caen en la abertura ──
toro("traje", "Panuelo", (0, CY - 0.004, 0.745), 0.165, 0.022, "Panuelo", None, escala=(1, 0.95, 0.8))
esfera("traje", "NudoPanuelo", (0.0, -0.188, 0.725), (0.032, 0.022, 0.03), "Panuelo", None)
for s in (1, -1):
    tubo("traje", f"CaboPanuelo{'I' if s > 0 else 'D'}", curva((0.008 * s, -0.192, 0.71), (0.02 * s, -0.205, 0.66), (0.016 * s, -0.2, 0.62), 6),
         [0.016, 0.017, 0.012], "Panuelo", None, seg=10)
# ── Mangas largas de la camisa, con vuelo en el antebrazo y recogidas en un puño de cuero claro ──
for lado, s in (("l", 1), ("r", -1)):
    T.manga_con_puno("traje", lado, cuerpo, arm, "Camisa", "CamisaSombra", mat_puno="CueroClaro", vuelo=0.098)
    T.pernera("traje", lado, 0.118, "Pantalon", alto_bota=0.19)
    T.bota("traje", lado, 0.118, "Cuero", "CueroClaro", alto=0.19)
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), "CueroOscuro", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), "Cuero", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# trabilla de la espalda del chaleco con su hebilla (detalle de chaleco de la época; por detrás, no en el costado)
a_t = R(90); rt = 0.205 + 0.006
toro("traje", "Trabilla", (0, CY + 0.004, 0.445), 0.207, 0.008, "ChalecoOscuro", None, escala=(0.55, 0.95, 1))
esfera("traje", "HebillaEspalda", (0.0, CY + math.sin(a_t) * rt * EL + 0.003, 0.445), (0.022, 0.006, 0.016), "Laton", None)
# cinturón fino bajo el chaleco, sobre el pantalón (no se ve más que por delante y por la V)
toro("traje", "Cinturon", (0, CY, 0.405), 0.198, 0.012, "Cuero", None, escala=(1, 0.92, 1))

# ── Pesos y unión ─────────────────────────────────────────────────────────────────────────
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
rl.pesar_piezas(rl.completar_zonas(GRUPOS["traje"]), cuerpo, arm)
tl.revisar_antes_de_exportar(GRUPOS["traje"], cuerpo, arm)
T.quitar_lo_tapado(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.455 and p.z > 0.55), "Pantalon")
cuerpo.name = "Body36_Relojero"
unir(GRUPOS["traje"], "Body36_Relojero", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("SILAS", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
