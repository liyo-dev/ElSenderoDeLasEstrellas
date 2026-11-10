# Step types

Each step does one thing when the story reaches it. Fields are set per step; ways out are the named outputs (a step without them simply continues, and several connections from it run in parallel).

## Flujo

### Checkpoint (prueba) (`CheckpointNode`)
Punto desde el que se puede arrancar el juego en el menú de test.
- `checkpointId` (text): Identificador estable y legible: 'ANTES_DEL_PUENTE'.
- `spawnAnchorId` (text): Marca del mundo donde aparece el jugador al arrancar aquí.
- `sceneName` (text): Escena de mundo que debe estar cargada (vacío = la escena por defecto del juego).

### Contador (`ChangeCounterNode`)
Suma a un contador de la historia o le da un valor.
- `counter` (text): Name of the counter.
- `mode` (Add | Set)
- `amount` (whole number): Amount to add (can be negative) or value to set.

### Fork (en paralelo) (`ForkNode`)
Todas las salidas arrancan a la vez y siguen en paralelo.

### Inicio (`StartNode`)
Punto de entrada del grafo.

### Poner flag (`SetFlagNode`)
Guarda un hecho persistente ('PUENTE_REPARADO') consultable con 'Según flag'.
- `flagKey` (text): Clave del flag. Convención: MAYÚSCULAS_CON_GUIONES, sin prefijo.
- `value` (yes/no): true = activar el flag; false = borrarlo.

### Según el momento (`ChooseByMomentNode`)
Elige la salida cuyas condiciones se cumplen; si varias se cumplen, la más concreta.
- `options` (list of Option)
- ways out: A / Si no

### Según flag (`BranchFlagNode`)
Bifurca Sí/No según un flag narrativo (o una señal ya disparada).
- `flagKey` (text): Clave del flag (SetFlagNode) o de la señal a comprobar.
- `invert` (yes/no): Si está marcado, invierte el resultado (Sí ↔ No).
- ways out: Sí / No

## Quests

### Completar pasos de quest (`CompleteQuestStepsNode`)
Marca pasos como hechos y, opcionalmente, cierra la quest.
- `standingActorId` (text)
- `standingDialogue` (NarrativeDialogueAsset asset): Frase del actor cuando se le habla después de este encargo.
- `questId` (text): ID de la quest que se actualizará.
- `stepConditionIds` (list of text): Lista de Step Condition IDs que se marcarán como completados. Ej: 'QUEST_PUENTE_STEP_00'
- `steps` (list of whole number): Lista de índices de pasos que se marcarán como completados. Solo se usa si stepConditionIds está vacío.
- `completeQuest` (yes/no): Cuando está activo, completará toda la quest tras procesar los pasos.
- `logWarnings` (yes/no): Muestra advertencias cuando los datos son inválidos.

### Esperar quest completada (`WaitQuestCompleteNode`)
Se queda esperando hasta que la quest esté completada.
- `questId` (text)
- `debugLogs` (yes/no)

### Iniciar quest (`StartQuestNode`)
Arranca la quest en el sistema de misiones del juego (aparece en el diario). Si se indica quién la encarga, ese personaje dice su frase de «mientras tanto» cuando se le habla con la quest en curso.
- `questId` (text)
- `giverId` (text): Personaje que encarga la quest. Si el jugador le habla mientras la quest sigue en curso y el grafo no está esperando esa conversación, dice el diálogo de abajo.
- `inProgressDialogue` (NarrativeDialogueAsset asset): Lo que dice quien encarga la quest si se le habla mientras sigue en curso (p. ej. una pista de qué hay que hacer).
- `noGiver` (yes/no): Márcalo solo si nadie encarga la quest (se la propone el propio jugador). Si no, toda quest necesita quién la encarga y su diálogo de mientras tanto: ningún personaje se queda sin decir nada.

### Según estado de quest (`BranchQuestStateNode`)
Bifurca por el estado actual de una quest (no iniciada / activa / pasos listos / completada).
- `questId` (text): ID de la quest a consultar.
- ways out: No iniciada / Activa / Pasos listos / Completada

