# Pelo de príncipe para Will (propuestas). Un volumen con forma (casquete con tupé o melena, hinchar_casquete con
# «volumen») y pocos mechones anchos en pétalo, como el pelo de dibujo: grandes masas, nada de hebras.
#   A «príncipe»: tupé suave que sube y cae hacia su derecha, raya a su izquierda, flequillo en tres pétalos.
#   B «melena»: media melena que tapa media oreja, con las puntas hacia fuera y flequillo de cortina.
#   C «héroe pulido»: sus picos de siempre, hacia arriba y atrás, pero anchos y con las puntas suaves.
#   D «príncipe con picos» (A + C): el tupé y el flequillo barrido de A, y en la coronilla y detrás picos suaves de C
#     que se van hacia atrás.
#   args: modular.blend salida.blend variante
import bpy, bmesh, math, sys, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a = sys.argv[sys.argv.index("--") + 1:]
VAR = a[2] if len(a) > 2 else "A"
sys.argv = ["x", "--", a[0], "Head01_Male", "normal", "neutra", "/dev/null", "chicoL", "x", "gris"]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "caras_lib.txt")).read())
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
# Los materiales se llaman como los del pelo de Will en el juego («Pelo», «PeloClaro»): su ficha les pone el rubio miel.
RUBIO = mat("Pelo", (0.72, 0.44, 0.16, 1)); CLARO = mat("PeloClaro", (0.86, 0.6, 0.28, 1)); RUBIO_O = RUBIO
mw = cabeza.matrix_world
C = Vector((0, -0.02, 0.99))

def direccion(az, el):
    t, e = math.radians(az), math.radians(el)
    return Vector((math.sin(t) * math.cos(e), -math.cos(t) * math.cos(e), math.sin(e))).normalized()
def campana(w, az, el, ancho_grados):
    """1 en la dirección (az, el) y baja suave alrededor."""
    d = (w - C).normalized(); ang = math.degrees(d.angle(direccion(az, el)))
    return math.exp(-(ang / ancho_grados) ** 2)
def linea(w, frente, lado, nuca):
    ang = math.atan2(-(w.y + 0.013), max(abs(w.x), 0.001))
    return lado + ((frente - lado) * math.sin(ang) ** 2 if ang > 0 else (nuca - lado) * min(1.0, 2.0 * abs(math.sin(ang))))

if VAR == "A":
    SEP, LINEA = 0.026, (1.17, 0.97, 0.80)
    VOL = lambda w: 0.07 * campana(w, -12, 50, 32) + 0.02 * campana(w, 0, 90, 50)
elif VAR == "B":
    SEP, LINEA = 0.03, (1.16, 0.90, 0.68)
    VOL = lambda w: 0.03 * campana(w, 0, 80, 60) + 0.03 * campana(w, 180, 20, 60)
elif VAR == "C":
    SEP, LINEA = 0.026, (1.17, 0.97, 0.80)
    VOL = lambda w: 0.03 * campana(w, 0, 70, 50)
else:
    SEP, LINEA = 0.026, (1.17, 0.97, 0.80)
    VOL = lambda w: 0.06 * campana(w, -12, 52, 30) + 0.02 * campana(w, 0, 90, 50)

# El volumen: una esfera fina proyectada sobre la cabeza (sin orejas) y separada SEP + VOL, cortada por la línea del
# pelo vértice a vértice (borde liso, no en escalera como las caras de la cabeza).
bmw = bmesh.new(); bmw.from_mesh(cabeza.data); bmw.transform(mw); ARBOL_CAB = BVHTree.FromBMesh(bmw)
bmw.faces.ensure_lookup_table(); islas, vistas = [], set()
for f in bmw.faces:
    if f in vistas: continue
    pila, isla = [f], []
    while pila:
        g = pila.pop()
        if g in vistas: continue
        vistas.add(g); isla.append(g); pila += [h for e in g.edges for h in e.link_faces if h not in vistas]
    islas.append(isla)
mayor = set(max(islas, key=len))
bmesh.ops.delete(bmw, geom=[f for f in bmw.faces if f not in mayor], context='FACES')
ARBOL_CRANEO = BVHTree.FromBMesh(bmw)          # la cabeza sin orejas: el pelo pasa por encima de ellas
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=64, v_segments=40, radius=1.0)
for v in bm.verts:
    d = v.co.normalized(); h = ARBOL_CRANEO.ray_cast(C + d * 0.9, -d, 1.0)
    r = (h[0] - C).length if h[0] is not None else 0.33
    v.co = C + d * r
