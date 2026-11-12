# Oliver: vestuario propio (11 nov 2026)

De la propuesta de vestuario de NPCs con peso (aprobada por Raúl). **Estado: previa hecha; todavía no está en el juego.**

- `oliver.py` (Blender 5.x, bpy) → `Body29_Aprendiz` (manos y torso de Body05 debajo). aprendiz: túnica de estudiante azul cielo algo grande (mangas anchas hasta la mano), cuello y ribetes naranjas, cordón a la cintura, zurrón cruzado con un pergamino, pantalón y botas. Su sombrero (Hat08) y sus gafas siguen siendo los del pack. Hoy lleva Body20 (`Assets/_NPCs/Oliver.prefab` y `_GrafoNarrativo/Oliver`).
  Argumentos: base.blend (`AllBodiesCloaks.fbx` importado) y salida.
- `oliver_previa.png`: frente, tres cuartos, lado y espalda (`../Eldran/previa.py` y `../Ninos/foto.py`).
- `para_unity/Body29_Aprendiz.fbx` (`../Prendas/exportar_prenda.py`). **No está en Assets**: entra cuando Raúl dé el visto
  bueno y Unity esté libre.

Sin comprobar: cómo se mueve al andar (los pesos se copian del cuerpo).

## Gorro de aprendiz (12 nov 2026, INC-1088)
`gorro.py` (args: base.blend, salida) → `Hat09_Aprendiz`: gorro azul de su túnica, punta doblada atrás, ala corta, cinta naranja, estrellita de latón delante y otra en la punta; pesado a «head». Previa: `gorro_previa.png`. Aprobado por Raúl (12 nov): en `Assets/Art/Characters/NPCs/Vestuario/Oliver/` y en su ficha de `VestuarioDeLosNPCs`, en lugar de Hat08.
