# -*- coding: utf-8 -*-
"""Utilidades para escribir escenas de Unity como texto, desde fuera del Editor.

Por qué existe: la sesión de Cowork y Raúl compiten por el foco de Play/Stop si se intenta
construir la escena con el Editor en vivo (pasó con SEQ_StarAwakening el 16 sep). Escribir el YAML
directamente es determinista, versionable y se puede repetir: el decorado del prólogo se genera
entero desde aquí, así que cambiar la composición es cambiar números en layout.py y volver a
ejecutar, no arrastrar ciento cincuenta objetos a mano.
"""
import json, os, re, math

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# ── guid <-> ruta ─────────────────────────────────────────────────────────────
# Se leen del SourceAssetDB de Unity (Library/), que trae la tabla entera y tarda un segundo.
def cargar_guids():
    cache = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_guids.json")
    if os.path.exists(cache):
        return json.load(open(cache, encoding="utf-8"))
    data = open(os.path.join(RAIZ, "Library", "SourceAssetDB"), "rb").read()
    m = {}
    for mo in re.finditer(rb"Assets/[^\x00]{1,300}", data):
        s = mo.start()
        g = data[s-16:s]
        if len(g) != 16:
            continue
        texto = "".join("%x%x" % (x & 0x0f, x >> 4) for x in g)
        ruta = mo.group().decode("utf-8", "ignore")
        if ruta.endswith(".meta"):
            continue
        m.setdefault(texto, ruta)
    json.dump(m, open(cache, "w", encoding="utf-8"))
    return m

GUIDS = cargar_guids()
POR_RUTA = {v: k for k, v in GUIDS.items()}
POR_NOMBRE = {}
for g, v in GUIDS.items():
    POR_NOMBRE.setdefault(os.path.basename(v), g)

def guid(nombre_o_ruta):
    """guid de un asset por nombre de fichero ('Tree02_a01.prefab') o por ruta completa."""
    if nombre_o_ruta in POR_RUTA:
        return POR_RUTA[nombre_o_ruta]
    g = POR_NOMBRE.get(nombre_o_ruta)
    if g is None:
        raise KeyError("No encuentro el asset '%s' en el proyecto" % nombre_o_ruta)
    return g

def ruta(nombre):
    return GUIDS[guid(nombre)]

# ── raíz de un prefab ─────────────────────────────────────────────────────────
_cache_raices = {}

def raiz_prefab(nombre):
    """(fileID del GameObject raíz, fileID del Transform raíz) de un prefab."""
    if nombre in _cache_raices:
        return _cache_raices[nombre]
    p = os.path.join(RAIZ, ruta(nombre))
    texto = open(p, encoding="utf-8", errors="ignore").read()
    docs = re.split(r"^--- !u!(\d+) &(\d+)(?: stripped)?\s*$", texto, flags=re.M)
    transform_raiz = go_raiz = None
    for i in range(1, len(docs), 3):
        cid, fid, cuerpo = docs[i], docs[i+1], docs[i+2]
        if cid != "4":
            continue
        padre = re.search(r"m_Father: \{fileID: (\d+)\}", cuerpo)
        if padre and padre.group(1) == "0":
            transform_raiz = fid
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", cuerpo)
            go_raiz = go.group(1)
            break
    if transform_raiz is None:
        raise RuntimeError("No encuentro el Transform raíz de %s" % nombre)
    _cache_raices[nombre] = (go_raiz, transform_raiz)
    return _cache_raices[nombre]

# ── generación de YAML ────────────────────────────────────────────────────────
def quat_y(grados):
    r = math.radians(grados) * 0.5
    return (0.0, math.sin(r), 0.0, math.cos(r))

def quat(euler):
    """Quaternion desde ángulos de Euler como los aplica Unity (Z, luego X, luego Y)."""
    if not isinstance(euler, (tuple, list)):
        euler = (0.0, euler, 0.0)
    ex, ey, ez = [math.radians(a) * 0.5 for a in euler]
    cx, sx = math.cos(ex), math.sin(ex)
    cy, sy = math.cos(ey), math.sin(ey)
    cz, sz = math.cos(ez), math.sin(ez)
    # q = qy * qx * qz
    qy = (0, sy, 0, cy); qx = (sx, 0, 0, cx); qz = (0, 0, sz, cz)
    def mul_q(a, b):
        ax, ay, az, aw = a; bx, by, bz, bw = b
        return (aw*bx + ax*bw + ay*bz - az*by,
                aw*by - ax*bz + ay*bw + az*bx,
                aw*bz + ax*by - ay*bx + az*bw,
                aw*bw - ax*bx - ay*by - az*bz)
    return mul_q(mul_q(qy, qx), qz)

def euler_de(rot):
    return rot if isinstance(rot, (tuple, list)) else (0.0, rot, 0.0)

