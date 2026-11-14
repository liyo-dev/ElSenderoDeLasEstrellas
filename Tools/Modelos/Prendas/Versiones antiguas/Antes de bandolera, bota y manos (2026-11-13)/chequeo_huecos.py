# Chequeo de huecos de un personaje hecho en Blender: lo que se ve desde abajo.
#
# Las cámaras del juego enfocan a menudo desde abajo (volando, tumbado boca arriba en la cama, contrapicados) y
# la ropa hecha por script suele tener por dentro el cuerpo quitado y bocas abiertas (bajo de la túnica, mangas,
# perneras). Unity no pinta el revés de las caras (shader de dibujo con Cull Back), así que donde la cámara ve el
# revés de una cara se ve a través: un hueco con el fondo o el interior vacío del muñeco.
#
# Qué hace: lanza rayos paralelos desde muchas direcciones por debajo (de casi horizontal a vertical, alrededor
# del personaje) y mira los rayos cuyo primer choque es el revés de una cara. Sigue el rayo a través:
#   - HUECO: detrás no hay nada pintado cerca (se ve el fondo, o el interior vacío del muñeco). Es lo grave.
#   - REVÉS: detrás hay otra pieza a poca distancia (p. ej. la cabeza bajo el borde del pelo). Aviso menor.
# Dice qué pieza lo causa y desde dónde se ve peor, y saca fotos desde abajo con el revés pintado en rojo.
#
# Uso (bpy 5.x):
#   python chequeo_huecos.py -- personaje.blend carpeta_salida [--anims carpeta] [--piezas a.fbx,b.fbx] [--sin-fotos]
# La cabeza la toma del FBX de la primera animación (como chequeo_choques.py), porque tapa el cuello.
# Veredicto: NO PASA si desde alguna dirección más de HUECO_GRAVE de lo que se ve del muñeco es hueco.
#
# Es genérico: no sabe nada de ningún personaje concreto. Los umbrales están arriba.
import bpy, sys, os, math, glob, json
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

PASO = 0.008                # separación entre rayos (m)
HUECO_AVISO = 0.003         # parte de lo que se ve que es hueco: aviso
DETRAS_CERCA = 0.06         # lo pintado que hay detrás de un revés a menos de esto (m) tapa el hueco: solo es «revés»
HUECO_GRAVE = 0.015         # parte de lo que se ve que es hueco: no pasa
ELEVACIONES = (-88, -65, -40, -15)      # grados: la cámara por debajo mirando hacia arriba (-90 = desde los pies)
AZIMUTS = range(0, 360, 45)             # alrededor del personaje (0 = de frente; el personaje mira a -Y)
ANIMS_POR_DEFECTO = ["WalkFWD_InPlace_NoWeapon", "Idle_Normal_NoWeapon"]
FOTOGRAMAS_ANDANDO = 3      # además del reposo, unos fotogramas de andar (las piernas abren el bajo)
CABEZA = "Head01_Male"

args = sys.argv[sys.argv.index("--") + 1:]
BLEND, SALIDA = args[0], args[1]
def opcion(nombre, defecto=None):
    return args[args.index(nombre) + 1] if nombre in args else defecto
CARPETA_ANIMS = opcion("--anims", os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..",
                                               "Assets", "Art", "Characters", "RPG Tiny Hero Duo", "Animation", "NoWeapon"))
PIEZAS = [p for p in (opcion("--piezas", "") or "").split(",") if p]
FOTOS = "--sin-fotos" not in args
os.makedirs(SALIDA, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=BLEND)
sc = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.data.pose_position = 'POSE'
mallas = [o for o in bpy.data.objects if o.type == 'MESH']

def enganchar(o):
    mw = o.matrix_world.copy(); o.parent = arm; o.matrix_world = mw
    for m in [m for m in o.modifiers if m.type == 'ARMATURE']: m.object = arm
    if not any(m.type == 'ARMATURE' for m in o.modifiers): o.modifiers.new("Esqueleto", 'ARMATURE').object = arm

def importar(ruta):
    antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=ruta)
    return [o for o in bpy.data.objects if o not in antes]

for p in PIEZAS:
    for o in importar(p):
        if o.type == 'MESH': enganchar(o); mallas.append(o)
        else: bpy.data.objects.remove(o)

todas = glob.glob(os.path.join(CARPETA_ANIMS, "**", "*.fbx"), recursive=True)
rutas = [r for n in ANIMS_POR_DEFECTO for r in todas if os.path.basename(r) == n + ".fbx"]
accion = None
for i, r in enumerate(rutas):
    nuevos = importar(r)
    a2 = next(o for o in nuevos if o.type == 'ARMATURE')
    if accion is None and "Walk" in os.path.basename(r):
        accion = a2.animation_data.action; accion.use_fake_user = True; accion.name = os.path.splitext(os.path.basename(r))[0]
    for o in nuevos:
        if o.type == 'MESH' and o.name.startswith(CABEZA) and not any(m.name.startswith(CABEZA) for m in mallas):
            enganchar(o); mallas.append(o); continue
        bpy.data.objects.remove(o)
