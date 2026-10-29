# GDD — El Sendero de las Estrellas

**Versión:** 3.1  
**Autor:** Raúl Báez Amate — Liyodev  
**Revisión:** 8 de octubre de 2026 (cierre de pendientes)

Esta es la biblia de diseño del juego: historia, personajes, recorrido, reglas jugables y contenido. Describe el juego que se quiere construir. La existencia de un texto o un asset no demuestra que una escena esté conectada o probada.

**Jerarquía:** decisiones de Raúl > GDD > novela adaptada. La novela fija la historia donde el juego no ha decidido una adaptación. Las diferencias deliberadas se identifican como adaptaciones del juego. Toda corrección narrativa se contrasta con ambos documentos; la novela se nivela si corresponde. No se cambia el canon por una limitación de implementación.

**Novela canónica:** `C:\Users\luarb\dev\El Sendero de las Estrellas\Novela\01_MANUSCRITO_FINAL\El_Sendero_de_las_Estrellas_manuscrito_canonico.txt` (7 de octubre de 2026).

- **GDD.md:** diseño y narrativa vigentes.
- **TDD.md:** arquitectura, API y reglas técnicas.
- **TRACKER.md:** incidencias, implementación pendiente y validación.
- **CLAUDE.md / AGENTS.md:** resumen de las reglas de entrada al proyecto.

## Índice

