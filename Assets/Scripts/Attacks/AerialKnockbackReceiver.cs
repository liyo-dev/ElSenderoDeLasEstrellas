using System.Collections;
using UnityEngine;
using Invector.vCharacterController;
using Core;

/// <summary>
/// Es la única fuente de movimiento durante el arco: desactiva el controlador y el root motion.
/// Invector LITE usa Rigidbody; se mantiene cinemático y sin gravedad para que no acumule
/// velocidad de caída mientras la trayectoria mueve al jugador. Permite recuperarse en el aire.
/// </summary>
[DisallowMultipleComponent]
public class AerialKnockbackReceiver : MonoBehaviour
{
    [Header("Recuperación y derribo (INC-654)")]
    [Tooltip("Tramo del vuelo (0-1) en el que pulsar saltar hace la voltereta de recuperación.")]
    [SerializeField] private Vector2 ventanaDeRecuperacion = new Vector2(0.15f, 0.9f);
    [Tooltip("Estado del Animator tumbado en el suelo tras un derribo.")]
    [SerializeField] private string estadoEnElSuelo = "Knockdown01_Ground";
    [Tooltip("Estado del Animator al levantarse tras un derribo.")]
    [SerializeField] private string estadoLevantarse = "Knockdown01_StandUp";
    [Tooltip("Segundos tumbado antes de levantarse.")]
    [SerializeField, Min(0f)] private float segundosEnElSuelo = 0.7f;
    [Tooltip("Tope de segundos para levantarse.")]
    [SerializeField, Min(0.2f)] private float topeParaLevantarse = 2f;
    [Tooltip("Clave de SFX al recuperarse en el aire. Vacío = sin sonido.")]
    [SerializeField] private string sfxDeRecuperacion = "";

    [Tooltip("Estado del Animator durante el vuelo de un derribo.")]
    [SerializeField] private string estadoEnVuelo = "Derribo_Vuelo";
    [Tooltip("Fracción del clip que recorre durante el arco, sin alcanzar la pose final.")]
    [UnityEngine.Serialization.FormerlySerializedAs("velocidadDeLaPoseDeVuelo")]
    [SerializeField, Range(0.1f, 0.9f)] private float fraccionDelClipEnVuelo = 0.65f;
    [Tooltip("Estado de la voltereta de recuperación; vacío usa el de VolteretaDelJugador.")]
    [SerializeField] private string estadoDeRecuperacion = "JumpAirSpin_InPlace_NoWeapon";
    [Tooltip("Velocidad vertical del impulso de recuperación, en metros por segundo.")]
    [SerializeField, Min(0f)] private float impulsoVerticalAlRecuperarse = 6f;
    [Tooltip("Inercia horizontal inicial sin girar al jugador, en metros por segundo. Con entrada, el control aéreo la dirige hacia donde mira; cero evita ese arrastre.")]
    [SerializeField, Min(0f)] private float arrastreHorizontalAlRecuperarse = 0f;
    [Tooltip("Segundos de invulnerabilidad sin parpadeo al recuperarse.")]
    [SerializeField, Min(0f)] private float invulnerabilidadAlRecuperarse = 0.4f;
    [Tooltip("Efecto breve de la recuperación en el aire.")]
    [SerializeField] private GameObject vfxDeRecuperacion;
    [Tooltip("Segundos que permanece activo el efecto de recuperación antes de volver al pool.")]
    [SerializeField, Min(0.01f)] private float duracionDelVfxDeRecuperacion = 1f;

    private PlayerHealthSystem _salud;
    private int _hashVuelo, _hashBloqueo;
    private bool _tieneBloqueo, _bloqueoTomado, _bloqueoAnterior;
    private Vector3 _direccionDelVuelo;
    private VolteretaDelJugador _voltereta;
    private bool _recuperado;

    /// <summary>Indica si el jugador se recupera en el aire durante el último lanzamiento.</summary>
    public bool SeRecupero => _recuperado;

    /// <summary>Indica si pulsar saltar permite recuperarse; lo consulta el aviso de combate.</summary>
    public bool VentanaDeRecuperacionAbierta { get; private set; }

