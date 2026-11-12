# Oliver: gorro de aprendiz nuevo, a juego con su túnica azul cielo (INC-1088). Gorro blando azul, más bajo que el
# sombrero de bruja del pack (Hat08), con la punta doblada hacia atrás y a un lado, ala corta,
# cinta naranja, una estrellita de latón delante y otra colgando de la punta. Deja ver la cara y las gafas.
# Sobre el esqueleto de RPG Tiny Hero (base.blend = la del vestuario de Oliver). Mira a -Y; su izquierda es +X.
#   Hat09_Aprendiz → pesado a «head». Args: base.blend, salida.blend
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
a = sys.argv[sys.argv.index("--")+1:]
BASE, SALIDA = a[0], a[1]
bpy.ops.wm.open_mainfile(filepath=BASE)
arm = bpy.data.objects["Armature"]
for o in list(bpy.data.objects):
    if o.type == 'MESH': bpy.data.objects.remove(o)
COLORES = {"Gorro": (0.2, 0.38, 0.64, 1), "GorroClaro": (0.5, 0.74, 0.93, 1), "Naranja": (0.96, 0.55, 0.16, 1), "Laton": (0.8, 0.62, 0.25, 1)}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c; m.diffuse_color = c; MAT[n] = m
R = math.radians
CY = -0.013   # centro de la cabeza (de delante atrás)
PIEZAS = []
def nueva(nombre, bm, mat, grosor=0.0):
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(MAT[mat])
    for p in o.data.polygons: p.use_smooth = True
    if grosor:
        s = o.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = grosor; s.offset = 1
    PIEZAS.append(o); return o
def transformar(bm, centro, escala, rot):
    M = Matrix.Translation(centro) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*escala, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
def esfera(nombre, centro, escala, mat, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1)
    transformar(bm, centro, escala, rot); return nueva(nombre, bm, mat)
def toro(nombre, centro, mayor, menor, mat, escala=(1, 1, 1), rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=mayor, minor_radius=menor, major_segments=36, minor_segments=10)
    t = bpy.context.object; bm = bmesh.new(); bm.from_mesh(t.data); bpy.data.objects.remove(t)
    transformar(bm, centro, escala, rot); return nueva(nombre, bm, mat)
def tubo(nombre, puntos, radios, mat, seg=24, elip=0.92):
    """Tubo por una curva con su radio en cada punto; cerrado en la punta."""
    bm = bmesh.new(); filas = []; pts = [Vector(p) for p in puntos]; t_ant = None; ax = None
    for i, p in enumerate(pts):
        t = (pts[min(i+1, len(pts)-1)] - pts[max(i-1, 0)]).normalized()
        if ax is None: ax = Vector((1, 0, 0))
        else: ax = t_ant.rotation_difference(t) @ ax
        ax = (ax - t * ax.dot(t)).normalized(); t_ant = t; b = t.cross(ax)
        filas.append([bm.verts.new(p + (ax * math.cos(2*math.pi*k/seg) + b * math.sin(2*math.pi*k/seg) * elip) * radios[i]) for k in range(seg)])
    for f in range(len(filas) - 1):
        for k in range(seg):
            bm.faces.new((filas[f][k], filas[f][(k+1) % seg], filas[f+1][(k+1) % seg], filas[f+1][k]))
    c = bm.verts.new(pts[-1])
    for k in range(seg): bm.faces.new((filas[-1][k], filas[-1][(k+1) % seg], c))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return nueva(nombre, bm, mat)
def bezier(p0, p1, p2, p3, n):
    P = [Vector(p) for p in (p0, p1, p2, p3)]
    return [(1-u)**3*P[0] + 3*(1-u)**2*u*P[1] + 3*(1-u)*u*u*P[2] + u**3*P[3] for u in (i/(n-1) for i in range(n))]

# Copa: sube recta desde la frente (la cabeza llega a 1,31 y mide ±0,37) y se cierra en una punta que cae
# hacia atrás y a su izquierda, con una estrellita colgando de la punta.
n = 16
camino = bezier((0, CY, 1.06), (0, CY, 1.5), (0.04, CY + 0.06, 1.78), (0.17, CY + 0.28, 1.7), n)
radios = [0.4, 0.385, 0.355, 0.315, 0.27, 0.225, 0.185, 0.15, 0.12, 0.095, 0.075, 0.058, 0.044, 0.032, 0.022, 0.014]
tubo("Copa", camino, radios, "Gorro")
# Vuelta del gorro (más clara, como el forro) y la cinta naranja encima
toro("Ala", (0, CY - 0.015, 1.07), 0.43, 0.06, "Gorro", escala=(1, 0.95, 0.38))   # ala corta y blanda, que no tape las gafas
toro("Cinta", (0, CY, 1.13), 0.4, 0.018, "Naranja", escala=(1, 0.93, 1))
# Estrellita de latón delante, sobre la cinta
def estrella(nombre, centro, r, rot):
    bm = bmesh.new(); puntas = []
    for k in range(10):
        ang = R(90 + 36 * k); rr = r if k % 2 == 0 else r * 0.44
        puntas.append(bm.verts.new((math.cos(ang) * rr, 0, math.sin(ang) * rr)))
    cara = bm.faces.new(puntas)
    bmesh.ops.solidify(bm, geom=[cara], thickness=0.014)
    transformar(bm, centro, (1, 1, 1), rot); return nueva(nombre, bm, "Laton")
estrella("Estrella", (0, CY - 0.4, 1.135), 0.055, (R(-8), 0, 0))
estrella("EstrellaPunta", camino[-1] + Vector((0.025, 0.02, -0.03)), 0.042, (0, 0, R(50)))

for o in PIEZAS:
    for m in [m for m in o.modifiers if m.type == 'SOLIDIFY']:
        bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
    o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
    vg = o.vertex_groups.new(name="head"); vg.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    md = o.modifiers.new("Esqueleto", 'ARMATURE'); md.object = arm
bpy.ops.object.select_all(action='DESELECT')
for o in PIEZAS: o.select_set(True)
bpy.context.view_layer.objects.active = PIEZAS[0]
bpy.ops.object.join(); bpy.context.object.name = "Hat09_Aprendiz"
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("HECHO")
