# Auditoría de sistemas — 15 nov 2026

Auditoría completa del proyecto **El Sendero de las Estrellas** (Unity 6 + URP): arquitectura, rendimiento, QA y deuda técnica. Hecha a petición de Raúl tras la tanda de regresiones del prólogo.

> **Alcance medido:** 1.247 ficheros `.cs`, **264.922 líneas** (juego + editores + kits + paquete del grafo).
> **Método:** conteo por fichero, agregados por subsistema, lectura de los 40 ficheros más grandes, análisis de la FSM de NPCs, de los cuatro cerebros de combate y de los sistemas de UI/cinemáticas, y búsqueda dirigida de patrones de riesgo (`Find*`, `Camera.main`, `GetComponent`, LINQ, físicas, logs, reflection, `Update`, `OnGUI`, `timeScale`, singletons).

---

## 1. Resumen ejecutivo

Ordenado por **impacto real** (riesgo × coste de mantenimiento), no por tamaño.

1. **La FSM de NPCs es el mayor foco de deuda.** 67 ficheros / 28.076 líneas. El `NPCStateContext` es un objeto-dios con más de 25 campos públicos mutables (social, refugio, combate, grupo, narrativa). Los estados guardan su propio estado y se **crean con `new` en cada transición**. `CinematicState` tiene **1.326 líneas**, `FollowPlayerState` 1.083 y `AllyCombatState` 961.
2. **Hay cuatro cerebros de combate distintos** que repiten el mismo patrón (enum de estados, cooldowns, línea de visión, maná, NavMesh, daño): `NPCCombatBrain` (3.020), `GolemBossAI` (2.033), `ImpDemonAI` (1.386), `MagoOscuroBossAI` (898) = **7.337 líneas**.
3. **El código de juego no tiene ensamblados propios.** Los 17 `.asmdef` son de kits, plugins y del paquete narrativo. Las ~191.500 líneas de `Assets/Scripts` compilan en **`Assembly-CSharp`**: cada cambio recompila todo (compilaciones de ~40 s vistas hoy) y no hay ninguna frontera de dependencias que impida que un kit use código del juego.
4. **Las herramientas de editor son el 28% del código**: 74.000 líneas y **234 menús**. Son potentes, pero también el origen de varios de los últimos sustos (escriben escenas y assets, y una línea rota en una de ellas dejó el proyecto sin compilar).
5. **La UI no tiene un dueño único de visibilidad ni de orden de dibujo.** El HUD se apaga por tres caminos (claves, escena, pantalla de carga), las bandas de cine y el telón usan órdenes de dibujo puestos a mano (9998, 9999, 9995, 4900, 0) y esta semana se han roto por eso dos cosas (HUD en la cinemática y bandas en el centro).
6. **Diálogo y cinemáticas** suman 4.495 líneas en dos clases (`DialogueManager` 1.949 + `DialogueCinematicController` 2.546); la segunda hace *staging*, cámara, ocultar personajes, intrusos, pool de cámaras y conversaciones de grupo.
7. **QA**: 67 métodos de test en **10 ficheros** para 265.000 líneas. No hay prueba automática del arranque ni del prólogo; las comprobaciones son menús manuales (234).
8. **Sistemas congelados con usos residuales**: `NPCInteractiveNarrativeExecutor` (9 usos en 8 ficheros), `NPCQuestIconManager` (6), `PlaceActorNode` (6). La política existe, la retirada no se ha terminado.
9. **El rendimiento global está mejor de lo que parece**: LOD de IA por distancia, chequeos *throttled* (LOS, social), `OverlapSphereNonAlloc`, pool de VFX, hashes de Animator y casi ningún `Find` en `Update` (29 hallazgos, casi todos logs). Los puntos negros son concretos y localizados.
10. **El arranque** (menú + `GameBootService` + `Partida` + `WorldBootstrap` + grafo `Cap1`) tiene demasiados dueños para lo crítico que es. El fallo de INC-1227 salió de ahí.
11. **Los kits apenas han empezado**: 3.843 líneas de ~200.000 (≈2%). La regla «genérico en Kits, del juego en ElSendero» existe en `AGENTS.md`, pero el código del juego sigue siendo el 95%.
12. **Higiene de logs buena**: 1.344 `Debug.Log`, pero solo **5 ficheros** tienen logs sin ningún `#if UNITY_EDITOR` alrededor. Aun así, quedan diagnósticos permanentes que deberían haberse retirado al cerrar su incidencia (p. ej. `PlayerHUDV2:DIAG`).

