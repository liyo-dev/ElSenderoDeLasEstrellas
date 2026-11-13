# Mago Oscuro: diseño propio (12 nov 2026)

Dirección elegida por Raúl: **figura imponente de sombra, sin cara hasta el final**, con **ojos de luz violeta**. **Estado: previa v3; todavía no está en el juego.**

## Piezas
- `mago_oscuro.py` (Blender 5.x, bpy) → dos piezas sobre el esqueleto de RPG Tiny Hero (base.blend = `AllBodiesCloaks.fbx` importado). Argumentos: base.blend y salida.
  - `Body33_MagoOscuro`: manos y torso de Body05 con la ropa encima. Túnica hasta el suelo con el bajo ondulado y franja de luz violeta; cinto con hebilla de agujero negro; capa rota con estrellas apagadas y forro violeta; cuello alto en abanico con punta central y cresta de siete pinchos de hueso nacidos de su borde; cuernos de hombro en media luna; mangas anchas con puño de luz.
  - `AC14_VeloDeSombra` (accesorio aparte): capucha alta con la punta caída hacia atrás y gorjal que tapa la mandíbula de la cabeza del pack. **Se quita para el final.**
- Dos juegos de caras anime (estilo A, el de todos), generados con `../Caras/kit_npcs.py` (filas nuevas; iris `violeta` nuevo en `caras_lib.txt`):
  - `Caras_mago_oscuro_sombra.fbx` — **Eye300-309**, `OJO_SOMBRA=1`: ojos de luz violeta (forma afilada, algo más grandes, sin blanco ni pupila que se note) para llevar el velo.
  - `Caras_mago_oscuro.fbx` — **Eye290-299**: su cara al descubrirse (ojos afilados con iris violeta, cejas finas). Las bocas, las de Liam (Mouth20-29), como los demás NPCs con cabeza de hombre.
  Cada juego trae sus símbolos de anime (Fx2900…, Fx3000…).

## Cómo funcionan los dos estados (propuesta para Unity)
- **Con velo:** AC14 puesto, la cabeza (Head01_Male) con material negro y el juego «sombra» (sin boca). Los ojos reaccionan a las emociones como los de los demás.
- **Al final:** se quita AC14, la cabeza vuelve a su piel y se cambia al juego de la cara (con boca). El cambio es quitar una pieza y cambiar dos cosas.

## Previas
- `mago_oscuro_previa.png`: con velo (frente, tres cuartos, lado, espalda), ojos «normal». `mago_oscuro_sin_velo.png`: la cara al descubrirse (frente y tres cuartos). Hechas con `../Eldran/previa.py`-style y `../Ninos/foto.py` (`FOTO_ALTO=1.9 FOTO_CENTRO=0.88`). La luz de Workbench aclara la cabeza negra más de lo que se verá en el juego.
- `para_unity/`: `Body33_MagoOscuro.fbx`, `AC14_VeloDeSombra.fbx`, `Caras_mago_oscuro.fbx`, `Caras_mago_oscuro_sombra.fbx`. **No están en Assets.**
- Versiones anteriores (pinchos flotando; velo con esfera negra y ojos de malla fija) en `Versiones antiguas/`.

## Pendiente / sin comprobar
- **Qué cara es de verdad** (edad, rasgos, si recuerda al Archimago), **su pelo** (la previa usa la cabeza calva del pack y piel pálida solo para verla) y el tono de piel: los decide Raúl.
- En Unity: ficha de `_MAGO_OSCURO` en `VestuarioDeLosNPCs` / tabla de `CarasDeLosNPCs` (con las claves `Caras_mago_oscuro*`), el cambio de velo (quitar AC14 + material de la cabeza + cambiar de juego de caras), materiales al shader de dibujo y que los ojos de luz brillen (emisivo). Nada de esto está hecho.
- Nada se ha visto moverse: túnica y capa copian los pesos del cuerpo (la túnica esconde las piernas al levitar); el velo va a `head`; cuello y pinchos a `spine_03`/`neck_01`; la capa no usa `CloakBone`.
- Con el Archimago (hoy el modelo antiguo de Will), pendiente: diseñarlo como pareja.

Medidas útiles: la cabeza del pack va de z 0,68 a 1,31, ~0,74 de ancho, centro (0, -0,013, 0,99).
