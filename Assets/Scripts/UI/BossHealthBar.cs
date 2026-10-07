using UnityEngine;

/// <summary>
/// Barra de vida de un jefe: sigue su Damageable y sus fases (IJefeConFases) y se oculta mientras
/// haya un menú o diálogo abierto. BossArenaController llama a Show() cuando empieza el combate.
/// Lo que se ve es BarraDeJefeUI (prefab Resources/UI/BarraDeJefe, con el arte del HUD).
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("ID de localización para el nombre del boss (ej: 'BOSS_DEMONIO_NAME'). Si está vacío, usa bossName.")]
    [SerializeField] private string bossNameId;
    [Tooltip("Nombre del boss mostrado en la barra de vida. Fallback si bossNameId no resuelve.")]
    [SerializeField] private string bossName = "Boss Demonio";
    [Tooltip("Fracción de vida por debajo de la cual la barra pasa a su tinte crítico.")]
    [SerializeField] private float criticalThreshold = 0.25f;

    [Header("Animación")]
    [SerializeField] private bool  animateHealthChanges = true;
    [SerializeField] private float animationSpeed       = 5f;
    [SerializeField] private float fadeInDuration       = 0.4f;
    [SerializeField] private float fadeOutDuration      = 0.5f;

    private Damageable    _bossDamageable;
    private BarraDeJefeUI _barra;
    private IJefeConFases _fases;

    private float _targetFillAmount  = 1f;
    private float _currentFillAmount = 1f;

    // Estado de visibilidad: activo en batalla y si está suspendido por un menú
    private bool _battleActive    = false;
    private bool _suspendedByMenu = false;

    void Start()
    {
        // Damageable lo añade en tiempo de ejecución NPCBehaviourManagerV2 (componente hermano) y
        // el orden de Awake entre componentes no está garantizado: se busca aquí, en Start.
        _bossDamageable = GetComponent<Damageable>();
        if (!_bossDamageable)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[BossHealthBar] No se encontró Damageable en el GameObject.", this);
#endif
            enabled = false;
            return;
        }

        _barra = BarraDeJefeUI.Crear();
        if (_barra == null)
        {
            enabled = false;
            return;
        }
        _barra.PonerNombre(GetLocalizedBossName());

        _fases = GetComponent<IJefeConFases>();
        if (_fases != null)
        {
            _barra.CrearMarcas(_fases.UmbralesDeFase, _fases.Fase);
            _fases.AlCambiarDeFase += OnCambioDeFase;
        }

        _bossDamageable.OnDamaged += OnBossDamaged;
        _bossDamageable.OnHealed  += OnBossHealed;
        _bossDamageable.OnDied    += OnBossDied;
        UpdateHealthBar();
        _currentFillAmount = _targetFillAmount;
        _barra.PonerRelleno(_currentFillAmount);
        // No auto-mostrar: BossArenaController llama a Show() cuando corresponde. Si ya lo ha
        // hecho antes de este Start, la barra aparece ahora.
        if (_battleActive && !_suspendedByMenu) Fundir(1f, fadeInDuration);
    }

    void OnEnable()
    {
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;
    }

    void OnDisable()
    {
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
    }

    void OnDestroy()
    {
        if (_bossDamageable)
        {
            _bossDamageable.OnDamaged -= OnBossDamaged;
            _bossDamageable.OnHealed  -= OnBossHealed;
            _bossDamageable.OnDied    -= OnBossDied;
        }
        if (_fases != null) _fases.AlCambiarDeFase -= OnCambioDeFase;
        if (_barra) Destroy(_barra.gameObject);
    }

    void Update()
    {
        if (!_battleActive || _suspendedByMenu || !_barra) return;
        if (Mathf.Abs(_currentFillAmount - _targetFillAmount) <= 0.001f) return;

        _currentFillAmount = Mathf.Lerp(_currentFillAmount, _targetFillAmount,
                                        Time.deltaTime * animationSpeed);
        if (Mathf.Abs(_currentFillAmount - _targetFillAmount) <= 0.001f)
            _currentFillAmount = _targetFillAmount;
        _barra.PonerRelleno(_currentFillAmount);
    }

    // ── API pública ────────────────────────────────────────────────────────

    public void Show()
    {
        _battleActive    = true;
        _suspendedByMenu = false;

        // Si hay un menú abierto, esperar a que cierre
        if (MenuManager.AnyOpen())
        {
            _suspendedByMenu = true;
            return;
        }

        Fundir(1f, fadeInDuration);
    }

    public void Hide()
    {
        _battleActive = false;
        Fundir(0f, fadeOutDuration);
    }

    // ── MenuManager ────────────────────────────────────────────────────────

    private void OnMenuOpened(MenuKind kind)
    {
        if (!_battleActive) return;
        _suspendedByMenu = true;
        Fundir(0f, fadeOutDuration * 0.7f);
    }

    private void OnMenuClosed(MenuKind kind)
    {
        if (!_battleActive || !_suspendedByMenu) return;
        if (MenuManager.AnyOpen()) return; // todavía hay otro menú abierto
        _suspendedByMenu = false;
        Fundir(1f, fadeInDuration);
    }

    // ── Eventos del boss ───────────────────────────────────────────────────

    private void OnBossDamaged(float _)
    {
        UpdateHealthBar();
        if (!_battleActive) Show(); // mostrar si por algún motivo no se había mostrado
        _barra.DestelloDano();
    }

    // Se ha curado (p. ej. un golpe a destiempo, ver SoloDanoCuandoExpuesto): la barra sube y
    // destella en verde, para que se lea que ese golpe le ha venido bien al jefe.
    private void OnBossHealed(float _)
    {
        UpdateHealthBar();
        _barra.DestelloCuracion();
    }

    private void OnBossDied()
    {
        UpdateHealthBar();
        _battleActive = false;
        _currentFillAmount = _targetFillAmount;
        _barra.PonerRelleno(_currentFillAmount);
        _barra.Fundido(0f, fadeOutDuration, 2f);
    }

    /// Al cambiar de fase la barra da un golpe y la marca superada se apaga.
    private void OnCambioDeFase(int fase)
    {
        _barra.MarcarSuperada(fase - 1);
        _barra.GolpeDeFase();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void Fundir(float alfa, float duracion)
    {
        if (_barra) _barra.Fundido(alfa, duracion);
    }

    private void UpdateHealthBar()
    {
        if (!_bossDamageable || !_barra) return;

        float pct = _bossDamageable.Current / _bossDamageable.Max;
        _targetFillAmount = Mathf.Clamp01(pct);

        if (!animateHealthChanges)
        {
            _currentFillAmount = _targetFillAmount;
            _barra.PonerRelleno(_currentFillAmount);
        }

        _barra.PonerVida(_bossDamageable.Current, _bossDamageable.Max, pct <= criticalThreshold);
    }

    /// <summary>Obtiene el nombre localizado del boss (usa bossNameId si está definido).</summary>
    private string GetLocalizedBossName()
    {
        if (!string.IsNullOrEmpty(bossNameId) && LocalizationManager.Instance != null)
            return LocalizationManager.Instance.Get(bossNameId, bossName);
        return bossName;
    }
}
