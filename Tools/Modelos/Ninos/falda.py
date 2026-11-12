# Falda de Nina menos abierta (INC-1082): con la talla de niña pequeña los brazos caídos quedaban dentro del vuelo
# de la falda y solo asomaban las manos, que parecían sueltas y se perdían contra el delantal. Por debajo del
# talle, el vuelo (lo que se abre más allá del ancho de la cintura) se queda en el 45 %.
# Se usa al final de nina.py, con la talla ya puesta, o sola: args entrada.blend salida.blend
import bpy, sys
from mathutils import Vector
if "cuerpo_nino" not in globals():
    a = sys.argv[sys.argv.index("--")+1:]
    bpy.ops.wm.open_mainfile(filepath=a[0]); SALIDA_FALDA = a[1]
else: SALIDA_FALDA = None
vestido = bpy.data.objects["Body25_Vestido"]; mwv = vestido.matrix_world; miv = mwv.inverted()
TALLE_Z, CINTURA, CY_FALDA, VUELO = 0.30, 0.16, -0.008, 0.45
de_falda = {i for p in vestido.data.polygons
            if vestido.data.materials[p.material_index].name in ("Vestido", "VestidoOscuro", "Delantal") for i in p.vertices}
movidos = 0
for i in de_falda:
    v = vestido.data.vertices[i]; p = mwv @ v.co
    if p.z >= TALLE_Z: continue
    d = Vector((p.x, p.y - CY_FALDA, 0)); r = d.length
    if r <= CINTURA: continue
    d *= (CINTURA + (r - CINTURA) * VUELO) / r
    v.co = miv @ Vector((d.x, d.y + CY_FALDA, p.z)); movidos += 1
print("FALDA", movidos, "vértices")
if SALIDA_FALDA: bpy.ops.wm.save_as_mainfile(filepath=SALIDA_FALDA)
