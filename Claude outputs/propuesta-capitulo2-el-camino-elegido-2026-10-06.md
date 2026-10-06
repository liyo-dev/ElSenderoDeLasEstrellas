# Propuesta: Capítulo 2, «El camino elegido»

**Fecha:** 6 oct 2026.
**Petición de Raúl:** «Hay que continuar con el capítulo 2, que empieza cuando ganamos al demonio. Primero, un estudio de qué vamos a incluir (gameplay, cinemáticas, puzles, investigación…) y me haces una propuesta».
**Fuentes:**
- La novela canónica (`01_MANUSCRITO_FINAL/El_Sendero_de_las_Estrellas_manuscrito_canonico.txt`), capítulos III a VIII.
- `GDD.md`: § 5 y § 6, la refactorización de gameplay y el banco de combate.
- `Cap1.asset`, `TRACKER.md` y `NpcRoster_MainWorld.asset`.
- La propuesta aprobada el 17 de septiembre (`propuesta-revision-juego-simplificada-2026-09-17.md`).
- El estado actual de assets, localización y `MainWorld.unity`.

**Estado:** aprobada en lo esencial (6 oct 2026): decisiones 1 a 4 cerradas; 5 a 7 pendientes (§ 11). En marcha la tanda 0.

---

## 0. En pocas palabras

En el capítulo 2, Will deja de ser «el chico del pueblo al que le sale fuego de las manos» y se convierte en alguien que **elige su camino y aprende a no recorrerlo solo**. El capítulo sigue este orden:

1. Se prepara en el pueblo.
2. Se despide.
3. Investiga los aros por el camino.
4. Encuentra a Estela y entrena con ella.
5. Vence al Gólem a su lado.
6. Acaba riéndose con Liam por una bota.

El capítulo termina donde acaba la Primera Parte de la novela. Es también donde ya se decidió cerrar la demo (el 17 de septiembre).

- **Duración objetivo:** unos 60 minutos de ruta principal y unos 15 de contenido opcional. Si hay que bajar a 45, en el § 9 se dice qué se recorta primero.
- **Pregunta que guía el capítulo:** **«¿Debes hacerlo?»**. Eldran la hace en el entrenamiento. Vuelve sin que nadie la diga con los contrabandistas, en la prueba de la campana y en el Gólem. No hace falta explicarla: se juega.

---

## 1. Alcance

| | |
|---|---|
| **Empieza** | Al ganar al Demonio. Hoy ahí está el nodo «Los planes de Liam» de `Cap1.asset`. |
| **Termina** | Liam se une al grupo y pierde la bota en el vado. Es el fin de la Primera Parte y el fin de la demo. |
| **Novela** | Del final del capítulo III al VIII. |
| **GDD** | El cierre del § 5 y el § 6 completo. |
| **Siguiente capítulo** | El capítulo 3 empieza en la taberna de la capital, donde Eldran les espera cada mediodía (taberna, montaña, arresto). |

**Por qué el corte va ahí:** es el único punto del tramo en el que el grupo ya está formado y la escena termina con humor. Cortar antes, por ejemplo al encontrar a Estela, deja el capítulo sin jefe y deja el Gólem huérfano al principio del siguiente. Si se ve demasiado grande, se puede partir en 2A (hasta encontrar a Estela) y 2B. Es la decisión 1 del § 11.

---

## 2. Punto de partida: qué hay hoy

### 2.1 Lo que bloquea

- **El capítulo 1 se queda colgado al ganar al Demonio.** El nodo «Los planes de Liam» lanza `LIAM_CRYSTAL_START` y espera `LIAM_CRYSTAL_DONE`. Su sequencer solo existía en `MainWorld_old` y ya no lo escucha nadie: lo comprobé con grep en escenas, prefabs, assets y scripts, y coincide con INC-453. Es la primera escena de este capítulo y lo primero que hay que hacer.
- **No existe `Cap2.asset`.** El capítulo 1 lanza `CH_Cap1_BACK_1` y nadie lo recoge todavía.
- **Liam aparece desde el principio.** En `NpcRoster_MainWorld.asset`, `_LIAM` tiene `startActive: 1` y ningún flag. Con el plan aprobado se une en la última escena, así que necesita `startActive: 0` y un flag (ya estaba apuntado en INC-220). Con Estela pasa lo mismo.

### 2.2 Lo que ya existe y se aprovecha

