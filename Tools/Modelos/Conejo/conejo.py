# Conejo heraldo de Chuchelandia: muñeco de juguete por piezas, esqueleto humanoide (nombres de Mecanim),
# piel rígida por pieza. Mira a -Y (de frente en Blender = +Z en Unity). Izquierda del personaje = +X.
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix, Euler
OUT = sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else "."
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

COLORES = {
 "Pelaje":  (0.97, 0.93, 0.88, 1),
 "Rosa":    (1.00, 0.55, 0.68, 1),
 "Ojos":    (0.08, 0.05, 0.07, 1),
 "Brillo":  (1, 1, 1, 1),
 "Tabardo": (0.85, 0.12, 0.25, 1),
 "Dorado":  (1.00, 0.78, 0.25, 1),
}
MAT = {}
for n, c in COLORES.items():
    m = bpy.data.materials.new(n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.6
    m.diffuse_color = c
    MAT[n] = m

PIEZAS = []  # (obj, hueso)
def pieza(obj, mat, hueso, nombre):
    obj.name = nombre
    obj.data.materials.append(MAT[mat])
    for p in obj.data.polygons: p.use_smooth = True
    PIEZAS.append((obj, hueso))
    return obj

def esfera(nombre, centro, escala, mat, hueso, rot=(0,0,0), seg=24, anillos=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=anillos, radius=1, location=centro, rotation=rot)
    o = bpy.context.object; o.scale = escala
    return pieza(o, mat, hueso, nombre)

def cilindro(nombre, centro, radio, alto, mat, hueso, rot=(0,0,0), radio2=None, v=24):
    bpy.ops.mesh.primitive_cone_add(vertices=v, radius1=radio, radius2=radio2 if radio2 is not None else radio, depth=alto, location=centro, rotation=rot)
    return pieza(bpy.context.object, mat, hueso, nombre)

def toro(nombre, centro, mayor, menor, mat, hueso, rot=(0,0,0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=mayor, minor_radius=menor, major_segments=32, minor_segments=10, location=centro, rotation=rot)
    return pieza(bpy.context.object, mat, hueso, nombre)

def capsula(nombre, a, b, radio, mat, hueso):
    a, b = Vector(a), Vector(b); d = b - a
    centro = (a + b) / 2
    o = esfera(nombre, centro, (radio, radio, d.length/2 + radio*0.6), mat, hueso)
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector((0,0,1)).rotation_difference(d.normalized())
    return o

R90 = math.radians(90)
for lado, s in (("Left", 1), ("Right", -1)):
    # Piernas cortas y pies grandes de conejo
    capsula(f"{lado}Muslo", (0.11*s, 0, 0.36), (0.11*s, 0, 0.24), 0.07, "Pelaje", f"{lado}UpperLeg")
    capsula(f"{lado}Pierna", (0.11*s, 0, 0.22), (0.11*s, 0, 0.10), 0.062, "Pelaje", f"{lado}LowerLeg")
    esfera(f"{lado}Pie", (0.12*s, -0.06, 0.055), (0.085, 0.16, 0.055), "Pelaje", f"{lado}Foot")
    esfera(f"{lado}Planta", (0.12*s, -0.07, 0.012), (0.07, 0.13, 0.012), "Rosa", f"{lado}Foot")
    # Brazos en T: manga roja con puño dorado y mano de manopla
    capsula(f"{lado}Manga", (0.22*s, 0, 0.72), (0.40*s, 0, 0.72), 0.062, "Tabardo", f"{lado}UpperArm")
    toro(f"{lado}Puno", (0.42*s, 0, 0.72), 0.058, 0.018, "Dorado", f"{lado}LowerArm", rot=(0, R90, 0))
    capsula(f"{lado}Antebrazo", (0.42*s, 0, 0.72), (0.55*s, 0, 0.72), 0.05, "Pelaje", f"{lado}LowerArm")
    esfera(f"{lado}Mano", (0.61*s, 0, 0.72), (0.075, 0.065, 0.07), "Pelaje", f"{lado}Hand")
    esfera(f"{lado}Pulgar", (0.60*s, -0.06, 0.74), (0.03, 0.03, 0.03), "Pelaje", f"{lado}Hand")
    # Cabeza: ojos, brillos, mofletes, colorete
    esfera(f"{lado}Ojo", (0.1*s, -0.225, 1.14), (0.048, 0.03, 0.06), "Ojos", "Head")
    esfera(f"{lado}Brillo", (0.088*s, -0.252, 1.165), (0.015, 0.008, 0.017), "Brillo", "Head")
    esfera(f"{lado}Moflete", (0.05*s, -0.215, 1.0), (0.075, 0.06, 0.06), "Pelaje", "Head")
    esfera(f"{lado}Colorete", (0.175*s, -0.19, 1.04), (0.045, 0.012, 0.03), "Rosa", "Head", rot=(0, 0, math.radians(-38*s)))
    # Orejas largas, un poco abiertas, con el interior rosa
    inclina = math.radians(-12*s)
    esfera(f"{lado}Oreja", (0.1*s, 0.01, 1.53), (0.075, 0.045, 0.27), "Pelaje", f"{lado}Ear", rot=(0, inclina, 0))
    esfera(f"{lado}OrejaDentro", (0.1*s + 0.0, -0.03, 1.53), (0.048, 0.02, 0.2), "Rosa", f"{lado}Ear", rot=(0, inclina, 0))

# Cuerpo de huevo, tabardo de heraldo con cinturón y botones dorados, colita
esfera("Cuerpo", (0, 0, 0.56), (0.25, 0.22, 0.3), "Pelaje", "Spine")
cilindro("Tabardo", (0, 0, 0.55), 0.285, 0.46, "Tabardo", "Spine", radio2=0.225)
toro("Cinturon", (0, 0, 0.47), 0.272, 0.026, "Dorado", "Hips")
esfera("Hebilla", (0, -0.29, 0.47), (0.05, 0.02, 0.04), "Dorado", "Hips")
for i, z in enumerate((0.58, 0.67)):
    esfera(f"Boton{i}", (0, -0.252 + (z-0.58)*0.35, z), (0.025, 0.012, 0.025), "Dorado", "Chest")
toro("Cuello", (0, 0, 0.79), 0.2, 0.035, "Dorado", "Chest")
esfera("Cola", (0, 0.25, 0.4), (0.085, 0.085, 0.085), "Pelaje", "Hips")
# Cabeza grande
esfera("Cabeza", (0, 0, 1.09), (0.28, 0.26, 0.26), "Pelaje", "Head", seg=32, anillos=18)
esfera("Nariz", (0, -0.27, 1.06), (0.038, 0.022, 0.028), "Rosa", "Head")
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -0.262, 0.95)); d = bpy.context.object
d.scale = (0.055, 0.02, 0.045); pieza(d, "Brillo", "Head", "Dientes")
# Gorrito de heraldo con pluma
esfera("Gorro", (0, 0.03, 1.335), (0.17, 0.16, 0.06), "Tabardo", "Head")
esfera("BotonGorro", (0, 0.03, 1.39), (0.03, 0.03, 0.025), "Dorado", "Head")
esfera("Pluma", (0.13, 0.1, 1.42), (0.03, 0.03, 0.13), "Dorado", "Head", rot=(0, math.radians(35), 0))

