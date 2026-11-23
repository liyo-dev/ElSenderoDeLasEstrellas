using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

/// <summary>
/// Defensa del jugador en la B (INC-491/INC-493).
/// <list type="bullet">
/// <item>Pulsar B abre una ventana corta (<see cref="parryWindow"/>): un proyectil enemigo que
/// llegue en ella cambia de dueño y regresa más fuerte (contraataque, con parón de
/// impacto); un golpe cuerpo a cuerpo se anula, empuja al enemigo y el jugador se aparta con una
/// voltereta hacia atrás (INC-653).</item>
/// <item>El escudo sale en el mismo fotograma de pulsar B (pose de defensa, sin preparación) y se
/// queda mientras se mantenga (gasta maná): bloquea proyectiles y reduce el daño cuerpo a cuerpo.
/// Durante la ventana no para nada por su cuenta, para que lo que llegue se devuelva o se
/// desvíe. Pulsar tarde es, en la práctica, bloquear. Ver INC-1350.</item>
/// <item>Fallar la ventana deja un pequeño margen (<see cref="parryWhiffCooldown"/>) antes de poder
/// abrir otra, para que machacar B no valga.</item>
/// <item>El escudo tiene color (<see cref="ElementoDelEscudo"/>): Will, siempre Luz; Estela y Liam,
/// el elemento del rápido que llevan elegido. La magia de su mismo color lo rompe
/// (<see cref="RuptoresDeEscudo.Rompe"/>) y el personaje queda aturdido 2 s, sin poder hacer nada.
/// Lo de otro color lo para; lo que no es magia (rocas, golpes) nunca lo rompe. Devolver en el
/// momento justo vale con cualquier color, pero solo con lo que se puede devolver. Es la misma
/// regla que los escudos de los NPCs (INC-1357). Escudos de colores en los dos bandos, 20 nov.</item>
/// </list>
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerShieldController : MonoBehaviour
{
    [Header("Escudo")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private Vector3 shieldOffset = Vector3.zero;
    [Tooltip("Efecto en el punto del escudo donde se para un golpe (destello y onda). Vacío = sin efecto.")]
    [SerializeField] private GameObject golpeEnElEscudo;

    [Header("Animaciones")]
    [SerializeField] private string defendAnimation = "UpperBody.Defend_NoWeapon";
    [SerializeField] private string defendHitAnimation = "UpperBody.DefendHit_NoWeapon";
    [SerializeField] private string locomotionAnimation = "UpperBody.UpperIdle";
    [SerializeField] private int upperBodyLayer = 1;
    [SerializeField] private float upperBodyTransitionDuration = 0.1f;
    [SerializeField, Min(0f)] private float hitFeedbackDuration = 0.3f; // tiempo de feedback antes de volver a defensa

    [Header("Colisiones a bloquear")]
    [SerializeField] private string[] blockLayerNames = { "Enemy", "ProjectileEnemy" };

    [Header("Coste de magia")]
    [Tooltip("Maná por segundo que consume mantener el escudo activo. 0 = gratuito.")]
    [SerializeField] private float manaPerSecond = 10f;

    [Header("Contraataque (B en el momento justo)")]
    [Tooltip("Segundos tras pulsar B en los que un golpe enemigo se devuelve.")]
    [SerializeField, Range(0.05f, 0.5f)] private float parryWindow = 0.25f;
    [Tooltip("Radio mínimo de contacto que permite devolver incluso sin vuelo restante.")]
    [SerializeField, Min(0.1f)] private float parryRadius = 1.2f;
    [Tooltip("Tiempo máximo restante hasta el pecho para capturar la bola durante la ventana.")]
    [SerializeField, Min(0f)] private float segundosDeAtrapado = 0.3f;
    [Tooltip("Distancia lateral máxima al pecho para considerar que la bola se dirige al jugador.")]
    [SerializeField, Min(0.1f)] private float margenLateral = 1.2f;
    [Tooltip("Espera tras una ventana fallida antes de poder abrir otra.")]
    [SerializeField] private float parryWhiffCooldown = 0.45f;
    [Tooltip("Daño del contraataque respecto al del proyectil devuelto.")]
    [SerializeField] private float counterDamageMultiplier = 2f;
    [SerializeField] private float minCounterDamage = 20f;
    [Tooltip("Segundos que el jugador queda comprometido con el gesto del contraataque.")]
    [SerializeField] private float counterLockSeconds = 0.45f;
    [SerializeField] private GameObject counterVfx;
    [SerializeField] private string counterSfxKey = "EstelaAppears_ShieldBlock";

    [Header("Escudo de color (se rompe con la magia de su mismo color)")]
    [Tooltip("Segundos aturdido, sin poder hacer nada, al romperle el escudo (igual que los NPCs).")]
    [SerializeField, Min(0f)] private float segundosAturdidoAlRomperse = 2f;
    [Tooltip("Estado de cuerpo entero (capa base) mientras está aturdido. Vacío o inexistente = sin animación.")]
    [SerializeField] private string estadoDeAturdido = "Dizzy_NoWeapon";
    [SerializeField] private string estadoDeLocomocion = "Free Locomotion";
    [Tooltip("Efecto al romperse el escudo (vacío = el golpe del escudo, más grande).")]
    [SerializeField] private GameObject alRomperse;
    [SerializeField] private string sonidoAlRomperse = "EstelaAppears_ShieldBlock";
    [SerializeField, Min(0f)] private float sacudidaAlRomperse = 0.45f;

    [Header("Cuerpo a cuerpo")]
    [Tooltip("Parte del daño cuerpo a cuerpo que pasa con el escudo levantado.")]
    [SerializeField, Range(0f, 1f)] private float meleeBlockFactor = 0.3f;
    [Tooltip("Empuje al enemigo que golpea cuerpo a cuerpo dentro de la ventana.")]
    [SerializeField] private float meleeDeflectPush = 8f;
    [SerializeField] private float meleeDeflectRadius = 3f;

    [Header("Voltereta al desviar cuerpo a cuerpo (INC-653)")]
    [Tooltip("Al anular un golpe cuerpo a cuerpo, el jugador se aparta con una voltereta hacia atrás.")]
    [SerializeField] private bool volteretaAlDesviar = true;
    [Tooltip("Altura de la voltereta, en alturas de la cabeza del personaje.")]
    [SerializeField, Min(0f)] private float alturaDeLaVoltereta = 0.8f;
    [Tooltip("Metros que se aparta del enemigo (menos si hay una pared detrás).")]
    [SerializeField, Min(0f)] private float distanciaDeLaVoltereta = 2.5f;

    private PlayerControls _controls;
    private bool _ownsControls;
    private Animator _animator;
    private GameObject _shieldInstance;
    private readonly HashSet<int> _blockedLayers = new();
    private bool _isDefending;
    private int _playerLayer;
    private float _originalUpperBodyWeight;
    private MagicCaster _magicCaster;
    private VolteretaDelJugador _voltereta;
    private EsquivaDelJugador _esquiva;
    private ContraataqueCoreografiado _coreografia;
    private Invector.vCharacterController.vThirdPersonController _controller;
    private ManaPool _manaPool;
    private PlayerActionManager _playerActionManager;

    private bool _bWasDown;
    private bool _sinManaHastaSoltar; // ya avisó de que no queda maná: no reintenta hasta soltar B
    private float _parryUntil = -1f;
    private bool _parryOpen;
    private bool _parried;
    private float _parryCooldownUntil = -1f;
    private int _deflectMask;
    private readonly Collider[] _parryBuffer = new Collider[16];


    private float _fundidoHasta;
    private float _pesoAlSalir;

    // Escudo de color y aturdimiento al romperse.
    private Renderer[] _renderersDelEscudo;
    private bool _pintado;
    private MagicElement _colorPintado;
    private float _aturdidoHasta = -1f;
    private bool _modoAturdido;
    private bool _rotoHastaSoltar;
    private GameObject _ultimoRecibido;
    private CuerpoDelGrupo _cuerpo;
    private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    private static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public bool IsDefending => _isDefending;

    /// <summary>Le acaban de romper el escudo y no puede hacer nada (2 s).</summary>
    public bool Aturdido => Time.time < _aturdidoHasta;

    /// <summary>
    /// Color (elemento) del escudo ahora: Will, siempre Luz; Estela y Liam, el del rápido elegido
    /// en la X (cambia con LB).
    /// </summary>
    public MagicElement ElementoDelEscudo
    {
        get
        {
            if (PersonajeDelCuerpo() == PartyControlManager.CharacterSlot.Will) return MagicElement.Light;
            var rapido = _magicCaster != null ? _magicCaster.ActiveBasic : null;
            return rapido != null ? rapido.element : MagicElement.Light;
        }
    }

    /// <summary>Ha cambiado el color del escudo (para el HUD).</summary>
    public event System.Action<MagicElement> AlCambiarElColor;

    /// <summary>Le han roto el escudo con la magia de su color (para avisos, lecciones y el HUD).</summary>
    public static event System.Action<PlayerShieldController, MagicElement> AlRomperseElEscudo;

    /// <summary>El escudo del jugador al que pertenece esa parte (vida, collider…), o null.</summary>
    public static PlayerShieldController De(Component parte)
    {
        if (parte == null) return null;
        var escudo = parte.GetComponentInParent<PlayerShieldController>();
        if (escudo == null) escudo = parte.GetComponentInChildren<PlayerShieldController>();
        if (escudo == null) escudo = parte.transform.root.GetComponentInChildren<PlayerShieldController>();
        return escudo;
    }
    public bool PuedeAbrirContraataque => isActiveAndEnabled && Time.time >= _parryCooldownUntil &&
        (_magicCaster == null || !_magicCaster.IsCasting) && (_playerActionManager == null || _playerActionManager.AllowShield);
    public float SegundosDeAtrapado => segundosDeAtrapado;
    public Vector3 CentroDeAtrapado => transform.position + Vector3.up;
    public float TiempoHastaElPecho(IAmenazaEntrante amenaza) =>
        AmenazasAlJugador.SegundosHastaAlcanzar(amenaza, CentroDeAtrapado, parryRadius, margenLateral);

    /// <summary>Segundos que dura la ventana de contraataque tras pulsar B.</summary>
    public float VentanaDeContraataque => parryWindow
        + (RasgosDePrenda.AlMandoLleva(RasgoDePrenda.ContraataqueMasAmplio) ? RasgosDePrenda.VentanaExtraDeContraataque : 0f);

    /// <summary>La ventana de contraataque está abierta.</summary>
    public bool IsParryWindowOpen => Time.time < _parryUntil;

    /// <summary>Contraataque o desvío logrado (para el HUD y la guía de combate).</summary>
    public event System.Action OnCounter;

    /// <summary>
    /// Contraataque o desvío logrado, con el punto del choque. Lo presenta
    /// PresentacionDelContraataque (congelado, cámara lenta, destello, vibración). Ver INC-670.
    /// </summary>
    public event System.Action<Vector3> AlContraatacar;

    /// Se ha devuelto un hechizo enemigo: quién lo había lanzado (null si no se sabe). Para que un
    /// jefe reaccione cuando le devuelven su propio ataque (el Mago Oscuro se aturde). Ver INC-509.
    public static event System.Action<GameObject> AlDevolverAtaque;

    /// <summary>Prefab de la burbuja del escudo (lo usan también los escudos elementales de los NPCs).</summary>
    public GameObject PrefabDelEscudo => shieldPrefab;

    /// Se ha parado con el escudo mantenido un proyectil enemigo (sin devolverlo): quién lo había
    /// lanzado (null si no se sabe). Para lecciones y reacciones al «te he parado».
    public static event System.Action<GameObject> AlPararAtaque;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsDevolver() { AlDevolverAtaque = null; AlPararAtaque = null; AlRomperseElEscudo = null; s_avisosDeRotura = 0; }
#endif

    void Awake()
    {
        _controls = Core.PlayerInputManager.GetSharedOrNew(out _ownsControls);
        _animator = GetComponent<Animator>();
        _playerLayer = gameObject.layer;
        CacheUpperBodyWeight();
        CacheBlockedLayers();
        _magicCaster = GetComponentInParent<MagicCaster>();
        _voltereta = GetComponentInParent<VolteretaDelJugador>();
        _esquiva = GetComponentInParent<EsquivaDelJugador>();
        _coreografia = GetComponentInParent<ContraataqueCoreografiado>();
        _controller = GetComponentInParent<Invector.vCharacterController.vThirdPersonController>();
        _manaPool = GetComponentInParent<ManaPool>();
        _playerActionManager = GetComponentInParent<PlayerActionManager>();

        _deflectMask = LayerMask.GetMask("Enemy", "Boss");
        CreateShieldInstance();
    }

    void Start()
    {
        // El filtro de daño va en el objeto de la vida del jugador, esté donde esté en la jerarquía.
        PlayerHealthSystem health = GetComponentInParent<PlayerHealthSystem>() ?? GetComponentInChildren<PlayerHealthSystem>();
        if (health == null) PlayerService.TryGetComponent(out health, allowSceneLookup: false);
        if (health != null)
        {
            var filtro = health.GetComponent<FiltroDeDefensa>() ?? health.gameObject.AddComponent<FiltroDeDefensa>();
            filtro.Owner = this;
        }
    }

    void OnEnable()
    {
        if (_controls == null) return;

        if (_ownsControls)
            _controls.Enable();
    }

    void OnDisable()
    {
        TerminarAturdimiento();
        CloseParryWindow();
        StopDefending();
        CancelInvoke();
        _bWasDown = false;
        _sinManaHastaSoltar = false;
        _fundidoHasta = 0f;
        ResetUpperBodyWeight();
        if (_controls != null && _ownsControls) _controls.Disable();
    }

    void Update()
    {
        if (_controls == null)
            return;

        bool bDown = GamepadInputReader.AttackMagicRightHeld;
        bool bPressed = bDown && !_bWasDown;
        _bWasDown = bDown;
        if (!bDown) _sinManaHastaSoltar = false;

        // Escudo roto: aturdido, sin guardia. Al acabar, no se levanta solo: hay que volver a pulsar.
        if (_modoAturdido || Aturdido)
        {
            CloseParryWindow();
            StopDefending();
            if (!Aturdido) TerminarAturdimiento();
            else return;
        }
        if (!bDown) _rotoHastaSoltar = false;

        if (!GameState.CanProcessGameplayInput)
        {
            CloseParryWindow();
            StopDefending();
            return;
        }

        // Tecleando un combo (Y), la B es un botón de la secuencia (INC-494).
        if (ComboCastController.IsComposing)
        {
            CloseParryWindow();
            StopDefending();
            return;
        }

        // Rodando (esquiva) o en la coreografía del contraataque no hay guardia; al acabar, si B
        // sigue pulsado, vuelve sola. Ver INC-1349 e INC-1352.
        if ((_esquiva != null && _esquiva.EnCurso) || (_coreografia != null && _coreografia.EnCurso))
        {
            CloseParryWindow();
            StopDefending();
            return;
        }

        if (_fundidoHasta > 0f)
        {
            float k = Mathf.Clamp01(1f - (_fundidoHasta - Time.time) / Mathf.Max(0.01f, upperBodyTransitionDuration));
            SetUpperBodyWeight(Mathf.Lerp(_pesoAlSalir, _originalUpperBodyWeight, k));
            if (k >= 1f) _fundidoHasta = 0f;
        }
        if (bPressed && PuedeAbrirContraataque && !_rotoHastaSoltar) OpenParryWindow();
        if (IsParryWindowOpen) ScanForParry();
        else if (_parryOpen) CloseParryWindow();

        if (_magicCaster != null && _magicCaster.IsCasting)
        {
            StopDefending();
            return;
        }

        // Drenar maná mientras el escudo está activo; desactivar si se agota
        if (_isDefending && _manaPool != null && manaPerSecond > 0f)
        {
            if (!_manaPool.TrySpend(manaPerSecond * Time.deltaTime))
            {
                AvisarSinMana();
                return;
            }
        }

        EvaluateDefenseState();
    }

    // ── Contraataque ────────────────────────────────────────────────────────

    private void OpenParryWindow()
    {
        if (Time.time < _parryCooldownUntil) return;
        if (_playerActionManager != null && !_playerActionManager.AllowShield) return;
        _parryUntil = Time.time + VentanaDeContraataque;
        _parryOpen = true;
        _parried = false;
        _fundidoHasta = 0f;
        if (TieneManaParaElEscudo() && !_sinManaHastaSoltar) StartDefending();
        ScanForParry();
    }

    private void CloseParryWindow()
    {
        if (!_parryOpen) return;
        _parryOpen = false;
        _parryUntil = -1f;
        if (!_parried)
            _parryCooldownUntil = Time.time + parryWhiffCooldown;
    }

    private void ScanForParry()
    {
        var proyectiles = AmenazasAlJugador.Proyectiles;
        for (int i = proyectiles.Count - 1; i >= 0; i--)
        {
            if (!(proyectiles[i] is IDevolvible bola) || bola.TipoDeAmenaza != TipoDeAmenaza.Devolvible ||
                TiempoHastaElPecho(bola) > segundosDeAtrapado) continue;
            Transform objetivo = bola is EnemyProjectile proyectil ? proyectil.ObjetivoFijoDeDevolucion : null;
            if (objetivo == null && _magicCaster != null) _magicCaster.TryGetTarget(out objetivo);
            if (objetivo == null && bola.Lanzador != null) objetivo = bola.Lanzador.transform;
            Vector3 direccion = -bola.VelocidadDeAmenaza;
            Vector3 punto = bola.PosicionDeAmenaza;
            GameObject lanzador = bola.Lanzador;
            float dano = Mathf.Max(minCounterDamage, bola.DanoEntrante * counterDamageMultiplier);
            _fundidoHasta = 0f;
            CancelInvoke(nameof(ReturnToDefendAnimation));
            StopDefending();
            bola.Devolver(gameObject, objetivo, direccion, dano);
            Vector3 facing = objetivo != null ? objetivo.position - transform.position : direccion;
            // Con coreografía, la bola se queda sostenida y sale al final de ella (INC-1352); si no
            // se puede (en el aire, sin coreografía), sale ya con el gesto corto de siempre.
            var devuelta = bola is Component componenteDeLaBola ? componenteDeLaBola.GetComponent<ProyectilDevuelto>() : null;
            bool coreografiado = _coreografia != null && devuelta != null && _coreografia.Empezar(devuelta, objetivo, facing);
            if (!coreografiado && _magicCaster != null) _magicCaster.BeginCounterGesture(counterLockSeconds, facing);
            CounterFeedback(punto);
            AlDevolverAtaque?.Invoke(lanzador);
            CloseParryWindow();
            break;
        }
    }
    /// <summary>
    /// Un golpe cuerpo a cuerpo ha llegado dentro de la ventana: se anula, empuja al enemigo y el
    /// jugador se aparta de él con una voltereta (en el aire, solo la voltereta).
    /// </summary>
    internal void OnMeleeDeflect()
    {
        Vector3 center = transform.position + Vector3.up;
        Vector3 alejarse = -transform.forward;
        float masCerca = float.MaxValue;
        int count = Physics.OverlapSphereNonAlloc(center, meleeDeflectRadius, _parryBuffer, _deflectMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Vector3 desdeEnemigo = transform.position - _parryBuffer[i].transform.position; desdeEnemigo.y = 0f;
            if (desdeEnemigo.sqrMagnitude > 0.0001f && desdeEnemigo.sqrMagnitude < masCerca)
            {
                masCerca = desdeEnemigo.sqrMagnitude;
                alejarse = desdeEnemigo;
            }

            var rb = _parryBuffer[i].attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;
            Vector3 dir = rb.position - transform.position; dir.y = 0f;
            rb.AddForce(dir.normalized * meleeDeflectPush, ForceMode.VelocityChange);
        }
        StopDefending();
        CounterFeedback(center + transform.forward * 0.8f);
        CloseParryWindow();
        VolteretaDeDesvio(alejarse);
    }

    private void VolteretaDeDesvio(Vector3 alejarse)
    {
        if (!volteretaAlDesviar || _voltereta == null || _voltereta.EnCurso) return;
        alejarse.y = 0f;
        if (alejarse.sqrMagnitude < 0.0001f) alejarse = -transform.forward;
        alejarse.Normalize();

        if (_controller != null && _controller.IsAirborne) _voltereta.EnElAire();
        else _voltereta.DesdeElSuelo(alturaDeLaVoltereta, alejarse * distanciaDeLaVoltereta);
    }

    private void CounterFeedback(Vector3 where)
    {
        _parried = true;
        if (counterVfx != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(counterVfx, where, Quaternion.identity, 0.6f);
        if (!string.IsNullOrEmpty(counterSfxKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(counterSfxKey);

        OnCounter?.Invoke();
        AlContraatacar?.Invoke(where);
    }

    /// <summary>Parte del daño que llega al jugador según la defensa (lo usa FiltroDeDefensa).</summary>
    internal float FilterIncomingDamage(float amount)
    {
        if (IsParryWindowOpen)
        {
            OnMeleeDeflect();
            return 0f;
        }
        return _isDefending ? amount * meleeBlockFactor : amount;
    }

    private void EvaluateDefenseState()
    {
        // El escudo está mientras se mantenga B y, con un toque, al menos lo que dura la ventana.
        bool wantsDefense = GamepadInputReader.AttackMagicRightHeld || IsParryWindowOpen;

        bool hasMana = TieneManaParaElEscudo();
        bool isAllowed = _playerActionManager == null || _playerActionManager.AllowShield;

        if (wantsDefense && !hasMana)
        {
            AvisarSinMana();
            return;
        }

        if (wantsDefense && isAllowed && !_sinManaHastaSoltar && !_rotoHastaSoltar)
        {
            StartDefending();
            PintarEscudo();
        }
        else
            StopDefending();
    }

    /// <summary>
    /// El escudo se apaga porque no queda maná: se suelta la pose una sola vez y se avisa con la
    /// misma respuesta que la magia sin maná (chispas, sonido, cara y barra que tiembla). No se
    /// reintenta mientras no se suelte B: si no, al empezar a regenerar volvía a subir y bajar la
    /// guardia cada fotograma y la animación se quedaba a medias.
    /// </summary>
    private void AvisarSinMana()
    {
        if (_sinManaHastaSoltar) return;
        _sinManaHastaSoltar = true;
        StopDefending();
        if (_magicCaster != null)
            _magicCaster.AvisarFaltaDeManaDeEscudo();
        else if (Sendero.UI.PlayerHUDV2.Instance != null)
            Sendero.UI.PlayerHUDV2.Instance.AvisarFaltaDeMana();
    }

    private void StartDefending()
    {
        if (_isDefending)
            return;

        if (_magicCaster != null && _magicCaster.IsCasting)
            return;

        if (_playerActionManager != null && !_playerActionManager.AllowShield)
            return;

        _isDefending = true;
        _fundidoHasta = 0f;
        ActivateShield();
        SetUpperBodyWeight(1f);
        PlayUpperBodyAnimation(defendAnimation);
    }

    private void StopDefending()
    {
        if (!_isDefending) return;
        _isDefending = false;
        DeactivateShield();
        CancelInvoke(nameof(ReturnToDefendAnimation));
        VolverALaLocomocion();
    }

    private bool TieneManaParaElEscudo() =>
        _manaPool == null || manaPerSecond <= 0f || _manaPool.Current > 0f;

    /// Suelta la pose de defensa con un fundido corto de la capa de brazos.
    private void VolverALaLocomocion()
    {
        PlayUpperBodyAnimation(locomotionAnimation);
        _pesoAlSalir = _animator != null && upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount ? _animator.GetLayerWeight(upperBodyLayer) : 0f;
        _fundidoHasta = Time.time + Mathf.Max(0.01f, upperBodyTransitionDuration);
    }
    /// <summary>
    /// Instancia el escudo una única vez (en Awake) en lugar de crearlo/destruirlo
    /// en cada activación — el jugador puede alternar el escudo muy seguido en combate.
    /// </summary>
    private void CreateShieldInstance()
    {
        if (shieldPrefab == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerShieldController] shieldPrefab no asignado, no se puede instanciar el escudo.");
#endif
            return;
        }

        _shieldInstance = Instantiate(shieldPrefab, transform);
        _shieldInstance.transform.localPosition = shieldOffset;
        _shieldInstance.transform.localRotation = Quaternion.identity;
        _renderersDelEscudo = _shieldInstance.GetComponentsInChildren<Renderer>(true);

        ConfigureShieldDetector(_shieldInstance);
        _shieldInstance.SetActive(false);
    }

    private void ActivateShield()
    {
        PintarEscudo();
        if (_shieldInstance != null && !_shieldInstance.activeSelf)
            _shieldInstance.SetActive(true);
    }

    // ── Escudo de color ─────────────────────────────────────────────────────

    private PartyControlManager.CharacterSlot PersonajeDelCuerpo()
    {
        if (_cuerpo == null) _cuerpo = GetComponentInParent<CuerpoDelGrupo>();
        if (_cuerpo != null) return _cuerpo.Personaje;
        var grupo = PartyControlManager.Instance;
        return grupo != null ? grupo.ActiveSlot : PartyControlManager.CharacterSlot.Will;
    }

    /// Tiñe la burbuja con el color de su elemento (solo si ha cambiado).
    private void PintarEscudo()
    {
        if (_renderersDelEscudo == null) return;
        MagicElement elemento = ElementoDelEscudo;
        if (_pintado && elemento == _colorPintado) return;
        _pintado = true;
        _colorPintado = elemento;
        PintarConElemento(_renderersDelEscudo, elemento);
        AlCambiarElColor?.Invoke(elemento);
    }

    /// <summary>
    /// Tiñe una burbuja fina (shader de energía: brillo, cuerpo y borde) con el color de un
    /// elemento. Sirve para el escudo y para la burbuja del contraataque.
    /// </summary>
    public static void PintarConElemento(Renderer[] renderers, MagicElement elemento)
    {
        if (renderers == null) return;
        Color c = RuptoresDeEscudo.ColorDe(elemento);
        Color brillo = Color.Lerp(Color.white, c, 0.35f) * 2.3f;
        Color cuerpo = c * 1.9f;
        Color borde = Color.Lerp(c, Color.white, 0.35f) * 2.3f;
        brillo.a = cuerpo.a = borde.a = 1f;
        var bloque = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            r.GetPropertyBlock(bloque);
            bloque.SetColor(CoreColorId, brillo);
            bloque.SetColor(MidColorId, cuerpo);
            bloque.SetColor(RimColorId, borde);
            bloque.SetColor(BaseColorId, c);
            bloque.SetColor(ColorId, c);
            r.SetPropertyBlock(bloque);
        }
    }

    /// <summary>
    /// Algo hostil ha llegado al escudo (a la burbuja o, por proximidad, al cuerpo con el escudo
    /// puesto). True si el escudo se ocupa de ello (lo para o se rompe): quien llama lo destruye.
    /// False si no hay escudo, o si está abierta la ventana del contraataque y se puede devolver
    /// (lo devuelve la B, ScanForParry).
    /// </summary>
    public bool RecibirEnElEscudo(GameObject golpe, Vector3 desde)
    {
        if (golpe == null || !_isDefending || Aturdido) return false;
        var enemigo = golpe.GetComponentInParent<EnemyProjectile>();
        bool noDevolvible = enemigo != null && enemigo.TipoDeAmenaza != TipoDeAmenaza.Devolvible;
        if (IsParryWindowOpen && !noDevolvible) return false;

        GameObject quien = enemigo != null ? enemigo.gameObject : golpe;
        if (quien == _ultimoRecibido) return true; // ya contado (burbuja y proyectil se ven los dos)
        _ultimoRecibido = quien;

        if (EsMagia(golpe, enemigo, out MagicElement elemento) && RuptoresDeEscudo.Rompe(elemento, ElementoDelEscudo))
        {
            Romperse(desde, elemento);
            return true;
        }
        OnShieldHit(desde);
        AlPararAtaque?.Invoke(enemigo != null ? enemigo.Lanzador : null);
        return true;
    }

    private static bool EsMagia(GameObject golpe, EnemyProjectile enemigo, out MagicElement elemento)
    {
        if (enemigo != null) return enemigo.TryGetElemento(out elemento);
        var magia = golpe.GetComponentInParent<MagicProjectile>();
        if (magia != null) { elemento = magia.Elemento; return true; }
        elemento = default;
        return false;
    }

    /// El escudo se rompe: fuera la guardia y aturdido unos segundos, sin poder hacer nada.
    private void Romperse(Vector3 desde, MagicElement elemento)
    {
        CloseParryWindow();
        StopDefending();
        _fundidoHasta = 0f;
        _rotoHastaSoltar = true;

        Vector3 centro = transform.position + Vector3.up * 0.95f;
        var efecto = alRomperse != null ? alRomperse : golpeEnElEscudo;
        if (efecto != null && VfxPoolService.Instance != null)
        {
            Vector3 dir = desde - centro;
            var t = VfxPoolService.Instance.Play(efecto, centro, Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir : transform.forward), 0.8f);
            if (t != null && alRomperse == null) t.localScale *= 1.8f;
        }
        if (!string.IsNullOrEmpty(sonidoAlRomperse) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sonidoAlRomperse);
        if (sacudidaAlRomperse > 0f) Sendero.Core.Feedback.FeedbackService.CameraShake(sacudidaAlRomperse, 0.25f);

        if (segundosAturdidoAlRomperse > 0f)
        {
            _aturdidoHasta = Time.time + segundosAturdidoAlRomperse;
            if (!_modoAturdido && _playerActionManager != null)
            {
                _playerActionManager.PushMode(ActionMode.Stunned);
                _modoAturdido = true;
            }
            if (_controller == null || !_controller.IsAirborne) ReproducirCuerpoEntero(estadoDeAturdido, 0.12f);
        }
        AlRomperseElEscudo?.Invoke(this, elemento);
        AvisarDeLaRegla(elemento);
    }

    private static int s_avisosDeRotura;

    /// Las primeras veces, el rótulo de abajo cuenta la regla: se rompe con su mismo color.
    private static void AvisarDeLaRegla(MagicElement elemento)
    {
        if (s_avisosDeRotura >= 3 || HudToastService.Instance == null) return;
        s_avisosDeRotura++;
        string L(string clave, string porDefecto) =>
            LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, porDefecto) : porDefecto;
        string nombre = elemento switch
        {
            MagicElement.Fire => L("ELEMENT_FIRE", "Fuego"),
            MagicElement.Storm => L("ELEMENT_STORM", "Viento"),
            MagicElement.Light => L("ELEMENT_LIGHT", "Luz"),
            MagicElement.Mind => L("ELEMENT_MIND", "Mente"),
            _ => L("ELEMENT_DARK", "Oscuro"),
        };
        HudToastService.Instance.ShowRaw(string.Format(L("SHIELD_BROKEN_SAME_COLOR",
            "¡Escudo roto! Era de {0}: lo rompe la magia de su mismo color. Cambia de personaje o de rápido."), nombre), 3.5f);
    }

    private void TerminarAturdimiento()
    {
        _aturdidoHasta = -1f;
        if (!_modoAturdido) return;
        _modoAturdido = false;
        if (_playerActionManager != null) _playerActionManager.PopMode(ActionMode.Stunned);
        // El mareo no tiene salida propia: se vuelve a la locomoción a mano.
        if (_animator != null && _animator.isActiveAndEnabled && !string.IsNullOrEmpty(estadoDeAturdido) &&
            _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash(estadoDeAturdido))
            ReproducirCuerpoEntero(estadoDeLocomocion, 0.2f);
    }

    private void ReproducirCuerpoEntero(string estado, float fundido)
    {
        if (_animator == null || !_animator.isActiveAndEnabled || string.IsNullOrEmpty(estado)) return;
        int hash = Animator.StringToHash(estado);
        if (_animator.HasState(0, hash)) _animator.CrossFadeInFixedTime(hash, fundido, 0);
    }

    private void DeactivateShield()
    {
        if (_shieldInstance != null && _shieldInstance.activeSelf)
            _shieldInstance.SetActive(false);
    }

    private void PlayUpperBodyAnimation(string animationName)
    {
        if (_animator == null || string.IsNullOrEmpty(animationName)) return;

        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
            _animator.CrossFade(animationName, upperBodyTransitionDuration, upperBodyLayer);
        else
            _animator.CrossFade(animationName, upperBodyTransitionDuration, 0);
    }

    private void CacheUpperBodyWeight()
    {
        _originalUpperBodyWeight = 0f;

        if (_animator == null)
            return;

        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
            _originalUpperBodyWeight = _animator.GetLayerWeight(upperBodyLayer);
    }

    private void SetUpperBodyWeight(float weight)
    {
        if (_animator == null)
            return;

        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
            _animator.SetLayerWeight(upperBodyLayer, weight);
    }

    private void ResetUpperBodyWeight()
    {
        SetUpperBodyWeight(_originalUpperBodyWeight);
    }

    private void CacheBlockedLayers()
    {
        _blockedLayers.Clear();
        foreach (var name in blockLayerNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0)
                _blockedLayers.Add(layer);
            else
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[PlayerShieldController] No se encontró la capa '{name}'.");
                #endif
            }
        }
    }

    private void ConfigureShieldDetector(GameObject shield)
    {
        if (!shield.TryGetComponent<Collider>(out var collider))
        {
            collider = shield.AddComponent<SphereCollider>();
            collider.isTrigger = true;
        }
        else
        {
            collider.isTrigger = true;
        }

        // No se requiere Rigidbody para el escudo

        // Marcador público para que proyectiles identifiquen el escudo explícitamente
        if (!shield.TryGetComponent<ShieldMarker>(out var _))
            shield.AddComponent<ShieldMarker>();

        var detector = shield.GetComponent<ShieldHitDetector>() ?? shield.AddComponent<ShieldHitDetector>();
        detector.Initialize(this, _blockedLayers);
    }

    internal void OnShieldHit(Vector3 desde)
    {
        if (_isDefending && golpeEnElEscudo != null && VfxPoolService.Instance != null && _shieldInstance != null)
        {
            // Sobre la superficie de la burbuja, en la dirección por la que llega el golpe.
            Vector3 centro = _shieldInstance.transform.position + Vector3.up * 0.95f;
            Vector3 dir = desde - centro;
            float radio = _shieldInstance.TryGetComponent(out SphereCollider esfera)
                ? esfera.radius * _shieldInstance.transform.lossyScale.x : 1.3f;
            Vector3 punto = dir.sqrMagnitude > 0.0001f ? centro + dir.normalized * radio : centro;
            VfxPoolService.Instance.Play(golpeEnElEscudo, punto, Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir : transform.forward), 0.6f);
        }

        // Reproduce animación de impacto breve en la capa superior y vuelve a defensa.
        if (!_isDefending || string.IsNullOrEmpty(defendHitAnimation)) return;
        PlayUpperBodyAnimation(defendHitAnimation);
        if (hitFeedbackDuration > 0f)
        {
            // Reinicia el temporizador ante impactos consecutivos
            CancelInvoke(nameof(ReturnToDefendAnimation));
            Invoke(nameof(ReturnToDefendAnimation), hitFeedbackDuration);
        }
    }

    private void ReturnToDefendAnimation()
    {
        if (_isDefending)
            PlayUpperBodyAnimation(defendAnimation);
    }

    private class ShieldHitDetector : MonoBehaviour
    {
        private PlayerShieldController _owner;
        private HashSet<int> _blockedLayers;
        private int _projectileEnemyLayer;
        private int _projectileLayer;

        public void Initialize(PlayerShieldController owner, HashSet<int> blockedLayers)
        {
            _owner = owner;
            _blockedLayers = blockedLayers;
            // Resolver capas en runtime (no en constructor/initializer)
            _projectileEnemyLayer = LayerMask.NameToLayer("ProjectileEnemy");
            _projectileLayer = LayerMask.NameToLayer("Projectile");
        }

        void OnTriggerEnter(Collider other)
        {
            HandleHit(other);
        }

        void OnCollisionEnter(Collision collision)
        {
            HandleHit(collision.collider);
        }

        private void HandleHit(Collider col)
        {
            if (_owner == null || _blockedLayers == null)
                return;

            var go = GetRootRigidbodyOrSelf(col);
            int layer = go.layer;
            if (!_blockedLayers.Contains(layer)) return;

            // Lo lanzado: lo para o, si es magia de su color, se rompe (RecibirEnElEscudo). En la
            // ventana del contraataque, lo devolvible se deja pasar para que lo devuelva la B.
            if (IsProjectile(go))
            {
                if (!_owner.RecibirEnElEscudo(go, col.bounds.center)) return;
                var enemigo = go.GetComponentInParent<EnemyProjectile>();
                if (enemigo != null) enemigo.DestroyProjectile();
                else SafeDestroy(go);
                return;
            }

            // Un cuerpo enemigo contra la burbuja; en la ventana, el desvío lo hace FilterIncomingDamage.
            if (_owner.IsParryWindowOpen) return;
            _owner.OnShieldHit(col.bounds.center);
        }

        private static GameObject GetRootRigidbodyOrSelf(Collider col)
        {
            return col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
        }

        private bool IsProjectile(GameObject go)
        {
            if (go == null) return false;

            // Component known for enemy projectiles
            if (go.GetComponentInParent<EnemyProjectile>() != null) return true;

            // Common layers for projectiles
            if (go.layer == _projectileEnemyLayer || go.layer == _projectileLayer) return true;

            return false;
        }

        private static void SafeDestroy(GameObject go)
        {
            if (!go) return;
            Object.Destroy(go);
        }
    }

    // Componente público y ligero para identificar el escudo en colisiones externas
    public class ShieldMarker : MonoBehaviour {}
}