## Diálogo

### Aviso de tutorial (`TutorialPromptNode`)
Muestra un botón/instrucción en pantalla.
- `action` (Show | Hide)
- `text` (text): Puede incluir el token literal "{BOTON}" (ver Core.InputGlyphs.InputGlyphLabels), que se sustituye en tiempo real por el nombre corto de la tecla/botón real según el dispositivo activo (p.ej. "E" en teclado, "A" en Xbox). Solo funciona si 'buttonName' está relleno. NO escribir a mano el nombre de una tecla/botón concreta aquí (p.ej. "Pulsa A..." o "Usa el Joystick..."): sería incorrecto en cuanto el dispositivo activo no coincida con lo escrito — usar el token en su lugar.
- `textId` (text): ID de localización. Si no está vacío, sobreescribe 'text'.
- `icon` (Sprite asset): Icono de botón que aparece a la izquierda del texto (opcional). Si 'buttonName' está relleno, este campo solo se usa como respaldo por si el nombre no resuelve nada para la familia activa.
- `buttonName` (text): Nombre simbólico del botón/acción (constantes en Core.InputGlyphs.InputGlyphNames). Resuelve el icono Y, si 'text' usa el token {BOTON}, también el literal — ambos en tiempo real según el mando/teclado activo. Usar InputGlyphNames.Confirm (no South) para cualquier prompt que dependa de UI/Submit en vez de GamePlay/Interact (p.ej. cualquier "pulsa para continuar" que aparezca con el mapa GamePlay deshabilitado, como en ActionMode.Cinematic — ver PlayerLockService.ApplyHardLock): en mando ambos botones son físicamente el mismo, pero en teclado Interactuar (E) y Confirmar (Espacio/Enter) son teclas distintas, y usar South ahí mostraría 'E' para algo que solo funciona con Espacio.
- `dismissWithCancel` (yes/no): Si está activo, este aviso se cierra con el botón Cancelar (mando: Este/B, PS Círculo, físicamente A en Switch — el mismo que ya usa el proyecto para 'atrás/cancelar', ver PlayerControls.inputactions → UI/Cancel) y con el icono 'X' de la esquina superior derecha (clic de ratón). En TECLADO no se usa Escape para cerrar: esa tecla también abre el menú de pausa (GamePlay/Start comparte tecla con UI/Cancel), así que cerraría el aviso Y pausaría el juego a la vez — en teclado/ratón (PC) este aviso se cierra solo con la X. Pensado para avisos puramente informativos (tutorial de movimiento, de minimapa...): con Confirmar/Interactuar, el mismo botón que avanza el diálogo, el jugador los cerraría sin querer nada más salir de una conversación. Dejar en 'false' (por defecto) para avisos que piden pulsar el botón de una mecánica real (p.ej. 'Pulsa {BOTON} para despertar'): ahí cerrar tiene que seguir exigiendo ESE botón, o el texto dejaría de coincidir con lo que hace falta pulsar.

Con esto activo el grafo no espera a que el jugador cierre el aviso: avanza en cuanto lo muestra. El aviso no bloquea el movimiento ni la interacción, así que esperar a que lo cierre dejaría el grafo parado si el jugador sigue jugando, y perdería las señales que dispare mientras tanto. Ver INC-201d.
- `cerrarConSenal` (text): Señal que significa «ya has hecho lo que dice el aviso» (p. ej. WILL_REACHED_ELDRAN para «Sigue el marcador para encontrar a Eldran»). Al emitirse, el aviso se quita solo. Si ya se había emitido antes de mostrarlo, el aviso ni siquiera sale. No consume la señal: el nodo que la espera la sigue recibiendo. Solo aplica con dismissWithCancel. INC-442.
- `cerrarAlEmpezarCinematica` (yes/no): Quitar el aviso en cuanto empiece una cinemática: el momento ya ha pasado y en mitad de una escena queda raro. Solo aplica con dismissWithCancel. INC-442.
- `cerrarAlEmpezarDialogo` (yes/no): Quitar el aviso cuando se abra cualquier diálogo (p. ej. «habla con quien lleva el icono»: en cuanto hablas, ya está hecho). Solo aplica con dismissWithCancel. INC-442.

