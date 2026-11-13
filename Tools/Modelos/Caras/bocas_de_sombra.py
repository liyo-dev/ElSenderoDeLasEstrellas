# Añade a un juego de caras «de sombra» (ojos de luz sobre una cabeza en negro) las bocas de otro juego, con sus
# materiales renombrados (BocaSombra, LenguaSombra) para que en Unity se pinten del color de la sombra y no se vean.
# Así el sistema de emociones sigue teniendo bocas que cambiar al hablar, pero la figura no enseña boca.
# (13 nov 2026: en el prólogo al Mago Oscuro se le veía la boca roja de su cara del final.)
#   args: caras_sombra.fbx  caras_con_bocas.fbx  salida.blend
import bpy, sys
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a[0])
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
antes = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=a[1])
nuevos = [o for o in bpy.data.objects if o not in antes]
renombre = {}
for o in nuevos:
    if o.type == 'MESH' and o.name.startswith("Mouth"):
        mw = o.matrix_world.copy(); o.parent = arm; o.matrix_world = mw
        for m in o.modifiers:
            if m.type == 'ARMATURE': m.object = arm
        for i, m in enumerate(o.data.materials):
            base = m.name.split(".")[0]
            if base not in renombre:
                c = m.copy(); c.name = base + "Sombra"; renombre[base] = c
            o.data.materials[i] = renombre[base]
for o in nuevos:
    if not (o.type == 'MESH' and o.name.startswith("Mouth")): bpy.data.objects.remove(o)
bpy.ops.wm.save_as_mainfile(filepath=a[2])
print("SOMBRA", sorted(o.name for o in bpy.data.objects if o.type == 'MESH'), sorted(renombre[k].name for k in renombre))