    private CharacterController _controller;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private vThirdPersonController _thirdPersonController;
    private PlayerActionManager _actionManager;

    private bool _isLaunching;
    private bool _cachedApplyRootMotion;
    private bool _controllerWasEnabled;
    private bool _stunnedModePushed;
    private bool _cachedRigidbodyKinematic;
    private bool _cachedRigidbodyUseGravity;

    private const float GroundProbeHeight = 3f;
    private const float GroundProbeMaxDistance = 10f;
    private const float GroundSkin = 0.05f;
    private float _lastGroundY;

    private const float PostLandingWatchDuration = 0.4f;
    private const float SinkTolerance = 0.15f;

    private LayerMask _groundProbeMask;

    private float _groundProbeRadius = 0.3f;

    private LayerMask _obstructionMask;
    private float _obstructionProbeHeight = 1f;

    private readonly RaycastHit[] _obstructionHitsBuffer = new RaycastHit[8];

    /// <summary>El receptor controla el arco o mantiene al jugador derribado hasta levantarse.</summary>
    public bool IsLaunching => _isLaunching;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _voltereta = GetComponentInParent<VolteretaDelJugador>();
        _salud = GetComponentInParent<PlayerHealthSystem>();
        _hashVuelo = Animator.StringToHash(estadoEnVuelo);
        _hashBloqueo = Animator.StringToHash("isFlying");
        if (_animator != null)
            for (int i = 0; i < _animator.parameterCount; i++)
            {
                var parametro = _animator.GetParameter(i);
                if (parametro.nameHash == _hashBloqueo && parametro.type == AnimatorControllerParameterType.Bool)
                    _tieneBloqueo = true;
            }
        _thirdPersonController = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();
        _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();
        // Floor y Obstacle limitan el sondeo al terreno sólido, sin tomar decoración por suelo.
        _groundProbeMask = LayerMask.GetMask("Floor", "Obstacle");

