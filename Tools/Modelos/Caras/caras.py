# Hoja de expresiones de prueba: ojos y bocas nuevos (formas planas pegadas a la cara con shrinkwrap)
# sobre Head01_Male. Cada expresión = un ojo (par) + una boca, como en EmotionProfile.
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=args[0])          # modular.blend
cabeza = bpy.data.objects[args[1]]
MANTENER = len(args) > 6 and args[6] == "mantener"
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o != cabeza and (not MANTENER or o.name.startswith(("Eye", "Mouth"))): bpy.data.objects.remove(o)
def mat(n, c, emis=False):
    m = bpy.data.materials.new(n); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = c; b.inputs["Roughness"].default_value = 0.8
    return m
PIEL = mat("Piel", (1, 0.82, 0.7, 1))
if not MANTENER: cabeza.data.materials.clear(); cabeza.data.materials.append(PIEL)
NEGRO = mat("Negro", (0.08, 0.05, 0.06, 1)); COLOR_IRIS = {"marron": (0.35, 0.2, 0.1, 1), "verde": (0.2, 0.55, 0.25, 1), "azul": (0.22, 0.42, 0.72, 1), "gris": (0.36, 0.43, 0.52, 1), "oscuro": (0.2, 0.12, 0.08, 1), "ambar": (0.68, 0.42, 0.14, 1)}.get(args[7] if len(args) > 7 else "", (0.35, 0.22, 0.12, 1))
IRIS = mat("Iris", COLOR_IRIS); BLANCO = mat("Blanco", (1, 1, 1, 1))
BOCA = mat("Boca", (0.45, 0.1, 0.12, 1)); LENGUA = mat("Lengua", (0.95, 0.45, 0.45, 1)); CEJA = mat("Ceja", (0.25, 0.14, 0.08, 1))
Y0 = -0.45   # plano delante de la cara desde el que se proyecta

def forma(nombre, puntos, material, z_orden=0.0):
    """Polígono plano (x, z) proyectado sobre la cara."""
    bm = bmesh.new()
    for poli in (puntos if isinstance(puntos[0][0], tuple) else [puntos]):   # uno o varios polígonos
        bm.faces.new([bm.verts.new((x, Y0 - z_orden * 0.001, z)) for x, z in poli])
    for f in bm.faces:   # todas mirando hacia delante (-Y): Unity no pinta el revés de las caras
        f.normal_update()
        if f.normal.y > 0: f.normal_flip()
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=2, use_grid_fill=True)
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(material)
    sw = o.modifiers.new("Pegar", 'SHRINKWRAP'); sw.target = cabeza; sw.wrap_method = 'PROJECT'
    sw.use_project_y = True; sw.use_negative_direction = True; sw.use_positive_direction = True; sw.offset = 0.004 + z_orden * 0.002
    return o
def elipse(cx, cz, rx, rz, n=24, a0=0, a1=2 * math.pi):
    return [(cx + rx * math.cos(a0 + (a1 - a0) * i / n), cz + rz * math.sin(a0 + (a1 - a0) * i / n)) for i in range(n + (0 if a1 - a0 >= 2*math.pi else 1))]
def arco(cx, cz, ancho, alto, grosor, n=16, sube=True):
    """Media luna (ojo cerrado feliz ^ o boca sonriente)."""
    s = 1 if sube else -1
    ext = [(cx - ancho + 2 * ancho * i / n, cz + s * alto * math.sin(math.pi * i / n)) for i in range(n + 1)]
    inte = [(x, z - s * grosor * (0.4 + 0.6 * math.sin(math.pi * i / n))) for i, (x, z) in enumerate(ext)]
    return ext + inte[::-1]
def ceja(cx, cz, inclina, lado):
    a = math.radians(inclina) * lado
    pts = [(-0.06, -0.008), (0.06, -0.008), (0.06, 0.008), (-0.06, 0.012)]
    return [(cx + x * math.cos(a) - z * math.sin(a), cz + x * math.sin(a) + z * math.cos(a)) for x, z in pts]

