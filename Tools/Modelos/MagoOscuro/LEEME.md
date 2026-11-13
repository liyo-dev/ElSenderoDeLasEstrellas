# Mago Oscuro: diseño propio (12 nov 2026)

Dirección elegida por Raúl: **figura imponente de sombra, sin cara hasta el final**, con **ojos de luz violeta**. Cara y pelo según su ficha del 12 nov. **Estado: previa v4, ya dentro del juego (INC-1131, sin probar con luz); antes de eso no estaba en el juego.**
El Mago Oscuro y el Archimago son **personajes distintos**: cada uno se diseña por separado (solo hay que cuidar que sus siluetas y paletas no se confundan).

## Piezas (`para_unity/`, no están en Assets)
- `mago_oscuro.py` (Blender 5.x, bpy; argumentos: base.blend y salida) → dos piezas sobre el esqueleto de RPG Tiny Hero:
  - `Body33_MagoOscuro`: manos y torso de Body05 con la ropa encima. Túnica hasta el suelo con bajo ondulado y franja de luz violeta; cinto con hebilla de agujero negro; capa rota con estrellas apagadas y forro violeta; cuello alto en abanico con cresta de siete pinchos; cuernos de hombro; mangas anchas con puño de luz.
  - `AC14_VeloDeSombra`: capucha alta con la punta caída hacia atrás y gorjal **bajo la barbilla** (ya no tapa la boca). **La capucha se queda puesta también al final**: la silueta es la misma en el prólogo y en el final.
- Cabeza: la del pack (`Head01_Male`). La versión con la barbilla afilada se probó y se quitó el 12 nov (`Versiones antiguas/`).
- `Hair19_MagoOscuro.fbx` (`../Caras/pelo_mago.py`): casquete negro con flequillo que cae en **dos picos sobre la frente** y un **mechón gris** en el de la derecha de la cara. Pesado a `head`. Número de pelo libre: el 19 (el 18 es de Nina).
- Caras anime estilo A, generadas con `../Caras/kit_npcs.py`:
  - `Caras_mago_oscuro_sombra.fbx` — **Eye300-309**: ojos de luz violeta, afilados, algo más grandes, **sin brillo**, sin boca. Para el prólogo (cabeza en negro).
  - `Caras_mago_oscuro.fbx` — **Eye290-299 + Mouth310-319**: ojos afilados con párpado caído e iris lila, **sin brillo blanco** («sin luz»), cejas casi negras, **ojeras violeta suaves**, **tres líneas finas de luz blanca con halo en cada sien** (guiño al altar; ponerles emisión), y bocas de línea fina: `neutra` = media sonrisa (Mouth310), `sonrisa` Mouth311, `risa` (carcajada) Mouth312, `triste` 313, `o` 314, `enfado` = línea recta de cuando deja de sonreír (Mouth315), `ondulada` 316, `picara` 317, `entreabierta` 318, `abierta` 319.
  Cada juego trae sus símbolos de anime (Fx2900…, Fx3000…).
- Piel de la cara: gris lavanda muy pálido casi blanco y mate, **(0.86, 0.83, 0.90)** en las previas; nariz con su puntito. La piel y el negro de la cabeza se cambian por material en Unity.

## Cómo funcionan los dos estados (propuesta para Unity)
- **Prólogo / sueño:** AC14 puesto, cabeza con material negro, juego «sombra» (Eye300-309, sin boca) y, si se quiere, `Hair19` (el mechón gris asoma sobre la cara en sombra y lo hace «persona»).
- **Final:** AC14 sigue puesto; cabeza con la piel pálida, juego `Caras_mago_oscuro` y `Hair19`. Cambia la cara, no la silueta.

## Previas
- `mago_oscuro_previa.png` (prólogo, 4 vistas), `mago_oscuro_final.png` (cara a la vista, 4 vistas) y `mago_oscuro_caras.png` (prólogo, media sonrisa, carcajada, deja de sonreír). Hechas con Workbench (`FOTO_ALTO=1.95 FOTO_CENTRO=0.88`); la luz de Workbench aclara la cabeza negra más de lo que se verá en el juego y no muestra emisión.
- Versiones anteriores en `Versiones antiguas/` (v1 pinchos flotando, v2 esfera negra, v3 sin pelo ni boca fina).

## Pendiente / sin comprobar
- Unity (INC-1131): hecho. Ficha en `VestuarioDeLosNPCs` (FBX en `Assets/Art/Characters/NPCs/Vestuario/MagoOscuro/`), dos prefabs (`_MAGO_OSCURO` = sombra con cabeza «sombra» y ojos de luz Eye300, el del prólogo; `_MAGO_OSCURO_REVELADO` = cabeza «lavanda», Hair19 y la cara del final Eye290/Mouth310, creado por `MagoOscuroPorEstados`), juegos `mago_oscuro` y `mago_oscuro_sombra` en `CarasDeLosNPCs`. Con brillo (emisión) los ojos de luz, las líneas de la Piedra y la franja de luz del cuerpo. Materiales en el shader de dibujo (`El Sendero/Dibujo`). El laboratorio de la batalla final usa el revelado. Falta verlo con luz y enchufar el revelado en las escenas finales que se monten (hoy solo el laboratorio).
- **Barbilla afilada = cabeza propia**: si el juego no admite cambiar la malla de la cabeza de un NPC, se queda la del pack (las caras valen igual).
- El pelo es un casquete bajo la capucha: con la capucha puesta solo se ve el flequillo. No se ha visto moverse (pelo y cabeza van a `head`).
- Archimago (hoy el modelo antiguo de Will): diseñarlo aparte, con su propia cara.

Medidas útiles: la cabeza del pack va de z 0,68 a 1,31, ~0,74 de ancho, centro (0, -0,013, 0,99).

## 13 nov 2026 — fallos al andar (vídeo del prólogo) y chequeo de choques
- En el vídeo `prologo_vfx_nuevos.mp4`:
  - el bajo de la túnica se inclinaba entero hacia una pierna, como un plato;
  - los puños de luz bailaban;
  - bajo la capucha se veía una boca roja y una franja clara en la frente.
- Túnica y capa (`mago_oscuro.py` con `../Prendas/ropa_lib.py`):
  - tronco y `Faldon` como el Archimago;
  - la capa solo cubre la espalda (de 28° a 152°; antes llegaba a los costados y los brazos la atravesaban);
  - las estrellas de la capa copian los pesos de la capa (`Zona(sobre=capa)`);
  - la hebilla va rígida a la pelvis;
  - puños ceñidos.
  - Chequeo: antes NO PASA (3 graves); ahora PASA con avisos menores. Detalle en `chequeo/` y `mago_oscuro_antes_despues.png`.
- Boca en la sombra: desde la v4 el gorjal baja a la barbilla y el juego de caras «sombra» usaba las bocas de la cara del final, así que se veían.
  - `Caras_mago_oscuro_sombra.fbx` lleva ahora sus propias bocas Mouth310-319 (`../Caras/bocas_de_sombra.py`) con materiales `BocaSombra` y `LenguaSombra`.
  - `CarasDeLosNPCs` las pinta del color de la sombra: el sistema de emociones sigue moviéndolas, pero no se ven.
- Franja clara de la frente: era la coronilla de la cabeza del pack, pintada en gris (90,90,90) en la textura para ir bajo el pelo. `TonosDePiel` tiene ahora `CueroPorTono`, y el tono «sombra» también pinta la coronilla.
