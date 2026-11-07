# Animaciones del conejo: poses con rotaciones ABSOLUTAS en el espacio del esqueleto (sobre la pose de
# reposo en T). Un hueso sin rotación propia hereda la de su padre. ("dir", v): apunta el hueso hacia v.
import bpy, math, sys
from mathutils import Vector, Matrix, Quaternion, Euler
args = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=args[0])
rig = bpy.data.objects["Conejo_Rig"]; arm = rig.data
sc = bpy.context.scene; sc.render.fps = 30
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
for pb in rig.pose.bones: pb.rotation_mode = 'QUATERNION'
ORDEN = []
def recorrer(b):
    ORDEN.append(b.name)
    for c in b.children: recorrer(c)
for b in arm.bones:
    if b.parent is None: recorrer(b)

def E(x=0, y=0, z=0): return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_quaternion()
def apuntar(hueso, d):
    rest = (arm.bones[hueso].tail_local - arm.bones[hueso].head_local).normalized()
    return rest.rotation_difference(Vector(d).normalized())

def aplicar(pose):
    """pose: {hueso: Quaternion absoluta | ('dir', v)}, '_hips': desplazamiento (x,y,z)."""
    absolutas = {}
    for n in ORDEN:
        b = arm.bones[n]
        v = pose.get(n)
        if isinstance(v, tuple) and v[0] == "dir": q = apuntar(n, v[1])
        elif isinstance(v, Quaternion): q = v
        else: q = absolutas.get(b.parent.name, Quaternion()) if b.parent else Quaternion()
        absolutas[n] = q
        pq = absolutas.get(b.parent.name, Quaternion()) if b.parent else Quaternion()
        # rotación local de Blender: en el marco del hueso en reposo, relativa a la del padre
        rel = pq.inverted() @ q
        m = b.matrix_local.to_3x3()
        local = (m.inverted() @ rel.to_matrix() @ m).to_quaternion()
        pb = rig.pose.bones[n]; pb.rotation_quaternion = local
    rig.pose.bones["Hips"].location = Vector((0, 0, 0))
    off = pose.get("_hips", (0, 0, 0))
    # location de Hips va en su marco local (Y = arriba del hueso)
    m = arm.bones["Hips"].matrix_local.to_3x3()
    rig.pose.bones["Hips"].location = m.inverted() @ Vector(off)

def clave(f):
    for pb in rig.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=f)
    rig.pose.bones["Hips"].keyframe_insert("location", frame=f)

def mezcla(*poses):
    r = {}
    for p in poses: r.update(p)
    return r

# Brazos caídos (relajados) y piernas rectas
def brazos(l=(0.45, -0.05, -1), lf=(0.32, -0.35, -1), r=(-0.45, -0.05, -1), rf=(-0.32, -0.35, -1)):
    return {"LeftUpperArm": ("dir", l), "LeftLowerArm": ("dir", lf), "RightUpperArm": ("dir", r), "RightLowerArm": ("dir", rf)}
REPOSO = brazos()
def piernas(l=0, r=0, lk=0, rk=0):
    # l/r: ángulo hacia delante (grados) del muslo; lk/rk: flexión de rodilla
    d = lambda a: (0, -math.sin(math.radians(a)), -math.cos(math.radians(a)))
    return {"LeftUpperLeg": ("dir", d(l)), "LeftLowerLeg": ("dir", d(l - lk)),
            "RightUpperLeg": ("dir", d(r)), "RightLowerLeg": ("dir", d(r - rk)),
            "LeftFoot": E(0, 0, 0), "RightFoot": E(0, 0, 0)}
def orejas(l=(0, 0), r=(0, 0), cabeza=E()):
    # (abrir hacia fuera, echar atrás) en grados
    return {"LeftEar": cabeza @ E(-r_(l[1]), l[0], 0), "RightEar": cabeza @ E(-r_(r[1]), -r[0], 0)}
def r_(x): return x

ACCIONES = {}
def accion(nombre, fotogramas, poses, ciclo=True):
    """poses: lista de (fotograma, pose). Si es ciclo, el último repite el primero."""
    act = bpy.data.actions.new(nombre); rig.animation_data_create(); rig.animation_data.action = act
    if ciclo: poses = poses + [(fotogramas, poses[0][1])]
    for f, p in poses:
        aplicar(p); clave(f)
    ACCIONES[nombre] = (act, fotogramas)
    rig.animation_data.action = None

