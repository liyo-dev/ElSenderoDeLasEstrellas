# Repesa la coleta de un pelo ya exportado para que su parte baja siga a la espalda y no solo a la cabeza
# (rígida a «head», al echar la cabeza atrás o al correr se metía en la capa). Genérico: cualquier pelo con
# una parte que cuelga por la espalda; lo que queda por detrás de Y_MIN y por debajo de Z_ARRIBA pasa de
# «head» a «spine_03» de forma gradual.
#   args: base.blend  pelo.fbx  salida.blend  [z_arriba z_abajo y_min]    Entorno: COLETA_RIGIDA=0 (reparto vértice a vértice)
import bpy, sys, math
a = sys.argv[sys.argv.index("--") + 1:]
BASE, PELO, SALIDA = a[:3]
Z_ARRIBA, Z_ABAJO, Y_MIN = (float(x) for x in (a[3:6] if len(a) >= 6 else ("0.9", "0.62", "0.25")))
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]
for o in [o for o in bpy.data.objects if o.type == 'MESH']: bpy.data.objects.remove(o)
antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=PELO)
nuevos = [o for o in bpy.data.objects if o not in antes]
pelo = next(o for o in nuevos if o.type == 'MESH')
for o in nuevos:
    if o is not pelo: bpy.data.objects.remove(o)
mw = pelo.matrix_world.copy(); pelo.parent = arm; pelo.matrix_world = mw
for m in pelo.modifiers:
    if m.type == 'ARMATURE': m.object = arm
def suave(t): t = min(1, max(0, t)); return t * t * (3 - 2 * t)
gh = pelo.vertex_groups.get("head") or pelo.vertex_groups.new(name="head")
gs = pelo.vertex_groups.get("spine_03") or pelo.vertex_groups.new(name="spine_03")
# Por en medio pasa por el cuello (19 nov 2026): de «head» a «spine_03» de golpe, al bajar la cabeza (cuello 22° + cabeza 30°)
# el tramo de en medio se estiraba hasta rasgarse (Archimago, Liora y la trenza de Selene en «cabeza_abajo»). Con «neck_01»
# en medio (reparto de Bézier: cabeza (1-w)², cuello 2w(1-w), espalda w²) el giro se reparte a lo largo de la coleta.
gn = pelo.vertex_groups.get("neck_01") or pelo.vertex_groups.new(name="neck_01")
# El casquete (la pieza más grande) se queda en la cabeza: solo se repesa lo que cuelga aparte (la coleta).
import bmesh
bm = bmesh.new(); bm.from_mesh(pelo.data); bm.verts.ensure_lookup_table(); isla, n_isla = {}, 0
for v0 in bm.verts:
    if v0.index in isla: continue
    pila = [v0]
    while pila:
        x = pila.pop()
        if x.index in isla: continue
        isla[x.index] = n_isla; pila += [e.other_vert(x) for e in x.link_edges]
    n_isla += 1
bm.free()
from collections import Counter
mayor = Counter(isla.values()).most_common(1)[0][0]
# Cada isla que cuelga (un eslabón de trenza, una coleta de una pieza) se mueve de una pieza: toda ella lleva los pesos de su
# centro. Repartidos vértice a vértice, al bajar la cabeza la isla se estiraba entre la cabeza y la espalda y se rasgaba.
import os
from mathutils import Vector
RIGIDAS = os.environ.get("COLETA_RIGIDA", "1") == "1"
centro = {}
for v in pelo.data.vertices:
    centro.setdefault(isla[v.index], []).append(pelo.matrix_world @ v.co)
pts_isla = centro
centro = {k: sum(ps, Vector()) / len(ps) for k, ps in pts_isla.items()}
# Lo que ata la coleta (un aro, una cinta: islas pequeñas que se cruzan con ella) va con ella: toma el centro de la isla
# más grande con la que se toca. Si no, al mover la cabeza el aro y la coleta se separaban o se atravesaban (Archimago).
def _caja(ps): return [min(p[i] for p in ps) for i in range(3)], [max(p[i] for p in ps) for i in range(3)]
_cajas = {k: _caja(ps) for k, ps in pts_isla.items()}
def _tocan(a, b): return all(_cajas[a][0][i] <= _cajas[b][1][i] and _cajas[b][0][i] <= _cajas[a][1][i] for i in range(3))
for k in list(centro):
    if k == mayor: continue
    mas = [j for j in centro if j not in (k, mayor) and len(pts_isla[j]) > len(pts_isla[k]) and _tocan(k, j)]
    if mas: centro[k] = centro[max(mas, key=lambda j: len(pts_isla[j]))]
n = 0
for v in pelo.data.vertices:
    if isla[v.index] == mayor: continue
    p = pelo.matrix_world @ v.co
    if RIGIDAS: p = centro[isla[v.index]]   # se decide por isla entera: si no, la raíz se quedaba en la cabeza y se rasgaba
    if p.y < Y_MIN or p.z > Z_ARRIBA: continue
    w = suave((Z_ARRIBA - p.z) / (Z_ARRIBA - Z_ABAJO))
    gh.add([v.index], (1 - w) ** 2, 'REPLACE'); gn.add([v.index], 2 * w * (1 - w), 'REPLACE'); gs.add([v.index], w * w, 'REPLACE'); n += 1
pelo.name = PELO.replace("\\", "/").split("/")[-1].rsplit(".", 1)[0]
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("COLETA", pelo.name, n, "vértices repesados")