| Pieza | Para qué sirve aquí |
|---|---|
| Secuencias como datos y **guiones horneados** (`.guion.txt` → `SequenceDefinition`) | Todas las cinemáticas del capítulo |
| `GuiarJugadorNode` + `CharlaEnBocadillos` | Caminar hablando (con Estela, con Liam) |
| `StartQuestNode` con frase «mientras está en curso» | Cumple la regla de que ningún NPC se quede mudo |
| `DialogueChoiceNode`, `BranchFlagNode`, `SetFlagNode`, `ForkNode` | Las decisiones del capítulo |
| `PlaceActorNode` | Eldran se va a la capital y el grafo sabe dónde está cada NPC (resuelve el problema de pruebas que comentaste) |
| `SetTimeOfDayNode` | Noche en la posada y con los contrabandistas, amanecer en la despedida |
| `InputPromptBeat` (mantener, soltar, pulsar a tiempo) | Momentos de interacción dentro de las cinemáticas |
| `UnlockWardrobeItemNode`, `UnlockAbilitiesNode`, grimorio y `PaginaDelGrimorio` | Capa, hechizos nuevos y páginas escondidas |
| Estadísticas de Will + bonos de equipo (`IFuenteDeBonos`) | La capa de Victoria puede dar defensa de verdad |
| **Gólem completo** (INC-489): `Encuentro_Golem1`, `Guion_Golem1_Estela`, armadura frontal, señuelo, tres fases, fusión con el bosque, remate en dúo, 17 frases `EVT_ESTELA_GOLEM_*` | El jefe del capítulo, con tres ajustes (§ 4, bloque 2.11) |
| `GuiaDeCombate`, `SenueloDeCombate`, `RemateObligatorio`, `DuoSpecialAttackSystem` | Estela guía y el remate se hace juntos |
| `PartyMembershipSignal` + roster con `requiredFlag` | Oliver sale del grupo; Estela y Liam entran |
| Bosque Prohibido en `MainWorld`: arañas y el puzle «Sello de las Piedras» (ya con premio) | Exploración y contenido opcional del bosque |
| Zonas de `MainWorld`: `Ambient_WillTown`, `Ambient_VillageByTheSea`, `Ambient_ForbidenWoods`, `Castle` | Pueblo, aldea del camino, bosque y capital (esta última, en el capítulo 3) |
| Prefabs: `_ESTELA`, `_LIAM`, `Erika`, `Victoria`, `WoodsGuard`, `Guard`, `Ladron1/2`, `NiñoPez`, `AmigaDelNiñoPez`, `Verónica`, `Sara`, `Patricia` + el generador de NPCs | El reparto nuevo sin arte nuevo |
| CombatLab | Probar la prueba de la campana y el Gólem antes de montarlos en el mundo |

### 2.3 Lo que no existe

- **Ninguna cinemática de este tramo.** Las antiguas, escritas a mano (`EstelaAppearsSequencer`, `LiamGolemSummon`, `LiamCrystalBallSequencer`, `TabernaSequencer`), ya se retiraron. Se rehacen como guiones horneados.
- **Escenarios:**
  - el interior de la torre de Liam;
  - el claro de la campana;
  - el vado del río;
  - el barro del Gólem;
  - el campamento de los contrabandistas y el puesto de guardia.
  En `MainWorld` no hay ningún río ni puente con nombre: el del bosque lo quitaste porque no quedaba bien.
- **Personajes sin prefab propio:** la boticaria, Pía, Lucía, el posadero, Tobías y el sargento. Todos salen de prefabs que ya existen (lista de arriba).

---

## 3. Cuatro adaptaciones que condicionan el capítulo

1. **El Demonio no es un lobo: su aro roto hace de «trozo de hierro».** En la novela, Will saca un fragmento de una cuneta y Eldran entierra el aro del lobo en un cofre de sal. En el juego no hay cuneta, pero el Demonio lleva su aro de runas (`RuneCollar`). **Propuesta:** Eldran guarda el aro en el cofre de sal y Will se lleva un trozo, que pasa a ser el objeto del bolsillo «Trozo del aro». Es el hilo de investigación de todo el capítulo: se enseña en la posada, Estela lo lee y Liam también (su llave se calienta cuando Will está cerca).
2. **Por qué atacó Liam.**
   - **En la novela,** manda al lobo por error: cree que Will es un ladrón.
   - **En la GDD (La Historia),** Liam «provoca el ataque para despertar su magia».
   - **Encaja la de la GDD,** porque en el juego no hay cuneta ni trozo robado.
   - **Propuesta:** la escena del cristal toma el motivo de la GDD y el tono de la novela. Liam no se ríe. Se levanta de la silla sin darse cuenta cuando Will cae, y se le revuelve el estómago al ver que el chico se pone delante del pueblo.
   - **Textos que cambian:** las líneas actuales (`EVT_LIAM_CRYSTAL_01-03`, que acaba en «Ja… ja, ja…», y `EVT_GOLEM_01`, «Esa estúpida va a arruinar mis planes») son de villano y chocan con su arco. Se reescriben.
   - **Registro:** es una adaptación del juego, no un cambio de canon. Se anota así en la GDD.
3. **Oliver.** No sale en la novela y ahora va en el grupo. **Propuesta:** participa en la preparación como compañero de Will en la prueba de Erika, junto a Pía, y se despide en la salida del pueblo. No viaja. Así, el grupo del final es el de la novela.
4. **La pregunta de Eldran es la lección del capítulo.** El juego ya enseña a atacar. Este capítulo enseña a **parar, avisar y apoyarse en otro**: cuándo no lanzar, cuándo descansar y cuándo dejar que te ayuden. Por eso hay más mecánicas de cooperación y de contención que de daño.

---

## 4. El capítulo, bloque a bloque

### 4.0 Ritmo

