using UnityEngine;
using UnityEngine.InputSystem;
using Core;
using System.Collections;
using Invector.vCharacterController;

// Después de las cámaras (INC-426): mientras Will duerme, el plano lo pone este LateUpdate.
[DefaultExecutionOrder(500)]
public class SleepTrigger : MonoBehaviour
{
    [Header("Referencia al jugador")]
    public GameObject player;
    [Header("Nombre del estado de animación de dormir")]
    public string sleepAnimationState = "Sleeping_NoWeapon";
    [Header("Anchor de posición en la cama")]
    public Transform bedPosition;

    [Header("Cámara cenital")]
    [Tooltip("Posición y rotación que adoptará la cámara mientras duerme. Déjalo vacío para no mover la cámara.")]
    public Transform sleepCameraAnchor;
    [Tooltip("Velocidad de transición de la cámara al dormir/despertar.")]
    public float cameraTransitionSpeed = 2f;

    [Header("Expresión facial")]
    [Tooltip("Expresión de la cara mientras duerme. None mantiene la expresión actual.")]
    public NPCEmotion sleepEmotion = NPCEmotion.Tired;

    [Header("Despertar")]
    [Tooltip("Posición en el suelo donde Will se coloca al despertar (pie de la cama). Sin esto, permanece en bedPosition.")]
    public Transform wakeUpPosition;
    [Tooltip("Rotación de referencia para la cámara al despertar (solo se usa euler.y → horizontal, euler.x → vertical). Evita que la cámara aparezca detrás de una pared.")]
    public Transform wakeUpCameraAnchor;

    [Header("Despertar subjetivo (INC-424)")]
    [Tooltip("Solo con sleepOnStart: al acabar el sueño la cámara está EN los ojos de Will, mirando " +
             "al techo, con los párpados pesados y dos parpadeos. El botón de despertar abre los ojos " +
             "del todo y le incorpora; después, el control normal.")]
    public bool despertarSubjetivo = true;

    [Header("Narrativa")]
    [Tooltip("Si true, Will empieza dormido en esta cama al arrancar la escena sin necesidad de entrar al trigger.")]
    public bool sleepOnStart = false;
    [Tooltip("Evento que se dispara al despertar. Compatible con WaitCustomEventNode.")]
    public string wakeNarrativeEvent = "";

    [Header("Uso único")]
    [Tooltip("Si true, este trigger solo puede activarse una vez. El flag se guarda en el preset.")]
    public bool playOnlyOnce = false;
    [Tooltip("ID único para recordar si este trigger ya se ejecutó (requerido con playOnlyOnce).")]
    public string persistenceId = "";

    private bool isSleeping = false;
    private float _sleepStartTime = -999f;
    private int _sleepStateHash = -1;

    private Animator playerAnimator;
    private PlayerActionManager playerActionManager;
    private NPCEmotionController _playerEmotion;

    private vThirdPersonInput _playerInput;

    private vThirdPersonCamera _tpsCamera;
    private Camera _mainCamera;
    private bool _wasCameraLocked;
    private Coroutine _cameraCoroutine;

