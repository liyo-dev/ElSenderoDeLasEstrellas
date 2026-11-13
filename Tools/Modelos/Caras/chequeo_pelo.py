# Chequeo de un pelo sobre su cabeza: mira la cabeza desde muchos lados (16 rumbos y 3 alturas) con rayos y marca
#   rojo  = piel de la zona del pelo que se ve (calva, sienes al aire o hueco entre el borde del pelo y la cabeza); la zona
#           va por encima de una línea de nacimiento: «frente» en el centro de la frente, «lado» sobre las orejas y «nuca»
#           detrás (alturas en metros del modelo), con 1,5 cm de margen;
#   negro = el revés del pelo (se ve por dentro: sale con el tono de sombra, «cosas negras»).
#   y además mide el pelo contra sí mismo: superficies del pelo casi pegadas y mirando al mismo lado, entre sí o con la piel
#   (a menos de 2 mm; dos caras que se miran de frente están tapadas y no cuentan) se pelean en
#   la profundidad y parpadean en el juego («parpadeo», en cm² y en % del área del pelo; no pasa de 1 %).
# Lo que la cabeza enseña fuera del pelo (cara, orejas sin cubrir, nuca corta a propósito) sale en color piel y no cuenta.
# Escribe <salida>.png (las vistas en rejilla) y <salida>.md (porcentaje por vista y total); PASA si ninguna vista pasa del tope (2,5 % por defecto: el borde del nacimiento va en escalera por las caras de la cabeza y deja astillas sueltas).
#   args: modular.blend pelo.fbx salida [frente=1.17 lado=0.95 nuca=0.71] [tope=2.5] [cabeza=Head01_Male]
import bpy, sys, math, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import bmesh
import numpy as np
from PIL import Image, ImageDraw
a = sys.argv[sys.argv.index("--") + 1:]
BASE, PELO, SALIDA = a[:3]
FRENTE, LADO, NUCA = (float(x) for x in (a[3:6] if len(a) >= 6 else ("1.17", "0.95", "0.71")))
TOPE = float(a[6]) if len(a) > 6 else 2.5
CABEZA = a[7] if len(a) > 7 else "Head01_Male"
bpy.ops.wm.open_mainfile(filepath=BASE)
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.name != CABEZA]: bpy.data.objects.remove(o)
antes = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=PELO)
pelo = next(o for o in bpy.data.objects if o not in antes and o.type == 'MESH')
dg = bpy.context.evaluated_depsgraph_get()
def arbol(o):
    bm = bmesh.new(); bm.from_object(o, dg); bm.transform(o.matrix_world); bm.faces.ensure_lookup_table()
    return BVHTree.FromBMesh(bm), bm
tc, bc = arbol(bpy.data.objects[CABEZA]); tp, bp = arbol(pelo)
zs = [v.co.z for v in bc.verts]; C = Vector((0, sum(v.co.y for v in bc.verts) / len(bc.verts), (min(zs) + max(zs)) / 2))
R = max((v.co - C).length for v in bp.verts) + 0.03
def bajo_el_pelo(p):
    ang = math.atan2(-(p.y + 0.013), max(abs(p.x), 0.001))
    linea = LADO + ((FRENTE - LADO) * math.sin(ang) ** 2 if ang > 0 else (NUCA - LADO) * min(1.0, 2.0 * abs(math.sin(ang))))
    return p.z > linea + 0.015
