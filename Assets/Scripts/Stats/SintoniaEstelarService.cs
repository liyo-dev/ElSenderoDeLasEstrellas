using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la progresión del jugador mediante Sintonía Estelar (GDD § 1 y § 6.1, INC-847).
///
/// Sin niveles numéricos clásicos: al derrotar enemigos y purificar amenazas se acumulan puntos
/// de Sintonía Estelar. Al completarse la barra, se alcanza un hito de expansión y el jugador
/// elige libremente entre dos dones:
///  - Vigor (+15 Vida Máxima para todo el grupo)
///  - Flujo (+10 Maná Máximo para todo el grupo)
///
/// Si se alcanza un hito durante combate o secuencias cinemáticas, el hito queda pendiente y se
/// abre automáticamente al salir de peligro o recuperar el control.
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

    [Header("Configuración de Umbrales")]
    [Tooltip("Puntos base necesarios para el primer hito de sintonía.")]
    [SerializeField, Min(10)] private int puntosBasePrimerHito = 100;
    [Tooltip("Incremento de puntos necesarios por cada hito alcanzado.")]
    [SerializeField, Min(0)] private int incrementoPuntosPorHito = 50;

    [Header("Tiempos de seguridad")]
    [Tooltip("Segundos de calma tras salir de combate antes de mostrar el popup.")]
    [SerializeField, Min(0f)] private float segundosDeCalmaPostCombate = 1.0f;

    // Estado en memoria
    private int _puntosActuales;
    private int _hitosAlcanzados;
    private int _hitosPendientes;
    private float _ultimoMomentoEnCombate = -999f;

    /// <summary>Puntos actuales hacia el siguiente hito y total necesario para completarlo.</summary>
    public event Action<int, int> OnProgresoModificado;

    /// <summary>Se ha alcanzado un hito y está disponible para ser elegido (pasa la cantidad de hitos pendientes).</summary>
    public event Action<int> OnHitoDisponible;

    // ─── Consultas públicas ──────────────────────────────────────────────────

    public int PuntosActuales => _puntosActuales;
    public int PuntosParaSiguienteHito => CalcularUmbralHito(_hitosAlcanzados);
    public float FraccionProgreso => PuntosParaSiguienteHito > 0 ? Mathf.Clamp01((float)_puntosActuales / PuntosParaSiguienteHito) : 1f;
    public int HitosAlcanzados => _hitosAlcanzados;
    public int HitosPendientes => _hitosPendientes;

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

    private void OnEnable()
    {
        GameBootService.OnProfileReady += CargarDesdePreset;
        ActiveCombatRegistry.OnNPCExitedCombat += AlSalirNPCDeCombate;
    }

    private void OnDisable()
    {
        GameBootService.OnProfileReady -= CargarDesdePreset;
        ActiveCombatRegistry.OnNPCExitedCombat -= AlSalirNPCDeCombate;
    }

    private void Update()
    {
        if (EstaEnCombate())
        {
            _ultimoMomentoEnCombate = Time.unscaledTime;
        }
    }

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
        OnProgresoModificado?.Invoke(_puntosActuales, PuntosParaSiguienteHito);

        if (nuevoHito)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[SintoniaEstelarService] ✨ ¡Nuevo hito alcanzado! Total hitos: {_hitosAlcanzados}, Pendientes: {_hitosPendientes}");
#endif
            if (HudToastService.Instance != null)
            {
                HudToastService.Instance.ShowRaw("✨ ¡Nueva Sintonía Estelar disponible! (Menú de Equipo)", 3.5f);
            }

            OnHitoDisponible?.Invoke(_hitosPendientes);
        }
    }

    /// <summary>
    /// Fuerza un hito inmediato (útil para pruebas y debug).
    /// </summary>
    public void ForzarHito()
    {
        _hitosAlcanzados++;
        _hitosPendientes++;
        GuardarEnPreset();
        OnProgresoModificado?.Invoke(_puntosActuales, PuntosParaSiguienteHito);
        OnHitoDisponible?.Invoke(_hitosPendientes);
        PopupEleccionSintoniaUI.Abrir();
    }

    /// <summary>
    /// Aplica la elección de expansión del jugador (+15 Vida o +10 Maná).
    /// </summary>
    public void AplicarEleccion(TipoEleccionSintonia eleccion)
    {
        if (_hitosPendientes > 0)
        {
            _hitosPendientes--;
        }

        if (eleccion == TipoEleccionSintonia.Vigor)
        {
            const float incrementoVida = 15f;
            EstadisticasDelPersonaje.Sumar(new Estadisticas(incrementoVida, 0f, 0f, 0f));
            if (SaludDelGrupo.Instance != null)
            {
                SaludDelGrupo.Instance.AumentarMaximosDelGrupo(incrementoVida, 0f);
            }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[SintoniaEstelarService] 💚 Elegido Vigor (+{incrementoVida} Vida Máxima)");
#endif
        }
        else if (eleccion == TipoEleccionSintonia.Flujo)
        {
            const float incrementoMana = 10f;
            EstadisticasDelPersonaje.Sumar(new Estadisticas(0f, incrementoMana, 0f, 0f));
            if (SaludDelGrupo.Instance != null)
            {
                SaludDelGrupo.Instance.AumentarMaximosDelGrupo(0f, incrementoMana);
            }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[SintoniaEstelarService] 💙 Elegido Flujo (+{incrementoMana} Maná Máximo)");
#endif
        }

        GuardarEnPreset();

        if (Sendero.UI.PlayerHUDV2.Instance != null)
        {
            Sendero.UI.PlayerHUDV2.Instance.ForceRefresh();
        }

        // Si aún quedan más hitos acumulados, abrir la siguiente elección tras un breve respiro
        if (_hitosPendientes > 0 && PuedeMostrarPopup())
        {
            StartCoroutine(Co_AbrirSiguienteHito());
        }
    }

    private IEnumerator Co_AbrirSiguienteHito()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        if (_hitosPendientes > 0 && PuedeMostrarPopup() && !PopupEleccionSintoniaUI.EstaAbierto)
        {
            NotificarHitoDisponible();
        }
    }

    public void NotificarHitoDisponible()
    {
        if (_hitosPendientes <= 0) return;
        OnHitoDisponible?.Invoke(_hitosPendientes);
    }

    // ─── Detección de combate y seguridad ───────────────────────────────────

    public bool EstaEnCombate()
    {
        if (ActiveCombatRegistry.Count > 0) return true;
        if (PlayerService.TryGetComponent(out PlayerActionManager am) && am.IsInMode(ActionMode.Combat))
            return true;
        return false;
    }

    public bool PuedeMostrarPopup()
    {
        if (EstaEnCombate()) return false;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return false;
        if (PlayerService.TryGetComponent(out PlayerActionManager am))
        {
            if (am.IsInMode(ActionMode.Cinematic) || am.IsInMode(ActionMode.Combat))
                return false;
        }
        return true;
    }

    private void AlSalirNPCDeCombate(GameObject npc)
    {
        if (!EstaEnCombate() && _hitosPendientes > 0)
        {
            _ultimoMomentoEnCombate = Time.unscaledTime;
        }
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
        OnProgresoModificado?.Invoke(_puntosActuales, PuntosParaSiguienteHito);
    }
}
