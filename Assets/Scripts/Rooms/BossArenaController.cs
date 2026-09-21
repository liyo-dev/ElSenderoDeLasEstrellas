using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.AI;
using Game.NPC; // Necesario para NPCBehaviourManagerV2 (bosses basados en FSM, p.ej. Mago Oscuro)

public class BossArenaController : MonoBehaviour
{
    [Header("Modo de Arena")]
    [Tooltip("Si es true, usa puertas. Si es false, usa un área delimitada por collider")]
    [SerializeField] private bool useDoorMode = true;

    [Header("Puertas (solo si useDoorMode = true)")]
    public DoorGate doorWest;   // Door_W/Barrier
    public DoorGate doorEast;   // Door_E/Barrier

    [Header("Área Delimitada (solo si useDoorMode = false)")]
    [Tooltip("Collider que delimita el área. Debe ser trigger. Si es null, se usa el collider de este GameObject")]
    [SerializeField] private Collider areaBarrierCollider;
    [Tooltip("Altura de la barrera visual. Si es 0, se calcula automáticamente")]
    [SerializeField] private float barrierHeight = 10f;
    [Tooltip("Grosor de la barrera visual")]
    [SerializeField] private float barrierThickness = 0.5f;

    [Header("Visual (Build-safe)")]
    [Tooltip("Material asset con tu shader URP (precompilado). No generar shaders en runtime.")]
    [SerializeField] private Material barrierMaterial;   // Asigna tu .mat
    [SerializeField] private Color barrierColor = new(0.3f, 0.5f, 1f, 0.25f);
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;
    [SerializeField] private float rimPower = 2f;
    [SerializeField] private float rimStrength = 1.5f;
    [SerializeField] private Vector2 noiseTiling = new(2, 2);
    [SerializeField] private Vector2 noiseSpeed = new(0.2f, 0f);
    [SerializeField] private float noiseStrength = 1f;
    [SerializeField] private float globalAlpha = 1f;

    private GameObject _barrierVisual;
    private BossArenaBarrier _barrierEffect; // referencia al primero para batch (Show/Hide)
    private bool _areaLocked = false;

    [Header("Spawn")]
    public Transform bossSpawn;
    public GameObject bossPrefab;   // tu boss
    public Transform portalSpawn;
    public GameObject portalPrefab; // PF_PortalExit
    [Tooltip("Layer del suelo para colocar al boss después de la presentación.")]
    [SerializeField] private LayerMask floorLayer = 1 << 6; // Floor por defecto
    
    [Header("VFX de Aparición del Boss")]
    [Tooltip("Prefab de VFX para la aparición del boss (portal, humo, etc). Se destruye automáticamente.")]
    [SerializeField] private GameObject bossSpawnVFXPrefab;
    [Tooltip("Duración en segundos del VFX de aparición antes de destruirse.")]
    [SerializeField] private float bossSpawnVFXDuration = 2f;
    [Tooltip("Offset vertical para el VFX de aparición (por defecto 0).")]
    [SerializeField] private float bossSpawnVFXHeightOffset = 0f;
    
    [Header("Boss Presentation")]
    [Tooltip("Componente opcional para presentación cinemática del boss")]
    [SerializeField] private BossIntroPresentation bossIntroPresentation;
    
    [Tooltip("ID de localización para el nombre del boss (ej: 'BOSS_DEMONIO_NAME'). Si está vacío, usa bossDisplayName.")]
    [SerializeField] private string bossDisplayNameId;
    [Tooltip("Nombre del boss para mostrar en la presentación (ej: 'DEMONIO'). Fallback si bossDisplayNameId no resuelve.")]
    [SerializeField] private string bossDisplayName = "DEMONIO";

    /// <summary>Obtiene el nombre localizado del boss (usa bossDisplayNameId si está definido).</summary>
    public string GetLocalizedBossName()
    {
        string id = (_activeEncounter != null && !string.IsNullOrEmpty(_activeEncounter.displayNameId)) ? _activeEncounter.displayNameId : bossDisplayNameId;
        string fallback = (_activeEncounter != null && !string.IsNullOrEmpty(_activeEncounter.displayName)) ? _activeEncounter.displayName : bossDisplayName;

        if (!string.IsNullOrEmpty(id) && LocalizationManager.Instance != null)
            return LocalizationManager.Instance.Get(id, fallback);
        return fallback;
    }

    [Header("Refs")]
    public RoomGoal roomGoal;
    public string playerTag = "Player";
    public AudioSource musicBoss;    // opcional

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    [Header("Progreso")]
    [Tooltip("ID único del boss para registrar su derrota en el guardado (ej: 'DEMONIO_01').")]
    [SerializeField] private string bossId;

    [Header("Battle Identification")]
    [Tooltip("ID único de la arena/batalla para poder activarla por referencia (ej: 'BATTLE_DEMON_01'). Si está vacío, no se registrará.")]
    [SerializeField] private string battleId;

    // Si no quieres mantener dos ids, por defecto usamos bossId como battleId cuando éste está vacío.
#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(battleId) && !string.IsNullOrEmpty(bossId))
        {
            battleId = bossId;
        }
    }
#endif

    // Registro estático para buscar arenas por id desde otros sistemas (p.ej. la cinematica)
    private static readonly System.Collections.Generic.Dictionary<string, BossArenaController> s_arenaRegistry = new();

    // Eventos globales para que otros sistemas (ej: minimapa) reaccionen al inicio/fin de batalla
    public static event Action OnAnyBattleStarted;
    public static event Action OnAnyBattleEnded;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_arenaRegistry.Clear();
        OnAnyBattleStarted = null;
        OnAnyBattleEnded = null;
    }
