# Posadero: vestuario propio (11 nov 2026)

De la propuesta de vestuario de NPCs con peso (aprobada por Raúl). **Estado: previa hecha; todavía no está en el juego.**

- `posadero.py` (Blender 5.x, bpy) → `Body31_Posadero` (manos y torso de Body05 debajo). camisa blanca con las mangas remangadas (el brazo, de piel, con el material del cuerpo), chaleco verde azulado abierto, delantal blanco de cintura con el nudo a un lado, paño con una raya al hombro izquierdo, pantalón y zapatos. Pelo y bigote, los suyos. Hoy `Assets/_NPCs/AldeaDelMar/Posadero.prefab` lleva Body05.
  Argumentos: base.blend (`AllBodiesCloaks.fbx` importado) y salida.
- `posadero_previa.png`: frente, tres cuartos, lado y espalda (`../Eldran/previa.py` y `../Ninos/foto.py`).
- `para_unity/Body31_Posadero.fbx` (`../Prendas/exportar_prenda.py`). **No está en Assets**: entra cuando Raúl dé el visto
  bueno y Unity esté libre.

Sin comprobar: cómo se mueve al andar (los pesos se copian del cuerpo).
