# Silas joven: diseño propio (19 nov 2026, INC-1338)

Para el recuerdo en blanco y negro de «La mañana después» (`Cap2_02_MananaDespues`: Selene, Eldran y Silas jóvenes,
GDD § 5.11). Antes salía el mago barbudo con sombrero del pack (`_NPCs/MagoPrimero.prefab`). El GDD solo dice de Silas que es
hechicero, amigo de juventud de Eldran y dueño de una colección de relojes que no miden el tiempo igual; el diseño parte de ahí.

## Piezas
- `silas.py` (bpy; argumentos: base.blend y salida) → `Body36_Relojero`: manos y torso de Body05 con la ropa encima. Camisa clara de
  manga larga recogida en la muñeca con puño de cuero claro (`Taller.manga_con_puno`), chaleco ciruela abierto por delante con
  botones de latón, **reloj de bolsillo** por delante (no en el costado) con la cadena hasta un botón, pañuelo azul claro al cuello,
  trabilla con hebilla en la espalda, pantalón gris pizarra y botas altas con la vuelta clara (`Taller.pernera`/`bota`).
- `../Caras/pelo_silas.py` (args: modular.blend, salida; `SOLO_PELO=1`) → `Hair22_Silas`: castaño cobrizo, con volumen arriba y
  mechones peinados hacia atrás, raya a su derecha y flequillo que barre la frente hacia su izquierda; tapa media oreja y llega a
  la nuca. Receta de `pelo_liora.py` (casquete liso a 4,5 cm con faldón, mechones con grosor y por encima del casquete).
- Cabeza del pack `Head01_Male`; cara del kit `h_almendra_verde` (Eye120…, juego `silas_joven` en `CarasDeLosNPCs`, cejas cobre).
- Paleta pensada por **valores** (el recuerdo va en blanco y negro): camisa clara, chaleco oscuro, pantalón medio y sin abrigo
  largo, para que no se confunda con Eldran joven (abrigo largo cerrado, pelo oscuro de pinchos). Ver `recuerdo_eldran_y_silas_bn.png`.
- `para_unity/`: `Body36_Relojero.fbx` y `Hair22_Silas.fbx` (`../Prendas/exportar_prenda.py`).

## Chequeos (`chequeo/`)
- Choques (`--solo Body36_Relojero --piezas Hair22_Silas.fbx`): PASA. La primera versión daba 1 grave: la esfera del reloj
  pegada a su caja (tela sobre tela a menos de 3 mm, parpadea); ahora sobresale 5 mm como un cristal.
- Huecos: PASA. Pelo (`../Caras/chequeo_pelo.py`): PASA.

## Previa
`silas_joven_previa.png` (frente, lado y espalda; abajo, en blanco y negro) y `recuerdo_eldran_y_silas_bn.png`.

## En Unity (19 nov 2026, 22:15)
FBX en `Assets/Art/Characters/NPCs/Vestuario/SilasJoven/`, prefab `Assets/_NPCs/Recuerdos/SilasJoven.prefab` (copia de `MagoPrimero.prefab`, vestido por *Vestuario propio de los NPCs*, cara `silas_joven` en *Caras de los NPCs*). En el reparto de `Cap2_02_MananaDespues`, rehorneado. Sin probar en Play.