#endif

    // Exponer el id públicamente de solo lectura
    public string BattleId => battleId;

    [Header("Eventos")]
    [SerializeField] private UnityEvent onBossDefeated;

    [Header("Bloqueos durante batalla")]
    [Tooltip("Lista de objetos a desactivar al iniciar la batalla y restaurar al finalizar.")]
    [SerializeField] private GameObject[] toDisableDuringBattle;

    private readonly Dictionary<GameObject, bool> _prevActiveStates = new();

    [Header("Activacion manual")]
    [Tooltip("Cuando es true, al entrar el jugador se inicia la batalla (verifica si el boss ya fue derrotado).")]
    [SerializeField] private bool startBarrierOnPlayerEnter;

    [Header("Modo Radio alrededor del jugador (INC-207)")]
    [Tooltip("Si es true (o si el nodo del grafo asigna un BattleEncounterSO), la arena usa un radio calculado dinámicamente alrededor de la posición del jugador al empezar la batalla, en vez de puertas o de un área delimitada por collider. Tiene prioridad sobre useDoorMode/areaBarrierCollider cuando está activo.")]
    [SerializeField] private bool useRadiusMode = false;

    [Tooltip("Radio en metros (solo si useRadiusMode = true y no hay BattleEncounterSO asignado desde el nodo — la SO trae su propio radio).")]
    [SerializeField, Min(1f)] private float radiusMeters = 15f;

    [Tooltip("Cada cuántos segundos se comprueba si el jugador ha cruzado el radio. Throttled a propósito (igual que NPCObstacleAvoidance) — nunca por frame.")]
    [SerializeField, Min(0.05f)] private float radiusCheckInterval = 0.2f;

    [Tooltip("Clave de localización del mensaje mostrado (vía HudToastService) si el jugador intenta cruzar el radio.")]
    [SerializeField] private string cannotFleeLocKey = "BATTLE_CANNOT_FLEE";

    /// Segundos que dura en pantalla el aviso de "no puedes huir" y, a la vez, tiempo mínimo
    /// entre dos avisos consecutivos (ver Co_EnforceRadius).
    private const float CannotFleeToastDuration = 3f;

    // Estado del modo radio — no serializado: se fija en tiempo de ejecución al empezar la batalla.
    private BattleEncounterSO _activeEncounter;
    private int _activeSpawnProfileIndex;
    private Vector3 _arenaCenter;
    private Quaternion _arenaPlayerRotation = Quaternion.identity;
    private float _effectiveRadius;
    private bool _radiusLocked;
    private Coroutine _radiusEnforceRoutine;
    private LayerMask _activeFloorLayer;

    bool started = false;
    bool _bossDefeatHandled = false;
    bool _bossDeathConfirmed = false;
    bool _pendingStartBattle = false;
    bool _profileReady = false; // ✅ Flag para rastrear si el perfil está listo
    EnemyMarker _activeBossMarker;
    Damageable _activeBossDamageable;
    BossHealthBar _activeBossHealthBar;

    public void SetStartBarrierOnPlayerEnter(bool value)
    {
        startBarrierOnPlayerEnter = value;
    }

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        // Runtime fallback: si no hay battleId explícito, usar bossId para registrar la arena
        if (string.IsNullOrEmpty(battleId) && !string.IsNullOrEmpty(bossId))
        {
            battleId = bossId;
        }

        // Si no se especifica un collider de barrera, usar el del propio GameObject
        if (!useDoorMode && areaBarrierCollider == null)
        {
            areaBarrierCollider = GetComponent<Collider>();
        }

        // Crear la barrera visual si estamos en modo área
        if (!useDoorMode && areaBarrierCollider != null)
        {
            CreateBarrierVisual();
        }

        // Registrar en el diccionario por battleId si procede
        if (!string.IsNullOrEmpty(battleId))
        {
            if (s_arenaRegistry.TryGetValue(battleId, out var existing) && existing != this)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[BossArenaController] BattleId '{battleId}' ya registrado por otro BossArenaController. Sobrescribiendo registro.");
#endif
            }
            s_arenaRegistry[battleId] = this;
            // Diagnostic log: confirmar registro en runtime (útil para debugging)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] Registrada arena con BattleId='{battleId}' en scene='{gameObject.scene.name}' (active={gameObject.activeInHierarchy}, enabled={this.enabled}).");
#endif
        }
    }

    void OnEnable()
    {
        BossProgressTracker.OnProgressRestored += HandleBossProgressRestored;
        GameBootService.OnProfileReady += HandleProfileReady; // ✅ Suscribirse a OnProfileReady
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(BossArenaController)); // 🔍 Registrar para diagnósticos

        // Asegurar registro en OnEnable (por si el orden de Awake/Start causa que el registro aún no exista
        // cuando otros sistemas intenten activarlo). Normalizamos la clave.
        if (!string.IsNullOrEmpty(battleId) || !string.IsNullOrEmpty(bossId))
        {
            var key = (!string.IsNullOrEmpty(battleId) ? battleId : bossId)?.Trim();
            if (!string.IsNullOrEmpty(key))
            {
                if (s_arenaRegistry.TryGetValue(key, out var existing) && existing != this)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[BossArenaController] OnEnable: BattleId '{key}' ya registrado por otro BossArenaController. Sobrescribiendo registro.");
#endif
                }
                s_arenaRegistry[key] = this;
                //Debug.Log($"[BossArenaController] OnEnable: Registrada arena con BattleId='{key}' (scene='{gameObject.scene.name}', active={gameObject.activeInHierarchy}).");
            }
        }

        // ✅ Verificar si el perfil ya está disponible (caso de reinicio de escena)
        if (GameBootService.IsAvailable && GameBootService.Profile != null)
        {
            HandleProfileReady();
        }

        // Si el inicio de batalla fue solicitado mientras el componente estaba inactivo,
        // arranquemos la batalla ahora que estamos habilitados.
        if (_pendingStartBattle)
        {
            _pendingStartBattle = false;
            if (showDebugLogs)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[BossArenaController] OnEnable: procesando StartBattle pendiente.");
                #endif
            }
            StartBattleInternal();
        }
    }

    void OnDisable()
    {
        BossProgressTracker.OnProgressRestored -= HandleBossProgressRestored;
        GameBootService.OnProfileReady -= HandleProfileReady; // ✅ Desuscribirse de OnProfileReady
        CleanupBossSubscriptions();

        // Desregistrar si estaba registrado
        if (!string.IsNullOrEmpty(battleId))
        {
            if (s_arenaRegistry.TryGetValue(battleId, out var existing) && existing == this)
            {
                s_arenaRegistry.Remove(battleId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[BossArenaController] Desregistrada arena con BattleId='{battleId}' en OnDisable.");
#endif
            }
        }
    }

    void Start()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] 🎬 Start() - Arena BattleId='{battleId}', BossId='{bossId}', ProfileReady={_profileReady}");
#endif
        
        // ✅ CRÍTICO: Solo verificar el estado del boss si el perfil ya está listo
        // Si no está listo, HandleProfileReady() lo verificará cuando se dispare OnProfileReady
        if (_profileReady)
        {
            CheckBossStateAndApply();
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ⏳ Esperando a que el perfil esté listo para verificar el estado del boss");
#endif
        }
    }

    /// <summary>
    /// Handler para OnProfileReady - se ejecuta cuando el perfil está completamente cargado y listo
    /// </summary>
    void HandleProfileReady()
    {
        if (_profileReady)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ⚠️ HandleProfileReady llamado múltiples veces para '{bossId}' - ignorando");
#endif
            return;
        }
        
        _profileReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] ✅ Perfil listo para '{bossId}' - verificando estado del boss");
#endif
        
        // Desuscribirse para no recibir más notificaciones
        GameBootService.OnProfileReady -= HandleProfileReady;
        
        // Verificar el estado del boss ahora que los datos están cargados
        CheckBossStateAndApply();
    }

    /// <summary>
    /// Verifica el estado del boss y aplica la configuración correspondiente
    /// </summary>
    void CheckBossStateAndApply()
    {
        bool isDefeated = IsBossAlreadyDefeated();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] 🔍 IsBossAlreadyDefeated() = {isDefeated} para BossId='{bossId}'");
