# Junta el cuerpo de un personaje con una cabeza/caras ya preparada, y opcionalmente su pelo, para fotografiarlos.
#   args: blend de la cara (cabeza + ojos + boca + armadura), blend del cuerpo, objetos del cuerpo a traer (coma), salida,
#         [blend del pelo, objetos del pelo (coma)]
import bpy, sys
face, cuerpo_blend, objs, salida, *pelo = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=face)
arm_cara = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
def traer(blend, nombres):
    antes = set(bpy.data.objects)
    with bpy.data.libraries.load(blend, link=False) as (d, a):
        a.objects = nombres.split(",") + ["Armature"]
    nuevos = [o for o in bpy.data.objects if o not in antes]
    for o in nuevos: bpy.context.scene.collection.objects.link(o)
    for o in nuevos:
        if o.type == 'MESH' and o.parent and o.parent.type == 'ARMATURE' and o.parent in nuevos:
            if o.parent != arm_cara: pass
    return nuevos
for blend, nombres in ([(cuerpo_blend, objs)] + ([(pelo[0], pelo[1])] if pelo else [])):
    nuevos = traer(blend, nombres)
    extra_arm = [o for o in nuevos if o.type == 'ARMATURE']
    if blend == pelo[0] if pelo else False:
        for o in nuevos:
            if o.type == 'MESH':
                o.parent = arm_cara; o.matrix_parent_inverse = arm_cara.matrix_world.inverted()
                for m in o.modifiers:
                    if m.type == 'ARMATURE': m.object = arm_cara
        for a in extra_arm: bpy.data.objects.remove(a)
bpy.ops.wm.save_as_mainfile(filepath=salida)
print("COMP", sorted(o.name for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith("Fx")))