| # | Bloque | Tipo | Min | Qué enseña o pone a prueba |
|---|---|---|---|---|
| 2.1 | Los planes de Liam | Cinemática | 1,5 | El jugador sabe más que Will |
| 2.2 | La mañana después | Cinemática con interacción | 3 | Origen de Will (la mujer de la llama) y objetivo: Estela |
| 2.3 | Preparar el viaje | Gameplay, cuatro encargos en el orden que quieras | 13 | Equipo, consumibles, defensa, tiro preciso, Bola Prisma, «¿Debes hacerlo?» |
| 2.4 | La despedida | Cinemática | 2,5 | Brújula y lista de motivos; Eldran se va a la capital |
| 2.5 | Tobías | Cinemática (interludio) | 2 | Para quién busca Liam la cura |
| 2.6 | La aldea del mar | **Investigación** | 6 | Escuchar, inspeccionar y unir pistas |
| 2.7 | Los contrabandistas | Sigilo ligero + **decisión** | 5 | Aplicar la lección sin que nadie la diga |
| 2.8 | El Bosque Prohibido y Estela | Exploración + cinemática cómica | 5 | Qué son los aros: la magia cuya fuerza «no es tuya» |
| 2.9 | La casa entre raíces | Respiro cómico + tutorial | 3 | Combo mágico (primera receta) |
| 2.10 | La prueba de la campana | **Puzle de combate por rondas** | 8 | Gestión de maná, cambio de personaje, avisar, fuego amigo, Levitación |
| 2.11 | El Gólem | **Jefe** | 8 | Todo lo anterior, junto |
| 2.12 | La manzana harinosa | Respiro | 1,5 | Dejar que te ayuden (al revés) |
| 2.13 | La bifurcación, Liam y la bota | Cinemática + gameplay | 4 | Liam entra al grupo. **Fin de la demo** |
| | | | **≈ 62** | |

Alternancia: cine, juego, cine, juego, investigación, decisión, cine, respiro, prueba, jefe, respiro y cine. Nunca hay dos bloques largos del mismo tipo seguidos.

### 2.1 Los planes de Liam (cinemática, 1,5 min)

- **Dónde:** en la torre de Liam, un interior aditivo en coordenadas lejanas, con el mismo patrón que `Prologo_Valle`.
- **Qué se ve:**
  1. Liam mira la bola de cristal: Will no huye, se queda delante del pueblo.
  2. Cuando Will cae, Liam se levanta de la silla sin darse cuenta.
  3. La llave del aro se enfría: el aro se ha roto.
  4. Liam se lo dice a sí mismo: no hace falta que Will le caiga bien; solo tiene que llevarlo hasta el camino.
- **Extensión:** tres o cuatro frases como mucho.
- **Efecto:** sustituye a la escena que hoy cuelga el capítulo 1.

### 2.2 La mañana después (cinemática con interacción, 3 min, en `WillHouse`)

1. **La vela.** `InputPromptBeat` en modo mantener y soltar a tiempo.
   - Si sueltas a tiempo, la mecha prende.
   - Si te pasas, se prende la manga y Will la apaga a manotazos. Es un gag; no hay fallo.
   - Eldran desde la puerta: «Un comienzo prometedor, si tu propósito es declarar la guerra a la ropa».
2. **El bollo de chocolate.** «Perdona por lo del bosque. No debí mandarte solo.» Encaja con el capítulo 1: fue Eldran quien le mandó a por la caja. Will cuenta la pelea, y en cada repetición el Demonio es más grande (dos cortes rápidos de cómic).
3. **«¿Siempre he podido hacer esto?»** Eldran le cuenta lo de **la mujer del carro volcado con una llamita entre los dedos**: «Solo le dio tiempo a pedirme que no te dejara solo. Se lo prometí». Es una semilla del origen de Will y no se explica más.
4. **«Hace cuarenta años me fui con unos amigos… Volví solo.» «Otro día.»** Es una semilla de Silas y Selene.
5. **Eldran saca el aro del cofre de sal.** Nadie sabe leerlo, pero en el Bosque Prohibido vive una hechicera, Estela. Will se lleva el trozo: **objeto «Trozo del aro»**.
6. Will: «Iré». Empieza la misión **«Prepárate para el viaje»**. La encarga Eldran, que tiene frase de mientras tanto.

### 2.3 Preparar el viaje (gameplay, 13 min, en el pueblo)

Son cuatro encargos y se hacen en el orden que quiera el jugador. Cada uno es un paso de la misma misión y cada uno enseña **una cosa distinta**. Ninguno es un recado de ir y volver.

**a) Victoria: la capa (2 min).** Ve la manga quemada y saca la cinta de medir («Quieto»). Le mide los hombros dos veces. Le pide que vuelva cuando haya hecho lo demás, y en el último paso se la da: «Una que no arda cada vez que te enfades».
- **Mecánica:** tutorial del menú Equipo. Las líneas actuales de Eldran sobre el menú se reaprovechan en boca de Victoria.
- **Premio:** la capa da defensa de verdad (`IFuenteDeBonos`). Así no es solo ropa.

