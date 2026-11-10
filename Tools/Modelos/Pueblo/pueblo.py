# Piezas del pueblo del prólogo hechas en Blender (bpy). Uso:
#   python pueblo.py <carpeta de salida>
# Saca Contraventana.fbx, Mirador.fbx y PuertaDeCasa.fbx (1 unidad = 1 m; en Unity: Y arriba).
# Convención: X a lo ancho, Z (Blender) arriba, la cara mira a -Y (Blender) = +Z en Unity.
# Las hojas que giran llevan su origen en la bisagra.
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

OUT = sys.argv[-1]
os.makedirs(OUT, exist_ok=True)

def limpiar():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(nombre, color):
    m = bpy.data.materials.get(nombre) or bpy.data.materials.new(nombre)
    m.diffuse_color = (*color, 1)
    return m

def caja(nombre, x0, x1, y0, y1, z0, z1, material, padre=None, bisel=0.012):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x = x0 + (v.co.x + 0.5) * (x1 - x0)
        v.co.y = y0 + (v.co.y + 0.5) * (y1 - y0)
        v.co.z = z0 + (v.co.z + 0.5) * (z1 - z0)
    if bisel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=min(bisel, 0.45 * min(x1 - x0, y1 - y0, z1 - z0)), segments=1, affect='EDGES')
    me = bpy.data.meshes.new(nombre)
    bm.to_mesh(me); bm.free()
    me.materials.append(material)
    ob = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(ob)
    if padre: ob.parent = padre
    return ob

def juntar(obs, nombre):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    obs[0].name = nombre; obs[0].data.name = nombre
    return obs[0]

def diagonal(nombre, ax, az, bx, bz, ancho, y0, y1, material):
    # Travesaño inclinado de (ax,az) a (bx,bz) en el plano XZ.
    largo = math.hypot(bx - ax, bz - az)
    ob = caja(nombre, -largo / 2, largo / 2, y0, y1, -ancho / 2, ancho / 2, material, bisel=0.006)
    ob.rotation_euler = (0, -math.atan2(bz - az, bx - ax), 0)
    ob.location = ((ax + bx) / 2, 0, (az + bz) / 2)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob

def hoja(nombre, ancho, alto, grueso, madera, hierro, espejo=False):
    """Una hoja de tablas con su Z por las dos caras y dos pernios. Bisagra en x=0, va hacia +x (o -x)."""
    s = -1 if espejo else 1
    piezas = []
    n = 3
    for i in range(n):
        a = i * ancho / n + 0.008; b = (i + 1) * ancho / n - 0.008
        piezas.append(caja(f"t{i}", min(s*a, s*b), max(s*a, s*b), -grueso / 2, grueso / 2, 0.0, alto, madera, bisel=0.01))
    for lado in (-1, 1):
        y0, y1 = (grueso / 2, grueso / 2 + 0.02) if lado < 0 else (-grueso / 2 - 0.02, -grueso / 2)
        # Y0/Y1 al revés para la cara de atrás
        ya, yb = (-grueso/2 - 0.02, -grueso/2) if lado < 0 else (grueso/2, grueso/2 + 0.02)
        for z in (0.12 * alto, 0.88 * alto):
            piezas.append(caja(f"b{lado}{z}", min(0, s*ancho), max(0, s*ancho), ya, yb, z - 0.05 * alto, z + 0.05 * alto, madera, bisel=0.008))
        piezas.append(diagonal(f"d{lado}", s*0.06*ancho, 0.17 * alto, s*0.94*ancho, 0.83 * alto, 0.08 * alto, ya, yb, madera))
    for z in (0.12 * alto, 0.88 * alto):
        piezas.append(caja(f"p{z}", min(0, s*0.45*ancho), max(0, s*0.45*ancho), -grueso/2 - 0.03, grueso/2 + 0.03, z - 0.025, z + 0.025, hierro, bisel=0.005))
    ob = juntar(piezas, nombre)
    return ob