### Bocadillo (`ShowSpeechBubbleNode`)
Frase corta sobre la cabeza de un personaje.
- `targetTag` (text): Tag del GameObject sobre el que aparece el bocadillo (ej: 'Player').
- `text` (text)
- `textId` (text): Clave de localización del texto y de su voz. Si está traducida, manda sobre 'text'.
- `duration` (number): Segundos que el bocadillo está en pantalla antes de desaparecer solo (nunca menos de lo que se tarda en leerlo ni de lo que dura su voz). 0 = permanece.
- `waitForCompletion` (yes/no): Si true y duration > 0, el grafo espera a que el bocadillo desaparezca.
- `animTrigger` (text): Nombre exacto del estado/animación del Animator del personaje (ej: 'Talk', 'Wave'). Vacío = sin animación.
- `emphasis` (yes/no): Si true, usa el sprite de énfasis (burbuja explosiva) en lugar del normal.
- `speakerName` (text): Nombre de quien habla. Opcional: se enseña encima del texto cuando la frase sale fija en pantalla o en subtítulo; en el bocadillo con pico, el pico ya señala a quien habla.

### Diálogo (`PlayDialogueNode`)
Reproduce un diálogo (con cámara sobre el actor si se indica uno).
- `dialogue` (NarrativeDialogueAsset asset): El asset de diálogo a reproducir
- `npcId` (text): Id persistente del actor interlocutor (para cámara y animaciones). Si está vacío, el diálogo se muestra sin cámara cinematográfica.
- `oneShotFlag` (text): Si se establece, este diálogo solo se reproducirá una vez. Se guarda en el blackboard del grafo.

### Popup de lore (`ShowLorePopupNode`)
- `config` (LorePopupConfig asset): Configuración del popup: entradas con retrato, hablante y texto.

### Pregunta con respuestas (`DialogueChoiceNode`)
Muestra una pregunta con dos opciones; cada opción es una salida distinta.
- `promptTextId` (text): Clave de localización del texto de la pregunta. Si está vacía se usa 'promptText'.
- `promptText` (text)
- `optionATextId` (text)
- `optionAText` (text)
- `optionBTextId` (text)
- `optionBText` (text)
- `rememberAsFlag` (text): Si se indica, la respuesta se guarda como flag: '{clave}' = eligió A. Deja vacío para no guardar nada.
- ways out: Sí / No

### Texto dramático (`DramaticTextNode`)
Texto a pantalla completa (sueños, títulos).
- `config` (DramaticPhraseConfig asset): Secuencia de frases dramáticas a mostrar.
- `waitForCompletion` (yes/no): Si true, el grafo espera a que terminen todas las frases antes de continuar. Si false, el grafo avanza de inmediato mientras las frases se reproducen en paralelo.
- `restoreSceneMusicWhenDone` (yes/no): Si true, al terminar las frases pide la música del lugar (zona, interior o escena). Si en ese momento otro sistema pone su música (una cinemática que viene detrás), manda la suya.
- `restoreMusicFade` (number): Segundos de fundido al volver la música del lugar.

## NPCs

### Colocar actor (`PlaceActorNode`)
Pone a un actor en una marca del mundo al instante, mirando hacia donde mira la marca. Para usar fuera de cámara o tras un corte. También deja constancia de dónde está al arrancar desde un nodo posterior.
- `actorId` (text): Id persistente del actor, o 'Player' para el jugador.
- `markId` (text): Marca del mundo donde se coloca.
- `applyPlayerEnvironment` (yes/no): Solo para el jugador: aplicar también lo que la marca dicte del lugar (entorno, interior…), sin fundido.
- `showActor` (yes/no): Muestra el actor colocado, también si estaba oculto.

