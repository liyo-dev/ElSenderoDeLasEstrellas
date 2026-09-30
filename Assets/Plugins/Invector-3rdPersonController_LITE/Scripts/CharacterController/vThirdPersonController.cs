using System.Collections;
using UnityEngine;

namespace Invector.vCharacterController
{
    /// <summary>
    /// Controlador de tercera persona: locomoción, salto y la capa de acciones de medio cuerpo
    /// (gestos de lanzar, defender...). No sabe nada de magia: el juego le pide que reproduzca un
    /// estado de la capa superior con <see cref="PlayUpperBodyAction"/>.
    ///
    /// Vive en el ensamblado de Plugins, que compila antes que el juego: no referencia tipos del
    /// juego. Se comunica con él mediante interfaces globales (IActionValidator) y
    /// CharacterControllerBridge.
    /// </summary>
    public class vThirdPersonController : vThirdPersonAnimator
    {
        [Header("Capa de acciones de medio cuerpo")]
        [SerializeField] private int upperLayerIndex = 1;
        [SerializeField, Min(0f)] private float upperLayerFadeOut = 0.22f;

        [Header("Doble salto")]
        [Tooltip("Saltos extra que se pueden dar en el aire antes de volver a tocar suelo.")]
        [SerializeField, Min(0)] private int airJumps = 1;
        [Tooltip("Fuerza del salto en el aire respecto al salto desde el suelo.")]
        [SerializeField, Range(0.3f, 1.5f)] private float airJumpImpulseFactor = 0.9f;

        [Header("SFX")]
        [SerializeField] private AudioClip jumpSfx;
        [SerializeField] private float sfxVolume = 1f;

        /// <summary>
        /// Se pulsa saltar en el aire sin saltos extra disponibles (tras el doble salto).
        /// PlayerFlyingController lo usa para echar a volar.
        /// </summary>
        public event System.Action OnJumpPressedWithoutAirJumps;

        /// <summary>
        /// Se lanza cuando la capa superior termina la acción pedida con PlayUpperBodyAction y su
        /// peso vuelve a cero. PlayerBattleModeController lo usa para volver a la pose de combate.
        /// </summary>
        public System.Action OnUpperBodyActionEnded;

        private Coroutine upperBodyCo;
        private IActionValidator _actionValidator;

        // ========================= Motor base =========================
        public virtual void ControlAnimatorRootMotion()
        {
            if (!this.enabled) return;

            // Con lockMovement no se resincroniza con el root del Animator: quien bloquea (asientos,
            // camas, cinemáticas) coloca al jugador a mano, y este resync lo devolvería a donde
            // estaba antes. El Animator visible vive en el hijo "model"; el de la raíz solo sigue el
            // root motion.
            if (lockMovement) return;

            if (inputSmooth == Vector3.zero)
            {
                transform.position = animator.rootPosition;
                transform.rotation = animator.rootRotation;
            }

            if (useRootMotion) MoveCharacter(moveDirection);
        }

        public virtual void ControlLocomotionType()
        {
            if (lockMovement) return;

            if (locomotionType.Equals(LocomotionType.FreeWithStrafe) && !isStrafing || locomotionType.Equals(LocomotionType.OnlyFree))
            {
                SetControllerMoveSpeed(freeSpeed);
                SetAnimatorMoveSpeed(freeSpeed);
            }
            else if (locomotionType.Equals(LocomotionType.OnlyStrafe) || locomotionType.Equals(LocomotionType.FreeWithStrafe) && isStrafing)
            {
                isStrafing = true;
                SetControllerMoveSpeed(strafeSpeed);
                SetAnimatorMoveSpeed(strafeSpeed);
            }

            if (!useRootMotion) MoveCharacter(moveDirection);
        }

        public virtual void ControlRotationType()
        {
            if (lockRotation) return;
            if (ApplyCommitFacing()) return;

            bool validInput = input != Vector3.zero || (isStrafing ? strafeSpeed.rotateWithCamera : freeSpeed.rotateWithCamera);

            if (validInput)
            {
                inputSmooth = Vector3.Lerp(inputSmooth, input, (isStrafing ? strafeSpeed.movementSmooth : freeSpeed.movementSmooth) * Time.deltaTime);
                Vector3 dir = (isStrafing && (!isSprinting || sprintOnlyFree == false) || (freeSpeed.rotateWithCamera && input == Vector3.zero)) && rotateTarget ? rotateTarget.forward : moveDirection;
                RotateToDirection(dir);
            }
        }

