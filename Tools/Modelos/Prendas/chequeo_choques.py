# Chequeo de choques de un personaje hecho en Blender, en movimiento.
#
# Mueve el personaje con animaciones reales del pack (andar, correr, idle, hablar, saludar, victoria) y con poses
# de gesto (brazos bajados, cruzados, mano al frente, brazos arriba, manos juntas delante del pecho, sentado, sentado hablando) y mide fotograma a fotograma:
#   - DESGARRO: aristas de ropa que se estiran mucho respecto al reposo (tela que se rasga o se abre).
#   - ASOMA: puntos del cuerpo de referencia que en reposo estaban tapados por la ropa y en la pose quedan fuera
#     (pierna que sale por el faldón, brazo que atraviesa la manga, cráneo que asoma por el pelo).
#   - SE MUETE: piezas de ropa que se hunden dentro de otra pieza o del cuerpo más que en reposo
#     (manga dentro del costado, puño dentro del pecho, capa dentro de la manga).
#     Un puño o una boca de manga metidos en el tronco (pelvis, spine) no se perdonan: se ven cortados.
#   - SUELTA: piezas que se separan de la pieza a la que van pegadas (aros de puño, capa, coleta).
#   - PEGADA: tela por encima de otra prenda a menos de PEGADA (peto sobre la blusa, delantal sobre la falda): a unos
#     metros de cámara las dos caras se pelean por el mismo píxel y la de encima parpadea; al moverse, se cruzan.
#   - PIEL EN LA ROPA: la piel propia del modelo (brazos de piel hechos a medida) que se mete en una prenda al moverse:
#     de espaldas con los brazos caídos se veía la camiseta atravesando la carne del brazo (Will, 19 nov 2026). Las
#     manos contra el pecho o la tripa (gestos de charla) solo se avisan.
#   - PELO POR EL SOMBRERO: mechones o picos del pelo que salen por fuera de un gorro o capucha (Liam, 19 nov 2026: un
#     pincho asomaba por encima del gorro). Se mide en todas las poses; pelo y gorro van los dos a «head».
#   - TUMBADO: pose boca arriba con las piernas dobladas y los brazos junto a la cabeza (Will en la cama de Eldran,
#     19 nov 2026) y la animación de dormir del pack; sus fotos se sacan también desde los pies.
#   - CABEZA EN LA ROPA: la cabeza (barbilla, mandíbula, nuca) que se hunde en una prenda del tronco al mover la cabeza
#     (mirar arriba o abajo, girarla). Un cuello alto pesado al pecho que llega a la barbilla corta la cara en cuanto la
#     cabeza se mueve: se ve como una franja que la parte.
# Saca un informe (texto y json) y una hoja de fotos con lo peor de cada cosa marcado en rojo.
#
# Uso (bpy 5.x, p. ej. pip install bpy==5.3.0):
#   python chequeo_choques.py -- personaje.blend carpeta_salida [--anims carpeta] [--piezas a.fbx,b.fbx] [--sin-fotos]
#                                 [--solo Body23_Aventurero]   (solo esas mallas del .blend: un cuerpo cada vez)
#                                 [--fotos-de pose1,pose2] [--vistas frente,lado,...]     (CHEQUEO="muestras=12" en el entorno)
# Veredicto: NO PASA si hay algo grave (tela rasgada, cuerpo que asoma al andar, pieza metida en otra prenda, pieza
# que se suelta); los demás son avisos menores para mirar en las fotos (chequeo.png).
# personaje.blend: el .blend que deja el script del personaje (Armature + la pieza de cuerpo/ropa unida).
# --piezas: otras piezas ya exportadas que van encima (pelo, capucha...), en FBX con el mismo esqueleto.
# Lo que el personaje no lleva (cabeza, cuerpo completo de referencia) lo toma del FBX de la primera animación.
#
# Es genérico: no sabe nada de ningún personaje concreto. Los umbrales están arriba.
import bpy, bmesh, sys, os, math, json, glob
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

DESGARRO_RATIO = 1.6        # arista que se alarga más que esto respecto al reposo
DESGARRO_MIN = 0.006        # aristas más cortas que esto (m) no cuentan
ASOMA_RAYO = 0.25
CENIDA = 0.02               # prenda a menos de esto de la piel: hace de piel (m)
CENIDA_MAX = 0.05           # prenda que en reposo ya hacía de piel a menos de esto: se le exige la misma distancia que en reposo
DENTRO_TOL = 0.004          # cuánto tiene que estar dentro para contar (m)
DENTRO_RADIO = 0.06         # solo se mira contra superficies cercanas (m)
SUELTA_EXTRA = 0.02
SE_METE_PARTE = 0.10        # parte de la pieza que tiene que meterse (más que en reposo) para avisar
PEGADA = 0.003             # tela de encima a menos de esto de la de debajo (m): parpadea en el juego
PEGADA_PARALELA = 0.9       # coseno mínimo entre las dos telas para contar como tendidas una sobre otra
PEGADA_PARTE = 0.12         # parte de la pieza así de pegada (en el peor momento) para avisar y no pasar
PEGADA_MIN = 20             # y al menos estos puntos (los bordes que se tocan no cuentan)
GRAVE_PEGADA = 0.25         # parte de la pieza tendida así sobre otra para que no pase (menos: aviso para mirar en Unity)
MANO = ("hand_", "index_", "thumb_", "middle_", "ring_", "pinky_")   # huesos de la mano
GRAVE_PIEL = 0.06           # parte de una pieza de piel propia metida en la ropa (más que en reposo) para no pasar
PELO_FUERA_TOL = 0.002      # cuánto tiene que salir un punto del pelo por fuera del gorro (m)
CENTRO_CABEZA = Vector((0, -0.013, 0.99))   # centro del cráneo de Head01 en reposo (m)
GORRO_TAPA_MIN = 0.08       # parte de las direcciones desde el centro del cráneo que tapa una pieza para ser gorro
GRAVE_PELO_FUERA = 2        # puntos del pelo por fuera del gorro para no pasar (un pincho ya se ve)
GRAVE_CABEZA = 0.03         # parte de los puntos de la cabeza hundidos en la ropa (más que en reposo) para no pasar
# Graves (hacen que no pase): lo que se ve sí o sí al andar. Lo demás son avisos para mirar en las fotos.
GRAVE_DESGARRO = 10         # aristas rasgadas en una pieza
GRAVE_ASOMA = 15            # puntos del cuerpo fuera por un hueso
GRAVE_SE_METE = 0.30        # parte de una pieza dentro de otra prenda (no del cuerpo: las manos sobre la tripa no cuentan)         # cuánto más lejos que en reposo puede quedar una pieza de su vecina (m)
MUESTRAS_POR_ANIM = int(dict(a.split("=") for a in os.environ.get("CHEQUEO", "").split() if "=" in a).get("muestras", 12))
ANIMS_POR_DEFECTO = ["WalkFWD_InPlace_NoWeapon", "MoveFWD_Normal_InPlace_NoWeapon", "Idle_Normal_NoWeapon",
                     "InteractWithPeople_NoWeapon", "Greeting01_NoWeapon", "Victory_NoWeapon", "Sleeping_NoWeapon"]