---

## 2. Números

| Métrica | Valor |
|---|---|
| Ficheros `.cs` | 1.247 |
| Líneas totales | 264.922 |
| `Assets/Scripts` (juego) | 863 f · 191.506 l |
| `Assets/ElSendero` (contenido/enchufes) | 33 f · 5.239 l |
| `Assets/Kits` | 26 f · 3.843 l |
| Paquete narrativo (runtime + editor) | 168 f · 18.325 l |
| Editor (`Assets/Editor` + `Assets/Scripts/Editor`) | 267 f · **73.999 l** |
| `.asmdef` | 17 (ninguno en el código de juego) |
| `void Update()` | 175 |
| `void LateUpdate()` | 44 |
| `void FixedUpdate()` | 6 |
| Uso de LINQ (`.Where/.Select/.ToList/...`) | 698 |
| `FindObjectOfType`/`FindAny`/`GameObject.Find` | 130 |
| `Camera.main` | 117 |
| `GetComponent<` | 1.339 |
| `Debug.Log` | 1.344 (5 ficheros sin guarda) |
| `OnGUI` | 27 (25 de editor; 6 de runtime son paneles de depuración) |
| Físicas que asignan (`RaycastAll`, `OverlapSphere` sin NonAlloc) | 2 en runtime + resto en editores |
| `StartCoroutine` | 403 |
| `new List/Dictionary/HashSet` | 1.408 |
| Singletons (`static … Instance`) | 50 |
| `ServiceLocator.Register` | 16 |
| `DontDestroyOnLoad` | 130 |
| `Resources.Load` | 47 |
| Escrituras directas de `Time.timeScale` | 2 (ambas en `TimeScaleArbiterService`; el resto son comentarios) |
| Tests (`[Test]`/`[UnityTest]`) | 67 en 10 ficheros |
| Menús de editor (`[MenuItem]`) | 234 |

---

## 3. Mapa por subsistema

| Subsistema | Ficheros | Líneas | Veredicto |
|---|---:|---:|---|
| UI | 106 | 28.509 | Grande y acoplada. Falta un dueño único de capas/visibilidad. |
| Behaviour NPC (FSM) | 67 | 28.076 | **Prioridad 1.** Contexto-dios, estados gigantes, allocaciones por transición. |
| Editor del proyecto (`Assets/Editor`) | 157 | 46.009 | Potente pero enorme (28% del código). Riesgo de romper assets. |
| Editor dentro de Scripts | 110 | 27.990 | Ídem. |
| World | 99 | 19.487 | `DayNightCycle` (1.931) hace demasiadas cosas. |
| Sequences | 57 | 18.260 | Beats muy grandes (`AperturaDelValleBeats` 916, `CinematicWorldBeats` 1.015). |
| Player | 47 | 15.561 | Sano en general; `PlayerHealthSystem` 1.193 mezcla daño, UI y muerte. |
| Core | 73 | 14.565 | `PlayerControls` es generado (no tocar). `GameBootService`/`Partida` bien, pero el arranque tiene muchos dueños. |
| IA / Enemigos | 18 | 8.784 | **Prioridad 2.** Cuatro cerebros de combate duplicados. |
| Dialogue | 14 | 6.446 | Dos monolitos de 2,5k y 2k. |
| Attacks | 15 | 4.901 | — |
| Combat | 35 | 4.716 | Pruebas del Sendero y jefes; sano. |
| Quests | 15 | 3.264 | Bien, pero conviven con restos del sistema congelado. |
| Interaction | 9 | 2.945 | — |
| Rooms | 12 | 2.717 | `BossArenaController` 1.462: alta concentración. |
| Puzzle | 21 | 2.628 | — |
| Kits | 26 | 3.843 | Correctos, pero cubren el 2% del código. |

