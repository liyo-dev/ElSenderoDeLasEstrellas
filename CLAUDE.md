# CLAUDE.md — El Sendero de las Estrellas

RPG de acción/aventura en Unity 6 (6000.6.2f1) + URP 17.6. Proyecto indie en solitario (Raúl Báez).

**Toda la documentación técnica detallada vive en `TDD.md` — fuente de verdad única.** Este archivo es un resumen corto de lo no negociable para quien (humano o IA) toque código; no sustituye a TDD.md, y si algo de aquí y de TDD.md alguna vez difieren, manda TDD.md. Detalle completo de cada punto de abajo, con ejemplos y contexto: ver la sección de TDD.md indicada entre paréntesis.

**Toda la documentación de diseño y narrativa (historia, guión, fichas de personaje, balance de hechizos y contenido) vive en `GDD.md` — fuente de verdad única, desde el 24 ago 2026.** Sustituye al antiguo Google Doc "GDD" v1.0, que se quedó desactualizado (ejemplo: contradecía una corrección de lore de Liam ya confirmada). Antes de escribir o corregir diálogos, hechizos nuevos, o cualquier ficha de personaje, consultar `GDD.md` primero. **Diseño vigente:** cinco elementos (Fuego, Viento, Luz, Mente y Oscuro), sin resistencias ni debilidades elementales en combate; los elementos sirven a identidad y puzles. Will y el Archimago son dos almas distintas; Will regresa vivo al final. **Filosofía y progresión:** respeto a la inteligencia del jugador (sin pilotos automáticos ni pintura amarilla; combos aprendidos por memoria muscular; jefes-puzle con mecánica y coordinación de grupo); sin niveles numéricos de EXP (Sintonía Estelar da a elegir entre +Vida o +Maná; equipo con rasgos tácticos, no números fríos; encuentros diseñados con situación, nunca enemigos dispersos). **La novela también es canon:** al corregir historia en `GDD.md` o en `C:\Users\luarb\dev\El Sendero de las Estrellas\Novela\01_MANUSCRITO_FINAL\El_Sendero_de_las_Estrellas_manuscrito_canonico.txt` (proyecto complementario), revisar y nivelar el otro documento si corresponde, según la jerarquía de la cabecera de `GDD.md`.

**Todas las incidencias (bugs, mejoras, funciones) viven en `TRACKER.md` — fuente de verdad única, desde el 24 ago 2026.** Sustituye al Tracker en Google Sheets (no se podía escribir en él directamente desde una sesión de IA, solo leer) y fusiona la numeración `INC-XXX` que hasta hoy llevaba también, por separado, el catálogo de bugs de `TDD.md` § 13. Al encontrar o arreglar una incidencia: añadir/actualizar su fila en `TRACKER.md` directamente (mismo commit que el fix), nunca dejarla pendiente de pegar a mano.

---

## 1. Entrada al proyecto (detalle: TDD.md § 1)

- **Escena de entrada:** `Assets/Scenes/Systems/Start.unity` — contiene todos los managers persistentes (`DontDestroyOnLoad`). Siempre debe estar cargada.
- Para testear cualquier escena directamente: abrir y dar Play. `AutoBootstrapOnPlay.cs` carga `Start` aditivamente de forma automática.
- **Script Execution Order crítico:** `GameBootService` -1000 → `PlayerService` -900 → `ServiceLocator` -800 → `WorldBootstrap` +200.

## 2. Reglas de código — no negociables (detalle: TDD.md § 12)

**Nunca en `Update` / `LateUpdate` / `FixedUpdate`:**
`FindObjectOfType`/`FindObjectsByType` (usar registros) · `GetComponent` sin cachear (cachear en `Awake`) · `Camera.main` sin cachear · `new List<T>()`/`.ToList()`/`.Where(...).ToList()` (iterar directo) · `LayerMask.GetMask(...)` sin cachear · `animator.parameters` (getter, cachear hashes) · `StartCoroutine(...)` sin comprobar si ya hay una corriendo · `SetActive(...)` sin guard de estado previo.

**Physics:** siempre `OverlapSphereNonAlloc` con buffer pre-alocado, nunca `Physics.OverlapSphere(...)`.

**VFX de un solo uso** (impacto, explosión, despawn): siempre `VfxPoolService.Instance.Play(prefab, pos, rot, lifetime)`, nunca `Instantiate(...); Destroy(fx, t)` directo.

**Personajes vs. geometría en raycasts:** los personajes no tienen capa propia (viven en `Default`, igual que la geometría). Para distinguirlos en un raycast de obstrucción, comprobar `root.GetComponent<NPCSimpleAnimator>() != null` (lo tienen el player y todos los NPCs, ningún prop de escenario).

**Reflection:** no usar `System.Reflection` en código de runtime frecuente (lento y frágil en IL2CPP).

**Logging:** todo `Debug.Log` de diagnóstico bajo `#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION` (`DEVELOPMENT_BUILD` está obsoleto desde Unity 6.6 —aviso UAC0009— y desaparece en 6.8; en builds lo decide *Player ▸ Managed Code Variant*).

**Idioma:** comentarios, documentación y mensajes de commit **en español**.

## 3. Arquitectura clave (detalle: TDD.md § 2)

Multi-escena aditiva (`Start` persiste siempre) · `ServiceLocator` para singletons globales · ScriptableObjects como datos, nunca lógica · eventos C# (`Action<T>`) para comunicación desacoplada · `DontDestroyOnLoad` solo en managers de `Start`.

