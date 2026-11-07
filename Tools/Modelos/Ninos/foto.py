# Foto de comprobación (Workbench, colores de los materiales; la piel del pack sale de color liso).
#   args: blend, salida.png, piezas visibles separadas por comas (vacío = todas), vistas (frente,tres,lado,espalda,tresizq)
#   Entorno: FOTO_ALTO (alto del encuadre en m), FOTO_CENTRO (altura del centro), FOTO_W / FOTO_H (píxeles por vista).
import bpy, sys, math, os
from mathutils import Vector
from PIL import Image
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
vis = [x for x in a[2].split(",") if x] if len(a) > 2 else []
vistas = a[3].split(",") if len(a) > 3 and a[3] else ["frente", "tres"]
sc = bpy.context.scene
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_render = bool(vis) and o.name not in vis
for m in bpy.data.materials:
    nodos = m.node_tree.nodes if m.use_nodes and m.node_tree else []
    if any(n.type == 'TEX_IMAGE' for n in nodos): m.diffuse_color = (1.0, 0.82, 0.7, 1)   # la textura del pack no se carga aquí
    elif "Principled BSDF" in nodos: m.diffuse_color = nodos["Principled BSDF"].inputs["Base Color"].default_value
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
sc.world = bpy.data.worlds.new("w")
bpy.context.view_layer.update()
pts = [o.matrix_world @ Vector(c) for o in bpy.data.objects if o.type == 'MESH' and not o.hide_render for c in o.bound_box]
alto = float(os.environ["FOTO_ALTO"]) if os.environ.get("FOTO_ALTO") else max(1.0, max(p.z for p in pts) + 0.08)
centro = float(os.environ.get("FOTO_CENTRO", alto * 0.5))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = alto * 1.08
sc.render.resolution_x = int(os.environ.get("FOTO_W", 520)); sc.render.resolution_y = int(os.environ.get("FOTO_H", 760))
ANG = {"frente": 0, "tres": 35, "lado": 90, "espalda": 180, "tresizq": -35}
tiles = []
for v in vistas:
    t = math.radians(ANG[v])
    cam.location = (6 * math.sin(t), -6 * math.cos(t), centro); cam.rotation_euler = (math.radians(90), 0, t)
    sc.render.filepath = a[1] + f"_{v}.png"
    bpy.ops.render.render(write_still=True); tiles.append(sc.render.filepath)
ims = [Image.open(p) for p in tiles]
out = Image.new("RGB", (sum(i.width for i in ims), ims[0].height))
x = 0
for im in ims: out.paste(im, (x, 0)); x += im.width
out.save(a[1])
for p in tiles: os.remove(p)
print("FOTO", a[1])