### Esperar entrega de objeto (`WaitItemDeliveryNode`)
Espera a que el jugador le entregue por proximidad un objeto llevado en brazos a un NPC: el objeto desaparece con VFX/SFX, el NPC se gira y hace un gesto, y queda el icono de 'habla conmigo'.
- `npcId` (text): persistenceId del NPC que recibe el objeto (el mismo que usa NPCBehaviourManagerV2).
- `expectedItemType` (Caja | Barril | Bolsa | Paquete | Otro): Tipo de objeto que se espera entregar (ObjectType del PickupObject que lleva el jugador).
- `detectionRadius` (number): Radio de detección de la entrega, en metros, alrededor del NPC.
- `deliveryVfxPrefab` (GameObject asset): VFX de un solo uso al desaparecer el objeto (se reproduce vía VfxPoolService, nunca Instantiate/Destroy directo — ver CLAUDE.md § 2).
- `vfxLifetime` (number): Vida del VFX de entrega, en segundos.
- `deliverySfxKey` (text): Clave de SFX en AudioGraphProfile a reproducir al entregar el objeto (ej: 'Chest').
- `deliveryGesture` (text): Nombre del estado de animación del gesto que hace el NPC al recibir el objeto (ej: 'HandClap01', ya presente en NPC_NoWeapon.controller).
- `questIcon` (GameObject asset): Icono a mostrar sobre la cabeza del NPC tras la entrega, si quieres uno distinto al icono por defecto del NPC. Vacío = usa el 'Default Quest Icon Prefab' del NarrativeActor (recomendado, mismo criterio que WaitNpcInteractionNode.questIcon).

### Esperar hablar con NPC (`WaitNpcInteractionNode`)
Se queda esperando hasta que el jugador interactúa con el NPC indicado.
- `npcId` (text): Id persistente del actor.
- `acceptEarlierInteraction` (yes/no): Si está marcado, una interacción ocurrida ANTES de llegar a este nodo también cuenta (comportamiento sticky). Normalmente NO.
- `questIcon` (GameObject asset): Marca propia de este nodo mientras se espera la charla, si se quiere una distinta de la del actor. Vacío = la marca por defecto del actor (recomendado: se pone una vez en el actor y vale para cualquier nodo).
- `showMarker` (yes/no): Enseñar la marca de «tiene algo para ti» (cabeza y minimapa) mientras se espera. Apagarlo para charlas que el jugador busca por su cuenta (p. ej. un servicio que se puede repetir siempre).

### Guiar al jugador (`GuiarJugadorNode`)
Un NPC camina hasta una marca (SpawnAnchor) y el jugador le sigue con el control, sin poder adelantarle. Si el jugador se queda atrás, el NPC le espera y le llama. Por el camino pueden ir charlando (bocadillos). Avanza cuando los dos están en la marca y la charla ha terminado.
- `npcId` (text): persistenceId del NPC que guía.
- `anchorId` (text): anchorId del SpawnAnchor al que lleva al jugador.
- `puntosDePaso` (list of text): Marcas (SpawnAnchor) por las que pasa antes de ir a 'anchorId', en orden. Sirven para que vaya por el camino que se quiere y no por el atajo que encuentre el NavMesh. Vacío = va directo.
- `llamadaKey` (text): Lo que dice el NPC, en bocadillo, cuando vuelve a por el jugador. Vacío = no dice nada.
- `duracionLlamada` (number): Segundos que dura el bocadillo de la llamada.
- `distanciaMaxima` (number): Si el jugador se queda más atrás que esto, en metros, el NPC se para a esperarle.
- `distanciaReanudar` (number): Cuando el jugador llega a esta distancia, en metros, el NPC sigue andando.
- `separacionDelGuia` (number): Espacio que el jugador conserva detrás del guía mientras camina, en metros.
- `radioLlegada` (number): Cuando el NPC ya está en la marca, el nodo avanza en cuanto el jugador llega a esta distancia de él, en metros.
- `velocidad` (number): Velocidad, en m/s, a la que anda el NPC mientras guía. Si su velocidad normal es mayor, se usa esa.
- `duracionMaxima` (number): Tope de segundos del paseo. Si se agota, el NPC se queda donde esté y se sigue esperando a que llegue el jugador.
- `distanciaMinima` (number): Metros. Si es mayor que cero, el jugador sigue al NPC sin que le vea: si se acerca más que esto, el NPC dice 'avisoAlAcercarseKey' y el jugador vuelve a 'anclaDeVuelta'. Sin tope de velocidad. 0 = escolta normal.
- `avisoAlAcercarseKey` (text): Lo que dice el NPC, en bocadillo, cuando el jugador se acerca demasiado (p. ej. «¿Quién anda ahí?»).
- `anclaDeVuelta` (text): Marca (SpawnAnchor) a la que vuelve el jugador si se acerca demasiado. Vacío = no se mueve.
- `charla` (DialogueAsset asset): Lo que se cuentan por el camino: un diálogo normal (hablante y texto por línea) que sale en bocadillos sobre quien habla mientras el NPC guía. Se pausa si el NPC se detiene a esperar al jugador o hay un menú abierto. Si llegan antes de acabarlo, lo terminan allí. Vacío = van en silencio.