**b) La boticaria: la poción y sus límites (2 min).** Le regala **la única Poción de Vida que tiene** y le obliga a repetir sus límites: estabiliza una herida grave, pero no resucita a nadie ni repara el agotamiento mágico.
- **Por qué importa:** es la primera semilla de las reglas del Hechizo de Resurrección.
- **Mecánica:** tutorial de consumibles (cómo usar la poción). La poción es un regalo, no una compra.

**c) Erika: la prueba del pañuelo (4 min).** Will tiene que cruzar la plaza con un pañuelo atado al brazo sin que tres aprendices se lo quiten. Elige equipo: **Oliver y Pía**.
- **Cómo se juega:**
  - Los aprendices lanzan bolas de práctica: se bloquean con el escudo (B) y se puede contraatacar.
  - Si te pones junto a Pía en su esquina, ciega a un aprendiz con su espejo.
  - Oliver conoce los callejones y abre una puerta lateral.
  - Si te quitan el pañuelo, vuelves a la última fuente. No hay derrota.
- **Remate de Erika:** «Tú solo no habrías llegado ni a la fuente».
- **Mecánica:** defensa y contraataque (`PlayerShieldController`) y el valor del grupo.

**d) Eldran: los cubos y las tres piedras (5 min).**
- **Los cubos.** Hay círculos en la tierra y cubos a distintas distancias. Hay que acertar diez veces seguidas **con el tiro preciso** (ya existe en la Bola de Fuego). «Lo ridículo es destruir una pared para encender una vela.»
  - **Premio:** la **Bola Prisma**, «la bola azul, más precisa que el fuego». Hay que comprobar que su efecto se ve azul.
- **Las tres piedras.** Eldran lanza tres piedras encantadas sin avisar. Will detiene dos; la tercera le da siempre (está guionizado). Will dice «Otra vez» y aparece el `DialogueChoiceNode` **«¿Debes hacerlo?»**:
  - «Puedo» → Eldran: «No te he preguntado eso». Repite la pregunta.
  - «No» → Eldran: «Bien. Esa es la buena».

**e) Opcional.**
- **El puente sin magia:** en la zona se bloquea la magia (`PlayerActionManager`). Hay que coger una cuerda y dos clavos para arreglar la tabla. Eldran da dos saltitos encima: «Aguanta». *Necesita un puente que hoy no hay en `MainWorld`.*
- **Los rumores de Estela:** al hablar con vecinos salen seis versiones distintas de quién es Estela, como frases en bocadillo. Cuesta poco y premia hablar con la gente.
- **La panadería:** pagar con la mermelada que nadie quiere. El objeto sale del bolsillo.

### 2.4 La despedida (cinemática, 2,5 min)

- **La noche anterior:**
  - Will escribe sus motivos: **objeto «Lista de motivos»**. Dice: aprender magia, saber de dónde le viene, ver algo que no haya visto Eldran primero y, tras dudar, vivir una aventura.
  - El gag de la manta que entra y sale de la mochila, en un solo plano.
  - Eldran: «Si alguien te ofrece de comer, acepta». Y además: «Yo también me voy, a la capital, a contarle al Rey lo de los aros. Estaré cada mediodía en la taberna de la plaza». Con `PlaceActorNode`, Eldran pasa a la taberna para el capítulo 3.
- **Al amanecer** (`SetTimeOfDayNode`):
  - medio pueblo en la calle con excusas malísimas;
  - Victoria le llena la mochila y le coloca la capa dos veces;
  - Erika: «Huir también es una estrategia»;
  - la boticaria: «No la gastes por una rozadura»;
  - **Oliver se despide y sale del grupo.**
- **Junto al primer árbol,** Eldran le da **la brújula que marca el camino a casa**: es un objeto y además pone un marcador «Casa» en el minimapa. «Puedes volver cuando quieras.» Y le dice «Vuelve».
- **Último plano:** Will se gira en la curva y Eldran sigue junto al árbol.

### 2.5 Tobías (interludio, 2 min, en la torre)

1. Tobías aparece en el espejo de comunicación: «Media sopa. La otra media ha ganado». La tos.
2. «Cuando vuelva iremos a pescar.» «No sabes pescar.»
3. **El dibujo de cuatro figuras pescando,** con la cuarta como una mancha sin rostro. Liam lo cuelga sobre el mapa de hilos, encima del hilo que lleva a Will.
4. Liam coge la llave del aro: «Se calentará cada vez que esté cerca».

El jugador entra al bosque sabiendo algo que Will no sabe. Esa tensión sostiene el resto del capítulo sin explicar nada.

### 2.6 La aldea del mar: «Las marcas de hierro» (investigación, 6 min, en `VillageByTheSea`)

**La posada.**
- «Una sopa, si alcanza.» «Alcanza. Pero no para pan.»
- **Lucía y la cuchara:** `InputPromptBeat` de mantener. La cuchara sube hasta la altura del pulgar y se cae. «Casi.» «No ha sido casi nada.» Es un adelanto de la Levitación, que sale de verdad en 2.10.
- Will pone el trozo del aro sobre la mesa y el posadero se queda muy quieto. **Empieza la misión «Las marcas de hierro».**

**Las tres pistas.** Cada una se desbloquea al escuchar a alguien, no con una flecha:

