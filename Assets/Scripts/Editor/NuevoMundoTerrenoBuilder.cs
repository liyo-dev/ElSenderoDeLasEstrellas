using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Petición de Raúl (10 sep 2026, sesión de Cowork): "base del mundo" nueva — terreno con montañas
/// de verdad (pisables), caminos de montaña que se cruzan con puentes, un pueblo pequeño arriba (frío,
/// con nieve) y otro pueblo pequeño abajo, el castillo, y el Bosque Prohibido rehecho.
///
/// *** REVISIÓN 3 (mismo día, tras ver capturas de la revisión 2 en el Editor) *** — tres pedidos:
/// 1. El terreno se veía "demasiado brillante" — las 3 capas eran color plano sin textura. Ahora cada
///    capa usa una textura con variación (ruido), `smoothness` bajo y `metallic` 0 (antes por defecto,
///    de ahí el brillo plástico). Se añade además una 4ª capa de arena para la playa (ver punto 3).
/// 2. "Una montaña super grande" para dar personalidad — un pico protagonista, esculpido como un bulto
///    adicional que se mezcla suavemente con el terreno de alrededor (no una prefab suelta — sigue la
///    misma decisión de la revisión 2 de que la montaña la hace el propio heightmap). Además, la nieve
///    de los picos ya NO depende de "qué tan al norte estás" sino de la ALTURA real de cada punto — así
///    que cualquier pico lo bastante alto se cubre de nieve, incluido este, sin tocar nada a mano.
/// 3. Mundo abierto con mar alrededor por los 4 lados y el continente en medio (playa → hierba → roca →
///    nieve). Se sustituye la curva de subida norte-sur (lineal, de la v1/v2) por una curva RADIAL desde
///    el centro del mapa: el terreno baja hacia cualquier borde, haya sea Norte, Sur, Este u Oeste — eso
///    es lo que hace que el terreno lea como una isla y no como una franja. El Pueblo de Nieve pasa a
///    estar en el centro/pico del mapa, el Pueblo Bajo + Castillo + Bosque Prohibido se quedan en la
///    franja costera sur, y se añaden un océano (plano grande) y unas piezas de orilla a lo largo de la
///    costa. El pico protagonista del punto 2 se coloca junto al Pueblo de Nieve, no encima.
///
/// *** REVISIÓN 4 (mismo día, capturas de la revisión 3: "la isla demasiado pequeña y se ve regular") ***
/// Causa real, no solo de cámara: el océano se escalaba a ciegas (factor fijo x60 sobre el tamaño del
/// propio prefab `Ocean.prefab`, que resultó ser mucho más grande de lo asumido) y además las anclas de
/// Pueblo Bajo / Castillo / Bosque Prohibido, heredadas de la disposición lineal de la v1/v2, quedaban tan
/// cerca del radio de costa (`ShelfRadial`) que su altura calculada caía POR DEBAJO del nivel del mar —
/// es decir, buena parte del "continente" ya estaba inundada antes de esta revisión, y solo asomaba el
/// núcleo central (de ahí la isla diminuta y monótona de la captura). Cambios:
/// - Océano: en vez de un factor de escala fijo, se mide el tamaño real del prefab (bounds del Renderer) y
///   se escala para cubrir un objetivo relativo a la diagonal del terreno (`OceanCoverageFactor`) — ya no
///   es una escala "a ojo" que puede salir desproporcionada.
/// - Curva de subida: de `SmoothStep` (muy plana cerca de la costa) a una curva "ease-out" cuadrática que
///   sube rápido nada más dejar la costa — así el borde habitable no queda pegado al nivel del mar.
/// - `ShelfRadial`/`PeakRadial` ensanchados y las anclas (Pueblo Bajo, Castillo, área del Bosque) recolocadas
///   a radios seguros, verificados a mano contra la nueva curva.
/// - Ruido de costa (`CoastlineNoise`, en función del ángulo) para que el litoral no sea un círculo perfecto
///   (bahías/penínsulas) — parte de por qué "se ve regular" además del tamaño.
/// - Comprobación de seguridad al generar (`VerificarAnclasSobreElNivelDelMar`): si alguna ancla clave sigue
///   quedando en/bajo el nivel del mar, se avisa por log en vez de fallar en silencio — para poder afinar
///   sin depender solo de mirar capturas.
///
/// *** CAMBIO DE PROCESO Y REVISIÓN 5 (mismo día, tras ver la revisión 3 en el Editor) ***
/// Raúl pidió ir paso a paso en vez de regenerar todo de golpe cada vez: 1) terreno + zonas (este script,
/// ahora), 2) caminos, 3) árboles/puentes/cascadas, 4) casitas (pueblos/castillo), 5) triggers y reubicar
/// las escenas exteriores. Este archivo, de momento, SOLO construye el paso 1: terreno + océano/costa +
/// marcadores de zona (esferas de colores en la posición y altura reales de cada futura zona, sin
/// construir todavía los edificios/árboles/caminos de verdad) — así se puede dar el visto bueno a la forma
/// del mundo antes de poblarlo. Los métodos de pasos futuros (caminos, pueblos, laberinto del bosque,
/// ambiente frío) se quedan definidos más abajo en el archivo, listos para engancharse en los próximos
/// pasos, pero de momento NO se llaman desde el menú.
///
/// También dos ajustes pedidos tras ver la isla salir diminuta:
/// - "Más que una isla debe ser un continente": el radio de costa (`ShelfRadial`) se ensancha mucho más
///   (antes dejaba solo un núcleo pequeño por encima del mar; ahora la mayor parte del cuadrado de terreno
///   es tierra firme, con el mar como borde/marco en los 4 lados y las esquinas, no como protagonista).
/// - Isla secundaria: un islote pequeño y bajo (no una montaña) al sureste del continente, en mar abierto,
///   para un futuro pueblo pesquero/zona de puerto — Raúl confirma que hay assets de sobra para vestirlo
///   más adelante (fase de "casitas"). De momento solo lleva su marcador de zona.
///
/// La petición de "poder volar y llegar a cualquier parte" es un sistema de JUGADOR (modo de vuelo en
/// `PlayerActionManager`, cámara, límites de mundo), no algo que toque este generador de terreno — el
/// catálogo de animaciones Invector del proyecto (`catalogo-animaciones-invector.md`) ya tiene
/// `fly_idle`/`fly_dive`, así que hay una base de la que partir, pero es trabajo aparte, pendiente de
/// que Raúl confirme cómo lo quiere (ver conversación).
///
/// *** REVISIÓN 6 (mismo día, tras ejecutar el Paso 1 en el Editor) *** — cinco cosas:
/// 1. **La isla secundaria quedaba pegada a la principal** (capturas: un istmo/bajío casi tocando ambas
///    costas). Se aleja mucho más (antes radial ~1.0, ahora ~1.2) para que quede un tramo de mar abierto
///    de verdad entre las dos, incluso con el ruido de costa en su peor caso.
/// 2. **Bug real de nieve/roca — nunca podía salir nieve.** `ComputeWeights` restaba primero la roca del
///    "resto" disponible y luego la nieve; con los umbrales anteriores la roca ya se había comido el 100%
///    del resto antes de llegar a la altura de nieve, así que la nieve NUNCA tenía nada que reclamar,
///    pasara lo que pasara con la altura — de ahí "no veo blanca en los picos". Se invierte el orden
///    (nieve reclama antes que roca, por ser la banda más alta) y se bajan los umbrales para que
///    encajen con la altura real que alcanza el terreno (con `PeakRiseFraction=0.50`, la meseta central
///    ronda 110m de 220m máx. — los umbrales viejos de nieve/roca estaban pensados para alturas que el
///    terreno nunca llega a tocar salvo en el pico protagonista).
/// 3. **Colores que no encajan con el pack ("estilo ghibli distinto").** Las texturas se generaban con
///    colores inventados a ojo y bastante ruido/grano — nada que ver con la paleta plana y saturada que
///    usa el propio pack (`RPG Tiny Fantasy World 01 PBR/Texture/Base Map.png`, un atlas de color plano
///    sin grano, el mismo que usa `Shore01`/`Ocean` vía `DefaultPBR.mat`). Se ajustan los colores base de
///    las 4 capas para acercarse a esa paleta y se reduce mucho el ruido de la textura generada (de
///    variación 0.82-1.18 a 0.94-1.06) para un acabado más plano/toon y menos "grano PBR realista".
/// 4. **"La isla la haría incluso más grande" + zonas de descanso planas.** El terreno pasa de 750×750 a
///    950×950 (continente más grande de verdad). Además, antes toda la superficie transitable tenía ruido
///    de montaña — ahora hay 4 "zonas de descanso" (`RestZoneNorm`) donde el ruido se amortigua localmente
///    (igual que ya se hacía en el surco del camino) dejando un claro llano a la altura ambiente de ese
///    punto — no todas a la misma altura, así hay descansos tanto cerca de la costa como a media montaña.
/// 5. **Nota de continuidad narrativa (Raúl):** hay que tener en cuenta la escena de la novela/guion en la
///    que Estela destruye media montaña "al borde del Reino" (cap. IX "La taberna y la montaña" —
///    `novela/manuscrito-novela-completo.md`, `claude/guion-doblaje-elevenlabs-2026-08-30.md`) al perder
///    los nervios con su fuego. De cara a los pasos 4-5 (casitas/reubicación), la zona del Castillo/Reino
///    de este mundo nuevo debería dejar sitio a una montaña visible y "rompible" cerca, para poder alojar
///    esa escena (o su equivalente) más adelante — no se toca nada de esto en el Paso 1, es solo la nota
///    para no perderla de vista.
///
/// *** REVISIÓN 7 (mismo día, tras ejecutar de nuevo el Paso 1) *** — Raúl reportó, tras la revisión 6:
/// "sigo viendo la misma textura en toda la isla... la isla parece que tiene brillo propio" y, ya sobre
/// la isla secundaria separada: "es inutilizable, si vamos ahí no podemos hacer nada" — más la idea de
/// hacer varias islitas navegables a nado, una de ellas con la Piedra Ancestral.
/// 1. **Bug real encontrado: `GetOrCreateLayer` reutilizaba el asset viejo en vez de regenerarlo.**
///    Cargaba la `.terrainlayer`/textura con `AssetDatabase.LoadAssetAtPath` y, si ya existía en disco
///    (de la primerísima ejecución, revisión 3), la devolvía tal cual — ignorando en silencio todos los
///    cambios de color/ruido/brillo de las revisiones 4, 5 y 6. Por eso "sigo viendo la misma textura":
///    literalmente lo era, la de la revisión 3, nunca actualizada. Esto rompe el idioma del propio script
///    ("regenerar = destruir y reconstruir"), así que ahora la textura y las propiedades de la capa se
///    SOBRESCRIBEN siempre (con `EditorUtility.SetDirty` cuando el asset ya existía), tanto si el archivo
///    es nuevo como si venía de una ejecución anterior — `GenerarPixelesTextura` sustituye a la antigua
///    `CreateNoisyTexture`, que devolvía un `Texture2D` ya hecho en vez de los píxeles a volcar encima.
///    El "brillo propio" probablemente era en parte esto también (la capa nunca llegó a coger el
///    smoothness/paleta pensados en la revisión 6).
/// 2. **Isla secundaria "inutilizable" — no tenía ni un metro llano.** Su forma era un smoothstep puro de
///    borde a pico (como el pico protagonista), así que toda la superficie era pendiente, sin ningún
///    llano donde plantar un edificio. Se añade una MESETA real: `SmallIslandPlateauFraction` (0.55) del
///    radio de cada islote queda completamente plano antes de que el aro exterior empiece a bajar hacia
///    el mar.
/// 3. **Archipiélago en vez de un islote único (idea de Raúl).** "Se me ocurre hacer islitas todas
///    navegables, que para eso aprendemos nado, y que en una de ellas esté la Piedra Ancestral donde
///    abrimos el portal al Sendero." La isla secundaria pasa a llamarse "Isla del Puerto" (mismo sitio,
///    ahora con meseta) y se añade una segunda, "Isla de la Piedra Ancestral", separada de la anterior
///    (verificado por muestreo real de altura, no solo por coordenadas — ver `VerificarIslasMenores`) y a
///    la misma distancia de la costa principal que ya se confirmó segura en la revisión 6 (mar abierto de
///    verdad, no un istmo). Da un sitio físico en el mundo abierto al combate/ritual de la Piedra
///    Ancestral, que hoy vive en una escena aparte (`PiedraAncestralGuardianBuilder.cs`, capítulo XVI de
///    la novela) sin ubicación en el mapa. El resto de islotes que Raúl quiera (más "islitas navegables")
///    es tan sencillo como añadir entradas al array `IslasMenores` — la lista está pensada para crecer.
///    Nota: la distancia de nado en sí (cuánto se tarda, si hace falta un sistema de resistencia, etc.) es
///    un tema de mecánica de jugador, no de este generador de terreno — pendiente de que Raúl confirme
///    cómo quiere que funcione nadar.
///
/// *** REVISIÓN 8 (mismo día, tras ejecutar de nuevo el Paso 1) *** — más feedback de Raúl sobre la
/// entrega de la revisión 7: "las islas deberían tener el típico camino de la playa a la meseta", "la
/// orilla tiene otro color al de la isla", "los colores no me convencen", "sigo viendo como la isla
/// brilla", y con capturas señalando que el Bosque Prohibido y el Pueblo de Will (el punto rosa) están
/// colocados en plena pendiente, no en llano.
/// 1. **Camino de playa a meseta en cada islote.** Cada islote menor tiene ahora un sector angular (hacia
///    el continente, por donde se llega a nado) donde la banda de subida es mucho más ancha —
///    `SmallIslandRampPlateauFraction`/`SmallIslandRampHalfAngleDeg` — así la pendiente se reparte en
///    mucho más distancia y se lee como una rampa caminable, no como un banco vertical. Sigue siendo forma
///    de terreno, no un camino de piedra de verdad con barandillas etc. — eso, como el resto de caminos,
///    es trabajo del Paso 2.
/// 2. **Bosque Prohibido y Pueblo de Will en pendiente — mismo tipo de bug que ya se arregló para las
///    zonas de descanso, pero sin aplicar a estas.** Solo las 4 `RestZoneNorm` amortiguaban el ruido de
///    montaña; los anclajes de pueblo/castillo y el rectángulo del Bosque no tenían ningún aplanado, así
///    que si su posición caía en ladera (como pasó aquí) se quedaban inclinados. Nueva función
///    `ZonasConstruiblesFlatten` aplica el mismo truco (amortiguar el ruido local, no fijar una altura) a
///    Pueblo Bajo, Castillo, Pueblo de Nieve, Pueblo de Will y el rectángulo del Bosque Prohibido.
/// 3. **"La orilla tiene otro color que la isla."** Investigado a fondo esta vez: `Base Map.png` es un
///    atlas de swatches de color sin organización temática (no hay una "fila de arena"), así que no hay
///    forma de saber, sin abrir el mesh de `Shore01` en el Editor, qué swatch exacto usa — cualquier color
///    que se elija a ojo para el terreno puede seguir sin coincidir con el suyo. La solución de verdad no
///    es adivinar mejor el color, es no depender de que coincida: los islotes menores ya NO llevan piezas
///    sueltas de `Shore01` (`CercaDeAlgunaIslaMenor` las excluye de `BuildCoastline`) — en un islote
///    pequeño una pieza aislada con un tono distinto se ve como un parche pegado, cosa que no pasa en la
///    costa larga del continente (que sí las conserva). De paso, los 4 colores base de las capas se
///    re-tomaron MUESTREANDO DE VERDAD píxeles reales de `Base Map.png` con un script (antes era una
///    aproximación a ojo del aspecto general del atlas).
/// 4. **"Sigo viendo como la isla brilla."** `smoothness` de las capas baja de 0.05 a 0 (el mínimo). Si
///    persiste tras esto, ya no es la capa de terreno — apunta a otra cosa (reflection probes del propio
///    Terrain, ambient probe/skybox) que esta sesión no puede revisar sin abrir el Editor.
/// 5. **Archipiélago con puentes + capítulo extra, vs. el mapa de referencia "Isla de Eldoria".** Raúl
///    propuso convertir el archipiélago en varias islas conectadas por puentes con casitas y gente
///    viviendo ahí (capítulo extra), y luego compartió un mapa de referencia ("Isla de Eldoria") que
///    plantea algo distinto: UNA sola isla grande con todo (pueblo inicial, pueblo de montaña, Bosque
///    Prohibido, pueblo pesquero — este último pegado al continente, no en isla aparte) más varios
///    islotes rocosos pequeños alrededor, sobre todo decorativos, más un río bajando de la montaña y
///    cascadas cayendo al mar por un lado. Estas dos ideas no encajan del todo entre sí (archipiélago
///    habitable grande vs. islote pesquero unido a tierra + roquedales decorativos), así que no se ha
///    tocado el diseño del archipiélago todavía a la espera de que Raúl aclare qué dirección seguir — ver
///    conversación/pregunta.
///
/// *** REVISIÓN 9 (mismo día, decisión de Raúl) *** — la revisión 8 dejó una pregunta abierta:
/// archipiélago grande con puentes (capítulo extra) vs. seguir el mapa de referencia "Isla de Eldoria".
/// Raúl eligió **Isla de Eldoria: una sola isla con todo**. Cambio aplicado: "Isla del Puerto" (el islote
/// que iba a ser el futuro pueblo pesquero) se retira del array `IslasMenores` y se sustituye por
/// `PuebloPesqueroCentro`, una zona COSTERA normal en el propio continente (mismo rincón sureste, para no
/// perder la orientación ya pensada) — con muelle/faro en la orilla, no en una isla aparte, tratada igual
/// que el resto de pueblos (aplanado vía `ZonasConstruiblesFlatten`, comprobación de altura vía
/// `VerificarAnclasSobreElNivelDelMar`). La Isla de la Piedra Ancestral se queda como único islote menor —
/// encaja con los "islotes rocosos, sobre todo decorativos" del mapa de referencia, y sigue teniendo
/// sentido como sitio aparte para el portal/secreto antiguo. Pendiente para más adelante (no en el Paso
/// 1): el río desde la montaña hasta la costa con cascada al mar que muestra el mapa de referencia — encaja
/// con el Paso 3 ya planeado ("árboles, puentes, cascadas"), y con las piezas de río/cascada/lago que ya
/// están inventariadas (`RiverRoadLakeFall`). También queda pendiente si añadir algún islote rocoso
/// puramente decorativo (sin meseta, sin zona) cerca de la costa, más como dressing del Paso 3-4 que como
/// forma de terreno de este paso.
///
/// *** REVISIÓN 10 (mismo día, tras ver una captura cenital de la revisión 9) *** — Raúl: "esto es lo que
/// has hecho y no se parece en nada al mapa que te he pasado" (isla circular, tono pardo/oliva casi
/// uniforme, sin verde/nieve visibles, playa apenas perceptible) y, ya sobre el fondo, el pedido que
/// cambia más cosas de esta revisión: "si te fijas en el mapa no hay tantas montañas, la isla actual es
/// todo montaña, no hay descanso. Si te fijas no es circular, tiene una formita. Arriba el montañón y va
/// bajando el caminito. A cada lado del camino hay algo y abajo playa real. Se parece más al primer diseño
/// que no era isla." Tres cosas, la tercera bastante más grande que las dos primeras:
/// 1. **Bug real de umbrales roca/nieve — la roca se comía casi toda la isla.** `RockHeightThreshold01`
///    (0.20 desde la revisión 6) estaba calibrado contra el máximo absoluto del mapa, no contra la altura
///    "ambiente" que la propia curva de subida ya alcanzaba a media distancia entre costa y centro (~0.37-
///    0.40 sin contar ni un pico) — así que la roca reclamaba casi todo el interior salvo una franja fina
///    junto a la costa, dejando la hierba reducida a casi nada (de ahí el tono pardo/oliva uniforme, sin
///    verde). Umbrales recalibrados contra la altura ambiente real del interior (roca 0.58, nieve 0.78 —
///    ver el comentario junto a esas constantes).
/// 2. **Litoral demasiado circular/regular.** Amplitud del ruido de costa subida y se añade una segunda
///    capa a más frecuencia (bahías grandes + mordiscos pequeños encima) y la franja de arena se ensancha
///    un poco (`BeachBlend01` 0.035→0.05) para que se lea mejor desde arriba.
/// 3. **Cambio de fondo: de curva RADIAL (desde el centro del mapa, revisiones 3-9) a curva DIRECCIONAL
///    norte→sur** — esto es lo que de verdad pedía el mensaje de Raúl, y toca casi todo lo que depende de
///    la forma del terreno. El modelo radial hacía que "cerca del centro" fuera sinónimo de "alto y
///    montañoso" en todas direcciones a la vez — de ahí "todo es montaña, no hay descanso": prácticamente
///    cualquier punto no pegado a la costa tenía ya bastante altura. Vuelve la lógica del diseño original
///    v1/v2 (franja llana al sur con Pueblo Bajo/Castillo/Bosque, ladera que sube, macizo con nieve al
///    norte — ver cabecera del archivo) pero conservando el marco de "isla rodeada de mar" que gusta desde
///    la revisión 3, en vez de que el mundo simplemente termine en el borde del terreno. Piezas nuevas:
///    `MountainFactor` (0 en la franja llana sur, 1 en el núcleo del macizo norte — controla TANTO la
///    altura objetivo COMO cuánta aspereza/ruido se aplica, así la franja sur queda lisa de verdad, no solo
///    baja) y `LongitudinalEdgeMask`/`LateralEdgeMask`/`CoastlineNoise2D` (fundido a mar por POSICIÓN, no
///    por ángulo respecto a un centro — ya no tiene sentido con una silueta direccional). La silueta lateral
///    (`LateralHalfWidthAt`) se estrecha de la mitad sur (ancha, pueblos) hacia el norte (promontorio del
///    macizo, no un bloque rectangular) — esto es lo que hace que "no sea circular" y "tenga una formita".
///    Pueblo de Nieve y el pico protagonista se recolocan del centro del mapa al extremo norte de verdad; el
///    camino pasa a terminar ahí en vez de en el centro. La esquina oeste del Bosque Prohibido se recoloca
///    un poco más adentro (margen de seguridad extra contra el ruido de costa lateral, con la silueta ahora
///    estrechándose hacia el norte). El resto de anclas (Pueblo Bajo, Castillo, Pueblo de Will, Pueblo
///    Pesquero, zonas de descanso, Isla de la Piedra Ancestral) se quedan en su sitio — todas caen ya
///    dentro de la franja llana sur o cerca de la costa, y las comprobaciones de seguridad ya existentes
///    (`VerificarAnclasSobreElNivelDelMar`, `VerificarIslasMenores`) siguen funcionando igual (muestrean
///    `ComputeHeight01` real, no dependen de si la curva de debajo es radial o direccional) — si alguna
///    queda justa de margen con la nueva silueta, el log de la próxima ejecución lo dirá.
///
/// *** REVISIÓN 11 (mismo día, tras ver la primera captura real de la revisión 10 en el Editor) *** —
/// la forma ya sale bien (no circular, base ancha al sur, promontorio al norte, camino bajando — se
/// confirmó cruzando la posición de los marcadores de zona conocidos contra la captura), pero dos bugs
/// nuevos, los dos concentrados junto a la punta norte donde la tierra es más estrecha:
/// 1. **Costa fragmentada en dedos/islotes sueltos cerca del promontorio norte.** La amplitud de ruido de
///    costa (`CoastNoiseAmplitude`+`2`, `LateralNoiseAmplitude`+`2`) está calibrada para la franja ancha
///    del sur; junto al norte, donde `LateralHalfWidthAt` estrecha la tierra a menos de la mitad, ese
///    mismo ruido es proporcionalmente enorme y cruza el umbral mar/tierra varias veces en un tramo corto
///    — en vez de una costa ondulada continua, salen penínsulas finas y trozos de tierra desconectados
///    flotando en el mar. Fix: `CoastNoiseScaleAt` escala la amplitud de ruido (longitudinal y lateral)
///    según cuánto ancho de tierra hay en esa fila (`LateralHalfWidthAt(zNorm) / LateralHalfWidthSouth`),
///    con un suelo (`CoastNoiseFloor` = 0.35) para no dejar la costa perfectamente lisa junto al pico. La
///    mitad sur no se toca (ahí el ancho es máximo, así que el factor de escala ya es ~1).
/// 2. **Cero color visible en todo el terreno, ni siquiera en el propio pico protagonista** (que según el
///    umbral de nieve debería salir casi blanco). Causa: la escena se crea vacía
///    (`NewSceneSetup.EmptyScene`, cero GameObjects) y este archivo nunca añadía ni un Directional Light
///    ni RenderSettings de ambiente — sin luz, el alphamap de las 4 capas (arena/hierba/roca/nieve) puede
///    estar perfectamente calculado y aun así no distinguirse nada a simple vista. Fix: `BuildLighting`
///    añade un Directional Light + ambiente trilight básicos, sin depender de ningún asset del proyecto.
///
/// *** REVISIÓN 12 (mismo día, tras ver la primera captura real de la revisión 11 en el Editor) *** — la
/// luz ya funciona (se ve sombra/contraste de verdad en el pico), pero dos cosas seguían mal:
/// 1. **Toda la isla, incluido el propio pico, sale del color de la ARENA — nada de verde/roca/nieve.**
///    Bug real de calibración: `baseHeight01 = RiseCurve(...) * PeakRiseFraction` — con `FlatBaseFraction`
///    en 0.22 y `PeakRiseFraction` en 0.50, la franja llana sur quedaba a una altura BASE real de
///    0.22×0.50 = 0.11, prácticamente pegada al techo de la banda de arena (`SeaLevel01`+`BeachBlend01` =
///    0.105) — el cálculo de la revisión 10 fijó 0.22 sin tener en cuenta el factor ×0.50 que se le aplica
///    después. Subido `FlatBaseFraction` a 0.42 (altura base real 0.21, el doble de la banda de arena) sin
///    tocar `PeakRiseFraction` ni los umbrales de roca/nieve.
/// 2. **La costa norte seguía fragmentándose en dedos/islotes** a pesar de escalar el ruido en la revisión
///    11 — quedaba una causa más grande: `LateralCoastBand` (0.11, ancho FIJO) pasa de ser un 25% del ancho
///    de tierra en la mitad sur (halfWidth 0.44) a más de la MITAD del ancho junto al norte (halfWidth
///    0.20) — gran parte de la península ya estaba dentro de la zona de erosión del smoothstep antes de
///    tocar nada de ruido. Fix en `LateralEdgeMask`: la banda se limita a como mucho la mitad del ancho
///    local (`halfWidth * 0.5`), así siempre queda un núcleo sólido sea cual sea el ancho en esa fila.
///
/// *** REVISIÓN 13 (mismo día, tras ver la primera captura real de la revisión 12 en el Editor) *** — la
/// captura salió IDÉNTICA en color a la de antes, ni un pixel de verde, pese al arreglo de altura de la
/// revisión 12. Como cambiar datos otra vez a ciegas ya no era razonable, se añadió un diagnóstico al log:
/// altura + pesos de arena/hierba/roca/nieve calculados en Pueblo Bajo, Castillo y el Pico, para saber si
/// el problema estaba en los datos o en cómo Unity los pinta. También se añadió `terrain.Flush()` por si
/// el Editor no refrescaba solo tras `SetAlphamaps` (no fue la causa, pero no hacía daño probarlo).
///
/// *** REVISIÓN 14 (mismo día, tras leer el log de diagnóstico de la revisión 13 junto a su captura) ***
/// — el log confirmó los datos SIN NINGUNA duda: Pueblo Bajo y Castillo con hierba=1.00, el Pico con
/// nieve=1.00 — y la captura, al lado, seguía siendo 100% del color de la Arena en TODA la isla, pico
/// incluido. Con los datos descartados como causa, el problema solo puede estar en cómo Unity pinta ese
/// alphamap. La sospecha más probable: `Terrain.CreateTerrainGameObject` no le estaba asignando al Terrain
/// un material capaz de mezclar 4 capas para URP (el render pipeline activo del proyecto), cayendo a un
/// material/fallback que solo pinta la capa base. Arreglo: se asigna explícitamente un material con el
/// shader `Universal Render Pipeline/Terrain/Lit` en vez de confiar en el automático — si el automático ya
/// estaba bien, esto no cambia nada visible; si era la causa real, la corrige de raíz.
///
/// *** REVISIÓN 15 (mismo día, tras ver que la revisión 14 tampoco cambió NADA — misma captura, mismo log
/// de datos correctos) *** — con el material también descartado a simple vista, tocaba dejar de intentar
/// arreglarlo a ciegas por tercera vez y mirar una diferencia real de código, no una suposición: la función
/// `GetOrCreateLayer` (capas de textura) SÍ comprueba si el asset ya existe en disco de una ejecución
/// anterior y lo sobrescribe en el sitio — pero el `TerrainData` del terreno NUNCA hacía esa comprobación:
/// cada ejecución creaba un `TerrainData` nuevo en memoria y llamaba a `AssetDatabase.CreateAsset` en la
/// MISMA ruta fija (`MundoNuevo_TerrainData.asset`) sin borrar antes el archivo de la ejecución anterior.
/// `CreateAsset` no sobrescribe de forma limpia un asset ya existente en ese path: el heightmap (un array
/// plano, va directo en el asset principal) sí parece colarse igualmente, pero los alphamaps se guardan
/// como sub-assets de textura aparte, y esos sub-assets viejos —100% arena, de la primerísima ejecución de
/// este script, antes de que el cálculo de pesos estuviera siquiera bien— se quedan colgando del mismo
/// archivo en vez de sustituirse. Encaja exactamente con lo observado en las 4 capturas seguidas: la FORMA
/// cambia en cada revisión (el heightmap sí se actualiza) pero el COLOR nunca (los alphamaps, no). Arreglo:
/// se borra el asset de `TerrainData` existente (si lo hay) antes de crear el nuevo, igual que ya se hace
/// con todo lo demás del contenedor ("destruir y reconstruir"). Además se añade una segunda tanda de
/// diagnóstico que, a diferencia de la de la revisión 13 (que calculaba los pesos con la fórmula, sin tocar
/// Unity para nada), lee los alphamaps YA GUARDADOS en el TerrainData real tras crear el Terrain — si este
/// arreglo es la causa real, ambas tandas de diagnóstico deberían coincidir por fin; si no lo es, esta
/// segunda lectura lo dirá sin necesidad de otra ronda a ciegas.
///
/// Detalle completo del planteamiento y el inventario de assets usado (ninguno nuevo, todo ya
/// importado): propuesta-mundo-nuevo-terreno-montanas-pueblos-2026-09-10.md (proyecto de Claude).
///
/// *** ESCENA NUEVA, NO TOCA MainWorld.unity ***
///
/// Uso: El Sendero → Mundo Nuevo → Paso 1 - Terreno y Zonas.
/// Idempotente por regeneración completa: cada ejecución borra y reconstruye el contenedor entero
/// (terreno incluido). Los ajustes van en las constantes de arriba de este archivo.
/// </summary>
public static class NuevoMundoTerrenoBuilder
{
    private const string ScenePath = "Assets/Scenes/Worlds/MundoNuevo_Prototipo.unity";
    private const string RootName = "MUNDO_NUEVO_ROOT";
    private const string TerrainFolder = "Assets/Terrain/MundoNuevo";
    private const int Seed = 20260910;