#endif
        
        if (isDefeated)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ✅ Boss '{bossId}' ya fue derrotado - desbloqueando área sin spawnearlo");
#endif
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ⚔️ Boss '{bossId}' NO ha sido derrotado - esperando trigger del player");
#endif
        }
    }

    void OnTriggerEnter(Collider other)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] 🚪 OnTriggerEnter - Tag: {other.tag}, Started: {started}, BossDefeated: {_bossDefeatHandled}, StartOnEnter: {startBarrierOnPlayerEnter}");
#endif
        
        if (started || _bossDefeatHandled) return;
        if (!other.CompareTag(playerTag)) return;
        if (!startBarrierOnPlayerEnter) 
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ⏸️ StartBarrierOnPlayerEnter está desactivado - no se inicia batalla automáticamente");
#endif
            return;
        }

        bool isDefeated = IsBossAlreadyDefeated();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] 🔍 Player entró al trigger - IsBossAlreadyDefeated={isDefeated}");
#endif
        
        if (isDefeated)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ✅ Boss ya derrotado - aplicando estado cleared");
#endif
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] ⚔️ Iniciando batalla con boss '{bossId}'");
#endif
        // Usamos el método centralizado para iniciar la batalla
        StartBattleInternal();
    }

    /// <summary>
    /// Igual que <see cref="TriggerStartBattle()"/>, pero con un BattleEncounterSO (INC-207): sus
    /// datos (prefab, nombre, radio, VFX) sustituyen a los campos configurados a mano en esta
    /// arena para esta batalla concreta, y fuerza el modo radio aunque useRadiusMode esté en
    /// false. Pensado para llamarse desde StartBattleNode cuando el nodo lleva un encounter
    /// asignado.
    /// </summary>
    public void TriggerStartBattle(BattleEncounterSO encounter, int spawnProfileIndex = 0)
    {
        _activeEncounter = encounter;
        _activeSpawnProfileIndex = spawnProfileIndex;
        TriggerStartBattle();
    }

    // Método público para permitir que la cinemática u otros sistemas inicien la batalla
    // Se puede enlazar directamente al UnityEvent onCinematicFinished (sin parámetros)
    public void TriggerStartBattle()
    {
        if (started || _bossDefeatHandled) return;

        if (IsBossAlreadyDefeated())
        {
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
            return;
        }

        StartBattleInternal();
    }

    // Lógica centralizada para arrancar la batalla (refactorizada desde OnTriggerEnter)
    private void StartBattleInternal()
    {
        if (started) return;

        // If this component is not active/enabled, defer starting the battle until OnEnable.
        if (!isActiveAndEnabled)
        {
            _pendingStartBattle = true;
            if (showDebugLogs)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[BossArenaController] StartBattleInternal deferred: component inactive; will start on OnEnable.");
                #endif
            }
            return;
        }

        started = true;
        _bossDeathConfirmed = false;

        OnAnyBattleStarted?.Invoke();

        // Puertas, área delimitada, o radio alrededor del jugador (INC-207)
        bool radiusModeActive = useRadiusMode || _activeEncounter != null;
        if (radiusModeActive)
        {
            LockRadiusArea();
        }
        else if (useDoorMode)
        {
            if (doorWest) doorWest.Close();
        }
        else
        {
            LockArea();
        }

        // Desactivar objetos durante la batalla
        ApplyBattleDisables();

        var id = !string.IsNullOrEmpty(battleId) ? battleId : bossId;
        if (!string.IsNullOrEmpty(id))
        {
            // Señal (si está a tiempo)
            DefaultNarrativeSignals.Instance?.RaiseCustom($"BATTLE_START:{id}", name);

            // Fallback directo (si el wiring llega tarde)
            if (AudioService.Instance != null)
                AudioService.Instance.BeginBattleById(id);
        }

        SpawnBoss();
    }

    private void SpawnBoss()
    {
        bool useEncounterData = _activeEncounter != null;

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (useEncounterData && _radiusLocked)
        {
            // Posición calculada a partir del radio de la arena y la posición del jugador al
            // empezar la batalla (INC-207) — ya no depende de un Transform bossSpawn colocado a
            // mano en la escena.
            spawnPosition = _activeEncounter.ComputeSpawnPosition(_arenaCenter, _arenaPlayerRotation, _activeSpawnProfileIndex);

            // El punto se calcula a ciegas (a X metros por delante del jugador), así que puede
            // caer dentro de un edificio, en el agua o simplemente fuera del NavMesh. Se proyecta
            // sobre la malla ANTES de instanciar: si no, el NavMeshAgent del enemigo falla al
            // crearse ("Failed to create agent because it is not close enough to the NavMesh") y
            // el boss se queda clavado en el sitio sin poder perseguir al jugador.
            spawnPosition = ResolveSpawnOnNavMesh(spawnPosition);

            Vector3 toCenter = _arenaCenter - spawnPosition;
            toCenter.y = 0f;
            spawnRotation = toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter.normalized) : transform.rotation;
        }
        else
        {
            spawnPosition = bossSpawn ? bossSpawn.position : transform.position;
            spawnRotation = bossSpawn ? bossSpawn.rotation : transform.rotation;
        }

        GameObject usedPortalPrefab = useEncounterData && _activeEncounter.portalPrefab != null ? _activeEncounter.portalPrefab : portalPrefab;
        GameObject usedSpawnVfxPrefab = useEncounterData && _activeEncounter.spawnVfxPrefab != null ? _activeEncounter.spawnVfxPrefab : bossSpawnVFXPrefab;
        float usedSpawnVfxDuration = useEncounterData ? _activeEncounter.spawnVfxDuration : bossSpawnVFXDuration;
        float usedSpawnVfxHeightOffset = useEncounterData ? _activeEncounter.spawnVfxHeightOffset : bossSpawnVFXHeightOffset;
        _activeFloorLayer = (useEncounterData && _activeEncounter.floorLayer.value != 0) ? _activeEncounter.floorLayer : floorLayer;

        // Instanciar portal de aparición si está configurado
        GameObject portalVFX = null;
        if (usedPortalPrefab != null)
        {
            Vector3 portalPosition = spawnPosition;
            Quaternion portalRotation = spawnRotation;
            if (!useEncounterData && portalSpawn != null)
            {
                portalPosition = portalSpawn.position;
                portalRotation = portalSpawn.rotation;
            }

            // Calcular posición del portal en el suelo usando raycast
            if (Physics.Raycast(portalPosition + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, _activeFloorLayer))
            {
                portalPosition = hit.point;
                if (showDebugLogs)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[BossArenaController] Portal de aparición colocado en el suelo: {portalPosition}");
                    #endif
                }
            }
            portalVFX = Instantiate(usedPortalPrefab, portalPosition, portalRotation, transform.parent);
            
            if (showDebugLogs)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[BossArenaController] Portal de aparición instanciado en {portalPosition}");
                #endif
            }
        }

        // Instanciar VFX de aparición adicional si está configurado
        if (usedSpawnVfxPrefab != null)
        {
            Vector3 vfxPosition = spawnPosition + Vector3.up * usedSpawnVfxHeightOffset;
            GameObject vfx = Instantiate(usedSpawnVfxPrefab, vfxPosition, spawnRotation);
            Destroy(vfx, usedSpawnVfxDuration);
            if (showDebugLogs)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[BossArenaController] VFX de aparición instanciado en {vfxPosition}, se destruirá en {usedSpawnVfxDuration}s");
                #endif
            }
        }

        GameObject boss = null;
        GameObject prefabToSpawn = useEncounterData && _activeEncounter.enemyPrefab != null ? _activeEncounter.enemyPrefab : bossPrefab;

        if (prefabToSpawn)
            boss = Instantiate(prefabToSpawn, spawnPosition, spawnRotation, transform.parent);
        else
            boss = FindExistingBossInRoom();

        if (!boss)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[BossArenaController] No hay boss para esta sala.");
