# Tobías, el hermano pequeño de Liam (12 años): castaño como él, con el pelo revuelto de crío (el Hair02 del
# pack recogido y pegado a la cabeza, con un remolino en la coronilla), túnica sencilla de estar en casa en tonos tierra (ocre con cuello de cordón, mangas largas con
# puño, un remiendo), cinto de tela teja, pantalón marrón y zapatillas de tela.
# Sobre base_adulto.blend; al final se le pone la talla de niño mayor (cuerpo_nino.py).
#   Hair17_Tobias → pelo, pesado a «head».      Body24_Tobias → manos de Body05 con la ropa, pesado como él.
#   args: base_adulto.blend, carpeta HeadParts del pack, salida.blend
import bpy, sys, os
D = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=args[0])
exec(open(os.path.join(D, "kit.py")).read())
sys.path.insert(0, D); import cuerpo_nino
arm = bpy.data.objects["Armature"]; cuerpo = bpy.data.objects["Body05"]; cabeza = bpy.data.objects["Head01_Male"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name not in ("Body05", "Head01_Male"): bpy.data.objects.remove(o)

materiales({
    "Pelo": (0.3, 0.16, 0.08, 1), "PeloClaro": (0.43, 0.25, 0.12, 1),
    "Tunica": (0.78, 0.6, 0.36, 1), "TunicaOscura": (0.55, 0.38, 0.2, 1), "Remiendo": (0.62, 0.5, 0.36, 1),
    "Cinto": (0.6, 0.27, 0.17, 1), "Cordon": (0.38, 0.24, 0.13, 1),
    "Pantalon": (0.38, 0.27, 0.18, 1), "Zapatilla": (0.3, 0.21, 0.15, 1), "Suela": (0.18, 0.12, 0.08, 1),
})

# ── Pelo: el Hair02 del pack (flequillo suave y despeinado) más pegado a la cabeza, con las alas de los lados
#    recogidas, el flequillo más corto (deja ver las cejas) y un remolino en la coronilla ──
pelo = pelo_del_pack(os.path.join(args[1], "Hair02.fbx"), "PeloBase", "Pelo")
C = CABEZA_HUESO + Vector((0, 0.02, 0.1))
def plegar(z, tope, k): return tope - (tope - z) * k if z < tope else z
for v in pelo.data.vertices:
    d = v.co - C
    if abs(d.x) > 0.36: v.co.x = math.copysign(0.36 + (abs(d.x) - 0.36) * 0.35, d.x)           # alas recogidas
    delante = max(0.0, min(1.0, (-d.y - 0.05) / 0.15))                                           # 1 en la frente
    v.co.z = plegar(v.co.z, 1.11, 0.35) * delante + v.co.z * (1 - delante)                      # flequillo corto: deja ver las cejas
arriba = max(pelo.data.vertices, key=lambda v: v.co.z - abs(v.co.x) - abs(v.co.y - 0.04)).co.copy()
def pico_en(nombre, base, d, largo, ancho, mat):
    d = Vector(d).normalized(); a = d.orthogonal().normalized(); b = d.cross(a); bm = bmesh.new()
    an = [bm.verts.new(base + (a * math.cos(2*math.pi*k/3) + b * math.sin(2*math.pi*k/3)) * ancho) for k in range(3)]
    p = bm.verts.new(base + d * largo)
    for k in range(3): bm.faces.new((an[k], an[(k+1) % 3], p))
    bm.faces.new(an[::-1]); return nueva("pelo", nombre, bm, mat, H, liso=False)
for i, (dx, dy, d, largo) in enumerate(((0.0, 0.06, (0.1, 0.5, 1), 0.15), (-0.04, 0.09, (-0.45, 0.5, 0.9), 0.11), (0.05, 0.1, (0.55, 0.45, 0.8), 0.1))):
    pico_en(f"Remolino{i}", arriba + Vector((dx, dy, -0.035)), d, largo, 0.045, "PeloClaro" if i == 0 else "Pelo")
# ── Ropa (medidas del adulto) ──
torno("traje", "Tunica", [(0.73, 0.15), (0.67, 0.2), (0.59, 0.21), (0.5, 0.198), (0.45, 0.196), (0.37, 0.214), (0.29, 0.226)],
      "Tunica", grosor=0.008)
toro("traje", "Bajo", (0, -0.01, 0.293), 0.226, 0.011, "TunicaOscura", None, escala=(1, 0.92, 1))
toro("traje", "Cuello", (0, -0.01, 0.728), 0.152, 0.014, "TunicaOscura", None, escala=(1, 0.92, 1))
for k in range(3):                                         # el cordón del cuello, cruzado
    z = 0.71 - 0.03 * k
    tubo("traje", f"Cordon{k}", [(-0.022, -0.205 + 0.004 * k, z), (0.022, -0.207 + 0.004 * k, z - 0.024)], [0.006], "Cordon", None, seg=6)
tubo("traje", "Lazo0", [(0.0, -0.21, 0.632), (0.018, -0.212, 0.6)], [0.006], "Cordon", None, seg=6)
tubo("traje", "Lazo1", [(0.0, -0.21, 0.632), (-0.012, -0.212, 0.598)], [0.006], "Cordon", None, seg=6)
esfera("traje", "Remiendo", (-0.15, -0.165, 0.36), (0.05, 0.012, 0.045), "Remiendo", None, rot=(0, 0, math.radians(-38)), u=8, v=4)
toro("traje", "Cinto", (0, -0.01, 0.452), 0.199, 0.019, "Cinto", None, escala=(1, 0.92, 1))
for i, (dx, dz) in enumerate(((0.03, -0.09), (-0.01, -0.075))):
    tubo("traje", f"Nudo{i}", [(0.12, -0.17, 0.45), (0.12 + dx, -0.175, 0.45 + dz)], [0.016, 0.012], "Cinto", None, seg=8)
esfera("traje", "Nudo", (0.12, -0.18, 0.452), (0.025, 0.014, 0.024), "Cinto", None)
for lado, s in (("l", 1), ("r", -1)):
    tubo("traje", f"Manga_{lado}", [(0.17 * s, -0.003, 0.636), (0.3 * s, -0.003, 0.634), (0.43 * s, -0.003, 0.633)],
         [0.074, 0.066, 0.058], "Tunica", None, cerrar=False)
    toro("traje", f"Puno_{lado}", (0.43 * s, -0.003, 0.633), 0.058, 0.012, "TunicaOscura", None, rot=(0, math.radians(90), 0))
    tubo("traje", f"Pernera_{lado}", [(0.098 * s, 0.003, 0.44), (0.103 * s, 0.0, 0.33), (0.11 * s, -0.001, 0.2), (0.118 * s, 0.004, 0.08)],
         [0.092, 0.086, 0.076, 0.07], "Pantalon", None, cerrar=False)
    o = torno("traje", f"Zapatilla_{lado}", [(0.095, 0.074), (0.07, 0.082), (0.03, 0.086)], "Zapatilla", None, seg=24, elip=1.0, cy=0.0, cx=0.118 * s)
    esfera("traje", f"Suela_{lado}", (0.118 * s, -0.03, 0.012), (0.088, 0.128, 0.014), "Suela", {f"foot_{lado}": 0.5, f"ball_{lado}": 0.5})
    esfera("traje", f"Puntera_{lado}", (0.122 * s, -0.08, 0.035), (0.072, 0.092, 0.042), "Zapatilla", {f"ball_{lado}": 0.6, f"foot_{lado}": 0.4})

# ── Pesos, recorte del cuerpo de debajo y talla ──
pesar(GRUPOS["pelo"], arm, cuerpo); pesar(GRUPOS["traje"], arm, cuerpo)
recortar_cuerpo(cuerpo, lambda p: p.z < 0.42 or (0.19 < abs(p.x) < 0.44 and p.z > 0.55))
print("PELO SACADO DE LA CARA", sacar_de_la_cara(GRUPOS["pelo"], cabeza))
unir(GRUPOS["pelo"], "Hair17_Tobias")
cuerpo.name = "Body24_Tobias"
unir(GRUPOS["traje"], "Body24_Tobias", extra=cuerpo)
cuerpo_nino.reproporcionar(arm, "mayor"); arm["talla"] = "mayor"
bpy.ops.wm.save_as_mainfile(filepath=args[2])
print("TOBIAS", [(o.name, len(o.data.vertices)) for o in bpy.data.objects if o.type == 'MESH'])