quitar = [v for v in bm.verts if v.co.z < linea(v.co, *LINEA)]
bmesh.ops.delete(bm, geom=quitar, context='VERTS')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
# Borde liso: los vértices del borde se promedian solo con sus vecinos del borde (quita los dientes de sierra) y se
# vuelven a apoyar en la cabeza.
for _ in range(12):
    nuevo = {}
    for v in bm.verts:
        vec = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
        if len(vec) == 2: nuevo[v] = v.co * 0.4 + (vec[0].co + vec[1].co) * 0.3
    for v, co in nuevo.items():
        d = (co - C).normalized(); h = ARBOL_CRANEO.ray_cast(C + d * 0.9, -d, 1.0)
        v.co = C + d * ((h[0] - C).length if h[0] is not None else (co - C).length)
ident = cabeza.matrix_world.Identity(4)
# hinchar_casquete trabaja en el espacio de la malla: aquí ya es el del mundo (escala 1)
bmc0 = bmesh.new(); bmc0.from_mesh(cabeza.data); bmc0.transform(mw); ARBOL_CAB_MUNDO = BVHTree.FromBMesh(bmc0)
normales = hinchar_casquete(bm, ident, SEP, arbol_cabeza=ARBOL_CAB_MUNDO, suavizar=4, volumen=VOL, subdividir=False)
me = bpy.data.meshes.new("Casquete"); bm.to_mesh(me); bm.free()
cas = bpy.data.objects.new("Casquete", me); bpy.context.scene.collection.objects.link(cas); cas.data.materials.append(RUBIO)
normales_del_faldon(me, normales)
bpy.context.view_layer.update()
ARBOL_CAS = BVHTree.FromObject(cas, bpy.context.evaluated_depsgraph_get())

def sobre(az, el, alto):
    """Punto a «alto» metros por encima de lo que haya debajo (el pelo o, fuera de él, la piel)."""
    d = direccion(az, el); r = 0.0
    for arbol in (ARBOL_CAS, ARBOL_CAB):
        h = arbol.ray_cast(C + d * 0.9, -d, 1.0)
        if h[0] is not None: r = max(r, (h[0] - C).length)
    return C + d * ((r or 0.34) + alto)

def petalo(nombre, ctrl, ancho, grosor, material, n=7, seg=8, curva=0.35):
    """Mechón ancho en pétalo por los puntos (az, el, alto): nace ancho, se afila en punta suave y se curva
    («curva»: la sección es una lente combada, como un mechón de dibujo)."""
    P = [sobre(*c) for c in ctrl]
    pts = []
    for i in range(len(P) - 1):
        p0, p1, p2, p3 = P[max(i - 1, 0)], P[i], P[i + 1], P[min(i + 2, len(P) - 1)]
        for k in range(n):
            u = k / n
            pts.append(0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3))
    pts.append(P[-1])
    bmm = bmesh.new(); filas = []
    for i, p in enumerate(pts):
        s = i / (len(pts) - 1)
        T = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        N = (p - C).normalized(); N = (N - T * N.dot(T)).normalized(); B = T.cross(N)
        w = ancho * (0.75 + 0.25 * math.sin(math.pi * min(s / 0.3, 1) * 0.5)) * (1 - s ** 1.6) + 0.003
        g = grosor * (1 - 0.7 * s)
        fila = []
        for k in range(seg):
            ang = 2 * math.pi * k / seg
            x, y = math.cos(ang), math.sin(ang)
            fila.append(bmm.verts.new(p + B * (w * x) + N * (g * y - curva * w * (1 - x * x) * 0.25)))
        filas.append(fila)
    for f in range(len(filas) - 1):
        for k in range(seg): bmm.faces.new((filas[f][k], filas[f][(k + 1) % seg], filas[f + 1][(k + 1) % seg], filas[f + 1][k]))
    for fila, inv in ((filas[0], True), (filas[-1], False)):
        c = bmm.verts.new(sum((v.co for v in fila), Vector()) / seg)
        for k in range(seg):
            tri = (c, fila[k], fila[(k + 1) % seg]); bmm.faces.new(tri[::-1] if inv else tri)
    bmesh.ops.recalc_face_normals(bmm, faces=bmm.faces[:])
    m2 = bpy.data.meshes.new(nombre); bmm.to_mesh(m2); bmm.free()
    o = bpy.data.objects.new(nombre, m2); bpy.context.scene.collection.objects.link(o); o.data.materials.append(material)
    for p in m2.polygons: p.use_smooth = True
    return o

