using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Maqueta independiente del mapa de Eldoria. Cada ejecución crea una versión nueva.
///
/// Generada originalmente por Codex (10 sep 2026) como maqueta de composición, no integración de gameplay
/// — importa geometría real de las escenas demo del Fantasy_Kingdom_Pack en vez de marcadores. Codex dejó
/// escrito antes de quedarse sin uso: "toca corregir esa integración: conservar o reconstruir el suelo
/// local de cada pueblo, unirlo al terreno exterior y hacer continuo el río. Después podremos trabajar el
/// parecido artístico con el boceto. No te pediría otra regeneración hasta corregir esos problemas de
/// base." Dos arreglos de Claude siguiendo ese plan, ese mismo día:
/// 1. **Suelo local de cada pueblo.** `Importar` borraba el `Terrain` de la escena demo de origen sin
///    guardar nada de él, y el terreno nuevo se aplanaba a una altura CONSTANTE dentro de la zona — solo
///    coincide con el edificio que estuviera justo en el punto más bajo del grupo importado (el ancla
///    vertical); el resto, si la demo original tenía algo de relieve entre edificios, se quedaba flotando o
///    semienterrado al no haber ya nada debajo que lo graduara. Ahora `MuestrearRelieve` captura la altura
///    real del `Terrain` de origen ANTES de borrarlo, y `AlturaRelieveZona` reconstruye ese relieve real
///    (no una constante) como objetivo del aplanado — ver comentarios en `Zona.muestrasRelieve`.
/// 2. **Río discontinuo.** El cauce se cava en `Altura` a partir de las alturas a mano de `rioSuave`, pero
///    el terreno final en cada punto es el resultado de mezclar ese cauce con todo lo demás (caminos,
///    zonas, macizo, costa) — no tiene por qué coincidir exactamente. La cinta visible del río usaba esas
///    alturas a mano tal cual, así que en los tramos donde otra cosa "ganaba" el mezclado el agua quedaba
///    flotando o cortando el terreno. Se arregla igual que ya se hacía para el arroyo de la ladera este (el
///    propio archivo ya tenía la técnica, solo no se había aplicado al río principal): la cinta visible se
///    apoya en la altura REAL ya cavada (`terreno.SampleHeight`), no en el valor a mano.
///
/// Revisión 2 (Claude, mismo día) — parecido artístico con el mapa de referencia "Isla de Eldoria", que era
/// justo el paso siguiente que Codex dejó apuntado. Todo son constantes al principio del archivo:
/// - **Islotes rocosos** alrededor de la isla (`Islotes`): bultos sobre el fondo marino, independientes del
///   polígono de costa; salen de roca por pendiente (ver abajo) con un anillo de arena.
/// - **Playas anchas** (`Playas`): en esas zonas la subida estándar desde el mar (con la que la franja de
///   arena quedaba en ~20 m, invisible desde arriba) se sustituye por una plataforma de arena casi llana.
/// - **Roca por pendiente**, no solo por altura: acantilados de costa, laderas de los picos e islotes
///   salen grises; de paso el alphamap se calcula leyendo el heightmap ya hecho en vez de volver a llamar
///   a `Altura` 262.000 veces (la generación tarda ~la mitad).
/// - **Cuarto pico** al noreste (`Pico` en `Altura`), para que la cordillera cruce todo el norte como en
///   el mapa y el arroyo de la ladera este caiga desde algo.
/// - **Hitos** (`Hitos`): portal del Bosque Prohibido al final del camino 4, torre vigía a modo de faro
///   sobre un cabo rocoso al sureste del puerto (el pack no trae faro), velero en mar abierto, y puentes
///   reales del pack en cada cruce camino×río (detectado por intersección de segmentos, no a mano). Cada
///   hito se escala a un tamaño objetivo medido por bounds, así no depende de la escala nativa del pack.
/// - **Vegetación con variedad** fuera del Bosque Prohibido (siete familias de árbol), rocas del pack
///   por la costa y los islotes, y sin árboles en playas ni encima de los hitos.
///
/// Revisión 3 (Claude, mismo día, tras la primera captura cenital de la revisión 2): playas a la mitad de
/// radio (se comían un cuarto de la isla), islotes con alto ≈ 0.9×radio para que salgan de roca y no de arena,
/// tramos de `Acantilados` con la costa vertical (norte, noreste, este), dos octavas más de relieve + crestas
/// en el macizo (la isla era una masa lisa), alfa 0 en las texturas de capa (URP TerrainLit lee la suavidad
/// del alfa del albedo — la isla brillaba), y árboles/rocas normalizados a una altura real medida por bounds
/// (los árboles sueltos salían 10 veces más grandes que los del bosque por la escala nativa de cada prefab).
///
/// Revisión 4 (Claude, mismo día, tras las capturas de cerca de la revisión 3 y decisiones de Raúl): las demos
/// CONSERVAN su Terrain propio (se perdía el suelo pintado y los cultivos del pueblo inicial); caminos pintados en
/// el terreno (capa 5) en vez de mallas; río/arroyo como tira continua (adiós zigzag) y arena solo cerca del mar
/// (salían orillas de arena río arriba); rocas antes que árboles, reservando sitio; puentes de exterior
/// (`Bridge03` de RPG Tiny); portal del bosque eliminado; pueblo de la demo 06 movido al pie del pico noreste con
/// el arroyo atravesándolo y una cascada a la entrada; y la cima pasa a ser la ciudadela amurallada de la demo 10.
///
/// Revisión 5 (Claude, mismo día, tras la captura de la revisión 4 — "una peora"): la demo 10 en la cima era
/// tan grande que su aplanado se comía los picos, y conservar el Terrain de las demos 05/06 metía cuadrados
/// verdes lisos. Se revierten las dos cosas: Terrain propio solo en la demo 09 (`conservarTerreno`), y en la
/// cima un pueblo construido a mano y compacto (iglesia, pozo, dos torres en la entrada, ocho casas variadas,
/// pinos) con la elipse de aplanado contenida a propósito. Pedido de Raúl: "mejor colocamos nosotros las casitas,
/// castillos y eso y lo ponemos bonito".
///
/// Revisión 6 (Claude, mismo día): pueblo de la montaña en TERRAZAS semicirculares con castillo arriba (mapa +
/// pedido de Raúl), mediante un relieve a medida por zona (`Zona.relieve`) y sin que el camino rebaje las
/// terrazas; cascada reutilizando el prefab `WaterFall_1_Stylized` de MainWorld (por GUID) en vez de la lámina
/// plana de RPG Tiny; `Colocar` mide cualquier `Renderer` (partículas incluidas).
///
/// Revisión 7 (Claude, mismo día): pueblo de montaña — hierba en las terrazas (la roca por altura se anula
/// dentro de la zona; queda solo la roca por pendiente en las cuestas), camino en espiral de la puerta baja
/// al castillo por cada terraza (ruta 6 de `Caminos`), el camino de montaña termina en la puerta baja, más
/// casas por terraza (6/8/10), castillo a 56 m y más pinos.
///
/// Revisión 8 (Claude, mismo día): la maqueta de las 16:19 se quedó sin .unity ni Informe — un fallo en un paso
/// posterior al terreno abortaba todo. Ahora hitos, vegetación, verificación y validación narrativa van cada uno
/// en `Paso(...)`: el error se anota en el informe y en la consola, y la escena se guarda igual. Rampa final al
/// castillo suavizada (estaba al 49 %, el límite es 50 %).
///
/// Revisión 9 (Claude, mismo día): castillo = `Castle01_b01` (el del Reino en MainWorld) en una meseta más
/// ancha (radio 40, terrazas desplazadas); pueblo de la cascada girado -42° para quedar paralelo al arroyo, que
/// ahora pasa a su lado y no por en medio; y un acantilado real de 18 m tallado en el terreno (`CascadaCentro`)
/// por el que cae el arroyo con el prefab de cascada de MainWorld — sin escalón se veía como láminas tumbadas.
///
/// Revisión 10 (Claude, mismo día): pueblo de la cascada — la composición de la demo 06 (tira de casas, muelle,
/// noria en un extremo) se mantiene, pero se arregla el ENTORNO: pueblo girado 180° para que la noria quede en el
/// extremo noroeste junto al acantilado, cascada movida a ese extremo (181,269), arroyo cayendo por ella y
/// corriendo por delante del pueblo (lado del muelle) hasta el mar, puente donde el camino 2 cruza el arroyo,
/// pinar en la ladera de detrás y rocas en la ceja del acantilado. El informe lista ahora cada pieza de la demo
/// con su prefab real, por si se decide rehacerla pieza a pieza.
///
/// Revisión 12 (Claude): fuera el "pueblo del río" de la demo 06 (en el mapa no existe: el este es cascada +
/// poza + puente); zona `Poza` con relieve de cuenco y disco de agua; sin VFX de cascada (otro arte) — la lámina
/// del arroyo cae por el acantilado; `AsentarPiezas` apoya en el terreno cada pieza de las demos importadas
/// (vallas, farolas… flotaban), salvo barcos y muelles.
///
/// Revisión 13 (Claude): poza más honda y de borde limpio; red de sendas del Bosque Prohibido (pintadas, con
/// bucles y callejones); casitas del pueblo de la montaña elegidas midiendo los 55 edificios del pack (pequeñas
/// y sin repetir).
///
/// Revisión 14 (Claude): caminos recortados en el borde de los pueblos importados (se pintaban por encima de las
/// casas hasta el centro); margen extra de aplanado en el pueblo pesquero (piezas de la orilla colgadas) y
/// tolerancia de asentado 6 m.
///
/// Revisión 15 (Claude): el suelo "chulísimo" del pueblo inicial (tierra, flores, cultivos) era la pintura del
/// Terrain de la demo 09, que se descartaba por tamaño. Ahora ese Terrain se reserva y su pintura se COPIA al
/// terreno de la isla dentro de la huella del pueblo — alturas exactas, capas de textura, flores/hierba de
/// detalle (resolución 2048) y árboles de terreno — y después se destruye: ni cuadrado ni suelo perdido.
///
/// Revisión 16 (Claude): flores de la demo por fin visibles (mismo modo de densidad que la demo y distancia de
/// detalle 400 m); copia de suelo también en el pesquero; praderas de hierba/flores por toda la isla con los
/// prototipos de la demo; zonas de lore nuevas — claro de la Piedra Ancestral en el islote sureste (cristal +
/// pilares provisionales) y caserío del "pueblo vecino" en la playa este; cráter de la montaña de Estela junto al
/// castillo.
///
/// Revisión 18 (Claude, 11 sep): tres reglas de diseño de Raúl (propuesta-separacion-zonas-lore-vegetacion-isla-
/// piedra-ancestral-2026-09-11.md). (1) Zonas de lore separadas: las Ruinas / Piedra Ancestral SALEN del Bosque
/// Prohibido (estaban en (-350,205) con el camino 4 llegando hasta ellas) y pasan a su propio islote del sureste, a
/// la vista del puerto — se llega en barco; el camino 4 muere en la boca del bosque. (2) Isla-jungla del hechicero
/// (exilio) frente a la playa este, a nado, con claro, cabaña y pozo; el caserío de la playa se queda como pueblo
/// vecino. (3) Paleta de vegetación POR ZONA con los colores reales del pack (`VegetacionPaletaInforme.md`):
/// Bosque Prohibido verde denso, Fuego Fatuo verde oscuro con setas azules, jungla turquesa/lima, Ruinas con todos
/// los rojos y otoñales del pack y acento azul/violeta junto a la Piedra, campo en verdes claros — más hierba y
/// setas del pack por paraje (`SueloVegetal`). Los árboles de jungla y ruinas van SIN el tinte verde de `MatizarArbol`.
///
/// Sigue siendo una maqueta aparte de `NuevoMundoTerrenoBuilder.cs` (Assets/Scripts/Editor) — mismo
/// problema (el mundo nuevo de "Isla de Eldoria"), dos scripts sin relación entre sí, sin riesgo de
/// pisarse: escenas y carpetas de assets distintas. Ver propuesta-mundo-nuevo-terreno-montanas-pueblos-
/// 2026-09-10.md (proyecto de Claude) para cómo se comparan.</summary>
public static partial class EldoriaCodexBuilder
{
    const string Demo = "Assets/Art/World/Fantasy_Kingdom_Pack/Demo/";
    const string Pack = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/";
    const string Tiny = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/";
    // Islotes rocosos (x, z, radio, alto sobre el fondo marino de -12 m) — revisión 2. Todos quedan a 75-115 m
    // fuera del polígono de costa (comprobado con `CostaDist`), o sea ~60-100 m de agua real hasta la orilla.
    // Colocados siguiendo el mapa: grupo noroeste, oeste, suroeste, sur, sureste, este, noreste y norte.
    // Revisión 18 (Claude): dos islotes HABITABLES, más grandes que los decorativos (tierra real ≈ 55-60 % del radio
    // base, medido sobre `RelieveIslote`): la isla-jungla del hechicero (exilio, se llega a nado desde la costa este —
    // ~65 m de agua) y la isla de las Ruinas / Piedra Ancestral (más lejos, ~95 m de agua: se llega en barco desde el
    // puerto, que la tiene a la vista al este). Sustituyen a los islotes decorativos (520,-200) y (500,-420).
    // Revisión 19 (Claude, tras las capturas de la 18: "islas inaccesibles, a la misma distancia, pobres y sin chicha"):
    // los dos islotes habitables dejan de ser bultos de `RelieveIslote` + aplanado de zona (salía una meseta pelada con
    // acantilado por todos lados y SIN sitio para árboles — ver `RelieveIsloteHabitable`). Ahora cada uno tiene una
    // PLAYA en el sector que mira hacia donde se llega (`…Playa`), acantilado en el resto, lomas suaves dentro y un claro
    // pequeño en el centro. Distancias distintas a propósito: la jungla a ~50 m de agua de la playa este (a nado); las
    // Ruinas con su playa mirando al puerto pesquero (~180 m de mar, en barco) y acantilado hacia la costa más cercana.
    static readonly Vector4 IsloteJungla = new Vector4(545,-180,100,0);   // x, z, radio base, (sin uso)
    static readonly Vector2 JunglaPlaya = new Vector2(-.85f,.53f);          // hacia la playa este del continente
    const float JunglaCota = 16; // baja, para que la rampa de la playa sea caminable (<35 %)
    static readonly Vector4 IsloteRuinas = new Vector4(565,-505,105,0);
    static readonly Vector2 RuinasPlaya = new Vector2(-.98f,.21f);          // hacia el puerto pesquero
    const float RuinasCota = 18;
    // Revisión 20 (Claude): el mar como zona jugable (propuesta-mar-jugable-ruta-ruinas-archipielago-isla-secreta-2026-09-11.md).
    // (1) Ruta difícil a las Ruinas: el barco fondea en "El Rompiente" (islote rocoso al sur del puerto) y desde ahí una
    // cadena de escollos unidos por pasarelas de tablones (que en gameplay se derrumbarán al cruzarlas — punto de no
    // retorno) lleva a la playa de las Ruinas; arrecife de rocas del pack asomando del agua alrededor de la isla y un
    // pecio encallado. (2) Isla secreta en la esquina noreste, detrás del macizo, con la playa mirando al mar abierto
    // (desde el continente solo se ven acantilados): cabaña del náufrago y paleta propia (lima + setas magenta/mostaza).
    // Revisión 21: el tercer escollo caía SOBRE la plataforma de arena de las Ruinas (que llega hasta r≈1.15) y la última
    // pasarela salía de 61 m tumbada en la playa. Cadena desplazada al oeste y plataforma de arena acortada (Paisaje.cs).
    static readonly Vector4 Rompiente = new Vector4(385,-565,26,30);           // x, z, radio base, alto — mismo formato que `Islotes`
    static readonly Vector4[] Escollos = { new Vector4(415,-553,14,22), new Vector4(435,-546,13,21), new Vector4(453,-538,13,21) };
    static readonly Vector4 IsloteSecreta = new Vector4(560,470,78,0); // revisión 21: algo mayor
    static readonly Vector2 SecretaPlaya = new Vector2(.7f,.7f);            // hacia mar abierto (noreste): de cara al continente, acantilado
    const float SecretaCota = 14;
    // Revisión 22: "Las Hermanas" — archipiélago del oeste (Raúl: "solo hay islas por el lado derecho"). Tres islotes al
    // noroeste encadenados a nado desde la playa oeste y uno al suroeste. Contenido secundario: cofre, altar y guarida
    // (marcadores). Formato: islote (x, z, radio), playa (dirección), cota.
    static readonly (Vector4 islote, Vector2 playa, float cota)[] Hermanas = {
        (new Vector4(-500,470,60,0), new Vector2(.61f,-.79f), 14),   // Hermana mayor: playa hacia el continente
        (new Vector4(-580,380,55,0), new Vector2(.67f,.75f), 16),    // Hermana del altar: playa hacia la mayor
        (new Vector4(-420,560,50,0), new Vector2(-.67f,-.75f), 12),  // Hermana pequeña: playa hacia la mayor
        (new Vector4(-560,-560,60,0), new Vector2(.6f,.8f), 13)      // Isla del Gigante (suroeste): guarida del boss extra
    };
    static readonly Vector4[] Islotes = {
        // Revisión 3: alto ≈ 0.9×radio para que la ladera pase de 29° y salga de roca (con 0.5×radio salían de arena).
        new Vector4(-450,340,70,62), new Vector4(-540,230,50,45), new Vector4(-600,-60,55,50), new Vector4(-590,-200,40,36),
        new Vector4(-520,-400,60,54), new Vector4(-400,-500,45,40), new Vector4(20,-560,50,45),
        new Vector4(560,80,45,40), new Vector4(370,420,65,58), new Vector4(430,300,45,40),
        new Vector4(180,590,50,45),
        // Cabo rocoso del faro, pegado al pueblo pesquero (dentro del polígono, en su borde) — ver `CaboFaro`.
        new Vector4(365,-470,45,34)
    };
    static readonly Vector2 CaboFaro = new Vector2(365,-470);
    // Cascada (revisión 9): un acantilado real de `CascadaSalto` m tallado en el terreno en `CascadaCentro`, con
    // el arroyo cayendo por él justo antes de entrar al pueblo. Sin escalón el prefab de cascada se veía como
    // láminas azules tumbadas en una ladera ("horrible"). La dirección del salto es la del arroyo ahí.
    static readonly Vector3 CascadaCentro = new Vector3(181,0,269); // revisión 10: pegada al extremo noroeste del pueblo (el de la noria)
    static readonly Vector2 CascadaDireccion = new Vector2(0.46f,-0.89f); // aguas abajo (bordea el pueblo por el suroeste)
    const float CascadaSalto = 18, CascadaRadio = 26;
    static float cascadaNivelBajo = float.NaN; // cota del pueblo junto al acantilado, se fija tras importar la demo 06
    // Playas anchas (x, z, radio) — revisión 2: playa oeste (donde termina el camino 5), playa este de las
    // cabañas, y la playa junto al pueblo pesquero. Dentro de cada una el terreno costero es una plataforma de
    // arena casi llana (1.2 → 5.5 m) en vez de la subida estándar; la zona del pueblo se aplica después y manda.
    static readonly Vector3[] Playas = { new Vector3(-390,-290,65), new Vector3(410,-100,55), new Vector3(150,-440,55) }; // revisión 3: radios a la mitad (salían playas de 180 m que se comían un cuarto de la isla)
    // Acantilados (x, z, radio) — revisión 3: en estos tramos la costa sube en ~30 m en vez de 85, así el
    // borde queda vertical y sale de roca por pendiente (norte, noreste y este, como en el mapa).
    static readonly Vector3[] Acantilados = { new Vector3(-120,430,150), new Vector3(400,250,140), new Vector3(480,60,110) };
    static readonly Vector2[] Costa = {
        new Vector2(-510,-220), new Vector2(-430,-390), new Vector2(-240,-480),
        new Vector2(-70,-440), new Vector2(120,-510), new Vector2(360,-470),
        new Vector2(455,-310), new Vector2(400,-170), new Vector2(505,0),
        new Vector2(430,180), new Vector2(300,230), new Vector2(245,435),
        new Vector2(100,520), new Vector2(-80,475), new Vector2(-200,350),
        new Vector2(-365,295), new Vector2(-465,160), new Vector2(-525,-30)
    };
    sealed class Zona
    {
        public string nombre, demo;
        public Vector3 centro;
        public Vector2 mitad;
        public Transform grupo;
        // Relieve local del pueblo (arreglo de Claude sobre el plan que dejó escrito Codex: "conservar o
        // reconstruir el suelo local de cada pueblo, unirlo al terreno exterior"). Antes, `Importar` tiraba
        // el `Terrain` de la escena demo de origen sin más, y el hueco se rellenaba aplanando el terreno
        // nuevo a una altura CONSTANTE (`zona.centro.y`) dentro de la elipse de la zona — eso solo coincide
        // con el edificio que estuviera justo en el punto más bajo del grupo (el ancla vertical, ver más
        // abajo); cualquier otro edificio del mismo pueblo, si el terreno original de la demo tenía algo de
        // relieve entre ellos, se queda flotando o semienterrado, porque ya no hay nada debajo que lo
        // gradúe. Aquí se muestrea la altura real del `Terrain` de origen ANTES de borrarlo (ver
        // `MuestrearRelieve`) y se usa esa forma real — no una constante — como objetivo del aplanado en
        // `AlturaRelieveZona`. Queda vacía (y se usa el aplanado plano de siempre) para el pueblo de
        // montaña, que no viene de ninguna demo y nunca tuvo terreno propio que perder.
        public List<Vector3> muestrasRelieve;
        // Revisión 4: la demo conserva su propio Terrain (suelo pintado, cultivos, parches con borde — todo lo
        // que se perdía al borrarlo); nuestro terreno se hunde 0.35 m bajo él dentro de la zona para no pelearse.
        public bool terrenoPropio;
        // Revisión 5: conservar el Terrain de la demo es opcional por zona. Con las demos 05 y 06 salían
        // cuadrados verdes lisos (su terreno es un parche plano sin pintar, mucho mayor que el pueblo); solo
        // merece la pena en la 09, que trae el suelo pintado y los cultivos.
        public bool conservarTerreno;
        // Revisión 15: Terrains de la demo que se guardan hasta construir el nuestro para COPIAR su pintura
        // (alturas exactas, capas de textura, flores/hierba de detalle y árboles de terreno) dentro de la huella
        // del pueblo, y luego se destruyen. Así el pueblo inicial recupera su suelo sin dejar un cuadrado.
        public List<Terrain> terrenosOrigen;
        // Revisión 6: relieve a medida de la zona (x, z, altura natural) → altura objetivo. Lo usa el pueblo de
        // la montaña para sus terrazas; si es null se usa el relieve de la demo o la cota fija de siempre.
        public Func<float,float,float,float> relieve;
        // Revisión 9: giro (grados, eje Y) del grupo importado tras colocarlo — para poner el pueblo de la
        // cascada paralelo al arroyo. Con giro, la elipse de aplanado pasa a ser un círculo del radio mayor.
        public float giro;
        public bool esPuerto; public bool esPoza; // revisión 12: zona de la poza de la cascada (relieve de cuenco, sin edificios)
        public bool esClaro;   // revisión 16: claro llano a cota fija (Piedra Ancestral en el islote sureste)
        public bool esCaserio; // revisión 16: caserío de tres casitas y pozo (cabañas de la playa este = "pueblo vecino" del hechicero)
        public bool esJungla;  // revisión 18: claro con la cabaña del hechicero en la cima de la isla-jungla
        public bool esIslote;  // revisión 19: zona sobre un islote habitable — sin aplanado de zona (lo hace el propio relieve del islote)
        public bool esSecreta; // revisión 20: la isla secreta del mito (cabaña del náufrago)
        public bool esGranja;  // revisión 22: granjas al pie de la cascada ("la esquina desaprovechada")
    }
    static readonly Zona[] Zonas = {
        new Zona { nombre="Pueblo inicial — Will — Demo 09", demo="09", centro=new Vector3(0,24,-130), conservarTerreno=true },
        new Zona { nombre="Pueblo pesquero — muelles y viviendas", demo=null, esPuerto=true, centro=new Vector3(270,5,-440) }, // revisión 16: también copia su suelo
        // Revisión 4: el pueblo de la demo 06 (con su noria) pasa al pie del pico noreste, atravesado por el arroyo
        // de la ladera este, con una cascada a la entrada — "pueblo pegado a la montaña" (pedido de Raúl).
        // Revisión 12: fuera el "pueblo del río" (demo 06) — en el mapa no hay ningún pueblo ahí: solo la cascada
        // cayendo a una poza entre acantilados. Zona de la poza: cuenco llano y hundido al pie del salto.
        new Zona { nombre="Poza de la cascada", demo=null, esPoza=true, centro=new Vector3(191,42,249) },
        // Revisión 16 — zonas del lore que faltaban: la Piedra Ancestral (claro apartado, se llega a nado al islote
        // sureste; guardián de piedra y portal al Sendero) y el "pueblo vecino" del hechicero amigo de Eldran, como
        // caserío de cabañas en la playa este (el mapa dibuja cabañas y un pequeño muelle ahí).
        // Revisión 18 (Claude): la Piedra Ancestral / Ruinas del Libro SALE del Bosque Prohibido (estaba en (-350,205),
        // dentro del bosque, con camino propio) y pasa a su islote del sureste — regla de Raúl: cada zona de lore se
        // revela en su momento, el bosque no lleva ruinas/monolitos/santuarios (propuesta-separacion-zonas-lore-…-2026-09-11.md).
        new Zona { nombre="Piedra Ancestral — Ruinas del Libro (isla, se llega en barco)", demo=null, esClaro=true, esIslote=true, centro=new Vector3(565,RuinasCota,-505) },
        new Zona { nombre="Pueblo vecino — terraza oriental", demo=null, esCaserio=true, centro=new Vector3(330,23,-115) },
        // Revisión 5: la demo 10 (ciudadela) era tan grande que su aplanado se comía los picos — "una peora".
        // Vuelve el pueblo construido a mano, pero con iglesia, torres, fortín, pozo, casas variadas y pinos.
        new Zona { nombre="Pueblo de la Montaña — construido a mano", demo=null, centro=new Vector3(15,116,295) },
        // Revisión 18 (Claude): isla-jungla del hechicero (exiliado; se llega a nado desde la playa este). Va al FINAL del
        // array a propósito: Zonas[2], [4] y [5] se usan por índice en otros sitios.
        new Zona { nombre="Isla del hechicero — jungla (claro y cabaña)", demo=null, esJungla=true, esIslote=true, centro=new Vector3(545,JunglaCota,-180) },
        // Revisión 20: isla secreta (mito de la taberna del puerto). También al final del array.
        new Zona { nombre="Isla secreta — cabaña del náufrago (no aparece en el mapa)", demo=null, esSecreta=true, esIslote=true, centro=new Vector3(560,SecretaCota,470) },
        // Revisión 22: granjas al pie de la cascada — el rincón entre la poza, el bosque del Fuego Fatuo y la costa este
        // estaba vacío. Sitio natural para "el campo de Erika" del GDD (aún sin ubicación). Al final del array (índices fijos).
        new Zona { nombre="Granjas de la cascada — campo de Erika (GDD)", demo=null, esGranja=true, centro=new Vector3(250,38,150) }
    };
    static readonly Vector3[][] Caminos = {
        new[]{new Vector3(0,24,-130),new Vector3(85,23,-220),new Vector3(170,16,-285),new Vector3(215,9,-365),new Vector3(270,5,-440)},
        new[]{new Vector3(0,24,-130),new Vector3(110,30,-50),new Vector3(190,36,80),new Vector3(232,38,150),new Vector3(250,44,240)}, // revisión 22: pasa por las granjas de la cascada antes de subir a la poza (cruza el arroyo: puente automático)
        // Acceso al Reino: subida larga con curvas y una rasante de diseño inferior al 20 %.
        new[]{new Vector3(0,24,-130),new Vector3(100,38,40),new Vector3(40,48,90),new Vector3(-80,59,130),new Vector3(-125,70,175),new Vector3(-110,80,225)},
        // Revisión 18: el camino 4 termina en la boca del Bosque Prohibido (donde nacen las sendas); ya no sigue hasta las ruinas, que se han ido a su isla.
        new[]{new Vector3(0,24,-130),new Vector3(-120,25,-90),new Vector3(-240,28,15),new Vector3(-305,40,105)},
        new[]{new Vector3(0,24,-130),new Vector3(-130,19,-240),new Vector3(-290,7,-305),new Vector3(-380,3,-285)},
        // Pueblo vecino: llegada a través de la arboleda del Fuego Fatuo, con salida a su plaza.
        new[]{new Vector3(190,36,80),new Vector3(268,34,102),new Vector3(350,32,68),new Vector3(292,28,12),new Vector3(350,25,-48),new Vector3(330,23,-115)},
        // Accesos a los claros y al encargo inicial; el barrio alto sigue siendo la última ruta.
        new[]{new Vector3(-305,40,105),new Vector3(-330,36,65)},
        new[]{new Vector3(-240,28,15),new Vector3(-95,32,65),new Vector3(-15,40,55),new Vector3(40,48,90)},
        new[]{new Vector3(-85,25,-100),new Vector3(-62,25,-61)},
        // Calles del barrio alto: el camino llega a la explanada, no trepa por las terrazas.
        new[]{new Vector3(-110,80,225),new Vector3(0,90,225),new Vector3(95,102,235),new Vector3(95,112,290),new Vector3(50,112,300),new Vector3(0,112,300),new Vector3(0,112,315)}
    };
    // Sendas del Bosque Prohibido (revisión 13): red de senderos que se cruzan y cierran en bucle, como el laberinto
    // del diseño. Solo se PINTAN (capa de camino, más estrechas) y despejan árboles a 6 m; no tocan el relieve.
    // Parten del final del camino 4 (-305,105). Se pueden añadir tramos libremente.
    static readonly Vector3[][] Sendas = {
        new[]{new Vector3(-305,0,105),new Vector3(-350,0,150),new Vector3(-400,0,120),new Vector3(-430,0,60),new Vector3(-390,0,0),new Vector3(-330,0,-40),new Vector3(-280,0,-90),new Vector3(-240,0,-60)},
        new[]{new Vector3(-305,0,105),new Vector3(-270,0,160),new Vector3(-300,0,210),new Vector3(-360,0,230),new Vector3(-420,0,190),new Vector3(-440,0,140)},
        new[]{new Vector3(-330,0,-40),new Vector3(-250,0,-20),new Vector3(-200,0,20),new Vector3(-170,0,80),new Vector3(-210,0,140),new Vector3(-270,0,160)},
        new[]{new Vector3(-400,0,120),new Vector3(-360,0,60),new Vector3(-300,0,40),new Vector3(-250,0,-20)},
        new[]{new Vector3(-360,0,230),new Vector3(-330,0,270)}, // callejón sin salida hacia el norte
        new[]{new Vector3(-430,0,60),new Vector3(-470,0,20)}   // callejón sin salida hacia la costa
    };
    static Vector3[][] sendasSuaves;
    static string carpeta;
    static readonly Vector3[] Rio = {new Vector3(-165,32,210),new Vector3(-210,27,85),new Vector3(-165,22,-30),new Vector3(-175,18,-160),new Vector3(-80,12,-270),new Vector3(80,6,-330),new Vector3(155,2,-405),new Vector3(185,0,-520)};
    static Vector3[][] rutasSuaves;
    static Vector3[] rioSuave;
    static Vector3[] arroyoSuave; // arroyo de la ladera este, ya apoyado en el terreno (revisión 4)
    static Terrain terreno;
    static readonly List<Bounds> barcos = new List<Bounds>();
    static readonly List<Bounds> terrenosDemo = new List<Bounds>(); // parches de Terrain de la demo en curso (revisión 4)
    // Zonas a despejar de vegetación (x, z, radio): las rellena `Hitos` (portal, faro, puentes, velero).
    static readonly List<Vector3> despejes = new List<Vector3>();
    static Transform raiz;
    static System.Text.StringBuilder informe;