ANIMS_CORRER = ["MoveFWD_Normal_InPlace_NoWeapon"]   # al correr se perdona el doble (una túnica larga deja ver las piernas)
CUERPO_REF = "Body05"
CABEZA = "Head01_Male"
HUESOS_PIEL = {"lambert1"}  # materiales que son piel del cuerpo de base (no son ropa)

args = sys.argv[sys.argv.index("--") + 1:]
BLEND, SALIDA = args[0], args[1]
def opcion(nombre, defecto=None):
    return args[args.index(nombre) + 1] if nombre in args else defecto
# por defecto, las animaciones del pack dentro del proyecto (Tools/Modelos/Prendas → Assets/...)
CARPETA_ANIMS = opcion("--anims", os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..",
                                               "Assets", "Art", "Characters", "RPG Tiny Hero Duo", "Animation", "NoWeapon"))
PIEZAS = [p for p in (opcion("--piezas", "") or "").split(",") if p]
FOTOS = "--sin-fotos" not in args
os.makedirs(SALIDA, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=BLEND)
sc = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.data.pose_position = 'POSE'
ropa = [o for o in bpy.data.objects if o.type == 'MESH']
# --solo Body23_Aventurero: el .blend de un personaje lleva a veces dos cuerpos que no se ponen a la vez (Will: con y sin
# chaqueta) y el pelo que hace el script aunque el del juego sea otro: medirlos juntos da «tela sobre tela» falsa
# (una camiseta sobre su gemela). Se mide solo lo que se nombra (más --piezas).
SOLO = [n for n in (opcion("--solo", "") or "").split(",") if n]
if SOLO:
    for o in [o for o in ropa if o.name not in SOLO]: bpy.data.objects.remove(o)
    ropa = [o for o in bpy.data.objects if o.type == 'MESH']

def enganchar(o):
    """Pone una malla importada sobre nuestro esqueleto (mismo esqueleto del pack, misma pose de reposo)."""
    mw = o.matrix_world.copy(); o.parent = arm; o.matrix_world = mw
    for m in [m for m in o.modifiers if m.type == 'ARMATURE']: m.object = arm
    if not any(m.type == 'ARMATURE' for m in o.modifiers): o.modifiers.new("Esqueleto", 'ARMATURE').object = arm

def importar(ruta):
    antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=ruta)
    return [o for o in bpy.data.objects if o not in antes]

for p in PIEZAS:
    for o in importar(p):
        if o.type == 'MESH': enganchar(o); ropa.append(o)
        elif o.type != 'MESH': bpy.data.objects.remove(o)

# Acciones de las animaciones; de la primera se quedan el cuerpo de referencia (oculto) y la cabeza (para las fotos)
acciones, ref, cabeza = [], None, None
todas = glob.glob(os.path.join(CARPETA_ANIMS, "**", "*.fbx"), recursive=True)
rutas = [r for n in ANIMS_POR_DEFECTO for r in todas if os.path.basename(r) == n + ".fbx"] or sorted(todas)[:6]
if not rutas: raise SystemExit(f"No hay animaciones en {CARPETA_ANIMS}")
for i, r in enumerate(rutas):
    nuevos = importar(r)
    a2 = next(o for o in nuevos if o.type == 'ARMATURE')
    act = a2.animation_data.action; act.name = os.path.splitext(os.path.basename(r))[0]; act.use_fake_user = True
    acciones.append(act)
    for o in nuevos:
        if i == 0 and o.type == 'MESH' and o.name.startswith(CUERPO_REF) and ref is None: ref = o; enganchar(o); continue
        if i == 0 and o.type == 'MESH' and o.name.startswith(CABEZA) and cabeza is None: cabeza = o; enganchar(o); continue
        bpy.data.objects.remove(o)
if ref is None: raise SystemExit("No hay cuerpo de referencia (%s) en las animaciones" % CUERPO_REF)
ref.hide_render = True
arm.animation_data_create()

# ── Poses de gesto (las del pack no cruzan brazos ni ponen la mano delante) ─────────────────────────────
def girar(pb, eje, grados):
    h = (arm.matrix_world.inverted() @ arm.matrix_world @ pb.matrix).to_translation()
    R = Matrix.Rotation(math.radians(grados), 4, eje)
    pb.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ pb.matrix
    bpy.context.view_layer.update()
def girar_cadena(nombres, eje, grados):
    """Gira un hueso y arrastra a sus hijos (se gira el primero y los demás ya lo siguen)."""
    girar(arm.pose.bones[nombres], eje, grados)
def pose_reposo():
    arm.animation_data.action = None
    for pb in arm.pose.bones: pb.matrix_basis = Matrix()
    bpy.context.view_layer.update()