arm.animation_data_create()

# ── Malla evaluada de todo el personaje, con la pieza de cada cara ──
def malla_entera():
    dg = bpy.context.evaluated_depsgraph_get()
    vs, fs, quien = [], [], []
    for o in mallas:
        oe = o.evaluated_get(dg); me = oe.to_mesh(); M = o.matrix_world
        lista = str(o.data.get("piezas", "")).split("|")
        at = me.attributes.get("pieza"); pz = [0] * len(me.polygons)
        if at and at.domain == 'FACE': at.data.foreach_get("value", pz)
        off = len(vs)
        vs.extend(M @ v.co for v in me.vertices)
        for p in me.polygons:
            fs.append([off + i for i in p.vertices])
            k = pz[p.index] if at else -1
            quien.append(lista[k] if 0 <= k < len(lista) and lista[k] else o.name.split(".")[0])
        oe.to_mesh_clear()
    return vs, fs, quien

def direccion(elev, azim):
    """Dirección en la que mira la cámara (hacia el personaje), desde abajo."""
    e, a = math.radians(elev), math.radians(azim)
    origen = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))   # dónde está la cámara
    return -origen

def medir(vs, fs, quien):
    bvh = BVHTree.FromPolygons(vs, fs)
    P = np.array([tuple(v) for v in vs]); centro = Vector(P.mean(0)); radio = float(np.linalg.norm(P - P.mean(0), axis=1).max())
    por_vista = {}
    for elev in ELEVACIONES:
        for azim in (AZIMUTS if elev > -85 else [0]):
            d = direccion(elev, azim)
            u = d.orthogonal().normalized(); w = d.cross(u)
            n = int(2 * radio / PASO)
            vistos = huecos = reves = 0; culpa = {}; culpa_reves = {}
            for i in range(n):
                for j in range(n):
                    o = centro - d * (radio + 0.5) + u * (-radio + i * PASO) + w * (-radio + j * PASO)
                    loc, nor, f, _ = bvh.ray_cast(o, d, 2 * radio + 1)
                    if loc is None: continue
                    vistos += 1
                    if nor.dot(d) > 0:      # revés de la cara: en Unity no se pinta; ¿qué se ve detrás?
                        p = loc; detras = None
                        for _ in range(12):
                            l2, n2, f2, _ = bvh.ray_cast(p + d * 1e-4, d, 2 * radio + 1)
                            if l2 is None: break
                            if n2.dot(d) <= 0: detras = (l2 - loc).length; break
                            p = l2
                        if detras is None or detras > DETRAS_CERCA:
                            huecos += 1; culpa[quien[f]] = culpa.get(quien[f], 0) + 1
                        else:
                            reves += 1; culpa_reves[quien[f]] = culpa_reves.get(quien[f], 0) + 1
            por_vista[(elev, azim)] = (vistos, huecos, culpa, reves, culpa_reves)
    return por_vista

poses = [("reposo", None)]
if accion:
    a, b = int(accion.frame_range[0]), int(accion.frame_range[1])
    poses += [(f"{accion.name}@{a + (b - a) * k / FOTOGRAMAS_ANDANDO:.1f}", a + (b - a) * k / FOTOGRAMAS_ANDANDO)
              for k in range(FOTOGRAMAS_ANDANDO)]

def ir_a(t):
    arm.animation_data.action = None
    for pb in arm.pose.bones: pb.matrix_basis = Matrix()
    if t is not None:
        arm.animation_data.action = accion
        try: arm.animation_data.action_slot = accion.slots[0]
        except Exception: pass
        sc.frame_set(int(t), subframe=t - int(t))
    bpy.context.view_layer.update()

resultados = []
for etiqueta, t in poses:
    ir_a(t)
    pv = medir(*malla_entera())
    for (elev, azim), (vistos, huecos, culpa, reves, culpa_reves) in pv.items():
        resultados.append({"pose": etiqueta, "elev": elev, "azim": azim, "vistos": vistos, "huecos": huecos,
                           "parte": huecos / max(1, vistos), "culpa": culpa,
                           "reves": reves, "parte_reves": reves / max(1, vistos), "culpa_reves": culpa_reves})
    print("HUECOS", etiqueta, max(r["parte"] for r in resultados if r["pose"] == etiqueta), flush=True)