#endif
            started = false;
            
            // Destruir el portal si no hay boss
            if (portalVFX != null)
                Destroy(portalVFX);
            
            return;
        }

        // Destruir el portal después de la duración del VFX
        if (portalVFX != null)
        {
            Destroy(portalVFX, usedSpawnVfxDuration);
            if (showDebugLogs)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[BossArenaController] Portal se destruirá en {usedSpawnVfxDuration}s");
                #endif
            }
        }

        // Preparar referencias para escuchar la derrota real del boss
        _activeBossMarker = boss.GetComponent<EnemyMarker>() ?? boss.AddComponent<EnemyMarker>();
        _activeBossMarker.onEnemyGone -= OnBossDead;
        _activeBossMarker.onEnemyGone += OnBossDead;

        _activeBossDamageable = boss.GetComponent<Damageable>();
        if (_activeBossDamageable != null)
        {
            _activeBossDamageable.OnDied -= HandleBossDamageableDied;
            _activeBossDamageable.OnDied += HandleBossDamageableDied;
        }

        _activeBossHealthBar = boss.GetComponent<BossHealthBar>();

        // Iniciar presentación (componente propio de la arena si lo tiene asignado, o si no el
        // servicio global BossIntroPresentationService — INC-207) o colocar directamente en el suelo
        if (bossIntroPresentation != null || BossIntroPresentationService.Instance != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ✅ Presentación de boss disponible para '{boss.name}'. Iniciando presentación...");
#endif
            StartCoroutine(PlayPresentationAndPlaceBoss(boss));
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ⚠️ No hay presentación de boss disponible para '{boss.name}'. Colocando boss directamente.");
#endif
            PlaceBossOnFloor(boss);
            EnableBossCombat(boss);
            _activeBossHealthBar?.Show();
        }
    }

    private IEnumerator PlayPresentationAndPlaceBoss(GameObject boss)
    {
        // Buscar la cámara hija del boss
        Camera bossCamera = boss.GetComponentInChildren<Camera>(true); // includeInactive = true
        
        if (bossCamera == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BossArenaController] ❌ No se encontró cámara en el boss '{boss.name}' (ni activa ni inactiva). Saltando presentación.");
#endif
            PlaceBossOnFloor(boss);
            EnableBossCombat(boss);
            yield break;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] 📷 Cámara encontrada: '{bossCamera.name}' en boss '{boss.name}'");