---

## 4. FSM de NPCs — análisis a fondo

### 4.1 Cómo está montada

`NPCBehaviourManagerV2` (1.166 l) → `NPCBrain` (166 l) → `NPCStateContext` (181 l) → `INPCState` (estados).

- `NPCBrain.Update()` llama **cada frame** a `OnUpdate` + `CheckTransitions` del estado actual.
- `CheckTransitions` devuelve `null` o **un estado nuevo**.

### 4.2 Lo que está bien

- Separación clara cerebro / contexto / estado.
- LOD real: `UpdateDistanceLOD()` duerme la FSM y apaga el `NavMeshAgent` a distancia, con histéresis y exenciones (combate, cinemática, escolta).
- Comprobaciones caras *throttled*: LOS cada 0,1 s; detección de jugador cada 0,2 s; social cada 3 s.
- Físicas con `NonAlloc` y buffers preasignados (`WanderState._socialBuffer`, `NPCStateBase.GroundProbeHitsBuffer`).
- Nada de `FindObjectOfType` en `Update`.

### 4.3 Problemas, con evidencia

**(a) El contexto es un objeto-dios.** `NPCStateContext` expone y muta 25+ campos: `LastKnownPosition`, `PendingSocialPartner`, `ShouldSeekShelter`, `CurrentShelter`, `ShelterOriginPosition`, `ShelterEdgePosition`, `Anthas`, `RelationshipId`, `FollowGraceUntil`… Cada función nueva añade campos a este objeto, que acaba siendo el punto por el que pasa todo. Cualquier cambio en un subsistema puede romper otro.

**(b) Los estados se crean con `new` en cada transición.** Conteo por fichero:

| Estado | `new` | `return new` |
|---|---:|---:|
| `WorkState` | 17 | 11 |
| `WanderState` | 16 | 10 |
| `WalkToActivityState` | 10 | 8 |
| `ReturnFromShelterState` | 8 | 8 |
| `SeekShelterState` | 8 | 6 |
| `IdleState` | 7 | 6 |
| `AlertState` | 6 | 6 |

No todas se ejecutan cada frame (solo cuando se cumple la condición), pero con 30+ NPCs y estados como `Wander` (que evalúa camino, atasco, jugador, social y llegada) el ritmo de allocaciones pequeñas es constante. Además impide reutilizar/instanciar estados y complica los tests.

**(c) Estados gigantes con lógica de varios sistemas dentro.** `CinematicState` (1.326 l) implementa secuencias enteras (llevar al jugador a un ancla, esperar, mover NPCs). `FollowPlayerState` (1.083 l) mezcla seguimiento, teletransporte de emergencia, formación y combate. `AllyCombatState` (961 l).

**(d) Transiciones globales duplicadas en cada estado.** Todos los estados repiten:

```csharp
if (context.IsInCinematic) return new CinematicState();
if (context.IsInCombat)    return new CombatState();
if (context.WasDefeatedInCombat) return new DeadState();
if (context.IsInteracting) return new IdleState();
```

Eso son 4 comprobaciones × N estados, y un orden de prioridad que hay que recordar en cada sitio. Debería vivir **una sola vez** en `NPCBrain`.

**(e) Logs con interpolación en rutas calientes.** `context.Log($"[{StateName}] OnEnter")` se ejecuta en `OnEnter`/`OnExit` de todos los estados; la cadena se construye aunque `DebugMode` sea false. En `CheckTransitions` hay más (`Log`, `LogWarning`).

**(f) Acoplamiento directo entre estados.** Un estado hace `new OtroEstado(...)` con sus parámetros. No hay tabla de transiciones ni fábrica: cambiar un estado obliga a tocar varios ficheros.

