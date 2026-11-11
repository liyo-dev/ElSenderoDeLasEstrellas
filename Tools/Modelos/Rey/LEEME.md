# Rey: vestuario propio (11 nov 2026)

De la propuesta de vestuario de NPCs con peso (aprobada por Raúl). **Estado: previa hecha; todavía no está en el juego.**

- `rey.py` (Blender 5.x, bpy) → `Body30_Rey` (manos y torso de Body05 debajo). túnica larga rojo oscuro con ribete y cinturón dorados, cadena con medallón, y manto largo por la espalda con el cuello (con motas) y el borde de armiño. La corona es la del pack (`AC06_Crown01`) en lugar de la capucha (Hat10). Hoy `Assets/_NPCs/King.prefab` lleva Body17, Cloak01, Hat10 y la cabeza Head02_Female sin pelo: **qué cabeza y qué pelo lleva bajo la corona lo decide Raúl** (la previa usa Head01_Male y Hair02 gris solo para que se vea la corona).
  Argumentos: base.blend (`AllBodiesCloaks.fbx` importado) y salida.
- `rey_previa.png`: frente, tres cuartos, lado y espalda (`../Eldran/previa.py` y `../Ninos/foto.py`).
- `para_unity/Body30_Rey.fbx` (`../Prendas/exportar_prenda.py`). **No está en Assets**: entra cuando Raúl dé el visto
  bueno y Unity esté libre.

Sin comprobar: cómo se mueve al andar (los pesos se copian del cuerpo).
