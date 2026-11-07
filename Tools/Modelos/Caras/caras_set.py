# Juego de caras para un personaje: cada ojo (con sus cejas) y cada boca es una pieza del modelo
# modular (Eye<NN> / Mouth<NN>), pegada a su cabeza y pesada al hueso «head».
#   args: modular.blend (o base_adulto.blend), cabeza (Head01_Male|Head02_Female), estilo (chico|chicoL|chicaC|chicaL; L = anime),
#         color (marron|verde|azul|gris|oscuro|ambar), base de numeración de los ojos (p. ej. 20 → Eye20…), base de las bocas
#         (o 0 para no hacer bocas), salida.
#   Variables de entorno: MEJILLAS (suave|tostada|pecas|mayor, combinables con «+»), CEJAS (fina|gruesa|poblada),
#   SIMBOLOS_FX (1 por defecto: añade los fotogramas de los símbolos, Fx<ojos×10…>), OJO_FORMA (redondo|almendra|noventa|afilado|chico), EXTRAS (1/0; por defecto solo chicas anime), y la colocación de caras_lib (OJO_X, OJO_Z, BOCA_Z,
#   OJO_ESCALA, BOCA_ESCALA).
import bpy, sys, os
args = sys.argv[sys.argv.index("--")+1:]
MOD, CAB_N, ESTILO, COLOR, BASE_OJO, BASE_BOCA, SALIDA = args
lib_args = [MOD, CAB_N, "normal", "neutra", "/dev/null", ESTILO, "x", COLOR]
sys.argv = ["x", "--"] + lib_args
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "caras_lib.txt")).read())
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
OJOS = ["normal", "feliz", "triste", "enfado", "sorpresa", "miedo", "pensar", "cansado", "cerrados"]
BOCAS = ["neutra", "sonrisa", "risa", "triste", "o", "enfado", "ondulada", "picara", "entreabierta", "abierta"]
MEJ = os.environ.get("MEJILLAS", "")          # las mejillas de todos los ojos (ver mejillas() en caras_lib)
if ESTILO == "L" and os.environ.get("EXTRAS", "1" if CHICA else "0") == "1":   # los extra del anime (Estela): guiño, conjuro y vergüenza; risa con colmillo
    OJOS = OJOS + ["guino", "conjuro", "verguenza"]
    BOCAS = BOCAS + ["risa+colmillo"]
OJOS = OJOS + ["llorar"]   # siempre la última: ojos cerrados con cejas de triste (para la emoción Crying, con las lágrimas Fx)
def pieza(nombre, crear):
    antes = set(bpy.data.objects)
    crear()
    nuevos = [o for o in bpy.data.objects if o not in antes]
    for o in nuevos:
        bpy.context.view_layer.objects.active = o
        for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.select_all(action='DESELECT')
    for o in nuevos: o.select_set(True)
    bpy.context.view_layer.objects.active = nuevos[0]
    bpy.ops.object.join()
    o = bpy.context.object; o.name = nombre
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    vg = o.vertex_groups.new(name="head"); vg.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
    o.modifiers.new("Esqueleto", 'ARMATURE').object = arm
    return o
nombres = []
for i, t in enumerate(OJOS):
    def hacer(t=t):
        ojos("feliz" if t == "verguenza" else t)
        mejillas("+".join(["verguenza"] + [m for m in MEJ.split("+") if m not in ("", "suave", "tostada")]) if t == "verguenza" else MEJ)
    n = f"Eye{int(BASE_OJO) + i}"; pieza(n, hacer); nombres.append((n, "ojos " + t))
if int(BASE_BOCA):
    for i, t in enumerate(BOCAS):
        def hacer_b(t=t):
            boca(t.replace("+colmillo", ""))
            if "+colmillo" in t: colmillo()
        n = f"Mouth{int(BASE_BOCA) + i}"; pieza(n, hacer_b); nombres.append((n, "boca " + t))
# Símbolos de anime (lágrimas, sudor, vena, susto): fotogramas Fx<ojos×10 + i>, en el orden de SIMBOLOS
if os.environ.get("SIMBOLOS_FX", "1") == "1":
    i_fx = 0
    for simb, total in SIMBOLOS:
        for f in range(total):
            def hacer_s(simb=simb, f=f): simbolos(simb, f)
            n = f"Fx{int(BASE_OJO) * 10 + i_fx}"; pieza(n, hacer_s); nombres.append((n, f"{simb} {f}")); i_fx += 1
if os.environ.get("CARAS_CON_CABEZA") != "1":
    bpy.data.objects.remove(cabeza)
bpy.ops.wm.save_as_mainfile(filepath=SALIDA)
print("PIEZAS", nombres)
