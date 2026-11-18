# IA de combate: cerebro nuevo por puntuación (etapas E1 y E2) — 18 nov 2026

Incidencia: INC-1283 en `TRACKER.md`. Etapas E1 (esqueleto) y E2 (decisión por puntuación) de la propuesta de IA de combate del 13 nov 2026. Este documento es la copia en el repo; falta subirlo al Project (`claude/ia-de-combate-cerebro-nuevo-e1-e2-2026-11-18.md`): el hilo que lo escribió no tenía acceso al Project.

**Aviso:** el hilo tampoco pudo leer los documentos del Project (la propuesta del 13 nov, el de Erika ni el de las correcciones del Cap. 3). Se trabajó con el resumen del encargo. Conviene repasar los pesos y las reglas de abajo con la propuesta delante.

## Estado

- Código escrito. **Sin compilar en Unity**: Unity estaba cerrado y en este hilo no se pudo usar el Roslyn del SDK de Unity (sin acceso a las DLL de Unity). Lo que sí se comprobó: los 16 archivos nuevos compilan con Roslyn (C# 9) contra unos stubs que imitan las firmas de Unity y del proyecto, y los 9 archivos modificados no tienen errores de sintaxis.
- **Sin probar en Play.** Por defecto ningún NPC cambia: todos siguen con el cerebro clásico.

## Cómo se activa en un NPC

1. Abrir su Combat Config (`NPCCombatConfig`) y, arriba del todo, en **🧠 Cerebro de combate**, poner `Cerebro` = **Por Puntuacion**.
2. (Opcional) Crear un perfil: *Create ▸ El Sendero ▸ NPCs ▸ Módulos ▸ Perfil de combate* y arrastrarlo a `Perfil De Combate`. Sin perfil, el cerebro saca carácter y defensas de la propia config, y los ataques de sus tres huecos de hechizo.
3. Los contrabandistas usan la config del pirata (`NPC_Combat_Config_BoyPirate.asset`). Para no cambiar al pirata, lo limpio es duplicarla (p. ej. `NPC_Combat_Config_Contrabandistas`), poner el cerebro nuevo en la copia y asignarla en el campo `Combat Config` del paso de combate del Cap. 3 (`PedirCombateContraNpc`). No se ha hecho: el grafo del Cap. 3 lo lleva otro hilo.

## Qué hay

**E1 · Esqueleto**

- `PerfilDeCombate` (ScriptableObject): catálogo de acciones (`AccionDeCombate`: proyectil, animación, distancia mínima y máxima, segundos de aviso, segundos de recuperación = ventana de castigo, enfriamiento, coste, daño, velocidad, peso, función —Golpe, Presión, Remate— y si se puede devolver); defensas (esquiva, escudo, cubrirse para recargar, radio de cobertura, recarga a cubierto); carácter (agresividad, cautela, movilidad, paciencia, distancias preferida, mínima y máxima, velocidades, atacantes a la vez); dificultad como un solo número. Huecos `fases` y `afinidades` sin efecto todavía (E3).
- Adaptador `PerfilEnUso`: monta el perfil del combate. Si el catálogo está vacío, los tres hechizos de la config entran como acciones (hueco 1 = presión, 2 = golpe, 3 = remate; su enfriamiento, coste, daño y probabilidad como peso). Sin perfil, el carácter sale de la config (agresivo → agresividad; franja de ataque abierta un poco, porque las configs de siempre dan franjas como 3–4 m).
- Convivencia: `ICerebroDeCombate` lo cumplen los dos cerebros. `CombatState` elige uno por el campo `cerebro` y lo apunta en `NPCStateContext.CerebroDeCombate`; `NPCCombatLifecycleHandler` y `NPCManaBarUI` hablan con el que esté peleando. El mapeo de la config a los ajustes del cerebro clásico pasa de `CombatState` a `NPCCombatBrain.AjustesDesdeConfig` (mismo contenido, sin cambios de comportamiento).

**E2 · Decisión por puntuación**

- `CerebroDeCombatePorPuntuacion`: cada 0,15–0,45 s (según dificultad) toma una foto (`SituacionDeCombate`), puntúa todas las opciones de 0 a 1 y ejecuta la mejor. Lo que hace se cambia a medias si otra opción supera a la actual en más de 0,15 (inercia), salvo en pleno ataque o esquiva. Sin el freno fijo de 1,5 s. Un poco de ruido en la puntuación cuanto más baja la dificultad.
- Opciones (una clase cada una, solo hablan con `ICombatiente`): **Atacar** (una por acción del catálogo), **Esquivar**, **Escudo**, **Cubrirse y recargar**, **Recolocarse**, **Rodear**, **Buscar**, **Esperar**.
- Ataque con sus tres tiempos (`LanzadorDeAcciones`): aviso con `CargaDeDisparo` (la bola crece en la mano y sale el aviso del jugador; ahora admite segundos y tipo de aviso por ataque), salida (cobra maná, asigna el lanzador al `EnemyProjectile`, dice si se puede devolver) y recuperación (se queda expuesto). Si le golpean mientras prepara, se corta sin cobrar.
- Conciencia de recursos: con poco maná (umbral según cautela) busca un escondite que le tape de **todos** los personajes del grupo del jugador, comprobado con rayos a pecho y cabeza; recarga allí más deprisa (×1,5); si alguno le ve, descarta ese sitio unos segundos y busca otro (hasta 3 veces); sin escondite, se aleja.
- Lectura del jugador (`LecturaDelJugador`): si está cargando, cuándo lanzó el último, si tiene el escudo, a quién apunta y cuántas devoluciones con la B ha hecho al grupo en 20 s. Esquiva si le cargan apuntándole o si acaba de salir el hechizo; los hechizos que le llegan los esquiva o no según una tirada única por hechizo (probabilidad por dificultad y movilidad): no esquiva todo. Con 2 o más devoluciones recientes baja mucho las acciones devolvibles y prefiere las que no (en el perfil, `sePuedeDevolver`; `EnemyProjectile` ahora puede salir no devolvible y avisa como «apártate»). Con el escudo del jugador arriba, ataca menos con lo devolvible. Los remates suben justo después de que el jugador lance.
- Memoria corta (`MemoriaDeCombate`): última posición vista, golpes recientes, escondites descartados, cuándo atacó, descanso de turno.
- Moverse bien: distancia preferida, puntos con línea de fuego, penaliza rincones (rayos en 8 direcciones) y pegarse a los compañeros; si no hace nada, a veces un paso lateral.
- **Grupo** (`CoordinacionDeGrupo` + `NPCAttackCoordinator`): los turnos de ataque siguen en `NPCAttackCoordinator` (sacado a su propio archivo, generalizado a cualquier cerebro y con limpieza de estáticos). Quien tiene turno presiona; si los turnos están cogidos, los demás **rodean** hacia un lado distinto cada uno (±115°, ±150°… respecto a la línea del que presiona). Tras atacar, deja el turno un rato (según paciencia) para que ataque el otro. Por defecto, 1 atacante a la vez.

## Qué no se ha tocado

Fases (E3), legibilidad completa (E4), Erika (E5), migrar los demás NPCs y retirar el cerebro clásico (E6), cinemáticas y grafo del Cap. 3, `PedirCombateContraNpc` (no hizo falta: el cerebro se elige por la config, y su copia en memoria conserva el campo).

## Prueba para Raúl (no se ha podido hacer)

1. Abrir Unity, dejar compilar y mirar `Logs/Editor.log`: 0 `error CS`. Si hay errores, son de firmas que no se pudieron comprobar sin Unity.
2. Duplicar `NPC_Combat_Config_BoyPirate.asset`, ponerle `Cerebro = Por Puntuacion` y asignarla al paso de combate del Cap. 3 (o, solo para probar, cambiar el campo en la del pirata y devolverlo luego).
3. Probar el combate de los contrabandistas desde ese paso. Seleccionar cada contrabandista en la jerarquía: el campo `Decision Actual` del componente dice qué hace; con `Registrar Decisiones` sale en consola.
4. Mirar: (a) no atacan a la vez; uno lanza y el otro se va hacia un lado; luego se cambian. (b) Cada lanzamiento avisa (bola creciendo, aviso de devolver) y tras lanzar se quedan un momento quietos: se les puede castigar. (c) Golpearles mientras preparan les corta el hechizo. (d) Devolver varias bolas seguidas con la B: dejan de lanzar tanto (con los hechizos del pirata todos son devolvibles, así que lanzan menos y se mueven más). (e) Gastarles el maná: van detrás de una roca o una casa donde no se les ve; si se les rodea, cambian de sitio. (f) No se quedan en rincones ni pegados entre sí. (g) Aturdir y dormir siguen funcionando (se marean y vuelven).
5. Lo mismo con un NPC solo con el cerebro clásico (cualquier otro): debe pelear igual que antes.

## Decisiones pendientes de Raúl

1. **Kit o juego.** Por la regla 9 del `CLAUDE.md`, lo genérico debería ir a un kit (`Kits.CombatAI`, con nombres en inglés). El encargo pedía nombres en español como el resto, así que el cerebro está en `Assets/Scripts/Behaviour NPC/Combate/`, con el núcleo (perfil, situación, opciones, memoria) sin dependencias del juego salvo las lecturas. ¿Se pasa a kit en E6, al retirar el cerebro clásico?
2. **Contrabandistas**: ¿config propia con el cerebro nuevo (recomendado) o se cambia la del pirata?
3. **Proyectiles no devolvibles**: hoy ningún NPC tiene uno. ¿Se crean variantes (otro color, para que se lean) para que tenga sentido «deja los devolvibles»?
4. Recarga ×1,5 a cubierto: es una mecánica nueva; ¿se queda?
5. Código repetido temporal: la búsqueda de cobertura y la línea de fuego existen también en el cerebro clásico. Se van con él en E6.
6. Repasar con la propuesta del 13 nov los pesos de las opciones (este hilo no pudo leerla).

## Archivos

Nuevos (`Assets/Scripts/Behaviour NPC/Combate/`): `ICerebroDeCombate.cs`, `PerfilDeCombate.cs`, `PerfilEnUso.cs`, `CerebroDeCombatePorPuntuacion.cs`, `ICombatiente.cs`, `SituacionDeCombate.cs`, `OpcionDeCombate.cs`, `OpcionAtacar.cs`, `OpcionesDeDefensa.cs`, `OpcionesDeMovimiento.cs`, `LanzadorDeAcciones.cs`, `LecturaDelJugador.cs`, `MemoriaDeCombate.cs`, `CoordinacionDeGrupo.cs`, `SondeoDelTerreno.cs`; y `Assets/Scripts/Behaviour NPC/NPCAttackCoordinator.cs` (sacado de `NPCCombatBrain.cs`).

Tocados: `NPCCombatBrain.cs`, `States/CombatState.cs`, `Common/NPCStateContext.cs`, `Modules/NPCCombatConfig.cs`, `Modules/NPCCombatLifecycleHandler.cs`, `UI/NPCManaBarUI.cs`, `IA/Enemy/CombatTargetProvider.cs` (`ObjetivosEnPie`), `IA/Enemy/EnemyProjectile.cs` (`PermitirDevolucion`), `Combat/CargaDeDisparo.cs` (segundos y tipo de aviso por disparo). Copias de antes en `_ClaudeBackups/ia-combate-e1-e2-2026-11-18/`.