    // --- Tamaño del terreno (cuadrado, para que la isla no salga alargada) ---
    // Revisión 6: "la haría incluso más grande" — de 750 a 950 (continente notablemente más grande).
    private const float TerrainWidth = 950f;
    private const float TerrainLength = 950f;
    private const float TerrainMaxHeight = 220f; // subida respecto a la v2 para dejar hueco de verdad al pico protagonista
    private const int HeightmapResolution = 513; // 2^n + 1
    private const int AlphamapResolution = 512;

    // --- Continente: modelo DIRECCIONAL norte→sur (revisión 10), no radial-desde-el-centro (revisiones 3-9) ---
    // Raúl, tras ver la revisión 9 en el Editor: "no hay tantas montañas, la isla actual es todo montaña,
    // no hay descanso. No es circular, tiene una formita. Arriba el montañón y va bajando el caminito. A
    // cada lado del camino hay algo y abajo playa real. Se parece más al primer diseño que no era isla."
    // Vuelve la lógica del diseño original v1/v2 (llano al sur → ladera → pico al norte, ver cabecera del
    // archivo) pero conservando el marco de "isla rodeada de mar" que gusta desde la revisión 3: el macizo
    // montañoso se concentra cerca del extremo norte (`MountainStartZ`→`NorthPeakZ`) y desde ahí hacia el
    // sur (zNorm creciente) el terreno se queda llano de verdad — ya no depende de la distancia al centro
    // del mapa, sino de en qué punto del recorrido norte-sur se está. La silueta deja de ser un círculo:
    // `LateralHalfWidthAt` estrecha la tierra según se acerca al norte (promontorio de montaña, no un
    // bloque rectangular) y `LongitudinalEdgeMask`/`LateralEdgeMask` funden a mar en los 4 bordes con ruido
    // de posición (`CoastlineNoise2D`), no en función del ángulo respecto a un centro (que ya no pinta nada
    // aquí). Ver esas funciones y `MountainFactor`.
    private const float PeakRiseFraction = 0.50f; // altura "normal" en el núcleo del macizo, como fracción de TerrainMaxHeight — deja hueco por encima para el pico protagonista
    // Revisión 12: 0.22 → 0.42. Bug real encontrado tras ver la captura real de la revisión 11 (ya con luz
    // arreglada): toda la isla, incluido el propio pico, salía del color de la ARENA — ni una pizca de
    // verde/roca/nieve. Causa: `baseHeight01 = RiseCurve(...) * PeakRiseFraction` (ComputeHeight01) — con
    // `FlatBaseFraction` en 0.22 y `PeakRiseFraction` en 0.50, la franja llana sur quedaba a una altura
    // BASE real de 0.22*0.50 = 0.11, prácticamente PEGADA al techo de la banda de arena
    // (`SeaLevel01`+`BeachBlend01` = 0.055+0.05 = 0.105) — el ruido de rugosidad ahí (±0.05, muy amortiguado
    // porque `MountainFactor`=0 en la franja llana) apenas la sacaba de la banda de arena, así que CASI TODA
    // la franja llana (la mayoría del mapa bajo el modelo direccional) se pintaba de arena en vez de hierba.
    // El cálculo de la revisión 10 ("0.22 es una altura baja pero no a nivel del mar") no tuvo en cuenta el
    // factor ×0.50 de `PeakRiseFraction` al fijar el número. Subido a 0.42 → altura base real 0.21, bien
    // por encima de la banda de arena (casi el doble), sin tocar `PeakRiseFraction` (que sigue controlando
    // la altura del macizo) ni los umbrales de roca/nieve (que ya estaban bien calibrados para la altura del
    // macizo, no para la franja llana).
    private const float FlatBaseFraction = 0.42f; // altura base en la franja llana sur (fracción de PeakRiseFraction) — Raúl: "no hay descanso"; ya no es 0 en todas partes salvo picos, es una franja baja pero no plana-a-nivel-del-mar
    private const float MountainStartZ = 0.56f;   // desde aquí hacia el sur (zNorm creciente) el factor de montaña es 0 — todo llano, aquí viven Pueblo Bajo/Castillo/Bosque/Will/Pesquero
    private const float NorthPeakZ = 0.17f;       // núcleo del macizo, cerca del extremo norte del mapa

