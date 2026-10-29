using UnityEngine;

/// <summary>
/// Gestiona la progresión del jugador mediante Sintonía Estelar (GDD § 1 y § 6.1, INC-847).
///
/// Sin niveles numéricos clásicos: al derrotar enemigos y purificar amenazas se acumulan puntos
/// de Sintonía Estelar. Al completarse la barra se alcanza un hito, que queda pendiente hasta que
/// el jugador lo elige desde el menú de Equipo (PopupEleccionSintoniaUI) entre dos dones:
///  - Vigor (+15 Vida Máxima para todo el grupo)
///  - Flujo (+10 Maná Máximo para todo el grupo)
/// Los dones suben los máximos por el camino de siempre (EstadisticasDelPersonaje.Sumar, que los
/// pasa a SaludDelGrupo para los tres personajes).
///
/// Se crea solo antes de cargar la primera escena (<see cref="AutoCrear"/>): no está montado en
/// Start.unity y no tiene nada que configurar en escena.
/// </summary>
public class SintoniaEstelarService : MonoBehaviour
{
    public static SintoniaEstelarService Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
#endif

    public enum TipoEleccionSintonia
    {
        Vigor, // +15 Vida Máxima
        Flujo  // +10 Maná Máximo
    }

    private const float VidaPorVigor = 15f;
    private const float ManaPorFlujo = 10f;

    [Header("Configuración de Umbrales")]
    [Tooltip("Puntos base necesarios para el primer hito de sintonía.")]
    [SerializeField, Min(10)] private int puntosBasePrimerHito = 100;
    [Tooltip("Incremento de puntos necesarios por cada hito alcanzado.")]
    [SerializeField, Min(0)] private int incrementoPuntosPorHito = 50;

    // Estado en memoria
    private int _puntosActuales;
    private int _hitosAlcanzados;
    private int _hitosPendientes;

    // ─── Consultas públicas ──────────────────────────────────────────────────

    public int PuntosActuales => _puntosActuales;
    public int PuntosParaSiguienteHito => CalcularUmbralHito(_hitosAlcanzados);
    public float FraccionProgreso => PuntosParaSiguienteHito > 0 ? Mathf.Clamp01((float)_puntosActuales / PuntosParaSiguienteHito) : 1f;
    public int HitosAlcanzados => _hitosAlcanzados;
    public int HitosPendientes => _hitosPendientes;

    // Servicio de partida sin datos de escena: se crea por código para que exista en cualquier
    // escena de prueba, sin depender de que Start.unity lo traiga montado.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCrear()
    {
        if (Instance != null) return;
        var go = new GameObject("[SintoniaEstelarService]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SintoniaEstelarService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CargarDesdePreset();
    }

    private void OnEnable() => GameBootService.OnProfileReady += CargarDesdePreset;

    private void OnDisable() => GameBootService.OnProfileReady -= CargarDesdePreset;

    // ─── Fórmulas y umbrales ────────────────────────────────────────────────

    public int CalcularUmbralHito(int hito)
    {
        return Mathf.Max(10, puntosBasePrimerHito + hito * incrementoPuntosPorHito);
    }

    // ─── Progresión y ganancias ─────────────────────────────────────────────

    /// <summary>
    /// Suma puntos de sintonía estelar y evalúa si se alcanza uno o más hitos.
    /// </summary>
    public void SumarSintonia(int puntos, Vector3? posicionOrigen = null)
    {
        if (puntos <= 0) return;

        _puntosActuales += puntos;

        bool nuevoHito = false;
        while (_puntosActuales >= PuntosParaSiguienteHito)
        {
            _puntosActuales -= PuntosParaSiguienteHito;
            _hitosAlcanzados++;
            _hitosPendientes++;
            nuevoHito = true;
        }

        GuardarEnPreset();

        if (nuevoHito)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[SintoniaEstelarService] Nuevo hito alcanzado. Total hitos: {_hitosAlcanzados}, pendientes: {_hitosPendientes}");
#endif
            if (HudToastService.Instance != null)
            {
                HudToastService.Instance.ShowRaw("✨ ¡Nueva Sintonía Estelar disponible! (Menú de Equipo)", 3.5f);
            }
        }
    }

    /// <summary>
    /// Fuerza un hito inmediato y abre la elección (pruebas y depuración).
    /// </summary>
    public void ForzarHito()
    {
        _hitosAlcanzados++;
        _hitosPendientes++;
        GuardarEnPreset();
        PopupEleccionSintoniaUI.Abrir();
    }

    /// <summary>
    /// Aplica la elección de expansión del jugador (+15 Vida o +10 Maná para los tres).
    /// </summary>
    public void AplicarEleccion(TipoEleccionSintonia eleccion)
    {
        if (_hitosPendientes > 0)
        {
            _hitosPendientes--;
        }

        var don = eleccion == TipoEleccionSintonia.Vigor
            ? new Estadisticas(VidaPorVigor, 0f, 0f, 0f)
            : new Estadisticas(0f, ManaPorFlujo, 0f, 0f);
        EstadisticasDelPersonaje.Sumar(don);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[SintoniaEstelarService] Elegido {eleccion} (+{don.vida} vida, +{don.magia} maná)");
#endif

        GuardarEnPreset();

        if (Sendero.UI.PlayerHUDV2.Instance != null)
        {
            Sendero.UI.PlayerHUDV2.Instance.ForceRefresh();
        }
    }

    /// <summary>Hay combate en curso (lo enseña el menú de depuración de Sintonía).</summary>
    public bool EstaEnCombate()
    {
        if (ActiveCombatRegistry.Count > 0) return true;
        return PlayerService.TryGetComponent(out PlayerActionManager am) && am.IsInMode(ActionMode.Combat);
    }

    // ─── Persistencia ───────────────────────────────────────────────────────

    public void GuardarEnPreset()
    {
        var preset = UnlockService.GetActivePreset();
        if (preset == null) return;

        preset.sintoniaPuntos = _puntosActuales;
        preset.sintoniaHitos = _hitosAlcanzados;
        preset.sintoniaHitosPendientes = _hitosPendientes;
    }

    public void CargarDesdePreset()
    {
        var preset = UnlockService.GetActivePreset();
        if (preset == null) return;

        _puntosActuales = preset.sintoniaPuntos;
        _hitosAlcanzados = preset.sintoniaHitos;
        _hitosPendientes = preset.sintoniaHitosPendientes;
    }
}