**Patrón obligatorio en singletons con estado estático** (evita contaminación entre sesiones de PlayMode en el editor):
```csharp
#if UNITY_EDITOR
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics() { _instance = null; OnMyStaticEvent = null; }
#endif
```

**FSM de NPCs:** `NPCBehaviourManagerV2 → NPCBrain → NPCStateContext → INPCState` (`Assets/Scripts/Behaviour NPC/`).

**Stack de modos del jugador** (`PlayerActionManager`): `PushMode(ActionMode.X)` / `PopMode(ActionMode.X)` — siempre en pareja.

## 4. Invariantes del grafo narrativo — NO violar (detalle y las 5 reglas completas: TDD.md § 10)

El grafo narrativo (`MainNarrative.asset`) y sus runners son `DontDestroyOnLoad`. Antes de tocar el arranque y la carga de la partida (`GameBootService.ReloadTestPreset()`/`PrepararContinuar()`, `Partida.AplicarPartidaCargada`/`ReiniciarSesionNarrativa`), `NarrativeRunner.StartFromStartNode`/`StartFromNode`, `WaitQuestCompleteNode`, la fork detection, o `DefaultNarrativeSignals._raised` — **leer TDD.md § 10 primero**. Son las reglas más fáciles de romper sin darse cuenta y las más caras de depurar después.

## 5. Convivencia Interactive ↔ Grafo narrativo (política completa: TDD.md § 10)

Dos motores narrativos en paralelo a propósito (`NarrativeGraph` activo, `NPCInteractiveNarrativeExecutor` **congelado**, no se fusionan). Todo NPC/quest nueva se construye en `NarrativeGraph`. Antes de dar por buena una entrega, correr `El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)`.

## 6. Bugs conocidos y estado del proyecto

Seguimiento (ID, estado, prioridad) en `TRACKER.md`. Detalle técnico largo (causa raíz, código) de los bugs encontrados en sesiones de IA en TDD.md § 13 (bugs pendientes) y § 19 (auditorías), enlazado desde su fila en `TRACKER.md`. No asumir que un bug de un catálogo viejo sigue vivo — verificar el archivo actual primero.

## 7. Dónde encontrar información

| Qué | Dónde |
|-----|-------|
| Arquitectura completa, API de sistemas, bugs, diseños, auditorías | `TDD.md` |
| Historia, guión técnico, fichas de personaje, balance de hechizos y contenido | `GDD.md` |
| Incidencias (bugs/mejoras/funciones) y su estado | `TRACKER.md` |
| Portada del repo | `README.md` |
| Configuración de NPCs, quests, hechizos | Assets SO en `_NPCs/`, `_QUEST/`, `_SPELLS/` |
| FSM de NPCs | `Assets/Scripts/Behaviour NPC/` |
| Grafo narrativo (núcleo genérico) | `Assets/NarrativeGraph/` |
| El juego conectado al grafo (adaptadores, nodos propios) | `Assets/Scripts/NarrativeIntegration/` |
| Debug visual en runtime | F3 (NPCs), F4 (panel general) |

## 8. Reglas de trabajo para sesiones de IA (desde el 25 sep 2026 — auditoría INC-447)

1. **Assets que Unity puede tener abiertos** (grafos `.asset`, escenas, prefabs): se modifican con un menú de Editor que use `AssetDatabase` (`SetDirty` + `SaveAssets`), **nunca escribiendo su YAML por fuera** — Unity guarda su copia en memoria encima (INC-441).
2. **Antes de crear un sistema, nodo o componente nuevo, buscar el oficial** (tabla de abajo) y extenderlo. No se añade una segunda forma de hacer lo mismo.
3. **Lo que un cambio deja sin uso se quita en el mismo cambio** (a `Versiones antiguas/` de la raíz, fuera de Assets). Nada de dejar huérfanos «por si acaso».
4. **Comentarios: qué hace y por qué, en presente.** Sin diario: nada de fechas, «Raúl: …», «antes/ahora», ni historia de arreglos — eso va en `TRACKER.md`. Como mucho `// Ver INC-###`. Los `[Tooltip]` describen el campo, sin historia.
5. **Logs:** los de diagnóstico se quitan al cerrar la incidencia que los motivó; nunca trazas de pila por defecto; nada de logs por frame.
6. **Compilar antes de entregar:** tras cada tanda, leer `Logs/Editor.log` (errores `error CS`) antes de pedir a Raúl que pruebe.
7. **`PushMode`/`PopMode`, suscripciones y bloqueos** se liberan también en `OnDisable`/`Exit()`: un `finally` de corrutina no se ejecuta si se para o se destruye el objeto.
8. **Menús de un solo uso:** evitarlos; si hacen falta, pasan a `El Sendero/Archivo/` en cuanto se han ejecutado.
9. **Sistemas genéricos = kits reutilizables (regla prioritaria, desde el 11 nov 2026).** Todo sistema, por simple que sea, se escribe genérico para venderlo o llevarlo al siguiente juego; lo concreto de El Sendero va aparte. Detalle y estado de la separación: TDD.md § 31.
   - **Dónde va cada cosa.** Lo genérico, en `Assets/Kits/<Kit>/`, cada kit con su asmdef. Lo del juego, en `Assets/ElSendero/`: `Content/` (jefes, hechizos, personajes, capítulos, scripts de un solo uso), `Data/` (assets del juego, incluidos los grafos `Cap*.asset`) y `Connections/` (enchufes que unen los kits entre sí y con el juego). Mientras un sistema no se haya pasado a `Kits/`, lo nuevo que se le añada ya cumple estas reglas.
   - **Un kit no sabe que existe El Sendero.** Prohibido en el código de un kit: nombres del juego (personajes, capítulos, zonas, hechizos, «Sendero»), IDs o rutas de assets del juego, menús `El Sendero/…` (los kits usan `Tools/<Kit>/…`), condiciones de historia escritas en código, `if` por personaje, escena o capítulo. Lo que cambia entre juegos va en datos (ScriptableObjects, campos del Inspector) o detrás de una interfaz.
   - **Dependencias en una sola dirección.** El juego usa los kits; un kit nunca usa clases de `ElSendero/`. Un kit solo usa otro kit a través de interfaces o eventos de un kit base (Core), nunca sus clases concretas. Sin ciclos: si el asmdef no compila, la regla se ha roto; no se arregla quitando el asmdef.
   - **Nada de sistemas paralelos «del Sendero».** Si el juego necesita algo que un kit no hace, se amplía el kit de forma genérica (opción, dato, interfaz) o se escribe un enchufe en `Connections/`. Prohibido copiar un sistema para personalizarlo, crear un `XDelSendero`, o meter lógica del juego dentro de un kit para salir del paso. Si no se ve cómo hacerlo genérico, se para y se pregunta a Raúl.
   - **Nombres.** En los kits, tipos, namespaces (`Kits.<Kit>`) y API en inglés; comentarios y documentación en español. Lo que ya existe se renombra cuando se pasa al kit, no antes.
   - **Datos serializados.** Al mover a un asmdef o renombrar una clase usada en `[SerializeReference]` (nodos del grafo, beats de secuencias) o en assets, poner `[MovedFrom]` con el namespace y el ensamblado de antes, y comprobar que los assets cargan. Sin eso, los grafos y secuencias pierden los nodos.
   - **El grafo narrativo** sigue además sus propias reglas: SOLID, sin `switch` por tipo de nodo fuera del propio nodo, el juego entra solo por los puertos de `Integration/Ports/`. Objetivo: Asset Store con soporte para IA.