POSES = {
    # (hueso, eje del esqueleto, grados) en orden; el lado derecho se hace en espejo. El personaje mira a -Y.
    "brazos_bajados": [("upperarm", 'Y', 72)],
    "mano_al_frente": [("upperarm", 'Y', 62), ("upperarm", 'X', 45), ("lowerarm", 'X', 55)],
    "brazos_cruzados": [("upperarm", 'Y', 68), ("upperarm", 'X', 28), ("lowerarm", 'Z', -95)],
    "brazos_arriba": [("upperarm", 'Y', -55), ("upperarm", 'X', 20)],
    # manos juntas delante del pecho (conjurar, sostener algo): los puños y bocas de manga rozan la tripa y el pecho
    "manos_juntas": [("upperarm", 'Y', 60), ("upperarm", 'X', -45), ("lowerarm", 'X', -50), ("lowerarm", 'Z', -80)],
    # sentado en un banco, con las manos en el regazo (cinemáticas en casa: Eldran, 14 nov 2026)
    "sentado": [("thigh", 'X', -80), ("calf", 'X', 85), ("upperarm", 'Y', 65), ("upperarm", 'X', -25), ("lowerarm", 'X', -45)],
    # sentado hablando con las manos: el tronco se echa adelante y las manos van al centro del regazo, sobre el faldón
    # (Eldran en el filo de la cama, 20 nov 2026: la mano caía sobre la solapa y la punta de la tira parecía un palo)
    "sentado_hablando": [("thigh", 'X', -80), ("calf", 'X', 85), ("spine_02", 'X', 10), ("upperarm", 'Y', 65),
                         ("upperarm", 'X', -35), ("lowerarm", 'X', -60), ("lowerarm", 'Z', -30)],
    # cabeza que se mueve sobre el cuello: lo que permite el avatar del pack (±22° cuello y cabeza) más la mirada de las
    # cinemáticas (DialogueHeadLook). Los huesos sin lado (neck_01, head) se giran una vez, sin espejo.
    "cabeza_arriba": [("neck_01", 'X', -22), ("head", 'X', -30)],
    "cabeza_abajo": [("neck_01", 'X', 22), ("head", 'X', 30)],
    "cabeza_girada": [("neck_01", 'Z', 22), ("head", 'Z', 35), ("head", 'X', -15)],
    # pecho que se dobla y se gira (los gestos de charla del juego lo hacen más que las animaciones del pack): las capas
    # pesadas de forma distinta (peto sobre blusa) se separan o se cruzan
    "tronco_adelante": [("spine_02", 'X', 15), ("spine_03", 'X', 12), ("upperarm", 'Y', 60)],
    "tronco_atras": [("spine_02", 'X', -15), ("spine_03", 'X', -12), ("upperarm", 'Y', 60)],
    "tronco_girado": [("spine_02", 'Z', 15), ("spine_03", 'Z', 12), ("spine_03", 'Y', 8), ("upperarm", 'Y', 60)],
    # tumbado boca arriba en la cama (Will en casa de Eldran, 19 nov 2026: «se cae a la cama… SÚPER ROTO»): piernas
    # dobladas como al sentarse, brazos abiertos con los codos doblados junto a la cabeza y la cabeza hacia atrás.
    # Las fotos de esta pose se sacan también desde los pies (la cámara de la cinemática).
    "tumbado": [("thigh", 'X', -80), ("calf", 'X', 85), ("spine_02", 'X', -8), ("upperarm", 'Y', 45),
                ("upperarm", 'X', -20), ("lowerarm", 'Y', -85), ("neck_01", 'X', -15), ("head", 'X', -15)],
}
def poner_pose(nombre):
    pose_reposo()
    for hueso, eje, g in POSES[nombre]:
        if hueso in arm.pose.bones: girar(arm.pose.bones[hueso], eje, g); continue
        for lado, s in (("l", 1), ("r", -1)):
            gg = g * (s if eje in ('Y', 'Z') else 1)
            girar(arm.pose.bones[f"{hueso}_{lado}"], eje, gg)

# ── Mallas evaluadas ───────────────────────────────────────────────────────────────────────────────
NORMALES = {}   # normales de la última pose medida, por objeto (las llena estado)
def normales(o):
    dg = bpy.context.evaluated_depsgraph_get(); oe = o.evaluated_get(dg); me = oe.to_mesh()
    n = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("normal", n); n = n.reshape(-1, 3)
    R = np.array(o.matrix_world.to_3x3()); n = n @ R.T
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9)
    oe.to_mesh_clear(); return n

def coords(o):
    dg = bpy.context.evaluated_depsgraph_get(); oe = o.evaluated_get(dg); me = oe.to_mesh()
    v = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
    M = np.array(o.matrix_world); v = v @ M[:3, :3].T + M[:3, 3]
    oe.to_mesh_clear(); return v

