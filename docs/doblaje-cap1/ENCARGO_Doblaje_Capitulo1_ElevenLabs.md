# Encargo: doblar con ElevenLabs el capítulo 1 de «El Sendero de las Estrellas»

Documento de traspaso para un hilo nuevo. Léelo entero antes de tocar nada. El dueño del proyecto es Raúl (vive en Málaga; el juego está en español de España).

## 1. Objetivo

Generar con ElevenLabs (vía web, usando el navegador) un archivo de audio por cada línea hablada del capítulo 1 del juego, con voces coherentes por personaje, nombrado con la clave de localización de la línea, y entregarlo ordenado en carpetas. **No hay que integrar nada en Unity**: solo producir y organizar los audios.

## 2. Alcance: qué es el «capítulo 1»

Es lo que contiene el grafo narrativo activo `Assets/NarrativeGraph/Cap1.asset` (el resto de capítulos antiguos están archivados y NO entran):

1. Prólogo jugable: la última noche del Archimago (28 líneas).
2. La mañana de Will: ventana, Oliver y la discusión de las peras (19 líneas).
3. Eldran y la caja de fruta, incluidas las charlas del camino y las frases de misión (22 líneas).
4. El Despertar de la Estrella (5 líneas).
5. El Demonio: frases de Eldran durante el combate (19 líneas).
6. Liam y la bola de cristal (3 líneas).

Total: 96 líneas, unos 4.000 caracteres. Es poco texto: el coste en créditos es mínimo, así que se puede repetir tomas sin problema.

## 3. Archivos de partida (ya preparados)

Carpeta del proyecto Unity: `ElSenderoDeLasEstrellas/docs/doblaje-cap1/`

- `lineas_cap1.csv` — **la fuente de verdad de este encargo.** Columnas: `n`, `bloque`, `fuente`, `clave`, `hablante`, `texto`, `nota`. Está extraída del texto vigente de `Assets/Resources/Localization/*_es.json` (a 1 oct 2026), con las etiquetas de interfaz (`<sprite>`, `<kbonly>`) ya limpiadas. Abrir con codificación UTF-8.
- `../guion-doblaje-elevenlabs-es.md` — guion antiguo de todo el juego (30 ago 2026). Úsalo solo como referencia de casting y de etiquetas de interpretación. **Su texto del capítulo 1 está desfasado**: el prólogo y varias escenas se han reescrito desde entonces. Manda el CSV.

El texto del CSV es el que se oye en el juego. **No lo edites** ni toques ningún archivo de `Assets/`. Si ves una errata o una frase rara, anótala en el informe final y deja que Raúl decida.

## 4. Reparto de voces

Recuento por personaje (líneas / caracteres aprox.):

| Personaje | Líneas | Cómo suena (dirección) |
|---|---|---|
| Eldran | 44 / 2.070 | Mentor de Will, adulto mayor, cálido y protector, algo despistado. En combate grita, anima y se alarma. Es la voz principal: elegirla bien. |
| Archimago | 12 / 470 | El Archimago del prólogo, antepasado de Will. Adulto, bondadoso, humor suave, cansado; en el duelo, firme. |
| Will | 9 / 240 | Protagonista, 18 años, voz cálida, algo insegura. La línea `EVT_AWAKEN_03` es un grito («¡NOO!»). |
| Oliver | 7 / 280 | Amigo de Will, joven, entusiasta, torpe con los hechizos, con prisa y sin aliento. |
| Liora | 7 / 290 | Pareja del Archimago, adulta, cariñosa pero firme. |
| Victoria | 4 / 220 | Vecina que discute con Eldran, adulta, práctica, con retranca. |
| Liam | 3 / 120 | Joven, sereno y seco, con un fondo oscuro; ve a Will en la bola de cristal. La tercera línea es una risa contenida. |
| Mago Oscuro | 2 / 60 | Villano del prólogo, grave, imponente, frío. |
| Extras | 7 / 280 | `Vecino` (5), `Vecina` (1), `Nina` (1, niña): voces de reparto. Con dos o tres voces distintas basta. |