## Señales

### Emitir señal (`RaiseCustomEventNode`)
Emite una señal custom que escuchan nodos, sequencers y triggers.
- `eventKey` (text): Clave del evento a emitir (ej: EVT_MOUNTAIN, EVT_BOSS_DEFEATED)

### Esperar señal (`WaitCustomEventNode`)
Espera una señal custom (sticky: si ya se disparó, pasa de largo).
- `eventKey` (text)
- `acceptEarlierEvent` (yes/no): Si está marcado (por defecto), una señal ocurrida ANTES de llegar a este nodo también cuenta (comportamiento sticky). Si se desmarca, se descarta cualquier emisión previa y solo avanza si la señal ocurre mientras este nodo está esperando.

### Esperar señal N veces (`WaitSignalCountNode`)
Cuenta las veces que llega una señal y avanza al llegar al total. Puede marcar un paso de quest por cada una.
- `eventKey` (text): Señal que se cuenta. Quien la emite debe hacerlo en cada ocasión (no «una sola vez»).
- `count` (whole number): Veces que tiene que llegar la señal para avanzar.
- `questId` (text): Si se indica, cada señal marca como hecho el siguiente paso de esta quest (paso 0, 1, 2…).

## Cinemáticas

### Reproducir cinemática (`PlayCinematicNode`)
Emite la señal de entrada de un Sequencer y espera su señal de fin (Hecho) o de fallo.
- `cinematicName` (text): Nombre legible (solo para el editor): 'Despertar de la estrella', 'Taberna'...
- `signalIn` (text): Señal que escucha el Sequencer (_signalIn). Ej: AWAKEN_START
- `signalDone` (text): Señal que emite el Sequencer al terminar bien (_signalOut). Ej: AWAKEN_DONE
- `signalFailed` (text): Opcional: señal que emite el Sequencer si la secuencia falla/aborta (salida 'Fallo'). Ej: AWAKEN_FAILED
- `secuencia` (SequenceDefinition asset): Si se asigna (con signalIn/signalDone iguales a los suyos), y si ninguna escena tiene un SequencePlayer para esta secuencia, se monta uno en vivo con los ajustes (cámara, audio, transiciones) del SequencePlayer 'plantillaDeAjustes'. Así una secuencia nueva no obliga a tocar la escena.
- `plantillaDeAjustes` (text): Nombre del GameObject del SequencePlayer ya montado del que copiar los ajustes.
- `acercarActor` (text): Actor que se coloca más cerca del jugador ANTES de empezar, fuera de plano, si está a más de 'distanciaMaxima' (para que un «viene hacia ti» no dure medio minuto). Vacío = nada.
- `distanciaMaxima` (number)
- `distanciaDeAparicion` (number)
- ways out: Hecho / Fallo

## Combate

