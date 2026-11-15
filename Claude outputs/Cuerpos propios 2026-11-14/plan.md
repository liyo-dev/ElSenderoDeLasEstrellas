# Cuerpos propios del grupo (INC-717, tanda 1): auditoría y plan

Fecha: 14 nov 2026. Encargo: pasos 3.1 a 3.5 del plan «Jugador vs. personaje» (aprobado el 27 sep).

## 1. Cómo está hoy (medido en el proyecto)

- **Cuerpos.**
  - `_WILL.prefab`: la raíz es un contenedor (`MuelleDeHuesos`) con un hijo `vBasicController_MaleCharacterPBR`. Ese hijo es el cuerpo, con 40 componentes del jugador (Invector, magia, vida, nado, vuelo, escudo, levitar, combo…) y `ActiveCharacterSwapper`.
  - `_ESTELA` y `_LIAM`: la raíz es el cuerpo, con la pila de NPC (`NavMeshAgent`, `NPCSimpleAnimator`, `NPCBehaviourManagerV2`, `Interactable`, `NPCObstacleAvoidance`, `SenueloDeCombate`, `NarrativeActor`…).
  - `_WILL_NPC`: Will como NPC. Lo instancia el relevo.
- **Las mallas son las mismas.** Los cuatro usan el mismo pack modular: mismas mallas y mismo esqueleto.
  - Will, Estela y Will NPC llevan el mismo avatar y el mismo controlador (`Invector@BasicLocomotion`).
  - Liam lleva su propio avatar y su propio controlador.
  - La apariencia la decide qué piezas van encendidas. `_WILL` lleva las piezas de los tres: así se disfrazaba.
- **Dónde están en las escenas.**
  - `_WILL` está colocado en MainWorld, MainMenu, Cascaditas, Chuchelandia, Parque, Sendero y EscapeSendero.
  - `_ESTELA` y `_LIAM` están colocados en todas esas menos MainWorld. En MainWorld salen de `NpcRoster_MainWorld.asset`, que guarda la referencia a la raíz del prefab.
  - MainWorld tiene dos referencias de escena al `PlayerActionManager` de Will y una a un Transform suyo.
- **Ya está hecho.**
  - 3.1 en parte: `GrupoDelJugador` (inventario, vestuario, cargas, dúos, `SaludDelGrupo`, `ReanimarManteniendoA`).
  - La puerta `ControlDelJugador` (`Activo`, `CuerpoDe`, `NpcDe`, `MiembroDe`, `RepresentadoPorElJugador`, `PersonajeDe`, `PasarElMandoA`). La usan unos 40 archivos.
- **El relevo de hoy.** `ActiveCharacterSwapper` hace el disfraz:
  - teletransporta, cambia la ropa y los hechizos;
  - esconde el NPC (`HiddenNpc`);
  - crea y destruye `_WILL_NPC` (`WillNpcInstance`);
  - tiene redes de seguridad: un `Update` cada 0,5 s y reafirmaciones de visibilidad.

## 2. Consumidores (auditoría)

### 2.1 `ActiveCharacterSwapper`, `HiddenNpc`, `WillNpcInstance`, `_WILL_NPC`