Hay una línea de pensamiento de Will (`NIGHTMARE_AGAIN`, «Otra vez esa pesadilla.»). Dóblala igualmente con tono íntimo y deja una nota para que Raúl decida si se usa como voz o solo como texto.

## 5. Cómo se hace en ElevenLabs (web)

Usa el navegador del que dispongas (Claude in Chrome tiene preferencia; si ElevenLabs no tiene la sesión iniciada, pídele a Raúl que inicie sesión él mismo; **nunca le pidas ni teclees contraseñas**).

**Herramienta correcta:** `Text to Speech` (o Studio). **No uses «Dubbing»**: ese módulo es para redoblar un audio o vídeo que ya existe, y aquí no hay audio original.

1. **Modelo:** el desplegable de la cuenta de Raúl ofrece ahora Eleven v4 (a 2 oct 2026); v3 y v4 entienden etiquetas de interpretación entre corchetes y admiten español. Usa el que esté marcado como más expresivo y mantén el mismo modelo en todo el capítulo. Límite de 5.000 caracteres por generación en web (mucho más de lo que necesitamos: se genera una línea por vez).
2. **Ajustes de voz:** punto de partida estabilidad ≈ 50, similitud ≈ 75, exageración de estilo 0. Baja la estabilidad para líneas muy emocionales (gritos, pánico) y súbela para las tranquilas. La velocidad no se ajusta en v3: si una línea sale lenta o rápida, se cambia con puntuación o etiquetas (`[rushed]`, `[slowly]`) o se regenera.
3. **Etiquetas de interpretación:** van **en inglés** y entre corchetes al principio o dentro de la frase, aunque el texto sea español. Útiles aquí: `[shouts]`, `[whispers]`, `[softly]`, `[laughs]`, `[sighs]`, `[surprised]`, `[annoyed]`, `[sad]`, `[happily]`, `[rushed]`, `[slowly]`, `[pause]`, `[booming]`. Los puntos suspensivos fuerzan pausa; una palabra en MAYÚSCULAS ganará énfasis. Comprueba al escuchar que no lee la etiqueta en voz alta; si lo hace, cámbiala por otra o quítala.
4. **Elegir voces (antes de generar nada en serio):**
   - En la biblioteca de voces filtra por idioma español y acento de España (castellano), y mira las categorías de personaje/narración.
   - Para cada personaje del apartado 4 propón **2 o 3 candidatas** y genera con cada una la **misma línea de prueba** (elige una representativa del CSV). Presenta el resultado a Raúl en una tabla (personaje, voz, enlace o nombre, línea de prueba) y **espera su aprobación**. No sigas sin ella.
   - Si ninguna voz encaja (por ejemplo Mago Oscuro o la niña Nina), usa «Voice Design» de ElevenLabs para crearla con una descripción en texto.
   - **No clones la voz de ninguna persona real** sin permiso explícito de Raúl.
5. **Generar:** una generación por línea del CSV (una línea = un archivo). Para las líneas importantes o emocionales (el grito de Will, el pánico de Eldran en `EVT_AWAKEN_01`/`EVT_AWAKEN_06`, la risa de Liam) genera 2 o 3 tomas, escúchalas y quédate con la mejor.
6. **Descargar:** formato MP3 a la máxima calidad que ofrezca su plan (192 kbps como mínimo). Si el plan permite WAV o FLAC, WAV es preferible para trabajar luego en Unity; confírmalo con Raúl si hay duda.

### Detalles del texto que conviene saber

