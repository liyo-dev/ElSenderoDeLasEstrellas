using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Centraliza el bloqueo de movimiento del jugador con referencia por solicitante.
/// Deshabilita acciones de gameplay, CharacterController, Rigidbody y script de locomoción.
/// Usa el sistema centralizado de PlayerInputManager para gestionar UI/Gameplay.
/// </summary>
[DefaultExecutionOrder(-275)]
public class PlayerLockService : MonoBehaviour
{
    static PlayerLockService _instance;
    static bool _isShuttingDown;
    public static bool HasInstance => _instance != null;
    public static PlayerLockService Instance
    {
        get
        {
            // No recrear la instancia si el juego/escena ya está cerrando: crear un
            // GameObject nuevo en ese momento es exactamente lo que dispara el warning
            // de Unity "Some objects were not cleaned up when closing the scene"
            // (el nuevo GO queda huérfano porque DontDestroyOnLoad no llega a tiempo).
            if (_isShuttingDown)
                return _instance;

            if (_instance == null)
            {
                var go = new GameObject("PlayerLockService");
                _instance = go.AddComponent<PlayerLockService>();
                DontDestroyOnLoad(go);
                ServiceLocator.Register(_instance);
            }
            return _instance;
        }
    }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        _isShuttingDown = false;
    }
#endif

    readonly HashSet<object> _owners = new HashSet<object>();

    bool _hardLockActive; // true solo si ApplyHardLock() hizo PushUIMode — para parear el Pop exacto
    CharacterController _charController;
    bool _charControllerWasEnabled;
    Rigidbody _rb;
    MonoBehaviour _movementScript;
    bool _movementScriptWasEnabled;
    Invector.vCharacterController.vThirdPersonMotor _lockedMotor;
    bool _motorWasLocked;
    Invector.vCharacterController.vThirdPersonInput _suppressedInput;

    public bool IsLocked => _owners.Count > 0;

    /// <summary>
    /// Método de emergencia para forzar el desbloqueo del player.
    /// Solo usar en debug si el player queda bloqueado por un bug.
    /// </summary>
    public void ForceUnlock()
    {
        if (_owners.Count == 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerLockService] ⚠️ ForceUnlock() llamado pero no hay locks activos");
#endif
            return;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[PlayerLockService] 🚨 FORCE UNLOCK - Limpiando {_owners.Count} locks forzadamente");
#endif
        _owners.Clear();
        _lockedMotor = null; // evitar restaurar lockMovement al estado bloqueado
        ReleaseHardLock();
    }

    public void Acquire(object owner)
    {
        if (owner == null) owner = this;
        if (_owners.Contains(owner))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PlayerLockService] ⚠️ Owner ya tenía un lock: {owner?.GetType().Name ?? "null"}");
#endif
            return;
        }

        _owners.Add(owner);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PlayerLockService] 🔒 Acquire de {owner?.GetType().Name ?? "null"}. Total locks: {_owners.Count}");
#endif
        if (_owners.Count == 1)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerLockService] 🚫 Primer lock - Deshabilitando movimiento del jugador");
#endif
            ApplyHardLock();
        }
    }

    public void Release(object owner)
    {
        if (owner == null) owner = this;
        
        if (!_owners.Contains(owner))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PlayerLockService] ⚠️ Intento de Release de owner no registrado: {owner?.GetType().Name ?? "null"}");
#endif
            return;
        }

        _owners.Remove(owner);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PlayerLockService] 🔓 Release de {owner?.GetType().Name ?? "null"}. Locks restantes: {_owners.Count}");
#endif
        if (_owners.Count == 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerLockService] ✅ Todos los locks liberados - Reactivando movimiento del jugador");
