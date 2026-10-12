# Encargo: revisión completa del GDD («la biblia del juego, cerrada y clara») — 8 oct 2026

Objetivo de Raúl: «Dejemos la biblia del juego cerrada y las cosas claras». El GDD (`GDD.md`, v2.8) acumula final viejo, estados de implementación, duplicados y tablas desactualizadas. Hay que dejarlo como **documento de diseño limpio y vigente**: qué es el juego y cómo funciona, sin historial ni seguimiento.

## Fuentes y jerarquía
1. Decisiones de Raúl (abajo) y lo ya decidido en el § 19 actual del GDD (batalla final con Amanecer).
2. Novela canónica: `C:\Users\luarb\dev\El Sendero de las Estrellas\Novela\01_MANUSCRITO_FINAL\El_Sendero_de_las_Estrellas_manuscrito_canonico.txt` (7 oct 2026). Regla: **«al final hay que adaptar la novela al juego»**: la novela manda en historia salvo donde el juego haya decidido adaptarla; las adaptaciones se marcan como tales.
3. Datos reales del proyecto: `Assets/_SPELLS/*.asset` (displayName, caster 0 Liam / 1 Will / 2 Estela, element, kind, damage, manaCost, combo), grafos `Assets/NarrativeGraph/Cap1.asset`, `Cap2.asset`, `Secundary.asset`.

## Decisiones de Raúl (cerradas)
- **Final (novela cap. XXVII–XXVIII)**: Liam muere en la batalla final (se interpone ante la hoja de sombra; «Cuida de mi hermano… Gracias»). Will crea Amanecer con la energía de Estela y destruye al Mago (el altar lo protegía de todo hechizo que existía cuando lo pidió; Amanecer no existía). Will pide en el altar la cura de Tobías (conservando su memoria y voluntad, sin que nadie cargue con el daño). El Sendero empieza a cerrarse; Estela carga con Liam hacia el umbral; Will la empuja fuera (la resurrección solo funciona dentro del Sendero), usa el Hechizo de Resurrección para devolver a Liam y destruye el Sendero desde dentro. **Will NO muere al final**: el Archimago, que vivía en él, usa la fuerza que le quedaba para devolverlo a casa y se va con los suyos (aparece ante Estela y Liam: «No soy Will»). Will vuelve («Creo que esta cinta es de alguien»), pierde los recuerdos del Archimago y la estrella de madera (queda en el cielo, torcida). Semanas después pescan con Tobías junto al río; Eldran llega al día siguiente.
- **Deseo del Mago**: «que ningún poder pudiera destruirlo» (no «oponerse»). Unificar en todo el documento.
- **Will no es «la reencarnación»**: el alma del Archimago vive con él («¿Yo soy tú? —No. Somos dos»). Will es Will. No llamar «Will original» al Archimago. Datos de origen según la novela (niño de 4 años cuyo corazón se paró; no recuerda nada anterior a que Eldran lo encontrara).
- **Elementos en combate: opción A**. Los elementos (Fuego, Viento, Luz, Mente, Oscuro) dan identidad a cada personaje y cuentan en los **puzles** (kit de puzles, INC-705: lo que arde, cristal de luz, molinete de viento…). **No hay debilidades ni resistencias elementales en combate.** Los enemigos se vencen por mecánica (ver lista abajo).
- **Hielo se elimina** del juego (no hay hechizos de hielo).
- **Hechizo que enseña el Rey: Corazón Estelar** (adaptación del juego; en la novela es una barrera de energía compartida sin nombre).
- **Chuchelandia y la feria se quedan como están en el GDD** (son las pruebas del Sendero en el juego; adaptación decidida). Chuchelandia está casi construida en Unity (escena «Candyland»), falta trabajarla. La feria necesita arte que Raúl tiene que comprar.
- **Biblioteca del Mago Oscuro (§ 18)**: no está en la novela (lo del sanador cuyo deseo salió mal está en el cap. XVI, con Silas). Mantenerla solo si ya tiene función de juego y marcarla como adaptación; si no, mover su contenido al § de Silas.
- **Guardián de la Piedra**: combate = adaptación del juego (en la novela el Guardián examina a Will y se aparta).
- **Mundo abierto (8 oct)**: tesoros desde el minuto 0 (cada tesoro declara su requisito: ninguno / habilidad / objeto / Amanecer). Barco propio: lo da **Silas** (el barco con el que él, Eldran y Selene fueron a buscar la Piedra, más sus mapas viejos; amarrado en el Pueblo Pesquero). **Vuelo**: Will aprende a levantarse a sí mismo en la **Piedra Ancestral** (cierra el ejercicio de la cuchara del cap. VI); vuelo libre en el mundo, bloqueado solo en zonas de puzle. **El Sendero es solo historia principal**: el umbral es el punto sin vuelta atrás (aviso + guardado); dentro no se vuelve al mundo real hasta terminar o empezar partida nueva. **Post-juego** tras el epílogo (desde la escena del río; Eldran puede abrirlo): templos/mazmorras con guardianes que solo se pasan con Amanecer u objetos; en el post-juego no se entra al Sendero. Posibles «dones» (vida infinita…) por pruebas concretas, activables/desactivables (propuesta, sin cerrar). Propuesta no cerrada: «Pruebas de las Estrellas» con constelaciones.
- **Puzles**: tres capas (puzles de historia obligatorios / Pruebas opcionales 5–10 min con zona sin vuelo / micro-puzles del entorno). Kit genérico en código: `IReaccionaAMagia`, `ReceptorDeMagia`, `PlataformaTemporal`, `EstadoPersistente`, `RequisitoDeAcceso`/`BarreraConRequisito`, `ZonaQueBloqueaHabilidades`.

