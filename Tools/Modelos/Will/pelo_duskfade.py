# Pelo de héroe de Will: melena rubia barrida hacia atrás, con copete alto, lados cortos y nuca en capas.
# Parte de la malla del pelo actual del juego (Hair16_Heroe) para conservar su estilo de pocas caras, sus
# materiales y sus pesos; no se modela de cero.
#
# Cómo lo hace:
#   1. Se queda solo la cara exterior del pelo (la capa que mira fuera de la cabeza) y tira la concha
#      interior que dejó el grosor anterior. Si se deforman las dos capas por separado se abren y se ve el
#      revés; con una sola capa y un Solidify nuevo al final, el grosor queda limpio.
#   2. Cada pico se peina: los pequeños se recogen contra el casquete y los grandes se ensanchan por los
#      lados, se bajan hacia la cabeza (menos el copete) y se corren hacia atrás o hacia abajo, según dónde
#      estén. Así dejan de ser una estrella radial y se leen como mechones.
#   3. Se da volumen al casquete en la coronilla y la nuca, y se baja el borde delantero en diagonal (más
#      por su derecha) para el flequillo barrido.
#   4. Se vuelve a dar grosor de 6 mm desde la cara exterior y se cuelga del esqueleto del modelo modular.
#
# Entrada: modular.blend (ModularCharacterPolyart.fbx importado tal cual), el Hair16_Heroe.fbx actual y la
# salida. El FBX se exporta con Prendas/exportar_prenda.py y se pasa Caras/chequeo_pelo.py con la línea del
# pelo de Will: `1.19 1.10 0.82`.
#   python pelo_duskfade.py -- modular.blend Hair16_Heroe.fbx salida.blend
import bpy, bmesh, math, sys, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index("--") + 1:]
BASE, PELO_ENTRADA, SALIDA = a[0], a[1], a[2]

# Parámetros del peinado (metros y factores). CORTE: altura a partir de la cual un pico se peina de verdad;
# los más bajos se recogen. PEQ: cuánto se quedan los pequeños. RADIAL/LAY: cuánto baja y cuánto se corre
# cada mechón. ANCHO: cuánto se ensancha. FLECO/VOL: bajada del flequillo y volumen del casquete.
CORTE, PEQ = 0.09, 0.25
RADIAL, LAY, ANCHO = 0.65, 0.80, 1.90
FLECO, VOL = 0.05, 0.015

bpy.ops.wm.open_mainfile(filepath=BASE)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Head01_Male": bpy.data.objects.remove(o)
antes = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=PELO_ENTRADA)
pelo = next(o for o in bpy.data.objects if o not in antes and o.type == 'MESH')
cab = bpy.data.objects["Head01_Male"]
mw = pelo.matrix_world.copy()
bmh = bmesh.new(); bmh.from_mesh(cab.data); bmh.transform(cab.matrix_world)
arbol = BVHTree.FromBMesh(bmh); bmh.free()
C = Vector((0, -0.013, 0.99))

def suave(a, b, x):
    u = min(1.0, max(0.0, (x - a) / (b - a)))
    return u * u * (3 - 2 * u)

def radio(t):
    """Distancia del centro de la cabeza a la piel en la dirección t."""
    h = arbol.ray_cast(C + t * 0.9, -t, 1.0)
    return (h[0] - C).length if h[0] else 0.33

bm = bmesh.new(); bm.from_mesh(pelo.data); bm.transform(mw); bm.faces.ensure_lookup_table()