# Topología (en reposo) de toda la ropa junta: islas, materiales, hueso dominante
class Pieza: pass
piezas = []
for o in ropa:
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    isla = np.full(len(bm.verts), -1); n = 0
    for v in bm.verts:
        if isla[v.index] >= 0: continue
        pila = [v]; isla[v.index] = n
        while pila:
            w = pila.pop()
            for e in w.link_edges:
                u = e.other_vert(w)
                if isla[u.index] < 0: isla[u.index] = n; pila.append(u)
        n += 1
    aristas = np.array([(e.verts[0].index, e.verts[1].index) for e in bm.edges])
    caras = [[v.index for v in f.verts] for f in bm.faces]
    mat_v = np.zeros(len(bm.verts), int); pieza_v = np.full(len(bm.verts), -1)
    at = me.attributes.get("pieza"); pz = [0] * len(me.polygons)
    if at: at.data.foreach_get("value", pz)
    for f in bm.faces:
        for v in f.verts:
            mat_v[v.index] = f.material_index
            if at: pieza_v[v.index] = pz[f.index]
    bm.free()
    grupos = {g.index: g.name for g in o.vertex_groups}
    hueso_v = []
    for v in me.vertices:
        g = max(v.groups, key=lambda x: x.weight, default=None)
        hueso_v.append(grupos.get(g.group, "?") if g else "?")
    for k in range(n):
        idx = np.where(isla == k)[0]
        p = Pieza(); p.obj = o; p.idx = idx
        mats = np.bincount(mat_v[idx]); mi = int(mats.argmax())
        p.mat = me.materials[mi].name if mi < len(me.materials) and me.materials[mi] else "?"
        hs = {}; [hs.__setitem__(hueso_v[i], hs.get(hueso_v[i], 0) + 1) for i in idx]
        p.hueso = max(hs, key=hs.get); p.huesos = hs
        p.piel = p.mat.split(".")[0] in HUESOS_PIEL
        c = np.array([tuple(o.matrix_world @ me.vertices[int(i)].co) for i in idx]).mean(0)
        lista = str(me.get("piezas", "")).split("|")   # nombres de pieza que guarda ropa_lib.etiquetar
        k_p = int(np.bincount(pieza_v[idx][pieza_v[idx] >= 0]).argmax()) if at and (pieza_v[idx] >= 0).any() else -1
        quien = lista[k_p] if 0 <= k_p < len(lista) and lista[k_p] else f"{p.mat}@{p.hueso}"
        if quien == "Cuerpo": p.piel = True
        p.nombre = f"{o.name}:{quien} ({c[0]:+.2f},{c[2]:.2f})"
        sel = np.isin(aristas[:, 0], idx) if len(aristas) else np.array([], bool)
        p.aristas = aristas[sel] if len(aristas) else aristas
        p.caras = [c for c in caras if isla[c[0]] == k]
        piezas.append(p)
# nombres únicos
vistos = {}
for p in piezas:
    vistos[p.nombre] = vistos.get(p.nombre, 0) + 1
    if vistos[p.nombre] > 1: p.nombre += f"#{vistos[p.nombre]}"
ropa_piezas = [p for p in piezas if not p.piel]
piel_piezas = [p for p in piezas if p.piel and len(p.caras)]
def _es_pelo(p):
    n = (p.obj.name + " " + p.nombre + " " + p.mat).lower()
    return p.hueso in ("head", "neck_01") and ("hair" in n or "pelo" in n) and not any(w in n for w in ("hat", "gorro", "capucha", "sombrero"))
def _es_sombrero(p):
    n = (p.obj.name + " " + p.nombre + " " + p.mat).lower()
    return (p.hueso in ("head", "neck_01") and not _es_pelo(p)
            and (any(w in n for w in ("hat", "gorro", "capucha", "sombrero", "casco", "velo")) or p.obj.name.lower().startswith("ac")))
pelo_piezas = [p for p in ropa_piezas if _es_pelo(p)]
GORROS_DE_VERDAD = {}
_n = 400; _ga = math.pi * (3 - math.sqrt(5))
DIRECCIONES_CABEZA = [Vector((math.cos(_ga * k) * math.sqrt(1 - (1 - 2 * (k + 0.5) / _n) ** 2), math.sin(_ga * k) * math.sqrt(1 - (1 - 2 * (k + 0.5) / _n) ** 2), 1 - 2 * (k + 0.5) / _n)) for k in range(_n)]
sombreros = [p for p in ropa_piezas if _es_sombrero(p)]
# lo que cuelga de la cabeza (pelo, sombreros, flequillo) se mueve con ella: no cuenta como ropa que tapa la cara
ropa_bajo_la_cabeza = [p for p in ropa_piezas if p.hueso not in ("head", "neck_01")]

def bvh_de(lista, C, con_dueno=False):
    vs, fs, dueno, off = [], [], [], 0
    for p in lista:
        loc = {int(i): j for j, i in enumerate(p.idx)}
        vs.extend(map(tuple, C[p.obj.name][p.idx]))
        fs.extend([[loc[i] + off for i in c] for c in p.caras]); dueno.extend([p.nombre] * len(p.caras))
        off += len(p.idx)
    b = BVHTree.FromPolygons(vs, fs, all_triangles=False) if fs else None
    return (b, dueno) if con_dueno else b

def ref_datos():
    dg = bpy.context.evaluated_depsgraph_get(); oe = ref.evaluated_get(dg); me = oe.to_mesh()
    M = ref.matrix_world; N = M.to_3x3().inverted().transposed()
    pts = [(M @ v.co, (N @ v.normal).normalized()) for v in me.vertices]
    fs = [list(p.vertices) for p in me.polygons]; vs = [p for p, _ in pts]
    oe.to_mesh_clear(); return pts, BVHTree.FromPolygons(vs, fs, all_triangles=False)

CERCA_EN_REPOSO = None      # distancia de cada punto del cuerpo a la ropa más cercana en reposo (la rellena el reposo)