def ceja_anime(cx, cz, inclina, lado, n=12):
    """Ceja de anime: un arco suave, gruesa por dentro y en punta por fuera (u = -1 dentro, 1 fuera).
    Tipo por la variable de entorno CEJAS: fina (por defecto), gruesa (más ancha y con menos punta) o poblada
    (ancha, casi recta y con el borde de arriba a mechones: gente mayor o seria)."""
    import os as _os
    tipo = _os.environ.get("CEJAS", "fina")
    gro, punta, curva = {"gruesa": (1.7, 0.5, 0.8), "poblada": (2.0, 0.65, 0.6)}.get(tipo, (1.0, 0.24, 1.0))
    a = math.radians(inclina)
    arriba, abajo = [], []
    for i in range(n + 1):
        u = -1 + 2 * i / n; t = (u + 1) / 2
        x = 0.05 * u * (1.06 if tipo == "poblada" else 1.0)
        z = (0.008 * (1 - u * u) - 0.004 * max(0.0, u) ** 2) * curva
        g = 0.0125 * ((1 - t) + punta * t) * gro * (1.0 if CHICA else 1.25)   # los chicos, algo más gruesa
        arriba.append((x, z + g / 2)); abajo.append((x, z - g / 2))
        if tipo == "poblada" and i in (5, 8, 11):   # tres mechones que salen hacia arriba y hacia fuera
            arriba.append((x + 0.006, z + g / 2 + 0.0045))
    pts = arriba + abajo[::-1]
    return [(cx + lado * (x * math.cos(a) - z * math.sin(a)), cz + x * math.sin(a) + z * math.cos(a)) for x, z in pts]

# Colocación y tamaño (variables de entorno para otras caras, p. ej. las de niño: ojos más grandes y algo más
# bajos, boca más pequeña y más cerca de los ojos): OJO_X, OJO_Z, BOCA_Z, OJO_ESCALA, BOCA_ESCALA
import os
OJO_X = float(os.environ.get("OJO_X", 0.125)); OJO_Z = float(os.environ.get("OJO_Z", 0.95)); BOCA_Z = float(os.environ.get("BOCA_Z", 0.81))
K = 1.25 * float(os.environ.get("OJO_ESCALA", 1))   # tamaño de los ojos
CHICA = len(args) > 5 and args[5].startswith('chica')
ESTILO = args[5][5:] if len(args) > 5 and args[5][:5] in ('chica', 'chico') else ''   # chicaA, chicaB, chicaC, chicaL, chicoL
KB = 1.6 * float(os.environ.get("BOCA_ESCALA", 1))  # tamaño de las bocas
BRILLA = mat("Brilla", (1.0, 0.85, 0.2, 1))
BRILLA.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = (1.0, 0.8, 0.2, 1)
BRILLA.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 3.0
IRIS_OSC = mat("IrisOscuro", tuple(c * 0.45 for c in COLOR_IRIS[:3]) + (1,))
def superelipse(cx, cz, rx, rz, n=40, p=2.6):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n; c, s_ = math.cos(a), math.sin(a)
        pts.append((cx + rx * math.copysign(abs(c) ** (2 / p), c), cz + rz * math.copysign(abs(s_) ** (2 / p), s_)))
    return pts