# ── Idle: respira, orejas que se mecen ──────────────────────────────────────────────────
base = mezcla(REPOSO, piernas())
accion("Idle", 60, [
    (0,  mezcla(base, {"Chest": E(), "Head": E(), "_hips": (0, 0, 0)}, orejas((4, 0), (4, 0)))),
    (30, mezcla(base, brazos(l=(0.47, -0.05, -1), r=(-0.47, -0.05, -1)), {"Chest": E(-3), "Head": E(-4), "_hips": (0, 0, -0.012)}, orejas((10, 6), (8, 4), E(-4)))),
])
# ── Idle2: mira a un lado, una oreja se le cae y la levanta ─────────────────────────────
accion("Idle2", 90, [
    (0,  mezcla(base, orejas((4, 0), (4, 0)))),
    (18, mezcla(base, {"Head": E(0, 0, 28)}, orejas((4, 0), (4, 0), E(0, 0, 28)))),
    (40, mezcla(base, {"Head": E(-6, 0, 28)}, orejas((4, 0), (70, 10), E(-6, 0, 28)))),
    (55, mezcla(base, brazos(r=(-0.3, -0.6, 0.4), rf=(0.1, -0.2, 1)), {"Head": E(-6, 0, 20)}, orejas((4, 0), (60, 8), E(-6, 0, 20)))),
    (70, mezcla(base, {"Head": E(0, 0, 5)}, orejas((6, 0), (8, 2), E(0, 0, 5)))),
    (90, mezcla(base, orejas((4, 0), (4, 0)))),
], ciclo=False)
# ── Dance: saltitos con los brazos arriba, alternando ──────────────────────────────────
arriba_l = brazos(l=(0.5, -0.1, 0.9), lf=(0.2, -0.1, 1), r=(-0.9, -0.1, -0.6), rf=(-0.5, -0.4, -0.6))
arriba_r = brazos(l=(0.9, -0.1, -0.6), lf=(0.5, -0.4, -0.6), r=(-0.5, -0.1, 0.9), rf=(-0.2, -0.1, 1))
accion("Dance", 60, [
    (0,  mezcla(arriba_l, piernas(0, 0, 20, 20), {"_hips": (0, 0, -0.03), "Chest": E(0, 8, 0), "Head": E(0, 12, 0)}, orejas((15, 0), (15, 0), E(0, 12, 0)))),
    (8,  mezcla(arriba_l, piernas(10, -10), {"_hips": (0, 0, 0.09), "Chest": E(0, 4, 0), "Head": E(0, 6, 0)}, orejas((-5, 25), (-5, 25), E(0, 6, 0)))),
    (15, mezcla(arriba_l, piernas(0, 0, 10, 10), {"_hips": (0, 0, 0.0)}, orejas((10, 5), (10, 5)))),
    (30, mezcla(arriba_r, piernas(0, 0, 20, 20), {"_hips": (0, 0, -0.03), "Chest": E(0, -8, 0), "Head": E(0, -12, 0)}, orejas((15, 0), (15, 0), E(0, -12, 0)))),
    (38, mezcla(arriba_r, piernas(-10, 10), {"_hips": (0, 0, 0.09), "Chest": E(0, -4, 0), "Head": E(0, -6, 0)}, orejas((-5, 25), (-5, 25), E(0, -6, 0)))),
    (45, mezcla(arriba_r, piernas(0, 0, 10, 10), {"_hips": (0, 0, 0.0)}, orejas((10, 5), (10, 5)))),
])
# ── Walk / Run: paso saltarín de juguete ───────────────────────────────────────────────
def paso(f, a, rodilla, bote, inclina, bra, n):
    s = 1 if n == 0 else -1
    return (f, mezcla(
        brazos(l=(0.45, 0.45*s*bra, -1), lf=(0.35, 0.2*s*bra - 0.3, -1), r=(-0.45, -0.45*s*bra, -1), rf=(-0.35, -0.2*s*bra - 0.3, -1)),
        piernas(a*s, -a*s, rodilla if s < 0 else 5, rodilla if s > 0 else 5),
        {"_hips": (0, 0, 0), "Spine": E(inclina), "Head": E(inclina*0.5)}, orejas((6, 12*bra), (6, 12*bra), E(inclina*0.5))))
def paso_alto(f, bote, inclina, bra):
    return (f, mezcla(brazos(), piernas(0, 0, 25, 25), {"_hips": (0, 0, bote), "Spine": E(inclina), "Head": E(inclina*0.5)}, orejas((2, 20*bra), (2, 20*bra), E(inclina*0.5))))
accion("Walk", 24, [paso(0, 32, 10, 0, 4, 1, 0), paso_alto(6, 0.03, 4, 1), paso(12, 32, 10, 0, 4, 1, 1), paso_alto(18, 0.03, 4, 1)])
accion("Run", 16, [paso(0, 48, 25, 0, 14, 1.6, 0), paso_alto(4, 0.07, 14, 1.6), paso(8, 48, 25, 0, 14, 1.6, 1), paso_alto(12, 0.07, 14, 1.6)])
# ── Trompeta: se lleva la trompetilla a la boca (mano derecha) y toca mirando arriba ───────
boca = brazos(r=(0.2, -0.85, 0.45), rf=(0.35, -0.8, 0.45), l=(0.5, 0.25, -0.8), lf=(-0.4, 0.2, 0.1))
toca = lambda bote, cab: mezcla(boca, piernas(), {"_hips": (0, 0, bote), "Chest": E(-8), "Head": E(-14 + cab)}, orejas((-8, 20), (-8, 20), E(-14 + cab)))
accion("Trompeta", 75, [
    (0, mezcla(base, orejas((4, 0), (4, 0)))),
    (12, toca(0, 0)), (22, toca(0.02, -6)), (30, toca(0, 0)), (38, toca(0.02, -6)), (46, toca(0, 0)), (56, toca(0.015, -4)),
    (75, mezcla(base, orejas((4, 0), (4, 0)))),
], ciclo=False)

bpy.ops.object.mode_set(mode='OBJECT')
# Reposo en T para el FBX; cada acción a su pista NLA para que el exportador saque un clip por acción.
for n, (act, fr) in ACCIONES.items():
    act.use_fake_user = True
    act.frame_range = (0, fr)
    for fc in act.fcurves if hasattr(act, "fcurves") else []:
        for k in fc.keyframe_points: k.interpolation = 'BEZIER'
bpy.ops.wm.save_as_mainfile(filepath=args[1])
print("ACCIONES", {n: f for n, (a, f) in ACCIONES.items()})
