# Caras por emoción (protagonistas, niños y NPCs)

- `caras.py`: las formas (ojos con cejas, pestañas para chica, bocas) proyectadas sobre la cabeza; sirve para probar una cara suelta.
  `caras_lib.txt` es su parte de funciones (la usa `caras_set.py`). Los dos llevan el mismo código: si se cambia uno, se cambia el otro.
- `caras_set.py`: un juego entero como piezas del modelo modular (Eye<NN>/Mouth<NN>, pesadas a «head»).
  Argumentos: modular.blend, cabeza, estilo, color, base de ojos, base de bocas (0 = sin), salida.
  - Estilo: `chico`, `chicaC` (sencillos) o `chicoL` / `chicaL` (anime: línea de arriba gruesa, iris de dos tonos, dos brillos, ceja fina en arco;
    a los chicos la línea les acaba hacia fuera y un poco abajo, el iris toca el párpado y la ceja va más baja; las chicas llevan dos pestañas).
  - Forma (`OJO_FORMA`): `redondo`, `almendra`, `noventa` (Estela), `afilado`, `chico` (Will y Liam; sacada de Syaoran, Card Captor Sakura).
  - Iris: `marron`, `verde`, `azul`, `gris`, `oscuro`, `ambar`. `EXTRAS=1` añade guiño, conjuro, vergüenza y risa con colmillo
    (en `kit_npcs.py`, solo Estela).
  - `MEJILLAS` (combinables con «+»; vacío = nada): `suave` (rubor), `tostada`, `pecas`, `mayor` (ojera y trazo junto a la nariz).
  - `CEJAS`: `fina` (por defecto), `gruesa`, `poblada`. Las cejas cambian con cada emoción (sorpresa arriba, triste y miedo en «tejadillo»…).
  - El último ojo de cada juego es **llorar** (cerrados con cejas de pena).
  - **Símbolos de anime** (`SIMBOLOS` en `caras_lib.txt`): cada juego trae sus fotogramas `Fx<ojos×10 + i>`: lágrimas 0-5 (caen),
    sudor 6-8 (resbala), vena 9-10 (late), susto 11-12 (tiembla). Unity los pasa uno tras otro (`EmotionMeshData.simbolos`).
    `caras.py` los prueba sueltos con `SIMBOLOS=lagrimas FOTOGRAMA=2` (también `llanto`, de comedia, sin usar todavía).
  - Colocación y tamaño por variables de entorno (`OJO_X`, `OJO_Z`, `BOCA_Z`, `OJO_ESCALA`, `BOCA_ESCALA`): las de niño, en `kit_npcs.py`.
- `exportar_caras.py`: FBX con todas las piezas.
- `kit_npcs.py`: genera de una vez los juegos del kit de NPCs y los de Will y Liam (args: modular.blend, carpeta de salida, claves opcionales).
  modular.blend = `ModularCharacterPolyart.fbx` importado tal cual.

## Numeración (ocupada)
| Ojos | Bocas | Juego |
|---|---|---|
| Eye20-29 | Mouth20-29 | Liam (chico anime, marrón); sus bocas las usan Will y los NPCs con cabeza de hombre |
| Eye30-39 | — | Will (chico anime, verde) |
| Eye40-52 | Mouth40-50 | Estela (noventa, marrón, extras 49-51, llorar 52); sus bocas las usan los NPCs con cabeza de mujer |
| Eye60-69 / Eye70-79 | Mouth60-69 / Mouth70-79 | Tobías / Nina (`../Ninos/`, estilo sencillo; caras_set con las medidas de niño y luego `cuerpo_nino.py` con su talla) |
| Eye80-209 | — | Kit de NPCs adultos (13 juegos de 10 ojos: h_ hombre, m_ mujer, hf_ hombre con cabeza de mujer, mh_ mujer con cabeza de hombre) |
| Eye210-219 / Eye220-229 | Mouth210-219 / Mouth220-229 | Niño y niña del kit (medidas de niño) |
| Eye230-289 | — | Kit, 4 nov: mayores (h_mayor, hf_mayor, m_mayor), tostado (h_tostado) y pecas (h_pecas, m_pecas) |
| Fx<ojos×10>… | | Los 13 fotogramas de símbolos de cada juego (Fx300 Will, Fx400 Estela, Fx800 el primero del kit…) |

FBX: los de los protagonistas en `Assets/Art/Characters/Protagonistas/Caras/`; los del kit en `Assets/Art/Characters/NPCs/Caras/`.

## En Unity
- *El Sendero ▸ Personajes ▸ Caras de los protagonistas* (`CarasDeLosProtagonistas`).
- *El Sendero ▸ Personajes ▸ Caras de los NPCs* (`CarasDeLosNPCs`): la tabla de qué juego lleva cada NPC; quita las cejas que traen los pelos y
  sombreros del pack (mallas sin cejas en `Assets/Art/Characters/NPCs/SinCejas/`) y pinta las cejas nuevas de su color. Pasar después del de los protagonistas.
- Otro juego: una fila en `kit_npcs.py` y otra en el `Kit` de `CarasDeLosNPCs.cs` con la misma clave, en una numeración libre.
