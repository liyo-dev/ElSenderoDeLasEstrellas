using Core;
using Invector.vCharacterController;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla el modo de vuelo del jugador (tipo Dragon Ball): se entra pulsando saltar en el aire
/// después del doble salto (tercer toque, vThirdPersonController.OnJumpPressedWithoutAirJumps),
/// el joystick izquierdo dirige y saltar de nuevo sale del vuelo. Al entrar, un pequeño impulso
/// hacia arriba con voltereta y efecto antes de echar a volar (INC-665).
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerFlyingController : MonoBehaviour
{
    private const float GroundCheckBuffer = 0.2f;

    // FIX (1 sep 2026) — "la animación de vuelo se quita y se pone": IsGrounded() usa un
    // Physics.CheckCapsule con un buffer pequeño (GroundCheckBuffer) contra el propio collider
    // del jugador. Volar a baja altura o rozar el terreno es parte normal del mecanismo (picados,
    // vuelo rasante) — bastaba un único frame de solape para que Update() llamara a ExitFlight()
    // de inmediato: eso desactiva el controller, restaura la gravedad y hace
    // PlayerActionManager.PopMode(ActionMode.Flying), así que el jugador empieza a caer aunque
    // siga con la intención de volar, y vuelve a entrar en vuelo al reintentar — visible como que
    // la animación se activa y desactiva en bucle. Igual que el resto del proyecto exige contacto
    // sostenido antes de aceptar una transición de movimiento (ver
    // FollowPlayerState._isFollowingSticky), exigimos que IsGrounded() se mantenga true durante
    // este margen antes de aceptarlo como aterrizaje real.
    private const float ExitFlightGroundedGrace = 0.12f;

    [Header("Animación")]
    [SerializeField] private int locomotionLayerIndex = 0;
    [SerializeField] private string flyIdleState = "fly_idle";
    [SerializeField] private string flyMoveState = "fly_move";
    [SerializeField] private string flyDiveState = "fly_dive";
    [SerializeField] private string flyLandingState = "Landing";
    [SerializeField] private float landingCrossfade = 0.08f;
    [SerializeField] private float hardLandingSpeed = -6f;
    [SerializeField] private string locomotionStateName = "Free Locomotion";
    [Tooltip("Pose mientras se lanza un hechizo en vuelo (el movimiento no cambia, solo la animación). INC-492.")]
    [SerializeField] private string castPoseState = "Falling";
    [SerializeField] private float castPoseCrossfade = 0.1f;
    [Tooltip("Tras disparar, la pose de caída se mantiene hasta que se avanza y han pasado estos segundos sin disparar; quieto en el aire se queda en caída (Raúl, INC-492).")]
    [SerializeField] private float castPoseRelease = 1f;

    [Header("Movimiento")]
    [SerializeField] private float horizontalSpeed = 12f;
    [SerializeField] private float verticalSpeed = 8f;
    [SerializeField] private float descendSpeed = 12f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float turnSpeed = 9f;
    [SerializeField] private float tiltAngleMultiplier = 15f;
    [SerializeField] private float tiltSpeed = 5f;
    [SerializeField] private float boostMultiplier = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Animator _animator;
    private MagicCaster _magicCaster;
    private bool _inCastPose;
    private bool _castPosePrevGrounded;
    private float _lastCastTime = -999f;
    private float _castPoseBlendUntil;
    private Rigidbody _rigidbody;
    private vThirdPersonController _controller;
    private VolteretaDelJugador _voltereta;
    private Coroutine _entrada;
    private Invector.vCharacterController.vThirdPersonInput _inputController;
    private FieldInfo _lockMovementField;
    private FieldInfo _lockRotationField;
    private FieldInfo _isJumpingField;
    private FieldInfo _jumpCounterField;
    private PlayerActionManager _actionManager;
    private Core.PlayerInputManager _inputManager;
    private CapsuleCollider _capsule;
    private Transform _cameraTransform;
    private PlayerControls _controls;
    private InputAction _moveAction;
    private InputAction _cameraAction;
    private InputAction _jumpAction;
    private bool _ownsControls;

    private Vector2 _moveInput;
    private Vector2 _cameraInput;
    private bool _jumpHeld;
    private bool _isFlying;
    private bool _extraGravitySuspended;
    private float _cachedExtraGravity;
    private bool _gravityStored;
    private bool _storedUseGravity;
    private float _currentPlanarSpeed;
    private bool _isBoosting;
    private bool _controllerWasEnabled = true;
    private bool _animRootMotionPrev;
    private float _prevFlightLayerWeight = -1f;
    private int _isFlyingHash = -1;
    private bool _justEnteredFlight;
    private float _currentPitch = 0f;
    private float _groundedWhileFlyingTimer;
    private bool _isPhysicsBobbingIdle;

    [Header("Entrada al vuelo (INC-665)")]
    [Tooltip("Al entrar en vuelo, pequeño impulso hacia arriba con voltereta (VolteretaDelJugador) y efecto antes de volar.")]
    [SerializeField] private bool volteretaAlEntrar = true;
    [Tooltip("Velocidad (m/s) del impulso hacia arriba al entrar.")]
    [SerializeField, Min(0f)] private float impulsoDeEntrada = 6f;
    [Tooltip("Segundos máximos de voltereta antes de echar a volar.")]
    [SerializeField, Min(0.1f)] private float esperaMaximaDeEntrada = 0.7f;
    [Tooltip("Efecto de un solo uso al entrar (pool de VFX). Vacío = nada.")]
    [SerializeField] private GameObject vfxDeEntrada;
    [SerializeField, Min(0.1f)] private float duracionVfxDeEntrada = 2f;
    [Tooltip("Clave de SFX al entrar. Vacío = sin sonido.")]
    [SerializeField] private string sfxDeEntrada = "";

    [Header("FX Vuelo")]
    [SerializeField] private Transform vfxAttach;
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private float trailSpeedThreshold = 1.5f;
    [SerializeField] private GameObject boostVfxPrefab;
    [SerializeField] private GameObject landingVfxPrefab;
     [Header("Vuelo - Movimiento visual")]
     [Tooltip("Amplitud (metros) del movimiento vertical oscilante mientras el jugador está en vuelo.")]
     [SerializeField] private float flightBobAmplitude = 0.8f;
     [Tooltip("Frecuencia (Hz) del movimiento oscilante mientras el jugador está en vuelo.")]
     [SerializeField] private float flightBobFrequency = 1.8f;
     [Tooltip("Transform raíz que contiene la malla/visual del personaje. Si se asigna, el bobbing se aplicará a la visual.")]
     [SerializeField] private Transform visualRoot;
     private float _visualRootBaseY;
     private bool _visualRootHasBase = false;
     [Header("Vuelo - Partículas de pies")]
    [Tooltip("Prefab (ParticleSystem) que se instanciará en los pies mientras el jugador vuela.")]
    [SerializeField] private GameObject footVfxPrefab;
    [Tooltip("Transform donde colocar las partículas de pies. Si es null, se usan las coordenadas del jugador.")]
    [SerializeField] private Transform feetAttach;

    private GameObject _trailInstance;
    private ParticleSystem _trailPs;
    private GameObject _boostInstance;
    private ParticleSystem _boostPs;
    // Instancia y sistema de partículas de los pies
    private GameObject _footVfxInstance;
    private ParticleSystem _footVfxPs;
    // Estado interno para evitar llamadas repetidas
    private bool _footVfxActive = false;

    public bool IsFlying => _isFlying;
    public bool IsBoosting => _isBoosting;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
        _controller = GetComponent<vThirdPersonController>();
        _voltereta = GetComponent<VolteretaDelJugador>();
        _inputController = GetComponent<Invector.vCharacterController.vThirdPersonInput>();
        _actionManager = GetComponent<PlayerActionManager>();
        _magicCaster = GetComponentInChildren<MagicCaster>(true) ?? GetComponentInParent<MagicCaster>();
        _inputManager = ServiceLocator.Get<Core.PlayerInputManager>(logIfMissing: false);
        _capsule = GetComponent<CapsuleCollider>() ?? GetComponentInChildren<CapsuleCollider>();
        CacheControllerLockFields();
        CacheCameraTransform();

        if (_inputManager != null && _inputManager.Controls != null)
        {
            _controls = _inputManager.Controls;
            _ownsControls = false;
        }
        else
        {
            _controls = new PlayerControls();
            _ownsControls = true;
        }

        _moveAction = _controls.GamePlay.Move;
        _cameraAction = _controls.GamePlay.CameraLook;
        _jumpAction = _controls.GamePlay.Jump;

        // Try to auto-detect the animator layer that contains the flight states
        DetectFlightLayer();

        // cache isFlying parameter hash if present
        if (_animator != null)
        {
            try { _isFlyingHash = Animator.StringToHash("isFlying"); }
            catch { _isFlyingHash = -1; }
        }
    }

    private void DetectFlightLayer()
    {
        if (_animator == null) return;

        string[] statesToFind = new string[] { flyIdleState, flyMoveState, flyDiveState };
        for (int layer = 0; layer < _animator.layerCount; layer++)
        {
            foreach (var s in statesToFind)
            {
                if (string.IsNullOrEmpty(s)) continue;
                int hash = Animator.StringToHash(s);
                try
                {
                    if (_animator.HasState(layer, hash))
                    {
                        locomotionLayerIndex = layer;
                        if (debugLogs)
                        {
                            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                            Debug.Log($"[PlayerFlyingController] Detected flight state '{s}' on animator layer {layer}. Using that layer for flight animations.");
                            #endif
                        }
                        return;
                    }
                }
                catch { }
            }
        }
    }

    void OnEnable()
    {
        if (_ownsControls)
            _controls?.Enable();
        if (_jumpAction != null)
        {
            _jumpAction.performed += OnJumpPerformed;
            _jumpAction.canceled += OnJumpCanceled;
        }
        if (_controller != null)
            _controller.OnJumpPressedWithoutAirJumps += OnJumpAfterDoubleJump;
        if (_moveAction != null)
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
        }
        if (_cameraAction != null)
        {
            _cameraAction.performed += OnCameraPerformed;
            _cameraAction.canceled += OnCameraCanceled;
        }
    }

    void OnDisable()
    {
        if (_entrada != null) { StopCoroutine(_entrada); _entrada = null; }
        if (_isFlying)
            ExitFlight(force: true);
        StopFlightVfx();

        if (_jumpAction != null)
        {
            _jumpAction.performed -= OnJumpPerformed;
            _jumpAction.canceled -= OnJumpCanceled;
        }
        if (_controller != null)
            _controller.OnJumpPressedWithoutAirJumps -= OnJumpAfterDoubleJump;
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
        }
        if (_cameraAction != null)
        {
            _cameraAction.performed -= OnCameraPerformed;
            _cameraAction.canceled -= OnCameraCanceled;
        }

        if (_ownsControls)
            _controls?.Disable();
    }

    void Update()
    {
        if (_isFlying)
        {
            if (IsGrounded())
            {
                _groundedWhileFlyingTimer += Time.deltaTime;
                if (_groundedWhileFlyingTimer >= ExitFlightGroundedGrace)
                {
                    ExitFlight();
                    return;
                }
            }
            else
            {
                _groundedWhileFlyingTimer = 0f;
            }
            CacheCameraTransform();
            UpdateFlightAnimation();
        }
    }

     void FixedUpdate()
     {
         if (_isFlying)
             ApplyFlightMovement();
     }

     void LateUpdate()
     {
         // Aplicar bobbing visual si visualRoot existe y estamos volando
         if (visualRoot == null || !_isFlying)
             return;

         // Capturar la posición base la primera vez
         if (!_visualRootHasBase)
         {
             _visualRootBaseY = visualRoot.localPosition.y;
             _visualRootHasBase = true;
         }

         if (Mathf.Abs(flightBobAmplitude) > 0.0001f && Mathf.Abs(flightBobFrequency) > 0.0001f)
         {
             float bob = Mathf.Sin(Time.time * (2f * Mathf.PI * flightBobFrequency)) * flightBobAmplitude;
             Vector3 lp = visualRoot.localPosition;
             lp.y = _visualRootBaseY + bob;
             visualRoot.localPosition = lp;
         }
         else if (_visualRootHasBase)
         {
             // Si no hay movimiento, restablecer a la posición base
             Vector3 lp = visualRoot.localPosition;
             if (Mathf.Abs(lp.y - _visualRootBaseY) > 0.0001f)
             {
                 lp.y = _visualRootBaseY;
                 visualRoot.localPosition = lp;
             }
         }
     }

     private void OnMovePerformed(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => _moveInput = Vector2.zero;
    private void OnCameraPerformed(InputAction.CallbackContext ctx) => _cameraInput = PlayerSettings.ApplyLookInversion(ctx.ReadValue<Vector2>(), true);
    private void OnCameraCanceled(InputAction.CallbackContext ctx) => _cameraInput = Vector2.zero;

    // Saltar volando sale del vuelo. Entrar lo decide OnJumpAfterDoubleJump.
    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (_inputManager != null && !_inputManager.CanProcess(PlayerAbility.Jump))
            return;

        _jumpHeld = true;

        if (_isFlying)
            ExitFlight();
    }

    // Tercer toque de salto: en el aire y sin saltos extra (ya se hizo el doble salto).
    private void OnJumpAfterDoubleJump()
    {
        if (_isFlying || _entrada != null || !CanEnterFlight()) return;
        if (volteretaAlEntrar && _voltereta != null && _controller != null && _controller.enabled)
            _entrada = StartCoroutine(Co_EntrarConVoltereta());
        else
            EnterFlight();
    }

    /// Impulso hacia arriba, efecto y voltereta; al acabar la voltereta (o tras
    /// esperaMaximaDeEntrada) echa a volar si sigue pudiendo.
    private IEnumerator Co_EntrarConVoltereta()
    {
        _controller.Impulsar(impulsoDeEntrada, Vector3.zero, devolverSaltosEnElAire: false);

        Transform punto = vfxAttach != null ? vfxAttach : transform;
        if (vfxDeEntrada != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxDeEntrada, punto.position, Quaternion.identity, duracionVfxDeEntrada);
        if (!string.IsNullOrEmpty(sfxDeEntrada) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxDeEntrada, 1f, punto.position);

        bool volteando = _voltereta.EnElAire();
        float tope = Time.time + esperaMaximaDeEntrada;
        while (volteando && _voltereta.EnCurso && Time.time < tope) yield return null;

        _entrada = null;
        if (!_isFlying && CanEnterFlight()) EnterFlight();
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx) => _jumpHeld = false;

    private bool CanEnterFlight()
    {
        if (_actionManager != null && !_actionManager.CanFly())
            return false;
        if (IsGrounded())
            return false;
        if (_actionManager != null && _actionManager.IsInMode(ActionMode.Swimming))
            return false;
        return true;
    }

     private void EnterFlight()
     {
         if (_isFlying)
             return;

         _isFlying = true;
         _groundedWhileFlyingTimer = 0f;
         _inCastPose = false;
         _castPoseBlendUntil = 0f;
         _visualRootHasBase = false; // Resetear bobbing visual para que capture la posición base
        _isBoosting = false;
        _justEnteredFlight = true;
        _jumpHeld = false; // evitar que el primer frame se considere dive por mantener salto
        if (_inputController != null)
            _inputController.CancelPendingJump();
        GamepadInputReader.IgnoreJumpButton(0.3f);
        if (_controller != null)
            _controller.suppressAirMovement = true;
        if (_actionManager != null)
            _actionManager.PushMode(ActionMode.Flying);
        if (_inputController != null)
            _inputController.DisableVerticalCameraRotation = true;
        if (_controller != null)
        {
            _controllerWasEnabled = _controller.enabled;
            _controller.enabled = false; // evitar que el controlador base fuerce animaciones de caída
        }
        if (_animator != null)
        {
            _animRootMotionPrev = _animator.applyRootMotion;
            _animator.applyRootMotion = false; // controlamos el movimiento vía Rigidbody
        }

        CacheCameraTransform();
        PlayFlightState(flyIdleState);
        SpawnTrailIfNeeded();
        SpawnFootVfxIfNeeded();

        // Ensure flight animator layer has full weight so flight states take precedence
        if (_animator != null && locomotionLayerIndex >= 0 && locomotionLayerIndex < _animator.layerCount)
        {
            try
            {
                _prevFlightLayerWeight = _animator.GetLayerWeight(locomotionLayerIndex);
                _animator.SetLayerWeight(locomotionLayerIndex, 1f);
            }
            catch { }
        }

        if (_rigidbody != null)
        {
            if (!_gravityStored)
            {
                _storedUseGravity = _rigidbody.useGravity;
                _gravityStored = true;
            }
            var vel = _rigidbody.linearVelocity;
            if (vel.y < 0f) vel.y = 0f;
            _rigidbody.linearVelocity = vel;
            _rigidbody.useGravity = false;
        }

        if (_controller != null)
        {
            SetControllerLocks(true);
            if (!_extraGravitySuspended)
            {
                _cachedExtraGravity = _controller.extraGravity;
                _controller.extraGravity = 0f;
                _extraGravitySuspended = true;
            }
        }

        // set animator flag
        if (_animator != null && _isFlyingHash != -1)
        {
            try { _animator.SetBool(_isFlyingHash, true); }
            catch { }
        }

        if (debugLogs)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerFlyingController] Enter Flight");
            #endif
        }
    }

    private void ExitFlight(bool force = false)
    {
        if (!_isFlying && !force)
            return;

        bool wasFlying = _isFlying;
        _isFlying = false;
        _inCastPose = false;
        _castPoseBlendUntil = 0f;
        _currentPlanarSpeed = 0f;
        _currentPitch = 0f;
        _isBoosting = false;
        if (_inputController != null)
            _inputController.CancelPendingJump();
        GamepadInputReader.IgnoreJumpButton(0.2f);

        if (wasFlying && _actionManager != null)
            _actionManager.PopMode(ActionMode.Flying);
        if (_inputController != null)
            _inputController.DisableVerticalCameraRotation = false;

        float verticalVel = _rigidbody != null ? _rigidbody.linearVelocity.y : 0f;
        bool hardLanding = verticalVel <= hardLandingSpeed || IsGrounded();
        if (wasFlying && _animator != null)
        {
            if (hardLanding && HasAnimatorState(flyLandingState))
                _animator.CrossFade(flyLandingState, landingCrossfade, locomotionLayerIndex);
            else
                _animator.CrossFade(locomotionStateName, 0.1f, locomotionLayerIndex);
        }
        PlayLandingVfx();
        StopFlightVfx();
        StopFootVfx();

        // Restaurar posición local del visual root si se desplazó por bobbing
        if (visualRoot != null && _visualRootHasBase)
        {
            Vector3 lp = visualRoot.localPosition;
            lp.y = _visualRootBaseY;
            visualRoot.localPosition = lp;
        }
        _visualRootHasBase = false;

        if (_rigidbody != null)
        {
            var vel = _rigidbody.linearVelocity;
            // Presión mínima hacia abajo para clavar el personaje al suelo sin rebotar;
            // si ya cae más rápido (aterrizaje duro) se respeta esa velocidad.
            vel.y = Mathf.Min(vel.y, -0.5f);
            _rigidbody.linearVelocity = vel;
            _rigidbody.angularVelocity = Vector3.zero;
            if (_gravityStored)
                _rigidbody.useGravity = _storedUseGravity;
        }

        if (_controller != null)
        {
            SetControllerLocks(false);
            if (_extraGravitySuspended)
            {
                _controller.extraGravity = _cachedExtraGravity;
                _extraGravitySuspended = false;
            }
            _controller.suppressAirMovement = false;
            _isJumpingField?.SetValue(_controller, false);
            _jumpCounterField?.SetValue(_controller, 0f);
            _controller.enabled = _controllerWasEnabled;
        }

        if (wasFlying && debugLogs)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerFlyingController] Exit Flight");
            #endif
        }

        if (_animator != null)
            _animator.applyRootMotion = _animRootMotionPrev;

        // Restore previous flight layer weight if we changed it
        if (_animator != null && locomotionLayerIndex >= 0 && locomotionLayerIndex < _animator.layerCount)
        {
            try
            {
                if (_prevFlightLayerWeight >= 0f)
                    _animator.SetLayerWeight(locomotionLayerIndex, _prevFlightLayerWeight);
            }
            catch { }
            _prevFlightLayerWeight = -1f;
        }

        // clear animator flag
        if (_animator != null && _isFlyingHash != -1)
        {
            try { _animator.SetBool(_isFlyingHash, false); }
            catch { }
        }
    }

    private void ApplyFlightMovement()
    {
        if (_rigidbody == null)
            return;

        Vector3 forward = GetCameraForwardOnPlane();
        Vector3 right = GetCameraRightOnPlane();

        Vector3 planar = (forward * _moveInput.y) + (right * _moveInput.x);
        if (planar.sqrMagnitude > 1f) planar.Normalize();
        _isBoosting = planar.sqrMagnitude > 0.01f && Mathf.Abs(_moveInput.y) > 0.8f;
        float speedMul = _isBoosting ? boostMultiplier : 1f;
        Vector3 desired = planar * horizontalSpeed * speedMul;

        float verticalInput = 0f;
        // Solo el stick derecho controla altura, y solo mientras avanzas con el izquierdo.
        if (planar.sqrMagnitude > 0.01f && Mathf.Abs(_cameraInput.y) > 0.1f)
            verticalInput += _cameraInput.y * verticalSpeed;

        // Mantener salto para descender siempre (independiente del stick)
        if (_jumpHeld)
            verticalInput -= descendSpeed;

        bool isIdleHover = planar.sqrMagnitude < 0.01f && !_jumpHeld && Mathf.Abs(verticalInput) < 0.1f;

         // Añadir control vertical del jugador
         desired += Vector3.up * verticalInput;

         Vector3 current = _rigidbody.linearVelocity;
        Vector3 target = Vector3.Lerp(current, desired, acceleration * Time.fixedDeltaTime);

        // Bobbing físico: solo cuando está quieto y no hay visualRoot (evita doble efecto)
        _isPhysicsBobbingIdle = isIdleHover && visualRoot == null
            && Mathf.Abs(flightBobAmplitude) > 0.0001f && flightBobFrequency > 0.0001f;
        if (_isPhysicsBobbingIdle)
        {
            float omega = 2f * Mathf.PI * flightBobFrequency;
            target.y = Mathf.Cos(Time.time * omega) * flightBobAmplitude * omega;
        }

        _rigidbody.linearVelocity = target;
        _currentPlanarSpeed = planar.magnitude * horizontalSpeed * speedMul;
        UpdateFlightVfx();
        UpdateFootVfxState();

        Vector3 faceDir = planar;
        if (faceDir.sqrMagnitude < 0.1f && Mathf.Abs(verticalInput) > 0.5f)
            faceDir = forward;
        if (faceDir.sqrMagnitude > 0.0001f)
        {
            // Lógica de inclinación (pitch)
            float maxVertSpeed = Mathf.Max(verticalSpeed, descendSpeed);
            float normalizedVertical = maxVertSpeed > 0.01f ? verticalInput / maxVertSpeed : 0f;
            float targetPitch = -normalizedVertical * tiltAngleMultiplier; // Negativo para que al subir (positivo) incline hacia arriba
            _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, tiltSpeed * Time.fixedDeltaTime);

            Quaternion targetRot = Quaternion.LookRotation(faceDir, Vector3.up);
            // Aplicar la inclinación en el eje X local del personaje
            Quaternion pitchRotation = Quaternion.AngleAxis(_currentPitch, Vector3.right);
            Quaternion finalRot = targetRot * pitchRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, turnSpeed * Time.fixedDeltaTime);
        }
    }

    private void UpdateFlightAnimation()
    {
        if (_animator == null)
            return;

        if (_justEnteredFlight)
        {
            _justEnteredFlight = false;
            PlayFlightState(flyIdleState);
            return;
        }

        // Lanzando un hechizo en vuelo: pose de caída mientras dura el gesto (INC-492). Al acabar,
        // vuelve con un fundido a la animación de vuelo que toque.
        bool castingNow = _magicCaster != null && _magicCaster.IsCasting;
        if (castingNow) _lastCastTime = Time.time;
        bool advancing = _moveInput.sqrMagnitude > 0.01f || _currentPlanarSpeed > 0.5f;
        // Se mantiene la pose de caída mientras dispara y después, hasta que avanza y lleva un rato
        // sin disparar. Quieto en el aire tras disparar sigue en caída.
        bool keepPose = castingNow || (_inCastPose && !(advancing && Time.time - _lastCastTime >= castPoseRelease));
        bool casting = keepPose && HasAnimatorState(castPoseState);
        if (casting)
        {
            // Falling pasa a LandLow si IsGrounded está a true (el modo vuelo lo fuerza a true para que
            // no salten animaciones de caída): mientras dura la pose se mantiene a false.
            int groundedHash = Invector.vCharacterController.vAnimatorParameters.IsGrounded;
            if (!_inCastPose)
            {
                _inCastPose = true;
                _castPosePrevGrounded = _animator.GetBool(groundedHash);
                _animator.CrossFadeInFixedTime(castPoseState, castPoseCrossfade, locomotionLayerIndex);
            }
            _animator.SetBool(groundedHash, false);
            return;
        }
        if (_inCastPose)
        {
            _inCastPose = false;
            _animator.SetBool(Invector.vCharacterController.vAnimatorParameters.IsGrounded, _castPosePrevGrounded);
            string next = ResolveFlightState();
            _animator.CrossFadeInFixedTime(next, castPoseCrossfade, locomotionLayerIndex);
            if (_inputController != null)
                _inputController.DisableVerticalCameraRotation = !string.Equals(next, flyIdleState, StringComparison.Ordinal);
            _castPoseBlendUntil = Time.time + castPoseCrossfade;
            return;
        }
        if (Time.time < _castPoseBlendUntil) return;

        PlayFlightState(ResolveFlightState());
    }

    /// <summary>Estado de vuelo según el movimiento: picado, avance o quieto.</summary>
    private string ResolveFlightState()
    {
        float verticalVel = _rigidbody != null ? _rigidbody.linearVelocity.y : 0f;
        bool diving = (!_isPhysicsBobbingIdle && verticalVel < -0.5f) || _jumpHeld;
        bool stickMoved = _moveInput.sqrMagnitude > 0.01f;
        if (stickMoved || diving) return flyDiveState;

        bool moving = _currentPlanarSpeed > 0.5f;
        bool useMove = moving && !string.IsNullOrEmpty(flyMoveState)
                       && !string.Equals(flyMoveState, flyIdleState, StringComparison.Ordinal)
                       && !string.Equals(flyMoveState, flyDiveState, StringComparison.Ordinal);
        return useMove ? flyMoveState : flyIdleState;
    }

    private void PlayFlightState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || _animator == null)
            return;
        _animator.Play(stateName, locomotionLayerIndex);

        // Allow vertical camera rotation only in fly idle
        bool isIdle = string.Equals(stateName, flyIdleState, StringComparison.Ordinal);
        if (_inputController != null)
            _inputController.DisableVerticalCameraRotation = !isIdle;
    }

    private bool HasAnimatorState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || _animator == null)
            return false;
        try
        {
            int hash = Animator.StringToHash(stateName);
            return _animator.HasState(locomotionLayerIndex, hash);
        }
        catch { return false; }
    }

    private bool IsGrounded()
    {
        if (_capsule == null)
            return Physics.Raycast(transform.position, Vector3.down, 0.6f, LayerMask.GetMask("Default"));

        Vector3 top = transform.TransformPoint(_capsule.center + Vector3.up * (Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius)));
        Vector3 bottom = transform.TransformPoint(_capsule.center - Vector3.up * (Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius)));
        float radius = _capsule.radius * 0.95f;

        return Physics.CheckCapsule(top, bottom, radius + GroundCheckBuffer, _controller != null ? _controller.groundLayer : ~0);
    }

    private void CacheCameraTransform()
    {
        if (_cameraTransform != null)
            return;

    _cameraTransform = Camera.main ? Camera.main.transform : ServiceLocator.Get<Camera>(false)?.transform;
    }

    void SpawnTrailIfNeeded()
    {
        if (_trailInstance != null || trailPrefab == null) return;
        var parent = vfxAttach != null ? vfxAttach : transform;
        _trailInstance = Instantiate(trailPrefab, parent);
        _trailPs = _trailInstance.GetComponent<ParticleSystem>();
        _trailInstance.SetActive(false);
    }

    void UpdateFlightVfx()
    {
        if (_trailInstance != null)
        {
            bool shouldTrail = _isFlying && _currentPlanarSpeed >= trailSpeedThreshold;
            if (_trailInstance.activeSelf != shouldTrail)
                _trailInstance.SetActive(shouldTrail);
            if (shouldTrail && _trailPs != null && !_trailPs.isPlaying) _trailPs.Play();
        }

        if (_isFlying && _isBoosting && boostVfxPrefab != null)
        {
            if (_boostInstance == null)
            {
                var parent = vfxAttach != null ? vfxAttach : transform;
                _boostInstance = Instantiate(boostVfxPrefab, parent);
                _boostPs = _boostInstance.GetComponent<ParticleSystem>();
            }
            if (!_boostInstance.activeSelf) _boostInstance.SetActive(true);
            if (_boostPs != null && !_boostPs.isPlaying) _boostPs.Play();
        }
        else if (_boostInstance != null && _boostInstance.activeSelf)
        {
            if (_boostPs != null) _boostPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _boostInstance.SetActive(false);
        }
    }

    // --- Gestión de VFX de pies ---
    void SpawnFootVfxIfNeeded()
    {
        if (_footVfxInstance != null || footVfxPrefab == null) return;
        var parent = feetAttach != null ? feetAttach : vfxAttach != null ? vfxAttach : transform;
        _footVfxInstance = Instantiate(footVfxPrefab, parent);
        _footVfxPs = _footVfxInstance.GetComponent<ParticleSystem>();
        // Inicialmente desactivadas hasta que el vuelo esté activo
        _footVfxInstance.SetActive(false);
    }

    void UpdateFootVfxState()
    {
        if (_footVfxInstance == null) return;

        bool shouldBeActive = _isFlying;

        if (shouldBeActive && !_footVfxActive)
        {
            _footVfxInstance.SetActive(true);
            if (_footVfxPs != null && !_footVfxPs.isPlaying) _footVfxPs.Play();
            _footVfxActive = true;
        }
        else if (!shouldBeActive && _footVfxActive)
        {
            if (_footVfxPs != null) _footVfxPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _footVfxInstance.SetActive(false);
            _footVfxActive = false;
        }
    }

    void StopFootVfx()
    {
        if (_footVfxInstance == null) return;
        if (_footVfxPs != null) _footVfxPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _footVfxInstance.SetActive(false);
        _footVfxActive = false;
    }

    void PlayLandingVfx()
    {
        if (landingVfxPrefab == null) return;
        var parent = vfxAttach != null ? vfxAttach : transform;
        Instantiate(landingVfxPrefab, parent.position, parent.rotation);
    }

    void StopFlightVfx()
    {
        if (_trailInstance != null)
        {
            if (_trailPs != null) _trailPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _trailInstance.SetActive(false);
        }
        if (_boostInstance != null)
        {
            if (_boostPs != null) _boostPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _boostInstance.SetActive(false);
        }
    }

    private void CacheControllerLockFields()
    {
        if (_controller == null) return;

        var type = _controller.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        _lockMovementField = type.GetField("lockMovement", flags);
        _lockRotationField = type.GetField("lockRotation", flags);
        _isJumpingField = type.GetField("isJumping", flags);
        _jumpCounterField = type.GetField("jumpCounter", flags);
    }

    private void SetControllerLocks(bool value)
    {
        if (_controller == null) return;
        if (_lockMovementField == null || _lockRotationField == null)
            CacheControllerLockFields();

        _lockMovementField?.SetValue(_controller, value);
        _lockRotationField?.SetValue(_controller, value);
    }

    private Vector3 GetCameraForwardOnPlane()
    {
        CacheCameraTransform();
        if (_cameraTransform == null)
            return Vector3.forward;

        Vector3 forward = _cameraTransform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private Vector3 GetCameraRightOnPlane()
    {
        CacheCameraTransform();
        if (_cameraTransform == null)
            return Vector3.right;

        Vector3 right = _cameraTransform.right;
        right.y = 0f;
        return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
    }
}