| Pista | Cómo se consigue | Qué aporta |
|---|---|---|
| El posadero | Diálogo | Luces bajas que se mueven entre los árboles del bosque, hace tres semanas |
| El molino | Lucía dice dónde vio al zorro. Allí se inspeccionan huellas y pelo enganchado en la cerca | Un zorro con aro, asustado, no fiero |
| El establo | Se inspeccionan las marcas en la madera donde el perro se raspó el cuello | Las líneas de dentro del aro «brillaban cuando el animal se movía» |

- **El objeto «Hoja de avistamientos»** gana una línea con cada pista. Se puede leer desde el bolsillo.
- **Al completarla,** el posadero dice: «Lo he contado a dos guardias y a un viajero… Tú traes uno encima».
- **Recompensa:** la hoja se paga en el bloque 2.8, cuando Estela la lee y dice: «Alguien está haciendo muchos». La investigación sirve para algo.
- **Regla:** si te dejas una pista, no se bloquea nada. Estela comenta con lo que lleves.

### 2.7 Los contrabandistas (sigilo ligero y decisión, 5 min, de noche)

1. **El carro.** Will acampa en el camino y oye pasar un carro con jaulas tapadas. Por un hueco de la manta sale una luz blanca que sube y baja como una respiración.
2. **Seguirlo de lejos.**
   - Si te acercas demasiado, el carro se para y alguien pregunta «¿Quién anda ahí?». Toca esconderse en un matorral o volver al último punto.
   - Si te quedas muy lejos, el carro te espera en la siguiente curva.
   - Es sigilo **solo por distancia**, sin conos de visión.
3. **El claro.** Hay cinco contrabandistas, cinco cuchillos, un perro y doce jaulas de pájaros con las plumas encendidas como brasas frías. A Will le sube el calor a las manos y se oye la voz de Eldran: **«¿Debes hacerlo?»**.
4. **La decisión se toma jugando:**
   - **Recomendado: coger la pluma** enganchada en la zarza (objeto «Pluma brillante»), dejar tres piedras apiladas y correr al puesto de guardia.
     - El sargento no se lo cree hasta que ve la pluma. Los guardias siguen al jugador hasta las piedras.
     - En la cinemática, el perro se calma con media hogaza de las que metió Victoria en la mochila, se abren las jaulas y los pájaros suben como chispas. El último se posa en el hombro de Will.
   - **Atacar solo:** hay un combate contra los contrabandistas (`Ladron1/2`). Se gana, pero varias jaulas arden en la pelea.
     - La cinemática cambia: menos chispas, y el sargento lo nota.
     - Queda un flag para más adelante. No hay game over.
5. **Versión barata, si hace falta:** la misma decisión con un `DialogueChoiceNode` en el matorral, sin combate de verdad. Es la decisión 5 del § 11.

### 2.8 El Bosque Prohibido y Estela (exploración y cinemática cómica, 5 min)

**La llegada.**
- Un cartel caído: «NO ENTRAR». Alguien ha añadido debajo, con pintura roja: «SALVO QUE TRAIGAS POSTRE».
- El bosque se recorre siguiendo **troncos chamuscados y marcas de zarpas**, cada vez más recientes. Es una guía visual, sin flecha.
- Se aplica a la vez lo ya aprobado el 5 de septiembre: arañas agrupadas en campamentos, una araña élite y más variedad de árboles.

**La entrada de Estela** (guion horneado, cómico):
1. Una explosión y una araña en llamas que sale volando.
2. La «pobre princesita perdida».
3. El «sapo con vestido».
4. La ráfaga y los guerreros que salen chillando.
5. Will aplaude: «Impresionante».

**Estela lee el trozo del aro:**
- **Qué son:** runas de sometimiento. El collar es la jaula, las runas son la orden y cada aro tiene una llave. «El monstruo no es el Demonio. Es quien le puso el aro.»
- **La magia oscura:** «Hay otra clase que no sale de ti: la sacas de otro… La llaman oscura porque la fuerza no es tuya.» Es la semilla de la magia de pacto de Liam.
- **El diálogo:** «No acepto aprendices.» «He venido a pedir ayuda.» Will le da la hoja de avistamientos: «Alguien está haciendo muchos». Estela acepta: «Eso es hacerte un favor. Y ya me lo cobraré».
- **Estela entra en el grupo.**

**Opcional en el bosque:**
- devolver la bolsa robada a los dos viajeros (1 min);
- el puzle «Sello de las Piedras», que ya existe;
- la plaga de arañas del Guardia del Bosque, rehecha en los tres actos que propone la GDD;
- páginas del grimorio escondidas.

### 2.9 La casa entre raíces (respiro y tutorial, 3 min)

- **El plato que persigue a Will** (opcional, 30 s): el plato lo confunde con una mancha. Se esquiva hasta que se rinde, o Will se sube a la silla. Mientras tanto, Estela se ríe en bocadillos: «¡Uy, qué miedo, un plato! ¡Corre, corre, que te alcanza!».
- **Detalles para inspeccionar:**
  - una carta sin abrir para «Estela Marael» con el remite «Mamá». Will no pregunta: es una semilla;
  - la tabla con sus tres apodos junto a la puerta;
  - la cena quemada.