        var capsule = GetComponent<CapsuleCollider>() ?? GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            _groundProbeRadius = Mathf.Max(0.15f, capsule.radius * 0.9f);
            _obstructionProbeHeight = capsule.center.y > 0f ? capsule.center.y : capsule.height * 0.5f;
        }

        _obstructionMask = ~0;
    }

    /// <summary>
    /// Devuelve física, animación y bloqueos porque desactivar el objeto detiene las corrutinas
    /// sin garantizar que alcancen su restauración final.
    /// </summary>
    private void OnDisable()
    {
        StopAllCoroutines();
        Restaurar();
    }

    /// <summary>Devuelve todos los recursos tomados una sola vez, incluso al cancelar el arco.</summary>
    private void Restaurar()
    {
        VentanaDeRecuperacionAbierta = false;
        if (!_isLaunching) return;
        _isLaunching = false;
        if (_rigidbody != null)
        {
            // La velocidad solo se puede limpiar después de devolver el cuerpo a dinámico.
            _rigidbody.isKinematic = _cachedRigidbodyKinematic;
            _rigidbody.useGravity = _cachedRigidbodyUseGravity;
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }
        if (_thirdPersonController != null) _thirdPersonController.enabled = _controllerWasEnabled;
        if (_animator != null)
        {
            _animator.applyRootMotion = _cachedApplyRootMotion;
            if (_bloqueoTomado) _animator.SetBool(_hashBloqueo, _bloqueoAnterior);
        }
        _bloqueoTomado = false;
        if (_stunnedModePushed && _actionManager != null) _actionManager.PopMode(ActionMode.Stunned);
        _stunnedModePushed = false;
    }
    /// <summary>Lanza en la dirección indicada sin derribar; permite recuperarse durante el arco.</summary>
    public void Launch(Vector3 backwardDirection, float distance, float height, float duration) =>
        Launch(backwardDirection, distance, height, duration, derriba: false);

    /// <summary>
    /// Lanza al cuerpo del jugador alejándolo del origen. Solo acepta al jugador vivo y sin
    /// escudo ni ventana de parada; devuelve true cuando aplica el derribo.
    /// </summary>
    public static bool Derribar(GameObject objetivo, Vector3 origen, float distancia, float altura, float duracion)
    {
        if (objetivo == null) return false;
        var jugador = PlayerService.Player;
        if (jugador == null || (objetivo != jugador && !objetivo.transform.IsChildOf(jugador.transform))) return false;

        var cuerpo = objetivo.GetComponentInParent<vThirdPersonController>();
        if (cuerpo == null) return false;
        var salud = cuerpo.GetComponent<PlayerHealthSystem>();
        if (salud != null && !salud.IsAlive) return false;
        var escudo = cuerpo.GetComponent<PlayerShieldController>();
        if (escudo != null && (escudo.IsParryWindowOpen || escudo.IsDefending)) return false;

        if (!cuerpo.TryGetComponent(out AerialKnockbackReceiver receptor))
            receptor = cuerpo.gameObject.AddComponent<AerialKnockbackReceiver>();
        Vector3 dir = cuerpo.transform.position - origen;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -cuerpo.transform.forward;
        receptor.Launch(dir.normalized, distancia, altura, duracion, derriba: true);
        return true;
    }

    /// <summary>
    /// Lanza en la dirección indicada. Si derriba, mira al golpe y permanece tumbado al aterrizar
    /// salvo que se recupere en el aire. No solapa lanzamientos.
    /// </summary>
    public void Launch(Vector3 backwardDirection, float distance, float height, float duration, bool derriba)
    {
        if (_isLaunching) return; // no solapar lanzamientos
        if (duration <= 0f || !isActiveAndEnabled) return;

        StopAllCoroutines();
        StartCoroutine(LaunchRoutine(backwardDirection, distance, height, duration, derriba));
    }

    private IEnumerator LaunchRoutine(Vector3 backwardDirection, float distance, float height, float duration, bool derriba)
    {
        _isLaunching = true;
        _recuperado = false;
        if (_animator != null)
        {
            _cachedApplyRootMotion = _animator.applyRootMotion;
            _animator.applyRootMotion = false;
        }
        if (_thirdPersonController != null)
        {
            _controllerWasEnabled = _thirdPersonController.enabled;
            _thirdPersonController.enabled = false;
        }

        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Stunned);
            _stunnedModePushed = true;
        }
        if (_rigidbody != null)
        {
            _cachedRigidbodyKinematic = _rigidbody.isKinematic;
            _cachedRigidbodyUseGravity = _rigidbody.useGravity;
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }

        Vector3 flatDir = backwardDirection;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude < 0.0001f) flatDir = -transform.forward;
        flatDir.Normalize();
        _direccionDelVuelo = flatDir;
        bool poseDeVuelo = derriba && _animator != null && _animator.HasState(0, _hashVuelo);
        if (derriba)
        {
            Quaternion mirandoAlGolpe = Quaternion.LookRotation(-flatDir, Vector3.up);
            if (_rigidbody != null) _rigidbody.rotation = mirandoAlGolpe;
            else transform.rotation = mirandoAlGolpe;
            if (poseDeVuelo)
            {
                if (_tieneBloqueo)
                {
                    _bloqueoAnterior = _animator.GetBool(_hashBloqueo);
                    _bloqueoTomado = true;
                    _animator.SetBool(_hashBloqueo, true);
                }
                _animator.CrossFadeInFixedTime(_hashVuelo, 0.08f, 0, 0f);
            }
        }
        Vector3 obstructionOrigin = transform.position + Vector3.up * _obstructionProbeHeight;
        distance = ClampLaunchDistance(obstructionOrigin, flatDir, distance);

        Vector3 startPos = transform.position;
        Vector3 lastIntendedPos = startPos;
        _lastGroundY = startPos.y;

        float elapsed = 0f;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        // Diagnóstico temporal de INC-687 (recuperación que no salta): se quita al cerrarla.
        int diagFramesVentana = 0;
        bool diagFisicoEnVentana = false, diagFisicoFuera = false;
