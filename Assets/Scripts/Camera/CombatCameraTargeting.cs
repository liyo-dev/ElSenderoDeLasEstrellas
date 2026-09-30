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
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
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

    private void Awake()
    {
        if (thirdPersonCamera == null)
            thirdPersonCamera = GetComponent<vThirdPersonCamera>();
        
        // Solo lee quién está registrado: la cámara no decide quién es el jugador (INC-482).
        if (playerTransform == null && PlayerService.TryGetPlayer(out var player, allowSceneLookup: false))
            playerTransform = player.transform;
        
        if (playerTargeting == null && playerTransform != null)
            playerTargeting = playerTransform.GetComponentInChildren<PlayerTargeting>();
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
        ReleaseLock();
    }

    // La cámara no es parte del personaje (CamaraDelJugador.prefab): sigue a quien se registre
    // como jugador. Ver INC-482.
    private void AlRegistrarJugador(GameObject jugador)
    {
        playerTransform = jugador != null ? jugador.transform : null;
        playerTargeting = playerTransform != null ? playerTransform.GetComponentInChildren<PlayerTargeting>() : null;
    }

    private void OnDialogueStarted(Transform _) => _isInDialogue = true;

    private void OnDialogueClosed(Transform _)
    {
        _isInDialogue = false;
        // Forzar resync inmediato del marker al cerrar el diálogo
        if (isLockActive && currentTarget != null)
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
        
        bool isInCombat = ActiveCombatRegistry.Count > 0;
        
        if (isInCombat && !wasInCombatLastFrame) OnEnterCombat();
        else if (!isInCombat && wasInCombatLastFrame) OnExitCombat();
        
        wasInCombatLastFrame = isInCombat;
        
        if (isInCombat && !isLockActive && !_suppressedByLevitation && !_targetingSuppressedByPlayer) TryAutoLock();
        if (isLockActive) EnsureVisualLockSync();
    }
    
    private void TryAutoLock()
    {
        if (_targetingSuppressedByPlayer) return;
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
            ToggleTargetingSuppressed();
            return;
        }

        if (!isLockActive || currentTarget == null)
            return;

        if (Time.unscaledTime - _lastShoulderSwitchAt < shoulderSwitchDebounce)
            return;
        
        switch (inputEvent.Type)
        {
            // RB pasa al siguiente enemigo; LB es de la rotación de hechizos básicos.
            case GamepadInputReader.InputEventType.RightShoulder:
                _lastShoulderSwitchAt = Time.unscaledTime;
                SwitchToNextTarget();
                break;
        }
    }

    /// <summary>
    /// Petición de Raúl (1 sep 2026): alterna si el jugador quiere que la cámara/objetivo de
    /// combate se enganchen solos. Al activar la supresión: libera cualquier lock en marcha y
    /// apaga también el auto-scan de PlayerTargeting (el marcador de apuntado libre), para que
    /// "desactivar targets" sea de verdad completo — ni cámara ni marcador — mientras el jugador
    /// quiera, por ejemplo, correr hacia un cofre sin que nada le distraiga hacia un enemigo
    /// cercano. Al desactivar la supresión, todo vuelve al comportamiento automático de siempre
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
    
    private void OnEnterCombat()
    {
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
        Log("🏳️ Saliendo de combate - Liberando lock");
        ReleaseLock();

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
            SwitchToNextTarget();
        }
    }
    
    private void SetTarget(GameObject newTarget)
    {
        if (newTarget == null)
        {
            ReleaseLock();
            return;
        }

        currentTarget = newTarget;
        isLockActive = true;

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetLockTarget(newTarget.transform);
        }

        // Con el objetivo fijado, el objetivo de los hechizos es el enemigo fijado aunque el
        // jugador esté de espaldas: al lanzar, Will se gira hacia él (INC-484/488). El auto-scan
        // se suprime para que no lo sustituya por otro enemigo.
        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.SetAutoScanSuppressed(true);
        }

        if (syncWithProjectileTargeting && playerTargeting != null && !_isInDialogue)
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
    
    private readonly List<GameObject> _orderedEnemiesCache = new();

    private List<GameObject> GetOrderedEnemies()
    {
        _orderedEnemiesCache.Clear();
        if (playerTransform == null) return _orderedEnemiesCache;

        var raw = ActiveCombatRegistry.GetAllInCombat();
        for (int i = 0; i < raw.Count; i++)
        {
            var g = raw[i];
            if (g != null && g.activeInHierarchy)
                _orderedEnemiesCache.Add(g);
        }

        if (_orderedEnemiesCache.Count <= 1) return _orderedEnemiesCache;

        Vector3 playerFwd = playerTransform.forward;
        playerFwd.y = 0;
        playerFwd.Normalize();
        Vector3 origin = playerTransform.position;

        _orderedEnemiesCache.Sort((a, b) =>
        {
            float angleA = Vector3.SignedAngle(playerFwd, (a.transform.position - origin).normalized, Vector3.up);
            float angleB = Vector3.SignedAngle(playerFwd, (b.transform.position - origin).normalized, Vector3.up);
            return angleA.CompareTo(angleB);
        });

        return _orderedEnemiesCache;
    }
    
    private void SwitchToNextTarget()
    {
        var enemies = GetOrderedEnemies();
        if (enemies.Count == 0) { ReleaseLock(); return; }
        if (enemies.Count == 1) { SetTarget(enemies[0]); return; }
        
        int currentIndex = enemies.IndexOf(currentTarget);
        int nextIndex = (currentIndex + 1) % enemies.Count;
        
        Log($"🔄 RB: Cambiando target → {enemies[nextIndex].name}");
        SetTarget(enemies[nextIndex]);
    }
    
    private void ReleaseLock()
    {
        if (!isLockActive) return;
        
        isLockActive = false;
        currentTarget = null;
        
        if (thirdPersonCamera != null)
        {
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
        if (currentTarget == null || !currentTarget.activeInHierarchy)
        {
            Log("⚠️ Target lock inválido, liberando lock.");
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

        // No sincronizar el marcador durante diálogos para evitar que aparezca
        if (_isInDialogue) return;

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