    void OnEnable()
    {
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
    }

    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleGamepadInput;
    }

    void Start()
    {
        if (!sleepOnStart) return;
        if (playOnlyOnce && AlreadyPlayed()) return;
        StartCoroutine(ForceSleepNextFrame());
    }

    void LateUpdate()
    {
        if (!isSleeping) return;

        if (bedPosition != null && player != null)
        {
            player.transform.position = bedPosition.position;
            player.transform.rotation = bedPosition.rotation;
        }

        // FIX (18 sep 2026): mientras Will duerme (sleepOnStart, p. ej. el anchor 'Bedroom'), este
        // LateUpdate clavaba la cámara en sleepCameraAnchor CADA FRAME sin saber si algún sistema
        // cinemático (CinematicCameraDriver vía CameraDirectorService, p. ej. SEQ_Prologo_UltimaNoche)
        // tenía la cámara reclamada para sus propios planos — el mismo tipo de choque entre dos
        // sistemas de cámara que no se conocen que motivó crear CameraDirectorService. Resultado real:
        // la cinemática del prólogo calculaba y aplicaba bien sus planos (el panadero, el horno...),
        // pero este trigger los pisaba el mismo frame o el siguiente, así que en pantalla nunca se
        // veía nada más que a Will dormido. isSleeping sigue en true durante toda la cinemática a
        // propósito (WakeUp() se ignora mientras CinematicSequencerBase.AnySequenceActive, ver
        // HandleGamepadInput/INC-084) — así que sin este guard no había forma de que la cinemática
        // ganara la cámara ni un solo frame. Cede el control mientras alguien la tenga reclamada;
        // en cuanto se libera (fin de la cinemática, con su margen de gracia), este trigger retoma
        // el plano cenital como antes.
        // El despertar subjetivo es el FINAL de un sueño: solo cuando ya ha pasado una secuencia
        // mientras dormía (el prólogo). Antes de eso, el plano de siempre.
        if (isSleeping && (CameraDirectorService.HasOwner || CinematicSequencerBase.AnySequenceActive))
            _vioUnaSecuencia = true;

        if (UsaDespertarSubjetivo && _vioUnaSecuencia && _mainCamera != null && _cameraCoroutine == null
            && !CameraDirectorService.HasOwner)
        {
            // Nadie más mueve esta cámara mientras Will abre los ojos (INC-426): al acabar el prólogo
            // algo vuelve a encender la cámara de juego, y su LateUpdate y este se pisaban el sitio
            // fotograma a fotograma — «la cámara hace algo raro, como si hubiese alguna en conflicto».
            if (_tpsCamera != null && _tpsCamera.enabled) _tpsCamera.enabled = false;

            if (PoseSubjetiva(out var pos, out var rot, respirando: !_abriendoLosOjos, _incorporado))
            {
                _mainCamera.transform.SetPositionAndRotation(pos, rot);
                EmpezarDespertarSubjetivoSiToca();
            }
        }
        else if (sleepCameraAnchor != null && _mainCamera != null && _cameraCoroutine == null
            && !CameraDirectorService.HasOwner)
        {
            _mainCamera.transform.position = sleepCameraAnchor.position;
            _mainCamera.transform.rotation = sleepCameraAnchor.rotation;
        }

        // Igual que PlayerAmbientActivityHandler.LateUpdate(): el motor corre con lockMovement=true,
        // pero con el CC desactivado podría registrar isGrounded=false → animación de caída.
        if (playerAnimator != null)
        {
            try { playerAnimator.SetBool(vAnimatorParameters.IsGrounded, true); } catch { }
            try { playerAnimator.SetFloat(vAnimatorParameters.GroundDistance, 0f); } catch { }

            // FIX (12 sep 2026): "Will sigue de pie en la cama" — reportado con sleepOnStart (Will
            // ya duerme al cargar la escena, antes de que arranque el sueño del prólogo) y, a
            // diferencia del bug del 15 ago (Animator sin el estado — eso ya deja su propio
            // Debug.LogError si vuelve a pasar), esta vez SetupSleep() sí consigue reproducir
            // 'sleepAnimationState' sin error. Hipótesis de Raúl: "es por temas de tiempos" — y
            // encaja con el propio comentario de más abajo en SetupSleep() sobre WorldBootstrap
            // pudiendo aplicar la apariencia del personaje activo DESPUÉS del Play() de aquí (mismo
            // orden de ejecución que ya causó el bug de "cae encima de la cama" con IsGrounded, ver
            // el FIX del 15 ago un poco más arriba en este archivo): un Rebind/reasignación de
            // Animator Controller en ese punto devuelve al Animator a su estado de entrada por
            // defecto (de pie), pisando el Play() que ya se había hecho. playerAnimator.Play() es
            // un one-shot, no algo que se reafirme solo — así que, igual que ya hacemos con
            // IsGrounded arriba, se vigila aquí CADA FRAME mientras isSleeping siga activo y se
            // fuerza de vuelta si algo externo lo saca del estado de dormir.
            if (_sleepStateHash != -1)
            {
                var stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.shortNameHash != _sleepStateHash && !playerAnimator.IsInTransition(0))
                {
                    playerAnimator.Play(_sleepStateHash, 0, 0f);
                }
            }
        }
    }

    IEnumerator ForceSleepNextFrame()
    {
        // FIX (11 sep 2026): "Will sigue sin salir acostado" — antes esto esperaba UN solo frame e
        // intentaba resolver al jugador UNA sola vez ('player' o PlayerService.Player); si en ese
        // frame el jugador todavía no estaba listo (p. ej. WorldBootstrap sigue cargando la escena
        // aditiva de esta habitación y/o esperando a que el jugador exista antes de teletransportarlo
        // — puede tardar varios frames), fallaba en silencio y Will nunca se dormía. Ahora reintenta
        // con un timeout, sondeando solo PlayerService.Player (referencia ya cacheada, sin
        // FindObjectOfType ni GameObject.Find — ver AGENTS.md § 2).
        const float maxWait = 5f;
        float elapsed = 0f;
        GameObject playerGO = null;

        while (elapsed < maxWait)
        {
            playerGO = player != null ? player : PlayerService.Player;
            if (playerGO != null) break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (playerGO != null)
        {
            ForceSleep(playerGO);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogError($"[SleepTrigger] '{name}': sleepOnStart=true pero no se pudo resolver el " +
                $"jugador tras {maxWait:F0}s (ni 'player' ni PlayerService.Player). Will no se ha " +
                $"dormido al arrancar la escena.", this);
        }
#endif
    }

    /// <summary>Pone a Will a dormir desde código (ej: llamado por el grafo narrativo o sleepOnStart).</summary>
    public void ForceSleep(GameObject playerGO)
    {
        player = playerGO;
        SetupSleep(playerGO);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeInHierarchy) return;
        if (isSleeping) return;
        if (!other.CompareTag("Player")) return;
        if (playOnlyOnce && AlreadyPlayed()) return;

        // FIX (15 ago 2026, prioridad demo): "Will cayendo/de pie en vez de dormido" — causa real
        // confirmada con el diagnóstico de esta misma sesión (log: "CC[sin CharacterController]").
        // El fallback a other.gameObject cuando GetComponentInParent<CharacterController>() no
        // encontraba nada operaba sobre el GameObject del propio collider del trigger — casi nunca
        // la raíz real del jugador — así que todo lo que hacía SetupSleep()/WakeUp() (teleport,
        // Play() de la animación, PushMode) se aplicaba a un objeto que nadie ve, mientras el Will
        // real seguía bajo el control normal de Invector (por eso "cae": su gravedad de siempre
        // seguía activa). PlayerService.Player es la misma referencia central que ya usa
        // ForceSleepNextFrame() más abajo en este archivo — se prioriza aquí también en vez de
        // fiarse de un fallback silencioso.
        var playerGO = PlayerService.Player != null
            ? PlayerService.Player
            : other.GetComponentInParent<CharacterController>()?.gameObject;

        if (playerGO == null)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SleepTrigger] '{name}': no se pudo resolver el GameObject real del " +
                $"jugador (ni PlayerService.Player ni CharacterController en los padres de " +
                $"'{other.name}'). Abortando para no operar sobre el objeto equivocado.", this);
            #endif
            return;
        }

        player = playerGO;
        SetupSleep(playerGO);
    }

    void SetupSleep(GameObject playerGO)
    {
        // FIX: en el rig del jugador (Invector) el Animator vive en un hijo ("model"), no en la
        // raíz — igual que _playerEmotion ya resolvía con GetComponentInChildren. Con
        // GetComponent() (solo raíz) esto devolvía null en el rig real, así que
        // `playerAnimator.Play(sleepAnimationState)` de abajo nunca llegaba a ejecutarse (el guard
        // `if (playerAnimator != null)` lo saltaba en silencio) y Will se quedaba en la pose en la
        // que estuviera (de pie) en vez de tumbarse. Antes esto quedaba tapado por el bug de AABB de
        // culling ya corregido (el personaje se veía "flotando/roto" de cualquier forma); al arreglar
        // ese bug quedó a la vista que la animación de dormir tampoco se estaba aplicando nunca.
        playerAnimator      = playerGO.GetComponentInChildren<Animator>(true);
        playerActionManager = playerGO.GetComponent<PlayerActionManager>();
        _playerEmotion      = playerGO.GetComponentInChildren<NPCEmotionController>();
        _playerInput         = playerGO.GetComponent<vThirdPersonInput>();

        // PushMode PRIMERO: PlayerLockService capturará CC=true y lockMovement=false como estado previo,
        // y los bloqueará. Si hacemos push después de bloquearlos manualmente, capturaría el estado
        // ya bloqueado y al despertar los "restauraría" en estado bloqueado.
        playerActionManager?.PushMode(ActionMode.Cinematic);

        // FIX (15 ago 2026): "se acuesta y enseguida se pone con la animación de caer" — causa real,
        // no la del intento anterior (el LateUpdate de más abajo forzando IsGrounded=true, que se
        // quedó corto). vThirdPersonInput.Update() llama cada frame a cc.UpdateMotor()/UpdateAnimator(),
        // que reescribe el parámetro IsGrounded del Animator con el resultado del ground-check propio
        // de Invector — falso, porque PlayerLockService ya desactivó el CharacterController al hacer
        // PushMode de arriba. Unity evalúa las transiciones del Animator justo después de Update() y
        // ANTES de LateUpdate(): por eso corregir IsGrounded=true en LateUpdate() (más abajo en este
        // mismo archivo) siempre llega un paso tarde — la transición hacia el estado de caída ya se
        // disparó ESE MISMO FRAME con el valor falso que Invector acaba de escribir en Update(). Único
        // arreglo real: que Invector deje de escribir el parámetro mientras se duerme. Desactivar el
        // componente entero detiene su Update() (y con él ese push erróneo); se reactiva en WakeUp().
        if (_playerInput != null) _playerInput.enabled = false;

        // Snap a la posición de cama (CC ya desactivado por PlayerLockService vía PushMode)
        if (bedPosition != null)
        {
            playerGO.transform.position = bedPosition.position;
            playerGO.transform.rotation = bedPosition.rotation;
        }

        // FIX (15 ago 2026, prioridad demo): "Will sale de pie en vez de dormido" — Play() con un
        // string NO avisa si ese estado no existe en el controller actualmente activo, se queda
        // callado y sin hacer nada (por eso no salía ninguna excepción en los logs). Encontrado por
        // datos, no adivinado: el clip "Sleeping_NoWeapon" solo existe en
        // NoWeaponStanceExtraAnim.controller — NoWeaponStance.controller (el otro candidato "sin
        // arma") no lo tiene. Si el Animator activo de Will está usando el controller que no tiene
        // el estado, esto es la causa exacta. HasState() lo confirma en el acto la próxima vez que
        // se pruebe, con el nombre real del controller puesto — no hace falta adivinar más.
        if (playerAnimator != null)
        {
            _sleepStateHash = Animator.StringToHash(sleepAnimationState);
            if (!playerAnimator.HasState(0, _sleepStateHash))
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SleepTrigger] El Animator Controller activo en '{playerGO.name}' " +
                    $"('{playerAnimator.runtimeAnimatorController?.name ?? "ninguno"}') NO tiene un " +
                    $"estado llamado '{sleepAnimationState}' en el layer 0 — por eso Will se queda de " +
                    $"pie en vez de tumbarse. El clip existe en NoWeaponStanceExtraAnim.controller; " +
                    $"revisa si es ese el controller que debería estar asignado aquí.", playerGO);
                #endif
                // Sin estado en el controller no hay nada que reafirmar en LateUpdate() — desactiva
                // el guard de más abajo para no gastar GetCurrentAnimatorStateInfo() cada frame en vano.
                _sleepStateHash = -1;
            }
            playerAnimator.Play(_sleepStateHash != -1 ? _sleepStateHash : Animator.StringToHash(sleepAnimationState));
        }

        if (sleepEmotion != NPCEmotion.None)
            _playerEmotion?.SetEmotion(sleepEmotion);

        // FIX "personaje flotando/desencajado en la cama" (bug reportado en el prólogo — Estela/
        // Will apareciendo flotando sobre la cama en vez de Will dormido normalmente): mismo AABB
        // de culling atascado que documenta ModularAutoBuilder.RefreshRendererBoundsAfterAppearanceChange.
        // Aquí el teleport a bedPosition + el Play() forzado de la animación de dormir cambian de
        // golpe la posición Y la pose del rig, justo después de que WorldBootstrap haya podido
        // aplicar la apariencia del personaje activo — sin este refresco, el bounds de culling de
        // cada SkinnedMeshRenderer puede quedarse calculado con la pose/posición ANTERIOR hasta que
        // algo más lo fuerce, dando la sensación de que el personaje "vuela" sobre la cama.
        RefreshPlayerRendererBounds(playerGO);

        MoveCameraToSleepAnchor();

        _sleepStartTime = Time.time;
        isSleeping = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // DIAGNÓSTICO TEMPORAL (15 ago 2026) — bug "Will cayendo encima de la cama": ninguna de las
        // pruebas hechas hasta ahora dejó una excepción asociada en el log, así que es un glitch
        // visual silencioso (no hay nada que ya quede registrado para diagnosticarlo con evidencia
        // real). Esto vuelca posición Y / estado de grounded frame a frame justo después de
        // SetupSleep(), para que la PRÓXIMA repro sí deje rastro en el log. Quitar en cuanto se
        // confirme la causa — no debe quedarse en el proyecto a largo plazo.
        StartCoroutine(DiagnosticLogSleepFrames(playerGO));
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private IEnumerator DiagnosticLogSleepFrames(GameObject playerGO)
    {
        var cc = playerGO.GetComponent<CharacterController>();
        for (int i = 0; i < 20; i++)
        {
            string ccState = cc != null ? $"enabled={cc.enabled} isGrounded={(cc.enabled ? cc.isGrounded.ToString() : "n/a (disabled)")}" : "sin CharacterController";
            string animGrounded = playerAnimator != null
                ? playerAnimator.GetBool(vAnimatorParameters.IsGrounded).ToString()
                : "sin animator";
            Debug.Log($"[SleepTrigger:DIAG] frame={i} y={playerGO.transform.position.y:F3} CC[{ccState}] anim.IsGrounded={animGrounded}");
            yield return null;
        }
    }
