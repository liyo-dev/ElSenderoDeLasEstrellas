# Piezas del Cap. 4 hechas en Blender (bpy). Uso:
#   python cap4.py <carpeta de salida>
# Saca CampanaDeLaPrueba.fbx y CharcoDeBarro.fbx (1 unidad = 1 m; en Unity: Y arriba).
# Convención (la de Tools/Modelos/Pueblo/pueblo.py): X a lo ancho, Z (Blender) arriba,
# la cara mira a -Y (Blender) = +Z en Unity.
import bpy, bmesh, sys, os, math, random
from mathutils import Vector

OUT = sys.argv[-1]
os.makedirs(OUT, exist_ok=True)

def limpiar():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(nombre, color):
    m = bpy.data.materials.get(nombre) or bpy.data.materials.new(nombre)
    m.diffuse_color = (*color, 1)
    return m

def objeto(nombre, bm, material, padre=None, suave=False):
    me = bpy.data.meshes.new(nombre)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = suave
    if isinstance(material, list):
        for m in material: me.materials.append(m)
    else:
        me.materials.append(material)
    ob = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(ob)
    if padre: ob.parent = padre
    return ob

def caja(nombre, x0, x1, y0, y1, z0, z1, material, bisel=0.015):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x = x0 + (v.co.x + 0.5) * (x1 - x0)
        v.co.y = y0 + (v.co.y + 0.5) * (y1 - y0)
        v.co.z = z0 + (v.co.z + 0.5) * (z1 - z0)
    if bisel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=min(bisel, 0.45 * min(x1 - x0, y1 - y0, z1 - z0)), segments=1, affect='EDGES')
    return objeto(nombre, bm, material)