### 4.4 Propuesta de refactor (FSM)

1. **Mover las transiciones globales a `NPCBrain`** con una tabla ordenada (cinemática > derrota > combate > interacción). Los estados solo devuelven sus transiciones **propias**.
2. **Partir `NPCStateContext`** en fachada + subcontextos: `MovimientoContext`, `CombateContext`, `SocialContext`, `RefugioContext`, `NarrativaContext`. La fachada expone lo común; cada estado recibe solo lo suyo (por constructor o interfaz).
3. **Pool de estados por NPC**: `Dictionary<Type, INPCState>` con `Reset()`/`OnEnter`. Sin allocaciones por transición.
4. **Partir los estados gigantes**:
   - `CinematicState` → un `CinematicSequenceRunner` (servicio) + estado fino que delega.
   - `FollowPlayerState` → seguimiento + `PartyTeleport` + formación, cada uno en su clase.
5. **Logs sin coste**: `if (DebugMode) context.Log(...)` o un `Log` que reciba `Action<string>`/plantilla y no formatee si está apagado.
6. **Tests de la tabla de transiciones** (hoy hay 0 para la FSM).

**Ganancia esperada:** menos GC, estados de 200–300 líneas, transiciones en un solo sitio y tests posibles. Es la refactorización con mejor relación coste/beneficio del proyecto.

---

## 5. Combate: cuatro cerebros con el mismo patrón

| Clase | Líneas | FSM propia | Notas |
|---|---:|---|---|
| `NPCCombatBrain` | 3.020 | `CombatState { EVALUATE, REPOSITION, ATTACK, DEFENSE, SEARCHING, HIDING_TO_RECHARGE, TACTICAL_RETREAT }` | Cooldowns, maná, LOS throttled, `FaceTarget`, retirada. |
| `GolemBossAI` | 2.033 | `BossState { Idle, Walking, Attacking, Jumping, Charging, Dead }` | Salto con NavMesh apagado, carga, fases. |
| `ImpDemonAI` | 1.386 | `BossState { Idle, Chasing, Attacking, CastingSpell, Underground, TakingDamage, Dead }` | — |
| `MagoOscuroBossAI` | 898 | — | — |

Todos repiten: elegir objetivo, comprobar distancia/LOS, cooldown de ataque, mirar al objetivo, gestionar animación, daño y muerte. **7.337 líneas** para lo mismo con matices.

**Propuesta:** un `CombatBrainBase` (o kit `Kits.Combat`) con:
- FSM de combate común (evaluar / reposicionar / atacar / defender / buscar / retirada).
- Cooldowns, maná y LOS (con throttle) resueltos una vez.
- Ataques como datos (`AttackDefinition`), no como métodos sueltos.
- Los jefes aportan **hooks** (`OnCharge`, `OnPhaseChange`) y datos, no otro cerebro.

**Ganancia:** −3.000 a −4.000 líneas, comportamiento homogéneo y testable.

---

## 6. UI, HUD y orden de dibujo

### 6.1 Tamaño

106 ficheros / 28.509 líneas. Mayores: `SpeechBubbleUI` 1.343, `MainMenuController` 971, `DramaticTextOverlayUI` 915, `BugReportFlyoutPanel` 753, `PlayerHUDV2` 678, `CreditsFlyoutPanel` 615, `GameOverManager` 604.

### 6.2 Duplicidad de visibilidad (el patrón que nos ha roto dos veces)

| Quién | Qué decide |
|---|---|
| `PlayerHUDV2.HideHUD/ShowHUD` | «¿lo tapa alguien?» con claves |
| `SceneBoundUI` | «¿su escena está cargada?», pantalla de carga y ahora cinemática |
| `BandasDeCineUI` | modo cine |
| `Telon` | negro entre escenas |
| `SceneTransitionLoader.PantallaDeCargaVisible` | carga |