- Punto de guardado en la cama.
- **Por la mañana,** el cartel «ENTRENAMIENTO DEL FUTURO GRAN HECHICERO», con «futuro» en letra pequeñita.
  - Estela: «El fuego no es una cosa sola: puede empujar, cortar, iluminar o calentar».
  - **Tutorial del combo mágico (Y), con su primera receta: Cúpula Estelar.** Se usa en el bloque 2.11.

### 2.10 La prueba de la campana (puzle de combate por rondas, 8 min)

En un claro hay figuras de barro que no hay que romper y una campana al fondo. Son cuatro rondas y **perder también hace avanzar**, porque esa es la lección.

| Ronda | Qué pasa | Qué enseña |
|---|---|---|
| **1** | Will cruza bajo los ataques de Estela. Si se cubre con un escudo demasiado grande, desvía el fuego hacia las figuras. Estela: «Te has salvado tú y has chamuscado a todos los que tenías que proteger» | Cubrirse con cabeza, sin abarcarlo todo |
| **2** | Si responde a todo con la bola azul, el maná se acaba a mitad de camino. **La respuesta buena es sentarse.** Estela: «¡Que la prueba no ha terminado!». Will: «Pues la pierdo. Prefiero perder a acabar desmayado» | Gestión de maná, y que parar es válido |
| **3** | Se cambian los papeles: **el jugador lleva a Estela** y Will, con IA, le pone obstáculos. Su explosión arrasa las figuras. Will: «Has ganado tú y has chamuscado a todos…» | Cambio de personaje y daño de área |
| **4** | **Juntos.** Las figuras se mueven y piden cosas contradictorias en bocadillos. Estela **anuncia** cada hechizo de área (marca en el suelo). Si no haces caso, te alcanza: es el fuego amigo. Solo se llega a la campana cuando dejáis de competir | Avisar y cooperar |

**En mitad de la ronda 4,** una figura resbala hacia el borde. Aparece un `InputPromptBeat` de mantener: Will quiere que no se rompa y la figura se queda flotando. **Aprende Levitación por instinto.** Estela: «Eso no te lo he enseñado yo». Will: «Ni yo sé qué ha sido».

**El cierre:**
1. Reconstruyen las figuras. Will hace una que se parece a Eldran y Estela le pone orejas enormes.
2. Otra lleva capa con capucha. «¿Y esa quién es?» «Alguien que nos observa desde hace días. Lo noto en la nuca.»
3. Corte a Liam en una loma, que apaga el cristal.

### 2.11 El Gólem (jefe, 8 min)

**Antes del combate.**
- **El camino al Reino** con Estela, charlando en bocadillos sobre el miedo. Estela: «Se aprende. Y si no, siempre te queda irte a vivir a un bosque».
- **Plano de Liam** desde el saliente: arranca la esquirla del cristal, traza el pacto y el Gólem sale de la tierra. Sus líneas se reescriben con el tono del § 3.

**El combate** es el que ya está diseñado y montado (INC-489), con **tres ajustes para acercarlo a la novela**:
1. **El fuego de frente lo alimenta.** Estela abre con una explosión al pecho y el Gólem crece. Will: «Pues habría estado bien saberlo antes». Estela: «Lo sabemos ahora». Es una regla de daño más (`IFiltroDeDano` ya existe) y explica por qué hay que darle por la espalda.
2. **El momento de la barrera.** Al principio pasa algo guionizado: un manotazo tira a Will delante de Estela, ella corta su hechizo a medias para no darle y Will levanta la **Cúpula Estelar** sobre los dos. Debajo de la barrera, Estela dice el plan: «Llévalo hacia el barro». Es el fuego amigo de la campana, ahora en serio.
3. **El barro.** Junto al arroyo hay una zona de barro. Si el Gólem la pisa, se hunde unos segundos. Es la ventana para que Estela le meta fuego por las grietas de la espalda y se vean las runas. Se convierte en la herramienta de la fase 2.

**El remate** sigue como está: sello de invocación y dúo. Lo nuevo es lo que viene después:
- Will queda agotado, con un hilo de sangre en la nariz y cara de cansancio. Estela tiene los dedos agrietados.
- Encuentran una esquirla de cristal entre las rocas: **objeto «Esquirla de cristal»**. Por un instante muestra un rostro.
- **Premio de estadísticas,** igual que el Demonio 1.
- **Último plano de Liam:** llega tarde, se esconde y escribe en su cuaderno: «No volver a utilizar seres vivos».

### 2.12 La manzana harinosa (respiro, 1,5 min)

1. Encuentran un campamento abandonado con las cenizas aún calientes. Es una pista de Liam, para inspeccionar.
2. Estela tropieza y rechaza la ayuda dos veces. **La tercera vez, el jugador se sienta en el camino** (`InputPromptBeat`): lo que se juega es no insistir.
3. Comparten la última manzana, harinosa. «Hacía mucho que nadie se ponía delante de mí. Gracias, idiota.»
4. Punto de guardado.
5. Siguen caminando con Estela apoyada en Will. Van más despacio, como pide la regla de que quien sigue a otro va más lento.

### 2.13 La bifurcación, Liam y la bota (cinemática y gameplay, 4 min)

