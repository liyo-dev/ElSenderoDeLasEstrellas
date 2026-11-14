# Caras por emoción (protagonistas, niños y NPCs)

- `caras.py`: las formas (ojos con cejas, pestañas para chica, bocas) proyectadas sobre la cabeza; sirve para probar una cara suelta.
  `caras_lib.txt` es su parte de funciones (la usa `caras_set.py`). Los dos llevan el mismo código: si se cambia uno, se cambia el otro.
- `caras_set.py`: un juego entero como piezas del modelo modular (Eye<NN>/Mouth<NN>, pesadas a «head»).
  Argumentos: modular.blend, cabeza, estilo, color, base de ojos, base de bocas (0 = sin), salida.
  - Estilo: `chico`, `chicaC` (sencillos) o `chicoL` / `chicaL` (anime: línea de arriba gruesa, iris de dos tonos, dos brillos, ceja fina en arco;
    a los chicos la línea les acaba hacia fuera y un poco abajo, el iris toca el párpado y la ceja va más baja; las chicas llevan dos pestañas).
  - Forma (`OJO_FORMA`): `redondo`, `almendra`, `noventa` (Estela), `afilado`, `chico` (Will y Liam; sacada de Syaoran, Card Captor Sakura).
  - Iris: `marron`, `verde`, `azul`, `gris`, `oscuro`, `ambar`, `violeta` (Mago Oscuro). `EXTRAS=1` añade guiño, conjuro, vergüenza y risa con colmillo
    (en `kit_npcs.py`, solo Estela).
  - `MEJILLAS` (combinables con «+»; vacío = nada): `suave` (rubor), `tostada`, `pecas`, `mayor` (ojera y trazo junto a la nariz).
  - `CEJAS`: `fina` (por defecto), `gruesa`, `poblada`. Las cejas cambian con cada emoción (sorpresa arriba, triste y miedo en «tejadillo»…).
  - **`ESTILO_OJO`** (INC-918, 7 nov 2026): **`a`** («anime sencillo», **por defecto**, el que llevan todos): el contorno del anime
    a 0.9 (`OJO_A_ESCALA`; con 0.8 se perdía a distancia de juego; los niños, además, por `OJO_ESCALA`), blanco, iris de un tono con
    pupila, un brillo, línea de arriba fina sin punta larga, sin línea de abajo, y la ceja de anime algo más baja y fina.
    Chicas: pestañas sueltas y curvadas en la esquina de fuera (`PESTANAS_A`: `tres`, por defecto, o `dos`) y una en los ojos
    cerrados y de risa. Conjuro (Estela): óvalo dorado con estrella y chispas. `CEJA_DZ` sube o baja las cejas (Estela: −0.008,
    en `AJUSTES` de `kit_npcs.py`). Opciones antiguas: `anime` (iris de dos tonos, dos brillos, línea gruesa) y `b2` («muñequito
    con iris», descartado). Mismas piezas, numeración y materiales en los tres.
  - `OJO_SOMBRA=1`: «ojos de sombra»: todo el ojo de luz violeta (sin blanco ni pupila que se note) para ponerlo sobre una cabeza negra.
  - `SIN_BRILLO=1`: quita el brillo blanco de los ojos (ojos «sin luz»). `CEJA_COLOR="r,g,b"`: color de las cejas.
  - `BOCA_ESTILO=fina`: bocas de línea fina. La de reposo (`neutra`) es la media sonrisa (una comisura arriba), `picara` la acentúa,
    `sonrisa` y `risa` son anchas y `enfado` es la línea recta de cuando deja de sonreír. Mejillas nuevas: `ojeras` (ojeras suaves violeta)
    y `piedra` (tres líneas finas de luz blanca con halo en las sienes, para llevarlas con emisión).
  - `BOCA_ESTILO=amable` (Archimago): solo la boca de reposo cambia, a una línea fina con sonrisa suave y simétrica; el resto, las de siempre.
  - `pelo_mago.py` hace el pelo del Mago Oscuro (`Hair19_MagoOscuro.fbx`). La barbilla afilada se probó y se quitó (12 nov): las caras van sobre la cabeza del pack.
  - El último ojo de cada juego es **llorar** (cerrados con cejas de pena).
  - **Símbolos de anime** (`SIMBOLOS` en `caras_lib.txt`): cada juego trae sus fotogramas `Fx<ojos×10 + i>`: lágrimas 0-5 (caen),
    sudor 6-8 (resbala), vena 9-10 (late), susto 11-12 (tiembla). Unity los pasa uno tras otro (`EmotionMeshData.simbolos`).
    `caras.py` los prueba sueltos con `SIMBOLOS=lagrimas FOTOGRAMA=2` (también `llanto`, de comedia, sin usar todavía).
  - Colocación y tamaño por variables de entorno (`OJO_X`, `OJO_Z`, `BOCA_Z`, `OJO_ESCALA`, `BOCA_ESCALA`): las de niño, en `kit_npcs.py`.
