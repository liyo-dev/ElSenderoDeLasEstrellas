# Victoria, vestuario propio: costurera. Blusa lila con las mangas remangadas, falda lila oscuro con vuelo,
# delantal crema con peto y lazo a la espalda, cinta de medir al cuello, acerico con alfileres en el
# bolsillo, medias y zapatos. El moño va aparte, como accesorio de cabeza. Propuesta aprobada por Raúl
# el 11 nov 2026. Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y.
#   Body27_Costurera → las manos y el torso de Body05 con la ropa encima, pesado como él.
#   AC13_Mono        → el moño con su cinta, pesado a «head» (del color del pelo en Unity).
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
    "Blusa": (0.70, 0.58, 0.86, 1), "BlusaClara": (0.86, 0.78, 0.95, 1), "Falda": (0.46, 0.34, 0.62, 1), "FaldaOscura": (0.33, 0.24, 0.46, 1),
    "Delantal": (0.96, 0.91, 0.79, 1), "DelantalOscuro": (0.82, 0.74, 0.58, 1), "Cinta": (0.98, 0.82, 0.25, 1),
    "Acerico": (0.80, 0.18, 0.2, 1), "Alfiler": (0.92, 0.92, 0.95, 1), "Media": (0.95, 0.93, 0.9, 1), "Zapato": (0.36, 0.2, 0.12, 1),
    "Piel": (0.96, 0.75, 0.59, 1), "Pelo": (0.95, 0.36, 0.9, 1),
}







def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]




T = tl.Taller(COLORES, grupos=('traje', 'mono'))   # funciones de modelado comunes (../Prendas/taller.py)
MAT, GRUPOS = T.mat, T.grupos
nueva, transformar, esfera, toro, torno, tubo, caja = T.nueva, T.transformar, T.esfera, T.toro, T.torno, T.tubo, T.caja
R = math.radians
ZAPATO = "Zapato"; H = {"head": 1.0}
# ── Blusa y falda ──
torno("traje", "Blusa", [(0.725, 0.14), (0.67, 0.192), (0.58, 0.2), (0.5, 0.19), (0.45, 0.188)], "Blusa", grosor=0.008)
torno("traje", "Cuello", [(0.755, 0.15), (0.735, 0.19), (0.712, 0.2)], "BlusaClara", seg=40, grosor=0.012)
torno("traje", "Falda", [(0.47, 0.186), (0.42, 0.212), (0.32, 0.25), (0.21, 0.288)], "Falda", grosor=0.008, onda=0.035, n_onda=10)
toro("traje", "Bajo", (0, -0.01, 0.212), 0.286, 0.01, "FaldaOscura", None, escala=(1, 0.92, 1))
# ── Delantal: el peto delante del pecho, la falda del delantal por delante, la cinta a la cintura y el lazo detrás ──
torno("traje", "Peto", [(0.67, 0.203), (0.6, 0.212), (0.52, 0.205), (0.46, 0.2)], "Delantal", abierto=(R(-125), R(-55)), grosor=0.007)
torno("traje", "DelantalFalda", [(0.47, 0.2), (0.42, 0.224), (0.33, 0.259), (0.25, 0.284)], "Delantal", abierto=(R(-160), R(-20)), grosor=0.007)
toro("traje", "CintaDelantal", (0, -0.01, 0.462), 0.203, 0.014, "DelantalOscuro", None, escala=(1, 0.92, 1))
for s in (1, -1):
    # tirantes del peto, por encima del hombro hasta la cintura por detrás
    tubo("traje", f"Tirante{s}", curva((0.1 * s, -0.19, 0.67), (0.13 * s, -0.02, 0.8), (0.09 * s, 0.17, 0.47), 10), [0.013] * 10, "Delantal", None, seg=8)
    esfera("traje", f"Lazo{s}", (0.07 * s, 0.215, 0.47), (0.065, 0.02, 0.04), "Delantal", {"spine_01": 1.0}, rot=(0, R(18 * s), 0))
    tubo("traje", f"CaboLazo{s}", curva((0.02 * s, 0.215, 0.46), (0.05 * s, 0.25, 0.4), (0.06 * s, 0.262, 0.33), 6), [0.014] * 6, "Delantal", {"pelvis": 1.0}, seg=8)
esfera("traje", "NudoLazo", (0, 0.218, 0.468), (0.026, 0.022, 0.026), "DelantalOscuro", {"spine_01": 1.0})
# Bolsillo con el acerico y sus alfileres
caja("traje", "Bolsillo", (0.09, -0.262, 0.345), (0.1, 0.012, 0.075), "DelantalOscuro", {"pelvis": 1.0}, rot=(R(-14), 0, 0))
esfera("traje", "Acerico", (0.09, -0.262, 0.39), (0.036, 0.026, 0.028), "Acerico", {"pelvis": 1.0})
for k, (dx, dz) in enumerate(((-0.018, 0.018), (0.0, 0.026), (0.02, 0.018))):
    esfera("traje", f"Alfiler{k}", (0.09 + dx, -0.272, 0.39 + dz), (0.007, 0.007, 0.007), "Alfiler", {"pelvis": 1.0})