### Iniciar batalla (`StartBattleNode`)
Arranca una arena de batalla y espera la victoria.
- `battleId` (text): ID de la batalla (coincide con BossArenaController.BattleId).
- `bossArena` (BossArenaController asset): Referencia directa a una arena existente en la escena (fallback).
- `useBattleById` (yes/no): Si true, intenta activar por battleId primero.
- `encounter` (BattleEncounterSO asset): Si se asigna, sustituye los datos de spawn configurados a mano en la arena (prefab, nombre, radio, VFX) por los de este ScriptableObject, y fuerza el modo radio alrededor del jugador. Si se deja vacío, la arena mantiene su comportamiento actual sin cambios.
- `spawnProfileIndex` (whole number): Índice del perfil de spawn de la SO a usar (ver BattleEncounterSO.spawnProfiles). Pensado para poder variar por dificultad más adelante.
- `completeMission` (yes/no)
- `missionStringId` (text): ID de misión/quest (string).
- `missionId` (whole number): Entero auxiliar (por ejemplo stepIndex o missionId según tu sistema).
- `arenaContext` (text): Identificador que se usará al suscribirse a OnBattleWon. Si está vacío, se usará battleId o el de la arena.

### Iniciar combate NPC (`StartNpcCombatNode`)
Pone a un NPC en combate contra el jugador y opcionalmente espera a que sea derrotado.
- `npcId` (text): persistenceId del NPC (el mismo que usa NPCBehaviourManagerV2 / NarrativeActor).
- `combatConfig` (NPCCombatConfig asset): Configuración de combate a usar. Si está vacía, se usará la que ya tenga asignada el NPC.
- `defeatEventKey` (text): Clave del evento emitido cuando el NPC es derrotado.
- `waitForDefeat` (yes/no): Si es true, el nodo espera a que el NPC sea derrotado para avanzar.
- `restorePeacefulOnDefeat` (yes/no): Si es true, tras la derrota el NPC vuelve a la capa Interactable y pasa a estado Idle.

## Objetos

### Dar objeto (`GiveInventoryItemNode`)
Añade un objeto al inventario del jugador.
- `item` (NarrativeItemAsset asset)
- `amount` (whole number)
- `logWarnings` (yes/no): Cuando está activo, loguea advertencias si no se encuentra inventario o item.
- `onlyOnce` (yes/no): Sin efecto: el nodo ya es idempotente (lo recuerda en el blackboard). Se conserva por los grafos guardados.

### Esperar objeto (`WaitForItemAddedNode`)
Espera a que el jugador consiga un objeto.
- `itemToWaitFor` (NarrativeItemAsset asset): El objeto que debe entrar en el inventario
- `minimumAmount` (whole number): Cantidad mínima que debe añadirse de una sola vez (0 = cualquier cantidad)
- `completeIfAlreadyOwned` (yes/no): Si está activo, se completa inmediatamente si el objeto ya está en el inventario
- `debugLogs` (yes/no): Activar logs de debug

### Requiere objeto (`RequireInventoryItemNode`)
Comprueba/consume un objeto del inventario; salida alternativa si falta.
- `item` (NarrativeItemAsset asset)
- `requiredAmount` (whole number)
- `consumeOnSuccess` (yes/no)
- `logWarnings` (yes/no)
- `questId` (text): Quest a actualizar cuando el jugador entrega el item.
- `questStepIndex` (whole number): Step a marcar como completado (opcional).
- `autoStartQuestIfInactive` (yes/no): Iniciar la quest si está inactiva antes de marcar el paso.
- `completeQuestInstead` (yes/no): Si se activa, en lugar de marcar un paso se completará toda la quest.
- `useAlternateOutputOnMissing` (yes/no): Cuando es true y falta el item, se salta a la salida alternateOutputIndex (p.ej. para diálogos distintos).
- `alternateOutputIndex` (whole number): Índice de la salida a usar cuando falta el item (por defecto 1).

## Jugador

### Bloquear / soltar jugador (`LockPlayerNode`)
Bloquea o libera el control del jugador.
- `bloquear` (yes/no)

