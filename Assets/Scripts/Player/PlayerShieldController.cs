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
/// <item>Mantener B = escudo (gasta maná): bloquea proyectiles y reduce el daño cuerpo a cuerpo.
/// Pulsar tarde es, en la práctica, bloquear.</item>
/// <item>Fallar la ventana deja un pequeño margen (<see cref="parryWhiffCooldown"/>) antes de poder
/// abrir otra, para que machacar B no valga.</item>
/// </list>
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerShieldController : MonoBehaviour
{
    [Header("Escudo")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private Vector3 shieldOffset = Vector3.zero;

    [Header("Animaciones")]
    [SerializeField] private string preparacionAnimation = "UpperBody.Magic.HumanM@MagicAttackDirect2H01 - Load";
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
    private Invector.vCharacterController.vThirdPersonController _controller;
    private ManaPool _manaPool;
    private PlayerActionManager _playerActionManager;

    private bool _bWasDown;
    private float _parryUntil = -1f;
    private bool _parryOpen;
    private bool _parried;
    private float _parryCooldownUntil = -1f;
    private float _pulsadoEn;
    private int _deflectMask;
    private readonly Collider[] _parryBuffer = new Collider[16];


    private bool _preparando;
    private float _fundidoHasta;
    private float _pesoAlSalir;
    public bool IsDefending => _isDefending;
    public bool PuedeAbrirContraataque => isActiveAndEnabled && Time.time >= _parryCooldownUntil &&
        (_magicCaster == null || !_magicCaster.IsCasting) && (_playerActionManager == null || _playerActionManager.AllowShield);
    public float SegundosDeAtrapado => segundosDeAtrapado;
    public Vector3 CentroDeAtrapado => transform.position + Vector3.up;
    public float TiempoHastaElPecho(IAmenazaEntrante amenaza) =>
        AmenazasAlJugador.SegundosHastaAlcanzar(amenaza, CentroDeAtrapado, parryRadius, margenLateral);

    /// <summary>Segundos que dura la ventana de contraataque tras pulsar B.</summary>
    public float VentanaDeContraataque => parryWindow;

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

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsDevolver() => AlDevolverAtaque = null;
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

        UnlockService.OnSpellUnlocked += OnSpellUnlocked;
    }

    void OnDisable()
    {
        UnlockService.OnSpellUnlocked -= OnSpellUnlocked;

        CloseParryWindow();
        StopDefending();
        SalirDePreparacion();
        CancelInvoke();
        _bWasDown = false;
        _fundidoHasta = 0f;
        ResetUpperBodyWeight();
        if (_controls != null && _ownsControls) _controls.Disable();
    }

    // INC-078: la proteccion contra magia enemiga se desbloquea en el mismo momento en que
    // se desbloquea Bola Prisma (justo al ganar la batalla con Erika), sin tener que tocar
    // los campos serializados del nodo UnlockAbilitiesNode del grafo.
    private void OnSpellUnlocked(SpellId spell)
    {
        if (spell == SpellId.Plasmaball)
            UnlockService.UnlockAbility(AbilityKey.Shield);
    }

    void Update()
    {
        if (_controls == null)
            return;

        bool bDown = GamepadInputReader.AttackMagicRightHeld;
        bool bPressed = bDown && !_bWasDown;
        _bWasDown = bDown;

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

        if (_fundidoHasta > 0f)
        {
            float k = Mathf.Clamp01(1f - (_fundidoHasta - Time.time) / Mathf.Max(0.01f, upperBodyTransitionDuration));
            SetUpperBodyWeight(Mathf.Lerp(_pesoAlSalir, _originalUpperBodyWeight, k));
            if (k >= 1f) _fundidoHasta = 0f;
        }
        if (bPressed)
        {
            _pulsadoEn = Time.time;
            if (PuedeAbrirContraataque) OpenParryWindow();
        }
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
                StopDefending();
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
        _parryUntil = Time.time + parryWindow;
        _parryOpen = true;
        _parried = false;
        _pulsadoEn = Time.time;
        StopDefending();
        _preparando = true;
        _fundidoHasta = 0f;
        SetUpperBodyWeight(1f);
        PlayUpperBodyAnimation(preparacionAnimation);
        ScanForParry();
    }

    private void CloseParryWindow()
    {
        if (!_parryOpen) return;
        _parryOpen = false;
        _parryUntil = -1f;
        if (!_parried)
        {
            _parryCooldownUntil = Time.time + parryWhiffCooldown;
            SalirDePreparacion();
        }
    }

    private void ScanForParry()
    {
        var proyectiles = AmenazasAlJugador.Proyectiles;
        for (int i = proyectiles.Count - 1; i >= 0; i--)
        {
            if (!(proyectiles[i] is IDevolvible bola) || bola.TipoDeAmenaza != TipoDeAmenaza.Devolvible ||
                TiempoHastaElPecho(bola) > segundosDeAtrapado) continue;
            Transform objetivo = null;
            if (_magicCaster != null) _magicCaster.TryGetTarget(out objetivo);
            if (objetivo == null && bola.Lanzador != null) objetivo = bola.Lanzador.transform;
            Vector3 direccion = -bola.VelocidadDeAmenaza;
            Vector3 punto = bola.PosicionDeAmenaza;
            GameObject lanzador = bola.Lanzador;
            float dano = Mathf.Max(minCounterDamage, bola.DanoEntrante * counterDamageMultiplier);
            _preparando = false;
            _fundidoHasta = 0f;
            CancelInvoke(nameof(ReturnToDefendAnimation));
            StopDefending();
            bola.Devolver(gameObject, objetivo, direccion, dano);
            Vector3 facing = objetivo != null ? objetivo.position - transform.position : direccion;
            if (_magicCaster != null) _magicCaster.BeginCounterGesture(counterLockSeconds, facing);
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
        SalirDePreparacion();
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
        _preparando = false;
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
        // La pose de bloqueo empieza al mantener B más allá de la preparación.
        bool wantsDefense = GamepadInputReader.AttackMagicRightHeld && Time.time >= _pulsadoEn + parryWindow && !IsParryWindowOpen;

        bool hasMana = _manaPool == null || manaPerSecond <= 0f || _manaPool.Current > 0f;
        bool isAllowed = _playerActionManager == null || _playerActionManager.AllowShield;
        if (wantsDefense && hasMana && isAllowed)
            StartDefending();
        else
            StopDefending();
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
        SalirDePreparacion(true);
    }

    private void SalirDePreparacion(bool forzar = false)
    {
        if (!_preparando && !forzar) return;
        _preparando = false;
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

        ConfigureShieldDetector(_shieldInstance);
        _shieldInstance.SetActive(false);
    }

    private void ActivateShield()
    {
        if (_shieldInstance != null && !_shieldInstance.activeSelf)
            _shieldInstance.SetActive(true);
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

    internal void OnShieldHit()
    {
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

            if (_blockedLayers.Contains(layer))
            {
                _owner.OnShieldHit();

                if (IsProjectile(go))
                {
                    SafeDestroy(go);
                }
            }
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
