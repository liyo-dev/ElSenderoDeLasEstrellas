# Taller de piezas para la ropa hecha por script sobre el esqueleto del pack (RPG Tiny Hero).
#
# Por qué existe: cada personaje llevaba su propia copia de las mismas funciones de modelado (tubo, torno, toro…),
# así que un arreglo hecho en uno no llegaba a los demás. Aquí están una sola vez, junto con piezas completas que ya
# traen las lecciones aprendidas (ver «Puntos a revisar» en LEEME.md), y una revisión antes de exportar.
#
# Uso en el script de un personaje:
#     sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Prendas"))
#     import ropa_lib as rl, taller as tl
#     t = tl.Taller(COLORES, grupos=("traje",))
#     t.torno("traje", "Tunica", PERFIL, "Crema", pesos)
#     t.manga_con_puno("traje", "l", cuerpo, arm, ...)
#     ...
#     tl.revisar_antes_de_exportar(t.grupos["traje"], cuerpo, arm)
#
# Es genérico: no sabe nada de ningún personaje concreto. El personaje mira a -Y; su izquierda es +X.
import math
import numpy as np
import bpy, bmesh
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

import ropa_lib as rl


class Taller:
    """Materiales y piezas de un personaje. grupos: nombres de los grupos de piezas (cada uno acaba en una malla)."""

    def __init__(self, colores, grupos=("traje",)):
        self.mat = {}
        for n, c in colores.items():
            m = bpy.data.materials.new(n); m.use_nodes = True
            m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
            m.diffuse_color = c; self.mat[n] = m
        self.grupos = {g: [] for g in grupos}

    # ── Primitivas ────────────────────────────────────────────────────────────────────────────────
    # Grosor que se pone solo a las piezas abiertas (mangas, perneras, botas, faldas sin grosor): por dentro hay que
    # pintar algo, porque Unity no pinta el revés y desde abajo se vería a través (ver chequeo_huecos.py).
    FORRO_MINIMO = 0.004

    def nueva(self, grupo, nombre, bm, mat, pesos=None, grosor=0.0, hacia_dentro=False):
        me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new(nombre, me); bpy.context.scene.collection.objects.link(o)
        o.data.materials.append(self.mat[mat[0] if isinstance(mat, tuple) else mat])
        if isinstance(mat, tuple): o.data.materials.append(self.mat[mat[1]])
        for p in o.data.polygons: p.use_smooth = True
        if grosor:
            s = o.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = grosor; s.offset = -1 if hacia_dentro else 1
            s.material_offset = 1 if isinstance(mat, tuple) else 0
        self.grupos[grupo].append((o, pesos)); return o

    @staticmethod
    def transformar(bm, centro, escala, rot):
        M = Matrix.Translation(centro) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*escala, 1))
        bmesh.ops.transform(bm, matrix=M, verts=bm.verts)

    def esfera(self, g, nombre, centro, escala, mat, pesos, rot=(0, 0, 0)):
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=1)
        self.transformar(bm, centro, escala, rot); return self.nueva(g, nombre, bm, mat, pesos)

    def toro(self, g, nombre, centro, mayor, menor, mat, pesos, rot=(0, 0, 0), escala=(1, 1, 1)):
        bpy.ops.mesh.primitive_torus_add(major_radius=mayor, minor_radius=menor, major_segments=28, minor_segments=10)
        o = bpy.context.object; bm = bmesh.new(); bm.from_mesh(o.data); bpy.data.objects.remove(o)
        self.transformar(bm, centro, escala, rot); return self.nueva(g, nombre, bm, mat, pesos)

    def torno(self, g, nombre, perfil, mat, pesos=None, seg=40, elip=0.92, cy=-0.01, onda=0.0, n_onda=8, grosor=None, abierto=None):
        """Pieza de revolución alrededor del eje Z: perfil [(z, radio)]. abierto=(a0, a1) deja sin cerrar ese arco
        (radianes, 0 = +X, π/2 = +Y atrás), para capas y túnicas abiertas.
        grosor: hacia fuera; sin decirlo, lleva FORRO_MINIMO hacia dentro (por dentro se pinta; mat=(fuera, dentro))."""
        bm = bmesh.new(); filas = []
        for f, (z, r) in enumerate(perfil):
            u = f / max(1, len(perfil) - 1); fila = []
            for k in range(seg + (1 if abierto else 0)):
                a = abierto[0] + (abierto[1] - abierto[0]) * k / seg if abierto else 2 * math.pi * k / seg
                rr = r * (1 + onda * u * math.sin(n_onda * a))
                fila.append(bm.verts.new((math.cos(a) * rr, cy + math.sin(a) * rr * elip, z)))
            filas.append(fila)
        n = len(filas[0])
        for f in range(len(filas) - 1):
            for k in range(n if not abierto else n - 1):
                cara = (filas[f][k], filas[f][(k + 1) % n], filas[f + 1][(k + 1) % n], filas[f + 1][k])
                bm.faces.new(cara if perfil[0][0] < perfil[-1][0] else cara[::-1])   # hacia fuera (Unity no pinta el revés)
        if grosor is None: return self.nueva(g, nombre, bm, mat, pesos, self.FORRO_MINIMO, hacia_dentro=True)
        return self.nueva(g, nombre, bm, mat, pesos, grosor)

    def tubo(self, g, nombre, puntos, radios, mat, pesos, seg=14, cerrar=True, grosor=None):
        """Tubo a lo largo de una curva de puntos, con su radio en cada punto.
        Abierto por los extremos (cerrar=False) y sin grosor dicho, lleva FORRO_MINIMO hacia dentro: por la boca se
        ve el interior. Un aro (primer punto = último) no lo necesita."""
        bm = bmesh.new(); filas = []; pts = [Vector(p) for p in puntos]
        if len(radios) != len(pts):
            radios = [radios[min(len(radios) - 1, round(i * (len(radios) - 1) / max(1, len(pts) - 1)))] for i in range(len(pts))]
        t_ant = None; a = None
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            if a is None: a = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0)); a = (a - t * a.dot(t)).normalized()
            else: a = (t_ant.rotation_difference(t) @ a); a = (a - t * a.dot(t)).normalized()
            t_ant = t; b = t.cross(a)
            filas.append([bm.verts.new(p + (a * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * radios[i]) for k in range(seg)])
        for f in range(len(filas) - 1):
            for k in range(seg):
                bm.faces.new((filas[f][k], filas[f][(k + 1) % seg], filas[f + 1][(k + 1) % seg], filas[f + 1][k]))
        if cerrar:
            for fila, inv in ((filas[0], True), (filas[-1], False)):
                c = bm.verts.new(sum((v.co for v in fila), Vector()) / len(fila))
                for k in range(seg):
                    tri = (c, fila[k], fila[(k + 1) % seg]); bm.faces.new(tri if not inv else tri[::-1])
        aro = (pts[0] - pts[-1]).length < 1e-6
        if grosor is None and not cerrar and not aro: grosor = self.FORRO_MINIMO
        if grosor: return self.nueva(g, nombre, bm, mat, pesos, grosor, hacia_dentro=True)
        return self.nueva(g, nombre, bm, mat, pesos)

    def caja(self, g, nombre, centro, medidas, mat, pesos, rot=(0, 0, 0)):
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
        self.transformar(bm, centro, medidas, rot); o = self.nueva(g, nombre, bm, mat, pesos)
        for p in o.data.polygons: p.use_smooth = False
        return o

    def estrella(self, g, nombre, centro, R_, r_, grosor, mat, pesos):
        """Estrella de cinco puntas mirando hacia delante (-Y)."""
        bm = bmesh.new(); cx, cy_, cz = centro
        fr = [bm.verts.new((cx + (R_ if k % 2 == 0 else r_) * math.sin(math.pi * k / 5), cy_ - grosor / 2,
                            cz + (R_ if k % 2 == 0 else r_) * math.cos(math.pi * k / 5))) for k in range(10)]
        at = [bm.verts.new((v.co.x, cy_ + grosor / 2, v.co.z)) for v in fr]
        bm.faces.new(fr); bm.faces.new(at[::-1])
        for k in range(10): bm.faces.new((fr[k], fr[(k + 1) % 10], at[(k + 1) % 10], at[k]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        o = self.nueva(g, nombre, bm, mat, pesos)
        for p in o.data.polygons: p.use_smooth = False
        return o

    def anillo(self, g, nombre, centro, eje, r_dentro, r_fuera, mat, pesos, seg=28):
        """Corona plana (arandela) perpendicular a `eje`, que mira hacia `eje`: tapa la boca de una manga o un bajo."""
        eje = Vector(eje).normalized(); a = eje.orthogonal().normalized(); b = eje.cross(a); c = Vector(centro)
        bm = bmesh.new()
        dentro = [bm.verts.new(c + (a * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * r_dentro) for k in range(seg)]
        fuera = [bm.verts.new(c + (a * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * r_fuera) for k in range(seg)]
        for k in range(seg):
            bm.faces.new((dentro[k], dentro[(k + 1) % seg], fuera[(k + 1) % seg], fuera[k]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        for f in bm.faces:
            if f.normal.dot(eje) < 0: f.normal_flip()
        return self.nueva(g, nombre, bm, mat, pesos)

    # ── Piezas completas ──────────────────────────────────────────────────────────────────────────
    def manga_con_puno(self, g, lado, cuerpo, arm, mat, mat_forro, mat_puno=None, mat_ribete=None,
                       hombro=0.17, boca=0.44, vuelo=0.10, holgura_puno=0.010, nombre="Manga", hombro_redondo=True):
        """Manga que se recoge en la muñeca, con el vuelo en el antebrazo, y su puño ceñido.
        - El radio del puño sale del brazo de verdad (medido en el cuerpo) más `holgura_puno`: un aro mucho más ancho
          que el brazo se mete en la tripa al juntar las manos delante (Archimago, 13 nov 2026).
        - La boca de la manga se tapa con una corona del color del forro, hasta el brazo: sin ella, en Unity (que no
          pinta el revés) se ve el hueco de dentro de la manga.
        - La manga copia los pesos del brazo; el puño y la tapa, también (no son aros rígidos a un hueso).
        hombro, boca: dónde empieza y acaba la manga (distancia al centro en X, en la pose en T).
        vuelo: radio máximo de la manga, a media altura del antebrazo.
        hombro_redondo: pone la bola del hombro (False si el personaje ya lleva la suya: hombreras, cuernos)."""
        s = 1 if lado == "l" else -1
        eje_y, eje_z = _eje_del_brazo(arm, lado)
        r_brazo = radio_del_miembro(cuerpo, arm, f"lowerarm_{lado}", f"hand_{lado}", boca)
        r_boca = r_brazo + holgura_puno
        codo = abs((arm.matrix_world @ arm.data.bones[f"lowerarm_{lado}"].head_local).x)
        xs = [hombro, (hombro + codo) / 2, codo, (codo + boca) / 2, boca - 0.012, boca]
        rs = [0.09, 0.088, 0.09, vuelo, r_boca + 0.012, r_boca]
        zona = rl.Zona(rl.BRAZO(lado))
        pts = [(x * s, eje_y, eje_z) for x in xs]
        manga = self.tubo(g, f"{nombre}_{lado}", pts, rs, (mat, mat_forro), zona, cerrar=False)
        if hombro_redondo: self.esfera(g, f"Hombro_{lado}", (0.175 * s, eje_y, eje_z + 0.007), (0.09, 0.086, 0.086), mat,
                    rl.Zona(rl.BRAZO(lado), suavizar=14))
        # tapa de la boca, del forro, desde dentro del brazo hasta la manga
        self.anillo(g, f"BocaManga_{lado}", (boca * s, eje_y, eje_z), (s, 0, 0), r_brazo * 0.8, r_boca + 0.002, mat_forro, zona)
        # puño: un cordón del forro en la boca y, detrás, un ribete fino; los dos ceñidos (a ~2 cm del brazo como mucho)
        def aro(x, r):
            return [(x * s, eje_y + r * math.cos(2 * math.pi * k / 24), eje_z + r * math.sin(2 * math.pi * k / 24)) for k in range(25)]
        self.tubo(g, f"PunoForro_{lado}", aro(boca - 0.006, r_boca + 0.001), [0.007], mat_puno or mat_forro, zona, seg=8, cerrar=False)
        if mat_ribete:
            self.tubo(g, f"PunoRibete_{lado}", aro(boca + 0.004, r_boca - 0.001), [0.005], mat_ribete, zona, seg=8, cerrar=False)
        return manga


    def bandolera(self, g, nombre, sobre, hombro, cadera_delante, cadera_detras, mat, radio=0.016, separacion=0.004, n=14):
        """Correa cruzada de una pieza: de la cadera, por delante, sobre el hombro junto al cuello y por la espalda
        hasta la cadera. Se apoya en la superficie de `sobre` (la prenda de debajo) y copia sus pesos, así no se mete
        en el cuerpo ni se separa (Oliver, 13 nov 2026: la correa eran dos medias que acababan en el hombro, sobre
        el arranque del brazo, y al moverlo se hundían).
        hombro: x del paso por el hombro (|x| ≤ 0,12: junto al cuello, no sobre el brazo)."""
        assert abs(hombro) <= 0.12, "la correa tiene que pasar junto al cuello, no sobre el arranque del brazo"
        dg = bpy.context.evaluated_depsgraph_get(); oe = sobre.evaluated_get(dg)
        bm = bmesh.new(); bm.from_object(sobre, dg); bm.transform(sobre.matrix_world)
        bvh = BVHTree.FromBMesh(bm); bm.free()
        cima = Vector((hombro, 0.0, 0.80))
        loc, nor, _, _ = bvh.ray_cast(cima, Vector((0, 0, -1)), 0.3)
        cima = loc if loc is not None else Vector((hombro, 0.0, 0.72))
        def tramo(a, b):
            a, b = Vector(a), Vector(b); medio = (a + b) / 2 + Vector((0, (a.y + b.y) * 0.6, 0))
            return [(1 - u) ** 2 * a + 2 * (1 - u) * u * medio + u * u * b for u in (i / (n - 1) for i in range(n))]
        pts = tramo(cadera_delante, cima)[:-1] + tramo(cima, cadera_detras)
        # cada punto se apoya en lo primero que se encuentra viniendo de fuera (no en la cara más cercana, que puede
        # ser el forro de dentro de la prenda)
        sobre_sup = []
        for p in pts:
            fuera = Vector((p.x, p.y + 0.01, 0.0))
            fuera = fuera.normalized() if fuera.length > 1e-6 else Vector((0, -1, 0))
            arriba = max(0.0, min(1.0, (p.z - 0.66) / 0.08))
            d = (fuera * (1 - arriba) + Vector((0, 0, 1)) * arriba).normalized()
            loc, nor, _, _ = bvh.ray_cast(p + d * 0.5, -d, 1.0)
            sobre_sup.append(loc + d * (radio + separacion) if loc is not None else p)
        return self.tubo(g, nombre, sobre_sup, [radio], mat, rl.Zona(sobre=sobre), seg=8, cerrar=True, grosor=0)

    def bota(self, g, lado, x, mat, mat_vuelta=None, alto=0.15, radio=0.08, nombre="Bota"):
        """Bota con una vuelta arriba que sobresale ~1 cm y es más clara: sin ese corte de silueta y de color, la bota
        y la pernera oscura se funden en una sola mancha a la sombra del faldón (Oliver, 13 nov 2026)."""
        s = 1 if lado == "l" else -1
        perfil = [(0.0, radio), (0.04, radio * 1.07), (alto * 0.66, radio * 0.92), (alto * 0.86, radio * 0.95), (alto, radio * 1.02)]
        o = self.torno(g, f"{nombre}_{lado}", perfil, mat, rl.Zona(rl.PIERNAS, suavizar=1), seg=24, elip=1.0, cy=0.0)
        for v in o.data.vertices: v.co.x += x * s
        if mat_vuelta:
            v = self.torno(g, f"{nombre}Vuelta_{lado}", [(alto - 0.006, radio * 1.13), (alto + 0.022, radio * 1.17)], mat_vuelta,
                           rl.Zona(rl.PIERNAS, suavizar=1), seg=24, elip=1.0, cy=0.0)
            for w in v.data.vertices: w.co.x += x * s
        return o

    def pernera(self, g, lado, x, mat, alto_bota=0.15, radio_bota=0.08, nombre="Pernera"):
        """Pernera que se mete en la bota: acaba 2 cm por dentro de su boca y más estrecha que ella. Si acaba a la par,
        al andar asoma por fuera de la bota y la bota «se pierde» en el pantalón (Oliver, 13 nov 2026)."""
        s = 1 if lado == "l" else -1
        fondo = alto_bota - 0.02
        pts = [(0.098 * s * x / 0.118, 0.003, 0.44), (0.103 * s * x / 0.118, 0.0, 0.33), (0.11 * s * x / 0.118, -0.001, 0.22),
               (0.116 * s * x / 0.118, 0.002, fondo)]
        return self.tubo(g, f"{nombre}_{lado}", pts, [0.092, 0.086, 0.078, radio_bota * 0.82], mat,
                         rl.Zona(rl.TRONCO + rl.PIERNAS, suavizar=1), cerrar=False)

    def quitar_lo_tapado(self, cuerpo, tapado, mat_tapa):
        """Quita del cuerpo de base lo que queda bajo la ropa (tapado(p) → True, p en coordenadas del mundo) y
        tapa los cortes con caras del color `mat_tapa`. Sin tapa, desde abajo (volando, tumbado) por el bajo de la
        ropa se ve el interior vacío del muñeco. Hacerlo después de pesar la ropa (copia los pesos del cuerpo entero)."""
        if mat_tapa not in [m.name for m in cuerpo.data.materials]: cuerpo.data.materials.append(self.mat[mat_tapa])
        mi = [m.name for m in cuerpo.data.materials].index(mat_tapa)
        bm = bmesh.new(); bm.from_mesh(cuerpo.data); mw = cuerpo.matrix_world
        for e in bm.edges: e.tag = e.is_boundary          # lo que ya estaba abierto (el cuello: lo tapa la cabeza)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if tapado(mw @ v.co)], context='VERTS')
        nuevos = [e for e in bm.edges if e.is_boundary and not e.tag]
        antes = set(bm.faces)
        bmesh.ops.holes_fill(bm, edges=nuevos, sides=0)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if f not in antes])
        tapas = [f for f in bm.faces if f not in antes]
        for f in tapas: f.material_index = mi
        # misma vuelta que la cara vecina del cuerpo (una arista compartida se recorre al revés en cada cara)
        def sentido(f, a, b):
            vs = list(f.verts); i = vs.index(a); return vs[(i + 1) % len(vs)] is b
        for _ in range(3):
            for f in tapas:
                for e in f.edges:
                    otra = next((g for g in e.link_faces if g is not f and (g not in tapas or _ > 0)), None)
                    if otra is None: continue
                    a_, b_ = e.verts
                    if sentido(f, a_, b_) == sentido(otra, a_, b_): f.normal_flip()
                    break
        bm.to_mesh(cuerpo.data); bm.free()
        return len(tapas)


# ── Medidas del cuerpo ────────────────────────────────────────────────────────────────────────────
def _eje_del_brazo(arm, lado):
    """Alto y fondo del brazo en la pose en T: la media entre el hombro, el codo y la muñeca."""
    hs = [arm.matrix_world @ arm.data.bones[f"{h}_{lado}"].head_local for h in ("upperarm", "lowerarm", "hand")]
    return sum(h.y for h in hs) / 3 - 0.003, sum(h.z for h in hs) / 3

def radio_del_miembro(cuerpo, arm, hueso_a, hueso_b, x, ancho=0.04):
    """Radio del cuerpo alrededor de la línea entre las cabezas de dos huesos (p. ej. lowerarm → hand), cerca de la
    distancia `x` al centro (en la pose en T). Se mide en los vértices del cuerpo con peso en esos huesos."""
    a = arm.matrix_world @ arm.data.bones[hueso_a].head_local
    b = arm.matrix_world @ arm.data.bones[hueso_b].head_local
    d = (b - a).normalized()
    idx = {g.index for g in cuerpo.vertex_groups if g.name in (hueso_a, hueso_b)}
    rs = []
    for v in cuerpo.data.vertices:
        if not any(gr.group in idx and gr.weight > 0.3 for gr in v.groups): continue
        p = cuerpo.matrix_world @ v.co
        t = (p - a).dot(d)
        if abs(abs(a.x + d.x * t) - abs(x)) > ancho: continue
        rs.append(((p - a) - d * t).length)
    if not rs: raise ValueError(f"No hay cuerpo entre {hueso_a} y {hueso_b} cerca de x={x}")
    return float(np.percentile(rs, 90))


# ── Contraste entre piezas que se tocan ─────────────────────────────────────────────────────────
CONTRASTE_MIN = 0.06        # diferencia mínima de luminosidad entre piezas que se tocan y no son del mismo color

def _luz(m):
    r, g, b = m.diffuse_color[:3]; return 0.2126 * r + 0.7152 * g + 0.0722 * b

def _contrastes(lista):
    """Piezas que se tocan con colores distintos pero casi la misma luminosidad: a la sombra se funden (las botas de
    Oliver contra el pantalón). Solo se avisa; puede ser a propósito."""
    avisos = []; cajas = []; vistos = set()
    for o, _ in lista:
        if not o.data.materials or not o.data.vertices: continue
        P = np.array([tuple(o.matrix_world @ v.co) for v in o.data.vertices])
        cajas.append((o, P.min(0), P.max(0), o.data.materials[0]))
    for i, (a, amin, amax, ma) in enumerate(cajas):
        for b, bmin, bmax, mb in cajas[i + 1:]:
            if ma is mb or ma.name.split(".")[0] == mb.name.split(".")[0]: continue
            if np.any(amin > bmax + 0.005) or np.any(bmin > amax + 0.005): continue
            d = abs(_luz(ma) - _luz(mb)); par = tuple(sorted((ma.name, mb.name)))
            if d < CONTRASTE_MIN and par not in vistos:
                vistos.add(par)
                avisos.append(f"Contraste: {par[0]} y {par[1]} (p. ej. {a.name} con {b.name}) se tocan con casi la misma luz "
                              f"({d:.2f}); a la sombra se funden. Si es a propósito (una figura toda en sombra), dejarlo.")
    return avisos


# ── Piezas en el paso de las manos ───────────────────────────────────────────────────────────────
# Con los brazos caídos y al andar, las manos van por los costados a la altura de la cadera. Una bolsa, un zurrón o un
# frasco ahí se lo come la mano (Oliver y Eldran, 14 nov 2026: la mano se metía en el libro del zurrón). Decisión de
# Raúl: los personajes no llevan bolsas ni mochilas; lo que vaya en la cintura, por delante o por detrás.
PASO_MANOS_X = (0.17, 0.40)      # distancia al centro, a cada lado
PASO_MANOS_Z = (0.22, 0.56)      # altura
PASO_MANOS_FONDO = 0.12          # solo lo que sobresale del costado (|y| pequeño: el costado, no delante ni detrás)
SOBRESALE = 0.215                # más lejos del eje del cuerpo que esto: no es la propia prenda del tronco

def _en_el_paso_de_las_manos(lista):
    avisos = []
    for o, pesos in lista:
        if isinstance(pesos, rl.Zona) and pesos.huesos and any("arm" in h or "hand" in h for h in pesos.huesos): continue
        P = np.array([tuple(o.matrix_world @ v.co) for v in o.data.vertices])
        if not len(P): continue
        if P[:, 0].min() < -0.1 and P[:, 0].max() > 0.1: continue      # da la vuelta al cuerpo (bajos, fajas): es ropa del tronco
        r = np.hypot(P[:, 0], (P[:, 1] + 0.01) / 0.92)
        dentro = ((np.abs(P[:, 0]) > PASO_MANOS_X[0]) & (np.abs(P[:, 0]) < PASO_MANOS_X[1]) & (P[:, 2] > PASO_MANOS_Z[0])
                  & (P[:, 2] < PASO_MANOS_Z[1]) & (np.abs(P[:, 1]) < PASO_MANOS_FONDO) & (r > SOBRESALE))
        if dentro.mean() > 0.3:
            avisos.append(f"Paso de las manos: {o.name} cuelga en el costado, donde van las manos al andar; la mano se mete "
                          f"dentro. Sin bolsas ni mochilas en el costado: por delante o por detrás.")
    return avisos


# ── Revisión antes de exportar ────────────────────────────────────────────────────────────────────
HOLGURA_RIGIDA = 0.02      # una pieza rígida alrededor de un miembro no se separa del cuerpo más que esto (m)

def revisar_antes_de_exportar(lista, cuerpo, arm, parar=False):
    """Lo que antes se veía tarde, en el juego:
    - escala: el personaje tiene que medir en metros (el pelo del Archimago se hinchaba en cm y quedaba pegado);
    - pesos: cada vértice suma 1 y no tiene más de 4 huesos (Unity corta el resto);
    - piezas rígidas (pesos fijos a un hueso del brazo o la pierna) que se separan del cuerpo más de HOLGURA_RIGIDA:
      al doblar el miembro hacia el tronco se meten en él (puños del Archimago).
    Devuelve la lista de avisos; con parar=True corta el script si hay alguno."""
    avisos = []
    zs = []
    for o, _ in lista:
        zs += [(o.matrix_world @ v.co).z for v in o.data.vertices]
    if zs and not (0.2 < max(zs) - min(zs) < 3.0):
        avisos.append(f"Escala: la ropa mide {max(zs) - min(zs):.3f} de alto; ¿está en metros?")
    for o, pesos in lista:
        me = o.data
        if any(g.name not in arm.data.bones for g in o.vertex_groups):
            avisos.append(f"{o.name}: grupos que no son huesos del esqueleto")
        malos = sum(1 for v in me.vertices if v.groups and abs(sum(g.weight for g in v.groups) - 1) > 0.02)
        muchos = sum(1 for v in me.vertices if len([g for g in v.groups if g.weight > 0.001]) > 4)
        if malos: avisos.append(f"{o.name}: {malos} vértices con pesos que no suman 1")
        if muchos: avisos.append(f"{o.name}: {muchos} vértices con más de 4 huesos")
        if isinstance(pesos, dict) and len(pesos) == 1:
            hueso = next(iter(pesos))
            if not any(k in hueso for k in ("arm", "hand", "calf", "thigh")): continue
            hijo = next((b.name for b in arm.data.bones[hueso].children), None)
            if not hijo: continue
            a = arm.matrix_world @ arm.data.bones[hueso].head_local
            b = arm.matrix_world @ arm.data.bones[hijo].head_local
            d = (b - a).normalized()
            P = [o.matrix_world @ v.co for v in me.vertices]
            r_pieza = max(((p - a) - d * (p - a).dot(d)).length for p in P)
            x = sum(p.x for p in P) / len(P)
            try: r_cuerpo = radio_del_miembro(cuerpo, arm, hueso, hijo, x)
            except ValueError: continue
            if r_pieza > r_cuerpo + HOLGURA_RIGIDA:
                avisos.append(f"{o.name}: pieza rígida en {hueso} a {r_pieza - r_cuerpo:.3f} m del cuerpo (máx. {HOLGURA_RIGIDA}); "
                              f"se meterá en el tronco al doblar el brazo o la pierna. Usa una pieza que copie pesos o cíñela.")
    avisos += _contrastes(lista)
    avisos += _en_el_paso_de_las_manos(lista)
    for a_ in avisos: print("REVISAR:", a_)
    if not avisos: print("REVISAR: nada")
    if parar and avisos: raise SystemExit("Revisión antes de exportar: " + "; ".join(avisos))
    return avisos
