# Eldran: vestuario propio (11 nov 2026)

Primera pieza de la propuesta de vestuario de NPCs con peso (`claude/propuesta-vestuario-npcs-con-peso-2026-11-11.md`,
aprobada por Raúl). **Estado: previa hecha; todavía no está en el juego.**

- `eldran.py` (Blender 5.x, módulo bpy): abrigo largo de mago viejo, verde musgo apagado, hasta la rodilla y con
  vuelo; cuello vuelto, solapa y botones de latón, cinturón, bufanda gris con un cabo a la
  espalda, coderas de cuero, zurrón cruzado con dos libros, pantalón y botas (`Body26_AbrigoDeMago`: manos y
  torso de Body05 debajo). Argumentos: base.blend (`AllBodiesCloaks.fbx` importado) y salida.
  Las funciones de modelado son las de `../Liam/liam.py`.
- `previa.py`: junta el traje con la cabeza y el pelo del pack (Head05_Santa, Hair13) para la foto.
  Argumentos: blend del traje, salida, piel r,g,b, pelo r,g,b y los FBX de cabeza y pelo.
- `eldran_previa.png`: frente, tres cuartos, lado y espalda (`../Ninos/foto.py`). La cara no lleva los ojos ni
  la boca del juego: van aparte.
- `para_unity/Body26_AbrigoDeMago.fbx`: exportado con `../Prendas/exportar_prenda.py`. **No está en Assets**:
  se copia a `Assets/Art/Characters/NPCs/Eldran/` cuando Raúl dé el visto bueno y Unity esté libre, y entonces
  falta el menú que se lo pone a `Assets/_NPCs/Eldran.prefab` y `_GrafoNarrativo/Eldran` (hoy llevan Body12),
  con sus materiales en el shader de dibujo.

Sin tocar: cabeza, barba, ojos y pelo. El «pelo con el suavizado de Will» de la propuesta queda pendiente.
Sin comprobar: cómo se mueve el abrigo al andar (los pesos se copian del cuerpo) y si el zurrón roza el brazo.

## Cambio del 11 nov (noche)
En el juego, el cabo de la bufanda que colgaba por delante salía por debajo de la barba de Head05_Santa y la
atravesaba (Raúl: «tiene una cosa gris que le atraviesa»). Se quita ese cabo y su fleco; quedan la vuelta al
cuello (tapada por la barba) y el cabo de la espalda. La previa nueva es con los brazos bajados y la cabeza sin
pelo. Lo anterior, en `Versiones antiguas/Bufanda con cabo delante (2026-11-11)/`.

## 13 nov 2026 — chequeo de choques (INC-1138)
Pesos con `../Prendas/ropa_lib.py` (`completar_zonas`): antes NO PASA, ahora PASA. Informe y fotos en `chequeo/`. FBX nuevo copiado a Assets; el anterior, en `Versiones antiguas/Antes del chequeo de choques (2026-11-13)/`.

## 14 nov 2026 — sin bolsas en el costado (INC-1183)
- Quitado el zurrón con los dos libros y su correa: la mano se metía dentro al andar. Decisión de Raúl: los personajes no llevan bolsas ni mochilas.
- Lo anterior, en `Versiones antiguas/Con bolsas en el costado (2026-11-13)/`.

## 19 nov 2026 — Eldran joven y hebilla rígida (INC-1338)
- **Eldran joven**, para el recuerdo en blanco y negro de «La mañana después» (antes salía el Eldran de ahora, con pelo blanco y
  bigote): el mismo abrigo (`Body26_AbrigoDeMago`), la cabeza del pack sin barba (`Head01_Male` en vez de `Head05_Santa`), su pelo
  de pinchos (Hair13) en castaño oscuro (`para_unity/Hair23_EldranJoven.fbx`, hecho con `../Caras/pelo_del_pack.py`, más
  `../Pelo/nuca.py`) y los ojos ámbar del kit sin las marcas de la edad (`h_almendra_ambar`, juego `eldran_joven`).
  Previa: `eldran_joven_previa.png`. Chequeos en `chequeo_joven/`: choques y huecos PASA (con `--solo Body26_AbrigoDeMago` y
  el pelo como pieza); pelo PASA con la línea de los lados por encima de la oreja (`1.19 1.10 0.82`: lleva las orejas a la vista).
- **Hebilla**: en el barrido del 19 nov daba un grave (la hebilla se rasgaba al echar el tronco atrás). Regla nueva de
  `../Prendas/ropa_lib.py`: las piezas pequeñas de tronco (adornos de menos de 8 cm: hebillas, botones) van rígidas. `eldran.py`
  vuelto a pasar: choques PASA. El `Body26_AbrigoDeMago.fbx` nuevo vale para los dos Eldran.
- Lo anterior, en `Versiones antiguas/Hebilla que se rasgaba (2026-11-19)/`.
- En Unity (19 nov, 22:15): `Hair23_EldranJoven.fbx` en `Assets/Art/Characters/NPCs/Vestuario/EldranJoven/`, prefab `Assets/_NPCs/Recuerdos/EldranJoven.prefab` (copia de `Eldran.prefab`); el `Body26` nuevo, ya en `Vestuario/Eldran/` (el viejo, en `Versiones antiguas/` de la raíz). Reparto del recuerdo cambiado y rehorneado.

## 20 nov 2026 — solapa hasta el cinturón (INC-1376)
- En la cinemática «La mañana después», Eldran sentado en el filo de la cama con la mano en el regazo: la solapa (tira
  oscura del cuello al bajo) y el botón de abajo se leían como un palo en la mano.
- La solapa acaba ahora bajo el cinturón (`SOLAPA_BAJO`); los tres botones van por encima de él (z 0,65 / 0,585 / 0,515).
  La solapa, los botones y el bajo copian los pesos del abrigo (`rl.Zona(sobre=abrigo)`; los botones, además, rígidos).
- Sentado (sentarse y hablar de Kevin Iglesias pasados al esqueleto), la solapa se separaba del abrigo 10 mm y el botón de
  abajo 15 mm; ahora, 2 mm y 1 mm. Foto: `chequeo/sentado_antes_y_ahora.png`.
- Choques PASA (25 avisos menores, con la pose nueva `sentado_hablando`) y huecos PASA. Eldran joven (`chequeo_joven/`,
  mismo abrigo con su pelo): PASA (27 avisos; solo el informe, sin fotos).
- Al volver a pasar el script entra también el ceñido del cuello sobre el hombro de `ropa_lib` (`HOMBRO_X`, 19 nov), que
  el FBX anterior no tenía: el cuello vuelto es un poco más estrecho a los lados.
- `Body26_AbrigoDeMago.fbx` nuevo en `para_unity/` y en `Assets/Art/Characters/NPCs/Vestuario/Eldran/` (vale para los
  dos Eldran). Lo anterior, en `Versiones antiguas/Solapa hasta el bajo (2026-11-20)/` (con las fotos del fallo) y el
  FBX de Unity, en `Versiones antiguas/` de la raíz.
