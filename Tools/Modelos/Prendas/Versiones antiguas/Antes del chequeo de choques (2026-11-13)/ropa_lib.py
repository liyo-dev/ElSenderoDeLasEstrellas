# Pesos de la ropa hecha por script sobre el esqueleto del pack (RPG Tiny Hero).
# Lo usan los scripts de cada personaje (Tools/Modelos/<Personaje>/<personaje>.py):
#     sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Prendas"))
#     import ropa_lib as rl
#     rl.pesar(objeto, cuerpo, arm, rl.TRONCO, faldon=rl.Faldon(0.36, 0.10))
#
# Por qué existe (13 nov 2026, vídeo del prólogo): copiar los pesos del cuerpo vértice a vértice
# («el más cercano») hace que una prenda de tronco que pasa cerca del arranque del brazo tome pesos del brazo
# (se rasga en la axila), que un faldón tome a la vez los dos muslos (se abre entre las piernas) y que los
# cambios de hueso sean bruscos. Aquí cada prenda dice de qué zona del cuerpo puede copiar, los pesos se
# interpolan en la cara del cuerpo más cercana (no del vértice suelto), se suavizan sobre la propia prenda
# y los faldones se pesan con una regla propia, continua de lado a lado.
import math
import numpy as np
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

# Zonas del esqueleto (nombres del pack; un esqueleto con otros nombres solo cambia estas listas)
TRONCO = ["pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "clavicle_l", "clavicle_r"]
PIERNAS = ["thigh_l", "calf_l", "foot_l", "ball_l", "thigh_r", "calf_r", "foot_r", "ball_r"]
def BRAZO(lado):
    return ["spine_03", f"clavicle_{lado}", f"upperarm_{lado}", f"lowerarm_{lado}", f"hand_{lado}"]
TODO = None

class Faldon:
    """Parte de la prenda por debajo de la cintura que cuelga como falda.
    z_cintura: donde empieza a colgar (por encima, los pesos copiados); z_bajo: el bajo.
    muslo: cuánto sigue al muslo de su lado en el bajo (0 = rígida a la pelvis, 1 = como una pernera).
    Va de un lado a otro sin saltos: por delante y por detrás reparte entre los dos muslos, así no se abre."""
    def __init__(self, z_cintura, z_bajo, muslo=0.5, huesos=("pelvis", "thigh_l", "thigh_r"), banda=0.06):
        self.z_cintura, self.z_bajo, self.muslo, self.huesos, self.banda = z_cintura, z_bajo, muslo, huesos, banda

def _suave(t):
    t = min(1.0, max(0.0, t)); return t * t * (3 - 2 * t)

def _pesos_del_cuerpo(cuerpo, nombres):
    W = np.zeros((len(cuerpo.data.vertices), len(nombres)))
    col = {g.index: nombres.index(g.name) for g in cuerpo.vertex_groups if g.name in nombres}
    for v in cuerpo.data.vertices:
        for g in v.groups:
            if g.group in col: W[v.index, col[g.group]] += g.weight
    return W

def _vecinos(me):
    n = [[] for _ in me.vertices]
    for e in me.edges:
        a, b = e.vertices; n[a].append(b); n[b].append(a)
    return n

def pesar(o, cuerpo, arm, zona=TODO, faldon=None, suavizar=4, fijos=None, sobre=None):
    """Pone pesos a la prenda `o` (sus modificadores de grosor ya aplicados) y la engancha a `arm`.
    zona: huesos de los que puede copiar (TRONCO, BRAZO('l'), TRONCO+PIERNAS...; None = todos).
    fijos: {hueso: peso} para piezas rígidas (hebillas, puños, joyas): ni se copia ni se suaviza.
    faldon: Faldon(...) para lo que cuelga por debajo de la cintura.
    sobre: prenda ya pesada sobre la que va pegada esta pieza (estrellas de una capa, botones, bordados): copia los
    pesos de esa prenda y no del cuerpo, así se mueven juntas y no se hunden la una en la otra."""
    nombres = [b.name for b in arm.data.bones]
    for m in [m for m in o.modifiers if m.type == 'SOLIDIFY']:
        bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
    mw = o.matrix_world.copy()
    o.parent = arm; o.matrix_world = mw
    for g in list(o.vertex_groups): o.vertex_groups.remove(g)
    grupos = {n: o.vertex_groups.new(name=n) for n in nombres}
    me = o.data; P = [o.matrix_world @ v.co for v in me.vertices]
    W = np.zeros((len(P), len(nombres)))
    if fijos:
        for h, w in fijos.items(): W[:, nombres.index(h)] = w
    else:
        fuente = sobre if sobre is not None else cuerpo
        if sobre is not None: zona, faldon, suavizar = None, None, 0
        Wc = _pesos_del_cuerpo(fuente, nombres)
        permit = np.ones(len(nombres), bool) if zona is None else np.array([n in zona for n in nombres])
        # caras del cuerpo que pertenecen a la zona (más de la mitad de su peso en huesos permitidos)
        en_zona = (Wc[:, permit].sum(1) > 0.5)
        bm = bmesh.new(); bm.from_mesh(fuente.data); bm.transform(fuente.matrix_world)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(en_zona[v.index] for v in f.verts)], context='FACES_ONLY')
        bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.faces.ensure_lookup_table()
        tris = [[v.index for v in f.verts] for f in bm.faces]
        vs = [v.co.copy() for v in bm.verts]; bm.free()
        if not tris: raise ValueError(f"{o.name}: la zona {zona} no tiene caras en el cuerpo")
        bvh = BVHTree.FromPolygons(vs, tris)
        Wz = np.where(permit, Wc, 0.0)
        for i, p in enumerate(P):
            loc, _, f, _ = bvh.find_nearest(p)
            a, b, c = (vs[k] for k in tris[f])
            # baricéntricas del punto más cercano en el triángulo
            v0, v1, v2 = b - a, c - a, loc - a
            d00, d01, d11, d20, d21 = v0.dot(v0), v0.dot(v1), v1.dot(v1), v2.dot(v0), v2.dot(v1)
            den = d00 * d11 - d01 * d01 or 1e-12
            l1 = (d11 * d20 - d01 * d21) / den; l2 = (d00 * d21 - d01 * d20) / den; l0 = 1 - l1 - l2
            W[i] = l0 * Wz[tris[f][0]] + l1 * Wz[tris[f][1]] + l2 * Wz[tris[f][2]]
        # suavizado sobre la prenda: quita los saltos bruscos de un hueso a otro
        vec = _vecinos(me)
        for _ in range(suavizar):
            W = 0.5 * W + 0.5 * np.array([W[n].mean(0) if n else W[i] for i, n in enumerate(vec)])
        if faldon:
            F = faldon; ip, il, ir = (nombres.index(h) for h in F.huesos)
            for i, p in enumerate(P):
                if p.z > F.z_cintura + F.banda: continue
                t = _suave((F.z_cintura - p.z) / max(1e-6, F.z_cintura - F.z_bajo))
                r = math.hypot(p.x, p.y) or 1e-6
                s = max(-1.0, min(1.0, p.x / r))              # +1 su izquierda (+X), -1 su derecha
                wl, wr = F.muslo * t * (1 + s) / 2, F.muslo * t * (1 - s) / 2
                Wf = np.zeros(len(nombres)); Wf[ip], Wf[il], Wf[ir] = 1 - wl - wr, wl, wr
                mezcla = _suave((F.z_cintura + F.banda - p.z) / F.banda)
                W[i] = (1 - mezcla) * W[i] + mezcla * Wf
        W[W < 0.01] = 0
        W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
    for j, n in enumerate(nombres):
        col = W[:, j]
        if not col.any(): o.vertex_groups.remove(grupos[n]); continue
        for i in np.nonzero(col)[0]: grupos[n].add([int(i)], float(col[i]), 'REPLACE')
    if not any(m.type == 'ARMATURE' for m in o.modifiers):
        o.modifiers.new("Esqueleto", 'ARMATURE').object = arm
    return o

