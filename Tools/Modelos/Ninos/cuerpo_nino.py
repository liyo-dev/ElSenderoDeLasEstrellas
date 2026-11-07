# Cuerpo de niño sobre el esqueleto de RPG Tiny Hero: el mismo esqueleto (mismos huesos, mismos nombres,
# mismas orientaciones) con otras proporciones — piernas, tronco, cuello y brazos más cortos, hombros y
# caderas más estrechos, manos y pies algo más pequeños. Las mallas se deforman con el esqueleto (cada
# vértice sigue a sus huesos, mezclado por sus pesos), así que el cuerpo se acorta en vez de solaparse.
# La cabeza no cambia de tamaño aquí: la aumenta o reduce Unity con la escala del hueso «head» (así vale
# para cualquier pieza de cabeza del pack o hecha aparte).
#   args: base_adulto.blend, talla (mayor|pequeno), salida.blend
import bpy, sys, math
from mathutils import Vector, Matrix

TALLAS = {
    # mayor: 11-13 años (Tobías). pequeño: 5-8 años (Nina, Pía, Will de niño…)
    "mayor":   dict(piernas=0.75, tronco=0.82, cuello=0.85, brazos=0.82, manos=0.9, ancho=0.9, pies=0.88, grosor=0.94),
    "pequeno": dict(piernas=0.56, tronco=0.64, cuello=0.75, brazos=0.68, manos=0.82, ancho=0.82, pies=0.78, grosor=0.9),
}

def D(x, y, z): return Matrix.Diagonal((x, y, z))

def reglas(t):
    """Por hueso: (cómo se acorta el tramo desde su padre, cómo se deforma lo que pesa sobre él), en ejes del mundo."""
    p, tr, cu, br, ma, an, pi, gr = (t[k] for k in ("piernas", "tronco", "cuello", "brazos", "manos", "ancho", "pies", "grosor"))
    capa = (tr + p) / 2
    r = {}
    r["root"] = (D(1, 1, 1), D(1, 1, 1))
    r["pelvis"] = (D(1, 1, p), D(an, gr, capa))
    for s in "lr":
        r[f"thigh_{s}"] = (D(an, an, p), D(gr, gr, p))
        r[f"calf_{s}"] = (D(an, 1, p), D(gr, gr, p))
        r[f"foot_{s}"] = (D(an, 1, p), D(gr, pi, (1 + p) / 2))
        r[f"ball_{s}"] = (D(1, pi, (1 + p) / 2), D(gr, pi, (1 + p) / 2))
        r[f"clavicle_{s}"] = (D(an, 1, tr), D(an, gr, tr))
        r[f"upperarm_{s}"] = (D(an, 1, 1), D(br, gr, gr))
        r[f"shoulderPadJoint_{s}"] = (D(an, 1, 1), D(br, gr, gr))
        r[f"lowerarm_{s}"] = (D(br, 1, 1), D(br, gr, gr))
        r[f"hand_{s}"] = (D(br, 1, 1), D(ma, ma, ma))
        for dedo in ("index_01", "index_02", "index_03", "thumb_01", "thumb_02", "thumb_03", "weapon"):
            r[f"{dedo}_{s}"] = (D(ma, ma, ma), D(ma, ma, ma))
    for b in ("spine_01", "spine_02", "spine_03"):
        r[b] = (D(1, 1, tr), D(an, gr, tr))
    r["neck_01"] = (D(1, 1, tr), D(an, gr, cu))
    r["head"] = (D(1, 1, 1), D(1, 1, 1))     # el tramo cuello→cabeza va casi entero dentro de la cabeza
    r["CloakBone01"] = (D(an, gr, tr), D(an, gr, capa))
    r["CloakBone02"] = (D(an, gr, capa), D(an, gr, capa))
    r["CloakBone03"] = (D(an, gr, capa), D(an, gr, capa))
    r["BackpackBone"] = (D(1, gr, tr), D(an, gr, tr))
    return r

def reproporcionar(arm, talla):
    """Cambia las proporciones del esqueleto «arm» y de todas las mallas que dependen de él. Devuelve, por
    hueso, el desplazamiento de su cabeza (en el mundo)."""
    r = reglas(TALLAS[talla])
    mw = arm.matrix_world
    viejo = {b.name: mw @ b.head_local for b in arm.data.bones}
    cola = {b.name: mw @ b.tail_local for b in arm.data.bones}
    nuevo = {}
    for b in arm.data.bones:          # de padres a hijos (el orden de bones lo garantiza)
        tramo, _ = r.get(b.name, (D(1, 1, 1), None))
        nuevo[b.name] = viejo[b.name] if b.parent is None else nuevo[b.parent.name] + tramo @ (viejo[b.name] - viejo[b.parent.name])
    def mapa(nombre):
        _, s = r.get(nombre, (None, D(1, 1, 1)))
        return lambda v: nuevo[nombre] + s @ (v - viejo[nombre])
    mapas = {b.name: mapa(b.name) for b in arm.data.bones}
    # Mallas pesadas al esqueleto: cada vértice, mezcla de sus huesos
    colgadas = []
    for o in [o for o in bpy.data.objects if o.type == 'MESH']:
        if o.parent == arm and o.parent_type == 'BONE':
            d = nuevo[o.parent_bone] - viejo[o.parent_bone]
            colgadas.append((o, Matrix.Translation(d) @ o.matrix_world))   # se recoloca al acabar con el esqueleto
            continue
        if not any(m.type == 'ARMATURE' and m.object == arm for m in o.modifiers): continue
        ow = o.matrix_world; inv = ow.inverted()
        grupos = {g.index: g.name for g in o.vertex_groups}
        for v in o.data.vertices:
            p = ow @ v.co; acc = Vector(); tot = 0.0
            for g in v.groups:
                n = grupos.get(g.group)
                if n in mapas and g.weight > 0: acc += mapas[n](p) * g.weight; tot += g.weight
            if tot > 0: v.co = inv @ (acc / tot)
    # El esqueleto: cada hueso en su sitio nuevo, con la misma dirección (las mismas rotaciones que el pack)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    inv = mw.inverted()
    for eb in arm.data.edit_bones:
        n = eb.name
        _, s = r.get(n, (None, D(1, 1, 1)))
        vec = cola[n] - viejo[n]
        largo = (s @ vec).length if vec.length > 1e-6 else 0
        roll = eb.roll
        h = inv @ nuevo[n]; t = inv @ (nuevo[n] + vec.normalized() * largo)
        eb.use_connect = False
        eb.head = h; eb.tail = t; eb.roll = roll
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    for o, m in colgadas: o.matrix_world = m
    return {n: nuevo[n] - viejo[n] for n in nuevo}

if __name__ == "__main__" and "--" in sys.argv:
    a = sys.argv[sys.argv.index("--")+1:]
    bpy.ops.wm.open_mainfile(filepath=a[0])
    arm = bpy.data.objects["Armature"]
    d = reproporcionar(arm, a[1])
    arm["talla"] = a[1]
    bpy.ops.wm.save_as_mainfile(filepath=a[2])
    print(f"TALLA {a[1]}: la cabeza baja {-d['head'].z:.3f} m")
