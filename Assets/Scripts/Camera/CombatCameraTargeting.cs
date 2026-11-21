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

    [Header("Cambiar de objetivo con el stick derecho")]
    [Tooltip("Inclinación lateral del stick derecho que cuenta como empujón para pasar al objetivo de ese lado (con uno fijado).")]
    [SerializeField, Range(0.3f, 1f)] private float empujonDelStick = 0.6f;
    [Tooltip("El stick tiene que volver por debajo de esta inclinación antes de contar otro empujón.")]
    [SerializeField, Range(0f, 0.6f)] private float rearmeDelStick = 0.35f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    public static CombatCameraTargeting Instance { get; private set; }
    public GameObject CurrentTarget => currentTarget;

    /// <summary>
    /// Fija la cámara en un objetivo aunque no esté en combate (p. ej. el NPC que acaba de ver al
    /// jugador, para que se vea quién le ha pillado). Se suelta como cualquier fijado.
    /// </summary>
    public void Enfocar(GameObject objetivo)
    {
        SetTarget(objetivo);
        _enfocadoHasta = Time.time + SegundosDeEnfoqueSinCombate;
    }

    // Un enfoque sin combate se suelta solo pasado este tiempo si no llega a empezar la pelea.
    private const float SegundosDeEnfoqueSinCombate = 8f;
    private float _enfocadoHasta;

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
    private bool _stickArmado = true;
    private bool _isInDialogue;
    private bool _suppressedByLevitation;
    // Ver ToggleTargetingSuppressed().
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
        var anterior = playerTargeting;
        playerTransform = jugador != null ? jugador.transform : null;
        playerTargeting = playerTransform != null ? playerTransform.GetComponentInChildren<PlayerTargeting>() : null;
        if (anterior == playerTargeting) return;

        // Al cambiar de personaje, el marcador del cuerpo anterior se apaga y el fijado pasa al nuevo
        // en el acto: si no, quedaban dos marcadores o ninguno hasta el siguiente repaso.
        if (anterior != null)
        {
            anterior.ClearManualTarget();
            anterior.SetAutoScanSuppressed(false);
        }
        if (isLockActive && currentTarget != null && syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.SetAutoScanSuppressed(true);
            if (!DebeSuprimirMarcador())
            {
                playerTargeting.SetManualTarget(currentTarget.transform);
                playerTargeting.ForceVisualRefresh();
            }
        }
        _lastVisualResync = 0f;
    }

    private bool DebeSuprimirMarcador()
    {
        if (Kits.Core.GameContext.Has(Situaciones.Combate)) return false;
        if (_isInDialogue) return true;
        if (CinematicSequencerBase.AnySequenceActive) return true;
        // Solo las cámaras de cine ocultan el marcador: la lateral del templo es de juego.
        if (CameraDirectorService.HayCine) return true;
        if (!CameraDirectorService.HasOwner && vThirdPersonCamera.lockCameraForCinematic) return true;
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
        ProcesarEmpujonDelStick();

        bool isInCombat = Kits.Core.GameContext.Has(Situaciones.Combate);
        
        if (isInCombat && !wasInCombatLastFrame) OnEnterCombat();
        else if (!isInCombat && wasInCombatLastFrame) OnExitCombat();
        
        wasInCombatLastFrame = isInCombat;
        
        if (isInCombat) _enfocadoHasta = 0f;
        else if (_enfocadoHasta > 0f && Time.time > _enfocadoHasta)
        {
            _enfocadoHasta = 0f;
            ReleaseLock();
        }

        if (isInCombat && !isLockActive && !_suppressedByLevitation && !_targetingSuppressedByPlayer && !DebeSuprimirMarcador()) TryAutoLock();
        if (isLockActive) EnsureVisualLockSync();
    }
    
    private void TryAutoLock()
    {
        if (_targetingSuppressedByPlayer || DebeSuprimirMarcador()) return;
        if (Time.time - _lastAutoLockAttempt < 0.5f) return;
        _lastAutoLockAttempt = Time.time;
        
        if (playerTransform == null) return;
        
        GameObject closestEnemy = EnemigoMasCercano();
        
        if (closestEnemy != null)
        {
            Log($"Auto-lock: {closestEnemy.name}");
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
            // La rueda del ratón (RB en teclado) pasa al siguiente objetivo; el RB del mando es la
            // esquiva y en mando se cambia con el stick derecho (ProcesarEmpujonDelStick).
            case GamepadInputReader.InputEventType.RightShoulder:
                if (GamepadInputReader.RightShoulderPressed) break;
                _lastShoulderSwitchAt = Time.unscaledTime;
                SwitchToNextTarget();
                break;
        }
    }

    /// <summary>
    /// Al activar la supresión: libera cualquier lock en marcha y apaga también el auto-scan de
    /// PlayerTargeting (el marcador de apuntado libre), para que "desactivar targets" sea de
    /// verdad completo — ni cámara ni marcador — mientras el jugador quiera, por ejemplo, correr
    /// hacia un cofre sin que nada le distraiga hacia un enemigo o una pieza cercana. Al
    /// desactivar la supresión, todo vuelve al comportamiento automático de siempre en el
    /// siguiente Update().
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
            Log("Lock-on de objetivo DESACTIVADO por el jugador");
        }
        else
        {
            if (syncWithProjectileTargeting && playerTargeting != null)
                playerTargeting.SetAutoScanSuppressed(false);
            _lastAutoLockAttempt = 0f; // permitir re-lock inmediato en el siguiente Update si procede

            HudToastService.Instance?.Show("COMBAT_TARGET_LOCK_ON", 2f);
            Log("Lock-on de objetivo REACTIVADO por el jugador");
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
        if (EsDelGrupo(candidato)) return false;

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

        Log($"Entrando en combate. Buscando objetivo...");
        GameObject closestEnemy = EnemigoMasCercano();
        
        if (closestEnemy != null)
        {
            Log($"Lock inicial en: {closestEnemy.name}");
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
            Log("Saliendo de combate - Liberando lock");
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

        // Siguen registrados en ActiveCombatRegistry (Battle Mode, ciclo manual L1/R1), y el
        // marcador de apuntado para hechizos lo sigue dando el auto-scan normal de
        // PlayerTargeting.
        if (currentTarget == null && playerTransform != null && !ActiveCombatRegistry.IsCameraLockExempt(npc) && !EsDelGrupo(npc))
        {
            if (Vector3.Distance(npc.transform.position, playerTransform.position) <= maxLockDistance)
            {
                Log($"Nuevo enemigo '{npc.name}' entró en combate - Haciendo lock");
                SetTarget(npc);
            }
        }
    }
    
    private void OnNPCExitedCombat(GameObject npc)
    {
        if (currentTarget == npc)
        {
            Log($"Target actual '{npc.name}' salió de combate - Buscando nuevo objetivo");
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
        Log($"Lock establecido en: {newTarget.name}");

        // Aclarado después: se ha quitado TODO el sistema de popups de "nueva mecánica"
        // (Teletransporte, Cambio de personaje, Unir/disolver equipo, Sígueme incluidos) — el
        // popup de desbloqueo de habilidades/ hechizos (AbilityUnlockPopupUI) queda tal y como
        // estaba antes de esta sesión.
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
    
    /// Con un objetivo fijado, un empujón lateral del stick derecho pasa al objetivo más cercano
    /// (en ángulo) hacia ese lado, sin dar la vuelta. El stick se rearma al volver al centro.
    private void ProcesarEmpujonDelStick()
    {
        Vector2 stick = GamepadInputReader.StickDerechoDelMando;
        float x = Mathf.Abs(stick.x);
        if (x < rearmeDelStick) { _stickArmado = true; return; }
        if (!_stickArmado || x < empujonDelStick || x < Mathf.Abs(stick.y) * 0.8f) return;
        _stickArmado = false;

        if (playerTransform == null || _suppressedByLevitation || DebeSuprimirMarcador()) return;
        if (Time.unscaledTime - _lastShoulderSwitchAt < shoulderSwitchDebounce) return;
        int lado = stick.x > 0f ? 1 : -1;

        // Sin nada fijado (soltado con R3, o el autoapuntado aún no ha enganchado), el empujón en
        // combate fija al enemigo más a ese lado de la pantalla.
        if (!isLockActive || currentTarget == null)
        {
            if (!Kits.Core.GameContext.Has(Situaciones.Combate)) return;
            _lastShoulderSwitchAt = Time.unscaledTime;
            FijarHaciaUnLado(lado);
            return;
        }

        _lastShoulderSwitchAt = Time.unscaledTime;
        CambiarHaciaUnLado(lado);
    }

    private void FijarHaciaUnLado(int lado)
    {
        var targets = GetOrderedTargets(incluirObjetos: false);
        if (targets.Count == 0) return;
        if (_camaraCache == null || !_camaraCache.isActiveAndEnabled) _camaraCache = Camera.main;
        Transform vista = _camaraCache != null ? _camaraCache.transform : playerTransform;
        Vector3 delante = vista.forward; delante.y = 0f;
        if (delante.sqrMagnitude < 0.0001f) delante = playerTransform.forward;
        Vector3 origen = playerTransform.position;

        GameObject mejor = null;
        float mejorAngulo = float.PositiveInfinity;
        for (int i = 0; i < targets.Count; i++)
        {
            var candidato = targets[i];
            if (candidato == null) continue;
            Vector3 dir = candidato.transform.position - origen;
            dir.y = 0f;
            float angulo = Vector3.SignedAngle(delante, dir, Vector3.up) * lado;   // > 0: hacia el lado pedido
            if (angulo < 0f) angulo = 360f + angulo;                               // los del otro lado, al final
            if (angulo >= mejorAngulo) continue;
            mejorAngulo = angulo;
            mejor = candidato;
        }
        if (mejor == null) return;
        Log($"Stick derecho: fijando {mejor.name}");
        SetTarget(mejor);
    }

    private Camera _camaraCache;

    /// El enemigo en combate más cercano al jugador que se puede fijar (nunca un cuerpo del grupo).
    private GameObject EnemigoMasCercano()
    {
        if (playerTransform == null) return null;
        ActiveCombatRegistry.GetAllInCombatNonAlloc(_combatBuffer);
        GameObject mejor = null;
        float mejorDist = maxLockDistance * maxLockDistance;
        Vector3 origen = playerTransform.position;
        for (int i = 0; i < _combatBuffer.Count; i++)
        {
            var npc = _combatBuffer[i];
            if (npc == null || ActiveCombatRegistry.IsCameraLockExempt(npc) || !EsFijable(npc)) continue;
            float d = (npc.transform.position - origen).sqrMagnitude;
            if (d < mejorDist) { mejorDist = d; mejor = npc; }
        }
        return mejor;
    }

    /// Los cuerpos de los tres protagonistas (también los que lleva la IA) no se fijan nunca: un
    /// compañero que entra en combate a su lado se apunta en el registro igual que un enemigo.
    private static bool EsDelGrupo(GameObject candidato) =>
        candidato != null && ControlDelJugador.PersonajeDe(candidato, out _);

    private void CambiarHaciaUnLado(int lado)
    {
        var targets = GetOrderedTargets();
        Vector3 origen = playerTransform.position;
        Vector3 referencia = currentTarget.transform.position - origen;
        referencia.y = 0f;
        if (referencia.sqrMagnitude < 0.0001f) referencia = playerTransform.forward;

        GameObject mejor = null;
        float mejorAngulo = float.PositiveInfinity;
        for (int i = 0; i < targets.Count; i++)
        {
            var candidato = targets[i];
            if (candidato == null || candidato == currentTarget) continue;
            Vector3 dir = candidato.transform.position - origen;
            dir.y = 0f;
            float angulo = Vector3.SignedAngle(referencia, dir, Vector3.up) * lado;   // > 0: hacia el lado pedido
            if (angulo <= 0.5f || angulo >= mejorAngulo) continue;
            mejorAngulo = angulo;
            mejor = candidato;
        }

        if (mejor == null) return;
        Log($"Stick derecho: cambiando target → {mejor.name}");
        SetTarget(mejor);
    }

    private void SwitchToNextTarget(bool incluirObjetos = true)
    {
        var targets = GetOrderedTargets(incluirObjetos);
        if (targets.Count == 0) { ReleaseLock(); return; }
        if (targets.Count == 1) { SetTarget(targets[0]); return; }
        
        int currentIndex = targets.IndexOf(currentTarget);
        int nextIndex = (currentIndex + 1) % targets.Count;
        
        Log($"RB: Cambiando target → {targets[nextIndex].name}");
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
        Log("Lock de cámara liberado");
    }

    private void EnsureVisualLockSync()
    {
        if (currentTarget == null || !currentTarget.activeInHierarchy ||
            (currentTarget.TryGetComponent<Targetable>(out var targetable) && !targetable.isActiveAndEnabled))
        {
            Log("Target lock inválido, liberando lock.");
            ReleaseLock();
            return;
        }

        var damageable = currentTarget.GetComponentInParent<Damageable>();
        if (damageable != null && !damageable.IsAlive)
        {
            Log("Target lock sin vida, liberando lock.");
            ReleaseLock();
            return;
        }

        if (Time.time - _lastVisualResync < visualResyncInterval) return;
        _lastVisualResync = Time.time;

        if (thirdPersonCamera != null && thirdPersonCamera.LockTarget != currentTarget.transform)
        {
            thirdPersonCamera.SetLockTarget(currentTarget.transform);
            Log($"Resync cámara -> {currentTarget.name}");
        }

        // No sincronizar el marcador durante diálogos ni cinemáticas para evitar que aparezca
        if (DebeSuprimirMarcador()) return;

        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            if (!playerTargeting.IsManualTargetActive || playerTargeting.CurrentTarget != currentTarget.transform)
            {
                playerTargeting.SetManualTarget(currentTarget.transform);
                Log($"Resync marcador -> {currentTarget.name}");
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
            Log("Levitación activa → liberando lock de cámara temporalmente");
            ReleaseLock();
        }
    }

    private void OnLevitationEnded()
    {
        _suppressedByLevitation = false;
        // Forzar re-lock inmediato en el siguiente Update si seguimos en combate
        _lastAutoLockAttempt = 0f;
        Log("Levitación terminada → restaurando lock de cámara");
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