10. **Dónde se trabaja: siempre en este proyecto (`C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas`, rama `main`).** Prohibido crear worktrees, ramas paralelas o carpetas de trabajo aparte para editar: los cambios hechos fuera no llegan a Unity y acaban en un sitio que Raúl no ve. Si una sesión arranca en otra ruta (worktree, `witty-star`, etc.), pasar el trabajo a este proyecto antes de dar nada por hecho. No commitear ni tocar cambios sin commitear que no sean de la tanda actual.

**Sistema oficial por responsabilidad** (lo demás está congelado: se mantiene, no se amplía):

| Responsabilidad | Oficial | Congelado / a retirar |
|---|---|---|
| Post-procesado de escena y cinemáticas | `PostprocesoDeEscena` (+ `PostprocesoBeat`) | Volumes globales que se pisan sin control; valores de look en código |
| Historia, misiones, señales | `NarrativeGraph` (StartQuestNode, CompleteQuestStepsNode, Wait*/Raise*) | `NPCInteractiveNarrativeExecutor`, `NPCQuestConfig`, `NPCQuestActionExecutor`, micro-componentes `Quest*` |
| La partida en curso: preset en memoria, guardar, cargar, partida nueva | `Partida` (`Core/Partida/`: `Preset`, `Guardar`, `Cargar`, `PrepararNueva`, `VolcarEstadoDelJuego`); `GameBootService` decide cuándo; `GameBootProfile` solo datos (TDD § 9) | lógica en `GameBootProfile`; leer o escribir el guardado por otro camino |
| Componentes que solo actúan en un tramo de la historia (oficios, discusiones, barreras) | campo `TramoDeHistoria` (señales y misiones de inicio y fin; `EnCurso()`/`HaAcabado()`; consulta, no escucha) (TDD § 3); objetos que solo están en un tramo (portal abierto, punto usable): `ActivoEnTramo` | campos sueltos «solo si señal/misión»; condiciones de historia escritas en código |
| Dependencias del grafo con el juego | puertos de `NarrativeGraph/Runtime/Integration/Ports/` pedidos con `NarrativeServices.TryGet`; adaptadores y nodos del juego en `Scripts/NarrativeIntegration/` (registro en `NarrativeGameServices`) (TDD § 10) | clases del juego dentro de `Assets/NarrativeGraph/` |
| Qué toca y cómo se ve un nodo en el editor | `NarrativeNode.Describe` (+ `CollectWarnings`, `IsGraphEntry`, `IsAnnotation`, `BranchesInParallel`); extensiones del editor en `Editor/Extensions/` | `switch` por tipo de nodo en ventanas y herramientas |
| Cinemáticas | `SequencePlayer` + `SequenceDefinition`, lanzadas con `PlayCinematicNode` (campo `secuencia` para montarlas en vivo) | `*Sequencer.cs` escritos a mano, `SimpleCinematicDirector` |
| Cámara | `CameraDirectorService` (Claim/Release) | apagar `Camera.main` o `vThirdPersonCamera` a mano |
| Mover / girar NPCs en escenas | `SequenceActor` + `SequenceMovement` | helpers propios en cada sistema |
| Lo que dice un NPC si se le habla y el grafo no espera esa charla (p. ej. misión en curso) | `INarrativeStandingLine` (en `StartQuestNode`: «Mientras está en curso») | `QuestChainEntry.dlgInProgress` de `NPCQuestConfig` |
| Un NPC que colecciona objetos (álbum con páginas y premios) | `ColeccionDeObjetos` (datos) + `Coleccionista` en el NPC (se engancha a `Interactable.OnFinished`, como una tienda) + `ColeccionUI` (menú montado por código con `PiezasDeUI` y un `EstiloDeMenuSencillo`); lo entregado se guarda con `FlagDePartida` | listas de objetos o menús de colección propios en cada NPC |
| Preguntas de Sí / No del grafo (`DialogueChoiceNode`) | `INarrativeChoicePresenter` → `ConfirmationPopupUI` (el popup unificado; la caja de diálogo solo si falta) | `DialogueManager.ShowWithChoices` desde el grafo |
| Qué hace un actor en cada tramo de la historia: si está, dónde y qué cuenta si se le habla cuando el grafo no tiene otra frase | `AgendaDelActor` (assets en `Resources/Agendas/`, entradas con `TramoDeHistoria`) aplicada por `AgendaDeActores` vía `INarrativeActorPresence`; sus frases las da `NarrativeStandingLines` después de las del grafo | `PlaceActorNode` para recolocar gente entre capítulos; activar o desactivar NPCs a mano por flag |
| Ocultar el HUD | `PlayerHUDV2.HideHUD(quien)`/`ShowHUD(quien)` (cada sistema con su clave) | contar ocultados a mano |
| Ocultar el minimapa | MinimapController.Ocultar(quien)/Mostrar(quien) (cada sistema con su clave; HoldToSkipUI lo oculta mientras se ve el icono de saltar) | un bool compartido entre sistemas; SetActive del minimapa a mano |
| Tapar lo que hay en pantalla durante una cinemática (HUD, minimapa, hora) | `IOcultableDuranteCinematica` + el registro `OcultablesDeCinematica`: el sistema se apunta en su `Awake` y se esconde con su clave | llamar al HUD y al minimapa a mano desde la cinemática |
| Seguir a un NPC (escolta jugable) | `GuiarJugadorNode` (tope de velocidad con `TopeDeVelocidadDelJugador`, charla por el camino con `CharlaEnBocadillos`) | escoltas de `NPCInteractiveNarrativeExecutor` |
| Seguir a un NPC sin que te vea (sigilo) | `SeguirSinSerVistoNode` (salidas «Llega» / «Le ve»; vista y oído con `VistaDelVigia`) + `Escondite` (trigger donde no se ve al jugador) + `SigiloDelGrupo` (grupo agachado y más lento, clave por sistema); pillado → `StartNpcCombatNode` con «Pierde» conectada para repetir sin pantalla de derrota | modo «de lejos» de `GuiarJugadorNode`; avisos de «te han visto» en bocadillo |
| Iconos sobre la cabeza | `NPCAlertIconController` (quests vía `NarrativeActor`) | `NPCQuestIconManager`, `NPCPersistentIconController` |
| Texto | `PresentadorDeLinea.Decir` (el camino único de las frases: traduce, elige el estilo, enseña el bocadillo y suelta la voz) sobre `SpeechBubbleUI` (bocadillo o subtítulo según `SayBeat.presentacion` / `SequenceDefinition.presentacionDeTexto`; el subtítulo va siempre en la banda de cine: los guiones con subtítulos llevan `BandasDeCineBeat` y el horneado las pone si faltan; un subtítulo sin bandas sale en bocadillo de habla: con cola sobre quien habla si está en cuadro y, si no, fijo en pantalla con su nombre; los circulitos hasta el nombre solo con `fijoEnPantalla` pedido a propósito) · `DialogueManager` (caja, la avanza el jugador) | `DialogueCameraController` |
| Cómo sale el texto según el momento (decidido por Raúl, 12 nov 2026) | **Cinemáticas: siempre en modo cine**, subtítulo con bandas (`TEXTO subtitulo` en el guion; `SequenceDefinition.presentacionDeTexto` = Subtitulo por defecto). **Bocadillos solo sin cortar el juego**: charlas mientras se sigue a alguien (`CharlaEnBocadillos`), pensamientos sueltos, comentarios del grupo. **Caja de diálogo** (`DialogueManager`) para las charlas que avanza el jugador | `TEXTO bocadillo` en cinemáticas; mezclar bocadillo y cine dentro de una misma cinemática (salvo carteles, como los de los jueces) |
| Avisos en pantalla (misión nueva, barra del jefe, desbloqueos, objetos, rótulos) | `IAvisoEnPantalla` + `ArbitroDeAvisos` (`Pedir`/`Terminar`; un aviso por canal, prioridad, calla con menús y cinemáticas) (TDD § 11) | cada aviso escuchando menús o cinemáticas por su cuenta; colas propias |
| Lo que se ve al caer el jugador (derrota) | `GameOverManager` + `NocheDeLaCaida` (velo de noche, viñeta y estrellas en su propio lienzo; la pregunta de continuar centrada) | destellos o pantallas de derrota propias en cada combate |
| Anunciar el lugar al entrar (primera vez con presentación, luego rótulo) | `PresentacionDeLugares.Anunciar(clave)` + `LugaresConocidos` (TDD § 11); el nombre, en `AmbientPreset.nombreDelLugar` | rótulos o carteles de zona propios; recordar zonas visitadas por su cuenta |
| Comentarios y numeritos del grupo al seguir al jugador | `ComentariosEnReposo` (numeritos y reacciones como datos; frases por `PresentadorDeLinea`) (TDD § 3) | lógica propia en cada `*IdleCommentary` |
| Tiempo (`timeScale`) | `TimeScaleArbiterService` | escribir `Time.timeScale` directamente |
| Situaciones en curso (combate, cinemática…) y quién cede ante ellas | `Kits.Core.GameContext` (`Enter`/`Exit` con clave desde el dueño de la situación, `Has`/`Changed` desde quien cede); vocabulario del juego en `Situaciones`; un aviso en pantalla que deba esperar declara `IAvisoEnPantalla.CedeAnte` (TDD § 32) | `ActiveCombatRegistry.Count > 0 \|\| IsBattleActive` y otros predicados sueltos en cada sistema; `if` de combate dentro de un aviso |
| Botones de combate del jugador (X, LB…) | `PlayerCombatInput` | leer botones de magia en cada sistema; reflexión en `vThirdPersonInput` |
| Defensa y contraataque (B) | `PlayerShieldController` (+ `FiltroDeDefensa`; el escudo lo desbloquea solo el grafo) | leer B en otro sitio; bloquear daño con `if` en cada IA; reglas de historia en el escudo |
| Daño de área de enemigos al grupo | `DanoEnZona` (`GolpearEnCilindro`/`GolpearEnEsfera`; al mando por `PlayerHealthSystem`, compañeros por `CuerpoDelGrupo`) (TDD § 4) | recorrer colisionadores y llamar a `TakeDamage` en cada ataque de zona |
| Combo mágico (Y) | `ComboCastController` (`IsComposing` para callar a los demás lectores) | abrir paneles de secuencia propios; leer A/B/X/Y mientras se teclea |
| Carga de equipo, dúos y trío (LT+RT) | `DuoSpecialAttackSystem` (en `GrupoDelJugador`) | medidores por compañero; ataques de equipo fuera de `SpecialAttackSO` |
| Lanzar un hechizo del jugador | `MagicCaster.Cast` (coste, giro, bloqueo, aire, gesto) → `MagicProjectileSpawner` | lanzar desde otro componente o instanciar el proyectil a mano |
| Música al terminar algo (cinemática, combate, minijuego, texto dramático) | `AudioService.PedirMusicaDelLugar` (relevo con gracia, como la cámara) | `PlayMusic`/`RestoreSceneMusic` directos al acabar |
| Volumen de los buses (música, SFX, diálogo) | `AudioService.SetVolume` = volumen del jugador; bajarlo un rato con `AtenuarBus(clave, bus, fracción)` / `SoltarAtenuacionDeBus(clave, bus)`; la atenuación bajo las voces, solo en `AudioGraphProfile` (TDD § 6) | `Mute(bus)`; escribir el volumen del jugador desde nodos o cinemáticas; valores de atenuación en código |
| Qué deja cambiado un nodo / cómo está el mundo en un nodo | `INarrativeStateEffect` + `NarrativeStateProjector` (TDD § 10) | `switch` por tipo de nodo en herramientas |
| Guardar que un objeto de escena se ha movido o retirado (entregado, consumido) | `ObjetoPersistente` (`Retirar()` para quitarlo; ID con `IdDePersistencia`) | `Destroy` directo de objetos de misión; IDs de posición escritos a mano en cada sistema |
| Voltereta del jugador (desde el suelo o en el aire) | `VolteretaDelJugador` (`DesdeElSuelo`, `EnElAire`) | parábolas o volteretas propias en cada sistema |
| Ropa que se pone encima del vestuario y no se guarda (disfraces de un nivel) | `AtuendoDeEscena` (datos) + `CharacterAppearanceRegistry.PonerAtuendo(quien)`/`QuitarAtuendo(quien)` (también viste a los NPCs del grupo; se guarda la ropa de debajo); por historia, `AtuendoEnTramo` (TDD § 4) | `SetAppearanceFromList` + guardar y devolver la ropa a mano en cada sistema |
| Magia del jugador que sale de otra forma en un sitio (sin daño, otro efecto) | `TransformacionDeMagia` (datos) + `ZonaQueTransformaLaMagia` → `MagicCaster.AnadirTransformacion(quien)` (TDD § 4); zonas del jugador: heredar de `ZonaDelJugador` | `if` por escena o por zona en hechizos, proyectiles o spawner |
| Lanzar al jugador hacia arriba desde el mundo (runas, trampolines) | `LanzadorDeSalto` (`soloDesdeArriba`, `parteQueRebota`; rebote extra por `RasgosDeMovimiento` de la ropa) → `vThirdPersonController.Impulsar` | impulsos o rebotes propios en cada objeto |
| Pruebas que juega el jugador con salida y sin derrota (carreras, escaladas, dianas contra el reloj) | `PruebaDelJugador` (ciclo: salida, cuenta atrás por el árbitro, reintento en el acto, `alGanar`) + `PruebaDeRecorrido` (`RutaDeRecorrido`/`PuntoDeRuta`, rivales `CorredorDeRuta`) o `PruebaDeObjetivos` (con `ActivationCounter`); lo que la acompaña, como etapas `IEtapasDePrueba` (el grupo: `GrupoEnLaPrueba`, también `IHablantesDePrueba`); hacer cosas en orden con el botón de interactuar: `PruebaDePasos` (TDD § 4) | minijuegos con cuenta atrás, reintento y UI propios (`TagMinigameController`); rivales con IA a medida en cada prueba |
| Concursos por rondas sin derrota (rival, jueces con carteles, prueba de botones del jugador) | `ConcursoPorRondas` (una `PruebaDelJugador`; rondas, pasos y carteles como datos, `Guionizada` o `PorRendimiento`) + pruebas de ronda `IPruebaDeRonda` (`EquilibrioEnRonda`, `RitmoEnRonda`, `MantenerEnRonda`) + `ConcursoUI`; botones de cara con el jugador bloqueado: `GamepadInputReader.BotonDeCaraPulsado/Mantenido` (TDD § 4) | marcadores, lectores de botones y HUD propios en cada minijuego |
| Algo que cada personaje del grupo prueba a su manera (interactuar o con magia) | `ReaccionSegunQuienLoPrueba` (reacciones por personaje y forma; `alProbarTodas`); que solo uno pueda usarlo: `WillOnlyInteractable` | `if` por personaje activo en cada objeto |
| Sacar a un compañero del grupo por historia y devolverlo; pasar el mando antes de una cinemática | `GrupoNode` del grafo → `SalidasDelGrupo` / `ControlDelJugador.PasarElMandoA` (TDD § 10) | `NarrativeActionType.JoinParty/LeaveParty` del ejecutor Interactive; desactivar compañeros a mano |
| Retirar de una escena lo que estorba a un montaje (reversible) | `RetiradosDeEscena` (`Retirar`/`Reponer`, registro en la escena; en MainWorld, el de `VestidoDelMundo`) (TDD § 23) | borrar o desactivar a mano lo que estorba |
| Planear | `PlaneoDelJugador` (lo concede `RasgosDeMovimiento.planear`) → `vThirdPersonMotor.SostenerPlaneo` | tocar la gravedad o la velocidad de caída del jugador desde otros sistemas |
| Avisar al jugador del momento justo (devolver, recuperarse, esquivar) | `AmenazasAlJugador` (proyectiles registrados; `Anunciar` para golpes) + `AvisoDeAmenazas` | iconos o textos de aviso propios en cada enemigo |
| Ambiente de noche (ventanas, faroles, estrellas fugaces) | `VentanasIluminadas` (emisión de materiales compartidos), `LuzNocturna` (Light), `EstrellasFugaces`, todos con `DayNightCycle.NocheActual` | encender luces o emisiones por hora en cada sistema |
| Partículas de ambiente fijas en el mundo (luciérnagas, niebla de bosque, humo) | heredar de `ParticulasDeAmbiente` (`LuciernagasNocturnas`, `NieblaNocturna`, `HumoDeChimenea`) | `ParticleSystem` sueltos con su propia activación por distancia |
| Clima (lluvia, tormenta, nieve, viento, niebla) y su efecto en el jugador | `DayNightCycle` + cielo `El Sendero/Cielo` y materiales por franja en `TimeOfDaySettings.skybox`; sol y luna con `DayNightCycle.HoraDelDia`, dibujados por el cielo y publicados por `SolYLunaEnElCielo` (+ `VfxDeClima` si no hay prefab, `ClimaSobreElJugador` para frenar/arrastrar con viento y tiritar con frío); escenas de hora fija sin clima (una franja, ciclo parado, sin sorteo) con `ClimaDelCicloWiring.FijarHoraSinClima` y su cielo propio con `ClimaDelCicloWiring.CieloDeEscena` | partículas de clima, empujes del viento o reacciones del jugador al clima por su cuenta en cada sistema; sortear lluvia fuera del sorteo de `DayNightCycle` (las nubes sueltas solo informan su cobertura); fijar la hora o apagar el clima de una escena con componentes propios |
| Vestir MainWorld (suelos de calle, capital y muralla, pavimento, vegetación, laguna, detalles, ruinas entre zonas) | `VestidoDelMundo` (*El Sendero ▸ Escenario ▸ MainWorld: vestir…*, TDD § 23): lo generado se rehace en cada ejecución, se retoca en sus tablas y reglas; lo que estorba se retira con `Retirar` (registro que *quitar* repone) | colocar a mano bajo «Vestido del mundo (generado)»; generadores de pueblos o plantadores de árboles aparte; borrar o desactivar a mano lo de la escena que estorba |
| Vestir el Sendero (camino de luz, plazas, puertas, altar, cielo) | `VestidoDelSendero` (*El Sendero ▸ Sendero ▸ 2. Vestir el Sendero*, TDD § 28): se retoca en sus tablas; piezas en shaders `Sendero/*` | `SenderoLevelBuilder` (retirado); primitivas o materiales sueltos en `Sendero.unity` |
| Portales con vista al otro mundo (la ventana ovalada con el otro lado dentro) | `VentanaDePortal` + shader `El Sendero/Ventana de portal` (panorámica de `VistaDelPortal`, *El Sendero ▸ Sendero ▸ Vistas de los portales*; en la cinemática final, cámaras en vivo) | VFX de portal de packs (Hovl *Portal blue*) como portal de la historia |
| Hacer desaparecer una escena pieza a pieza (luces que se apagan, objetos en polvo de luz) | `DesvanecerEscena` + `DesvanecerEscenaBeat` | desactivar objetos a mano desde una cinemática |
| Vida, maná y caídas de cada personaje del grupo; quién va al mando | `SaludDelGrupo` (vida y maná por personaje, caer, relevo del mando, levantarse) + `ControlDelJugador` (quién va al mando y qué cuerpo tiene cada uno) | preguntar a `ActiveCharacterSwapper.HiddenNpc` / `WillNpcInstance`; vida de compañeros por su cuenta |
| Tesoros (cofres y puntos con requisito, registro, mapas) | `Tesoro` + `RequisitoDeAcceso`; `RegistroDeTesoros` / `CatalogoDeTesoros`; `MarcaDeMapaDelTesoro` | `ChestInteractable` + `WorldPickup` como cofre |
| Abrir la protección de algo con un hechizo (Amanecer: jefes, cristales, sellos, guardianes) | `IProteccionQueSeAbre` + `ProteccionQueSeAbre` (campo `abreProteccionSegundos` del `MagicSpellSO`; con `SoloDanoCuandoExpuesto`) | flags o `if` a medida por jefe o sello |
| Enemigos del mundo (no jefes) | heredar de `EnemigoDelMundo` (objetivo con `CombatTargetProvider`, daño con `Damageable`, avisos con `AmenazasAlJugador`, Esencia con `OrbDropper`); reglas de daño propias como `IFiltroDeDano` (`ReveladoPorLuz`…) | IA de enemigo escrita desde cero en cada uno |
| Retratos del grupo en el HUD (busto, reacciones, retratos vivos) | `RetratoDelGrupo` + `IFuenteDeRetrato` (`JuegoDeRetratos` horneado con *Hornear retratos del grupo*, `EstudioDeRetratos` en vivo); la vida y las caídas solo vía `EstadoDelGrupoParaRetratos` (TDD § 11) | sprites, alfas o imágenes de IA puestos a mano en `PartyHUDPanel` |
| Oficios ambientales de NPCs (mover cajas de un lado a otro, puestos con ida y vuelta) | `NPCWorkRoute` (con su `tramo`) + `WorkState` + `NPCAmbientConfig.permitirTrabajo`/`puestoId` (TDD § 3) | IA de oficio escrita desde cero en cada NPC; activar el wander para que no estén quietos |
| Aspecto de dibujo de personajes y escenario (dos tonos, sombra con color, brillo de pelo) | shader `El Sendero/Dibujo` + ajustes globales en `AspectoDeDibujo` (*El Sendero ▸ Aspecto de dibujo*); materiales nuevos o de packs nuevos, con *Pasar los materiales al aspecto de dibujo* (`PasoAlAspectoDeDibujo`); el escenario de color liso lleva pinceladas (`_PINCEL`, *Pinceladas en el escenario de color liso*); la luz de cada franja del día, con *Mundo ▸ Luz por franja: paleta de dibujo*; el postprocesado general que le acompaña, con `PostprocesoDeDibujo` (*Postprocesado: pasar al de dibujo*, con vuelta atrás) (TDD § 26.4) | materiales nuevos con Quibli Stylized Lit o URP Lit; valores de luz o sombra del look en cada material o en código |
| Tono de piel de un NPC | tabla de `TonosDePiel` (*El Sendero ▸ Personajes ▸ Tonos de piel de los NPCs*; también al final de *Caras de los NPCs*); el tono del pack, en `PersonajeHechoEnBlender.PielDelPack` | materiales o texturas de piel copiados a mano por NPC |
| Poses sostenidas de actuación para las cinemáticas (brazos en jarras, mano al pecho, ofrecer, cabizbajo…) | tabla de `PosesDeActuacion` (*El Sendero ▸ Personajes ▸ Animaciones ▸ Poses de actuación…*), sobre `PoseSobreClip`; en el guion, `bucle <Estado>` | un archivo de Editor por pose; poses puestas girando huesos desde una cinemática |
| Terreno y agua con el aspecto de dibujo | `El Sendero/Terreno de dibujo` (material del terreno; cuánto se ve la textura, en `AspectoDeDibujo.texturaDelSuelo`) y `El Sendero/Agua de dibujo` (bandas por hondura, espuma con tinta, destellos); se pasan con *El Sendero ▸ Aspecto de dibujo ▸ Terreno y agua: pasar al dibujo* (INC-1137) | URP Terrain/Lit; aguas de packs (`ithappy/WaterURP`) o el agua de la maqueta |
| Contorno de los modelos (siluetas y pliegues) | `ContornoDeTinta` (Volume) + `ContornoDeTintaFeature` en `PC_Renderer`: un pase de pantalla que lee profundidad y normales (TDD § 26) | outline por geometría de Quibli (`_OutlineEnabled`, inverted hull) en materiales del mundo |
| Niños (talla y cuerpo) | cuerpo de niño propio: `CuerpoDeNino` (esqueleto del pack con proporciones de niño, *Tools/Modelos/Ninos*, tallas mayor y pequeña; la cabeza no cambia) + `TallaDeNino` con cuerpo propio (su altura, para el bocadillo); cada niño, una entrada de `NinosHechosEnBlender` | encogerlos escalando la raíz (`TallaDeNino` sin cuerpo propio: solo mientras un niño no tenga el suyo) |
| Caras de los personajes (ojos con cejas y bocas por emoción, símbolos de anime) | juegos de piezas `Eye<NN>`/`Mouth<NN>` y fotogramas de símbolos `Fx<NN>` (lágrimas, sudor, vena, susto; los pasa `NPCEmotionController` desde `EmotionMeshData.simbolos`) hechos con *Tools/Modelos/Caras* (`caras_set.py`, estilo A «anime sencillo» por defecto desde INC-918 (`ESTILO_OJO`); kit de NPCs con mejillas y cejas por juego en `kit_npcs.py`) + perfil `EmotionProfile` por juego; ojos cerrados: emoción `Asleep` o `AplicarOjos` con los del pack («Eye09», se traducen a los del perfil); protagonistas con `CarasDeLosProtagonistas`, niños con `NinosHechosEnBlender`, NPCs con la tabla de `CarasDeLosNPCs` (quita las cejas de los pelos y sombreros del pack y pinta las nuevas de su color) | ojos, bocas o cejas del pack en personajes nuevos; `CejasPorEmocion` en quien lleva cara nueva |
| Piezas que lleva un personaje y se sueltan al caer él (aro, casco, cadena) | `PiezaQueCae` (`Soltar`: sale del esqueleto, cae con física, se apagan sus brillos y se congela quieta; vive en la pieza, sobrevive a que el personaje se destruya) | `Destroy` o `SetActive(false)` de lo que lleva puesto; física de trozos propia en cada jefe |
| Hojas que giran sobre su bisagra (contraventanas, puertas que alguien abre en una escena) | `HojaConBisagra` (grupo, ángulo, abierta al empezar; sin Update) + `HojasConBisagraBeat` en las secuencias (por grupo o por nombre, escalonado, portazo) | girar la hoja con `PropMoveBeat` uno a uno; animadores propios en cada puerta |
| Dos personajes de la mano en una cinemática | guion: `X de_la_mano Y [lado=der\|izq]` / `X suelta` (el horneado lleva a X al lado de Y y pone `DeLaManoBeat`); en juego, `DeLaMano` (IK de dos huesos a mano, sin «IK Pass») | caminos paralelos a mano; `formacion=` para ir cogidos |
| Puertas, ventanas y adornos pegados a las fachadas de un edificio | `VestidoDeFachadas` (mide la malla con rayos y apoya la pieza en pared plana); el prólogo, `VestidoDelPrologo` (*El Sendero ▸ Escenario ▸ Prólogo: vestir el pueblo*, TDD § 30) | colocar ventanas a mano en la escena |
| Decorado fino que cuelga (guirnaldas, cuerdas) y no debe contar como algo que tapa un plano | componente `NoTapaLaCamara` (el horneado de cámaras lo ignora) | excepciones por nombre en `HornoCamaras` |
| Sombra bajo los personajes | `SombraDeContacto` (se añade sola desde `NPCSimpleAnimator` y `EnemigoDelMundo`; para quitarla, ponerla desactivada) | sombras de personaje propias en cada sistema |
| Cámaras de retratos e iconos | renderer `Retratos_Renderer` (sin SSAO ni post), por nombre desde `EstudioDeRetratos.RendererDeRetratos` | cámaras auxiliares con el renderer principal |
| Movimiento secundario de huesos (coleta, cola, capa) que sigue la velocidad del personaje | `MuelleDeHuesos` (cadena de huesos por nombre, varias por personaje; los huesos propios de una pieza los crea `PiezaNuevaDelModelo`; en los protagonistas, `Ficha.CadenaConMuelle`); la capa del pack (`CloakBone01–03`), con `MuelleDeHuesos.AsegurarCapa`, que se pone sola desde `NPCSimpleAnimator` | rotar huesos de pelo, capas o colas a mano en cada sistema |
| Menú de Start (Inventario, Magia, Equipo, Sintonía) | `MenuDeStart` + una vista `IVistaDelMenuDeStart` por pestaña, montado por código (`Scripts/UI/MenuDeStart/`); `PlayerEquipmentMenuController` solo abre, cierra y pasa la entrada | paneles del menú en Start.unity; vistas viejas (`InventoryView`, `SpellView`, `EquipmentView`) |
| Lo que da la ropa | rasgos (`RasgoDePrenda` en `WardrobeItemSO.rasgos`, consulta con `RasgosDePrenda`; requisito de paso con `RequisitoDeAcceso.rasgos`) | bonos numéricos de ataque, defensa, vida o magia en las prendas |
| Vida y maná en barras | `BarraApilada` + `EstiloDeBarrasApiladas` (capas de color, «×N») | barras de un solo color que se encogen |
| Aspecto de la interfaz (hojas, pestañas, fichas, colores) | colores de `PaletaUI` + hoja de `PiezasDeUI.Papel` (papel crema con borde de tinta, filo y sombra dura; lo que convive con el mundo va en papel, los menús lo ponen sobre el fondo de noche); la caja de diálogo, con `PapelDeLaCajaDeDialogo`; los textos de escenas y prefabs, con *El Sendero ▸ UI ▸ Pasar la interfaz al papel* (`InterfazDePapelBuilder`, TDD § 11) | sprites de cristal con borde de neón (`menu_*_glass`, `confirm_dialog_glass`, `hud_panel_bg`) en piezas nuevas; colores escritos en cada pantalla |

---

**Nota de mantenimiento:** este archivo (y su gemelo `AGENTS.md`) es un resumen deliberadamente corto para que herramientas de IA lo carguen como contexto sin gastar espacio de más. Si cambia una regla no negociable o un invariante narrativo en `TDD.md`, algo estructural en `GDD.md` (personajes, sistemas de balance), o el criterio de seguimiento de incidencias en `TRACKER.md`, actualizar el resumen aquí también — no dejar que se desincronicen.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