    // --- Silueta lateral (este/oeste): más ancha en la mitad sur (pueblos), se estrecha hacia el norte —
    // así el macizo lee como un promontorio, no como un bloque rectangular, y la isla entera "tiene una
    // formita" en vez de ser un círculo o un cuadrado. Ver `LateralHalfWidthAt`.
    private const float LateralHalfWidthSouth = 0.44f;
    private const float LateralHalfWidthNorth = 0.20f;
    private const float LateralTaperStartZ = 0.70f; // a partir de aquí (hacia el sur) ya está al ancho máximo
    private const float LateralTaperEndZ = 0.15f;   // aquí ya está al ancho mínimo (norte)

    // --- Fundido a mar en los bordes (ancho de la franja de transición tierra→mar) ---
    private const float NorthCoastInnerZ = 0.05f; // por debajo de esto (zNorm) ya es mar — costa norte, junto al macizo
    private const float SouthCoastInnerZ = 0.95f; // por encima de esto ya es mar — costa sur, la playa real que pidió Raúl
    private const float LongitudinalCoastBand = 0.09f;
    private const float LateralCoastBand = 0.11f;

    // --- Ruido de costa: rompe la línea recta/el círculo perfecto (bahías/penínsulas), ahora muestreado
    // por POSICIÓN (`CoastlineNoise2D`), no por ángulo respecto a un centro — un ángulo ya no tiene sentido
    // para una silueta direccional. Dos juegos de amplitud: el longitudinal (norte/sur) puede ser más
    // fuerte, ahí es donde de verdad se quiere ver la costa orgánica del macizo y de la playa sur; el
    // lateral se deja más suave a propósito — un mordisco lateral grande podría comerse alguna de las
    // anclas de pueblo ya verificadas (Bosque Prohibido, Pueblo de Will, Pueblo Pesquero), que no están
    // pensadas para tener margen de sobra en el eje X.
    private const float CoastNoiseAmplitude = 0.10f;
    private const float CoastNoiseFrequency = 2.3f;
    private const float CoastNoiseAmplitude2 = 0.035f;
    private const float CoastNoiseFrequency2 = 5.4f;
    private const float LateralNoiseAmplitude = 0.035f;
    private const float LateralNoiseAmplitude2 = 0.015f;
    // Revisión 11: fracción mínima de amplitud de ruido que se conserva incluso en el punto más estrecho
    // (la punta norte, junto al pico) — 0 apagaría el ruido del todo ahí (costa perfectamente lisa, se ve
    // artificial); un valor bajo pero no nulo deja algo de silueta orgánica sin fragmentar la lengua de
    // tierra en dedos/islotes. Ver `CoastNoiseScaleAt`.
    private const float CoastNoiseFloor = 0.35f;

    // --- Mar y costa ---
    private const float SeaLevel01 = 0.055f;   // nivel del mar, como fracción de TerrainMaxHeight
    private const float BeachBlend01 = 0.05f;  // revisión 10: ensanchada (antes 0.035) — franja de arena más visible desde arriba
    private const string TinyWorldLandMass = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/LandMass";
    private const float OceanCoverageFactor = 2.4f; // objetivo de tamaño del océano, relativo a la DIAGONAL del terreno — no una escala a ciegas
    private const float OceanFallbackScale = 8f; // solo si el prefab no tiene Renderer (no debería pasar)
    private const int CoastlinePieceCount = 28;
    private const float CoastlineBand = 0.012f;

    // --- Islotes menores (bajos, no montañas) — revisión 7, reducido a uno solo en la revisión 9 ---
    // Revisión 7: había dos islotes (uno de ellos "Isla del Puerto", futuro pueblo pesquero). Revisión 9:
    // Raúl, tras ver el mapa de referencia "Isla de Eldoria", eligió esa dirección — UNA sola isla grande
    // con todo, con el pueblo pesquero pegado al continente (no en isla aparte). "Isla del Puerto" se
    // retira de este array y se sustituye por `PuebloPesqueroCentro`, una zona costera normal como el resto
    // de pueblos (ver esa constante y `ZonasConstruiblesFlatten`). Solo queda la Isla de la Piedra
    // Ancestral, que sí sigue teniendo sentido como islote separado (portal/secreto antiguo, no un pueblo)
    // — encaja con los "islotes rocosos alrededor" del mapa de referencia. Cada islote tiene una MESETA
    // plana real en el centro (`SmallIslandPlateauFraction` del radio) y solo el aro exterior baja en
    // pendiente hacia el mar, con una rampa hacia el continente (ver `SmallIslandRampPlateauFraction`).
    private struct IslaMenor
    {
        public string Nombre;
        public Vector2 CentroNorm;
        public float RadioNorm;
        public float AlturaObjetivo01;
        public Color ColorZona;
    }

    private static readonly IslaMenor[] IslasMenores =
    {
        // Isla de la Piedra Ancestral (revisión 7, idea de Raúl): un islote propio para la Piedra
        // Ancestral y la apertura del portal al Sendero. Hoy ese combate/ritual (`PiedraAncestralGuardianBuilder.cs`,
        // capítulo XVI de la novela) vive en una escena aparte sin sitio físico en el mundo abierto —
        // este islote, separado y solo alcanzable a nado (o en barca más adelante), le da uno: un lugar
        // apartado y algo misterioso encaja bien con lo que es (un secreto antiguo, guardado). Radio
        // deliberadamente pequeño (islote de altar, no un pueblo).
        new IslaMenor
        {
            Nombre = "Isla de la Piedra Ancestral", CentroNorm = new Vector2(0.97f, 0.80f), RadioNorm = 0.035f,
            AlturaObjetivo01 = 0.14f, ColorZona = new Color(0.55f, 0.30f, 0.80f),
        },
    };

    // Fracción del radio de cada islote que queda COMPLETAMENTE LLANA (meseta) antes de empezar a bajar
    // hacia el mar — esto es lo que arregla el "no podemos hacer nada ahí" (antes 0 = ningún llano, todo cono).
    private const float SmallIslandPlateauFraction = 0.55f;

    // Revisión 7: "las islas deberían tener el típico camino de la playa a la meseta". Dentro de un sector
    // angular (hacia el continente — por donde llega a nado quien viene desde tierra firme), la banda de
    // subida se ensancha mucho (fracción mucho menor = la pendiente se reparte en más distancia = un
    // camino/rampa caminable en vez de un banco vertical). Fuera de ese sector, el islote sigue siendo un
    // acantilado normal hacia el mar — no hace falta que TODO el borde sea rampa, solo un acceso.
    private const float SmallIslandRampPlateauFraction = 0.12f;
    private const float SmallIslandRampHalfAngleDeg = 22f;

    // --- Bandas de altura para roca/nieve (la nieve depende de la ALTURA real, no de la posición) ---
    // Revisión 10: la recalibración de la revisión 6 se quedó corta al revés de lo que parecía — con
    // `RockHeightThreshold01=0.20`, la roca empezaba a reclamar terreno en cuanto se superaba una altura
    // que buena parte del INTERIOR del continente ya alcanza solo por la curva de subida normal (a mitad
    // de camino entre costa y centro la altura "ambiente" ronda ya 0.37-0.40, sin contar ni un pico), así
    // que la roca se comía casi toda la isla salvo una franja fina junto a la costa — de ahí que en las
    // capturas de la revisión 10 la isla se vea de un tono pardo/oliva casi uniforme, sin apenas verde.
    // Ahora los umbrales se recalibran contra la altura "ambiente" típica del interior (que ronda 0.5 en el
    // centro, antes de sumar el pico protagonista) en vez de contra el máximo absoluto del mapa: la roca
    // solo debería aparecer en crestas de verdad altas, y la nieve casi en exclusiva en el pico
    // protagonista (altura fija 0.95) y en las puntas más extremas del ruido de cresta cerca del centro.
    private const float RockHeightThreshold01 = 0.58f;
    private const float RockBlend01 = 0.15f;
    private const float SnowHeightThreshold01 = 0.78f;
    private const float SnowBlend01 = 0.12f;

    // --- Zonas de descanso: claros llanos donde se amortigua el ruido de montaña (no todas a la misma
    // altura — cerca de la costa y a media montaña), pedido de Raúl tras ver todo el continente montañoso ---
    private static readonly Vector2[] RestZoneNorm =
    {
        new Vector2(0.30f, 0.42f), // ladera oeste, altura media
        new Vector2(0.70f, 0.60f), // ladera este, altura media
        new Vector2(0.38f, 0.78f), // claro costero cerca del Pueblo Bajo (lado oeste)
        new Vector2(0.65f, 0.78f), // claro costero cerca del Pueblo Bajo (lado este)
    };
    private const float RestZoneRadiusNorm = 0.10f;

    // --- Pico protagonista ("montaña super grande") — junto al Pueblo de Nieve, no encima. Revisión 10:
    // recolocado al norte (antes en el centro del mapa, era el núcleo de la vieja curva radial) — mismo
    // desplazamiento relativo a Pueblo de Nieve que ya tenía (+0.11 en X, -0.07 en Z aprox.), conserva la
    // idea original, solo cambia dónde está "el norte" ahora.
    private static readonly Vector2 HeroPeakNorm = new Vector2(0.63f, 0.14f);
    private const float HeroPeakRadiusNorm = 0.11f;
    private const float HeroPeakTargetHeight01 = 0.95f;

