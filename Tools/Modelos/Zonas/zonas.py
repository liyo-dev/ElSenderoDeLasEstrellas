# Casas nuevas de las zonas hechas en Blender (bpy de pip). Uso:
#   python zonas.py <carpeta de salida> [fotos]
# Saca CasaDeLaAldea.fbx (casa encalada sobre pilotes) y CasaDeCiudad_A/B/C.fbx (casas de ciudad que
# encajan pared con pared). Con «fotos», además dos imágenes de maqueta (aldea.png y capital.png).
# 1 unidad = 1 m. Convención: X a lo ancho, Z (Blender) arriba, la fachada mira a -Y (Blender); en el FBX sale hacia -Z de Unity, como las casas del pack.
# La casa de la aldea apoya su plataforma a 1,6 m sobre el agua (z = 0 es la lámina del mar).
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

ARGS = [a for a in sys.argv[1:] if not a.startswith("-")]
OUT = ARGS[0] if ARGS else "salida"
FOTOS = "fotos" in ARGS
os.makedirs(OUT, exist_ok=True)

def hexa(h):
    """Color sRGB «#rrggbb» a lineal."""
    h = h.lstrip("#")
    return tuple(((int(h[i:i + 2], 16) / 255.0) ** 2.2) for i in (0, 2, 4))

COLORES = {
    "Cal": "#f6f1e4", "Zocalo": "#2f79b0", "TejaTurquesa": "#1fa596", "TejaAzul": "#2b6f9e", "TejaSombra": "#178a7d", "Carpinteria": "#1b8f84",
    "CarpinteriaAzul": "#285f9c", "Madera": "#a8713f", "MaderaOscura": "#7a5230", "Cuerda": "#d8c9a0", "Cristal": "#5b8fb8",
    "Boya": "#e0483e", "Flor": "#f06a8a", "Hojas": "#3fa85c", "Hierro": "#3a3340", "Luz": "#ffe58a",
    "PiedraA": "#eadcc0", "PiedraB": "#d9b196", "PiedraC": "#d8c58f", "PiedraLila": "#9a8fc0", "Pizarra": "#2f6fb5",
    "PizarraOscura": "#285f9c", "PizarraSombra": "#2a64a6", "Fondo": "#3a2f3f", "Estandarte": "#2f6fb5", "Oro": "#f2c14e",
    "Agua": "#2f86c0", "Barro": "#c98a5a", "Arena": "#ecd9a0",
    "TejaCoral": "#e2674f", "TejaCoralSombra": "#c9553f", "TejaAmarilla": "#f0b83a", "TejaAmarillaSombra": "#d9a02a",
    "CarpinteriaCoral": "#c9553f", "ZocaloCoral": "#e2674f", "ZocaloTurquesa": "#1fa596", "ZocaloAmarillo": "#f0b83a",
    "Pescado": "#9fb6c4", "PescadoOscuro": "#6f8fa3", "Roca": "#8d8f9c", "RojoFaro": "#e0483e", "CopaClara": "#6fc46a",
    "PiedraD": "#e6cfc2", "PiedraE": "#cfd6e6", "PiedraGris": "#b9b4c8",
    "ToldoRojo": "#e0483e", "ToldoVerde": "#3fa85c", "ToldoAmarillo": "#f0b83a",
    # Pueblo de Will (campo), parajes y templos.
    "Estuco": "#f3e3c0", "PiedraCampo": "#b8a88c", "Paja": "#e3b44a", "PajaSombra": "#c9982f", "ContraRoja": "#c9553f", "ContraVerde": "#3f8f5c",
    "RojoGranero": "#b8483a", "Hierba": "#5fb85f", "RocaOscura": "#4a4658", "SueloDeCueva": "#6b6470", "Hiedra": "#2f8f4e",
    "PiedraTemplo": "#d9d2c0", "Musgo": "#6f9f5a", "Bronce": "#b8763a", "Portal": "#7fe0d8",
}

def limpiar():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(nombre):
    m = bpy.data.materials.get(nombre)
    if m: return m
    m = bpy.data.materials.new(nombre)
    c = hexa(COLORES[nombre])
    m.diffuse_color = (*c, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    if p:
        p.inputs["Base Color"].default_value = (*c, 1)
        p.inputs["Roughness"].default_value = 1.0
        if nombre in ("Luz", "Portal"):
            p.inputs["Emission Color"].default_value = (*c, 1)
            p.inputs["Emission Strength"].default_value = 2.0
    return m

def objeto(nombre, bm, material):
    me = bpy.data.meshes.new(nombre)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    me.materials.append(mat(material))
    ob = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

def caja(x0, x1, y0, y1, z0, z1, material, bisel=0.03):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x = x0 + (v.co.x + 0.5) * (x1 - x0)
        v.co.y = y0 + (v.co.y + 0.5) * (y1 - y0)
        v.co.z = z0 + (v.co.z + 0.5) * (z1 - z0)
    if bisel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=min(bisel, 0.45 * min(x1 - x0, y1 - y0, z1 - z0)), segments=1, affect='EDGES')
    return objeto("caja", bm, material)

def aplicar(ob):
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob

def poste(x, y, z0, z1, radio, material, lados=8):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=lados, radius1=radio * 1.08, radius2=radio, depth=z1 - z0)
    for v in bm.verts: v.co += Vector((x, y, (z0 + z1) / 2))
    return objeto("poste", bm, material)

def viga(a, b, grueso, material):
    """Listón de sección cuadrada entre los puntos a y b."""
    a, b = Vector(a), Vector(b)
    ob = caja(-(b - a).length / 2, (b - a).length / 2, -grueso / 2, grueso / 2, -grueso / 2, grueso / 2, material, bisel=0.01)
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = Vector((1, 0, 0)).rotation_difference((b - a).normalized())
    ob.location = (a + b) / 2
    return aplicar(ob)