| Archivo | Uso | Qué se hace |
|---|---|---|
| `Player/ActiveCharacterSwapper.cs` | el relevo | fuera (a Versiones antiguas) |
| `Personajes/ControlDelJugador.cs` | `WillNpcInstance`, `HiddenNpc`, `IsReady` | lee los cuerpos de su registro y la bandera del piloto |
| `Player/PartyControlManager.cs` | `SwitchCharacter`, `IsReady`, `TeleportWillNpcToPlayer`, `ResetState` | cambia con `ControlDelJugador.CambiarElMando`; Will se reagrupa como un compañero más |
| `Player/PlayerPresetService.cs` | `TrySetWillBasicsWhileAway` | cada cuerpo lleva sus hechizos en su `MagicCaster` |
| `Behaviour NPC/PlayerParty.cs` | `TeleportWillNpcToPlayer` | el cuerpo de Will por `NpcDe(Will)` |
| `Dialogue/DialogueCinematicController.cs` | `TeleportWillNpcToPlayer(forzar)` | ídem |
| `World/TeleportService.cs` | ídem | ídem |
| `UI/GameOverManager.cs` | `ResetState` | lo que haga falta va en `ControlDelJugador` |
| `ElSendero/.../PedirGrupo.cs` | `IsReady` | fuera |
| `ElSendero/.../PedirDesbloqueo.cs` | comentario | se corrige el comentario |
| `NPCStateContext`, `NPCBehaviourManagerV2`, `NPCPartyMember`, `ModularAutoBuilder`, `InteractionDetector`, `SleepTrigger` | comentarios | se corrigen |
| Editor: `FotosDeGestos`, `PosesDeActuacion`, `ArmarioDeLosProtagonistas`, `AspectoDeProtagonista`, `CarasDeLosProtagonistas`, `IconosDelArmarioWindow` | rutas a `_WILL_NPC.prefab` | se cambia a `_WILL.prefab`, que ya es el mismo cuerpo |

### 2.2 «Es el jugador» porque tiene un componente del jugador

Con tres cuerpos completos esto deja de valer: los compañeros también tienen esos componentes, aunque estén apagados. Hay 22 sitios que miran un colisionador cualquiera:

- `MagicProjectil:498`;
- `MagicProjectileSpawner:554,582`;
- `AerialKnockbackReceiver:174`;
- `EnemyProjectile:431,472,530,613`;
- `GolemBossAI:1497,1769`;
- `EnemigoDelMundo:164,185`;
- `ImpLadron:177`;
- `RocaEmbestidora:155,186`;
- `ZonaDelJugador:28,39`;
- `LanzadorDeSalto:77`;
- `PlacaDeDerriboDelLab:25`;
- `DianaAstralSendero:77`;
- `PickupObject:40`.

Todos pasan a una sola pregunta, `ControlDelJugador.DelJugador<T>(desde, out T)`: el componente T del cuerpo que lleva el jugador, si `desde` es de ese cuerpo. Los que buscan componentes del propio cuerpo (`GetComponentInParent` en `Awake`, unos 60) no cambian. Las comprobaciones por la etiqueta `Player` (`PlayerLocator`, `SenderoTriggerArea`, `Spider1AI`, `DianaAstralSendero`) valen porque la etiqueta pasa al cuerpo del jugador.

### 2.3 El jugador guardado en caché

- `PlayerService` vacía su caché al registrar. `ServiceLocator` busca el primer `PlayerActionManager`/`ManaPool`/`PlayerHealthSystem` que encuentra y se lo queda: al cambiar, el piloto vuelve a registrar sus componentes.
- Guardan el jugador en un campo y no escuchan `OnPlayerRegistered`:
  - `NPCAlertIconController.s_accionesJugador` (estático);
  - `FollowPlayerState._playerActionManager`;
  - `PlayerParty:972`;
  - `ClimaSobreElJugador`, `PasajeroJugador`, `GuiaDeCombate`, `PozoGravitatorio`, `TagMinigameController`, `ConcursoPorRondas`, `MinimapController`, `TeamGaugeHUD`, `PlayerDamageScreenEffects`, `PlayerEquipmentMenuController`, `ShopUI`, `MarcaDeMapaDelTesoro`, `CombatLabMagicPanel`, `VistaDeMagiaDelMenu`, `DialogueManager`, `Interactable`, `AllyCombatState`.

  Se revisan uno a uno: o preguntan en el momento, o escuchan `OnPlayerRegistered`.
- La cámara:
  - `vThirdPersonInput.Start` busca la `vThirdPersonCamera` con `FindAny` y la apunta a sí mismo: con tres cuerpos, ganaría el último.
  - Solo la apunta el cuerpo que lleva el jugador. Al cambiar, la vuelve a apuntar `ControlDelJugador`.
  - `CombatCameraTargeting` ya escucha `OnPlayerRegistered`.

### 2.4 La entrada (3.2)

El input es global (`GamepadInputReader` estático y un `PlayerControls` por cada nadar y volar). Leen botones:

- `vThirdPersonInput`, `InteractionDetector`;
- `PlayerSwimmingController`, `PlayerFlyingController`, `PlayerShieldController`, `PlayerLevitationController`;
- apuntado preciso (`PlayerTargeting`), `PlayerAmbientActivityHandler`, `PlayerCombatInput`, `ComboCastController`;
- `AerialKnockbackReceiver`, `PlaneoDelJugador`, `PlayerActionManager`.

Todos leen en `Update` o se suscriben en `OnEnable`. Con el cuerpo en manos de la IA se apagan, así que no leen nada.

`PlayerLockService` bloquea al jugador registrado en `PlayerService`: con el cambio de registro vale tal cual.

**Escalar:** `PlayerClimbingController` sigue en el cuerpo y ya no está en el diseño. Sobra: sale con su `.meta` y sus menciones.

### 2.5 Estado guardado (3.5)

- **Ya va por personaje:**
  - vida y maná: Will en `maxHP`/`currentHP`/`maxMP`/`currentMP`, Estela y Liam en `companionHealth`;
  - básicos: Will en `basicSpellIds`, Estela y Liam en `companionBasics`;
  - apariencia.
- **Compartido:** `ataque`/`defensa`, la base de estadísticas. Pasa a ir por personaje, con migración: una partida sin esos datos los toma de la ficha de cada uno, y Will conserva los suyos.
- **Leen «el cuerpo del jugador» al guardar** (hechizos de `MagicCaster`, apariencia de `ModularAutoBuilder`): con Estela al mando se guardaban como de Will. Se pasa a leer el cuerpo de cada personaje.

## 3. Diseño

- **`PilotoDelCuerpo`** (`Scripts/Personajes/`, genérico, sin nombres del juego). Va en cada cuerpo y es la bandera «lo lleva el jugador».
  - Tiene dos listas de datos: lo que solo funciona con el jugador (Invector y los componentes del jugador) y lo que solo funciona con la IA (agente, animador de NPC, FSM, charla). Las rellena el constructor del prefab.
  - Opcional: el controlador del animador con IA (el de Liam).
  - `TomarElMando()`:
    - enciende lo del jugador y apaga lo de la IA;
    - pone el Rigidbody dinámico;
    - pone la etiqueta `Player`.
  - `DarALaIA()`:
    - hace lo contrario;
    - pone el Rigidbody cinemático;
    - pone la etiqueta de NPC;
    - deja el agente en el NavMesh donde está.
  - Avisa con un evento. Los cuerpos se apuntan en un registro por personaje (su `Personaje`).
- **`ControlDelJugador.CambiarElMando(de, a)`** sustituye a `ActiveCharacterSwapper`:
  1. el cuerpo de `de` pasa a la IA;
  2. el de `a` toma el mando;
  3. `PlayerService.RegisterPlayer(cuerpo)`, que avisa con `OnPlayerRegistered`;
  4. registro en el `ServiceLocator`;
  5. cámara;
  6. el que sale se pone en el modo del grupo (siguiendo, anclado o en combate).

  No hay teletransporte, ni ropa, ni hechizos, ni NPC escondido o copiado.
- **Prefab común** `Assets/Prefabs/Personajes/CuerpoDelGrupo_Base.prefab`, con el cuerpo como raíz:
  - el cuerpo de Will;
  - la pila de compañero de `_WILL_NPC`;
  - `PilotoDelCuerpo`.

  Las variantes `_WILL`, `_ESTELA` y `_LIAM` llevan su ficha, la configuración de su NPC, sus piezas encendidas, su cara (`NPCEmotionController`), su `NarrativeActor` y sus extras (escudo de Estela, flecha de Liam, señuelo), todo copiado de su prefab de hoy.
- **Migración** con un menú: *El Sendero ▸ Archivo ▸ Personajes: cuerpos propios del grupo (INC-717)*.
  - Crea la base y las variantes.
  - En cada escena con instancias: la abre en aditivo y sustituye cada instancia conservando el nombre, el padre, la posición y las referencias de la escena a sus componentes (por tipo y ruta). Solo guarda la escena si no estaba modificada por otro.
  - Actualiza el roster y `CombatLabConfig`.
  - Los prefabs viejos y `_WILL_NPC` van a Versiones antiguas.