#endif
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            VentanaDeRecuperacionAbierta = !_recuperado && t >= ventanaDeRecuperacion.x && t <= ventanaDeRecuperacion.y;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            {
                var mando = UnityEngine.InputSystem.Gamepad.current;
                var teclado = UnityEngine.InputSystem.Keyboard.current;
                bool fisico = (mando != null && mando.buttonSouth.isPressed) || (teclado != null && teclado.spaceKey.isPressed);
                if (VentanaDeRecuperacionAbierta) { diagFramesVentana++; diagFisicoEnVentana |= fisico; }
                else diagFisicoFuera |= fisico;
            }
#endif
            // Durante el lanzamiento el modo Stunned bloquea el mapa GamePlay: la A se lee sin ese bloqueo.
            if (VentanaDeRecuperacionAbierta && GamepadInputReader.JumpPressedAunqueBloqueado)
            {
                Recuperarse();
                yield break;
            }
            if (poseDeVuelo && !_animator.IsInTransition(0))
                _animator.Play(_hashVuelo, 0, t * fraccionDelClipEnVuelo);
            float horizontalT = 1f - (1f - t) * (1f - t);
            float verticalY = Mathf.Sin(t * Mathf.PI) * height;

            Vector3 targetPos = startPos + flatDir * (distance * horizontalT);
            // El suelo se sondea en cada posición del arco para aterrizar sobre pendientes y escalones.
            float groundY = SampleGroundY(targetPos.x, targetPos.z);
            targetPos.y = groundY + GroundSkin + verticalY;

            Vector3 frameDelta = targetPos - lastIntendedPos;
            lastIntendedPos = targetPos;

            if (_controller != null && _controller.enabled)
                _controller.Move(frameDelta);
            else if (_rigidbody != null && _rigidbody.isKinematic)
                _rigidbody.MovePosition(transform.position + frameDelta);
            else
                transform.position += frameDelta;

            yield return null;
        }
        float finalGroundY = SampleGroundY(transform.position.x, transform.position.z);
        Vector3 finalPos = transform.position;
        finalPos.y = finalGroundY + GroundSkin;
        if (_rigidbody != null && _rigidbody.isKinematic)
            _rigidbody.MovePosition(finalPos);
        else
            transform.position = finalPos;


        VentanaDeRecuperacionAbierta = false;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Derribo-diag] Sin recuperarse. duración={duration:F2}s, frames con ventana={diagFramesVentana}, " +
                  $"A física pulsada en la ventana={diagFisicoEnVentana}, A física fuera de la ventana={diagFisicoFuera}, " +
                  $"mando={(UnityEngine.InputSystem.Gamepad.current != null ? UnityEngine.InputSystem.Gamepad.current.displayName : "ninguno")}, timeScale={Time.timeScale:F2}");
#endif
        if (derriba && !_recuperado)
            yield return Co_DerriboEnElSuelo();

        Restaurar();

        StartCoroutine(PostLandingSafetyNet());
    }

    private void Recuperarse()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[Derribo-diag] Recuperación con A detectada.");