import os
from shapely.geometry import Polygon, Point
from shapely import affinity
# Formas del ojo: puntos de control (u hacia FUERA de la cara, v hacia arriba), de -1..1, y su tamaño (rx, rz).
FORMAS_OJO = {
    "redondo":  None,
    "almendra": ([(-1, -0.1), (-0.7, 0.55), (-0.2, 0.85), (0.4, 0.82), (0.85, 0.5), (1.08, 0.2), (0.8, -0.35), (0.3, -0.62), (-0.3, -0.62), (-0.75, -0.42)], 0.072, 0.07),
    "noventa":  ([(-0.95, -0.35), (-0.97, 0.35), (-0.75, 0.82), (-0.15, 1.0), (0.5, 0.97), (0.95, 0.8), (1.18, 0.62), (0.98, 0.1), (0.8, -0.55), (0.35, -0.92), (-0.3, -0.95), (-0.78, -0.72)], 0.064, 0.092),
    # chico (Syaoran, CCS): más ancho que alto, la línea de arriba casi plana y que baja un poco hacia fuera
    "chico":    ([(-1, -0.15), (-0.85, 0.45), (-0.45, 0.8), (0.1, 0.88), (0.6, 0.8), (1.0, 0.52), (1.1, 0.15), (0.85, -0.4), (0.35, -0.75), (-0.25, -0.8), (-0.75, -0.55)], 0.074, 0.07),
    "afilado":  ([(-1, 0.0), (-0.6, 0.55), (0.0, 0.78), (0.6, 0.7), (1.12, 0.42), (0.75, -0.25), (0.2, -0.55), (-0.5, -0.45)], 0.078, 0.06),
}
FORMA_OJO = os.environ.get("OJO_FORMA", "redondo")
def suave(ctrl, n=10):
    """Catmull-Rom cerrado."""
    out = []; m = len(ctrl)
    for i in range(m):
        p0, p1, p2, p3 = ctrl[i - 1], ctrl[i], ctrl[(i + 1) % m], ctrl[(i + 2) % m]
        for k in range(n):
            t = k / n; t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * (2 * p1[j] + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    return out
def contorno(cx, cz, s, esc=1.0):
    f = FORMAS_OJO[FORMA_OJO]
    if f is None:
        return superelipse(cx, cz, 0.06 * esc, 0.085 * esc, 48), 0.06, 0.085
    ctrl, rx, rz = f
    return [(cx + u * rx * esc * s, cz + v * rz * esc) for u, v in suave(ctrl)], rx, rz
def poli(g):
    if g.is_empty: return None
    if g.geom_type == "MultiPolygon": g = max(g.geoms, key=lambda p: p.area)
    if g.geom_type != "Polygon": return None
    return [tuple(p) for p in list(g.exterior.coords)[:-1]]
def ojo_anime(lado, s, tipo):
    """Ojo de anime con forma elegible (OJO_FORMA): línea de arriba gruesa que acaba en punta hacia fuera, iris de dos tonos y dos brillos."""
    cx = OJO_X * s
    pts, rx, rz = contorno(cx, OJO_Z, s)
    if tipo == "cerrados":   # cerrado tranquilo (parpadeo): una línea curvada hacia abajo con su rabito
        forma(f"Ojo_{lado}", arco(cx, OJO_Z - 0.02, rx, 0.022, 0.014, sube=False), NEGRO, 1)
        forma(f"Rabito_{lado}", [(cx + rx * 0.85 * s, OJO_Z - 0.008), (cx + rx * 1.4 * s, OJO_Z + 0.012), (cx + rx * 0.95 * s, OJO_Z - 0.018)], NEGRO, 2)
        return
    if tipo in ("feliz", "guino_cerrado"):
        forma(f"Ojo_{lado}", arco(cx, OJO_Z - 0.01, rx, 0.045, 0.022), NEGRO, 1)
        forma(f"Rabito_{lado}", [(cx + rx * 0.85 * s, OJO_Z - 0.012), (cx + rx * 1.45 * s, OJO_Z + 0.014), (cx + rx * s, OJO_Z + 0.004)], NEGRO, 2)
        return
    caido = {"enfado": 0.3, "cansado": 0.5, "pensar": 0.22}.get(tipo, 0.0)
    inclin = {"enfado": 0.05, "triste": -0.035, "miedo": -0.03}.get(tipo, 0.0)
    tope = OJO_Z + rz * (1 - 2 * caido)
    ojo = Polygon(pts).buffer(0)
    if caido or inclin:   # el párpado baja o se inclina: corta todo lo que quede por encima
        corte = Polygon([(cx - rx * 3, tope - inclin * 3 * s * s), (cx + rx * 3 * s, tope + inclin * 3), (cx + rx * 3 * s, OJO_Z - 1), (cx - rx * 3 * s, OJO_Z - 1)]).buffer(0)
        lim = Polygon([(cx - rx * 3 * s, tope - inclin * 3), (cx + rx * 3 * s, tope + inclin * 3), (cx + rx * 3 * s, OJO_Z - 1), (cx - rx * 3 * s, OJO_Z - 1)]).buffer(0)
        ojo = ojo.intersection(lim)
    def dentro(p): return poli(Polygon(p).buffer(0).intersection(ojo))
    forma(f"Blanco_{lado}", poli(ojo), BLANCO, 0)
    k = {"sorpresa": 0.62, "miedo": 0.5}.get(tipo, 0.86 if CHICA else 0.95)   # los chicos: iris grande que toca el párpado
    irx, irz = 0.06 * k * (1.05 if FORMA_OJO != "redondo" else 1), 0.085 * k * 0.95
    icx, icz = cx - 0.004 * s, OJO_Z - (0.01 if CHICA else 0.004)
    if tipo == "pensar": icx, icz = icx + 0.016, icz + 0.012     # mira arriba y a un lado
    iris = dentro(elipse(icx, icz, irx, irz, 40))
    if iris: forma(f"Iris_{lado}", iris, BRILLA if tipo == "conjuro" else IRIS, 1)
    if tipo != "conjuro":
        arriba = dentro([(icx - irx * 1.2, icz + irz * 0.25), (icx + irx * 1.2, icz + irz * 0.25), (icx + irx * 1.2, icz + irz * 2), (icx - irx * 1.2, icz + irz * 2)])
        if arriba and iris:
            g = Polygon(arriba).intersection(Polygon(iris)); g = poli(g)
            if g: forma(f"IrisArriba_{lado}", g, IRIS_OSC, 2)
        pu = dentro(elipse(icx, icz - 0.004, irx * 0.4, irz * 0.44))
        if pu: forma(f"Pupila_{lado}", pu, NEGRO, 3)
    else:
        est = [(icx + (0.03 if i % 2 == 0 else 0.012) * math.sin(i * math.pi / 5), icz + (0.03 if i % 2 == 0 else 0.012) * math.cos(i * math.pi / 5)) for i in range(10)]
        forma(f"Estrella_{lado}", est, BLANCO, 3)
    b1 = dentro(elipse(cx + 0.02 * s, OJO_Z + rz * 0.35, 0.018, 0.024))
    if b1: forma(f"Brillo_{lado}", b1, BLANCO, 4)
    b2 = dentro(elipse(cx - 0.018 * s, OJO_Z - rz * 0.45, 0.009, 0.01))
    if b2: forma(f"Brillo2_{lado}", b2, BLANCO, 4)
    # línea de arriba: la envolvente de arriba del ojo (para cada x, el punto más alto), engordada y con punta hacia fuera
    from shapely.geometry import LineString
    x0, _, x1, _ = ojo.bounds
    def envolvente(arriba_):
        out = []
        for k in range(1, 30):
            x = x0 + (x1 - x0) * k / 30
            cut = ojo.intersection(LineString([(x, OJO_Z - 1), (x, OJO_Z + 1)]))
            if cut.is_empty: continue
            zs = [c[1] for c in (cut.coords if cut.geom_type == "LineString" else [p for g in cut.geoms for p in g.coords])]
            out.append((x, max(zs) if arriba_ else min(zs)))
        return sorted(out, key=lambda p: p[0] * s)
    top = envolvente(True)
    if len(top) > 3:
        u = lambda x: ((x - cx) * s / rx + 1) / 2                     # 0 por dentro, 1 por fuera
        grueso = (0.005 if FORMA_OJO == "redondo" else 0.0085) * (1.0 if CHICA else 1.25)   # los chicos, línea más gruesa
        arriba = [(x, z + 0.003 + 0.004 * u(x)) for x, z in top]
        abajo = [(x, z - grueso * (0.35 + 0.65 * min(1.0, u(x) * 1.4))) for x, z in top][::-1]
        ult = top[-1]
        punta = (ult[0] + rx * 0.42 * s, ult[1] + rz * 0.22) if CHICA else (ult[0] + rx * 0.3 * s, ult[1] - rz * 0.08)   # chicos: hacia fuera y un poco abajo
        forma(f"Linea_{lado}", arriba + [punta] + abajo, NEGRO, 5)
        if CHICA and FORMA_OJO != "redondo":   # dos pestañas en la esquina de fuera
            for j2, (fr, dx, dz) in enumerate(((0.82, 0.32, 0.3), (0.68, 0.18, 0.36))):
                bx, bz = top[int(fr * (len(top) - 1))]
                forma(f"Pestana{j2}_{lado}", [(bx - rx * 0.06 * s, bz), (bx + rx * dx * s, bz + rz * dz), (bx + rx * 0.08 * s, bz)], NEGRO, 5)
    # línea de abajo: fina, solo la mitad de fuera
    bot = [(x, z) for x, z in envolvente(False) if (x - cx) * s > rx * 0.05]
    if len(bot) > 2:
        forma(f"LineaAbajo_{lado}", [(x, z - 0.001) for x, z in bot] + [(x, z - 0.0055) for x, z in bot][::-1], NEGRO, 5)
    if tipo == "conjuro":
        for k2, (dx, dz) in enumerate(((1.6, 0.9), (-1.5, 1.0), (1.4, -0.8))):
            c2 = (cx + rx * dx * s, OJO_Z + rz * dz)
            forma(f"Chispa{k2}_{lado}", [(c2[0], c2[1] + 0.02), (c2[0] + 0.005, c2[1] + 0.005), (c2[0] + 0.02, c2[1]), (c2[0] + 0.005, c2[1] - 0.005),
                                          (c2[0], c2[1] - 0.02), (c2[0] - 0.005, c2[1] - 0.005), (c2[0] - 0.02, c2[1]), (c2[0] - 0.005, c2[1] + 0.005)], BRILLA, 6)

def ojos(tipo):
    # llorar: ojos cerrados con las cejas de triste (las lágrimas van aparte, en los símbolos Fx)
    gesto = "triste" if tipo == "llorar" else None
    if tipo == "llorar": tipo = "cerrados"
    if ESTILO == "L":
        for lado, s in (("l", 1), ("r", -1)):
            t = "guino_cerrado" if (tipo == "guino" and s == 1) else ("normal" if tipo == "guino" else tipo)
            ojo_anime(lado, s, t)
            # Cejas expresivas: sorpresa, muy arriba; triste y miedo, levantadas por dentro (en «tejadillo»);
            # pensar, una arriba y la otra algo fruncida
            incl = {"triste": -28, "miedo": -30, "enfado": 24, "sorpresa": -12, "feliz": -6, "pensar": -8 if s == 1 else 14}.get(gesto or t, 0)
            alto = {"sorpresa": 0.138, "enfado": 0.095, "triste": 0.114, "miedo": 0.122, "pensar": 0.126 if s == 1 else 0.102}.get(gesto or t, 0.108) - (0 if CHICA else 0.012)   # los chicos, cejas más bajas
            alto += max(0.0, (FORMAS_OJO[FORMA_OJO] or (0, 0, 0.085))[2] - 0.07) * 0.9   # ojos altos (noventa): la ceja sube con ellos
            forma(f"Ceja_{lado}", ceja_anime(OJO_X * s + 0.005 * s, OJO_Z + alto, incl, s), CEJA, 2)
        return
    for lado, s in (("l", 1), ("r", -1)):
        cx = OJO_X * s
        if tipo in ("normal", "sorpresa", "miedo", "triste", "enfado", "pensar", "cansado"):
            rz = {"sorpresa": 0.07, "miedo": 0.068}.get(tipo, 0.062) * K; rx = {"sorpresa": 0.05, "miedo": 0.048}.get(tipo, 0.043) * K
            caido = {"cansado": 0.55, "pensar": 0.3, "enfado": 0.28}.get(tipo, 0.0)
            tope = OJO_Z + rz * (1 - 2 * caido)
            cortar = lambda pts: [(x, min(z, tope)) for x, z in pts]
            forma(f"Blanco_{lado}", cortar(elipse(cx, OJO_Z, rx, rz, 40)), BLANCO, 0)
            ir = 0.6 if tipo in ("sorpresa", "miedo") else 0.82
            dz = 0.014 if tipo == "pensar" else 0
            forma(f"Iris_{lado}", cortar(elipse(cx - 0.004 * s, OJO_Z - 0.006 + dz, rx * ir, rz * ir, 32)), IRIS, 1)
            forma(f"Pupila_{lado}", cortar(elipse(cx - 0.004 * s, OJO_Z - 0.008 + dz, rx * ir * 0.5, rz * ir * 0.5)), NEGRO, 2)
            if tope > OJO_Z + 0.015: forma(f"Brillo_{lado}", elipse(cx + 0.01 * s, min(OJO_Z + 0.024 + dz, tope - 0.012), 0.014, 0.016), BLANCO, 3)
            if CHICA and tope > OJO_Z: forma(f"Brillo2_{lado}", elipse(cx - 0.014 * s, OJO_Z - 0.03, 0.007, 0.008), BLANCO, 3)
            if caido:
                forma(f"Parpado_{lado}", [(cx - rx * 1.15, tope - 0.006), (cx + rx * 1.15, tope - 0.006), (cx + rx * 1.12, tope + 0.008), (cx - rx * 1.12, tope + 0.008)], NEGRO, 4)
            else:
                forma(f"Parpado_{lado}", arco(cx, OJO_Z + rz * 0.75, rx * 1.12, rz * 0.3, 0.022 if CHICA else 0.014), NEGRO, 4)
            if CHICA:
                borde_z = tope if caido else OJO_Z + rz * 0.95
                if ESTILO == "A":
                    # Anime: un bloque de pestañas largas que sale de la esquina de fuera, con tres puntas
                    base = [(cx + rx * 0.3 * s, borde_z + 0.004), (cx + rx * 1.15 * s, borde_z - rz * 0.35)]
                    puntas = [(cx + rx * 0.6 * s, borde_z + 0.03), (cx + rx * 1.15 * s, borde_z + 0.02), (cx + rx * 1.55 * s, borde_z - rz * 0.05)]
                    forma(f"Pestanas_{lado}", [base[0], puntas[0], (cx + rx * 0.85 * s, borde_z + 0.012), puntas[1], (cx + rx * 1.3 * s, borde_z - rz * 0.12), puntas[2], base[1]], NEGRO, 5)
                elif ESTILO == "B":
                    # Rabillo: la línea de arriba se alarga hacia fuera y arriba en una punta (tipo «ojo de gato»)
                    forma(f"Rabillo_{lado}", [(cx + rx * 0.8 * s, borde_z - 0.004), (cx + rx * 1.15 * s, borde_z - rz * 0.3),
                                               (cx + rx * 1.7 * s, borde_z + 0.018), (cx + rx * 1.1 * s, borde_z + 0.012)], NEGRO, 5)
                else:
                    # Pestañas sueltas, largas y curvadas (cinco a lo largo del párpado)
                    for k in range(5):
                        u = 0.15 + 0.85 * k / 4
                        bx = cx + rx * (u * 1.1 - 0.1) * s
                        bz = OJO_Z + rz * math.sqrt(max(0.0, 1 - ((bx - cx) / (rx * 1.12)) ** 2)) * 0.95 if not caido else tope
                        ang = math.radians(70 - 45 * u); largo = 0.022 + 0.018 * u
                        tip = (bx + math.cos(ang) * largo * s, bz + math.sin(ang) * largo)
                        forma(f"Pestana{k}_{lado}", [(bx - 0.006 * s, bz - 0.003), (bx + 0.006 * s, bz + 0.002), tip], NEGRO, 5)
                if not caido:
                    forma(f"PestanaAbajo_{lado}", [(cx + rx * 0.45 * s, OJO_Z - rz * 0.92), (cx + rx * 0.95 * s, OJO_Z - rz * 0.62),
                                                  (cx + rx * 0.98 * s, OJO_Z - rz * 0.55), (cx + rx * 0.45 * s, OJO_Z - rz * 0.86)], NEGRO, 5)
        elif tipo == "cerrados":
            forma(f"Ojo_{lado}", arco(cx, OJO_Z - 0.03, 0.055, 0.018, 0.014, sube=False), NEGRO, 1)
            if CHICA:
                for k, dx in enumerate((0.035, 0.055)):
                    bx = cx + dx * s; bz = OJO_Z - 0.03 - 0.004 * k
                    forma(f"PestanaC{k}_{lado}", [(bx - 0.006 * s, bz), (bx + 0.006 * s, bz - 0.006), (bx + 0.028 * s, bz - 0.018)], NEGRO, 5)
        elif tipo == "feliz":
            forma(f"Ojo_{lado}", arco(cx, OJO_Z - 0.01, 0.055, 0.042, 0.016), NEGRO, 1)
            if CHICA:
                for k, dx in enumerate((0.03, 0.05)):
                    bx = cx + dx * s; bz = OJO_Z - 0.01 + 0.01 + 0.02 * (1 - k)
                    forma(f"PestanaF{k}_{lado}", [(bx - 0.006 * s, bz), (bx + 0.006 * s, bz + 0.006), (bx + 0.03 * s, bz + 0.026 - 0.01 * k)], NEGRO, 5)
        incl = {"triste": -20, "miedo": -18, "enfado": 22, "pensar": 8, "feliz": -5, "sorpresa": -8}.get(gesto or tipo, 0)
        alto = {"sorpresa": 0.125, "miedo": 0.12}.get(gesto or tipo, 0.11) * (K / 1.25)   # la ceja sube con el tamaño del ojo
        forma(f"Ceja_{lado}", ceja(cx + 0.005 * s, OJO_Z + alto, incl, s), CEJA, 2)
FORMA_BASE = forma
def boca(tipo):
    z = BOCA_Z
    def forma(nombre, puntos, material, z_orden=0.0):   # las bocas, escaladas KB alrededor de su centro
        esc = lambda ps: [(x * KB, z + (zz - z) * KB) for x, zz in ps]
        return FORMA_BASE(nombre, [esc(p) for p in puntos] if isinstance(puntos[0][0], tuple) else esc(puntos), material, z_orden)
    if tipo == "neutra": forma("Boca", [(-0.025, z), (0.025, z), (0.025, z + 0.008), (-0.025, z + 0.008)], BOCA, 1)
    elif tipo == "sonrisa": forma("Boca", arco(0, z + 0.012, 0.035, 0.02, 0.01, sube=False), BOCA, 1)
    elif tipo == "risa":
        forma("Boca", arco(0, z + 0.02, 0.045, 0.045, 0.045, sube=False), BOCA, 1)
        forma("Lengua", elipse(0, z - 0.012, 0.022, 0.01), LENGUA, 2)
    elif tipo == "triste": forma("Boca", arco(0, z - 0.008, 0.03, 0.016, 0.009, sube=True), BOCA, 1)
    elif tipo == "o": forma("Boca", elipse(0, z, 0.02, 0.026), BOCA, 1)
    elif tipo == "enfado":
        forma("Boca", [(-0.04, z + 0.01), (0.04, z + 0.01), (0.03, z - 0.016), (-0.03, z - 0.016)], BOCA, 1)
        forma("Dientes", [(-0.036, z + 0.01), (0.036, z + 0.01), (0.034, z + 0.002), (-0.034, z + 0.002)], BLANCO, 2)
    elif tipo == "ondulada":
        pts = [(-0.035 + 0.07 * i / 24, z + 0.006 * math.sin(i * 0.8)) for i in range(25)]
        forma("Boca", [[pts[i], pts[i + 1], (pts[i + 1][0], pts[i + 1][1] + 0.009), (pts[i][0], pts[i][1] + 0.009)] for i in range(24)], BOCA, 1)
    elif tipo == "entreabierta": forma("Boca", elipse(0, z, 0.022, 0.012), BOCA, 1)
    elif tipo == "abierta":
        forma("Boca", elipse(0, z - 0.004, 0.028, 0.026), BOCA, 1)
        forma("Lengua", elipse(0, z - 0.02, 0.018, 0.008), LENGUA, 2)
    elif tipo == "picara": forma("Boca", arco(0.012, z + 0.006, 0.028, 0.012, 0.009, sube=False), BOCA, 1)

import os
ROSA = mat("Rubor", (1.0, 0.55, 0.6, 1)); ROSA_FUERTE = mat("RuborFuerte", (0.95, 0.3, 0.38, 1))
TOSTADO = mat("Tostado", (0.93, 0.6, 0.47, 1)); PECA = mat("Peca", (0.72, 0.44, 0.3, 1)); ARRUGA = mat("Arruga", (0.78, 0.56, 0.48, 1))
def linea(p0, p1, grosor=0.0032):
    """Trazo fino recto entre dos puntos (x, z)."""
    dx, dz = p1[0] - p0[0], p1[1] - p0[1]; l = math.hypot(dx, dz) or 1; nx, nz = -dz / l * grosor / 2, dx / l * grosor / 2
    return [(p0[0] + nx, p0[1] + nz), (p1[0] + nx, p1[1] + nz), (p1[0] - nx, p1[1] - nz), (p0[0] - nx, p0[1] - nz)]
def mejillas(tipo):
    """Uno o varios tipos unidos con «+» (p. ej. «pecas+suave»). Vacío = sin nada.
    suave: dos óvalos rosados. verguenza: óvalos más fuertes y rayitas encima, como la figura de Lina.
    tostada: óvalo tostado, de quien trabaja al sol. pecas: puntitos en las mejillas.
    mayor: rasgos de edad (ojera fina y un trazo corto junto a la nariz)."""
    for t in [x for x in (tipo or "").split("+") if x]: _mejilla(t)
def _mejilla(tipo):
    for lado, s in (("l", 1), ("r", -1)):
        cx, cz = (OJO_X + 0.035) * s, OJO_Z - 0.115 * (K / 1.25)
        if tipo == "tostada":
            forma(f"Mejilla_{lado}", elipse(cx, cz + 0.004, 0.04, 0.017), TOSTADO, 0.5)
        elif tipo == "pecas":
            for k, (dx, dz) in enumerate(((-0.03, 0.008), (-0.012, 0.016), (0.006, 0.006), (-0.02, -0.006), (0.022, 0.016), (0.012, -0.008))):
                forma(f"Peca{k}_{lado}", elipse(cx + dx * s, cz + 0.012 + dz, 0.0042, 0.0042, 10), PECA, 0.6)
        elif tipo == "mayor":
            forma(f"Ojera_{lado}", arco(OJO_X * s, OJO_Z - 0.062 * (K / 1.25), 0.032 * (K / 1.25), 0.008, 0.0035, sube=False), ARRUGA, 0.6)
            forma(f"Surco_{lado}", linea((0.05 * s, BOCA_Z + 0.046), (0.06 * s, BOCA_Z + 0.02), 0.0034), ARRUGA, 0.6)
        if tipo not in ("suave", "verguenza"): continue
        forma(f"Mejilla_{lado}", elipse(cx, cz, 0.042, 0.02), ROSA if tipo == "suave" else ROSA_FUERTE, 0.5)
        if tipo == "verguenza":
            for i in range(3):
                x0 = cx + (-0.022 + 0.022 * i)
                forma(f"Raya{i}_{lado}", [(x0 - 0.008, cz - 0.012), (x0 - 0.004, cz - 0.012), (x0 + 0.008, cz + 0.012), (x0 + 0.004, cz + 0.012)], ROSA_FUERTE if False else NEGRO, 1)
# ── Símbolos de anime (manpu) pegados a la cara ─────────────────────────────────────────────────────────────────
# Cada símbolo es una tira de fotogramas (Fx<NN> en el juego de caras); NPCEmotionController los pasa uno tras otro.
AGUA = mat("Lagrima", (0.4, 0.7, 1.0, 1)); VENA = mat("Vena", (0.86, 0.16, 0.2, 1)); SUSTO = mat("Susto", (0.42, 0.48, 0.86, 1))
SIMBOLOS = [("lagrimas", 6), ("sudor", 3), ("vena", 2), ("susto", 2)]   # (símbolo, fotogramas): el orden de las piezas Fx
def gota(cx, cz, r, punta=2.3):
    """Gota con la punta hacia arriba (lágrima o sudor)."""
    from shapely.geometry import Point
    g = Point(cx, cz).buffer(r, 16).union(Polygon([(cx - r * 0.9, cz + r * 0.42), (cx + r * 0.9, cz + r * 0.42), (cx, cz + r * punta)]))
    return poli(g)
def simbolos(tipo, fotograma=0):
    """Uno o varios con «+», en el fotograma dado (ver SIMBOLOS):
    lagrimas: salen del rabillo de los ojos (cerrados o de risa), bajan por la mejilla y se van; cada lado a su ritmo.
    llanto: dos chorros que bajan de los ojos (de comedia; un solo fotograma).
    sudor: gota grande en la sien que resbala un poco. vena: la cruz de enfado, que late.
    susto: rayitas azules sobre los ojos, que tiemblan."""
    k = K / 1.25
    rx = (FORMAS_OJO[FORMA_OJO] or (None, 0.06, 0))[1]
    for t in [x for x in (tipo or "").split("+") if x]:
        if t == "lagrimas":
            for lado, s in (("l", 1), ("r", -1)):
                fase = (fotograma + (0 if s == 1 else 3)) % 6
                x0, z0 = (OJO_X + rx * 0.62) * s, OJO_Z - 0.04 * k
                forma(f"Charco_{lado}", elipse(x0, z0, 0.01 * k, 0.005 * k), AGUA, 6)   # el rabillo, siempre mojado
                if fase == 5: continue                                                 # una pausa entre lágrima y lágrima
                r = 0.015 * k * (1.0 + 0.12 * min(fase, 2) - 0.22 * max(0, fase - 3))
                x, z = x0 + 0.004 * fase * s, z0 - (0.016 + 0.026 * fase) * k
                forma(f"Lagrima_{lado}", gota(x, z, r), AGUA, 6)
                forma(f"LagrimaBrillo_{lado}", elipse(x - 0.003 * s, z + r * 0.15, r * 0.25, r * 0.35), BLANCO, 7)
        elif t == "llanto":
            for lado, s in (("l", 1), ("r", -1)):
                cx = OJO_X * s
                z0, z1 = OJO_Z - 0.05 * k, BOCA_Z - 0.04
                pts_d, pts_i = [], []
                for i in range(13):
                    u = i / 12; z = z0 + (z1 - z0) * u; w = 0.012 + 0.008 * u
                    ondula = 0.005 * math.sin(u * math.pi * 3 + fotograma * 2.1)
                    pts_d.append((cx + (0.01 + w) * s + ondula, z)); pts_i.append((cx + (0.01 - w) * s + ondula, z))
                forma(f"Llanto_{lado}", pts_d + pts_i[::-1], AGUA, 6)
                forma(f"LlantoBrillo_{lado}", linea((cx + 0.012 * s, z0 - 0.02), (cx + 0.014 * s, z0 - 0.07), 0.003), BLANCO, 7)
        elif t == "sudor":
            cx, cz = (OJO_X + 0.07 * k), OJO_Z + (0.07 - 0.006 * fotograma) * k
            forma("Sudor", gota(cx, cz, 0.026 * k, 2.4), AGUA, 6)
            forma("SudorBrillo", elipse(cx - 0.008, cz + 0.004, 0.006, 0.009), BLANCO, 7)
        elif t == "vena":
            esc = 1.0 + 0.18 * (fotograma % 2)
            cx, cz, r = (OJO_X + 0.06 * k), OJO_Z + 0.12 * k, 0.026 * esc
            for i in range(4):   # cuatro trazos curvos alrededor del centro, abombados hacia dentro: la cruz de enfado
                a = math.radians(90 * i)
                pts = arco(0, r, r * 0.62, r * 0.42, 0.0075 * esc, n=10, sube=False)
                rot = [(cx + x * math.cos(a) - z * math.sin(a), cz + x * math.sin(a) + z * math.cos(a)) for x, z in pts]
                forma(f"Vena{i}", rot, VENA, 6)
        elif t == "susto":
            for i in range(7):
                x = -0.15 + 0.05 * i + (0.003 if (i + fotograma) % 2 else -0.003)
                if abs(x) < 0.012: continue
                top, bot = OJO_Z + 0.16 * k, OJO_Z + (0.04 + 0.03 * ((i + fotograma) % 2)) * k
                forma(f"Susto{i}", [(x - 0.004, top), (x + 0.004, top), (x + 0.0015, bot), (x - 0.0015, bot)], SUSTO, 1.5)
def colmillo():
    z = BOCA_Z
    FORMA_BASE("Colmillo", [(x * KB, z + (zz - z) * KB) for x, zz in [(-0.024, z + 0.02), (-0.012, z + 0.02), (-0.018, z + 0.006)]], BLANCO, 3)
ojos(args[2]); boca(args[3].replace("+colmillo", "")) if "+colmillo" in args[3] else boca(args[3])
if "+colmillo" in args[3]: colmillo()
mejillas(os.environ.get("MEJILLAS", ""))
simbolos(os.environ.get("SIMBOLOS", ""), int(os.environ.get("FOTOGRAMA", 0)))
bpy.ops.wm.save_as_mainfile(filepath=args[4])