def f(x):
    """Unity escribe los floats sin ceros de más."""
    if x == int(x):
        return str(int(x))
    return repr(round(float(x), 5))

class Escena:
    def __init__(self):
        self.docs = []
        self._id = 1000000

    def nuevo_id(self):
        self._id += 2
        return self._id

    def añadir(self, texto):
        self.docs.append(texto.rstrip("\n") + "\n")

    def reservar(self):
        go = self.nuevo_id()
        return go, go + 1

    def bloque(self, n):
        """Reserva n identificadores CONSECUTIVOS y deja el contador pasado de ellos.

        Hace falta porque un objeto con varios componentes (el suelo: GameObject + Transform +
        MeshFilter + MeshRenderer + MeshCollider) necesita un id por componente, y calcularlos
        como tr+2, tr+4... pisaba los del objeto siguiente. Unity lo rechaza entero: "File has
        multiple objects with same identifiers"."""
        base = self._id + 2
        self._id = base + n + 1
        return [base + i for i in range(n)]

    def vacio(self, nombre, pos=(0, 0, 0), padre=0, rot_y=0.0, activo=True, hijos=None, ids=None):
        """GameObject vacío (grupo de jerarquía o marca de posición)."""
        go, tr = ids if ids else (self.nuevo_id(), self.nuevo_id() - 1 + 1)
        if not ids: tr = go + 1
        qx, qy, qz, qw = quat(rot_y)
        lista_hijos = "".join("\n  - {fileID: %d}" % h for h in (hijos or [])) or " []"
        self.añadir("""--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: %d}
  m_Layer: 0
  m_Name: %s
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: %d
--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}
  m_LocalPosition: {x: %s, y: %s, z: %s}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:%s
  m_Father: {fileID: %d}
  m_LocalEulerAnglesHint: {x: 0, y: %s, z: 0}""" % (
            go, tr, nombre, 1 if activo else 0, tr, go,
            f(qx), f(qy), f(qz), f(qw), f(pos[0]), f(pos[1]), f(pos[2]),
            lista_hijos, padre, f(euler_de(rot_y)[1])))
        return go, tr

    def prefab(self, nombre_prefab, nombre_objeto, pos, rot_y=0.0, escala=1.0, padre=0,
               estatico=True, activo=True):
        """Instancia de prefab con sus modificaciones de posición/rotación/escala."""
        g = guid(nombre_prefab)
        go_src, tr_src = raiz_prefab(nombre_prefab)
        inst = self.nuevo_id()
        tr_stripped = self.nuevo_id()
        qx, qy, qz, qw = quat(rot_y)
        ex_, ey_, ez_ = euler_de(rot_y)
        sx = sy = sz = escala if not isinstance(escala, (tuple, list)) else None
        if isinstance(escala, (tuple, list)):
            sx, sy, sz = escala

        mods = [
            ("m_Name", nombre_objeto, go_src),
            ("m_IsActive", "1" if activo else "0", go_src),
            ("m_StaticEditorFlags", "4294967295" if estatico else "0", go_src),
            ("m_LocalPosition.x", f(pos[0]), tr_src),
            ("m_LocalPosition.y", f(pos[1]), tr_src),
            ("m_LocalPosition.z", f(pos[2]), tr_src),
            ("m_LocalRotation.x", f(qx), tr_src),
            ("m_LocalRotation.y", f(qy), tr_src),
            ("m_LocalRotation.z", f(qz), tr_src),
            ("m_LocalRotation.w", f(qw), tr_src),
            ("m_LocalScale.x", f(sx), tr_src),
            ("m_LocalScale.y", f(sy), tr_src),
            ("m_LocalScale.z", f(sz), tr_src),
            ("m_LocalEulerAnglesHint.x", f(ex_), tr_src),
            ("m_LocalEulerAnglesHint.y", f(ey_), tr_src),
            ("m_LocalEulerAnglesHint.z", f(ez_), tr_src),
        ]
        cuerpo = "".join(
            "    - target: {fileID: %s, guid: %s, type: 3}\n"
            "      propertyPath: %s\n"
            "      value: %s\n"
            "      objectReference: {fileID: 0}\n" % (destino, g, prop, valor)
            for prop, valor, destino in mods)

        self.añadir("""--- !u!1001 &%d
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: %d}
    m_Modifications:
%s    m_RemovedComponents: []
  m_RemovedGameObjects: []
  m_AddedGameObjects: []
  m_AddedComponents: []
  m_SourcePrefab: {fileID: 100100000, guid: %s, type: 3}
--- !u!4 &%d stripped
Transform:
  m_CorrespondingSourceObject: {fileID: %s, guid: %s, type: 3}
  m_PrefabInstance: {fileID: %d}
  m_PrefabAsset: {fileID: 0}""" % (inst, padre, cuerpo, g, tr_stripped, tr_src, g, inst))
        return tr_stripped
