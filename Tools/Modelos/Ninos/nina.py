# Nina, la hija de Liora y el Archimago (niña pequeña, el prólogo): coletas bajas cobrizas con lazos (el
# Hair08 del pack con el flequillo algo más corto y las coletas más grandes) y vestido de aldea: corpiño y
# falda con vuelo color coral, blusa crema con cuello redondo y mangas de farol, delantal crema, medias
# blancas hasta la rodilla y merceditas marrones.
# Sobre base_adulto.blend; al final se le pone la talla de niño pequeño (cuerpo_nino.py).
#   Hair18_Coletas → pelo con los lazos, pesado a «head».   Body25_Vestido → manos de Body05 con la ropa, pesado como él.
#   args: base_adulto.blend, carpeta HeadParts del pack, salida.blend
import bpy, sys, os
D = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=args[0])
exec(open(os.path.join(D, "kit.py")).read())
sys.path.insert(0, D); import cuerpo_nino
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]; cabeza = bpy.data.objects["Head02_Female"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name not in ("Body05", "Head02_Female"): bpy.data.objects.remove(o)

materiales({
    "Pelo": (0.56, 0.26, 0.11, 1), "Lazo": (0.9, 0.32, 0.4, 1),
    "Vestido": (0.86, 0.47, 0.42, 1), "VestidoOscuro": (0.66, 0.3, 0.28, 1), "Blusa": (0.97, 0.93, 0.84, 1),
    "Delantal": (0.96, 0.92, 0.82, 1), "Piel": (1.0, 0.82, 0.7, 1), "Media": (0.97, 0.97, 0.95, 1),
    "Zapato": (0.42, 0.24, 0.14, 1), "Suela": (0.2, 0.12, 0.08, 1),
})

# ── Pelo: Hair08 con el flequillo un poco más corto y las coletas más grandes, y un lazo en cada una ──
pelo = pelo_del_pack(os.path.join(args[1], "Hair08.fbx"), "PeloBase", "Pelo")
C = CABEZA_HUESO + Vector((0, 0.02, 0.1))
NUDO = {s: Vector((0.235 * s, 0.31, 0.79)) for s in (1, -1)}              # donde se atan las coletas (medido en Hair08)
for v in pelo.data.vertices:
    d = v.co - C
    delante = max(0.0, min(1.0, (-d.y - 0.05) / 0.15))
    if v.co.z < 1.1: v.co.z = v.co.z * (1 - delante) + (1.1 - (1.1 - v.co.z) * 0.7) * delante     # flequillo un poco más corto
    s = 1 if v.co.x > 0 else -1
    if abs(v.co.x) > 0.18 and v.co.y > 0.12 and v.co.z < 0.8:                                    # coletas más grandes
        v.co = NUDO[s] + (v.co - NUDO[s]) * 1.2
for s in (1, -1):
    lazo("pelo", f"Lazo{'L' if s > 0 else 'R'}", NUDO[s] + Vector((0.05 * s, -0.02, 0.025)), "Lazo", tam=1.7, girar=math.radians(-55 * s),
         pesos={"head": 1.0})   # Ver INC-975

# ── Ropa (medidas del adulto) ──
toro("traje", "Cuello", (0, -0.012, 0.727), 0.152, 0.02, "Blusa", None, escala=(1, 0.92, 1))
torno("traje", "Corpino", [(0.725, 0.152), (0.67, 0.2), (0.59, 0.208), (0.5, 0.194), (0.46, 0.192)], "Vestido", grosor=0.008)
torno("traje", "Falda", [(0.47, 0.19), (0.43, 0.215), (0.37, 0.25), (0.31, 0.285), (0.26, 0.31)], "Vestido", onda=0.05, n_onda=9, grosor=0.008)
toro("traje", "Bajo", (0, -0.01, 0.262), 0.31, 0.012, "VestidoOscuro", None, escala=(1, 0.92, 1))
toro("traje", "Talle", (0, -0.01, 0.468), 0.196, 0.016, "VestidoOscuro", None, escala=(1, 0.92, 1))
torno("traje", "Delantal", [(0.465, 0.212), (0.42, 0.232), (0.36, 0.264), (0.3, 0.298)], "Delantal", grosor=0.006,
      arco=(math.radians(-150), math.radians(-30)), onda=0.04, n_onda=9)
torno("traje", "Peto", [(0.62, 0.218), (0.55, 0.218), (0.47, 0.205)], "Delantal", grosor=0.006, arco=(math.radians(-125), math.radians(-55)))
for i in range(2):
    esfera("traje", f"Boton{i}", (0, -0.214, 0.69 - 0.04 * i), (0.012, 0.006, 0.012), "VestidoOscuro", None, u=8, v=5)
for lado, s in (("l", 1), ("r", -1)):
    esfera("traje", f"Farol_{lado}", (0.235 * s, -0.003, 0.636), (0.1, 0.088, 0.088), "Blusa", None)
    toro("traje", f"Ribete_{lado}", (0.315 * s, -0.003, 0.635), 0.068, 0.01, "Blusa", None, rot=(0, math.radians(90), 0))
    tubo("traje", f"Brazo_{lado}", [(0.3 * s, -0.003, 0.635), (0.38 * s, -0.003, 0.634), (0.45 * s, -0.003, 0.633)],
         [0.06, 0.054, 0.05], "Piel", None, cerrar=False)
    tubo("traje", f"Pierna_{lado}", [(0.098 * s, 0.003, 0.45), (0.103 * s, 0.0, 0.33), (0.108 * s, 0.0, 0.22)],
         [0.084, 0.078, 0.07], "Piel", None, cerrar=False)
    tubo("traje", f"Media_{lado}", [(0.108 * s, 0.0, 0.225), (0.113 * s, -0.001, 0.14), (0.118 * s, 0.003, 0.06)],
         [0.074, 0.068, 0.066], "Media", None, cerrar=False)
    toro("traje", f"Vuelta_{lado}", (0.108 * s, 0.0, 0.222), 0.074, 0.014, "Media", None)
    torno("traje", f"Zapato_{lado}", [(0.075, 0.072), (0.05, 0.08), (0.02, 0.084)], "Zapato", None, seg=24, elip=1.0, cy=0.0, cx=0.118 * s)
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.01), (0.085, 0.124, 0.012), "Suela", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.121 * s, -0.08, 0.03), (0.07, 0.088, 0.038), "Zapato", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})
    tubo("traje", f"Tira_{lado}", [(0.05 * s + 0.118 * s - 0.05 * s - 0.065, -0.06, 0.055), (0.118 * s, -0.075, 0.068), (0.118 * s + 0.065, -0.06, 0.055)],
         [0.009], "Zapato", None, seg=6)
    esfera("traje", f"Hebilla_{lado}", (0.118 * s + 0.06 * s, -0.062, 0.056), (0.01, 0.008, 0.01), "Media", None, u=8, v=5)