### Desbloquear habilidades (`UnlockAbilitiesNode`)
Desbloquea habilidades y hechizos.
- `abilityKeysToUnlock` (list of Swim | Jump | Climb | Magic | Fly | Sprint | Shield): Habilidades principales (swim/jump/climb/magic/fly) a desbloquear desde el grafo
- `spellsToUnlock` (list of None | Fireball | Plasmaball | CorazonEstelar | Levitation | AuraEstelar | Cycloneburst | GarraDelPacto | Huracan | SelloDelPacto | MagoOscuroGolpe | MagoOscuroGrieta | Meteoro | Rafaga | MuroDeFuego | TormentaDeFuego | JuicioDelPacto | EstrellaFugaz | LluviaDeChispas | ChispaIgnea | Eco | ChispaIgneaFuego | DardoMental | CadenasDelPacto | Remolino | BrisaSanadora | CupulaEstelar | NovaDeLuz | PasoSombrio | BolaDeFuegoEstela | Amanecer): Lista de hechizos a desbloquear desde el grafo
- `assignSpellsToEmptySlot` (yes/no): Si se asignan hechizos, intentar ponerlos en ranuras vacías
- `forceWillBeforeSpellUnlock` (yes/no): Los hechizos dinámicos (aprendidos en runtime, vía UnlockService) solo existen en el preset de Will: UnlockService siempre escribe en runtimePreset, que ES el preset de Will. Liam y Estela no tienen progresión propia — usan un loadout fijo desde su NPCPartyConfig (ver ActiveCharacterSwapper.ApplySpells). Con esta opción activa, si hay hechizos que desbloquear el nodo cambia el personaje activo a Will ANTES de aplicarlos, para que quien se ve en pantalla coincida siempre con quien realmente aprende el hechizo. Cambiar esto de raíz (que lo aprenda quien esté seleccionado) requiere dar progresión propia a Liam/Estela — eso sí es un cambio de arquitectura, no lo toques aquí.
- `oneShotFlag` (text): Si se establece, añadirá este flag al preset (one-shot).
- `applyPresetAfterUnlock` (yes/no)
- `saveAfterUnlock` (yes/no)

### Desbloquear vestuario (`UnlockWardrobeItemNode`)
- `wardrobeItem` (WardrobeItemSO asset)
- `logWarnings` (yes/no)

## Mundo

### Enfocar cámara (`FocusCameraNode`)
Mueve la cámara a un punto de interés.
- `focusId` (text): Id del punto de interés de la escena al que cortar la cámara.
- `holdDuration` (number): Segundos que la cámara permanece en el punto de foco.
- `revealFadeDuration` (number): Duración del fundido de reaparición si la pantalla llega tapada. Si no está tapada no se aplica ningún fundido.
- `waitForCompletion` (yes/no): Si true, el grafo espera a que acabe el plano. Si false, avanza inmediatamente.
- `zoomFactor` (number): Factor de FOV relativo durante el hold. 1 = sin zoom. 0.7 = zoom in (FOV al 70%).
- `zoomInDuration` (number): Duración del zoom in en segundos (0 = instantáneo).
- `zoomOutDuration` (number): Duración del zoom out en segundos (0 = instantáneo).

### Escena aditiva (cargar/descargar) (`AdditiveSceneNode`)
Carga o descarga una escena en aditivo desde el grafo.
- `operacion` (Cargar | Descargar): Qué hacer con la escena.
- `sceneName` (text): Nombre de la escena, sin ruta ni extensión. Debe estar en Build Settings.
- `waitForCompletion` (yes/no): Esperar a que la carga/descarga termine antes de avanzar al nodo siguiente. Déjalo marcado si lo siguiente que hace el grafo depende de que la escena ya esté ahí, que es el caso normal: cargar la escena y justo después emitir la señal que la pone en marcha.

### Fin de la demo (`FinDeLaDemoNode`)
En la build de la demo, guarda y lleva a los créditos. En el juego completo no hace nada y sigue.
- `escenaFinal` (text): Escena a la que se va al terminar la demo.
- `espera` (number): Segundos de espera antes de cambiar de escena (con la pantalla ya tapada).

