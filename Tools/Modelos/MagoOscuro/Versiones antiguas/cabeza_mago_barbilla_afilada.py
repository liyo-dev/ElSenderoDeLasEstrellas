# Cabeza del Mago Oscuro (Head01_MagoOscuro): la Head01_Male del pack con la barbilla más afilada (BARBILLA, 0.2 por defecto).
#   args: modular.blend, salida.blend
import bpy, sys, os
a = sys.argv[sys.argv.index("--") + 1:]
os.environ.setdefault("BARBILLA", "0.2")
sys.argv = ["x", "--", a[0], "Head01_Male", "normal", "neutra", "/dev/null", "chicoL", "x", "violeta"]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "caras_lib.txt")).read())
cabeza.name = "Head01_MagoOscuro"; cabeza.data.name = "Head01_MagoOscuro"
# La cabeza del pack va emparentada al hueso «head» (sin pesos): se deja así, igual que la original.
bpy.ops.wm.save_as_mainfile(filepath=a[1])
print("CABEZA", len(cabeza.data.vertices), [g.name for g in cabeza.vertex_groups][:6], [m.name for m in cabeza.data.materials])