# ── Pesos, recorte del cuerpo de debajo y talla ──
pesar(GRUPOS["pelo"], arm, cuerpo); pesar(GRUPOS["traje"], arm, cuerpo)
recortar_cuerpo(cuerpo, lambda p: p.z < 0.44 or (0.19 < abs(p.x) < 0.445 and p.z > 0.55))
print("PELO SACADO DE LA CARA", sacar_de_la_cara(GRUPOS["pelo"], cabeza))
print("FLEQUILLO PEGADO", pegar_flequillo(GRUPOS["pelo"], cabeza))   # INC-934: de perfil se veía hueco
unir(GRUPOS["pelo"], "Hair18_Coletas")
cuerpo.name = "Body25_Vestido"
unir(GRUPOS["traje"], "Body25_Vestido", extra=cuerpo)
cuerpo_nino.reproporcionar(arm, "pequeno"); arm["talla"] = "pequeno"

# ── Coletas más cortas y abiertas (INC-975): con la talla puesta colgaban hasta la cintura y se metían
# en los hombros y los brazos. Por debajo del nudo se acortan al 58 % y se abren hacia fuera y atrás.
pelo = bpy.data.objects["Hair18_Coletas"]; mw = pelo.matrix_world; mi = mw.inverted()
nudo = {}
for s in (1, -1):
    pts = [mw @ v.co for v in pelo.data.vertices if (mw @ v.co).x * s > 0.17 and 0.50 < (mw @ v.co).z < 0.56]
    nudo[s] = sum(pts, Vector()) / len(pts)
for v in pelo.data.vertices:
    p = mw @ v.co; s = 1 if p.x > 0 else -1
    if abs(p.x) < 0.17 or p.z >= 0.53: continue
    t = nudo[s].z - p.z
    q = Vector((p.x + s * 0.07 * min(1.0, t / 0.3), p.y + 0.03 * min(1.0, t / 0.3), nudo[s].z - t * 0.58))
    v.co = mi @ q
# Los lazos, sobre el eje de la coleta ya abierta (se quedaban delante, INC-975).
lazo_i = {i for p in pelo.data.polygons if pelo.data.materials[p.material_index].name == "Lazo" for i in p.vertices}
for i in lazo_i:
    v = pelo.data.vertices[i]; p = mw @ v.co; s = 1 if p.x > 0 else -1
    v.co = mi @ (p + Vector((0.015 * s, 0.04, -0.01)))
# Las manos del pack traen guante: color de piel (INC-975).
nombres = {g.index: g.name for g in cuerpo.vertex_groups}
mats = [m.name for m in cuerpo.data.materials]
for p in cuerpo.data.polygons:
    v = cuerpo.data.vertices[p.vertices[0]]
    if mats[p.material_index] == "lambert1" and v.groups and nombres[max(v.groups, key=lambda g: g.weight).group].startswith(
            ("hand", "index", "thumb", "middle", "ring", "pinky", "lowerarm")):
        p.material_index = mats.index("Piel")
exec(open(os.path.join(D, "falda.py")).read())   # falda menos abierta (INC-1082)
bpy.ops.wm.save_as_mainfile(filepath=args[2])
print("NINA", [(o.name, len(o.data.vertices)) for o in bpy.data.objects if o.type == 'MESH'])