        public virtual void UpdateMoveDirection(Transform referenceTransform = null)
        {
            if (input.magnitude <= 0.01)
            {
                moveDirection = Vector3.Lerp(moveDirection, Vector3.zero, (isStrafing ? strafeSpeed.movementSmooth : freeSpeed.movementSmooth) * Time.deltaTime);
                return;
            }

            if (referenceTransform && !rotateByWorld)
            {
                var right = referenceTransform.right; right.y = 0;
                var forward = Quaternion.AngleAxis(-90, Vector3.up) * right;
                moveDirection = (inputSmooth.x * right) + (inputSmooth.z * forward);
            }
            else
            {
                moveDirection = new Vector3(inputSmooth.x, 0, inputSmooth.z);
            }
        }

        public virtual void Sprint(bool value)
        {
            if (_actionValidator != null && !_actionValidator.CanSprint()) return;

            var sprintConditions = (input.sqrMagnitude > 0.1f && isGrounded &&
                !(isStrafing && !strafeSpeed.walkByDefault && (horizontalSpeed >= 0.5 || horizontalSpeed <= -0.5 || verticalSpeed <= 0.1f)));

            if (value && sprintConditions)
            {
                if (input.sqrMagnitude > 0.1f)
                {
                    if (isGrounded && useContinuousSprint) isSprinting = !isSprinting;
                    else if (!isSprinting) isSprinting = true;
                }
                else if (!useContinuousSprint && isSprinting) isSprinting = false;
            }
            else if (isSprinting) isSprinting = false;
        }

        public virtual void Strafe() => isStrafing = !isStrafing;

        /// <summary>Salto desde el suelo.</summary>
        public virtual void Jump() => DoJump(1f);

        /// <summary>
        /// Pulsación de salto en el aire: doble salto si quedan saltos extra; si no, avisa con
        /// OnJumpPressedWithoutAirJumps (vuelo). Sin efecto con el controlador deshabilitado o en
        /// vuelo (suppressAirMovement).
        /// </summary>
        public void JumpInAir()
        {
            if (!enabled || suppressAirMovement) return;
            if (_actionValidator != null && !_actionValidator.CanJump()) return;

            if (airJumpsUsed < airJumps)
            {
                airJumpsUsed++;
                DoJump(airJumpImpulseFactor);
            }
            else
            {
                OnJumpPressedWithoutAirJumps?.Invoke();
            }
        }

        private void DoJump(float impulseFactor, bool ignoreValidator = false)
        {
            if (!ignoreValidator && _actionValidator != null && !_actionValidator.CanJump()) return;

            // Saltar cancela el compromiso de un ataque o lanzamiento y el sostén en el aire.
            CancelActionCommit();
            CancelAirHold();

            jumpCounter = jumpTimer;
            isJumping = true;

            // Impulso real al despegar para que no se sienta pegado al suelo; el sustain del motor
            // modula la curva del salto en los fotogramas siguientes.
            if (_rigidbody != null)
            {
                var vel = _rigidbody.linearVelocity;
                float actionRPGMinImpulse = useActionRPGJump ? 7.6f : 0f;
                float baseJumpImpulse = Mathf.Max(jumpHeight * jumpTakeoffBoost, minJumpTakeoffSpeed, actionRPGMinImpulse) * impulseFactor;

                // Si llega con velocidad negativa por pendiente o escalón, se limpia antes del despegue.
                if (vel.y < 0f)
                    vel.y = 0f;

                if (vel.y < baseJumpImpulse)
                {
                    vel.y = baseJumpImpulse;
                    if (!float.IsNaN(vel.x) && !float.IsNaN(vel.y) && !float.IsNaN(vel.z) &&
                        !float.IsInfinity(vel.x) && !float.IsInfinity(vel.y) && !float.IsInfinity(vel.z))
                    {
                        _rigidbody.linearVelocity = vel;
                    }
                }
            }

            if (jumpSfx != null)
                CharacterControllerBridge.PlaySfx?.Invoke(jumpSfx, sfxVolume, transform.position);

            if (input.sqrMagnitude < 0.1f) animator.CrossFadeInFixedTime("Jump", 0.05f, 0);
            else                            animator.CrossFadeInFixedTime("JumpMove", 0.07f, 0);
        }

        // ========================= Capa de medio cuerpo =========================

        /// <summary>
        /// Reproduce un estado de la capa superior (ruta completa, p. ej. "UpperBody.Magic.MagicLeft")
        /// con peso 1 y, cuando el Animator sale de él por su Exit Time, baja el peso suavemente y
        /// lanza <see cref="OnUpperBodyActionEnded"/>. Una acción nueva sustituye a la anterior.
        /// </summary>
        public void PlayUpperBodyAction(string fullPath)
        {
            if (animator == null || string.IsNullOrEmpty(fullPath)) return;

            if (upperBodyCo != null) { StopCoroutine(upperBodyCo); upperBodyCo = null; }
            animator.SetLayerWeight(upperLayerIndex, 1f);
            animator.Play(fullPath, upperLayerIndex, 0f);

            upperBodyCo = StartCoroutine(Co_WaitStateExitThenLowerLayer(Animator.StringToHash(fullPath)));
        }