Hoy funciona, pero no hay **una** decisión: hay cinco sistemas que se preguntan cosas parecidas y se avisan por eventos.

### 6.3 Órdenes de dibujo puestos a mano

| Capa | Orden |
|---|---:|
| Velo de apertura | 4.900 |
| Telón (`FeedbackService`) | 9.998 |
| Flash de pantalla | 9.999 |
| Bandas de cine | 9.995 |
| Pantalla de carga | 0 → subida a 10.000 al mostrarse |
| Subtítulos / bocadillos | 9.997 |
| Avisos | 61–110 |

No hay tabla única ni dueño; cualquiera añade un canvas con un número distinto.

**Propuesta:**
1. Un `CapaDePantalla` (o constante central `OrdenesDeDibujo`) con **todos** los órdenes y comentario de para qué es cada uno.
2. Los canvas a pantalla completa **nunca** se cuelgan de una raíz con escala (la regla que rompió las bandas).
3. `SceneBoundUI` solo decide escena/carga; lo de «cinemática» se consulta a `GameContext` o al registro de ocultables, no con un `if` propio.
4. Test de editor: con una cinemática en marcha, comprobar que HUD/minimapa están ocultos y bandas visibles.

---

## 7. Diálogo y cinemáticas

| Clase | Líneas | Qué hace |
|---|---:|---|
| `DialogueCinematicController` | 2.546 | Cámara de diálogo (pool, encuadres, obstruction), *staging* de grupo, ocultar jugador/NPC/intrusos, efectos por línea. |
| `DialogueManager` | 1.949 | Caja de diálogo, input, typewriter, retratos, opciones. |
| `SequenceActor` | 1.019 | Mover/girar/posar actores en secuencias. |
| `AperturaDelValleBeats` | 916 | 3 beats de la apertura, con cámara propia y VFX. |
| `CinematicWorldBeats` | 1.015 | Beats de mundo. |
| `ModuloBatallaFinal` / `ModuloCinematicaFinal` | 890 / 847 | Montajes a mano. |

**Propuesta:**
- Extraer de `DialogueCinematicController` un `CinematicStaging` (ocultar/mostrar actores, intrusos) y un `CameraShotDirector` (pool y encuadres). La clase se queda en orquestar.
- `DialogueManager` → vista + presentador + input, separados.
- Los beats de apertura: mover a datos lo que sea posible (ya hay guion horneado; los 916 líneas de la apertura son lógica de cámara/VFX).

---

## 8. Mundo: `DayNightCycle`

1.931 líneas para: hora, sol/luna, cielo, luces, niebla, clima, eventos, guardado. `EnvironmentController` 813 y los dos `NightSky*Spawner` 1.435.

**Propuesta:** partir en `RelojDelMundo` (hora), `CieloYDios` (skybox, sol/luna), `ClimaDelMundo` (lluvia/nieve/viento) y `LucesDelMundo`. `DayNightCycle` queda como fachada. Ya existe `ClimaDelCicloWiring` y `ClimaSobreElJugador`; es cuestión de mover responsabilidades, no de inventar.

---

## 9. Jugador

`PlayerHealthSystem` 1.193, `PlayerSwimmingController` 902, `DuoSpecialAttackSystem.Coreografias` 824, `PresentacionDelCombo` 767, `PlayerFlyingController` 753, `PlayerActionManager` 674.

`PlayerHealthSystem` mezcla daño, muerte, UI y eventos. **Propuesta:** separar el **daño** (recibir, mitigar, morir) de la **presentación** (barra, flash, cámara). El resto está razonablemente separado.

---

## 10. Editor: 74.000 líneas y 234 menús

Herramientas mayores: `VestidoDelMundo` (15.532 en 21 ficheros), `Guion` (9.227), `MontarCinematicaFinal` 2.011, `CorrectorDeAnimaciones` 1.339, `AuditorDeAnimaciones` 1.016, `VestidoDelPrologo`, `HorneadorDeGuion` 1.578.