#endif
        
        // Configurar y reproducir presentación: componente propio de la arena si lo tiene
        // asignado (comportamiento sin cambios), o si no el servicio global (INC-207).
        if (bossIntroPresentation != null)
        {
            bossIntroPresentation.SetupBoss(boss.transform, bossCamera, GetLocalizedBossName());
            yield return StartCoroutine(bossIntroPresentation.PlayIntroduction());
        }
        else
        {
            // IMPORTANTE (INC-207 hotfix 2026-09-15): arrancar esta corrutina en el propio
            // servicio (persistente, DontDestroyOnLoad), no en "this" (la arena). Si se arranca
            // en la arena y la arena se desactiva/destruye a media presentación (cambio de
            // escena, ApplyBattleDisables, etc.), Unity aborta la corrutina sin pasar por su
            // finally — _isPlaying del servicio se queda en true para siempre y ninguna futura
            // batalla del juego vuelve a revelar la pantalla. Arrancarla en el propio servicio
            // hace que sobreviva a la arena y garantiza que su finally (fundido de emergencia
            // incluido) siempre se ejecute.
            yield return BossIntroPresentationService.Instance.StartCoroutine(
                BossIntroPresentationService.Instance.PlayIntroduction(boss.transform, bossCamera, GetLocalizedBossName()));
        }
        
        // Después de la presentación:
        // 1. Desactivar la cámara del boss
        bossCamera.gameObject.SetActive(false);

        // 2. Colocar en el suelo
        PlaceBossOnFloor(boss);

        // 3. Activar combate del boss
        EnableBossCombat(boss);

        // 4. Mostrar barra de vida del boss con DOTween
        _activeBossHealthBar?.Show();

        // 5. Avisar de que la presentación ha TERMINADO.
        //
        // Hasta aquí la pantalla es del jefe: cámara propia, nombre en grande, su propia
        // transición. Cualquier bocadillo lanzado antes de este punto se lo come esa puesta en
        // escena o directamente no se ve. Quien quiera hablar justo después del jefe —hoy Eldran,
        // explicando la mecánica nueva en el primer combate del juego— escucha esta señal.
        //
        // Se levantan las dos: la genérica, para quien solo quiera saber "ya ha terminado una
        // presentación", y la que lleva el id, para quien tenga que distinguir DE QUÉ jefe.
        DefaultNarrativeSignals.Instance?.RaiseCustom("BOSS_INTRO_DONE", name);
        if (!string.IsNullOrEmpty(BattleId))
            DefaultNarrativeSignals.Instance?.RaiseCustom($"BOSS_INTRO_DONE:{BattleId}", name);
    }

    private void EnableBossCombat(GameObject boss)
    {
        // Activar el combate en el componente de IA del boss (si existe)
        var impDemonAI = boss.GetComponent<ImpDemonAI>();
        if (impDemonAI != null)
        {
            impDemonAI.canStartCombat = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[BossArenaController] Combate del boss (ImpDemon) activado.");
#endif
            return;
        }
        
        var golemBossAI = boss.GetComponent<GolemBossAI>();
        if (golemBossAI != null)
        {
            golemBossAI.canStartCombat = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[BossArenaController] Combate del boss (Golem) activado.");
#endif
            return;
        }

        // Bosses basados en el FSM genérico NPCBehaviourManagerV2 (p.ej. Mago Oscuro, batalla final).
        // A diferencia de ImpDemonAI/GolemBossAI (flag canStartCombat leído por su propio Update),
        // NPCBehaviourManagerV2 expone EnterCombat() como entrada directa: registra el NPC en
        // ActiveCombatRegistry y fuerza el cambio a CombatState de inmediato.
        var npcManager = boss.GetComponent<NPCBehaviourManagerV2>();
        if (npcManager != null)
        {
            // FIX (ronda 16): mientras el boss permanezca en su layer ambiente "Interactable",
            // los hechizos lanzados por aliados (p.ej. Estela) nunca le hacen daño: MagicProjectileSpawner
            // calcula hitLayers = LayerMask.GetMask("Enemy", "Boss") y MagicProjectil.ResolveHit() descarta
            // en silencio (sin log) cualquier impacto cuyo layer no esté en esa máscara. NPCCombatLifecycleHandler
            // ya revierte a "Interactable" al terminar el combate (SetupPostCombatInteraction), así que aquí
            // hacemos el cambio simétrico de entrada a combate.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                boss.layer = enemyLayer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[BossArenaController] Layer del boss cambiado a 'Enemy' para permitir recibir daño.");
#endif
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[BossArenaController] No se encontró el layer 'Enemy' en el proyecto; el boss podría no recibir daño.");
#endif
            }

            npcManager.EnterCombat();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[BossArenaController] Combate del boss (NPCBehaviourManagerV2) activado.");
#endif
            return;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning("[BossArenaController] No se encontró componente de IA compatible en el boss.");
#endif
    }

    /// <summary>
    /// Proyecta sobre el NavMesh la posición de spawn calculada por <see cref="BattleEncounterSO.ComputeSpawnPosition"/>.
    /// Si el punto exacto no vale, prueba otros ángulos alrededor del centro de la arena (de 45º
    /// en 45º) y, si tampoco, acercándose al centro. Si no encuentra nada, devuelve la posición
    /// original con un aviso — mejor un boss mal colocado que ninguna batalla.
    /// </summary>
    private Vector3 ResolveSpawnOnNavMesh(Vector3 desired)
    {
        const float SampleRadius = 3f;

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas))
            return hit.position;

        Vector3 offset = desired - _arenaCenter;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance < 0.01f) return desired;

        Vector3 dir = offset / distance;

        // Mismo criterio que NPCObstacleAvoidance: barrido acotado y determinista, sin bucles
        // abiertos. 3 distancias x 8 ángulos = 24 sondeos como mucho, una sola vez por batalla.
        float[] distanceFactors = { 1f, 0.7f, 0.45f };
        for (int d = 0; d < distanceFactors.Length; d++)
        {
            float dist = distance * distanceFactors[d];
            for (int angle = 0; angle < 360; angle += 45)
            {
                if (d == 0 && angle == 0) continue; // ya probado arriba

                Vector3 candidate = _arenaCenter + (Quaternion.Euler(0f, angle, 0f) * dir) * dist;
                if (NavMesh.SamplePosition(candidate, out hit, SampleRadius, NavMesh.AllAreas))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[BossArenaController] Spawn calculado fuera del NavMesh: reubicado a {hit.position} (giro {angle}º, {distanceFactors[d]:P0} de la distancia original).");
#endif
                    return hit.position;
                }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[BossArenaController] No se encontró NavMesh cerca de la posición de spawn calculada ({desired}). El enemigo aparecerá ahí igualmente, pero puede que no pueda moverse. Revisa el bakeado de NavMesh en esa zona.");
#endif
        return desired;
    }

    // Buffer pre-alocado para el raycast de asentado del boss (regla de CLAUDE.md § 2: nada de
    // Physics.RaycastAll ni arrays nuevos en caliente). 8 impactos son de sobra: solo se usa una
    // vez por batalla.
    private static readonly RaycastHit[] s_floorHits = new RaycastHit[8];

    private void PlaceBossOnFloor(GameObject boss)
    {
        // _activeFloorLayer se resuelve en SpawnBoss() (encounter si lo trae, si no el campo floorLayer de siempre).
        LayerMask layerToUse = _activeFloorLayer.value != 0 ? _activeFloorLayer : floorLayer;

        // Raycast hacia abajo para encontrar el suelo. Se usa la versión NonAlloc y se descartan
        // los impactos contra el propio boss: su raíz está en la capa Enemy, pero muchos de sus
        // hijos (hitboxes, props) están en Default, así que un Raycast simple podía devolver su
        // propio collider y "asentarlo" sobre sí mismo, dejándolo flotando. Los triggers se
        // ignoran a propósito (zonas de ambiente, portales, etc. no son suelo).
        Vector3 origin = boss.transform.position + Vector3.up * 2f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, s_floorHits, 102f, layerToUse.value, QueryTriggerInteraction.Ignore);

        Transform bossRoot = boss.transform;
        float bestDistance = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = s_floorHits[i].collider;
            if (col == null) continue;
            if (col.transform == bossRoot || col.transform.IsChildOf(bossRoot)) continue;

            if (s_floorHits[i].distance < bestDistance)
            {
                bestDistance = s_floorHits[i].distance;
                bestPoint = s_floorHits[i].point;
                found = true;
            }
        }

        if (found)
        {
            boss.transform.position = bestPoint;
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BossArenaController] No se encontró Floor debajo del boss en {boss.transform.position} (máscara={layerToUse.value}). Se queda a la altura calculada.");
#endif
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Si el área está bloqueada y el jugador intenta salir, empujarlo de vuelta
        if (!useDoorMode && _areaLocked && other.CompareTag(playerTag))
        {
            StartCoroutine(PushPlayerBack(other.transform));
        }
    }

    private System.Collections.IEnumerator PushPlayerBack(Transform player)
    {
        // Esperar un frame para evitar conflictos
        yield return null;

        // Empujar al jugador hacia el centro del área
        if (areaBarrierCollider != null)
        {
            Vector3 center = areaBarrierCollider.bounds.center;
            Vector3 direction = (center - player.position).normalized;

            // Teleportar ligeramente hacia dentro
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc)
            {
                cc.enabled = false;
                player.position += direction * 2f;
                cc.enabled = true;
            }
            else
            {
                player.position += direction * 2f;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[BossArenaController] Jugador empujado de vuelta al área del boss.");
#endif
        }
    }

    GameObject FindExistingBossInRoom()
    {
        // busca un EnemyMarker ya colocado como hijo de la sala
        var markers = GetComponentsInParent<RoomGoal>(true);
        if (markers.Length > 0)
        {
            var roomRoot = markers[0].transform;
            var existing = roomRoot.GetComponentInChildren<EnemyMarker>(true);
            return existing ? existing.gameObject : null;
        }
        return null;
    }

    void OnBossDead(EnemyMarker marker)
    {
        if (marker != null)
            marker.onEnemyGone -= OnBossDead;

        if (_bossDefeatHandled)
        {
            CleanupBossSubscriptions();
            return;
        }

        if (!_bossDeathConfirmed)
        {
            // El boss fue destruido sin haber sido derrotado (p.ej. cambio de escena tras Game
            // Over, killzone, despawn o limpieza externa). FIX A2 (auditoría 2026-08-07): antes
            // este camino solo hacía CleanupBossSubscriptions()+started=false, sin reabrir
            // puertas, desbloquear el área ni parar la música de batalla — el jugador quedaba
            // encerrado con música de boss infinita y sin poder re-disparar el trigger (started
            // seguía en el estado de "batalla en curso" a efectos de puertas/barrera). Se reusa
            // ApplyBossClearedState (misma limpieza que una victoria real) pero sin marcar
            // derrota real: no cuenta como victoria en BossProgressTracker, no dispara eventos
            // de Unity ni señales narrativas de "batalla ganada".
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
            return;
        }
        
        ApplyBossClearedState(invokeUnityEvents: true, markDefeatedInTracker: true);
    }

    void HandleBossDamageableDied()
    {
        _bossDeathConfirmed = true;
        ApplyBossClearedState(invokeUnityEvents: true, markDefeatedInTracker: true);
    }

    /// Final alternativo (Paso 6 del refactor Tramo 1, RuneCollar): cierra la arena/batalla
    /// exactamente igual que una derrota real del boss (abre puertas o desbloquea el radio, para
    /// la música de jefe, marca bossId como derrotado en el tracker, dispara onBossDefeated y
    /// RaiseBattleWon) pero SIN pasar por Damageable.OnDied -- el propio boss decide no llamar a
    /// Damageable.Kill() para poder quedarse en escena tras el combate (ver
    /// ImpDemonAI.ForceFallenByCollarBreak, que es quien llama a esto). ApplyBossClearedState ya
    /// es idempotente (_bossDefeatHandled), así que si el boss también llegara a morir por HP
    /// normal casi a la vez, la segunda llamada no hace nada.
    public void NotifyBossDefeatedByAlternateEnding()
    {
        _bossDeathConfirmed = true;
        ApplyBossClearedState(invokeUnityEvents: true, markDefeatedInTracker: true);
    }

    void CleanupBossSubscriptions()
    {
        if (_activeBossMarker != null)
        {
            _activeBossMarker.onEnemyGone -= OnBossDead;
            _activeBossMarker = null;
        }

        if (_activeBossDamageable != null)
        {
            _activeBossDamageable.OnDied -= HandleBossDamageableDied;
            _activeBossDamageable = null;
        }
    }

    void HandleBossProgressRestored()
    {
        if (_bossDefeatHandled) return;
        if (IsBossAlreadyDefeated())
        {
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
        }
    }

    bool IsBossAlreadyDefeated()
    {
        if (string.IsNullOrEmpty(bossId))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] ⚠️ IsBossAlreadyDefeated: bossId está vacío para battleId='{battleId}'");