        /// <summary>
        /// Mantiene una pose en la capa superior (ruta completa) con peso 1 y sin bajarla sola: para
        /// poses sostenidas como la del combo mágico. Se sale con <see cref="PlayUpperBodyAction"/>
        /// (un gesto que sí baja la capa al acabar) o con <see cref="ReleaseUpperBodyPose"/>.
        /// </summary>
        public void HoldUpperBodyPose(string fullPath, float crossfade = 0.15f)
        {
            if (animator == null || string.IsNullOrEmpty(fullPath)) return;
            if (upperBodyCo != null) { StopCoroutine(upperBodyCo); upperBodyCo = null; }
            animator.SetLayerWeight(upperLayerIndex, 1f);
            animator.CrossFadeInFixedTime(fullPath, crossfade, upperLayerIndex);
        }

        /// <summary>
        /// Pone un estado de la capa superior durante 'seconds' y luego baja la capa, aunque el estado
        /// no tenga salida (poses como FoundSomething, brazos arriba). Para gestos de hechizo de área.
        /// </summary>
        public void PlayUpperBodyActionFor(string fullPath, float seconds, float crossfade = 0.1f)
        {
            if (animator == null || string.IsNullOrEmpty(fullPath)) return;
            if (upperBodyCo != null) { StopCoroutine(upperBodyCo); upperBodyCo = null; }
            animator.SetLayerWeight(upperLayerIndex, 1f);
            animator.CrossFadeInFixedTime(fullPath, crossfade, upperLayerIndex);
            upperBodyCo = StartCoroutine(Co_HoldThenLower(seconds));
        }

        private IEnumerator Co_HoldThenLower(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, seconds));
            upperBodyCo = null;
            ReleaseUpperBodyPose();
        }

        /// <summary>Saltito desde el suelo (impulso relativo al salto normal). Para lanzar hechizos de área.</summary>
        public bool Hop(float impulseFactor)
        {
            if (!isGrounded || isJumping) return false;
            // Es parte del gesto del hechizo, no un salto del jugador: no depende de tener el salto.
            DoJump(impulseFactor, ignoreValidator: true);
            return true;
        }

        /// <summary>Suelta una pose sostenida bajando la capa superior suavemente.</summary>
        public void ReleaseUpperBodyPose()
        {
            if (animator == null) return;
            if (upperBodyCo != null) { StopCoroutine(upperBodyCo); upperBodyCo = null; }
            upperBodyCo = StartCoroutine(Co_LowerLayer());
        }

        private IEnumerator Co_LowerLayer()
        {
            int layer = upperLayerIndex;
            float t = 0f, start = animator.GetLayerWeight(layer);
            while (t < upperLayerFadeOut && animator != null)
            {
                t += Time.deltaTime;
                animator.SetLayerWeight(layer, Mathf.Lerp(start, 0f, t / upperLayerFadeOut));
                yield return null;
            }
            if (animator != null) animator.SetLayerWeight(layer, 0f);
            upperBodyCo = null;
            OnUpperBodyActionEnded?.Invoke();
        }

        private IEnumerator Co_WaitStateExitThenLowerLayer(int targetHash)
        {
            int layer = upperLayerIndex;

            // Esperar a entrar de verdad en el estado.
            while (animator != null && animator.GetCurrentAnimatorStateInfo(layer).fullPathHash != targetHash)
                yield return null;
            if (animator == null) { upperBodyCo = null; yield break; }

            // Esperar a que salga por su Exit Time (el clip no se corta).
            while (animator != null && animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == targetHash)
                yield return null;
            if (animator == null) { upperBodyCo = null; yield break; }

            float t = 0f, start = animator.GetLayerWeight(layer);
            while (t < upperLayerFadeOut)
            {
                t += Time.deltaTime;
                if (animator == null) break;
                animator.SetLayerWeight(layer, Mathf.Lerp(start, 0f, t / upperLayerFadeOut));
                yield return null;
            }

            if (animator != null) animator.SetLayerWeight(layer, 0f);
            upperBodyCo = null;

            OnUpperBodyActionEnded?.Invoke();
        }

        // ========================= Ciclo de vida =========================
        private void Start()
        {
            _actionValidator = GetComponent<IActionValidator>();
        }

        // Si el objeto se desactiva o destruye a mitad de una acción, la corrutina no termina:
        // se para aquí y el peso de la capa no se queda atascado.
        private void OnDisable()
        {
            if (upperBodyCo != null) { StopCoroutine(upperBodyCo); upperBodyCo = null; }
            if (animator != null) animator.SetLayerWeight(upperLayerIndex, 0f);
        }

        private void OnDestroy()
        {
            if (upperBodyCo != null) { StopCoroutine(upperBodyCo); upperBodyCo = null; }
            if (animator != null) animator.SetLayerWeight(upperLayerIndex, 0f);
        }
    }
}