# 1) Solo la cara exterior. La interior mira hacia la cabeza y queda a pocos milímetros de una cara
#    paralela por fuera; el Solidify del final la vuelve a crear.
matar = [f for f in bm.faces if f.normal.dot((f.calc_center_median() - C).normalized()) < -0.15]
bmesh.ops.delete(bm, geom=matar, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.verts.ensure_lookup_table()

# 2) Picos: altura sobre la piel y grupos conectados por encima de 4 cm.
alto = {}
for v in bm.verts:
    t = (v.co - C).normalized()
    alto[v.index] = (v.co - C).length - radio(t)
vistos = set(); islas = []
for v in bm.verts:
    if v.index in vistos or alto[v.index] < 0.04: continue
    pila = [v]; vistos.add(v.index); isla = []
    while pila:
        g = pila.pop(); isla.append(g)
        for e in g.link_edges:
            w = e.other_vert(g)
            if w.index not in vistos and alto[w.index] >= 0.04:
                vistos.add(w.index); pila.append(w)
    islas.append(isla)

# 3) Peinar cada pico. El flujo se elige por dónde está la punta: atrás en la coronilla y el frente,
#    abajo en la nuca y atrás/abajo en las sienes.
for isla in islas:
    H = max(alto[v.index] for v in isla)
    if H < 0.045: continue
    tip = max(isla, key=lambda v: alto[v.index]); tc = (tip.co - C).normalized()
    if H < CORTE:
        for v in isla:
            t = (v.co - C).normalized()
            v.co = C + t * (radio(t) + alto[v.index] * PEQ)
        continue
    if tc.z > 0.45 or tc.y < -0.1: flujo = Vector((0, 1, 0.3))
    elif tc.y > 0.25: flujo = Vector((0, 0.3, -1))
    else: flujo = Vector((math.copysign(0.4, tc.x), 0.6, -0.4))
    grande = H > 0.3
    for v in isla:
        t = (v.co - C).normalized(); h = alto[v.index]
        u = h / max(H, 1e-5)
        d = v.co - C; dt = d - tc * d.dot(tc)
        # ensanchar (nada en la punta) y bajar/correr el mechón
        v.co += dt * ((1.6 if grande else ANCHO) - 1.0) * max(0.0, 1.0 - u * u)
        t = (v.co - C).normalized(); h = max(0.0, (v.co - C).length - radio(t))
        f = (flujo - t * flujo.dot(t)).normalized()
        hr = 0.035 + max(0.0, h - 0.035) * (0.7 if grande else RADIAL)
        v.co = C + t * (radio(t) + hr) + f * (h * (0.7 if grande else LAY))

# 4) Volumen del casquete por detrás y en la coronilla, para que no se vea el casco liso entre mechón y mechón.
for v in bm.verts:
    t = (v.co - C).normalized(); h = (v.co - C).length - radio(t)
    if h < 0.12:
        vol = VOL * suave(0.15, 0.55, t.y) * suave(0.05, 0.4, t.z) + 0.6 * VOL * suave(0.35, 0.75, t.z)
        v.co += t * vol * suave(0.0, 0.03, h)

# 5) Flequillo: baja el borde delantero y un poco más por su derecha (-X), para que la línea quede en diagonal.
for v in bm.verts:
    t = (v.co - C).normalized()
    if t.y < -0.45 and -0.05 < t.z < 0.5 and alto[v.index] < 0.1:
        v.co.z -= 0.03 + FLECO * max(0.0, -t.x / 0.32)

bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bm.transform(mw.inverted()); bm.to_mesh(pelo.data); bm.free()

# 6) Grosor nuevo desde la cara exterior y enganche al esqueleto del modelo modular (el que se exporta).
s = pelo.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = 0.006; s.offset = -1; s.use_rim = True
bpy.context.view_layer.objects.active = pelo
bpy.ops.object.modifier_apply(modifier=s.name)
arm = bpy.data.objects["Armature"]
mw2 = pelo.matrix_world.copy()
pelo.parent = None
for m in list(pelo.modifiers):
    if m.type == 'ARMATURE': pelo.modifiers.remove(m)
pelo.parent = arm
pelo.matrix_parent_inverse = arm.matrix_world.inverted()
pelo.matrix_world = mw2
pelo.modifiers.new("Esqueleto", 'ARMATURE').object = arm
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("PELO", pelo.name, len(pelo.data.vertices), "vértices,", len(pelo.data.polygons), "caras;",
      len(matar), "caras interiores quitadas;", len(islas), "picos")
