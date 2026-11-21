# Un pelo del pack (HeadParts/HairNN.fbx) como pelo propio de un personaje, en otro color: misma forma, sin las cejas que
# trae dentro (las cejas van en las caras nuevas), con dos materiales lisos (lo de arriba algo más claro, como hace will.py
# con su Hair01), grosor por dentro (desde abajo no se ve a través; ver ../Prendas/chequeo_huecos.py), pesado a «head» y
# con el esqueleto de modular.blend. Hecho para Eldran joven (19 nov 2026, INC-1338: Hair13 de Eldran, en castaño oscuro).
#   args: modular.blend, HairNN.fbx, salida.blend
#   Entorno: NOMBRE_PELO (Hair23), PELO_COLOR / PELO_COLOR_CLARO ("r,g,b"), MAT_PELO / MAT_PELO_CLARO (nombres de los
#            materiales), PELO_GROSOR (0.006 m), SOLO_PELO=1 (quita la cabeza del .blend para exportar)
import bpy, bmesh, sys, os
from mathutils import Matrix
a = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name != "Head01_Male": bpy.data.objects.remove(o)
def mat(n, c):
    m = bpy.data.materials.new(n); m.use_nodes = True; m.diffuse_color = c
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = c; return m
col = lambda k, d: tuple(float(x) for x in os.environ.get(k, d).split(",")) + (1,)
M_PELO = mat(os.environ.get("MAT_PELO", "Pelo"), col("PELO_COLOR", "0.24,0.15,0.09"))
M_CLARO = mat(os.environ.get("MAT_PELO_CLARO", "PeloClaro"), col("PELO_COLOR_CLARO", "0.33,0.21,0.13"))
antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=a[1]); bpy.context.view_layer.update()
nuevos = [o for o in bpy.data.objects if o not in antes]
pelo = [o for o in nuevos if o.type == 'MESH'][0]
for o in nuevos:
    if o is not pelo: bpy.data.objects.remove(o)
mw = pelo.matrix_world.copy(); pelo.parent = None; pelo.data.transform(mw); pelo.matrix_world = Matrix.Identity(4)
pelo.data.transform(Matrix.Translation((0, -0.0129, 0.914)))   # las piezas sueltas de cabeza vienen sin el sitio del esqueleto
# fuera las cejas (islas pequeñas delante de la frente), como en will.py
bm = bmesh.new(); bm.from_mesh(pelo.data); bm.verts.ensure_lookup_table(); vistos = set(); quitar = []
for v0 in bm.verts:
    if v0.index in vistos: continue
    isla = [v0]; vistos.add(v0.index); k = 0
    while k < len(isla):
        for e in isla[k].link_edges:
            w = e.other_vert(isla[k])
            if w.index not in vistos: vistos.add(w.index); isla.append(w)
        k += 1
    if len(isla) < 60 and max(v.co.y for v in isla) < -0.2: quitar += isla
bmesh.ops.delete(bm, geom=quitar, context='VERTS'); bm.to_mesh(pelo.data); bm.free()
print("CEJAS FUERA", len(quitar))
pelo.data.materials.clear(); pelo.data.materials.append(M_PELO); pelo.data.materials.append(M_CLARO)
for f in pelo.data.polygons: f.material_index = 1 if f.normal.z > 0.55 else 0
for g in list(pelo.vertex_groups): pelo.vertex_groups.remove(g)
s = pelo.modifiers.new("Grosor", 'SOLIDIFY'); s.thickness = float(os.environ.get("PELO_GROSOR", "0.006")); s.offset = -1; s.use_rim = True
bpy.context.view_layer.objects.active = pelo; bpy.ops.object.modifier_apply(modifier="Grosor")
pelo.name = os.environ.get("NOMBRE_PELO", "Hair23")
vg = pelo.vertex_groups.new(name="head"); vg.add(list(range(len(pelo.data.vertices))), 1.0, 'REPLACE')
pelo.parent = arm; pelo.matrix_parent_inverse = arm.matrix_world.inverted()
pelo.modifiers.new("Esqueleto", 'ARMATURE').object = arm
if os.environ.get("SOLO_PELO") == "1": bpy.data.objects.remove(bpy.data.objects["Head01_Male"])
bpy.ops.wm.save_as_mainfile(filepath=a[2])
print("PELO", pelo.name, len(pelo.data.vertices), [m.name for m in pelo.data.materials])
