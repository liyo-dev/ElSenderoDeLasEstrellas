# Repesa la coleta de un pelo ya exportado para que su parte baja siga a la espalda y no solo a la cabeza
# (rígida a «head», al echar la cabeza atrás o al correr se metía en la capa). Genérico: cualquier pelo con
# una parte que cuelga por la espalda; lo que queda por detrás de Y_MIN y por debajo de Z_ARRIBA pasa de
# «head» a «spine_03» de forma gradual.
#   args: base.blend  pelo.fbx  salida.blend  [z_arriba z_abajo y_min]
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
n = 0
for v in pelo.data.vertices:
    if isla[v.index] == mayor: continue
    p = pelo.matrix_world @ v.co
    if p.y < Y_MIN or p.z > Z_ARRIBA: continue
    w = suave((Z_ARRIBA - p.z) / (Z_ARRIBA - Z_ABAJO))
    gh.add([v.index], 1 - w, 'REPLACE'); gs.add([v.index], w, 'REPLACE'); n += 1
pelo.name = PELO.replace("\\", "/").split("/")[-1].rsplit(".", 1)[0]
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("COLETA", pelo.name, n, "vértices repesados")