M = []
if VAR == "A":
    # Flequillo: tres pétalos que salen del tupé y caen barridos hacia su derecha, dejando ver la frente por su izquierda.
    M.append(petalo("Fleco1", [(22, 62, -0.012), (5, 48, 0.03), (-18, 36, 0.028), (-38, 27, 0.012)], 0.15, 0.032, CLARO, curva=0.3))
    M.append(petalo("Fleco2", [(0, 60, -0.012), (-22, 46, 0.028), (-44, 34, 0.02), (-60, 24, 0.008)], 0.12, 0.028, RUBIO, curva=0.3))
    M.append(petalo("Fleco3", [(36, 58, -0.012), (42, 46, 0.02), (48, 34, 0.012)], 0.08, 0.022, RUBIO, curva=0.3))
elif VAR == "B":
    # Cortina partida a su izquierda.
    M.append(petalo("CortinaD", [(22, 70, -0.012), (5, 52, 0.03), (-18, 36, 0.03), (-42, 22, 0.02), (-58, 8, 0.012)], 0.13, 0.03, CLARO))
    M.append(petalo("CortinaI", [(28, 70, -0.012), (40, 52, 0.03), (55, 34, 0.025), (66, 18, 0.012)], 0.1, 0.028, RUBIO))
    # Melena alrededor: pétalos anchos que bajan y doblan la punta hacia fuera.
    for k, az0 in enumerate(range(80, 290, 26)):
        M.append(petalo(f"Melena{k}", [(az0, 60, -0.012), (az0, 30, 0.02), (az0, 4, 0.025), (az0 + 3, -8, 0.05)], 0.16, 0.03, RUBIO if k % 2 else RUBIO_O))
elif VAR == "D":
    M.append(petalo("Fleco1", [(22, 62, -0.012), (5, 48, 0.03), (-18, 36, 0.028), (-38, 27, 0.012)], 0.15, 0.032, CLARO, curva=0.3))
    M.append(petalo("Fleco2", [(0, 60, -0.012), (-22, 46, 0.028), (-44, 34, 0.02), (-60, 24, 0.008)], 0.12, 0.028, RUBIO, curva=0.3))
    M.append(petalo("Fleco3", [(36, 58, -0.012), (42, 46, 0.02), (48, 34, 0.012)], 0.08, 0.022, RUBIO, curva=0.3))
    # Coronilla y detrás: mechones largos que nacen arriba y bajan peinados hacia atrás, con la punta un poco levantada.
    for k, az0 in enumerate((62, 95, 128, 160, 200, 232, 265, 298)):
        atras = 1 if az0 < 180 else -1
        M.append(petalo(f"Atras{k}", [(az0 - 10 * atras, 84, -0.012), (az0, 66, 0.022), (az0 + 6 * atras, 46, 0.03), (az0 + 10 * atras, 30, 0.045)],
                        0.13, 0.03, CLARO if k % 3 == 1 else RUBIO, curva=0.25))
else:
    for k, (az0, el0, lev, w) in enumerate([(5, 60, 0.09, 0.14), (40, 62, 0.08, 0.13), (-35, 62, 0.08, 0.13), (80, 58, 0.06, 0.12), (-80, 58, 0.06, 0.12),
                                            (120, 60, 0.05, 0.12), (-120, 60, 0.05, 0.12), (160, 64, 0.05, 0.12), (-160, 64, 0.05, 0.12)]):
        M.append(petalo(f"Pico{k}", [(az0, el0 - 16, -0.012), (az0 + 3, el0, 0.03), (az0 + 8, el0 + 12, lev), (az0 + 12, el0 + 16, lev + 0.025)], w, 0.035, CLARO if k % 3 == 0 else RUBIO, curva=0.2))
    M.append(petalo("Flequillo", [(25, 58, -0.012), (8, 46, 0.03), (-14, 36, 0.025), (-34, 28, 0.01)], 0.11, 0.028, RUBIO))
    M.append(petalo("Flequillo2", [(0, 56, -0.012), (-18, 44, 0.025), (-36, 36, 0.015), (-50, 30, 0.006)], 0.08, 0.022, RUBIO_O))

bpy.ops.object.select_all(action='DESELECT')
for o in [cas] + M: o.select_set(True)
bpy.context.view_layer.objects.active = cas
bpy.ops.object.join(); pelo = bpy.context.object; pelo.name = os.environ.get("NOMBRE_PELO", "Hair21_Principe")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
vg = pelo.vertex_groups.new(name="head"); vg.add(list(range(len(pelo.data.vertices))), 1.0, 'REPLACE')
pelo.parent = arm; pelo.matrix_parent_inverse = arm.matrix_world.inverted()
pelo.modifiers.new("Esqueleto", 'ARMATURE').object = arm
if os.environ.get("SOLO_PELO") == "1": bpy.data.objects.remove(cabeza)
bpy.ops.wm.save_as_mainfile(filepath=a[1])
print("PELO", VAR, pelo.name, len(pelo.data.vertices), [m.name for m in pelo.data.materials])
