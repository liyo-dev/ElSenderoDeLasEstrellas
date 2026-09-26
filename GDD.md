# GDD — El Sendero de las Estrellas

**Versión:** 2.7 (revisión de diálogos del prólogo y el tramo inicial)
**Autor original:** Raúl Báez Amate — Liyodev
**Última revisión:** 25 de septiembre de 2026 — ver [Registro de cambios](#registro-de-cambios).

---

## Sobre este documento

Este archivo sustituye al antiguo GDD en Google Docs como **fuente de verdad única para diseño y narrativa**, con el mismo criterio que `TDD.md` ya aplica a la documentación técnica (ver `TDD.md` §20, "Convenciones de documentación del proyecto"): un solo sitio versionado con el código, en vez de contenido disperso que se queda desactualizado sin que nadie se dé cuenta.

El Google Doc original quedó congelado en "Versión 1.0" mientras el juego seguía cambiando por debajo — la corrección de lore de Liam (ver más abajo) es el ejemplo más claro, pero no el único. A partir de ahora, cualquier cambio de historia, personajes o balance de contenido se edita aquí, igual que un cambio técnico se edita en `TDD.md`.

**Qué va en cada documento:**
- `TDD.md` — arquitectura, sistemas, reglas de código, bugs técnicos.
- `GDD.md` (este archivo) — historia, guión, fichas de personaje, balance de hechizos/contenido, estado de diálogos.
- `CLAUDE.md` / `AGENTS.md` — resumen corto de ambos para que las IA lo carguen sin gastar espacio de más.

**Canon narrativo:** la novela también es fuente del universo. La versión revisada está en el proyecto complementario, en Novela/tmp/build_final/epub/libro.md. Toda corrección de historia se contrasta con ese manuscrito; si no se edita la novela, el GDD debe dejar claro que la modificación es una adaptación de juego y no un cambio de canon.

---

## Índice

1. [La Historia](#la-historia)
2. [La Verdadera Historia de Will](#la-verdadera-historia-de-will)
3. [Guión Técnico](#guión-técnico)
4. [Fichas de Personajes](#fichas-de-personajes)
5. [Estado de Balance de Hechizos](#estado-de-balance-de-hechizos)
6. [Estado de Diálogos](#estado-de-diálogos)
7. [Cobertura espacial de Eldoria](#cobertura-espacial-de-la-maqueta-eldoria--base-del-10-requisitos-revisados-el-25-de-septiembre-de-2026)
8. [Registro de Cambios](#registro-de-cambios)

---

## La Historia

Hace siglos, un mago recorrió el Sendero de las Estrellas y pidió un poder al que nadie pudiera oponerse. Al regresar al mundo, inició una Marcha de Conquista. Un mago del valle se enfrentó a él para proteger a su gente; el choque entre el hechizo destructivo del invasor y la Protección Absoluta del defensor selló al Mago Oscuro dentro del Sendero y acabó con la vida del héroe. Su alma sobrevivió y, con el tiempo, se unió a la de un joven llamado Will cuando este murió de una fiebre.

En el presente, Liam busca una cura para Tobías, su hermano menor. Sus investigaciones apuntan al Sendero y al poder de su altar, pero necesita a alguien capaz de abrirlo. Liam encuentra a Will y provoca el ataque del bosque para despertar su magia. Will cree al principio que viaja para aprender a controlarla; Liam no le revela el motivo ni la medida de la manipulación.

Estela se une al viaje. Los tres atraviesan el Reino, recuperan las partes que faltan en el Libro y abren el Sendero juntos. Allí superan en grupo la prueba de Will y la de Estela, descansan en una feria y llegan a la Caja, la prueba de Liam. La Caja muestra a Will y Estela la verdad sobre la emboscada. Liam asume sus decisiones, pero la confianza queda rota y el grupo se separa.

Will elige continuar por convicción propia. Encuentra de nuevo a Estela y ambos rescatan a Liam, sin fingir que la traición o el golpe accidental de Estela no ocurrieron. Antes del combate final acuerdan límites y preparan un plan conjunto. En la biblioteca del Mago Oscuro entienden cómo los deseos se tuercen cuando pretenden sustituir las decisiones de las personas.

El Mago Oscuro revela que Will es la reencarnación del mago que lo detuvo. Los tres luchan juntos. Will emplea el Hechizo del Tiempo y queda exhausto; Liam se interpone en el ataque que iba a matarlo. Estela comparte su energía con Will y los dos cortan el vínculo de sombra que sostenía al Mago Oscuro.

Will pide al altar que cure a Tobías sin alterar su voluntad, sus recuerdos ni trasladar el daño a nadie. Después usa el Hechizo de Resurrección para devolver la vida a Liam, entregando la suya y destruyendo el Sendero. Liam despierta junto a Estela en el mundo real. El espíritu de Will se despide de ambos y se reúne con su familia bajo las estrellas.

## La Verdadera Historia de Will

El Mago Oscuro obtuvo en el Sendero el poder de que nadie pudiera oponérsele. Su Marcha de Conquista avanzó de reino en reino. En el valle, el Will original —un mago que usaba su don para sanar y ayudar— fue el único que se interpuso entre el ejército invasor y su gente.

El Mago Oscuro lanzó un hechizo prohibido para borrar el valle. Al perder el control de la energía, amenazó con desgarrar la realidad. Will respondió con la Protección Absoluta para contener la explosión y salvar a los habitantes, no para salvarse a sí mismo. El choque los separó: el Mago Oscuro quedó ligado y sellado dentro del Sendero; Will murió, pero su alma sobrevivió al sacrificio.

Siglos después, un joven llamado Will enfermó de gravedad. Cuando su corazón dejó de latir, el alma del antiguo mago encontró en él un recipiente compatible y lo devolvió a la vida. El muchacho creció sin recordar su vida anterior. La pesadilla recurrente del prólogo es el primer eco de aquel enfrentamiento.

## Guión Técnico

> **Alcance y fuentes de estado (25 sep 2026):** el grafo activo de esta revisión es Assets/NarrativeGraph/Cap1.asset, junto con Secundary.asset. Los archivos MainNarrative_Cap2.asset a Cap6.asset están en Assets/NarrativeGraph/Versiones antiguas y no se tratan como flujo vigente. Para confirmar implementación se contrastan el grafo activo, los assets de secuencia y las quests; para fijar canon se usa la novela completa.

> **Lectura del texto en pantalla:** cada turno en un bocadillo debe tener como máximo tres frases y ocupar una unidad de sentido completa. Si no cabe, se reescribe o se divide con una reacción/pausa natural. No dejar una palabra sola en la página siguiente. La revisión de frases y el ajuste visual son tareas distintas; las páginas solo se consideran revisadas al verlas en la interfaz real del juego.

### 1. Prólogo: la última noche del Archimago

El arranque actual es la secuencia jugable SEQ_Prologo_UltimaNoche, disparada mediante PROLOGUE_START y cerrada con PROLOGUE_DONE en Cap1.asset. La secuencia cuenta la última noche del Archimago antes de convertirse en la pesadilla recurrente de Will. El jugador resuelve tres encargos cotidianos en el valle —el horno, una carreta y un globo atascado en el campanario—, comparte el desayuno con Liora y ve la carta de la academia que el Archimago no alcanza a responder. Después ayuda a evacuar el valle; no puede salvar a todo el mundo, se despide de Liora y se enfrenta al Mago Oscuro.

El duelo no se gana por daño: el jugador debe redirigir el hechizo del Mago Oscuro hacia el Sendero. La secuencia termina en el corte al despertar de Will. Es una pesadilla jugable que anticipa el sacrificio antiguo, sin explicar aún la reencarnación ni el parentesco del héroe con Will.

**Estado:** la secuencia y sus señales están en assets y conectadas desde el Capítulo 1 activo. Sustituye la descripción antigua de seis planos de PrologueDreamSequencer; esa secuencia anterior ya no describe el contenido actual.

### 2. La mañana de Will: casa, Oliver y Eldran

En esta adaptación del juego no hay una carta de Eldran: Will despierta tras la pesadilla, oye jaleo fuera desde la ventana y sale a ver qué ocurre. Allí se encuentra con Oliver, que llega corriendo, sin aliento, le enseña un hechizo nuevo y provoca un estallido que les salpica a ambos. La secuencia termina con una transición a los menús y al control del grupo.

El jugador recorre el pueblo y presencia la discusión de Eldran y Victoria por cómo colocar la fruta: queda una sola caja y Eldran teme que las manzanas magullen las peras. La discusión se resuelve entre ellos y establece su familiaridad y el tono cotidiano del pueblo. Oliver, que ya ha llegado con Will, se ofrece a ayudarles con las cajas antes de que ambos se marchen. Eldran da a Will una moneda como adelanto de su regalo por los dieciocho años. Will pregunta para qué la quiere y Eldran le responde que quiere que tenga algo para él, aunque sea una tontería; Will acaba eligiendo una estrella de madera en el puesto de Tomasa.

**Estado:** SEQ_OliverSaludo y SEQ_PerasEldran están en Assets/_SEQUENCES; Cap1 contiene sus señales de entrada y salida, la incorporación de Oliver al grupo y el tutorial de minimapa para encontrar a Eldran.

### 3. La caja de fruta: encargo y control del mapa

Tras comprar el objeto inútil que Eldran pidió, SEQ_EldranCaja hace que Eldran llame a Will desde lejos, se acerque y le diga «Ven, sígueme». El jugador le sigue con el control (Eldran le espera si se queda atrás). Por el camino paran en el punto de guardado de la entrada del reino: Eldran explica para qué sirve y Oliver bromea con que él para ahí cada vez que un hechizo le sale mal. Siguen hasta el borde del bosque y allí Eldran le encarga recuperar la caja que dejó al pie de un fresno y le dice que le espera. El jugador sigue el marcador del minimapa hasta la caja, la recoge y se la devuelve en ese mismo punto, de modo que el Despertar de la Estrella ocurre fuera del pueblo, sin vecinos alrededor. El marcador y el tutorial sirven para enseñar exploración y entrega de objetos antes del Despertar de la Estrella.

**Estado:** SEQ_EldranCaja está en Assets/_SEQUENCES y el grafo activo Cap1 incluye la llamada de Eldran, el paseo guiado con parada en el punto de guardado (20d, 20d2, `Eldran_PuntoGuardado`) y hasta la marca `Eldran_Bosque` (20d3, GuiarJugadorNode), el encargo en el bosque (20e), el tutorial de minimapa y la entrega (INC-460). La petición de la caja se presenta como encargo cotidiano: conservarla como vínculo con la novela y no añadir una explicación profética antes de tiempo.

### 4. El Despertar de una Estrella

Eldran avisa a Will cuando el proyectil mágico se dirige hacia él. La secuencia ralentiza el tiempo y deja al jugador responder bajo presión. Si Will no consigue activar el escudo instintivo, la secuencia reinicia el mismo evento; al completarlo, se desbloquean el ataque mágico y Bola de Fuego.

**Confirmado en los assets vigentes:** SEQ_StarAwakening usa AWAKEN_START y emite AWAKEN_DONE o AWAKEN_FAILED. Cap1.asset espera ambos resultados; el fallo vuelve a iniciar el evento. La victoria desbloquea habilidades antes de iniciar la misión del Demonio.

### 5. El Demonio y los planes de Liam

El enfrentamiento contra el primer Demonio es un combate normal de gameplay, no una cinemática. El jugador aprende magia básica, se mueve por la arena y combate mientras Eldran lo anima desde una posición cercana.

**Confirmado en Cap1.asset:** tras AWAKEN_DONE se desbloquea el ataque mágico y Bola de Fuego; comienza ELDRAN_MISSION3 y se lanza el combate Demon_1. Después de la victoria se dispara la secuencia de Liam con la bola de cristal, mediante LIAM_CRYSTAL_START y LIAM_CRYSTAL_DONE. Es un beat aparte del combate y revela que Liam está observando a Will.

Al concluir, el grafo completa la misión ELDRAN_MISSION3. El nodo se titula «ELDRAN SE VA A CASA DE WILL»; la ubicación del punto de guardado depende del montaje de escena y no se infiere solo por el título.

### Estado de la adaptación a partir del Demonio — revisión del 25 de septiembre de 2026

El orden de abajo sigue la novela como canon y traduce cada bloque a una experiencia de juego. Las mecánicas son objetivos de diseño, no funciones aprobadas por el mero hecho de aparecer aquí.

**Estado de implementación:** la carpeta activa Assets/NarrativeGraph contiene Cap1.asset y Secundary.asset; los antiguos MainNarrative_Cap2.asset a Cap6.asset están en Versiones antiguas. Por tanto, aquellas comprobaciones de agosto contra seis capítulos describen una versión archivada del grafo y no prueban el estado actual del juego. Los assets de secuencia, quests, escenas y localización prueban que existe contenido preparado, pero no que el recorrido posterior al Demonio esté conectado y sea jugable de principio a fin. Cada estado de abajo distingue esos casos.

**Alcance de la refactorización actual:** la base de gameplay se da por trabajada hasta la Caja. Las secciones 16–21 describen el recorrido posterior a esa prueba y se desarrollan abajo con objetivos jugables, puzles, combates, descansos y uso de habilidades. Esta ampliación no sustituye la verificación posterior en Unity.

**Criterio para textos en bocadillo:** un bocadillo no debe pasar de tres frases. Si una intervención necesita más, se convierte en dos turnos con una pausa, reacción o cambio de plano que justifique el relevo. No se parte una frase para dejar una palabra suelta en el bocadillo siguiente ni se fuerza una página manual: se ajusta la redacción al espacio real del cuadro. Si aun así no cabe, se recorta o se divide en una unidad de sentido completa. Las líneas largas existentes en localización se consideran pendientes de adaptación a esta pauta.

### 6. La preparación y el camino a Estela — novelas IV–VII

Tras el primer Demonio, Eldran explica que el grupo necesita ayuda experta. Will se prepara para salir del Reino y buscar a Estela en el Bosque Prohibido. La capa de Victoria, la poción y el entrenamiento con Erika pueden funcionar como tareas opcionales o como objetivos de preparación; la progresión debe enseñar equipo, consumibles y combate sin detener la aventura con tres recados equivalentes.

**Diseño jugable:** presentar las tareas en paralelo y permitir que el jugador elija el orden. El entrenamiento con Erika introduce la Bola Prisma si esa progresión se conserva. El bosque conduce a la presentación de Estela y a un combate cooperativo contra el Gólem. El incidente deja claro que el grupo ya no viaja solo con Will y Eldran.

**Canon que debe conservarse:** la novela muestra el miedo de Will a no estar a la altura, los planes de Liam y la desconfianza de Estela hacia su conducta controladora. La invocación del Gólem es una acción de Liam; el título antiguo del nodo «LIAM AMENAZADO POR ESTELA» no basta para establecer que Estela amenazara a Liam.

**Estado:** el guion y los assets antiguos describen misiones, secuencias y desbloqueos para este tramo. La conexión vigente debe comprobarse en las escenas y quests actuales antes de marcarlo como jugable. La secuencia de aparición de Estela y el combate contra el Gólem no se dan por confirmados a partir del grafo archivado.

### 7. La taberna, el arresto y la deuda del Rey — novelas IX–XII

La escena de la taberna combina un respiro cómico con una persecución de Estela que pone al pueblo en peligro. La explosión que daña la montaña tiene que sentirse como consecuencia de la persecución, no como un gag aislado: el jugador ayuda a evacuar o proteger a los vecinos antes del remate de la secuencia.

Después, la guardia conduce al grupo ante el Rey. La tensión por los daños y la sospecha sobre Liam llevan al arresto; escapar del calabozo exige usar el cambio de personaje y cooperar. Al salir, el grupo ayuda a defender el Reino del segundo Demonio. Tras la victoria, el Rey reconoce que los juzgó mal y anula la condena. Después les permite consultar los archivos y enseña a Will Corazón Estelar: una magia de energía compartida que nadie puede imponer a otra persona.

**Adaptación jugable:** persecución con rutas y rescate de civiles; audiencia que establezca la condena; puzzle de celda basado en habilidades complementarias; defensa del castillo como combate distinto del primer Demonio. Separar claramente los dos jefes y dar a la victoria una consecuencia narrativa visible.

**Estado:** estas escenas están descritas en la novela y en documentación de versiones previas del grafo. Su cableado actual y el evento de aprendizaje de Corazón Estelar necesitan confirmación contra los assets activos. El perdón del Rey ocurre después de defender el castillo, no durante la audiencia ni al salir del calabozo. La deuda del título del capítulo incluye la condena que el Rey retira y el coste de reparar los daños.

### 8. La biblioteca y la salida del Reino — novelas XIII–XV

En la biblioteca real, el grupo investiga las versiones contradictorias del Sendero y las referencias a deseos de curación. Liam oculta para quién busca una cura; Will acaba preguntándoselo directamente. Antes de partir, una conversación con Tobías a través del espejo conecta la aventura con la persona concreta que Liam intenta salvar. La escena importa porque Liam deja de ser solo «el compañero misterioso»: el jugador ve el vínculo fraternal y lo que está en juego.

Durante el viaje, el grupo aprende a leer rastros mágicos. La ruta del Fuego Fatuo puede convertirse en un segmento de exploración nocturna: seguir señales auténticas, comparar rastros y reconocer los engaños del entorno. La charla de camino deja que los tres se conozcan y marca que protección y control pueden parecerse, pero no son lo mismo.

**Diseño jugable:** intercalar búsqueda de información, conversación opcional y exploración; evitar que el Fuego Fatuo sea únicamente un puzzle de seguir un color. Las pistas deben poder leerse por forma, movimiento o reacción del entorno, además del color, y el jugador debe poder recuperarse de una ruta falsa sin reiniciar un tramo largo.

**Estado:** existen claves de diálogo para la conversación de Will y Liam, y hay contenido de quests de Fuego Fatuo y del amigo de Eldran en el proyecto. Esto acredita material preparado, no la secuencia completa ni el estado de integración.

### 9. Silas y el hechizo del tiempo — novela XVI

El amigo de Eldran se llama Silas. Su petición no es un favor de recadero: el grupo recupera un reloj perdido en un taller atrapado en un ciclo de cuarenta segundos. Resolverlo requiere observar qué cambia, recordar qué persiste entre ciclos y combinar las capacidades de Will, Estela y Liam. La historia revela la amistad de juventud entre Silas y Eldran y el duelo de Eldran por Selene, que explica parte de su sobreprotección.

Silas explica el ritual para llegar a la Piedra y la regla del Sendero: no premia una etiqueta de «bueno», sino que pone a prueba si cada viajero puede reconocer aquello que corrompería su deseo. También describe la regresión temporal como una oportunidad breve con coste, no como un reinicio gratuito.

**Diseño jugable:** puzzle de bucle corto con intentos rápidos, pistas persistentes y coste legible. El jugador debe aprender una regla y aplicarla, no repetir la misma secuencia hasta acertar. Mantener las heridas, el agotamiento o el recurso gastado al volver atrás, según lo que permita el sistema final.

**Estado:** capítulo canónico de la novela. La misión del reloj, la conversación de Silas y la explicación del hechizo siguen pendientes de confirmar en la versión jugable actual.

### 10. El precio pequeño: Risco y Vega — novela XVII

Antes de llegar a la Piedra, Will y sus amigos encuentran dos aldeas que comparten un manantial. Una construyó la presa; la otra depende del agua. Ambas tienen documentos que justifican sus reclamos. Liam propone resolverlo con persuasión mágica; Estela se opone a decidir por la gente. Will propone investigar y negociar, aunque no exista una solución perfecta ni todos queden satisfechos.

**Diseño jugable:** misión sistémica breve con tres fuentes de información: medir la fuga de la presa, escuchar a las familias de Vega y acompañar la apertura de las compuertas de Risco. El jugador reúne pruebas y ayuda a establecer un reparto y una revisión futura. Evitar un diálogo de elección binaria que finja que una aldea tiene toda la razón: la salida debe depender de comprender el problema y pactar condiciones.

Este episodio es una prueba del carácter de Will antes de la prueba mágica. El «precio pequeño» es aceptar que un acuerdo imperfecto puede ser mejor que imponer una solución definitiva.

**Estado:** canon de la novela. La misión no aparece en el guion antiguo del juego; debe añadirse o descartarse deliberadamente al convertir esta parte en nivel jugable.

### 11. La Piedra Ancestral y el acceso al Sendero — novela XVIII

El camino atraviesa ruinas donde quienes llegaron antes dejaron sus nombres y motivos. La cámara de la Piedra solo se ve reflejada en el agua; el grupo debe avanzar confiando en ese reflejo. Al recitar Will el conjuro de Silas, aparece el Guardián. El combate se gana cooperando: Will sostiene la línea, Estela presiona y Liam encuentra puntos débiles con su magia de pacto.

Tras la victoria, las copias incompletas del Libro encajan con las inscripciones del pedestal. El grupo obtiene el ritual de apertura y las fórmulas del Hechizo del Tiempo y del Hechizo de Resurrección. La segunda exige una vida voluntaria a cambio de otra recién perdida. El coste debe explicarse con claridad antes de que la historia llegue al sacrificio; no presentarlo como una sorpresa mecánica.

El grupo estudia las reglas, acuerda señales y un punto de reunión y decide cruzar juntos. El acceso puede ser el último punto de guardado seguro antes de entrar.

**Diseño jugable:** navegación por reflejos, jefe con funciones complementarias y un momento de preparación en el que se revisan controles, consumibles y reglas del Sendero. El jugador debe entender que el hechizo temporal no cura ni revierte el coste de sus acciones y que el de resurrección intercambia vidas.

**Estado:** los diálogos localizados PIEDRA_ANCESTRAL_01–08 y APERTURA_SENDERO_01–03 existen en los archivos de localización. Su puesta en escena, asignación de voces y conexión en el juego aún requieren verificación. La descripción antigua del GDD no mencionaba la obtención de las fórmulas en la Piedra; queda sustituida por esta versión de la novela.

### 12. Entrada al Sendero y prueba de Will — novelas XIX–XX

Los tres cruzan juntos. La primera prueba enfrenta a Will con recuerdos, no con una sucesión de caminos individuales para cada personaje. Al principio el laberinto convierte una vergüenza social en humor; después llega a la pesadilla recurrente y a recuerdos que alimentan el miedo de Will a no llegar a tiempo. Estela y Liam lo acompañan hasta que el Sendero los separa y Will debe dejar de obedecer cada llamada de auxilio ilusoria. La salida consiste en reconocer que no puede salvar a todo el mundo y aun así elegir a quién escuchar.

**Diseño jugable:** laberinto de recuerdos con cambios de tono y una mecánica de priorización. Las voces falsas reutilizan patrones aprendidos, pero el jugador obtiene señales para distinguirlas. No castigar la exploración con un fallo terminal por atender a una ilusión; el conflicto debe expresar el límite emocional de Will, no parecer una trampa arbitraria.

**Diálogo:** las líneas PRUEBA_WILL_01–04 están en cinemáticas localizadas. Deben distribuirse entre Estela, Will y Liam según la escena; no poner en una misma caja la broma y la confesión emocional. Mantener cada bocadillo por debajo de tres frases y comprobar que no quede una palabra aislada en una página.

**Estado:** texto localizado presente; secuencia jugable y acompañamiento de los otros personajes pendientes de comprobación.

### 13. Chuchelandia — novela XXI

La segunda prueba pertenece a Estela, pero el grupo la afronta unido. El reino obliga a sus habitantes a declararse felices y castiga cualquier muestra de tristeza. A Estela le asignan el papel de reina; Will y Liam quedan disfrazados y pueden investigar el palacio y ayudar a quienes esconden lo que sienten. El antagonista no es solo un reino cursi: es una falsa misericordia que evita el dolor borrando la libertad de sentirlo.

Estela debe enfrentarse a la pérdida de su hermana Mara y a la culpa que arrastra. El duelo contra el Duque de Regaliz y el Gólem de azúcar no la «cura» ni elimina su tristeza: le permite recordar a Mara sin fingir que solo existe el último día. La criatura de gominola es parte de ese conflicto y del desenlace del combate; no reducirla a un chiste o a un objeto coleccionable.

**Diseño jugable:** exploración social con disfraces, pistas y personajes que necesitan ayuda; jefe de varias fases que conecta las mecánicas aprendidas por Estela con el apoyo de Will y Liam. Tras la victoria, dejar un momento jugable y tranquilo para hablar de Mara y permitir que los habitantes decidan si salen del reino.

**Estado:** DLG_CANDYLAND_01–17 está localizado en ES/EN y existe trabajo de construcción de la escena. El bloque actual de 17 líneas es una adaptación de juego, pero no cubre por sí solo todo el conflicto de la novela; completar el arco del osito, la criatura de mazapán, la jaula y Mara antes de dar el nivel por narrativamente equivalente.

### 14. La feria y la pausa del grupo — novela XXII

Después de las pruebas, los amigos descansan en una isla segura y luego visitan una feria. Comparten atracciones, comida y conversación. Will recibe un peluche con forma de bola de fuego. La feria ofrece al jugador un respiro real: lavar ropa, reparar equipo, jugar y conversar sin una amenaza inmediata. Durante esta pausa Liam se acerca a confesar algo, pero aún no se atreve a decirlo.

**Diseño jugable:** zona opcional de interacción y preparación, con actividades cortas que refuercen lo que cada personaje sabe de los otros. Las conversaciones no deben adelantar falsamente la confesión completa. El regalo y las rutinas compartidas deben regresar visual o mecánicamente más tarde como recuerdos, no como pistas obligatorias para resolver un puzzle.

**Estado:** PARQUE_LIAM_01–12 está localizado. La clave PARQUE_LIAM_10 contiene actualmente cinco frases largas; dividirla en dos turnos completos y localizar ambos como frases con sentido. No conservar una página que termine con «entendido» o cualquier otro fragmento aislado.

### 15. La Caja: prueba de Liam — novela XXIII

La tercera prueba es la única que separa al grupo. Liam entra solo en la Caja, un laberinto de espejos; Will y Estela ven parte de la prueba desde fuera a través de una superficie de luz. La Caja muestra primero un recuerdo feliz de Liam con Tobías y después el deterioro de sus decisiones: el diagnóstico, el libro robado, el pacto y el uso de Will como cebo.

Liam se enfrenta a una sombra que representa sus propias decisiones. La salida no consiste en negar la desesperación ni culpar a una parte separada de sí mismo: debe reconocer que fue él quien tomó esas decisiones y rechazar la última oportunidad de volver a sacrificar a Will a cambio de salvar a Tobías. La Voz Ancestral pregunta por el precio real. La visión revela al grupo la emboscada del bosque y las manipulaciones posteriores.

**Diseño jugable:** tramo de Liam en solitario que combina navegación, combate y recuerdos interactivos. Cada espejo ofrece contexto y también una decisión de Liam. Evitar que el combate venza a la responsabilidad moral: la sombra se supera admitiendo la autoría de los actos. Alternar control de Liam con la observación de Will y Estela solo en momentos que añadan contexto.

**Estado:** PARQUE_LIAM_12 está localizado y coincide con la pregunta de la novela. El resto del nivel, el recuerdo feliz, la sombra y la proyección de la verdad necesitan guion y validación de implementación. Las escenas 14 y 15 del guion antiguo se consideraban una misión fusionada; esa nota dependía de Cap6 archivado y se retira como estado actual.

### 16. La ruptura y sus consecuencias — novela XXIV

Al salir Liam, Estela exige una explicación. Will pregunta directamente si Liam pensaba matarlo; Liam admite que quiso usar su poder para abrir el altar y que se dijo que evitaría su muerte. No consigue justificar haberle quitado la posibilidad de elegir. Estela ataca impulsivamente; Will recibe parte del golpe al interponerse. Estela se marcha y Liam se aleja por otro camino.

No resolver el conflicto con una sola disculpa ni con un QTE que haga creer que el jugador podía evitar la ruptura. Después, Will permanece solo y se permite estar enfadado. Decide continuar por un motivo propio: detener al Mago Oscuro y buscar cómo volver, sin prometer entregar su vida.

**Diseño jugable:** conversación con interrupciones breves y actuación, seguida de una separación espacial real. Como transición, dar al jugador control de Will para vendarse, revisar sus objetos y decidir continuar. La violencia de Estela debe ser accidental y no premiada; su propia historia y el daño a Will se abordan después.

**Estado:** RUPTURA_ESTELA_01–04, RUPTURA_LIAM_01–02 y RUPTURA_WILL_01 están en localización. El texto abreviado actual debe revisarse frente al intercambio completo de la novela: Will pide hechos, Liam asume responsabilidad y Estela no abandona la escena sin que se vea el daño que causó.

### 17. Reunión con límites y confianza reconstruida — novela XXV

Will encuentra primero a Estela y reconoce que ella le hizo daño; decide acompañarla sin exigir que finja estar bien. Juntos rescatan a Liam de las bestias. Will le ofrece la mano, pero el reencuentro no borra la traición: el grupo acuerda reglas concretas. Liam responde preguntas, no toma decisiones por ellos y no usa magia oscura sin consentimiento; Estela no tiene que declarar perdón inmediato. Los tres vuelven a cooperar porque eligen hacerlo con límites claros.

**Diseño jugable:** dos tramos de combate y exploración —Will solo, luego Will y Estela— que culminan en rescatar a Liam. La recuperación de poder puede expresarse como coordinación de equipo, pero no como borrón y cuenta nueva ni como aprobación retroactiva del engaño. Después del rescate, el jugador ayuda a elegir qué equipo y qué información se comparte para preparar el tramo final.

**Estado:** REUNION_* está localizado; el texto de la localización no refleja del todo la versión canónica más reciente, donde Will aún está enfadado y dice que todavía no sabe si puede perdonarlo. Revisar antes de reutilizar las frases como diálogo final.

### 18. Biblioteca del Mago Oscuro y preparación — novela XXVI

Antes del altar, el grupo encuentra una biblioteca de deseos cumplidos de forma literal y dañina. El Mago Oscuro conserva esos ejemplos para defender su visión absoluta del poder. Will entiende que un deseo no puede reemplazar las decisiones que siguen. Los tres preparan el combate, acuerdan señales de retirada y reglas para no tratar a nadie como sacrificable. Hablan de la posibilidad de que el altar separe al Archimago de Will y de la cura de Tobías.

**Diseño jugable:** explorar la biblioteca como una serie de viñetas interactivas breves. Cada deseo se presenta con su intención y su consecuencia; el jugador debe poder inspeccionar ambas. Luego se prepara una estrategia flexible con rutas y roles, no una secuencia de pasos perfecta que el combate desautorice al primer cambio.

**Estado:** la conversación y la biblioteca son canon de la novela; el GDD anterior saltaba directamente del Sendero a la revelación del villano. Incorporar este bloque antes de la confrontación. No consta aquí una secuencia activa de juego.

### 19. El Mago Oscuro y la batalla final — novelas XXVI–XXVII

El grupo llega junto al altar y se enfrenta al Mago Oscuro. La verdad sobre la vida pasada de Will se revela durante el conflicto; no convertir una explicación monologada en una cinemática interminable. Repartir la revelación entre líneas y recuerdos breves que permitan al jugador conservar orientación y control.

El combate tiene cooperación real entre los tres. El Mago Oscuro deforma el espacio y cambia sus patrones. En su ataque de área, Will usa el Hechizo del Tiempo y retrocede diez segundos: conserva el agotamiento y las heridas, pero obtiene conocimiento para encontrar un resquicio. Después, debilitado, Will queda expuesto al ataque final y Liam se interpone. Sus últimas palabras piden a Will que cuide de Tobías.

Estela comparte la energía restante con Will. Corazón Estelar se convierte en una aguja de luz que corta el conducto de sombra que une el deseo del Mago Oscuro con el altar. El Mago se deshace cuando pierde ese vínculo; no es la muerte de Liam ni una victoria del jefe lo que por sí solo derrumba el Sendero.

**Diseño jugable:** jefe por patrones y lectura del escenario; tutorial de tiempo integrado en el ataque inevitable y repetición limitada por recurso. La estrategia incluye cambiar de plan cuando el jefe altera la arena. Tras la muerte de Liam, una acción compartida de Estela y Will corta el conducto, respetando el acuerdo previo del grupo de que el poder de otra persona requiere consentimiento.

**Estado:** el GDD antiguo proponía un jefe final centrado en la Regresión Temporal y situaba el sacrificio de Liam antes de vencer al Mago. Queda sustituido por el orden de la novela. Las secuencias y mecánicas finales no se marcan como implementadas sin evidencia en los assets activos.

### 20. El deseo, el derrumbe y el sacrificio de Will — novela XXVII

Will llega al altar con el cuerpo de Liam y pide una cura precisa para Tobías, sin alterar su memoria, su voluntad ni trasladar el daño a otra persona. El altar cura a Tobías; no resucita a Liam. Will sabe que esa frontera exige el Hechizo de Resurrección, cuyo precio es su propia vida. Mientras el Sendero colapsa, usa el conjuro para devolver a Liam la vida y abrir una salida para él. Will se queda dentro y muere al destruir el Sendero.

No reutilizar la versión anterior del GDD donde Will revive a Liam antes del deseo, empuja a Estela fuera del portal y se sacrifica en una escena aislada. En la novela, Liam muere durante la batalla final; Estela y Will vencen juntos; Will pide por Tobías y luego paga voluntariamente la resurrección.

**Diseño jugable:** separar con claridad las dos decisiones: el deseo por Tobías y el intercambio vital por Liam. No usar un QTE con posibilidad de fallo para la decisión final. Mantener la agencia antes de la elección y dejar que el jugador confirme la decisión de Will mediante una acción deliberada sin penalización por tardar.

**Estado:** los textos existentes de deseo/sacrificio deben cotejarse con esta continuidad antes de cerrar subtítulos. La secuencia de colapso, la decisión y el uso del hechizo requieren implementación y revisión emocional en juego.

### 21. Epílogo: la despedida — novela XXVIII

Liam despierta junto a la Piedra apagada y pregunta por Will. Estela le explica que usó el hechizo de resurrección para salvarlo y que el Sendero desapareció con Will dentro. El espíritu de Will aparece con la silueta del antiguo Archimago. Se despide de ambos, pide a Liam que cuide de Tobías y les agradece haber sido su familia. Su espíritu se reúne con su familia bajo las estrellas. Liam no queda curado de culpa en un instante; decide cargar con ella y vivir de otra manera.

**Diseño jugable:** epílogo cinemático breve, claro y sin volver a explicar información que el jugador acaba de ver. Dejar respirar la respuesta de Liam, las líneas de Will y el plano final del cielo. Los silencios y las miradas cuentan; no añadir una cadena de frases para verbalizar cada emoción.

**Estado:** EPILOGO_LIAM_01–02, EPILOGO_ESTELA_01, EPILOGO_WILL_01–03, ESTELA_EXPLAINS_EPILOGUE y WILL_FAREWELL existen en localización. Hay que ordenar esas líneas conforme a la novela, evitar duplicar «cuida de tu hermano» y revisar la puntuación. La despedida no está confirmada como secuencia conectada.

### Correspondencia de hitos narrativos

| Tramo de juego | Canon de la novela | Función de juego | Estado actual |
|---|---|---|---|
| Preparación, bosque y Gólem | IV–VIII | Aprendizaje, presentación de Estela y cooperación | Contenido descrito; integración actual por verificar |
| Taberna, arresto y Reino | IX–XII | Humor, puzzle de prisión y defensa del Reino | Contenido descrito; grafo citado anteriormente está archivado |
| Biblioteca, viaje y Silas | XIII–XVI | Pistas sobre Liam y aprendizaje del tiempo | Hay claves/quests; recorrido completo por verificar |
| Risco, Vega y Piedra | XVII–XVIII | Decisión social, puzzle de reflejo y jefe cooperativo | Canon novelado; adaptar e implementar |
| Pruebas de Will y Estela | XIX–XXI | Recuerdo, exploración y combate narrativo | Candyland tiene 17 líneas localizadas; arco entero por completar |
| Feria, Caja y ruptura | XXII–XXIV | Pausa, prueba de Liam, revelación y separación | Claves localizadas; secuencia por verificar/revisar contra canon |
| Reunión, biblioteca y final | XXV–XXVIII | Confianza con límites, jefe, sacrificio y despedida | Narrativa actualizada; contenido de juego por implementar/verificar |

### Refactorización de gameplay desde la Caja — diseño propuesto

**Punto de corte:** el contenido hasta la prueba de la Caja (novela XXIII) se considera la base de gameplay ya trabajada. Esta sección convierte el resto de la historia —ruptura, reencuentro, biblioteca, batalla final, sacrificio y epílogo— en una propuesta de recorrido jugable. No afirma que esos niveles estén implementados. Los beats y nombres de sistema son guía para diseño; cualquier mecánica nueva queda pendiente de prototipo y validación.

#### Objetivos de experiencia

- Cada zona posterior a la Caja debe tener una acción dominante distinta: sobrevivir solo, coordinarse, interpretar consecuencias, dominar un combate por patrones y, finalmente, elegir qué significa salvar a alguien.
- Alternar presión y descanso. Tras una secuencia emocional intensa, devolver control al jugador en un espacio pequeño, con una tarea sencilla y sin urgencia, antes de pedir otra decisión dramática.
- Evitar el recado de ir y volver como objetivo principal. Una misión secundaria debe aportar un personaje, una mecánica, una historia local o una recompensa útil y visible.
- Aprovechar cambio de personaje, compañeros controlados por IA y ataques especiales conjuntos ya presentes. Los puzles y jefes deben poder leerse sin cambiar de personaje, pero cambiar debe dar una ventaja clara, no ser un requisito opaco.
- Mantener diálogos de bocadillo en un máximo de tres frases. Cada cambio de página debe cerrar una idea o coincidir con una reacción, movimiento o cambio de escena; no dejar una palabra o coletilla aislada. La comprobación final requiere ver cada idioma dentro de la interfaz real.

#### Ruta jugable posterior a la Caja

| Nivel / tramo | Recorrido y actividad principal | Puzle, minijuego o combate | Uso de habilidades y recompensa narrativa | Ritmo y estado |
|---|---|---|---|---|
| 1. Salida de la Caja y ruptura | La revelación termina; el jugador recupera el control de Will para recorrer el borde de la sala, atender la herida y decidir cuándo avanzar. La discusión sucede en un espacio legible y sin amenaza activa. | No hay combate ni QTE para impedir la ruptura. La acción interactiva es atender a Will y abrir el camino de salida cuando esté listo. | Movimiento e inventario devuelven agencia después de la escena. La recompensa es una motivación propia de Will, no un objeto ni una aprobación del grupo. | Beat breve y emocional. La ruptura ocurre por las decisiones ya tomadas; el jugador puede respirar, pero no revertir el canon con una pulsación. |
| 2. Camino en solitario | Tramo compacto con tres zonas conectadas: orientación, obstáculo y emboscada. Will encuentra rastros de Estela y señales de Liam; la ruta avanza por lectura del terreno, no por una cadena de marcadores. | Encuentros cortos que enseñan a sobrevivir solo: enemigos que obligan a moverse, un bloqueo del camino que se abre con magia y un combate de desgaste contra un grupo pequeño. Sin oleadas repetidas. | Fuego resuelve objetivos a distancia; levitación/desplazamiento y hechizos de área se usan si están disponibles. El juego comunica qué herramienta sirve antes de castigar un intento. | Presión moderada con punto seguro al final. Diseñar como nivel breve, no como zona abierta para farmear. |
| 3. Reencuentro con Estela | Will la alcanza; una ruta compartida conduce al rastro de Liam. El jugador alterna exploración y conversaciones opcionales que muestran que Estela sigue dolida y se hace cargo de haber herido a Will. | Combate de pareja con objetivos simultáneos: contener a las bestias que bloquean el camino y liberar a Liam de la zona de peligro. Una bestia cambia de objetivo para premiar proteger al compañero. | Cambio de personaje y colocación de compañeros presentan cooperación práctica. Al rescatar a Liam, el equipo vuelve por elección; se comunica el acuerdo de límites antes del siguiente tramo. | Liberación tras el segmento en solitario. No convertir la reconciliación en una barra de amistad ni exigir perdón instantáneo. |
| 4. Puesto de preparación | Área segura junto al acceso a la biblioteca. El jugador revisa objetos, hechizos y miembros del grupo; puede escuchar una charla opcional y entrar cuando quiera. | Preparación libre; sin combate obligatorio, puzle de confirmación ni temporizador. | Permitir equipar los hechizos que ya se poseen. El grupo verbaliza señales de combate y consentimiento para usar la magia de los demás. | Pausa explícita tras el rescate. Guardado recomendado antes del bloque final si la progresión actual lo permite. |
| 4A. Simulacro de señales (opcional) | En el puesto hay tres blancos de práctica y cada compañero explica una señal: ataque entrante, objetivo marcado y ventana de castigo. | Minijuego de coordinación en tres rondas cortas: cambiar al personaje indicado, esquivar/defender la señal y golpear el blanco cuando queda expuesto. La secuencia no usa cuenta atrás estricta y se puede repetir al instante. | Enseña cambio de personaje, lectura de telegráficos y oportunidad para cargar/activar un especial conjunto. Recompensa: diálogo de equipo y comentarios distintos durante el jefe, sin ventaja estadística exclusiva. | Actividad opcional y autocontenida; saltarla no penaliza ni bloquea el acceso a la biblioteca. |
| 5. Biblioteca de los deseos | Explorar tres salas cortas. Cada sala presenta un deseo, la interpretación literal que recibió y a quién dañó. Se reconstruye la relación causa–efecto con objetos y testimonios, no con texto expositivo largo. | Tres viñetas-puzle: emparejar intención y consecuencia; reconstruir una sala tras una alteración; descubrir qué testimonio falta antes de salir. Fallar ofrece una pista nueva, no reinicia el nivel. | Inspección, levitación y control de área ofrecen formas distintas de revelar objetos. La conclusión prepara al jugador para atacar el vínculo del jefe, no para encontrar una “respuesta moral correcta”. | Exploración silenciosa con salida clara. Limitar cada viñeta a una idea y devolver control entre ellas. |
| 6. Acceso al altar y batalla final | Aproximación corta que anticipa cambios de arena. El Mago Oscuro revela el pasado de Will en fragmentos durante el combate; el jugador nunca recibe un monólogo largo mientras no puede actuar. | Jefe en tres fases: (1) patrones reconocibles y esquiva; (2) altera plataformas y obliga a reposicionarse; (3) expone conductos de sombra y convoca amenazas para distraer. Cada fase introduce una regla, la demuestra y deja practicarla. | Will castiga aperturas a distancia; Estela despeja enemigos menores/controla espacio; Liam coloca trampas o interrumpe al jefe. Alternar personaje mejora la respuesta. El especial conjunto es opción táctica, no puerta de progreso. | No subir solo la vida del jefe: cambiar patrones. Punto de guardado antes y repetición desde fase/ataque corto. La muerte de Liam sucede en el beat de la novela, no por daño aleatorio ni por fallar un QTE. |
| 7. Cortar el vínculo y colapso | Tras el sacrificio de Liam, Will y Estela actúan juntos para romper el conducto de sombra. Empieza el derrumbe y el jugador conduce a Estela hacia la salida mientras Will queda atrás por decisión propia. | Escape breve con dos obstáculos que reutilizan habilidades conocidas y una última acción conjunta de Corazón Estelar. El reto es comprender el plan; no poner un cronómetro estricto sobre el duelo. | Corazón Estelar expresa energía compartida solo con acuerdo explícito de Estela. El Hechizo del Tiempo permite aprender de un ataque decisivo según las reglas de Silas, pero no borra heridas ni coste. | Clímax jugable seguido de secuencia narrativa. No hacer repetir la muerte de Liam ni la elección de Will. |
| 8. Deseo, resurrección y epílogo | El deseo por Tobías y el intercambio de vida por Liam se presentan como dos hechos distintos. Después, el jugador acompaña unos instantes a Estela y Liam en el mundo real antes del adiós de Will. | Sin puzle, jefe, fallo ni recompensa coleccionable después de la batalla. La interacción confirma la decisión de Will sin cuenta atrás ni consecuencias alternativas que contradigan la novela. | El altar cura a Tobías sin alterar su voluntad ni trasladar el daño. Resurrección devuelve a Liam la vida a cambio de Will y destruye el Sendero. | Cierre contemplativo. Silencios, miradas y el plano del cielo sustituyen diálogo redundante. El epílogo no reabre objetivos. |

#### Revisión de misiones secundarias existentes

La carpeta activa contiene misiones de entrega y exploración que sirven para poblar el Reino, pero varias comparten la misma estructura: hablar, conseguir objetos y volver. Deben revisarse como contenido opcional y evitar que su suma interrumpa la ruta principal. Las propuestas siguientes son de diseño; no cambian automáticamente los assets ni las claves de localización.

| Misión / NPC | Estructura actual observada | Problema de diseño | Refactorización propuesta |
|---|---|---|---|
| Roberto — mareo | Pide una poción de vida y la devuelve con una recompensa. | Repite el intercambio de consumible y el síntoma no genera juego. | Desafío opcional de orientación: seguir indicaciones contradictorias por un camino corto y encontrar la causa del mareo. La poción puede ser solución rápida; investigar añade diálogo/recompensa. |
| Manuel — abuela débil | Pide dos pociones de vida a cambio de una recompensa de vestuario. | Recado de compra; se solapa con Roberto. | Tarea de cuidado: revisar qué necesita la abuela, entregar el remedio y recibir un cambio visible en su estado o una escena familiar. No exigir comprar consumibles que el jugador quizá ya gastó. |
| Nora — experimento mágico | Pide tres pociones de maná para probar un hechizo. | La prueba no es jugable. | Minijuego de control mágico: estabilizar tres focos usando disparos de precisión y control de área. Las pociones pasan a ser una vía alternativa, no una barrera. |
| Tabernera — remedio de resaca | Pide dos algas y ofrece pago. | Colección plana, sin consecuencia. | Ruta costera corta con una elección de calidad/cantidad y un gag de preparación del remedio. Sin combate obligatorio. |
| Tendera — remedios | Pide dos pociones de maná a cambio de algas; también entrega algas en otra interacción. | Intercambio circular y posible duplicidad con tabernera. | Unificar ambos pedidos como encargo de abastecimiento del mercado: el jugador decide si intercambia algas o las guarda para el Niño Pez. Mostrar qué desbloquea cada opción. |
| Guardia del Bosque — plaga de arañas | El asset contiene veinte pasos consecutivos. | Fragmenta una actividad y puede volverse limpieza repetitiva. | Agrupar en tres actos: localizar nidos con señales ambientales; elegir quemar, atraer o desviar arañas; cerrar con pelea contra la reina/nido. Conservar progreso y premiar con una herramienta/ruta útil del bosque. |
| Niño Pez — algas y caracola (carpeta PRINCIPALES) | Cadena que entrega algas para poder nadar y después busca una caracola. | El contenido parece lateral, pero el desbloqueo de nado es una habilidad de recorrido y puede sentirse imprescindible. | Decidir expresamente si el nado pertenece a la progresión principal. Dejar la caracola como actividad lateral submarina con corriente, ruta corta y recompensa cosmética o de colección. No clasificar la cadena como secundaria mientras siga en PRINCIPALES. |
| Rudolfo — salto (carpeta PRINCIPALES) | Reto de salto con recompensa de botas según el nombre/configuración de quest. | La recompensa y su pertenencia a progresión principal no quedan claras solo por la configuración observada. | Circuito contrarreloj opcional con atajos y puntos de control; enseñar el salto antes del cronómetro y mostrar la mejora de botas después. Revisar si debe moverse a SECUNDARIA; no asumirlo por su tono de reto. |

**Reglas para cerrar secundarias:** ninguna recompensa básica detrás de muchos objetos aleatorios; no pedir lo mismo en la misma zona sin compartir progreso; permitir volver rápido al NPC; premiar con algo perceptible (ruta, interacción, herramienta, personalización o cambio visible), no solo monedas. Ninguna secundaria será necesaria para comprender el objetivo principal.

#### Papel de las habilidades y lectura del combate

El proyecto ya tiene cambio entre Will, Liam y Estela, aliados con IA y ataques especiales conjuntos con cargas separadas. La refactorización debe dar motivos concretos para usarlos. La asignación siguiente es una función de diseño, no una confirmación de balance o desbloqueo por capítulo.

| Herramienta | Función legible | Uso destacado | Ajuste / cautela |
|---|---|---|---|
| Bola de Fuego | Ataque rápido de alcance medio para iniciar un encuentro. | Enemigos pequeños, blancos expuestos y, si el entorno lo admite, objetos inflamables. | Mantenerla como referencia básica; no hacerla la mejor en daño, control y coste a la vez. |
| Bola Prisma | Golpe pesado para castigar una ventana o romper defensa. | Gólem, Guardián y fases que exponen un punto débil. | Los 25 de daño y 10 de maná son datos del asset; balancear junto con cadencia, alcance y facilidad de impacto. |
| Levitation | Manipular la posición/altura de un objetivo o resolver obstáculos. | Puzles del taller y biblioteca, control de enemigos ligeros. | Comunicar rango/peso y mostrar qué responde. No debe invalidar a todos los enemigos voladores. |
| Tornado / Aura Estelar | Controlar el espacio y separar grupos. | Defensa de civiles, enemigos que rodean y reposicionamiento. | Evitar que se sientan como dos áreas con distinto daño. Dar a uno desplazamiento/interrupción y al otro protección/apoyo si el sistema lo permite. |
| Huracán | Especial de alto impacto para grupo o apertura grande. | Oleadas o fase de jefe con varios blancos. | El coste y la recarga deben justificar el impacto; no diseñar jefes que solo admitan esta habilidad. |
| Garra / Sello del Pacto | Herramientas de Liam para enganchar, marcar o interrumpir. | Trampas, enemigos que preparan ataque y anclajes del Mago Oscuro. | Aclarar diferencia: si ambos solo hacen daño, no sostienen una identidad táctica. |
| Corazón Estelar | El relato lo define como energía compartida voluntariamente; su versión de combate debe expresar apoyo/cooperación. | Ruptura del conducto final y, si se implementa antes, defensa de aliados o ataque combinado. | El asset actual registra 100 de daño, 25 de maná y slot especial. Decidir si es ataque, apoyo o dos usos diferenciados; no equiparar automáticamente stats y función narrativa. |
| Hechizo del Tiempo | Repetir un momento corto para leer un patrón, manteniendo el coste. | Ataque de área anunciado como inevitable y demostración previa con Silas. | No cura, resucita ni borra decisiones. Una repetición tutorializada basta; evitar rebobinado gratuito en cada error. |
| Especiales de Liam y Estela | Remate coordinado que recompensa combatir junto al aliado correspondiente. | Jefes y oleadas donde el jugador prepara carga y escoge el momento. | Aviso visual/audio de carga y oportunidades claras para usar cada especial. No esconder la carga tras atacar solo con un compañero inactivo. |

**Patrón de encuentros recomendado:** señal clara antes del ataque peligroso; presentar una regla nueva en baja presión; combinarla luego con una conocida; cambiar geometría u objetivo antes de subir mucho la vida; cerrar con una ventana de castigo. Alternar combate, exploración, conversación y descanso. Si una habilidad no resuelve, facilita o cambia ninguna situación durante varios niveles, revisar su coste, función y tutorial antes de añadir más hechizos.

**Ritmo anti-repetición:** como objetivo de diseño inicial, cada tramo principal debe cambiar la actividad dominante antes de que el jugador repita por tercera vez el mismo patrón. Para cada zona, diseño anotará: verbo principal, novedad, reutilización, respiro y recompensa. Un minijuego sirve si comunica carácter o enseña una habilidad; un puzle si cambia cómo se lee el espacio; una batalla si exige una decisión distinta de la anterior.

#### Dirección de rediseño del combate — auditoría del 25 de septiembre de 2026

La meta de sensación es acción mágica fluida y espectacular, inspirada por lo que Raúl valora de *Hogwarts Legacy* y *Kingdom Hearts*, con una identidad propia que todavía no está elegida. Los combos de hechizos, los cambios de estado y los ataques de grupo ya aparecen en juegos publicados; no se registran como la novedad por sí mismos. [Gameplay de Hogwarts Legacy](https://blog.playstation.com/2022/03/17/hogwarts-legacy-your-first-look-at-extended-gameplay/), [sistema de elementos de Magicka 2](https://store.steampowered.com/app/238370/Magicka_2/) y [manual oficial de Kingdom Hearts 2.5](https://www.kingdomhearts.com/kh25manual/kingdom_hearts_25_manual_us.pdf).

**Fantasía que vamos a evaluar:** el jugador se mueve, lanza magia con respuesta inmediata, siente los impactos, decide el siguiente paso sin salir del flujo y puede pasar la iniciativa entre Will, Liam y Estela. Esta es una dirección de prueba, no una mecánica aprobada.

**Lo que hay en el proyecto (lectura estática, sin prueba en ejecución):**

- `NPCCombatBrain` contiene evaluación, reposicionamiento, ataque, defensa, búsqueda, retirada para recuperar maná y retirada táctica; también comprueba proyectiles entrantes, distancia, visión y línea de tiro. Esa complejidad está concentrada en lógica general y selección de ranura; no garantiza que un tipo de enemigo tenga identidad reconocible.
- La selección de ataque del NPC humanoide prioriza el especial y después las ranuras derecha/izquierda. Aunque sus hechizos tengan datos distintos, la decisión no está descrita como una elección por función táctica o estado del objetivo.
- `ImpDemonAI` sí tiene ataques por fases, tiempos de recuperación y avisos para ataques a distancia/lluvia. El `RuneCollar` ofrece un punto débil que solo se puede romper con un disparo preciso durante su ventana activa: es un buen ejemplo de ataque, señal y respuesta con relación clara.
- El jugador tiene proyectiles, una defensa sostenida que consume maná, levitación y carga para ataques especiales de Liam/Estela. `Bola de Fuego` admite un disparo preciso con menos daño, más velocidad y menor tamaño. No hay ataque cuerpo a cuerpo general.
- `MagicElement` etiqueta hechizos, y ciertos objetos (`Burnable`) reaccionan al elemento, pero `Damageable` aplica salud numérica y eventos; no se observa una capa central de ventajas elementales y estados de combate para todos los enemigos. Por eso cambiar Fire por Storm no debe prometer por sí solo una estrategia distinta.
- Varias diferencias actuales se expresan principalmente con daño, coste y recarga. Ejemplo de configuración: Bola de Fuego hace 10 de daño por 5 de maná; Bola Prisma 25 por 10; Tornado y Aura Estelar hacen 30 por 5/10 respectivamente. Estos datos no bastan para decidir balance sin medir impacto, cadencia y control en juego.

**Riesgos de experiencia que hay que comprobar al jugar:**

1. Que los NPC humanoides esquiven o bloqueen tantos proyectiles que el jugador perciba sus hechizos como ignorados.
2. Que los enemigos se acerquen, se aparten o se retiren sin una señal clara que permita al jugador anticiparlo.
3. Que mantener el escudo sea la respuesta óptima mientras se espera a que vuelva el maná, convirtiendo el combate en aguante.
4. Que apuntar a cualquier enemigo y repetir el proyectil más eficiente sea suficiente para casi todos los encuentros.
5. Que cambiar de personaje y cargar especiales añada botones al combate, pero no decisiones que alteren el resultado.

Estos cinco puntos son hipótesis de diseño derivadas de la arquitectura y los datos actuales; no se declaran defectos confirmados en partida hasta probarlos.

#### Referencias de sensación e identidad propia

La dirección buscada combina la lectura y variedad de hechizos en tiempo real que atraen de *Hogwarts Legacy* con el ritmo, la movilidad, los relevos y la espectacularidad de *Kingdom Hearts*. Son referencias de sensación, no una plantilla para copiar sistemas, controles ni contenido.

El laboratorio debe averiguar qué combinación de decisiones produce una identidad reconocible para *El Sendero de las Estrellas*. No se da por hecho que exista una mecánica única que lo diferencie, ni que haya que inventar una novedad por obligación. Se buscará si las interacciones entre magia, compañeros, enemigos y espacio crean momentos propios que se entienden mientras se juega. Cada prototipo se compara primero con la base actual; la originalidad se evalúa después por lo que el jugador puede hacer y decide, no por el nombre llamativo de la mecánica.

#### Banco de experimentos — ideas, no decisiones

Cada idea se prototipa por separado y se compara con el combate actual. El banco no presupone que haya que implementarlas todas. Las combinaciones se prueban solo después de seleccionar ideas que funcionen individualmente.

| ID | Área | Opción a probar | Pregunta que debe contestar el prototipo |
|---|---|---|---|
| CMB-01 | Sensación | Más respuesta al impacto: hit stop corto, reacción corporal, sonido y VFX que confirmen el acierto. | ¿Cada hechizo se siente contundente sin frenar el ritmo? |
| CMB-02 | Magia | Hechizos con efectos de control diferentes: interrumpir, desplazar, elevar, marcar o crear zona. | ¿El jugador cambia de hechizo por la situación y no solo por el número de daño? |
| CMB-03 | Magia | Interacciones de dos hechizos/estados (p. ej. elevar y rematar, agrupar y golpear, marcar y activar). | ¿Las combinaciones surgen de forma intuitiva sin memorizar recetas ni añadir una tabla elemental? |
| CMB-04 | Magia / defensa | Desviar, capturar o transformar un proyectil enemigo usando magia o escudo. | ¿Transformar la amenaza es más divertido que limitarse a bloquearla o esquivarla? |
| CMB-05 | Puntería | Comparar disparo rápido, apuntado preciso y carga/soltado de hechizo. | ¿El modo de apuntar añade una decisión útil sin volver torpe el control? |
| CMB-06 | Entorno | Levantar y lanzar objetos o usar elementos de la arena contra enemigos. | ¿El escenario añade soluciones expresivas o distrae del combate principal? |
| CMB-07 | Personajes | Cambiar de personaje durante una acción y continuar una secuencia con su hechizo. | ¿El cambio se siente fluido, legible y suficientemente distinto para justificarlo? |
| CMB-08 | Cooperación | Compañero IA que remata una apertura vs. especial conjunto activado manualmente. | ¿Qué grado de autonomía mantiene al jugador al mando y hace que el grupo importe? |
| CMB-09 | IA | Ataques enemigos con anticipación y recuperación claramente visibles. | ¿Mejora la lectura sin hacer el combate lento o demasiado predecible? |
| CMB-10 | IA | Enemigos con roles distintos: perseguidor, tirador, guardián, controlador o apoyo. | ¿Se reconoce qué hace cada enemigo y cambia la prioridad de objetivos? |
| CMB-11 | IA grupal | Turnos de ataque, flanqueos y presión limitada cuando hay varios enemigos. | ¿El jugador recibe presión interesante sin sufrir ataques simultáneos inevitables? |
| CMB-12 | IA reactiva | Enemigos que responden a repetir una misma táctica, con límites claros y sin contrarrestar todo. | ¿La adaptación invita a variar o se percibe como trampa/injusticia? |
| CMB-13 | Recursos | Comparar maná regenerativo, coste del escudo sostenido y recompensa por defensa puntual. | ¿El maná anima a tomar decisiones o fuerza a esperar y protegerse? |
| CMB-14 | Jefes | Puntos débiles, partes/runa expuestas, patrones que cambian el espacio y fases con reglas distintas. | ¿El jefe pide aprender y ejecutar algo diferente sin convertirse en una barra larga? |

#### Escena común de pruebas: CombatLab

Construir una escena de test independiente y reutilizable, no un nivel narrativo. Debe permitir cambiar entre variantes sin retocar MainWorld ni los niveles de historia. La escena tendrá un estado base para comparar y estaciones activables para probar una sola idea cada vez:

1. **Hechizos y objetivos:** blancos quietos, móviles y con defensa; maná/cooldowns reiniciables.
2. **Duelo:** un enemigo humanoide y un enemigo de avance directo, con perfiles de IA intercambiables.
3. **Grupo:** dos o tres enemigos para observar coordinación, presión y cambio de objetivo.
4. **Entorno:** cobertura, desnivel, objeto levantable y obstáculo reutilizable para desvío/impacto.
5. **Jefe:** arena pequeña con el Gólem o Demonio, vida/fase reiniciable y punto débil visible.
6. **Pareja:** Will, Liam y Estela disponibles para swap, seguimiento IA y especiales.

Cada estación debe reiniciar vida, maná, cooldowns, fase del jefe, objetivos y posición; mostrar qué variante está activa; y permitir repetir el mismo escenario con el sistema base y con un único cambio. No se juzga una mecánica combinada con otras seis novedades en la misma pasada.

**Montaje inicial del laboratorio (25 sep 2026):** el generador de Editor está en `Assets/Scripts/Editor/CombatLabBuilder.cs`; crea `Assets/Scenes/Test/CombatLab.unity`, una arena aislada con blanco de práctica, coberturas y NavMesh. El suelo usa la layer `Floor`, y el generador conserva esa asignación al recrear la escena. Will y el preset activo se cargan desde `_WILL.prefab`. Estela y Liam se instancian al iniciar desde los prefabs reales, referenciados por `Resources/CombatLab/CombatLabConfig.asset`, fuera de la progresión normal. El montaje evita `WorldBootstrap` para no poblar el laboratorio con el roster de NPCs del mundo. Las opciones de grupo son temporales y deben restaurar los IDs de la partida al cambiar de composición o salir; no se desbloquean personajes en la campaña. La entrada de Build Settings y los materiales generados son temporales y se revisarán en la limpieza final.

**Prueba de estaciones (25 sep 2026):** el HUD del laboratorio presenta botones para Base, Duelo, Grupo, Jefe y Reiniciar; estos controles permiten probar aunque el foco de teclado no llegue a las teclas de función. Verificado en Play Mode: Grupo activa y muestra Arañas y Demonio; Jefe muestra al Gólem; Reiniciar vuelve a Base y reaplica el preset; en Duelo la Araña se acerca al jugador. El bootstrap conserva el AudioListener de Will y desactiva los demás listeners del laboratorio, evitando la advertencia de listeners duplicados que provocaban algunos prefabs. **Límites y siguiente ajuste:** no se han evaluado hechizos, ventanas de ataque, defensa, NavMesh de todos los arquetipos ni coordinación de grupo; el Gólem queda demasiado cerca y ocupa casi toda la cámara, así que hay que ajustar su encuadre antes de juzgar el experimento CMB-14. La exploración CMB-01–14 sigue pendiente, sin aprobaciones ni descartes.

**Carga de magia, habilidades y grupo en CombatLab (25 sep 2026):** tras aplicar el preset, el bootstrap habilita Magia y Escudo en el `PlayerActionManager` de la instancia de Will; de lo contrario el preset puede bloquear los ataques. En el panel se puede alternar Magia, Salto y Vuelo durante la prueba. Ninguno de esos cambios modifica desbloqueos, habilidades guardadas ni la partida. El panel modal equipa hechizos compatibles desde `SpellLibrary` en las ranuras Izquierda, Derecha y Especial; también rellena maná/carga y reinicia cooldowns. Si el preset deja el pool en 0/0 porque Magia aún no está desbloqueada en la campaña, CombatLab crea para esta instancia una reserva temporal llena de al menos 50 puntos (o el doble del coste del hechizo equipado más caro); el panel muestra además la cantidad actual/máxima. Esto solo modifica el componente en memoria de Will durante la prueba. Mientras el panel está abierto, el input de juego se suspende: hay que pulsar «Cerrar panel y probar habilidades» para jugar. El propio panel recuerda los controles: Espacio salta y, al pulsarlo de nuevo en el aire, activa o desactiva el vuelo; clic izquierdo/derecho lanza hechizos y Q usa el especial. Al cerrar el panel, su resumen muestra si detectó la última pulsación mágica y si el slot estaba listo o qué bloqueo encontró; en Editor también deja ese resultado en la consola. `M` o clic central en el resumen de carga deberían reabrirlo; falta verificar esos atajos en Play Mode. CombatLab habilita durante la sesión el `SceneBoundUI` oficial `PlayerHUD_UI`, que normalmente se oculta en escenas de prueba; así se ve la barra de maná real mientras se lanzan hechizos y se espera la regeneración, sin duplicar el HUD ni cambiar la lista persistente de escenas permitidas. El panel conserva el botón para recargar maná/carga entre pruebas. El panel Party se añade al arrancar la escena existente e instancia prefabs configurados por `Resources/CombatLab/CombatLabConfig.asset`, así que no necesita regenerar la escena binaria. En la arena se puede elegir Will solo, Will+Estela, Will+Liam o los tres. Los compañeros usan seguimiento y lógica de combate existentes. Cada unión de prueba pasa por el party real, pero restaura los IDs guardados del preset; hay que verificar en ejecución que se incorporen y ataquen, y que el jugador pueda volver a Solo sin dejar datos de laboratorio en la partida. El reparto final de controles de magia, especiales y habilidades sigue pendiente de validación con teclado y mando. La opción Solo informa que Will combate sin compañeros y no muestra un estado de preparación.

#### Auditoría de reutilización antes de prototipar

La inspección de código y assets encuentra estas piezas reutilizables. Esta tabla evita reescribir lo que ya existe y separa hechos de código de resultados todavía no comprobados jugando:

| Candidato | Ya existe y puede servir de base | Qué debe comprobar o prototipar CombatLab | Estado de auditoría |
|---|---|---|---|
| CMB-01 | Daño, animaciones de reacción y servicio central de feedback/VFX; `NPCCombatLifecycleHandler` ya combina animación de impacto, sacudida de cámara y hit-stop. | En la estación Base, repetir el mismo hechizo contra el mismo blanco y distancia. Tomar la respuesta actual como referencia; después cambiar una sola variable por pasada (hit-stop, reacción o sacudida), y comparar finalmente un perfil diferenciado por familia de hechizo. Mantener daño, cadencia y coste sin cambios para no confundir el resultado. Registrar si el golpe se lee, se siente contundente y permite continuar controlando a Will; no aceptar el perfil más intenso si entorpece apuntado o encadena pausas. | Sistemas identificados y protocolo aislado; prueba jugable de variantes pendiente. |
| CMB-02 | Proyectiles, zonas, levitación, empuje/atracción y escudo. | Comprobar si cada hechizo cambia la situación y si el efecto se entiende con varios enemigos. | Capacidades identificadas; roles redundantes posibles, pendientes de medir. |
| CMB-03 | Levitación y algunos efectos de área permiten preparar posiciones y remates. | Probar pares de interacciones, uno por uno; el proyecto no muestra una matriz global que se deba ampliar sin prueba. | Interacciones candidatas, sin sistema nuevo aprobado. |
| CMB-04 | Escudo sostenido y proyectiles enemigos. | Ver si una defensa puntual puede devolver, capturar o alterar una amenaza con una ventana legible. | El reflejo universal no está confirmado en los sistemas revisados. |
| CMB-05 | `PlayerPreciseAimController` y modo preciso ya soportado por Bola de Fuego. | Comparar toque normal, mantener para precisión y carga, con el mismo blanco y distancia. | Base funcional existente; balance/claridad pendientes de juego. |
| CMB-06 | Levitación puede mover y lanzar objetivos; el mundo tiene objetos con reacciones elementales. | Ver qué props existentes pueden entrar en combate y si usarlos resulta útil sin preparar una arena especial. | Reutilización probable; no asumir que cualquier objeto admite interacción. |
| CMB-07 | `PartyControlManager`, cambio de personaje activo y prefabs de Liam/Estela. | Comparar Solo / +Estela / +Liam / ambos con el mismo enemigo; después medir el relevo durante acción y diferencia de kits. | El montaje de compañeros está en progreso; join/NavMesh/combate y swap requieren validación en Play Mode. |
| CMB-08 | `DuoSpecialAttackSystem` y carga de especiales de Estela/Liam. | Comparar ayuda automática del compañero con especial conjunto activado por el jugador en cada composición. | Sistema existente; autonomía y control pendientes de juego. |
| CMB-09 | `NPCCombatBrain` y `ImpDemonAI` tienen estados, avisos y recuperaciones; el Demonio avisa varios ataques. | Comparar cuánto se leen los avisos y cuánto dura la recuperación frente al ritmo de ataque. | Diferencias entre enemigos confirmadas en código; justicia/ritmo pendientes de juego. |
| CMB-10 | Prefabs de Araña, Demonio, Demonio 2 y Gólem, además de la FSM táctica humanoide. | Comparar roles y amenazas; retirar o diferenciar los enemigos que no generen decisiones distintas. | Variedad existente identificada; arquetipos efectivos pendientes de jugar. |
| CMB-11 | `NPCAttackCoordinator` limita reservas de ataque simultáneas. | Probar la presión real de 2–3 rivales, flanqueo y seguridad de las ventanas de acción. | Coordinador existente; utilidad y límites pendientes de jugar. |
| CMB-12 | La FSM incluye reposicionamiento, retirada táctica, búsqueda y ajuste de frecuencia/dificultad. | Comprobar si responde a tácticas repetidas sin cancelar toda respuesta del jugador. | Adaptación específica no confirmada en los sistemas revisados. |
| CMB-13 | `ManaPool` regenera; el escudo y la levitación pueden consumir maná. | Medir espera y gasto en secuencias comparables; probar defensa puntual frente a escudo sostenido. | Base existente; economía y riesgo de espera pendientes de jugar. |
| CMB-14 | Demonio con fases y collar rúnico de punto débil; Gólem con ataques de área y fase. | Comparar punto débil, cambio de patrón y uso del espacio en un mismo jefe. | Dos bases identificadas; profundidad de encuentro pendiente de jugar. |

**Primera pasada construible:** la escena permite comparar base, duelo, grupo de enemigos, jefe y cuatro composiciones de party con los mismos encuentros. El cambio de personaje y los especiales siguen siendo pruebas separadas, porque no basta con que la etiqueta del grupo cambie: los compañeros deben unirse a la IA de combate y el intercambio debe funcionar. Cada variante nueva vive en archivos propios de CombatLab o bajo una bandera de experimento, y su ficha registra qué se añadió para poder retirarlo si se descarta.

#### Cómo evaluar y decidir

Después de cada experimento, registrar una ficha corta: ID, variante probada, qué se cambió, qué pasó, qué confundió, mejor momento, peor momento y decisión **conservar / ajustar / descartar**. Puntuar de 1 a 5: diversión, claridad, sensación de control, impacto mágico, variedad y ganas de volver a usarlo. Añadir una observación libre del jugador; las cifras ayudan a comparar, pero no deciden solas.

**Primera pasada:** implementar y probar cada candidato por separado en CombatLab; no integrarlo aún en la campaña. En esta pasada se mide si la acción es satisfactoria, legible y controlable, sin exigir que cada candidato sea novedoso por sí solo. **Segunda pasada:** seleccionar las ideas con mejor resultado y probarlas juntas para detectar conflictos de controles, ritmo, balance o legibilidad, y comprobar si su interacción produce una identidad propia frente a las referencias. **Tercera pasada:** llevar únicamente la combinación elegida a un encuentro real del juego y comprobar que encaja con la historia, el tono, el coste de producción y las habilidades disponibles.

Una idea se descarta si solo funciona cuando se explica, si domina a todas las otras opciones, si hace perder control de cámara/personaje, si necesita demasiado arte nuevo para comunicar lo que hace o si la IA la ejecuta mejor que el jugador. Una idea se conserva si se entiende durante la acción, produce una decisión voluntaria, crea una reacción vistosa y deja más de una respuesta viable.

#### Papel de las habilidades en los experimentos

Las asignaciones siguientes son hipótesis que deben entrar al banco de pruebas, no funciones aprobadas:

| Hechizo / familia | Hipótesis funcional | Candidato relacionado |
|---|---|---|
| Bola de Fuego | Proyectil rápido; modo preciso contra blancos/puntos débiles. | CMB-01, CMB-05 |
| Bola Prisma | Interrupción o ruptura de guardia durante una preparación visible. | CMB-02, CMB-09 |
| Levitation | Mover/enlazar enemigo u objeto y aprovechar altura/posición. | CMB-02, CMB-03, CMB-06 |
| Tornado / Aura Estelar | Desplazar, agrupar, proteger o interactuar con proyectiles; probar para que no sean redundantes. | CMB-02, CMB-03, CMB-04 |
| Garra / Sello del Pacto | Marcar, interrumpir o preparar una zona/trampa para el siguiente golpe. | CMB-02, CMB-03 |
| Huracán | Impacto de área de coste alto contra grupo/apertura. | CMB-01, CMB-03 |
| Corazón Estelar | Cooperación/energía compartida según canon; probar por separado su uso jugable frente al asset ofensivo actual. | CMB-08 |
| Especiales de Liam y Estela | Ataques de relevo o remate manual. | CMB-07, CMB-08 |
| Hechizo del Tiempo | Alterar/repetir un instante con coste, si puede implementarse sin borrar consecuencias ni trivializar errores. | CMB-04, CMB-14 |

Los hechizos actuales tienen distintos daños, costes, cadencias, zonas y control, pero no se reajustan sus valores hasta que CombatLab permita comparar impactos y frecuencia con datos consistentes.

## Fichas de Personajes

### Vista general del reparto

| Personaje | Función narrativa | Función jugable | Arco |
|---|---|---|---|
| Will | Protagonista y reencarnación del mago que detuvo al Mago Oscuro | Magia de fuego/luz, combate adaptable y decisiones de equipo | Aprende a actuar por convicción propia y a no confundir bondad con sacrificarse siempre |
| Liam | Amigo y mago de pacto que manipuló los ataques para llegar al Sendero | Invocación, trampas, lectura de patrones y apoyo táctico | Reconoce que la desesperación no le daba derecho a decidir por Will; se redime asumiendo sus actos |
| Estela | Hechicera prodigio y compañera | Daño de área y control de grupos | Aprende a contenerse sin negar su duelo por Mara y a reparar el daño que causa |
| Eldran | Mentor y protector de Will | Guía, misiones iniciales y punto de apoyo | Su sobreprotección nace en parte del duelo por Selene; debe dejar que Will elija |
| Silas | Antiguo amigo de Eldran y guía hacia la Piedra | Encargo del reloj y enseñanza del hechizo temporal | Reabre su vínculo con Eldran y entrega al grupo reglas, no una solución milagrosa |
| Mago Oscuro | Antagonista ligado al Sendero por su deseo de poder absoluto | Jefe final que altera el espacio y exige cooperación | Es derrotado al cortar el vínculo entre su deseo y el altar |

### Will

Will es un joven de buen corazón, pero el juego no debe convertirlo en un héroe que acepta cualquier daño sin preguntarse qué quiere. Su conflicto abarca el miedo a fallar, la revelación de su vida anterior y el aprendizaje de elegir por sí mismo. Su bondad se expresa al escuchar, pedir consentimiento y ofrecer ayuda, no en renunciar automáticamente a su propia vida.

En combate comienza con magia de fuego y desarrolla herramientas de luz y tiempo. El Hechizo del Tiempo permite retroceder un intervalo corto, pero conserva el coste físico y mágico. El Hechizo de Resurrección requiere entregar voluntariamente una vida a cambio de otra recién perdida. Son reglas narrativas centrales y deben ser coherentes con los sistemas de juego.

### Liam

Liam ama a su hermano Tobías y teme perderlo. Esa desesperación explica su conducta, pero no la absuelve: organizó los ataques y utilizó a Will sin contarle la verdad. La prueba de la Caja le obliga a reconocer que esas decisiones fueron suyas, sin culpar a una sombra ni a su necesidad de curar a Tobías.

Liam posee magia propia, centrada en pactos, invocación y trampas. El límite narrativo que lo lleva a buscar a Will es que no puede abrir el Sendero por sí solo, no que carezca de magia. Tras la revelación, su redención es gradual: responde preguntas, acepta límites y deja de decidir por el grupo.

### Estela

Estela es una hechicera prodigiosa, impulsiva y franca. Su humor y su hambre alivian la tensión, pero no sustituyen su historia de duelo. La muerte de su hermana Mara sigue afectándola, en especial ante el azúcar caliente y la criatura de gominola de Chuchelandia.

Su poder de área debe sentirse fuerte sin presentar el descontrol como algo inocuo. En la ruptura hiere accidentalmente a Will; después se responsabiliza del daño y decide no huir. El arco no exige que deje de estar triste, sino que pueda recordar a Mara sin reducir su vida al día en que murió.

### Eldran

Eldran cuidó de Will y lo protegió durante años. Su cautela se relaciona con la pérdida de Selene y con la creencia de que más conocimientos le habrían permitido salvarla. Quiere evitar que Will sufra, pero esa intención puede limitar la capacidad de Will para elegir. Su relación con Silas da contexto a su juventud y a la persona que era antes de cerrarse al mundo.

En juego es mentor inicial y apoyo narrativo. Si actúa como guía o punto seguro, no debe resolver las decisiones que corresponden al jugador.

### Silas

Silas es un hechicero, antiguo amigo de Eldran y dueño de una colección de relojes que no miden el tiempo de la misma manera. Ayuda al grupo tras recuperar su reloj en un bucle temporal. Enseña el ritual de la Piedra y explica el coste de la magia temporal.

### Mago Oscuro

El Mago Oscuro es el mago que pidió un poder al que nadie pudiera oponerse. Su deseo desató la Marcha de Conquista. El choque con la Protección Absoluta del mago del valle lo dejó ligado al Sendero; no es un archienemigo que simplemente vuelva a la vida en un cuerpo físico. Su fuerza final depende del vínculo entre su deseo corrompido y el altar.

La denominación «Mago del Cataclismo» que aparece en perfiles antiguos se retira para evitar que parezca un personaje distinto. Usar «Mago Oscuro» en guion, localización y documentación, salvo que se defina otro nombre de forma expresa.

## Estado de Balance de Hechizos

Los valores de esta tabla se han vuelto a leer directamente de los MagicSpellSO actuales en Assets/_SPELLS, el 25 de septiembre de 2026. La lista describe configuración de assets, no balance aprobado ni quién equipa cada hechizo en cada capítulo.

| Hechizo | ID | Elemento | Daño | Maná | Recarga | Slot | Observaciones |
|---|---:|---|---:|---:|---:|---|---|
| Bola de Fuego | 1 | Fuego | 10 | 5 | 0,5 s | Cualquiera | Velocidad inicial 20 |
| Llama Astral | 1 | Fuego | 10 | 5 | 0,5 s | Cualquiera | Comparte ID con Bola de Fuego; velocidad inicial 10 |
| Bola Prisma | 2 | Tormenta | 25 | 10 | 1 s | Cualquiera | — |
| Corazón Estelar | 3 | Luz | 100 | 25 | 3 s | Especial | En la novela también expresa un vínculo de energía compartida y voluntaria; ese uso cooperativo no queda demostrado por las stats del asset. |
| Levitation | 4 | Mental | 10 | 5 | 0,5 s | Cualquiera | Vida del proyectil 8 s; herramienta de movimiento/control |
| Aura Estelar | 5 | Tormenta | 30 | 10 | 1 s | Cualquiera | — |
| Tornado | 6 | Tormenta | 30 | 5 | 1 s | Cualquiera | Clave de audio actual: Tornado |
| Garra del Pacto | 7 | Mental | 35 | 15 | 1,2 s | Cualquiera | — |
| Huracán | 8 | Tormenta | 55 | 20 | 3 s | Cualquiera | — |
| Sello del Pacto | 9 | Mental | 12 | 25 | 7 s | Especial | — |
| Golpe del Mago Oscuro | 10 | Oscuridad | 25 | 20 | 2,2 s | Cualquiera | — |
| Grieta del Mago Oscuro | 11 | Oscuridad | 15 | 30 | 8 s | Cualquiera | — |

**Puntos de diseño por resolver:**

- Bola de Fuego y Llama Astral mantienen el mismo ID numérico (1) y stats similares. Confirmar si son dos variantes que deben coexistir; si lo son, asignarles IDs diferenciados antes de depender de ese ID en inventario, guardado o desbloqueos.
- Los assets de Tornado y Huracán ya tienen claves de audio propias. Se retira el aviso anterior de que Tornado heredaba la clave LlamaAstral.
- Hay hechizos de Will, Liam y del Mago Oscuro en la misma carpeta. Esta tabla no les asigna dueño por sí sola; vincular cada uno con personaje, desbloqueo y escena cuando esté confirmado en las escenas activas.

### Disponibilidad narrativa de hechizos

Los desbloqueos que describía la tabla anterior se dedujeron de MainNarrative_Cap1 a Cap6. Como Cap2–Cap6 están ahora archivados en Versiones antiguas, esa tabla no es prueba de la progresión vigente. Los assets actuales confirman que existen Bola de Fuego, Bola Prisma, Corazón Estelar y otros hechizos; no confirman en qué misión se desbloquean. Conservar como hipótesis de trabajo la progresión Bola de Fuego → Bola Prisma → Corazón Estelar, pendiente de contrastarla con el grafo y las quests activas.

## Estado de Diálogos

El estado distingue entre **cadena localizada** y **secuencia conectada/revisada en juego**. La presencia de una clave en JSON no demuestra que el juego la llame, que el diálogo esté aprobado o que quepa en su interfaz.

| Tramo / claves | Texto disponible | Alineación con canon | Estado en juego / acción |
|---|---|---|---|
| Prólogo y casa | Claves de prologue, cinemáticas y diálogos iniciales | La leyenda, el sueño y el despertar siguen la biografía del Mago Oscuro y del Will original | SEQ_Prologo_UltimaNoche y sus señales conectadas a Cap1 se confirmaron en assets; falta revisar la caja en ejecución |
| Preparación–Reino | Diálogos de quests, cinemáticas y escenas iniciales | Adaptar las tareas del tutorial y el incidente del primer Demonio al orden de la novela | Revisar en el juego; no usar los antiguos Cap2–Cap6 como estado actual |
| Piedra / apertura | PIEDRA_ANCESTRAL_01–08, APERTURA_SENDERO_01–03 | Texto localizado; cotejar fórmulas, coste y partida del grupo con novela XVIII–XIX | Conexión y presentación visual por verificar |
| Prueba de Will | PRUEBA_WILL_01–04 | Líneas cortas presentes; mantener el cambio de humor a miedo | Secuencia y voces por verificar |
| Chuchelandia | DLG_CANDYLAND_01–17 | El bloque no narra por sí solo el arco de Mara, la gominola y la libertad de sentir | Completar/contrastar antes de cerrar el nivel |
| Feria / Caja | PARQUE_LIAM_01–12 | Incluye la pausa y la pregunta de la Voz; no incluye por sí sola toda la prueba de Liam | PARQUE_LIAM_10 supera tres frases; dividir en dos intervenciones localizadas |
| Ruptura / reencuentro | RUPTURA_* y REUNION_* | Revisar contra capítulos XXIV–XXV: Will se enfada, Estela asume el daño y el perdón no es instantáneo | Varias líneas están resumidas respecto de la novela; no marcar como finales hasta reescribir |
| Verdad / sacrificio / epílogo | MAGOOSCURO_MONOLOGUE, WILL_FLASHBACK_REVELATION, VOICE_VISION_*, claves de final y EPILOGO_* | Las localizaciones conservan el monólogo heredado y todavía no siguen el orden de la revelación, la muerte de Liam, el deseo por Tobías y la resurrección | Reescribir y reordenar antes de conectar la secuencia final |

### Hallazgos de lectura del texto localizado

- MAGOOSCURO_MONOLOGUE tiene ocho frases y unos 800 caracteres: no funciona como un único turno. Reescribirlo como intercambio con pausas, recuerdos y control jugable; cada bocadillo/subtítulo debe tener como máximo tres frases.
- PARQUE_LIAM_10 tiene cinco frases y pide dos turnos completos. PARQUE_LIAM_08 y REUNION_WILL_01 reúnen más de tres ideas/frases; volver a puntuar y separar donde la escena permita una respiración natural.
- EVT_REINOEXIT_ESTELA_01, EVT_REINOEXIT_WILL_01, EVT_ESTELA_DRAMATIC, OLIVER_GREETING_BEFORE_MENUS_HANDOFF, VOICE_VISION_REASSURANCE y WILL_FLASHBACK_REVELATION también acumulan más de tres frases o ideas en un turno. Dividirlos en beats con sentido. Revisar en especial que la actuación de Estela en el bosque no se convierta en una página entera de parodia antes de que el jugador pueda responder.
- PIEDRA_ANCESTRAL_05 agrupa varios fragmentos cortos en una sola intervención; comprobar el ritmo y no dejar una palabra suelta en el cambio de página. VOICE_VISION_EXPLANATION no excede tres frases gramaticales, pero ronda los 315 caracteres y debe dividirse o condensarse.
- DLG_CANDYLAND_09 es una respuesta larga aunque tenga pocas frases; revisar su extensión en la caja real. La cantidad de frases no sustituye la prueba de lectura.
- Los puntos suspensivos pueden hacer que un contador automático detecte frases que no existen. La decisión final se toma leyendo el ritmo y comprobando la caja real.

**Regla de revisión:** mostrar el texto en la caja real, en español e inglés, con el tamaño de fuente y resolución objetivo. Si la última página queda con una palabra o un fragmento, reescribir o dividir el turno en un punto de respiración natural. No declarar esa revisión hecha hasta ver el render dentro del juego.

**Incidencias de contenido:** los literales español e inglés del prólogo hasta la caja de manzanas se revisan en `Assets/Resources/Localization`; la adaptación de juego no cambia la novela. Seguimiento en `INC-455` de `TRACKER.md`.

## Registro de Cambios

**25 de septiembre de 2026 — Codex, revisión de diálogos iniciales solicitada por Raúl.**

Se revisan las voces en español e inglés desde la última noche del Archimago hasta el encargo de la caja de manzanas, con frases naturales y turnos breves. Se retira el texto del relato histórico antiguo; `PRLG_WILL_WAKE_UP` se conserva. La plegaria mantiene su referencia de desesperación, y «No quiero perder a Liora» expresa el temor del Archimago a perderla sin sugerir que ya se había marchado. En el despertar, Will oye jaleo desde la ventana y sale a comprobar qué pasa; se quitan las referencias a la carta de Eldran. La novela no cambia: es una adaptación del tutorial del juego. Las señales heredadas de `SplashScreen` aún apuntan a las claves antiguas, por lo que estas conservan traducción vacía hasta poder retirar esas llamadas desde Unity Editor. Seguimiento: `INC-455`.

**26 de septiembre de 2026 — Codex, revisión de la discusión y el regalo de Eldran solicitada por Raúl.**

La discusión de Eldran y Victoria ahora tiene una causa clara —solo queda una caja y Eldran teme que se magullen las peras— y se cierra con una concesión ligera. Eldran saluda a Will sin hablar de sí mismo en tercera persona. Oliver ofrece ayudar con las cajas, en lugar de anunciar que va con Will cuando ya está a su lado. Tras darle la moneda por adelantado, Eldran responde a la pregunta de Will explicándole que quiere que tenga algo para él, aunque sea una tontería; Will acepta y se dispone a buscar algo. En la despedida del prólogo, el Archimago admite que no sabe quién es el Mago y le pide a Liora que evacue a todos mientras él se queda a enfrentarlo. Se restaura la plegaria «Quiero volver con Liora» y el hechizo culmina con «¡Protégelos...! ¡A todos!». Seguimiento: `INC-455`.

**25 de septiembre de 2026 — Codex, banco de experimentos de combate solicitado por Raúl.**

Se retiró la Reescritura del Hilo como mecánica protagonista elegida y se convirtió en una de varias ideas candidatas. El GDD propone catorce experimentos independientes, fija Hogwarts Legacy y Kingdom Hearts como referencias de sensación, y deja que la identidad propia emerja al probar interacciones en una segunda pasada. Se añadió la auditoría de sistemas reutilizables, el diseño de CombatLab y criterios para conservar o retirar prototipos con evidencia. La escena compila y arranca; se verificaron los botones de estación, el reinicio, la activación de encuentros y el seguimiento de Will por la Araña. Se agregaron controles visibles de HUD y se desactivaron listeners sobrantes en la escena de prueba. El encuadre del Gólem y las pruebas CMB-01–14 quedan pendientes. Los archivos y la entrada temporal de Build Settings siguen anotados para su limpieza cuando se elija el modo final.

**25 de septiembre de 2026 — Codex, dirección de rediseño de combate solicitada por Raúl.**

Se auditó estáticamente `NPCCombatBrain`, las IA del Demonio/Gólem, la defensa del jugador, la configuración de hechizos, `Damageable` y los ataques especiales conjuntos. Se añadió al GDD una dirección para que el combate gire en torno a leer señales, provocar aperturas y combinar efectos mágicos; incluye arquetipos enemigos, papeles propuestos para los hechizos y una secuencia pequeña de prototipos. La auditoría separa observaciones del código de hipótesis de sensación que requieren jugarse. No se modificó el comportamiento de combate ni se declararon balanceados los valores actuales.

**25 de septiembre de 2026 — Codex, ajuste de identidad de combate tras referencias aportadas por Raúl.**

Se alineó la meta de sensación con acción mágica fluida y espectacular inspirada por *Hogwarts Legacy* y *Kingdom Hearts*, sin copiarlos. El contraste documental confirmó que las combinaciones de hechizos, los cambios de estado y los ataques de grupo ya existen en referentes publicados; por eso no se presentan como la novedad del juego. Se sustituyó la mecánica candidata de encadenar marcas por la «Reescritura del Hilo»: alterar trayectoria, objetivo o zona de un ataque enemigo y usar esa intervención como inicio de un relevo mágico entre personajes. Se añadieron ejemplos con el Gólem y el Mago Oscuro y se cambió el plan de prototipo para validar primero un ataque reescribible. Sigue siendo una hipótesis de diseño, no una afirmación de novedad de mercado ni una implementación.

**25 de septiembre de 2026 — Codex, refactorización de gameplay posterior a la Caja solicitada por Raúl.**

Se tomó la Caja (novela XXIII) como corte de la base jugable existente y se diseñó el recorrido desde la ruptura hasta el epílogo como niveles y actividades concretas. Se añadieron propuestas para el tramo de Will en solitario, el reencuentro y rescate de Liam, la biblioteca de deseos, el jefe por fases, la rotura del vínculo, el colapso y el cierre. Se revisaron las misiones laterales observadas en los assets, identificando recados repetidos y la cadena de veinte pasos de arañas, y se anotó la clasificación actual de Niño Pez y Rudolfo. También se asignaron funciones de diseño a las habilidades y se añadieron criterios de variedad y ritmo alrededor del cambio de personaje y los especiales conjuntos existentes. Son propuestas para GDD, no cambios de assets ni confirmación de niveles implementados.

**25 de septiembre de 2026 — Codex, actualización integral solicitada por Raúl.**

Se actualizó el prólogo jugable y el tramo inicial hasta la caja contra Cap1.asset y las secuencias actuales. Se rehízo el guion posterior al Demonio según la novela completa, incorporando Silas, el bucle del reloj, Risco y Vega, las reglas de la Piedra, el orden de las pruebas, la Caja y la secuencia actual del final. Se ajustaron las fichas al canon, se volvió a leer el inventario de hechizos y se retiraron como estado vigente las conclusiones basadas en Cap2–Cap6, que hoy están en Versiones antiguas. Se documentaron la pauta de tres frases por bocadillo y los literales que requieren división o reescritura. Este cambio actualiza el GDD; no reescribe los JSON ni confirma la integración del contenido posterior al Capítulo 1.

**Nota histórica:** las entradas anteriores conservan el registro de decisiones de su fecha. Sus afirmaciones de implementación no prevalecen sobre el estado actual descrito arriba.


**10 de septiembre de 2026 — Codex, nivelación documental solicitada por Raúl.**

Actualizado el resumen de implementación para distinguir el alcance de la revisión del grafo de agosto de las escenas y herramientas existentes en septiembre. No se modifican el canon, los diálogos ni los estados de validación en juego. Seguimiento: INC-187 en `TRACKER.md`.

**1 de septiembre de 2026 — Claude (Cowork), a petición de Raúl ("nivela la novela con el GDD, revisa los diálogos del juego contra la novela, y completa el GDD con diálogos y gameplay de lo que falta").**

- **Nivelado "La Historia" (resumen del principio del documento) con "La Verdadera Historia de Will" y con la novela.** El resumen seguía con el planteamiento heredado del Google Doc v1.0 ("los dioses sellaron el Sendero... ocultaron sus secretos en un libro prohibido"), que nunca se corrigió cuando se escribió la versión detallada y ya correcta de más abajo (ni cuando la novela fijó el mismo canon: sin dioses, el Mago Oscuro queda sellado dentro del propio Sendero por el choque de su hechizo con la Protección Absoluta del mago del valle). Reescrito el resumen para que cuente la misma historia que el resto del documento. Ver `INC-152` en `TRACKER.md`.
- **Corregida la misma línea de la Voz en tres sitios que se habían quedado desalineados entre sí:** `GDD.md` (escena 20), `docs/guion-doblaje-elevenlabs-es.md` y las claves de localización `VOICE_VISION_PROTECT_THEM` en `cinematics_es.json`/`cinematics_en.json` seguían con la frase antigua ("Así podrás regresar con tu familia y volver a ser quien eras" / "Only then will you truly become who you were again"), ya sustituida en la novela el 30 ago 2026 por ser ambigua (Will no "vuelve" a nada, muere) — la corrección real es "Solo así demostrarás, de una vez por todas, quién eres de verdad" / "Only then will you truly prove, once and for all, who you really are". El juego (documentación + localización) no se había enterado de ese cambio. Ver `INC-153` en `TRACKER.md`.
- **Corregida una contradicción de lore real entre un diálogo ya implementado y el desenlace real de la historia:** `LORE_CELDA_7` (`dialogues_es.json`/`dialogues_en.json`, escena 11, el calabozo) decía que el Sendero concede "un deseo a cada uno" de los tres — pero el desenlace real (aquí mismo y en la novela/sinopsis, "Tres destinos rotos. Un único deseo.") es que solo Will pide un deseo, y lo pide para el hermano de Liam, no para sí mismo. Corregido a "un único deseo" / "a single wish" en ambos idiomas. Ver `INC-154` en `TRACKER.md`.
- **Prólogo del juego (`prologue_es.json`/`prologue_en.json`) reescrito** — tenía una leyenda fundacional distinta e incompatible con el canon ya fijado ("los dioses sellaron el Sendero"/"the gods sealed the knowledge of the Path"), probablemente una versión más antigua que nunca se actualizó cuando se desarrolló "La Verdadera Historia de Will". Reescrito en ambos idiomas para contar la versión real: un mago pide poder absoluto, emprende una Marcha de Conquista, y un mago sin nombre lo detiene a costa de su vida, sellándolo dentro del propio Sendero. Es la primera cinemática que ve cualquier jugador — con diferencia el hallazgo de mayor alcance de esta pasada. Ver `INC-152` en `TRACKER.md` (mismo incidente que el resumen de "La Historia", una sola causa raíz con dos síntomas).
- **Revisados de punta a punta los 6 archivos de diálogo/cinemáticas en español e inglés** (`dialogues_es/en.json`, `cinematics_es/en.json`, `prologue_es/en.json`, `other_es/en.json`) contra `biblia-del-universo.md` y la novela — el resto del contenido revisado (fichas de Liam sin capa, Eldran/Victoria sin relación romántica, Eldran conoce a Estela solo de oídas, el gag del estómago de Estela, la "eones sin amenaza" del Reino, etc.) ya estaba alineado, sin cambios necesarios.
- **Completadas con diálogo y gameplay las escenas 18, 19, 21 y 22**, que hasta ahora solo tenían un resumen narrativo sin líneas concretas ni detalle de mecánica (a diferencia de la 17 y la 20, ya desarrolladas). Añadido en cada una: tabla de diálogo ES/EN adaptado de la novela con claves de localización propuestas, y una subsección de "Gameplay (detalle propuesto)". Incluye una propuesta para el punto que quedaba abierto en `guion-tecnico-batalla-final-2026-08-30.md` sobre cómo se resuelve visualmente la derrota del Mago Oscuro (escena 21-A): se disuelve en las mismas partículas de luz del Sendero, y ese instante dispara el colapso, en vez de dos beats sueltos. **Todo lo añadido en esta pasada es propuesta de diseño, no canon confirmado — pendiente de que Raúl lo apruebe o lo ajuste antes de que otro hilo lo use para implementar.**

**24 de agosto de 2026 — Claude (Cowork), a petición de Raúl.**

- Migrado el contenido íntegro del GDD desde el Google Doc (v1.0) a este archivo, siguiendo el mismo criterio de "fuente de verdad única" que `TDD.md` ya aplica a lo técnico (ver nota al principio de este documento).
- Corregida la ficha de Liam: eliminada la descripción "sin magia innata" (contradicha por la corrección de lore ya confirmada por Raúl el 23 ago 2026 — Liam sí invoca demonios/gólem con magia propia; lo que le falta es un "corazón puro").
- Completada la tabla "Vista General del Reparto": faltaban Estela, Eldran y El Mago del Cataclismo (tenían ficha extendida pero no fila en la tabla).
- Añadida la sección "Estado de Balance de Hechizos" con los valores reales leídos de `Assets/_SPELLS/*.asset` en esta fecha, y dos avisos abiertos: el `SpellId` duplicado entre Bola de Fuego/Llama Astral, y el `castSFXKey` de Cycloneburst apuntando a LlamaAstral.
- Añadida la sección "Estado de Diálogos" (plantilla vacía + bugs conocidos ya documentados en TDD.md §8) — no existía ningún sitio que llevara este registro.
- Añadidas notas de estado de implementación en 3 escenas del guión técnico (Golem, Taberna, Reunión Estratégica) con evidencia directa del código/TDD.md; el resto de escenas quedan sin verificar explícitamente.
- El Google Doc original queda como referencia histórica (v1.0) — los cambios de diseño/narrativa a partir de ahora se hacen en este archivo.

**24 de agosto de 2026 (continuación, tras aviso de Raúl) — Claude (Cowork).**

- Raúl avisó de que el guión técnico estaba escrito contra el pipeline de cinemáticas antiguo (`DramaticTextNode`/`DramaticTextOverlayUI`, overlay de texto sobre fondo de "modo sueño", sin actores reales) y que ese sistema se ha sustituido por escenas reales (`XxxSequencer : CinematicSequencerBase`). Se añadió una nota general al principio del guión técnico explicando el cambio de pipeline, y se revisó `Assets/Scripts/Cinematics/` a fondo para corregir el guión escena por escena en vez de dejarlo como suposición.
- Escenas 1 (Prólogo) y 2 (La Casa de Will) fusionadas en la documentación: están implementadas como una sola secuencia real, `PrologueDreamSequencer.cs`, que dramatiza visualmente el enfrentamiento antiguo (no ya audio-only) y termina en el propio "Will, despierta". Se marcó como pendiente de confirmar si `MainNarrative.asset` ya dispara este sequencer o si la sustitución del `DramaticTextNode` viejo sigue sin cablear.
- Escena 4 confirmada contra `StarAwakeningSequencer.cs` (señales `AWAKEN_START`/`AWAKEN_DONE`/`AWAKEN_FAILED`); escena 5 marcada como probablemente cubierta por el mismo sequencer, con el aviso de que el "barrido de cámara hacia Liam" podría ser en realidad `LiamCrystalBallSequencer.cs`.
- Escena 7 confirmada contra `EstelaAppearsSequencer.cs`, con el beat a beat real (incluye contenido no presente en el guión original: los guerreros, el insulto "princefea").
- Escena 9 corregida: son dos sequencers separados, `TabernaSequencer.cs` (diálogo/minijuego) y `MountainSequencer.cs` (la bola de fuego que destruye la montaña), no uno solo.
- **Corregido un error de la primera pasada:** se había marcado la escena 13 (La Reunión Estratégica) como implementada por `ReinoExitBanterSequencer.cs` — no es correcto, ese sequencer es un beat distinto. Se quitó esa afirmación y se movió `ReinoExitBanterSequencer.cs` (junto con `LiamCrystalBallSequencer.cs`, que tampoco tenía mapeo claro) a una sección nueva al final del guión técnico, en vez de forzarlos dentro de la numeración sin confirmar.
- Mencionado `SimpleCinematicDirector.cs` como tercer sistema de cinemáticas (más antiguo, por lista de steps, catalogado como legacy en `TDD.md`), sin escena concreta asignada todavía.
- Quitada la "Descripción original del guión (v1.0)" que se había dejado como bloque de referencia histórica en las escenas 1-2 — a petición de Raúl, no se dejan bloques de contenido desactualizado en el documento, ni siquiera marcados como histórico.

**24 de agosto de 2026 (segunda continuación) — Claude (Cowork), resolviendo dudas contra el grafo narrativo real.**

- Raúl pidió no dejar las escenas 4/5 en "probablemente"/"sin confirmar" y resolverlo mirando el grafo narrativo (`Assets/NarrativeGraph/MainNarrative_Cap1.asset`) en vez de suponer. Confirmado con la cadena real de nodos:
  - El combate de la escena 5 (El Demonio) **es un `StartBattleNode` normal** (`battleId: Demon_1`), no una cinemática — resuelto, ya no es una suposición.
  - El "barrido de cámara hacia Liam observando" del guión original **es `LiamCrystalBallSequencer.cs`**, y se dispara justo después de ganar el combate (nodo "Los planes de Liam", señal `LIAM_CRYSTAL_START` → `LIAM_CRYSTAL_DONE`), antes de volver a casa con Eldran — no durante la escena 5. Se quitó de la sección de "sin mapeo claro" del final del guión porque ya tiene posición confirmada.
  - De paso se confirmó que el `DramaticTextNode` viejo ("Recuerdos prologo") **sigue activo en el grafo real de la escena 1-2**, en paralelo al arranque del Capítulo 1 — `PrologueDreamSequencer.cs` existe en el código pero **no está conectado todavía** al `MainNarrative.asset`. El nodo también reveló que la pesadilla del prólogo es literalmente eso narrativamente: Will piensa "Otra vez esa pesadilla" (`textId: NIGHTMARE_AGAIN`) nada más despertar.
- Raúl aportó un dato nuevo no visible en el grafo: al terminar el combate, Eldran lleva a Will a un punto de guardado. El grafo no modela puntos de guardado como nodo propio (son objetos de mundo, no hay un `SavePointNode` en `Graph/NodeTypes/`), así que esto no aparece explícito — el único nodo de esa zona es `CompleteQuestStepsNode` titulado "7.- ELDRAN SE VA A CASA DE WILL". Anotado como posible desajuste entre el título del nodo y el diseño actual, a confirmar con Raúl si hace falta renombrarlo en el Editor.

**24 de agosto de 2026 (tercera continuación) — Claude (Cowork), revisión completa del documento a petición de Raúl ("revisa todo el documento").**

Se leyeron enteros los 6 capítulos de `Assets/NarrativeGraph/MainNarrative_Cap1.asset` a `Cap6.asset` (antes solo se había revisado Cap1) y se corrigió/confirmó el guión técnico escena por escena contra la cadena real de nodos, no contra sequencers sueltos. Cambios:

- **Escena 6 (La Preparación del Héroe):** confirmadas 2 de las 3 tareas contra `Cap2` — la capa de Victoria y el entrenamiento con Erika (que desbloquea Bola Prisma directamente). La poción de vida no tiene nodo propio en el grafo, sin confirmar cómo se obtiene.
- **Escena 8 (El Golem):** confirmado el disparo real contra `Cap3` — el nodo se titula "LIAM AMENAZADO POR ESTELA", sugiriendo una confrontación directa entre ambos no descrita en el guión original. Confirmado también que el combate en sí es un `StartBattleNode` (`Golem_1`) aparte de la cinemática de invocación.
- **Escena 9 (Taberna/Montaña):** corregido un error propio de la pasada anterior — `MOUNTAIN_START`/`MOUNTAIN_DONE` no existen como nodos del grafo en ningún capítulo (se dijo que sí). Lo real es un `FocusCameraNode` con `focusId: MOUNTAIN_EXPLOSION_EVENT` al principio del Capítulo 4, desacoplado del patrón `WaitCustomEventNode`. Confirmado que el minijuego de huida es un `StartTagMinigameNode` real (`TAG_MINIGAME_01`).
- **Escenas 10-13 (Arresto, Calabozo, Emboscada, Reunión Estratégica):** confirmadas contra `Cap4`/`Cap5`/`Cap6` nodo por nodo. Hallazgos nuevos: hablar con el Rey en la escena 10 no termina en indulto — deriva directamente en "hay que escapar de la prisión" (escena 11, `Cap5`, "Escapar de la mazmorra"). La Emboscada (12) usa un segundo `battleId` (`Demon_2`, distinto del `Demon_1` de la escena 5) y el Rey premia la victoria enseñándoles el hechizo **Corazón Estelar** — recompensa no documentada en el guión original v1.0. La Reunión Estratégica (13) resultó ser diálogo normal gateado por finalización de quest, sin sequencer propio.
- **Escenas 14-15 (Fuego Fatuo / Hechicero amigo):** hallazgo importante — están **fusionadas en una sola misión implementada** ("Misión 14: Buscar al amigo de Eldran", tracking interno `FUEGOFATUO_1`). Confirmado (ya no es una suposición) que `ReinoExitBanterSequencer.cs` es la transición de salida del Reino de esta escena, seguida de un `KingdomExitTransitionNode` no documentado en el guión (plano de paisaje + logo del juego, con campos para cerrar la demo aquí mismo, actualmente desactivados). **El grafo narrativo autorado termina justo después de esto** — las escenas 15 (segunda mitad) a 22 no tienen ningún nodo en `Cap6.asset`. Añadida una nota explícita de "hasta dónde llega el contenido implementado" en el guión.
- **Sección "sin mapeo claro" cerrada:** los dos sequencers que quedaban sin posición confirmada (`ReinoExitBanterSequencer.cs`, `LiamCrystalBallSequencer.cs`) ya la tienen. Solo `SimpleCinematicDirector.cs` sigue sin escena concreta asignada.
- **Balance de hechizos:** añadida la tabla "Orden de desbloqueo narrativo", reconstruida a partir de los `UnlockAbilitiesNode` reales de los 6 capítulos — Bola de Fuego (Cap.1) → Bola Prisma (Cap.2) → Corazón Estelar (Cap.5, regalo del Rey). Son los únicos 3 hechizos con desbloqueo narrativo confirmado dentro de lo implementado; el resto probablemente pertenece a Liam/Estela o a contenido aún sin construir.
- **Estado de Diálogos:** sembrada la tabla vacía con los 4 nodos de texto/diálogo reales encontrados (el `DramaticTextNode` del prólogo, el pensamiento de Will sobre la pesadilla, y los dos `ShowLorePopupNode` de las escenas 10 y 14) — aclarando que solo se confirma que existen y están bien enlazados, no que su contenido esté revisado.
- **Bug técnico encontrado y anotado (no corregido):** en la escena 10, el `WaitCustomEventNode(EVT_ARRESTADOS)` tiene una salida que apunta a un GUID que no corresponde a ningún nodo del Capítulo 4 — enlace roto/huérfano en el grafo, probablemente resto de una edición anterior en el Editor. No bloqueante (las otras dos salidas sí conectan), pero merece revisión.

**30 de agosto de 2026 — Claude (Cowork), a petición de Raúl (nivelar el guión con la novela ya corregida).**

- Corregida la escena 17 (El Sendero de las Estrellas): el guión describía las tres pruebas como caminos solitarios ("Prueba 1 de Will", "Prueba 2 de Estela", "Prueba 3 de Liam"), heredado sin cambios del Google Doc v1.0. Esto ya no coincidía con la corrección de canon que Raúl aclaró y que la novela (`novela/manuscrito-novela-completo.md`) ya refleja: las pruebas 1 y 2 las vive el grupo entero junto; solo la prueba 3 separa al grupo, y lo hace de otra forma (parque de atracciones + laberinto de espejos donde Will y Estela se quedan fuera viendo la traición de Liam desde fuera, no entrando los tres juntos como decía la versión anterior). Ver `INC-126` en `TRACKER.md`. Sin impacto en el juego implementado: las escenas 15-22 siguen sin ningún nodo en el grafo narrativo actual (ver nota de "hasta dónde llega el contenido implementado" más arriba), así que es una corrección puramente de diseño/documentación.

## Cobertura espacial de la maqueta Eldoria — base del 10, requisitos revisados el 25 de septiembre de 2026

La composición espacial se propuso para las variantes Eldoria Codex (INC-188), no para el estado de las misiones en MainWorld. La revisión narrativa añade necesidades que aún no tienen ubicación asignada. Las posiciones son propuestas de composición: una reserva espacial no acredita que existan NPCs, disparadores, colisiones transitables, navegación o una misión conectada.

| Necesidad de nivel | Situación y trabajo pendiente |
|---|---|
| Prólogo del valle | La secuencia usa un escenario aparte del pueblo jugable. El valle necesita encargos legibles, ruta de evacuación, la despedida de Liora y arena del Mago Oscuro. Comprobar que sus transiciones respetan el final jugable del prólogo. |
| Casa de Will, Oliver, Eldran y caja de fruta | Pueblo inicial conservado; falta asignar casas, entrada, mercado, disputa de las cajas de fruta y punto del encargo a los objetos narrativos. |
| Despertar y primer Demonio | Pradera propuesta junto al pueblo; faltan conexión al combate, actores, salida del proyectil y posición protegida de Eldran. |
| Preparación, comercios y Erika | Hay edificios, pero faltan asignación de comercios, objetos de preparación y patio de entrenamiento. |
| Bosque Prohibido, Estela y Gólem | Masa forestal, sendas y claro propuesto con troncos quemados; falta montar aparición de Estela, invocación y combate. |
| Taberna y montaña | Taberna propuesta y plaza baja del barrio. La montaña destruida debe ser un estado posterior al evento, no una ruina presente desde el inicio. Falta el recorrido de la persecución. |
| Castillo, calabozo y segundo Demonio | Castillo y explanada exterior reorganizados; sala del trono y calabozo no construidos en esta maqueta. La arena debe admitir la defensa de guardias y población. |
| Biblioteca real y espejo de Tobías | Sin ubicación asignada en la propuesta de Eldoria. Reservar espacio interior para investigación y conversación íntima antes del viaje. |
| Ruta del Fuego Fatuo y pueblo vecino | Bosque oriental y ruta con curvas propuestos. Falta diseñar lectura de rastros y ubicar el pueblo del amigo de Eldran; la casa de Silas y su taller de bucle temporal aún no están representados. |
| Risco, Vega y el manantial | No hay ubicación asignada. Requiere dos aldeas, presa, cauce compartido y rutas entre zonas agrícolas; mantener separadas sus necesidades sin inventar un atajo mágico que resuelva el conflicto. |
| Piedra Ancestral y ruinas | La propuesta del 11 de septiembre la traslada a una isla propia al sureste, separada del Bosque Prohibido. Se conservan monolito, ruinas y ruta marítima propuesta. Faltan adaptación al combate del Guardián y el retorno seguro; no se acredita navegación jugable. |
| Sendero, pruebas y Caja | Se mantienen como espacios separados de Eldoria. Candyland y la feria requieren zonas propias; la Caja necesita un espacio que permita a Will y Estela observar la prueba de Liam sin entrar en ella. Revisar conexiones cuando se integre el recorrido. |
| Biblioteca final, altar y salida | Necesidades de diseño nuevas del tramo final: deseos conservados, arena del Mago Oscuro, conducto hacia el altar y salida de resurrección. No tienen aún propuesta espacial ni integración confirmada. |
| Puerto pesquero y playas | Ambientación del mapa de referencia. Puerto rehecho con viviendas y embarcadero; playas despejadas de falsos edificios. No se inventan misiones para justificar estos lugares. |

Prioridad de trabajo: primero comprobar recorrido continuo a pie y escala de jugador; después resolver accesos, interiores y arenas; por último conectar personajes, misiones y eventos. La revisión visual por capturas no sustituye la prueba jugable.