class Zona:
    """Lo que se le pasa a cada pieza en el script del personaje: de qué huesos copia y si cuelga como falda."""
    def __init__(self, huesos=None, faldon=None, suavizar=4, sobre=None):
        self.huesos, self.faldon, self.suavizar, self.sobre = huesos, faldon, suavizar, sobre

def pesar_piezas(lista, cuerpo, arm, con_cuerpo=True):
    """El cuerpo de base (lo que queda de él) también se etiqueta, como «Cuerpo»."""
    """lista: [(objeto, pesos)], con pesos = {hueso: peso} (rígida), Zona(...) o None (copia de todo el cuerpo).
    Etiqueta cada pieza con su nombre (atributo de cara «pieza») para que el chequeo diga qué pieza falla."""
    # primero las prendas y después lo que va pegado sobre ellas
    for o, pesos in sorted(lista, key=lambda x: isinstance(x[1], Zona) and x[1].sobre is not None):
        if isinstance(pesos, dict): pesar(o, cuerpo, arm, fijos=pesos)
        elif isinstance(pesos, Zona): pesar(o, cuerpo, arm, pesos.huesos, pesos.faldon, pesos.suavizar, sobre=pesos.sobre)
        else: pesar(o, cuerpo, arm)
    if con_cuerpo: etiquetar([o for o, _ in lista] + [cuerpo], [o.name for o, _ in lista] + ["Cuerpo"])
    else: etiquetar([o for o, _ in lista])

def etiquetar(objs, nombres=None):
    """Atributo de cara «pieza» con el índice de cada objeto antes de unirlos; la lista va en me['piezas']."""
    nombres = nombres or [o.name for o in objs]
    for k, o in enumerate(objs):
        at = o.data.attributes.get("pieza") or o.data.attributes.new("pieza", 'INT', 'FACE')
        at.data.foreach_set("value", [k] * len(o.data.polygons))
        o.data["piezas"] = "|".join(nombres)
    return nombres