    // --- Camino: del Pueblo Bajo (costa sur, la playa real) al Pueblo de Nieve (macizo norte) — revisión
    // 10: vuelve a ser un recorrido sur→norte de verdad (como el diseño original v1/v2), ya no termina en
    // el centro del mapa sino junto al nuevo Pueblo de Nieve, cerca del macizo.
    private static readonly Vector2 PathStartNorm = new Vector2(0.5f, 0.85f);
    private static readonly Vector2 PathEndNorm = new Vector2(0.52f, 0.21f);
    private const int PathPolylineSamples = 60;
    private const float PathHalfWidth = 13f;
    private const float PathFalloff = 34f;
    private const float PathGrooveDepth01 = 0.05f;
    private static readonly float[] RavineTs = { 0.32f, 0.66f };
    private const float RavineWidthT = 0.05f;
    private const float RavineHalfWidth = 55f;
    private const float RavineFalloff = 30f;
    private const float RavineDepth01 = 0.11f;
    private const float RoadSpacingWorld = 18f;

    private static List<Vector2> _pathPolylineWorld;

    // --- Pueblos y castillo (fracción de ancho/largo del terreno, 0..1) ---
    // Recolocados en la revisión 5: con el continente ensanchado hay margen de sobra, pero estas
    // posiciones ya están verificadas a mano contra la nueva curva (ver VerificarAnclasSobreElNivelDelMar).
    private static readonly Vector2 PuebloBajoCentro = new Vector2(0.5f, 0.85f);
    private const float PuebloBajoRadio = 40f;
    private static readonly string[] PuebloBajoBuildings = { "BuildingAT02", "BuildingAT03", "BuildingAT15", "BuildingAT22", "BuildingAT23", "BuildingAT24", "BuildingAT29" };

    private static readonly Vector2 CastilloCentro = new Vector2(0.5f, 0.71f);
    private static readonly string[] CastilloBuildings = { "BuildingAT48", "BuildingAT55" };

    // Revisión 10: recolocado del centro del mapa (0.5, 0.5, núcleo de la vieja curva radial) al extremo
    // norte de verdad (cerca de `NorthPeakZ`), siguiendo el pedido de Raúl de volver a un recorrido
    // direccional sur→norte con el macizo concentrado arriba del todo.
    private static readonly Vector2 PuebloNieveCentro = new Vector2(0.52f, 0.19f);
    private const float PuebloNieveRadio = 24f;
    private static readonly string[] PuebloNieveBuildings = { "BuildingAT22", "BuildingAT23", "BuildingAT24", "BuildingAT15" };

    // --- Pueblo de Will (revisión 6): zona nueva, distinta de Pueblo Bajo — ladera este, costera, lejos
    // del resto de zonas y de la isla secundaria. Candidata a vestirse con la escena demo 08 del pack.
    private static readonly Vector2 PuebloDeWillCentro = new Vector2(0.83f, 0.66f);
    private const float PuebloDeWillRadio = 30f;

    // --- Pueblo Pesquero (revisión 9): Raúl eligió seguir el mapa de referencia "Isla de Eldoria" —
    // una sola isla con todo, con el pueblo pesquero PEGADO al continente (muelle/faro en la propia costa),
    // no en una isla aparte. Sustituye a lo que antes era "Isla del Puerto" en `IslasMenores` — mismo rincón
    // sureste del mapa (para no perder la orientación ya pensada), pero ahora es tierra firme costera de
    // verdad, con su propio aplanado como el resto de pueblos (ver `ZonasConstruiblesFlatten`).
    private static readonly Vector2 PuebloPesqueroCentro = new Vector2(0.75f, 0.82f);
    private const float PuebloPesqueroRadio = 35f;

    // --- Bosque Prohibido: laberinto de sendas, franja costera suroeste ---
    // Revisión 10: esquina oeste recolocada un poco más adentro (0.13→0.18) — con la silueta lateral ahora
    // dependiente de `LateralHalfWidthAt` (que se ESTRECHA hacia el norte, y la esquina NO del Bosque cae
    // en zNorm=0.55, ya empezando a estrechar) hacía falta más margen del que daba la posición original
    // frente al ruido de costa lateral, para no arriesgarse a que esa esquina quedara bajo el nivel del mar.
    private static readonly Vector2 BosqueAreaMinNorm = new Vector2(0.18f, 0.55f);
    private static readonly Vector2 BosqueAreaMaxNorm = new Vector2(0.35f, 0.67f);
    private const int BosqueCols = 5;
    private const int BosqueRows = 4;
    private const int BosqueEntradaCol = BosqueCols - 1;
    private const int BosqueEntradaRow = 1;
    private const int BosqueExtraLoopConnections = 3;
    private const float BosqueWallSpacing = 3.5f;
    private const string BosqueWallTreeName = "Tree02_a01";

    private static readonly string[] BosqueDecoracion =
    {
        "Tree01_a01", "Tree01_b01", "Tree03_a01", "Tree03_b01", "Tree04_a01", "Tree04_d01",
        "Tree05_a01", "Tree06_a01", "Tree07_a01",
        "Mushroom01_a01", "Mushroom02_a01", "Mushroom03_a01", "Mushroom04_a01",
        "Flower01_a01", "Flower02_a01", "Flower03_a01", "Flower04_a01", "Flower05_a01",
        "Plant01_a01", "Plant02_a01", "Plant04_a01",
    };

    // N=0, E=1, S=2, O=3 — mismo convenio que WillTrialMazeBuilder.
    private static readonly int[] DX = { 0, 1, 0, -1 };
    private static readonly int[] DZ = { 1, 0, -1, 0 };

    // --- Rutas de assets ---
    private const string KingdomBridge = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Bridge";
    private const string KingdomBuildingCombo = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Building Combination";
    private const string KingdomVegetation = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation";
    private const string TinyWorldRoads = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/RiverRoadLakeFall";
    private const string AmbientPresetMountainsPath = "Assets/AmbientPresets/AmbientPreset_Mountains.asset";

    private static readonly string[] RoadNames =
    {
        "RoadA01", "RoadA02", "RoadB01", "RoadB02", "RoadB03", "RoadB04",
        "RoadC01", "RoadC02", "RoadC03", "RoadC04", "RoadD01", "RoadD02",
        "RoadE01", "RoadE02", "RoadE03",
    };

    private static readonly string[] BridgeNames = { "Bridge01_a01", "Bridge02_b01", "Bridge03_a01", "Bridge04_c01", "Bridge05_a01" };

    private class CeldaBosque { public bool visitada; public int conexiones; }

    [MenuItem("El Sendero/Mundo Nuevo/Paso 1 - Terreno y Zonas")]
    public static void GenerarMundoNuevoPaso1()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("=== Mundo Nuevo — Paso 1: Terreno y Zonas (revisión 15 — TerrainData viejo borrado antes de crear el nuevo) ===");
        log.AppendLine("Solo terreno + océano/costa + marcadores de zona. Caminos/árboles/pueblos/triggers van en los siguientes pasos.");

        Scene scene = OpenOrCreateScene();

        GameObject root = GameObject.Find(RootName);
        if (root != null) Object.DestroyImmediate(root);
        root = new GameObject(RootName);

        var gruposTerreno = new GameObject("Terreno").transform; gruposTerreno.SetParent(root.transform);
        var gruposCosta = new GameObject("Océano y Costa").transform; gruposCosta.SetParent(root.transform);
        var gruposZonas = new GameObject("Zonas (vista previa — paso 1)").transform; gruposZonas.SetParent(root.transform);

        BuildLighting(root.transform, log);
        Terrain terrain = BuildTerrain(gruposTerreno, log);

        BuildCoastline(terrain, gruposCosta, log);
        VerificarAnclasSobreElNivelDelMar(log);
        VerificarIslasMenores(log);
        BuildZoneMarkers(terrain, gruposZonas, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine("--- Paso 1 listo. Escena guardada en " + ScenePath + " — MainWorld.unity no se ha tocado. ---");
        log.AppendLine("Cuando el terreno y las zonas estén bien, seguimos con el Paso 2 (caminos).");
        Debug.Log(log.ToString());
    }

    // ==================== ILUMINACIÓN ====================

