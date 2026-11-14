# El Mago Oscuro, vestuario propio: figura imponente de sombra (túnica hasta el suelo, cuello alto, capa rota con
# estrellas apagadas, cuernos de hombro, hebilla de agujero negro, corona de fragmentos). El rostro va tapado por un
# VELO (accesorio aparte, AC14_VeloDeSombra: capucha + gorjal; con él puesto la cabeza va en negro con ojos de luz) que se quita al final para ver la cara.
# Dirección elegida por Raúl el 12 nov 2026 (figura de sombra, sin cara hasta el final).
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body33_MagoOscuro → las manos y el torso de Body05 con la ropa encima, pesado como él.
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







def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]




R = math.radians
import random
random.seed(7)
# ── Colores: negro violáceo, forro violeta que "tiene luz", hueso frío ────────────────────────
COLORES = {
    "Tunica": (0.075, 0.055, 0.13, 1), "Capa": (0.05, 0.038, 0.095, 1), "Forro": (0.30, 0.12, 0.52, 1),
    "Luz": (0.74, 0.45, 1.0, 1), "Hueso": (0.66, 0.62, 0.78, 1), "Vacio": (0.012, 0.008, 0.025, 1),
    "Estrella": (0.40, 0.24, 0.70, 1), "Cinto": (0.12, 0.09, 0.19, 1), "Piel": (0.80, 0.74, 0.78, 1),
}
T = tl.Taller(COLORES, grupos=('traje', 'velo'))   # funciones de modelado comunes (../Prendas/taller.py)
MAT, GRUPOS = T.mat, T.grupos
nueva, transformar, esfera, toro, torno, tubo, caja = T.nueva, T.transformar, T.esfera, T.toro, T.torno, T.tubo, T.caja

# ── Túnica hasta el suelo, con el bajo ondulado (esconde las piernas: sirve cuando levita) ──
# Pesos (ropa_lib): el tronco copia solo del tronco y las piernas; por debajo de la cintura es faldón (pelvis y muslos
# repartidos sin saltos: antes el bajo se inclinaba entero hacia una pierna, como un plato).
FALDON = rl.Faldon(z_cintura=0.40, z_bajo=0.06, muslo=0.55)
ZT = rl.Zona(rl.TRONCO + rl.PIERNAS, FALDON)
# Bajo el brazo no pasa de 0,195 (el cuerpo mide 0,18 y el brazo arranca en 0,20).
PERFIL_TUNICA = [(0.72, 0.15), (0.66, 0.19), (0.55, 0.198), (0.45, 0.2), (0.32, 0.25), (0.18, 0.33), (0.06, 0.42)]
torno("traje", "Tunica", PERFIL_TUNICA, ("Tunica", "Forro"), ZT, grosor=0.01, onda=0.09, n_onda=9)
# Franja violeta que "sangra luz" por el bajo
torno("traje", "BajoLuz", [(0.075, 0.425), (0.035, 0.442)], "Luz", ZT, grosor=0.006, onda=0.09, n_onda=9)
# ── Cinto con el agujero negro como hebilla ──
toro("traje", "Cinto", (0, -0.01, 0.45), 0.205, 0.024, "Cinto", ZT, escala=(1, 0.92, 1))
toro("traje", "AroLuz", (0.0, -0.215, 0.45), 0.052, 0.011, "Luz", {"pelvis": 1.0}, rot=(R(90), 0, 0))   # hebilla rígida
esfera("traje", "Vacio", (0.0, -0.2, 0.45), (0.046, 0.022, 0.046), "Vacio", {"pelvis": 1.0})
# ── Cuello alto en abanico por la espalda, con la punta central más alta (enmarca la cabeza sin taparla) ──
ARCO_CUELLO = (R(26), R(154))
o_cuello = torno("traje", "Cuello", [(0.67, 0.25), (0.8, 0.34), (0.95, 0.42), (1.1, 0.47), (1.24, 0.5)], ("Capa", "Forro"), abierto=ARCO_CUELLO, grosor=0.012,
      pesos={"spine_03": 0.55, "neck_01": 0.45}, cy=0.03)
for v in o_cuello.data.vertices:
    if v.co.z > 1.2:
        ang = math.degrees(math.atan2((v.co.y - 0.03) / 0.92, v.co.x))
        v.co.z += 0.34 * max(0.0, 1 - abs(ang - 90) / 64) ** 1.3
# ── Capa larga y rota por la espalda, con estrellas apagadas ──
# Solo por la espalda (antes de -18° a 198°: llegaba a los costados y los brazos la atravesaban al andar).
ARCO = (R(28), R(152))
PERFIL_CAPA = [(0.74, 0.215), (0.66, 0.262), (0.52, 0.29), (0.36, 0.33), (0.2, 0.39), (0.05, 0.45)]
# Sigue a la espalda y por abajo cuelga como faldón (muslos poco: es una capa, no una falda)
ZC = rl.Zona(rl.TRONCO + rl.PIERNAS, rl.Faldon(z_cintura=0.40, z_bajo=0.05, muslo=0.3), suavizar=8)
o_capa = torno("traje", "Capa", PERFIL_CAPA, ("Capa", "Forro"), ZC, abierto=ARCO, grosor=0.012, onda=0.07, n_onda=7)
def radio_capa(z):
    for (z0, r0), (z1, r1) in zip(PERFIL_CAPA, PERFIL_CAPA[1:]):
        if z1 <= z <= z0: return r1 + (r0 - r1) * (z - z1) / (z0 - z1)
    return PERFIL_CAPA[-1][1]