**La cinemática.**
1. Will y Estela discuten en la bifurcación: el río o los pinos, el mapa o el cielo.
2. **Aparece Liam**, y Estela se pone delante de Will.
3. Estela: «Enséñaselo. No se lo des». Liam lee el trozo desde lejos: «sujeción, obediencia, vínculo».
4. Al ver la esquirla, Liam aprieta dos dedos contra la correa de la mochila. Es un gesto pequeño, para quien se fije.
5. Will: «Podemos viajar juntos hasta el Reino». Estela: «El hierro no lo toca nadie». **Liam entra en el grupo.**

**El atajo.**
- Empieza a llover y cruzan el vado con el agua por las rodillas (más despacio en el agua).
- A Liam se le queda la bota en el barro y la corriente se la lleva. **El jugador corre tras ella** hasta que se engancha entre dos ramas, y se la devuelve.
- A Liam se le escapa una sonrisa. Estela se ríe en la orilla. «El mapa no indicaba el barro.» «Ni que fueras a perder una bota en él.» «Eso no sale en ningún mapa.»

**El plano final:** Estela delante, Will a su lado y Liam detrás, dejando huellas de barro. **Fin del capítulo 2 y fin de la demo.**

---

## 5. Progresión: qué aprende el jugador y dónde lo usa

| Mecánica | Se enseña en | Se pone a prueba en |
|---|---|---|
| Menú Equipo y bonos de ropa | 2.3a, la capa | El resto del juego |
| Consumibles | 2.3b, la poción | El Gólem |
| Defensa y contraataque (B) | 2.3c, Erika | La campana, el Gólem |
| Tiro preciso + Bola Prisma | 2.3d, los cubos | La campana, las runas del Gólem |
| «Parar es válido» y gestión de maná | 2.3d, las piedras | Contrabandistas, ronda 2, manzana |
| Investigar: escuchar e inspeccionar | 2.6 | Estela lee la hoja (2.8) y el capítulo 3 (biblioteca) |
| Sigilo por distancia | 2.7 | Reutilizable más adelante (calabozo) |
| Combo mágico (Y) y Cúpula Estelar | 2.9 | El Gólem (la barrera) |
| Cambio de personaje | 2.10, ronda 3 | El Gólem |
| Avisar y fuego amigo | 2.10, ronda 4 | El Gólem |
| Levitación | 2.10 | Puzles de los capítulos siguientes |
| Dúo (LT) | 2.11, remate | Jefes siguientes |

Así, el Gólem deja de ser «el segundo jefe» y pasa a ser el examen del capítulo.

## 6. Objetos del bolsillo nuevos

| Objeto | Aparece en | Vuelve en |
|---|---|---|
| Trozo del aro | 2.2 | 2.6, 2.8, 2.13 (Liam lo lee) |
| Lista de motivos | 2.4 | Al final del juego (tú decides si se relee en el Sendero) |
| Brújula de Eldran | 2.4 | Siempre: marca «Casa» en el minimapa. En la Ruptura, la «puerta al valle» la hace pesar |
| Hoja de avistamientos | 2.6 | 2.8 |
| Pluma brillante | 2.7 | La prueba para el sargento |
| Esquirla de cristal | 2.11 | 2.13 (reacción de Liam) y la Caja |

## 7. Contenido opcional del capítulo

- **En el pueblo:**
  - el puente sin magia;
  - los rumores de Estela;
  - la mermelada en la panadería;
  - **Rudolfo**, rehecho como circuito de salto con botas, según la GDD.

  El resto de secundarias del pueblo se deja para otros capítulos: la GDD avisa de que repiten la misma estructura.
- **En el camino:** las páginas del grimorio escondidas.
- **En el bosque:**
  - el puzle «Sello de las Piedras» (ya existe);
  - la plaga de arañas del Guardia, en tres actos;
  - la bolsa robada;
  - el plato que persigue a Will.

---

## 8. Qué hay que construir

### 8.1 Piezas nuevas de sistema

Todas son genéricas y valen para cualquier escena. Ninguna lleva nombres del juego en el núcleo.

| # | Pieza | Para qué más sirve | Coste |
|---|---|---|---|
| 1 | **Prueba por rondas**: objetivos que no hay que dañar, una meta, reglas por ronda y la opción de que perder avance | El simulacro de señales del final (GDD 4A) y los entrenamientos de otros capítulos | Medio |
| 2 | **Aviso de área de los aliados + fuego amigo** (mecánica 4, aprobada el 17 de septiembre y aún sin construir) | Todos los combates con grupo, y la Ruptura | Medio |
| 3 | **Seguir sin acercarse**: distancia mínima en `GuiarJugadorNode`, con el aviso «te han visto» y vuelta al último punto | Cualquier persecución discreta | Bajo |
| 4 | **NPCs que siguen al jugador** sin entrar en el grupo (los guardias). Antes, verificar si el seguimiento de compañeros ya lo permite | Escoltas al revés | Bajo |
| 5 | **Objeto con entradas que se añaden por flag** (la hoja, la lista) | Diarios y cartas | Bajo |
| 6 | **Zona que frena a enemigos grandes** (el barro) | Otros jefes | Bajo |
| 7 | **Regla «el fuego de frente le cura o le hace crecer»** sobre `IFiltroDeDano` | Cualquier enemigo con elemento | Muy bajo |
| 8 | **Objeto arrastrado por la corriente** (la bota) | Gags y misiones de agua | Muy bajo |