def exportar(nombre):
    ruta = os.path.join(OUT, nombre + ".fbx")
    bpy.ops.export_scene.fbx(filepath=ruta, use_selection=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH', 'EMPTY'},
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
    print("exportado", ruta)

MADERA = (0.20, 0.47, 0.50)       # contraventana pintada (Unity la cambia por casa)
HIERRO = (0.16, 0.14, 0.13)
MARCO = (0.55, 0.33, 0.16)
YESO = (0.93, 0.80, 0.52)
TEJA = (0.16, 0.45, 0.55)
FONDO = (0.10, 0.07, 0.06)
FLOR_TIERRA = (0.35, 0.22, 0.12)

# 1 · Contraventana: hoja de 1 × 1 m (en Unity se escala al hueco de cada ventana).
limpiar()
hoja("Contraventana", 1.0, 1.0, 0.05, mat("Madera", MADERA), mat("Hierro", HIERRO))
exportar("Contraventana")

# 2 · Mirador: ventana saliente con antepecho, para que alguien se asome. Espalda contra la pared (y=0),
#     sale hacia -y (Blender) 0,62 m. Ancho 1,4. Suelo interior a 0. Hueco de 0,95 a 1,9 m.
limpiar()
m_marco, m_yeso, m_teja, m_fondo = mat("Marco", MARCO), mat("Yeso", YESO), mat("Teja", TEJA), mat("Fondo", FONDO)
m_madera, m_hierro = mat("Madera", MADERA), mat("Hierro", HIERRO)
W, Dp, H0, H1, H = 1.4, 0.62, 0.95, 1.9, 2.05
cuerpo = []
cuerpo.append(caja("antepecho", -W/2, W/2, -Dp, -Dp + 0.07, 0.0, H0, m_yeso))             # frente bajo
cuerpo.append(caja("lado_i", -W/2, -W/2 + 0.07, -Dp, 0.0, 0.0, H, m_yeso))
cuerpo.append(caja("lado_d", W/2 - 0.07, W/2, -Dp, 0.0, 0.0, H, m_yeso))
cuerpo.append(caja("dintel", -W/2, W/2, -Dp, -Dp + 0.07, H1, H, m_yeso))
cuerpo.append(caja("fondo", -W/2 + 0.07, W/2 - 0.07, -0.03, 0.0, 0.0, H, m_fondo, bisel=0))
# Entramado de madera, como las casas del pack
cuerpo.append(caja("viga_baja", -W/2 - 0.02, W/2 + 0.02, -Dp - 0.025, -Dp + 0.02, H0 - 0.09, H0 + 0.02, m_marco))   # alféizar
cuerpo.append(caja("viga_alta", -W/2 - 0.02, W/2 + 0.02, -Dp - 0.025, -Dp + 0.02, H1 - 0.02, H1 + 0.08, m_marco))
cuerpo.append(caja("pie_i", -W/2 - 0.02, -W/2 + 0.08, -Dp - 0.025, -Dp + 0.02, 0.0, H, m_marco))
cuerpo.append(caja("pie_d", W/2 - 0.08, W/2 + 0.02, -Dp - 0.025, -Dp + 0.02, 0.0, H, m_marco))
cuerpo.append(caja("zocalo", -W/2 - 0.03, W/2 + 0.03, -Dp - 0.03, 0.0, 0.0, 0.12, m_marco))
for x in (-0.35, 0.0, 0.35):
    cuerpo.append(diagonal(f"aspa{x}", x - 0.16, 0.18, x + 0.16, H0 - 0.15, 0.05, -Dp - 0.02, -Dp + 0.01, m_marco))
# Tejadillo a un agua
techo = caja("techo", -W/2 - 0.12, W/2 + 0.12, -Dp - 0.18, 0.02, 0.0, 0.07, m_teja)
# Inclinado hacia la calle, girando sobre el canto de la pared (y=0) a la altura del dintel.
rot = Matrix.Rotation(math.radians(16), 4, 'X')
for v in techo.data.vertices:
    v.co = rot @ v.co
    v.co.z += H
cuerpo.append(techo)
# Jardinera en el alféizar
cuerpo.append(caja("jardinera", -W/2 + 0.08, W/2 - 0.08, -Dp - 0.2, -Dp - 0.01, H0 - 0.2, H0 - 0.02, m_marco))
cuerpo.append(caja("tierra", -W/2 + 0.11, W/2 - 0.11, -Dp - 0.18, -Dp - 0.03, H0 - 0.04, H0 - 0.0, mat("Tierra", FLOR_TIERRA), bisel=0))
import random
random.seed(4)
flores = []
for i in range(9):
    x = -W/2 + 0.16 + i * (W - 0.32) / 8
    col = [(0.95, 0.35, 0.45), (0.98, 0.85, 0.3), (0.95, 0.95, 0.95), (0.7, 0.4, 0.9)][i % 4]
    flores.append(caja(f"hoja{i}", x - 0.07, x + 0.07, -Dp - 0.17, -Dp - 0.04, H0 - 0.02, H0 + 0.07, mat("Hojas", (0.3, 0.62, 0.25)), bisel=0.02))
    flores.append(caja(f"flor{i}", x - 0.04, x + 0.04, -Dp - 0.15, -Dp - 0.07, H0 + 0.06, H0 + 0.12, mat(f"Flor{i%4}", col), bisel=0.015))
cuerpo += flores
juntar(cuerpo, "Mirador")
# Hojas: bisagras en los cantos del hueco, delante del marco
for lado, nombre in ((-1, "Hoja_Izq"), (1, "Hoja_Der")):
    hj = hoja(nombre, (W - 0.16) / 2, H1 - H0 + 0.02, 0.04, m_madera, m_hierro, espejo=(lado > 0))
    # De cara a la calle (-y Blender), en el borde exterior del hueco
    for v in hj.data.vertices: v.co.y += 0.0
    hj.location = (lado * (W/2 - 0.08), -Dp - 0.05, H0 - 0.01)
exportar("Mirador")

# 3 · Puerta de casa que se abre: marco, hoja con bisagra a la izquierda y el vano oscuro detrás.
limpiar()
m_marco, m_fondo, m_madera, m_hierro = mat("Marco", MARCO), mat("Fondo", FONDO), mat("Puerta", (0.48, 0.28, 0.14)), mat("Hierro", HIERRO)
PW, PH = 1.0, 2.05
marco = [caja("jamba_i", -PW/2 - 0.14, -PW/2, -0.16, 0.0, 0.0, PH + 0.12, m_marco),
         caja("jamba_d", PW/2, PW/2 + 0.14, -0.16, 0.0, 0.0, PH + 0.12, m_marco),
         caja("dintel", -PW/2 - 0.2, PW/2 + 0.2, -0.18, 0.0, PH, PH + 0.18, m_marco),
         caja("umbral", -PW/2 - 0.16, PW/2 + 0.16, -0.3, 0.0, 0.0, 0.06, mat("Piedra", (0.62, 0.62, 0.6))),
         caja("vano", -PW/2, PW/2, -0.03, 0.0, 0.06, PH, m_fondo, bisel=0)]
juntar(marco, "Marco")
hp = hoja("Hoja", PW - 0.02, PH - 0.08, 0.06, m_madera, m_hierro)
hp.location = (-PW/2 + 0.01, -0.09, 0.07)
exportar("PuertaDeCasa")