## 4. Orden

1. **3.1.**
   - `PlayerPresetService` pasa de cuerpo a `GrupoDelJugador`: aplica a cada cuerpo lo suyo.
   - Parte global de `PlayerBattleModeController` (cámara de victoria, música) a un componente del grupo.
   - Compilar.
2. **3.2.**
   - `PilotoDelCuerpo` y `ControlDelJugador.DelJugador<T>`; los 22 sitios.
   - Re-registro en el `ServiceLocator` y la cámara solo del cuerpo al mando.
   - Fuera escalar.
   - Con un solo cuerpo no cambia nada. Compilar.
3. **3.4 y 3.3 juntos.** Sin el relevo nuevo, los prefabs nuevos no funcionan, y al revés.
   - `ControlDelJugador` sin swapper; consumidores migrados.
   - `CharacterAppearanceRegistry` con un constructor por cuerpo.
   - Constructor de prefabs y migración de escenas.
   - Compilar y pasar el menú.
4. **3.5.** Base de estadísticas por personaje con migración, y el guardado leyendo el cuerpo de cada uno. Compilar.
5. **Play:**
   - cambiar con los tres;
   - cambiar en combate;
   - nadar y volar;
   - una cinemática;
   - guardar y cargar.

## 5. Tanda 2 (15 nov 2026): dudas decididas por Raúl

- **Duda 2. Cada personaje empieza desde su ficha.**
  - Ya no hay base compartida. El ataque y la defensa de Estela y Liam salen de su entrada de `companionHealth` o de su ficha, nunca de los de Will. La vida y el maná ya eran de cada uno (`SaludDelGrupo`), y los hechizos también (`basicSpellIds`, `companionBasics`).
  - Habilidades por personaje: `HabilidadesDelPersonaje`.
    - Will: `preset.abilities`.
    - Estela y Liam: `FichaDePersonaje.HabilidadesIniciales` (por defecto nadar, saltar, magia y esprintar; sin volar ni escudo) más `EstadoDeCompanero.habilidadesAprendidas`, que va en el guardado.
  - `UnlockService.UnlockAbility(AbilityKey)` las enseña a los tres. `HasAbility(key)` mira al que va al mando.
  - `PlayerPresetService` pone a cada cuerpo las suyas. El volcado ya no copia habilidades del cuerpo al preset.
  - Partida vieja: sin `habilidadesAprendidas`, Estela y Liam toman las de su ficha; `canClimb` se ignora.
- **Duda 3. Escalada fuera.**
  - Enums: `ActionMode.Climbing` (8), `AbilityKey.Climb` (2) y `PlayerAbility.Climb` (10, era el último). Los demás valores conservan su número.
  - Código: `abilities.climb`, `canClimb`, `AllowClimb`, `CanClimb`, las ramas de `FollowPlayerState` (con la pausa del Animator, que solo usaba la escalada) y la preparación de `NPCPartyMember`.
  - Herramientas: el permiso «Trepar» del LAB, la ficha del aviso, los textos `ABILITY_CLIMB_*` y `REQUISITO_FALTA_CLIMB`, y los editores de preset.
  - Datos: ningún grafo ni guion la usaba. Chuchelandia, la zona «se puede trepar» de la torre ya no concede nada. Start está pendiente: tiene cambios de otro trabajo.
- **Duda 4. Celebración de victoria partida.**
  - `CelebracionDeVictoria` (en `GrupoDelJugador`, que la añade si falta) lleva la cámara, la música, el SFX, la foto de grupo y su paso del cierre.
  - `PlayerBattleModeController` solo lleva lo del cuerpo.
- **Duda 5. FBX.** Los prefabs viejos no tenían ninguna instancia anidada: nunca estuvieron enlazados al FBX. El común es autónomo igual que antes.
- **Duda 1 (cinemáticas).** Sin tocar.