#endif
            ReleaseHardLock();
        }
    }

    /// <summary>
    /// Atajo para el patrón "puente de un trigger hasta que el sistema narrativo tome el
    /// control": adquiere el lock YA (freeze inmediato) y lo libera en cuanto
    /// ActionMode.Cinematic esté activo en PlayerActionManager, o tras maxFramesSafety frames
    /// si el grafo nunca llega a empujar ese modo (para no dejar al jugador congelado para
    /// siempre por un evento sin nodo de bloqueo después, o un WaitCustomEventNode que tarda
    /// varios frames en encadenar hasta el nodo que realmente hace PushMode).
    ///
    /// La corrutina vive aquí (DontDestroyOnLoad) por dos razones:
    /// 1) NarrativeRunner avanza nodo a nodo con yield, siempre al menos 1 frame por nodo aunque
    ///    sea síncrono. Si entre el WaitCustomEventNode y el LockPlayerNode hay varios nodos, un
    ///    freeze de "1 frame fijo" se soltaría antes de que el grafo tome el control real.
    /// 2) Triggers con DestroyElement=1 (p.ej. EXIT_FROM_WOODS_ESTELA) destruyen su propio
    ///    GameObject el mismo frame en que emiten el evento; una corrutina alojada en ese
    ///    GameObject se abortaría por OnDestroy y el lock se soltaría en el acto.
    /// </summary>
    public void AcquireBridgeUntilCinematic(object owner, int maxFramesSafety = 60)
    {
        Acquire(owner);
        StartCoroutine(Co_ReleaseWhenCinematicOrTimeout(owner, maxFramesSafety));
    }

    IEnumerator Co_ReleaseWhenCinematicOrTimeout(object owner, int maxFramesSafety)
    {
        var pam = ServiceLocator.Get<PlayerActionManager>(logIfMissing: false);
        int frames = 0;
        yield return null; // como mínimo 1 frame, igual que el comportamiento anterior
        while (frames < maxFramesSafety && (pam == null || !pam.IsInMode(ActionMode.Cinematic)))
        {
            frames++;
            yield return null;
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (pam != null && !pam.IsInMode(ActionMode.Cinematic))
        {
            Debug.LogWarning($"[PlayerLockService] Puente de {owner?.GetType().Name ?? "null"} liberado por timeout " +
                              $"({maxFramesSafety} frames) sin que el grafo narrativo activara ActionMode.Cinematic. " +
                              "¿Falta un LockPlayerNode/CinematicSequencerBase tras el WaitCustomEventNode de este evento?");
        }
#endif
        Release(owner);
    }

    void ApplyHardLock()
    {
        if (!PlayerService.TryGetPlayer(out var player, true) || player == null)
            return;

        // Cambiar a modo UI usando el sistema centralizado
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
        {
            pim.PushUIMode();
            _hardLockActive = true;
        }

        _charController = player.GetComponent<CharacterController>();
        if (_charController != null)
        {
            _charControllerWasEnabled = _charController.enabled;
            _charController.enabled = false;
        }

        _rb = player.GetComponent<Rigidbody>();
        if (_rb != null)
        {
            // Solo modificar velocidad si NO es kinematic
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            
            // NO PONER EN KINEMATIC - dejar que los scripts de Invector se deshabiliten
            // _rb.isKinematic = true; // ❌ ESTO CAUSA LOS WARNINGS
        }

        // Bloquear movimiento sin deshabilitar el controller completo.
        // Deshabilitar vThirdPersonController para UpdateMotor() que gestiona
        // CheckGround() y ControlMaterialPhysics(). Sin esto el CapsuleCollider
        // queda con slippyPhysics (fricción 0) y el player cae a través del suelo.
        _lockedMotor = player.GetComponent<Invector.vCharacterController.vThirdPersonMotor>();
        if (_lockedMotor != null)
        {
            _motorWasLocked = _lockedMotor.lockMovement;
            _lockedMotor.lockMovement = true;

            // lockMovement no resetea inputSmooth/moveDirection (Invector: "lock the movement,
            // not the animation"). ControlAnimatorRootMotion (OnAnimatorMove) ignora lockMovement,
            // así que el root motion acumula desfase mientras el lock está activo; al soltarlo el
            // player "salta" hacia delante. ResetInputSmoothing() previene ese acumulado.
            _lockedMotor.ResetInputSmoothing();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerLockService] lockMovement=true en vThirdPersonMotor (inputSmooth/moveDirection reseteados)");
#endif
        }
        else
        {
            // Fallback: deshabilitar el script si no se encuentra vThirdPersonMotor
            _movementScript = player.GetComponents<MonoBehaviour>()
                .FirstOrDefault(m => m != null && m.enabled && m != this && !(m is PlayerActionManager) && (
                    m.GetType().Name == "vThirdPersonController" ||
                    m.GetType().Name == "vThirdPersonInput" ||
                    m.GetType().Name == "ThirdPersonController" ||
                    m.GetType().Name == "ThirdPersonInput"
                ));
            if (_movementScript != null)
            {
                _movementScriptWasEnabled = _movementScript.enabled;
                _movementScript.enabled = false;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[PlayerLockService] Fallback: script '{_movementScript.GetType().Name}' DESHABILITADO");
#endif
            }
            else
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerLockService] No se encontró vThirdPersonMotor ni script de movimiento");
#endif
            }
        }

        // Pone cc.input a cero de inmediato y bloquea jump/sprint en vThirdPersonInput.
        // Llamar MoveInput() explícitamente para zerear cc.input en este frame sin esperar al
        // próximo Update() — evita que un FixedUpdate intermedio aplique movimiento residual.
        _suppressedInput = player.GetComponent<Invector.vCharacterController.vThirdPersonInput>();
        if (_suppressedInput != null)
        {
            _suppressedInput.SuppressMoveInput = true;
            _suppressedInput.MoveInput();
        }
    }

    void ReleaseHardLock()
    {
        // Restaurar modo Gameplay usando el sistema centralizado — solo si PushUIMode fue emitido
        if (_hardLockActive)
        {
            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PopUIMode();
            _hardLockActive = false;
        }

        if (_charController != null)
        {
            _charController.enabled = _charControllerWasEnabled;
        }
        _charController = null;

        // NO restaurar isKinematic ya que nunca lo cambiamos
        // if (_rb != null)
        // {
        //     _rb.isKinematic = _rbWasKinematic;
        // }
        _rb = null;

        if (_lockedMotor != null)
        {
            _lockedMotor.lockMovement = _motorWasLocked;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerLockService] lockMovement restaurado en vThirdPersonMotor");
#endif
            _lockedMotor = null;
        }
        else if (_movementScript != null)
        {
            _movementScript.enabled = _movementScriptWasEnabled;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlayerLockService] Script de movimiento '{_movementScript.GetType().Name}' RESTAURADO");