### Fundido de pantalla (`ScreenFadeNode`)
- `color` (Color)
- `duration` (number)
- `fadeIn` (yes/no)
- `transicion` (ScriptableObject asset): Solo al destapar (Fade In desmarcado). Transición del juego con la que se destapa la pantalla. Vacío = fundido normal. Con transición, 'Duration' es lo que tarda la pantalla en pasar a 'Color' si estaba tapada de otro color.
- `restaurarMusicaDeEscena` (yes/no): Al terminar de destapar, pedir la música del lugar en el que se está.
- `fundidoDeMusica` (number)

### Hora del día (`SetTimeOfDayNode`)
- `targetTime` (AfterNoon | BrightMorning | Cloudy | EarlyDusk | HaloSky | Midnight | Morning | Night | Sunset): Periodo del día al que transicionar.
- `immediate` (yes/no): Si true, aplica el cambio instantáneamente sin transición suave de luz.

### Rótulo sobre la pantalla (`TitleCardNode`)
Texto centrado sobre la pantalla tapada, p. ej. «A la mañana siguiente…».
- `text` (text): Texto de respaldo si no hay ID de localización o no se encuentra.
- `textId` (text): ID de localización. Si no está vacío, sobreescribe 'text'.
- `color` (Color)
- `fondo` (Color): Color con el que se tapa la pantalla si no lo estaba ya.
- `fuente` (TMP_FontAsset asset): Vacío = fuente por defecto del presentador.
- `tamano` (number)
- `aparecer` (number)
- `mantener` (number)
- `desaparecer` (number)

## Audio y cine

### Efecto de sonido (`PlaySfxNode`)
- `clip` (AudioClip asset)
- `volume` (number)

### Música (`PlayMusicNode`)
- `clip` (AudioClip asset)
- `fadeSeconds` (number)

### Parar música (`StopMusicNode`)
- `fadeSeconds` (number)

### Silenciar (`MuteAudioNode`)
- `bus` (Master | Music | Sfx | Dialogue)
- `mute` (yes/no): Activo: silencia el bus. Inactivo: quita el silencio (y cualquier volumen puesto con la misma clave).
- `clave` (text): Quién pide la atenuación. Dos nodos con la misma clave y el mismo bus se pisan entre sí; con claves distintas manda la más baja.

### Volumen (`SetAudioVolumeNode`)
- `bus` (Master | Music | Sfx | Dialogue)
- `volume` (number): Fracción del volumen del jugador (1 = el volumen del jugador, 0 = silencio).
- `clave` (text): Quién pide la atenuación. Dos nodos con la misma clave y el mismo bus se pisan entre sí; con claves distintas manda la más baja.

### Voz (`PlayVoiceNode`)
- `clip` (AudioClip asset)
- `volume` (number)

## Minijuegos

### Minijuego pilla-pilla (`StartTagMinigameNode`)
- `minigameId` (text): ID del minijuego (coincide con TagMinigameController.MinigameId).
- `activateOnStart` (yes/no): Si true, activa el GameObject del minijuego antes de iniciarlo.
- `deactivateOnEnd` (yes/no): Si true, desactiva el GameObject del minijuego al terminar.

## Notas

### Nota (`GraphNoteNode`)
Comentario libre en el grafo. No se ejecuta.
- `note` (text)
- `accent` (Color)

## Otros

### Con Salida De Error De Prueba (`ConSalidaDeErrorDePrueba`)
- ways out: Normal / Error

### Decision De Prueba (`DecisionDePrueba`)
- ways out: Si / No

### Emite Senal De Prueba (`EmiteSenalDePrueba`)

### Espera Senal De Prueba (`EsperaSenalDePrueba`)

### Nodo De Prueba (`NodoDePrueba`)

## Grupo

### Entrar o salir del grupo (`GrupoNode`)
Un compañero sale del grupo (y, si se pide, de la escena) o vuelve a él; o un personaje pasa a ir al mando.
- `accion` (SaleDelGrupo | VuelveAlGrupo | PasaAlMando | EntraEnElGrupo)
- `personaje` (text): Will, Estela o Liam.
- `retirarDeLaEscena` (yes/no): Al salir del grupo, deja también de estar en la escena hasta que vuelva.