- `exportar_caras.py`: FBX con todas las piezas.
- `kit_npcs.py`: genera de una vez los juegos del kit de NPCs y los de Will y Liam (args: modular.blend, carpeta de salida, claves opcionales).
  modular.blend = `ModularCharacterPolyart.fbx` importado tal cual.

## Regenerar todo (estilo A)
1. Pasar los FBX actuales a `Versiones antiguas/`.
2. `python kit_npcs.py -- modular.blend <salida>` (protagonistas y kit; los `Caras_chic*` van a `Assets/Art/Characters/Protagonistas/Caras/`, el resto a `NPCs/Caras/`).
3. `bash ../Ninos/caras_ninos.sh modular.blend <salida>` (Tobías y Nina, a `Assets/Art/Characters/Ninos/<nombre>/`).
4. Unity: *Caras de los protagonistas* → *Caras de los NPCs* → *Niños hechos en Blender* → *Guion ▸ 5. Hornear Final Parte 1* (con `CinematicaFinal_Preview` abierta).

## Numeración (ocupada)
| Ojos | Bocas | Juego |
|---|---|---|
| Eye20-29 | Mouth20-29 | Liam (chico anime, marrón); sus bocas las usan Will y los NPCs con cabeza de hombre |
| Eye30-39 | — | Will (chico anime, verde) |
| Eye40-52 | Mouth40-50 | Estela (noventa, marrón, extras 49-51, llorar 52); sus bocas las usan los NPCs con cabeza de mujer |
| Eye60-69 / Eye70-79 | Mouth60-69 / Mouth70-79 | Tobías / Nina (`../Ninos/`, estilo A desde INC-918 con `chicoL`/`chicaL`; `../Ninos/caras_ninos.sh`: caras_set con las medidas de niño y luego `cuerpo_nino.py` con su talla) |
| Eye80-209 | — | Kit de NPCs adultos (13 juegos de 10 ojos: h_ hombre, m_ mujer, hf_ hombre con cabeza de mujer, mh_ mujer con cabeza de hombre) |
| Eye210-219 / Eye220-229 | Mouth210-219 / Mouth220-229 | Niño y niña del kit (medidas de niño) |
| Eye230-289 | — | Kit, 4 nov: mayores (h_mayor, hf_mayor, m_mayor), tostado (h_tostado) y pecas (h_pecas, m_pecas) |
| Eye290-299 / Eye300-309 / Mouth310-319 | — | Mago Oscuro (12 nov, `afilado`, iris `violeta`): `Caras_mago_oscuro` (su cara al descubrirse; con **bocas propias Mouth310-319** de línea fina, ojos sin brillo, ojeras y líneas de la Piedra) y `Caras_mago_oscuro_sombra` (ojos de luz bajo el velo, `OJO_SOMBRA=1` + `OJO_ESCALA=1.12`, para una cabeza en negro). FBX en `Tools/Modelos/MagoOscuro/para_unity/` hasta que entren al juego |
| Eye320-329 / Mouth320-329 | — | Archimago (12 nov, `almendra`, iris `gris`, mejillas `mayor`, cejas `gruesa` grises, `BOCA_ESTILO=amable`): `Caras_archimago`. FBX en `Tools/Modelos/Archimago/para_unity/` hasta que entren al juego. Su pelo y perilla: `pelo_archimago.py` → `Hair20_Archimago` |
| Fx<ojos×10>… | | Los 13 fotogramas de símbolos de cada juego (Fx300 Will, Fx400 Estela, Fx800 el primero del kit…) |

FBX: los de los protagonistas en `Assets/Art/Characters/Protagonistas/Caras/`; los del kit en `Assets/Art/Characters/NPCs/Caras/`.

