# Boticaria: vestuario propio (11 nov 2026)

De la propuesta de vestuario de NPCs con peso (aprobada por Raúl). **Estado: previa hecha; todavía no está en el juego.**

- `boticaria.py` (Blender 5.x, bpy) → `Body32_Herborista` (manos y torso de Body05 debajo). herborista: vestido verde con vuelo, delantal ámbar con peto y un bolsillo del que asoman ramitas, cinturón con dos bolsitas y un frasco colgado, pañoleta crema al cuello con el pico atrás, medias y zapatos. Hoy `Assets/_NPCs/_GrafoNarrativo/Boticaria.prefab` lleva Body10.
  Argumentos: base.blend (`AllBodiesCloaks.fbx` importado) y salida.
- `boticaria_previa.png`: frente, tres cuartos, lado y espalda (`../Eldran/previa.py` y `../Ninos/foto.py`).
- `para_unity/Body32_Herborista.fbx` (`../Prendas/exportar_prenda.py`). **No está en Assets**: entra cuando Raúl dé el visto
  bueno y Unity esté libre.

Sin comprobar: cómo se mueve al andar (los pesos se copian del cuerpo).

## Pañuelo verde salvia (12 nov 2026, INC-1111)
`panuelo.py` (args: base.blend, Hair09.fbx, salida) → `AC14_Panuelo`: copia la diadema de Hair09 hacia fuera (la tapa), nudo con dos orejas donde estaba la piedra y una ramita al lado; pesado a «head». Previa: `panuelo_previa.png`. Ya está en `Assets/Art/Characters/NPCs/Vestuario/Boticaria/` y en la ficha de `VestuarioDeLosNPCs`.

## 13 nov 2026 — chequeo de choques (INC-1138)
Pesos con `../Prendas/ropa_lib.py` (`completar_zonas`): antes NO PASA, ahora PASA. Informe y fotos en `chequeo/`. FBX nuevo copiado a Assets; el anterior, en `Versiones antiguas/Antes del chequeo de choques (2026-11-13)/`.
