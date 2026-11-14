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

## 13 nov 2026 — chequeo de choques (INC-1138)
Pesos con `../Prendas/ropa_lib.py` (`completar_zonas`): antes NO PASA, ahora PASA. Informe y fotos en `chequeo/`. FBX nuevo copiado a Assets; el anterior, en `Versiones antiguas/Antes del chequeo de choques (2026-11-13)/`.
- Cambio: el cuello copia los pesos de la túnica y es algo más estrecho abajo (se separaba de ella al bajar los brazos).

## 13 nov 2026 — correa, botas y manos (capturas de Raúl)
- La correa del zurrón se metía en el modelo junto al cuello: eran dos medias correas que acababan sobre el arranque
  del brazo. Ahora es `T.bandolera` (una sola, por el hombro derecho junto al cuello, apoyada en la túnica).
- Las botas se perdían: la pernera asomaba por fuera de la bota al andar. Ahora `T.pernera` (metida en la bota) y
  `T.bota` con vuelta de cuero más claro.
- Mangas con `T.manga_con_puno` (recogidas en la muñeca, puño naranja ceñido): dejan ver la mano y ya no se meten en la
  tripa (el puño rígido era grave en el chequeo).
- Por dentro de la túnica, el corte del cuerpo se tapa con el color del pantalón (`quitar_lo_tapado`).
- Chequeos: choques PASA (antes NO PASA, 5 graves); huecos PASA; manos 71 % / 83 %. Comparación en
  `oliver_correa_botas_manos_antes_despues.png`. Lo anterior, en `Versiones antiguas/Antes del taller (2026-11-13)/`.

## 14 nov 2026 — sin bolsas en el costado (INC-1183)
- Quitado el zurrón con el pergamino y su correa: la mano se metía dentro al andar. Decisión de Raúl: los personajes no llevan bolsas ni mochilas.
- Lo anterior, en `Versiones antiguas/Con bolsas en el costado (2026-11-13)/`.