#endif
        }
        _movementScript = null;

        // Restaurar SuppressMoveInput y añadir gracia para evitar salto al cerrar UI
        if (_suppressedInput != null)
        {
            _suppressedInput.SuppressMoveInput = false;
            _suppressedInput = null;
        }
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);
    }


    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _isShuttingDown = false;

        // Suscribirse a cambios de escena para auto-limpieza
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_instance == this)
        {
            _instance = null;
            // _isShuttingDown NO se pone a true aquí: solo lo hace OnApplicationQuit.
            // Si se pusiera en OnDestroy, cualquier destrucción accidental del singleton
            // (recarga de escena en testeo, bug externo) lo dejaría atascado en true para
            // siempre — Instance devolvería null en silencio y ningún freeze funcionaría
            // el resto de la sesión. Ver INC-448.
        }
    }

    void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }
    
    /// <summary>
    /// Al cargar una nueva escena, limpiar locks huérfanos (de objetos destruidos).
    /// Esto previene que el player quede bloqueado en escenas de testeo.
    /// </summary>
    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Solo limpiar en carga normal (no aditiva)
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlayerLockService] 🔍 Escena cargada '{scene.name}' - Verificando locks...");
#endif
            var deadOwners = new List<object>();
            foreach (var owner in _owners)
            {
                if (owner is UnityEngine.Object unityObj && unityObj == null)
                    deadOwners.Add(owner);
            }

            if (deadOwners.Count > 0)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[PlayerLockService] 🧹 Limpiando {deadOwners.Count} locks huérfanos al cargar escena '{scene.name}'");
#endif
                foreach (var dead in deadOwners)
                    _owners.Remove(dead);
            }

            // En modo testeo se limpian todos los locks para evitar que el player quede
            // bloqueado cuando se saltan cinemáticas en grafos narrativos.
            bool isTestingMode = GameBootService.IsAvailable &&
                                 GameBootService.Profile != null &&
                                 GameBootService.Profile.ShouldBootFromPreset();

            if (isTestingMode && _owners.Count > 0)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[PlayerLockService] 🧪 Modo testeo detectado - Limpiando {_owners.Count} locks al cargar escena '{scene.name}'");
#endif
                _owners.Clear();
            }

            if (_owners.Count == 0)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[PlayerLockService] ✅ Todos los locks limpiados - Reactivando movimiento del jugador");
#endif
                ReleaseHardLock();
            }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            else
            {
                Debug.Log($"[PlayerLockService] ⚠️ {_owners.Count} locks aún activos tras limpieza");
            }
#endif
        }
    }
}
