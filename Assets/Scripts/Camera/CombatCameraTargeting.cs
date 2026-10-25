using UnityEngine;
using System.Collections.Generic;
using Core;

public class CombatCameraTargeting : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private vThirdPersonCamera thirdPersonCamera;
    [SerializeField] private Transform playerTransform;
    
    [Header("Integración con Sistema de Proyectiles")]
    [SerializeField] private PlayerTargeting playerTargeting;
    [SerializeField] private bool syncWithProjectileTargeting = true;
    
    [Header("Configuración")]
    [SerializeField] private float maxLockDistance = 30f;
    [SerializeField] private float visualResyncInterval = 0.1f;
    [SerializeField] private float shoulderSwitchDebounce = 0.12f;
    [Tooltip("Segundos que hay que mantener R3/F para activar o desactivar el autoapuntado.")]
    [SerializeField, Min(0.2f)] private float holdToToggleAutotargetSeconds = 0.5f;
    [Tooltip("Ángulo máximo respecto al frente del jugador para fijar un objetivo que no está ya bajo el marcador.")]
    [SerializeField] private float lockConeDegrees = 140f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    public static CombatCameraTargeting Instance { get; private set; }
    public GameObject CurrentTarget => currentTarget;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    private GameObject currentTarget;
    private bool isLockActive;
    private bool wasInCombatLastFrame;
    private float _lastAutoLockAttempt;
    private float _lastVisualResync;
    private float _lastShoulderSwitchAt = -999f;
    private bool _isInDialogue;
    private bool _suppressedByLevitation;
    // Petición de Raúl (1 sep 2026): permite al jugador apagar el lock-on de cámara/objetivo a
    // voluntad (p.ej. para correr hacia un cofre sin que la cámara se empeñe en enfocar a un
    // enemigo cercano). Ver ToggleTargetingSuppressed().
    private bool _targetingSuppressedByPlayer;
    // Pulsación de R3/F en curso: corta = fijar/soltar objetivo; mantenida = autoapuntado on/off.
    private float _lockButtonDownAt = -1f;
    private bool _lockButtonHoldHandled;
    private float _targetHeightOffsetOriginal;

    private void Awake()
    {
        Instance = this;

        if (thirdPersonCamera == null)
            thirdPersonCamera = GetComponent<vThirdPersonCamera>();
        if (thirdPersonCamera != null)
            _targetHeightOffsetOriginal = thirdPersonCamera.overrideTargetHeightOffset;
        
        // Solo lee quién está registrado: la cámara no decide quién es el jugador (INC-482).
        if (playerTransform == null && PlayerService.TryGetPlayer(out var player, allowSceneLookup: false))
            playerTransform = player.transform;
        
        if (playerTargeting == null && playerTransform != null)
            playerTargeting = playerTransform.GetComponentInChildren<PlayerTargeting>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    
    private void OnEnable()
    {
        ActiveCombatRegistry.OnNPCEnteredCombat += OnNPCEnteredCombat;
        ActiveCombatRegistry.OnNPCExitedCombat += OnNPCExitedCombat;
        GamepadInputReader.OnInput += HandleGamepadInput;
        DialogueManager.OnDialogueStarted += OnDialogueStarted;
        DialogueManager.OnDialogueClosed += OnDialogueClosed;
        LevitationTarget.OnAnyLevitationStarted += OnLevitationStarted;
        LevitationTarget.OnAnyLevitationEnded += OnLevitationEnded;
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
    }

    private void OnDisable()
    {
        ActiveCombatRegistry.OnNPCEnteredCombat -= OnNPCEnteredCombat;
        ActiveCombatRegistry.OnNPCExitedCombat -= OnNPCExitedCombat;
        GamepadInputReader.OnInput -= HandleGamepadInput;
        DialogueManager.OnDialogueStarted -= OnDialogueStarted;
        DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        LevitationTarget.OnAnyLevitationStarted -= OnLevitationStarted;
        LevitationTarget.OnAnyLevitationEnded -= OnLevitationEnded;
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        _lockButtonDownAt = -1f;
        _lockButtonHoldHandled = false;
        ReleaseLock();
    }

    // La cámara no es parte del personaje (CamaraDelJugador.prefab): sigue a quien se registre
    // como jugador. Ver INC-482.
    private void AlRegistrarJugador(GameObject jugador)
    {
        playerTransform = jugador != null ? jugador.transform : null;
        playerTargeting = playerTransform != null ? playerTransform.GetComponentInChildren<PlayerTargeting>() : null;
    }

    private bool DebeSuprimirMarcador()
    {
        if (ActiveCombatRegistry.Count > 0) return false;
        if (_isInDialogue) return true;
        if (CinematicSequencerBase.AnySequenceActive) return true;
        if (CameraDirectorService.HasOwner || vThirdPersonCamera.lockCameraForCinematic) return true;
        if (playerTargeting != null && playerTargeting.DebeOcultarMarcador()) return true;
        return false;
    }

    private void OnDialogueStarted(Transform _) => _isInDialogue = true;

    private void OnDialogueClosed(Transform _)
    {
        _isInDialogue = false;
        // Forzar resync inmediato del marker al cerrar el diálogo
        if (isLockActive && currentTarget != null && !DebeSuprimirMarcador())
        {
            _lastVisualResync = 0f;
            if (syncWithProjectileTargeting && playerTargeting != null)
            {
                playerTargeting.SetManualTarget(currentTarget.transform);
                playerTargeting.ForceVisualRefresh();
            }
        }
    }
    
    private void Update()
    {
        if (playerTransform == null)
        {
            if (PlayerService.TryGetPlayer(out var player, allowSceneLookup: false))
            {
                playerTransform = player.transform;
                if (playerTargeting == null)
                    playerTargeting = playerTransform.GetComponentInChildren<PlayerTargeting>();
            }
            else return;
        }

        ProcesarPulsacionDeLock();

        bool isInCombat = ActiveCombatRegistry.Count > 0;
        
        if (isInCombat && !wasInCombatLastFrame) OnEnterCombat();
        else if (!isInCombat && wasInCombatLastFrame) OnExitCombat();
        
        wasInCombatLastFrame = isInCombat;
        
        if (isInCombat && !isLockActive && !_suppressedByLevitation && !_targetingSuppressedByPlayer && !DebeSuprimirMarcador()) TryAutoLock();
        if (isLockActive) EnsureVisualLockSync();
    }
    
    private void TryAutoLock()
    {
        if (_targetingSuppressedByPlayer || DebeSuprimirMarcador()) return;
        if (Time.time - _lastAutoLockAttempt < 0.5f) return;
        _lastAutoLockAttempt = Time.time;
        
        if (playerTransform == null) return;
        
        GameObject closestEnemy = ActiveCombatRegistry.GetClosestCameraLockableCombatNPC(playerTransform.position, maxLockDistance);
        
        if (closestEnemy != null)
        {
            Log($"🎯 Auto-lock: {closestEnemy.name}");
            SetTarget(closestEnemy);
        }
    }
    
    private void HandleGamepadInput(GamepadInputReader.InputEvent inputEvent)
    {
        if (inputEvent.Phase != UnityEngine.InputSystem.InputActionPhase.Performed)
            return;

        // El toggle de lock-on funciona siempre (haya o no un target activo ahora mismo) — es lo
        // que permite tanto quitar un lock en marcha como impedir que uno nuevo se enganche solo.
        if (inputEvent.Type == GamepadInputReader.InputEventType.ToggleTargetLock)
        {
            _lockButtonDownAt = Time.unscaledTime;
            _lockButtonHoldHandled = false;
            return;
        }

        if (!isLockActive || currentTarget == null)
            return;

        if (Time.unscaledTime - _lastShoulderSwitchAt < shoulderSwitchDebounce)
            return;
        
        switch (inputEvent.Type)
        {
            // RB pasa al siguiente objetivo (enemigo en combate o pieza marcable); LB es de la
            // rotación de hechizos básicos.
            case GamepadInputReader.InputEventType.RightShoulder:
                _lastShoulderSwitchAt = Time.unscaledTime;
                SwitchToNextTarget();
                break;
        }
    }

    /// <summary>
    /// Petición de Raúl (1 sep 2026): alterna si el jugador quiere que la cámara/objetivo se
    /// enganchen solos. Al activar la supresión: libera cualquier lock en marcha y apaga también
    /// el auto-scan de PlayerTargeting (el marcador de apuntado libre), para que "desactivar
    /// targets" sea de verdad completo — ni cámara ni marcador — mientras el jugador quiera, por
    /// ejemplo, correr hacia un cofre sin que nada le distraiga hacia un enemigo o una pieza
    /// cercana. Al desactivar la supresión, todo vuelve al comportamiento automático de siempre
    /// en el siguiente Update().
    /// </summary>
    private void ToggleTargetingSuppressed()
    {
        _targetingSuppressedByPlayer = !_targetingSuppressedByPlayer;

        if (_targetingSuppressedByPlayer)
        {
            ReleaseLock();
            if (syncWithProjectileTargeting && playerTargeting != null)
                playerTargeting.SetAutoScanSuppressed(true);

            HudToastService.Instance?.Show("COMBAT_TARGET_LOCK_OFF", 2f);
            Log("🚫 Lock-on de objetivo DESACTIVADO por el jugador");
        }
        else
        {
            if (syncWithProjectileTargeting && playerTargeting != null)
                playerTargeting.SetAutoScanSuppressed(false);
            _lastAutoLockAttempt = 0f; // permitir re-lock inmediato en el siguiente Update si procede

            HudToastService.Instance?.Show("COMBAT_TARGET_LOCK_ON", 2f);
            Log("🎯 Lock-on de objetivo REACTIVADO por el jugador");
        }
    }

    /// <summary>
    /// R3/F: pulsación corta fija o suelta el objetivo; mantenida enciende o apaga el
    /// autoapuntado (INC-141). La detección vive aquí para no añadir otro estado al lector de input.
    /// </summary>
    private void ProcesarPulsacionDeLock()
    {
        if (_lockButtonDownAt < 0f) return;

        bool held = GamepadInputReader.TargetLockHeld;

        if (!_lockButtonHoldHandled && held &&
            Time.unscaledTime - _lockButtonDownAt >= holdToToggleAutotargetSeconds)
        {
            _lockButtonHoldHandled = true;
            if (!DebeSuprimirMarcador())
                ToggleTargetingSuppressed();
            return;
        }

        if (held) return;

        bool pulsacionCorta = !_lockButtonHoldHandled;
        _lockButtonDownAt = -1f;
        _lockButtonHoldHandled = false;

        if (pulsacionCorta && !DebeSuprimirMarcador())
            AlternarLockDeObjetivo();
    }

    private void AlternarLockDeObjetivo()
    {
        if (_suppressedByLevitation) return;

        if (isLockActive)
        {
            ReleaseLock();
            return;
        }

        // Un R3 manual vuelve a permitir el apuntado aunque el jugador lo hubiera apagado.
        if (_targetingSuppressedByPlayer)
        {
            _targetingSuppressedByPlayer = false;
            if (syncWithProjectileTargeting && playerTargeting != null)
                playerTargeting.SetAutoScanSuppressed(false);
        }

        if (TryEncontrarObjetivoParaFijar(out var objetivo))
            SetTarget(objetivo);
    }

    /// <summary>
    /// Prioriza lo que ya está bajo el marcador libre y, si no hay nada, el mejor candidato
    /// (enemigo en combate o pieza marcable) dentro del cono de mirada del jugador.
    /// </summary>
    private bool TryEncontrarObjetivoParaFijar(out GameObject objetivo)
    {
        objetivo = null;
        if (playerTransform == null) return false;

        if (syncWithProjectileTargeting && playerTargeting != null && playerTargeting.CurrentTarget != null)
        {
            var raiz = ObtenerRaizFijable(playerTargeting.CurrentTarget);
            if (raiz != null && EsFijable(raiz) &&
                Vector3.Distance(raiz.transform.position, playerTransform.position) <= maxLockDistance)
            {
                objetivo = raiz;
                return true;
            }
        }

        Vector3 origen = playerTransform.position;
        Vector3 fwd = playerTransform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = playerTransform.forward;
        fwd.Normalize();

        float mejorPuntuacion = float.NegativeInfinity;

        ActiveCombatRegistry.GetAllInCombatNonAlloc(_combatBuffer);
        for (int i = 0; i < _combatBuffer.Count; i++)
            ConsiderarFijable(_combatBuffer[i], origen, fwd, ref mejorPuntuacion, ref objetivo);

        int marcablesCount = Targetable.ActivosCount;
        for (int i = 0; i < marcablesCount; i++)
        {
            var targetable = Targetable.ActivoEn(i);
            if (targetable != null && targetable.esMarcableSiempre)
                ConsiderarFijable(targetable.gameObject, origen, fwd, ref mejorPuntuacion, ref objetivo);
        }

        return objetivo != null;
    }

    private void ConsiderarFijable(GameObject candidato, Vector3 origen, Vector3 fwd,
                                    ref float mejorPuntuacion, ref GameObject mejor)
    {
        if (!EsFijable(candidato)) return;

        Vector3 to = candidato.transform.position - origen;
        float dist = to.magnitude;
        if (dist < 0.01f || dist > maxLockDistance) return;

        Vector3 dir = to / dist;
        float dot = Vector3.Dot(fwd, dir);
        if (dot < Mathf.Cos(lockConeDegrees * 0.5f * Mathf.Deg2Rad)) return;

        if (!EsVisibleParaFijar(candidato, origen)) return;

        float puntuacion = dot - (dist / maxLockDistance) * 0.5f;
        if (puntuacion > mejorPuntuacion)
        {
            mejorPuntuacion = puntuacion;
            mejor = candidato;
        }
    }

    /// <summary>
    /// Los enemigos conservan el comportamiento de siempre; las piezas de puzle exigen línea de
    /// visión para no fijar un cristal que está al otro lado de una pared.
    /// </summary>
    private bool EsVisibleParaFijar(GameObject candidato, Vector3 origen)
    {
        var targetable = candidato.GetComponentInParent<Targetable>();
        if (targetable == null || !targetable.esMarcableSiempre) return true;

        var col = targetable.ColliderDeApuntado;
        Vector3 punto = col != null ? col.bounds.center : candidato.transform.position + Vector3.up;
        Vector3 desde = origen + Vector3.up;
        Vector3 hacia = punto - desde;
        float dist = hacia.magnitude;
        if (dist < 0.01f) return true;

        if (!Physics.Raycast(desde, hacia / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            return true;

        return hit.collider.transform.root == candidato.transform.root ||
               (playerTransform != null && hit.collider.transform.root == playerTransform.root);
    }

    private static bool EsFijable(GameObject candidato)
    {
        if (candidato == null || !candidato.activeInHierarchy) return false;

        var damageable = candidato.GetComponentInParent<Damageable>();
        if (damageable != null && !damageable.IsAlive) return false;

        if (ActiveCombatRegistry.IsInCombat(candidato)) return true;

        var targetable = candidato.GetComponentInParent<Targetable>();
        return targetable != null && targetable.esMarcableSiempre && targetable.isActiveAndEnabled;
    }

    private static GameObject ObtenerRaizFijable(Transform transform)
    {
        if (transform == null) return null;

        var targetable = transform.GetComponentInParent<Targetable>();
        if (targetable != null) return targetable.gameObject;

        return ActiveCombatRegistry.IsInCombat(transform.gameObject) ? transform.gameObject : null;
    }
    
    private void OnEnterCombat()
    {
        // Si el jugador ya había fijado algo (por ejemplo un cristal de puzle), el combate no
        // le roba el objetivo: sigue fijado hasta que él lo suelte. Tampoco se engancha si
        // apagó el autoapuntado o está levitando.
        if (isLockActive || _targetingSuppressedByPlayer || _suppressedByLevitation) return;

        Log($"🎯 Entrando en combate. Buscando objetivo...");
        GameObject closestEnemy = ActiveCombatRegistry.GetClosestCameraLockableCombatNPC(playerTransform.position, maxLockDistance);
        
        if (closestEnemy != null)
        {
            Log($"✅ Lock inicial en: {closestEnemy.name}");
            SetTarget(closestEnemy);
        }
    }
    
    private void OnExitCombat()
    {
        // Un objeto de puzle fijado no se suelta porque termine un combate cercano.
        bool objetoFijado = isLockActive && currentTarget != null &&
                            currentTarget.TryGetComponent<Targetable>(out var targetable) &&
                            targetable.esMarcableSiempre && targetable.isActiveAndEnabled;
        if (!objetoFijado)
        {
            Log("🏳️ Saliendo de combate - Liberando lock");
            ReleaseLock();
        }

        // La supresión manual es un override puntual de ESTA pelea, no una preferencia
        // permanente — al no quedar ningún NPC en combate, se resetea para el próximo encuentro.
        if (_targetingSuppressedByPlayer)
        {
            _targetingSuppressedByPlayer = false;
            if (syncWithProjectileTargeting && playerTargeting != null)
                playerTargeting.SetAutoScanSuppressed(false);
        }
    }
    
    private void OnNPCEnteredCombat(GameObject npc)
    {
        if (DebeSuprimirMarcador() || _targetingSuppressedByPlayer || _suppressedByLevitation) return;

        // Petición de Raúl (1 sep 2026): enemigos menores (arañas) no deben enganchar la cámara
        // solos al entrar en combate — ver ActiveCombatRegistry.IsCameraLockExempt. Siguen
        // registrados en ActiveCombatRegistry (Battle Mode, ciclo manual L1/R1), y el marcador de
        // apuntado para hechizos lo sigue dando el auto-scan normal de PlayerTargeting.
        if (currentTarget == null && playerTransform != null && !ActiveCombatRegistry.IsCameraLockExempt(npc))
        {
            if (Vector3.Distance(npc.transform.position, playerTransform.position) <= maxLockDistance)
            {
                Log($"🎯 Nuevo enemigo '{npc.name}' entró en combate - Haciendo lock");
                SetTarget(npc);
            }
        }
    }
    
    private void OnNPCExitedCombat(GameObject npc)
    {
        if (currentTarget == npc)
        {
            Log($"⚠️ Target actual '{npc.name}' salió de combate - Buscando nuevo objetivo");
            SwitchToNextTarget(incluirObjetos: false);
        }
    }
    
    private void SetTarget(GameObject newTarget)
    {
        if (newTarget == null)
        {
            ReleaseLock();
            return;
        }

        // Normalizar al objeto que lleva el Targetable (el NPC raíz o la pieza de puzle), no a
        // un collider hijo, para que el ciclo y la comprobación de combate usen siempre la raíz.
        var raiz = ObtenerRaizFijable(newTarget.transform);
        if (raiz != null) newTarget = raiz;

        currentTarget = newTarget;
        isLockActive = true;

        if (thirdPersonCamera != null)
        {
            // El pivote de un enemigo está a sus pies (+1,5 m de offset). El de una pieza de
            // puzle puede estar en su centro (cristal) o en su base (molinete): se usa la altura
            // del collider para que la cámara la enfoque de verdad.
            var targetable = newTarget.GetComponent<Targetable>();
            bool esPiezaDePuzle = !ActiveCombatRegistry.IsInCombat(newTarget) &&
                                  targetable != null && targetable.esMarcableSiempre;

            float alturaDeEnfoque = _targetHeightOffsetOriginal;
            if (esPiezaDePuzle)
            {
                var col = targetable.ColliderDeApuntado;
                alturaDeEnfoque = col != null ? col.bounds.center.y - newTarget.transform.position.y : 0f;
            }

            thirdPersonCamera.overrideTargetHeightOffset = alturaDeEnfoque;
            thirdPersonCamera.SetLockTarget(newTarget.transform);
        }

        // Con el objetivo fijado, el objetivo de los hechizos es el enemigo fijado aunque el
        // jugador esté de espaldas: al lanzar, Will se gira hacia él (INC-484/488). El auto-scan
        // se suprime para que no lo sustituya por otro enemigo.
        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.SetAutoScanSuppressed(true);
        }

        if (syncWithProjectileTargeting && playerTargeting != null && !DebeSuprimirMarcador())
        {
            playerTargeting.SetManualTarget(newTarget.transform);
            playerTargeting.ForceVisualRefresh();
        }

        _lastVisualResync = 0f; // Forzar resync en el siguiente Update.
        Log($"🎯 Lock establecido en: {newTarget.name}");

        // Petición de Raúl (1 sep 2026, revertido el mismo día): aquí se disparaba un popup
        // explicativo ("MechanicId.TargetLockToggle", "pulsa F...") la primera vez que había 2+
        // objetivos en combate — Raúl lo probó y pidió quitarlo. Aclarado después: se ha quitado
        // TODO el sistema de popups de "nueva mecánica" (Teletransporte, Cambio de personaje,
        // Unir/disolver equipo, Sígueme incluidos) — el popup de desbloqueo de habilidades/
        // hechizos (AbilityUnlockPopupUI) queda tal y como estaba antes de esta sesión.
    }
    
    private readonly List<GameObject> _orderedTargetsCache = new();
    private readonly List<GameObject> _combatBuffer = new();

    private List<GameObject> GetOrderedTargets(bool incluirObjetos = true)
    {
        _orderedTargetsCache.Clear();
        if (playerTransform == null) return _orderedTargetsCache;

        ActiveCombatRegistry.GetAllInCombatNonAlloc(_combatBuffer);
        for (int i = 0; i < _combatBuffer.Count; i++)
            AñadirFijable(_combatBuffer[i]);

        if (incluirObjetos)
        {
            int marcablesCount = Targetable.ActivosCount;
            for (int i = 0; i < marcablesCount; i++)
            {
                var targetable = Targetable.ActivoEn(i);
                if (targetable != null && targetable.esMarcableSiempre)
                    AñadirFijable(targetable.gameObject);
            }
        }

        if (_orderedTargetsCache.Count <= 1) return _orderedTargetsCache;

        Vector3 playerFwd = playerTransform.forward;
        playerFwd.y = 0;
        playerFwd.Normalize();
        Vector3 origin = playerTransform.position;

        _orderedTargetsCache.Sort((a, b) =>
        {
            float angleA = Vector3.SignedAngle(playerFwd, (a.transform.position - origin).normalized, Vector3.up);
            float angleB = Vector3.SignedAngle(playerFwd, (b.transform.position - origin).normalized, Vector3.up);
            return angleA.CompareTo(angleB);
        });

        return _orderedTargetsCache;
    }

    private void AñadirFijable(GameObject candidato)
    {
        if (!EsFijable(candidato)) return;
        if (Vector3.Distance(candidato.transform.position, playerTransform.position) > maxLockDistance) return;
        if (!EsVisibleParaFijar(candidato, playerTransform.position)) return;
        if (_orderedTargetsCache.Contains(candidato)) return;
        _orderedTargetsCache.Add(candidato);
    }
    
    private void SwitchToNextTarget(bool incluirObjetos = true)
    {
        var targets = GetOrderedTargets(incluirObjetos);
        if (targets.Count == 0) { ReleaseLock(); return; }
        if (targets.Count == 1) { SetTarget(targets[0]); return; }
        
        int currentIndex = targets.IndexOf(currentTarget);
        int nextIndex = (currentIndex + 1) % targets.Count;
        
        Log($"🔄 RB: Cambiando target → {targets[nextIndex].name}");
        SetTarget(targets[nextIndex]);
    }
    
    private void ReleaseLock()
    {
        if (!isLockActive) return;
        
        isLockActive = false;
        currentTarget = null;
        
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.overrideTargetHeightOffset = _targetHeightOffsetOriginal;
            thirdPersonCamera.ClearLockTarget();
        }
        
        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.ClearManualTarget();
            playerTargeting.SetAutoScanSuppressed(false);
        }
        Log("🔓 Lock de cámara liberado");
    }

    private void EnsureVisualLockSync()
    {
        if (currentTarget == null || !currentTarget.activeInHierarchy ||
            (currentTarget.TryGetComponent<Targetable>(out var targetable) && !targetable.isActiveAndEnabled))
        {
            Log("⚠️ Target lock inválido, liberando lock.");
            ReleaseLock();
            return;
        }

        var damageable = currentTarget.GetComponentInParent<Damageable>();
        if (damageable != null && !damageable.IsAlive)
        {
            Log("⚠️ Target lock sin vida, liberando lock.");
            ReleaseLock();
            return;
        }

        if (Time.time - _lastVisualResync < visualResyncInterval) return;
        _lastVisualResync = Time.time;

        if (thirdPersonCamera != null && thirdPersonCamera.LockTarget != currentTarget.transform)
        {
            thirdPersonCamera.SetLockTarget(currentTarget.transform);
            Log($"🔁 Resync cámara -> {currentTarget.name}");
        }

        // No sincronizar el marcador durante diálogos ni cinemáticas para evitar que aparezca
        if (DebeSuprimirMarcador()) return;

        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            if (!playerTargeting.IsManualTargetActive || playerTargeting.CurrentTarget != currentTarget.transform)
            {
                playerTargeting.SetManualTarget(currentTarget.transform);
                Log($"🔁 Resync marcador -> {currentTarget.name}");
            }
            else if (Time.frameCount % 30 == 0)
            {
                playerTargeting.ForceVisualRefresh();
            }
        }
    }
    
    private void OnLevitationStarted()
    {
        _suppressedByLevitation = true;
        if (isLockActive)
        {
            Log("🪄 Levitación activa → liberando lock de cámara temporalmente");
            ReleaseLock();
        }
    }

    private void OnLevitationEnded()
    {
        _suppressedByLevitation = false;
        // Forzar re-lock inmediato en el siguiente Update si seguimos en combate
        _lastAutoLockAttempt = 0f;
        Log("🪄 Levitación terminada → restaurando lock de cámara");
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[CombatCameraTargeting] {message}");
            #endif
        }
    }
}