    /// <summary>*** REVISIÓN 11 *** La escena de Mundo Nuevo se crea vacía (`NewSceneSetup.EmptyScene`) para
    /// no arrastrar nada de MainWorld — pero eso también significa CERO luz: sin Directional Light ni
    /// RenderSettings de ambiente, el terreno se renderiza plano/oscuro y las 4 capas de textura (arena,
    /// hierba, roca, nieve) no se distinguen entre sí aunque el alphamap esté bien calculado — justo lo que
    /// se vio en la captura de la revisión 10 (ni rastro de nieve, ni siquiera en el propio pico
    /// protagonista, donde el umbral la da por hecha). Luz + ambiente básicos, sin depender de ningún asset
    /// del proyecto (a diferencia de `SetupSnowAmbient`, que engancha un `AmbientPreset` ya existente para
    /// el Pueblo de Nieve más adelante) — esto es solo para que el Paso 1 sea evaluable por sí solo.</summary>
    private static void BuildLighting(Transform parent, System.Text.StringBuilder log)
    {
        var sunGo = new GameObject("Sol (Directional Light)");
        sunGo.transform.SetParent(parent);
        sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.957f, 0.898f); // blanco cálido suave, no tiñe las capas del terreno
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.596f, 0.686f, 0.784f);
        RenderSettings.ambientEquatorColor = new Color(0.443f, 0.443f, 0.443f);
        RenderSettings.ambientGroundColor = new Color(0.278f, 0.259f, 0.235f);
        RenderSettings.ambientIntensity = 1f;

        log.AppendLine("Luz: Directional Light + ambiente trilight básicos añadidos (la escena se crea vacía, sin ninguno de los dos) — sin esto el terreno se ve plano y sin color aunque el alphamap esté bien calculado.");
    }

    // ==================== TERRENO ====================

    private static Terrain BuildTerrain(Transform parent, System.Text.StringBuilder log)
    {
        EnsureFolder(TerrainFolder);
        EnsureFolder($"{TerrainFolder}/Layers");

        _pathPolylineWorld = BuildPathPolylineWorld(PathPolylineSamples);

        // Revisión 6: colores tomados de la paleta plana real del pack (RPG Tiny Fantasy World 01 PBR
        // /Texture/Base Map.png, el atlas que usa Shore01/Ocean vía DefaultPBR.mat) en vez de inventados a
        // ojo — para que la orilla y el terreno lean como el mismo estilo, no dos packs distintos.
        // Revisión 7: colores re-tomados muestreando de verdad píxeles de Base Map.png (antes eran una
        // aproximación a ojo del aspecto general del atlas, no colores reales del archivo) — hex
        // dbb74b/89a106/7c7c7c/f2f2f2, swatches reales de la paleta plana del pack. Sigue sin ser posible
        // saber con certeza qué swatch exacto usa `Shore01` sin abrir el mesh en el Editor (no tiene forma
        // de inspeccionar UVs desde aquí) — por eso el islote ya no lleva piezas de `Shore01` sueltas (ver
        // `CercaDeAlgunaIslaMenor`), que es lo que de verdad arregla el parche de color pegado a la isla.
        TerrainLayer sandLayer = GetOrCreateLayer("Arena", new Color(0.859f, 0.718f, 0.294f));
        TerrainLayer grassLayer = GetOrCreateLayer("Hierba", new Color(0.349f, 0.631f, 0.024f));
        TerrainLayer rockLayer = GetOrCreateLayer("Roca", new Color(0.486f, 0.486f, 0.486f));
        TerrainLayer snowLayer = GetOrCreateLayer("Nieve", new Color(0.949f, 0.949f, 0.949f));

        var terrainData = new TerrainData { heightmapResolution = HeightmapResolution };
        terrainData.size = new Vector3(TerrainWidth, TerrainMaxHeight, TerrainLength);
        terrainData.terrainLayers = new[] { sandLayer, grassLayer, rockLayer, snowLayer };

        int hRes = terrainData.heightmapResolution;
        var heights = new float[hRes, hRes];
        for (int zi = 0; zi < hRes; zi++)
        {
            float zNorm = zi / (float)(hRes - 1);
            for (int xi = 0; xi < hRes; xi++)
            {
                float xNorm = xi / (float)(hRes - 1);
                heights[zi, xi] = ComputeHeight01(xNorm, zNorm);
            }
        }
        terrainData.SetHeights(0, 0, heights);

        terrainData.alphamapResolution = AlphamapResolution;
        int aW = terrainData.alphamapWidth;
        int aH = terrainData.alphamapHeight;
        var alphas = new float[aH, aW, 4];
        for (int zi = 0; zi < aH; zi++)
        {
            float zNorm = zi / (float)(aH - 1);
            for (int xi = 0; xi < aW; xi++)
            {
                float xNorm = xi / (float)(aW - 1);
                ComputeWeights(xNorm, zNorm, out float sand, out float grass, out float rock, out float snow);
                alphas[zi, xi, 0] = sand;
                alphas[zi, xi, 1] = grass;
                alphas[zi, xi, 2] = rock;
                alphas[zi, xi, 3] = snow;
            }
        }
        terrainData.SetAlphamaps(0, 0, alphas);

        // *** REVISIÓN 15 *** — a diferencia de `GetOrCreateLayer`, esto nunca comprobaba si ya había un
        // TerrainData de una ejecución anterior en esta misma ruta. `AssetDatabase.CreateAsset` no sustituye
        // de forma limpia un asset ya existente: el heightmap se cuela igual, pero los sub-assets de textura
        // de los alphamaps viejos (100% arena, de la primera vez que se corrió este script) se quedan
        // colgando del archivo en vez de sustituirse — coincide con "la forma cambia, el color nunca".
        string terrainDataPath = $"{TerrainFolder}/MundoNuevo_TerrainData.asset";
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath) != null)
        {
            AssetDatabase.DeleteAsset(terrainDataPath);
        }
        AssetDatabase.CreateAsset(terrainData, terrainDataPath);

        GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
        terrainGo.name = "Terreno_MundoNuevo";
        terrainGo.transform.SetParent(parent);
        terrainGo.transform.position = new Vector3(-TerrainWidth * 0.5f, 0f, 0f);
        Terrain terrainComp = terrainGo.GetComponent<Terrain>();
        terrainComp.Flush(); // fuerza a Unity a releer terrainData (alphamaps incluidos) por si el Editor no refresca solo

        // *** REVISIÓN 14 *** — el log de diagnóstico de la revisión 13 confirmó (con la captura real de
        // Raúl al lado) que el DATO es correcto: Pueblo Bajo/Castillo salían con hierba=1.00 y el Pico con
        // nieve=1.00, y aun así el terreno seguía viéndose 100% del color de la Arena en TODA la isla, pico
        // incluido. Con los datos descartados como causa, el problema tiene que estar en cómo Unity pinta
        // ese alphamap — y la causa más probable de "el Terrain solo pinta la capa 0 pase lo que pase" es
        // que `Terrain.CreateTerrainGameObject` no le esté asignando un material capaz de mezclar 4 capas
        // para el render pipeline activo (URP, confirmado por el shader que ya usa este mismo archivo más
        // abajo — ver `PlacePathLineRenderer`), y esté cayendo a un material/fallback que solo muestra la
        // textura base. Arreglo: se asigna explícitamente un material con el shader de terreno de URP en
        // vez de confiar en el material automático — si el automático ya estaba bien, esto no cambia nada
        // visible; si era la causa real, esto la corrige de raíz.
        Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (terrainShader != null)
        {
            var terrainMat = new Material(terrainShader) { name = "Mat_Terreno_MundoNuevo" };
            terrainComp.materialTemplate = terrainMat;
            log.AppendLine("Material del Terrain: asignado explícitamente 'Universal Render Pipeline/Terrain/Lit' (revisión 14) — antes se dejaba el automático de Unity, sospechoso de no mezclar las 4 capas.");
        }
        else
        {
            log.AppendLine("AVISO (revisión 14): no se encontró el shader 'Universal Render Pipeline/Terrain/Lit' — el proyecto podría no ser URP, o el shader no está disponible. El Terrain se queda con el material automático de Unity.");
        }

        // Diagnóstico (revisión 13, se mantiene): pesos CALCULADOS con la fórmula, sin tocar Unity para nada.
        void LogPesos(string etiqueta, Vector2 norm)
        {
            float h = ComputeHeight01(norm.x, norm.y);
            ComputeWeights(norm.x, norm.y, out float s, out float g, out float r, out float n);
            log.AppendLine($"  [diagnóstico calculado] {etiqueta}: altura h={h:0.000}  arena={s:0.00} hierba={g:0.00} roca={r:0.00} nieve={n:0.00}");
        }
        log.AppendLine("Pesos de textura en puntos conocidos (para comparar con lo que se vea en el Editor):");
        LogPesos("Pueblo Bajo (franja llana sur)", PuebloBajoCentro);
        LogPesos("Castillo (franja llana sur)", CastilloCentro);
        LogPesos("Pico protagonista (macizo norte)", HeroPeakNorm);

        // *** REVISIÓN 15 *** — diagnóstico nuevo: esta vez LEE los alphamaps que de verdad quedaron
        // guardados en el TerrainData del Terrain ya creado (no la fórmula). Si el fix de arriba (borrar el
        // asset viejo antes de crear el nuevo) era la causa real, esto debería coincidir por fin con los
        // valores "calculado" de arriba. Si NO coincide, el problema sigue sin estar resuelto y ya tenemos
        // el dato para el siguiente paso sin necesidad de otra ronda a ciegas.
        void LogPesosReales(string etiqueta, Vector2 norm)
        {
            int ax = Mathf.Clamp(Mathf.RoundToInt(norm.x * (aW - 1)), 0, aW - 1);
            int az = Mathf.Clamp(Mathf.RoundToInt(norm.y * (aH - 1)), 0, aH - 1);
            float[,,] real = terrainComp.terrainData.GetAlphamaps(ax, az, 1, 1);
            log.AppendLine($"  [diagnóstico REAL guardado] {etiqueta}: arena={real[0, 0, 0]:0.00} hierba={real[0, 0, 1]:0.00} roca={real[0, 0, 2]:0.00} nieve={real[0, 0, 3]:0.00}");
        }
        log.AppendLine("Pesos REALES leídos del TerrainData ya guardado (esto es lo que Unity debería estar pintando):");
        LogPesosReales("Pueblo Bajo (franja llana sur)", PuebloBajoCentro);
        LogPesosReales("Castillo (franja llana sur)", CastilloCentro);
        LogPesosReales("Pico protagonista (macizo norte)", HeroPeakNorm);

        log.AppendLine($"Terreno {TerrainWidth}x{TerrainLength}m (altura máx {TerrainMaxHeight}m) — silueta direccional norte-sur (llano al sur, macizo al norte, rodeado de mar), 4 capas (arena/hierba/roca/nieve, con textura y brillo bajo), pico protagonista + islote secundario incluidos.");
        return terrainComp;
    }

    /// <summary>Ruido de costa muestreado por POSICIÓN (xNorm, zNorm), no por ángulo respecto a un centro —
    /// reemplaza a la vieja `CoastlineNoise` angular, que ya no tiene sentido para una silueta direccional
    /// en vez de radial. Dos octavas (bahías/penínsulas grandes + mordiscos pequeños encima), con un offset
    /// distinto por llamada para que el ruido lateral y el longitudinal no queden idénticos/alineados.</summary>
    private static float CoastlineNoise2D(float xNorm, float zNorm, float amp1, float amp2, float offset)
    {
        float n1 = Mathf.PerlinNoise(xNorm * CoastNoiseFrequency + offset, zNorm * CoastNoiseFrequency + offset + 50f);
        float n2 = Mathf.PerlinNoise(xNorm * CoastNoiseFrequency2 + offset + 300f, zNorm * CoastNoiseFrequency2 + offset + 300f);
        return (n1 - 0.5f) * 2f * amp1 + (n2 - 0.5f) * 2f * amp2;
    }

    /// <summary>0 nada más entrar en la franja de mar, 1 bien tierra adentro — suavizado (smoothstep) en el
    /// ancho `band`. Función genérica compartida por el fundido longitudinal (norte/sur) y el lateral
    /// (este/oeste): `distIntoLand` es la distancia (en 0..1 normalizado) desde el borde de tierra hacia
    /// dentro; negativo o cero ya es mar.</summary>
    private static float EdgeFalloff01(float distIntoLand, float band)
    {
        float t = Mathf.Clamp01(distIntoLand / band);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Ancho lateral (medio-ancho, en xNorm) de la franja de tierra en esta altura del mapa (zNorm):
    /// máximo en la mitad sur (pueblos, franja llana — el mapa de referencia es bastante ancho ahí) y se
    /// estrecha según se acerca al norte (el macizo se lee como un promontorio, no como un bloque
    /// rectangular) — esto es lo que le da a la isla "una formita" en vez de un círculo o un cuadrado.</summary>
    private static float LateralHalfWidthAt(float zNorm)
    {
        float t = Mathf.Clamp01(Mathf.InverseLerp(LateralTaperEndZ, LateralTaperStartZ, zNorm)); // 0 cerca del norte, 1 en la mitad sur
        float eased = t * t * (3f - 2f * t);
        return Mathf.Lerp(LateralHalfWidthNorth, LateralHalfWidthSouth, eased);
    }

    /// <summary>*** REVISIÓN 11 *** Cuánto ruido de costa "cabe" en esta franja del mapa sin fragmentarla en
    /// dedos/islotes sueltos — 1 donde la tierra es ancha (mitad sur), bajando hacia `CoastNoiseFloor` según
    /// se acerca al promontorio norte (`LateralHalfWidthAt` se estrecha ahí). La captura de la revisión 10
    /// mostró justo eso: dedos e islotes flotantes cerca de la punta norte, porque la amplitud de ruido
    /// (pensada para la franja ancha del sur) es demasiado grande relativa al poco ancho de tierra que queda
    /// junto al pico — el ruido cruza el umbral mar/tierra varias veces en un tramo corto en vez de dibujar
    /// una costa ondulada continua. No se toca la amplitud de la mitad sur (bahías orgánicas a propósito).</summary>
    private static float CoastNoiseScaleAt(float zNorm)
    {
        float widthFrac = Mathf.Clamp01(LateralHalfWidthAt(zNorm) / LateralHalfWidthSouth);
        return Mathf.Lerp(CoastNoiseFloor, 1f, widthFrac);
    }

    /// <summary>Fundido a mar en los extremos norte (junto al macizo) y sur (la playa real que pidió Raúl) —
    /// 0 en mar abierto, 1 bien dentro de la franja habitable. Reemplaza al viejo radial: aquí "tierra
    /// adentro" significa "lejos de los dos bordes norte/sur", no "cerca del centro del mapa".</summary>
    private static float LongitudinalEdgeMask(float xNorm, float zNorm)
    {
        float scale = CoastNoiseScaleAt(zNorm);
        float noise = CoastlineNoise2D(xNorm, zNorm, CoastNoiseAmplitude * scale, CoastNoiseAmplitude2 * scale, 50f);
        float zEff = zNorm + noise;
        float maskNorte = EdgeFalloff01(zEff - NorthCoastInnerZ, LongitudinalCoastBand);
        float maskSur = EdgeFalloff01(SouthCoastInnerZ - zEff, LongitudinalCoastBand);
        return Mathf.Min(maskNorte, maskSur);
    }

    /// <summary>Fundido a mar en los lados este/oeste, contra el ancho lateral variable de `LateralHalfWidthAt`
    /// (no un ancho fijo) — así el estrechamiento hacia el norte también queda suavizado hacia el mar, no es
    /// un escalón. Ruido más suave a propósito (ver comentario de `LateralNoiseAmplitude`).</summary>
    private static float LateralEdgeMask(float xNorm, float zNorm)
    {
        float halfWidth = LateralHalfWidthAt(zNorm);
        float scale = CoastNoiseScaleAt(zNorm);
        float noise = CoastlineNoise2D(xNorm, zNorm, LateralNoiseAmplitude * scale, LateralNoiseAmplitude2 * scale, 900f);
        float dx = Mathf.Abs(xNorm - 0.5f) - noise;
        // Revisión 12: la captura de la revisión 11 seguía mostrando dedos/islotes sueltos junto al
        // promontorio norte a pesar de escalar el RUIDO (`CoastNoiseScaleAt`) — quedaba una causa más grande
        // sin tocar: `LateralCoastBand` (0.11) es una banda de transición de ANCHO FIJO, calibrada contra la
        // mitad sur ancha (halfWidth 0.44, banda = 25% de eso). Junto al norte, halfWidth cae a 0.20 — la
        // MISMA banda de 0.11 pasa a ser más de la mitad del ancho de la península entera, así que gran
        // parte de la "tierra" ya estaba dentro de la zona de erosión del smoothstep antes de tocar nada de
        // ruido; con cualquier perturbación pequeña, tramos enteros caían por debajo del umbral y la
        // península se rompía en trozos. Fix: la banda se limita a como mucho la mitad del ancho local
        // (`halfWidth * 0.5`), así SIEMPRE queda un núcleo sólido de tierra en el centro sin erosionar, sea
        // cual sea el ancho en esa fila del mapa. La mitad sur no se ve afectada (0.11 ya es menos de la
        // mitad de 0.44).
        float band = Mathf.Min(LateralCoastBand, halfWidth * 0.5f);
        return EdgeFalloff01(halfWidth - dx, band);
    }

    /// <summary>0 en la franja llana sur (desde `MountainStartZ` hacia el sur, zNorm creciente), 1 en el
    /// núcleo del macizo (`NorthPeakZ` o más al norte). Ease-in (t²): sube despacio nada más dejar la franja
    /// llana y se acelera cerca del macizo — así "no hay tantas montañas", la mayor parte del recorrido
    /// norte-sur se queda baja y solo el tramo final junto al pico es de verdad montañoso. Controla tanto la
    /// altura objetivo (`ComputeHeight01`) como cuánto ruido/aspereza se aplica — la franja llana sur se
    /// queda visualmente lisa, no solo baja.</summary>
    private static float MountainFactor(float zNorm)
    {
        float t = Mathf.Clamp01(Mathf.InverseLerp(MountainStartZ, NorthPeakZ, zNorm));
        return t * t;
    }

    /// <summary>Altura "objetivo" de continente en este punto (0 = mar, 1 = núcleo del macizo), antes de
    /// ruido/surco/pico protagonista/islotes. Combina el fundido a mar (longitudinal + lateral — de ahí sale
    /// la FORMA de la isla) con cuánto de montaña toca aquí (`MountainFactor` — de ahí sale la ALTURA), dos
    /// cosas antes mezcladas en una sola curva radial y que ahora son independientes a propósito: se puede
    /// estar "tierra adentro de verdad" (fundido a mar = 1) y aun así en la franja llana sur (montaña = 0).</summary>
    private static float RiseCurve(float xNorm, float zNorm)
    {
        float coastFade = Mathf.Min(LongitudinalEdgeMask(xNorm, zNorm), LateralEdgeMask(xNorm, zNorm));
        float mountain = MountainFactor(zNorm);
        float riseObjetivo = Mathf.Lerp(FlatBaseFraction, 1f, mountain);
        return coastFade * riseObjetivo;
    }

    /// <summary>0 lejos de cualquier islote menor, 1 dentro de su meseta central — a diferencia del pico
    /// protagonista (un cono puro), aquí una fracción del radio (`SmallIslandPlateauFraction`) queda
    /// COMPLETAMENTE LLANA antes de que el terreno empiece a bajar hacia el mar en el aro exterior. Sin
    /// esto la isla es un cono liso de la orilla a la cumbre, sin un metro llano — justo la queja de Raúl
    /// ("es inutilizable, si vamos ahí no podemos hacer nada"). Si el punto cae dentro de más de un
    /// islote (no debería, si están bien separados) gana el de falloff más fuerte.</summary>
    private static readonly Vector2 CentroMapaNorm = new Vector2(0.5f, 0.5f);

    private static bool IslasMenoresFalloff(float xNorm, float zNorm, out float falloff, out float alturaObjetivo)
    {
        falloff = 0f;
        alturaObjetivo = 0f;
        foreach (var isla in IslasMenores)
        {
            float dx = xNorm - isla.CentroNorm.x;
            float dz = (zNorm - isla.CentroNorm.y) * (TerrainLength / TerrainWidth);
            float d = Mathf.Sqrt(dx * dx + dz * dz) / isla.RadioNorm;
            if (d >= 1f) continue;

            // Sector de rampa hacia el continente (ver comentario de las constantes) — dentro de él, la
            // banda de subida es mucho más ancha (plateauLocal mucho menor) para que se lea como un camino
            // caminable de la playa a la meseta, no como un banco vertical.
            Vector2 dirIslaDesdeCentro = new Vector2(isla.CentroNorm.x - CentroMapaNorm.x, (isla.CentroNorm.y - CentroMapaNorm.y) * (TerrainLength / TerrainWidth));
            float bearingRampaDeg = Mathf.Atan2(-dirIslaDesdeCentro.y, -dirIslaDesdeCentro.x) * Mathf.Rad2Deg;
            float anguloPuntoDeg = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            float diffAnguloDeg = Mathf.Abs(Mathf.DeltaAngle(anguloPuntoDeg, bearingRampaDeg));
            float rampaFactor = Mathf.Clamp01(1f - diffAnguloDeg / SmallIslandRampHalfAngleDeg);
            float plateauLocal = Mathf.Lerp(SmallIslandPlateauFraction, SmallIslandRampPlateauFraction, rampaFactor);

            float t = Mathf.InverseLerp(1f, plateauLocal, d); // 0 en el borde exterior, 1 en toda la meseta (ya clamps a [0,1])
            float f = t * t * (3f - 2f * t);
            if (f > falloff)
            {
                falloff = f;
                alturaObjetivo = isla.AlturaObjetivo01;
            }
        }
        return falloff > 0f;
    }

    private static Vector2 PathPointNorm(float t)
    {
        t = Mathf.Clamp01(t);
        Vector2 baseP = Vector2.Lerp(PathStartNorm, PathEndNorm, t);
        baseP.x += 0.09f * Mathf.Sin(t * 7f) + 0.04f * Mathf.Sin(t * 15f + 1.1f);
        return baseP;
    }

    private static List<Vector2> BuildPathPolylineWorld(int samples)
    {
        var list = new List<Vector2>(samples + 1);
        for (int i = 0; i <= samples; i++)
        {
            Vector2 n = PathPointNorm(i / (float)samples);
            list.Add(new Vector2(n.x * TerrainWidth - TerrainWidth * 0.5f, n.y * TerrainLength));
        }
        return list;
    }

    /// <summary>Distancia (mundo) al punto más cercano del camino, y en qué fracción "t" del camino cae.</summary>
    private static float DistanceToPathWorld(float worldX, float worldZ, out float nearestT)
    {
        float min = float.MaxValue;
        int bestI = 0;
        for (int i = 0; i < _pathPolylineWorld.Count; i++)
        {
            float dx = worldX - _pathPolylineWorld[i].x;
            float dz = worldZ - _pathPolylineWorld[i].y;
            float d = dx * dx + dz * dz;
            if (d < min) { min = d; bestI = i; }
        }
        nearestT = bestI / (float)(_pathPolylineWorld.Count - 1);
        return Mathf.Sqrt(min);
    }

    private static float FractalNoise(float x, float z)
    {
        float total = 0f, amplitude = 1f, frequency = 1f, maxAmp = 0f;
        for (int o = 0; o < 4; o++)
        {
            total += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
            maxAmp += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.15f;
        }
        return total / maxAmp;
    }

    private static float RidgeNoise(float x, float z)
    {
        float n = FractalNoise(x, z);
        float ridge = 1f - Mathf.Abs(n * 2f - 1f);
        return ridge * ridge;
    }

    /// <summary>0 lejos del pico protagonista, 1 justo en su cumbre (smoothstep radial).</summary>
    private static float HeroPeakFalloff(float xNorm, float zNorm)
    {
        float dx = xNorm - HeroPeakNorm.x;
        float dz = (zNorm - HeroPeakNorm.y) * (TerrainLength / TerrainWidth); // corrige aspecto: el pico es redondo en el mundo, no elíptico
        float d = Mathf.Sqrt(dx * dx + dz * dz) / HeroPeakRadiusNorm;
        float f = Mathf.Clamp01(1f - d);
        return f * f * (3f - 2f * f);
    }

    /// <summary>0 lejos de cualquier zona de descanso, 1 en su centro — usado para amortiguar el ruido de
    /// montaña localmente (igual que el surco del camino), NO para fijar una altura concreta: cada zona de
    /// descanso queda llana a la altura ambiente de su posición, así hay descansos a distintas alturas.</summary>
    private static float RestZoneFlatten(float xNorm, float zNorm)
    {
        float mejor = 0f;
        foreach (Vector2 centro in RestZoneNorm)
        {
            float dx = xNorm - centro.x;
            float dz = (zNorm - centro.y) * (TerrainLength / TerrainWidth);
            float d = Mathf.Sqrt(dx * dx + dz * dz) / RestZoneRadiusNorm;
            float f = Mathf.Clamp01(1f - d);
            float suavizado = f * f * (3f - 2f * f);
            if (suavizado > mejor) mejor = suavizado;
        }
        return mejor;
    }

    /// <summary>0 lejos de cualquier zona "construible" (pueblos, castillo, Bosque Prohibido), 1 dentro de
    /// ella — mismo truco que `RestZoneFlatten` (amortigua el ruido de montaña localmente, no fija una
    /// altura), aplicado esta vez a las zonas donde va a haber edificios/laberinto de verdad. Revisión 7:
    /// antes solo las 4 zonas de descanso tenían este aplanado — los anclajes de pueblo/castillo y el
    /// Bosque Prohibido no lo tenían, así que si su posición caía en mitad de una ladera (como pasó con el
    /// Bosque y con el Pueblo de Will: "estamos poniendo el bosque prohibido en una pendiente" / "el punto
    /// rosa está en otra pendiente") se quedaban en pendiente sin que nada lo corrigiera.</summary>
    private static float ZonasConstruiblesFlatten(float xNorm, float zNorm)
    {
        float mejor = 0f;

        void ProbarCirculo(Vector2 centroNorm, float radioMundo)
        {
            float radioNorm = radioMundo / TerrainWidth;
            float dx = xNorm - centroNorm.x;
            float dz = (zNorm - centroNorm.y) * (TerrainLength / TerrainWidth);
            float d = Mathf.Sqrt(dx * dx + dz * dz) / radioNorm;
            float f = Mathf.Clamp01(1f - d);
            float suavizado = f * f * (3f - 2f * f);
            if (suavizado > mejor) mejor = suavizado;
        }

        ProbarCirculo(PuebloBajoCentro, PuebloBajoRadio + 15f);
        ProbarCirculo(CastilloCentro, 45f);
        ProbarCirculo(PuebloNieveCentro, PuebloNieveRadio + 15f);
        ProbarCirculo(PuebloDeWillCentro, PuebloDeWillRadio + 15f);
        ProbarCirculo(PuebloPesqueroCentro, PuebloPesqueroRadio + 15f);

        // Bosque Prohibido: rectángulo, no círculo — distancia al rectángulo (0 dentro, crece hacia fuera),
        // con 40m de margen fuera del rectángulo para que el aplanado no corte en seco justo en el borde.
        float cx = Mathf.Clamp(xNorm, BosqueAreaMinNorm.x, BosqueAreaMaxNorm.x);
        float cz = Mathf.Clamp(zNorm, BosqueAreaMinNorm.y, BosqueAreaMaxNorm.y);
        float distBosqueMundo = new Vector2((xNorm - cx) * TerrainWidth, (zNorm - cz) * TerrainLength).magnitude;
        float fBosque = Mathf.Clamp01(1f - distBosqueMundo / 40f);
        float suavizadoBosque = fBosque * fBosque * (3f - 2f * fBosque);
        if (suavizadoBosque > mejor) mejor = suavizadoBosque;

        return mejor;
    }

    private static float ComputeHeight01(float xNorm, float zNorm)
    {
        float rise = RiseCurve(xNorm, zNorm);
        float baseHeight01 = rise * PeakRiseFraction;

        // Revisión 10: el ruido/aspereza ya no escala con `rise` (que ahora mezcla forma de costa + altura
        // de montaña) sino con `MountainFactor` — así la franja llana sur se queda lisa de verdad (Raúl:
        // "no hay descanso"), no solo baja, y toda la aspereza de cresta se concentra cerca del macizo norte.
        float mountainFactor = MountainFactor(zNorm);
        float aspect = TerrainLength / TerrainWidth;
        float baseNoise = FractalNoise(xNorm * 4.5f + 11.3f, zNorm * aspect * 4.5f + 4.7f);
        float ridge = RidgeNoise(xNorm * 3.1f + 7.7f, zNorm * aspect * 3.1f + 2.1f);
        float combinedNoise = Mathf.Lerp(baseNoise, ridge, mountainFactor * 0.7f);
        float noiseAmplitude = Mathf.Lerp(0.05f, 0.30f, mountainFactor);
        float ruggedness = (combinedNoise - 0.5f) * 2f * noiseAmplitude;

        float worldX = xNorm * TerrainWidth - TerrainWidth * 0.5f;
        float worldZ = zNorm * TerrainLength;
        float dPath = DistanceToPathWorld(worldX, worldZ, out float nearestT);

        // Surco del camino: aplana/hunde ligeramente la franja alrededor del centro del camino.
        float grooveFactor = Mathf.Clamp01(1f - Mathf.Max(0f, dPath - PathHalfWidth) / PathFalloff);
        ruggedness *= (1f - grooveFactor * 0.9f);
        float groove = PathGrooveDepth01 * grooveFactor * rise;

        // Zonas de descanso: mismo truco que el surco del camino — se amortigua el ruido de montaña, no se
        // fuerza una altura fija, así cada claro queda llano a la altura que le toque por su posición.
        float restFlatten = RestZoneFlatten(xNorm, zNorm);
        float construibleFlatten = ZonasConstruiblesFlatten(xNorm, zNorm);
        float flattenTotal = Mathf.Max(restFlatten, construibleFlatten);
        ruggedness *= (1f - flattenTotal * 0.92f);

        // Barrancos: más anchos que el camino (de ahí RavineHalfWidth > PathHalfWidth) — cruzan la
        // senda en dos puntos concretos (RavineTs), ahí van los puentes.
        float ravineAlong = 0f;
        foreach (float rt in RavineTs)
        {
            float distT = Mathf.Abs(nearestT - rt);
            ravineAlong = Mathf.Max(ravineAlong, Mathf.Clamp01(1f - distT / RavineWidthT));
        }
        float ravineSpread = Mathf.Clamp01(1f - Mathf.Max(0f, dPath - RavineHalfWidth) / RavineFalloff);
        float ravine = ravineAlong * ravineSpread * RavineDepth01 * Mathf.Clamp01(rise * 1.3f);

        float h = baseHeight01 + ruggedness - groove - ravine;

        // Pico protagonista: se MEZCLA con el terreno de alrededor (no se suma encima), para que la
        // base sea progresiva y no aparezca un escalón raro donde empieza su radio de influencia.
        float heroFalloff = HeroPeakFalloff(xNorm, zNorm);
        if (heroFalloff > 0f) h = Mathf.Lerp(h, HeroPeakTargetHeight01, heroFalloff);

        // Islotes menores: igual, se mezclan con el mar de alrededor — cada uno queda bajo, con una
        // meseta plana real en el centro (ver IslasMenoresFalloff), no un cono.
        if (IslasMenoresFalloff(xNorm, zNorm, out float islandFalloff, out float islandTarget))
            h = Mathf.Lerp(h, islandTarget, islandFalloff);

        return Mathf.Clamp(h, 0.01f, 0.985f);
    }

    /// <summary>Arena/hierba/roca/nieve por ALTURA real (no por posición) — así cualquier pico lo bastante
    /// alto coge nieve, no solo el que está "más al norte". Revisión 6: la nieve reclama su parte del
    /// "resto" ANTES que la roca (no después) — con el orden anterior la roca podía consumir el 100% del
    /// resto antes de que la nieve llegara a pintar nada, y la nieve no aparecía NUNCA sin importar la
    /// altura. La nieve es la banda más alta, así que debe tener prioridad sobre lo que queda por encima
    /// del umbral de roca.</summary>
    private static void ComputeWeights(float xNorm, float zNorm, out float sand, out float grass, out float rock, out float snow)
    {
        float h = ComputeHeight01(xNorm, zNorm);
        float remaining = 1f;

        float sandFrac = 1f - Mathf.Clamp01(Mathf.InverseLerp(SeaLevel01, SeaLevel01 + BeachBlend01, h));
        sand = remaining * sandFrac; remaining -= sand;

        float snowFrac = Mathf.Clamp01(Mathf.InverseLerp(SnowHeightThreshold01, SnowHeightThreshold01 + SnowBlend01, h));
        snow = remaining * snowFrac; remaining -= snow;

        float rockFrac = Mathf.Clamp01(Mathf.InverseLerp(RockHeightThreshold01, RockHeightThreshold01 + RockBlend01, h));
        rock = remaining * rockFrac; remaining -= rock;

        grass = Mathf.Max(0f, remaining);
    }

    /// <summary>Crea (o, si ya existe de una ejecución anterior, REGENERA en el sitio) la capa de terreno.
    /// Revisión 6: antes, si el archivo ya existía en disco (de cualquier revisión previa — este script
    /// lleva ya varias ejecuciones), se devolvía tal cual y se ignoraban por completo los cambios de color
    /// /ruido/brillo de código — de ahí "sigo viendo la misma textura" aunque el script hubiera cambiado.
    /// Ahora la textura y las propiedades de la capa se sobrescriben SIEMPRE, coherente con el resto del
    /// script (regenerar = destruir y reconstruir, no "usar lo que hubiera de antes").</summary>
    private static TerrainLayer GetOrCreateLayer(string nombre, Color color)
    {
        string texPath = $"{TerrainFolder}/Layers/Tex_{nombre}.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var pixels = GenerarPixelesTextura(nombre, color, 64);
        if (tex == null)
        {
            tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = $"Tex_{nombre}", wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels(pixels);
            tex.Apply(true);
            AssetDatabase.CreateAsset(tex, texPath);
        }
        else
        {
            tex.SetPixels(pixels);
            tex.Apply(true);
            EditorUtility.SetDirty(tex);
        }

        string path = $"{TerrainFolder}/Layers/TL_{nombre}.terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        bool esNuevo = layer == null;
        if (esNuevo) layer = new TerrainLayer { name = $"TL_{nombre}" };

        layer.diffuseTexture = tex;
        layer.tileSize = new Vector2(28f, 28f);
        layer.smoothness = 0f; // revisión 7: bajado de 0.05 a 0 — Raúl seguía viendo "brillo propio" tras la 6
        layer.metallic = 0f;

        if (esNuevo) AssetDatabase.CreateAsset(layer, path);
        else EditorUtility.SetDirty(layer);

        return layer;
    }

    /// <summary>Píxeles con variación de tono (ruido), no un color plano — para que el terreno no se vea
    /// liso/plástico de cerca. Devuelve el array de colores (no el Texture2D ya hecho) para que
    /// `GetOrCreateLayer` pueda volcarlos siempre sobre la MISMA textura, tanto si es nueva como si ya
    /// existía en disco de una ejecución anterior.</summary>
    private static Color[] GenerarPixelesTextura(string nombre, Color baseColor, int size)
    {
        float offset = Mathf.Abs(nombre.GetHashCode() % 1000);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.08f + offset, y * 0.08f + offset);
                float n2 = Mathf.PerlinNoise(x * 0.23f + offset * 2f, y * 0.23f + offset * 2f);
                float variacion = Mathf.Lerp(0.94f, 1.06f, n1 * 0.65f + n2 * 0.35f); // revisión 6: mucho menos grano — la paleta del pack es plana/toon, no PBR realista
                Color c = baseColor * variacion; c.a = 1f;
                pixels[y * size + x] = c;
            }
        }
        return pixels;
    }

    // ==================== OCÉANO Y COSTA ====================

    private static void BuildCoastline(Terrain terrain, Transform parent, System.Text.StringBuilder log)
    {
        Vector3 centroOceano = new Vector3(0f, SeaLevel01 * TerrainMaxHeight, TerrainLength * 0.5f);
        GameObject oceano = PlacePrefab($"{TinyWorldLandMass}/Ocean.prefab", centroOceano, Quaternion.identity, parent, "Oceano");
        if (oceano != null) EscalarOceanoParaCubrirTerreno(oceano, log);

        var rng = new System.Random(Seed ^ 0x0C0A57);
        int colocadas = 0, intentos = 0;
        while (colocadas < CoastlinePieceCount && intentos < CoastlinePieceCount * 15)
        {
            intentos++;
            float xNorm = (float)rng.NextDouble();
            float zNorm = (float)rng.NextDouble();
            float h = ComputeHeight01(xNorm, zNorm);
            if (Mathf.Abs(h - SeaLevel01) > CoastlineBand) continue;

            // Revisión 7: NO repartir piezas de `Shore01` cerca de los islotes menores — en un islote tan
            // pequeño, una pieza suelta con un tono de la paleta distinto al de la capa de arena generada
            // se lee como un parche pegado encima ("la orilla tiene otro color al de la isla"), mucho más
            // que en la costa larga del continente donde una pieza aislada pesa menos. En el continente sí
            // se siguen colocando.
            if (CercaDeAlgunaIslaMenor(xNorm, zNorm)) continue;

            float worldX = xNorm * TerrainWidth - TerrainWidth * 0.5f;
            float worldZ = zNorm * TerrainLength;
            float worldY = SampleHeight(terrain, worldX, worldZ);
            Vector3 haciaCentro = new Vector3(0f - worldX, 0f, TerrainLength * 0.5f - worldZ);
            if (haciaCentro.sqrMagnitude < 0.001f) haciaCentro = Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(haciaCentro.normalized, Vector3.up);

            GameObject pieza = PlacePrefab($"{TinyWorldLandMass}/Shore01.prefab", new Vector3(worldX, worldY, worldZ), rot, parent, $"Orilla_{colocadas + 1}");
            if (pieza != null) colocadas++;
        }

        log.AppendLine($"Costa: océano escalado por tamaño real (no a ciegas) + {colocadas} piezas de orilla repartidas donde el CONTINENTE cruza el nivel del mar (los islotes menores no llevan piezas de orilla sueltas — ver revisión 7).");
    }

    /// <summary>True si el punto cae dentro (o muy cerca) de la influencia de algún islote menor — usado
    /// para no repartir ahí piezas sueltas de orilla (`Shore01`), que en un islote pequeño se ven como un
    /// parche de color pegado encima en vez de una transición natural.</summary>
    private static bool CercaDeAlgunaIslaMenor(float xNorm, float zNorm)
    {
        foreach (var isla in IslasMenores)
        {
            float dx = xNorm - isla.CentroNorm.x;
            float dz = (zNorm - isla.CentroNorm.y) * (TerrainLength / TerrainWidth);
            float d = Mathf.Sqrt(dx * dx + dz * dz) / isla.RadioNorm;
            if (d < 1.4f) return true;
        }
        return false;
    }

    /// <summary>Mide el tamaño real del prefab del océano (bounds del Renderer, no la escala a ciegas de la
    /// revisión 3) y lo escala para cubrir un objetivo relativo a la diagonal del terreno.</summary>
    private static void EscalarOceanoParaCubrirTerreno(GameObject oceano, System.Text.StringBuilder log)
    {
        var renderers = oceano.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            oceano.transform.localScale = Vector3.one * OceanFallbackScale;
            log.AppendLine("AVISO: 'Ocean.prefab' no tiene Renderer — se aplicó una escala de emergencia, revisa a ojo.");
            return;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        float baseSize = Mathf.Max(b.size.x, b.size.z);
        if (baseSize < 0.01f) baseSize = 1f;

        float terrainDiag = Mathf.Sqrt(TerrainWidth * TerrainWidth + TerrainLength * TerrainLength);
        float targetSize = terrainDiag * OceanCoverageFactor;
        float factor = targetSize / baseSize;
        oceano.transform.localScale = Vector3.one * factor;
        log.AppendLine($"Océano: prefab base ~{baseSize:0}m → escalado x{factor:0.00} para cubrir ~{targetSize:0}m (antes se escalaba a ciegas y salía desproporcionado — de ahí la isla diminuta de la captura anterior).");
    }

    // ==================== ZONAS (VISTA PREVIA — PASO 1) ====================

    /// <summary>Marcadores visuales (esferas de colores) en la posición y altura REAL de cada futura zona,
    /// sin construir todavía nada de verdad — para dar el visto bueno a la disposición antes de poblarla en
    /// los pasos siguientes. Si una zona queda cerca o bajo el nivel del mar, su marcador se pinta en rojo
    /// (ver VerificarAnclasSobreElNivelDelMar, que además lo deja escrito en el log).</summary>
    private static void BuildZoneMarkers(Terrain terrain, Transform parent, System.Text.StringBuilder log)
    {
        int total = 0;

        CrearMarcadorZona(terrain, parent, "ZONA_PuebloBajo", PuebloBajoCentro, new Color(0.95f, 0.80f, 0.25f), 8f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_Castillo", CastilloCentro, new Color(0.55f, 0.55f, 0.62f), 7f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_PuebloDeNieve", PuebloNieveCentro, new Color(0.75f, 0.90f, 1f), 6f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_PuebloDeWill", PuebloDeWillCentro, new Color(0.95f, 0.55f, 0.75f), 7f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_PuebloPesquero", PuebloPesqueroCentro, new Color(0.20f, 0.75f, 0.75f), 7f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_PicoProtagonista", HeroPeakNorm, new Color(0.85f, 0.35f, 0.20f), 6f); total++;

        // Archipiélago de islotes menores (revisión 7) — uno por entrada de IslasMenores.
        foreach (var isla in IslasMenores)
        {
            string nombreZona = "ZONA_" + isla.Nombre.Replace(" ", "").Replace("á", "a").Replace("Á", "A");
            CrearMarcadorZona(terrain, parent, nombreZona, isla.CentroNorm, isla.ColorZona, 6f);
            total++;
        }

        // Bosque Prohibido: las 4 esquinas del rectángulo del laberinto + su centro, en vez de un único punto.
        Vector2 bMin = BosqueAreaMinNorm, bMax = BosqueAreaMaxNorm;
        Color bosqueColor = new Color(0.25f, 0.55f, 0.22f);
        CrearMarcadorZona(terrain, parent, "ZONA_BosqueProhibido_EsquinaNO", new Vector2(bMin.x, bMin.y), bosqueColor, 4f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_BosqueProhibido_EsquinaNE", new Vector2(bMax.x, bMin.y), bosqueColor, 4f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_BosqueProhibido_EsquinaSO", new Vector2(bMin.x, bMax.y), bosqueColor, 4f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_BosqueProhibido_EsquinaSE", new Vector2(bMax.x, bMax.y), bosqueColor, 4f); total++;
        CrearMarcadorZona(terrain, parent, "ZONA_BosqueProhibido_Centro", new Vector2((bMin.x + bMax.x) * 0.5f, (bMin.y + bMax.y) * 0.5f), bosqueColor, 5f); total++;

        // Zonas de descanso: claros llanos repartidos por el continente (pedido tras ver todo montañoso).
        Color descansoColor = new Color(0.75f, 0.95f, 0.45f);
        for (int i = 0; i < RestZoneNorm.Length; i++)
        {
            CrearMarcadorZona(terrain, parent, $"ZONA_Descanso_{i + 1}", RestZoneNorm[i], descansoColor, 5f);
            total++;
        }

        // Camino: una línea de puntos pequeños siguiendo la polilínea ya calculada para el terreno.
        var caminoGo = new GameObject("ZONA_Camino_VistaPrevia");
        caminoGo.transform.SetParent(parent);
        var lr = caminoGo.AddComponent<LineRenderer>();
        lr.material = CrearMaterialColor(new Color(0.95f, 0.85f, 0.55f));
        lr.startWidth = lr.endWidth = 2.5f;
        lr.positionCount = _pathPolylineWorld.Count;
        for (int i = 0; i < _pathPolylineWorld.Count; i++)
        {
            float worldY = SampleHeight(terrain, _pathPolylineWorld[i].x, _pathPolylineWorld[i].y) + 1.5f;
            lr.SetPosition(i, new Vector3(_pathPolylineWorld[i].x, worldY, _pathPolylineWorld[i].y));
        }

        log.AppendLine($"Zonas: {total} marcadores colocados (Pueblo Bajo, Castillo, Pueblo de Nieve, Pueblo de Will, Pueblo Pesquero, Pico Protagonista, {IslasMenores.Length}x islote menor, 5x Bosque Prohibido, {RestZoneNorm.Length}x Descanso) + línea de vista previa del camino.");
    }

    /// <summary>Comprueba que las anclas clave queden por encima del nivel del mar — si no, avisa por log en
    /// vez de dejarlo para que se note solo mirando una captura (como pasó en la revisión 3).</summary>
    private static void VerificarAnclasSobreElNivelDelMar(System.Text.StringBuilder log)
    {
        void Chequear(string nombre, Vector2 norm)
        {
            float h = ComputeHeight01(norm.x, norm.y);
            float margenM = (h - SeaLevel01) * TerrainMaxHeight;
            if (h <= SeaLevel01)
                log.AppendLine($"AVISO: '{nombre}' queda BAJO el nivel del mar (altura {h:0.000} vs mar {SeaLevel01:0.000}) — hay que recolocarla o subir el terreno ahí.");
            else if (margenM < 8f)
                log.AppendLine($"AVISO: '{nombre}' queda muy cerca del nivel del mar (solo {margenM:0.0}m de margen) — revisar a ojo, puede inundarse en parte con el ruido.");
        }

        Chequear("Pueblo Bajo", PuebloBajoCentro);
        Chequear("Castillo", CastilloCentro);
        Chequear("Pueblo de Nieve", PuebloNieveCentro);
        Chequear("Pueblo de Will", PuebloDeWillCentro);
        Chequear("Pueblo Pesquero", PuebloPesqueroCentro);
        Chequear("Bosque Prohibido (esquina NO)", BosqueAreaMinNorm);
        Chequear("Bosque Prohibido (esquina SE)", BosqueAreaMaxNorm);
        Chequear("Bosque Prohibido (esquina NE)", new Vector2(BosqueAreaMaxNorm.x, BosqueAreaMinNorm.y));
        Chequear("Bosque Prohibido (esquina SO)", new Vector2(BosqueAreaMinNorm.x, BosqueAreaMaxNorm.y));
        foreach (var isla in IslasMenores) Chequear(isla.Nombre, isla.CentroNorm);
    }

    /// <summary>Red de seguridad específica para el archipiélago (revisión 7): comprueba, sampleando
    /// alturas reales (no solo mirando las coordenadas), que ningún islote quede pegado al continente ni a
    /// otro islote por un istmo — el mismo tipo de bug ya corregido una vez en la revisión 6 ("la isla
    /// secundaria está pegada a la principal"), ahora con más de un islote en juego. Avisa por log en vez
    /// de fallar en silencio, porque esta sesión no puede abrir el Editor para comprobarlo a ojo.</summary>
    private static void VerificarIslasMenores(System.Text.StringBuilder log)
    {
        Vector2 centroMapa = new Vector2(0.5f, 0.5f);
        foreach (var isla in IslasMenores)
        {
            float hCentro = ComputeHeight01(isla.CentroNorm.x, isla.CentroNorm.y);
            if (hCentro <= SeaLevel01 + 0.01f)
                log.AppendLine($"AVISO: '{isla.Nombre}' casi no asoma sobre el nivel del mar en su centro (altura {hCentro:0.000}) — revisar a ojo.");

            // Punto justo en el aro exterior hacia el continente: si ahí SIGUE por encima del mar, hay istmo.
            Vector2 dir = (centroMapa - isla.CentroNorm).normalized;
            Vector2 puntoHaciaContinente = isla.CentroNorm + dir * (isla.RadioNorm * 1.15f);
            float hHaciaContinente = ComputeHeight01(puntoHaciaContinente.x, puntoHaciaContinente.y);
            if (hHaciaContinente > SeaLevel01 + 0.005f)
                log.AppendLine($"AVISO: '{isla.Nombre}' podría seguir pegada al continente por un istmo (altura justo fuera de su radio, hacia el centro del mapa = {hHaciaContinente:0.000}, mar = {SeaLevel01:0.000}).");

            foreach (var otra in IslasMenores)
            {
                if (otra.Nombre == isla.Nombre) continue;
                float distNorm = Vector2.Distance(isla.CentroNorm, otra.CentroNorm);
                float sumaRadios = isla.RadioNorm + otra.RadioNorm;
                if (distNorm < sumaRadios * 1.3f)
                    log.AppendLine($"AVISO: '{isla.Nombre}' y '{otra.Nombre}' están demasiado cerca entre sí (separación {distNorm:0.000} vs radios {sumaRadios:0.000}) — podrían quedar pegadas o casi.");
            }
        }
    }

    private static Material CrearMaterialColor(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }

    /// <summary>Esfera marcadora en la posición y altura real del terreno para esa zona. Se pinta en rojo
    /// (y se agranda) si queda en/bajo el nivel del mar, para que el problema se vea sin leer el log.</summary>
    private static GameObject CrearMarcadorZona(Terrain terrain, Transform parent, string nombre, Vector2 norm, Color color, float radioVisual)
    {
        float worldX = norm.x * TerrainWidth - TerrainWidth * 0.5f;
        float worldZ = norm.y * TerrainLength;
        float worldY = SampleHeight(terrain, worldX, worldZ);

        float h = ComputeHeight01(norm.x, norm.y);
        bool bajoElMar = h <= SeaLevel01;
        Color colorFinal = bajoElMar ? new Color(0.95f, 0.10f, 0.10f) : color;
        float radioFinal = bajoElMar ? radioVisual * 1.4f : radioVisual;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = bajoElMar ? $"{nombre} (¡BAJO EL NIVEL DEL MAR!)" : nombre;
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(worldX, worldY + radioFinal, worldZ);
        go.transform.localScale = Vector3.one * radioFinal * 2f;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = CrearMaterialColor(colorFinal);
        return go;
    }

    // ==================== PASOS FUTUROS (2-5, no llamados desde el menú todavía) ====================
    // Caminos/puentes de verdad (paso 2), árboles/laberinto del bosque (paso 3), pueblos/castillo (paso 4).
    // Quedan aquí ya escritos de la revisión anterior, listos para engancharse cuando toque ese paso.

    // ==================== CAMINO Y PUENTES ====================

    private static void BuildRoadAndBridges(Terrain terrain, Transform parent, System.Text.StringBuilder log)
    {
        int tramos = 0;
        float dt = RoadSpacingWorld / Vector2.Distance(PathStartNorm * new Vector2(TerrainWidth, TerrainLength), PathEndNorm * new Vector2(TerrainWidth, TerrainLength));
        int index = 0;
        for (float t = 0.02f; t < 0.98f; t += dt)
        {
            if (EstaCercaDeBarranco(t)) continue;

            Vector3 pos = PathWorldPoint(terrain, t);
            Vector3 posSiguiente = PathWorldPoint(terrain, Mathf.Min(t + dt, 1f));
            Vector3 dir = posSiguiente - pos;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);

            string nombre = RoadNames[index % RoadNames.Length];
            PlacePrefab($"{TinyWorldRoads}/{nombre}.prefab", pos, rot, parent);
            index++;
            tramos++;
        }

        int puentes = 0;
        foreach (float rt in RavineTs)
        {
            float tAntes = rt - RavineWidthT * 1.6f;
            float tDespues = rt + RavineWidthT * 1.6f;
            Vector3 pAntes = PathWorldPoint(terrain, tAntes);
            Vector3 pDespues = PathWorldPoint(terrain, tDespues);
            Vector3 centro = (pAntes + pDespues) * 0.5f;
            centro.y = Mathf.Max(pAntes.y, pDespues.y) - 0.3f;
            Vector3 dirPuente = (pDespues - pAntes).normalized;
            Quaternion rotPuente = Quaternion.LookRotation(dirPuente, Vector3.up);

            string nombre = BridgeNames[puentes % BridgeNames.Length];
            GameObject puente = PlacePrefab($"{KingdomBridge}/{nombre}.prefab", centro, rotPuente, parent, $"Puente_Barranco_{puentes + 1}");
            if (puente != null) puentes++;
        }

        log.AppendLine($"Camino: {tramos} tramos de carretera. Puentes: {puentes} (sobre los {RavineTs.Length} barrancos definidos).");
    }

    private static bool EstaCercaDeBarranco(float t)
    {
        foreach (float rt in RavineTs)
            if (Mathf.Abs(t - rt) < RavineWidthT * 1.8f) return true;
        return false;
    }

    private static Vector3 PathWorldPoint(Terrain terrain, float t)
    {
        Vector2 n = PathPointNorm(t);
        float worldX = n.x * TerrainWidth - TerrainWidth * 0.5f;
        float worldZ = n.y * TerrainLength;
        float worldY = SampleHeight(terrain, worldX, worldZ);
        return new Vector3(worldX, worldY, worldZ);
    }

    // ==================== PUEBLOS Y CASTILLO ====================

    private static void BuildVillageCluster(Terrain terrain, Transform parent, Vector2 centroNorm, float radio, string[] buildingNames, string etiqueta, System.Text.StringBuilder log)
    {
        float centroX = centroNorm.x * TerrainWidth - TerrainWidth * 0.5f;
        float centroZ = centroNorm.y * TerrainLength;

        var rng = new System.Random(Seed ^ etiqueta.GetHashCode());
        int colocados = 0;
        for (int i = 0; i < buildingNames.Length; i++)
        {
            float angulo = (i / (float)buildingNames.Length) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
            float radioReal = radio * Mathf.Lerp(0.5f, 1f, (float)rng.NextDouble());
            float worldX = centroX + Mathf.Cos(angulo) * radioReal;
            float worldZ = centroZ + Mathf.Sin(angulo) * radioReal * 0.6f;

            float worldY = SampleHeight(terrain, worldX, worldZ);
            Quaternion rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            GameObject instancia = PlacePrefab($"{KingdomBuildingCombo}/{buildingNames[i]}.prefab", new Vector3(worldX, worldY, worldZ), rot, parent);
            if (instancia != null) colocados++;
        }

        log.AppendLine($"{etiqueta}: {colocados}/{buildingNames.Length} edificios colocados alrededor de ({centroNorm.x:0.00}, {centroNorm.y:0.00}), radio {radio}m.");
    }

    // ==================== BOSQUE PROHIBIDO: LABERINTO DE SENDAS ====================

    private static void BuildBosqueLaberinto(Terrain terrain, Transform parent, System.Text.StringBuilder log)
    {
        var rng = new System.Random(Seed ^ 0x5AF3);
        var grid = new CeldaBosque[BosqueCols, BosqueRows];
        for (int cx = 0; cx < BosqueCols; cx++)
            for (int cz = 0; cz < BosqueRows; cz++)
                grid[cx, cz] = new CeldaBosque();

        var entrada = new Vector2Int(BosqueEntradaCol, BosqueEntradaRow);
        var stack = new Stack<Vector2Int>();
        grid[entrada.x, entrada.y].visitada = true;
        stack.Push(entrada);
        while (stack.Count > 0)
        {
            Vector2Int actual = stack.Peek();
            var sinVisitar = new List<int>();
            for (int dir = 0; dir < 4; dir++)
            {
                int nx = actual.x + DX[dir];
                int nz = actual.y + DZ[dir];
                if (nx < 0 || nx >= BosqueCols || nz < 0 || nz >= BosqueRows) continue;
                if (!grid[nx, nz].visitada) sinVisitar.Add(dir);
            }
            if (sinVisitar.Count == 0) { stack.Pop(); continue; }

            int dirElegido = sinVisitar[rng.Next(sinVisitar.Count)];
            int nx2 = actual.x + DX[dirElegido];
            int nz2 = actual.y + DZ[dirElegido];
            int opuesto = (dirElegido + 2) % 4;
            grid[actual.x, actual.y].conexiones |= (1 << dirElegido);
            grid[nx2, nz2].conexiones |= (1 << opuesto);
            grid[nx2, nz2].visitada = true;
            stack.Push(new Vector2Int(nx2, nz2));
        }

        Vector2Int salida = FindFarthestCellBosque(grid, entrada);
        AddExtraLoopConnectionsBosque(grid, rng, BosqueExtraLoopConnections);

        int callejones = 0;
        for (int cx = 0; cx < BosqueCols; cx++)
            for (int cz = 0; cz < BosqueRows; cz++)
            {
                var pos = new Vector2Int(cx, cz);
                if (pos == entrada || pos == salida) continue;
                if (PopCount(grid[cx, cz].conexiones) == 1) callejones++;
            }

        grid[entrada.x, entrada.y].conexiones |= (1 << 1); // Este, sin muro = por ahí se entra desde el camino
        int dirSalida = DireccionHaciaBorde(salida);
        grid[salida.x, salida.y].conexiones |= (1 << dirSalida);

        float cellWorldX = (BosqueAreaMaxNorm.x - BosqueAreaMinNorm.x) * TerrainWidth / BosqueCols;
        float cellWorldZ = (BosqueAreaMaxNorm.y - BosqueAreaMinNorm.y) * TerrainLength / BosqueRows;
        int arbolesValla = 0, decoraciones = 0;

        for (int cx = 0; cx < BosqueCols; cx++)
        {
            for (int cz = 0; cz < BosqueRows; cz++)
            {
                int conexiones = grid[cx, cz].conexiones;
                Vector3 centro = CeldaBosqueCentroWorld(terrain, cx, cz);

                if ((conexiones & (1 << 0)) == 0) arbolesValla += BuildWallSegmentArboles(terrain, parent, centro, cellWorldX, cellWorldZ, 0, rng);
                if ((conexiones & (1 << 1)) == 0) arbolesValla += BuildWallSegmentArboles(terrain, parent, centro, cellWorldX, cellWorldZ, 1, rng);
                if (cz == 0 && (conexiones & (1 << 2)) == 0) arbolesValla += BuildWallSegmentArboles(terrain, parent, centro, cellWorldX, cellWorldZ, 2, rng);
                if (cx == 0 && (conexiones & (1 << 3)) == 0) arbolesValla += BuildWallSegmentArboles(terrain, parent, centro, cellWorldX, cellWorldZ, 3, rng);

                decoraciones += DecorarCeldaBosque(terrain, parent, centro, cellWorldX, cellWorldZ, rng);
            }
        }

        log.AppendLine($"Bosque Prohibido (laberinto {BosqueCols}x{BosqueRows}): entrada en celda {entrada} (hacia el Pueblo Bajo), salida en celda {salida} (hacia fuera del mapa). {callejones} callejones sin salida. {arbolesValla} árboles de valla ('{BosqueWallTreeName}', un único modelo) + {decoraciones} decoraciones de variedad.");
    }

    private static Vector3 CeldaBosqueCentroWorld(Terrain terrain, int cx, int cz)
    {
        float u = (cx + 0.5f) / BosqueCols;
        float v = (cz + 0.5f) / BosqueRows;
        float xNorm = Mathf.Lerp(BosqueAreaMinNorm.x, BosqueAreaMaxNorm.x, u);
        float zNorm = Mathf.Lerp(BosqueAreaMinNorm.y, BosqueAreaMaxNorm.y, v);
        float worldX = xNorm * TerrainWidth - TerrainWidth * 0.5f;
        float worldZ = zNorm * TerrainLength;
        float worldY = SampleHeight(terrain, worldX, worldZ);
        return new Vector3(worldX, worldY, worldZ);
    }

    private static int BuildWallSegmentArboles(Terrain terrain, Transform parent, Vector3 cellCenter, float cellWorldX, float cellWorldZ, int dir, System.Random rng)
    {
        Vector3 dirVec = new Vector3(DX[dir], 0f, DZ[dir]);
        Vector3 perp = new Vector3(-dirVec.z, 0f, dirVec.x);
        Vector3 edgeCenter = cellCenter + Vector3.Scale(dirVec, new Vector3(cellWorldX, 0f, cellWorldZ)) * 0.5f;
        float edgeLength = (dir == 0 || dir == 2) ? cellWorldX : cellWorldZ;
        int count = Mathf.Max(2, Mathf.RoundToInt(edgeLength / BosqueWallSpacing));

        int colocados = 0;
        for (int i = 0; i <= count; i++)
        {
            float f = (i / (float)count) - 0.5f;
            Vector3 pos = edgeCenter + perp * (f * edgeLength);
            float jitter = ((float)rng.NextDouble() - 0.5f) * 0.6f;
            pos += dirVec * jitter;
            pos.y = SampleHeight(terrain, pos.x, pos.z);

            Quaternion rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            GameObject instancia = PlacePrefab($"{KingdomVegetation}/{BosqueWallTreeName}.prefab", pos, rot, parent);
            if (instancia == null) continue;
            instancia.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.2f, (float)rng.NextDouble());
            colocados++;
        }
        return colocados;
    }

    private static int DecorarCeldaBosque(Terrain terrain, Transform parent, Vector3 centro, float cellWorldX, float cellWorldZ, System.Random rng)
    {
        int cantidad = 2 + rng.Next(3);
        int colocados = 0;
        for (int i = 0; i < cantidad; i++)
        {
            float ox = ((float)rng.NextDouble() - 0.5f) * cellWorldX * 0.7f;
            float oz = ((float)rng.NextDouble() - 0.5f) * cellWorldZ * 0.7f;
            Vector3 pos = centro + new Vector3(ox, 0f, oz);
            pos.y = SampleHeight(terrain, pos.x, pos.z);

            string nombre = BosqueDecoracion[rng.Next(BosqueDecoracion.Length)];
            Quaternion rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            GameObject instancia = PlacePrefab($"{KingdomVegetation}/{nombre}.prefab", pos, rot, parent);
            if (instancia == null) continue;
            instancia.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble());
            colocados++;
        }
        return colocados;
    }

    private static int DireccionHaciaBorde(Vector2Int celda)
    {
        if (celda.x == 0) return 3;
        if (celda.x == BosqueCols - 1) return 1;
        if (celda.y == 0) return 2;
        if (celda.y == BosqueRows - 1) return 0;
        return 3;
    }

    private static Vector2Int FindFarthestCellBosque(CeldaBosque[,] grid, Vector2Int from)
    {
        var dist = new Dictionary<Vector2Int, int>();
        var queue = new Queue<Vector2Int>();
        dist[from] = 0;
        queue.Enqueue(from);
        Vector2Int farthest = from;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (dist[current] > dist[farthest]) farthest = current;

            int conexiones = grid[current.x, current.y].conexiones;
            for (int dir = 0; dir < 4; dir++)
            {
                if ((conexiones & (1 << dir)) == 0) continue;
                var next = new Vector2Int(current.x + DX[dir], current.y + DZ[dir]);
                if (dist.ContainsKey(next)) continue;
                dist[next] = dist[current] + 1;
                queue.Enqueue(next);
            }
        }
        return farthest;
    }

    private static void AddExtraLoopConnectionsBosque(CeldaBosque[,] grid, System.Random rng, int count)
    {
        var candidatos = new List<(Vector2Int a, Vector2Int b, int dir)>();
        for (int x = 0; x < BosqueCols; x++)
        {
            for (int z = 0; z < BosqueRows; z++)
            {
                var a = new Vector2Int(x, z);
                foreach (int dir in new[] { 1, 0 })
                {
                    int nx = x + DX[dir];
                    int nz = z + DZ[dir];
                    if (nx < 0 || nx >= BosqueCols || nz < 0 || nz >= BosqueRows) continue;
                    if ((grid[x, z].conexiones & (1 << dir)) != 0) continue;
                    candidatos.Add((a, new Vector2Int(nx, nz), dir));
                }
            }
        }
        for (int i = candidatos.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidatos[i], candidatos[j]) = (candidatos[j], candidatos[i]);
        }

        int hechas = 0;
        foreach (var candidato in candidatos)
        {
            if (hechas >= count) break;
            int dir = candidato.dir;
            int opuesto = (dir + 2) % 4;
            grid[candidato.a.x, candidato.a.y].conexiones |= (1 << dir);
            grid[candidato.b.x, candidato.b.y].conexiones |= (1 << opuesto);
            hechas++;
        }
    }

    private static int PopCount(int bitmask)
    {
        int count = 0;
        while (bitmask != 0) { count += bitmask & 1; bitmask >>= 1; }
        return count;
    }

    // ==================== AMBIENTE FRÍO (PUEBLO DE NIEVE) ====================

    private static void SetupSnowAmbient(Terrain terrain, Transform parentPuebloNieve, System.Text.StringBuilder log)
    {
        float centroX = PuebloNieveCentro.x * TerrainWidth - TerrainWidth * 0.5f;
        float centroZ = PuebloNieveCentro.y * TerrainLength;
        float centroY = SampleHeight(terrain, centroX, centroZ);
        Vector3 centro = new Vector3(centroX, centroY, centroZ);

        var zonaGo = new GameObject("AmbientZone_PuebloNieve");
        zonaGo.transform.SetParent(parentPuebloNieve);
        zonaGo.transform.position = centro + Vector3.up * 15f;
        var collider = zonaGo.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(PuebloNieveRadio * 2.6f, 60f, PuebloNieveRadio * 2.6f);

        var ambientPreset = AssetDatabase.LoadAssetAtPath<AmbientPreset>(AmbientPresetMountainsPath);
        if (ambientPreset != null)
        {
            var zonaComponent = zonaGo.AddComponent<AmbientZone>();
            var so = new SerializedObject(zonaComponent);
            so.FindProperty("ambientPreset").objectReferenceValue = ambientPreset;
            so.ApplyModifiedProperties();
            log.AppendLine("Pueblo de Nieve: AmbientZone enganchada a AmbientPreset_Mountains (ya existente en el proyecto).");
        }
        else
        {
            log.AppendLine($"AVISO: no se encontró '{AmbientPresetMountainsPath}' — la AmbientZone del Pueblo de Nieve se creó sin preset asignado, asígnalo a mano en el Inspector.");
        }

        var nieveGo = new GameObject("Nieve_Particulas");
        nieveGo.transform.SetParent(parentPuebloNieve);
        nieveGo.transform.position = centro + Vector3.up * 25f;
        var ps = nieveGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 7f;
        main.startSpeed = 1.2f;
        main.startSize = 0.12f;
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;

        var emission = ps.emission;
        emission.rateOverTime = 60f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(PuebloNieveRadio * 2.4f, 1f, PuebloNieveRadio * 2.4f);

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-1.5f);

        log.AppendLine("Pueblo de Nieve: sistema de partículas de nieve añadido (material por defecto — cambiar en el Inspector si Raúl quiere un copo propio).");
    }

    // ==================== UTILIDADES ====================

    private static GameObject PlacePrefab(string prefabPath, Vector3 worldPos, Quaternion rot, Transform parent, string nombreOverride = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[NuevoMundoTerrenoBuilder] No se encontró el prefab '{prefabPath}'.");
            return null;
        }
        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instancia.transform.position = worldPos;
        instancia.transform.rotation = rot;
        instancia.name = nombreOverride ?? prefab.name;
        return instancia;
    }

    private static float SampleHeight(Terrain terrain, float worldX, float worldZ)
    {
        Vector3 terrainPos = terrain.transform.position;
        float sample = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));
        return sample + terrainPos.y;
    }

    private static Scene OpenOrCreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        string dir = System.IO.Path.GetDirectoryName(ScenePath).Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir)) EnsureFolder(dir);
        EditorSceneManager.SaveScene(newScene, ScenePath);
        return newScene;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