Es el **28% del código total** y el origen de varios incidentes (menús de un solo uso, herramientas que escriben escenas/assets, una línea rota que impidió compilar).

**Propuesta:**
1. Unificar los *builders* de vestuario en un solo punto de entrada con **modo seco** (dry-run) y registro de cambios.
2. Regla ya existente («menús de un solo uso → `Archivo/`») aplicada de verdad: hay 234 menús; revisar cuántos son de un solo uso.
3. Los editores que tocan escenas deben usar `AssetDatabase` + `SetDirty` + `SaveAssets` (ya es la norma) y **comprobar que no hay cambios sin guardar** antes de tocar.
4. Tests de editor para los horneadores (ya hay 2: `TiempoDeGuionTests`, `TallerDeRodajeTests`). Extender a `HorneadorDeGuion` y `HornoCamaras`.

---

## 11. Rendimiento

### 11.1 Lo que está bien

- **LOD de IA** por distancia con histéresis y exenciones (combate, cinemática, escolta).
- **Throttling** de LOS (0,1 s), detección de jugador (0,2 s) y social (3 s).
- **Físicas**: `OverlapSphereNonAlloc`, `RaycastNonAlloc` con buffers estáticos.
- **VFX**: `VfxPoolService` en lugar de `Instantiate/Destroy`.
- **Animator**: parámetros con hash (`InputMagnitudeHash`), no strings por frame.
- **Casi ningún `Find` en `Update`**: 29 hallazgos, y la mayoría son `Debug.Log` o cachés de `Camera.main`.

### 11.2 Puntos negros concretos

| Punto | Evidencia | Impacto | Arreglo |
|---|---|---|---|
| Estados nuevos por transición | 7–17 `new Estado()` por fichero de estado | GC periódico con 30+ NPCs | Pool de estados |
| Logs con interpolación en rutas calientes | `NPCStateBase.OnEnter/OnExit`, `CheckTransitions` | Alloc aunque `DebugMode=false` | `if (DebugMode)` o plantilla |
| `RaycastAll` en runtime | `NPCBehaviourManagerV2.cs:363` | Alloc por llamada | `RaycastNonAlloc` con buffer |
| `RaycastAll` en props | `PropsQueFlotan.cs:102` | Alloc | Ídem |
| `Camera.main` en bucles/fallbacks | 117 usos; `DayNightCycle` 5, UI 2 con fallback cada frame | Búsqueda por tag | Cachear una vez |
| LINQ en editores | 698 usos, 28 en `HorneadorDeGuion`, 29 en `HornoSalida` | Solo editor | Aceptable; no en runtime |
| `OnGUI` | 6 runtime, todos paneles de depuración | Solo en debug | Aceptable |
| `Resources.Load` | 47 | Carga síncrona, difícil de trazar | Assets directos o Addressables |

**Medición recomendada:** usar el `PerformanceCapture` que ya existe y añadir marcadores `ProfilerMarker` en: `NPCBrain.Update`, `NPCBehaviourManagerV2.Update`, `NPCSimpleAnimator.Update`, `DayNightCycle` y `DialogueCinematicController.LateUpdate`.

---

## 12. QA y proceso

- **Tests:** 67 métodos de test en 10 ficheros, todos de editor: `TallerDeRodaje`, `TiempoDeGuion`, `GuardianDeKits`, `ChooseByMoment`, `DefaultNarrativeSignals`, `IdDePersistencia`, `NarrativeStateProjector`, `PlayerActionManager`, `StoryBatchEdit`, `TimeScaleArbiterService`. **Ninguno cubre el arranque, el prólogo, la FSM de NPCs ni el combate.**
- **Comprobaciones manuales:** 234 menús, entre ellos los validadores del grafo (`NarrativeKitQa`, «Validar Interactive vs Grafo»), `AuditorDeAnimaciones`, `PruebaDeLaHistoria`. Son buenos, pero dependen de que alguien se acuerde.
- **Lo que falta y explicaría las últimas regresiones:**
  1. **Prueba de humo del arranque**: Nueva Partida → carga → deseo → valle → plaza, con comprobación de errores en consola y de que HUD/bandas aparecen y desaparecen donde toca.
  2. **Lista de ficheros «core»** (`Telon`, `SceneTransitionLoader`, `BandasDeCineUI`, `SceneBoundUI`, `OcultablesDeCinematica`, `Cap1`, `WorldBootstrap`): si se tocan, se corre la prueba de humo.
  3. **Validador de proyecto**: escenas en Build Settings, referencias nulas, `Missing Script`, canvas a pantalla completa colgados de raíces con escala, duplicados de singletons.
  4. **Registro de capas de pantalla** (ver §6.3) para que no se inventen órdenes.