- `EVT_08` en el juego muestra un icono de botón que el CSV ya ha quitado, y queda «presiona y derrota al demonio». Para audio, di «Usa tu magia y derrota al demonio.» (sin mencionar botón, porque varía con teclado y mando) y anótalo en el informe.
- `EVT_AWAKEN_06` tiene una errata en el juego («apartate» sin tilde). Al generar, escribe «apártate» para que la lectura sea correcta, pero **no cambies el CSV**; anótala para Raúl.
- `OLIVER_GREETING_BEFORE_MENUS_HANDOFF` tiene dos frases separadas por un salto de línea; genera una única toma con las dos.
- Líneas muy cortas de Eldran en combate (`EVT_ELDRAN_AHORA_01/02` y similares): en el juego deben oírse como mucho en 1-2 segundos y pueden interrumpirse. Procura que el audio dure poco, con energía alta.
- En las secuencias, cada burbuja de texto dura unos 2,6 s por defecto. Al terminar, calcula la duración de cada audio y avisa de las líneas que pasen claramente de ese tiempo, para que Raúl decida si ajusta el texto o el tiempo.

### Voces ya aprobadas por Raúl (2 oct 2026)

- **Will:** Kaito Renji - Youthful and Warm.
- **Oliver:** Loren - Mannered, Parodic and Playful.
- **Victoria:** Cristina - Empathetic Customer support.
- **Eldran:** Rafael - Expressive and Theatrical (acento peninsular, voz de anciano; Raúl descartó las voces con acento latino).
- **Archimago:** Luis - Polished, Mature and Credible (acento peninsular; cuesta el doble de créditos por generación, unos 940 créditos para sus 12 líneas).
- **Mago Oscuro:** Victor - Deep, Malevolent and Ancient (acento peninsular; Raúl lo quiere con aire de Jafar: suave, siniestro y teatral. Probar etiquetas como [sinister] o [whispers] si hace falta).
- **Liam:** Pablo - Deep, Confident and Clear (acento peninsular; Raúl lo ve como Eric de La Sirenita: joven, cálido y seguro. Ojo: la tercera línea es una risa contenida).
- **Nina:** Sara Martin - Young and Reflective (acento peninsular, voz joven de mujer; es la voz de la niña del globo).
- **Liora:** Sara Martin - Gentle and Layered (acento peninsular, voz de mujer de edad media, suave y amable; distinta de la de Nina).

**Reparto cerrado: los 9 personajes principales tienen ya voz aprobada.**

### Extras y voces de resguardo (decidido por Raúl, 2 oct 2026)

Raúl no quiere elegir una a una las voces de los personajes secundarios: para ellos se usa una **voz de resguardo** de la lista de abajo. Todas tienen acento peninsular (filtro Idioma Spanish + Acento Peninsular de la biblioteca de ElevenLabs). No se han escuchado todas, así que el ejecutor debe comprobar que suenan bien con su línea; si alguna no encaja, puede cambiarla por otra de la misma fila y dejarlo anotado en `registro_doblaje.csv`.

| Uso | Voz de resguardo | Alternativa |
|---|---|---|
| Hombre adulto o maduro | Julio (voz masculina madura, profunda y clara) | Juanjo Relatos |
| Hombre joven | David Martin - Clear, Calm and Elegant | Gabriel Blanco - Measured and Relatable |
| Mujer adulta | Cristina - Casual conversation | Clara - Elegant, Sultry and Soft |
| Mujer joven | Sara Martin - Young and Reflective | Estela - Conversacional peninsular |
| Mujer mayor o regañona | MariCarmen - Hurried and Intimidating | Rosa - Neutral, Calm and Genuine |
| Anciana de caricatura | Granny - Cartoonish Old Lady | |

**Asignación de los extras del capítulo 1:**

- **Vecino** (5 líneas): Julio.
- **Vecina** (1 línea): Cristina - Casual conversation.

Los extras con voz de resguardo son los únicos personajes que no tienen que pasar por la aprobación de Raúl. Las voces de los 9 principales no se tocan.

### Estado de la cuenta (2 oct 2026)