for k in range(16):   # estrellas apagadas: puntitos de un cielo que ya no brilla
    z = random.uniform(0.14, 0.62); a = R(random.uniform(38, 142)); r = radio_capa(z) * 0.92 + 0.013
    esfera("traje", f"Estrella{k}", (math.cos(a) * r, -0.01 + math.sin(a) * r, z), (0.011, 0.007, 0.011), "Estrella", rl.Zona(sobre=o_capa))
# ── Cuernos de hombro que curvan hacia arriba (silueta) ──
for s in (1, -1):
    tubo("traje", f"Cuerno{s}", curva((0.17 * s, 0.02, 0.69), (0.40 * s, 0.03, 0.74), (0.43 * s, 0.03, 1.02), 9),
         [0.062, 0.056, 0.05, 0.043, 0.035, 0.027, 0.018, 0.01, 0.003], "Hueso", {"spine_03": 1.0}, seg=10)
    esfera("traje", f"Hombro{s}", (0.185 * s, 0.0, 0.655), (0.1, 0.09, 0.085), "Tunica", {"spine_03": 1.0})
# ── Mangas anchas que caen, con puño de luz ──
for lado, s in (("l", 1), ("r", -1)):
    # recogida en la muñeca con el puño de luz ceñido (taller): un aro rígido y ancho se metía en la tripa
    T.manga_con_puno("traje", lado, cuerpo, arm, "Tunica", "Forro", mat_puno="Luz", boca=0.45, hombro_redondo=False)
# ── Cresta de pinchos que nace del borde del cuello (pegados a él, no flotando; siguen al cuello) ──
for k in range(7):
    a = R(42 + 96 * k / 6); r = 0.5
    cima = 0.34 * max(0.0, 1 - abs(math.degrees(a) - 90) / 64) ** 1.3   # altura del borde en ese punto (la punta central es la más alta)
    cx, cy, z = math.cos(a) * r, 0.03 + math.sin(a) * r * 0.92, 1.24 + cima - 0.05
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=5, radius1=0.034, radius2=0.0, depth=0.2 + 0.07 * (1 - abs(k - 3) / 3))
    transformar(bm, (cx, cy, z + 0.1), (1, 0.7, 1), (0, R(26) * math.cos(a), 0))
    nueva("traje", f"Pincho{k}", bm, "Hueso", {"spine_03": 0.55, "neck_01": 0.45})
# ── EL VELO (accesorio aparte: «AC14_VeloDeSombra»; sin él se ve la cara, para el final) ──
# Capucha alta y apuntada que cae a la espalda, con gorjal que tapa la mandíbula del pack.
CC = (0.0, -0.013, 0.99)   # centro de la cabeza del pack
torno("velo", "Capucha", [(0.8, 0.30), (0.9, 0.385), (1.0, 0.415), (1.12, 0.405), (1.24, 0.345), (1.34, 0.23), (1.41, 0.1)], ("Capa", "Forro"),
      abierto=(R(-58), R(238)), grosor=0.012, pesos={"head": 1.0}, cy=0.02, elip=0.95)
tubo("velo", "PuntaCapucha", curva((0.0, 0.03, 1.38), (0.0, 0.2, 1.62), (0.0, 0.5, 1.42), 8), [0.1, 0.085, 0.07, 0.056, 0.042, 0.028, 0.016, 0.006], "Capa", {"head": 1.0}, seg=10)
torno("velo", "Gorjal", [(0.60, 0.2), (0.66, 0.27), (0.725, 0.31)], "Vacio", pesos={"head": 1.0}, seg=32, elip=1.0, cy=-0.013)   # bajo la barbilla: deja a la vista la boca cuando la cara va descubierta
# (La cara negra y los ojos de luz ya no son malla del velo: la cabeza va en negro mientras lleva el velo y los ojos salen
#  del sistema de caras, juego «Caras_mago_oscuro_sombra» Eye300-309; al final se quita el velo y se vuelve a la cabeza de piel
#  con «Caras_mago_oscuro» Eye290-299.)

# ── Pesos y unión ─────────────────────────────────────────────────────────────────────────
def unir(lista, nombre, extra=None):
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o, _ in lista] + ([extra] if extra else [])
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = extra or objs[0]
    bpy.ops.object.join(); bpy.context.object.name = nombre
    return bpy.context.object
# La piel de los brazos usa el material del propio cuerpo (manos y cabeza), con sus UV en el color de piel.
uv_cuerpo = cuerpo.data.uv_layers.active
piel_uv = None
for p in cuerpo.data.polygons:
    if abs((cuerpo.matrix_world @ p.center).x) > 0.5:
        piel_uv = tuple(uv_cuerpo.data[p.loop_indices[0]].uv); break
rl.pesar_piezas(GRUPOS["velo"], cuerpo, arm, con_cuerpo=False)
rl.pesar_piezas(GRUPOS["traje"], cuerpo, arm)
tl.revisar_antes_de_exportar([x for g in GRUPOS.values() for x in g], cuerpo, arm)
# Del Body05 solo quedan las manos y el torso (lo demás lo tapa la ropa), después de copiar los pesos.
# Los cortes se tapan con «Vacio»: es lo que se ve desde abajo por el bajo de la ropa (ver chequeo_huecos.py).
T.quitar_lo_tapado(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.455 and p.z > 0.55), "Vacio")
cuerpo.name = "Body33_MagoOscuro"
unir(GRUPOS["traje"], "Body33_MagoOscuro", extra=cuerpo)
unir(GRUPOS["velo"], "AC14_VeloDeSombra")
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