#endif
        _recuperado = true;
        Restaurar();
        if (_thirdPersonController != null)
        {
            // El empuje cero evita que Impulsar gire al jugador y deje de mirar al golpe.
            _thirdPersonController.Impulsar(impulsoVerticalAlRecuperarse,
                Vector3.zero, devolverSaltosEnElAire: false);
            if (_thirdPersonController.isActiveAndEnabled && !_thirdPersonController.suppressAirMovement &&
                _rigidbody != null && !_rigidbody.isKinematic && arrastreHorizontalAlRecuperarse > 0f)
            {
                // Sin entrada se frena esta inercia; con entrada Invector la orienta hacia donde mira.
                Vector3 velocidad = _direccionDelVuelo * arrastreHorizontalAlRecuperarse;
                velocidad.y = _rigidbody.linearVelocity.y;
                _rigidbody.linearVelocity = velocidad;
            }
        }
        if (_voltereta != null) _voltereta.EnElAire(estadoDeRecuperacion);
        if (_salud != null) _salud.HacerInvulnerable(invulnerabilidadAlRecuperarse, parpadeo: false);
        if (vfxDeRecuperacion != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxDeRecuperacion, transform.position, transform.rotation, duracionDelVfxDeRecuperacion);
        if (!string.IsNullOrEmpty(sfxDeRecuperacion) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxDeRecuperacion);
    }

    private IEnumerator Co_DerriboEnElSuelo()
    {
        int suelo = Animator.StringToHash(estadoEnElSuelo);
        int levantarse = Animator.StringToHash(estadoLevantarse);
        bool animado = _animator != null && _animator.HasState(0, suelo) && _animator.HasState(0, levantarse);

        if (animado) _animator.CrossFadeInFixedTime(suelo, 0.1f, 0);
        if (segundosEnElSuelo > 0f) yield return new WaitForSeconds(segundosEnElSuelo);
        if (!animado) yield break;

        _animator.CrossFadeInFixedTime(levantarse, 0.12f, 0);
        float tope = Time.time + topeParaLevantarse;
        float empezo = Time.time;
        while (Time.time < tope)
        {
            var info = _animator.IsInTransition(0) ? _animator.GetNextAnimatorStateInfo(0) : _animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == levantarse && info.normalizedTime >= 0.9f) break;
            if (info.shortNameHash != levantarse && Time.time > empezo + 0.4f) break;   // ya ha salido
            yield return null;
        }
    }

    /// <summary>
    /// Corrige hundimientos breves al reactivar la física, que puede resolver contactos bajo el
    /// suelo tras el movimiento cinemático. Solo vigila el aterrizaje, nunca la recuperación aérea.
    /// </summary>
    private IEnumerator PostLandingSafetyNet()
    {
        float watched = 0f;
        while (watched < PostLandingWatchDuration)
        {
            watched += Time.deltaTime;

            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                float groundY = SampleGroundY(transform.position.x, transform.position.z);
                float sinkAmount = groundY - transform.position.y;

                if (sinkAmount > SinkTolerance)
                {
                    Vector3 corrected = _rigidbody.position;
                    corrected.y = groundY + GroundSkin;
                    _rigidbody.position = corrected;

                    if (_rigidbody.linearVelocity.y < 0f)
                    {
                        Vector3 vel = _rigidbody.linearVelocity;
                        vel.y = 0f;
                        _rigidbody.linearVelocity = vel;
                    }
                }
            }

            yield return null;
        }
    }

    /// <summary>Sondea con SphereCast para no caer por rendijas entre piezas del suelo.</summary>
    private float SampleGroundY(float x, float z)
    {
        Vector3 origin = new Vector3(x, _lastGroundY + GroundProbeHeight, z);

        if (Physics.SphereCast(origin, _groundProbeRadius, Vector3.down, out RaycastHit hit, GroundProbeHeight + GroundProbeMaxDistance, _groundProbeMask, QueryTriggerInteraction.Ignore))
        {
            // Un sondeo que empieza solapando puede devolver punto y normal nulos: no es suelo válido.
            // Se comprueban ambos para no descartar un suelo real situado en el origen del mundo.
            bool isBogusZeroHit = hit.point == Vector3.zero && hit.normal == Vector3.zero;
            if (!isBogusZeroHit)
                _lastGroundY = hit.point.y;
        }

        return _lastGroundY;
    }

    private float ClampLaunchDistance(Vector3 origin, Vector3 direction, float requestedDistance)
    {
        if (requestedDistance <= 0f) return requestedDistance;

        // NonAlloc recoge todos los hits en el buffer: el collider propio no debe ocultar una pared.
        // Personajes y geometría comparten Default, por lo que se consulta todo y se filtran personajes.
        int count = Physics.SphereCastNonAlloc(origin, _groundProbeRadius, direction, _obstructionHitsBuffer, requestedDistance, _obstructionMask, QueryTriggerInteraction.Ignore);
        if (count <= 0) return requestedDistance;

        bool foundObstruction = false;
        float closestDistance = requestedDistance;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _obstructionHitsBuffer[i];
            Transform root = hit.collider.transform.root;
            if (root.GetComponent<NPCSimpleAnimator>() != null)
                continue; // es un personaje (incluye al propio jugador), no un obstáculo real

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                foundObstruction = true;
            }
        }

        if (!foundObstruction) return requestedDistance;

        float clamped = Mathf.Max(0f, closestDistance - GroundSkin);

        return clamped;
    }

}