---

## 13. Deuda y limpieza

- **Sistemas congelados con usos residuales**: `NPCInteractiveNarrativeExecutor` (9 usos / 8 ficheros), `NPCQuestIconManager` (6/5), `PlaceActorNode` (6/4), `NPCQuestConfig` (2/2). Terminar la retirada o justificar su permanencia.
- **`PlayerControls.cs` (2.349 l) es generado** por el Input System: no es deuda, no tocar a mano.
- **50 singletons + 130 `DontDestroyOnLoad`**: coherente con la arquitectura elegida, pero conviene un registro único (ya existe `ServiceLocator`) y una regla: «singleton solo si sobrevive a escenas y no tiene dueño natural».
- **`Resources.Load` (47)**: sustituir por referencias directas o Addressables donde se pueda.

---

## 14. Plan por fases

### Fase 0 — Blindaje (1 día) · impacto alto, riesgo bajo
1. Prueba de humo del arranque/prólogo (menú de editor + log).
2. Tabla única de órdenes de dibujo y regla de canvas sin padre con escala.
3. Lista de ficheros «core» y obligación de prueba de humo al tocarlos.
4. Quitar diagnósticos permanentes (`PlayerHUDV2:DIAG`).

### Fase 1 — FSM de NPCs (3–5 días) · impacto alto, riesgo medio
1. Transiciones globales a `NPCBrain`.
2. Pool de estados por NPC.
3. Partir `NPCStateContext` en subcontextos.
4. Partir `CinematicState` y `FollowPlayerState`.
5. Tests de la tabla de transiciones.

### Fase 2 — Combate compartido (1–2 semanas) · impacto alto, riesgo medio
1. `CombatBrainBase` + `AttackDefinition`.
2. Migrar `NPCCombatBrain` primero; después los tres jefes.
3. Tests de cooldowns, LOS y retirada.

### Fase 3 — Ensamblados y kits (2–3 semanas) · impacto alto, riesgo medio-alto
1. `ElSendero.Runtime`/`ElSendero.Editor` y asmdefs por capa (UI, Mundo, Secuencias…).
2. Mover a `Kits` lo genérico (combate, diálogo, cinemáticas) con interfaces.
3. Regla de dependencias comprobada por el `GuardianDeKits` (ya existe).

### Fase 4 — Editor y herramientas (continuo)
1. Unificar builders de vestuario y guion con modo seco.
2. Revisar los 234 menús y archivar los de un solo uso.
3. Tests de horneadores.

---

## 15. Anexo: cómo se ha medido

- Conteo de líneas por fichero (`Get-Content | Measure-Object -Line`).
- Agregados por carpeta (tres niveles).
- Búsqueda de patrones con expresiones regulares sobre los 1.247 ficheros.
- Lectura de los 40 ficheros mayores y de la FSM completa (`NPCBrain`, `NPCStateContext`, `NPCStateBase`, `WanderState`).
- Análisis de los `Update`/`LateUpdate`/`FixedUpdate` por conteo de llaves.
- Recuento de `[MenuItem]`, `[Test]`, asmdefs y sistemas congelados.

> Los números son del árbol de trabajo **15 nov 2026** y pueden variar con la tanda en curso (hay sesiones en paralelo sobre `ElSendero`, `Kits` y el grafo narrativo).