def medir(C):
    """Medidas de un fotograma. C: coords por objeto."""
    global CERCA_EN_REPOSO
    pts, bvh_ref = ref_datos()
    bvh_ropa = bvh_de(ropa_piezas, C)
    tapado = np.zeros(len(pts), bool)
    reposo = CERCA_EN_REPOSO is None
    if reposo: CERCA_EN_REPOSO = np.full(len(pts), 1e9)
    if bvh_ropa:
        for i, (p, n) in enumerate(pts):
            hit = bvh_ropa.ray_cast(p + n * 0.003, n, ASOMA_RAYO)
            # tapado si hay ropa por fuera, o si una prenda ceñida ocupa su sitio (pernera, manga, media: el cuerpo
            # de debajo está quitado y la prenda hace de piel). Ceñida es a menos de CENIDA o, si en reposo ya había
            # una prenda que hacía de piel algo más fina que el cuerpo del pack (hasta CENIDA_MAX), a la misma
            # distancia que en reposo: una media bajo la falda sigue haciendo de pierna aunque la falda se aparte.
            loc, _, _, d = bvh_ropa.find_nearest(p)
            d = d if loc is not None else 1e9
            if reposo: CERCA_EN_REPOSO[i] = d
            margen = CENIDA if CERCA_EN_REPOSO[i] > CENIDA_MAX else max(CENIDA, CERCA_EN_REPOSO[i] + 0.01)
            tapado[i] = hit[0] is not None or d <= margen
    dentro = {}
    # la piel propia del modelo (brazos de piel hechos a medida, más gruesos que los del pack): la ropa que se mete en
    # ella se ve igual que si se metiera en otra prenda, y el cuerpo de referencia no la tiene
    # (lo que queda del cuerpo del pack, «Cuerpo», ya está en el de referencia)
    # (las manos del pack, con o sin nombre de pieza, tampoco: una mano sobre el cinturón es un gesto, no un choque)
    piel_propia, dueno_piel = bvh_de([q for q in piezas if q.piel and len(q.caras) and ":Cuerpo " not in q.nombre
                                      and not any(h.startswith(MANO) for h in q.huesos)], C, True)
    for p in ropa_piezas:
        otros, dueno = bvh_de([q for q in ropa_piezas if q is not p], C, True)
        mismos = bvh_de([q for q in ropa_piezas if q is not p and q.obj is p.obj], C)   # «suelta»: solo dentro de la misma pieza exportada
        P = C[p.obj.name][p.idx]
        cnt = 0; dist_min = 1e9; contra = {}; pegada = {}
        NP = NORMALES.get(p.obj.name)
        for j, x in enumerate(P):
            x = Vector(x)
            if otros is not None and NP is not None:
                # tela tendida en paralelo justo encima de otra (no un borde que solo la toca: ahí las normales se cruzan)
                loc, nor, f, _ = otros.find_nearest(x, PEGADA * 2)
                if (loc is not None and 0.0 <= (x - loc).dot(nor) < PEGADA
                        and Vector(NP[p.idx[j]]).dot(nor) > PEGADA_PARALELA):
                    q = dueno[f]; pegada[q] = pegada.get(q, 0) + 1
            for b, quien in ((otros, lambda f: dueno[f]), (piel_propia, lambda f: "piel/" + dueno_piel[f].split(":")[-1].split(" (")[0]),
                             (bvh_ref, lambda f: "cuerpo/" + hueso_cara_ref[f])):
                if b is None: continue
                loc, nor, f, d = b.find_nearest(x, DENTRO_RADIO)
                if loc is not None and (x - loc).dot(nor) < -DENTRO_TOL:
                    cnt += 1; q = quien(f); contra[q] = contra.get(q, 0) + 1; break
            if mismos:
                loc, _, _, d = mismos.find_nearest(x)
                if d is not None: dist_min = min(dist_min, d)
        dentro[p.nombre] = (cnt, dist_min, contra, pegada)
    # piel propia (y las manos) dentro de la ropa del cuerpo: la carne del brazo atravesada por la camiseta
    # (lo que cuelga de la cabeza ya lo mide «se mete»)
    b_piel, dueno_p = bvh_de(ropa_bajo_la_cabeza, C, True)
    for p in piel_piezas:
        if b_piel is None: break
        cnt, contra = 0, {}
        for x in C[p.obj.name][p.idx]:
            x = Vector(x); loc, nor, f, _ = b_piel.find_nearest(x, DENTRO_RADIO)
            if loc is not None and (x - loc).dot(nor) < -DENTRO_TOL:
                cnt += 1; q = dueno_p[f]; contra[q] = contra.get(q, 0) + 1
        dentro["piel:" + p.nombre] = (cnt, 0, contra, {})
    # pelo por fuera del gorro: punto del pelo que, mirando desde el centro del cráneo hacia él, tiene el gorro delante
    # (el gorro debería taparlo)
    if pelo_piezas and sombreros:
        hb = arm.pose.bones["head"]   # el centro del cráneo de reposo, llevado con el hueso de la cabeza
        cen = (arm.matrix_world @ hb.matrix) @ (arm.matrix_world @ hb.bone.matrix_local).inverted() @ CENTRO_CABEZA
        # Solo cuenta como gorro lo que tapa la cabeza (una parte de lo que se ve desde el centro del cráneo): la cinta
        # que ata un moño también va en la cabeza, pero el pelo del moño la atraviesa a propósito (Victoria, 19 nov 2026)
        if not GORROS_DE_VERDAD:
            for q in sombreros:
                bq, _ = bvh_de([q], C, True)
                GORROS_DE_VERDAD[q.nombre] = sum(bq.ray_cast(cen, dd, 1.0)[0] is not None for dd in DIRECCIONES_CABEZA) / len(DIRECCIONES_CABEZA)
        de_verdad = [q for q in sombreros if GORROS_DE_VERDAD.get(q.nombre, 1.0) >= GORRO_TAPA_MIN]
    if pelo_piezas and sombreros and de_verdad:
        b, dueno = bvh_de(de_verdad, C, True)
        for p in pelo_piezas:
            fuera, contra = [], {}
            for j, x in enumerate(C[p.obj.name][p.idx]):
                x = Vector(x); d = x - cen; L = d.length
                if L < 1e-6: continue
                hit = b.ray_cast(cen, d / L, L)
                if hit[0] is not None and (L - hit[3]) > PELO_FUERA_TOL:
                    fuera.append(int(p.idx[j])); q = dueno[hit[2]]; contra[q] = contra.get(q, 0) + 1
            dentro["pelo:" + p.nombre] = (len(fuera), 0, contra, {}, fuera)
    return tapado, dentro

def cabeza_en_la_ropa(C):
    """{punto de la cabeza: prenda} de los puntos hundidos en una prenda que no cuelga de ella."""
    if cabeza is None: return {}
    bvh, dueno = bvh_de(ropa_bajo_la_cabeza, C, True)
    if bvh is None: return {}
    dentro = {}
    for i, x in enumerate(coords(cabeza)):
        x = Vector(x)
        loc, nor, f, _ = bvh.find_nearest(x, DENTRO_RADIO)
        if loc is not None and (x - loc).dot(nor) < -DENTRO_TOL: dentro[i] = dueno[f]
    return dentro