- Raúl ya tiene la sesión iniciada en ElevenLabs y unos 240.000 créditos gratis disponibles; el capítulo entero cuesta unos 4.000.
- La pantalla de Text to Speech tiene un borrador del canal de Tentino (Pez Pepe y Tentino) sin generar. **No lo borres ni lo sobrescribas**; trabaja en una pestaña nueva o limpia el editor solo si Raúl lo autoriza.
- Biblioteca de voces: filtrando idioma Spanish y acento Peninsular salen unas 446 voces; con la categoría Personajes, 31. La dirección es `elevenlabs.io/app/voice-library?required_languages=es&accent=peninsular`.
- Quien ejecute esto no puede escuchar audio. Las voces se eligen por descripción y Raúl las confirma escuchándolas.

## 6. Nombres y carpetas de salida

Crea dentro del proyecto, junto al CSV:

```
docs/doblaje-cap1/audio/
  1_Prologo/
  2_Manana_de_Will/
  3_Eldran_y_la_caja/
  4_Despertar_de_la_Estrella/
  5_Demonio/
  6_Liam_bola_de_cristal/
  Versiones antiguas/        <- tomas descartadas y versiones superadas
```

- Cada archivo se llama exactamente como su `clave` del CSV: `PROLOGO_MANANA_INTRO.mp3`, `EVT_AWAKEN_01.mp3`, etc. Sin espacios ni abreviaturas, para poder enlazarlo luego por nombre.
- Las tomas que descartes o sustituyas por otras mejores **no se dejan mezcladas** con las buenas: muévelas a `Versiones antiguas/` (regla de organización de Raúl).
- Mantén además `docs/doblaje-cap1/registro_doblaje.csv` con una fila por línea y estas columnas: `clave`, `hablante`, `voz_elevenlabs`, `id_o_enlace_de_voz`, `texto_enviado` (con etiquetas), `ajustes` (estabilidad/similitud/estilo), `toma_elegida`, `duracion_s`, `estado` (`ok`, `revisar`, `pendiente`), `notas`.

### Mover los archivos al proyecto

Las descargas del navegador caen en la carpeta Descargas del ordenador de Raúl. Con acceso al ordenador (shell local), muévelas a las carpetas de arriba con `mv -n` (nunca sobrescribir) y renómbralas por clave. Si no tienes acceso a esa carpeta, pide el acceso con la herramienta correspondiente y espera la aprobación de Raúl.

## 7. Orden de trabajo sugerido

1. Leer `lineas_cap1.csv` y comprobar que tiene 96 filas y ninguna con texto vacío.
2. Propuesta de voces (apartado 5.4) y **parada para que Raúl apruebe**.
3. Generar primero los bloques pequeños (4 y 6) como prueba de calidad y enseñar 2 o 3 audios a Raúl.
4. Generar el resto por bloques, actualizando `registro_doblaje.csv` sobre la marcha.
5. Verificación final.

## 8. Verificación antes de dar por terminado

- Hay un `.mp3` (o `.wav`) por cada fila del CSV, con el nombre exacto de la clave; ninguno de más ni de menos.
- Escucha al menos una línea de cada personaje y todas las tomas emocionales; confirma que no se lee ninguna etiqueta en voz alta.
- La misma voz suena igual en todo el capítulo (mismo ajuste de estabilidad por personaje salvo excepciones anotadas).
- `registro_doblaje.csv` está completo y las duraciones calculadas.
- No se ha modificado nada fuera de `docs/doblaje-cap1/`.

## 9. Informe final a Raúl (corto)

Indica: cuántas líneas quedaron `ok` y cuáles `revisar`, qué voz se usó para cada personaje, las erratas y dudas de texto encontradas (apartado 5), las líneas que exceden la duración de burbuja y los créditos aproximados gastados. Una frase por cada punto, sin recapitular todo el proceso.

## 10. Límites

- No tocar `Assets/`, `GDD.md`, `TRACKER.md` ni ningún archivo del proyecto fuera de `docs/doblaje-cap1/`.
- No integrar los audios en Unity: eso es un paso posterior.
- No gastar créditos en bloques fuera del capítulo 1.
- Si el plan de ElevenLabs no permite algo (modelo, formato, límite de créditos), párate y pregunta en vez de buscar rodeos.
