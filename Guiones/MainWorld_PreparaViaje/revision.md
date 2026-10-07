# T1-B · Preparación del viaje

## Estado pendiente de Unity

El Editor está detenido en el diálogo modal de validación. El registro del buzón aún termina en 469. Las órdenes 471–472 pertenecen a T1-A y permanecen intactas. Se han añadido 473–477 para captura, refresco e inspección del pueblo; no se ha ordenado todavía montar porque falta elegir el lugar en la captura.

## Código y textos preparados

- Cap2PrepararViaje.cs: menús archivados de inspección, montaje idempotente y verificación. IDs estables; ForkNode conserva otras ramas. No añade el cierre de PREPARA_VIAJE.
- WardrobeItemSO: bonos por datos de cada prenda. ModularAutoBuilder implementa IFuenteDeBonos solo para el cuerpo registrado como jugador; libera registro y suscripciones en OnDisable.
- CompleteQuestStepsNode: frase posterior opcional mediante INarrativeStandingLine y flag persistente proyectable.
- NarrativeFlags y NpcSpawner: evento de cambio para reevaluar rosters condicionados con el mundo ya cargado. La entrada prevista requiere NARRATIVE_FLAG:CAP2_INICIADO.
- NPCBrain: la charla del grafo y su frase posterior tienen prioridad sobre el motor Interactive congelado.
- dialogues_es/en.json y other_es/en.json: diálogo de Victoria, boticaria, frases posteriores, nombre visible y tutoriales. JSON válidos sin duplicados; se conservan los finales LF.

## Nodos previstos

1b. Comienza el Cap. 2 (flag), después de Esperar fin del Cap. 1 y antes del checkpoint inicial.
16. Fork · encargos en paralelo, tras checkpoint 15.
17–21. Hablar con Victoria → diálogo → desbloquear Cloak02 → tutorial Equipo ▸ Capas → completar PREPARA_VIAJE_CAPA.
22–26. Hablar con la boticaria → diálogo → IT_PocionVida ×1 → tutorial Inventario/Usar → completar PREPARA_VIAJE_POCION.

Cloak02 es la capa de mago de principiante; +3 defensa solo equipada. El modelo previsto es Patricia, ausente del roster MainWorld; su única referencia de escena encontrada está en PlayerTest. El prefab propio utiliza el FSM oficial, identidad NPC_Boticaria, configuración ambiental fija, Interactable HandOffToTarget y NarrativeActor.

## Retirada

No se retiran los diálogos antiguos de Victoria: NPC_InteractiveNarrative_Config_Victoria.asset todavía referencia DG_VICTORIA y DG_VICTORIA_BEFORE. Sus claves siguen en uso por esos assets.

## Validación pendiente

Se ha compilado runtime y Editor con Roslyn de Unity y sus respuestas/referencias actuales, salida 0 en ambos. No sustituye la recompilación del Editor abierto ni la ejecución del montaje. El último Editor.log disponible no contiene error CS, pero no refleja esta tanda.

Falta: captura del pueblo, posicion.json (x/y/z/rumbo) elegido visualmente, comprobación NavMesh, montaje por menú, segunda ejecución idempotente, lectura del grafo guardado, medición TMP ≤3 líneas ES/EN, comprobación de iconos y validación cruzada Interactive ↔ Grafo.
