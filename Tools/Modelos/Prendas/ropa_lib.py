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
    atras: la parte de detrás sigue al muslo solo esta fracción (sentado, la espalda queda en el asiento).
    Va de un lado a otro sin saltos: por delante y por detrás reparte entre los dos muslos, así no se abre."""
    def __init__(self, z_cintura, z_bajo, muslo=0.5, huesos=("pelvis", "thigh_l", "thigh_r"), banda=0.06, atras=0.35):
        self.z_cintura, self.z_bajo, self.muslo, self.huesos, self.banda = z_cintura, z_bajo, muslo, huesos, banda
        self.atras = atras   # cuánto sigue al muslo la parte de detrás respecto a la de delante (1 = igual)

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

def pesar(o, cuerpo, arm, zona=TODO, faldon=None, suavizar=4, fijos=None, sobre=None, rigida=False):
    """Pone pesos a la prenda `o` (sus modificadores de grosor ya aplicados) y la engancha a `arm`.
    zona: huesos de los que puede copiar (TRONCO, BRAZO('l'), TRONCO+PIERNAS...; None = todos).
    fijos: {hueso: peso} para piezas rígidas (hebillas, puños, joyas): ni se copia ni se suaviza.
    faldon: Faldon(...) para lo que cuelga por debajo de la cintura.
    sobre: prenda ya pesada sobre la que va pegada esta pieza (estrellas de una capa, botones, bordados): copia los
    pesos de esa prenda y no del cuerpo, así se mueven juntas y no se hunden la una en la otra.
    rigida: la pieza entera lleva los mismos pesos (la media de los que copia), así se mueve de una pieza y no se
    estira: un adorno pequeño (hebilla, botón, broche) entre dos huesos se rasgaba al doblar el tronco (19 nov 2026)."""
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
                # por detrás sigue menos al muslo: sentado, la espalda del faldón se queda en el asiento y no se rasga
                delante = max(-1.0, min(1.0, -(p.y + 0.01) / r))
                m = F.muslo * (F.atras + (1 - F.atras) * (delante + 1) / 2)
                wl, wr = m * t * (1 + s) / 2, m * t * (1 - s) / 2
                Wf = np.zeros(len(nombres)); Wf[ip], Wf[il], Wf[ir] = 1 - wl - wr, wl, wr
                mezcla = _suave((F.z_cintura + F.banda - p.z) / F.banda)
                W[i] = (1 - mezcla) * W[i] + mezcla * Wf
        if rigida: W[:] = W.mean(0)
        W[W < 0.01] = 0
        # como mucho 4 huesos por vértice (Unity descarta el resto y la prenda se mueve distinto que en Blender)
        if W.shape[1] > 4:
            corte = np.sort(W, 1)[:, -4][:, None]
            W[W < corte] = 0
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
    def __init__(self, huesos=None, faldon=None, suavizar=4, sobre=None, rigida=False):
        self.huesos, self.faldon, self.suavizar, self.sobre, self.rigida = huesos, faldon, suavizar, sobre, rigida

def pesar_piezas(lista, cuerpo, arm, con_cuerpo=True):
    """El cuerpo de base (lo que queda de él) también se etiqueta, como «Cuerpo»."""
    """lista: [(objeto, pesos)], con pesos = {hueso: peso} (rígida), Zona(...) o None (copia de todo el cuerpo).
    Etiqueta cada pieza con su nombre (atributo de cara «pieza») para que el chequeo diga qué pieza falla."""
    # primero las prendas y después lo que va pegado sobre ellas
    for o, pesos in sorted(lista, key=lambda x: isinstance(x[1], Zona) and x[1].sobre is not None):
        if isinstance(pesos, dict): pesar(o, cuerpo, arm, fijos=pesos)
        elif isinstance(pesos, Zona): pesar(o, cuerpo, arm, pesos.huesos, pesos.faldon, pesos.suavizar, sobre=pesos.sobre, rigida=pesos.rigida)
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


def seguir_etiquetas(objs, base):
    """Etiqueta `objs` a continuación de las piezas que ya lleva `base` (una malla ya unida con su lista me['piezas']),
    para unirlos a ella: si no, sus índices se pisan con los de `base` y el chequeo da nombres cambiados (Will con
    chaqueta, 19 nov 2026: la manga de la chaqueta salía como «Pernera_l»)."""
    previas = str(base.data.get("piezas", "")).split("|") if base.data.get("piezas") else []
    nombres = previas + [o.name for o in objs]
    for k, o in enumerate(objs):
        at = o.data.attributes.get("pieza") or o.data.attributes.new("pieza", 'INT', 'FACE')
        at.data.foreach_set("value", [len(previas) + k] * len(o.data.polygons))
    base.data["piezas"] = "|".join(nombres)
    for o in objs: o.data["piezas"] = "|".join(nombres)
    return nombres


# ── Zona de cada pieza a partir de su nombre y su forma (para los scripts que no la dicen pieza a pieza) ──
PALABRAS_DE_BRAZO = ("brazo", "manga", "hombro", "codera", "puno", "puño", "brazal", "guante", "muñequera", "remango")
PALABRAS_DE_PIERNA = ("bota", "zapato", "media", "calcet", "polaina", "suela", "puntera", "tobill", "sandalia")
AXILA_Z = (0.54, 0.70)      # alto del arranque del brazo en el cuerpo del pack
ADORNO_MAX = 0.08           # una pieza de tronco que cabe en una caja de este lado es un adorno: va rígida (ver pesar)
AXILA_X = 0.205             # ancho máximo de una prenda de tronco a esa altura (el cuerpo mide 0,18 y el brazo arranca en 0,20)

def _lado(nombre):
    """Lado de una pieza por su nombre: «Manga_l», «CorreaBrazal_r340»… (lo que sigue al lado son números)."""
    import re
    m = re.search(r"_([lr])\d*$", nombre)
    return m.group(1) if m else None

def zona_por_forma(o, z_cintura=0.40, muslo=0.65):
    """Zona de una pieza sin zona dicha: brazo o pierna por su nombre (Manga_l, Bota_r…); lo demás es tronco, y si
    baja de la cintura cuelga como faldón hasta su punto más bajo. Las perneras siguen tronco y piernas."""
    n = o.name.lower(); lado = _lado(n)
    # Por dónde está, además de por el nombre: una pieza que queda entera sobre el brazo (más allá del arranque, a la
    # altura del brazo en T) es del brazo aunque se llame «Vuelta» o «Remate». Si no, iba al tronco: al bajar el brazo
    # se quedaba flotando como un aro y el brazo la atravesaba (Victoria y el posadero, 19 nov 2026).
    ps = [o.matrix_world @ v.co for v in o.data.vertices]
    if ps and min(abs(p.x) for p in ps) > AXILA_X + 0.02 and AXILA_Z[0] < sum(p.z for p in ps) / len(ps) < AXILA_Z[1] \
            and len({p.x > 0 for p in ps}) == 1:
        return Zona(BRAZO("l" if ps[0].x > 0 else "r"))
    if any(k in n for k in PALABRAS_DE_BRAZO):
        if lado: return Zona(BRAZO(lado), suavizar=30 if "hombro" in n else 4)
        return Zona(TRONCO + BRAZO("l")[1:] + BRAZO("r")[1:])
    if "pernera" in n or "pantal" in n: return Zona(TRONCO + PIERNAS, suavizar=1)
    # en la pierna se suaviza poco: la rodilla tiene que doblar como la del cuerpo (suavizada, se queda atrás al correr)
    if any(k in n for k in PALABRAS_DE_PIERNA): return Zona(PIERNAS + ["pelvis"], suavizar=1)
    zs = [(o.matrix_world @ v.co).z for v in o.data.vertices]
    ps = [o.matrix_world @ v.co for v in o.data.vertices]
    if ps and max(max(p[k] for p in ps) - min(p[k] for p in ps) for k in range(3)) < ADORNO_MAX:
        return Zona(TRONCO + PIERNAS, rigida=True)   # hebilla, botón, broche: de una pieza
    # Solo cuelga como faldón lo que baja claramente de la cintura (un ribete a la altura de la cadera no)
    if zs and min(zs) < z_cintura - 0.06:
        return Zona(TRONCO + PIERNAS, Faldon(z_cintura, min(zs), muslo))
    return Zona(TRONCO + PIERNAS)

HOMBRO_Z = (0.70, 0.80)      # de lo alto de la axila al cuello
HOMBRO_X = 0.175             # ancho máximo de una prenda de tronco sobre el hombro (con el brazo bajado, el hombro acaba ahí)

def ceñir_axila(o):
    """Una prenda de tronco no pasa de AXILA_X de ancho a la altura del arranque del brazo: si la envuelve,
    al levantar el brazo la tela se rasga o el brazo la atraviesa.
    Por encima, sobre el hombro, no pasa de HOMBRO_X: un cuello ancho (0,20) se escondía en la manga con el brazo en T
    y, con el brazo bajado, asomaba a los lados como dos placas planas (Victoria, 19 nov 2026)."""
    for v in o.data.vertices:
        p = o.matrix_world @ v.co
        tope = AXILA_X if AXILA_Z[0] < p.z < AXILA_Z[1] else HOMBRO_X if HOMBRO_Z[0] <= p.z < HOMBRO_Z[1] else None
        if tope is not None and abs(p.x) > tope:
            q = p.copy(); q.x = math.copysign(tope, p.x)
            v.co = o.matrix_world.inverted() @ q

def anillos_en_la_rodilla(o, minimo=0.03):
    """Parte en tramos de como mucho `minimo` las aristas largas a lo largo de una pieza de pierna: un tubo con
    tres anillos no puede doblar la rodilla y el cuerpo la atraviesa al andar."""
    bm = bmesh.new(); bm.from_mesh(o.data)
    largas = [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > minimo * 1.5
              and abs(e.verts[0].co.z - e.verts[1].co.z) > (e.verts[0].co.xy - e.verts[1].co.xy).length]
    if largas:
        tramos = max(1, int(max(abs(e.verts[0].co.z - e.verts[1].co.z) for e in largas) / minimo))
        bmesh.ops.subdivide_edges(bm, edges=largas, cuts=min(tramos, 6), use_grid_fill=True)
    bm.to_mesh(o.data); bm.free()

def completar_zonas(lista, z_cintura=0.40, muslo=0.65, axila=True):
    """[(objeto, pesos)] con pesos None → la zona por su forma; las prendas de tronco se ciñen bajo el brazo.
    Los grosores (solidify) se aplican antes para medir la forma real."""
    salida = []
    for o, pesos in lista:
        if pesos is None:
            for m in [m for m in o.modifiers if m.type == 'SOLIDIFY']:
                bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
            pesos = zona_por_forma(o, z_cintura, muslo)
            if axila and pesos.huesos == TRONCO + PIERNAS and pesos.suavizar != 1: ceñir_axila(o)
            if pesos.suavizar == 1 and pesos.huesos and "pelvis" in pesos.huesos: anillos_en_la_rodilla(o)
        salida.append((o, pesos))
    return salida
