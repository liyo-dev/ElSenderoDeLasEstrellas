# Erika: vestuario propio (11 nov 2026)

Tercera pieza de la propuesta de vestuario de NPCs con peso (aprobada por Raúl). **Estado: previa hecha; todavía
no está en el juego.**

- `erika.py` (Blender 5.x, bpy): guerrera ligera. Camisa verde, peto de cuero con correas, brazales, una
  hombrera con remaches en el hombro izquierdo, cinturón ancho con bolsa, capa corta solo por la espalda,
  pañuelo azul claro al cuello con el pico por delante (el que luego lleva Will), pantalón y botas altas con
  vuelta (`Body28_GuerreraLigera`). Argumentos: base.blend (`AllBodiesCloaks.fbx` importado) y salida.
- `erika_previa.png`: con Head02_Female y Hair07 del pack (`../Eldran/previa.py` y `../Ninos/foto.py`).
- `para_unity/Body28_GuerreraLigera.fbx` (`../Prendas/exportar_prenda.py`). **No está en Assets**; va a
  `Assets/Art/Characters/NPCs/Erika/` cuando Raúl dé el visto bueno y Unity esté libre. Hoy
  (`Assets/_NPCs/Combat/Erika.prefab`) lleva Body08.

La capa corta va pesada a la espalda (no a los huesos de capa): no ondea. Sin comprobar: cómo se mueve en
combate, que es donde más se la ve.