#endif

    /// <summary>
    /// Fuerza el recálculo del AABB de culling de cada SkinnedMeshRenderer del rig del jugador —
    /// mismo patrón que ModularAutoBuilder.RefreshRendererBoundsAfterAppearanceChange() y los otros
    /// call sites documentados en ActiveCharacterSwapper. Se llama tras cualquier teleport+cambio
    /// de pose brusco de este trigger (SetupSleep/WakeUp) para evitar que el personaje se vea
    /// "flotando" un instante hasta que algo más refresque sus bounds.
    /// </summary>
    private static void RefreshPlayerRendererBounds(GameObject playerGO)
    {
        if (playerGO == null) return;
        var animator = playerGO.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.Update(0f);

        foreach (var smr in playerGO.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.updateWhenOffscreen) smr.updateWhenOffscreen = true;
            if (smr.gameObject.activeInHierarchy) _ = smr.bounds;
        }
    }

    void WakeUp()
    {
        if (!isSleeping) return;
        isSleeping = false;
        TerminarDespertarSubjetivo();

        // Forzar ángulo de cámara antes de re-habilitarla (evita que aparezca detrás de la pared)
        if (wakeUpCameraAnchor != null && _tpsCamera != null)
        {
            var a = wakeUpCameraAnchor.eulerAngles;
            _tpsCamera.SetAngles(a.y, a.x);
        }
        RestoreCamera();

        // Teleportar al suelo (CC todavía desactivado — PlayerLockService lo gestiona)
        if (wakeUpPosition != null)
        {
            player.transform.position = wakeUpPosition.position;
            player.transform.rotation = wakeUpPosition.rotation;
        }

        // Mismo fix que en SetupSleep(): otro teleport brusco del rig, misma necesidad de refrescar
        // el bounds de culling después.
        RefreshPlayerRendererBounds(player);

        _playerEmotion?.ForceReset();

        // PopMode → PlayerLockService.Release → ReleaseHardLock:
        // restaura CC=true, lockMovement=false, SuppressMoveInput=false, PopUIMode, IgnoreJumpButton
        playerActionManager?.PopMode(ActionMode.Cinematic);

        // Reactivar vThirdPersonInput (desactivado en SetupSleep) DESPUÉS de que PopMode ya haya
        // restaurado el CharacterController — así su primer Update() con ground-check real encuentra
        // el CC ya activo, en vez de un frame intermedio con el CC todavía desactivado.
        if (_playerInput != null) _playerInput.enabled = true;

        // El motor corre con lockMovement=false → CrossFade funciona correctamente
        if (playerAnimator != null)
            playerAnimator.CrossFadeInFixedTime("Free Locomotion", 0.2f, 0);

        TutorialPromptUI.Instance?.Hide();

        if (!string.IsNullOrEmpty(wakeNarrativeEvent))
            DefaultNarrativeSignals.Instance?.RaiseCustom(wakeNarrativeEvent, name);

        if (playOnlyOnce)
            MarkAsPlayed();
    }

    void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (!isSleeping) return;
        if (input.Phase != InputActionPhase.Performed) return;
        // Aceptar Interact (GamePlay map) y Submit (fallback hardware que siempre emite aunque
        // GamePlay esté deshabilitado, p.ej. en modo Cinematic).
        bool isWakeInput = input.Type == GamepadInputReader.InputEventType.Interact
                        || input.Type == GamepadInputReader.InputEventType.Submit;
        if (!isWakeInput) return;

        // FIX (24 ago 2026, INC-084): "Will se levanta de la cama al saltar una secuencia del
        // Prólogo" — Submit se diseñó a propósito para llegar siempre aquí, incluso en modo
        // Cinematic (ver comentario de arriba), precisamente para poder despertar a Will con un
        // botón. Pero HoldToSkipUI (el botón global de "mantener para saltar") usa ese MISMO
        // InputAction (Controls.UI.Submit) como hold — y GamepadInputReader emite el evento Submit
        // ya en el primer frame de pulsación (performed), no al completar el hold. Resultado: en
        // cuanto el jugador empieza a mantener pulsado para saltar cualquier secuencia que ocurra
        // mientras Will sigue dormido (p. ej. el sueño del Prólogo, PrologueDreamSequencer, que se
        // reproduce con Will ya en la cama), este listener recibía ese mismo Submit como "orden de
        // despertar" y sacaba a Will de la cama al instante — sin esperar a que la propia secuencia
        // terminase ni a que empezara de verdad la escena de despertar. Ignorar el input de
        // despertar mientras haya cualquier cinemática/secuencia narrativa activa o saltable evita
        // el falso positivo sin afectar al resto de usos de SleepTrigger: fuera de una secuencia
        // activa (p. ej. una cama normal explorando el mundo) despertar por input sigue funcionando
        // exactamente igual que antes.
        if (CinematicSequencerBase.AnySequenceActive || NarrativeSkipHub.AnySkippable) return;

        // Grace period: ignorar input del primer segundo para evitar despertar inmediato al cargar escena
        if (Time.time - _sleepStartTime < 1f) return;

        if (UsaDespertarSubjetivo && _subjetivoEmpezado && _mainCamera != null)
        {
            if (_abriendoLosOjos) return;
            _abriendoLosOjos = true;
            StartCoroutine(Co_AbrirLosOjos());
            return;
        }

        WakeUp();
    }

    // --- Uso único ---

    private string FlagKey() => $"SLEEP_DONE:{persistenceId}";

    private bool AlreadyPlayed()
    {
        if (string.IsNullOrEmpty(persistenceId))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SleepTrigger] '{name}' tiene playOnlyOnce=true pero persistenceId está vacío. El trigger no se desactivará.", this);