## En Unity
- *El Sendero ▸ Personajes ▸ Caras de los protagonistas* (`CarasDeLosProtagonistas`).
- *El Sendero ▸ Personajes ▸ Caras de los NPCs* (`CarasDeLosNPCs`): la tabla de qué juego lleva cada NPC; quita las cejas que traen los pelos y
  sombreros del pack (mallas sin cejas en `Assets/Art/Characters/NPCs/SinCejas/`) y pinta las cejas nuevas de su color. Pasar después del de los protagonistas.
- *El Sendero ▸ Personajes ▸ Hoja de caras de los NPCs y los niños (prueba)*: la cara neutra de cerca de unos 20 NPCs, Tobías y Nina, en `Guiones/_fotos_caras/NPCs/`.
- *El Sendero ▸ Personajes ▸ Fotos de caras de Will (prueba)* (`FotosDeCaras`): Will sin scripts en la plaza de MainWorld (si está cargada), cada emoción de su perfil, fotos de URP (cerca, medio y distancia de juego) en `Guiones/_fotos_caras/Will/`. Funciona desde el buzón. Busca un sitio al sol (o el de `Guiones/_fotos_caras/sitio.txt`, «x z»). *… de Will con Liam y Estela*, *… de Estela con Will y Liam* y *… de Liam con Will y Estela (prueba)*: el personaje con los otros dos a los lados; salen todas las emociones de su perfil y los ojos del juego que no usa ninguna (p. ej. conjuro y vergüenza de Estela), en `Guiones/_fotos_caras/<personaje>/`.
- Otro juego: una fila en `kit_npcs.py` y otra en el `Kit` de `CarasDeLosNPCs.cs` con la misma clave, en una numeración libre.

## Pelos hechos con las caras de la cabeza (13 nov 2026)
Reglas (`hinchar_casquete` de `caras_lib.txt`, usada por `pelo_archimago.py` y `pelo_mago.py`):
- La separación va en metros: la cabeza del pack está en centímetros bajo el esqueleto (escala 0,01).
- El borde del pelo baja siempre hasta la cabeza (faldón), con la normal del borde: si no, por el hueco se ven la piel y el revés oscuro del pelo.
- Pelos con volumen (`suavizar`): solo la pieza más grande (las orejas del pack son islas sueltas), subdividida y suavizada, y nunca a menos de `holgura` × separación de la cabeza.
- La línea del nacimiento es continua (frente, sienes y patillas, nuca); nada de cortes aparte que dejen calvas a los lados.
- Lo que va encima del casquete (mechones) va a más distancia que él: a la par se pelean y parpadean.
- Ningún pelo sale sin pasar `chequeo_pelo.py`:
  `python chequeo_pelo.py -- modular.blend pelo.fbx salida [frente lado nuca] [tope]` → `salida.png` (rojo: piel de la zona del pelo que se ve; negro: revés del pelo) y `salida.md` (PASA / NO PASA).
- `chequeo_pelo.py` mide también el **parpadeo**: superficies del pelo que miran hacia fuera y tienen otra (del pelo o de la piel) mirando al mismo lado a menos de 2 mm. Tope: 1 % del área. Comprobado con el pelo del Archimago que parpadeaba en el juego (v2, a 0,45 mm de la cabeza): da 5,6 %. Para pelo corto con las orejas a la vista, pasar la línea «lado» por encima de la oreja (`1.19 1.10 0.82`).
- Protagonistas (13 nov 2026): Will PASA (parpadeo 0,4 %), Liam PASA (0 %), Estela NO PASA (parpadeo 18 %: los mechones de `Hair14_Coleta` se cruzan con las caras de arriba casi a la par; además, un 3,6 % de revés en una vista). Informes en `../<Personaje>/chequeo/pelo.md`.

## Láminas sueltas con grosor (13 nov 2026, INC-1169)
- `con_grosor(objetos, metros=0.006)` en `caras_lib.txt`: grosor hacia dentro a mechones y flequillos que se separan
  de la cabeza. Sin él, desde abajo se ve su revés (Unity no lo pinta) y se ve el cielo a través del pelo.
- Lo usa `pelo_archimago.py` (los cuatro mechones). Comprobar con `../Prendas/chequeo_huecos.py`.
- Will (`Hair16_Heroe`) no pasa ese chequeo: pendiente.
