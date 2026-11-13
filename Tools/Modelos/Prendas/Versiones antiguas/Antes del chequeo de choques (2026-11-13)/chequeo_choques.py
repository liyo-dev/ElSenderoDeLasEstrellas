# Chequeo de choques de un personaje hecho en Blender, en movimiento.
#
# Mueve el personaje con animaciones reales del pack (andar, correr, idle, hablar, saludar, victoria) y con poses
# de gesto (brazos bajados, cruzados, mano al frente, brazos arriba) y mide fotograma a fotograma:
#   - DESGARRO: aristas de ropa que se estiran mucho respecto al reposo (tela que se rasga o se abre).
#   - ASOMA: puntos del cuerpo de referencia que en reposo estaban tapados por la ropa y en la pose quedan fuera
#     (pierna que sale por el faldón, brazo que atraviesa la manga, cráneo que asoma por el pelo).
#   - SE MUETE: piezas de ropa que se hunden dentro de otra pieza o del cuerpo más que en reposo
#     (manga dentro del costado, puño dentro del pecho, capa dentro de la manga).
#   - SUELTA: piezas que se separan de la pieza a la que van pegadas (aros de puño, capa, coleta).
# Saca un informe (texto y json) y una hoja de fotos con lo peor de cada cosa marcado en rojo.
#
# Uso (bpy 5.x, p. ej. pip install bpy==5.3.0):
#   python chequeo_choques.py -- personaje.blend carpeta_salida [--anims carpeta] [--piezas a.fbx,b.fbx] [--sin-fotos]
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
CENIDA = 0.02               # prenda a menos de esto de la piel: hace de piel           # hasta dónde se busca ropa por fuera de un punto del cuerpo (m)
DENTRO_TOL = 0.004          # cuánto tiene que estar dentro para contar (m)
DENTRO_RADIO = 0.06         # solo se mira contra superficies cercanas (m)
SUELTA_EXTRA = 0.02
SE_METE_PARTE = 0.10        # parte de la pieza que tiene que meterse (más que en reposo) para avisar
# Graves (hacen que no pase): lo que se ve sí o sí al andar. Lo demás son avisos para mirar en las fotos.
GRAVE_DESGARRO = 10         # aristas rasgadas en una pieza
GRAVE_ASOMA = 15            # puntos del cuerpo fuera por un hueso
GRAVE_SE_METE = 0.30        # parte de una pieza dentro de otra prenda (no del cuerpo: las manos sobre la tripa no cuentan)         # cuánto más lejos que en reposo puede quedar una pieza de su vecina (m)
MUESTRAS_POR_ANIM = int(dict(a.split("=") for a in os.environ.get("CHEQUEO", "").split() if "=" in a).get("muestras", 12))
ANIMS_POR_DEFECTO = ["WalkFWD_InPlace_NoWeapon", "MoveFWD_Normal_InPlace_NoWeapon", "Idle_Normal_NoWeapon",
                     "InteractWithPeople_NoWeapon", "Greeting01_NoWeapon", "Victory_NoWeapon"]
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
}
def poner_pose(nombre):
    pose_reposo()
    for hueso, eje, g in POSES[nombre]:
        for lado, s in (("l", 1), ("r", -1)):
            gg = g * (s if eje in ('Y', 'Z') else 1)
            girar(arm.pose.bones[f"{hueso}_{lado}"], eje, gg)

# ── Mallas evaluadas ───────────────────────────────────────────────────────────────────────────────
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

