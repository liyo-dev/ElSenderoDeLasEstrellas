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

## 13 nov 2026 — chequeo de choques (INC-1138)
Pesos con `../Prendas/ropa_lib.py` (`completar_zonas`): antes NO PASA, ahora PASA. Informe y fotos en `chequeo/`. FBX nuevo copiado a Assets; el anterior, en `Versiones antiguas/Antes del chequeo de choques (2026-11-13)/`.
- Cambios de forma: el brazal empieza pasado el codo y copia los pesos del brazo como la manga, las correas abrazan el brazal y los remaches van en fila por fuera de la hombrera (se metían en ella y en el pañuelo).

## 14 nov 2026 — sin bolsas en el costado (INC-1183)
- Quitado el bolsa del cinturón: la mano se metía dentro al andar. Decisión de Raúl: los personajes no llevan bolsas ni mochilas.
- Lo anterior, en `Versiones antiguas/Con bolsas en el costado (2026-11-13)/`.
