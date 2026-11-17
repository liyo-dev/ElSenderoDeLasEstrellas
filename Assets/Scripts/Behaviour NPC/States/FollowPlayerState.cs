using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado FSM para NPCs que siguen al jugador como compañeros de equipo.
    /// Replica las animaciones del jugador en modos vuelo, nado y plataformas sin NavMesh.
    /// </summary>
    public class FollowPlayerState : NPCStateBase
    {
        public override string StateName => "FollowPlayer";

        private readonly NPCPartyMember _partyMember;
        private NPCPartyConfig _config;

        // Timers
        private float _pathUpdateTimer;
        private float _stateTimer;
        private float _idleTimer;

        // Estados internos NavMesh
        private bool _isInitialized;
        private Vector3 _lastPlayerPosition;
        private bool _isWanderingNearPlayer;
        private Vector3 _wanderTarget;

        // FIX — "el party va a trompicones/tembloroso cuando el jugador va lento"
        // (nadando, o siguiendo a un guardia con la velocidad reducida por un lore pop-up).
        // _isFollowingSticky reemplaza la decisión "moverse si playerIsMoving || distance >
        // stopDist*1.2" por una histéresis persistente (igual que ya usa _isMovingSpecial más
        // abajo para el seguimiento especial): playerIsMoving solo es true el frame exacto en que
        // el jugador acumula 0.1m de movimiento, así que a velocidades bajas se queda "false" la
        // mayoría de los frames y parpadea a "true" de forma intermitente — con la condición OR
        // original eso hacía que el Agent se detuviera (isStopped=true + ResetPath()) y se
        // reactivara en cada parpadeo mientras la distancia se quedaba en la zona gris entre
        // stopDist y stopDist*1.2, visible como el compañero caminando a trompicones. Con
        // histéresis: una vez en marcha, sigue hasta volver a stopDist (ya cubierto por la rama de
        // parada de más abajo); una vez parado, no arranca hasta superar stopDist*1.2 de verdad.
        private bool _isFollowingSticky;

        // FIX — "el party se queda atrás al esprintar y se teletransporta cerca,
        // viéndose feo" + el mismo trompiqueo de arriba pero visto desde el otro lado (el
        // compañero corriendo a una velocidad fija muy por encima del jugador cuando este va
        // despacio, así que adelanta y frena en seco una y otra vez). Causa raíz común: la
        // velocidad de seguimiento (walkSpeed/runSpeed del NPCPartyConfig, o SPECIAL_FOLLOW_SPEED
        // en el seguimiento especial) era una CONSTANTE fija sin relación con la velocidad real del
        // jugador — que va de ~5 caminando a 15 esprintando en _WILL.prefab, y baja mucho durante
        // secciones controladas (nado, seguir a un NPC despacio, lore pop-ups). Se estima la
        // velocidad real del jugador por posición (delta/deltaTime, suavizado) y se usa como suelo
        // dinámico para la velocidad de seguimiento en ambos caminos (NavMesh normal y seguimiento
        // especial), en vez de una constante pensada solo para el paso normal.
        private Vector3 _prevPlayerPosForSpeed;
        private float _smoothedPlayerSpeed;
        private bool _speedSampleInitialized;


        // Frenado automático del agente tal y como estaba al entrar (se apaga al ir al lado de Will).
        private bool _autoBrakingOriginal;
        private bool _autoBrakingGuardado;
        private const float PLAYER_SPEED_SMOOTH_RATE = 6f;   // /s, más alto = se adapta más rápido
        // Deltas de posición del jugador por encima de esta velocidad implícita se consideran un
        // teletransporte/warp (no locomoción real) y se ignoran para no disparar la media hacia un
        // pico de un solo frame — el sprint real más alto del proyecto es 15 (_WILL.prefab), así
        // que 30 deja margen de sobra sin colar teletransportes.
        private const float PLAYER_SPEED_TELEPORT_CUTOFF = 30f;

        // FIX — continuación de INC-096: aunque Agent.speed sube dinámicamente hasta
        // runSpeed*2.5 para alcanzar al jugador en sprint, Agent.acceleration se quedaba en el
        // valor de fábrica del prefab (8 en Estela/Liam) sin relación con esa velocidad objetivo.
        // Con el destino recalculándose cada PATH_UPDATE_INTERVAL mientras el jugador sigue en
        // movimiento, el agente nunca llegaba a alcanzar de verdad la velocidad alta que se le
        // pedía — seguía quedándose atrás hasta cruzar distanciaParaTeletransporte y disparar el
        // teletransporte de emergencia ("de pronto aparecen" / pop-in en cámara). Escalamos la
        // aceleración junto con la velocidad en vez de dejarla fija.
        private const float AGENT_ACCELERATION_MULTIPLIER = 2.5f;
        private const float AGENT_MIN_ACCELERATION = 8f;

        // Constantes NavMesh
        private const float PLAYER_STATIC_BEFORE_WANDER = 3f;
        private const float PATH_UPDATE_INTERVAL = 0.1f;
        private const float DEFAULT_STOP_DISTANCE = 1.2f;
        private const float DEFAULT_RUN_DISTANCE = 3f;
        private const float DEFAULT_WALK_SPEED = 3.5f;
        private const float DEFAULT_RUN_SPEED = 7.5f;
        private const float PLAYER_MOVE_THRESHOLD = 0.1f;
        private const float INITIAL_DELAY = 0.3f;
        private const float ROTATION_ANGLE_DEADZONE = 2.5f;

        // --- Seguimiento especial (vuelo, nado, plataformas sin NavMesh) ---
        private Transform _jugadorCacheado;
        private PlayerActionManager _playerActionManager;
        private PlayerSwimmingController _playerSwimming;
        private Animator _playerAnimator;
        private bool _inSpecialFollow;

        // _lastSpecialMode: modo actual (puede ser Default durante transiciones)
        // _exitMode: último modo SIGNIFICATIVO (Flying/Swimming), usado para cleanup al salir
        // Esto evita que una transición transitoria por Default borre el contexto de vuelo/nado
        private ActionMode _lastSpecialMode = ActionMode.Default;
        private ActionMode _exitMode        = ActionMode.Default;

        private bool _storedGravity;
        private bool _gravityStored;
        private int _npcFlightLayerIndex = 0;
        private GameObject _footVfxInstance;
        private ParticleSystem _footVfxPs;

        // Parámetros del Animator del NPC cacheados una vez por entrada al estado (no en Update).
        // Animator.SetBool/SetFloat loguean un error nativo si el hash no existe en el controller,
        // incluso dentro de try/catch (no lanzan excepción C#), así que hay que filtrar antes de llamar.
        private HashSet<int> _animatorParams;

        // Posición "lógica" durante el seguimiento especial (sin el offset visual de bobbing de vuelo).
        // Se usa para los cálculos de formación/distancia; el bobbing se aplica solo al Transform final.
        private Vector3 _specialFollowPosition;
        private float _bobPhase;

        // Estado persistente (no recalculado desde cero cada frame) para la histéresis de
        // isMoving en seguimiento especial, y valor suavizado del InputMagnitude de nado.
        private bool _isMovingSpecial;
        private float _swimInputMag;

        // Seguimiento especial RELATIVO al jugador (INC-895). El jugador se mueve por Rigidbody en
        // FixedUpdate (50 Hz, sin interpolación) y la cámara lo sigue en LateUpdate, así que Will
        // avanza a saltos de 50 Hz y la cámara con él. Si el compañero se mueve en mundo a ritmo de
        // frame, cada frame queda un poco adelantado o atrasado respecto a ese salto: a 12-18 m/s
        // de vuelo se ve temblar. Guardando su posición como desplazamiento respecto al jugador,
        // viaja en bloque con él (mismo salto que Will y la cámara) y solo se suaviza el hueco.
        private Vector3 _specialFollowOffset;
        private Vector3 _specialOffsetVel;     // velocidad interna del SmoothDamp del desplazamiento
        private Vector3 _specialSmoothedVel;   // velocidad real en mundo, suavizada (animación/giro)
        // Rumbo de la formación (grados), suavizado: al girar el jugador, los puestos dan la vuelta
        // a su alrededor en arco. Sin esto el hueco se interpolaba en línea recta y en un giro
        // cerrado el compañero cruzaba por encima de Will.
        private float _formationYaw;
        private float _formationYawVel;
        // Distancia entre donde va el compañero y su puesto: si es grande está alcanzando al grupo.
        private float _specialSlotError;
        private int   _playerFlightLayerIndex;

        // --- Rastro del jugador en plataformas ---
        private PlayerParty _party;
        private float _distanciaBaseRastro;
        private float _separacionPorPuestoRastro;
        private float _separacionLateralRastro;
        private float _tiempoEstudioSalto;
        private int _puestoRastro;
        private int _totalPuestosRastro = 1;
        private float _tiempoSiguientePuestoRastro;
        private float _cursorDistanciaRastro;
        private bool _rastroInicializado;
        private bool _usandoRastro;
        private bool _enSaltoDeRastro;
        private bool _estabaEnAireRastro;
        private bool _tieneTurnoSalto;
        private bool _saltoConEspacio;
        private float _tiempoFinEstudioSalto;
        private float _ultimoSaltoEstudiado = -1f;
        private Vector3 _direccionRastro;
        private float _timerComprobarIslaNavMesh;
        private bool _caminoNavMeshCortado;
        private Vector3 _formationTarget;
        private bool _tieneDestinoFormacion;

        private const float CAIDA_MAXIMA_DESDE_RASTRO = 1.2f;
        private const float VELOCIDAD_MINIMA_SALTO_RASTRO = 6.5f;
        private const float INTERVALO_COMPROBAR_ISLA = 0.25f;

        // Estado de vuelo pedido al Animator del compañero (0 = ninguno) y desde cuándo, para
        // pedirlo con fundido una sola vez, igual que PlayerFlyingController.PlayFlightState.
        private int _flightStateRequested;
        private float _flightStateSince;

        // Fuera del NavMesh sin volar ni nadar (plataformas, escaleras, saltos): el compañero pisa
        // el suelo Floor que tiene debajo de su puesto en vez de ir a la altura de Will.
        private int   _mascaraDeSuelo = -1;
        private bool  _pisandoSuelo;
        private float _velYSuelo;
        private const float SUELO_RAYO_ARRIBA    = 1.5f;  // desde cuánto por encima se busca el suelo
        private const float SUELO_CAIDA_MAXIMA   = 4f;    // más abajo de Will que esto es un hueco: sigue su altura
        private const float SUELO_SMOOTH_TIME    = 0.08f; // ajuste de altura (peldaños, rampas)

        private const float SPECIAL_FOLLOW_SPEED    = 8f;
        // Tiempo de ajuste del hueco con el jugador (formación) y tope de la corrección (m/s).
        private const float SPECIAL_OFFSET_SMOOTH_TIME = 0.25f;
        private const float FORMATION_YAW_SMOOTH_TIME  = 0.4f;
        // Más lejos que esto de su puesto, el compañero está alcanzando al grupo: mira hacia donde
        // va y vuela en picado aunque Will esté quieto. Más cerca, mira y posa como Will.
        private const float SPECIAL_CATCH_UP_DIST = 2.5f;
        private const float SPECIAL_OFFSET_MAX_SPEED   = SPECIAL_FOLLOW_SPEED * 1.5f;
        // Histéresis de "moviéndose" sobre la velocidad real en mundo (m/s): con un único umbral
        // el estado oscilaría y el blend/estado de animación saltaría entre quieto y moviéndose.
        private const float SPECIAL_MOVE_SPEED_ENTER = 1.2f;
        private const float SPECIAL_MOVE_SPEED_EXIT  = 0.5f;
        private const float SPECIAL_VEL_SMOOTH_RATE  = 8f;   // /s
        // Fundido entre fly_idle y fly_dive (el mismo que usa Will) y tiempo mínimo en un estado
        // antes de cambiar: antes se cambiaba con Play en seco y un frame de pose erguida en mitad
        // del picado se veía como un parpadeo.
        private const float FLIGHT_STATE_CROSSFADE = 0.25f;
        private const float FLIGHT_STATE_MIN_HOLD  = 0.3f;
        // Velocidad de interpolación del parámetro InputMagnitude del nado (unidades/seg).
        // Evita que el blend tree de nado salte en seco entre "quieto" y "nadando".
        private const float SWIM_INPUT_MAG_LERP_SPEED = 3f;
        private const float OFF_NAVMESH_THRESHOLD   = 1.5f;
        private const float WARP_SEARCH_RADIUS      = 12f;
        // Valores reales usados por Will (_WILL.prefab), no los defaults de PlayerFlyingController:
        // el campo del script vale 0.8/1.8, pero el prefab lo tiene afinado a 0.18/1.2.
        private const float DEFAULT_FLIGHT_BOB_AMPLITUDE = 0.18f;
        private const float DEFAULT_FLIGHT_BOB_FREQUENCY = 1.2f;

        // Hashes de estados y parámetros (compartidos con el animator del jugador)
        private static readonly int HashFlyIdle    = Animator.StringToHash("fly_idle");
        private static readonly int HashFlyDive    = Animator.StringToHash("fly_dive");
        private static readonly int HashFlyLanding = Animator.StringToHash("Landing");
        private static readonly int HashLocomotion = Animator.StringToHash("Free Locomotion");
        private static readonly int HashSwimFloat  = Animator.StringToHash("Swimming_Floating_NoWeapon");
        private static readonly int HashIsFlying   = Animator.StringToHash("isFlying");
        private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int HashGroundDist = Animator.StringToHash("GroundDistance");
        private static readonly int HashInputMag   = Animator.StringToHash("InputMagnitude");
        private static readonly int HashJumpMove   = Animator.StringToHash("JumpMove");
        private static readonly int HashJumpStart  = Animator.StringToHash("JumpStart_InPlace_NoWeapon");
        private static readonly int HashLanding    = Animator.StringToHash("Landing");

        private readonly bool _skipPartyCheck;

        public FollowPlayerState(NPCPartyMember partyMember, bool skipPartyCheck = false)
        {
            _partyMember = partyMember;
            _skipPartyCheck = skipPartyCheck;
            _config = partyMember?.PartyConfig;
        }

        private void CachearJugador(Transform jugador)
        {
            _jugadorCacheado = jugador;
            _playerActionManager = jugador.GetComponent<PlayerActionManager>();
            _playerSwimming = jugador.GetComponent<PlayerSwimmingController>();
            _playerAnimator = jugador.GetComponent<Animator>();

            // Al pasar el mando a otro personaje cambia qué miembro va al mando y, con él, el
            // reparto de puestos del rastro. Se vuelve a enganchar desde donde esté cada uno.
            if (_party == null && PlayerParty.HasInstance) _party = PlayerParty.Instance;
            if (_party != null)
            {
                _rastroInicializado = false;
                ActualizarDatosDelRastro();
            }
        }

        public override void OnEnter(NPCStateContext context)
        {
            if (context.Agent != null)
            {
                _autoBrakingOriginal = context.Agent.autoBraking;
                _autoBrakingGuardado = true;
            }

            base.OnEnter(context);

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[FollowPlayerState] {context.Transform.name} entró en FollowPlayerState");
#endif

            _pathUpdateTimer   = 0f;
            _stateTimer        = 0f;
            _idleTimer         = 0f;
            _isInitialized     = false;
            _isWanderingNearPlayer = false;
            _inSpecialFollow   = false;
            _lastSpecialMode   = ActionMode.Default;
            _exitMode          = ActionMode.Default;
            _gravityStored       = false;
            _npcFlightLayerIndex = DetectFlightLayer(context.UnityAnimator);
            _animatorParams      = BuildAnimatorParamSet(context.UnityAnimator);
            _bobPhase            = (_partyMember?.PartyIndex ?? 0) * 0.7f;
            _footVfxInstance     = null;
            _footVfxPs           = null;
            _isMovingSpecial     = false;
            _swimInputMag        = 0f;
            _flightStateRequested = 0;
            _isFollowingSticky   = false;
            _speedSampleInitialized = false;
            _smoothedPlayerSpeed = 0f;
            _usandoRastro        = false;
            _rastroInicializado  = false;
            _enSaltoDeRastro     = false;
            _estabaEnAireRastro  = false;
            _tieneTurnoSalto     = false;
            _saltoConEspacio     = false;
            _cursorDistanciaRastro = 0f;
            _ultimoSaltoEstudiado = -1f;
            _tiempoFinEstudioSalto = 0f;
            _timerComprobarIslaNavMesh = 0f;
            _caminoNavMeshCortado = false;
            _tieneDestinoFormacion = false;
            _tiempoSiguientePuestoRastro = 0f;
            _party = PlayerParty.HasInstance ? PlayerParty.Instance : null;
            ActualizarDatosDelRastro();
            if (_partyMember != null) _partyMember.EnSaltoDeRastro = false;

            // Ver NPCStateContext.IsActivelyFollowingPlayer: cubre también a compañeros
            // temporales (p.ej. el NPC de Will) que nunca pasan por PlayerParty.AddMember, para
            // que NPCBehaviourManagerV2 no los "duerma" por LOD de distancia mientras siguen.
            context.IsActivelyFollowingPlayer = true;

            if (context.Player != null)
            {
                _lastPlayerPosition = context.Player.position;
                CachearJugador(context.Player);
            }

            if (context.Agent != null)
            {
                // isStopped/ResetPath sólo son válidos con el agente sobre el
                // NavMesh. PlayerParty.TeleportMemberToPlayer() puede mover el transform "a mano"
                // (member.transform.position = targetPos) cuando el agente no estaba sobre malla,
                // y llama a OnTeleportedToPlayer() -> StartFollowing() en el mismo frame: este
                // OnEnter se ejecuta con el agente todavía fuera de malla, y agent.isStopped = true
                // lanzaba "Stop can only be called on an active agent that has been placed on a
                // NavMesh." Igual que ya hace NavMeshAgentUtility.SafeSetStopped/HardStop.
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.isStopped = true;
                    context.Agent.ResetPath();
                }
                context.Agent.updatePosition = false;
                context.Agent.updateRotation = false;
                SetAgentSpeed(context.Agent, GetWalkSpeed());
            }

            context.Animator?.SetMovementSpeed(0f);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (context.Player == null) return;
            // El jugador puede pasar a otro personaje mientras se le sigue: se sigue al nuevo cuerpo.
            if (context.Player != _jugadorCacheado) CachearJugador(context.Player);

            UpdatePlayerSpeedEstimate(context);

            _stateTimer += Time.deltaTime;

            // --- MODO ESPECIAL: vuelo, nado, jugador fuera del NavMesh o plataformas ---
            ActionMode playerMode     = _playerActionManager?.Top ?? ActionMode.Default;
            bool needsSpecialFollow = playerMode == ActionMode.Flying
                                   || playerMode == ActionMode.Swimming
                                   || NecesitaSeguimientoDePlataformas(context, playerMode);

            if (needsSpecialFollow)
            {
                HandleSpecialFollow(context, playerMode);
                return;
            }

            // Salir del modo especial si el jugador volvió al suelo
            if (_inSpecialFollow)
            {
                ExitSpecialFollow(context);
                return;
            }

            // --- SEGUIMIENTO NORMAL POR NAVMESH ---
            if (context.Agent == null || !context.Agent.isOnNavMesh) return;

            // Si el jugador está en el aire y el modo especial no llegó a activarse, no se le
            // persigue: el agente apuntaría a su posición en el aire y se le metería dentro.
            if (_party != null && _party.JugadorEnElAire)
            {
                if (!context.Agent.isStopped)
                {
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false;
                    context.Agent.ResetPath();
                }
                context.Animator?.SetMovementSpeed(0f);
                return;
            }

            if (_stateTimer < INITIAL_DELAY)
            {
                context.Agent.isStopped = true;
                context.Animator?.SetMovementSpeed(0f);
                return;
            }

            // FIX INC-028: margen de gracia tras restaurar posición/rotación persistida al
            // cargar partida (ver GameBootProfile.ApplyNpcPositionsToScene y
            // NPCStateContext.FollowGraceUntil). El jugador casi siempre se restaura solo a
            // un ancla aproximada (no a su Vector3/Quaternion exacto), así que nada más cargar
            // este NPC suele estar a más de stopDist de él — sin esta comprobación arrancaría
            // a caminar de inmediato y NPCSimpleAnimator.SyncWithNavMeshAgent() sustituiría la
            // rotación recién restaurada por la de marcha antes de que se llegue a ver quieto.
            if (Time.time < context.FollowGraceUntil)
            {
                context.Agent.isStopped = true;
                context.Animator?.SetMovementSpeed(0f);
                return;
            }

            if (!_isInitialized)
            {
                _isInitialized = true;
                _lastPlayerPosition = context.Player.position;
            }

            float distance  = Vector3.Distance(context.Transform.position, context.Player.position);
            // Al lado del jugador (quedarseDetras = false): las distancias se miden a su puesto
            // junto a Will, no a Will; si no, se para a distanciaParaPararse de él y se queda atrás.
            bool alLado = _config != null && !_config.quedarseDetras;
            bool preferBehind = !alLado;
            bool jugadorQuieto = _smoothedPlayerSpeed < 0.3f;
            float stopDist  = _config?.distanciaParaPararse ?? DEFAULT_STOP_DISTANCE;
            float runDist   = _config?.distanciaParaCorrer  ?? DEFAULT_RUN_DISTANCE;
            float walkSpeed = _config?.velocidadCaminando   ?? DEFAULT_WALK_SPEED;
            float runSpeed  = _config?.velocidadCorriendo   ?? DEFAULT_RUN_SPEED;

            if (preferBehind)
            {
                // La formación manda: no vale con pararse a 2 m del jugador si el puesto está a
                // 2,6-3 m. Se deja una banda de carrera por encima de la parada.
                float minimoDetras = _config != null && _config.distanciaMinimaFormacionDetras > 0.01f
                    ? _config.distanciaMinimaFormacionDetras : 2.5f;
                stopDist = Mathf.Max(stopDist, minimoDetras);
                runDist  = Mathf.Max(runDist, stopDist + 1.4f);
            }

            bool playerIsMoving = Vector3.Distance(_lastPlayerPosition, context.Player.position) > PLAYER_MOVE_THRESHOLD;
            if (playerIsMoving)
            {
                _lastPlayerPosition = context.Player.position;
                _idleTimer = 0f;
                _isWanderingNearPlayer = false;

                // El puesto se mueve con el jugador: el destino guardado se queda viejo. Se
                // invalida para que el siguiente UpdateDestination lo recalcule y el compañero
                // no se quede clavado en el hueco de donde arrancó el jugador.
                if (preferBehind) _tieneDestinoFormacion = false;
            }

            float alPuesto = alLado
                ? DistanciaPlana(context.Transform.position, PuestoAlLado(context))
                : (preferBehind && _tieneDestinoFormacion
                    ? DistanciaPlana(context.Transform.position, _formationTarget)
                    : distance);

            if (_isWanderingNearPlayer)
            {
                bool reached = !context.Agent.pathPending && context.Agent.remainingDistance < stopDist * 0.5f;
                if (reached)
                {
                    _isWanderingNearPlayer = false;
                    _idleTimer = 0f;
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false;
                    context.Animator?.SetMovementSpeed(0f);
                }
                else
                {
                    UpdateMovementAnimation(context);
                    return;
                }
            }

            bool enSuPuesto = alLado
                ? (jugadorQuieto && alPuesto <= PUESTO_LLEGADA)
                : (preferBehind
                    ? (_tieneDestinoFormacion && alPuesto <= PUESTO_LLEGADA && !playerIsMoving)
                    : distance <= stopDist);

            if (enSuPuesto)
            {
                _isFollowingSticky = false; // alcanzado al jugador: rearma la histéresis de arranque
                if (!context.Agent.isStopped || context.Agent.updatePosition)
                {
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false;
                    context.Agent.ResetPath();
                }
                context.Animator?.SetMovementSpeed(0f);

                _idleTimer += Time.deltaTime;

                if (_idleTimer > 0.5f)
                    RotateTowardsPlayer(context);

                if (_config != null && _config.puedeVagarCerca &&
                    _idleTimer >= PLAYER_STATIC_BEFORE_WANDER)
                {
                    TryStartPartyWander(context, stopDist);
                }
                return;
            }

            // decisión de moverse con histéresis persistente en vez de
            // "playerIsMoving || distance > stopDist*1.2" evaluado fresco cada frame — ver
            // comentario de _isFollowingSticky en la cabecera de la clase.
            _isFollowingSticky = _isFollowingSticky
                ? true // seguimos moviéndonos: la rama "enSuPuesto" de más arriba ya corta el
                       // movimiento en cuanto se alcanza el puesto
                : (alLado ? (playerIsMoving || alPuesto > PUESTO_LLEGADA * 1.5f)
                          : (preferBehind
                              ? (playerIsMoving || !_tieneDestinoFormacion || alPuesto > PUESTO_LLEGADA * 1.5f)
                              : (playerIsMoving || distance > stopDist * 1.2f)));

            if (_isFollowingSticky)
            {
                if (!context.Agent.updatePosition)
                {
                    if (context.Agent.isOnNavMesh)
                        context.Agent.nextPosition = context.Transform.position;
                    context.Agent.updatePosition = true;
                    context.Agent.updateRotation = false;
                }

                context.Agent.isStopped = false;
                // Suelo dinámico: nunca más lento que el ritmo real del jugador. El margen sobre
                // esa velocidad ya NO es un 15% fijo (FIX 25 ago 2026 anterior, KO en juego): con
                // un margen fijo, el NPC como mucho EMPATABA con el jugador en la práctica (el
                // suavizado de _smoothedPlayerSpeed tarda en converger tras cada arranque de
                // sprint, y el camino por NavMesh casi siempre rodea más que la línea recta que
                // usa PlayerParty para medir distancia), así que el hueco no se cerraba de verdad:
                // oscilaba justo alrededor del umbral de teletransporte de emergencia y disparaba
                // el warp una y otra vez en vez de evitarlo (visto en juego: "Liam/Estela demasiado
                // lejos" repetido cada pocos segundos durante un sprint sostenido). Ahora el margen
                // crece con la propia distancia — igual que ya hacía HandleSpecialFollow más abajo
                // (baseSpecialSpeed*1.5 cuando dist > STOP_DIST*3) para nado/vuelo, pero aquí nunca
                // se había traído al seguimiento normal por NavMesh: cerca de distanciaParaCorrer
                // el margen sigue siendo un 15% discreto, y crece hasta un 60% al acercarse al
                // umbral de teletransporte de este NPC — así el NPC gana terreno de verdad cuanto
                // más atrás se queda, en vez de solo no perder más.
                float catchUpDistance = _config?.distanciaParaTeletransporte ?? 25f;
                float catchUpT = Mathf.Clamp01((distance - runDist) / Mathf.Max(1f, catchUpDistance - runDist));
                float speedMargin = Mathf.Lerp(1.15f, 1.6f, catchUpT);
                float dynamicRunSpeed = Mathf.Clamp(_smoothedPlayerSpeed * speedMargin, runSpeed, runSpeed * 2.5f);
                float targetAgentSpeed = distance > runDist ? dynamicRunSpeed : walkSpeed;
                if (alLado && alPuesto <= runDist)
                {
                    // Al paso de Will, un poco más rápido cuanto más lejos de su puesto: con
                    // velocidadCaminando fija o se adelanta y frena, o se queda atrás.
                    targetAgentSpeed = jugadorQuieto
                        ? walkSpeed
                        : Mathf.Clamp(_smoothedPlayerSpeed * Mathf.Lerp(1f, 1.4f, alPuesto / Mathf.Max(0.5f, runDist)),
                                      Common.NavMeshAgentUtility.VelocidadMinimaAndando,
                                      Mathf.Max(walkSpeed, dynamicRunSpeed));
                }
                SetAgentSpeed(context.Agent, targetAgentSpeed);

                // Al lado de Will el destino es un puesto que se mueve con él y está siempre a
                // uno o dos metros: con el frenado automático del agente, iba frenando todo el
                // rato y avanzaba arrastrando los pies. Ver INC-464.
                if (alLado) context.Agent.autoBraking = false;

                _pathUpdateTimer += Time.deltaTime;
                if (_pathUpdateTimer >= PATH_UPDATE_INTERVAL)
                {
                    _pathUpdateTimer = 0f;
                    UpdateDestination(context, stopDist);
                }
            }
            else
            {
                if (!context.Agent.isStopped || context.Agent.updatePosition)
                {
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false;
                    context.Agent.ResetPath();
                }
                context.Animator?.SetMovementSpeed(0f);
            }

            UpdateFollowGaitAnimation(context);
        }

        /// <summary>
        /// Animación del seguimiento normal por NavMesh, con el criterio común de
        /// NavMeshAgentUtility.FactorDeLocomocion (INC-466). El seguimiento especial
        /// (vuelo/nado/plataformas) tiene su propio cálculo en UpdateSpecialModeAnimation().
        /// </summary>
        private void UpdateFollowGaitAnimation(NPCStateContext context)
        {
            if (context.Agent == null || !context.Agent.isOnNavMesh || context.Animator == null)
                return;

            float speedFactor = Common.NavMeshAgentUtility.FactorDeLocomocion(context.Agent);
            if (context.Config != null && speedFactor > 0f)
            {
                speedFactor = Mathf.Max(speedFactor, context.Config.minAnimSpeed);
            }
            context.Animator.SetMovementSpeed(speedFactor);
        }

        // =========================================================================
        // SEGUIMIENTO ESPECIAL
        // =========================================================================

        private bool IsPlayerOffNavMesh(NPCStateContext context)
        {
            if (context.Player == null) return false;
            return !NavMesh.SamplePosition(context.Player.position, out _, OFF_NAVMESH_THRESHOLD, NavMesh.AllAreas);
        }

        /// <summary>
        /// Altura del suelo Floor bajo un punto, buscando desde algo por encima de <paramref name="desdeY"/>.
        /// Falso si no hay suelo o está más de <paramref name="caidaMaxima"/> por debajo de la
        /// referencia (un hueco). El seguimiento por rastro usa una caída más corta para no tomar
        /// el fondo de un foso como suelo.
        /// </summary>
        private bool BuscarSuelo(Vector3 punto, float desdeY, float playerY, out float ySuelo,
                                 float caidaMaxima = SUELO_CAIDA_MAXIMA)
        {
            if (_mascaraDeSuelo < 0) _mascaraDeSuelo = LayerMask.GetMask("Floor");
            ySuelo = 0f;
            float inicio = desdeY + SUELO_RAYO_ARRIBA;
            float largo  = inicio - (playerY - caidaMaxima);
            Vector3 origen = new Vector3(punto.x, inicio, punto.z);
            if (Physics.Raycast(origen, Vector3.down, out RaycastHit hit, largo, _mascaraDeSuelo, QueryTriggerInteraction.Ignore))
            {
                ySuelo = hit.point.y;
                return true;
            }
            // Sin Floor debajo (suelo de un pack que no se pasó a su capa, una pieza nueva…): vale cualquier
            // superficie sólida que no sea un personaje. Si no, el compañero copiaba la altura y el estado
            // «en el aire» de quien manejas y se quedaba flotando sin mover los pies.
            if (_mascaraDeRespaldo == 0)
                _mascaraDeRespaldo = ~LayerMask.GetMask("Player", "Enemy", "Projectile", "ProjectileEnemy", "UI", "Ignore Raycast",
                                                        "Water", "InteractHint", "Minimap", "AmbientZone", "Weather", "UI_Portrait", "PauseUI");
            int n = Physics.RaycastNonAlloc(origen, Vector3.down, _golpesDeSuelo, largo, _mascaraDeRespaldo, QueryTriggerInteraction.Ignore);
            float mejor = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var c = _golpesDeSuelo[i].collider;
                if (c == null || c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                if (c.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null || c.GetComponentInParent<CharacterController>() != null) continue;
                if (_golpesDeSuelo[i].point.y > mejor) mejor = _golpesDeSuelo[i].point.y;
            }
            if (float.IsNegativeInfinity(mejor)) return false;
            ySuelo = mejor;
            return true;
        }
        private int _mascaraDeRespaldo;
        private float _avisoSinSuelo;
        private readonly RaycastHit[] _golpesDeSuelo = new RaycastHit[12];

        private float GetRunSpeed() => _config?.velocidadCorriendo ?? DEFAULT_RUN_SPEED;

        /// <summary>
        /// ¿Toca seguir por el rastro? Cubre las plataformas sin NavMesh, un salto confirmado del
        /// jugador y el caso de islas de NavMesh separadas por un salto reciente.
        /// </summary>
        private bool NecesitaSeguimientoDePlataformas(NPCStateContext context, ActionMode playerMode)
        {
            if (playerMode == ActionMode.Flying || playerMode == ActionMode.Swimming) return false;

            if (_party == null && PlayerParty.HasInstance)
            {
                _party = PlayerParty.Instance;
                ActualizarDatosDelRastro();
            }

            // Un salto del rastro en curso no se corta a mitad.
            if (_inSpecialFollow && _enSaltoDeRastro) return true;

            // El compañero ya está fuera de la malla (cruzando una plataforma): el rastro lo
            // devuelve al suelo; no se le abandona a medio camino.
            if (_inSpecialFollow && context.Agent != null && !context.Agent.isOnNavMesh) return true;

            // En cuanto el jugador deja el suelo, el seguimiento normal deja de perseguirle: si
            // no, el NavMeshAgent apunta a su posición en el aire y el compañero se le mete
            // dentro. El rastro decide después si hay salto que copiar o solo esperar detrás.
            if (_party != null && _party.JugadorEnElAire) return true;

            if (IsPlayerOffNavMesh(context)) return true;

            if (_config != null && !_config.seguirRastroEnPlataformas) return false;
            if (_party == null || _party.TotalMuestrasRastro < 2) return false;
            if (_party.JugadorEnSaltoConfirmado) return true;

            // Islas de NavMesh separadas: el agente no tiene camino completo hasta el jugador,
            // pero el rastro tiene un salto reciente. Es un tramo de plataformas.
            _timerComprobarIslaNavMesh += Time.deltaTime;
            if (_timerComprobarIslaNavMesh >= INTERVALO_COMPROBAR_ISLA)
            {
                _timerComprobarIslaNavMesh = 0f;
                bool caminoCortado = context.Agent != null && context.Agent.isOnNavMesh
                    && !PlayerParty.PuedeLlegarHasta(context.Transform, context.Player.position);

                // Ya dentro del rastro, el salto que importa es el que tiene delante el cursor
                // (aunque el jugador se haya alejado mucho); al entrar, solo uno reciente.
                float desde = _inSpecialFollow
                    ? Mathf.Max(0f, _cursorDistanciaRastro - 1f)
                    : Mathf.Max(0f, _party.DistanciaTotalRastro - 12f);
                bool saltoCerca = _party.HaySaltoEnTramoDeRastro(
                    desde, _party.DistanciaTotalRastro, out _, out _);
                _caminoNavMeshCortado = caminoCortado && saltoCerca;
            }

            return _inSpecialFollow && _caminoNavMeshCortado;
        }

        private void ActualizarDatosDelRastro()
        {
            // Si un asset antiguo trae los campos nuevos a cero, se usan los valores de diseño:
            // cero haría que el compañero siguiera el punto exacto del jugador.
            _distanciaBaseRastro = _config != null && _config.distanciaBaseEnRastro > 0.01f
                ? _config.distanciaBaseEnRastro : 2.2f;
            _separacionPorPuestoRastro = _config != null && _config.separacionPorPuestoEnRastro > 0.01f
                ? _config.separacionPorPuestoEnRastro : 1.6f;
            _separacionLateralRastro = _config != null && _config.separacionLateralEnRastro > 0.001f
                ? _config.separacionLateralEnRastro : 0.45f;
            _tiempoEstudioSalto = _config != null && _config.tiempoEstudioSalto > 0.001f
                ? _config.tiempoEstudioSalto : 0.2f;

            if (_party != null)
                _puestoRastro = _party.ObtenerPuestoDeFormacion(_partyMember, out _totalPuestosRastro);
            else
            {
                _puestoRastro = 0;
                _totalPuestosRastro = 1;
            }
        }

        private void InicializarCursorRastro(NPCStateContext context)
        {
            if (_tieneTurnoSalto && _party != null)
                _party.LiberarTurnoDeSalto(_partyMember);
            _tieneTurnoSalto = false;
            _saltoConEspacio = false;

            _rastroInicializado = false;
            if (_party == null || _party.TotalMuestrasRastro < 2) return;

            if (_party.BuscarDistanciaMasCercanaEnRastro(context.Transform.position, 8f, out float cercana))
                _cursorDistanciaRastro = cercana;
            else
                _cursorDistanciaRastro = Mathf.Max(0f, _party.DistanciaTotalRastro
                    - Vector3.Distance(context.Transform.position, context.Player.position));

            _rastroInicializado = true;
            _ultimoSaltoEstudiado = -1f;
            _tiempoFinEstudioSalto = 0f;
            _enSaltoDeRastro = false;
            _estabaEnAireRastro = false;
            if (_partyMember != null) _partyMember.EnSaltoDeRastro = false;
        }

        private void HandleSpecialFollow(NPCStateContext context, ActionMode playerMode)
        {
            bool modeChanged = playerMode != _lastSpecialMode;

            // Primera entrada o cambio de modo
            if (!_inSpecialFollow || modeChanged)
            {
                if (!_inSpecialFollow)
                {
                    // Primer frame: desactivar NavMesh y gravedad
                    _inSpecialFollow = true;
                    _pathUpdateTimer = 0f;
                    _isWanderingNearPlayer = false;
                    _specialFollowPosition = context.Transform.position;
                    _specialFollowOffset   = _specialFollowPosition - context.Player.position;
                    _specialOffsetVel      = Vector3.zero;
                    _specialSmoothedVel    = Vector3.zero;
                    _formationYaw          = YawDe(context.Player.forward, context.Transform.eulerAngles.y);
                    _formationYawVel       = 0f;
                    _velYSuelo             = 0f;
                    _pisandoSuelo          = false;
                    _playerFlightLayerIndex = DetectFlightLayer(_playerAnimator);

                    if (context.Agent != null && context.Agent.isActiveAndEnabled)
                    {
                        // isStopped solo es válido con el agente colocado en el NavMesh (ver INC-454).
                        if (context.Agent.isOnNavMesh)
                        {
                            context.Agent.ResetPath();
                            context.Agent.isStopped = true;
                        }
                        context.Agent.updatePosition = false;
                        context.Agent.updateRotation = false;
                    }

                    if (context.Rigidbody != null && !_gravityStored)
                    {
                        _storedGravity = context.Rigidbody.useGravity;
                        context.Rigidbody.useGravity = false;
                        if (!context.Rigidbody.isKinematic)
                            context.Rigidbody.linearVelocity = Vector3.zero;
                        _gravityStored = true;
                    }
                }

                OnSpecialModeEnter(context, playerMode);
                _lastSpecialMode = playerMode;
            }

            // Calcular posición objetivo y mover
            _formationYaw = Mathf.SmoothDampAngle(_formationYaw,
                YawDe(context.Player.forward, _formationYaw), ref _formationYawVel,
                FORMATION_YAW_SMOOTH_TIME, Mathf.Infinity, Mathf.Max(Time.deltaTime, 0.0001f));

            Vector3 targetPos;
            bool usaAlturaDelRastro = false;
            bool rastroEnAire = false;
            _usandoRastro = false;

            // _specialFollowPosition es la posición "lógica" (sin bobbing) usada para toda la
            // navegación/formación. El offset visual de vuelo se aplica solo al final, sobre
            // el Transform real, para no contaminar los cálculos de distancia/destino.
            // Se mueve en bloque con el jugador y solo se suaviza el hueco hasta su puesto (INC-895).
            float   dt        = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 playerPos = context.Player.position;
            Vector3 prevPos   = _specialFollowPosition;

            bool puedeUsarRastro = playerMode == ActionMode.Default
                                   && (_config == null || _config.seguirRastroEnPlataformas)
                                   && _party != null && _party.TotalMuestrasRastro >= 2;

            if (puedeUsarRastro)
            {
                if (_partyMember != null && _partyMember.ConsumirReinicioDeRastro())
                    _rastroInicializado = false;

                if (!_rastroInicializado
                    || _cursorDistanciaRastro > _party.DistanciaTotalRastro + 0.5f)
                {
                    InicializarCursorRastro(context);
                }

                // El reparto de puestos puede cambiar si el grupo se recompone (un miembro entra,
                // sale o pasa el mando). Si cambia, se vuelve a enganchar al rastro con su nuevo
                // hueco; si no, dos compañeros podrían apuntar al mismo sitio y montarse.
                if (Time.time >= _tiempoSiguientePuestoRastro)
                {
                    int puestoAnterior = _puestoRastro;
                    int totalAnterior = _totalPuestosRastro;
                    ActualizarDatosDelRastro();

                    if (puestoAnterior != _puestoRastro || totalAnterior != _totalPuestosRastro)
                    {
                        _rastroInicializado = false;
                        if (_tieneTurnoSalto)
                        {
                            _party.LiberarTurnoDeSalto(_partyMember);
                            _tieneTurnoSalto = false;
                        }
                    }

                    _tiempoSiguientePuestoRastro = Time.time + 0.5f;
                }

                float total = _party.DistanciaTotalRastro;
                float detras = Mathf.Max(0.5f, _distanciaBaseRastro + _puestoRastro * _separacionPorPuestoRastro);
                float objetivo = Mathf.Max(0f, total - detras);

                bool haySalto = _party.HaySaltoEnTramoDeRastro(
                    _cursorDistanciaRastro - 0.05f, total,
                    out float distanciaDespegue, out float distanciaAterrizaje);

                float espacioTrasAterrizaje = _config != null && _config.espacioTrasAterrizaje > 0.01f
                    ? _config.espacioTrasAterrizaje : 2.5f;
                bool saltoCompleto = haySalto && !_party.JugadorEnSaltoConfirmado
                                     && distanciaAterrizaje < total - 0.05f;

                // Además del hueco que pida el diseño, el jugador tiene que haberse alejado lo
                // suficiente para que el puesto de este compañero caiga YA en la plataforma de
                // destino. Si no, cruzaría y volvería a caminar hacia atrás, hacia el hueco.
                float distanciaMinima = Mathf.Max(espacioTrasAterrizaje, detras + 0.3f);
                bool espacioLibre = saltoCompleto
                                    && (total - distanciaAterrizaje) >= distanciaMinima;

                // El turno puede haberse soltado por seguridad o al vaciar el rastro.
                if (_tieneTurnoSalto && !_enSaltoDeRastro
                    && !_party.TieneTurnoDeSalto(_partyMember))
                    _tieneTurnoSalto = false;

                if (haySalto)
                {
                    if (distanciaDespegue > _ultimoSaltoEstudiado + 0.3f)
                    {
                        _ultimoSaltoEstudiado = distanciaDespegue;
                        _saltoConEspacio = false;
                        _tiempoFinEstudioSalto = 0f;
                    }

                    if (espacioLibre && !_saltoConEspacio)
                    {
                        _saltoConEspacio = true;
                        _tiempoFinEstudioSalto = Time.time + _tiempoEstudioSalto + _puestoRastro * 0.16f;
                    }

                    // El primer puesto espera en el mismo borde; el segundo, un metro atrás.
                    float esperaDetras = Mathf.Max(0f, distanciaDespegue - _puestoRastro * 1f);

                    if (!_enSaltoDeRastro)
                    {
                        bool enElBorde = _cursorDistanciaRastro >= esperaDetras - 0.1f;
                        if (!enElBorde || !espacioLibre || Time.time < _tiempoFinEstudioSalto)
                        {
                            // Se acerca al borde y espera, mirando el salto del jugador.
                            objetivo = Mathf.Min(objetivo, esperaDetras);
                        }
                        else
                        {
                            if (_tieneTurnoSalto && !_party.TieneTurnoDeSalto(_partyMember))
                                _tieneTurnoSalto = false;

                            if (!_tieneTurnoSalto)
                            {
                                Vector3 puntoAterrizaje = context.Player.position;
                                if (_party.IntentarMuestrearRastroEnDistancia(
                                        Mathf.Min(total, distanciaAterrizaje + 0.15f),
                                        out PlayerParty.PuntoDeRastro muestraAterrizaje))
                                    puntoAterrizaje = muestraAterrizaje.posicion;

                                if (_party.IntentarTomarTurnoDeSalto(_partyMember, puntoAterrizaje))
                                    _tieneTurnoSalto = true;
                            }

                            if (_tieneTurnoSalto)
                            {
                                // Turno concedido: cruza el arco entero, aunque el jugador se pare.
                                objetivo = Mathf.Max(objetivo,
                                    Mathf.Min(total, distanciaAterrizaje + 0.25f));
                            }
                            else
                            {
                                // Otro compañero está cruzando: se queda en la cola.
                                objetivo = Mathf.Min(objetivo, esperaDetras);
                            }
                        }
                    }
                    else
                    {
                        // Ya está cruzando: conserva el turno hasta aterrizar.
                        _tieneTurnoSalto = true;
                    }
                }

                float velocidadCursor = _enSaltoDeRastro
                    ? Mathf.Max(VELOCIDAD_MINIMA_SALTO_RASTRO, _smoothedPlayerSpeed * 1.15f)
                    : (objetivo - _cursorDistanciaRastro > 1.5f
                        ? Mathf.Max(GetRunSpeed(), _smoothedPlayerSpeed * 1.2f)
                        : GetWalkSpeed());
                _cursorDistanciaRastro = Mathf.MoveTowards(_cursorDistanciaRastro, objetivo,
                    velocidadCursor * dt);

                if (_party.IntentarMuestrearRastroEnDistancia(_cursorDistanciaRastro, out PlayerParty.PuntoDeRastro muestra))
                {
                    _usandoRastro = true;
                    _direccionRastro = muestra.direccion;
                    _direccionRastro.y = 0f;
                    if (_direccionRastro.sqrMagnitude < 0.0001f)
                        _direccionRastro = context.Player.forward;
                    else
                        _direccionRastro.Normalize();

                    float separacion = _totalPuestosRastro > 1 ? _separacionLateralRastro : 0f;
                    float lado = _puestoRastro % 2 == 0 ? 1f : -1f;
                    Vector3 lateral = Vector3.Cross(Vector3.up, _direccionRastro) * (lado * separacion);

                    targetPos = muestra.posicion + lateral;
                    rastroEnAire = muestra.enAire;
                    usaAlturaDelRastro = true;
                }
                else
                {
                    targetPos = GetFormationTarget3D(context, playerMode);
                }
            }
            else
            {
                targetPos = GetFormationTarget3D(context, playerMode);
            }

            float tiempoOffset = usaAlturaDelRastro
                ? (rastroEnAire ? 0.08f : 0.16f)
                : SPECIAL_OFFSET_SMOOTH_TIME;
            float maxSpeedOffset = usaAlturaDelRastro
                ? Mathf.Max(SPECIAL_OFFSET_MAX_SPEED, _smoothedPlayerSpeed * 1.8f)
                : SPECIAL_OFFSET_MAX_SPEED;
            _specialFollowOffset = Vector3.SmoothDamp(_specialFollowOffset, targetPos - playerPos,
                ref _specialOffsetVel, tiempoOffset, maxSpeedOffset, dt);
            _specialSlotError = Vector3.Distance(_specialFollowOffset, targetPos - playerPos);
            _specialFollowPosition = playerPos + _specialFollowOffset;

            // Sin volar ni nadar, la altura sale del rastro (o del suelo bajo el puesto en el
            // respaldo en V). Así no se sube y baja con el salto del jugador, pero sí se cruza
            // el arco cuando el rastro lo marca.
            bool estabaEnSaltoDeRastro = _enSaltoDeRastro;
            _pisandoSuelo = false;
            bool vaPorElSuelo = playerMode != ActionMode.Flying && playerMode != ActionMode.Swimming;
            if (usaAlturaDelRastro)
            {
                _enSaltoDeRastro = rastroEnAire;
                if (rastroEnAire)
                {
                    float y = Mathf.SmoothDamp(prevPos.y, targetPos.y, ref _velYSuelo,
                        0.04f, Mathf.Infinity, dt);
                    _specialFollowPosition.y = y;
                    _specialFollowOffset.y   = y - playerPos.y;
                    _specialOffsetVel.y      = 0f;
                    _pisandoSuelo = false;
                }
                else
                {
                    float yReferencia = targetPos.y;
                    float yFinal = yReferencia;
                    if (BuscarSuelo(_specialFollowPosition, yReferencia, yReferencia,
                                    out float ySuelo, CAIDA_MAXIMA_DESDE_RASTRO))
                        yFinal = ySuelo;

                    float y = Mathf.SmoothDamp(prevPos.y, yFinal, ref _velYSuelo,
                        SUELO_SMOOTH_TIME, Mathf.Infinity, dt);
                    _specialFollowPosition.y = y;
                    _specialFollowOffset.y   = y - playerPos.y;
                    _specialOffsetVel.y      = 0f;
                    _pisandoSuelo = true;
                }

                if (_partyMember != null) _partyMember.EnSaltoDeRastro = _enSaltoDeRastro;
            }
            else if (vaPorElSuelo
                     && BuscarSuelo(_specialFollowPosition, Mathf.Max(playerPos.y, prevPos.y),
                                    playerPos.y, out float ySueloRespaldo))
            {
                float y = Mathf.SmoothDamp(prevPos.y, ySueloRespaldo, ref _velYSuelo,
                    SUELO_SMOOTH_TIME, Mathf.Infinity, dt);
                _specialFollowPosition.y = y;
                _specialFollowOffset.y   = y - playerPos.y;
                _specialOffsetVel.y      = 0f;
                _pisandoSuelo = true;
                _enSaltoDeRastro = false;
                if (_partyMember != null) _partyMember.EnSaltoDeRastro = false;
            }
            else
            {
                _velYSuelo = 0f;
                _enSaltoDeRastro = false;
                if (_partyMember != null) _partyMember.EnSaltoDeRastro = false;
                if (vaPorElSuelo && Time.time > _avisoSinSuelo)
                {
                    _avisoSinSuelo = Time.time + 3f;
                    Debug.LogWarning($"[Seguir] {context.Transform.name} no encuentra suelo bajo su puesto {_specialFollowPosition} (modo {playerMode}): va a la altura de {context.Player.name}.");
                }
            }

            // Al aterrizar se suelta el turno para que cruce el siguiente compañero.
            if (_tieneTurnoSalto && estabaEnSaltoDeRastro && !_enSaltoDeRastro)
            {
                if (_party != null) _party.LiberarTurnoDeSalto(_partyMember);
                _tieneTurnoSalto = false;
            }

            Vector3 instantVel = (_specialFollowPosition - prevPos) / dt;
            _specialSmoothedVel = Vector3.Lerp(_specialSmoothedVel, instantVel, Mathf.Clamp01(SPECIAL_VEL_SMOOTH_RATE * dt));
            // Solo la componente horizontal: el balanceo de flotar en el aire (o el de la superficie
            // del agua) también mueve en vertical y no debe contar como avanzar.
            float worldSpeed = new Vector2(_specialSmoothedVel.x, _specialSmoothedVel.z).magnitude;

            // Histéresis: si ya se estaba moviendo, sigue hasta bajar de MOVE_SPEED_EXIT; si estaba
            // parado, no arranca hasta superar MOVE_SPEED_ENTER.
            _isMovingSpecial = _isMovingSpecial
                ? worldSpeed > SPECIAL_MOVE_SPEED_EXIT
                : worldSpeed > SPECIAL_MOVE_SPEED_ENTER;
            bool isMoving = _isMovingSpecial;

            if (context.Rigidbody != null && !context.Rigidbody.isKinematic)
                context.Rigidbody.linearVelocity = Vector3.zero;

            // Mirar al punto de caída mientras el jugador salta (estudiar el salto); durante el
            // propio salto, mirar hacia delante por el rastro. Parado, mirar al jugador.
            Vector3 faceDir = _specialSmoothedVel; faceDir.y = 0f;
            bool jugadorSaltando = _party != null && _party.JugadorEnSaltoConfirmado;
            if (_usandoRastro && _enSaltoDeRastro && _direccionRastro.sqrMagnitude > 0.01f)
            {
                context.Animator?.FaceDirection(_direccionRastro);
            }
            else if (_pisandoSuelo && jugadorSaltando && _party != null)
            {
                // Si el rayo encuentra suelo bajo el jugador, se mira ahí; si salta sobre un
                // hueco, al propio jugador (que es hacia donde va).
                Vector3 objetivoMirada = _party.HayPuntoDeAterrizaje
                    ? _party.PuntoDeAterrizajeDelJugador
                    : context.Player.position;
                Vector3 haciaCaida = objetivoMirada - context.Transform.position;
                haciaCaida.y = 0f;
                if (haciaCaida.sqrMagnitude > 0.01f)
                    context.Animator?.FaceDirection(haciaCaida.normalized);
                else
                    context.Animator?.FaceDirection(Quaternion.Euler(0f, _formationYaw, 0f) * Vector3.forward);
            }
            else if (_usandoRastro && !_enSaltoDeRastro && !isMoving && context.Player != null)
            {
                Vector3 haciaJugador = context.Player.position - context.Transform.position;
                haciaJugador.y = 0f;
                if (haciaJugador.sqrMagnitude > 0.01f)
                    context.Animator?.FaceDirection(haciaJugador.normalized);
                else
                    context.Animator?.FaceDirection(Quaternion.Euler(0f, _formationYaw, 0f) * Vector3.forward);
            }
            else if (_specialSlotError > SPECIAL_CATCH_UP_DIST && faceDir.sqrMagnitude > 0.01f)
            {
                context.Animator?.FaceDirection(faceDir);
            }
            else
            {
                context.Animator?.FaceDirection(Quaternion.Euler(0f, _formationYaw, 0f) * Vector3.forward);
            }

            // Bobbing visual de vuelo: replica el movimiento arriba/abajo que PlayerFlyingController
            // aplica al jugador (Mathf.Sin sobre Time.time), para que los compañeros que vuelan
            // también parezcan flotar en el aire. Solo afecta al Transform final, no a la lógica.
            float bobOffset = 0f;
            if (playerMode == ActionMode.Flying)
            {
                float amp  = _config?.flightBobAmplitude ?? DEFAULT_FLIGHT_BOB_AMPLITUDE;
                float freq = _config?.flightBobFrequency ?? DEFAULT_FLIGHT_BOB_FREQUENCY;
                if (Mathf.Abs(amp) > 0.0001f && Mathf.Abs(freq) > 0.0001f)
                    bobOffset = Mathf.Sin(Time.time * (2f * Mathf.PI * freq) + _bobPhase) * amp;
            }
            context.Transform.position = _specialFollowPosition + Vector3.up * bobOffset;

            UpdateSpecialModeAnimation(context, playerMode, _specialSmoothedVel, isMoving);
        }

        /// <summary>
        /// Animación de entrada al activar un modo especial.
        /// Solo Flying/Swimming actualizan _exitMode (modo "significativo").
        /// Las transiciones transitorias por Default no sobreescriben _exitMode,
        /// así ExitSpecialFollow sabe siempre de qué modo real salimos.
        /// </summary>
        private void OnSpecialModeEnter(NPCStateContext context, ActionMode mode)
        {
            Animator anim = context.UnityAnimator;

            // Si salimos del modo vuelo hacia cualquier otro, limpiar isFlying inmediatamente
            // para que no quede el personaje en fly_idle durante la transición transitoria
            if (_lastSpecialMode == ActionMode.Flying && mode != ActionMode.Flying)
            {
                TrySetBool(anim, HashIsFlying, false);
                StopFootVfx();
            }

            switch (mode)
            {
                case ActionMode.Flying:
                    _exitMode = mode;
                    if (_tieneTurnoSalto && _party != null) _party.LiberarTurnoDeSalto(_partyMember);
                    _tieneTurnoSalto = false;
                    _saltoConEspacio = false;
                    _enSaltoDeRastro = false;
                    _estabaEnAireRastro = false;
                    if (_partyMember != null) _partyMember.EnSaltoDeRastro = false;
                    TrySetBool(anim, HashIsFlying, true);
                    _flightStateRequested = 0;
                    RequestFlightState(anim, HashFlyIdle, ignorarEspera: true);
                    SpawnFootVfx(context);
                    break;

                case ActionMode.Swimming:
                    _exitMode = mode;
                    if (_tieneTurnoSalto && _party != null) _party.LiberarTurnoDeSalto(_partyMember);
                    _tieneTurnoSalto = false;
                    _saltoConEspacio = false;
                    _enSaltoDeRastro = false;
                    _estabaEnAireRastro = false;
                    if (_partyMember != null) _partyMember.EnSaltoDeRastro = false;
                    TryPlay(anim, HashSwimFloat, 0);
                    TrySetBool(anim, HashIsGrounded, true);
                    TrySetFloat(anim, HashGroundDist, 0f);
                    break;


                // Default (plataforma sin NavMesh / salto): el compañero empieza pisando suelo.
                // El estado aéreo lo decide su propio rastro, no el salto de quien maneja.
                default:
                    TrySetBool(anim, HashIsGrounded, true);
                    TrySetFloat(anim, HashGroundDist, 0f);
                    _estabaEnAireRastro = false;
                    _enSaltoDeRastro = false;
                    _saltoConEspacio = false;
                    _ultimoSaltoEstudiado = -1f;
                    break;
            }
        }

        /// <summary>Actualización de animación cada frame según modo y movimiento.</summary>
        private void UpdateSpecialModeAnimation(NPCStateContext context, ActionMode mode,
                                                Vector3 velocity, bool isMoving)
        {
            Animator anim = context.UnityAnimator;
            if (anim == null) return;

            switch (mode)
            {
                case ActionMode.Flying:
                    RequestFlightState(anim, EstadoDeVueloDelGrupo(isMoving));
                    break;

                case ActionMode.Swimming:
                    TrySetBool(anim, HashIsGrounded, true);
                    TrySetFloat(anim, HashGroundDist, 0f);
                    // Interpolado, no asignado en seco: InputMagnitude es a la vez el blend
                    // parameter del blend tree de nado y el speed parameter de al menos un
                    // estado, así que un salto discreto entre 0 y 0.5 se ve como un tirón tanto
                    // en la pose como en el ritmo de reproducción del clip.
                    float targetInputMag = isMoving ? 0.5f : 0f;
                    _swimInputMag = Mathf.MoveTowards(_swimInputMag, targetInputMag, SWIM_INPUT_MAG_LERP_SPEED * Time.deltaTime);
                    TrySetFloat(anim, HashInputMag, _swimInputMag);
                    break;

                default:
                    Vector3 planarVel = velocity; planarVel.y = 0f;
                    context.Animator?.SetMovementSpeed(Mathf.Clamp01(planarVel.magnitude / DEFAULT_RUN_SPEED));

                    if (_usandoRastro)
                    {
                        // El salto lo marca el tramo del rastro, no el estado actual del jugador.
                        TrySetBool(anim, HashIsGrounded, !_enSaltoDeRastro);
                        TrySetFloat(anim, HashGroundDist, _enSaltoDeRastro ? 1f : 0f);

                        if (_enSaltoDeRastro && !_estabaEnAireRastro)
                        {
                            if (!TryCrossFade(anim, HashJumpMove, 0.07f, 0))
                                TryCrossFade(anim, HashJumpStart, 0.07f, 0);
                        }
                        else if (!_enSaltoDeRastro && _estabaEnAireRastro)
                        {
                            if (!TryCrossFade(anim, HashLanding, 0.08f, 0))
                                TryCrossFade(anim, HashLocomotion, 0.1f, 0);
                        }

                        _estabaEnAireRastro = _enSaltoDeRastro;
                    }
                    else if (_pisandoSuelo)
                    {
                        TrySetBool(anim, HashIsGrounded, true);
                        TrySetFloat(anim, HashGroundDist, 0f);
                    }
                    else if (_playerAnimator != null && anim != null)
                    {
                        // Respaldo en V sin rastro: si el puesto va sobre un hueco, se copia el
                        // estado aéreo de quien maneja.
                        bool playerGrounded = _playerAnimator.GetBool(HashIsGrounded);
                        float playerGroundDist = _playerAnimator.GetFloat(HashGroundDist);
                        TrySetBool(anim, HashIsGrounded, playerGrounded);
                        TrySetFloat(anim, HashGroundDist, playerGroundDist);
                    }
                    break;
            }
        }

        /// <summary>
        /// Sale del modo especial: restaura física y animator, snappea al NavMesh.
        /// Usa _exitMode (último modo significativo) para saber qué animación de salida reproducir.
        /// </summary>
        private void ExitSpecialFollow(NPCStateContext context)
        {
            Animator anim         = context.UnityAnimator;
            ActionMode exitingMode = _exitMode;   // último modo significativo (Flying/Swimming/Default)

            // Quitar cualquier offset visual de bobbing residual antes de restaurar física/navmesh
            context.Transform.position = _specialFollowPosition;

            _inSpecialFollow = false;
            _lastSpecialMode = ActionMode.Default;
            _exitMode        = ActionMode.Default;
            _flightStateRequested = 0;
            _usandoRastro = false;
            _rastroInicializado = false;
            _enSaltoDeRastro = false;
            _estabaEnAireRastro = false;
            if (_tieneTurnoSalto && _party != null) _party.LiberarTurnoDeSalto(_partyMember);
            _tieneTurnoSalto = false;
            _saltoConEspacio = false;
            if (_partyMember != null) _partyMember.EnSaltoDeRastro = false;

            // Siempre limpiar isFlying al salir para que no quede el personaje en fly_idle
            TrySetBool(anim, HashIsFlying, false);

            // Animación de salida según el modo real del que venimos
            if (anim != null)
            {
                switch (exitingMode)
                {
                    case ActionMode.Flying:
                        TrySetBool(anim, HashIsGrounded, true);
                        TrySetFloat(anim, HashGroundDist, 0f);
                        StopFootVfx();
                        if (!TryCrossFade(anim, HashFlyLanding, 0.08f, _npcFlightLayerIndex))
                            TryCrossFade(anim, HashLocomotion, 0.1f, _npcFlightLayerIndex);
                        break;

                    case ActionMode.Swimming:
                        TryCrossFade(anim, HashLocomotion, 0.1f, 0);
                        break;

                    default:
                        // Plataforma/salto: restaurar estado grounded para no quedar en mid-air
                        TrySetBool(anim, HashIsGrounded, true);
                        TrySetFloat(anim, HashGroundDist, 0f);
                        break;
                }
            }

            // Restaurar gravedad
            if (context.Rigidbody != null && _gravityStored)
            {
                context.Rigidbody.useGravity = _storedGravity;
                if (!context.Rigidbody.isKinematic)
                    context.Rigidbody.linearVelocity = Vector3.zero;
                _gravityStored = false;
            }

            // Snap al NavMesh y devolver control al agent
            if (context.Agent != null && context.Agent.isActiveAndEnabled)
            {
                // Activar updatePosition ANTES del Warp para que éste actualice el transform
                context.Agent.updatePosition = true;

                if (!context.Agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(context.Transform.position, out NavMeshHit hit, WARP_SEARCH_RADIUS, NavMesh.AllAreas)
                        && hit.position.y >= context.Transform.position.y - 1.5f)
                    {
                        // Mover el transform directamente además del Warp como garantía extra.
                        // No se hace si la malla más cercana es el fondo de un foso.
                        context.Transform.position = hit.position;
                        context.Agent.Warp(hit.position);
                    }
                }
                else
                {
                    context.Agent.nextPosition = context.Transform.position;
                }

                if (context.Agent.isOnNavMesh) context.Agent.isStopped = true;
                context.Agent.updateRotation = false;
            }
        }

        // =========================================================================
        // FORMACIÓN 3D
        // =========================================================================

        /// <summary>
        /// Puesto en el seguimiento especial (vuelo, nado, plataformas): en V detrás del jugador,
        /// orientada con el rumbo suavizado del grupo y a su misma altura.
        /// Puesto 0: detrás a la derecha · 1: detrás a la izquierda · 2: segunda fila a la derecha…
        /// Quien sigue sin estar en PlayerParty (p. ej. el NPC de Will) va al primer puesto libre
        /// tras los del equipo; antes caía en el mismo sitio que el puesto 0 (detrás en el centro)
        /// y además ese puesto, a un metro, se metía en el cuerpo de Will.
        /// </summary>
        private Vector3 GetFormationTarget3D(NPCStateContext context, ActionMode mode)
        {
            int   puesto  = _puestoRastro;
            if (_partyMember != null && _partyMember.PartyIndex >= 0)
                puesto = _partyMember.PartyIndex;

            bool aPie = mode == ActionMode.Default;
            float spreadAire = _config != null && _config.flightFormationSpread > 0.01f
                ? _config.flightFormationSpread : 1.5f;
            float detrasAire = _config != null && _config.flightFormationBack > 0.01f
                ? _config.flightFormationBack : 1.8f;
            float spreadPie = _config != null && _config.plataformaFormationSpread > 0.01f
                ? _config.plataformaFormationSpread : 1.35f;
            float detrasPie = _config != null && _config.plataformaFormationBack > 0.01f
                ? _config.plataformaFormationBack : 2.4f;
            float spread = aPie ? spreadPie : spreadAire;
            float detras = aPie ? detrasPie : detrasAire;
            int   fila    = puesto / 2 + 1;
            float lado    = puesto % 2 == 0 ? 1f : -1f;
            Vector3 local = new Vector3(lado * spread * fila, 0f, -detras * fila);
            Vector3 hueco = Quaternion.Euler(0f, _formationYaw, 0f) * local;

            // En el agua, a la altura de flote del jugador: si Will se zambulle, los demás no bucean con él.
            float y = _playerSwimming != null && _playerSwimming.EstaEnElAgua
                ? _playerSwimming.AlturaDeFlote
                : context.Player.position.y;

            return new Vector3(context.Player.position.x + hueco.x, y, context.Player.position.z + hueco.z);
        }

        /// <summary>Rumbo (grados) de una dirección en el plano; si es vertical, el de respaldo.</summary>
        private static float YawDe(Vector3 forward, float respaldo)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
                : respaldo;
        }

        /// <summary>
        /// Pose de vuelo del compañero: la misma que lleva Will (picado o quieto), leída de su
        /// Animator, para que el grupo cambie a la vez y no cada uno por su cuenta. Si Will está en
        /// otra pose (p. ej. la de lanzar un hechizo) se mantiene la que había. Alcanzando al grupo
        /// desde lejos, picado.
        /// </summary>
        private int EstadoDeVueloDelGrupo(bool isMoving)
        {
            if (_specialSlotError > SPECIAL_CATCH_UP_DIST) return HashFlyDive;
            if (_playerAnimator != null)
            {
                try
                {
                    var info = _playerAnimator.IsInTransition(_playerFlightLayerIndex)
                        ? _playerAnimator.GetNextAnimatorStateInfo(_playerFlightLayerIndex)
                        : _playerAnimator.GetCurrentAnimatorStateInfo(_playerFlightLayerIndex);
                    if (info.shortNameHash == HashFlyDive || info.shortNameHash == HashFlyIdle)
                        return info.shortNameHash;
                    if (_flightStateRequested != 0) return _flightStateRequested;
                }
                catch { }
            }
            return isMoving ? HashFlyDive : HashFlyIdle;
        }

        // =========================================================================
        // HELPERS ANIMATOR (acceso seguro al Animator raw)
        // =========================================================================

        /// <summary>
        /// Detecta el layer del animator del NPC que contiene los estados de vuelo.
        /// Equivalente a PlayerFlyingController.DetectFlightLayer para evitar hardcodear layer 0.
        /// </summary>
        private static int DetectFlightLayer(Animator anim)
        {
            if (anim == null) return 0;
            int hash = Animator.StringToHash("fly_idle");
            for (int i = 0; i < anim.layerCount; i++)
            {
                try { if (anim.HasState(i, hash)) return i; }
                catch { }
            }
            return 0;
        }

        private void SpawnFootVfx(NPCStateContext context)
        {
            var prefab = _config?.footVfxPrefab;
            if (prefab == null || _footVfxInstance != null) return;
            _footVfxInstance = Object.Instantiate(prefab, context.Transform);
            _footVfxPs = _footVfxInstance.GetComponent<ParticleSystem>();
            _footVfxInstance.SetActive(true);
            if (_footVfxPs != null && !_footVfxPs.isPlaying) _footVfxPs.Play();
        }

        private void StopFootVfx()
        {
            if (_footVfxInstance == null) return;
            if (_footVfxPs != null) _footVfxPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(_footVfxInstance);
            _footVfxInstance = null;
            _footVfxPs = null;
        }

        private static void TryPlay(Animator anim, int stateHash, int layer)
        {
            if (anim == null) return;
            try
            {
                if (anim.HasState(layer, stateHash) &&
                    anim.GetCurrentAnimatorStateInfo(layer).shortNameHash != stateHash)
                    anim.Play(stateHash, layer);
            }
            catch { }
        }

        /// <returns>true si el estado existe y se hizo crossfade.</returns>
        private static bool TryCrossFade(Animator anim, int stateHash, float duration, int layer)
        {
            if (anim == null) return false;
            try
            {
                if (!anim.HasState(layer, stateHash)) return false;
                if (anim.GetCurrentAnimatorStateInfo(layer).shortNameHash != stateHash)
                    anim.CrossFade(stateHash, duration, layer);
                return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Pide un estado de vuelo con fundido, una sola vez por cambio (no cada frame: repetir el
        /// CrossFade lo reiniciaría y durante el fundido el estado actual sigue siendo el anterior).
        /// Tras un cambio espera FLIGHT_STATE_MIN_HOLD antes de aceptar otro. INC-895.
        /// </summary>
        private void RequestFlightState(Animator anim, int stateHash, bool ignorarEspera = false)
        {
            if (anim == null || stateHash == _flightStateRequested) return;
            if (!ignorarEspera && Time.time - _flightStateSince < FLIGHT_STATE_MIN_HOLD) return;
            try
            {
                if (!anim.HasState(_npcFlightLayerIndex, stateHash)) return;
                anim.CrossFadeInFixedTime(stateHash, FLIGHT_STATE_CROSSFADE, _npcFlightLayerIndex);
            }
            catch { return; }
            _flightStateRequested = stateHash;
            _flightStateSince     = Time.time;
        }

        /// <summary>
        /// Snapshot de los hashes de parámetros del Animator, tomado una sola vez al entrar al estado.
        /// No apto para llamar en Update: recorre anim.parameters, que es costoso.
        /// </summary>
        private static HashSet<int> BuildAnimatorParamSet(Animator anim)
        {
            var set = new HashSet<int>();
            if (anim == null) return set;
            try
            {
                var parameters = anim.parameters;
                for (int i = 0; i < parameters.Length; i++)
                    set.Add(parameters[i].nameHash);
            }
            catch { }
            return set;
        }

        private void TrySetBool(Animator anim, int paramHash, bool value)
        {
            if (anim == null) return;
            if (_animatorParams != null && !_animatorParams.Contains(paramHash)) return;
            try { anim.SetBool(paramHash, value); } catch { }
        }

        private void TrySetFloat(Animator anim, int paramHash, float value)
        {
            if (anim == null) return;
            if (_animatorParams != null && !_animatorParams.Contains(paramHash)) return;
            try { anim.SetFloat(paramHash, value); } catch { }
        }

        // =========================================================================
        // HELPERS ORIGINALES
        // =========================================================================

        /// <summary>
        /// Estima la velocidad real actual del jugador por delta de posición (suavizada), para usar
        /// como suelo dinámico de la velocidad de seguimiento en vez de constantes fijas — ver
        /// comentario de _smoothedPlayerSpeed en la cabecera. Ignora saltos de posición enormes en un
        /// solo frame (teletransportes) para que no disparen la media hacia un pico irreal.
        /// </summary>
        private void UpdatePlayerSpeedEstimate(NPCStateContext context)
        {
            if (!_speedSampleInitialized)
            {
                _prevPlayerPosForSpeed = context.Player.position;
                _speedSampleInitialized = true;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = context.Player.position - _prevPlayerPosForSpeed;
            _prevPlayerPosForSpeed = context.Player.position;

            float instantaneousSpeed = delta.magnitude / dt;
            if (instantaneousSpeed > PLAYER_SPEED_TELEPORT_CUTOFF)
                return; // teletransporte/warp: no es locomoción real, no contaminar la media

            _smoothedPlayerSpeed = Mathf.Lerp(_smoothedPlayerSpeed, instantaneousSpeed,
                Mathf.Clamp01(PLAYER_SPEED_SMOOTH_RATE * dt));
        }

        private const float PUESTO_LLEGADA = 0.7f;

        /// Puesto junto al jugador cuando el compañero va a su lado: en el lado y a la distancia
        /// de ladoPreferidoDialogo / distanciaLateralDialogo, algo adelantado según lo rápido que
        /// va Will para que no llegue siempre tarde.
        private Vector3 PuestoAlLado(NPCStateContext context)
        {
            Transform p = context.Player;
            float lado = _config.ladoPreferidoDialogo == DialoguePositionSide.Left ? -1f : 1f;
            Vector3 derecha = p.right; derecha.y = 0f; derecha.Normalize();
            Vector3 frente = p.forward; frente.y = 0f; frente.Normalize();
            float adelanto = Mathf.Min(_smoothedPlayerSpeed * 0.35f, 1.5f);
            return p.position + derecha * (lado * _config.distanciaLateralDialogo) + frente * adelanto;
        }

        private static float DistanciaPlana(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void RotateTowardsPlayer(NPCStateContext context)
        {
            if (context.Player == null) return;
            Vector3 dir = context.Player.position - context.Transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.Angle(context.Transform.forward, dir.normalized);
                if (angle <= ROTATION_ANGLE_DEADZONE) return;
                context.Animator?.FaceDirection(dir);
            }
        }

        private void UpdateDestination(NPCStateContext context, float followDist)
        {
            _tieneDestinoFormacion = false;
            bool preferBehind = _config?.quedarseDetras ?? true;
            if (!preferBehind && _config != null)
            {
                Vector3 puesto = PuestoAlLado(context);
                if (NavMesh.SamplePosition(puesto, out NavMeshHit hitLado, 2f, NavMesh.AllAreas))
                {
                    if (Vector3.Distance(context.Agent.destination, hitLado.position) > 0.3f)
                        context.Agent.SetDestination(hitLado.position);
                    return;
                }
            }

            // Detrás: el puesto de cada uno en la formación del grupo (abanico), no todos al mismo punto.
            Vector3 targetPos = preferBehind && _partyMember != null
                ? _partyMember.GetFormationPosition()
                : context.Player.position + (context.Transform.position - context.Player.position).normalized * followDist;

            // El muestreo puede caer en el fondo de un foso que está a menos de 3 m o delante del
            // jugador. Se prefiere el hueco solo si está a su altura y detrás; si no, se va hacia
            // el jugador.
            Vector3 adelanteJugador = context.Player.forward;
            adelanteJugador.y = 0f;
            if (adelanteJugador.sqrMagnitude < 0.0001f) adelanteJugador = Vector3.forward;
            adelanteJugador.Normalize();

            Vector3 destino;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - context.Player.position.y) <= 2.2f
                && (!preferBehind || Vector3.Dot(hit.position - context.Player.position, adelanteJugador) <= 0.25f))
            {
                destino = hit.position;
            }
            else if (NavMesh.SamplePosition(context.Player.position, out NavMeshHit hitJugador, 2f, NavMesh.AllAreas)
                     && Mathf.Abs(hitJugador.position.y - context.Player.position.y) <= 2.2f)
            {
                destino = hitJugador.position;
            }
            else
            {
                destino = context.Player.position;
            }

            _formationTarget = destino;
            _tieneDestinoFormacion = preferBehind;
            if (Vector3.Distance(context.Agent.destination, destino) > 0.5f)
                context.Agent.SetDestination(destino);
        }

        private float GetWalkSpeed() => _config?.velocidadCaminando ?? DEFAULT_WALK_SPEED;

        /// <summary>
        /// Fija Agent.speed y escala Agent.acceleration junto con ella (ver comentario de
        /// AGENT_ACCELERATION_MULTIPLIER) para que el NavMeshAgent pueda alcanzar de verdad
        /// velocidades altas de catch-up en vez de quedarse limitado por la aceleración fija
        /// serializada en el prefab.
        /// </summary>
        private static void SetAgentSpeed(NavMeshAgent agent, float speed)
        {
            agent.speed = speed;
            agent.acceleration = Mathf.Max(AGENT_MIN_ACCELERATION, speed * AGENT_ACCELERATION_MULTIPLIER);
        }

        private void TryStartPartyWander(NPCStateContext context, float stopDist)
        {
            if (context.Player == null || context.Agent == null) return;
            float radius = _config.radioVagabundeo;
            Vector3 randomDir = Random.insideUnitSphere; randomDir.y = 0;
            if (randomDir.sqrMagnitude < 0.01f) randomDir = Vector3.right;
            Vector3 candidate = context.Player.position + randomDir.normalized * Random.Range(radius * 0.4f, radius);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas)) return;

            _isWanderingNearPlayer = true;
            _wanderTarget = hit.position;
            _idleTimer = 0f;
            if (!context.Agent.updatePosition)
            {
                context.Agent.nextPosition = context.Transform.position;
                context.Agent.updatePosition = true;
            }
            context.Agent.isStopped = false;
            SetAgentSpeed(context.Agent, _config.velocidadCaminando);
            context.Agent.SetDestination(_wanderTarget);
        }

        public override void OnExit(NPCStateContext context)
        {
            context.IsActivelyFollowingPlayer = false;
            if (_autoBrakingGuardado && context.Agent != null)
            {
                context.Agent.autoBraking = _autoBrakingOriginal;
                _autoBrakingGuardado = false;
            }

            _usandoRastro = false;
            _rastroInicializado = false;
            _enSaltoDeRastro = false;
            _estabaEnAireRastro = false;
            if (_tieneTurnoSalto && _party != null) _party.LiberarTurnoDeSalto(_partyMember);
            _tieneTurnoSalto = false;
            _saltoConEspacio = false;
            if (_partyMember != null) _partyMember.EnSaltoDeRastro = false;

            if (_inSpecialFollow)
            {
                _inSpecialFollow = false;

                // Quitar cualquier offset visual de bobbing residual
                context.Transform.position = _specialFollowPosition;

                // Siempre limpiar isFlying al salir del estado
                TrySetBool(context.UnityAnimator, HashIsFlying, false);
                StopFootVfx();

                if (context.Rigidbody != null && _gravityStored)
                {
                    context.Rigidbody.useGravity = _storedGravity;
                    context.Rigidbody.linearVelocity = Vector3.zero;
                    _gravityStored = false;
                }
            }

            if (context.Agent != null && context.Agent.isActiveAndEnabled)
            {
                context.Agent.updatePosition = true;
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.isStopped = true;
                    context.Agent.nextPosition = context.Transform.position;
                }
                else if (NavMesh.SamplePosition(context.Transform.position, out NavMeshHit hit, WARP_SEARCH_RADIUS, NavMesh.AllAreas)
                         && hit.position.y >= context.Transform.position.y - 1.5f)
                {
                    context.Transform.position = hit.position;
                    context.Agent.Warp(hit.position);
                }
                context.Agent.updateRotation = false;
            }

            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic) return new CinematicState();
            if (context.IsInCombat) return new AllyCombatState();
            if (!_skipPartyCheck && _partyMember != null && !_partyMember.IsInParty)
                return new IdleState();
            return null;
        }
    }
}