def medir(C):
    """Medidas de un fotograma. C: coords por objeto."""
    pts, bvh_ref = ref_datos()
    bvh_ropa = bvh_de(ropa_piezas, C)
    tapado = np.zeros(len(pts), bool)
    if bvh_ropa:
        for i, (p, n) in enumerate(pts):
            hit = bvh_ropa.ray_cast(p + n * 0.003, n, ASOMA_RAYO)
            # tapado si hay ropa por fuera, o si una prenda ceñida ocupa su sitio (pernera, manga: el cuerpo
            # de debajo está quitado y la prenda hace de piel)
            tapado[i] = hit[0] is not None or bvh_ropa.find_nearest(p, CENIDA)[0] is not None
    dentro = {}
    for p in ropa_piezas:
        otros, dueno = bvh_de([q for q in ropa_piezas if q is not p], C, True)
        mismos = bvh_de([q for q in ropa_piezas if q is not p and q.obj is p.obj], C)   # «suelta»: solo dentro de la misma pieza exportada
        P = C[p.obj.name][p.idx]
        cnt = 0; dist_min = 1e9; contra = {}
        for x in P:
            x = Vector(x)
            for b, quien in ((otros, lambda f: dueno[f]), (bvh_ref, lambda f: "cuerpo/" + hueso_cara_ref[f])):
                if b is None: continue
                loc, nor, f, d = b.find_nearest(x, DENTRO_RADIO)
                if loc is not None and (x - loc).dot(nor) < -DENTRO_TOL:
                    cnt += 1; q = quien(f); contra[q] = contra.get(q, 0) + 1; break
            if mismos:
                loc, _, _, d = mismos.find_nearest(x)
                if d is not None: dist_min = min(dist_min, d)
        dentro[p.nombre] = (cnt, dist_min, contra)
    return tapado, dentro

def estado(pose):
    return {o.name: coords(o) for o in ropa}

# hueso dominante de cada cara del cuerpo de referencia (para decir «dentro de cuerpo/calf_l»)
_g = {g.index: g.name for g in ref.vertex_groups}
_hv = [(_g.get(max(v.groups, key=lambda x: x.weight).group, "?") if v.groups else "?") for v in ref.data.vertices]
hueso_cara_ref = [_hv[pl.vertices[0]] for pl in ref.data.polygons]
# ── Reposo ──
pose_reposo()
C0 = estado("reposo")
tapado0, dentro0 = medir(C0)
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
    r = {"pose": etiqueta, "desgarro": {}, "asoma": {}, "se_mete": {}, "suelta": {}, "_vert": {}}
    for p in ropa_piezas:
        if not len(p.aristas): continue
        l = np.linalg.norm(C[p.obj.name][p.aristas[:, 0]] - C[p.obj.name][p.aristas[:, 1]], axis=1)
        ok = largo0[p.nombre] > DESGARRO_MIN
        ratio = np.where(ok, l / np.maximum(largo0[p.nombre], 1e-9), 1.0)
        malos = ratio > DESGARRO_RATIO
        if malos.any():
            r["desgarro"][p.nombre] = {"aristas": int(malos.sum()), "max": round(float(ratio.max()), 2)}
            r["_vert"].setdefault(p.obj.name, set()).update(map(int, p.aristas[malos].ravel()))
        c, d, contra = dentro[p.nombre]; c0, d0, contra0 = dentro0[p.nombre]
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
            + sum(v["puntos"] for v in r["se_mete"].values()) + 20 * len(r["suelta"]))
resumen = {"desgarro": {}, "asoma": {}, "se_mete": {}, "suelta": {}}
for f, r in resultados:
    for k in resumen:
        for nombre, val in r[k].items():
            peso = val if isinstance(val, int) else next(iter(val.values()))
            pose = r["pose"] + (f", dentro de {val['en']}" if isinstance(val, dict) and "en" in val else "")
            prev = resumen[k].get(nombre)
            if prev is None or peso > prev[0]: resumen[k][nombre] = (peso, pose)
lineas = [f"# Chequeo de choques — {os.path.basename(BLEND)}", "",
          f"Poses revisadas: {len(resultados)} ({', '.join(a.name for a in acciones)} + {', '.join(POSES)})", ""]
titulos = {"desgarro": "Tela que se rasga (aristas estiradas)", "asoma": "Cuerpo que asoma por la ropa (puntos, por hueso)",
           "se_mete": "Piezas que se meten dentro de otra (puntos)", "suelta": "Piezas que se separan (cm)"}
total = graves = 0
tam = {p.nombre: len(p.idx) for p in ropa_piezas}
def es_grave(k, nombre, peso, pose):
    f = 2 if any(pose.startswith(a) for a in ANIMS_CORRER) else 1
    if k == "desgarro": return peso > GRAVE_DESGARRO * f
    if k == "asoma": return peso > GRAVE_ASOMA * f
    if k == "se_mete": return "dentro de cuerpo" not in pose and peso > GRAVE_SE_METE * tam.get(nombre, 1e9)
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
        for nombre, giro in vistas:
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