## Datos a corregir (auditoría)
- Hechizo inicial de Will en Cap1 = **Llama Astral** (spellId 1). **Bola de Fuego (id 29) es de Estela.** Corregir § 4–5 y fichas.
- Grafos activos: Cap1, **Cap2** y Secundary.
- Elementos con los nombres del juego: Fuego, Viento (Storm), Luz, Mente (Mind), Oscuro (Dark). Nada de «Tormenta», «Mental», «Oscuridad».
- Tabla de hechizos: rehacerla desde los `.asset` (faltan 17 hechizos; Bola Prisma es Luz; «Tornado» se llama Ciclón; «Levitation» → Levitación; Mago: «Golpe del Sendero Corrompido» y «Grieta del Sendero»). Sin columna de recarga (no existe). Columnas: nombre, personaje, elemento, tipo, daño, maná, combo de la Y (secuencia si la tiene), cómo se aprende. Los remates aéreos van aparte. Amanecer, Hechizo del Tiempo y Resurrección se describen aunque no tengan asset (Tiempo y Resurrección viven en scripts; Amanecer está pendiente de construir).
- Progresión de desbloqueo vigente (marcar lo no implementado): Llama Astral (Cap1) → Bola Prisma (Eldran, 2.3) → Cúpula Estelar (2.9) → Levitación (2.10) → Corazón Estelar (Rey) → Tiempo y Resurrección (Piedra) → vuelo (Piedra) → Amanecer (final). Saltar y nadar vienen de secundarias; barco de Silas.
- Enemigos y jefes: ficha corta de cada uno (Demonio 1, Demonio 2, Gólem, Araña, Guardián de la Piedra, Mago Oscuro): qué enseña y **cómo se le hace daño** (solo expuesto / aro, armadura frontal del Gólem 20 %, Liam ×2,5 contra anclas, remate obligatorio con dúo, estados a la mitad en jefes…). Plan de enemigos nuevos (propuesta, sin cerrar): fantasmas, Beholder, mímico, Imp ladrón, rocas que embisten; pack gratuito de Dungeon Mason (slime, tortuga).
- Prólogo (§ 1): el duelo actual (cuatro asaltos, agujero negro, plegaria del Archimago, «¡Protégelos a todos!»); quitar referencias a `PrologueDreamSequencer`.
- § 4: dejar una sola versión de qué pasa al perder.
- Ruta del canon: la de arriba (no `Novela/tmp/...`).

## Qué sale del GDD
- **Registro de cambios** (≈ L.612–715) → `docs/GDD-registro-de-cambios.md` íntegro. En el GDD solo una línea con enlace.
- **Todos los «Estado: …» por escena, columnas «Estado actual», «Hallazgos de lectura del texto localizado»** → fuera. Lo pendiente de verdad ya está o debe estar en `TRACKER.md` (no hace falta copiarlo si ya está; si algo no está, añádelo como fila nueva al final de TRACKER con el siguiente INC libre).
- **LAB/CombatLab, auditoría de reutilización, banco de experimentos CMB, dirección de rediseño, cómo evaluar prototipos** → `docs/LAB-y-experimentos-de-combate.md`. En el GDD solo las decisiones de combate ya tomadas.
- **Tablas de papel de habilidades duplicadas**: una sola versión, actualizada.

## Estructura final del GDD
0. Cabecera: versión 3.0, fecha, qué es este documento, jerarquía (decisiones de Raúl > GDD > novela adaptada), qué va en GDD/TDD/TRACKER.
1. Convenciones y reglas de diseño (cada una UNA vez): bocadillos de máximo 3 líneas (si no cabe, se pagina); cuadros de diálogo (A) que no se saltan vs cinemáticas que sí; los personajes no gritan el nombre de los hechizos; doblaje solo en español; interiores sin clima; clima realista (encapotado con lluvia/nieve, transiciones lentas); cada hechizo tiene VFX propio; quien encarga una misión tiene frase mientras está en curso; al seguir a un NPC el jugador va más lento que él; pop-up de objeto conseguido para todo salvo lo ganado en batalla (informe de victoria); arte estilo Quibli.
2. Historia: premisa, la Luz primera y el Sendero, la verdadera historia del Archimago y del Mago Oscuro, resumen del arco con el final correcto.
3. Personajes: fichas (Will, Estela, Liam, Eldran, Silas, Tobías, el Rey, Mago Oscuro, secundarios) + enemigos y jefes.
4. Estructura del juego: capítulos; mundo abierto (islas, barco, vuelo, tesoros); el Sendero como tramo sin vuelta; post-juego.
5. Guion por capítulos (§ 1–21 actuales): solo diseño y adaptaciones, sin estados.
6. Sistemas: combate (decisiones tomadas), magia (elementos = identidad + puzles; familias por personaje; combos de la Y; tabla de hechizos; progresión), puzles (capas y kit), tesoros, secundarias.
7. Mundo y niveles (cobertura espacial, separando maqueta Eldoria de MainWorld).
8. Anexos enlazados.