# ── Informe ──
peor = max(resultados, key=lambda r: r["parte"])
graves = [r for r in resultados if r["parte"] > HUECO_GRAVE]
avisos = [r for r in resultados if HUECO_AVISO < r["parte"] <= HUECO_GRAVE]
def peores_piezas(clave):
    out = {}
    for r in resultados:
        for k, v in r[clave].items():
            c = out.setdefault(k, [0, None, 0.0])
            frac = v / max(1, r["vistos"])
            if frac > c[2]: c[1] = f'{r["pose"]}, desde abajo a {-r["elev"]}°, giro {r["azim"]}°'; c[2] = frac
            c[0] += v
    return out
culpables = peores_piezas("culpa"); culpables_reves = peores_piezas("culpa_reves")
veredicto = (f"NO PASA ({len(graves)} vistas con hueco)" if graves else
             f"PASA ({len(avisos)} vistas con algún hueco pequeño: mirar las fotos)" if avisos else "PASA")
lineas = [f"# Chequeo de huecos vistos desde abajo — {os.path.basename(BLEND)}", f"**{veredicto}**", "",
          f"Poses: {', '.join(p for p, _ in poses)}. Vistas por pose: {len(resultados) // len(poses)} "
          f"(desde abajo a {', '.join(str(-e) for e in ELEVACIONES)}°). Hueco = la cámara ve el revés de una cara, "
          f"que en Unity no se pinta.", "",
          f"Peor vista: {peor['pose']}, desde abajo a {-peor['elev']}°, giro {peor['azim']}°: "
          f"{100 * peor['parte']:.1f} % de lo que se ve del muñeco es hueco.", "",
          "## Piezas por las que se ve hueco: el fondo o el interior vacío (peor parte de una vista)"]
for k, (tot, donde, frac) in sorted(culpables.items(), key=lambda x: -x[1][2]):
    marca = "**GRAVE** " if frac > HUECO_GRAVE else ""
    lineas.append(f"- {marca}{k}: {100 * frac:.2f} % (peor en {donde})")
if not culpables: lineas.append("- nada")
lineas += ["", "## Revés con algo pintado justo detrás (aviso menor: se ve lo de debajo por el borde)"]
for k, (tot, donde, frac) in sorted(culpables_reves.items(), key=lambda x: -x[1][2]):
    if frac > 0.0005: lineas.append(f"- {k}: {100 * frac:.2f} % (peor en {donde})")
open(os.path.join(SALIDA, "huecos.md"), "w", encoding="utf-8").write("\n".join(lineas))
json.dump(resultados, open(os.path.join(SALIDA, "huecos.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("\n".join(lineas))

# ── Fotos desde abajo: lo que en Unity sería hueco, en rojo ──
if FOTOS:
    PIEL = (0.89, 0.62, 0.42, 1)
    for m in bpy.data.materials:
        color = PIEL if m.name.split(".")[0] == "lambert1" else tuple(m.diffuse_color)
        m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); bsdf.inputs["Base Color"].default_value = color
        rojo = nt.nodes.new("ShaderNodeEmission"); rojo.inputs[0].default_value = (1, 0.02, 0.02, 1); rojo.inputs[1].default_value = 1.5
        geo = nt.nodes.new("ShaderNodeNewGeometry"); mix = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(geo.outputs["Backfacing"], mix.inputs[0]); nt.links.new(bsdf.outputs[0], mix.inputs[1])
        nt.links.new(rojo.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], nt.nodes.new("ShaderNodeOutputMaterial").inputs[0])
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 12; sc.cycles.device = 'CPU'
    sc.render.resolution_x = sc.render.resolution_y = 460
    sc.world = sc.world or bpy.data.worlds.new("W"); sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.78, 0.82, 1)
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.2
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.3
    vistas = sorted([r for r in resultados if r["pose"] == peor["pose"]], key=lambda r: -r["parte"])[:3]
    vistas += [r for r in resultados if r["pose"] == "reposo" and r["elev"] == ELEVACIONES[0]]
    imgs = []
    for r in vistas:
        ir_a(next(t for p, t in poses if p == r["pose"]))
        d = direccion(r["elev"], r["azim"])
        cam.location = Vector((0, 0, 0.55)) - d * 4
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        ruta = os.path.join(SALIDA, f"_h_{len(imgs):02d}.png"); sc.render.filepath = ruta
        bpy.ops.render.render(write_still=True)
        imgs.append((ruta, f'{r["pose"]} - desde abajo {-r["elev"]}°, giro {r["azim"]}° - {100 * r["parte"]:.1f} %'))
    try:
        from PIL import Image, ImageDraw
        W = sc.render.resolution_x
        hoja = Image.new("RGB", (W * len(imgs), W), "white"); dr = ImageDraw.Draw(hoja)
        for i, (ruta, texto) in enumerate(imgs):
            hoja.paste(Image.open(ruta).convert("RGB"), (i * W, 0)); dr.text((i * W + 6, 6), texto, fill="black")
            os.remove(ruta)
        hoja.save(os.path.join(SALIDA, "huecos.png"))
    except ImportError:
        pass
print("FIN", veredicto)