#endif
            return false;
        }
        
        if (BossProgressTracker.TryGetInstance(out var tracker))
        {
            bool defeated = tracker.IsDefeated(bossId);
            var allDefeated = tracker.DefeatedBossIds;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BossArenaController] 🔍 Tracker encontrado - BossId='{bossId}', IsDefeated={defeated}, DefeatedBossIds=[{string.Join(", ", allDefeated)}]");
#endif
            return defeated;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] ⚠️ BossProgressTracker no encontrado - asumiendo boss NO derrotado");
#endif
        return false;
    }

    void ApplyBossClearedState(bool invokeUnityEvents, bool markDefeatedInTracker, bool raiseSignals = true)
    {
        if (_bossDefeatHandled) return;
        _bossDefeatHandled = true;
        started = true;

        CleanupBossSubscriptions();

        if (_radiusLocked)
        {
            UnlockRadiusArea();
        }
        else if (useDoorMode)
        {
            if (doorWest) doorWest.Open();
            if (doorEast) doorEast.Open();
        }
        else
        {
            UnlockArea();
        }

        if (musicBoss && musicBoss.isPlaying)
        {
            musicBoss.Stop();
        }

        if (markDefeatedInTracker && !string.IsNullOrEmpty(bossId))
        {
            BossProgressTracker.Instance.MarkDefeated(bossId);
        }

        if (roomGoal) roomGoal.MarkCleared();

        if (invokeUnityEvents)
        {
            onBossDefeated?.Invoke();
        }

        // Emitir la señal del grafo para indicar que la batalla/arena ha sido ganada
        // SOLO si raiseSignals=true (evita sonar música al cargar partida)
        if (raiseSignals)
        {
            OnAnyBattleEnded?.Invoke();

            try
            {
                // Usar BattleId (fallback a bossId ya fue aplicado en Awake)
                DefaultNarrativeSignals.Instance?.RaiseBattleWon(BattleId);
                
                if (!string.IsNullOrEmpty(BattleId) && AudioService.Instance != null)
                    AudioService.Instance.EndBattleById(BattleId);

            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[BossArenaController] Error notificando BattleWon: {ex.Message}");
#endif
            }
        }

        RestoreBattleDisables();
        // Portal ya no se spawneea al final, solo al inicio de la batalla
    }

    // =========================== Visual Barrier ===========================

    private void CreateBarrierVisual()
    {
        if (areaBarrierCollider == null) return;

        // Crear GameObject contenedor
        _barrierVisual = new GameObject("BossArenaBarrier_Visual");
        _barrierVisual.transform.SetParent(transform);
        _barrierVisual.transform.localPosition = Vector3.zero;
        _barrierVisual.transform.localRotation = Quaternion.identity;

        // Dimensiones del collider (en mundo)
        Bounds bounds = areaBarrierCollider.bounds;
        Vector3 size = bounds.size;
        Vector3 centerWorld = bounds.center;
        Vector3 centerLocal = _barrierVisual.transform.InverseTransformPoint(centerWorld);

        // Altura efectiva
        float height = barrierHeight > 0 ? barrierHeight : size.y;

        // Crear paredes según tipo de collider
        if (areaBarrierCollider is BoxCollider)
        {
            CreateBoxBarrierWalls(centerLocal, size, height);
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[BossArenaController] Solo se soportan BoxCollider para las barreras visuales. Creando barrera genérica.");
#endif
            CreateGenericBarrier(bounds, height);
        }
    }

    private void CreateBoxBarrierWalls(Vector3 centerLocal, Vector3 worldSize, float height)
    {
        float halfWidth = worldSize.x * 0.5f;
        float halfDepth = worldSize.z * 0.5f;

        // Pared Norte (Z+)
        CreateWall("Wall_North",
            new Vector3(centerLocal.x, centerLocal.y + height * 0.5f, centerLocal.z + halfDepth),
            new Vector3(worldSize.x, height, barrierThickness));

        // Pared Sur (Z-)
        CreateWall("Wall_South",
            new Vector3(centerLocal.x, centerLocal.y + height * 0.5f, centerLocal.z - halfDepth),
            new Vector3(worldSize.x, height, barrierThickness));

        // Pared Este (X+)
        CreateWall("Wall_East",
            new Vector3(centerLocal.x + halfWidth, centerLocal.y + height * 0.5f, centerLocal.z),
            new Vector3(barrierThickness, height, worldSize.z));

        // Pared Oeste (X-)
        CreateWall("Wall_West",
            new Vector3(centerLocal.x - halfWidth, centerLocal.y + height * 0.5f, centerLocal.z),
            new Vector3(barrierThickness, height, worldSize.z));
    }

    private void CreateWall(string wallName, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(_barrierVisual.transform, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = size;

        // Eliminar collider (no interactúa)
        var col = wall.GetComponent<Collider>();
        if (col) Destroy(col);

        // Efecto visual (build-safe, NO compila shaders en runtime)
        var barrierEffect = wall.AddComponent<BossArenaBarrier>();
        barrierEffect.Setup(
            barrierMaterial,
            barrierColor,
            pulseSpeed,
            pulseIntensity,
            rimPower,
            rimStrength,
            noiseTiling,
            noiseSpeed,
            noiseStrength,
            globalAlpha
        );

        // Guardar el primero para batch de Show/Hide (luego cogemos todos con GetComponentsInChildren)
        if (_barrierEffect == null) _barrierEffect = barrierEffect;
    }

    private void CreateGenericBarrier(Bounds boundsWorld, float height)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wall.name = "GenericBarrier";
        wall.transform.SetParent(_barrierVisual.transform, false);

        Vector3 centerWorld = boundsWorld.center;
        Vector3 localCenter = _barrierVisual.transform.InverseTransformPoint(centerWorld);
        wall.transform.localPosition = localCenter;
        wall.transform.localRotation = Quaternion.identity;

        float radius = Mathf.Max(boundsWorld.extents.x, boundsWorld.extents.z);
        wall.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

        var col = wall.GetComponent<Collider>();
        if (col) Destroy(col);

        _barrierEffect = wall.AddComponent<BossArenaBarrier>();
        _barrierEffect.Setup(
            barrierMaterial,
            barrierColor,
            pulseSpeed,
            pulseIntensity,
            rimPower,
            rimStrength,
            noiseTiling,
            noiseSpeed,
            noiseStrength,
            globalAlpha
        );
    }

    private void LockArea()
    {
        _areaLocked = true;

        // Activar la barrera visual
        if (_barrierVisual != null)
        {
            var barriers = _barrierVisual.GetComponentsInChildren<BossArenaBarrier>();
            foreach (var barrier in barriers)
                barrier.Show();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[BossArenaController] Área del boss bloqueada.");
#endif
    }

    private void UnlockArea()
    {
        _areaLocked = false;

        // Desactivar la barrera visual
        if (_barrierVisual != null)
        {
            var barriers = _barrierVisual.GetComponentsInChildren<BossArenaBarrier>();
            foreach (var barrier in barriers)
                barrier.Hide();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[BossArenaController] Área del boss desbloqueada.");
#endif
    }

    // =========================== Radio alrededor del jugador (INC-207) ===========================

    private void LockRadiusArea()
    {
        Transform playerT = ResolvePlayerTransform();
        _arenaCenter = playerT != null ? playerT.position : transform.position;
        _arenaPlayerRotation = playerT != null ? playerT.rotation : transform.rotation;
        _effectiveRadius = (_activeEncounter != null && _activeEncounter.arenaRadius > 0f) ? _activeEncounter.arenaRadius : radiusMeters;
        _radiusLocked = true;

        if (_radiusEnforceRoutine == null)
            _radiusEnforceRoutine = StartCoroutine(Co_EnforceRadius());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] Arena en modo radio: centro={_arenaCenter}, radio={_effectiveRadius}m.");
#endif
    }

    private void UnlockRadiusArea()
    {
        _radiusLocked = false;
        if (_radiusEnforceRoutine != null)
        {
            StopCoroutine(_radiusEnforceRoutine);
            _radiusEnforceRoutine = null;
        }
    }

    private static Transform ResolvePlayerTransform()
    {
        var playerGo = PlayerService.Player;
        return playerGo != null ? playerGo.transform : null;
    }

    // Throttled (radiusCheckInterval, no cada frame — mismo criterio que NPCObstacleAvoidance):
    // comprueba si el jugador ha cruzado el radio y, si es así, lo empuja de vuelta y muestra el
    // aviso de que no puede huir de la batalla.
    private IEnumerator Co_EnforceRadius()
    {
        var wait = new WaitForSeconds(radiusCheckInterval);

        // El empuje se aplica en cada comprobación (si no, el jugador se escaparía entre una y
        // otra), pero el aviso NO: como tras el empuje el jugador queda justo sobre el borde,
        // seguir andando hacia fuera vuelve a cumplir la condición cada radiusCheckInterval
        // (0,2 s por defecto) y el toast se reiniciaría sin parar. Se limita a uno cada
        // CannotFleeToastDuration segundos, que es justo lo que dura el mensaje en pantalla.
        float nextToastTime = 0f;

        while (_radiusLocked)
        {
            Transform playerT = ResolvePlayerTransform();
            if (playerT != null)
            {
                Vector3 toPlayer = playerT.position - _arenaCenter;
                toPlayer.y = 0f;
                float dist = toPlayer.magnitude;

                if (dist > _effectiveRadius && dist > 0.0001f)
                {
                    Vector3 clampedXZ = _arenaCenter + toPlayer.normalized * _effectiveRadius;
                    Vector3 clamped = new Vector3(clampedXZ.x, playerT.position.y, clampedXZ.z);

                    CharacterController cc = playerT.GetComponent<CharacterController>();
                    if (cc)
                    {
                        cc.enabled = false;
                        playerT.position = clamped;
                        cc.enabled = true;
                    }
                    else
                    {
                        playerT.position = clamped;
                    }

                    if (Time.time >= nextToastTime)
                    {
                        HudToastService.Instance?.Show(cannotFleeLocKey, CannotFleeToastDuration);
                        nextToastTime = Time.time + CannotFleeToastDuration;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log("[BossArenaController] Jugador intentó cruzar el radio de la arena — empujado de vuelta.");
#endif
                    }
                }
            }
            yield return wait;
        }
    }

    // =========================== Battle toggles ===========================

    private void ApplyBattleDisables()
    {
        _prevActiveStates.Clear();
        if (toDisableDuringBattle == null || toDisableDuringBattle.Length == 0) return;
        foreach (var go in toDisableDuringBattle)
        {
            if (!go) continue;
            if (!_prevActiveStates.ContainsKey(go))
                _prevActiveStates.Add(go, go.activeSelf);
            go.SetActive(false);
        }
    }

    private void RestoreBattleDisables(bool skipObjectsInSameScene = false)
    {
        if (_prevActiveStates.Count == 0) return;
        foreach (var kvp in _prevActiveStates)
        {
            var go = kvp.Key;
            if (!go) continue;
            if (skipObjectsInSameScene && go.scene == gameObject.scene)
                continue;
            go.SetActive(kvp.Value);
        }
        _prevActiveStates.Clear();
    }

    void OnDestroy()
    {
        UnlockRadiusArea();

        // Restaurar objetos desactivados si quedara algo pendiente.
        // Si la escena se estǭ descargando, los objetos del mismo scene tambiǭn se destruyen,
        // y Unity lanza un error al intentar reactivarlos.
        bool sceneStillLoaded = gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        RestoreBattleDisables(skipObjectsInSameScene: !sceneStillLoaded);
        CleanupBossSubscriptions();

        // Limpiar la barrera visual si fue creada
        if (_barrierVisual != null)
        {
            Destroy(_barrierVisual);
        }

        // Asegurar desregistro final
        if (!string.IsNullOrEmpty(battleId))
        {
            if (s_arenaRegistry.TryGetValue(battleId, out var existing) && existing == this)
            {
                s_arenaRegistry.Remove(battleId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[BossArenaController] Desregistrada arena con BattleId='{battleId}' en OnDestroy.");
#endif
            }
        }
    }

    // =================== Static registry helpers ===================
    public static bool TryTriggerBattleById(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        // Normalizar (quitar espacios que puedan venir de editores o importaciones)
        var key = id.Trim();
        if (string.IsNullOrEmpty(key)) return false;

        // Lookup directo por key normalizada
        if (s_arenaRegistry.TryGetValue(key, out var arena) && arena != null)
        {
            arena.TriggerStartBattle();
            return true;
        }

        // Fallback: intentar búsqueda case-insensitive / trim sobre las keys registradas
        try
        {
            foreach (var kvp in s_arenaRegistry)
            {
                if (string.Equals(kvp.Key?.Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value != null)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning($"[BossArenaController] TryTriggerBattleById: Lookup directo falló para '{id}', usando fallback con key registrada '{kvp.Key}'.");
#endif
                        kvp.Value.TriggerStartBattle();
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BossArenaController] Error durante fallback de búsqueda de BattleId '{id}': {ex.Message}");
#endif
        }

        // Diagnostic: si no se encuentra, mostrar los ids registrados (útil para debugging)
        try
        {
            if (s_arenaRegistry.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[BossArenaController] Intento de activar BattleId '{id}' pero el registry está vacío.");
#endif
            }
            else
            {
                var keys = string.Join(", ", s_arenaRegistry.Keys);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[BossArenaController] Intento de activar BattleId '{id}' pero no se encontró. Keys registradas: {keys}");
#endif
            }
        }
        catch (Exception ex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BossArenaController] Error al listar ids registrados: {ex.Message}");
#endif
        }

        return false;
    }

    public static bool TryGetById(string id, out BossArenaController arena)
    {
        arena = null;
        if (string.IsNullOrEmpty(id)) return false;
        return s_arenaRegistry.TryGetValue(id, out arena) && arena != null;
    }

    // =================== Arena creada en runtime (INC-207, "nada en la escena") ===================

    /// <summary>True si esta arena fue creada en runtime por <see cref="CreateRuntimeArena"/>.</summary>
    public bool IsRuntimeArena => _isRuntimeArena;
    private bool _isRuntimeArena;

    /// <summary>
    /// Crea en runtime una arena mínima a partir de un <see cref="BattleEncounterSO"/>, sin
    /// necesidad de que exista ningún GameObject de arena en la escena (INC-207: todo lo
    /// configurable vive en el grafo, la escena solo lleva diseño).
    ///
    /// La arena creada se coloca en la posición del jugador y arranca siempre en modo radio: el
    /// centro, el radio y la posición de spawn del enemigo se calculan a partir de la posición
    /// del jugador al empezar la batalla y de los datos de la SO, así que no hace falta ni
    /// collider de área, ni barreras visuales, ni Transform de bossSpawn.
    ///
    /// Se configura con el GameObject desactivado a propósito: así Awake()/OnEnable() (que son
    /// los que registran la arena en s_arenaRegistry y crean la barrera visual si hay collider)
    /// se ejecutan con battleId/bossId ya puestos y con useDoorMode/areaBarrierCollider ya
    /// limpiados.
    /// </summary>
    /// <param name="battleId">Id de la batalla (el mismo que usa StartBattleNode para suscribirse a OnBattleWon).</param>
    /// <param name="encounter">Configuración del encuentro (prefab del enemigo, nombre, radio, VFX).</param>
    /// <returns>La arena creada, o null si faltan datos imprescindibles.</returns>
    public static BossArenaController CreateRuntimeArena(string battleId, BattleEncounterSO encounter)
    {
        if (string.IsNullOrEmpty(battleId))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[BossArenaController] CreateRuntimeArena: battleId vacío. No se crea arena.");
#endif
            return null;
        }

        if (encounter == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BossArenaController] CreateRuntimeArena('{battleId}'): no hay BattleEncounterSO. Sin SO no hay datos de enemigo, así que no se crea arena.");
#endif
            return null;
        }

        if (encounter.enemyPrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BossArenaController] CreateRuntimeArena('{battleId}'): la SO '{encounter.name}' no tiene enemyPrefab asignado. La batalla no tendría enemigo.");
#endif
            return null;
        }

        Transform playerT = ResolvePlayerTransform();
        Vector3 center = playerT != null ? playerT.position : Vector3.zero;

        var go = new GameObject($"BattleArena_Runtime_{battleId}");
        go.SetActive(false); // configurar ANTES de que corran Awake/OnEnable
        go.transform.SetPositionAndRotation(center, playerT != null ? playerT.rotation : Quaternion.identity);

        var arena = go.AddComponent<BossArenaController>();
        arena._isRuntimeArena = true;
        arena.battleId = battleId;
        arena.bossId = battleId;

        // Modo radio puro: ni puertas, ni collider de área, ni barrera visual, ni bossSpawn.
        arena.useDoorMode = false;
        arena.areaBarrierCollider = null;
        arena.useRadiusMode = true;
        arena.radiusMeters = encounter.arenaRadius > 0f ? encounter.arenaRadius : arena.radiusMeters;
        arena.startBarrierOnPlayerEnter = false;
        arena.bossSpawn = null;
        arena.bossPrefab = null;     // el prefab lo aporta la SO
        arena.portalSpawn = null;
        arena.roomGoal = null;
        arena.bossIntroPresentation = null; // → usa BossIntroPresentationService (servicio global)

        // Capa de suelo para asentar al enemigo tras la presentación. Si la SO trae la suya,
        // SpawnBoss() la usa y esto no se llega a mirar; esto es solo el fallback.
        int floorLayerIndex = LayerMask.NameToLayer("Floor");
        int mask = 1 << 0; // Default
        if (floorLayerIndex >= 0) mask |= 1 << floorLayerIndex;
        arena.floorLayer = mask;

        go.SetActive(true); // Awake + OnEnable → queda registrada en s_arenaRegistry

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BossArenaController] 🏟️ Arena creada en runtime para battleId='{battleId}' (encounter='{encounter.name}', radio={arena.radiusMeters}m, centro={center}). No hacía falta ningún objeto de arena en la escena.");
#endif
        return arena;
    }

}