def estado(pose):
    NORMALES.clear(); NORMALES.update({o.name: normales(o) for o in ropa})
    return {o.name: coords(o) for o in ropa}

# hueso dominante de cada cara del cuerpo de referencia (para decir «dentro de cuerpo/calf_l»)
_g = {g.index: g.name for g in ref.vertex_groups}
_hv = [(_g.get(max(v.groups, key=lambda x: x.weight).group, "?") if v.groups else "?") for v in ref.data.vertices]
hueso_cara_ref = [_hv[pl.vertices[0]] for pl in ref.data.polygons]
# ── Reposo ──
pose_reposo()
C0 = estado("reposo")
tapado0, dentro0 = medir(C0)
cabeza0 = cabeza_en_la_ropa(C0)
N_CABEZA = len(cabeza.data.vertices) if cabeza is not None else 0
largo0 = {p.nombre: np.linalg.norm(C0[p.obj.name][p.aristas[:, 0]] - C0[p.obj.name][p.aristas[:, 1]], axis=1) if len(p.aristas) else np.array([]) for p in ropa_piezas}
huesos_ref = []
grupos_ref = {g.index: g.name for g in ref.vertex_groups}
for v in ref.data.vertices:
    g = max(v.groups, key=lambda x: x.weight, default=None); huesos_ref.append(grupos_ref.get(g.group, "?") if g else "?")
huesos_ref = np.array(huesos_ref)

# ── Recorrido ──
fotogramas = []
for act in acciones:
    a, b = int(act.frame_range[0]), int(act.frame_range[1])
    for k in range(MUESTRAS_POR_ANIM):
        fotogramas.append((act, a + (b - a) * k / MUESTRAS_POR_ANIM))
for nombre in POSES: fotogramas.append((nombre, None))

def ir_a(f):
    act, t = f
    if isinstance(act, str): poner_pose(act); return act
    pose_reposo(); arm.animation_data.action = act
    try: arm.animation_data.action_slot = act.slots[0]
    except Exception: pass
    sc.frame_set(int(t), subframe=t - int(t)); bpy.context.view_layer.update()
    return f"{act.name}@{t:.1f}"

resultados = []
for f in fotogramas:
    etiqueta = ir_a(f)
    C = estado(etiqueta)
    tapado, dentro = medir(C)
    r = {"pose": etiqueta, "desgarro": {}, "asoma": {}, "se_mete": {}, "suelta": {}, "pegada": {}, "cabeza": {}, "piel": {},
         "pelo": {}, "_vert": {}}
    for p in piel_piezas:
        k = "piel:" + p.nombre
        if k not in dentro: continue
        c, _, contra, _ = dentro[k]; c0, _, contra0, _ = dentro0[k]
        if c - c0 >= max(8, GRAVE_PIEL / 2 * len(p.idx)):
            mas = {q: v - contra0.get(q, 0) for q, v in contra.items() if v - contra0.get(q, 0) > 0}
            en = max(mas, key=mas.get) if mas else "?"
            r["piel"][p.nombre] = {"puntos": int(c - c0), "de": len(p.idx), "en": en.split(":")[-1]}
    for p in pelo_piezas:
        k = "pelo:" + p.nombre
        if k in dentro and dentro[k][0]:
            n, _, contra, _, fuera = dentro[k]
            r["pelo"][p.nombre] = {"puntos": n, "de": len(p.idx), "sobre": max(contra, key=contra.get).split(":")[-1]}
            r["_vert"].setdefault(p.obj.name, set()).update(fuera)
    nuevos = {}
    for i, q in cabeza_en_la_ropa(C).items():
        if i not in cabeza0: nuevos[q] = nuevos.get(q, 0) + 1
    for nombre, n in nuevos.items():
        if n >= 3: r["cabeza"][nombre] = {"puntos": n, "de": N_CABEZA}
    for p in ropa_piezas:
        if not len(p.aristas): continue
        l = np.linalg.norm(C[p.obj.name][p.aristas[:, 0]] - C[p.obj.name][p.aristas[:, 1]], axis=1)
        ok = largo0[p.nombre] > DESGARRO_MIN
        ratio = np.where(ok, l / np.maximum(largo0[p.nombre], 1e-9), 1.0)
        malos = ratio > DESGARRO_RATIO
        if malos.any():
            r["desgarro"][p.nombre] = {"aristas": int(malos.sum()), "max": round(float(ratio.max()), 2)}
            r["_vert"].setdefault(p.obj.name, set()).update(map(int, p.aristas[malos].ravel()))
        c, d, contra, pegada = dentro[p.nombre]; c0, d0, contra0, _ = dentro0[p.nombre]
        if pegada:
            q = max(pegada, key=pegada.get)
            if pegada[q] >= max(PEGADA_MIN, PEGADA_PARTE * len(p.idx)):
                r["pegada"][p.nombre] = {"puntos": pegada[q], "de": len(p.idx), "sobre": q.split(":")[-1]}
        if c - c0 >= max(8, SE_METE_PARTE * len(p.idx)):
            mas = {k: v - contra0.get(k, 0) for k, v in contra.items() if v - contra0.get(k, 0) > 0}
            en = max(mas, key=mas.get) if mas else "?"
            r["se_mete"][p.nombre] = {"puntos": int(c - c0), "de": len(p.idx), "en": en.split(":")[-1]}
        if d0 < 0.01 and d - d0 > SUELTA_EXTRA:
            r["suelta"][p.nombre] = {"separa_cm": round(100 * (d - d0), 1)}
    salen = np.where(tapado0 & ~tapado)[0]
    for h in set(huesos_ref[salen]):
        n = int((huesos_ref[salen] == h).sum())
        if n >= 3: r["asoma"][h] = n
    r["_salen"] = salen
    resultados.append((f, r))
    print("CHEQUEO", etiqueta, {k: len(v) for k, v in r.items() if not k.startswith("_")}, flush=True)

