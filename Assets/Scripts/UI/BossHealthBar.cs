using UnityEngine;

/// <summary>
/// Barra de vida de un jefe: sigue su Damageable y sus fases (IJefeConFases). BossArenaController
/// llama a Show() cuando empieza el combate y Hide() al acabar. Lo que se ve es BarraDeJefeUI
/// (prefab Resources/UI/BarraDeJefe, con el arte del HUD).
///
/// Es un aviso del canal Cabecera (<see cref="ArbitroDeAvisos"/>) que dura todo el combate: el
/// árbitro la esconde mientras hay un menú o una cinemática, y la aparta mientras se ve un aviso
/// más importante del mismo canal (el banner de misión nueva), que entra antes que ella (INC-859).
/// Si el jefe recibe un golpe antes de que le llegue el turno, pide paso con prisa.
/// </summary>
public class BossHealthBar : MonoBehaviour, IAvisoEnPantalla
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

    [Header("Turno")]
    [Tooltip("Prioridad dentro del canal Cabecera. Más baja que la del banner de misión nueva para entrar detrás de él.")]
    [SerializeField] private int prioridad = 10;

    private Damageable    _bossDamageable;
    private BarraDeJefeUI _barra;
    private IJefeConFases _fases;

    private float _targetFillAmount  = 1f;
    private float _currentFillAmount = 1f;

    // En combate (entre Show y Hide o la muerte del jefe).
    private bool _battleActive;
    // Tiene el turno y nada la tapa.
    private bool _aLaVista;
    // Ya ha entrado en pantalla en este combate.
    private bool _haEntrado;

    public CanalDeAviso Canal => CanalDeAviso.Cabecera;
    public int Prioridad => prioridad;
    public bool Pasajero => false;

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
        // BossArenaController puede haber llamado a Show() antes de este Start: si ya tiene el
        // turno, la barra aparece ahora que existe.
        if (_aLaVista) Fundir(1f, fadeInDuration);
    }

    void OnEnable()
    {
        if (_battleActive) ArbitroDeAvisos.Pedir(this);
    }

    void OnDisable()
    {
        ArbitroDeAvisos.Terminar(this);
        _aLaVista = false;
        Fundir(0f, 0f);
    }

    void OnDestroy()
    {
        ArbitroDeAvisos.Terminar(this);
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
        if (!_aLaVista || !_barra) return;
        if (Mathf.Abs(_currentFillAmount - _targetFillAmount) <= 0.001f) return;

        _currentFillAmount = Mathf.Lerp(_currentFillAmount, _targetFillAmount,
                                        Time.deltaTime * animationSpeed);
        if (Mathf.Abs(_currentFillAmount - _targetFillAmount) <= 0.001f)
            _currentFillAmount = _targetFillAmount;
        _barra.PonerRelleno(_currentFillAmount);
    }

    // ── API pública ────────────────────────────────────────────────────────

    /// Empieza el combate: la barra pide su turno y aparece en cuanto el árbitro se lo da.
    public void Show()
    {
        if (!enabled) return; // sin Damageable o sin prefab de la barra (ver Start)
        if (!_battleActive) _haEntrado = false;
        _battleActive = true;
        ArbitroDeAvisos.Pedir(this);
    }

    public void Hide()
    {
        _battleActive = false;
        ArbitroDeAvisos.Terminar(this);
        _aLaVista = false;
        Fundir(0f, fadeOutDuration);
    }

    // ── IAvisoEnPantalla ───────────────────────────────────────────────────

    public void AlTenerTurno()
    {
        _aLaVista = true;
        _haEntrado = true;
        // La vida ha podido cambiar mientras estaba escondida: entra ya con el valor real.
        _currentFillAmount = _targetFillAmount;
        if (_barra) _barra.PonerRelleno(_currentFillAmount);
        Fundir(1f, fadeInDuration);
    }

    public void AlPerderTurno()
    {
        _aLaVista = false;
        Fundir(0f, fadeOutDuration * 0.7f);
    }

    // No es pasajera: el árbitro nunca la descarta.
    public void AlDescartar() => AlPerderTurno();

    // ── Eventos del boss ───────────────────────────────────────────────────

    private void OnBossDamaged(float _)
    {
        UpdateHealthBar();
        // Primer golpe del combate con la barra aún esperando turno (p. ej. detrás del banner de
        // misión nueva): el combate ya está en marcha y la barra no espera más.
        if (!_battleActive) Show();
        if (_battleActive && !_haEntrado && !ArbitroDeAvisos.Silenciado) ArbitroDeAvisos.Adelantar(this);
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
        ArbitroDeAvisos.Terminar(this);
        _aLaVista = false;
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