#endif
            return false;
        }
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        return preset?.flags != null && preset.flags.Contains(FlagKey());
    }

    private void MarkAsPlayed()
    {
        if (string.IsNullOrEmpty(persistenceId)) return;
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset == null) return;
        preset.flags ??= new System.Collections.Generic.List<string>();
        if (!preset.flags.Contains(FlagKey()))
            preset.flags.Add(FlagKey());
    }

    // --- Cámara ---

    void InitCameraIfNeeded()
    {
        if (_tpsCamera != null) return;
        _tpsCamera = ServiceLocator.Get<vThirdPersonCamera>(false);
        if (_tpsCamera != null)
            _mainCamera = _tpsCamera.GetComponent<Camera>();
    }

    void MoveCameraToSleepAnchor()
    {
        if (sleepCameraAnchor == null) return;
        InitCameraIfNeeded();
        if (_tpsCamera == null || _mainCamera == null) return;

        _wasCameraLocked = _tpsCamera.lockCamera;
        // Deshabilitar el componente completo detiene su LateUpdate, que de lo contrario
        // sobreescribiría la posición de cámara que establece el coroutine cada frame.
        _tpsCamera.enabled = false;

        if (_cameraCoroutine != null) StopCoroutine(_cameraCoroutine);

        // FIX INC-397 (24 sep 2026): el mismo choque que el del LateUpdate (18 sep), pero por la
        // otra puerta. Con el prólogo, WillHouse se carga DESPUÉS de que la cinemática haya
        // empezado y puesto su primer plano: este viaje de medio segundo cogía la cámara desde
        // el cielo del valle y la dejaba clavada en el plano cenital de la cama. Mientras la
        // secuencia usaba un travelling largo (que reescribe la cámara cada frame) no se notaba;
        // con un corte seco se quedaba ahí hasta el plano siguiente — «lo de las nubes sale en la
        // habitación de Will». Si alguien tiene la cámara reclamada, no se toca: el LateUpdate ya
        // pone el plano de la cama en cuanto la suelte.
        if (CameraDirectorService.HasOwner) return;

        _cameraCoroutine = StartCoroutine(TransitionCamera(
            _mainCamera.transform.position, _mainCamera.transform.rotation,
            sleepCameraAnchor.position,     sleepCameraAnchor.rotation));
    }

    void RestoreCamera()
    {
        if (_tpsCamera == null) return;
        _tpsCamera.lockCamera = _wasCameraLocked;
        _tpsCamera.enabled = true;
        if (_cameraCoroutine != null) { StopCoroutine(_cameraCoroutine); _cameraCoroutine = null; }
    }

    IEnumerator TransitionCamera(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot)
    {
        float elapsed = 0f;
        float duration = 1f / Mathf.Max(0.01f, cameraTransitionSpeed);

        while (elapsed < duration)
        {
            // Si una cinemática reclama la cámara a mitad del viaje, se le deja (INC-397).
            if (CameraDirectorService.HasOwner) { _cameraCoroutine = null; yield break; }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _mainCamera.transform.position = Vector3.Lerp(fromPos, toPos, t);
            _mainCamera.transform.rotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }

        _mainCamera.transform.position = toPos;
        _mainCamera.transform.rotation = toRot;
        _cameraCoroutine = null;
    }

    // ── Despertar subjetivo (INC-424) ─────────────────────────────────────────────────────────
    //
    // «La transición a despertar a Will no termina de convencerme» → opción B, «el sueño se
    // deshace». El prólogo acaba en BLANCO (el escudo llena la pantalla); ese blanco se funde
    // en la luz de la mañana y lo que se ve es el techo del cuarto, desde los ojos de Will, con
    // los párpados todavía pesados. Dos parpadeos. El botón abre los ojos del todo; al incorporarse
    // cierra los ojos y los abre ya de pie, en la cámara de juego (INC-434: sin barrido de cámara).

    private bool UsaDespertarSubjetivo => despertarSubjetivo && sleepOnStart && isSleeping;

    private bool _subjetivoEmpezado;
    private bool _vioUnaSecuencia;
    private bool _abriendoLosOjos;
    private bool _cerrandoParaLevantarse;
    private Coroutine _parpadeos;
    private float _incorporado;                 // 0 = tumbado, 1 = sentado (lo lee LateUpdate)
    private const string LoopLluviaDespertar = "Despertar_Lluvia";
    private Coroutine _tormenta;
    private bool _hudOcultado;
    private float _apertura = 0.25f;           // 0 = ojos cerrados, 1 = abiertos del todo
    private GameObject _parpados;
    private RectTransform _parpadoArriba, _parpadoAbajo;
    private Renderer[] _rendersOcultos;

    /// Dónde están los ojos de Will tumbado y hacia dónde mira. Duerme DE LADO, mirando a la
    /// pared (INC-426): «es mejor que lo que enfoquemos sea la pared hacia la que mira». La cara se
    /// saca del propio esqueleto — hombros y columna — así que vale para cualquier postura de
    /// dormir: adelante = hombro izquierdo→derecho × cadera→cabeza.
    private bool PoseSubjetiva(out Vector3 pos, out Quaternion rot, bool respirando, float incorporado = 0f)
    {
        pos = default; rot = default;
        if (playerAnimator == null || !playerAnimator.isHuman) return false;
        Transform cabeza = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
        Transform cadera = playerAnimator.GetBoneTransform(HumanBodyBones.Hips);
        Transform hIzq = playerAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform hDer = playerAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (cabeza == null || cadera == null || hIzq == null || hDer == null) return false;

        Vector3 ojos = cabeza.position;
        Vector3 arribaDelCuerpo = (cabeza.position - cadera.position).normalized;
        Vector3 cara = Vector3.Cross(hDer.position - hIzq.position, arribaDelCuerpo).normalized;
        if (cara.sqrMagnitude < 0.5f) return false;

        // Tumbado: justo delante de la cara, mirando hacia donde mira él (la pared), con el
        // horizonte algo ladeado —está tumbado— pero sin llegar a los 90°, que marean.
        // En prologo20 la cara del esqueleto apuntaba bastante hacia arriba y el plano salía
        // mirando a la esquina del techo, ladeado unos 30° (INC-434): la mirada se aplana hacia
        // la horizontal y el ladeo baja a la mitad.
        Vector3 caraPlana = new Vector3(cara.x, 0f, cara.z);
        Vector3 mirada = caraPlana.sqrMagnitude > 0.01f
            ? Vector3.Slerp(caraPlana.normalized, cara, 0.2f).normalized
            : cara;
        Vector3 arribaTumbado = Vector3.Slerp(Vector3.up, arribaDelCuerpo, 0.15f);
        Vector3 posTumbado = ojos + cara * 0.12f;
        Quaternion rotTumbado = Quaternion.LookRotation(mirada, arribaTumbado);

        // Incorporado: sentado, la cabeza más alta, se vuelve hacia el cuarto (de espaldas a la
        // pared) y mira un pelo hacia abajo.
        Vector3 cuarto = -new Vector3(cara.x, 0f, cara.z);
        if (cuarto.sqrMagnitude < 0.0001f) cuarto = Vector3.forward;
        cuarto.Normalize();
        Vector3 posSentado = new Vector3(ojos.x, ojos.y + 0.45f, ojos.z) + cuarto * 0.2f;
        Quaternion rotSentado = Quaternion.LookRotation((cuarto - Vector3.up * 0.15f).normalized, Vector3.up);

        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(incorporado));
        pos = Vector3.Lerp(posTumbado, posSentado, k);
        rot = Quaternion.Slerp(rotTumbado, rotSentado, k);

        if (respirando)
            pos += Vector3.up * (Mathf.Sin(Time.time * 1.6f) * 0.006f);
        return true;
    }

    /// Arranca los párpados la primera vez que la cámara es suya: al acabar el prólogo, con la
    /// pantalla todavía en blanco.
    private void EmpezarDespertarSubjetivoSiToca()
    {
        if (_subjetivoEmpezado) return;
        if (CinematicSequencerBase.AnySequenceActive) return;
        _subjetivoEmpezado = true;

        // Will no se ve desde dentro de sus propios ojos: el pelo taparía el techo.
        _rendersOcultos = player != null ? player.GetComponentsInChildren<Renderer>(false) : null;
        if (_rendersOcultos != null) foreach (var r in _rendersOcultos) if (r != null) r.forceRenderingOff = true;

        // Nada de HUD mientras se despierta: aparece cuando ya está de pie.
        if (Sendero.UI.PlayerHUDV2.Instance != null) { Sendero.UI.PlayerHUDV2.Instance.HideHUD(0.05f); _hudOcultado = true; }

        CrearParpados();
        _parpadeos = StartCoroutine(Co_Parpadeos());
        _tormenta = StartCoroutine(Co_TormentaAlDespertar());
    }

    private IEnumerator Co_Parpadeos()
    {
        PonerApertura(0.25f);

        // Que el blanco se vaya fundiendo antes del primer parpadeo.
        float tope = Time.unscaledTime + 6f;
        while (Sendero.Core.Feedback.FeedbackService.IsScreenFaded && Time.unscaledTime < tope) yield return null;
        yield return new WaitForSecondsRealtime(1.0f);

        if (_abriendoLosOjos) yield break;
        yield return Parpado(0.55f, 0.8f);
        yield return new WaitForSecondsRealtime(0.4f);
        if (_abriendoLosOjos) yield break;
        yield return Parpado(0f, 0.12f);                       // primer parpadeo
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Parpado(0.7f, 0.35f);
        yield return new WaitForSecondsRealtime(0.6f);
        if (_abriendoLosOjos) yield break;
        yield return Parpado(0f, 0.1f);                        // segundo parpadeo
        yield return new WaitForSecondsRealtime(0.12f);
        yield return Parpado(0.45f, 0.45f);                    // y se quedan pesados
    }

    /// El botón: abre los ojos del todo, se queda un momento mirando la pared, y al incorporarse
    /// cierra los ojos (un parpadeo largo) y los abre ya de pie, en la cámara de juego.
    ///
    /// Antes la cámara se incorporaba con él y giraba media vuelta hacia el cuarto en 1,3 s. En
    /// prologo20 eso se veía como la cámara barriendo la habitación ladeada, pegada a la pared y a
    /// la ventana, y después el corte a la cámara de juego — «algo pasa con la cámara» (INC-434).
    /// Con el parpadeo no hay ningún movimiento de cámara que ver: tumbado → negro → de pie.
    private IEnumerator Co_AbrirLosOjos()
    {
        if (_parpadeos != null) { StopCoroutine(_parpadeos); _parpadeos = null; }
        _incorporado = 0f;

        yield return Parpado(1f, 0.45f);                       // abre los ojos del todo
        yield return new WaitForSecondsRealtime(0.7f);          // la pared, un momento

        _cerrandoParaLevantarse = true;                         // este cierre sí se deja hacer
        yield return Parpado(0f, 0.35f);                        // se incorpora con los ojos cerrados
        yield return Sendero.Core.Feedback.FeedbackService.ScreenFadeAsync(Color.black, 0.12f, true);

        WakeUp();
        // La cámara de juego necesita unos fotogramas para colocarse detrás de él; que lo haga
        // con la pantalla en negro.
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        yield return Sendero.Core.Feedback.FeedbackService.ScreenFadeAsync(Color.black, 0.6f, false);
    }

    /// La tormenta del sueño todavía se oye al abrir los ojos (INC-427): lluvia contra la ventana,
    /// un relámpago que ilumina el cuarto y su trueno, y otro más lejos. Al levantarse, la lluvia
    /// se va apagando: era el sueño.
    private IEnumerator Co_TormentaAlDespertar()
    {
        var audio = AudioService.Instance;
        audio?.PlayLoopingSFX(LoopLluviaDespertar, "rain", 0.22f);   // desde dentro, contra la ventana (INC-433)

        yield return new WaitForSecondsRealtime(2.2f);
        Relampago(0.5f);
        yield return new WaitForSecondsRealtime(0.08f);
        Relampago(0.3f);
        yield return new WaitForSecondsRealtime(0.9f);
        audio?.PlaySFX("Prologo_Trueno", 0.8f);

        yield return new WaitForSecondsRealtime(6.5f);
        Relampago(0.25f);
        yield return new WaitForSecondsRealtime(1.8f);
        audio?.PlaySFX("Weather_Thunder", 0.6f);
        _tormenta = null;
    }

    private static void Relampago(float fuerza)
        => Sendero.Core.Feedback.FeedbackService.ScreenFlash(new Color(0.82f, 0.88f, 1f, fuerza), 0.14f);

    private IEnumerator Parpado(float hasta, float segundos)
    {
        float desde = _apertura, t = 0f;
        while (t < segundos)
        {
            // Si ya se está despertando, los parpadeos del sueño no le cierran los ojos; el cierre
            // de levantarse (Co_AbrirLosOjos) sí.
            if (_abriendoLosOjos && !_cerrandoParaLevantarse && hasta < _apertura) yield break;
            t += Time.unscaledDeltaTime;
            PonerApertura(Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / segundos)));
            yield return null;
        }
        PonerApertura(hasta);
    }

    private void CrearParpados()
    {
        if (_parpados != null) return;
        _parpados = new GameObject("[Parpados de Will]");
        var canvas = _parpados.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        _parpados.AddComponent<UnityEngine.UI.CanvasScaler>();

        // Borde difuminado: el párpado no es una persiana.
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float a = Mathf.SmoothStep(0f, 1f, y / 63f);
            tex.SetPixel(0, y, new Color(0.02f, 0.01f, 0.02f, a));
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));

        _parpadoArriba = CrearParpado("Arriba", sprite, arriba: true);
        _parpadoAbajo = CrearParpado("Abajo", sprite, arriba: false);
    }

    private RectTransform CrearParpado(string nombre, Sprite sprite, bool arriba)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(_parpados.transform, false);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, arriba ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, arriba ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, arriba ? 1f : 0f);
        // El degradado va de transparente (y=0) a opaco (y=1): el de abajo se da la vuelta.
        rt.localScale = new Vector3(1f, arriba ? 1f : -1f, 1f);
        if (!arriba) rt.pivot = new Vector2(0.5f, 1f);
        return rt;
    }

    private void PonerApertura(float a)
    {
        _apertura = Mathf.Clamp01(a);
        if (_parpadoArriba == null) return;
        float alto = ((RectTransform)_parpados.transform).rect.height;
        if (alto <= 1f) alto = Screen.height;
        float h = (1f - _apertura) * (alto * 0.5f + alto * 0.08f);   // +8 %: el borde suave se solapa al cerrar
        _parpadoArriba.sizeDelta = new Vector2(0f, h);
        _parpadoAbajo.sizeDelta = new Vector2(0f, h);
    }

    private void TerminarDespertarSubjetivo()
    {
        if (_tormenta != null) { StopCoroutine(_tormenta); _tormenta = null; }
        if (_subjetivoEmpezado) AudioService.Instance?.StopLoopingSFX(LoopLluviaDespertar, 5f);
        if (_parpados != null) Destroy(_parpados);
        _parpados = null;
        if (_rendersOcultos != null) foreach (var r in _rendersOcultos) if (r != null) r.forceRenderingOff = false;
        _rendersOcultos = null;
        if (_hudOcultado && Sendero.UI.PlayerHUDV2.Instance != null) Sendero.UI.PlayerHUDV2.Instance.ShowHUD();
        _hudOcultado = false;
    }
}
