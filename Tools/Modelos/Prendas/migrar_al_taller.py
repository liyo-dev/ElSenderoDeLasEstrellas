# Uso: python migrar_al_taller.py personaje.py ColorDeLaTapa   (el color, uno de los COLORES del personaje)
# Pasa un script de personaje al taller: quita sus copias de las funciones de modelado y del montaje de
# materiales, y cambia el borrado del cuerpo tapado por T.quitar_lo_tapado (tapa los cortes).
import ast, sys, re
ruta, tapa = sys.argv[1], sys.argv[2]
s = open(ruta, encoding='utf-8').read()
lineas = s.split('\n')
arbol = ast.parse(s)
quitar = set(); fin_defs = 0
NOMBRES = {"nueva", "transformar", "esfera", "toro", "torno", "tubo", "caja"}
for n in arbol.body:
    if isinstance(n, ast.FunctionDef) and n.name in NOMBRES:
        ini = n.lineno - 1 - len(n.decorator_list)
        quitar.update(range(ini, n.end_lineno)); fin_defs = max(fin_defs, n.end_lineno)
    if isinstance(n, ast.Assign) and any(getattr(t, 'id', '') == 'MAT' for t in n.targets) and isinstance(n.value, ast.Dict):
        quitar.add(n.lineno - 1); fin_defs = max(fin_defs, n.end_lineno)
    if isinstance(n, ast.For) and 'MAT[' in ast.get_source_segment(s, n):
        quitar.update(range(n.lineno - 1, n.end_lineno)); fin_defs = max(fin_defs, n.end_lineno)
    if isinstance(n, ast.Assign) and any(getattr(t, 'id', '') == 'GRUPOS' for t in n.targets):
        quitar.add(n.lineno - 1); fin_defs = max(fin_defs, n.end_lineno); grupos = ast.literal_eval(n.value)
puesta = (f'T = tl.Taller(COLORES, grupos={tuple(grupos)!r})   # funciones de modelado comunes (../Prendas/taller.py)\n'
          'MAT, GRUPOS = T.mat, T.grupos\n'
          'nueva, transformar, esfera, toro, torno, tubo, caja = T.nueva, T.transformar, T.esfera, T.toro, T.torno, T.tubo, T.caja')
salida = []
for i, l in enumerate(lineas):
    if i in quitar:
        if i == fin_defs - 1: salida.append(puesta)
        continue
    salida.append(l)
s = '\n'.join(salida)
s = s.replace('import ropa_lib as rl\n', 'import ropa_lib as rl, taller as tl\n', 1)
viejo = re.search(r"bm = bmesh\.new\(\); bm\.from_mesh\(cuerpo\.data\)\nmw = cuerpo\.matrix_world\nfuera = \[v for v in bm\.verts if (.*?)\]\nbmesh\.ops\.delete\(bm, geom=fuera, context='VERTS'\)\nbm\.to_mesh\(cuerpo\.data\); bm\.free\(\)", s)
cond = viejo.group(1).replace("(mw @ v.co)", "p")
s = s.replace(viejo.group(0), f'# Los cortes se tapan con «{tapa}»: es lo que se ve desde abajo por el bajo de la ropa (ver chequeo_huecos.py).\n'
              f'T.quitar_lo_tapado(cuerpo, lambda p: {cond}, "{tapa}")')
assert T_ok if (T_ok := 'T = tl.Taller' in s) else False
open(ruta, 'w', encoding='utf-8').write(s)
print(ruta, "ok", len(quitar), "líneas quitadas")