# ── Informe ──
def gravedad(r):
    return (sum(v["aristas"] for v in r["desgarro"].values()) + sum(r["asoma"].values())
            + sum(v["puntos"] for v in r["se_mete"].values()) + 20 * len(r["suelta"])
            + sum(v["puntos"] for v in r["cabeza"].values()) + sum(v["puntos"] for v in r["piel"].values())
            + 20 * sum(v["puntos"] for v in r["pelo"].values()))
resumen = {"desgarro": {}, "asoma": {}, "se_mete": {}, "suelta": {}, "pegada": {}, "cabeza": {}, "piel": {}, "pelo": {}}
for f, r in resultados:
    for k in resumen:
        for nombre, val in r[k].items():
            # un puño contra la cadera con el brazo caído a propósito no es un choque que se vea
            if (k == "se_mete" and r["pose"].startswith(("brazos_bajados",)) and isinstance(val, dict)
                    and any(w in nombre.lower() for w in ("puno", "puño", "brazal", "muñequera"))
                    and any(h in val.get("en", "") for h in ("pelvis", "spine"))): continue
            peso = val if isinstance(val, int) else next(iter(val.values()))
            pose = r["pose"] + (f", dentro de {val['en']}" if isinstance(val, dict) and "en" in val else "") \
                + (f", sobre {val['sobre']}" if isinstance(val, dict) and "sobre" in val else "")
            prev = resumen[k].get(nombre)
            if prev is None or peso > prev[0]: resumen[k][nombre] = (peso, pose)
lineas = [f"# Chequeo de choques — {os.path.basename(BLEND)}", "",
          f"Poses revisadas: {len(resultados)} ({', '.join(a.name for a in acciones)} + {', '.join(POSES)})", ""]
titulos = {"desgarro": "Tela que se rasga (aristas estiradas)", "asoma": "Cuerpo que asoma por la ropa (puntos, por hueso)",
           "se_mete": "Piezas que se meten dentro de otra (puntos)", "suelta": "Piezas que se separan (cm)",
           "pegada": f"Tela tendida sobre otra prenda a menos de {PEGADA * 1000:.0f} mm: parpadea (puntos)",
           "cabeza": "Cabeza que se hunde en la ropa al moverla (puntos de la cabeza)",
           "piel": "Piel propia que se mete en la ropa (puntos: carne atravesada por la tela; las manos solo avisan)",
           "pelo": "Pelo que sale por fuera del gorro o sombrero (puntos)"}
total = graves = 0
tam = {p.nombre: len(p.idx) for p in ropa_piezas}
tam_piel = {p.nombre: len(p.idx) for p in piel_piezas}
_mano = {p.nombre: (":Cuerpo " in p.nombre or any(h.startswith(MANO) for h in p.huesos)) for p in piel_piezas}
def es_mano(nombre): return _mano.get(nombre, False)
PIEZAS_DE_PUNO = ("puno", "puño", "brazal", "muñequera")
POSES_BRAZO_PEGADO = ("brazos_bajados",)   # el brazo descansa sobre la cadera a propósito: ahí no cuenta
HUESOS_DE_TRONCO = ("pelvis", "spine")
def puno_en_el_tronco(k, nombre, pose):
    """Puño o boca de manga metido en el tronco: no se perdona como «manos sobre la tripa» (el aro se ve cortado)."""
    return (k == "se_mete" and any(p in nombre.lower() for p in PIEZAS_DE_PUNO)
            and any(f"dentro de cuerpo/{h}" in pose for h in HUESOS_DE_TRONCO))
PIEZAS_DE_ARO = PIEZAS_DE_PUNO + ("ribete",)
def aro_en_otra_prenda(nombre, pose):
    """Ribete o puño de manga que se hunde en otra prenda (la manga en el costado de la camiseta, por la axila): por la
    espalda se ve la tela del tronco atravesando la manga y el contorno se corta ahí. Ver INC-1309."""
    return (any(p in nombre.lower() for p in PIEZAS_DE_ARO) and "dentro de " in pose
            and "dentro de cuerpo/" not in pose and "dentro de piel/" not in pose)
def es_grave(k, nombre, peso, pose):
    f = 2 if any(pose.startswith(a) for a in ANIMS_CORRER) else 1
    if k == "desgarro": return peso > GRAVE_DESGARRO * f
    if k == "asoma": return peso > GRAVE_ASOMA * f
    if k == "se_mete" and puno_en_el_tronco(k, nombre, pose):
        if pose.startswith(POSES_BRAZO_PEGADO): return False
        return peso > SE_METE_PARTE * f * tam.get(nombre, 1e9)
    if k == "se_mete" and aro_en_otra_prenda(nombre, pose): return peso > SE_METE_PARTE * f * tam.get(nombre, 1e9)
    if k == "se_mete": return "dentro de cuerpo" not in pose and peso > GRAVE_SE_METE * tam.get(nombre, 1e9)
    if k == "pegada": return peso > GRAVE_PEGADA * tam.get(nombre, 1e9)
    if k == "cabeza": return peso > GRAVE_CABEZA * max(1, N_CABEZA)
    if k == "piel": return not es_mano(nombre) and peso > GRAVE_PIEL * f * tam_piel.get(nombre, 1e9)
    if k == "pelo": return peso >= GRAVE_PELO_FUERA
    return k == "suelta"
for k, t in titulos.items():
    lineas.append(f"## {t}")
    if not resumen[k]: lineas.append("- nada"); lineas.append(""); continue
    for nombre, (peso, pose) in sorted(resumen[k].items(), key=lambda x: -x[1][0]):
        g = es_grave(k, nombre, peso, pose); graves += g; total += 1
        lineas.append(f"- {'**GRAVE** ' if g else ''}{nombre}: {peso} (peor en {pose})")
    lineas.append("")
