# Victoria: vestuario propio (11 nov 2026)

Segunda pieza de la propuesta de vestuario de NPCs con peso (aprobada por Raúl). **Estado: previa hecha; todavía
no está en el juego.**

- `victoria.py` (Blender 5.x, bpy): costurera. Blusa lila con las mangas remangadas (el brazo, de piel, con el
  material del cuerpo), falda lila oscuro con vuelo, delantal crema con peto, tirantes y lazo a la espalda,
  cinta de medir al cuello, bolsillo con el acerico y sus alfileres, medias y zapatos (`Body27_Costurera`).
  El moño va aparte, como accesorio de cabeza (`AC13_Mono`, con la cinta crema), por fuera de su Hair06.
  Argumentos: base.blend (`AllBodiesCloaks.fbx` importado) y salida.
- `victoria_previa.png`: con Head02_Female y Hair06 del pack (`../Eldran/previa.py` y `../Ninos/foto.py`).
- `para_unity/`: los dos FBX (`../Prendas/exportar_prenda.py`). **No están en Assets**; van a
  `Assets/Art/Characters/NPCs/Victoria/` cuando Raúl dé el visto bueno y Unity esté libre. Hoy lleva Body04.

Sin comprobar: la falda y el delantal al andar, y el moño con el color de pelo del juego (en Unity tiene que
llevar el material de su pelo).

## Cambio del 11 nov (noche)
En el juego, los dos cabos amarillos de la cinta de medir se despegaban del delantal y colgaban por debajo del
peto (Raúl: «eso que sobresale amarillo»). Ahora van pegados a la superficie del peto, terminan encima de la
cinta del delantal y se pesan como el peto (copiados del cuerpo), así se mueven con él. La previa nueva es con los
brazos bajados y la cabeza sin pelo. Lo anterior, en `Versiones antiguas/Cinta de medir despegada (2026-11-11)/`.

## 13 nov 2026 — chequeo de choques (INC-1138)
Pesos con `../Prendas/ropa_lib.py` (`completar_zonas`): antes NO PASA, ahora PASA. Informe y fotos en `chequeo/`. FBX nuevo copiado a Assets; el anterior, en `Versiones antiguas/Antes del chequeo de choques (2026-11-13)/`.
- Cambios de forma: la media sube hasta la cadera por debajo de la falda (al correr se veía el hueco), y falda, bajo y delantal con algo más de vuelo.
