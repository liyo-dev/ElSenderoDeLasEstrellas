using System.Collections.Generic;
using Invector.vCharacterController;
using UnityEngine;
using Core;

/// <summary>
/// Nado del jugador: detecta el agua, gestiona la entrada y mantiene la línea de agua a la
/// altura del cuello mientras flota.
///
/// Fases:
///   · Fuera      — no está en el agua.
///   · Zambullida — ha entrado cayendo rápido: salpica, se hunde con la animación de caída
///                  (tanto más cuanto más rápido cae) y sube a flote.
///   · Flotando   — animación de nado, cabeza fuera, ondas al moverse.
///   · Saliendo   — nadando contra un borde alcanzable (bordillo, escalón, orilla alta), se
///                  impulsa hacia arriba y pasa por encima; al terminar vuelve a andar.
///
/// La altura de flote se calcula con el hueso del cuello del esqueleto humanoide, así que vale
/// para cualquier personaje sin ajustar números a mano. Ver INC-512.
/// </summary>
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class PlayerSwimmingController : MonoBehaviour
{
    private enum Fase { Fuera, Zambullida, Flotando, Saliendo }

    private const float RaycastVerticalOffset = 3f;
    private const float RaycastDepth = 10f;
    private const float SurfaceMemoryTime = 0.4f;
    private const float EnterBuffer = 0.05f;
    private const float MaxVerticalSpeed = 6f;
    private const float ExitCrossFade = 0.15f;
    private const float SwimMoveSpeed = 2.5f;
    private const float SwimAcceleration = 12f;
    private const float SwimResponsiveness = 18f;
    private const float TiempoAjusteFlote = 0.22f;
    private const float SuavizadoCuello = 4f;
    private const float TiempoMaximoZambullida = 3f;
    private const float MargenSobreElFondo = 0.12f;
    private const float AlcanceSondaFondo = 6f;
    private const float ControlDuranteZambullida = 0.3f;
    private const float FrenadoHorizontalZambullida = 5f;
    private const float VelocidadMaximaImpacto = 14f;
    private const float DistanciaDeteccionBorde = 0.35f;
    private const float TiempoMaximoSalida = 1f;
    private const float EmpujeContraElBorde = 0.3f;
    private const float VelocidadPasarElBorde = 2.2f;

    [Header("Animación")]
    [SerializeField] private int locomotionLayerIndex = 0;
    [SerializeField] private string swimStateName = "Swimming_Floating_NoWeapon";
    [SerializeField] private string locomotionStateName = "Free Locomotion";
    [SerializeField, Tooltip("Duración del fundido a la animación de nado al empezar a flotar (s).")]
    private float fundidoAlFlotar = 0.25f;

    [Header("Línea de agua")]
    [SerializeField, Tooltip("Distancia entre el hueso del cuello y la superficie al flotar (m). Más alto = más cuerpo fuera del agua.")]
    private float cuelloSobreElAgua = 0.04f;
    [SerializeField, Tooltip("Profundidad del pivote bajo la superficie cuando el modelo no tiene esqueleto humanoide (m).")]
    private float surfaceOffset = 0.45f;

    [Header("Zambullida")]
    [SerializeField, Tooltip("Velocidad de caída (m/s) a partir de la cual entrar al agua es una zambullida.")]
    private float velocidadMinimaZambullida = 3f;
    [SerializeField, Tooltip("Metros que se hunde por debajo de la altura de flote por cada m/s de velocidad de caída.")]
    private float hundimientoPorVelocidad = 0.12f;
    [SerializeField, Tooltip("Hundimiento mínimo y máximo por debajo de la altura de flote (m).")]
    private Vector2 hundimientoMinMax = new Vector2(0.35f, 1.4f);
    [SerializeField, Tooltip("Velocidad máxima de subida a flote (m/s).")]
    private float velocidadSubida = 1.8f;
    [SerializeField, Tooltip("Aceleración de subida a flote (m/s²).")]
    private float aceleracionSubida = 5f;

    [Header("Salir del agua")]
    [SerializeField, Tooltip("Altura máxima de un borde sobre la superficie del agua al que se puede subir nadando (m).")]
    private float alturaMaximaSalida = 0.9f;
    [SerializeField, Tooltip("Velocidad con la que se impulsa hacia arriba para subir al borde (m/s).")]
    private float velocidadSubirBorde = 3f;

    [Header("Ondas al nadar")]
    [SerializeField, Tooltip("Segundos entre ondas mientras nada.")]
    private float intervaloOndasNadando = 0.3f;
    [SerializeField, Tooltip("Segundos entre ondas mientras flota quieto.")]
    private float intervaloOndasQuieto = 1.3f;

    [Header("Sonido (claves de AudioGraphProfile)")]
    [SerializeField] private string sfxZambullida = "Agua_Zambullida";
    [SerializeField] private string sfxEntrada = "Agua_Entrada";
    [SerializeField] private string sfxSalirAFlote = "Agua_SalirAFlote";

    [SerializeField] private bool debugLogs = false;

    private readonly List<Collider> _waterVolumes = new();
    private readonly HashSet<Collider> _waterSet = new();
    private readonly RaycastHit[] _raycastHits = new RaycastHit[8];
    private readonly RaycastHit[] _groundHits = new RaycastHit[8];
    private int _waterMask;
    private int _solidMask;
    private int _floorMask;
    private int _swimStateHash;
    private Animator _animator;
    private PlayerActionManager _actionManager;
    private Rigidbody _rigidbody;
    private CapsuleCollider _capsule;
    private vThirdPersonController _thirdPersonController;
    private Transform _cameraTransform;
    private Transform _huesoCuello;
    private SalpicaduraDeAgua _salpicadura;

    private Fase _fase = Fase.Fuera;
    private float _lastSurfaceSeenTime;
    private float _rememberedSurfaceY;
    private float _alturaFlote;
    private float _offsetCuello;
    private bool _tieneOffsetCuello;
    private float _velSuavizadaY;
    private float _velZambullidaY;
    private float _deceleracionZambullida;
    private float _fondoZambullida;
    private float _inicioZambullida;
    private float _proximaOnda;
    private Vector3 _destinoSalida;
    private Vector3 _direccionSalida;
    private float _inicioSalida;

    private float _cachedExtraGravity;
    private bool _suspendedExtraGravity;
    private int _storedGroundMask;
    private bool _groundMaskOverridden;
    private float _originalAirSpeed;
    private float _originalAirSmooth;
    private bool _airTuningApplied;
    private PlayerControls _controls;
    private bool _ownsControls;

    /// <summary>True mientras está en el agua (zambulléndose o flotando).</summary>
    public bool EstaEnElAgua => _fase != Fase.Fuera;

    /// <summary>Altura del pivote a la que flota. Solo válida con <see cref="EstaEnElAgua"/>.</summary>
    public float AlturaDeFlote => _alturaFlote;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _actionManager = GetComponent<PlayerActionManager>();
        _rigidbody = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>() ?? GetComponentInChildren<CapsuleCollider>();
        _thirdPersonController = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();

        _waterMask = LayerMask.GetMask("Water");
        _solidMask = _waterMask == 0
            ? Physics.DefaultRaycastLayers
            : Physics.DefaultRaycastLayers & ~_waterMask;
        if (_waterMask == 0)
            _waterMask = ~0; // sin capa Water: cualquier capa vale como agua

        _floorMask = LayerMask.GetMask("Floor");
        _swimStateHash = Animator.StringToHash(swimStateName);
        CacheCameraTransform();

        _controls = Core.PlayerInputManager.GetSharedOrNew(out _ownsControls);

        var vfx = new GameObject("Salpicaduras de agua");
        vfx.transform.SetParent(transform, false);
        _salpicadura = vfx.AddComponent<SalpicaduraDeAgua>();
    }

    void Start()
    {
        ResolverHuesoCuello();
        if (_huesoCuello != null)
        {
            _offsetCuello = Mathf.Clamp(_huesoCuello.position.y - transform.position.y, 0.1f, 3f);
            _tieneOffsetCuello = true;
        }
    }

    void OnEnable()
    {
        if (_ownsControls)
            _controls?.Enable();
    }

    void OnDisable()
    {
        if (_ownsControls)
            _controls?.Disable();
        if (_fase != Fase.Fuera)
            ExitSwimming(force: true);
    }

    void Update()
    {
        if (_animator == null)
            return;

        bool hasWater = CleanupWaterList();
        bool hasSurface = false;
        float surfaceY = 0f;

        if (hasWater && TryGetWaterSurface(out surfaceY))
        {
            hasSurface = true;
            _rememberedSurfaceY = surfaceY;
            _lastSurfaceSeenTime = Time.time;
        }
        else if (_fase != Fase.Fuera && Time.time - _lastSurfaceSeenTime <= SurfaceMemoryTime)
        {
            hasSurface = true;
            surfaceY = _rememberedSurfaceY;
        }

        if (_fase == Fase.Fuera)
        {
            if (hasSurface && ShouldEnter(surfaceY) && (_actionManager == null || _actionManager.CanSwim()))
                EnterSwimming(surfaceY);
            return;
        }

        // Subiendo al borde: la sale del volumen de agua a mitad de camino, así que aquí no se
        // mira el agua; FixedUpdate decide cuándo ha terminado.
        if (_fase == Fase.Saliendo)
        {
            ForceAnimatorGrounded();
            return;
        }

        if (!hasSurface)
        {
            ExitSwimming();
            return;
        }

        _alturaFlote = CalcularAlturaDeFlote(surfaceY);

        if (_fase == Fase.Zambullida)
        {
            ForceAnimatorAirborne();
            return;
        }

        if (HaySueloParaPisar())
        {
            ExitSwimming();
            return;
        }

        MedirCuello();
        ForceAnimatorGrounded();
        OndasAlNadar(surfaceY);
    }

    void FixedUpdate()
    {
        if (_fase == Fase.Fuera || _rigidbody == null)
            return;

        float dt = Time.fixedDeltaTime;
        var vel = _rigidbody.linearVelocity;

        if (_fase == Fase.Zambullida)
        {
            if (_velZambullidaY < 0f)
                _velZambullidaY = Mathf.Min(0f, _velZambullidaY + _deceleracionZambullida * dt);
            else
                _velZambullidaY = Mathf.MoveTowards(_velZambullidaY, velocidadSubida, aceleracionSubida * dt);

            float y = _rigidbody.position.y;
            if (_velZambullidaY < 0f && y + _velZambullidaY * dt < _fondoZambullida)
                _velZambullidaY = 0f;

            vel.y = _velZambullidaY;
            vel = FrenarEnZambullida(vel, dt);
            _rigidbody.linearVelocity = vel;

            bool haSubido = _velZambullidaY > 0f && y >= _alturaFlote - 0.1f;
            if (haSubido || Time.time - _inicioZambullida > TiempoMaximoZambullida)
                EmpezarAFlotar(salidaAFlote: true);
            return;
        }

        if (_fase == Fase.Saliendo)
        {
            _rigidbody.linearVelocity = VelocidadDeSalida();
            return;
        }

        if (TryDetectarBorde(out Vector3 borde, out Vector3 direccion))
        {
            EmpezarSalida(borde, direccion);
            return;
        }

        Mathf.SmoothDamp(_rigidbody.position.y, _alturaFlote, ref _velSuavizadaY, TiempoAjusteFlote, MaxVerticalSpeed, dt);
        vel.y = _velSuavizadaY;
        vel = ApplySwimHorizontalControl(vel, 1f);
        _rigidbody.linearVelocity = vel;
    }

    // ── Entrada y salida ─────────────────────────────────────────────────

    void EnterSwimming(float surfaceY)
    {
        if (_fase != Fase.Fuera)
            return;

        if (_actionManager != null && !_actionManager.CanSwim())
            return;

        _rememberedSurfaceY = surfaceY;
        if (_huesoCuello == null)
            ResolverHuesoCuello();
        _alturaFlote = CalcularAlturaDeFlote(surfaceY);

        if (_actionManager != null)
            _actionManager.PushMode(ActionMode.Swimming);

        SuspenderFisicaDeTierra();

        float velocidadCaida = 0f;
        if (_rigidbody != null)
            velocidadCaida = Mathf.Clamp(-_rigidbody.linearVelocity.y, 0f, VelocidadMaximaImpacto);

        Vector3 puntoSuperficie = new Vector3(transform.position.x, surfaceY, transform.position.z);

        if (velocidadCaida >= velocidadMinimaZambullida && EmpezarZambullida(velocidadCaida))
        {
            float fuerza = Mathf.InverseLerp(velocidadMinimaZambullida * 0.5f, VelocidadMaximaImpacto, velocidadCaida);
            _salpicadura.Salpicar(puntoSuperficie, fuerza);
            _salpicadura.Burbujas(transform.position + Vector3.up * 0.3f, 0.3f, Mathf.RoundToInt(Mathf.Lerp(6, 18, fuerza)));
            ReproducirSfx(sfxZambullida, Mathf.Lerp(0.55f, 1f, fuerza), puntoSuperficie);
        }
        else
        {
            float velocidadHorizontal = 0f;
            if (_rigidbody != null)
            {
                var v = _rigidbody.linearVelocity;
                velocidadHorizontal = new Vector2(v.x, v.z).magnitude;
            }
            float fuerza = Mathf.Clamp01(Mathf.Max(velocidadCaida, velocidadHorizontal * 0.5f) / 8f);
            _salpicadura.Salpicar(puntoSuperficie, fuerza * 0.6f);
            ReproducirSfx(sfxEntrada, Mathf.Lerp(0.35f, 0.7f, fuerza), puntoSuperficie);
            EmpezarAFlotar(salidaAFlote: false);
        }

        Log("Entra en el agua (" + _fase + ")");
    }

    bool EmpezarZambullida(float velocidadCaida)
    {
        if (_rigidbody == null)
            return false;

        float y = _rigidbody.position.y;
        float hundimiento = Mathf.Clamp(velocidadCaida * hundimientoPorVelocidad, hundimientoMinMax.x, hundimientoMinMax.y);
        float objetivo = _alturaFlote - hundimiento;

        if (TryGetSueloBajo(out float sueloY))
            objetivo = Mathf.Max(objetivo, sueloY + MargenSobreElFondo);

        float recorrido = y - objetivo;
        if (recorrido < 0.1f)
            return false;

        _fase = Fase.Zambullida;
        _fondoZambullida = objetivo;
        _velZambullidaY = -velocidadCaida;
        _deceleracionZambullida = velocidadCaida * velocidadCaida / (2f * recorrido);
        _inicioZambullida = Time.time;
        return true;
    }

    void EmpezarAFlotar(bool salidaAFlote)
    {
        float velocidadInicial = 0f;
        if (salidaAFlote)
            velocidadInicial = _velZambullidaY;
        else if (_rigidbody != null)
            velocidadInicial = Mathf.Clamp(_rigidbody.linearVelocity.y, -2f, 2f);

        _fase = Fase.Flotando;
        _velSuavizadaY = velocidadInicial;
        _proximaOnda = Time.time + intervaloOndasNadando;

        if (_animator != null)
            _animator.CrossFadeInFixedTime(_swimStateHash, fundidoAlFlotar, locomotionLayerIndex);

        if (salidaAFlote)
        {
            Vector3 punto = new Vector3(transform.position.x, _rememberedSurfaceY, transform.position.z);
            _salpicadura.Onda(punto, 0.9f);
            _salpicadura.Gotitas(punto, 6);
            ReproducirSfx(sfxSalirAFlote, 0.5f, punto);
        }
    }

    void ExitSwimming(bool force = false)
    {
        if (_fase == Fase.Fuera && !force)
            return;

        bool estabaEnElAgua = _fase != Fase.Fuera;
        bool yaAndando = _fase == Fase.Saliendo;
        _fase = Fase.Fuera;

        if (estabaEnElAgua && _actionManager != null)
            _actionManager.PopMode(ActionMode.Swimming);

        if (estabaEnElAgua && !yaAndando && _animator != null)
            _animator.CrossFade(locomotionStateName, ExitCrossFade, locomotionLayerIndex);

        RestaurarFisicaDeTierra();

        if (estabaEnElAgua)
            Log("Sale del agua");
    }

    void SuspenderFisicaDeTierra()
    {
        if (_rigidbody != null)
            _rigidbody.useGravity = false;

        if (_thirdPersonController == null)
            return;

        if (!_suspendedExtraGravity)
        {
            _cachedExtraGravity = _thirdPersonController.extraGravity;
            _thirdPersonController.extraGravity = 0f;
            _suspendedExtraGravity = true;
        }

        if (!_airTuningApplied)
        {
            _originalAirSpeed = _thirdPersonController.airSpeed;
            _originalAirSmooth = _thirdPersonController.airSmooth;
            _airTuningApplied = true;
        }
        _thirdPersonController.airSpeed = SwimMoveSpeed;
        _thirdPersonController.airSmooth = SwimResponsiveness;

        if (!_groundMaskOverridden && _floorMask != 0)
        {
            _storedGroundMask = _thirdPersonController.groundLayer.value;
            _thirdPersonController.groundLayer = _storedGroundMask & ~_floorMask;
            _groundMaskOverridden = true;
        }
    }

    void RestaurarFisicaDeTierra()
    {
        if (_rigidbody != null)
            _rigidbody.useGravity = true;

        if (_thirdPersonController == null)
            return;

        if (_suspendedExtraGravity)
        {
            _thirdPersonController.extraGravity = _cachedExtraGravity;
            _suspendedExtraGravity = false;
        }

        if (_groundMaskOverridden)
        {
            _thirdPersonController.groundLayer = _storedGroundMask;
            _groundMaskOverridden = false;
        }

        if (_airTuningApplied)
        {
            _thirdPersonController.airSpeed = _originalAirSpeed;
            _thirdPersonController.airSmooth = _originalAirSmooth;
            _airTuningApplied = false;
        }
    }

    // ── Salir del agua por un borde ──────────────────────────────────────

    // Nadando hacia una pared: si arriba hay un borde pisable a una altura alcanzable y cabe el
    // cuerpo, se sube. Las orillas en rampa no pasan por aquí: ahí basta con la salida por suelo.
    bool TryDetectarBorde(out Vector3 borde, out Vector3 direccion)
    {
        borde = default;
        direccion = LeerDireccionDeseada();
        if (direccion.sqrMagnitude < 0.09f)
            return false;
        direccion.Normalize();

        Vector3 pos = _rigidbody.position;
        float radio = RadioCuerpo();
        Vector3 origen = pos + Vector3.up * 0.08f;
        if (!RaycastSinPropio(origen, direccion, radio + DistanciaDeteccionBorde, out RaycastHit pared))
            return false;
        if (pared.normal.y > 0.5f)
            return false;

        float techo = _rememberedSurfaceY + alturaMaximaSalida;
        Vector3 arriba = new Vector3(pared.point.x, techo + 0.1f, pared.point.z) + direccion * (radio + 0.15f);
        if (!RaycastSinPropio(arriba, Vector3.down, techo + 0.1f - pos.y, out RaycastHit encima))
            return false;
        if (encima.normal.y < 0.7f)
            return false;

        float alto = encima.point.y;
        if (alto <= pos.y + 0.05f || alto > techo)
            return false;

        float altoCuerpo = _capsule != null ? _capsule.bounds.size.y : 1.7f;
        Vector3 pies = encima.point + Vector3.up * (radio + 0.03f);
        Vector3 cabeza = encima.point + Vector3.up * Mathf.Max(radio + 0.04f, altoCuerpo - radio);
        if (Physics.CheckCapsule(pies, cabeza, radio * 0.9f, _solidMask, QueryTriggerInteraction.Ignore))
            return false;

        borde = encima.point;
        return true;
    }

    void EmpezarSalida(Vector3 borde, Vector3 direccion)
    {
        _fase = Fase.Saliendo;
        _destinoSalida = borde + direccion * (RadioCuerpo() + 0.1f);
        _direccionSalida = direccion;
        _inicioSalida = Time.time;

        if (_animator != null)
            _animator.CrossFade(locomotionStateName, 0.2f, locomotionLayerIndex);

        Vector3 punto = new Vector3(_rigidbody.position.x, _rememberedSurfaceY, _rigidbody.position.z);
        _salpicadura.Onda(punto, 0.8f);
        _salpicadura.Gotitas(punto, 8);
        ReproducirSfx(sfxEntrada, 0.4f, punto);
        Log("Sale del agua por un borde");
    }

    // Primero sube pegado a la pared hasta la altura del borde; luego avanza por encima.
    Vector3 VelocidadDeSalida()
    {
        Vector3 pos = _rigidbody.position;
        bool agotado = Time.time - _inicioSalida > TiempoMaximoSalida;

        if (pos.y < _destinoSalida.y + 0.04f)
        {
            if (agotado)
            {
                _fase = Fase.Flotando;
                _velSuavizadaY = 0f;
                if (_animator != null)
                    _animator.CrossFadeInFixedTime(_swimStateHash, fundidoAlFlotar, locomotionLayerIndex);
                return Vector3.zero;
            }
            return Vector3.up * velocidadSubirBorde + _direccionSalida * EmpujeContraElBorde;
        }

        Vector3 falta = _destinoSalida - pos;
        falta.y = 0f;
        if (falta.sqrMagnitude < 0.01f || Vector3.Dot(falta, _direccionSalida) <= 0f || agotado)
        {
            ExitSwimming();
            return _direccionSalida * 1f;
        }
        return _direccionSalida * VelocidadPasarElBorde;
    }

    float RadioCuerpo()
    {
        return _capsule != null ? Mathf.Max(0.05f, _capsule.bounds.extents.x) : 0.3f;
    }

    bool RaycastSinPropio(Vector3 origen, Vector3 direccion, float distancia, out RaycastHit resultado)
    {
        resultado = default;
        if (distancia <= 0f)
            return false;

        int hits = Physics.RaycastNonAlloc(origen, direccion, _groundHits, distancia, _solidMask, QueryTriggerInteraction.Ignore);
        bool found = false;
        float mejor = float.MaxValue;
        for (int i = 0; i < hits; ++i)
        {
            var col = _groundHits[i].collider;
            if (col == null || col.transform.IsChildOf(transform))
                continue;
            if (_groundHits[i].distance < mejor)
            {
                mejor = _groundHits[i].distance;
                resultado = _groundHits[i];
                found = true;
            }
        }
        return found;
    }

    // ── Altura de flote ──────────────────────────────────────────────────

    void ResolverHuesoCuello()
    {
        if (_animator == null || !_animator.isHuman)
            return;
        _huesoCuello = _animator.GetBoneTransform(HumanBodyBones.Neck)
                    ?? _animator.GetBoneTransform(HumanBodyBones.Head);
    }

    // La distancia pivote→cuello no depende de dónde esté el pivote, así que medirla cada frame
    // no realimenta la altura: solo sigue el balanceo propio de la animación de nado.
    void MedirCuello()
    {
        if (_huesoCuello == null)
            return;

        float medido = Mathf.Clamp(_huesoCuello.position.y - transform.position.y, 0.1f, 3f);
        if (!_tieneOffsetCuello)
        {
            _offsetCuello = medido;
            _tieneOffsetCuello = true;
            return;
        }
        float t = 1f - Mathf.Exp(-SuavizadoCuello * Time.deltaTime);
        _offsetCuello = Mathf.Lerp(_offsetCuello, medido, t);
    }

    float CalcularAlturaDeFlote(float surfaceY)
    {
        if (_tieneOffsetCuello)
            return surfaceY + cuelloSobreElAgua - _offsetCuello;
        return surfaceY - surfaceOffset;
    }

    bool ShouldEnter(float surfaceY)
    {
        SampleBody(out float feet, out float head, out float chest);
        if (chest + EnterBuffer >= surfaceY)
            return false;

        // En agua poco honda (más baja que la altura de flote) se camina, no se nada.
        if (TryGetSueloBajo(out float sueloY))
        {
            float profundidadFlote = surfaceY - CalcularAlturaDeFlote(surfaceY);
            if (surfaceY - sueloY < profundidadFlote + 0.1f)
                return false;
        }
        return true;
    }

    // Sale del agua cuando el suelo queda por encima de donde irían los pies al flotar.
    bool HaySueloParaPisar()
    {
        return TryGetSueloBajo(out float sueloY) && sueloY > _alturaFlote - 0.02f;
    }

    bool TryGetSueloBajo(out float sueloY)
    {
        sueloY = 0f;
        Vector3 origen = transform.position + Vector3.up * 0.5f;
        int hits = Physics.RaycastNonAlloc(origen, Vector3.down, _groundHits, AlcanceSondaFondo, _solidMask, QueryTriggerInteraction.Ignore);
        bool found = false;
        float mejor = float.MinValue;
        for (int i = 0; i < hits; ++i)
        {
            var col = _groundHits[i].collider;
            if (col == null || col.transform.IsChildOf(transform))
                continue;
            if (_groundHits[i].point.y > mejor)
            {
                mejor = _groundHits[i].point.y;
                found = true;
            }
        }
        if (found)
            sueloY = mejor;
        return found;
    }

    void SampleBody(out float feet, out float head, out float chest)
    {
        if (_capsule != null)
        {
            var bounds = _capsule.bounds;
            feet = bounds.min.y;
            head = bounds.max.y;
        }
        else
        {
            feet = transform.position.y;
            head = feet + 1.7f;
        }
        chest = Mathf.Lerp(feet, head, 0.75f);
    }

    // ── Animación ────────────────────────────────────────────────────────

    void ForceAnimatorGrounded()
    {
        _animator.SetBool(vAnimatorParameters.IsGrounded, true);
        _animator.SetFloat(vAnimatorParameters.GroundDistance, 0f);
    }

    // Durante la zambullida se mantiene la animación de caída de Invector.
    void ForceAnimatorAirborne()
    {
        _animator.SetBool(vAnimatorParameters.IsGrounded, false);
    }

    // ── Efectos ──────────────────────────────────────────────────────────

    void OndasAlNadar(float surfaceY)
    {
        if (Time.time < _proximaOnda || _rigidbody == null)
            return;

        var v = _rigidbody.linearVelocity;
        float velocidad = new Vector2(v.x, v.z).magnitude;
        bool nadando = velocidad > 0.6f;
        Vector3 punto = new Vector3(transform.position.x, surfaceY, transform.position.z);

        if (nadando)
        {
            _salpicadura.Onda(punto, Mathf.Lerp(0.5f, 0.8f, velocidad / SwimMoveSpeed));
            _salpicadura.Gotitas(punto + new Vector3(v.x, 0f, v.z).normalized * 0.15f, 2);
            _proximaOnda = Time.time + intervaloOndasNadando;
        }
        else
        {
            _salpicadura.Onda(punto, 0.45f);
            _proximaOnda = Time.time + intervaloOndasQuieto;
        }
    }

    void ReproducirSfx(string clave, float volumen, Vector3 posicion)
    {
        if (string.IsNullOrEmpty(clave) || AudioService.Instance == null)
            return;
        AudioService.Instance.PlaySFX(clave, volumen, posicion);
    }

    // ── Movimiento horizontal ────────────────────────────────────────────

    Vector3 FrenarEnZambullida(Vector3 vel, float dt)
    {
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
        Vector3 deseado = LeerDireccionDeseada() * (SwimMoveSpeed * ControlDuranteZambullida);
        horizontal = Vector3.MoveTowards(horizontal, deseado, FrenadoHorizontalZambullida * dt);
        vel.x = horizontal.x;
        vel.z = horizontal.z;
        return vel;
    }

    Vector3 ApplySwimHorizontalControl(Vector3 currentVelocity, float control)
    {
        Vector3 desired = LeerDireccionDeseada() * (SwimMoveSpeed * control);
        Vector3 horizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        horizontal = Vector3.MoveTowards(horizontal, desired, SwimAcceleration * Time.fixedDeltaTime);
        currentVelocity.x = horizontal.x;
        currentVelocity.z = horizontal.z;
        return currentVelocity;
    }

    Vector3 LeerDireccionDeseada()
    {
        if (_controls == null)
            return Vector3.zero;

        Vector2 moveInput = _controls.GamePlay.Move.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        Transform basis = GetReferenceBasis();

        Vector3 forward = basis.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;
        forward.Normalize();

        Vector3 right = basis.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
            right = new Vector3(forward.z, 0f, -forward.x);
        right.Normalize();

        Vector3 desired = forward * moveInput.y + right * moveInput.x;
        if (desired.sqrMagnitude > 1f)
            desired.Normalize();
        return desired;
    }

    Transform GetReferenceBasis()
    {
        if (_cameraTransform == null)
            CacheCameraTransform();
        return _cameraTransform != null ? _cameraTransform : transform;
    }

    void CacheCameraTransform()
    {
        var cam = Camera.main;
        if (cam != null)
            _cameraTransform = cam.transform;
    }

    // ── Detección del agua ───────────────────────────────────────────────

    bool CleanupWaterList()
    {
        bool any = false;
        for (int i = _waterVolumes.Count - 1; i >= 0; --i)
        {
            var col = _waterVolumes[i];
            if (!col)
            {
                _waterVolumes.RemoveAt(i);
                _waterSet.Remove(col);
            }
            else
            {
                any = true;
            }
        }
        return any;
    }

    bool TryGetWaterSurface(out float surfaceY)
    {
        surfaceY = 0f;
        if (TrySampleSurfaceWithRay(out surfaceY))
            return true;

        bool found = false;
        for (int i = _waterVolumes.Count - 1; i >= 0; --i)
        {
            var col = _waterVolumes[i];
            if (!col)
            {
                _waterVolumes.RemoveAt(i);
                _waterSet.Remove(col);
                continue;
            }

            float candidate = ResolveSurfaceHeight(col);
            if (!found || candidate > surfaceY)
            {
                surfaceY = candidate;
                found = true;
            }
        }
        return found;
    }

    bool TrySampleSurfaceWithRay(out float surfaceY)
    {
        surfaceY = 0f;
        if (_waterVolumes.Count == 0)
            return false;

        Vector3 origin = transform.position + Vector3.up * RaycastVerticalOffset;
        float maxDistance = Mathf.Max(0.5f, RaycastVerticalOffset + RaycastDepth);
        int hits = Physics.RaycastNonAlloc(origin, Vector3.down, _raycastHits, maxDistance, _waterMask, QueryTriggerInteraction.Collide);
        if (hits <= 0)
            return false;

        bool found = false;
        float best = float.MinValue;
        for (int i = 0; i < hits; ++i)
        {
            var hit = _raycastHits[i];
            if (!IsTrackedWaterCollider(hit.collider))
                continue;
            if (!found || hit.point.y > best)
            {
                best = hit.point.y;
                found = true;
            }
        }

        if (found)
            surfaceY = best;

        return found;
    }

    float ResolveSurfaceHeight(Collider col)
    {
        if (col == null)
            return _rememberedSurfaceY;

        switch (col)
        {
            case BoxCollider box:
                var topLocal = box.center + Vector3.up * (box.size.y * 0.5f);
                return col.transform.TransformPoint(topLocal).y;

            case CapsuleCollider capsule:
                var half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
                var capTop = capsule.center + Vector3.up * half;
                return col.transform.TransformPoint(capTop).y + capsule.radius;

            default:
                return col.bounds.max.y;
        }
    }

    void RegisterWater(Collider other)
    {
        if (!other || !_waterSet.Add(other))
            return;
        _waterVolumes.Add(other);
    }

    void UnregisterWater(Collider other)
    {
        if (!other) return;
        if (_waterSet.Remove(other))
            _waterVolumes.Remove(other);
    }

    bool IsWaterCollider(Collider other)
    {
        if (!other) return false;
        int mask = 1 << other.gameObject.layer;
        return (mask & _waterMask) != 0;
    }

    bool IsTrackedWaterCollider(Collider other) => other && _waterSet.Contains(other);

    void OnTriggerEnter(Collider other)
    {
        if (IsWaterCollider(other))
            RegisterWater(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (IsWaterCollider(other))
            UnregisterWater(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsWaterCollider(collision.collider))
            RegisterWater(collision.collider);
    }

    void OnCollisionExit(Collision collision)
    {
        if (IsWaterCollider(collision.collider))
            UnregisterWater(collision.collider);
    }

    void Log(string mensaje)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (debugLogs)
            Debug.Log("[PlayerSwimmingController] " + mensaje, this);
#endif
    }

    void OnDrawGizmosSelected()
    {
        if (_fase == Fase.Fuera) return;
        Gizmos.color = _fase == Fase.Zambullida ? Color.blue : Color.cyan;
        if (_fase == Fase.Saliendo)
            Gizmos.DrawWireSphere(_destinoSalida, 0.1f);
        Vector3 p = transform.position;
        Gizmos.DrawLine(new Vector3(p.x - 0.4f, _alturaFlote, p.z), new Vector3(p.x + 0.4f, _alturaFlote, p.z));
        Gizmos.DrawLine(new Vector3(p.x - 0.4f, _rememberedSurfaceY, p.z), new Vector3(p.x + 0.4f, _rememberedSurfaceY, p.z));
    }
}
