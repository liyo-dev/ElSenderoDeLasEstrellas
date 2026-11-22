using System.Collections;
using UnityEngine;

/// <summary>
/// Anuncia el lugar en el que entra el jugador con la cinta de papel con el nombre letra a letra
/// (<see cref="PresentacionDeLugarUI"/>): la primera vez en la partida, con la pestaña de lugar
/// nuevo; las siguientes, sin pestaña y más corta. Sin cinta, con un rótulo (<see cref="HudToastService"/>). Lo que ya se conoce lo
/// recuerda la partida (<see cref="LugaresConocidos"/>). Si se sale de un lugar y se vuelve a
/// entrar sin pasar por otro, no se anuncia de nuevo; un lugar ya conocido tampoco se recuerda si
/// se avisó de él hace poco (<see cref="EsperaParaRecordar"/>, también tras cargar la partida) ni
/// si se llega a él dentro de una cinemática o una conversación.
/// Ver INC-1171.
///
/// No conoce zonas, escenas ni mapas: lo llama quien sepa que el jugador ha entrado en un lugar
/// con nombre (hoy <see cref="AmbientZone"/>, con el nombre de su <see cref="AmbientPreset"/>).
///
/// Es un aviso del canal Cabecera (<see cref="ArbitroDeAvisos"/>): espera mientras hay un menú, una
/// cinemática, una conversación o un combate, con el tiempo parado, y va antes que el banner de misión nueva si coinciden.
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
    /// Segundos (reales) que tienen que pasar para volver a recordar un lugar ya conocido.
    const float EsperaParaRecordar = 300f;

    // Cuándo se avisó por última vez de cada lugar (Time.unscaledTime). Es de la sesión: cargar
    // la partida no lo borra, para que «Continuar» no repita el lugar del que se acaba de avisar.
    private readonly System.Collections.Generic.Dictionary<string, float> _ultimoAviso = new();

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
    // Espera corta antes de pedir el turno (ver Co_PedirTurno).
    private Coroutine _pedirTurno;

    // En pleno combate no sale el nombre del lugar: espera a que acabe. Con una cinemática o una
    // conversación en marcha, la presentación espera a que termine: el cine siempre manda y el
    // lugar se anuncia después (ver INC-1234). En sigilo tampoco: el plano aéreo quitaría la vista
    // de quien se sigue (INC-1403).
    private static readonly string[] s_cedeAnte = { Situaciones.Combate, Situaciones.Cinematica, Situaciones.Dialogo, Situaciones.Sigilo };

    public CanalDeAviso Canal => CanalDeAviso.Cabecera;
    public int Prioridad => 30;
    public bool Pasajero => true;
    public System.Collections.Generic.IReadOnlyList<string> CedeAnte => s_cedeAnte;

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

        // Un lugar ya conocido (o sin partida donde apuntarlo) lleva la cinta corta: no se gasta la
        // primera vez de un sitio en algo que no se va a recordar. Si todavía no hay dónde dibujar
        // la cinta (el overlay aún no ha arrancado), se espera al turno: el rótulo es solo el último
        // recurso, cuando le toca y sigue sin haber cinta. Ver INC-1152.
        bool breve = LugaresConocidos.EsConocido(clave) || !LugaresConocidos.Marcar(clave);
        if (breve && !TocaRecordar(clave))
        {
            // Si lo que esperaba turno era otro recordatorio, ya no vale: el jugador está aquí.
            if (_pendiente && !_empezada && _breve) Descartar();
            return;
        }
        _ultimoAviso[clave] = Time.unscaledTime;
        _breve = breve;
        string nombre = Texto(clave, clave);

        // Un lugar detrás de otro: manda el último, que es donde está el jugador.
        _nombre = nombre;
        _pendiente = true;
        _empezada = false;
        _t = 0f;
        if (_pedirTurno != null) { StopCoroutine(_pedirTurno); _pedirTurno = null; }
        if (_aLaVista) Empezar();
        else _pedirTurno = StartCoroutine(Co_PedirTurno());
    }

    /// El lugar y la cinemática que arranca al entrar suelen dispararse en el mismo paso de
    /// física. Se espera un instante antes de tomar el turno para que el cine llegue primero:
    /// entonces la presentación cede (INC-1234) y no suena su música para nada.
    private IEnumerator Co_PedirTurno()
    {
        yield return new WaitForSecondsRealtime(0.15f);
        _pedirTurno = null;
        if (_pendiente && !_aLaVista) ArbitroDeAvisos.Pedir(this);
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
        if (_pendiente && Vista == null) RotuloEnSuLugar();
        if (!_pendiente)
        {
            ArbitroDeAvisos.Terminar(this);
            return;
        }
        if (!_empezada) Empezar();
        else Vista.Ver(true);
    }

    // Si algo tapa la presentación a medias, se esconde la cinta.
    public void AlPerderTurno()
    {
        _aLaVista = false;
        Vista?.Ver(false);
        // Un recordatorio que coincide con un combate caduca al perder el turno.
        if (_breve && Kits.Core.GameContext.Has(Situaciones.Combate)) Descartar();
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

    // Un lugar conocido se recuerda si hace rato que no se avisa de él y si el jugador ha llegado
    // por su cuenta: lo que mueve una cinemática o pasa en una conversación no es «llegar».
    private bool TocaRecordar(string clave)
    {
        if (_ultimoAviso.TryGetValue(clave, out float cuando) && Time.unscaledTime - cuando < EsperaParaRecordar)
            return false;
        for (int i = 0; i < s_cedeAnte.Length; i++)
            if (Kits.Core.GameContext.Has(s_cedeAnte[i])) return false;
        return true;
    }

    // Retira la presentación que esperaba turno sin haber empezado.
    private void Descartar()
    {
        if (_pedirTurno != null) { StopCoroutine(_pedirTurno); _pedirTurno = null; }
        _pendiente = false;
        _empezada = false;
        _aLaVista = false;
        ArbitroDeAvisos.Terminar(this);
    }

    private static PresentacionDeLugarUI Vista => DramaticTextOverlayUI.Instance != null
        ? DramaticTextOverlayUI.Instance.PresentacionDeLugar
        : null;

    private void Empezar()
    {
        var vista = Vista;
        if (vista == null) { RotuloEnSuLugar(); Acabar(); return; }
        _t = 0f;
        _empezada = true;
        vista.Preparar(_nombre, _breve ? "" : Texto(ClaveDelEncabezado, ""), _breve);
        vista.Ver(true);
        if (!_breve) AudioService.Instance?.PlaySFX(SonidoDeLaPresentacion);
    }

    // Sin cinta, el nombre sale en un rótulo y la presentación se da por hecha.
    private void RotuloEnSuLugar()
    {
        if (!string.IsNullOrEmpty(_nombre)) HudToastService.Instance?.ShowRaw(_nombre, DuracionDelRecordatorio);
        _pendiente = false;
        _empezada = false;
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