### 8.2 Contenido

- **Unos 11 guiones horneados:**
  1. Los planes de Liam;
  2. La mañana después;
  3. Las tres piedras;
  4. La despedida;
  5. Tobías;
  6. La posada;
  7. Los pájaros;
  8. La entrada de Estela;
  9. La campana (cierre);
  10. Liam invoca al Gólem y el después;
  11. La bifurcación.
- **`Cap2.asset`**, que arranca con `CH_Cap1_BACK_1`, con un **checkpoint por bloque** para poder probar cualquier tramo.
- **Cuatro misiones:**
  - Prepárate para el viaje;
  - Las marcas de hierro;
  - Encuentra a Estela;
  - Rumbo al Reino.
- **Diálogos y localización ES/EN.** Ningún bocadillo pasa de tres líneas.
- **Entradas del roster:** Estela y Liam con flag, más el reparto nuevo.
- **Escenarios en `MainWorld`:**
  - la posada de la aldea;
  - el molino y el establo;
  - el puesto de guardia y el campamento;
  - el claro de la campana;
  - el barro;
  - el vado.

  La torre de Liam va en una escena aditiva aparte.

### 8.3 Lo que se retira

Siguiendo la regla 3 de `CLAUDE.md`, va a `Versiones antiguas/`:
- las misiones y diálogos viejos que ya no encajan:
  - `DLG_ELDRAN_MISSION5_OFFER` («para que el guardia te deje pasar», «compra la poción en cualquier tienda»);
  - `DLG_ELDRAN_MISSION6_*` («Ha sido pan comido… sobre todo la parte del Gólem»);
  - `Q_ELDRAN_MISSION4`, `Q_ELDRAN_MISSION5_2` y `Q_ELDRAN_MISSION6`, si no se reaprovechan.
- las líneas de villano de Liam, que se reescriben.

---

## 9. Lo que se corta (y que no vuelva)

Se ordena de lo primero que se recorta a lo último:

- Will perdido y el mapa de Lucía con la araña gigante. Queda solo como una frase de Lucía.
- La segunda aldea (las gachas y el canal), los aprendices que van a la capital y la duda de irse con ellos.
- El leñador del hacha helada, el guiso de Will y el diario como mecánica.
- Los cuatro días de fracasos (el cubo congelado, la escoba que le persigue). Como mucho, un plano gag en 2.2.
- Los primeros auxilios de la boticaria quedan en una frase, sin minijuego.
- Si hay que bajar a 45 minutos, se recortan en este orden: el plato que persigue, el puente sin magia, los contrabandistas en versión barata y la ronda 1 de la campana.

---

## 10. Orden de trabajo

Cada tanda deja algo jugable. Claude prepara, Codex ejecuta y Claude revisa. Tú lo pruebas en Unity con pasos concretos. El ajuste fino de cámara se deja para el final, como acordamos.

| Tanda | Qué | Resultado jugable |
|---|---|---|
| **0. Desbloquear** | «Los planes de Liam» como secuencia, esqueleto de `Cap2.asset` con checkpoints, roster (Liam y Estela con flag) y Eldran colocado por el grafo | El capítulo 1 termina sin colgarse y el 2 arranca |
| **1. El pueblo** | Bloques 2.2 a 2.4 | De la mañana después a la despedida, de principio a fin |
| **2. El camino** | Bloques 2.5 a 2.7 + piezas 3, 4 y 5 | Tobías, la aldea con su investigación y los contrabandistas |
| **3. El bosque** | Bloques 2.8 a 2.10 + piezas 1 y 2. La campana se prueba primero en CombatLab | Estela, la casa y la prueba de la campana |
| **4. El Gólem y Liam** | Bloques 2.11 a 2.13 + piezas 6, 7 y 8 | Fin de la demo |

---

## 11. Decisiones

**Cerradas por Raúl (6 oct 2026):**
1. **Alcance:** el capítulo llega **hasta la bota** (fin de la demo). No se parte.
2. **Oliver:** sale del grupo en la despedida y **no sale del pueblo**.
3. **Motivo de Liam:** el de la GDD con el tono de la novela; se reescriben sus líneas de villano y se anota en la GDD como adaptación.
4. **El aro roto del Demonio** hace de «trozo de hierro» de la investigación.

**Pendientes (no afectan a la tanda 0):**
5. **Contrabandistas:** con combate real opcional y consecuencia (recomendado), o versión barata con elección de diálogo.
6. **Fuego amigo:** confirmar que entra ya en este capítulo.
7. **Opcionales:** ¿entran el puente sin magia y el plato que persigue?

**Tanda 0 en marcha:** nivelación de la GDD y filas INC-608 a INC-611 en `TRACKER.md`; escena «Los planes de Liam» (torre de Liam, secuencia, roster con flag); esqueleto de `Cap2.asset`.