N = 140
vistas = []
for elev in (0, 25, 55):
    for k in range(16):
        az = math.radians(22.5 * k); el = math.radians(elev)
        camara = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))   # rumbo 0: de frente; altura: desde arriba
        fwd = -camara
        der = fwd.cross(Vector((0, 0, 1))).normalized(); arr = der.cross(fwd).normalized()
        img = np.zeros((N, N, 3), np.uint8); img[:] = (60, 60, 60)
        n_pelo = n_rojo = n_negro = 0
        for i in range(N):
            for j in range(N):
                o = C + der * ((j / (N - 1) - 0.5) * 2 * R) + arr * ((0.5 - i / (N - 1)) * 2 * R) - fwd * 2.0
                hc = tc.ray_cast(o, fwd, 4.0); hp = tp.ray_cast(o, fwd, 4.0)
                if hp[0] is not None and (hc[0] is None or hp[3] <= hc[3] + 1e-5):
                    if hp[1].dot(fwd) > 0.05: img[i, j] = (10, 10, 10); n_negro += 1
                    else:
                        s = 0.55 + 0.45 * max(0.0, -hp[1].dot(fwd)); img[i, j] = (int(170 * s), int(170 * s), int(220 * s))
                    n_pelo += 1
                elif hc[0] is not None:
                    if bajo_el_pelo(hc[0]): img[i, j] = (230, 30, 30); n_rojo += 1; n_pelo += 1
                    else:
                        s = 0.6 + 0.4 * max(0.0, -hc[1].dot(fwd)); img[i, j] = (int(250 * s), int(200 * s), int(160 * s))
        vistas.append((f"rumbo {int(22.5 * k)}°, altura {elev}°", img, 100 * n_rojo / max(1, n_pelo), 100 * n_negro / max(1, n_pelo)))
cols = 8; filas = (len(vistas) + cols - 1) // cols
hoja = Image.new("RGB", (cols * N, filas * (N + 14)), (30, 30, 30)); dib = ImageDraw.Draw(hoja)
lineas = [f"# Chequeo del pelo: {os.path.basename(PELO)}", "", "| Vista | Piel bajo el pelo que se ve (%) | Revés del pelo (%) |", "|---|---|---|"]
peor = 0
for idx, (nombre, img, rojo, negro) in enumerate(vistas):
    x, y = (idx % cols) * N, (idx // cols) * (N + 14)
    hoja.paste(Image.fromarray(img), (x, y + 14)); dib.text((x + 3, y + 1), f"{nombre.split(',')[0][6:]}/{nombre.split()[-1]} r{rojo:.1f} n{negro:.1f}", fill=(255, 255, 255))
    lineas.append(f"| {nombre} | {rojo:.1f} | {negro:.1f} |"); peor = max(peor, rojo, negro)
hoja.save(SALIDA + ".png")
# Parpadeo: caras del pelo con otra cara paralela a menos de 2 mm (por delante o por detrás)
area_total = sum(f.calc_area() for f in bp.faces); area_pelea = 0.0
for f in bp.faces:
    c = f.calc_center_median(); n = f.normal
    if n.dot((c - C).normalized()) < 0.3: continue   # solo lo que mira hacia fuera (lo de debajo, contra la cabeza, no se ve)
    for d in (n, -n):
        h = tp.ray_cast(c + d * 1e-5, d, 0.002)
        if h[0] is not None and h[2] != f.index and h[3] >= 2e-5 and bp.faces[h[2]].normal.dot(n) > 0.9:
            area_pelea += f.calc_area(); break
        hc = tc.ray_cast(c + d * 1e-5, d, 0.002)   # pegado a la piel: se pelea con la cabeza
        if hc[0] is not None and hc[1].dot(n) > 0.9: area_pelea += f.calc_area(); break
pelea = 100 * area_pelea / max(area_total, 1e-9)
tot_r = sum(v[2] for v in vistas) / len(vistas); tot_n = sum(v[3] for v in vistas) / len(vistas)
veredicto = "PASA" if peor <= TOPE and pelea <= 1.0 else "NO PASA"
lineas[1:1] = ["", f"**{veredicto}** (tope {TOPE} % por vista y 1 % de parpadeo). Media: piel {tot_r:.2f} %, revés {tot_n:.2f} %; peor vista {peor:.1f} %. Parpadeo: {pelea:.2f} % del pelo ({area_pelea * 1e4:.1f} cm²).", ""]
open(SALIDA + ".md", "w", encoding="utf-8").write("\n".join(lineas) + "\n")
print("CHEQUEO_PELO", veredicto, f"piel {tot_r:.2f} revés {tot_n:.2f} peor {peor:.1f} parpadeo {pelea:.2f}")