# ── Esqueleto (nombres de Mecanim) ────────────────────────────────────────────────────────
arm = bpy.data.armatures.new("Esqueleto"); rig = bpy.data.objects.new("Conejo_Rig", arm)
sc.collection.objects.link(rig); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
def hueso(n, a, b, padre=None):
    h = arm.edit_bones.new(n); h.head = a; h.tail = b
    if padre: h.parent = arm.edit_bones[padre]; h.use_connect = False
    return h
hueso("Hips", (0,0,0.42), (0,0,0.5))
hueso("Spine", (0,0,0.5), (0,0,0.62), "Hips")
hueso("Chest", (0,0,0.62), (0,0,0.78), "Spine")
hueso("Neck", (0,0,0.78), (0,0,0.86), "Chest")
hueso("Head", (0,0,0.86), (0,0,1.3), "Neck")
for lado, s in (("Left", 1), ("Right", -1)):
    hueso(f"{lado}Shoulder", (0.06*s,0,0.74), (0.2*s,0,0.72), "Chest")
    hueso(f"{lado}UpperArm", (0.2*s,0,0.72), (0.41*s,0,0.72), f"{lado}Shoulder")
    hueso(f"{lado}LowerArm", (0.41*s,0,0.72), (0.56*s,0,0.72), f"{lado}UpperArm")
    hueso(f"{lado}Hand", (0.56*s,0,0.72), (0.68*s,0,0.72), f"{lado}LowerArm")
    hueso(f"{lado}HandProp", (0.61*s,-0.02,0.72), (0.61*s,-0.12,0.72), f"{lado}Hand")
    hueso(f"{lado}UpperLeg", (0.11*s,0,0.4), (0.11*s,0,0.23), "Hips")
    hueso(f"{lado}LowerLeg", (0.11*s,0,0.23), (0.11*s,0,0.08), f"{lado}UpperLeg")
    hueso(f"{lado}Foot", (0.11*s,0,0.08), (0.12*s,-0.15,0.03), f"{lado}LowerLeg")
    hueso(f"{lado}Ear", (0.07*s,0,1.3), (0.13*s,0.01,1.78), "Head")
bpy.ops.object.mode_set(mode='OBJECT')

# ── Una sola malla, piel rígida por pieza ────────────────────────────────────────────────
for o, h in PIEZAS:
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    vg = o.vertex_groups.new(name=h); vg.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    o.select_set(False)
for o, _ in PIEZAS: o.select_set(True)
bpy.context.view_layer.objects.active = PIEZAS[0][0]
bpy.ops.object.join()
cuerpo = bpy.context.object; cuerpo.name = "Conejo"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
cuerpo.parent = rig
mod = cuerpo.modifiers.new("Esqueleto", 'ARMATURE'); mod.object = rig
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/Conejo_Heraldo.blend")
print("vertices", len(cuerpo.data.vertices), "materiales", [m.name for m in cuerpo.data.materials])