def torno(nombre, perfil, material, segmentos=24, suave=True, cerrar_arriba=True, cerrar_abajo=False):
    """Pieza de revolución alrededor de Z. perfil = [(z, radio)] de arriba abajo."""
    bm = bmesh.new()
    anillos = []
    for (z, r) in perfil:
        anillo = [bm.verts.new((math.cos(2 * math.pi * k / segmentos) * r, math.sin(2 * math.pi * k / segmentos) * r, z)) for k in range(segmentos)]
        anillos.append(anillo)
    for a, b in zip(anillos, anillos[1:]):
        for k in range(segmentos):
            bm.faces.new((a[k], a[(k + 1) % segmentos], b[(k + 1) % segmentos], b[k]))
    if cerrar_arriba: bm.faces.new(list(reversed(anillos[0])))
    if cerrar_abajo: bm.faces.new(anillos[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return objeto(nombre, bm, material, suave=suave)

def cilindro(nombre, a, b, radio, material, segmentos=10, suave=True):
    """Cilindro entre los puntos a y b."""
    a, b = Vector(a), Vector(b)
    ob = torno(nombre, [(0.0, radio), (-(b - a).length, radio)], material, segmentos, suave, True, True)
    eje = (b - a).normalized()
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = Vector((0, 0, -1)).rotation_difference(eje)
    ob.location = a
    aplicar(ob)
    return ob

def bola(nombre, centro, radios, material, sub=2, suave=True, ruido=0.0, semilla=0):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    rnd = random.Random(semilla)
    for v in bm.verts:
        f = 1.0 + (rnd.uniform(-ruido, ruido) if ruido else 0.0)
        v.co = Vector((v.co.x * radios[0] * f + centro[0], v.co.y * radios[1] * f + centro[1], v.co.z * radios[2] * f + centro[2]))
    return objeto(nombre, bm, material, suave=suave)

def aplicar(ob):
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def juntar(obs, nombre):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    obs[0].name = nombre; obs[0].data.name = nombre
    return obs[0]

def diagonal(nombre, ax, az, bx, bz, grueso, y0, y1, material):
    largo = math.hypot(bx - ax, bz - az)
    ob = caja(nombre, -largo / 2, largo / 2, y0, y1, -grueso / 2, grueso / 2, material, bisel=0.01)
    ob.rotation_euler = (0, -math.atan2(bz - az, bx - ax), 0)
    ob.location = ((ax + bx) / 2, 0, (az + bz) / 2)
    aplicar(ob)
    return ob

def exportar(nombre):
    ruta = os.path.join(OUT, nombre + ".fbx")
    bpy.ops.export_scene.fbx(filepath=ruta, use_selection=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH', 'EMPTY'},
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
    print("exportado", ruta)

MADERA = (0.42, 0.28, 0.16)
MADERA_OSCURA = (0.30, 0.19, 0.11)
ORO = (0.95, 0.75, 0.25)
HIERRO = (0.16, 0.14, 0.13)
CUERDA = (0.78, 0.66, 0.45)
PIEDRA = (0.56, 0.54, 0.50)
BARRO = (0.36, 0.26, 0.16)
BARRO_SECO = (0.50, 0.38, 0.25)
BARRO_MOJADO = (0.22, 0.16, 0.10)

# ── 1 · La campana de la prueba ──────────────────────────────────────────────────────────
# Un armazón de dos postes con travesaño, tornapuntas y tejadillo a dos aguas; la campana
# cuelga en medio. «Campana» es una pieza aparte con el origen en su eje de giro (debajo del
# travesaño), para poder mecerla al tocarla; lleva dentro el badajo y la cuerda.
limpiar()
m_madera, m_oscura, m_oro = mat("Madera", MADERA), mat("MaderaOscura", MADERA_OSCURA), mat("Oro", ORO)
m_hierro, m_cuerda, m_piedra = mat("Hierro", HIERRO), mat("Cuerda", CUERDA), mat("Piedra", PIEDRA)

X = 0.72          # media separación de los postes
P = 0.08          # medio grueso del poste
ALTO = 2.62       # arriba del poste
VIGA0, VIGA1 = 2.50, 2.68
EJE = VIGA0       # eje de giro de la campana

arm = []
for s, lado in ((-1, "Izq"), (1, "Der")):
    arm.append(caja(f"Poste_{lado}", s * X - P, s * X + P, -P, P, 0.12, ALTO, m_madera))
    arm.append(caja(f"Basa_{lado}", s * X - 0.2, s * X + 0.2, -0.2, 0.2, 0.0, 0.14, m_piedra, bisel=0.04))
    arm.append(diagonal(f"Tornapunta_{lado}", s * (X - P), 2.05, s * (X - 0.42), VIGA0 + 0.01, 0.09, -0.05, 0.05, m_madera))
    # Pie inclinado para que no parezca una portería
    arm.append(diagonal(f"Pie_{lado}", s * X, 0.95, s * (X + 0.40), 0.10, 0.10, -0.055, 0.055, m_madera))
arm.append(caja("Travesano", -X - 0.22, X + 0.22, -0.1, 0.1, VIGA0, VIGA1, m_madera))
# Tejadillo a dos aguas (tablas oscuras) con la cumbrera a lo largo de X
for s in (-1, 1):
    y0, y1 = (0.0, 0.48) if s > 0 else (-0.48, 0.0)
    t = caja(f"Faldon_{s}", -X - 0.34, X + 0.34, y0, y1, -0.025, 0.025, m_oscura, bisel=0.01)
    t.rotation_euler = (math.radians(-28 * s), 0, 0)   # cada faldón cae hacia su lado
    t.location = (0, 0, ALTO + 0.30)
    aplicar(t)
    arm.append(t)
arm.append(caja("Cumbrera", -X - 0.36, X + 0.36, -0.05, 0.05, ALTO + 0.27, ALTO + 0.36, m_madera))
# Montantes que sujetan el tejadillo sobre los postes
for s in (-1, 1):
    arm.append(caja(f"Montante_{s}", s * X - 0.05, s * X + 0.05, -0.05, 0.05, ALTO - 0.02, ALTO + 0.30, m_madera))
armazon = juntar(arm, "Armazon")
for p in armazon.data.polygons: p.use_smooth = False

# La campana (origen en el eje de giro)
pie = EJE
bell = [(0.00, 0.06), (-0.03, 0.10), (-0.08, 0.15), (-0.16, 0.17), (-0.26, 0.18), (-0.36, 0.21),
        (-0.44, 0.26), (-0.50, 0.31), (-0.53, 0.33), (-0.55, 0.33),
        (-0.55, 0.29), (-0.50, 0.26), (-0.40, 0.17), (-0.25, 0.13), (-0.12, 0.10)]
campana = torno("Campana", [(z - 0.14, r) for z, r in bell], m_oro, segmentos=28, cerrar_arriba=True, cerrar_abajo=True)
piezas = [campana]
piezas.append(caja("Yugo", -0.14, 0.14, -0.08, 0.08, -0.15, 0.0, m_oscura, bisel=0.02))
for s in (-1, 1):
    piezas.append(caja(f"Abrazadera_{s}", s * 0.10 - 0.02, s * 0.10 + 0.02, -0.085, 0.085, -0.22, -0.02, m_hierro, bisel=0.006))
piezas.append(torno("Anillo", [(-0.60, 0.335), (-0.62, 0.345), (-0.645, 0.335)], m_oro, segmentos=28, cerrar_arriba=False))
# Badajo y cuerda: asoman por debajo de la boca
piezas.append(cilindro("Varilla", (0, 0, -0.20), (0, 0, -0.66), 0.014, m_hierro, 8))
piezas.append(bola("Badajo", (0, 0, -0.68), (0.055, 0.055, 0.065), m_hierro, sub=2))
piezas.append(cilindro("Cuerda", (0, 0, -0.72), (0.02, 0.03, -1.45), 0.017, m_cuerda, 8))
piezas.append(bola("Nudo", (0.02, 0.03, -1.47), (0.035, 0.035, 0.04), m_cuerda, sub=1))
for i, z in enumerate((-1.52, -1.60)):
    piezas.append(cilindro(f"Cabo{i}", (0.02, 0.03, -1.49), (0.02 + 0.03 * (i * 2 - 1), 0.04, z), 0.012, m_cuerda, 6))
campana = juntar(piezas, "Campana")
campana.location = (0, 0, pie)

exportar("CampanaDeLaPrueba")

# ── 2 · El charco de barro del arroyo ────────────────────────────────────────────────────
# Mancha irregular de ~4 m de radio (el de ZonaDeBarro): borde seco que baja al suelo, barro en
# medio, charquitos mojados y algún pegote y piedra. Apoyado en z = 0.
limpiar()
m_barro, m_seco, m_mojado, m_piedra = mat("Barro", BARRO), mat("BarroSeco", BARRO_SECO), mat("BarroMojado", BARRO_MOJADO), mat("Piedra", PIEDRA)
rnd = random.Random(7)

def contorno(t, radio, fase=0.0):
    return radio * (1.0 - 0.06 + 0.06 * math.sin(3 * t + fase) + 0.035 * math.sin(5 * t + 1.3 + fase) + 0.025 * math.cos(8 * t + fase))

def mancha(nombre, cx, cy, radio, alto, material, fase, seg=48, anillos=5, ondula=0.0, borde_seco=None):
    """Disco irregular que baja a z=0 en el borde. Si borde_seco, el anillo exterior lleva ese material (índice 1)."""
    bm = bmesh.new()
    centro = bm.verts.new((cx, cy, alto))
    filas = []
    for i in range(1, anillos + 1):
        f = i / anillos
        fila = []
        for k in range(seg):
            t = 2 * math.pi * k / seg
            r = contorno(t, radio, fase) * f
            if i == anillos:
                z = 0.0
            elif i == anillos - 1 and borde_seco is not None:
                z = alto + 0.018       # el reborde seco abulta un poco
            else:
                z = alto + ondula * math.sin(t * 3 + i) * math.cos(t * 2 - i)
            fila.append(bm.verts.new((cx + math.cos(t) * r, cy + math.sin(t) * r, z)))
        filas.append(fila)
    for k in range(seg):
        bm.faces.new((centro, filas[0][k], filas[0][(k + 1) % seg]))
    for j, (a, b) in enumerate(zip(filas, filas[1:])):
        for k in range(seg):
            f = bm.faces.new((a[k], b[k], b[(k + 1) % seg], a[(k + 1) % seg]))
            if borde_seco is not None and j >= len(filas) - 3:
                f.material_index = 1
    for f in bm.faces:
        f.normal_update()
        if f.normal.z < 0: f.normal_flip()
    mats = [material, borde_seco] if borde_seco is not None else material
    return objeto(nombre, bm, mats, suave=True)

partes = [mancha("Charco", 0, 0, 4.0, 0.035, m_barro, 0.0, seg=64, anillos=7, ondula=0.006, borde_seco=m_seco)]
for i, (cx, cy, r) in enumerate(((-1.1, 0.6, 1.15), (1.3, -0.9, 0.8), (0.4, 1.7, 0.55), (-0.6, -1.6, 0.6))):
    partes.append(mancha(f"Mojado{i}", cx, cy, r, 0.047, m_mojado, fase=i * 1.7, seg=28, anillos=3))
for i in range(9):
    a = rnd.uniform(0, 2 * math.pi); d = rnd.uniform(0.8, 3.0)
    partes.append(bola(f"Pegote{i}", (math.cos(a) * d, math.sin(a) * d, 0.04),
                       (rnd.uniform(0.12, 0.26), rnd.uniform(0.10, 0.22), rnd.uniform(0.04, 0.07)), m_barro, sub=1, ruido=0.15, semilla=i))
for i in range(7):
    a = rnd.uniform(0, 2 * math.pi); d = rnd.uniform(3.2, 3.9)
    partes.append(bola(f"Piedra{i}", (math.cos(a) * d, math.sin(a) * d, 0.03),
                       (rnd.uniform(0.10, 0.22), rnd.uniform(0.08, 0.18), rnd.uniform(0.06, 0.11)), m_piedra, sub=1, suave=False, ruido=0.2, semilla=20 + i))
charco = juntar(partes, "CharcoDeBarro")
exportar("CharcoDeBarro")
