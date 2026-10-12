# LAB y experimentos de combate

Contenido trasladado del GDD v2.8. Conserva la auditoría y sus hipótesis como referencia de trabajo; no establece el diseño vigente ni acredita la implementación actual. Las decisiones cerradas están en [GDD.md](../GDD.md) y el seguimiento en [TRACKER.md](../TRACKER.md), INC-446. Las menciones históricas a recargas y controles deben contrastarse con TDD.md antes de probar.

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