veredicto = (f"NO PASA ({graves} graves, {total} avisos)" if graves else
             f"PASA ({total} avisos menores: mirar las fotos)" if total else "PASA")
lineas.insert(1, f"**{veredicto}**")
open(os.path.join(SALIDA, "chequeo.md"), "w", encoding="utf-8").write("\n".join(lineas))
json.dump([{k: v for k, v in r.items() if not k.startswith("_")} for _, r in resultados],
          open(os.path.join(SALIDA, "chequeo.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n".join(lineas))

# ── Fotos de lo peor, con lo malo en rojo ──
if FOTOS:
    pedidas = [x for x in (opcion("--fotos-de", "") or "").split(",") if x]   # poses concretas (p. ej. para un antes/después)
    if pedidas:
        peores = [next(x for x in resultados if x[1]["pose"].startswith(q)) for q in pedidas]
    else:
        peores = sorted(resultados, key=lambda x: -gravedad(x[1]))[:4]
        peores = [x for x in peores if gravedad(x[1]) > 0] or resultados[:1]
        # tumbado siempre se mira (desde los pies se ve lo que en las otras poses no): 19 nov 2026
        peores += [x for x in resultados if x[1]["pose"] == "tumbado" and x not in peores]
    rojo = bpy.data.materials.new("ChequeoRojo"); rojo.use_nodes = True
    nt = rojo.node_tree; nt.nodes.clear()
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (1, 0.02, 0.02, 1); em.inputs[1].default_value = 1.5
    nt.links.new(em.outputs[0], nt.nodes.new("ShaderNodeOutputMaterial").inputs[0])
    PIEL = (0.89, 0.62, 0.42, 1)
    for m in bpy.data.materials:
        if m is rojo or not m.use_nodes or "Principled BSDF" not in m.node_tree.nodes: continue
        bsdf = m.node_tree.nodes["Principled BSDF"]
        for l in list(bsdf.inputs["Base Color"].links):   # texturas del pack que no están: color liso
            img = getattr(l.from_node, "image", None)
            if img is None or not img.has_data: m.node_tree.links.remove(l)
        bsdf.inputs["Base Color"].default_value = PIEL if m.name.split(".")[0] in HUESOS_PIEL else m.diffuse_color
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 12; sc.cycles.device = 'CPU'
    sc.render.resolution_x, sc.render.resolution_y = 420, 520
    sc.world = sc.world or bpy.data.worlds.new("W"); sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.78, 0.82, 1)
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.2
    sol = bpy.data.objects.new("Sol", bpy.data.lights.new("Sol", 'SUN')); sc.collection.objects.link(sol)
    sol.data.energy = 3; sol.rotation_euler = (math.radians(50), 0, math.radians(30))
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.3
    vistas = [v for v in [("frente", 0), ("tres_cuartos", 40), ("lado", 90), ("espalda", 180)]
              if not opcion("--vistas") or v[0] in opcion("--vistas").split(",")]
    imgs = []
    for f, r in peores:
        ir_a(f)
        marcas = []
        for o in ropa:
            vs = r["_vert"].get(o.name)
            if not vs: continue
            if rojo.name not in [m.name for m in o.data.materials if m]: o.data.materials.append(rojo)
            ri = [m.name for m in o.data.materials].index(rojo.name)
            for poly in o.data.polygons:
                if any(v in vs for v in poly.vertices): marcas.append((o, poly.index, poly.material_index)); poly.material_index = ri
        # el cuerpo que asoma: se enseña el cuerpo de referencia en rojo solo en esos puntos (bolitas)
        bolas = []
        if len(r["_salen"]):
            dg = bpy.context.evaluated_depsgraph_get(); me = ref.evaluated_get(dg).to_mesh()
            for i in r["_salen"][::3]:
                bpy.ops.mesh.primitive_ico_sphere_add(radius=0.012, subdivisions=1, location=ref.matrix_world @ me.vertices[int(i)].co)
                b = bpy.context.object; b.data.materials.append(rojo); bolas.append(b)
            ref.evaluated_get(dg).to_mesh_clear()
        vv = [(("desde_los_pies", None) if v[0] == "tres_cuartos" and r["pose"].startswith("tumbado") else v) for v in vistas]
        for nombre, giro in vv:
            if giro is None:   # tumbado: como la cámara de la cinemática, desde los pies y por debajo
                cam.location = (0.35, -3.2, -2.2); cam.rotation_euler = (math.radians(132), 0, math.radians(6))
            else:
                a = math.radians(giro)
                cam.location = (math.sin(a) * 4, -math.cos(a) * 4, 0.52); cam.rotation_euler = (math.radians(90), 0, a)
            ruta = os.path.join(SALIDA, f"_f_{len(imgs):02d}.png"); sc.render.filepath = ruta
            bpy.ops.render.render(write_still=True); imgs.append((ruta, r["pose"], nombre))
        for o, i, m in marcas: o.data.polygons[i].material_index = m
        for b in bolas: bpy.data.objects.remove(b)
    try:   # hoja con todas las fotos (si hay Pillow); si no, quedan las sueltas
        from PIL import Image, ImageDraw
        W, H = sc.render.resolution_x, sc.render.resolution_y; cols = len(vistas); filas = (len(imgs) + cols - 1) // cols
        hoja = Image.new("RGB", (W * cols, H * filas), "white"); d = ImageDraw.Draw(hoja)
        for i, (ruta, pose, vista) in enumerate(imgs):
            x, y = (i % cols) * W, (i // cols) * H
            hoja.paste(Image.open(ruta).convert("RGB"), (x, y)); d.text((x + 6, y + 6), f"{pose} - {vista}", fill="black")
            os.remove(ruta)
        hoja.save(os.path.join(SALIDA, "chequeo.png"))
    except ImportError:
        pass
print("FIN", veredicto)