# Cinta de medir: sobre los hombros, por detrás del cuello, con los dos cabos colgando por delante.
# Los cabos van pegados al peto (a su superficie más el grosor de la cinta) y terminan encima de la cinta del
# delantal; se pesan como el peto (copiados del cuerpo), así no se separan de él al moverse (en el juego se veían
# despegados y acababan colgando por debajo del peto).
PETO = [(0.67, 0.203), (0.6, 0.212), (0.52, 0.205), (0.46, 0.2)]
def sobre_el_peto(x, z, r_cinta=0.013):
    zs = sorted(PETO)
    z = min(max(z, zs[0][0]), zs[-1][0])
    for (z0, r0), (z1, r1) in zip(zs, zs[1:]):
        if z0 <= z <= z1: r = r0 + (r1 - r0) * (z - z0) / (z1 - z0); break
    return -0.01 - r * 0.92 * math.sqrt(max(0.0, 1 - (x / r) ** 2)) - 0.007 - r_cinta * 0.8
cabo = [(0.1, 0.665), (0.088, 0.6), (0.082, 0.54), (0.08, 0.49)]
lado_der = [(-x, sobre_el_peto(x, z), z) for x, z in cabo]
lado_izq = [(x, sobre_el_peto(x, z), z) for x, z in cabo]
tubo("traje", "CintaDeMedir", lado_der[::-1] + [(-0.125, -0.15, 0.725), (-0.11, 0.0, 0.762), (0.0, 0.1, 0.755),
     (0.11, 0.0, 0.762), (0.125, -0.15, 0.725)] + lado_izq, [0.013] * 13, "Cinta", None, seg=8)
for lado, s in (("l", 1), ("r", -1)):
    # manga remangada hasta el codo, con la vuelta más clara; el brazo, de piel, entra en la mano
    tubo("traje", f"Manga_{lado}", [(0.15 * s, -0.003, 0.636), (0.22 * s, -0.003, 0.635), (0.3 * s, -0.003, 0.634)], [0.082, 0.08, 0.079], "Blusa", None, cerrar=False)
    esfera("traje", f"Hombro_{lado}", (0.165 * s, -0.003, 0.64), (0.084, 0.082, 0.082), "Blusa", None)
    toro("traje", f"Vuelta_{lado}", (0.3 * s, -0.003, 0.634), 0.079, 0.02, "BlusaClara", None, rot=(0, R(90), 0))
    tubo("traje", f"Brazo_{lado}", [(0.26 * s, -0.003, 0.634), (0.33 * s, -0.002, 0.634), (0.4 * s, -0.002, 0.632), (0.475 * s, -0.003, 0.629)],
         [0.068, 0.067, 0.068, 0.07], "Piel", None, cerrar=False)
    # la media sube hasta la cadera, por debajo de la falda: al correr la pierna sale por el bajo y antes se veía el hueco
    tubo("traje", f"Media_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.108 * s, 0.0, 0.2), (0.116 * s, 0.003, 0.07)], [0.094, 0.089, 0.082, 0.079], "Media", None, cerrar=False)
    o = torno("traje", f"Zapato_{lado}", [(0.0, 0.08), (0.04, 0.085), (0.085, 0.074), (0.1, 0.078)], ZAPATO, None, seg=24, elip=1.0, cy=0.0)
    for v in o.data.vertices: v.co.x += 0.118 * s
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.086, 0.128, 0.014), ZAPATO, {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.068, 0.088, 0.042), ZAPATO, {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
# ── Moño (accesorio de cabeza) ──
# Va por fuera del pelo (Hair06 llega a 0,38 por detrás): arriba y atrás, con su cinta en la base
esfera("mono", "Mono", (0, 0.37, 1.2), (0.125, 0.12, 0.118), "Pelo", H)
toro("mono", "CintaMono", (0, 0.3, 1.17), 0.085, 0.018, "Delantal", H, rot=(R(-70), 0, 0))
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
rl.pesar_piezas(GRUPOS["mono"], cuerpo, arm, con_cuerpo=False)
tl.revisar_antes_de_exportar([x for g in GRUPOS.values() for x in g], cuerpo, arm)
# Lo del cuerpo de debajo que queda tapado por la ropa se quita (después de copiar los pesos: la ropa los
# toma del cuerpo entero). Del Body05 solo quedan las manos y el torso.
# Los cortes se tapan con «FaldaOscura»: es lo que se ve desde abajo por el bajo de la ropa (ver chequeo_huecos.py).
T.quitar_lo_tapado(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.455 and p.z > 0.55), "FaldaOscura")
cuerpo.name = "Body27_Costurera"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body27_Costurera", extra=cuerpo)
unir(GRUPOS["mono"], "AC13_Mono")
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