    [MenuItem("El Sendero/Eldoria Codex/Crear nueva maqueta del mapa")]
    public static void Crear()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play antes de crear la maqueta.");
        foreach (var zona in Zonas)
            if (zona.demo!=null && !File.Exists(Demo+zona.demo+".unity")) throw new FileNotFoundException(Demo+zona.demo+".unity");
        var anterior = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(escena);
        carpeta = "Assets/Scenes/Worlds/EldoriaCodex_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(carpeta);
        AssetDatabase.Refresh();
        informe = new System.Text.StringBuilder("Eldoria Codex — maqueta de composición, no integración de gameplay.\n");
        try
        {
            raiz = new GameObject("ELDORIA — propuesta Codex").transform;
            barcos.Clear(); terrenosDemo.Clear(); factores.Clear(); despejes.Clear(); PrepararUrbanismo(); capasIntegradas.Clear();
            arroyoSuave=null; cascadaNivelBajo=float.NaN;
            foreach(var zona in Zonas) { zona.terrenoPropio=false; zona.muestrasRelieve=null; zona.relieve=null; zona.terrenosOrigen=null; }
            sendasSuaves=new Vector3[Sendas.Length][];for(int i=0;i<Sendas.Length;i++)sendasSuaves[i]=Suavizar(Sendas[i]);
            rutasSuaves=new Vector3[Caminos.Length][];
            for(int i=0;i<Caminos.Length;i++)rutasSuaves[i]=Suavizar(Caminos[i]);
            rioSuave=Suavizar(Rio);
            foreach(var zona in Zonas)
                if(zona.esPuerto) ConstruirPuerto(zona); else if(zona.esPoza) Poza(zona); else if(zona.esClaro) Claro(zona); else if(zona.esJungla) Jungla(zona); else if(zona.esSecreta) Secreta(zona); else if(zona.esGranja) Granja(zona); else if(zona.esCaserio) Caserio(zona); else if(zona.demo==null) PuebloMontana(zona); else Importar(zona, escena);
            RecortarCaminosEnPueblos(); // revisión 14: los caminos paran en el borde de cada pueblo importado, no en su centro
            // Cota baja del acantilado de la cascada = relieve del pueblo de la cascada extrapolado a ese punto.
            PrepararEspacios();
            cascadaNivelBajo=Zonas[2].centro.y; // borde de la poza (revisión 12)
            PrepararArroyo();
            ConstruirTerreno();
            // Revisión 10: el mismo material de agua que MainWorld (`Water`, shader ithappy/WaterURP, localizado por
            // GUID en la escena) para el mar, el río y el arroyo; si no aparece, el color plano de antes.
            var agua = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("7444131032ad74a439b35a1a4ceb95ef"));
            if(agua==null){agua=Material("Agua", new Color(.045f,.43f,.53f));informe.AppendLine("Agua: no se encontró el material de MainWorld por GUID; se usa color plano.");}
            else informe.AppendLine("Agua: material de MainWorld "+AssetDatabase.GetAssetPath(agua));
            var aguaRio=AguaPaisaje(agua,true);
            agua=AguaPaisaje(agua,false);
            Plano("Mar", new Vector3(0,0,0), new Vector3(240,1,240), agua);
            // Revisión 4: los caminos ya no son cintas de malla — van pintados en el propio terreno (capa 5,
            // ver `ConstruirTerreno`), como pidió Raúl. Los puentes cubren los cruces con el río.
            // El lecho y la superficie usan las mismas cotas; las orillas cubren los bordes de la cinta.
            Cinta("Río del bosque",rioSuave,15,aguaRio,false);
            Cinta("Arroyo de la ladera este",arroyoSuave,9,aguaRio,false);
            despejes.Clear();
            // Revisión 8: cada paso decorativo va protegido — si uno falla, el error queda en el informe y en la
            // consola, pero la escena SE GUARDA igual (antes un fallo aquí dejaba la carpeta sin .unity ni Informe).
            Paso("Hitos",Hitos); // antes de la vegetación: registra los despejes donde no deben caer árboles
            Paso("Transiciones de pueblos",IntegrarPueblos);
            Paso("Asentar piezas de las demos",()=>{ foreach(var zona in Zonas) if(zona.demo!=null) AsentarPiezas(zona); });
            Paso("Vegetación",Vegetacion);
            Paso("Apoyo final de árboles",AsentarArbolado);
            Paso("Vida de los pueblos",VestirEspacios);
            Paso("Verificación urbana",VerificarUrbanismo);
            Paso("Verificación del relieve",VerificarRelieve);
            var luz = new GameObject("Sol").AddComponent<Light>();
            luz.transform.SetParent(raiz); luz.type=LightType.Directional; luz.intensity=1.05f;
            luz.color=new Color(1f,.94f,.83f);
            luz.transform.rotation=Quaternion.Euler(48,-35,0); luz.shadows=LightShadows.Soft;
            RenderSettings.fog=false; RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.65f,.75f,.83f);
            RenderSettings.ambientEquatorColor=new Color(.42f,.46f,.4f);
            RenderSettings.ambientGroundColor=new Color(.2f,.24f,.22f);
            var cam=new GameObject("Cámara — vista del mapa").AddComponent<Camera>();
            cam.transform.SetParent(raiz); cam.transform.position=new Vector3(0,1100,-1000);
            cam.transform.LookAt(new Vector3(0,35,0)); cam.orthographic=true;cam.orthographicSize=670;
            cam.farClipPlane=3000;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.06f,.32f,.42f);
            var datosCam=cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            datosCam.requiresDepthTexture=true;
            Paso("Validación Interactive/Grafo",()=>{
                var validacion=Sendero.Narrative.Editor.CrossSystemNarrativeValidator.Validate();
                informe.AppendLine($"Validación Interactive/Grafo: {validacion.Errors.Count} errores, {validacion.Warnings.Count} advertencias.");
                foreach(var error in validacion.Errors) informe.AppendLine(error.ToString());
                foreach(var aviso in validacion.Warnings) informe.AppendLine(aviso.ToString());
            });
            AssetDatabase.SaveAssets();
            string ruta=carpeta+"/Eldoria_Codex.unity";
            if(!EditorSceneManager.SaveScene(escena,ruta)) throw new IOException("No se pudo guardar "+ruta);
            Paso("Capturas de composición",()=>CapturarPaisaje(cam));
            File.WriteAllText(carpeta+"/Informe.txt",informe.ToString());
            File.WriteAllText("Temp/EldoriaCodex-result.txt",ruta+"\n"+informe);
            Debug.Log("Eldoria guardada: "+ruta);
        }
        finally
        {
            if(anterior.IsValid() && anterior.isLoaded) SceneManager.SetActiveScene(anterior);
            EditorSceneManager.CloseScene(escena,true);
            AssetDatabase.Refresh();
        }
    }

    // Ejecuta un paso decorativo; si lanza, lo anota (informe + consola) y sigue, para que la escena se guarde.
    static void Paso(string nombre,Action accion)
    {
        try { accion(); }
        catch(Exception e)
        {
            informe.AppendLine($"ERROR en el paso '{nombre}': {e.GetType().Name}: {e.Message}");
            Debug.LogException(e);
        }
    }

    // Revisión 14: los caminos se diseñan de centro de pueblo a centro de pueblo, así que se pintaban por encima
    // de las casas y morían en seco en medio del pueblo. Aquí cada extremo que caiga dentro de la elipse de un
    // pueblo importado se retrae a lo largo de su último tramo hasta quedar justo fuera (las demos traen sus
    // propias calles). No se tocan las zonas con relieve a medida (la espiral del pueblo de montaña nace dentro).
    static void RecortarCaminosEnPueblos()
    {
        int recortes=0;
        for(int r=0;r<Caminos.Length;r++)
        {
            var ruta=(Vector3[])Caminos[r].Clone();
            for(int extremo=0;extremo<2;extremo++)
            {
                int iE=extremo==0?0:ruta.Length-1, iP=extremo==0?1:ruta.Length-2;
                var E=ruta[iE];var P=ruta[iP];
                foreach(var zona in Zonas)
                {
                    if(zona.demo==null||zona.relieve!=null||zona.mitad.x<1)continue;
                    float Norm(Vector3 q)=>new Vector2((q.x-zona.centro.x)/zona.mitad.x,(q.z-zona.centro.z)/zona.mitad.y).magnitude;
                    if(Norm(E)>=1.05f)continue;
                    var dir=P-E;float largo=dir.magnitude;if(largo<1)continue;dir/=largo;
                    float t=0;var q=E;
                    while(t<largo-4&&Norm(q)<1.05f){t+=2;q=E+dir*t;}
                    ruta[iE]=q;recortes++;break;
                }
            }
            rutasSuaves[r]=Suavizar(ruta);
        }
        informe.AppendLine($"Caminos recortados en el borde de los pueblos: {recortes} extremos.");
    }

    // Revisión 17: piezas decorativas de la demo 09 que se retiran de la copia (plataformas de césped con
    // muro de roca a la vista — "Ground*" y los montículos "Hill02_*" que forman el anillo alrededor del
    // pueblo). El suelo pintado real llega por otra vía (AplicarPinturaDemo/AplicarDetallesDemo) y no se toca.
    static bool EsPlataformaDecorativa(string nombre)
    {
        return nombre.StartsWith("Ground",StringComparison.OrdinalIgnoreCase)
            || nombre.StartsWith("Hill",StringComparison.OrdinalIgnoreCase);
    }

    static void Importar(Zona zona, Scene destino)
    {
        // La escena de origen se abre como preview: nunca se guarda ni cambia la sesión del usuario.
        var origen=EditorSceneManager.OpenPreviewScene(Demo+zona.demo+".unity");
        try
        {
            var grupo=new GameObject(zona.nombre); SceneManager.MoveGameObjectToScene(grupo,destino);
            grupo.transform.SetParent(raiz); zona.grupo=grupo.transform;
            zona.muestrasRelieve=new List<Vector3>();
            foreach(var objeto in origen.GetRootGameObjects())
            {
                if(PrefabUtility.IsPrefabAssetMissing(objeto)) { informe.AppendLine(zona.demo+": prefab ausente omitido: "+objeto.name);continue; }
                if(zona.demo=="06") // revisión 10: inventario de piezas de la demo (nombre real del prefab) para poder rehacerla a mano
                {
                    var fuente=PrefabUtility.GetCorrespondingObjectFromSource(objeto);
                    informe.AppendLine($"  demo06 pieza: '{objeto.name}' prefab='{(fuente!=null?AssetDatabase.GetAssetPath(fuente):"(sin prefab)")}' pos={objeto.transform.position} rotY={objeto.transform.eulerAngles.y:0}");
                }
                // Revisión 17: además de "Ground*" (suelo de césped), la demo 09 trae montículos "Hill02_*" con
                // muro de roca naranja que forman un anillo alrededor del pueblo — se retiran igual, conservando
                // el suelo pintado (se copia aparte con AplicarPinturaDemo) y los jardines/vallas de la demo.
                if(zona.demo=="09"&&EsPlataformaDecorativa(objeto.name)){informe.AppendLine("Retirada plataforma decorativa de césped: "+objeto.name);continue;}
                var copia=Object.Instantiate(objeto); SceneManager.MoveGameObjectToScene(copia,destino);
                copia.name=objeto.name; copia.transform.SetParent(grupo.transform,true);
                // Se eliminan también plataformas anidadas: las demos combinan estos bloques dentro de prefabs.
                if(zona.demo=="09")foreach(var pieza in copia.GetComponentsInChildren<Transform>(true))
                    if(pieza!=null&&pieza!=copia.transform&&EsPlataformaDecorativa(pieza.name))Object.DestroyImmediate(pieza.gameObject);
                // `grupo` sigue en el origen (posición/rotación identidad) en este punto del método — el
                // reparent de arriba usa worldPositionStays, así que la posición/altura de origen del
                // Terrain que vamos a borrar EQUIVALE a su posición local dentro de `grupo`. Guardamos su
                // relieve real ANTES de destruirlo (ver comentario en el campo `muestrasRelieve`); la
                // conversión a espacio de destino se hace una sola vez, al final, cuando `grupo` ya tiene su
                // transform definitivo (traslación + el giro del puerto si aplica).
                foreach(var t in copia.GetComponentsInChildren<Terrain>(true))
                {
                    MuestrearRelieve(t, zona.muestrasRelieve);
                    // Revisión 4: el Terrain de la demo SE CONSERVA (antes se borraba y con él se iban el suelo
                    // pintado, los cultivos y cualquier decoración que la demo colgara de él — "hemos perdido su
                    // suelo que era chulísimo"). Solo se descarta si es desproporcionado para la isla.
                    var datosT=t.terrainData;
                    if(zona.conservarTerreno && datosT!=null)
                    {
                        // Revisión 15: ya no se conserva como objeto (sea del tamaño que sea) — se guarda para
                        // transferir su pintura al terreno de la isla y se destruye después (`TransferirPinturaDemo`).
                        if(zona.terrenosOrigen==null)zona.terrenosOrigen=new List<Terrain>();
                        zona.terrenosOrigen.Add(t);
                        informe.AppendLine($"Demo {zona.demo}: Terrain '{t.name}' ({datosT.size.x:0}×{datosT.size.z:0} m) reservado para copiar su pintura.");
                    }
                    else Object.DestroyImmediate(t.gameObject);
                }
                if(copia==null) continue;
                foreach(var c in copia.GetComponentsInChildren<Camera>(true))
                {
                    if(c==null)continue;
                    // Los efectos legacy requieren Camera: se elimina el objeto completo con sus efectos.
                    // Los hijos se conservan por si la demo colgaba geometría de esa cámara.
                    var nodo=c.transform;
                    while(nodo.childCount>0)nodo.GetChild(0).SetParent(nodo.parent,true);
                    Object.DestroyImmediate(c.gameObject);
                }
                if(copia==null) continue;
                foreach(var c in copia.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(c);
                foreach(var c in copia.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c);
            }
            // (Revisión 11, Claude: Codex escalaba aquí cada demo ×1.15-1.55 "para que tengan peso"; retirado — las
            // casas del pesquero salían un 55 % más grandes que las del pueblo inicial y las de la cima, y el jugador
            // es el mismo en todas partes. Si hace falta más presencia, se añaden edificios, no se agrandan.)
            bool primero=true;var bounds=new Bounds();
            foreach(var r in grupo.GetComponentsInChildren<MeshRenderer>())
            { if(primero){bounds=r.bounds;primero=false;}else bounds.Encapsulate(r.bounds); }
            if(primero) throw new InvalidOperationException("Demo sin geometría: "+zona.demo);
            // Se conserva la escala del pack; la huella se mide antes de aplanar el terreno. El ancla vertical
            // sigue siendo la base de los edificios; la huella en planta incluye el Terrain propio (revisión 4)
            // para que el aplanado cubra todo el parche y no solo los edificios.
            var planta=bounds;
            foreach(var bT in terrenosDemo){ planta.Encapsulate(new Vector3(bT.min.x,bounds.center.y,bT.min.z)); planta.Encapsulate(new Vector3(bT.max.x,bounds.center.y,bT.max.z)); }
            terrenosDemo.Clear();
            zona.mitad=new Vector2(planta.extents.x+8,planta.extents.z+8);
            if(zona.demo=="05")zona.mitad+=new Vector2(14,14); // revisión 14: margen extra — las piezas de la orilla quedaban colgadas donde el aplanado se acababa y el terreno bajaba al mar
            bounds.center=new Vector3(planta.center.x,bounds.center.y,planta.center.z);bounds.size=new Vector3(planta.size.x,bounds.size.y,planta.size.z);
            grupo.transform.position=zona.centro-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            if(zona.demo=="05") AjustarPuerto(zona);
            if(Mathf.Abs(zona.giro)>.01f)
            {
                grupo.transform.RotateAround(zona.centro,Vector3.up,zona.giro);
                float mayor=Mathf.Max(zona.mitad.x,zona.mitad.y);zona.mitad=new Vector2(mayor,mayor);
            }
            // `grupo.transform` ya es el definitivo (traslación + giro del puerto si lo hubo) — convertir
            // las muestras de relieve, capturadas en espacio local de `grupo`, a su posición final de una
            // sola vez aquí (y no dentro de `Altura`, que se llama cientos de miles de veces).
            for(int i=0;i<zona.muestrasRelieve.Count;i++) zona.muestrasRelieve[i]=grupo.transform.TransformPoint(zona.muestrasRelieve[i]);
            informe.AppendLine($"Demo {zona.demo}: huella {bounds.size}; centro {zona.centro}; {zona.muestrasRelieve.Count} muestras de relieve capturadas del terreno de origen. Comprobar uniones, suelo y acceso en vista de jugador.");
        }
        finally { EditorSceneManager.ClosePreviewScene(origen); }
    }

    // Rejilla de muestreo por cada parche de Terrain de la escena demo de origen (algunas traen varios
    // parches, del estilo "New Terrain 1..16"). Deliberadamente modesta: `AlturaRelieveZona` hace una media
    // ponderada por distancia inversa sobre TODAS las muestras de la zona, así que un pueblo con muchos
    // parches puede acumular varios cientos de puntos — de sobra para el relieve suave de una demo de este
    // pack, sin encarecer el muestreo del heightmap/alphamap nuevo (que llama a `Altura` cientos de miles
    // de veces).
    const int MuestrasRelievePorLado = 6;
    static void MuestrearRelieve(Terrain origen, List<Vector3> destino)
    {
        var datos=origen.terrainData;
        if(datos==null) return;
        Vector3 p=origen.transform.position;
        for(int j=0;j<=MuestrasRelievePorLado;j++)for(int i=0;i<=MuestrasRelievePorLado;i++)
        {
            float x=p.x+i/(float)MuestrasRelievePorLado*datos.size.x;
            float z=p.z+j/(float)MuestrasRelievePorLado*datos.size.z;
            destino.Add(new Vector3(x,origen.SampleHeight(new Vector3(x,0,z))+p.y,z));
        }
    }

    // Pueblo de la Montaña (revisión 6) — como en el mapa y como pidió Raúl: "cualquier pueblo de montaña tiene
    // cuestas y arriba del todo un castillo". Semicírculo de TERRAZAS abierto al sur alrededor de un foco F
    // (40 m al norte del centro de la zona): castillo en la meseta alta (r<30, 140 m), y tres terrazas de casas
    // a 131, 122 y 113 m separadas por cuestas de ~10 m. El camino de montaña llega por el sur hasta la
    // terraza 1. Detrás del castillo (norte) el terreno natural se respeta (Max), así los picos siguen ahí.
    const float TerrazaAlta=140, TerrazaPaso=9, FocoNorte=40;
    static readonly float[] TerrazaRadios={40,62,84}; // bordes superiores de cada cuesta (10 m de ancho cada una): llanos en 50-62, 72-84, 94-106
    static float RelieveTerrazas(Zona zona,float x,float z,float natural)
    {
        float fx=zona.centro.x, fz=zona.centro.z+FocoNorte;
        float dx=x-fx, dz=z-fz, r=Mathf.Sqrt(dx*dx+dz*dz);
        float nivel=TerrazaAlta;
        foreach(var borde in TerrazaRadios) nivel-=TerrazaPaso*Mathf.SmoothStep(0,1,Mathf.Clamp01((r-borde)/10));
        // Mitad norte: el pueblo no sigue por detrás del castillo — se conserva la montaña si es más alta.
        if(dz>0) nivel=Mathf.Max(nivel,Mathf.Lerp(natural,nivel,Mathf.Clamp01(1-(dz-20)/30)));
        return nivel;
    }
    // Poza de la cascada (revisión 12): cuenco de 1.6 m bajo el borde en un radio de 16 m, fundido suave hasta 26 m.
    // Revisión 13: cuenco de fondo plano (r<14) a 3 m bajo el borde, con ribera corta (14→20 m) para que la orilla
    // se lea como un círculo limpio; el agua va 1.2 m bajo el borde. Antes (1.6 m y ribera larga) el disco de agua
    // asomaba a trozos y se veía como una mancha azul irregular ("no entiendo lo que es").
    const float PozaRadio=14, PozaHondo=3f, PozaAgua=1.2f;
    static void Poza(Zona zona)
    {
        zona.relieve=(x,z,h)=>{
            float d=Vector2.Distance(new Vector2(x,z),new Vector2(zona.centro.x,zona.centro.z));
            return zona.centro.y-PozaHondo*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((d-PozaRadio)/6)));
        };
        zona.mitad=new Vector2(44,44);
        zona.grupo=new GameObject(zona.nombre).transform;zona.grupo.SetParent(raiz);zona.grupo.position=zona.centro;
        informe.AppendLine($"Poza de la cascada en {zona.centro}, radio {PozaRadio} m.");
    }

    // Asienta cada pieza de una demo importada en el terreno ya construido (revisión 12): el relieve de la demo
    // se reconstruye suavizado, así que vallas, farolas y muelles quedaban flotando o enterrados. Solo se mueven
    // piezas cuya base esté a menos de 3 m del suelo (lo demás son estructuras grandes que no conviene tocar) y
    // nunca barcos/boyas/muelles, que van al agua.
    static void AsentarPiezas(Zona zona)
    {
        if(zona.grupo==null||zona.terrenoPropio)return; int movidas=0;
        foreach(Transform t in zona.grupo)
        {
            string n=t.name.ToLowerInvariant();
            if(n.StartsWith("ship")||n.StartsWith("boat")||n.Contains("fishing")||n.Contains("pier")||n.Contains("dock"))continue;
            var rs=t.GetComponentsInChildren<MeshRenderer>();if(rs.Length==0)continue;
            var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            float suelo=terreno.SampleHeight(b.center)+terreno.transform.position.y;
            float delta=suelo+.05f-b.min.y;
            if(Mathf.Abs(delta)<.04f)continue; // revisión 14: tolerancia 3→6 m
            t.position+=Vector3.up*delta;movidas++;
            if(b.size.x>8&&b.size.y>2&&!t.name.StartsWith("Tree"))informe.AppendLine($"Pieza amplia demo: {t.name}, tamaño {b.size}, centro {b.center}.");
        }
        informe.AppendLine($"Demo {zona.demo}: {movidas} piezas asentadas en el terreno.");
    }

    // Mide cada `Building Combination/BuildingATnn` con una instancia temporal y devuelve `n` nombres distintos
    // cuya huella (lado mayor) esté entre `ladoMin` y `ladoMax` y su alto por debajo de `altoMax`, barajados.
    static string[] SeleccionarCasitas(int n,float ladoMin,float ladoMax,float altoMax,int semilla)
    {
        var validas=new List<string>();var medidas=new List<string>();
        for(int i=1;i<=55;i++)
        {
            string nombre="BuildingAT"+i.ToString("00");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Building Combination/"+nombre+".prefab");
            if(prefab==null)continue;
            var tmp=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var rs=tmp.GetComponentsInChildren<MeshRenderer>();
            if(rs.Length>0)
            {
                var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                float lado=Mathf.Max(b.size.x,b.size.z);
                medidas.Add($"{nombre} {b.size.x:0}×{b.size.z:0}×{b.size.y:0}");
                if(lado>=ladoMin&&lado<=ladoMax&&b.size.y<=altoMax)validas.Add(nombre);
            }
            Object.DestroyImmediate(tmp);
        }
        informe.AppendLine("Edificios del pack medidos (x×z×alto): "+string.Join(", ",medidas));
        var azar=new System.Random(semilla);
        for(int i=validas.Count-1;i>0;i--){int j=azar.Next(i+1);var t=validas[i];validas[i]=validas[j];validas[j]=t;}
        if(validas.Count==0)validas.AddRange(new[]{"BuildingAT35","BuildingAT53"});
        var salida=new string[n];for(int i=0;i<n;i++)salida[i]=validas[i%validas.Count];
        informe.AppendLine($"Casitas elegidas ({validas.Count} candidatas entre {ladoMin}-{ladoMax} m): "+string.Join(", ",validas));
        return salida;
    }

    // Claro llano a cota fija (revisión 16): relieve constante, elipse pequeña. Los hitos (piedra, pilares) van en `Hitos`.
    static void Claro(Zona zona)
    {
        zona.relieve=zona.esIslote?null:(Func<float,float,float,float>)((x,z,h)=>zona.centro.y); // revisión 19: en islote el claro lo talla el relieve del islote
        zona.mitad=new Vector2(16,16); // revisión 19: solo el recinto de ruinas (radio 13) — el resto de la isla se puebla de vegetación
        zona.grupo=new GameObject(zona.nombre).transform;zona.grupo.SetParent(raiz);zona.grupo.position=zona.centro;
        informe.AppendLine($"Claro '{zona.nombre}' en {zona.centro}.");
    }
    // Revisión 18 (Claude): claro en la cima de la isla-jungla con la cabaña del hechicero (la vegetación turquesa la pone
    // `Arboledas` por paraje alrededor). La casa se cimenta como solar para que el aplanado y los árboles la respeten.
    static void Jungla(Zona zona)
    {
        zona.relieve=null; // revisión 19: el claro (r≈20 m) lo talla `RelieveIsloteHabitable`; el resto de la isla es jungla
        zona.mitad=new Vector2(14,12);
        zona.grupo=new GameObject(zona.nombre).transform;zona.grupo.SetParent(raiz);zona.grupo.position=zona.centro;
        PiezaUrbana(zona.grupo,Pack+"Building Combination/"+Viviendas[2]+".prefab","Casa del hechicero — exilio en la isla-jungla (GDD 15)",zona.centro+new Vector3(0,0,6),Vector3.back);
        PiezaUrbana(zona.grupo,Tiny+"BuildingUtilityDeco/Well01.prefab","Pozo del hechicero",zona.centro+new Vector3(-9,0,-8),Vector3.back);
        informe.AppendLine($"Isla del hechicero: claro de ~40 m a {zona.centro.y:0} m con cabaña y pozo en {zona.centro}; playa hacia el continente (a nado, ~50 m de agua), acantilado en el resto, jungla turquesa por toda la isla.");
    }
    // Caserío (revisión 16): tres casitas pequeñas mirando a un pozo, sin más. Cota fija de la zona.
    static void Caserio(Zona zona) => ConstruirPuebloVecino(zona);
    // Revisión 20: isla secreta — claro con la cabaña del náufrago (marcador provisional de "aquí hay algo único"; qué es
    // exactamente —equipo con nombre, diario de lore, el pueblo flotante aparcado— se decide en guion, no aquí).
    static void Secreta(Zona zona)
    {
        zona.relieve=null;zona.mitad=new Vector2(12,10);
        zona.grupo=new GameObject(zona.nombre).transform;zona.grupo.SetParent(raiz);zona.grupo.position=zona.centro;
        PiezaUrbana(zona.grupo,Pack+"Building Combination/"+Viviendas[5]+".prefab","Cabaña del náufrago — isla secreta",zona.centro+new Vector3(0,0,4),Vector3.back);
        for(int i=0;i<3;i++)PiezaUrbana(zona.grupo,Pack+"Props/Goods/Barrel01_a01.prefab","Barril del náufrago",zona.centro+new Vector3(-7+i*1.3f,0,-5),Vector3.back,false);
        informe.AppendLine($"Isla secreta: en {zona.centro}, detrás del macizo, playa hacia mar abierto; cabaña y barriles. Solo un marcador — el mito (3 versiones en la taberna) y la condición de acceso son guion/gameplay.");
    }

    static void PuebloMontana(Zona zona) => ConstruirBarrioMontana(zona);
    static void Granja(Zona zona) => ConstruirGranjaCascada(zona);

    static float DistanciaSegmento(Vector2 p,Vector2 a,Vector2 b,out float t)
    { var v=b-a;t=v.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude);return Vector2.Distance(p,a+t*v); }

    static Vector3[] Suavizar(Vector3[] puntos)
    {
        // Chaikin conserva los extremos y no introduce sobreoscilaciones en altura.
        var lista=new List<Vector3>(puntos);
        for(int vuelta=0;vuelta<2;vuelta++)
        {
            var siguiente=new List<Vector3>{lista[0]};
            for(int i=1;i<lista.Count;i++)
            { siguiente.Add(Vector3.Lerp(lista[i-1],lista[i],.25f));siguiente.Add(Vector3.Lerp(lista[i-1],lista[i],.75f)); }
            siguiente.Add(lista[lista.Count-1]);lista=siguiente;
        }
        return lista.ToArray();
    }

    static float Cercania(Vector2 p,Vector3[] linea,out float altura)
    {
        float distancia=float.MaxValue;altura=0;
        for(int i=1;i<linea.Length;i++)
        {
            float t;float d=DistanciaSegmento(p,new Vector2(linea[i-1].x,linea[i-1].z),new Vector2(linea[i].x,linea[i].z),out t);
            if(d<distancia){distancia=d;altura=Mathf.Lerp(linea[i-1].y,linea[i].y,t);}
        }
        return distancia;
    }

    static void AjustarPuerto(Zona zona)
    {
        var navios=new List<Transform>();Vector3 centro=Vector3.zero;
        foreach(Transform t in zona.grupo)
            if(t.name.StartsWith("Ship",StringComparison.OrdinalIgnoreCase)||t.name.StartsWith("Boat",StringComparison.OrdinalIgnoreCase))
            {navios.Add(t);centro+=t.position;}
        if(navios.Count==0)throw new InvalidOperationException("No se han identificado barcos en la demo 05; revisar el puerto antes de generarlo.");
        centro/=navios.Count;var direccion=centro-zona.centro;direccion.y=0;
        if(direccion.sqrMagnitude>.01f)zona.grupo.RotateAround(zona.centro,Vector3.up,Vector3.SignedAngle(direccion,Vector3.back,Vector3.up));
        foreach(var t in navios)
        {
            var rs=t.GetComponentsInChildren<MeshRenderer>();if(rs.Length==0)continue;
            var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            t.position+=Vector3.up*(-.8f-b.min.y);
            b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            barcos.Add(b);
        }
        // La tierra se conserva hacia el interior; los barcos y el acceso al mar se excavan después.
        zona.mitad=new Vector2(Mathf.Max(zona.mitad.x,zona.mitad.y),Mathf.Max(zona.mitad.x,zona.mitad.y));
        informe.AppendLine($"Puerto orientado al sur: {barcos.Count} barcos sobre ensenada conectada al océano; revisar altura de muelles en Unity.");
    }

    static float Pico(float x,float z,float cx,float cz,float radio,float alto)
    {
        float dx=(x-cx)/radio,dz=(z-cz)/radio;
        float r=Mathf.Sqrt(dx*dx+dz*dz);
        float angulo=Mathf.Atan2(dz,dx);
        float arista=1+.17f*Mathf.Sin(angulo*5+cx*.01f)+.09f*Mathf.Cos(angulo*3);
        return alto*Mathf.Pow(Mathf.Clamp01(1-r*arista),1.35f);
    }
    static float CostaDist(Vector2 p)
    {
        bool dentro=false;float d=9999;
        for(int i=0,j=Costa.Length-1;i<Costa.Length;j=i++)
        {
            var a=Costa[i];var b=Costa[j];float t;
            d=Mathf.Min(d,DistanciaSegmento(p,a,b,out t));
            if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) dentro=!dentro;
        }
        return dentro?d:-d;
    }
    /// <summary>Altura objetivo del aplanado de una zona importada: reconstruye el relieve real capturado
    /// del `Terrain` de la escena demo de origen (media ponderada por distancia inversa sobre
    /// `zona.muestrasRelieve` — misma técnica que ya usa esta función más abajo para la rasante de los
    /// caminos) en vez de una altura constante. El pueblo de montaña (sin demo de origen, sin muestras) se
    /// queda con el aplanado a `zona.centro.y` de siempre — no tenía terreno propio que perder.</summary>
    static float AlturaRelieveZona(Zona zona,float x,float z)
    {
        if(zona.muestrasRelieve==null||zona.muestrasRelieve.Count==0) return zona.centro.y;
        float pesoTotal=0,alturaTotal=0;
        foreach(var m in zona.muestrasRelieve)
        {
            float dx=x-m.x,dz=z-m.z;
            float w=1f/(dx*dx+dz*dz+9f); // +9: evita un pico de peso justo sobre la muestra más cercana
            pesoTotal+=w;alturaTotal+=w*m.y;
        }
        return pesoTotal>1e-9f?alturaTotal/pesoTotal:zona.centro.y;
    }
    static float Altura(float x,float z)
    {
        float costa=CostaDist(new Vector2(x,z));
        // Rampa costera: 85 m de subida por defecto; en los tramos de `Acantilados` se acorta a 30 m (revisión 3).
        float rampa=85;
        foreach(var a in Acantilados)
        {
            float w=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Vector2.Distance(new Vector2(x,z),new Vector2(a.x,a.y))-a.z)/60));
            rampa=Mathf.Lerp(rampa,30,w);
        }
        float h=Mathf.Lerp(-12,23,Mathf.SmoothStep(0,1,(costa+18)/rampa));
        float tierra=Mathf.Clamp01(costa/50);
        h+=(Mathf.PerlinNoise(x*.008f+30,z*.008f+20)-.5f)*5*tierra;
        // Revisión 3: dos octavas más de relieve para que la isla no sea una masa lisa — lomas medianas en
        // todas partes y rugosidad fina que crece con la altura (laderas del macizo). Los pueblos, caminos y
        // el río se imponen después sobre esto, así que no les afecta.
        h+=(Mathf.PerlinNoise(x*.022f+7,z*.022f+3)-.5f)*7*tierra;
        h+=(Mathf.PerlinNoise(x*.06f+11,z*.06f+5)-.5f)*3*tierra;
        // INC-197 (12 sept 2026): `tierra` (el peso de las tres octavas de arriba) ya llega a 1 a partir de
        // 50 m de la costa, pero la rampa base de subida puede durar hasta 85 m (sin acantilado cerca) —
        // en esa franja intermedia el ruido (hasta ±6 m combinado) podía arrastrar la altura por debajo de 0
        // sin que hubiera ningún río, zona ni playa de por medio: el plano "Mar" (siempre a Y=0) asomaba ahí
        // sin que `VerificarAgua` lo detectara (solo comprobaba el cauce del río/arroyo) — así se vio "de
        // pronto" tapando el minimapa en el juego. A partir de 15 m tierra adentro de la costa real (fuera de
        // la franja donde SÍ se espera seguir bajando hacia el agua) el ruido ya no puede bajar de +0,6 m.
        // Todo lo de abajo (playas, islotes, caminos, ríos, relieve propio de cada zona) sigue pudiendo bajar
        // el terreno donde de verdad hace falta, porque se aplica después de este suelo, no antes.
        if(costa>15f) h=Mathf.Max(h,.6f);
        // Playas (revisión 2): plataforma de arena casi llana desde el propio borde del polígono de costa. Con la
        // subida estándar de arriba la orilla real (h=0) queda a ~16 m dentro del polígono y la arena (h 4-15)
        // ocupa solo ~20 m — desde arriba no se veía playa. Aquí la orilla sale al borde del polígono y la
        // arena se extiende ~90 m tierra adentro, fundida en 70 m con el terreno normal fuera del radio.
        foreach(var p in Playas)
        {
            float w=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Vector2.Distance(new Vector2(x,z),new Vector2(p.x,p.y))-p.z)/45));
            if(w<=0f)continue;
            float plataforma=costa<0
                ?Mathf.Lerp(-8,1.2f,Mathf.SmoothStep(0,1,Mathf.Clamp01((costa+30)/30)))
                :Mathf.Lerp(1.2f,8f,Mathf.SmoothStep(0,1,Mathf.Clamp01(costa/70)));
            h=Mathf.Lerp(h,plataforma,w);
        }
        // Islotes rocosos y cabo del faro (revisión 2): bultos sobre el fondo marino, ajenos al polígono de costa.
        foreach(var b in Islotes) h=Mathf.Max(h,RelieveIslote(x,z,b));
        // Revisión 19: islotes habitables con playa, acantilado, lomas y claro (ver Paisaje.cs).
        h=Mathf.Max(h,RelieveIsloteHabitable(x,z,IsloteJungla,JunglaPlaya,JunglaCota));
        h=Mathf.Max(h,RelieveIsloteHabitable(x,z,IsloteRuinas,RuinasPlaya,RuinasCota));
        // Revisión 20: El Rompiente, los escollos de la cadena y la isla secreta.
        h=Mathf.Max(h,RelieveIslote(x,z,Rompiente));
        foreach(var e in Escollos) h=Mathf.Max(h,RelieveIslote(x,z,e));
        h=Mathf.Max(h,RelieveIsloteHabitable(x,z,IsloteSecreta,SecretaPlaya,SecretaCota));
        foreach(var hermana in Hermanas) h=Mathf.Max(h,RelieveIsloteHabitable(x,z,hermana.islote,hermana.playa,hermana.cota)); // revisión 22
        h+=RelieveLomas(x,z)*tierra;
        float macizo=78*Mathf.Exp(-((x-10)*(x-10)/47000+(z-305)*(z-305)/34000));
        macizo+=Mathf.Max(Pico(x,z,-55,423,155,160),Mathf.Max(Pico(x,z,75,433,125,190),Pico(x,z,160,380,105,122)));
        // Cuarto pico al noreste (revisión 2): la cordillera cruza todo el norte como en el mapa, y el arroyo de
        // la ladera este nace de verdad en una montaña. A ~200 m del pueblo de montaña (fuera de su radio) y
        // solapando un poco con el pico oriental de Codex, para que formen cresta y no dos conos sueltos. Queda
        // ~55 m dentro del polígono de costa: más al este el fundido costero (`costa/55`) lo aplastaría.
        macizo+=Pico(x,z,215,330,95,90);
        // Revisión 3: crestas en las laderas del macizo (ruido de "cresta" = 1-|ruido|), proporcional a la altura.
        macizo+=Mathf.Clamp01(macizo/60)*22*(1-Mathf.Abs(Mathf.PerlinNoise(x*.018f+2,z*.018f+9)*2-1));
        // Revisión 16 — "la montaña que destruye Estela" (cap. IX): mordisco en la cara suroeste del pico oeste, al
        // borde del Reino (el castillo). Cráter gaussiano de ~42 m de profundidad y ~35 m de radio.
        // El estado destruido se integrará con el evento de Estela; no se hornea antes de que ocurra.
        h+=macizo*Mathf.SmoothStep(0,1,Mathf.Clamp01(costa/55));
        float pesoTerrazas=0; // revisión 6: dentro de las terrazas del pueblo de montaña el camino no rebaja el terreno
        foreach(var zona in Zonas)
        {
            if(zona.esIslote)continue; // revisión 19: el claro de los islotes lo talla `RelieveIsloteHabitable`, sin aplanar media isla
            float d=(new Vector2((x-zona.centro.x)/zona.mitad.x,(z-zona.centro.z)/zona.mitad.y).magnitude-1)*Mathf.Min(zona.mitad.x,zona.mitad.y);
            float mezcla=zona.demo==null?38:42; // Preservar los contrafuertes entre las plataformas habitadas.
            float peso=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(d/mezcla));
            if(peso>0f) h=Mathf.Lerp(h,zona.relieve!=null?zona.relieve(x,z,h):AlturaRelieveZona(zona,x,z)-(zona.terrenoPropio?.35f:0),peso);
            if(zona.relieve!=null) pesoTerrazas=Mathf.Max(pesoTerrazas,peso);
        }
        // Acantilado de la cascada (revisión 9, DESPUÉS de las zonas para que el aplanado del pueblo no lo borre): disco de radio `CascadaRadio` donde aguas arriba el terreno queda a
        // nivelBajo+salto y aguas abajo a nivelBajo, con el escalón en 6 m; fundido con el exterior en 14 m.
        if(!float.IsNaN(cascadaNivelBajo))
        {
            float ddx=x-CascadaCentro.x, ddz=z-CascadaCentro.z; float dist=Mathf.Sqrt(ddx*ddx+ddz*ddz);
            if(dist<CascadaRadio+14)
            {
                float s=ddx*CascadaDireccion.x+ddz*CascadaDireccion.y; // >0 aguas abajo
                float objetivo=Mathf.Lerp(cascadaNivelBajo+CascadaSalto,cascadaNivelBajo,Mathf.SmoothStep(0,1,Mathf.Clamp01((s+3)/6)));
                float w=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((dist-CascadaRadio)/14));
                h=Mathf.Lerp(h,objetivo,w);
            }
        }
        float mejor=float.MaxValue,rasante=0,pesoTotal=0;
        foreach(var ruta in rutasSuaves)
        {
            for(int i=1;i<ruta.Length;i++)
            {
                var a=new Vector2(ruta[i-1].x,ruta[i-1].z);var b=new Vector2(ruta[i].x,ruta[i].z);
                float t;float d=DistanciaSegmento(new Vector2(x,z),a,b,out t);
                mejor=Mathf.Min(mejor,d);
                // Mezcla continua entre ramales: evita un salto al cambiar el segmento más próximo.
                float peso=Vector2.Distance(a,b)/Mathf.Pow(d*d+16,2);
                pesoTotal+=peso;rasante+=peso*Mathf.Lerp(ruta[i-1].y,ruta[i].y,t);
            }
        }
        rasante/=Mathf.Max(pesoTotal,1e-12f);
        h=Mathf.Lerp(h,rasante,(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((mejor-6)/32)))*(1-pesoTerrazas));
        float nivel;float distancia=Cercania(new Vector2(x,z),rioSuave,out nivel);
        // Valle ancho y cauce poco profundo: la superficie de agua y el lecho comparten recorrido.
        h=Mathf.Lerp(h,nivel+2.2f,1-Mathf.SmoothStep(0,1,Mathf.Clamp01((distancia-12)/38)));
        h=Mathf.Lerp(h,nivel-1.8f,1-Mathf.SmoothStep(0,1,Mathf.Clamp01((distancia-5)/6)));
        if(arroyoSuave!=null)
        {
            float cota;float d=Cercania(new Vector2(x,z),arroyoSuave,out cota);
            // Cauce real también en el este: el agua deja de ser una cinta sobre la hierba.
            h=Mathf.Lerp(h,cota-2.8f,1-Mathf.SmoothStep(0,1,Mathf.Clamp01((d-4)/5)));
        }
        foreach(var b in barcos)
        {
            // Franja abierta hacia el sur: no crea una piscina cerrada dentro de la isla.
            float lateral=Mathf.Abs(x-b.center.x)-b.extents.x-5;
            float interior=z-b.max.z-5;
            float borde=Mathf.Max(lateral,interior);
            float salida=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((b.min.z-z-45)/70));
            h=Mathf.Lerp(h,-4,(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(borde/16)))*salida);
        }
        return h;
    }
    // Peso (0-1) de las zonas con relieve a medida (terrazas del pueblo de montaña) en un punto — misma elipse y
    // fundido que usa `Altura`, para que el pintado coincida con el relieve (revisión 7).
    static float PesoZonaTerrazas(float x,float z)
    {
        float peso=0;
        foreach(var zona in Zonas)
        {
            if(zona.relieve==null)continue;
            float d=(new Vector2((x-zona.centro.x)/zona.mitad.x,(z-zona.centro.z)/zona.mitad.y).magnitude-1)*Mathf.Min(zona.mitad.x,zona.mitad.y);
            peso=Mathf.Max(peso,1-Mathf.SmoothStep(0,1,Mathf.Clamp01(d/38)));
        }
        return peso;
    }
    // Distancia en planta al camino más cercano (para pintar la capa de camino, revisión 4).
    static float DistanciaCaminos(float x,float z)
    {
        float mejor=float.MaxValue;var p=new Vector2(x,z);
        foreach(var ruta in rutasSuaves)for(int i=1;i<ruta.Length;i++)
        { float t;mejor=Mathf.Min(mejor,DistanciaSegmento(p,new Vector2(ruta[i-1].x,ruta[i-1].z),new Vector2(ruta[i].x,ruta[i].z),out t)); }
        foreach(var calle in callesUrbanas){float t;mejor=Mathf.Min(mejor,DistanciaSegmento(p,new Vector2(calle[0].x,calle[0].z),new Vector2(calle[1].x,calle[1].z),out t)+2); }
        // Sendas del bosque (revisión 13): cuentan como camino a la mitad de ancho (se les suma 1.6 m de distancia).
        if(sendasSuaves!=null) foreach(var senda in sendasSuaves)for(int i=1;i<senda.Length;i++)
        { float t;mejor=Mathf.Min(mejor,DistanciaSegmento(p,new Vector2(senda[i-1].x,senda[i-1].z),new Vector2(senda[i].x,senda[i].z),out t)+1.6f); }
        return mejor;
    }
    // ---- Revisión 15: transferencia de la pintura del Terrain de una demo al terreno de la isla ----
    // Peso de mezcla en un punto: 1 dentro de la elipse del pueblo, fundido a 0 en 18 m; y 0 fuera del parche.
    static float PesoTransferencia(Zona zona,Terrain t,float wx,float wz)
    {
        var p=t.transform.position;var sz=t.terrainData.size;
        if(wx<p.x+1||wx>p.x+sz.x-1||wz<p.z+1||wz>p.z+sz.z-1)return 0;
        float d=(new Vector2((wx-zona.centro.x)/zona.mitad.x,(wz-zona.centro.z)/zona.mitad.y).magnitude-1)*Mathf.Min(zona.mitad.x,zona.mitad.y);
        return 1-Mathf.SmoothStep(0,1,Mathf.Clamp01(d/18));
    }
    static void AplicarAlturasDemo(float[,] alturas)
    {
        foreach(var zona in Zonas)
        {
            if(zona.terrenosOrigen==null)continue;
            int celdas=0;
            for(int z=0;z<513;z++)for(int x=0;x<513;x++)
            {
                float wx=x/512f*1300-650, wz=z/512f*1300-650;
                foreach(var t in zona.terrenosOrigen)
                {
                    float w=PesoTransferencia(zona,t,wx,wz);if(w<=0)continue;
                    float hDemo=t.SampleHeight(new Vector3(wx,0,wz))+t.transform.position.y;
                    float hNuestra=alturas[z,x]*300-25;
                    alturas[z,x]=(Mathf.Lerp(hNuestra,hDemo,w)+25)/300;celdas++;
                }
            }
            informe.AppendLine($"Demo {zona.demo}: alturas copiadas en {celdas} celdas del heightmap.");
        }
    }
    static void AplicarPinturaDemo(TerrainData datos,ref TerrainLayer[] capas,ref float[,,] pesos)
    {
        foreach(var zona in Zonas)
        {
            if(zona.terrenosOrigen==null)continue;
            foreach(var t in zona.terrenosOrigen)
            {
                var td=t.terrainData;var capasDemo=td.terrainLayers;if(capasDemo==null||capasDemo.Length==0)continue;
                // Índice de cada capa de la demo en nuestro array (se añaden las que falten).
                var indice=new int[capasDemo.Length];var lista=new List<TerrainLayer>(capas);
                for(int i=0;i<capasDemo.Length;i++){var integrada=CapaIntegrada(capasDemo[i]);int k=lista.IndexOf(integrada);if(k<0){lista.Add(integrada);k=lista.Count-1;}indice[i]=k;}
                if(lista.Count!=capas.Length)
                {
                    var nuevo=new float[512,512,lista.Count];
                    for(int z=0;z<512;z++)for(int x=0;x<512;x++)for(int c=0;c<capas.Length;c++)nuevo[z,x,c]=pesos[z,x,c];
                    pesos=nuevo;capas=lista.ToArray();
                }
                int res=td.alphamapResolution;var mapa=td.GetAlphamaps(0,0,res,res);
                var p=t.transform.position;var sz=td.size;int celdas=0;
                for(int z=0;z<512;z++)for(int x=0;x<512;x++)
                {
                    float wx=x/511f*1300-650, wz=z/511f*1300-650;
                    float w=PesoPinturaIntegrada(zona,t,wx,wz);if(w<=0)continue;
                    int ax=Mathf.Clamp(Mathf.RoundToInt((wx-p.x)/sz.x*(res-1)),0,res-1), az=Mathf.Clamp(Mathf.RoundToInt((wz-p.z)/sz.z*(res-1)),0,res-1);
                    for(int c=0;c<capas.Length;c++)pesos[z,x,c]*=1-w;
                    for(int i=0;i<capasDemo.Length;i++)pesos[z,x,indice[i]]+=mapa[az,ax,i]*w;
                    celdas++;
                }
                informe.AppendLine($"Demo {zona.demo}: {capasDemo.Length} capas de textura copiadas en {celdas} celdas (total de capas ahora {capas.Length}).");
            }
        }
    }
    static void AplicarDetallesDemo()
    {
        foreach(var zona in Zonas)
        {
            if(zona.terrenosOrigen==null)continue;
            var nuestro=terreno.terrainData;
            foreach(var t in zona.terrenosOrigen)
            {
                var td=t.terrainData;var p=t.transform.position;var sz=td.size;
                // Detalles (flores/hierba): prototipos de la demo en nuestro terreno, resolución 2048 (0.63 m/celda).
                if(td.detailPrototypes!=null&&td.detailPrototypes.Length>0)
                {
                    var protos=new List<DetailPrototype>(nuestro.detailPrototypes);int base0=protos.Count;
                    protos.AddRange(td.detailPrototypes);nuestro.detailPrototypes=protos.ToArray();
                    // Revisión 16: mismo modo de densidad que la demo (si la demo cuenta instancias y nosotros
                    // cobertura 0-255, valores ≤16 no se ven — eso explicaba las flores ausentes).
                    nuestro.SetDetailScatterMode(td.detailScatterMode);
                    if(nuestro.detailResolution<2048)nuestro.SetDetailResolution(2048,32);
                    int resN=nuestro.detailResolution, resD=td.detailResolution;
                    float areaN=(1300f/resN)*(1300f/resN), areaD=(sz.x/resD)*(sz.z/resD); float factor=Mathf.Clamp(areaN/areaD,.2f,6f);
                    int total=0;
                    for(int capa=0;capa<td.detailPrototypes.Length;capa++)
                    {
                        var origen=td.GetDetailLayer(0,0,resD,resD,capa);
                        var destino=nuestro.GetDetailLayer(0,0,resN,resN,base0+capa);
                        for(int z=0;z<resN;z++)for(int x=0;x<resN;x++)
                        {
                            float wx=(x+.5f)/resN*1300-650, wz=(z+.5f)/resN*1300-650;
                            float w=PesoTransferencia(zona,t,wx,wz);if(w<=0)continue;
                            int dx=Mathf.Clamp((int)((wx-p.x)/sz.x*resD),0,resD-1), dz=Mathf.Clamp((int)((wz-p.z)/sz.z*resD),0,resD-1);
                            int v=Mathf.RoundToInt(origen[dz,dx]*factor*w);if(v>0){destino[z,x]=Mathf.Min(td.detailScatterMode==DetailScatterMode.CoverageMode?255:16,v);total++;}
                        }
                        nuestro.SetDetailLayer(0,0,base0+capa,destino);
                    }
                    informe.AppendLine($"Demo {zona.demo}: {td.detailPrototypes.Length} capas de detalle (flores/hierba) copiadas, {total} celdas.");
                }
                // Árboles de terreno de la demo → instancias en el nuestro.
                if(td.treePrototypes!=null&&td.treePrototypes.Length>0&&td.treeInstances.Length>0)
                {
                    var protos=new List<TreePrototype>(nuestro.treePrototypes);int base0=protos.Count;
                    protos.AddRange(td.treePrototypes);nuestro.treePrototypes=protos.ToArray();
                    var lista=new List<TreeInstance>(nuestro.treeInstances);int copiados=0;
                    foreach(var ti in td.treeInstances)
                    {
                        var mundo=new Vector3(p.x+ti.position.x*sz.x,0,p.z+ti.position.z*sz.z);
                        if(PesoTransferencia(zona,t,mundo.x,mundo.z)<.5f)continue;
                        var copia=ti;copia.prototypeIndex=ti.prototypeIndex+base0;
                        copia.position=new Vector3((mundo.x+650)/1300f,nuestro.GetInterpolatedHeight((mundo.x+650)/1300f,(mundo.z+650)/1300f)/nuestro.size.y,(mundo.z+650)/1300f);
                        lista.Add(copia);copiados++;
                    }
                    nuestro.treeInstances=lista.ToArray();
                    informe.AppendLine($"Demo {zona.demo}: {copiados} árboles de terreno copiados.");
                }
                // Si la demo colgaba decoración del Terrain, se conserva antes de destruirlo.
                while(t.transform.childCount>0)t.transform.GetChild(0).SetParent(t.transform.parent,true);
                Object.DestroyImmediate(t.gameObject);
            }
            zona.terrenosOrigen=null;
            terreno.Flush();
        }
        // Revisión 16: los detalles solo se dibujan a `detailObjectDistance` de la cámara (80 m por defecto — desde
        // una vista de maqueta no se veía ninguna flor). Para la maqueta, 400 m.
        terreno.detailObjectDistance=400;terreno.detailObjectDensity=1;terreno.treeDistance=1500;terreno.drawTreesAndFoliage=true;
        PraderasGlobales();
    }

    // Revisión 16: praderas por toda la isla con los mismos prototipos de hierba/flores que trae la demo 09
    // (hasta 4), en manchas de ruido, fuera de arena, roca, nieve y pueblos. Da "vidilla" al mundo abierto.
    static void PraderasGlobales()
    {
        var nuestro=terreno.terrainData;int n=Mathf.Min(4,nuestro.detailPrototypes.Length);if(n==0)return;
        int res=nuestro.detailResolution;bool cobertura=nuestro.detailScatterMode==DetailScatterMode.CoverageMode;
        int total=0;
        for(int i=0;i<n;i++)
        {
            var capa=nuestro.GetDetailLayer(0,0,res,res,i);float ox=i*37.3f, oz=i*91.7f;
            for(int z=0;z<res;z++)for(int x=0;x<res;x++)
            {
                if(capa[z,x]>0)continue; // lo copiado de la demo manda
                float wx=(x+.5f)/res*1300-650, wz=(z+.5f)/res*1300-650;
                float mancha=Mathf.PerlinNoise(wx*.011f+ox,wz*.011f+oz);
                if(mancha<.66f)continue;
                if(distanciasSenderos[Mathf.Clamp((int)((wz+650)/1300*511),0,511),Mathf.Clamp((int)((wx+650)/1300*511),0,511)]<6||EnSolar(wx,wz))continue;
                float h=terreno.SampleHeight(new Vector3(wx,0,wz))+terreno.transform.position.y;
                if(h<5||h>105)continue;
                if(CostaDist(new Vector2(wx,wz))<40)continue;
                if(nuestro.GetSteepness((wx+650)/1300f,(wz+650)/1300f)>28)continue;
                bool enPueblo=false;foreach(var zona in Zonas){if(zona.demo==null)continue;float d=new Vector2((wx-zona.centro.x)/Mathf.Max(1,zona.mitad.x),(wz-zona.centro.z)/Mathf.Max(1,zona.mitad.y)).magnitude;if(d<1.15f){enPueblo=true;break;}} // revisión 22: solo se excluyen los pueblos de demo (traen sus propias flores); en los hechos a mano sí hay pradera
                if(enPueblo)continue;
                float densidad=Mathf.InverseLerp(.58f,.8f,mancha)*(1-PesoBosque(wx,wz)*.7f);
                int v=cobertura?Mathf.RoundToInt(densidad*35):Mathf.RoundToInt(densidad*1.5f);
                if(v>0){capa[z,x]=v;total++;}
            }
            nuestro.SetDetailLayer(0,0,i,capa);
        }
        terreno.Flush();
        informe.AppendLine($"Praderas: {total} celdas de hierba/flores repartidas por la isla con {n} prototipos de la demo.");
    }

    static void ConstruirTerreno()
    {
        var datos=new TerrainData {heightmapResolution=513,size=new Vector3(1300,300,1300),alphamapResolution=512};
        // Crear el asset antes de pintar permite persistir las texturas de control del terreno.
        AssetDatabase.CreateAsset(datos,carpeta+"/Terreno.asset");
        distanciasSenderos=new float[512,512];
        var alturas=new float[513,513];
        for(int z=0;z<513;z++)for(int x=0;x<513;x++)alturas[z,x]=(Altura(x/512f*1300-650,z/512f*1300-650)+25)/300;
        AplicarAlturasDemo(alturas); // revisión 15: alturas exactas de la demo 09 dentro de su huella
        CorregirRasantesYApoyos(alturas);
        datos.SetHeights(0,0,alturas);
        var fondo=new Texture2D(513,513,TextureFormat.RFloat,false,true){name="Batimetría",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        var fondoPix=new Color[513*513];
        for(int z=0;z<513;z++)for(int x=0;x<513;x++)fondoPix[z*513+x]=new Color(alturas[z,x],0,0,1);
        fondo.SetPixels(fondoPix);fondo.Apply();AssetDatabase.CreateAsset(fondo,carpeta+"/Batimetria.asset");
        // Arena más cálida y hierba más luminosa (revisión 2), en la línea de las referencias low-poly que
        // compartió Raúl; la roca se queda gris neutra para que los acantilados se lean bien contra la hierba.
        // Revisión 4: quinta capa "camino" (tierra) — los caminos se pintan en el terreno en vez de ser mallas.
        // Revisión 22: octava capa "empedrado" (piedra clara) para plazas y calles de los pueblos hechos a mano.
        var colores=new[]{new Color(.79f,.72f,.52f),new Color(.32f,.46f,.20f),new Color(.38f,.39f,.36f),new Color(.86f,.90f,.91f),new Color(.47f,.35f,.22f),new Color(.22f,.32f,.15f),new Color(.43f,.49f,.25f),new Color(.58f,.56f,.50f)};
        var capas=new TerrainLayer[colores.Length];
        for(int i=0;i<capas.Length;i++)
        {
            var tex=new Texture2D(32,32); var pix=new Color[1024];
            for(int j=0;j<1024;j++){pix[j]=colores[i]*Mathf.Lerp(.96f,1.04f,Mathf.PerlinNoise(j%32*.23f,j/32*.23f));pix[j].a=0;} // alfa 0: URP TerrainLit lee la suavidad del alfa del albedo — con 1 la isla brillaba (revisión 3)
            tex.SetPixels(pix);tex.Apply();AssetDatabase.CreateAsset(tex,carpeta+"/Textura"+i+".asset");
            capas[i]=new TerrainLayer{diffuseTexture=tex,tileSize=new Vector2(i==4||i==7?6:12,i==4||i==7?6:12),smoothness=0,metallic=0};
            AssetDatabase.CreateAsset(capas[i],carpeta+"/Capa"+i+".terrainlayer");
        }
        datos.terrainLayers=capas;var pesos=new float[512,512,colores.Length];
        const float celda=1300/512f;
        for(int z=0;z<512;z++)for(int x=0;x<512;x++)
        {
            // Revisión 2: altura y pendiente leídas del heightmap recién calculado (`alturas` es 513×513, así
            // que x+1 y z+1 siempre existen) en vez de volver a llamar a `Altura` para cada celda.
            float h=alturas[z,x]*300-25;
            float dx=(alturas[z,x+1]-alturas[z,x])*300/celda, dz=(alturas[z+1,x]-alturas[z,x])*300/celda;
            float pendiente=Mathf.Sqrt(dx*dx+dz*dz); // 1.0 = 45°
            float nieve=Mathf.SmoothStep(0,1,Mathf.InverseLerp(145,185,h));
            // Roca por altura (macizo) O por pendiente (acantilados de costa, laderas de los picos, islotes):
            // a partir de ~29° empieza a asomar la roca y a ~48° es roca del todo.
            float wx=x/511f*1300-650, wz=z/511f*1300-650;
            // Revisión 7: dentro del pueblo de montaña (zona con terrazas) la roca solo sale por pendiente — las
            // terrazas son hierba y las cuestas roca; por altura sola el pueblo entero salía gris.
            float terrazas=PesoZonaTerrazas(wx,wz);
            float roca=(1-nieve)*Mathf.Max(Mathf.SmoothStep(0,1,Mathf.InverseLerp(80,135,h))*(1-terrazas),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1.1f,pendiente)));
            // Revisión 4: la arena solo cerca del mar — por altura sola salían orillas de arena a lo largo de todo
            // el río tierra adentro (el cauce baja de 15 m mucho antes de llegar a la costa).
            float costaAqui=CostaDist(new Vector2(wx,wz));
            float arena=(1-nieve-roca)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,15,h)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(45,95,costaAqui)));
            // Revisión 4: camino pintado (4 m de ancho pleno, fundido hasta 6.5 m), por encima de cualquier otra capa.
            float camino=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.2f,5.4f,DistanciaCaminos(wx,wz)));
            float nivelRio;
            if(Cercania(new Vector2(wx,wz),rioSuave,out nivelRio)<9 || (arroyoSuave!=null&&Cercania(new Vector2(wx,wz),arroyoSuave,out nivelRio)<6))camino=0;
            float resto=1-camino;
            float hierba=Mathf.Max(0,1-arena-roca-nieve)*resto;
            float bosque=Mathf.Max(PesoBosque(wx,wz)*.8f,PesoJungla(wx,wz)*.85f); // revisión 18: la jungla también pinta suelo umbrío
            float prado=(1-bosque)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.68f,Mathf.PerlinNoise(wx*.012f+61,wz*.012f+43)))*.7f;
            pesos[z,x,0]=arena*resto;pesos[z,x,1]=hierba*(1-bosque-prado);pesos[z,x,2]=roca*resto;pesos[z,x,3]=nieve*resto;pesos[z,x,4]=camino;
            pesos[z,x,5]=hierba*bosque;pesos[z,x,6]=hierba*prado;
        }
        for(int z=0;z<512;z++)for(int x=0;x<512;x++)distanciasSenderos[z,x]=DistanciaCaminos(x/511f*1300-650,z/511f*1300-650);
        AplicarPinturaDemo(datos,ref capas,ref pesos); // revisión 15: capas y pesos de la demo 09 dentro de su huella
        datos.terrainLayers=capas;
        PintarPueblos(pesos); // revisión 22: suelo de los pueblos hechos a mano (antes solo la demo 09 tenía suelo pintado)
        PintarPlazas(pesos);datos.SetAlphamaps(0,0,pesos);EditorUtility.SetDirty(datos);
        var go=Terrain.CreateTerrainGameObject(datos);go.name="Isla — terreno editable";go.transform.SetParent(raiz);go.transform.position=new Vector3(-650,-25,-650);
        terreno=go.GetComponent<Terrain>();
        AplicarDetallesDemo(); // revisión 15: flores/hierba y árboles de terreno de la demo 09; destruye sus Terrains al acabar
        // El LOD visual también debe respetar el cauce estrecho; el muestreo de altura usa máxima resolución.
        terreno.heightmapPixelError=.5f;
        { int capa=LayerMask.NameToLayer("Floor"); if(capa>=0) go.layer=capa; } // revisión 11: sin la capa "Floor" no se rompe
        var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if(shader!=null){var mat=new Material(shader);AssetDatabase.CreateAsset(mat,carpeta+"/Terreno.mat");go.GetComponent<Terrain>().materialTemplate=mat;}
    }
    static Material Material(string nombre,Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",0);
        AssetDatabase.CreateAsset(m,carpeta+"/"+nombre+".mat");return m;
    }
    static void Plano(string nombre,Vector3 posicion,Vector3 escala,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Plane);go.name=nombre;go.transform.SetParent(raiz);go.transform.position=posicion;go.transform.localScale=escala;
        go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Cinta(string nombre,Vector3[] puntos,float ancho,Material mat,bool suelo)
    {
        // Revisión 4: tira CONTINUA. Antes cada tramo tenía su propia normal y sus propios vértices, así que en
        // cada unión los bordes daban un salto — el río se veía en zigzag/escalera. Ahora se remuestrea la
        // polilínea cada ~3 m, cada punto lleva la normal media de sus dos tramos vecinos, y los vértices se
        // comparten entre quads. Además la cota de cada punto se apoya en el terreno real (no solo en los puntos
        // de control), así la lámina sigue el cauce en vez de flotar entre punto y punto.
        var muestra=new List<Vector3>();
        for(int s=1;s<puntos.Length;s++)
        {
            int pasos=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(puntos[s-1],puntos[s])/3));
            for(int j=(s==1?0:1);j<=pasos;j++) muestra.Add(Vector3.Lerp(puntos[s-1],puntos[s],j/(float)pasos));
        }
        var v=new List<Vector3>();var indices=new List<int>();
        for(int i=0;i<muestra.Count;i++)
        {
            var prev=muestra[Mathf.Max(0,i-1)];var next=muestra[Mathf.Min(muestra.Count-1,i+1)];
            var dir=next-prev;dir.y=0;if(dir.sqrMagnitude<1e-4f)dir=Vector3.forward;
            var normal=Vector3.Cross(dir.normalized,Vector3.up).normalized*ancho/2;
            var p=muestra[i];
            float centro=terreno.SampleHeight(p)+terreno.transform.position.y;
            var a=p-normal;var b=p+normal;
            if(suelo)
            {
                a.y=terreno.SampleHeight(a)+terreno.transform.position.y+.22f;b.y=terreno.SampleHeight(b)+terreno.transform.position.y+.22f;
            }
            else
            {
                // Agua: lámina horizontal de orilla a orilla, apoyada en el centro del cauce real; si el borde
                // del cauce queda por debajo (cauce más ancho que la cinta) no importa, el agua tapa.
                float lamina=Mathf.Max(0,p.y); // La cota diseñada se comparte con el cauce excavado.
                float nivel;
                Cercania(new Vector2(p.x,p.z),nombre=="Río del bosque"?rioSuave:arroyoSuave,out nivel);
                lamina=Mathf.Max(0,nivel);
                a.y=lamina;b.y=lamina;
            }
            int n=v.Count;v.Add(a);v.Add(b);
            if(i>0)indices.AddRange(new[]{n-2,n-1,n,n-1,n+1,n});
        }
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh,carpeta+"/"+nombre+".asset");
        var go=new GameObject(nombre,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(raiz);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        if(suelo)go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }

    // ---- Hitos del mapa (revisión 2): portal, faro, velero y puentes ----

    /// <summary>Instancia un prefab, lo escala a un tamaño objetivo medido por bounds (así no depende de la
    /// escala nativa de cada pack), lo orienta hacia `frente`, y lo apoya por la base de sus bounds sobre el
    /// terreno real (o sobre `baseY` si se indica). Registra un despeje de vegetación y lo anota en el informe
    /// con el tamaño resultante, para que se pueda corregir la constante a ojo desde el Editor.</summary>
    static bool EnCadenaEscollos(float x,float z)
    {
        var p=new Vector2(x,z);
        if(Vector2.Distance(p,new Vector2(Rompiente.x,Rompiente.y))<Rompiente.z*.8f)return true;
        foreach(var e in Escollos)if(Vector2.Distance(p,new Vector2(e.x,e.y))<e.z*.9f)return true;
        return false;
    }

    // Revisión 20: la ruta difícil a las Ruinas — velero fondeado en El Rompiente, pasarelas de tablones entre escollos
    // hasta la playa, arrecife de rocas del pack asomando del agua alrededor de la isla y un pecio encallado.
    static void RutaAlArrecife(Transform hitos)
    {
        var grupo=new GameObject("Ruta a las Ruinas — Rompiente, pasarelas y arrecife").transform;grupo.SetParent(hitos);
        // Velero fondeado a sotavento del Rompiente (lado del puerto), flotando como el del puerto (revisión 3: cota -0.8).
        Colocar(grupo,Pack+"Props/Ship/Ship01_a01.prefab","Velero fondeado — El Rompiente",new Vector3(Rompiente.x-34,0,Rompiente.y+6),new Vector3(.3f,0,1),18,false,-.8f,10);
        // Cadena: Rompiente → escollo 1 → 2 → 3 → playa de las Ruinas. Cada pasarela une la cima de un escollo con la del siguiente.
        var cimas=new List<Vector3>{new Vector3(Rompiente.x,0,Rompiente.y)};
        foreach(var e in Escollos)cimas.Add(new Vector3(e.x,0,e.y));
        cimas.Add(new Vector3(IsloteRuinas.x+RuinasPlaya.x*IsloteRuinas.z*.86f,0,IsloteRuinas.y+RuinasPlaya.y*IsloteRuinas.z*.86f));
        int pasarelas=0;
        for(int i=1;i<cimas.Count;i++)
        {
            var a=cimas[i-1];var b=cimas[i];var dir=(b-a).normalized;
            // Los extremos se apoyan en el borde de cada roca, no en el centro: se busca desde el centro hacia el siguiente
            // el último punto que sigue por encima del agua (h>1) y se retrocede 1,5 m.
            Vector3 Borde(Vector3 desde,Vector3 hacia)
            {
                var d=(hacia-desde).normalized;var ultimo=desde;
                for(float t=0;t<40;t+=.5f){var q=desde+d*t;if(terreno.SampleHeight(q)+terreno.transform.position.y<1f)break;ultimo=q;}
                return ultimo-d*1.5f;
            }
            var pa=Borde(a,b);var pb=Borde(b,a);
            PasarelaEntreRocas(grupo,$"Pasarela derrumbable {i} (gameplay: cae al cruzarla)",pa,pb);pasarelas++;
        }
        informe.AppendLine($"Ruta a las Ruinas: velero fondeado en El Rompiente ({Rompiente.x:0},{Rompiente.y:0}), {pasarelas} pasarelas entre escollos hasta la playa. Las pasarelas son estáticas en la maqueta; el derrumbe es gameplay.");
        // Arrecife: rocas del pack asomando del agua en un cinturón alrededor de las Ruinas y del Rompiente; fuera del corredor de la cadena.
        var rocas=new List<GameObject>();
        foreach(var n in new[]{"Rock01_a01","Rock02_a01","Rock03_a01","Rock04_a01"})
        { var v=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Rock/"+n+".prefab"); if(v!=null)rocas.Add(v); }
        var azar=new System.Random(20260911);int arrecife=0;
        if(rocas.Count>0)for(int i=0;i<900&&arrecife<90;i++) // revisión 21: 140→90 rocas, más pequeñas (la primera maqueta era un amasijo de losas)
        {
            float ang=(float)azar.NextDouble()*Mathf.PI*2;
            bool ruinas=azar.NextDouble()<.7;
            float radio=ruinas?IsloteRuinas.z*Mathf.Lerp(1.12f,1.55f,(float)azar.NextDouble()):Rompiente.z*Mathf.Lerp(1.6f,3.4f,(float)azar.NextDouble());
            var c=ruinas?new Vector2(IsloteRuinas.x,IsloteRuinas.y):new Vector2(Rompiente.x,Rompiente.y);
            var q=c+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*radio;
            if(Mathf.Abs(q.x)>640||Mathf.Abs(q.y)>640)continue;
            if(CostaDist(q)>-25)continue; // solo en mar abierto, lejos de la costa del continente
            float h=terreno.SampleHeight(new Vector3(q.x,0,q.y))+terreno.transform.position.y;
            if(h>-3)continue; // no sobre tierra ni sobre los escollos
            bool cerca=false;foreach(var e in Escollos)if(Vector2.Distance(q,new Vector2(e.x,e.y))<e.z+10){cerca=true;break;}
            if(cerca)continue;
            // Playa de las Ruinas y cala del velero despejadas: por ahí se llega.
            var haciaPlaya=(q-new Vector2(IsloteRuinas.x,IsloteRuinas.y)).normalized;
            if(ruinas&&Vector2.Dot(haciaPlaya,RuinasPlaya)>.75f)continue;
            var modelo=rocas[azar.Next(rocas.Count)];
            var roca=(GameObject)PrefabUtility.InstantiatePrefab(modelo,grupo);roca.name="Roca de arrecife";
            roca.transform.rotation=Quaternion.Euler(0,(float)azar.NextDouble()*360,0);
            float alto=Mathf.Lerp(3,6.5f,(float)azar.NextDouble());
            roca.transform.localScale*=FactorAltura(modelo,alto);
            roca.transform.position=new Vector3(q.x,Mathf.Lerp(-2.6f,-1.2f,(float)azar.NextDouble()),q.y); // la base bajo el agua, la punta fuera
            despejes.Add(new Vector3(q.x,q.y,alto*.6f));arrecife++;
        }
        // Pecio: el velero del pack encallado e inclinado en el arrecife sur de las Ruinas.
        var pecio=Colocar(grupo,Pack+"Props/Ship/Ship01_a01.prefab","Pecio encallado en el arrecife",new Vector3(IsloteRuinas.x-30,0,IsloteRuinas.y-135),new Vector3(-.6f,0,.8f),20,false,-4.5f,14);
        if(pecio!=null)pecio.transform.rotation*=Quaternion.Euler(0,0,22);
        // Cositas del puerto: barriles a la deriva.
        for(int i=0;i<6;i++)
        {
            var q=new Vector2(238+(float)azar.NextDouble()*70,-505-(float)azar.NextDouble()*30);
            Colocar(grupo,Pack+"Props/Goods/Barrel01_a01.prefab","Barril a la deriva",new Vector3(q.x,0,q.y),new Vector3(1,0,.3f),1.1f,true,-.35f,0);
        }
        informe.AppendLine($"Arrecife: {arrecife} rocas asomando alrededor de las Ruinas y del Rompiente (playa y cala del velero libres); pecio encallado al sur; barriles a la deriva junto al puerto.");
    }

    // Pasarela de tablones entre dos puntos ya apoyados (revisión 20). Misma construcción que `PuenteTransitable` (tablones,
    // postes, pasamanos y colisión continua), pero con extremos dados en vez de buscarlos por la anchura de un cauce.
    static void PasarelaEntreRocas(Transform padre,string nombre,Vector3 a,Vector3 b)
    {
        a.y=terreno.SampleHeight(a)+terreno.transform.position.y+.05f;b.y=terreno.SampleHeight(b)+terreno.transform.position.y+.05f;
        if(maderaPuerto==null)maderaPuerto=Material("Madera de pasos",new Color(.34f,.20f,.095f));
        var direccion=b-a;direccion.y=0;float largo=direccion.magnitude;if(largo<1f)return;direccion/=largo;
        Vector3 lateral=Vector3.Cross(Vector3.up,direccion);
        var grupo=new GameObject(nombre+$" — {largo:0.0} m").transform;grupo.SetParent(padre);
        var vertices=new List<Vector3>();var indices=new List<int>();int n=Mathf.CeilToInt(largo/.7f);
        Vector3 Punto(float t)=>Vector3.Lerp(a,b,t)-Vector3.up*(Mathf.Sin(t*Mathf.PI)*.35f); // comba hacia abajo: pasarela colgante
        for(int i=0;i<=n;i++)
        {
            var p=Punto(i/(float)n);vertices.Add(p-lateral*1.1f);vertices.Add(p+lateral*1.1f);
            if(i>0){int k=i*2;indices.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}
            if(i==n)continue;
            var q=Punto((i+1)/(float)n);var tablon=BloqueUrbano(grupo,"Tablón",(p+q)*.5f-Vector3.up*.08f,new Vector3(2.2f,.16f,Vector3.Distance(p,q)+.015f),maderaPuerto);
            tablon.transform.rotation=Quaternion.LookRotation(q-p);Object.DestroyImmediate(tablon.GetComponent<Collider>());
            if(i%4==0&&i>0&&i<n-1)foreach(float lado in new[]{-1f,1f})BloqueUrbano(grupo,"Poste",p+lateral*(lado*1.2f)+Vector3.up*.5f,new Vector3(.14f,1.1f,.14f),maderaPuerto);
        }
        foreach(float lado in new[]{-1f,1f})
        {
            var inicio=Punto(.03f)+lateral*lado*1.2f+Vector3.up*.95f;var fin=Punto(.97f)+lateral*lado*1.2f+Vector3.up*.95f;
            var cuerda=BloqueUrbano(grupo,"Cuerda",(inicio+fin)*.5f,new Vector3(.08f,.08f,Vector3.Distance(inicio,fin)),maderaPuerto);cuerda.transform.rotation=Quaternion.LookRotation(fin-inicio);
        }
        var mesh=new Mesh{name=nombre+" colisión"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh,carpeta+"/"+nombre.Replace(" ","_").Replace("(","").Replace(")","").Replace(":","")+"_paso.asset");
        var colision=grupo.gameObject.AddComponent<MeshCollider>();colision.sharedMesh=mesh;tablerosPuentes.Add(colision);
        int capa=LayerMask.NameToLayer("Floor");if(capa>=0)grupo.gameObject.layer=capa;
        for(float d=0;d<=largo;d+=3){var p=a+direccion*d;despejes.Add(new Vector3(p.x,p.z,3));}
        informe.AppendLine($"{nombre}: {largo:0.0} m, de ({a.x:0},{a.z:0}) a ({b.x:0},{b.z:0}), desnivel {Mathf.Abs(b.y-a.y):0.0} m.");
    }

    static GameObject Colocar(Transform padre,string ruta,string nombre,Vector3 posicion,Vector3 frente,float objetivo,bool objetivoEsAltura,float? baseY,float despeje,bool alinearLargo=true)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if(prefab==null){informe.AppendLine("Hito omitido, prefab no encontrado: "+ruta);return null;}
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,padre);go.name=nombre;
        go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;
        var rs=go.GetComponentsInChildren<Renderer>(); // revisión 6: Renderer (no solo MeshRenderer) — la cascada es de partículas
        if(rs.Length==0){informe.AppendLine("Hito omitido, sin geometría: "+ruta);Object.DestroyImmediate(go);return null;}
        var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
        // Medido en reposo: si el lado largo del prefab va en X, se gira 90° extra para que quede a lo largo de `frente`.
        bool largoEnX=alinearLargo&&b.size.x>b.size.z;
        float medida=objetivoEsAltura?b.size.y:Mathf.Max(b.size.x,b.size.z);
        // Un sistema de partículas en reposo puede medir ~0: se asume prefab de tamaño unidad (en MainWorld la
        // cascada va a escala ~50×69×50, o sea, la escala ES el tamaño en metros).
        float escala=medida>.5f?objetivo/medida:objetivo;
        go.transform.localScale=go.transform.localScale*escala;
        frente.y=0;if(frente.sqrMagnitude<.001f)frente=Vector3.forward;
        go.transform.rotation=Quaternion.LookRotation(frente.normalized)*(largoEnX?Quaternion.Euler(0,90,0):Quaternion.identity);
        b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
        float suelo=baseY??(terreno.SampleHeight(new Vector3(posicion.x,0,posicion.z))+terreno.transform.position.y);
        go.transform.position+=new Vector3(posicion.x-b.center.x,suelo-b.min.y,posicion.z-b.center.z);
        if(despeje>0)despejes.Add(new Vector3(posicion.x,posicion.z,despeje));
        informe.AppendLine($"{nombre}: en ({posicion.x:0},{posicion.z:0}), base a {suelo:0.0} m, tamaño {b.size} (escala ×{escala:0.00}). Revisar tamaño y apoyo en Unity.");
        return go;
    }

    // Cruce de segmentos 2D; devuelve t a lo largo de a→b.
    static bool Cruce(Vector2 a,Vector2 b,Vector2 c,Vector2 d,out float t)
    {
        var r=b-a;var s=d-c;float den=r.x*s.y-r.y*s.x;t=0;
        if(Mathf.Abs(den)<1e-6f)return false;
        var ac=c-a;t=(ac.x*s.y-ac.y*s.x)/den;float u=(ac.x*r.y-ac.y*r.x)/den;
        return t>=0&&t<=1&&u>=0&&u<=1;
    }

    // Revisión 25: material mínimo para las partículas de la cascada, sin depender de ningún asset del
    // proyecto. `Universal Render Pipeline/Particles/Unlit` es el shader de partículas que trae URP de
    // fábrica; si por lo que sea no está (paquete recortado), se cae a `null` y el llamador usa el material
    // de partícula por defecto de Unity — no ideal pero no debería romperse (no es magenta de shader perdido,
    // solo el degradado gris por defecto).
    static Material MaterialParticulaAgua(string nombre,Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Universal Render Pipeline/Particles/Lit");
        if(shader==null)return null;
        var mat=new Material(shader){name=nombre};
        if(mat.HasProperty("_Surface"))mat.SetFloat("_Surface",1); // 1 = Transparent, en el shader de partículas de URP
        mat.SetOverrideTag("RenderType","Transparent");
        mat.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if(mat.HasProperty("_ZWrite"))mat.SetFloat("_ZWrite",0);
        mat.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);
        else if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);
        AssetDatabase.CreateAsset(mat,carpeta+"/"+nombre.Replace(" ","")+".mat");
        return mat;
    }

    // Revisión 25: velo de agua cayendo (partículas estiradas del borde de arriba al de abajo del salto) +
    // salpicadura continua en la poza. No toca la lámina existente (`Cinta`) ni su material — es un añadido
    // encima, así que si algo de esto falla o se ve mal, se puede desactivar/borrar el GameObject sin
    // afectar al resto de la cascada.
    static void CascadaVFX(Transform grupo)
    {
        var dir=new Vector3(CascadaDireccion.x,0,CascadaDireccion.y).normalized;
        float anguloY=Vector3.SignedAngle(Vector3.forward,dir,Vector3.up);
        const float ancho=9; // mismo ancho que "Arroyo de la ladera este" (Cinta)
        var arriba=CascadaCentro-dir*3;arriba.y=cascadaNivelBajo+CascadaSalto;
        var abajoPos=CascadaCentro+dir*3;
        var matVelo=MaterialParticulaAgua("Cascada — velo",new Color(.86f,.95f,.97f,.55f));
        var matEspuma=MaterialParticulaAgua("Cascada — espuma",new Color(1,1,1,.8f));

        var velo=new GameObject("Cascada — velo de agua").transform;velo.SetParent(grupo);velo.position=arriba;
        var ps=velo.gameObject.AddComponent<ParticleSystem>();
        const float vidaVelo=1.4f;
        var main=ps.main;
        main.loop=true;main.startSpeed=0f;main.startSize=new ParticleSystem.MinMaxCurve(.6f,1.3f);
        main.startLifetime=vidaVelo;main.startColor=new Color(.86f,.95f,.97f,.55f);
        main.gravityModifier=0f;main.maxParticles=250;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var vel=ps.velocityOverLifetime;vel.enabled=true;vel.space=ParticleSystemSimulationSpace.World;
        vel.y=new ParticleSystem.MinMaxCurve(-(CascadaSalto/vidaVelo)*1.05f);
        var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(ancho,.1f,.6f);shape.rotation=new Vector3(0,anguloY,0);
        var emission=ps.emission;emission.rateOverTime=ancho*6;
        var renderVelo=ps.GetComponent<ParticleSystemRenderer>();
        renderVelo.renderMode=ParticleSystemRenderMode.Stretch;renderVelo.lengthScale=5;renderVelo.velocityScale=.06f;
        if(matVelo!=null)renderVelo.material=matVelo;

        var salpica=new GameObject("Cascada — salpicadura").transform;salpica.SetParent(grupo);
        salpica.position=new Vector3(abajoPos.x,cascadaNivelBajo-PozaAgua+.15f,abajoPos.z);
        var ps2=salpica.gameObject.AddComponent<ParticleSystem>();
        var main2=ps2.main;
        main2.loop=true;main2.startLifetime=.6f;main2.startSpeed=new ParticleSystem.MinMaxCurve(1.2f,3f);
        main2.startSize=new ParticleSystem.MinMaxCurve(.2f,.5f);main2.startColor=new Color(1,1,1,.75f);
        main2.gravityModifier=1.3f;main2.maxParticles=120;
        var shape2=ps2.shape;shape2.enabled=true;shape2.shapeType=ParticleSystemShapeType.Box;
        shape2.scale=new Vector3(ancho*.8f,.1f,1.4f);shape2.rotation=new Vector3(-70,anguloY,0);
        var emission2=ps2.emission;emission2.rateOverTime=ancho*5;
        var renderSalpica=ps2.GetComponent<ParticleSystemRenderer>();
        renderSalpica.renderMode=ParticleSystemRenderMode.Billboard;
        if(matEspuma!=null)renderSalpica.material=matEspuma;

        despejes.Add(new Vector3(arriba.x,arriba.z,ancho*.7f));
        informe.AppendLine($"Cascada — VFX de partículas: velo de {ancho:0} m de ancho (vida {vidaVelo:0.0} s) y salpicadura en la poza. Material propio (Particles/Unlit de URP) {(matVelo!=null?"aplicado":"NO disponible — usa el material de partícula por defecto, revisar en el Editor")}.");
    }

    // Revisión 27 (Raúl: "esa esquina está desaprovechada... ponte un mirador, sorpréndeme"). Repisa de roca al
    // lado del velo de agua, a media altura del salto — se ve la cascada de cerca Y la poza/mar/islotes de enfrente
    // de un vistazo. Acceso por una trepa de piedras (mismas rocas del arrecife, no un asset de escalera de otro
    // pack, para no mezclar estilos). Detalle narrativo pequeño (hoguera apagada + barril): alguien ya subió aquí.
    // Puramente aditivo: no toca la lámina de la cascada, el VFX de partículas, ni el terreno tallado del
    // acantilado — si algo no encaja, se puede desactivar/mover el GameObject "Mirador de la cascada" sin más.
    static void MiradorCascada(Transform grupo)
    {
        var dir=new Vector3(CascadaDireccion.x,0,CascadaDireccion.y).normalized;
        var lateral=Vector3.Cross(Vector3.up,dir);
        const float distLateral=15f; // al lado del velo de agua (9 m de ancho), sobre roca seca, no sobre el agua
        float altoMirador=cascadaNivelBajo+CascadaSalto*0.55f; // media altura del salto: cascada de cerca + vista abajo
        var centro=CascadaCentro+lateral*distLateral;centro.y=altoMirador;
        var frente=-lateral; // mirando hacia el velo de agua y, más allá, la poza, el mar y los islotes de enfrente

        var mirador=new GameObject("Mirador de la cascada").transform;mirador.SetParent(grupo);
        var rocaMirador=Material("Roca de mirador",new Color(.42f,.4f,.37f));
        var maderaMirador=Material("Madera de mirador",new Color(.3f,.18f,.09f));

        // Repisa: dos losas ligeramente descentradas (no un cubo perfecto) para que no se lea como una baldosa.
        BloqueUrbano(mirador,"Repisa — base",centro-Vector3.up*.2f,new Vector3(6.5f,.5f,5.5f),rocaMirador);
        BloqueUrbano(mirador,"Repisa — reborde",centro+frente*1.6f-Vector3.up*.05f,new Vector3(4.2f,.35f,2.6f),rocaMirador);

        // Barandilla solo en el borde exterior (hacia la vista) — con el macizo detrás no hace falta cerrar el resto.
        var borde=centro+frente*3.1f;
        foreach(float lado in new[]{-1f,1f})
            BloqueUrbano(mirador,"Barandilla — poste",borde+lateral*(lado*1.7f)+Vector3.up*.55f,new Vector3(.16f,1.05f,.16f),maderaMirador);
        var travesano=BloqueUrbano(mirador,"Barandilla — travesaño",borde+Vector3.up*.95f,new Vector3(3.6f,.12f,.12f),maderaMirador);
        travesano.transform.rotation=Quaternion.LookRotation(lateral);

        // Acceso: trepa de piedras del pie de la ladera hasta la repisa (mismas Rock01-03 del arrecife).
        var rocasEscalon=new List<GameObject>();
        foreach(var n in new[]{"Rock01_a01","Rock02_a01","Rock03_a01"})
        { var v=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Rock/"+n+".prefab"); if(v!=null)rocasEscalon.Add(v); }
        if(rocasEscalon.Count>0)
        {
            var azarMirador=new System.Random(20260911+27);
            var pieBase=centro+lateral*9f;pieBase.y=cascadaNivelBajo+1f;
            var cima=centro+lateral*1.5f;cima.y=altoMirador-.3f;
            const int pasos=7;
            for(int i=0;i<pasos;i++)
            {
                float t=(i+1)/(float)(pasos+1);
                var p=Vector3.Lerp(pieBase,cima,t);
                var modelo=rocasEscalon[azarMirador.Next(rocasEscalon.Count)];
                var roca=(GameObject)PrefabUtility.InstantiatePrefab(modelo,mirador);roca.name="Roca — escalón "+(i+1);
                roca.transform.position=p;roca.transform.rotation=Quaternion.Euler(0,(float)azarMirador.NextDouble()*360,0);
                roca.transform.localScale*=Mathf.Lerp(1.1f,1.8f,(float)azarMirador.NextDouble());
                despejes.Add(new Vector3(p.x,p.z,2.5f));
            }
        }

        // Detalle narrativo pequeño (mismo lenguaje que los marcadores de Las Hermanas): no es recompensa real.
        Colocar(mirador,Tiny+"BuildingUtilityDeco/Fire01.prefab","Hoguera apagada — mirador",centro+frente*.5f+lateral*1.2f,frente,1.1f,true,altoMirador,0);
        Colocar(mirador,Pack+"Props/Goods/Barrel01_a01.prefab","Barril de provisiones — mirador",centro+frente*.3f-lateral*1.4f,frente,1f,true,altoMirador,0);

        despejes.Add(new Vector3(centro.x,centro.z,9));
        informe.AppendLine($"Mirador de la cascada: repisa de piedra a {altoMirador-cascadaNivelBajo:0.0} m sobre el nivel del pueblo (mitad del salto), {distLateral:0} m al lado del velo de agua — vista a la poza, el mar y los islotes de enfrente. Acceso: {(rocasEscalon.Count>0?"trepa de "+7+" rocas desde la ladera":"SIN escalones — prefabs de roca no encontrados, revisar rutas")}. Hoguera apagada y barril como detalle narrativo, no recompensa real. Revisar en el Editor: la altura aquí sale de la fórmula del salto (no de muestrear el terreno, porque el acantilado se talla aparte), así que puede no apoyar exactamente en roca — y que la barandilla quede en el borde real, no en el aire.");
    }

    static void Hitos()
    {
        var hitos=new GameObject("Hitos del mapa — faro, velero, puentes, cascada").transform;hitos.SetParent(raiz);
        // (El portal del Bosque Prohibido de la revisión 2 se quitó en la revisión 4 — Raúl no le veía sentido.)
        // Cascada a la entrada del pueblo de la cascada (revisión 4): el arroyo baja del pico noreste y entra en
        // el pueblo por el noroeste; el prefab de cascada se escala al desnivel real entre el punto de arriba
        // (más de 80 m aguas arriba del centro) y el nivel del pueblo. Sin el Editor no se puede saber dónde
        // queda exactamente la noria de la demo 06 — el informe anota la posición para ajustarla a mano.
        // Cascada (revisión 9): en el acantilado tallado en `Altura` (ver `CascadaCentro`), con la altura del salto,
        // mirando aguas abajo. Prefab: el mismo `WaterFall_1_Stylized` de MainWorld (por GUID).
        // Revisión 12: sin prefab de cascada — el VFX de MainWorld "tiene otro arte" (Raúl). La propia lámina del
        // arroyo (mismo material que el río) cae por el acantilado tallado y se remansa en la poza, que lleva su
        // propio disco de agua.
        {
            var poza=Zonas[2];
            var disco=GameObject.CreatePrimitive(PrimitiveType.Plane);disco.name="Poza de la cascada — agua";disco.transform.SetParent(hitos);
            disco.transform.position=new Vector3(poza.centro.x,poza.centro.y-PozaAgua,poza.centro.z);
            disco.transform.localScale=new Vector3((PozaRadio+5)/5f,1,(PozaRadio+5)/5f);
            var mr=disco.GetComponent<Renderer>();var aguaMar=AssetDatabase.LoadAssetAtPath<Material>(carpeta+"/AguaRio.mat");
            if(aguaMar!=null)mr.sharedMaterial=aguaMar;
            Object.DestroyImmediate(disco.GetComponent<Collider>());
            despejes.Add(new Vector3(poza.centro.x,poza.centro.z,PozaRadio+8));
            informe.AppendLine($"Cascada: acantilado de {CascadaSalto} m en ({CascadaCentro.x:0},{CascadaCentro.z:0}); la lámina del arroyo hace de cascada; poza con agua a {poza.centro.y-PozaAgua:0.0} m.");
            // Revisión 25 (petición de Raúl: "no veo que caiga agua" — la lámina sola no lo transmite, sobre
            // todo en cenital). Dos sistemas de partículas, puramente aditivos a lo de arriba: no se toca la
            // lámina ni el material del río.
            CascadaVFX(hitos);
            // Revisión 27: mirador junto a la cascada — ver `MiradorCascada` arriba.
            MiradorCascada(hitos);
        }
        // Faro: el pack no trae faro, así que una torre vigía sobre el cabo rocoso del sureste del puerto.
        // Piedra Ancestral (revisión 16): recinto de ruinas con monolito en el claro — marcador de sitio, la piedra real y el
        // guardián vienen de `PiedraAncestralGuardianBuilder`. Revisión 18: el claro está ahora en la isla del sureste.
        foreach(var zona in Zonas)
        {
            if(!zona.esClaro)continue;
            RuinasAncestrales(hitos,zona);
        }
        RutaAlArrecife(hitos); // revisión 20
        // Revisión 22: marcadores de contenido en Las Hermanas (qué recompensa exacta va en cada una es diseño de loot).
        {
            var grupo=new GameObject("Las Hermanas — marcadores de recompensa").transform;grupo.SetParent(hitos);
            Vector3 Cima(int i)=>new Vector3(Hermanas[i].islote.x,0,Hermanas[i].islote.y);
            Colocar(grupo,Tiny+"BuildingUtilityDeco/WoodBarrel01.prefab","Cofre — Hermana mayor (marcador provisional)",Cima(0)+new Vector3(4,0,3),Vector3.back,1.3f,true,null,3);
            Colocar(grupo,Tiny+"BuildingUtilityDeco/Crystal01.prefab","Altar — Hermana del altar (hechizo/mejora, marcador)",Cima(1),Vector3.back,3.5f,true,null,5);
            Colocar(grupo,Tiny+"BuildingUtilityDeco/WoodBarrel01.prefab","Cofre — Hermana pequeña (marcador provisional)",Cima(2)+new Vector3(-3,0,2),Vector3.back,1.3f,true,null,3);
            Colocar(grupo,Tiny+"BuildingUtilityDeco/Fire01.prefab","Hoguera — guarida del Gigante",Cima(3),Vector3.back,1.6f,true,null,4);
            Colocar(grupo,Tiny+"BuildingUtilityDeco/Skull01.prefab","Calavera — guarida del Gigante",Cima(3)+new Vector3(5,0,-4),new Vector3(-1,0,.5f),1.4f,true,null,2);
            informe.AppendLine("Las Hermanas: cofres en la mayor y la pequeña, altar (cristal) en la del altar, hoguera y calavera en la Isla del Gigante (boss extra). Marcadores, no recompensas reales.");
        }
        // Revisión 19: una barca varada en la playa de las Ruinas, mirando al puerto — "la gente ha dejado de visitar el templo".
        {
            var playa=new Vector3(IsloteRuinas.x+RuinasPlaya.x*IsloteRuinas.z*.74f,0,IsloteRuinas.y+RuinasPlaya.y*IsloteRuinas.z*.74f);
            Colocar(hitos,Pack+"Props/Ship/Boat01_a01.prefab","Barca varada — playa de las Ruinas",playa,new Vector3(RuinasPlaya.y,0,-RuinasPlaya.x),6,false,null,7);
        }
        var faro=Colocar(hitos,Tiny+"BuildingUtilityDeco/WatchTower01.prefab","Faro (torre vigía) — cabo del puerto",new Vector3(CaboFaro.x,0,CaboFaro.y),Vector3.back,18,true,null,20);
        ApoyarFaro(faro);
        // Velero en mar abierto al suroeste, como en el mapa; flota a la misma cota que los barcos del puerto.
        Colocar(hitos,Pack+"Props/Ship/Ship01_a01.prefab","Velero",new Vector3(-470,0,-450),new Vector3(1,0,.4f),18,false,-.8f,0);
        // Puentes: uno por cada camino que cruza el río (intersección de segmentos sobre las rutas ya suavizadas).
        // Revisión 4: puente de exterior `Bridge03` de RPG Tiny (Raúl: "hay puentes que son para exterior"), no el
        // de piedra del castillo. El tablero se apoya a la rasante del camino menos un 35% de la altura del prefab
        // (barandillas por encima, estructura por debajo) — la mejor estimación sin conocer el pivote; se anota.
        for(int r=0;r<rutasSuaves.Length;r++)
        {
            var ruta=rutasSuaves[r];bool puesto=false;
            foreach(var cauce in new[]{rioSuave,arroyoSuave}) // revisión 10: también los cruces con el arroyo
            if(cauce!=null)
            for(int i=1;i<ruta.Length&&!puesto;i++)for(int j=1;j<cauce.Length&&!puesto;j++)
            {
                float t;
                if(!Cruce(new Vector2(ruta[i-1].x,ruta[i-1].z),new Vector2(ruta[i].x,ruta[i].z),new Vector2(cauce[j-1].x,cauce[j-1].z),new Vector2(cauce[j].x,cauce[j].z),out t))continue;
                var p=Vector3.Lerp(ruta[i-1],ruta[i],t);var dir=ruta[i]-ruta[i-1];
                PuenteTransitable(hitos,"Puente_camino_"+(r+1),p,dir,cauce);
                puesto=true;
            }
        }
    }

    // Factor de escala para que un prefab mida `alturaObjetivo` de alto (revisión 3): cada familia del pack
    // tiene una escala nativa distinta — en la maqueta anterior los árboles sueltos salían 10 veces más grandes
    // que los del bosque. Se mide una vez por prefab con una instancia temporal.
    static readonly Dictionary<GameObject,float> factores=new Dictionary<GameObject,float>();
    static float FactorAltura(GameObject prefab,float alturaObjetivo)
    {
        float f;if(factores.TryGetValue(prefab,out f))return f*alturaObjetivo;
        var tmp=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var rs=tmp.GetComponentsInChildren<MeshRenderer>();f=1;
        if(rs.Length>0){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);if(b.size.y>.01f)f=1f/b.size.y;}
        Object.DestroyImmediate(tmp);factores[prefab]=f;return f*alturaObjetivo;
    }

    static void Vegetacion()
    {
        factores.Clear();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Vegetation/Tree01_a01.prefab");
        if(prefab==null)throw new FileNotFoundException("Árbol del pack no encontrado");
        var bosque=new GameObject("Bosque Prohibido y arboledas").transform;bosque.SetParent(raiz);
        var azar=new System.Random(10092026);
        // Rocas del pack por la costa y sobre los islotes (revisión 2), normalizadas a 3-7 m (revisión 3).
        var rocas=new List<GameObject>();
        foreach(var n in new[]{"Rock01_a01","Rock02_a01","Rock03_a01","Rock04_a01"})
        { var v=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Rock/"+n+".prefab"); if(v!=null)rocas.Add(v); }
        if(rocas.Count>0)
        {
            var grupo=new GameObject("Rocas de costa e islotes").transform;grupo.SetParent(raiz);
            int puestas=0;
            for(int i=0;i<3000&&puestas<400;i++)
            {
                float x=(float)azar.NextDouble()*1300-650,z=(float)azar.NextDouble()*1300-650;
                float h=terreno.SampleHeight(new Vector3(x,0,z))+terreno.transform.position.y;float costa=CostaDist(new Vector2(x,z));
                bool orilla=costa>=18&&costa<=70, islote=costa<0&&h>1.5f;
                if(islote&&(PesoJungla(x,z)>.3f||PesoRuinas(x,z)>.3f||PesoSecreta(x,z)>.3f||PesoHermanas(x,z)>.3f)&&h>7)continue; // revisión 19: en los islotes habitables las rocas solo en la orilla — el interior es para la vegetación
                if(islote&&EnCadenaEscollos(x,z))continue; // revisión 20: las cimas de los escollos son el camino, sin rocas encima
                if(!(orilla||islote)||h<1.5f||h>40||!Libre(x,z,h))continue;
                var modelo=rocas[azar.Next(rocas.Count)];
                var roca=(GameObject)PrefabUtility.InstantiatePrefab(modelo,grupo);
                roca.transform.position=new Vector3(x,h-.4f,z);roca.transform.rotation=Quaternion.Euler(0,(float)azar.NextDouble()*360,0);
                roca.transform.localScale*=FactorAltura(modelo,Mathf.Lerp(3,7,(float)azar.NextDouble()));puestas++;
                despejes.Add(new Vector3(x,z,5.5f)); // revisión 4: las rocas van ANTES que los árboles y reservan sitio — nada de árboles encima de rocas
            }
            informe.AppendLine($"Rocas colocadas: {puestas} (costa e islotes), normalizadas a 3-7 m.");
        }
        var frondosa=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Vegetation/Tree05_a01.prefab");
        Arboledas(bosque,prefab,frondosa!=null?frondosa:prefab,azar);
        // Revisión 11 (Claude): recuperado de la revisión 10 — pinar en la ladera detrás del pueblo de la cascada
        // (lado noreste, hacia el pico; `Arboledas` no llega ahí porque descarta pendientes >33°) y rocas en la
        // ceja del acantilado, para que el pueblo quede "pegado a la montaña" y no delante de una pared gris.
        {
            var pueblo=Zonas[2]; var u=new Vector2(0.86f,-0.51f); var ne=new Vector2(0.51f,0.86f); int pinos=0; // Zonas[2] = poza (revisión 12): el pinar queda en la ladera al noreste de la poza
            var pino=frondosa!=null?frondosa:prefab;
            for(int i=0;i<900&&pinos<220;i++)
            {
                float t=Mathf.Lerp(-70,90,(float)azar.NextDouble()), w=Mathf.Lerp(26,95,(float)azar.NextDouble());
                var q=new Vector2(pueblo.centro.x,pueblo.centro.z)+u*t+ne*w;
                float h=terreno.SampleHeight(new Vector3(q.x,0,q.y))+terreno.transform.position.y; if(h<2||h>105||CostaDist(q)<25)continue;
                if(terreno.terrainData.GetSteepness((q.x+650)/1300,(q.y+650)/1300)>30)continue;
                if(Vector2.Distance(q,new Vector2(CascadaCentro.x,CascadaCentro.z))<CascadaRadio+6)continue;
                float nivel;if(arroyoSuave!=null&&Cercania(q,arroyoSuave,out nivel)<9)continue;
                bool libre=true;foreach(var d in despejes)if(Vector2.Distance(q,new Vector2(d.x,d.y))<d.z){libre=false;break;} if(!libre)continue;
                var arbol=(GameObject)PrefabUtility.InstantiatePrefab(pino,bosque);
                arbol.transform.position=new Vector3(q.x,h-.15f,q.y);arbol.transform.rotation=Quaternion.Euler(0,(float)azar.NextDouble()*360,0);
                arbol.transform.localScale*=FactorAltura(pino,Mathf.Lerp(9,13,(float)azar.NextDouble()));MatizarArbol(arbol);pinos++;
            }
            if(rocas.Count>0) for(int i=0;i<60;i++)
            {
                float ang=(float)azar.NextDouble()*Mathf.PI*2, r=Mathf.Lerp(CascadaRadio-6,CascadaRadio+4,(float)azar.NextDouble());
                var q=new Vector2(CascadaCentro.x+Mathf.Cos(ang)*r,CascadaCentro.z+Mathf.Sin(ang)*r);
                float sLado=(q.x-CascadaCentro.x)*CascadaDireccion.x+(q.y-CascadaCentro.z)*CascadaDireccion.y;
                if(sLado>-4)continue; // solo la ceja alta (aguas arriba)
                float nivel;if(arroyoSuave!=null&&Cercania(q,arroyoSuave,out nivel)<7)continue;
                var modelo=rocas[azar.Next(rocas.Count)];
                var roca=(GameObject)PrefabUtility.InstantiatePrefab(modelo,bosque);
                roca.transform.position=new Vector3(q.x,terreno.SampleHeight(new Vector3(q.x,0,q.y))+terreno.transform.position.y-.4f,q.y);roca.transform.rotation=Quaternion.Euler(0,(float)azar.NextDouble()*360,0);
                roca.transform.localScale*=FactorAltura(modelo,Mathf.Lerp(3,6,(float)azar.NextDouble()));
                despejes.Add(new Vector3(q.x,q.y,5));
            }
            informe.AppendLine($"Pueblo de la cascada: {pinos} pinos en la ladera de detrás y rocas en la ceja del acantilado.");
        }
    }
    // Sitio libre para vegetación/rocas: fuera de pueblos, caminos, río, playas bajas, hitos y del agua.
    static bool Libre(float x,float z,float h)
    {
        if(EnSolar(x,z))return false;
        foreach(var zona in Zonas)if(Mathf.Abs(x-zona.centro.x)<zona.mitad.x+12&&Mathf.Abs(z-zona.centro.z)<zona.mitad.y+12)return false;
        foreach(var ruta in rutasSuaves){float altura;if(Cercania(new Vector2(x,z),ruta,out altura)<12)return false;}
        if(sendasSuaves!=null) foreach(var senda in sendasSuaves){float altura;if(Cercania(new Vector2(x,z),senda,out altura)<6)return false;} // revisión 13
        float nivel;if(Cercania(new Vector2(x,z),rioSuave,out nivel)<15)return false;
        if(arroyoSuave!=null&&Cercania(new Vector2(x,z),arroyoSuave,out nivel)<11)return false;
        foreach(var d in despejes)if(Vector2.Distance(new Vector2(x,z),new Vector2(d.x,d.y))<d.z)return false;
        foreach(var p in Playas)if(h<9&&Vector2.Distance(new Vector2(x,z),new Vector2(p.x,p.y))<p.z+30)return false;
        return h>=1.5f;
    }

    static void VerificarRelieve()
    {
        for(int i=0;i<rutasSuaves.Length;i++)
        {
            float pendiente=0;
            var ruta=rutasSuaves[i];
            for(int j=1;j<ruta.Length;j++)
            {
                var delta=ruta[j]-ruta[j-1];float horizontal=new Vector2(delta.x,delta.z).magnitude;
                if(horizontal>.01f)pendiente=Mathf.Max(pendiente,Mathf.Abs(delta.y)/horizontal);
            }
            informe.AppendLine($"Camino {i+1}: pendiente máxima de diseño {pendiente:P1}. Colisiones y pasos por los pueblos pendientes de recorrido en Unity.");
            if(pendiente>.5f)throw new InvalidOperationException("Pendiente de diseño excesiva en camino "+(i+1));
        }
        foreach(var b in barcos)
        {
            float lecho=terreno.SampleHeight(b.center)+terreno.transform.position.y;
            if(lecho> -1)throw new InvalidOperationException("Un barco sigue sobre tierra: "+b.center);
        }
        VerificarAgua();
        for(int z=0;z<513;z+=8)for(int x=0;x<513;x+=8)
        {
            float h=terreno.terrainData.GetHeight(x,z)+terreno.transform.position.y;
            if(float.IsNaN(h)||h<= -24.9f||h>=274.9f)throw new InvalidOperationException("Relieve fuera del rango del TerrainData.");
        }
        informe.AppendLine("Relieve revisado: cauce curvo, valle ancho, tres picos y ensenada. Verificaciones numéricas superadas; no equivalen a validación visual ni de navegación.");
    }
}