1. [Convenciones y reglas de diseño](#1-convenciones-y-reglas-de-diseño)
2. [Historia](#2-historia)
3. [Personajes, enemigos y jefes](#3-personajes-enemigos-y-jefes)
4. [Estructura del juego](#4-estructura-del-juego)
5. [Guion por capítulos](#5-guion-por-capítulos)
6. [Sistemas](#6-sistemas)
7. [Mundo y niveles](#7-mundo-y-niveles)
8. [Anexos](#8-anexos)
9. [Pendiente de decidir](#pendiente-de-decidir)

## 1. Convenciones y reglas de diseño

- Los bocadillos tienen un máximo de **tres líneas**. Si una intervención no cabe, se pagina en unidades de sentido completas. No se deja una palabra suelta en la página siguiente. Se comprueba la caja real en español e inglés.
- Los cuadros de diálogo se avanzan con **A** y no se saltan. Las cinemáticas sí permiten saltar.
- Los personajes no gritan el nombre de los hechizos. Los nombres sirven al jugador en menús y tutoriales.
- El doblaje es solo en español. Los textos se localizan en español e inglés.
- Los interiores no tienen clima. La lluvia y la nieve aparecen con cielo encapotado; las transiciones del clima son lentas y realistas.
- Cada hechizo tiene VFX propio y reconocible.
- Quien encarga una misión tiene una frase para cuando se le habla mientras sigue en curso.
- Al seguir a un NPC, el jugador avanza más lento que él. El guía espera cuando el jugador se retrasa.
- Todo objeto conseguido muestra un pop-up, salvo el botín de batalla, que se presenta en el informe de victoria.
- El arte sigue el estilo Quibli.

### Filosofía y Manifiesto de Diseño (Nuestra Brújula)

*Para consultar en momentos de duda, bloqueo o fatiga creativa. Este juego no compite contra gigantes en horas de cinemáticas ni en fotorrealismo: compite en alma, profundidad, misterio y respeto a la inteligencia del jugador.*

1. **Sin pintura amarilla ni pilotos automáticos:** El juego no resuelve los puzles por el jugador. No hay acompañantes gritando la solución a los cuatro segundos ni flechas fluorescentes guiando cada paso. El mapa sitúa los puntos de interés y da orientación, pero el misterio y la solución se conquistan con el ingenio y la observación del jugador (espíritu de la Cueva Helada de Pokémon o los templos clásicos de Zelda).
2. **Combates que hacen pensar (No machacar botones):** El combate exige estrategia, lectura de aperturas, gestión de amenazas y sinergia real de los tres personajes (Will, Estela, Liam). Cada jefe es un **jefe-puzle** dinámico (aperturas rúnicas, interacción con el entorno, debilidades mecánicas, coordinación en dúo), no un saco de vida.
3. **Maestría real (La memoria muscular del mago):** El grimorio no es un menú de pausa cómodo para elegir hechizos mientras el tiempo se detiene; los combos avanzados ($X, A, B, Y$) se aprenden a fuego en la memoria de los dedos. Lanzar un conjuro potente bajo la presión de un enemigo que embiste hace que el jugador sienta el auténtico mérito de ser mago.
4. **Progresión orgánica (Sin niveles numéricos de relleno):** No hay nivel 1 ni nivel 99, ni inflación numérica de estadísticas artificiales. El jugador se vuelve más fuerte porque amplía su repertorio, domina a su grupo y toma decisiones reales sobre su crecimiento.
5. **Recompensas con propósito y botín escaso:** Los combates normales alimentan la economía universal de **Esencia y Monedas** para comprar y mejorar en los pueblos (Renard, Tomasa, Patricia) y recargan orbes en caliente. Las piezas de equipo y las mejoras permanentes son **escasas y celebradas**, custodiadas por victorias reales y secretos del mundo.

## 2. Historia

### Premisa

Will cumple dieciocho años en el pueblo donde lo ha criado Eldran. El ataque de un Demonio despierta su magia. Liam busca una cura para Tobías, su hermano menor, y necesita a alguien capaz de abrir el Sendero de las Estrellas. Provoca el ataque para encontrar a una persona de buen corazón y utiliza a Will sin contarle la verdad. Estela se une al viaje. Los tres aprenden a cooperar sin decidir por los demás.

### La Luz primera y el Sendero

La Luz primera, la fuerza que encendió las estrellas, tendió el Sendero. No juzga ni elige. El camino pone a cada viajero ante aquello que podría corromper su deseo. El altar concede literalmente lo pedido: no interpreta, corrige ni devuelve a los muertos. Al final hay **un único deseo**, aunque entren varios viajeros.

La Piedra Ancestral sí elige a quién deja pasar. Las crónicas hablan de «corazón puro», pero no explican qué mide. También dejó entrar a quien después fue el Mago Oscuro. No convertir esa incertidumbre en una prueba simple de bondad.

### El Archimago, Will y el Mago Oscuro

Hace siglos, el Mago pidió **que ningún poder pudiera destruirlo**. El altar lo protegió de todos los hechizos que existían al pedirlo. Emprendió una Marcha de Conquista. El Archimago del valle se quedó para proteger a su gente y a Liora. Suplicó a la Luz primera un hechizo que no existía. El choque de esa protección con el hechizo destructivo del invasor encerró al Mago dentro del Sendero. El Archimago murió; su alma permaneció.

Siglos después encontró a un niño de cuatro años cuyo corazón se había parado, lo hizo latir y vivió con él. Ese niño es Will. **Son dos almas distintas.** Will no recuerda nada anterior a que Eldran lo encontrara. Las pesadillas son recuerdos que el Archimago comparte, no recuerdos de otra vida de Will. Venir al Sendero es una decisión de Will.

La magia de sometimiento está ligada al camino corrompido. Cada runa abre una puerta a esa influencia. Las criaturas obedecen al principio a quien las marcó; con el tiempo pueden levantarse solas. Detener a un invocador no elimina la fuente.

### Arco y desenlace

El grupo se prepara en el Reino, encuentra a Silas y recupera en la Piedra las fórmulas que faltan en el Libro. En el Sendero afronta las pruebas de Will y Estela en compañía. La Caja separa a Liam y muestra a sus amigos la verdad sobre los ataques. La confianza se rompe. Will decide continuar por convicción propia; después encuentra a Estela y ambos rescatan a Liam. Vuelven a viajar con límites claros, sin fingir un perdón inmediato.

En la batalla final, el Mago regenera su vida. Will retrocede diez segundos con el Hechizo del Tiempo y reúne a sus compañeros bajo el escudo para resistir la Marea. Queda exhausto. Liam se interpone ante la hoja de sombra dirigida a Will y muere. Pide que cuide de su hermano y le da las gracias.

Will crea **Amanecer** con la energía que Estela le entrega voluntariamente. Es un hechizo nuevo: no existía cuando el Mago formuló su deseo. Abre su protección y permite destruirlo. Will pide al altar que cure a Tobías conservando su memoria y voluntad, sin trasladar a nadie la enfermedad ni el daño de curarla.

El Sendero empieza a cerrarse. Estela carga con Liam hasta el umbral. Allí Will la empuja fuera y mantiene el cuerpo dentro: el Hechizo de Resurrección solo funciona en el Sendero. Entrega voluntariamente su vida, devuelve a Liam y destruye el camino desde dentro.

El Archimago usa su última fuerza para devolver a Will a casa. Ante Estela y Liam aparece como alguien distinto: «No soy Will». Se marcha con los suyos. Will regresa vivo y devuelve la cinta roja de Estela. Pierde los recuerdos del Archimago y la estrella de madera, que queda en el cielo, torcida. Semanas después, los tres pescan con Tobías junto al río. Eldran llega al día siguiente. El cierre conserva a Will vivo.

## 3. Personajes, enemigos y jefes

### Will

Will es un joven de buen corazón, pero el juego no debe convertirlo en un héroe que acepta cualquier daño sin preguntarse qué quiere. Su conflicto abarca el miedo a fallar, la revelación del alma que vive con él y el aprendizaje de elegir por sí mismo. Su bondad se expresa al escuchar, pedir consentimiento y ofrecer ayuda, no en renunciar automáticamente a su propia vida.

Su hechizo inicial es Llama Astral. Desarrolla herramientas de Luz, Mente y tiempo. Bola de Fuego pertenece a Estela. El Hechizo del Tiempo permite retroceder un intervalo corto, pero conserva el coste físico y mágico. El Hechizo de Resurrección requiere entregar voluntariamente una vida a cambio de otra recién perdida. Son reglas narrativas centrales y deben ser coherentes con los sistemas de juego.

### Liam

Liam ama a su hermano Tobías y teme perderlo. Esa desesperación explica su conducta, pero no la absuelve: organizó los ataques y utilizó a Will sin contarle la verdad. La prueba de la Caja le obliga a reconocer que esas decisiones fueron suyas, sin culpar a una sombra ni a su necesidad de curar a Tobías.

Liam posee magia propia, centrada en pactos, invocación y trampas. El límite narrativo que lo lleva a buscar a Will es que no puede abrir el Sendero por sí solo, no que carezca de magia. Tras la revelación, su redención es gradual: responde preguntas, acepta límites y deja de decidir por el grupo.

### Estela

Estela es una hechicera prodigiosa, impulsiva y franca. Su humor y su hambre alivian la tensión, pero no sustituyen su historia de duelo. La muerte de su hermana Mara sigue afectándola, en especial ante el azúcar caliente y la criatura de gominola de Chuchelandia.

Su poder de área debe sentirse fuerte sin presentar el descontrol como algo inocuo. En la ruptura hiere accidentalmente a Will; después se responsabiliza del daño y decide no huir. El arco no exige que deje de estar triste, sino que pueda recordar a Mara sin reducir su vida al día en que murió.

### Eldran

Eldran cuidó de Will y lo protegió durante años. Su cautela se relaciona con la pérdida de Selene y con la creencia de que más conocimientos le habrían permitido salvarla. Quiere evitar que Will sufra, pero esa intención puede limitar la capacidad de Will para elegir. Su relación con Silas da contexto a su juventud y a la persona que era antes de cerrarse al mundo.

Eldran y Victoria tienen una relación cercana y amistosa, sin romance. Eldran conoce a Estela solo de oídas. En juego es mentor inicial y apoyo narrativo. Si actúa como guía o punto seguro, no debe resolver las decisiones que corresponden al jugador.

### Silas

Silas es un hechicero, antiguo amigo de Eldran y dueño de una colección de relojes que no miden el tiempo de la misma manera. Ayuda al grupo tras recuperar su reloj en un bucle temporal. Enseña el ritual de la Piedra y explica el coste de la magia temporal.

### Tobías

Hermano menor de Liam. Tiene doce años y una enfermedad que consume su corazón y su magia. Los sanadores solo consiguen retrasarla. Habla con Liam a través del espejo; prefiere que su hermano vuelva antes que verlo arriesgarse sin límite. La cura final conserva sus recuerdos, su voluntad y su identidad. En el epílogo aprende a pescar con el grupo.

### El Rey

Condena al grupo tras los daños en el Reino. Después de la defensa del castillo reconoce su error y anula la condena. Permite consultar los archivos y enseña Corazón Estelar. Esa enseñanza con nombre propio es una adaptación del juego: en la novela se trabaja una barrera de energía compartida sin nombre.

### Mago Oscuro

Pidió que ningún poder pudiera destruirlo. Su deseo desató la Marcha de Conquista y lo mantiene ligado al altar. Es indestructible para los hechizos que existían entonces. Amanecer abre su protección y lo destruye. Su derrota exige un poder nuevo y cooperación voluntaria. «Mago del Cataclismo» no designa a otro personaje.

### Secundarios

| Personaje | Papel |
|---|---|
| Archimago y Liora | La última noche del valle y la vida que el Archimago dejó atrás. |
| Selene | Amiga de juventud de Eldran y Silas; su muerte en la búsqueda de la Piedra explica el duelo de ambos. |
| Mara y la madre de Estela | El duelo de Estela y su vínculo con casa; recordar a Mara no exige dejar de sentir tristeza. |
| Oliver y Pía | Aprendizaje y vida cotidiana de Will. Oliver acompaña el tutorial y sale del grupo en la despedida. |
| Victoria | Capa de preparación y relación cotidiana con Eldran. |
| Erika | Defensa y lectura corporal en el entrenamiento. |
| Tomasa | Tallista del mercado; vende la estrella torcida elegida con la moneda de Eldran. |
| Boticaria | Regala la poción de preparación de Will. |
| Roberto, Manuel, Nora, tabernera y tendera | Historias y encargos opcionales del Reino. |
| Guardia del Bosque | Plaga de arañas y acceso al bosque. |
| Niño Pez y Rudolfo | Secundarias que enseñan nado y salto. |

### Enemigos y jefes

El daño depende de la mecánica, la posición y la ventana del enemigo. Las reglas generales de elementos y estados se definen en 6.1.

| Enemigo | Qué enseña | Cómo se le hace daño |
|---|---|---|
| Demonio 1 | Leer el aro, esquivar y castigar la recuperación; Eldran guía el aprendizaje. | Solo con el aro encendido, durante el aviso, ataque y agotamiento. Fuera de esa ventana no recibe daño y **no se cura**. El aro admite impactos precisos mientras brilla; romperlo ofrece el final de Demonio caído. Tres fases: garras/embestida, fuego y sombras/subsuelo/combos. |
| Demonio 2 | Usar a los tres personajes y el espacio del castillo al defender el Reino. | **Aro de tres runas**, una por personaje (Will, Estela, Liam): cada runa solo la rompe su personaje, así que obliga a cambiar de personaje o a usar dúos; rotas las tres, queda expuesto. **Guerra en el aire:** en la fase 2 sube a las almenas y ataca desde arriba; el jugador sube (plataformas, levitación, vuelo si ya lo tiene) para alcanzarlo. Desde la fase 2, algunas ráfagas se sustituyen por una carga de plasma ocasional: cae donde estaba Will, queda fija y cambia de azul a amarillo y rojo antes de explotar con daño de área. La cuenta atrás da tiempo para apartarse. (Hoy el prefab recibe daño normal: falta construirlo.) |
| Gólem | Rodear una armadura, alternar objetivos y coordinar un remate. | Recibe el **20 %** del daño por delante/lados y el daño completo por detrás. Estela atrae su atención y protege al grupo. Lluvia de rocas y salto con onda amplían los patrones. **El fuego de frente lo hace crecer** (regla propia de este jefe, no una debilidad elemental general). Llevarlo al **barro** del arroyo lo hunde unos segundos y abre la ventana de la fase 2. El sello final exige el dúo Will + Estela; la magia normal no lo remata. |
| Araña | Distancia, esquiva y control de grupos pequeños. | Daño normal al cuerpo; no tiene una ventana elemental ni armadura frontal especial. Su acercamiento y preparación del ataque dejan leer cuándo apartarse y responder. |
| Guardián de la Piedra | Cooperar antes de acceder a las fórmulas y leer el escenario reflejado. | Recibe daño normal; no se acredita un filtro de exposición. Will sostiene, Estela presiona y Liam busca aperturas. **Adaptación del juego:** en la novela examina a Will y se aparta. |
| Mago Oscuro | Dominio del arsenal, defensa que devuelve proyectiles y uso del espacio. | Las espinas reducen el daño; devolver rayos rompe espinas y aturde. Sin espinas queda expuesto. En los pilares, romper anclas lo derriba; **Liam hace ×2,5 de daño a las anclas** por su personaje, no por su elemento. Tras regenerarse no recibe daño hasta Amanecer. El tramo completo está en 5.19. |

El Duque de Regaliz, el Gólem de azúcar y la criatura de gominola pertenecen al diseño de Chuchelandia, descrito en 5.13.

**Enemigos del mundo abierto (decididos, por construir).** La variedad está en el comportamiento: cada uno pide una respuesta distinta, sin debilidades elementales.

| Enemigo | Comportamiento |
|---|---|
| Fantasma | Intangible salvo bajo la luz; aparece de noche. |
| Beholder | Torreta que dispara rayos; se le devuelven con la defensa (B). También es pieza de puzle. |
| Mímico | Algún cofre de tesoro o de prueba muerde. |
| Imp ladrón | Volador que roba un objeto y huye. |
| Rocas | Embisten; si chocan contra un muro lo rompen. |
| Slime | Se divide al golpearlo; cerca del agua. Pack gratuito de Dungeon Mason. |
| Tortuga | Bloquea de frente; un empujón (Ráfaga, Nova de Luz) la voltea y queda expuesta. Pack gratuito de Dungeon Mason. |

**Regla de diseño de encuentros (no colocar enemigos al azar):**
En *MainWorld* y mazmorras **nunca se colocan enemigos sueltos simplemente para rellenar espacio vacío o patrullar caminos sin sentido**. Se diseñan **encuentros situacionales** con cuatro pilares obligatorios:
1. **El escenario como actor:** alturas, coberturas, desfiladeros o cuellos de botella, zonas de sombra/luz o elementos destructibles.
2. **Propósito táctico y sinergia del grupo:** la situación debe invitar a alternar personajes o mecánicas (el escudo de Estela, el posicionamiento de Liam, la magia a distancia de Will o el contraataque con B).
3. **Composición de roles:** no agrupar enemigos idénticos; combinar presión frontal con molestias a distancia o disruptores.
4. **Contexto ambiental o narrativo:** cada combate responde a una situación reconocible: un puesto de guardia/bloqueo de paso, una emboscada de codicia (mímicos / imps ladrones), un claro espectral nocturno (fantasmas) o un puzle en combate (engañar a una roca para derribar un muro agrietado y abrir un cofre). Menos combates, pero cada uno con identidad propia, lectura clara y recompensa.


## 4. Estructura del juego

### Capítulos y ritmo

La historia principal combina acción, exploración, puzles, conversación y descanso estructurada en capítulos cortos y autónomos para marcar un progreso claro:
- **Capítulo 1: El Despertar** (novelas I–II): prólogo de la última noche del Archimago, vida cotidiana en el pueblo y combate contra el Demonio 1.
- **Capítulo 2: Prepárate para el viaje** (novelas III–IV): la mañana después en WillHouse, los 4 encargos del pueblo (incluyendo combate de entrenamiento de defensa con Erika y puntería con Eldran) y la despedida con Oliver y Eldran.
- **Capítulo 3: La pista del mar** (novelas V–VI): interludio del espejo de Tobías, investigación en la aldea de la costa y dilema con los contrabandistas.
- **Capítulo 4: El Bosque Prohibido y el Gólem** (novelas VII–VIII): encuentro con Estela, Cúpula Estelar, puzle de Levitación, batalla contra el Gólem y unión de Liam en el vado (fin de la Primera Parte y de la demo).

El resto del arco recorre el Reino, Silas, la Piedra y el Sendero hasta el epílogo. Los grafos activos de referencia son **Cap1.asset, Cap2.asset y Secundary.asset**, continuándose en los activos modulares de cada capítulo.

### Mundo abierto

El mundo conecta pueblos, bosques, costa, islas y ruinas. Hay tesoros desde el minuto cero. Cada uno declara su requisito: ninguno, habilidad, objeto o Amanecer. El juego comunica lo que falta para poder regresar más tarde.

**Barco propio:** Silas entrega el barco con el que él, Eldran y Selene buscaron la Piedra, junto con sus mapas viejos. Está amarrado en el Pueblo Pesquero. Esta entrega es una adaptación jugable decidida.

**Vuelo:** Will aprende en la Piedra Ancestral a levantarse a sí mismo, cerrando el ejercicio de la cuchara. Puede volar libremente por el mundo. Solo se bloquea dentro de zonas de puzle, con límites y aviso legibles.

### El Sendero

Es exclusivamente historia principal. El umbral marca el punto sin vuelta atrás: aviso explícito y guardado antes de entrar. Dentro no se vuelve al mundo real hasta terminar la historia o empezar una partida nueva. No es una zona de farmeo ni una ruta hacia misiones del mundo.

### Post-juego

Se abre tras el epílogo, desde la escena del río; Eldran puede dar acceso. Contiene templos y mazmorras con guardianes que requieren Amanecer u objetos concretos. No permite entrar al Sendero.

**Pruebas de las Estrellas (decidido).** Pruebas opcionales repartidas por el mundo, dejadas por los viajeros que buscaron el Sendero antes que Will. Cada una enciende una estrella de una constelación en el cielo nocturno; una constelación completa da una recompensa grande (equipo con nombre, hechizo o lore del Sendero). Se juegan con la zona sin vuelo del kit de puzles.

## 5. Guion por capítulos

### 5.1. Prólogo: la última noche del Archimago

El jugador resuelve tres encargos cotidianos en el valle: el horno, una carreta y un globo en el campanario. Comparte el desayuno con Liora y ve la carta de la academia que el Archimago no llega a responder. Después ayuda a evacuar el valle; no puede salvar a todos. Se despide de Liora y se enfrenta al Mago Oscuro.

El duelo tiene cuatro asaltos y culmina en el agujero negro. No se gana agotando una barra de vida: el Archimago suplica a la Luz primera un hechizo que no existe y lo culmina con «¡Protégelos a todos!». La protección contiene el ataque y sella al Mago en el Sendero. El corte lleva al despertar de Will. El prólogo anticipa el sacrificio sin explicar todavía que el alma del Archimago vive con él.

### 5.2. La mañana de Will: casa, Oliver y Eldran

En esta adaptación del juego no hay una carta de Eldran: Will despierta tras la pesadilla, oye jaleo fuera desde la ventana y sale a ver qué ocurre. Allí se encuentra con Oliver, que llega corriendo, sin aliento, le enseña un hechizo nuevo y provoca un estallido que les salpica a ambos. La secuencia termina con una transición a los menús y al control del grupo.

El jugador recorre el pueblo y presencia la discusión amistosa de Eldran y Victoria por la caja de manzanas que falta: Eldran jura que trajo dos y a Victoria solo le ha llegado una. Cuando ella le pregunta si paró por el camino, Eldran admite que descansó un momento a la sombra de un árbol, y Victoria concluye que allí la tendrá. Eldran no se enfada: se ríe de sí mismo («perdiendo cajas a mi edad»). Es la caja que Eldran se dejó en el bosque y que luego le pide a Will: la discusión lleva al encargo. Establece la familiaridad entre ellos (sin relación romántica) y el tono cotidiano del pueblo. Oliver, que ya ha llegado con Will, se ofrece a ayudarles con las cajas antes de que ambos se marchen. Eldran da a Will una moneda como adelanto de su regalo por los dieciocho años. Will pregunta para qué la quiere y Eldran le responde que quiere que tenga algo para él, aunque sea una tontería; Will acaba eligiendo una estrella de madera en el puesto de Tomasa.

### 5.3. La caja de fruta: encargo y control del mapa

Tras comprar el objeto inútil que Eldran pidió, SEQ_EldranCaja hace que Eldran llame a Will desde lejos, se acerque, le diga que ya sabe dónde está la otra caja y le pida «Ven, sígueme». Empieza la misión «Sigue a Eldran» (a ver qué quiere), que se cierra cuando Eldran le pide la caja. Oliver se queda con Victoria en el pueblo, así que Will sigue a Eldran solo. El jugador le sigue con el control, sin poder adelantarle (Eldran le espera si se queda atrás), y por el camino charlan en bocadillos: hasta el punto de guardado, de la caja que faltaba («¿Y la otra caja?» «En el bosque. La dejé allí esta mañana.»); del punto de guardado al bosque, de la estrella torcida que Will acaba de comprar («En una estrella de madera. Algo torcida.» «Entonces es perfecta.»). Por el camino paran en el punto de guardado de la entrada del reino: Eldran explica para qué sirve. Siguen hasta el borde del bosque y allí Eldran le encarga recuperar la caja, que dejó al pie de un fresno, y le dice que le espera. Al entregarla, Eldran le pide que a Victoria ni una palabra. El jugador sigue el marcador del minimapa hasta la caja, la recoge y se la devuelve en ese mismo punto, de modo que el Despertar de la Estrella ocurre fuera del pueblo, sin vecinos alrededor. El marcador y el tutorial sirven para enseñar exploración y entrega de objetos antes del Despertar de la Estrella.

### 5.4. El Despertar de una Estrella

Eldran avisa a Will cuando un proyectil mágico se dirige hacia él. El tiempo se ralentiza y el jugador debe pulsar X para responder con su magia instintiva. Si no responde, el plasma lo alcanza: **Game Over y continuación desde el último punto de guardado**. Al superar el evento se desbloquean el ataque mágico y **Llama Astral**, antes de la misión del Demonio.

### 5.5. El Demonio y los planes de Liam

El enfrentamiento contra el primer Demonio es un combate normal de gameplay, no una cinemática. El jugador aprende magia básica, se mueve por la arena y combate mientras Eldran lo anima desde una posición cercana.

**Diseño del combate:** solo se le hace daño mientras brilla el aro de runas; si se le golpea sin el aro, el golpe no hace daño ni lo cura. El aro se enciende al preparar cada ataque y sigue encendido mientras el Demonio queda agotado tras golpear: la lección es esquivar y castigar después. Si Will se aleja, el Demonio marca el suelo y embiste. Tres fases con identidad propia, marcadas en su barra de vida: 1) garras y embestida; 2) además, fuego a distancia; 3) además, lluvia de sombras, paso bajo tierra y combos, con menos respiro. Cada cambio de fase es un rugido con cámara lenta y una onda que aparta a Will, y el cuerpo del Demonio se enrojece. Eldran guía todo el combate: explica el aro al empezar, avisa las primeras veces que brilla, corrige los golpes a destiempo, explica cada fase nueva y anima cuando falta poco.

Tras la victoria, una escena aparte muestra a Liam observando a Will en la bola de cristal. No ocurre durante el combate.

Después de la victoria se completa el encargo de Eldran y se vuelve a un punto de guardado.

**ADAPTACIÓN DEL JUEGO frente a la novela V:** Liam provoca el ataque para encontrar a alguien de buen corazón, con el tono de la novela. En la escena del cristal no se ríe; se levanta cuando Will cae y la llave del aro se apaga al romperse el aro. Su frase clave: «No hace falta que me caiga bien. Solo tengo que llevarlo hasta allí.» En la novela manda al lobo por error, creyendo que Will es un ladrón; en el juego no hay cuneta ni trozo robado.

### 5.6. Capítulo 2: Prepárate para el viaje — novelas III–IV

El capítulo empieza al ganar al Demonio 1 y abarca la recuperación y preparación de Will en su pueblo natal hasta su partida oficial. Se estructura en cuatro momentos bien diferenciados:

| Bloque | Qué pasa | Tipo | Qué enseña o pone a prueba |
|---|---|---|---|
| **2.1** | Liam observa a Will en el cristal | Cinemática | El jugador sabe más que Will (Liam busca a alguien de buen corazón; no es villano típico). |
| **2.2** | La mañana después: vela, origen de Will y trozo del aro | Cinemática con interacción | Control fino de la magia con la vela. Eldran revela el origen de Will y le entrega el **Trozo del aro** (guardando el resto en sal). Meta: encontrar a Estela. |
| **2.3** | Cuatro encargos de preparación en orden libre | Gameplay / Tutoriales | **Victoria:** Capa de viaje (+3 defensa).<br>**Boticaria:** Poción de vida regalada (uso de consumibles).<br>**Erika:** Reto, combate de entrenamiento (defensa/lectura corporal con escudo B) y entrega del **Pañuelo**.<br>**Eldran:** Tiro a cubos y piedras en la plaza; aprende **Bola Prisma** y túnica de viaje. «¿Debes hacerlo?» |
| **2.4** | Lista de motivos, brújula y despedida | Cinemática / Diálogo | Despedida emotiva en el cruce: Oliver sale del grupo (permanece en el pueblo) y Eldran parte a la capital. Checkpoint `CAP2_FIN`. |

**ADAPTACIONES DEL JUEGO:**
1. **El aro roto del Demonio sustituye al trozo de hierro.** No hay lobo ni cuneta. Eldran guarda el aro en el cofre de sal y Will lleva el objeto «Trozo del aro» en su inventario.
2. **Entrenamiento con Erika:** Retoma el combate de duelo tutorial. Erika reta a Will; Will pone a prueba su defensa frente a los ataques y escudo de Erika. Tras vencerla, Erika le entrega el **Pañuelo** (*«para el sudor y para acordarte de volver»*).
3. **Oliver participa en la preparación**, acompañando a Will hasta la despedida en el cruce. No sale del pueblo; el grupo final será Will, Estela y Liam.

---

### 5.7. Capítulo 3: La pista del mar — novelas V–VI

Will viaja en solitario hacia la costa buscando pistas sobre los aros y las personas vistas en la visión.

| Bloque | Qué pasa | Tipo | Qué enseña o pone a prueba |
|---|---|---|---|
| **3.1** | Liam habla con Tobías por el espejo | Cinemática de interludio | Para quién busca la cura Liam y su dilema interno. |
| **3.2** | Will reúne pistas en la aldea del mar | Investigación | Escuchar, inspeccionar y unir pistas en la hoja de avistamientos. |
| **3.3** | Sigue el carro de los contrabandistas y decide cómo actuar | Sigilo ligero y decisión | Aplicar la lección de Eldran: combate opcional, evitable, con consecuencias según la elección (avisar a la guardia o combatir; jaulas quemadas). |

---

### 5.8. Capítulo 4: El Bosque Prohibido y el Gólem — novelas VII–VIII

Will se adentra en el bosque, encuentra a Estela y se enfrentan a la primera gran prueba de invocación.

| Bloque | Qué pasa | Tipo | Qué enseña o pone a prueba |
|---|---|---|---|
| **4.1** | Explora el Bosque Prohibido; Estela lee el aro y la hoja | Exploración y cinemática | Estela reconoce las runas de sometimiento y entra en el grupo. |
| **4.2** | Descanso en la casa entre raíces y primera receta | Respiro cómico y tutorial | Aprende el combo mágico **Cúpula Estelar** (Y → X → B → A). |
| **4.3** | Prueba de la campana en cuatro rondas | Puzle de combate por rondas | Aprende **Levitación**: gestión de maná, cambio de personaje, fuego amigo y áreas. |
| **4.4** | Liam invoca al Gólem; Will y Estela lo vencen | Combate de Jefe | Mecánica de armadura, Cúpula Estelar, zona de barro y remate en dúo. |
| **4.5** | Comparten la manzana harinosa y continúan apoyándose | Respiro | Aceptar ayuda sin imponerla. |
| **4.6** | Bifurcación, entrada de Liam al grupo y bota en el vado | Cinemática y gameplay | Los tres viajan juntos; fin de la Primera Parte y de la demo jugable. |

**Diseño del combate del Gólem:**
- Una explosión frontal muestra la armadura. El fuego de frente lo hace crecer: regla propia de este jefe.
- Un manotazo tira a Will delante de Estela; ella corta su hechizo para no alcanzarlo y Will levanta Cúpula Estelar sobre ambos. Bajo la barrera acuerdan llevar al Gólem al barro.
- Una zona de barro junto al arroyo lo hunde unos segundos. Abre la ventana de la fase 2 para atacar las grietas y mostrar las runas de la espalda. Remate final en dúo Will + Estela.

---

### 5.9. Capítulo 5: La taberna, el arresto y la deuda del Rey — novelas IX–XII

La escena de la taberna combina un respiro cómico con una persecución de Estela que pone al pueblo en peligro. La explosión que daña la montaña tiene que sentirse como consecuencia de la persecución, no como un gag aislado: el jugador ayuda a evacuar o proteger a los vecinos antes del remate de la secuencia.

Después, la guardia conduce al grupo ante el Rey. La tensión por los daños y la sospecha sobre Liam llevan al arresto; escapar del calabozo exige usar el cambio de personaje y cooperar. Al salir, el grupo ayuda a defender el Reino del segundo Demonio. Tras la victoria, el Rey reconoce que los juzgó mal y anula la condena. Después les permite consultar los archivos y enseña a Will Corazón Estelar: una magia de energía compartida que nadie puede imponer a otra persona.

**Adaptación jugable:** persecución con rutas y rescate de civiles; audiencia que establezca la condena; puzzle de celda basado en habilidades complementarias; defensa del castillo como combate distinto del primer Demonio. Separar claramente los dos jefes y dar a la victoria una consecuencia narrativa visible.

### 5.10. La biblioteca y la salida del Reino — novelas XIII–XV

En la biblioteca real, el grupo investiga las versiones contradictorias del Sendero y las referencias a deseos de curación. Liam oculta para quién busca una cura; Will acaba preguntándoselo directamente. Antes de partir, una conversación con Tobías a través del espejo conecta la aventura con la persona concreta que Liam intenta salvar. La escena importa porque Liam deja de ser solo «el compañero misterioso»: el jugador ve el vínculo fraternal y lo que está en juego.

Durante el viaje, el grupo aprende a leer rastros mágicos. La ruta del Fuego Fatuo puede convertirse en un segmento de exploración nocturna: seguir señales auténticas, comparar rastros y reconocer los engaños del entorno. La charla de camino deja que los tres se conozcan y marca que protección y control pueden parecerse, pero no son lo mismo.

**Diseño jugable:** intercalar búsqueda de información, conversación opcional y exploración; evitar que el Fuego Fatuo sea únicamente un puzzle de seguir un color. Las pistas deben poder leerse por forma, movimiento o reacción del entorno, además del color, y el jugador debe poder recuperarse de una ruta falsa sin reiniciar un tramo largo.

### 5.11. Silas y el hechizo del tiempo — novela XVI

El amigo de Eldran se llama Silas. Su petición no es un favor de recadero: el grupo recupera un reloj perdido en un taller atrapado en un ciclo de cuarenta segundos. Resolverlo requiere observar qué cambia, recordar qué persiste entre ciclos y combinar las capacidades de Will, Estela y Liam. La historia revela la amistad de juventud entre Silas y Eldran y el duelo de Eldran por Selene, que explica parte de su sobreprotección.

Silas explica el ritual para llegar a la Piedra y la regla del Sendero: no premia una etiqueta de «bueno», sino que pone a prueba si cada viajero puede reconocer aquello que corrompería su deseo. También describe la regresión temporal como una oportunidad breve con coste, no como un reinicio gratuito.

**Diseño jugable:** puzle de bucle corto con intentos rápidos, pistas persistentes y coste legible. El jugador observa qué cambios permanecen y coordina al grupo para detener la máquina. Silas provoca las vueltas y paga su coste físico; no vuelve atrás con los demás.

**Archivos de Silas:** el grupo lee el caso del sanador que deseó eliminar todas las enfermedades. El altar creó un lugar sin enfermedad porque nada estaba vivo. Will y Liam ensayan el deseo por Tobías, protegiendo cuerpo, memoria y voluntad. Estela añade que no se traslade el daño a nadie. Este aprendizaje pertenece a Silas, no a una biblioteca del Mago.

Silas explica que la Piedra elige, que el altar concede un único deseo literal y que no resucita a los muertos. Revela cómo las runas de sometimiento filtran la corrupción del Sendero. El grupo elige entrar también para detener esa fuente. Silas entrega el barco y sus mapas viejos del Pueblo Pesquero como adaptación de juego; no enseña todavía la fórmula del tiempo, que se obtiene en la Piedra.

### 5.12. El precio pequeño: Risco y Vega — novela XVII

Antes de llegar a la Piedra, Will y sus amigos encuentran dos aldeas que comparten un manantial. Una construyó la presa; la otra depende del agua. Ambas tienen documentos que justifican sus reclamos. Liam propone resolverlo con persuasión mágica; Estela se opone a decidir por la gente. Will propone investigar y negociar, aunque no exista una solución perfecta ni todos queden satisfechos.

**Diseño jugable:** misión sistémica breve con tres fuentes de información: medir la fuga de la presa, escuchar a las familias de Vega y acompañar la apertura de las compuertas de Risco. El jugador reúne pruebas y ayuda a establecer un reparto y una revisión futura. Evitar un diálogo de elección binaria que finja que una aldea tiene toda la razón: la salida debe depender de comprender el problema y pactar condiciones.

Este episodio es una prueba del carácter de Will antes de la prueba mágica. El «precio pequeño» es aceptar que un acuerdo imperfecto puede ser mejor que imponer una solución definitiva.

### 5.13. La Piedra Ancestral y el acceso al Sendero — novela XVIII

El camino atraviesa ruinas donde quienes llegaron antes dejaron sus nombres y motivos. La cámara de la Piedra solo se ve reflejada en el agua; el grupo debe avanzar confiando en ese reflejo. Al recitar Will el conjuro de Silas, aparece el Guardián. El combate se gana cooperando: Will sostiene la línea, Estela presiona y Liam encuentra puntos débiles con su magia de pacto.

Tras la victoria, las copias incompletas del Libro encajan con las inscripciones del pedestal. El grupo obtiene el ritual de apertura y las fórmulas del Hechizo del Tiempo y del Hechizo de Resurrección. La segunda exige una vida voluntaria a cambio de otra recién perdida. El coste debe explicarse con claridad antes de que la historia llegue al sacrificio; no presentarlo como una sorpresa mecánica.

El grupo estudia las reglas, acuerda señales y un punto de reunión y decide cruzar juntos. El acceso puede ser el último punto de guardado seguro antes de entrar.

**Diseño jugable:** navegación por reflejos, jefe con funciones complementarias y un momento de preparación en el que se revisan controles, consumibles y reglas del Sendero. El jugador debe entender que el hechizo temporal no cura ni revierte el coste de sus acciones y que el de resurrección intercambia vidas.

**Adaptación del juego:** el combate del Guardián sustituye al examen de Will tras el que se aparta en la novela. En la Piedra, Will aprende también el vuelo: levantarse a sí mismo completa el ejercicio de la cuchara. El umbral ofrece aviso de punto sin retorno y guardado explícito.

### 5.14. Entrada al Sendero y prueba de Will — novelas XIX–XX

Los tres cruzan juntos. La primera prueba enfrenta a Will con recuerdos, no con una sucesión de caminos individuales para cada personaje. Al principio el laberinto convierte una vergüenza social en humor; después llega a la pesadilla recurrente y a recuerdos que alimentan el miedo de Will a no llegar a tiempo. Estela y Liam lo acompañan hasta que el Sendero los separa y Will debe dejar de obedecer cada llamada de auxilio ilusoria. La salida consiste en reconocer que no puede salvar a todo el mundo y aun así elegir a quién escuchar.

**Diseño jugable:** laberinto de recuerdos con cambios de tono y una mecánica de priorización. Las voces falsas reutilizan patrones aprendidos, pero el jugador obtiene señales para distinguirlas. No castigar la exploración con un fallo terminal por atender a una ilusión; el conflicto debe expresar el límite emocional de Will, no parecer una trampa arbitraria.

### 5.15. Chuchelandia — novela XXI

La segunda prueba pertenece a Estela, pero el grupo la afronta unido. El reino obliga a sus habitantes a declararse felices y castiga cualquier muestra de tristeza. A Estela le asignan el papel de reina; Will y Liam quedan disfrazados y pueden investigar el palacio y ayudar a quienes esconden lo que sienten. El antagonista no es solo un reino cursi: es una falsa misericordia que evita el dolor borrando la libertad de sentirlo.

Estela debe enfrentarse a la pérdida de su hermana Mara y a la culpa que arrastra. El duelo contra el Duque de Regaliz y el Gólem de azúcar no la «cura» ni elimina su tristeza: le permite recordar a Mara sin fingir que solo existe el último día. La criatura de gominola es parte de ese conflicto y del desenlace del combate; no reducirla a un chiste o a un objeto coleccionable.

**Diseño jugable:** exploración social con disfraces, pistas y personajes que necesitan ayuda; jefe de varias fases que conecta las mecánicas aprendidas por Estela con el apoyo de Will y Liam. Tras la victoria, dejar un momento jugable y tranquilo para hablar de Mara y permitir que los habitantes decidan si salen del reino.

**Adaptación del juego:** se conserva el diseño de esta prueba y su puesta en escena del GDD. No se presenta como reproducción literal de la novela.

### 5.16. La feria y la pausa del grupo — novela XXII

Después de las pruebas, los amigos descansan en una isla segura y luego visitan una feria. Comparten atracciones, comida y conversación. Will recibe un peluche con forma de bola de fuego. La feria ofrece al jugador un respiro real: lavar ropa, reparar equipo, jugar y conversar sin una amenaza inmediata. Durante esta pausa Liam se acerca a confesar algo, pero aún no se atreve a decirlo.

**Diseño jugable:** zona opcional de interacción y preparación, con actividades cortas que refuercen lo que cada personaje sabe de los otros. Las conversaciones no deben adelantar falsamente la confesión completa. El regalo y las rutinas compartidas deben regresar visual o mecánicamente más tarde como recuerdos, no como pistas obligatorias para resolver un puzzle.

**Adaptación del juego:** se conserva el diseño de esta prueba y su puesta en escena del GDD. No se presenta como reproducción literal de la novela.

### 5.17. La Caja: prueba de Liam — novela XXIII

La tercera prueba es la única que separa al grupo. Liam entra solo en la Caja, un laberinto de espejos; Will y Estela ven parte de la prueba desde fuera a través de una superficie de luz. La Caja muestra primero un recuerdo feliz de Liam con Tobías y después el deterioro de sus decisiones: el diagnóstico, el libro robado, el pacto y el uso de Will como cebo.

Liam se enfrenta a una sombra que representa sus propias decisiones. La salida no consiste en negar la desesperación ni culpar a una parte separada de sí mismo: debe reconocer que fue él quien tomó esas decisiones y rechazar la última oportunidad de volver a sacrificar a Will a cambio de salvar a Tobías. La Voz Ancestral pregunta por el precio real. La visión revela al grupo la emboscada del bosque y las manipulaciones posteriores.

**Diseño jugable:** tramo de Liam en solitario que combina navegación, combate y recuerdos interactivos. Cada espejo ofrece contexto y también una decisión de Liam. Evitar que el combate venza a la responsabilidad moral: la sombra se supera admitiendo la autoría de los actos. Alternar control de Liam con la observación de Will y Estela solo en momentos que añadan contexto.

### 5.18. La ruptura y sus consecuencias — novela XXIV

Al salir Liam, Estela exige una explicación. Will pregunta directamente si Liam pensaba matarlo; Liam admite que quiso usar su poder para abrir el altar y que se dijo que evitaría su muerte. No consigue justificar haberle quitado la posibilidad de elegir. Estela ataca impulsivamente; Will recibe parte del golpe al interponerse. Estela se marcha y Liam se aleja por otro camino.

No resolver el conflicto con una sola disculpa ni con un QTE que haga creer que el jugador podía evitar la ruptura. Después, Will permanece solo y se permite estar enfadado. Decide continuar por un motivo propio: detener al Mago Oscuro y buscar cómo volver, sin prometer entregar su vida.

**Diseño jugable:** conversación con interrupciones breves y actuación, seguida de una separación espacial real. La violencia de Estela debe ser accidental y no premiada; su historia y el daño a Will se abordan después.

**Recorrido:** al salir de la Caja, Will atiende su herida y revisa sus objetos sin una amenaza activa. Luego cruza un tramo compacto de orientación, obstáculo y emboscada, leyendo rastros de Estela y Liam. Encuentros cortos enseñan a sobrevivir solo; la magia abre un bloqueo y permite castigar blancos expuestos. El final tiene un punto seguro. No se alarga como una zona de farmeo.

### 5.19. Reunión con límites y confianza reconstruida — novela XXV

Will encuentra primero a Estela y reconoce que ella le hizo daño; decide acompañarla sin exigir que finja estar bien. Juntos rescatan a Liam de las bestias. Will le ofrece la mano, pero el reencuentro no borra la traición: el grupo acuerda reglas concretas. Liam responde preguntas, no toma decisiones por ellos y no usa magia oscura sin consentimiento; Estela no tiene que declarar perdón inmediato. Los tres vuelven a cooperar porque eligen hacerlo con límites claros.

**Diseño jugable:** dos tramos de combate y exploración —Will solo, luego Will y Estela— que culminan en rescatar a Liam. La recuperación de poder puede expresarse como coordinación de equipo, pero no como borrón y cuenta nueva ni como aprobación retroactiva del engaño. Después del rescate, el jugador ayuda a elegir qué equipo y qué información se comparte para preparar el tramo final.

**Recorrido:** Will y Estela avanzan por una ruta compartida con conversaciones opcionales. El rescate de Liam combina contener bestias y liberarlo del peligro; una amenaza cambia de objetivo para premiar la protección del compañero. No hay barra de amistad ni perdón automático.

### 5.20. Preparación antes del altar

Los tres revisan sus recursos, acuerdan señales de retirada y dejan claro que nadie es sacrificable. Hablan de la cura de Tobías y de lo que aún desconocen de Will. No anticipan como certeza la separación del Archimago antes de su revelación.

**Diseño jugable:** área segura para revisar objetos, hechizos y grupo; conversación opcional y entrada al combate cuando el jugador quiera. Sin temporizador ni puzle de confirmación. El simulacro de práctica previo al altar no entra por ahora.

El grupo aplica lo aprendido sobre los deseos literales en los archivos de Silas.

### 5.21. El Mago Oscuro y la batalla final — novelas XXVI–XXVII

El grupo llega junto al altar y se enfrenta al Mago Oscuro. La verdad sobre el Archimago que vive con Will se revela durante el conflicto; no convertir una explicación monologada en una cinemática interminable. Repartir la revelación entre líneas y recuerdos breves que permitan al jugador conservar orientación y control.

El combate tiene cooperación real entre los tres. El Mago Oscuro deforma el espacio y cambia sus patrones. En su ataque de área, Will usa el Hechizo del Tiempo y retrocede diez segundos: conserva el agotamiento y las heridas, y, al repetir el momento, reúne al grupo detrás de su escudo, que aguanta la segunda marea. Después, debilitado, Will queda expuesto al ataque final y Liam se interpone. Sus últimas palabras piden a Will que cuide de Tobías.

Will intenta otra vez el tiempo y no le queda nada. Le pide a la Luz primera algo que no existía cuando el Mago pidió su deseo, reflejando la plegaria del Archimago. Estela le da su energía y nace Amanecer. La protección del Mago se abre. En el choque final gana la luz y el Mago se deshace gritando que ningún poder puede destruirle.

**Diseño jugable:** el jefe más difícil del juego, por patrones y lectura del escenario; el jugador lo tiene todo menos Amanecer. La barra baja, se regenera entera antes de la Marea y solo baja de verdad con Amanecer combinado con el resto del arsenal. El tiempo se usa una vez, integrado en el ataque inevitable. Ningún momento de la historia se puede fallar.

**Adaptación jugable de la batalla.** Se adapta la novela al juego: es el jefe más difícil, pone a prueba todo lo aprendido y nadie enseña controles durante el combate. Al empezar solo está el altar de piedra blanca; lo demás lo conjura el Mago.

- **Fase 1 (patrones):** teletransporte, salvas de rayos que se pueden devolver con la defensa (le aturde y rompe una espina), grietas, nova si te acercas y el Pozo (el agujero negro del prólogo) si estás lejos. Conjura espinas: mientras queden recibe poco daño y cada una suma un rayo a la salva. Rotas todas, queda aturdido y expuesto; luego las vuelve a conjurar.
- **Fase 2 (lo levanta todo):** conjura pilares con anclas, plataformas flotantes y lanzadores de salto, y vuela de pilar en pilar. El suelo se corrompe donde estás y llueve sombra. Rotas las anclas (Liam les hace ×2,5 de daño), se desploma expuesto y conjura el siguiente juego en otros sitios.
- **Regeneración:** al llegar a su umbral vuelve al altar y la barra se llena entera a la vista. Desde ahí ningún golpe le hace nada: su deseo no le hace intocable, le hace indestructible.
- **La Marea:** la primera no tiene hueco; el tiempo se para y Will rebobina (se conservan heridas y cansancio). Después Will grita «¡Conmigo! ¡Ya!», los compañeros se colocan tras él y su escudo aguanta la segunda. Nada se puede fallar.
- **El hechizo final se llama Amanecer.** La energía de Estela solo aparece en la cinemática en la que Will lo crea; después es un combo normal de la Y, y LT+RT con Estela sigue siendo el dúo. Con Amanecer la barra baja de verdad hasta romperse. La hoja de sombra, el sacrificio de Liam, la plegaria y el choque final reflejan el prólogo.

### 5.22. El deseo, el derrumbe y la resurrección

Tras destruir al Mago, Will pide al altar la cura de Tobías: conserva sus recuerdos y voluntad, y nadie carga con la enfermedad ni con el daño de curarlo. El altar cura a Tobías; no devuelve a Liam.

El Sendero se cierra. Estela carga con Liam hacia el umbral. Allí deja el cuerpo para ayudar a Will. Él la empuja fuera y permanece con Liam dentro: cruzar el cuerpo impediría resucitarlo. Pronuncia el Hechizo de Resurrección, entrega su vida y envía a Liam vivo junto a Estela. Después destruye el Sendero desde dentro para que nadie vuelva a pedir allí lo que pidió el Mago.

**Diseño jugable:** deseo y resurrección son decisiones distintas y comprensibles. No se pueden fallar por un QTE ni por tardar en confirmar. El colapso conduce al umbral con obstáculos breves que reutilizan habilidades conocidas, sin un cronómetro que obligue a repetir el duelo. La elección final conserva el deseo de Will de vivir y su decisión voluntaria; el regreso se revela en el epílogo.

### 5.23. Epílogo: el adiós del Archimago y el regreso de Will

Liam despierta junto a Estela y la Piedra apagada. Ella cuenta que Will la empujó fuera. Una figura aparece sobre la piedra. Estela cree que es Will; la figura responde «No soy Will». Es el Archimago. Usó la fuerza que le quedaba para devolver a Will a casa y se marcha con Liora, su familia y sus amigos.

Will aparece vivo con la cinta roja: «Creo que esta cinta es de alguien». Los tres se abrazan. Los recuerdos del Archimago se desvanecen; la magia de Will sigue siendo suya. La estrella de madera ya no está con él y queda torcida en el cielo.

Semanas después pescan con Tobías recuperado junto al río. Eldran llegará al día siguiente. **Diseño jugable:** cierre breve, contemplativo, con silencios y miradas. Después se abre el post-juego desde el río, sin volver al Sendero.

## 6. Sistemas

### 6.1. Combate

Acción mágica en tiempo real. El jugador lee señales, esquiva, se defiende, devuelve proyectiles y castiga aperturas. Puede cambiar entre Will, Estela y Liam; los compañeros actúan con IA. La carga de equipo es compartida y permite dúos y trío con LT+RT. Cada encuentro introduce una regla legible, permite practicarla y después la combina con una conocida. El espacio y los patrones cambian antes de limitarse a subir la vida del enemigo.

Los elementos son **identidad y herramientas de puzle**. No hay debilidades ni resistencias elementales en combate. Las restricciones de daño de los jefes son mecánicas. El bonus de Liam contra las anclas depende del personaje. Los estados ralentizar, inmovilizar, atraer y empujar añaden control; en jefes duran la mitad.

**El grupo en combate (decidido 8 oct).** Will, Estela y Liam tienen **vida y maná propios**, igual al mando que con IA. Los enemigos reparten a quién atacan, así que proteger al compañero tiene sentido. Un personaje que llega a 0 queda **caído** en el suelo y deja de luchar; si era el que llevaba el jugador, el control pasa solo a otro que siga en pie. **Game Over solo si caen los tres.** Se revive de cuatro formas: otro personaje se acerca y **mantiene A** unos segundos (expuesto mientras lo hace); un **objeto** raro (pluma o elixir); **Brisa Sanadora** levanta a los caídos de su zona; y **al ganar el combate** los caídos se levantan con poca vida. La muerte de Liam en la batalla final es historia, no una caída de combate.

El maná y el gesto de lanzamiento marcan el ritmo; los hechizos no tienen recarga propia. El escudo sostenido consume maná. Se alternan presión y descanso. Los momentos narrativos del jefe final, la muerte de Liam y las decisiones finales no tienen posibilidad de fallo.

**Progresión del jugador: sin niveles numéricos, elección entre Vida y Maná (decidido 25 oct 2026).**
El jugador no acumula niveles clásicos (1 a 50) ni sube estadísticas de ataque/defensa. La progresión sigue la fórmula de acción-aventura:
1. **Medidor de Sintonía Estelar:** Los combates derrotando criaturas y purificando amenazas llenan una barra de progreso. Al completarse, el jugador alcanza un hito de expansión y **elige libremente entre dos dones**:
   - 💚 **Vigor (+Vida Máxima):** Aumenta permanentemente la barra de vida y la resistencia a errores.
   - 💙 **Flujo (+Maná Máximo):** Amplía la reserva mágica para sostener escudos y encadenar combos y hechizos potentes.
2. **Sin estadísticas frías de Ataque/Defensa:** El daño base de los hechizos es predecible y depende de la ejecución y el punto débil del enemigo. El equipo (túnicas, capas, accesorios) no da números flotantes de ataque/defensa; otorga **rasgos y pasivas de estilo de juego** (ej: reducción de coste de maná en magias de Viento, mayor ventana de aturdimiento en contraataques, bonos de evasión).
3. **Flujo de economía (Esencia y Monedas):** Los combates comunes otorgan Esencia y Monedas (`IT_Esencia`, `IT_Coin`) y orbes de recuperación en caliente. La Esencia acumulada se invierte en los pueblos (tienda y contratos de Renard, sastres y artesanos) para adquirir nuevas prendas y mejoras.
4. **Equipo escaso y significativo:** Las piezas de equipo nunca caen de enemigos comunes al azar; se obtienen como recompensa de jefes, pruebas opcionales o cofres fuertemente custodiados.

### 6.2. Magia

Elementos: **Fuego, Viento (Storm), Luz, Mente (Mind) y Oscuro (Dark)**. Fuego prende lo que arde; Luz activa cristales; Viento mueve molinetes. El resto de reacciones se define por la pieza de puzle, sin prometer una ventaja de daño. No hay hechizos de Hielo.

**Cómo se consiguen (decidido): mixto.** Los hechizos de la historia se aprenden en su momento (tabla de progresión). El resto de **básicos** se encuentran como **páginas del grimorio** repartidas por el mundo (son tesoros, con su requisito). Los **combos de la Y** los enseñan personajes o momentos de la historia; qué personaje enseña cada uno se fija al construir su capítulo.

**Familias:** Will empieza con Llama Astral y desarrolla Luz, manipulación y recursos narrativos de tiempo. Estela combina Fuego y Viento para presión, zonas y apoyo. Liam usa Mente y magia del pacto para preparar y controlar el encuentro. El Mago utiliza Oscuro. Excepciones decididas: Huracán es un hechizo de Viento de Will y se queda. Los hechizos del Mago son arsenal enemigo y no están en ningún grimorio.

**Controles:** X lanza la serie básica; mantenerla permite el modo preciso cuando el hechizo lo admite. Y inicia la composición de un combo y después se introduce la secuencia A/B/X/Y del hechizo. La secuencia vacía no es un combo. Ninguna secuencia puede ser prefijo de otra. El personaje al mando determina qué combos puede lanzar.

### Catálogo de hechizos

Lectura de `Assets/_SPELLS/*.asset` y `MagicSpellSO`, 8 de octubre de 2026. Los números son configuración, no una declaración de balance terminado. En las zonas, el daño es **por tick**. «Sin combo» significa secuencia vacía. Obtención según la regla mixta de arriba.

| Nombre | Personaje | Elemento | Tipo | Daño | Maná | Combo de la Y | Cómo se aprende |
|---|---|---|---|---:|---:|---|---|
| Aura Estelar | Liam | Mente | Proyectil | 30 | 10 | Sin combo | Página del grimorio |
| Bola de Fuego | Estela | Fuego | Proyectil | 10 | 5 | Sin combo | Kit de Estela; punto de obtención no fijado |
| Bola Prisma | Will | Luz | Proyectil | 25 | 10 | Sin combo | Eldran, 2.3; desbloqueo aún no conectado en Cap2 |
| Brisa Sanadora | Estela | Viento | Zona | 0 | 30 | Y → A → A | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Cadenas del Pacto | Liam | Mente | Zona | 4 | 30 | B → X → Y | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Chispa Ígnea | Estela | Fuego | Proyectil | 10 | 10 | Sin combo | Página del grimorio |
| Chispa Ígnea (fuego en el suelo) | Estela | Fuego | Zona | 6 | 0 | Sin combo | Efecto secundario de Chispa Ígnea; no se aprende aparte |
| Corazón Estelar | Will | Luz | Especial | 100 | 25 | X → Y → B | El Rey tras defender el castillo; aún no conectado en los grafos activos |
| Cúpula Estelar | Will | Luz | Zona | 0 | 30 | X → B → A | Primera receta, 2.9; aún no conectado en Cap2 |
| Dardo Mental | Liam | Mente | Proyectil | 12 | 7 | Sin combo | Página del grimorio |
| Eco | Liam | Mente | Proyectil | 20 | 12 | Sin combo | Página del grimorio |
| Estrella Fugaz | Will | Luz | Proyectil | 14 | 8 | Sin combo | Página del grimorio |
| Garra del Pacto | Liam | Mente | Proyectil | 35 | 15 | Sin combo | Página del grimorio |
| Huracán | Will | Viento | Proyectil | 55 | 20 | Sin combo | Página del grimorio |
| Juicio del Pacto | Liam | Mente | Zona | 20 | 40 | B → X → A → Y | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Levitación | Will | Mente | Levitación | 10 | 5 | Sin combo | Campana, 2.10; aún no conectado en Cap2 |
| Llama Astral | Will | Fuego | Proyectil | 10 | 5 | Sin combo | Despertar (Cap1, ID 1) |
| Lluvia de Chispas | Will | Luz | Proyectil | 9 | 9 | Sin combo | Página del grimorio |
| Golpe del Sendero Corrompido | Mago Oscuro | Oscuro | Proyectil | 25 | 20 | Sin combo | Arsenal enemigo (fuera del grimorio); no se aprende |
| Grieta del Sendero | Mago Oscuro | Oscuro | Zona | 15 | 30 | Sin combo | Arsenal enemigo (fuera del grimorio); no se aprende |
| Amanecer | Will | Luz | Especial | 120 | 40 | X → A → B → Y | Batalla final (aún sin asset; valores de partida, se ajustan en el LAB) |
| Meteoro | Will | Luz | Zona | 15 | 30 | X → A → Y | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Muro de Fuego | Estela | Fuego | Zona | 10 | 25 | Y → X → X | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Nova de Luz | Will | Luz | Zona | 30 | 30 | X → Y → A → B | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Paso Sombrío | Liam | Mente | Teletransporte | 0 | 20 | B → Y → B | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Ráfaga | Estela | Viento | Proyectil | 15 | 8 | Sin combo | Página del grimorio |
| Remolino | Estela | Viento | Zona | 6 | 30 | Y → B → X | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Sello del Pacto | Liam | Mente | Zona | 12 | 25 | B → A → X | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Tormenta de Fuego | Estela | Fuego | Zona | 18 | 40 | Y → X → B → A | Lo enseña un personaje o la historia (quién, al construir el capítulo) |
| Ciclón | Estela | Viento | Proyectil | 30 | 5 | Sin combo | Página del grimorio |

Lluvia de Chispas lanza tres estrellas en abanico. Cada estrella cubre un radio de 1,2 m al impactar, visible en su efecto; los tres impactos pueden alcanzar zonas distintas.

### Remates aéreos

Son el tercer golpe de la serie de X en el aire: voltereta y lanzamiento en picado, que deja una zona al caer. No tienen combo de Y ni aprendizaje independiente. Datos de `Assets/_SPELLS/Remates aéreos/`.

| Remate | Personaje | Elemento | Daño por tick | Maná adicional |
|---|---|---|---:|---:|
| Fuego | Estela | Fuego | 5 | 0 |
| Luz | Will | Luz | 10 | 0 |
| Mente | Liam | Mente | 4 | 0 |
| Viento | Estela | Viento | 6 | 0 |

### Magia narrativa y progresión

**Hechizo del Tiempo:** obtenido en la Piedra. Retrocede un intervalo corto sin borrar heridas, agotamiento ni energía gastada por quien lo lanza. Silas demuestra su coste en el taller de cuarenta segundos. En el final, Will retrocede diez segundos una sola vez. Existe en scripts, sin un MagicSpellSO propio.

**Hechizo de Resurrección:** obtenido en la Piedra. Exige entregar voluntariamente una vida para devolver otra recién perdida. Solo funciona dentro del Sendero mientras el cuerpo sigue allí. El altar no puede sustituirlo. Existe en scripts, sin un MagicSpellSO propio. El regreso posterior de Will es la última acción del Archimago, no un uso gratuito del conjuro.

**Amanecer:** Will lo crea en la batalla final con energía ofrecida por Estela. Abre la protección del Mago y, combinado con el arsenal, permite que la barra baje de verdad hasta destruirlo. Después es un combo normal de Y (**X → A → B → Y**; 120 de daño y 40 de maná como punto de partida, a ajustar en el LAB); LT+RT con Estela sigue siendo el dúo. No tiene asset todavía.

| Hito | Aprendizaje de diseño | Evidencia en los grafos activos |
|---|---|---|
| Despertar | Llama Astral | Cap1: `spellsToUnlock: 01000000` = ID 1. El título del nodo aún dice Bola de Fuego; los datos desbloquean Llama Astral. |
| 2.3 | Bola Prisma, con Eldran | Cap2 existe; no contiene todavía este desbloqueo mágico. |
| 2.9 | Cúpula Estelar | Sin desbloqueo conectado en Cap2. |
| 2.10 | Levitación | Sin desbloqueo conectado en Cap2. |
| Defensa del Reino | Corazón Estelar, con el Rey | No conectado en Cap1/Cap2/Secundary. |
| Piedra Ancestral | Tiempo, Resurrección y vuelo | Aprendizaje de historia aún sin conexión en estos grafos. |
| Batalla final | Amanecer | Pendiente de construir, sin asset ni desbloqueo en estos grafos. |
| Rudolfo | Salto | Secundary: nodo «Desbloqueo el Salto». |
| Niño Pez | Nado | Secundary: nodo «Desbloqueo la habilidad de nadar». |
| Silas | Barco y mapas | Entrega decidida, sin evento acreditado en estos grafos. |

El seguimiento de las conexiones pendientes está en TRACKER.md; la tabla distingue evidencia de diseño para no dar por implementado un aprendizaje.

### Papel de las habilidades

| Herramienta | Función de diseño | Uso |
|---|---|---|
| Llama Astral / Bola de Fuego | Proyectil básico de Will / Estela. | Blancos expuestos y objetos inflamables; la precisión permite leer y alcanzar puntos concretos. |
| Bola Prisma y Luz | Castigar aperturas y activar piezas de Luz. | Ventanas del jefe y cristales; sin bonus elemental contra enemigos. |
| Levitación | Mover o elevar objetivos compatibles. | Obstáculos y control de posición; comunicar rango y peso. |
| Ciclón, Ráfaga y Remolino | Presión y control del espacio. | Grupos, molinetes y reposicionamiento. |
| Pacto de Liam | Preparar zonas, atrapar y coordinar ataques. | Amenazas y anclas; el bonus de las anclas es de Liam. |
| Brisa Sanadora / Cúpula Estelar | Curar y proteger al grupo. | Mantener la cooperación sin sustituir la lectura de ataques. |
| Corazón Estelar | Especial ofensivo de Luz de Will que enseña el Rey. | Se queda como está (decidido): no tiene que replicar la barrera de la novela. |
| Dúos y trío | Ataque coordinado con carga de equipo. | Remate del Gólem y oportunidades elegidas por el jugador. |
| Tiempo / Resurrección / Amanecer | Recursos narrativos con reglas propias. | Piedra, Marea y cierre del Sendero; no borran las consecuencias. |

### Equipo y vestuario (decidido 8 oct)

El **armario y el dinero son del grupo**, pero **cada personaje viste y equipa sus propias prendas**, con sus propias estadísticas. Hay prendas comunes y prendas exclusivas de cada personaje. El menú de Equipo elige primero a quién se viste. La apariencia y el equipo de los tres se guardan en la partida.

### 6.3. Puzles

Tres capas: puzles obligatorios de historia; Pruebas opcionales de **5–10 minutos** en zonas sin vuelo; micro-puzles del entorno. Las señales se leen por forma, movimiento o reacción además del color. Fallar una hipótesis ofrece otra pista o un reintento corto.

El kit genérico usa `IReaccionaAMagia`, `ReceptorDeMagia`, `PlataformaTemporal`, `EstadoPersistente`, `RequisitoDeAcceso` / `BarreraConRequisito` y `ZonaQueBloqueaHabilidades` (TDD; INC-705). Permite combinar lo que arde, cristales de Luz, molinetes de Viento, plataformas y requisitos persistentes. El diseño declara qué habilidades bloquea cada zona y comunica cuándo se recuperan.

### 6.4. Tesoros

Disponibles desde el comienzo. Cada tesoro tiene un requisito declarado y una recompensa identificable. Los que exigen Amanecer se alcanzan después del final. Los requisitos de habilidad u objeto abren motivos para volver al mundo; no obligan a regresar al Sendero.

### 6.5. Secundarias

Son opcionales para comprender la historia. Salto y nado se obtienen en ellas. Cada encargo aporta una historia local, mecánica o recompensa perceptible. Evitar muchas entregas iguales y objetos aleatorios para recompensas básicas; compartir progreso cuando dos pedidos usan lo mismo y facilitar el regreso al NPC.

Las variantes de la tabla son propuestas de diseño conservadas; no cambian las quests por aparecer aquí.

| Misión / NPC | Contenido | Variante propuesta |
|---|---|---|
| Roberto — mareo | Poción de vida y recompensa. | Orientación con indicaciones contradictorias y causa investigable; poción como solución rápida. |
| Manuel — abuela | Dos pociones de vida y vestuario. | Cuidado, remedio y una mejora visible o escena familiar, sin forzar compras ya gastadas. |
| Nora — experimento | Tres pociones de maná. | Estabilizar tres focos con precisión y control; pociones como alternativa. |
| Tabernera — resaca | Dos algas y pago. | Ruta costera, elección de calidad/cantidad y preparación cómica del remedio. |
| Tendera — remedios | Intercambio de maná y algas. | Abastecimiento del mercado; comunicar el resultado de guardar o intercambiar algas. |
| Guardia del Bosque | Plaga de arañas. | Localizar nidos, elegir cómo despejar y cerrar con un encuentro; compartir progreso en lugar de veinte recados. |
| Niño Pez | Algas para aprender a nadar; búsqueda de caracola. | Ruta submarina corta con corriente y recompensa de colección; nado sigue siendo secundaria. |
| Rudolfo | Reto de salto y botas. | Enseñar antes del cronómetro, circuito con atajos y puntos de control; recompensa visible. |

**Ritmo:** cada tramo cambia su actividad dominante antes de repetir por tercera vez el mismo patrón. Cada zona define verbo principal, novedad, reutilización, respiro y recompensa. Una habilidad debe cambiar o facilitar una situación; si no aporta durante varios niveles, se revisa antes de añadir otra.

## 7. Mundo y niveles

### 7.1. Maqueta Eldoria

Es una propuesta de composición (INC-188), separada de MainWorld. Una reserva espacial no implica NPCs, navegación, colisiones o misiones conectadas. La Piedra se sitúa en una isla propia al sureste, fuera del Bosque Prohibido, con monolito, ruinas y ruta marítima.

| Necesidad | Diseño espacial |
|---|---|
| Prólogo del valle | Escenario separado; encargos legibles, evacuación, despedida y arena del duelo. |
| Pueblo inicial | Casa de Will, mercado, encuentro con Oliver, discusión Eldran/Victoria y acceso al bosque. |
| Despertar y Demonio 1 | Pradera junto al pueblo, espacio de proyectil y posición protegida de Eldran. |
| Preparación | Comercios y patio de entrenamiento para Erika, Oliver y Pía. |
| Bosque Prohibido | Sendas, aparición de Estela y claro con troncos quemados para el Gólem. |
| Taberna y montaña | Plaza baja, persecución y montaña cuyo aspecto cambia tras la explosión; no es una ruina desde el inicio. |
| Castillo y Demonio 2 | Explanada defensiva, sala del trono y calabozo; espacio para guardias y población. |
| Biblioteca real | Interior de investigación y conversación íntima mediante el espejo de Tobías. |
| Fuego Fatuo y Silas | Bosque oriental con rutas curvas, pueblo vecino, casa y taller temporal. |
| Risco y Vega | Dos aldeas, presa, cauce compartido y rutas agrícolas; ubicación sin fijar en la maqueta. |
| Piedra Ancestral | Isla sureste, ruinas, monolito, cámara del reflejo y arena del Guardián. |
| Sendero y pruebas | Niveles separados de Eldoria; Chuchelandia, feria y Caja con observación exterior de Will y Estela. |
| Tramo final | Preparación, arena del altar y umbral de resurrección; sin conducto de sombra ni biblioteca final. |
| Pueblo Pesquero y playas | Viviendas, embarcadero y barco de Silas; playas legibles sin edificios falsos. |

El diseño exige recorrido continuo a escala del jugador, accesos y arenas claros. Las capturas de composición no sustituyen una prueba de recorrido.

### 7.2. MainWorld

El vestido de MainWorld es una adaptación de juego (TDD §23). La capital es una ciudad amurallada en dos terrazas: Puerta Real al final del camino, Plaza de la Puerta, Plaza del Mercado con la taberna, Escalinata Real y Plaza Real frente al castillo. Dos subidas rodean el talud central para sostener la persecución. La escolta recorre taberna, escalinata y castillo; la Plaza Real alberga la audiencia y defensa contra el segundo Demonio.

La **Laguna de la Era** es natural y de ribera, al sureste de la subida a las granjas. Es ambientación sin misión, presa ni acequias. No sustituye al manantial compartido de Risco y Vega. El marjal sureste queda reservado. La montaña destruida debe respetar la laguna y los recorridos.

## 8. Anexos

- [Registro de cambios](docs/GDD-registro-de-cambios.md).
- [LAB y experimentos de combate](docs/LAB-y-experimentos-de-combate.md): propuestas y protocolos, fuera del diseño cerrado.
- [GDD v2.8 íntegro](<docs/Versiones antiguas/GDD_v2.8_antes-de-la-revision-2026-10-08.md>): referencia histórica.
- [Encargo y decisiones de esta revisión](docs/codex_tareas/GDD_revision_2026-10-08.md).
- [TDD](TDD.md) y [TRACKER](TRACKER.md): detalle técnico y seguimiento.
- Propuesta aprobada del capítulo 2: documento del proyecto de Claude `claude/propuesta-capitulo2-el-camino-elegido-2026-10-06.md`.

## Pendiente de decidir

Cerrado el 8 oct 2026: contrabandistas, extras del capítulo 2, Gólem, Demonio 2, Amanecer, obtención de hechizos, Aura Estelar, arsenal del Mago, Huracán, Corazón Estelar, Pruebas de las Estrellas, enemigos nuevos, grupo en combate y vestuario. Queda abierto solo lo que se decide al construir cada parte:

- **Diseño concreto** de cada templo y mazmorra del post-juego y de cada Prueba de las Estrellas (qué constelaciones, qué recompensas).
- **Qué personaje enseña cada combo de la Y** y dónde está cada página del grimorio: se fija al construir cada capítulo y zona.
- **Valores** (vida y maná de cada personaje, daño y maná de Amanecer, objeto para revivir): se ajustan probando en el LAB.
- **Secundarias:** qué variante de cada una se construye, al llegar a ella.
- **No entran por ahora:** los dones activables (vida infinita…) y el simulacro antes del altar. Se pueden recuperar más adelante.
