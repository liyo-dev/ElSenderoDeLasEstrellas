using UnityEngine;

namespace Invector.vCharacterController
{
    /// <summary>
    /// Entrada de movimiento, cámara, sprint y salto del jugador. La lee de
    /// <see cref="CharacterControllerBridge.Input"/>, que el juego registra al arrancar con la
    /// supresión de menús y la inversión de cámara ya aplicadas. La magia no pasa por aquí: la lee
    /// el propio juego (PlayerCombatInput).
    /// </summary>
    public class vThirdPersonInput : MonoBehaviour
    {
        [SerializeField, Tooltip("Referencia opcional que implementa IActionValidator (p. ej. PlayerActionManager).")]
        private MonoBehaviour actionValidatorSource;

        private IActionValidator actionValidator;

        [SerializeField, Min(0f), Tooltip("Segundos que se guarda una pulsación de salto que llega justo antes de tocar suelo. Pasado este tiempo se descarta: una pulsación en el aire no puede saltar sola al aterrizar.")]
        private float jumpBufferSeconds = 0.15f;

        [HideInInspector] public vThirdPersonController cc;
        [HideInInspector] public vThirdPersonCamera tpCamera;
        [HideInInspector] public Camera cameraMain;

        /// <summary>Bloquea la rotación vertical de la cámara (vuelo).</summary>
        public bool DisableVerticalCameraRotation { get; set; } = false;

        /// <summary>
        /// Suprime movimiento, sprint y salto pero mantiene la rotación de cámara. Lo usan, entre
        /// otros, PartyControlManager al poseer a un compañero y PlayerAmbientActivityHandler con
        /// el jugador sentado o tumbado.
        /// </summary>
        public bool SuppressMoveInput { get; set; } = false;

        private Vector2 moveInput;
        private Vector2 cameraInput;
        private bool jumpPressed;
        private float jumpPressedAt;
        private bool sprintHeld;

        protected virtual void Awake()
        {
            ResolveServices();
        }

        private void ResolveServices()
        {
            if (actionValidator != null) return;
            if (actionValidatorSource != null)
                actionValidator = actionValidatorSource as IActionValidator;
            if (actionValidator == null)
                actionValidator = GetComponent<IActionValidator>();
        }

        protected virtual void Start()
        {
            InitilizeController();
            InitializeTpCamera();
        }

        protected virtual void Update()
        {
            ReadInput();
            InputHandle();

            // El motor corre en Update, a la misma cadencia que la cámara (LateUpdate): en
            // FixedUpdate el jugador avanzaba a saltos respecto a la cámara. Sus cálculos ya usan
            // Time.deltaTime.
            //
            // Con SuppressMoveInput no se actualizan motor ni Animator: con el CharacterController
            // apagado (sentado, durmiendo) el ground-check concluiría «en el aire» y empujaría
            // IsGrounded=false al Animator antes de que nadie pueda corregirlo en LateUpdate.
            // La cámara sí sigue funcionando (CameraInput, dentro de InputHandle).
            if (!SuppressMoveInput)
            {
                cc.UpdateMotor();
                cc.ControlLocomotionType();
                cc.ControlRotationType();
                cc.AirVelocity();

                cc.UpdateAnimator();
            }
        }

        private void ReadInput()
        {
            var source = CharacterControllerBridge.Input;
            if (source == null)
            {
                moveInput = Vector2.zero;
                cameraInput = Vector2.zero;
                sprintHeld = false;
                return;
            }

            moveInput = source.Move;
            cameraInput = source.CameraLook;
            sprintHeld = CanSprint() && source.SprintHeld;
            if (CanJump() && source.JumpPressed)
            {
                jumpPressed = true;
                jumpPressedAt = Time.time;
            }
        }

        public virtual void OnAnimatorMove()
        {
            cc.ControlAnimatorRootMotion();
        }

        protected virtual void InitilizeController()
        {
            cc = GetComponent<vThirdPersonController>();
            if (cc != null) cc.Init();
        }

        protected virtual void InitializeTpCamera()
        {
            if (tpCamera == null)
            {
                tpCamera = FindAnyObjectByType<vThirdPersonCamera>();
                if (tpCamera == null) return;

                tpCamera.SetMainTarget(this.transform);
                tpCamera.Init();
            }
        }

        protected virtual void InputHandle()
        {
            MoveInput();
            CameraInput();
            SprintInput();
            JumpInput();
        }

        public virtual void MoveInput()
        {
            // Durante un ataque o lanzamiento (cc.IsActionCommitted) el stick no mueve. Ver INC-484.
            if (SuppressMoveInput || cc.IsActionCommitted) { cc.input.x = 0; cc.input.z = 0; return; }
            cc.input.x = moveInput.x;
            cc.input.z = moveInput.y;
        }

        protected virtual void CameraInput()
        {
            if (!cameraMain)
            {
                if (!Camera.main) Debug.Log("Missing a Camera with the tag MainCamera, please add one.");
                else
                {
                    cameraMain = Camera.main;
                    cc.rotateTarget = cameraMain.transform;
                }
            }

            if (cameraMain) cc.UpdateMoveDirection(cameraMain.transform);
            if (tpCamera == null) return;

            float y = DisableVerticalCameraRotation ? 0f : cameraInput.y;
            tpCamera.RotateCamera(cameraInput.x, y);
        }

        protected virtual void SprintInput() => cc.Sprint(SuppressMoveInput ? false : sprintHeld);

        protected virtual bool JumpConditions()
        {
            return cc.isGrounded && cc.GroundAngle() < cc.slopeLimit && !cc.isJumping && !cc.stopMove;
        }

        protected virtual void JumpInput()
        {
            // El salto pendiente caduca: sin esto, pulsar en el aire (doble toque para volar, o
            // mientras se lanza sostenido) dejaba un salto guardado que saltaba solo al aterrizar.
            if (jumpPressed && Time.time - jumpPressedAt > jumpBufferSeconds)
                jumpPressed = false;

            if (SuppressMoveInput || !jumpPressed) return;

            if (JumpConditions())
            {
                cc.Jump();
                jumpPressed = false;
            }
            else if (!cc.isGrounded)
            {
                // En el aire: doble salto o, sin saltos extra, el aviso para volar.
                cc.JumpInAir();
                jumpPressed = false;
            }
        }

        private bool CanJump()   => actionValidator?.CanJump() ?? true;
        private bool CanSprint() => actionValidator?.CanSprint() ?? true;

        /// <summary>
        /// Cancela el salto pendiente en cola. Se llama al salir del vuelo para que el salto que
        /// lo activó no se ejecute solo al aterrizar.
        /// </summary>
        public void CancelPendingJump() => jumpPressed = false;
    }
}
