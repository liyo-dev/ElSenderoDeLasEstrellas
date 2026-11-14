# Oliver, vestuario propio: aprendiz. Túnica de estudiante azul cielo, algo grande (mangas anchas que le
# llegan a la mano, bajo por el muslo), con cuello y ribetes naranjas, cordón a la cintura, pantalón y botas
# (el zurrón cruzado con pergamino se quitó el 14 nov 2026: la mano se metía en él). Propuesta aprobada por Raúl el 11 nov 2026.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = AllBodiesCloaks importado). Mira a -Y; su izquierda es +X.
#   Body29_Aprendiz → las manos y el torso de Body05 con la ropa encima, pesado como él.
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
    "Tunica": (0.50, 0.74, 0.93, 1), "TunicaOscura": (0.3, 0.52, 0.75, 1), "Naranja": (0.96, 0.55, 0.16, 1), "Cordon": (0.93, 0.87, 0.7, 1),
    "Cuero": (0.42, 0.26, 0.14, 1), "CueroOscuro": (0.25, 0.15, 0.08, 1), "Papel": (0.95, 0.91, 0.8, 1), "Pantalon": (0.36, 0.36, 0.42, 1), "Laton": (0.8, 0.62, 0.25, 1),
}







def curva(p0, p1, p2, n):
    return [tuple((1-u)**2 * Vector(p0) + 2*(1-u)*u * Vector(p1) + u*u * Vector(p2)) for u in (i/(n-1) for i in range(n))]




T = tl.Taller(COLORES, grupos=('traje',))   # funciones de modelado comunes (../Prendas/taller.py)
MAT, GRUPOS = T.mat, T.grupos
nueva, transformar, esfera, toro, torno, tubo, caja = T.nueva, T.transformar, T.esfera, T.toro, T.torno, T.tubo, T.caja
R = math.radians
ZAPATO = "CueroOscuro"
# ── Túnica grande: le sobra por todas partes ──
tunica = torno("traje", "Tunica", [(0.725, 0.15), (0.67, 0.205), (0.58, 0.216), (0.5, 0.208), (0.45, 0.21), (0.36, 0.232), (0.29, 0.25)], "Tunica", grosor=0.009)
toro("traje", "Bajo", (0, -0.01, 0.292), 0.249, 0.013, "Naranja", None, escala=(1, 0.92, 1))
# el cuello copia los pesos de la túnica en la que se apoya: con los del cuerpo se separaba de ella al bajar los brazos
torno("traje", "Cuello", [(0.775, 0.17), (0.75, 0.2), (0.715, 0.21)], "Naranja", rl.Zona(sobre=tunica), seg=40, grosor=0.014)
tubo("traje", "Tapeta", curva((0.0, -0.17, 0.72), (0.0, -0.228, 0.6), (0.0, -0.21, 0.47), 8), [0.015] * 8, "Naranja", None, seg=8)
# Cordón a la cintura con el nudo y dos cabos
toro("traje", "Cordon", (0, -0.01, 0.455), 0.211, 0.013, "Cordon", None, escala=(1, 0.92, 1))
esfera("traje", "Nudo", (-0.07, -0.212, 0.455), (0.022, 0.016, 0.022), "Cordon", None)
for k, dx in enumerate((-0.085, -0.055)):
    tubo("traje", f"CaboCordon{k}", curva((-0.07, -0.212, 0.45), (dx, -0.245, 0.4), (dx, -0.25, 0.34 + 0.03 * k), 6), [0.01] * 6, "Cordon", {"pelvis": 1.0}, seg=6)
# Sin zurrón: en el costado se lo comía la mano al andar (decisión de Raúl, 14 nov 2026: sin bolsas ni mochilas)
for lado, s in (("l", 1), ("r", -1)):
    # manga ancha recogida en la muñeca con puño naranja (taller): deja ver la mano y no se mete en la tripa
    T.manga_con_puno("traje", lado, cuerpo, arm, "Tunica", "TunicaOscura", mat_puno="Naranja", boca=0.45, vuelo=0.098)
    # pernera metida en la bota, y bota con la vuelta más clara (taller): si no, se funden al andar
    T.pernera("traje", lado, 0.118, "Pantalon")
    T.bota("traje", lado, 0.118, ZAPATO, mat_vuelta="Cuero")
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.13, 0.014), ZAPATO, {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.123 * s, -0.085, 0.035), (0.07, 0.09, 0.044), ZAPATO, {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
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
cuerpo.name = "Body29_Aprendiz"   # el cuerpo de debajo va dentro: manos y torso
unir(GRUPOS["traje"], "Body29_Aprendiz", extra=cuerpo)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO", [(o.name, len(o.data.vertices), [m.name for m in o.data.materials]) for o in bpy.data.objects if o.type == 'MESH'])