def prisma(perfil, d0, d1, material, eje='Y'):
    """Extruye un perfil. eje 'Y': perfil en (x, z), de y = d0 a d1. eje 'X': perfil en (y, z), de x = d0 a d1."""
    bm = bmesh.new()
    def p(u, z, d): return (u, d, z) if eje == 'Y' else (d, u, z)
    a = [bm.verts.new(p(u, z, d0)) for u, z in perfil]
    b = [bm.verts.new(p(u, z, d1)) for u, z in perfil]
    bm.faces.new(a); bm.faces.new(list(reversed(b)))
    n = len(perfil)
    for i in range(n):
        bm.faces.new([a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    return objeto("prisma", bm, material)

def cono(x, y, z0, z1, r0, r1, material, lados=12, giro=0.0):
    """Tronco de cono vertical (r1 casi 0: punta). «giro» en radianes, para orientar las caras."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=lados, radius1=r0, radius2=max(r1, 0.01), depth=z1 - z0)
    bmesh.ops.rotate(bm, verts=bm.verts[:], cent=(0, 0, 0), matrix=Matrix.Rotation(giro, 3, 'Z'))
    for v in bm.verts: v.co += Vector((x, y, (z0 + z1) / 2))
    return objeto("cono", bm, material)

def bola(c, r, material, sub=1):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    for v in bm.verts: v.co += Vector(c)
    return objeto("bola", bm, material)

def girar(obs, grados, a=(0, 0, 0)):
    """Gira las piezas alrededor del eje vertical y las lleva a «a»."""
    for o in obs:
        o.rotation_euler = (0, 0, math.radians(grados)); o.location = a
        aplicar(o)
    return obs

def juntar(obs, nombre):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    obs[0].name = nombre; obs[0].data.name = nombre
    return obs[0]

def exportar(ob):
    # Las casas del pack tienen la fachada hacia -Z de Unity: se les da la vuelta al sacarlas.
    ob.rotation_euler = (0, 0, math.pi); aplicar(ob)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    ruta = os.path.join(OUT, ob.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=ruta, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH'},
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
    print("exportado", ruta)

def hiladas(alero, cumbre, d0, d1, materiales, eje, n=5, vuelo=0.0, hueco=False):
    """Hiladas de teja sobre un tejado a dos aguas simétrico: «alero» y «cumbre» son (u, z) del faldón derecho.
    Cada capa arranca más arriba y sobresale un poco más, así se ve un escalón por hilada. Con «hueco», las
    capas son láminas (para tejados con el hastial a la vista)."""
    o = []
    for k in range(1, n):
        t = k / n
        u = alero[0] + (cumbre[0] - alero[0]) * t
        z = alero[1] + (cumbre[1] - alero[1]) * t + 0.05 * k
        c = cumbre[1] + 0.07 * k
        perfil = [(-u, z), (0, c), (u, z), (u, z - 0.14), (0, c - 0.14), (-u, z - 0.14)] if hueco else [(-u, z), (u, z), (0, c)]
        o.append(prisma(perfil, d0 + vuelo * k, d1 - vuelo * k, materiales[k % 2], eje=eje))
    return o

def sillares(x, y0, y1, z0, z1, material, lado, alto=0.42):
    """Cadena de sillares de esquina, alternando largo y corto. «lado» +1 o -1: hacia dónde crecen en X."""
    o, z, k = [], z0, 0
    while z + alto <= z1 + 0.01:
        largo = 0.55 if k % 2 == 0 else 0.34
        xa, xb = (x, x + lado * largo)
        o.append(caja(min(xa, xb), max(xa, xb), y0, y1, z + 0.02, z + alto - 0.02, material, 0.03))
        z += alto; k += 1
    return o

# ── Piezas comunes ──────────────────────────────────────────────────────────────────────────

def ventana(ancho, alto, carpinteria, marco, jardinera=True):
    """Ventana con contraventanas, centrada en x = 0, pegada al plano y = 0 y mirando a -Y; su base en z = 0."""
    o = [caja(-ancho / 2 - 0.1, ancho / 2 + 0.1, -0.07, 0.03, -0.1, alto + 0.1, marco, 0.02),
         caja(-ancho / 2, ancho / 2, -0.09, 0.0, 0.0, alto, "Cristal", 0.0),
         caja(-0.03, 0.03, -0.11, -0.05, 0.0, alto, marco, 0.0),
         caja(-ancho / 2, ancho / 2, -0.11, -0.05, alto * 0.55, alto * 0.55 + 0.06, marco, 0.0)]
    for s in (-1, 1):
        x = s * (ancho / 2 + 0.12)
        o.append(caja(min(x, x + s * ancho * 0.48), max(x, x + s * ancho * 0.48), -0.12, -0.05, -0.04, alto + 0.04, carpinteria, 0.02))
    if jardinera:
        o.append(caja(-ancho / 2 - 0.15, ancho / 2 + 0.15, -0.38, -0.08, -0.34, -0.1, "Madera", 0.03))
        for i in range(4):
            x = -ancho / 2 + (i + 0.5) * ancho / 4
            o.append(bola((x, -0.23, -0.02), 0.13, "Hojas" if i % 2 else "Flor"))
    return o

def poner(obs, x, y, z, grados=0):
    for o in obs:
        o.rotation_euler = (0, 0, math.radians(grados)); o.location = (x, y, z)
        aplicar(o)
    return obs

# ── Casa de la aldea: encalada, sobre pilotes ───────────────────────────────────────────────

ALTO_PLATAFORMA = 1.6

SOMBRA_DE_TEJA = {"TejaTurquesa": "TejaSombra", "TejaAzul": "PizarraOscura", "TejaCoral": "TejaCoralSombra", "TejaAmarilla": "TejaAmarillaSombra"}

def casa_de_la_aldea(nombre, teja="TejaTurquesa", carpinteria="Carpinteria", espejo=False, zocalo="Zocalo", ancho=4.6, fondo=3.6, plantas=1, en_tierra=False):
    """«en_tierra»: sin pilotes, sobre un zócalo de piedra de 0,45 m y con dos peldaños delante."""
    a, f, zp = ancho, fondo, (0.45 if en_tierra else ALTO_PLATAFORMA)
    porche = 1.5
    y_frente, y_fondo = -f / 2 - porche, f / 2 + 0.4
    xl, xr = -a / 2 - 0.6, a / 2 + 0.6
    o = []
    # Plataforma de tablas.
    n = 13
    paso = (xr - xl) / n
    for i in range(n):
        o.append(caja(xl + i * paso + 0.02, xl + (i + 1) * paso - 0.02, y_frente, y_fondo, zp - 0.16, zp, "Madera" if i % 3 else "MaderaOscura", 0.02))
    for y in (y_frente + 0.25, 0.0, y_fondo - 0.25):
        o.append(caja(xl - 0.05, xr + 0.05, y - 0.1, y + 0.1, zp - 0.34, zp - 0.16, "MaderaOscura", 0.02))
    if en_tierra:
        o.append(caja(xl + 0.05, xr - 0.05, y_frente + 0.05, y_fondo - 0.05, -0.7, zp - 0.16, "Roca", 0.05))
    else:
        # Pilotes y sus riostras.
        for x in (xl + 0.2, 0.0, xr - 0.2):
            for y in (y_frente + 0.25, y_fondo - 0.25):
                o.append(poste(x, y, -2.4, zp - 0.16, 0.17, "MaderaOscura"))
        for y in (y_frente + 0.25, y_fondo - 0.25):
            o.append(viga((xl + 0.2, y, 0.25), (0.0, y, zp - 0.4), 0.12, "MaderaOscura"))
            o.append(viga((xr - 0.2, y, 0.25), (0.0, y, zp - 0.4), 0.12, "MaderaOscura"))
    # Muros encalados con zócalo de color.
    zt = zp + 2.5 * plantas
    o.append(caja(-a / 2, a / 2, -f / 2, f / 2, zp, zt, "Cal", 0.06))
    o.append(caja(-a / 2 - 0.04, a / 2 + 0.04, -f / 2 - 0.04, f / 2 + 0.04, zp, zp + 0.6, zocalo, 0.03))
    if plantas > 1:
        o.append(caja(-a / 2 - 0.05, a / 2 + 0.05, -f / 2 - 0.05, f / 2 + 0.05, zp + 2.42, zp + 2.6, "MaderaOscura", 0.03))
        for s in (-1, 1):
            o += poner(ventana(min(0.9, a * 0.2), 1.0, carpinteria, "MaderaOscura"), s * a * 0.25, -f / 2, zp + 3.4)
    # Hastiales y tejado a dos aguas (cumbrera a lo ancho).
    h = 1.55
    o.append(prisma([(-f / 2, zt - 0.02), (f / 2, zt - 0.02), (0, zt + h)], -a / 2 + 0.02, a / 2 - 0.02, "Cal", eje='X'))
    o.append(prisma([(-f / 2 - 0.5, zt - 0.2), (f / 2 + 0.5, zt - 0.2), (0, zt + h + 0.24)], -a / 2 - 0.4, a / 2 + 0.4, teja, eje='X'))
    o += hiladas((f / 2 + 0.5, zt - 0.2), (0, zt + h + 0.24), -a / 2 - 0.4, a / 2 + 0.4, (teja, SOMBRA_DE_TEJA[teja]), 'X', vuelo=-0.012)
    o.append(caja(-a / 2 - 0.55, a / 2 + 0.55, -0.12, 0.12, zt + h + 0.4, zt + h + 0.62, "MaderaOscura", 0.03))
    o += poner(ventana(0.9, 1.0, carpinteria, "MaderaOscura"), 0.0, f / 2, zp + 1.05, 180)
    # Chimenea.
    o.append(caja(a / 2 - 1.3, a / 2 - 0.8, 0.35, 0.85, zt + 0.5, zt + h + 0.55, "Cal", 0.04))
    o.append(caja(a / 2 - 1.38, a / 2 - 0.72, 0.27, 0.93, zt + h + 0.55, zt + h + 0.7, teja, 0.03))
    # Puerta y ventanas de la fachada.
    xd = -a * 0.22
    o.append(caja(xd - 0.6, xd + 0.6, -f / 2 - 0.07, -f / 2 + 0.02, zp, zp + 2.1, "MaderaOscura", 0.02))
    o.append(caja(xd - 0.48, xd + 0.48, -f / 2 - 0.11, -f / 2 - 0.03, zp + 0.02, zp + 1.98, carpinteria, 0.02))
    o.append(bola((xd + 0.32, -f / 2 - 0.14, zp + 1.0), 0.05, "Oro"))
    o += poner(ventana(min(0.9, a * 0.2), 1.0, carpinteria, "MaderaOscura"), a * 0.25, -f / 2, zp + 1.05)
    o += poner(ventana(0.8, 0.9, carpinteria, "MaderaOscura", jardinera=False), a / 2, 0.0, zp + 1.15, 90)
    o += poner(ventana(0.8, 0.9, carpinteria, "MaderaOscura", jardinera=False), -a / 2, 0.0, zp + 1.15, -90)
    # Barandilla del porche con cabo, y boyas colgadas.
    xs = [xl + 0.15, xl + 1.5, 0.6, xr - 1.4, xr - 0.15]
    for x in xs:
        o.append(poste(x, y_frente + 0.12, zp, zp + 1.0, 0.07, "Madera", 6))
    for x0, x1 in ((xs[0], xs[1]), (xs[2], xs[3]), (xs[3], xs[4])):
        o.append(viga((x0, y_frente + 0.12, zp + 0.98), (x1, y_frente + 0.12, zp + 0.98), 0.09, "Madera"))
        o.append(viga((x0, y_frente + 0.12, zp + 0.55), (x1, y_frente + 0.12, zp + 0.5), 0.05, "Cuerda"))
    for x in (xs[3] - 0.5, xs[3] + 0.6):
        o.append(viga((x, y_frente + 0.02, zp + 0.95), (x, y_frente + 0.02, zp + 0.55), 0.03, "Cuerda"))
        o.append(bola((x, y_frente + 0.02, zp + 0.42), 0.17, "Boya"))
    if en_tierra:
        xe = (xs[1] + xs[2]) / 2
        for i in range(2):
            o.append(caja(xe - 0.65, xe + 0.65, y_frente - 0.38 * (i + 1), y_frente - 0.38 * i + 0.02, -0.7, zp - 0.16 - 0.14 * (i + 1) + 0.14, "Roca", 0.03))
    else:
        # Escalones de tabla hasta el agua, en el hueco de la barandilla: por aquí se sube andando al salir de nadar.
        xe = (xs[1] + xs[2]) / 2
        n_esc = 7
        for i in range(n_esc):
            y1 = y_frente - i * 0.36
            z1 = zp - (i + 1) * (zp + 0.5) / n_esc
            o.append(caja(xe - 0.5, xe + 0.5, y1 - 0.4, y1, z1 - 0.1, z1, "Madera" if i % 2 else "MaderaOscura", 0.02))
        for s_ in (-0.5, 0.5):
            o.append(viga((xe + s_, y_frente, zp - 0.2), (xe + s_, y_frente - n_esc * 0.36, -0.7), 0.1, "MaderaOscura"))
            o.append(poste(xe + s_, y_frente - n_esc * 0.36 + 0.1, -2.4, 0.2, 0.09, "MaderaOscura", 6))
    # Farol del porche.
    o.append(poste(xl + 0.15, y_frente + 0.12, zp + 1.0, zp + 2.2, 0.05, "MaderaOscura", 6))
    o.append(caja(xl + 0.03, xl + 0.27, y_frente, y_frente + 0.24, zp + 2.2, zp + 2.5, "Luz", 0.02))
    o.append(caja(xl - 0.02, xl + 0.32, y_frente - 0.05, y_frente + 0.29, zp + 2.5, zp + 2.58, "Hierro", 0.02))
    casa = juntar(o, nombre)
    if espejo:
        # Aplicar una escala negativa ya da la vuelta a las caras: no hay que invertirlas después.
        casa.scale = (-1, 1, 1); aplicar(casa)
    return casa

def pasarela(x0, x1, y, nombre="Pasarela"):
    """Tramo de pasarela de tablas entre dos plataformas, con pilotes y un cabo a cada lado."""
    zp, o = ALTO_PLATAFORMA, []
    n = max(2, int((x1 - x0) / 0.45))
    paso = (x1 - x0) / n
    for i in range(n):
        o.append(caja(x0 + i * paso + 0.02, x0 + (i + 1) * paso - 0.02, y - 0.7, y + 0.7, zp - 0.14, zp, "Madera" if i % 2 else "MaderaOscura", 0.02))
    xm = (x0 + x1) / 2
    for s in (-0.62, 0.62):
        o.append(poste(xm, y + s, -2.4, zp + 0.9, 0.1, "MaderaOscura", 6))
        o.append(viga((x0, y + s, zp + 0.8), (xm, y + s, zp + 0.62), 0.05, "Cuerda"))
        o.append(viga((xm, y + s, zp + 0.62), (x1, y + s, zp + 0.8), 0.05, "Cuerda"))
    return juntar(o, nombre)

# ── Casa de ciudad: alta, estrecha, con soportal; encaja pared con pared ────────────────────

def arco(x0, x1, z_arranque, z_techo, y0, y1, material):
    """Enjuta de un vano: rectángulo de z_arranque a z_techo con un medio punto abierto por debajo."""
    r, xc = (x1 - x0) / 2, (x0 + x1) / 2
    perfil = [(x0, z_arranque)]
    for i in range(1, 12):
        ang = math.pi - i * math.pi / 12
        perfil.append((xc + r * math.cos(ang), z_arranque + r * math.sin(ang)))
    perfil += [(x1, z_arranque), (x1, z_techo), (x0, z_techo)]
    return prisma(perfil, y0, y1, material, eje='Y')

def casa_de_ciudad(nombre, ancho=4.4, plantas=3, piedra="PiedraA", tejado="Pizarra", balcon=True, alero=False, mirador=False,
                   fondo=6.0, vanos=None, toldo=None):
    """Casa o edificio de ciudad. «vanos»: arcos del soportal (por defecto, uno cada 2,7 m de fachada; mínimo dos).
    «toldo»: material de un toldo de rayas sobre cada arco (tiendas)."""
    w, f = ancho, fondo
    bajo, piso = 2.9, 2.35
    o = []
    # Planta baja: soportal y, retranqueada, la tienda.
    pilar = 0.5
    n = vanos or max(2, round(w / 2.7))
    vano = (w - (n + 1) * pilar) / n
    pilares = [-w / 2 + i * (pilar + vano) for i in range(n + 1)]
    centros = [x + pilar + vano / 2 for x in pilares[:-1]]
    for x in pilares:
        o.append(caja(x, x + pilar, -f / 2, -f / 2 + 0.55, 0, bajo, "PiedraLila", 0.04))
        o.append(caja(max(x - 0.05, -w / 2), min(x + pilar + 0.05, w / 2), -f / 2 - 0.05, -f / 2 + 0.6, 0, 0.35, "PiedraLila", 0.04))
    z_arr = bajo - 0.35 - vano / 2
    for x in pilares[:-1]:
        o.append(arco(x + pilar, x + pilar + vano, z_arr, bajo, -f / 2 + 0.03, -f / 2 + 0.52, piedra))
    o.append(caja(-w / 2, w / 2, -f / 2 + 1.7, f / 2, 0, bajo, piedra, 0.03))
    o.append(caja(-w / 2, w / 2, -f / 2 + 0.55, -f / 2 + 1.7, bajo - 0.25, bajo, "MaderaOscura", 0.0))
    for k, xc in enumerate(centros):
        if k == 0:
            o.append(caja(xc - 0.55, xc + 0.55, -f / 2 + 1.62, -f / 2 + 1.72, 0, 2.15, "MaderaOscura", 0.02))
            o.append(caja(xc - 0.45, xc + 0.45, -f / 2 + 1.58, -f / 2 + 1.66, 0.02, 2.05, "CarpinteriaAzul", 0.02))
        else:
            o.append(caja(xc - 0.7, xc + 0.7, -f / 2 + 1.6, -f / 2 + 1.72, 0.9, 2.1, "MaderaOscura", 0.02))
            o.append(caja(xc - 0.6, xc + 0.6, -f / 2 + 1.57, -f / 2 + 1.65, 1.0, 2.0, "Cristal", 0.0))
            o.append(caja(xc - 0.75, xc + 0.75, -f / 2 + 1.3, -f / 2 + 1.72, 0.75, 0.9, "Madera", 0.02))
    if toldo:
        # Toldo de rayas sobre cada arco.
        for x in pilares[:-1]:
            x0, rayas = x + pilar - 0.15, 6
            paso = (vano + 0.3) / rayas
            for r in range(rayas):
                o.append(prisma([(-f / 2 - 0.02, bajo - 0.2), (-f / 2 - 1.3, bajo - 0.85), (-f / 2 - 1.3, bajo - 0.95), (-f / 2 - 0.02, bajo - 0.32)],
                                x0 + r * paso, x0 + (r + 1) * paso, toldo if r % 2 == 0 else "Cal", eje='X'))
    # Plantas altas, con imposta de piedra lila en cada forjado.
    zt = bajo + piso * (plantas - 1)
    o.append(caja(-w / 2, w / 2, -f / 2, f / 2, bajo, zt, piedra, 0.03))
    for k in range(plantas):
        z = bajo + piso * k
        o.append(caja(-w / 2, w / 2, -f / 2 - 0.08, -f / 2 + 0.1, z - 0.14, z + 0.14, "PiedraLila", 0.03))
    for k in range(plantas - 1):
        z = bajo + piso * k + 0.62
        for xc in centros:
            if not (mirador and k == 0 and abs(xc) < 2.3):
                o += poner(ventana(0.95, 1.25, "CarpinteriaAzul", "PiedraLila", jardinera=(k > 0 or not balcon)), xc, -f / 2, z)
            o += poner(ventana(0.95, 1.25, "CarpinteriaAzul", "PiedraLila", jardinera=False), xc, f / 2, z, 180)
    if mirador:
        # Mirador de madera y cristal en el primer piso, con su tejadillo.
        zm = bajo + 0.35
        o.append(caja(-1.3, 1.3, -f / 2 - 0.7, -f / 2, zm - 0.2, zm, "PiedraLila", 0.03))
        o.append(caja(-1.2, 1.2, -f / 2 - 0.62, -f / 2, zm, zm + 1.75, "CarpinteriaAzul", 0.03))
        for x in (-0.78, 0.0, 0.78):
            o.append(caja(x - 0.3, x + 0.3, -f / 2 - 0.66, -f / 2 - 0.6, zm + 0.45, zm + 1.5, "Cristal", 0.0))
        o.append(prisma([(-f / 2 - 0.85, zm + 1.75), (-f / 2, zm + 1.75), (-f / 2, zm + 2.3)], -1.35, 1.35, tejado, eje='X'))
    elif balcon:
        zb = bajo + 0.55
        o.append(caja(-w / 2 + 0.35, w / 2 - 0.35, -f / 2 - 0.75, -f / 2, zb - 0.16, zb, "PiedraLila", 0.03))
        nb = max(6, int(w * 1.7))
        for i in range(nb + 1):
            x = -w / 2 + 0.42 + i * (w - 0.84) / nb
            o.append(caja(x - 0.025, x + 0.025, -f / 2 - 0.7, -f / 2 - 0.65, zb, zb + 0.85, "Hierro", 0.0))
        o.append(caja(-w / 2 + 0.38, w / 2 - 0.38, -f / 2 - 0.72, -f / 2 - 0.63, zb + 0.85, zb + 0.92, "Hierro", 0.01))
        for s in (-1, 1):
            o.append(caja(s * (w / 2 - 0.42) - 0.03, s * (w / 2 - 0.42) + 0.03, -f / 2 - 0.7, -f / 2, zb + 0.85, zb + 0.92, "Hierro", 0.0))
        for xc in centros:
            o.append(caja(xc - 0.5, xc + 0.5, -f / 2 - 0.66, -f / 2 - 0.4, zb, zb + 0.24, "Madera", 0.03))
            for i in range(4):
                o.append(bola((xc - 0.36 + i * 0.24, -f / 2 - 0.53, zb + 0.32), 0.13, "Flor" if i % 2 else "Hojas"))
    # Sillares en las esquinas de la fachada y zócalo de piedra en la trasera.
    for s in (-1, 1):
        o += sillares(s * (w / 2 - 0.01), -f / 2 - 0.06, -f / 2 + 0.3, bajo + 0.14, zt - 0.14, "PiedraLila", -s)
    o.append(caja(-w / 2, w / 2, f / 2 - 0.05, f / 2 + 0.07, 0, 0.7, "PiedraLila", 0.03))
    o.append(caja(-0.5, 0.5, f / 2 - 0.02, f / 2 + 0.06, 0, 2.05, "MaderaOscura", 0.02))
    o.append(caja(-0.4, 0.4, f / 2 + 0.02, f / 2 + 0.1, 0.02, 1.95, "CarpinteriaAzul", 0.02))
    # Costados: zócalo, impostas y ventanas. Pared con pared quedan dentro de la casa vecina; en el extremo de una
    # hilera, o por encima de una vecina más baja, el costado se ve y no es un muro ciego.
    ys = [-f / 2 + 2.9 + i * 2.2 for i in range(max(1, int((f - 3.6) / 2.2) + 1))]
    for s in (-1, 1):
        o.append(caja(s * w / 2 - 0.03, s * w / 2 + 0.03, -f / 2 + 1.7, f / 2, 0, 0.7, "PiedraLila", 0.02))
        for k in range(1, plantas):
            z = bajo + piso * k
            o.append(caja(s * w / 2 - 0.03, s * w / 2 + 0.03, -f / 2, f / 2, z - 0.12, z + 0.12, "PiedraLila", 0.02))
        for k in range(plantas - 1):
            for y in ys:
                o += poner(ventana(0.85, 1.25, "CarpinteriaAzul", "PiedraLila", jardinera=False), s * w / 2, y, bajo + piso * k + 0.62, 90 * s)
        if alero:
            # Con la cumbrera a lo ancho el costado acaba en hastial: de piedra, con un ojo de buey.
            hh = f * 0.45
            o.append(prisma([(-f / 2 + 0.05, zt), (f / 2 - 0.3, zt), (-0.2, zt + hh - 0.3)], min(s * w / 2 - 0.05, s * w / 2 + 0.012), max(s * w / 2 - 0.05, s * w / 2 + 0.012), piedra, eje='X'))
            o.append(caja(min(s * w / 2, s * w / 2 + s * 0.05), max(s * w / 2, s * w / 2 + s * 0.05), -0.55, 0.15, zt + 0.45, zt + 1.15, "PiedraLila", 0.02))
            o.append(caja(min(s * w / 2, s * w / 2 + s * 0.07), max(s * w / 2, s * w / 2 + s * 0.07), -0.45, 0.05, zt + 0.55, zt + 1.05, "Cristal", 0.0))
    if alero:
        # Alero a la calle: cumbrera a lo ancho, con buhardillas.
        hr = f * 0.45
        o.append(prisma([(-f / 2 - 0.45, zt), (f / 2 + 0.05, zt), (-0.2, zt + hr)], -w / 2, w / 2, tejado, eje='X'))
        for k in range(1, 5):
            t = k / 5
            ya, yb = -f / 2 - 0.45 + (f / 2 + 0.25) * t, f / 2 + 0.05 - (f / 2 + 0.25) * t
            o.append(prisma([(ya, zt + hr * t + 0.05 * k), (yb, zt + hr * t + 0.05 * k), (-0.2, zt + hr + 0.07 * k)], -w / 2, w / 2, (tejado, "PizarraSombra")[k % 2], eje='X'))
        o.append(caja(-w / 2, w / 2, -0.32, -0.08, zt + hr + 0.3, zt + hr + 0.52, "PizarraOscura", 0.03))
        for xb in ([0.0] if n < 3 else centros[::2] if n % 2 else centros[1::2] if n > 3 else [0.0]):
            o.append(caja(xb - 0.7, xb + 0.7, -f / 2 + 0.2, -f / 2 + 1.7, zt + 0.2, zt + 1.5, piedra, 0.03))
            o.append(prisma([(xb - 0.85, zt + 1.5), (xb + 0.85, zt + 1.5), (xb, zt + 2.25)], -f / 2 + 0.05, -f / 2 + 1.9, tejado, eje='Y'))
            o.append(caja(xb - 0.42, xb + 0.42, -f / 2 + 0.14, -f / 2 + 0.22, zt + 0.45, zt + 1.3, "Cristal", 0.0))
            o.append(caja(xb - 0.5, xb + 0.5, -f / 2 + 0.16, -f / 2 + 0.24, zt + 0.35, zt + 1.4, "PiedraLila", 0.02))
    else:
        # Hastial a la calle y tejado empinado de pizarra, sin vuelo a los lados.
        hr = w * 0.62
        o.append(prisma([(-w / 2, zt - 0.02), (w / 2, zt - 0.02), (0, zt + hr)], -f / 2, f / 2, piedra, eje='Y'))
        o.append(prisma([(-w / 2, zt), (0, zt + hr), (w / 2, zt), (w / 2, zt + 0.34), (0, zt + hr + 0.4), (-w / 2, zt + 0.34)], -f / 2 - 0.45, f / 2 + 0.05, tejado, eje='Y'))
        o += hiladas((w / 2, zt + 0.34), (0, zt + hr + 0.4), -f / 2 - 0.45, f / 2 + 0.05, (tejado, "PizarraSombra"), 'Y', hueco=True)
        o.append(caja(-0.12, 0.12, -f / 2 - 0.5, f / 2 + 0.08, zt + hr + 0.62, zt + hr + 0.84, "PizarraOscura", 0.03))
        o.append(poste(0, -f / 2 - 0.42, zt + hr + 0.8, zt + hr + 1.5, 0.04, "Hierro", 6))
        o.append(bola((0, -f / 2 - 0.42, zt + hr + 1.55), 0.11, "Oro"))
        o.append(caja(-0.42, 0.42, -f / 2 - 0.07, -f / 2 + 0.02, zt + hr * 0.25, zt + hr * 0.25 + 0.85, "PiedraLila", 0.03))
        o.append(caja(-0.32, 0.32, -f / 2 - 0.09, -f / 2, zt + hr * 0.25 + 0.1, zt + hr * 0.25 + 0.75, "Cristal", 0.0))
    o.append(caja(w * 0.22, w * 0.22 + 0.55, 0.8, 1.4, zt + hr * 0.35, zt + hr + 0.5, "PiedraLila", 0.04))
    o.append(caja(w * 0.22 - 0.06, w * 0.22 + 0.61, 0.74, 1.46, zt + hr + 0.5, zt + hr + 0.66, "PizarraOscura", 0.03))
    # Estandarte en su asta y farol en un pilar.
    ze = bajo + piso + 0.4
    o.append(viga((-w / 2 + 0.3, -f / 2, ze), (-w / 2 + 0.3, -f / 2 - 1.1, ze + 0.25), 0.07, "MaderaOscura"))
    o.append(caja(-w / 2 + 0.27, -w / 2 + 0.33, -f / 2 - 1.0, -f / 2 - 0.3, ze - 1.25, ze + 0.1, "Estandarte", 0.0))
    o.append(bola((-w / 2 + 0.3, -f / 2 - 0.65, ze - 0.55), 0.14, "Oro"))
    xf = pilares[n // 2] + pilar / 2
    o.append(viga((xf, -f / 2, 2.55), (xf, -f / 2 - 0.45, 2.7), 0.05, "Hierro"))
    o.append(caja(xf - 0.12, xf + 0.12, -f / 2 - 0.57, -f / 2 - 0.33, 2.3, 2.62, "Luz", 0.02))
    o.append(caja(xf - 0.15, xf + 0.15, -f / 2 - 0.6, -f / 2 - 0.3, 2.62, 2.7, "Hierro", 0.02))
    return juntar(o, nombre)

VARIANTES_DE_CIUDAD = (
    ("CasaDeCiudad_A", dict(ancho=5.4, plantas=3, piedra="PiedraA", tejado="Pizarra", balcon=True)),
    ("CasaDeCiudad_B", dict(ancho=4.8, plantas=4, piedra="PiedraB", tejado="PizarraOscura", balcon=False)),
    ("CasaDeCiudad_C", dict(ancho=5.8, plantas=2, piedra="PiedraC", tejado="Pizarra", balcon=True)),
    ("CasaDeCiudad_D", dict(ancho=5.2, plantas=3, piedra="PiedraD", tejado="Pizarra", balcon=False, alero=True, mirador=True)),
    ("CasaDeCiudad_E", dict(ancho=5.0, plantas=3, piedra="PiedraE", tejado="PizarraOscura", balcon=False, mirador=True)),
    ("CasaDeCiudad_F", dict(ancho=6.2, plantas=2, piedra="PiedraA", tejado="PizarraOscura", balcon=True, alero=True)),
    # Tiendas (toldo de rayas) y edificios públicos.
    ("CasaDeCiudad_G", dict(ancho=5.6, plantas=2, piedra="PiedraC", tejado="Pizarra", balcon=True, toldo="ToldoRojo")),
    ("CasaDeCiudad_H", dict(ancho=6.0, plantas=3, piedra="PiedraA", tejado="PizarraOscura", balcon=False, alero=True, toldo="ToldoVerde")),
    ("EdificioDeCiudad_Taberna", dict(ancho=9.0, plantas=2, piedra="PiedraC", tejado="Pizarra", balcon=True, alero=True, fondo=8.0, toldo="ToldoRojo")),
    ("EdificioDeCiudad_Posada", dict(ancho=12.0, plantas=3, piedra="PiedraD", tejado="PizarraOscura", balcon=False, alero=True, mirador=True, fondo=8.0, toldo="ToldoAmarillo")),
    ("EdificioDeCiudad_Palacio", dict(ancho=10.5, plantas=3, piedra="PiedraA", tejado="Pizarra", balcon=True, alero=True, fondo=8.0)),
    ("EdificioDeCiudad_Archivo", dict(ancho=12.0, plantas=2, piedra="PiedraE", tejado="PizarraOscura", balcon=True, alero=True, fondo=8.0)),
    ("EdificioDeCiudad_Guardia", dict(ancho=6.8, plantas=2, piedra="PiedraGris", tejado="PizarraOscura", balcon=False, alero=True, fondo=6.0)),
)

VARIANTES_DE_ALDEA = (
    ("CasaDeLaAldea", dict(teja="TejaTurquesa", carpinteria="Carpinteria", zocalo="Zocalo", ancho=4.6, fondo=3.6, plantas=1)),
    ("CasaDeLaAldea_Azul", dict(teja="TejaAzul", carpinteria="CarpinteriaAzul", zocalo="ZocaloTurquesa", ancho=4.2, fondo=3.4, plantas=1, espejo=True)),
    ("CasaDeLaAldea_Coral", dict(teja="TejaCoral", carpinteria="CarpinteriaCoral", zocalo="ZocaloCoral", ancho=3.4, fondo=3.0, plantas=1)),
    ("CasaDeLaAldea_Amarilla", dict(teja="TejaAmarilla", carpinteria="CarpinteriaAzul", zocalo="ZocaloAmarillo", ancho=4.8, fondo=3.6, plantas=2)),
    ("CasaDeLaAldea_Alta", dict(teja="TejaTurquesa", carpinteria="Carpinteria", zocalo="ZocaloCoral", ancho=3.8, fondo=3.2, plantas=2, espejo=True)),
    ("CasaDeLaAldea_Pequena", dict(teja="TejaAzul", carpinteria="Carpinteria", zocalo="Zocalo", ancho=3.2, fondo=2.8, plantas=1)),
    # En tierra: las de la aldea que no están sobre el agua.
    ("CasaDeLaAldeaEnTierra_Turquesa", dict(teja="TejaTurquesa", carpinteria="Carpinteria", zocalo="Zocalo", ancho=5.0, fondo=4.0, plantas=1, en_tierra=True)),
    ("CasaDeLaAldeaEnTierra_Azul", dict(teja="TejaAzul", carpinteria="CarpinteriaAzul", zocalo="ZocaloTurquesa", ancho=4.4, fondo=3.6, plantas=1, en_tierra=True, espejo=True)),
    ("CasaDeLaAldeaEnTierra_Coral", dict(teja="TejaCoral", carpinteria="CarpinteriaCoral", zocalo="ZocaloCoral", ancho=4.0, fondo=3.4, plantas=2, en_tierra=True)),
    ("CasaDeLaAldeaEnTierra_Amarilla", dict(teja="TejaAmarilla", carpinteria="CarpinteriaAzul", zocalo="ZocaloAmarillo", ancho=6.0, fondo=4.4, plantas=2, en_tierra=True)),
)

# ── Piezas con oficio y de carácter ─────────────────────────────────────────────────────────

def faro(nombre="Faro"):
    """Faro de franjas sobre un peñasco: 15 m, con galería y linterna. La puerta mira a -Y."""
    o = [cono(0, 0, -1.5, 0.7, 4.2, 3.4, "Roca", 9, 0.3), cono(0.4, 0.2, 0.7, 1.4, 3.1, 2.7, "Roca", 8)]
    for x, y, r in ((-3.4, 1.2, 1.3), (3.2, -1.6, 1.1), (1.5, 3.2, 1.4), (-2.2, -2.8, 1.0)):
        o.append(bola((x, y, 0.2), r, "Roca"))
    z0, alto, r0, r1 = 1.4, 11.0, 2.0, 1.35
    def radio(z): return r0 + (r1 - r0) * (z - z0) / alto
    for i in range(5):
        za, zb = z0 + alto * i / 5, z0 + alto * (i + 1) / 5
        o.append(cono(0, 0, za, zb, radio(za), radio(zb), "Cal" if i % 2 == 0 else "RojoFaro", 14))
    zt = z0 + alto
    o.append(cono(0, 0, zt, zt + 0.25, 2.05, 2.05, "Hierro", 14))
    n = 10
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        p0, p1 = (1.9 * math.cos(a0), 1.9 * math.sin(a0)), (1.9 * math.cos(a1), 1.9 * math.sin(a1))
        o.append(poste(p0[0], p0[1], zt + 0.25, zt + 1.15, 0.04, "Hierro", 6))
        o.append(viga((p0[0], p0[1], zt + 1.15), (p1[0], p1[1], zt + 1.15), 0.06, "Hierro"))
    o.append(cono(0, 0, zt + 0.25, zt + 2.0, 1.05, 1.05, "Luz", 10))
    for i in range(5):
        a = 2 * math.pi * i / 5
        o.append(poste(1.08 * math.cos(a), 1.08 * math.sin(a), zt + 0.25, zt + 2.0, 0.05, "Hierro", 6))
    o.append(cono(0, 0, zt + 2.0, zt + 2.2, 1.4, 1.4, "Hierro", 10))
    o.append(cono(0, 0, zt + 2.2, zt + 3.5, 1.4, 0.0, "TejaTurquesa", 10))
    o.append(bola((0, 0, zt + 3.62), 0.16, "Oro"))
    o.append(caja(-0.62, 0.62, -2.1, -1.85, 1.4, 3.45, "MaderaOscura", 0.03))
    o.append(caja(-0.5, 0.5, -2.14, -1.9, 1.42, 3.3, "Carpinteria", 0.03))
    o.append(caja(-1.0, 1.0, -3.0, -2.0, 0.85, 1.4, "Roca", 0.05))
    o.append(caja(-0.8, 0.8, -3.6, -2.9, 0.4, 0.9, "Roca", 0.05))
    for z in (6.2, 9.6):
        y = -radio(z) - 0.02
        o.append(caja(-0.34, 0.34, y - 0.05, y + 0.25, z - 0.1, z + 0.9, "MaderaOscura", 0.02))
        o.append(caja(-0.24, 0.24, y - 0.08, y + 0.2, z, z + 0.8, "Cristal", 0.0))
    return juntar(o, nombre)

def lonja(nombre="Lonja"):
    """Lonja abierta de 8 × 5 m: seis postes, tejado a dos aguas y mostradores con el pescado."""
    o = []
    for x in (-3.6, 0.0, 3.6):
        for y in (-2.1, 2.1):
            o.append(poste(x, y, 0, 3.0, 0.16, "MaderaOscura"))
    for y in (-2.1, 2.1):
        o.append(caja(-4.0, 4.0, y - 0.12, y + 0.12, 2.9, 3.15, "MaderaOscura", 0.03))
    for x in (-3.6, 0.0, 3.6):
        o.append(caja(x - 0.1, x + 0.1, -2.3, 2.3, 2.95, 3.15, "MaderaOscura", 0.03))
    o.append(prisma([(-2.9, 3.1), (2.9, 3.1), (0, 4.7)], -4.3, 4.3, "TejaTurquesa", eje='X'))
    o += hiladas((2.9, 3.1), (0, 4.7), -4.3, 4.3, ("TejaTurquesa", "TejaSombra"), 'X', vuelo=-0.012)
    o.append(caja(-4.4, 4.4, -0.12, 0.12, 4.85, 5.05, "MaderaOscura", 0.03))
    for x0 in (-3.2, 0.5):
        o.append(caja(x0, x0 + 2.7, -0.6, 0.6, 0.78, 0.92, "Madera", 0.03))
        for x in (x0 + 0.15, x0 + 2.55):
            for y in (-0.45, 0.45):
                o.append(caja(x - 0.07, x + 0.07, y - 0.07, y + 0.07, 0, 0.78, "MaderaOscura", 0.01))
        for i in range(5):
            x = x0 + 0.35 + i * 0.5
            p = caja(-0.3, 0.3, -0.09, 0.09, 0.92, 1.02, "Pescado" if i % 2 else "PescadoOscuro", 0.03)
            p.rotation_euler = (0, 0, math.radians(70 + 12 * i)); p.location = (x, 0.1 - 0.12 * (i % 2), 0); aplicar(p)
            o.append(p)
    o.append(cono(3.0, -1.2, 0, 0.95, 0.42, 0.42, "MaderaOscura", 10))
    o.append(caja(-3.5, -2.7, 1.0, 1.8, 0, 0.6, "Madera", 0.04))
    o.append(caja(-3.3, -2.6, 1.1, 1.75, 0.6, 1.1, "MaderaOscura", 0.04))
    for x in (-1.8, 1.8):
        o.append(viga((x, 0, 2.95), (x, 0, 2.5), 0.03, "Hierro"))
        o.append(caja(x - 0.13, x + 0.13, -0.13, 0.13, 2.2, 2.5, "Luz", 0.02))
    return juntar(o, nombre)

def secadero(nombre="SecaderoDePescado"):
    """Secadero: dos caballetes y tres varas con el pescado colgado."""
    o = []
    for x in (-1.7, 1.7):
        o.append(viga((x, -0.7, 0), (x, 0, 2.4), 0.11, "MaderaOscura"))
        o.append(viga((x, 0.7, 0), (x, 0, 2.4), 0.11, "MaderaOscura"))
        o.append(viga((x, -0.42, 0.95), (x, 0.42, 0.95), 0.08, "MaderaOscura"))
    for y, z in ((0, 2.35), (-0.33, 1.3), (0.33, 1.3)):
        o.append(viga((-1.95, y, z), (1.95, y, z), 0.07, "Madera"))
        for i in range(7):
            x = -1.4 + i * 0.46 + (0.1 if y else 0)
            largo = 0.5 + 0.08 * (i % 3)
            o.append(caja(x - 0.08, x + 0.08, y - 0.025, y + 0.025, z - 0.06 - largo, z - 0.06, "Pescado" if (i + (y > 0)) % 2 else "PescadoOscuro", 0.02))
    return juntar(o, nombre)

def tablas(x0, x1, y0, y1, z, a_lo_largo_de='x', ancho=0.5):
    """Entarimado de tablas alternas con la cara de arriba en z."""
    o, k = [], 0
    if a_lo_largo_de == 'x':
        y = y0
        while y < y1 - 0.01:
            o.append(caja(x0, x1, y + 0.02, min(y + ancho, y1) - 0.02, z - 0.16, z, "Madera" if k % 3 else "MaderaOscura", 0.02)); y += ancho; k += 1
    else:
        x = x0
        while x < x1 - 0.01:
            o.append(caja(x + 0.02, min(x + ancho, x1) - 0.02, y0, y1, z - 0.16, z, "Madera" if k % 3 else "MaderaOscura", 0.02)); x += ancho; k += 1
    return o

def baranda(a, b, z):
    """Tramo de barandilla de dos postes con pasamanos y cabo, de a a b (x, y)."""
    o = [poste(a[0], a[1], z, z + 1.0, 0.07, "Madera", 6), poste(b[0], b[1], z, z + 1.0, 0.07, "Madera", 6),
         viga((a[0], a[1], z + 0.98), (b[0], b[1], z + 0.98), 0.09, "Madera"), viga((a[0], a[1], z + 0.55), (b[0], b[1], z + 0.5), 0.05, "Cuerda")]
    return o

def banco(x, y, z, largo, grados=0):
    """Banco de tablas con respaldo, centrado en (x, y), mirando a -Y antes de girar."""
    o = [caja(-largo / 2, largo / 2, -0.22, 0.22, 0.4, 0.48, "Madera", 0.02), caja(-largo / 2, largo / 2, 0.16, 0.24, 0.48, 0.95, "Madera", 0.02)]
    for s in (-1, 1):
        o.append(caja(s * (largo / 2 - 0.12) - 0.05, s * (largo / 2 - 0.12) + 0.05, -0.2, 0.22, 0, 0.4, "MaderaOscura", 0.01))
    return poner(o, x, y, z, grados)

def farol_de_poste(x, y, z, alto=2.2):
    return [poste(x, y, z, z + alto, 0.05, "MaderaOscura", 6), caja(x - 0.12, x + 0.12, y - 0.12, y + 0.12, z + alto, z + alto + 0.3, "Luz", 0.02),
            caja(x - 0.17, x + 0.17, y - 0.17, y + 0.17, z + alto + 0.3, z + alto + 0.38, "Hierro", 0.02)]

def rellano(nombre="RellanoDeLaAldea"):
    """Rellano de descanso de 3 m de calle: la calle pasa a lo largo de X por y entre -0,7 y 0,7 y el rellano
    se abre 3 m hacia -Y, con banco, farol y barriles. En el borde de -Y queda un paso de 1,4 m para un ramal."""
    zp, o = ALTO_PLATAFORMA, []
    o += tablas(-1.5, 1.5, -3.7, 0.7, zp, 'y')
    for x in (-1.3, 1.3):
        for y in (-3.5, 0.5):
            o.append(poste(x, y, -2.4, zp - 0.16, 0.15, "MaderaOscura"))
    for y in (-3.5, -1.5, 0.5):
        o.append(caja(-1.55, 1.55, y - 0.1, y + 0.1, zp - 0.34, zp - 0.16, "MaderaOscura", 0.02))
    o += baranda((-1.42, 0.62), (1.42, 0.62), zp)
    o += baranda((-1.42, -0.85), (-1.42, -3.62), zp) + baranda((1.42, -0.85), (1.42, -3.62), zp)
    o += baranda((-1.42, -3.62), (-0.75, -3.62), zp) + baranda((0.75, -3.62), (1.42, -3.62), zp)
    o += banco(-0.95, -2.2, zp, 1.9, 90)
    o += farol_de_poste(1.42, -0.85, zp + 1.0, 1.3)
    o.append(cono(0.95, -3.1, zp, zp + 0.9, 0.36, 0.36, "MaderaOscura", 10))
    o.append(cono(0.95, -3.1, zp + 0.3, zp + 0.4, 0.38, 0.38, "Hierro", 10))
    o.append(cono(0.9, -2.3, zp, zp + 0.4, 0.26, 0.3, "Boya", 8)); o.append(bola((0.9, -2.3, zp + 0.6), 0.32, "Hojas", 2)); o.append(bola((1.0, -2.2, zp + 0.85), 0.14, "Flor"))
    return juntar(o, nombre)

def tienda(nombre="TiendaDeLaAldea", toldo="ToldoRojo", teja="TejaTurquesa", zocalo="Zocalo", genero="Pescado"):
    """Tienda sobre pilotes: plataforma de 5,2 × 4,6 m con un quiosco encalado, mostrador y toldo de rayas.
    Se llega por +Y (hueco de 1,4 m en la barandilla); el quiosco queda al fondo, en -Y."""
    zp, o = ALTO_PLATAFORMA, []
    o += tablas(-2.6, 2.6, -2.3, 2.3, zp, 'y')
    for x in (-2.4, 0.0, 2.4):
        for y in (-2.1, 2.1):
            o.append(poste(x, y, -2.4, zp - 0.16, 0.16, "MaderaOscura"))
    for y in (-2.1, 0.0, 2.1):
        o.append(caja(-2.65, 2.65, y - 0.1, y + 0.1, zp - 0.34, zp - 0.16, "MaderaOscura", 0.02))
    o += baranda((-2.52, 2.22), (-0.75, 2.22), zp) + baranda((0.75, 2.22), (2.52, 2.22), zp)
    o += baranda((-2.52, 2.22), (-2.52, -0.6), zp) + baranda((2.52, 2.22), (2.52, -0.6), zp)
    # Quiosco: muro del fondo y costados, mostrador con género, tejado y toldo.
    o.append(caja(-2.3, 2.3, -2.2, -2.02, zp, zp + 2.4, "Cal", 0.04))
    o.append(caja(-2.34, 2.34, -2.24, -1.98, zp, zp + 0.6, zocalo, 0.03))
    for s in (-1, 1):
        o.append(caja(min(s * 2.3, s * 2.12), max(s * 2.3, s * 2.12), -2.2, -0.8, zp, zp + 2.4, "Cal", 0.04))
    o.append(caja(-2.1, 2.1, -0.95, -0.5, zp + 0.86, zp + 1.0, "Madera", 0.03))
    o.append(caja(-2.05, 2.05, -0.62, -0.54, zp, zp + 0.86, "MaderaOscura", 0.02))
    for i in range(6):
        x = -1.6 + i * 0.64
        if genero == "Pescado":
            p = caja(-0.28, 0.28, -0.08, 0.08, zp + 1.0, zp + 1.1, "Pescado" if i % 2 else "PescadoOscuro", 0.03)
            p.rotation_euler = (0, 0, math.radians(60 + 15 * i)); p.location = (x, -0.72, 0); aplicar(p); o.append(p)
        else:
            o.append(caja(x - 0.24, x + 0.24, -0.92, -0.56, zp + 1.0, zp + 1.12, "MaderaOscura", 0.02))
            for dx, dy in ((-0.1, -0.08), (0.1, -0.06), (0.0, 0.08)):
                o.append(bola((x + dx, -0.74 + dy, zp + 1.18), 0.1, genero if i % 2 else "Boya"))
    o.append(prisma([(-2.55, zp + 2.4), (-0.3, zp + 2.4), (-1.42, zp + 3.3)], -2.6, 2.6, teja, eje='X'))
    o += hiladas((1.12, zp + 2.4), (0, zp + 3.3), -2.6, 2.6, (teja, SOMBRA_DE_TEJA.get(teja, "TejaSombra")), 'X', n=4)[0:0]
    rayas = 8
    for r in range(rayas):
        x0 = -2.4 + r * 4.8 / rayas
        o.append(prisma([(-0.35, zp + 2.42), (1.1, zp + 2.0), (1.1, zp + 1.9), (-0.35, zp + 2.32)], x0, x0 + 4.8 / rayas, toldo if r % 2 == 0 else "Cal", eje='X'))
    for s in (-1, 1):
        o.append(poste(s * 2.3, 1.05, zp, zp + 2.0, 0.05, "MaderaOscura", 6))
    o.append(cono(-1.9, 1.5, zp, zp + 0.9, 0.36, 0.36, "MaderaOscura", 10))
    o.append(caja(1.4, 2.2, 1.0, 1.8, zp, zp + 0.6, "Madera", 0.04)); o.append(caja(1.5, 2.1, 1.1, 1.7, zp + 0.6, zp + 1.05, "MaderaOscura", 0.04))
    o += farol_de_poste(-2.52, 2.22, zp + 1.0, 1.3)
    return juntar(o, nombre)

def mirador(nombre="MiradorDeLaAldea"):
    """Templete de descanso de 5 × 5 m sobre pilotes, con tejado a cuatro aguas y bancos. Paso de 1,6 m en el
    centro de cada lado."""
    zp, o = ALTO_PLATAFORMA, []
    o += tablas(-2.5, 2.5, -2.5, 2.5, zp, 'y')
    for x in (-2.25, 2.25):
        for y in (-2.25, 2.25):
            o.append(poste(x, y, -2.4, zp + 2.7, 0.14, "MaderaOscura"))
            o += baranda((x, y), (x, y * 0.36), zp) + baranda((x, y), (x * 0.36, y), zp)
    for y in (-2.25, 0.0, 2.25):
        o.append(caja(-2.55, 2.55, y - 0.1, y + 0.1, zp - 0.34, zp - 0.16, "MaderaOscura", 0.02))
    for a, b in (((-2.4, -2.25), (2.4, -2.25)), ((-2.4, 2.25), (2.4, 2.25)), ((-2.25, -2.4), (-2.25, 2.4)), ((2.25, -2.4), (2.25, 2.4))):
        o.append(viga((a[0], a[1], zp + 2.65), (b[0], b[1], zp + 2.65), 0.16, "MaderaOscura"))
    o.append(cono(0, 0, zp + 2.7, zp + 4.5, 3.75, 0.0, "TejaTurquesa", 4, math.pi / 4))
    o.append(cono(0, 0, zp + 2.62, zp + 2.74, 3.9, 3.9, "TejaSombra", 4, math.pi / 4))
    o.append(bola((0, 0, zp + 4.6), 0.16, "Oro"))
    o += banco(-1.75, -1.55, zp, 1.3, 90) + banco(1.75, 1.55, zp, 1.3, -90)
    o.append(viga((0, 0, zp + 2.7), (0, 0, zp + 2.2), 0.03, "Hierro")); o.append(caja(-0.14, 0.14, -0.14, 0.14, zp + 1.9, zp + 2.2, "Luz", 0.02))
    return juntar(o, nombre)

def fuente(nombre="FuenteDeLaPlaza"):
    """Fuente de plaza de tres tazas, 7,2 m de diámetro y 4,3 m de alto, con un escalón alrededor."""
    o = [cono(0, 0, 0, 0.2, 3.6, 3.6, "PiedraLila", 16), cono(0, 0, 0.2, 0.95, 3.2, 3.05, "PiedraA", 16),
         cono(0, 0, 0.95, 1.08, 3.3, 3.3, "PiedraLila", 16), cono(0, 0, 1.0, 1.1, 2.85, 2.85, "Agua", 16),
         cono(0, 0, 1.0, 2.2, 0.5, 0.38, "PiedraLila", 10), cono(0, 0, 2.2, 2.5, 0.6, 1.7, "PiedraA", 14),
         cono(0, 0, 2.46, 2.53, 1.5, 1.5, "Agua", 14), cono(0, 0, 2.5, 3.4, 0.3, 0.24, "PiedraLila", 10),
         cono(0, 0, 3.4, 3.6, 0.35, 0.95, "PiedraA", 12), cono(0, 0, 3.57, 3.63, 0.8, 0.8, "Agua", 12),
         cono(0, 0, 3.6, 4.0, 0.14, 0.08, "PiedraLila", 8), bola((0, 0, 4.12), 0.2, "Oro", 2)]
    for i in range(8):
        a = 2 * math.pi * i / 8
        o.append(viga((1.55 * math.cos(a), 1.55 * math.sin(a), 2.5), (2.3 * math.cos(a), 2.3 * math.sin(a), 1.1), 0.07, "Agua"))
    return juntar(o, nombre)

def arbol_de_plaza(nombre="ArbolDePlaza"):
    """Árbol de copa redonda en un alcorque de piedra con banco corrido alrededor."""
    o = [cono(0, 0, 0, 0.5, 1.5, 1.5, "PiedraLila", 8, math.pi / 8), cono(0, 0, 0.4, 0.52, 1.25, 1.25, "Hojas", 8, math.pi / 8),
         cono(0, 0, 0.3, 0.46, 1.95, 1.95, "Madera", 8, math.pi / 8), cono(0, 0, 0, 0.3, 1.75, 1.75, "PiedraA", 8, math.pi / 8),
         cono(0, 0, 0.4, 3.2, 0.34, 0.24, "MaderaOscura", 8)]
    for k, (x, y, z, r) in enumerate(((0, 0, 4.4, 1.9), (-1.1, 0.4, 3.9, 1.3), (1.2, -0.3, 4.0, 1.35), (0.2, 1.1, 3.8, 1.2), (-0.3, -1.1, 3.9, 1.15))):
        o.append(bola((x, y, z), r, "Hojas" if k % 2 == 0 else "CopaClara", 2))
    return juntar(o, nombre)

def puesto(nombre="PuestoDeMercado", toldo="ToldoRojo", genero="Flor"):
    """Puesto de mercado de 3 × 2 m: mostrador con género, cuatro palos y toldo de rayas a dos aguas. Se atiende por -Y."""
    o = [caja(-1.5, 1.5, -0.2, 0.75, 0.82, 0.95, "Madera", 0.03), caja(-1.45, 1.45, -0.18, -0.1, 0, 0.82, "MaderaOscura", 0.02)]
    for x in (-1.45, 1.45):
        for y in (-0.85, 0.85):
            o.append(poste(x, y, 0, 2.3 if y < 0 else 2.6, 0.05, "MaderaOscura", 6))
        o.append(caja(x - 0.04, x + 0.04, -0.15, 0.75, 0, 0.82, "MaderaOscura", 0.01))
    rayas = 6
    for r in range(rayas):
        x0 = -1.65 + r * 3.3 / rayas
        o.append(prisma([(-1.25, 2.2), (1.0, 2.62), (1.0, 2.7), (-1.25, 2.28)], x0, x0 + 3.3 / rayas, toldo if r % 2 == 0 else "Cal", eje='X'))
    for i in range(5):
        x = -1.15 + i * 0.58
        o.append(caja(x - 0.24, x + 0.24, 0.0, 0.6, 0.95, 1.08, "MaderaOscura", 0.02))
        for dx, dy in ((-0.1, 0.2), (0.1, 0.25), (0.0, 0.42), (-0.08, 0.45)):
            o.append(bola((x + dx, dy, 1.14), 0.1, genero if i % 2 == 0 else ("Boya" if genero != "Boya" else "TejaAmarilla")))
    o.append(caja(1.7, 2.3, -0.3, 0.3, 0, 0.5, "Madera", 0.04)); o.append(cono(-2.0, 0.2, 0, 0.85, 0.34, 0.34, "MaderaOscura", 10))
    return juntar(o, nombre)

def losa(puntos, z0, z1, material):
    """Polígono en planta (x, y) con grosor de z0 a z1."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y, z0)) for x, y in puntos]; b = [bm.verts.new((x, y, z1)) for x, y in puntos]
    bm.faces.new(list(reversed(a))); bm.faces.new(b)
    for i in range(len(puntos)):
        bm.faces.new([a[i], a[(i + 1) % len(puntos)], b[(i + 1) % len(puntos)], b[i]])
    return objeto("losa", bm, material)

def rosa_de_los_vientos(nombre="RosaDeLosVientos"):
    """Rosa de los vientos de 9,4 m sobre una tarima de piedra de dos peldaños bajos (z = 0 es el peldaño de fuera).
    El cuerpo baja 1,2 m para asentarse en una plaza que no es llana."""
    zr = 0.2
    o = [cono(0, 0, -1.2, 0.0, 5.3, 5.3, "PiedraGris", 32), cono(0, 0, 0.0, zr - 0.03, 4.75, 4.75, "PiedraLila", 32),
         cono(0, 0, zr - 0.03, zr - 0.015, 4.7, 4.7, "Zocalo", 32), cono(0, 0, zr - 0.015, zr, 4.4, 4.4, "Cal", 32)]
    for k in range(8):
        ang, largo, mat_ = k * math.pi / 4, (4.2 if k % 2 == 0 else 2.7), ("Zocalo" if k % 2 == 0 else "RojoFaro")
        d, p = (math.cos(ang), math.sin(ang)), (-math.sin(ang), math.cos(ang))
        o.append(losa([(d[0] * largo, d[1] * largo), (p[0] * 0.55, p[1] * 0.55), (0, 0)], zr, zr + 0.015, mat_))
        o.append(losa([(d[0] * largo, d[1] * largo), (0, 0), (-p[0] * 0.55, -p[1] * 0.55)], zr, zr + 0.015, "TejaTurquesa" if k % 2 == 0 else "TejaAmarilla"))
    o.append(cono(0, 0, zr + 0.015, zr + 0.03, 0.7, 0.7, "Oro", 16))
    return juntar(o, nombre)

def banderines(nombre="Banderines", luz=20.0):
    """Ristra de banderines entre dos palos de 5,2 m separados «luz» metros (a lo largo de X)."""
    colores = ("RojoFaro", "TejaAmarilla", "TejaTurquesa", "Cal", "Zocalo")
    o = [poste(-luz / 2, 0, 0, 5.2, 0.09, "MaderaOscura", 8), poste(luz / 2, 0, 0, 5.2, 0.09, "MaderaOscura", 8),
         bola((-luz / 2, 0, 5.3), 0.14, "Oro"), bola((luz / 2, 0, 5.3), 0.14, "Oro")]
    n = int(luz / 0.8)
    def cuerda(t): return (-luz / 2 + luz * t, 0, 5.1 - 1.2 * (1 - (2 * t - 1) ** 2))
    for i in range(n):
        p0, p1 = cuerda(i / n), cuerda((i + 1) / n)
        o.append(viga(p0, p1, 0.03, "Cuerda"))
        xm, zm = (p0[0] + p1[0]) / 2, (p0[2] + p1[2]) / 2
        o.append(prisma([(xm - 0.3, zm), (xm + 0.3, zm), (xm, zm - 0.62)], -0.012, 0.012, colores[i % len(colores)], eje='Y'))
    return juntar(o, nombre)

def paseo(nombre="PaseoDeTablas"):
    """Tramo de paseo de tablas de 3 × 1,8 m para ir por la arena; apoya en dos durmientes."""
    o = tablas(-1.5, 1.5, -0.9, 0.9, 0.2, 'y', 0.45)
    for x in (-1.1, 1.1):
        o.append(caja(x - 0.12, x + 0.12, -0.95, 0.95, -0.4, 0.04, "MaderaOscura", 0.02))
    return juntar(o, nombre)

def reloj(radio):
    """Esfera de reloj mirando a -Y, pegada al plano y = 0, centrada en el origen."""
    def disco(r, y0, material):
        return prisma([(r * math.cos(2 * math.pi * i / 16), r * math.sin(2 * math.pi * i / 16)) for i in range(16)], y0, 0.0, material, eje='Y')
    return [disco(radio + 0.16, -0.08, "Oro"), disco(radio, -0.14, "Cal"),
            viga((0, -0.17, 0), (0, -0.17, radio * 0.72), 0.08, "Hierro"), viga((0, -0.17, 0), (radio * 0.5, -0.17, -radio * 0.2), 0.08, "Hierro"),
            bola((0, -0.17, 0), 0.1, "Oro")]

def torre_del_reloj(nombre="TorreDelReloj"):
    """Torre del reloj de 4,6 m de lado y 22 m: paso con arco abajo, tres esferas, campanario y aguja de pizarra.
    Va unida a las casas de ciudad (mismos materiales y soportal a la misma altura)."""
    w, bajo, o = 4.6, 3.6, []
    for s in (-1, 1):
        o.append(caja(min(s * w / 2, s * (w / 2 - 0.7)), max(s * w / 2, s * (w / 2 - 0.7)), -w / 2, -w / 2 + 0.65, 0, bajo, "PiedraLila", 0.04))
    o.append(arco(-w / 2 + 0.7, w / 2 - 0.7, bajo - 0.35 - (w - 1.4) / 2, bajo, -w / 2 + 0.03, -w / 2 + 0.6, "PiedraA"))
    o.append(caja(-w / 2, w / 2, -w / 2 + 0.65, w / 2, 0, bajo, "PiedraA", 0.03))
    o.append(caja(-1.25, 1.25, -w / 2 + 0.6, -w / 2 + 0.68, 0, 2.7, "Fondo", 0.0))
    alto = 13.2
    o.append(caja(-w / 2, w / 2, -w / 2, w / 2, bajo, alto, "PiedraA", 0.03))
    for z in (bajo, 8.2, alto):
        o.append(caja(-w / 2 - 0.1, w / 2 + 0.1, -w / 2 - 0.1, w / 2 + 0.1, z - 0.16, z + 0.16, "PiedraLila", 0.03))
    for s in (-1, 1):
        o += sillares(s * (w / 2 - 0.01), -w / 2 - 0.06, -w / 2 + 0.3, bajo + 0.16, alto - 0.16, "PiedraLila", -s)
    o += poner(ventana(0.9, 1.4, "CarpinteriaAzul", "PiedraLila", jardinera=False), 0, -w / 2, 5.0)
    o += poner(reloj(1.25), 0, -w / 2, 10.6)
    o += poner(reloj(1.25), w / 2, 0, 10.6, 90)
    o += poner(reloj(1.25), -w / 2, 0, 10.6, -90)
    zc = alto + 0.16
    o.append(caja(-w / 2 + 0.25, w / 2 - 0.25, -w / 2 + 0.25, w / 2 - 0.25, zc, zc + 2.6, "PiedraA", 0.03))
    o.append(caja(-0.65, 0.65, -w / 2 + 0.2, w / 2 - 0.2, zc + 0.35, zc + 2.1, "Fondo", 0.0))
    o.append(caja(-w / 2 + 0.2, w / 2 - 0.2, -0.65, 0.65, zc + 0.35, zc + 2.1, "Fondo", 0.0))
    o.append(bola((0, 0, zc + 1.3), 0.5, "Oro", 2))
    o.append(caja(-w / 2 - 0.05, w / 2 + 0.05, -w / 2 - 0.05, w / 2 + 0.05, zc + 2.6, zc + 2.9, "PiedraLila", 0.03))
    o.append(cono(0, 0, zc + 2.9, zc + 8.4, w * 0.76, 0.0, "Pizarra", 4, math.pi / 4))
    o.append(poste(0, 0, zc + 8.3, zc + 9.3, 0.05, "Hierro", 6))
    o.append(bola((0, 0, zc + 9.35), 0.16, "Oro"))
    return juntar(o, nombre)

def arbol_con_columpio(nombre="ArbolConColumpio"):
    """Árbol grande de copa redonda con la rama del columpio. El columpio va aparte (columpio_del_arbol), en el
    mismo sistema de coordenadas, para que en Unity se balancee colgado de la rama."""
    o = [cono(0, 0, 0, 3.4, 0.6, 0.4, "MaderaOscura", 8), cono(0, 0, -0.1, 0.5, 0.95, 0.6, "MaderaOscura", 8),
         viga((0, 0, 2.9), (2.7, 0, 3.6), 0.3, "MaderaOscura"), viga((0, 0, 3.0), (-1.7, 0.6, 4.2), 0.26, "MaderaOscura")]
    for k, (x, y, z, r) in enumerate(((0, 0, 5.4, 2.7), (-2.0, 0.6, 4.8, 1.9), (2.1, -0.5, 5.2, 2.0), (0.4, 1.7, 4.7, 1.7), (3.1, 0.3, 4.6, 1.35), (-0.6, -1.6, 4.8, 1.6))):
        o.append(bola((x, y, z), r, "Hojas" if k % 2 == 0 else "CopaClara", 2))
    # Argollas donde se atan las cuerdas, a la misma altura (el eje del balanceo).
    for x in (1.55, 2.25):
        o.append(caja(x - 0.06, x + 0.06, -0.06, 0.06, ALTO_DEL_EJE - 0.04, 2.9 + 0.7 * x / 2.7 - 0.1, "Hierro", 0.01))
    return juntar(o, nombre)

ALTO_DEL_EJE = 3.22   # altura (m) de la que cuelgan las cuerdas: justo bajo la rama en las dos argollas
ALTO_DEL_ASIENTO = 0.55

def columpio_del_arbol(nombre="ColumpioDelArbol"):
    """Cuerdas y tabla del columpio, en el sitio del árbol. Cuelgan de y = 0, z = ALTO_DEL_EJE (el eje del
    balanceo va a lo largo de X); la tabla queda con su cara de arriba a ALTO_DEL_ASIENTO del suelo."""
    o = []
    for x in (1.55, 2.25):
        o.append(viga((x, 0, ALTO_DEL_EJE), (x, 0, ALTO_DEL_ASIENTO - 0.02), 0.045, "Cuerda"))
    o.append(caja(1.4, 2.4, -0.22, 0.22, ALTO_DEL_ASIENTO - 0.09, ALTO_DEL_ASIENTO, "Madera", 0.02))
    return juntar(o, nombre)

# ── Fotos de maqueta ────────────────────────────────────────────────────────────────────────

def foto(ruta, camara, mira, suelo, lente=35):
    esc = bpy.context.scene
    esc.collection.objects.link(bpy.data.objects.new("Suelo", None)) if False else None
    s = caja(-200, 200, -200, 200, -0.02 if suelo != "Agua" else -3.0, 0.0, suelo, 0.0)
    cam = bpy.data.objects.new("Camara", bpy.data.cameras.new("Camara")); esc.collection.objects.link(cam)
    cam.location = camara; cam.data.lens = lente
    d = Vector(mira) - Vector(camara)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    esc.camera = cam
    sol = bpy.data.objects.new("Sol", bpy.data.lights.new("Sol", 'SUN')); esc.collection.objects.link(sol)
    sol.data.energy = 3.4; sol.data.angle = math.radians(3); sol.rotation_euler = (math.radians(52), 0, math.radians(-35))
    mundo = bpy.data.worlds.new("Cielo"); esc.world = mundo; mundo.use_nodes = True
    mundo.node_tree.nodes["Background"].inputs[0].default_value = (*hexa("#bfe3f2"), 1)
    mundo.node_tree.nodes["Background"].inputs[1].default_value = 1.1
    esc.render.engine = 'CYCLES'; esc.cycles.samples = 40; esc.cycles.use_denoising = True
    esc.render.resolution_x, esc.render.resolution_y = 1280, 720
    esc.view_settings.view_transform = 'Standard'
    esc.render.filepath = ruta
    bpy.ops.render.render(write_still=True)
    print("foto", ruta)

# ── Pueblo de Will: casas de campo de entramado y paja ──────────────────────────────────────

import random

def roca(c, r, material="Roca", semilla=0, chata=0.75):
    """Peñasco irregular: icosfera deformada con «semilla», achatada."""
    az = random.Random(semilla)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=r)
    for v in bm.verts:
        k = 1.0 + az.uniform(-0.16, 0.16)
        v.co = Vector((v.co.x * k, v.co.y * k, v.co.z * k * chata)) + Vector(c)
    return objeto("roca", bm, material)

def tejado_de_paja(u, zt, h, d0, d1, eje):
    """Tejado de paja a dos aguas, grueso y con vuelo: «u» es el medio ancho del faldón (con vuelo)."""
    o = [prisma([(-u, zt - 0.25), (u, zt - 0.25), (u * 0.12, zt + h + 0.3), (-u * 0.12, zt + h + 0.3)], d0, d1, "Paja", eje=eje)]
    o += hiladas((u, zt - 0.25), (u * 0.12, zt + h + 0.3), d0, d1, ("Paja", "PajaSombra"), eje, n=4, vuelo=-0.015)
    o.append(prisma([(-u * 0.2, zt + h + 0.25), (u * 0.2, zt + h + 0.25), (u * 0.1, zt + h + 0.62), (-u * 0.1, zt + h + 0.62)], d0 - 0.1, d1 + 0.1, "PajaSombra", eje=eje))
    return o

def casa_del_pueblo(nombre, ancho=6.0, fondo=5.0, plantas=1, contra="ContraRoja", hastial_a_la_calle=False, leñera=True, espejo=False):
    """Casa de campo del pueblo de Will: zócalo de piedra, estuco con entramado de madera, tejado de paja,
    porche con tejadillo y chimenea de piedra. La fachada mira a -Y; la puerta, descentrada."""
    w, f, zs, piso = ancho, fondo, 0.6, 2.6
    zt = zs + piso * plantas
    o = [caja(-w / 2 - 0.08, w / 2 + 0.08, -f / 2 - 0.08, f / 2 + 0.08, -0.6, zs, "PiedraCampo", 0.05),
         caja(-w / 2, w / 2, -f / 2, f / 2, zs, zt, "Estuco", 0.03)]
    # Entramado: postes en esquinas y tercios, soleras en cada forjado y riostras.
    xs = [-w / 2 + 0.11, -w / 6, w / 6, w / 2 - 0.11]
    for y in (-f / 2 - 0.03, f / 2 + 0.03):
        for x in xs:
            o.append(caja(x - 0.11, x + 0.11, y - 0.05, y + 0.05, zs, zt, "MaderaOscura", 0.01))
        for k in range(plantas + 1):
            z = zs + piso * k
            o.append(caja(-w / 2 - 0.02, w / 2 + 0.02, y - 0.06, y + 0.06, z - 0.1 if k else z, z + 0.12 if k < plantas else z, "MaderaOscura", 0.01))
        for s in (-1, 1):
            o.append(viga((s * (w / 2 - 0.15), y, zt - piso + 0.15), (s * (w / 6 + 0.1), y, zt - 0.15), 0.13, "MaderaOscura"))
    for x in (-w / 2 - 0.03, w / 2 + 0.03):
        for y in (-f / 2 + 0.11, 0.0, f / 2 - 0.11):
            o.append(caja(x - 0.05, x + 0.05, y - 0.11, y + 0.11, zs, zt, "MaderaOscura", 0.01))
        for k in range(1, plantas + 1):
            o.append(caja(x - 0.06, x + 0.06, -f / 2, f / 2, zs + piso * k - 0.1, zs + piso * k + (0.12 if k < plantas else 0), "MaderaOscura", 0.01))
    # Tejado y hastiales.
    if hastial_a_la_calle:
        h = w * 0.55
        o.append(prisma([(-w / 2, zt - 0.02), (w / 2, zt - 0.02), (0, zt + h)], -f / 2, f / 2, "Estuco", eje='Y'))
        for y in (-f / 2 - 0.03, f / 2 + 0.03):
            o.append(viga((-w / 2 + 0.2, y, zt + 0.1), (0, y, zt + h - 0.25), 0.12, "MaderaOscura"))
            o.append(viga((w / 2 - 0.2, y, zt + 0.1), (0, y, zt + h - 0.25), 0.12, "MaderaOscura"))
            o.append(caja(-0.07, 0.07, y - 0.05, y + 0.05, zt, zt + h - 0.4, "MaderaOscura", 0.01))
        o += tejado_de_paja(w / 2 + 0.6, zt, h, -f / 2 - 0.55, f / 2 + 0.55, 'Y')
        o += poner(ventana(0.7, 0.8, contra, "MaderaOscura", jardinera=False), w * 0.2, -f / 2, zt + 0.35)
    else:
        h = f * 0.6
        o.append(prisma([(-f / 2, zt - 0.02), (f / 2, zt - 0.02), (0, zt + h)], -w / 2, w / 2, "Estuco", eje='X'))
        o += tejado_de_paja(f / 2 + 0.6, zt, h, -w / 2 - 0.55, w / 2 + 0.55, 'X')
    # Chimenea de piedra en un costado.
    o.append(caja(w / 2 - 0.25, w / 2 + 0.55, f * 0.12, f * 0.12 + 0.95, -0.6, zt + h + 0.9, "PiedraCampo", 0.05))
    o.append(caja(w / 2 - 0.33, w / 2 + 0.63, f * 0.12 - 0.08, f * 0.12 + 1.03, zt + h + 0.9, zt + h + 1.08, "Roca", 0.03))
    # Puerta con porche y tejadillo de paja.
    xd = -w * 0.2
    o.append(caja(xd - 0.62, xd + 0.62, -f / 2 - 0.08, -f / 2 + 0.02, zs, zs + 2.15, "MaderaOscura", 0.02))
    o.append(caja(xd - 0.5, xd + 0.5, -f / 2 - 0.12, -f / 2 - 0.04, zs + 0.02, zs + 2.03, contra, 0.02))
    o.append(bola((xd + 0.34, -f / 2 - 0.15, zs + 1.0), 0.05, "Oro"))
    o.append(caja(xd - 1.1, xd + 1.1, -f / 2 - 1.3, -f / 2, 0.36, zs, "PiedraCampo", 0.04))
    o.append(caja(xd - 0.9, xd + 0.9, -f / 2 - 1.75, -f / 2 - 1.25, -0.6, 0.38, "PiedraCampo", 0.04))
    o.append(caja(xd - 0.8, xd + 0.8, -f / 2 - 2.15, -f / 2 - 1.7, -0.6, 0.19, "PiedraCampo", 0.04))
    for s in (-1, 1):
        o.append(poste(xd + s * 0.95, -f / 2 - 1.15, zs, zs + 2.3, 0.08, "MaderaOscura", 6))
    o.append(prisma([(-f / 2 - 1.45, zs + 2.25), (-f / 2, zs + 2.75), (-f / 2, zs + 2.95), (-f / 2 - 1.45, zs + 2.45)], xd - 1.2, xd + 1.2, "Paja", eje='X'))
    o.append(prisma([(-f / 2 - 1.5, zs + 2.22), (-f / 2 - 1.3, zs + 2.3), (-f / 2 - 1.3, zs + 2.5), (-f / 2 - 1.5, zs + 2.44)], xd - 1.25, xd + 1.25, "PajaSombra", eje='X'))
    # Ventanas: fachada, trasera y costado libre.
    for k in range(plantas):
        z = zs + piso * k + 0.95
        o += poner(ventana(0.9, 1.05, contra, "MaderaOscura"), w * 0.24, -f / 2, z)
        if k > 0:
            o += poner(ventana(0.9, 1.05, contra, "MaderaOscura"), xd, -f / 2, z)
        o += poner(ventana(0.9, 1.05, contra, "MaderaOscura", jardinera=False), -w * 0.2, f / 2, z, 180)
        o += poner(ventana(0.8, 1.0, contra, "MaderaOscura", jardinera=False), -w / 2, 0.0, z, -90)
    # Farol junto a la puerta y, en un costado, la leñera o un banco.
    o.append(viga((xd + 0.85, -f / 2, zs + 2.0), (xd + 0.85, -f / 2 - 0.4, zs + 2.1), 0.05, "Hierro"))
    o.append(caja(xd + 0.74, xd + 0.96, -f / 2 - 0.52, -f / 2 - 0.3, zs + 1.72, zs + 2.02, "Luz", 0.02))
    if leñera:
        x0 = -w / 2 - 1.5
        o.append(prisma([(x0 - 0.15, 1.75), (-w / 2, 2.25), (-w / 2, 2.4), (x0 - 0.15, 1.9)], -f / 2 + 0.6, f / 2 - 0.6, "PajaSombra", eje='Y'))
        for y in (-f / 2 + 0.75, f / 2 - 0.75):
            o.append(poste(x0, y, -0.6, 1.8, 0.08, "MaderaOscura", 6))
        az = random.Random(len(nombre))
        for fila in range(4):
            for i in range(int((f - 1.8) / 0.34)):
                y = -f / 2 + 1.0 + i * 0.34
                o.append(cono(-w / 2 - 0.75, y, 0, 0.02, 0.0, 0.0, "Madera", 3)) if False else None
                t = caja(-w / 2 - 1.3, -w / 2 - 0.2, y - 0.14, y + 0.14, fila * 0.3 - 0.02, fila * 0.3 + 0.26, "Madera" if az.random() < 0.6 else "MaderaOscura", 0.05)
                o.append(t)
    else:
        o += banco(w * 0.24, -f / 2 - 0.45, 0.0, 1.5, 180)
    o = [x for x in o if x is not None]
    casa = juntar(o, nombre)
    if espejo:
        casa.scale = (-1, 1, 1); aplicar(casa)
    return casa

VARIANTES_DEL_PUEBLO = (
    ("CasaDelPueblo_A", dict(ancho=6.0, fondo=5.0, plantas=1, contra="ContraRoja")),
    ("CasaDelPueblo_B", dict(ancho=5.2, fondo=4.6, plantas=2, contra="ContraVerde", leñera=False)),
    ("CasaDelPueblo_C", dict(ancho=7.2, fondo=5.4, plantas=1, contra="CarpinteriaAzul", espejo=True)),
    ("CasaDelPueblo_D", dict(ancho=5.6, fondo=6.0, plantas=2, contra="ContraRoja", hastial_a_la_calle=True, leñera=False)),
    ("CasaDelPueblo_E", dict(ancho=4.6, fondo=4.2, plantas=1, contra="ContraVerde", hastial_a_la_calle=True, espejo=True)),
    ("CasaDelPueblo_F", dict(ancho=8.4, fondo=6.0, plantas=2, contra="CarpinteriaAzul")),
)

def granero(nombre="GraneroDelPueblo"):
    """Granero de tablas rojas de 8 × 6,5 m, con portón doble, pajar y balas de paja."""
    w, f, zt = 8.0, 6.5, 3.6
    o = [caja(-w / 2 - 0.08, w / 2 + 0.08, -f / 2 - 0.08, f / 2 + 0.08, -0.6, 0.4, "PiedraCampo", 0.05)]
    n = 16
    for i in range(n):
        x0 = -w / 2 + i * w / n
        o.append(caja(x0 + 0.01, x0 + w / n - 0.01, -f / 2, f / 2, 0.4, zt, "RojoGranero" if i % 4 else "ContraRoja", 0.01))
    h = w * 0.5
    o.append(prisma([(-w / 2, zt - 0.02), (w / 2, zt - 0.02), (0, zt + h)], -f / 2, f / 2, "RojoGranero", eje='Y'))
    o += tejado_de_paja(w / 2 + 0.6, zt, h, -f / 2 - 0.6, f / 2 + 0.6, 'Y')
    for y in (-f / 2 - 0.04,):
        o.append(caja(-1.75, 1.75, y - 0.05, y + 0.03, 0.4, 3.25, "Cal", 0.02))
        for s in (-1, 1):
            x0, x1 = (0.04, 1.62) if s > 0 else (-1.62, -0.04)
            o.append(caja(x0, x1, y - 0.09, y - 0.01, 0.45, 3.12, "RojoGranero", 0.02))
            o.append(viga((x0 + 0.08, y - 0.11, 0.55), (x1 - 0.08, y - 0.11, 3.0), 0.1, "Cal"))
            o.append(viga((x1 - 0.08, y - 0.11, 0.55), (x0 + 0.08, y - 0.11, 3.0), 0.1, "Cal"))
        o.append(caja(-0.7, 0.7, y - 0.05, y + 0.03, zt + 0.5, zt + 1.95, "Cal", 0.02))
        o.append(caja(-0.58, 0.58, y - 0.08, y, zt + 0.6, zt + 1.85, "MaderaOscura", 0.02))
        o.append(viga((0, y, zt + h - 0.5), (0, y - 1.3, zt + h - 0.4), 0.12, "MaderaOscura"))
        o.append(viga((0, y - 1.2, zt + h - 0.45), (0, y - 1.2, zt + 2.2), 0.03, "Cuerda"))
    for s in (-1, 1):
        o.append(caja(s * w / 2 - 0.1, s * w / 2 + 0.1, -f / 2 - 0.1, -f / 2 + 0.1, 0.4, zt, "Cal", 0.02))
    for x, y, z in ((2.9, -4.3, 0), (3.9, -4.4, 0), (3.4, -4.35, 0.62), (-3.3, -4.2, 0)):
        o.append(caja(x - 0.5, x + 0.5, y - 0.32, y + 0.32, z, z + 0.62, "Paja", 0.06))
        o.append(caja(x - 0.2, x - 0.14, y - 0.33, y + 0.33, z - 0.01, z + 0.63, "Cuerda", 0.0))
        o.append(caja(x + 0.14, x + 0.2, y - 0.33, y + 0.33, z - 0.01, z + 0.63, "Cuerda", 0.0))
    return juntar(o, nombre)

def pozo(nombre="PozoDelPueblo"):
    o = [cono(0, 0, -0.5, 0.9, 1.15, 1.1, "PiedraCampo", 10), cono(0, 0, 0.9, 1.0, 1.25, 1.25, "Roca", 10), cono(0, 0, 0.55, 0.62, 0.9, 0.9, "Agua", 10)]
    for s in (-1, 1):
        o.append(poste(s * 1.0, 0, 0.9, 2.7, 0.09, "MaderaOscura", 6))
    o.append(viga((-1.15, 0, 2.2), (1.15, 0, 2.2), 0.12, "Madera"))
    o += [prisma([(-1.0, 2.6), (1.0, 2.6), (0, 3.4)], -1.4, 1.4, "Paja", eje='X'), prisma([(-1.08, 2.56), (1.08, 2.56), (0.9, 2.74), (-0.9, 2.74)], -1.45, 1.45, "PajaSombra", eje='X')]
    o.append(viga((0.2, 0, 2.2), (0.2, 0, 1.35), 0.03, "Cuerda"))
    o.append(cono(0.2, 0, 1.0, 1.35, 0.2, 0.25, "Madera", 8))
    return juntar(o, nombre)

def arco_del_pueblo(nombre="ArcoDelPueblo"):
    """Arco de entrada al pueblo de Will: dos postes gruesos, tejadillo de paja, cartel y faroles. Paso de 4 m."""
    o = []
    for s in (-1, 1):
        o.append(caja(s * 2.3 - 0.45, s * 2.3 + 0.45, -0.45, 0.45, -0.6, 0.9, "PiedraCampo", 0.06))
        o.append(caja(s * 2.3 - 0.17, s * 2.3 + 0.17, -0.17, 0.17, 0.9, 4.0, "MaderaOscura", 0.03))
        o.append(viga((s * 2.3, 0, 3.0), (s * 1.3, 0, 3.9), 0.14, "MaderaOscura"))
        o.append(viga((s * 2.3, 0, 3.3), (s * 2.3, -0.5, 3.4), 0.05, "Hierro"))
        o.append(caja(s * 2.3 - 0.13, s * 2.3 + 0.13, -0.63, -0.37, 2.95, 3.3, "Luz", 0.02))
        o.append(cono(s * 2.3, -0.75, 0.0, 0.45, 0.3, 0.36, "ContraRoja", 8)); o.append(bola((s * 2.3, -0.75, 0.7), 0.36, "Hojas", 2)); o.append(bola((s * 2.3 + 0.1, -0.8, 1.0), 0.15, "Flor"))
    o.append(caja(-2.9, 2.9, -0.15, 0.15, 3.9, 4.2, "MaderaOscura", 0.03))
    o += [prisma([(-0.95, 4.2), (0.95, 4.2), (0.1, 5.0), (-0.1, 5.0)], -3.2, 3.2, "Paja", eje='X'), prisma([(-1.03, 4.16), (1.03, 4.16), (0.85, 4.36), (-0.85, 4.36)], -3.25, 3.25, "PajaSombra", eje='X')]
    for x in (-0.9, 0.9):
        o.append(viga((x, 0, 3.9), (x, 0, 3.55), 0.03, "Hierro"))
    o.append(caja(-1.3, 1.3, -0.05, 0.05, 2.85, 3.55, "Madera", 0.03)); o.append(caja(-1.15, 1.15, -0.07, 0.07, 3.0, 3.4, "Estuco", 0.02))
    return juntar(o, nombre)

def arco_de_la_aldea(nombre="ArcoDeLaAldea"):
    """Entrada a la Aldea del Mar: dos mástiles con verga, red colgada con boyas, gallardete y farol. Paso de 4,4 m."""
    o = []
    for s in (-1, 1):
        o.append(cono(s * 2.5, 0, -0.6, 0.5, 0.5, 0.42, "Roca", 8))
        o.append(poste(s * 2.5, 0, 0.5, 5.4, 0.13, "MaderaOscura", 8))
        o.append(bola((s * 2.5, 0, 5.5), 0.16, "Oro"))
        for k in range(3):
            o.append(cono(s * 2.5, 0, 0.9 + k * 0.5, 1.05 + k * 0.5, 0.17, 0.17, "Cuerda", 8))
        o.append(viga((s * 2.5, 0, 3.4), (s * 2.5, -0.5, 3.5), 0.05, "Hierro"))
        o.append(caja(s * 2.5 - 0.13, s * 2.5 + 0.13, -0.63, -0.37, 3.05, 3.4, "Luz", 0.02))
    o.append(viga((-3.1, 0, 4.6), (3.1, 0, 4.6), 0.16, "Madera"))
    n = 11
    for i in range(n + 1):
        x = -2.2 + i * 4.4 / n
        bajo = 4.6 - 1.0 - 0.5 * (1 - (2 * i / n - 1) ** 2)
        o.append(viga((x, 0, 4.55), (x, 0, bajo), 0.025, "Cuerda"))
        if i % 3 == 1:
            o.append(bola((x, 0, bajo - 0.12), 0.17, "Boya" if i % 2 else "TejaAmarilla"))
    for z in (4.2, 3.8):
        o.append(viga((-2.2, 0, z), (2.2, 0, z), 0.025, "Cuerda"))
    o.append(prisma([(2.6, 5.3), (2.6, 4.8), (3.9, 5.05)], -0.012, 0.012, "TejaTurquesa", eje='Y'))
    return juntar(o, nombre)

# ── Parajes: repisas, escalones, puentes, cuevas y templos ──────────────────────────────────

def repisa(nombre, radio, semilla):
    """Repisa de roca con la cara de arriba llana en z = 0 y hierba encima; el cuerpo baja 7 m para clavarse en
    una ladera. Se salta de una a otra: su borde se reconoce por la forma."""
    az = random.Random(semilla)
    lados = 9
    pts = []
    for i in range(lados):
        a = 2 * math.pi * i / lados
        r = radio * az.uniform(0.82, 1.08)
        pts.append((r * math.cos(a), r * math.sin(a)))
    o = [losa(pts, -0.9, -0.12, "Roca"), losa([(x * 0.97, y * 0.97) for x, y in pts], -0.14, 0.0, "Hierba")]
    bajo = [(x * 0.86, y * 0.86) for x, y in pts]
    o.append(losa(bajo, -3.6, -0.88, "Roca")); o.append(losa([(x * 0.7, y * 0.7) for x, y in pts], -7.0, -3.58, "Roca"))
    for k in range(3):
        a = az.uniform(0, 6.28); d = radio * az.uniform(0.75, 0.95)
        o.append(roca((d * math.cos(a), d * math.sin(a), -0.5 - k * 0.9), radio * az.uniform(0.22, 0.34), "Roca", semilla * 7 + k))
    for k in range(int(radio * 2)):
        a = az.uniform(0, 6.28); d = radio * az.uniform(0.1, 0.7)
        o.append(bola((d * math.cos(a), d * math.sin(a), 0.1), 0.11, "Flor" if k % 2 else "Luz"))
        o.append(cono(d * math.cos(a), d * math.sin(a), 0.0, 0.1, 0.02, 0.02, "Hojas", 4))
    return juntar(o, nombre)

def escalones(nombre="EscalonesDePiedra", peldaños=10, ancho=2.6):
    """Tramo de escalones de piedra: sube 0,2 m y avanza 0,45 m por peldaño hacia +Y; arranca en z = 0 en y = 0.
    Macizo hasta 3 m por debajo, con bordillo a los lados. Se sube andando."""
    o = []
    for i in range(peldaños):
        o.append(caja(-ancho / 2, ancho / 2, i * 0.45, (i + 1) * 0.45 + 0.04, -3.0, (i + 1) * 0.2, "PiedraGris" if i % 2 else "Roca", 0.03))
    largo, alto = peldaños * 0.45, peldaños * 0.2
    for s in (-1, 1):
        o.append(prisma([(0, -3.0), (largo, -3.0), (largo, alto + 0.35), (largo - 0.4, alto + 0.35), (0, 0.4)], min(s * ancho / 2, s * (ancho / 2 + 0.35)), max(s * ancho / 2, s * (ancho / 2 + 0.35)), "Roca", eje='X'))
        o.append(roca((s * (ancho / 2 + 0.3), -0.2, 0.1), 0.5, "Roca", 3 + s))
    return juntar(o, nombre)

def puente_colgante(nombre="PuenteColgante", luz=10.0):
    """Puente colgante de tablas de «luz» metros a lo largo de Y, con el piso en z = 0 en los estribos y 0,3 m de
    comba en el centro; cuatro postes y pasamanos de cabo."""
    o, n = [], int(luz / 0.5)
    def comba(t): return -0.3 * (1 - (2 * t - 1) ** 2)
    for i in range(n):
        t = (i + 0.5) / n
        y, z = -luz / 2 + luz * t, comba(t)
        o.append(caja(-0.95, 0.95, y - 0.22, y + 0.22, z - 0.1, z, "Madera" if i % 3 else "MaderaOscura", 0.02))
    for s in (-1, 1):
        for e in (-1, 1):
            o.append(poste(s * 1.05, e * (luz / 2 + 0.2), -1.2, 1.5, 0.12, "MaderaOscura", 8))
            o.append(roca((s * 1.3, e * (luz / 2 + 0.5), -0.1), 0.45, "Roca", 11 + s + 3 * e))
        m = 12
        for i in range(m):
            t0, t1 = i / m, (i + 1) / m
            for zc, g in ((1.3, 0.06), (-0.08, 0.07)):
                p0 = (s * 1.05, -luz / 2 - 0.2 + (luz + 0.4) * t0, zc + (comba(t0) * (1.0 if zc < 0 else 1.6)))
                p1 = (s * 1.05, -luz / 2 - 0.2 + (luz + 0.4) * t1, zc + (comba(t1) * (1.0 if zc < 0 else 1.6)))
                o.append(viga(p0, p1, g, "Cuerda"))
            if 0 < i < m:
                y = -luz / 2 - 0.2 + (luz + 0.4) * t0
                o.append(viga((s * 1.05, y, comba(t0) - 0.05), (s * 1.05, y, 1.3 + comba(t0) * 1.6), 0.03, "Cuerda"))
    for e in (-1, 1):
        o.append(caja(-1.2, 1.2, e * (luz / 2) - 0.35 if e < 0 else luz / 2 - 0.05, -luz / 2 + 0.05 if e < 0 else luz / 2 + 0.35, -1.2, 0.0, "MaderaOscura", 0.03))
    return juntar(o, nombre)

def mirador_de_cumbre(nombre="MiradorDeCumbre"):
    """Mirador de piedra de 8 m para una cumbre: pretil almenado bajo, banco, catalejo y un asta con gallardete
    que se ve desde lejos (destino de vuelo)."""
    o = [cono(0, 0, -4.0, 0.0, 4.6, 4.2, "Roca", 10), cono(0, 0, 0.0, 0.25, 4.2, 4.2, "PiedraGris", 10)]
    for i in range(10):
        if i in (7, 8): continue
        a0 = 2 * math.pi * (i + 0.12) / 10; a1 = 2 * math.pi * (i + 0.88) / 10
        o.append(viga((3.9 * math.cos(a0), 3.9 * math.sin(a0), 0.7), (3.9 * math.cos(a1), 3.9 * math.sin(a1), 0.7), 0.5, "PiedraA"))
        o.append(viga((3.9 * math.cos(a0), 3.9 * math.sin(a0), 1.0), (3.9 * math.cos(a1), 3.9 * math.sin(a1), 1.0), 0.3, "PiedraLila"))
    o.append(poste(0, 0, 0.25, 9.0, 0.11, "MaderaOscura", 8)); o.append(bola((0, 0, 9.1), 0.2, "Oro", 2))
    o.append(prisma([(0.1, 8.8), (0.1, 7.4), (3.2, 8.1)], -0.015, 0.015, "RojoFaro", eje='Y'))
    o.append(cono(0, 0, 0.25, 0.6, 0.6, 0.45, "PiedraLila", 8))
    o += banco(-2.2, 1.6, 0.25, 1.8, -50)
    o.append(poste(2.4, 2.0, 0.25, 1.4, 0.06, "Hierro", 6)); o.append(viga((2.1, 1.75, 1.45), (2.9, 2.4, 1.7), 0.14, "Bronce"))
    return juntar(o, nombre)

def arco_hueco(perfil, d0, d1, material, sube=0.0):
    """Arco macizo extruido a lo largo de Y. «perfil» trae primero el contorno de fuera y luego, de vuelta, el del
    hueco (mismo número de puntos): las tapas se hacen por tiras entre los dos, sin polígonos cóncavos. Con
    «sube», el extremo d1 queda más alto (túnel en rampa)."""
    n = len(perfil) // 2
    fuera, dentro = perfil[:n], perfil[n:][::-1]
    bm = bmesh.new()
    def anillo(pts, d, dz): return [bm.verts.new((u, d, z + dz)) for u, z in pts]
    f0, f1, i0, i1 = anillo(fuera, d0, 0), anillo(fuera, d1, sube), anillo(dentro, d0, 0), anillo(dentro, d1, sube)
    for k in range(n - 1):
        bm.faces.new([f0[k], f0[k + 1], i0[k + 1], i0[k]]); bm.faces.new([f1[k], f1[k + 1], i1[k + 1], i1[k]])
        bm.faces.new([f0[k], f0[k + 1], f1[k + 1], f1[k]]); bm.faces.new([i0[k], i0[k + 1], i1[k + 1], i1[k]])
    for k in (0, n - 1):
        bm.faces.new([f0[k], i0[k], i1[k], f1[k]])
    return objeto("arco", bm, material)

def arco_de_roca(we, he, wi, hi):
    """Perfil (x, z) de un arco macizo: contorno de fuera y, de vuelta, el hueco."""
    return [(-we / 2, -1.0), (-we / 2, he * 0.62), (-we * 0.27, he), (we * 0.27, he), (we / 2, he * 0.62), (we / 2, -1.0),
            (wi / 2, -1.0), (wi / 2, hi * 0.66), (wi * 0.27, hi), (-wi * 0.27, hi), (-wi / 2, hi * 0.66), (-wi / 2, -1.0)]

def tunel(nombre="TunelDeRoca", largo=8.0, boca=False, semilla=1):
    """Tramo de túnel de roca a lo largo de Y, con paso de 3,2 × 3,4 m y suelo en z = 0. Por fuera es un lomo de
    peñascos (va medio enterrado en la ladera). Con «boca», un extremo lleva el arco de entrada y dos peñas."""
    az = random.Random(semilla)
    o = [arco_hueco(arco_de_roca(7.4, 5.6, 3.6, 3.8), -largo / 2, largo / 2, "Roca"),
         arco_hueco(arco_de_roca(3.9, 3.95, 3.2, 3.4), -largo / 2 + 0.05, largo / 2 - 0.05, "RocaOscura"),
         caja(-2.0, 2.0, -largo / 2, largo / 2, -1.0, 0.0, "SueloDeCueva", 0.0)]
    n = int(largo / 1.6)
    for i in range(n):
        y = -largo / 2 + (i + 0.5) * largo / n
        for s in (-1, 1):
            o.append(roca((s * az.uniform(3.2, 3.9), y + az.uniform(-0.4, 0.4), az.uniform(0.3, 1.6)), az.uniform(1.5, 2.2), "Roca", semilla * 31 + i * 2 + s))
        o.append(roca((az.uniform(-1.2, 1.2), y, 5.0 + az.uniform(-0.2, 0.5)), az.uniform(1.6, 2.3), "Roca", semilla * 17 + i, 0.6))
        o.append(roca((az.choice((-1.35, 1.35)), y + az.uniform(-0.3, 0.3), 0.05), az.uniform(0.2, 0.35), "RocaOscura", semilla * 5 + i))
    if boca:
        y = -largo / 2
        o.append(arco_hueco(arco_de_roca(8.6, 6.4, 3.4, 3.6), y - 0.9, y + 0.3, "Roca"))
        for s in (-1, 1):
            o.append(roca((s * 3.6, y - 1.0, 0.8), 2.0, "Roca", semilla + 40 + s))
            o.append(roca((s * 2.5, y - 1.6, 0.1), 0.9, "Roca", semilla + 50 + s))
        o.append(roca((0.3, y - 0.4, 6.0), 2.4, "Roca", semilla + 60, 0.55))
        for k in range(5):
            x = -1.6 + k * 0.8
            o.append(caja(x - 0.05, x + 0.05, y - 0.98, y - 0.9, 3.5 - az.uniform(0.6, 1.5), 3.9, "Hiedra", 0.01))
    return juntar(o, nombre)

def prisma_inclinado(perfil, d0, d1, sube, material):
    """Como «prisma» a lo largo de Y, pero el extremo d1 queda «sube» metros más alto: túneles en rampa."""
    bm = bmesh.new()
    a = [bm.verts.new((u, d0, z)) for u, z in perfil]
    b = [bm.verts.new((u, d1, z + sube)) for u, z in perfil]
    bm.faces.new(a); bm.faces.new(list(reversed(b)))
    n = len(perfil)
    for i in range(n):
        bm.faces.new([a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    return objeto("prisma", bm, material)

def tunel_escalonado(nombre="TunelEscalonado", largo=8.0, sube=5.0, semilla=5):
    """Tramo de túnel con escalera tallada: como «tunel», pero sube «sube» metros hacia +Y. El suelo del extremo
    de abajo (y = -largo/2) está en z = 0 y el de arriba en z = sube. Peldaños de 0,2 m: se sube andando."""
    az = random.Random(semilla)
    o = [arco_hueco(arco_de_roca(7.4, 5.6, 3.6, 3.8), -largo / 2, largo / 2, "Roca", sube),
         arco_hueco(arco_de_roca(3.9, 3.95, 3.2, 3.4), -largo / 2 + 0.05, largo / 2 - 0.05, "RocaOscura", sube),
         prisma_inclinado([(-2.0, -1.6), (2.0, -1.6), (2.0, -0.02), (-2.0, -0.02)], -largo / 2, largo / 2, sube, "SueloDeCueva")]
    n = int(round(sube / 0.2))
    paso = largo / n
    for i in range(n):
        y0 = -largo / 2 + i * paso
        o.append(caja(-1.62, 1.62, y0, y0 + paso + 0.03, i * 0.2 - 0.5, (i + 1) * 0.2, "SueloDeCueva" if i % 2 else "RocaOscura", 0.0))
    m = int(largo / 1.6)
    for i in range(m):
        t = (i + 0.5) / m
        y, z = -largo / 2 + t * largo, t * sube
        for s in (-1, 1):
            o.append(roca((s * az.uniform(3.2, 3.9), y + az.uniform(-0.4, 0.4), z + az.uniform(0.3, 1.6)), az.uniform(1.5, 2.2), "Roca", semilla * 31 + i * 2 + s))
        o.append(roca((az.uniform(-1.2, 1.2), y, z + 5.0 + az.uniform(-0.2, 0.5)), az.uniform(1.6, 2.3), "Roca", semilla * 17 + i, 0.6))
        if i % 2 == 0:
            x = az.choice((-1.45, 1.45))
            o.append(cono(x, y, z + 0.3, z + 0.6, 0.06, 0.04, "Cal", 6)); o.append(cono(x, y, z + 0.6, z + 0.78, 0.22, 0.04, "Portal", 8))
    return juntar(o, nombre)

def sala_de_cueva(nombre="SalaDeLaCueva"):
    """Sala redonda de 11 m con dos pasos enfrentados (±Y) y una claraboya por la que entra la luz: el corazón de
    la cueva de la cascada. Suelo en z = 0, con una poza y setas de luz."""
    o = [cono(0, 0, -1.0, 0.0, 6.2, 6.2, "SueloDeCueva", 14)]
    n = 14
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        if abs(math.sin(a)) > 0.93: continue            # los dos pasos
        x, y = 6.3 * math.cos(a), 6.3 * math.sin(a)
        o.append(roca((x, y, 2.0), 2.6, "RocaOscura", 100 + i, 1.3))
        o.append(roca((x * 1.22, y * 1.22, 2.6), 3.2, "Roca", 200 + i, 1.2))
    for s in (-1, 1):
        t = arco_hueco(arco_de_roca(7.4, 5.6, 3.4, 3.6), s * 5.4 - 0.9, s * 5.4 + 0.9, "Roca"); o.append(t)
        o.append(arco_hueco(arco_de_roca(3.9, 3.95, 3.2, 3.4), s * 5.4 - 0.85, s * 5.4 + 0.85, "RocaOscura"))
    for i in range(10):
        a = 2 * math.pi * i / 10
        o.append(roca((4.2 * math.cos(a), 4.2 * math.sin(a), 6.3), 3.0, "Roca", 300 + i, 0.6))
        o.append(roca((4.0 * math.cos(a), 4.0 * math.sin(a), 5.2), 2.2, "RocaOscura", 320 + i, 0.5))
    o.append(cono(2.2, -1.0, 0.0, 0.05, 2.0, 2.0, "Agua", 10)); o.append(cono(2.2, -1.0, -0.05, 0.1, 2.3, 2.2, "RocaOscura", 10))
    for k, (x, y) in enumerate(((-3.6, 2.2), (-4.2, -1.5), (3.9, 2.8), (-2.6, -3.6), (4.4, -3.0))):
        o.append(cono(x, y, 0, 0.35 + 0.1 * (k % 2), 0.07, 0.05, "Cal", 6)); o.append(cono(x, y, 0.35 + 0.1 * (k % 2), 0.55 + 0.1 * (k % 2), 0.3, 0.05, "Portal", 8))
    return juntar(o, nombre)

def cortina_de_hiedra(nombre="CortinaDeHiedra"):
    """Cortina de hiedra de 3,6 × 3,8 m que tapa un paso: se atraviesa andando (sin colisión)."""
    az, o = random.Random(4), [caja(-1.9, 1.9, -0.12, 0.12, 3.65, 3.9, "Hiedra", 0.05)]
    for i in range(22):
        x = -1.75 + i * 3.5 / 21
        largo = az.uniform(2.6, 3.7)
        o.append(caja(x - 0.05, x + 0.05, az.uniform(-0.06, 0.06) - 0.03, az.uniform(-0.06, 0.06) + 0.03, 3.7 - largo, 3.7, "Hiedra" if i % 3 else "Hojas", 0.01))
        for k in range(int(largo / 0.5)):
            o.append(bola((x + az.uniform(-0.08, 0.08), az.uniform(-0.1, 0.1), 3.6 - k * 0.5 - az.uniform(0, 0.2)), 0.16, "Hojas" if (i + k) % 2 else "Hiedra"))
    return juntar(o, nombre)

def grieta(nombre="GrietaSecreta", largo=7.0):
    """Pasillo secreto: dos lajas de roca apoyadas una en otra dejan un paso de 1,5 m a lo largo de Y."""
    o = [caja(-1.0, 1.0, -largo / 2, largo / 2, -1.0, 0.0, "SueloDeCueva", 0.0)]
    for s in (-1, 1):
        o.append(prisma([(s * 0.75, -1.0), (s * 3.4, -1.0), (s * 2.6, 3.4), (s * 0.2 - 0.25 * s, 5.2), (s * 0.75, 2.6)] if s > 0 else
                        [(-3.4, -1.0), (-0.75, -1.0), (-0.75, 2.6), (0.35, 4.6), (-2.6, 3.4)], -largo / 2, largo / 2, "Roca" if s > 0 else "PiedraGris"))
        for i in range(3):
            o.append(roca((s * 3.0, -largo / 2 + (i + 0.5) * largo / 3, 0.6), 1.5, "Roca", 70 + i + 5 * s))
    for e in (-1, 1):
        o.append(roca((2.3, e * (largo / 2 + 0.3), 0.3), 1.1, "Roca", 90 + e)); o.append(roca((-2.4, e * (largo / 2 + 0.2), 0.2), 0.9, "Roca", 95 + e))
    return juntar(o, nombre)

def entrada_de_templo(nombre="EntradaDeTemplo"):
    """Entrada de un templo de las pruebas: frente de piedra clara con musgo, arco apuntado con el velo de luz al
    fondo (ahí va el disparador que carga el nivel), aros de bronce sobre la clave, escalinata y dos fanales.
    Fachada a -Y; el cuerpo se mete 6 m en la ladera. Sin monolitos, altares ni inscripciones: no es una ruina del Libro."""
    o = [caja(-5.0, 5.0, 0.0, 6.0, -1.0, 6.2, "PiedraTemplo", 0.12), caja(-4.2, 4.2, 0.4, 6.0, 6.2, 7.3, "PiedraTemplo", 0.12),
         caja(-5.4, 5.4, -0.3, 0.3, -1.0, 0.9, "PiedraGris", 0.08), caja(-5.2, 5.2, -0.12, 0.2, 5.75, 6.3, "PiedraGris", 0.06)]
    # Hueco: jambas y arco apuntado por delante del cuerpo, con el interior oscuro y el velo al fondo.
    perfil = [(-2.6, 0.9), (-2.6, 4.2), (0.0, 5.6), (2.6, 4.2), (2.6, 0.9), (1.5, 0.9), (1.5, 3.3), (0.0, 4.5), (-1.5, 3.3), (-1.5, 0.9)]
    o.append(arco_hueco(perfil, -0.55, 0.05, "PiedraGris"))
    o.append(prisma([(-1.5, 0.9), (1.5, 0.9), (1.5, 3.3), (0.0, 4.5), (-1.5, 3.3)], -0.02, 0.03, "Fondo"))
    o.append(prisma([(-1.25, 0.95), (1.25, 0.95), (1.25, 3.15), (0.0, 4.15), (-1.25, 3.15)], -0.1, -0.03, "Portal"))
    for s in (-1, 1):
        o.append(caja(min(s * 1.5, s * 1.85), max(s * 1.5, s * 1.85), -0.75, -0.5, 0.9, 3.3, "PiedraTemplo", 0.04))
    # Aros de bronce sobre la clave.
    for r, g in ((0.95, 0.09), (0.6, 0.07), (0.28, 0.07)):
        m = 16
        for i in range(m):
            a0, a1 = 2 * math.pi * i / m, 2 * math.pi * (i + 1) / m
            o.append(viga((r * math.cos(a0), -0.2, 5.0 + 1.0 + r * math.sin(a0) - 0.75), (r * math.cos(a1), -0.2, 5.0 + 1.0 + r * math.sin(a1) - 0.75), g, "Bronce"))
    # Escalinata de cinco peldaños y fanales.
    for i in range(5):
        o.append(caja(-2.6 - 0.25 * (4 - i), 2.6 + 0.25 * (4 - i), -0.6 - 0.5 * (5 - i), -0.3, -1.0, 0.18 * (i + 1), "PiedraTemplo" if i % 2 else "PiedraGris", 0.03))
    for s in (-1, 1):
        o.append(caja(s * 4.0 - 0.5, s * 4.0 + 0.5, -1.9, -0.9, -1.0, 1.3, "PiedraGris", 0.06))
        o.append(caja(s * 4.0 - 0.32, s * 4.0 + 0.32, -1.72, -1.08, 1.3, 2.5, "PiedraTemplo", 0.05))
        o.append(caja(s * 4.0 - 0.26, s * 4.0 + 0.26, -1.66, -1.14, 2.5, 3.1, "Luz", 0.03))
        o.append(cono(s * 4.0, -1.4, 3.1, 3.7, 0.62, 0.05, "Bronce", 4, math.pi / 4))
        for k in range(4):
            o.append(caja(s * (2.9 + 0.01 * k) - 0.2, s * (2.9 + 0.01 * k) + 0.2, -0.36, -0.3, 1.2 + k * 1.1, 1.75 + k * 1.1, "PiedraGris", 0.03))
    # Musgo y hiedra: lleva tiempo ahí.
    az = random.Random(9)
    for k in range(9):
        x = az.uniform(-4.8, 4.8)
        o.append(roca((x, az.uniform(0.2, 1.2), 6.3 if abs(x) > 4.2 else 7.35), az.uniform(0.5, 0.9), "Musgo", 500 + k, 0.35))
    for k in range(7):
        x = az.choice((-1, 1)) * az.uniform(3.0, 4.9)
        largo = az.uniform(1.0, 2.6)
        o.append(caja(x - 0.06, x + 0.06, -0.18, -0.1, 6.0 - largo, 6.05, "Hiedra", 0.01)); o.append(bola((x, -0.16, 6.0 - largo), 0.16, "Hojas"))
    return juntar(o, nombre)

# ── Salida ──────────────────────────────────────────────────────────────────────────────────

for nombre, opciones in VARIANTES_DE_ALDEA:
    limpiar(); exportar(casa_de_la_aldea(nombre, **opciones))
limpiar(); exportar(pasarela(0, 3.0, 0, "PasarelaDeLaAldea"))
for nombre, opciones in VARIANTES_DE_CIUDAD:
    limpiar(); exportar(casa_de_ciudad(nombre, **opciones))
for nombre, opciones in (("PuestoDeMercado_Fruta", dict(toldo="ToldoRojo", genero="Flor")), ("PuestoDeMercado_Verdura", dict(toldo="ToldoVerde", genero="Hojas")),
                         ("PuestoDeMercado_Pan", dict(toldo="ToldoAmarillo", genero="TejaAmarilla")), ("PuestoDeMercado_Pescado", dict(toldo="Pizarra", genero="Pescado"))):
    limpiar(); exportar(puesto(nombre, **opciones))
for pieza in (faro, lonja, secadero, torre_del_reloj, arbol_con_columpio, columpio_del_arbol, rellano, mirador, fuente, arbol_de_plaza, rosa_de_los_vientos, banderines, paseo):
    limpiar(); exportar(pieza())
for nombre, opciones in (("TiendaDeLaAldea_Pescado", dict(toldo="ToldoRojo", teja="TejaTurquesa", zocalo="Zocalo", genero="Pescado")),
                         ("TiendaDeLaAldea_Fruta", dict(toldo="ToldoAmarillo", teja="TejaCoral", zocalo="ZocaloCoral", genero="Flor")),
                         ("TiendaDeLaAldea_Aparejos", dict(toldo="ToldoVerde", teja="TejaAzul", zocalo="ZocaloTurquesa", genero="Cuerda"))):
    limpiar(); exportar(tienda(nombre, **opciones))

for nombre, opciones in VARIANTES_DEL_PUEBLO:
    limpiar(); exportar(casa_del_pueblo(nombre, **opciones))
for pieza in (granero, pozo, arco_del_pueblo, arco_de_la_aldea, escalones, puente_colgante, mirador_de_cumbre, sala_de_cueva, cortina_de_hiedra, grieta, entrada_de_templo):
    limpiar(); exportar(pieza())
for nombre, radio, semilla in (("RepisaDeRoca_Grande", 5.5, 1), ("RepisaDeRoca_Mediana", 3.8, 2), ("RepisaDeRoca_Pequena", 2.4, 3)):
    limpiar(); exportar(repisa(nombre, radio, semilla))
limpiar(); exportar(tunel("TunelDeRoca", 8.0))
limpiar(); exportar(tunel_escalonado())
limpiar(); exportar(tunel("BocaDeCueva", 5.0, boca=True, semilla=2))
limpiar(); exportar(puente_colgante("PuenteColgante_Largo", 16.0))

if FOTOS:
    limpiar()
    x, anterior = 0.0, None
    for nombre, opciones in VARIANTES_DE_ALDEA[:5]:
        medio = opciones["ancho"] / 2 + 0.6
        if anterior is not None:
            pasarela(anterior, anterior + 2.4, -0.75, "P" + nombre)
            x = anterior + 2.4 + medio
        casa = casa_de_la_aldea("F" + nombre, **opciones)
        casa.location = (x, opciones["fondo"] / 2, 0)
        anterior = x + medio
    faro("FFaro").location = (anterior + 9, 9, 0)
    caja(-24, -5.5, -5, 12, -1.0, 0.25, "Arena", 0.0)
    lonja("FLonja").location = (-13, 4, 0.25)
    secadero("FSecadero").location = (-17.5, -2.2, 0.25)
    secadero("FSecadero2").location = (-9.5, -2.6, 0.25)
    foto(os.path.join(OUT, "aldea.png"), (-7, -47, 15), (6, 2, 3.5), "Agua", 28)
    limpiar()
    x = 0.0
    orden = [VARIANTES_DE_CIUDAD[k] for k in (0, 3, 1)] + [None] + [VARIANTES_DE_CIUDAD[k] for k in (4, 2, 5)]
    for v in orden:
        if v is None:
            torre_del_reloj("FTorre").location = (x + 2.3, 0.7, 0); x += 4.6
            continue
        casa = casa_de_ciudad("F" + v[0], **v[1])
        casa.location = (x + v[1]["ancho"] / 2, 0, 0); x += v[1]["ancho"]
    arbol_con_columpio("FArbol").location = (x + 5, -7, 0)
    foto(os.path.join(OUT, "capital.png"), (-6, -44, 9), (x / 2 + 1, 0, 8.5), "Barro", 28)

if "piezas" in ARGS:
    limpiar()
    x = 0.0
    for nombre, opciones in VARIANTES_DE_CIUDAD[6:]:
        c = casa_de_ciudad("X" + nombre, **opciones); c.location = (x + opciones["ancho"] / 2, 8, 0); x += opciones["ancho"] + 1.0
    fuente("XFuente").location = (8, -6, 0); arbol_de_plaza("XArbol").location = (16, -5, 0)
    for k, (t, g) in enumerate((("ToldoRojo", "Flor"), ("ToldoVerde", "Hojas"), ("ToldoAmarillo", "TejaAmarilla"), ("Pizarra", "Pescado"))):
        puesto("XPuesto%d" % k, t, g).location = (22 + k * 4.5, -5, 0)
    rellano("XRellano").location = (44, -5, -1.4); mirador("XMirador").location = (52, -5, -1.4)
    tienda("XT1").location = (44, 5, -1.4); tienda("XT2", "ToldoAmarillo", "TejaCoral", "ZocaloCoral", "Flor").location = (51, 5, -1.4)
    foto(os.path.join(OUT, "piezas.png"), (28, -44, 16), (28, 2, 4), "Barro", 26)
