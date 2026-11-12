using UnityEngine;

/// <summary>
/// Anuncia el lugar en el que entra el jugador con la cinta de papel con el nombre letra a letra
/// (<see cref="PresentacionDeLugarUI"/>): la primera vez en la partida, con la pestaña de lugar
/// nuevo; las siguientes, sin pestaña y más corta. Sin cinta, con un rótulo (<see cref="HudToastService"/>). Lo que ya se conoce lo
/// recuerda la partida (<see cref="LugaresConocidos"/>). Si se sale de un lugar y se vuelve a
/// entrar sin pasar por otro, no se anuncia de nuevo.
///
/// No conoce zonas, escenas ni mapas: lo llama quien sepa que el jugador ha entrado en un lugar
/// con nombre (hoy <see cref="AmbientZone"/>, con el nombre de su <see cref="AmbientPreset"/>).
///
/// Es un aviso del canal Cabecera (<see cref="ArbitroDeAvisos"/>): espera mientras hay un menú o
/// una cinemática, con el tiempo parado, y va antes que el banner de misión nueva si coinciden.
/// Se auto-crea al arrancar el juego.
/// </summary>
public class PresentacionDeLugares : MonoBehaviour, IAvisoEnPantalla
{
    public static PresentacionDeLugares Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    /// Texto de la pestaña de la presentación («Nuevo lugar»). Sin texto, no sale la pestaña.
    const string ClaveDelEncabezado = "LUGAR_NUEVO";
    /// Sonido de la presentación en el AudioGraphProfile. Sin clip para la clave, no suena nada.
    const string SonidoDeLaPresentacion = "UI_LugarDescubierto";
    const float DuracionDelRecordatorio = 2.5f;

    private string _ultimoLugar;
    private string _nombre;
    // Hay una presentación por enseñar o a medio enseñar.
    private bool _pendiente;
    // La que toca es la corta (lugar ya conocido).
    private bool _breve;
    // Tiene el turno y nada la tapa: su tiempo corre.
    private bool _aLaVista;
    private bool _empezada;
    private float _t;

    public CanalDeAviso Canal => CanalDeAviso.Cabecera;
    public int Prioridad => 30;
    public bool Pasajero => true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var root = new GameObject("[PresentacionDeLugares]");
        DontDestroyOnLoad(root);
        Instance = root.AddComponent<PresentacionDeLugares>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        PlayerPresetService.OnPresetApplying -= AlCargarPartida;
        PlayerPresetService.OnPresetApplying += AlCargarPartida;
    }

    private void OnDestroy()
    {
        PlayerPresetService.OnPresetApplying -= AlCargarPartida;
        ArbitroDeAvisos.Terminar(this);
        if (Instance == this) Instance = null;
    }

    // Al cargar, el lugar en el que se aparece se vuelve a anunciar.
    private void AlCargarPartida() => _ultimoLugar = null;

    // ────────────────────────────────────────────────────────────────────────
    #region API pública

    /// True mientras hay una presentación de lugar nuevo pedida o a la vista (la corta no cuenta).
    public static bool EnCurso => Instance != null && Instance._pendiente && !Instance._breve;

    /// <summary>El jugador acaba de entrar en el lugar cuyo nombre es <paramref name="claveDelLugar"/>.</summary>
    public static void Anunciar(string claveDelLugar)
    {
        if (Instance != null) Instance.AnunciarLugar(claveDelLugar);
    }

    #endregion

    private void AnunciarLugar(string clave)
    {
        if (string.IsNullOrEmpty(clave) || clave == _ultimoLugar) return;
        _ultimoLugar = clave;
        string nombre = Texto(clave, clave);

        // Sin dónde dibujarla, rótulo. Un lugar ya conocido (o sin partida donde apuntarlo) lleva
        // la cinta corta: no se gasta la primera vez de un sitio en algo que no se va a recordar.
        if (Vista == null)
        {
            HudToastService.Instance?.ShowRaw(nombre, DuracionDelRecordatorio);
            return;
        }
        _breve = LugaresConocidos.EsConocido(clave) || !LugaresConocidos.Marcar(clave);

        // Un lugar detrás de otro: manda el último, que es donde está el jugador.
        _nombre = nombre;
        _pendiente = true;
        _empezada = false;
        _t = 0f;
        if (_aLaVista) Empezar();
        else ArbitroDeAvisos.Pedir(this);
    }

    private void Update()
    {
        if (!_pendiente || !_aLaVista || !_empezada) return;
        var vista = Vista;
        if (vista == null) { Acabar(); return; }

        _t += Time.unscaledDeltaTime;
        vista.Evaluar(_t);
        if (_t >= vista.Duracion) Acabar();
    }

    // ────────────────────────────────────────────────────────────────────────
    #region IAvisoEnPantalla

    public void AlTenerTurno()
    {
        _aLaVista = true;
        if (!_pendiente || Vista == null)
        {
            _pendiente = false;
            ArbitroDeAvisos.Terminar(this);
            return;
        }
        if (!_empezada) Empezar();
        else Vista.Ver(true);
    }

    public void AlPerderTurno()
    {
        _aLaVista = false;
        Vista?.Ver(false);
    }

    public void AlDescartar()
    {
        _aLaVista = false;
        _pendiente = false;
        _empezada = false;
        Vista?.Ver(false);
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region Internals

    private static PresentacionDeLugarUI Vista => DramaticTextOverlayUI.Instance != null
        ? DramaticTextOverlayUI.Instance.PresentacionDeLugar
        : null;

    private void Empezar()
    {
        var vista = Vista;
        if (vista == null) { Acabar(); return; }
        _t = 0f;
        _empezada = true;
        vista.Preparar(_nombre, _breve ? "" : Texto(ClaveDelEncabezado, ""), _breve);
        vista.Ver(true);
        if (!_breve) AudioService.Instance?.PlaySFX(SonidoDeLaPresentacion);
    }

    private void Acabar()
    {
        Vista?.Ver(false);
        _pendiente = false;
        _empezada = false;
        _aLaVista = false;
        ArbitroDeAvisos.Terminar(this);
    }

    private static string Texto(string clave, string siFalta)
        => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, siFalta) : siFalta;

    #endregion
}
