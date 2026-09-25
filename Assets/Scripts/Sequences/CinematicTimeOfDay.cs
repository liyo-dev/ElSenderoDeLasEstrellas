using UnityEngine;
using UnityEngine.SceneManagement;

/// La hora del día mientras manda una cinemática.
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// El prólogo necesita que amanezca, atardezca y anochezca en cinco minutos: la luz es la que
/// cuenta que esa mañana tranquila era la última. Pero la hora del día de este juego no es un
/// ajuste de escena, es un sistema vivo — DayNightCycle es, por diseño, la única fuente de verdad
/// de RenderSettings.fog*, del skybox y del sol (ver su comentario de cabecera). Escribir la luz
/// del prólogo en su propia escena no habría funcionado por dos motivos distintos, y los dos ya
/// estaban pasando:
///
///   1. La escena del prólogo se carga en ADITIVO y nunca se marca como activa, y Unity coge el
///      skybox, la niebla y la luz ambiente de la escena ACTIVA. Todo lo que tenía puesto
///      Prologo_Valle en su cabecera de Lighting no se aplicaba nunca.
///   2. Aunque se aplicara, DayNightCycle lo volvería a escribir en cuanto cambiara de periodo,
///      a mitad de una frase.
///
/// Así que no se pelea con el sistema: se le manda. Esto le pide la hora que quiere la escena,
/// le para el reloj mientras dura, y al terminar — también si el jugador se salta la cinemática,
/// que es donde esto se rompería si dependiera de un beat final — le devuelve la hora que había.
/// Una pesadilla no puede dejar el mundo anocheciendo.
public static class CinematicTimeOfDay
{
    /// La hora con la que se queda el MUNDO cuando acaba la cinemática, si la escena lo pide
    /// (ver TimeOfDayBeat.esLaHoraDeVolver). Sin esto se devuelve la que había, y el prólogo
    /// terminaba dejando la aldea como estaba: «cuando acaba el prólogo en MainWorld debe estar
    /// amaneciendo».
    public static DayNightCycle.TimeOfDay? HoraAlVolver { get; set; }

    private static bool _guardado;
    private static DayNightCycle.TimeOfDay _horaPrevia;
    private static bool _avanceAutomaticoPrevio;

    // La escena que estaba activa antes de la cinemática. Ver ActivarEscena.
    private static Scene _escenaPrevia;
    private static bool _escenaCambiada;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _guardado = false; _escenaCambiada = false; _exteriorForzado = false; HoraAlVolver = null; }
#endif

    /// Pone la hora del día que pide la cinemática. La primera vez guarda la que había.
    public static void Apply(DayNightCycle.TimeOfDay hora, bool immediate)
    {
        var ciclo = DayNightCycle.Instance;
        if (ciclo == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CinematicTimeOfDay] No hay ningún DayNightCycle cargado, así que la " +
                "hora del día de esta cinemática no se puede cambiar. La escena se verá con la luz " +
                "que haya. (Normal en una escena de prueba abierta suelta; en partida no debería pasar.)");
#endif
            return;
        }

        if (!_guardado)
        {
            _horaPrevia = ciclo.CurrentTimeOfDay;
            _avanceAutomaticoPrevio = ciclo.AutoAdvance;
            _guardado = true;
        }

        // Mientras manda la escena, el reloj no corre: si no, el ciclo puede cambiar de periodo a
        // mitad de un plano y deshacer lo que acaba de pedir la cinemática.
        ciclo.AutoAdvance = false;
        // Ni se sortea clima nuevo (INC-409): el tiempo de una cinemática lo pone la cinemática.
        DayNightCycle.SorteoDeClimaEnPausa = true;
        ciclo.SetTimeOfDay(hora, immediate);

        // Y que el cielo sea el del ciclo, pase lo que pase (INC-380). Mientras hay un override de
        // entorno, DayNightCycle NO toca RenderSettings.skybox a propósito
        // (IsSkyboxLockedByEnvironment): está pensado para que un interior no se llene de cielo.
        // Aquí es al revés — la cámara está en el valle —, así que el material que el ciclo va
        // tintando por horas tiene que ser el que se pinta, o el atardecer y la noche se quedan
        // solo en la niebla y la luz, que es exactamente lo que se veía.
        var cielo = ciclo.SkyboxEnUso;
        if (cielo != null && RenderSettings.skybox != cielo)
        {
            RenderSettings.skybox = cielo;
            DynamicGI.UpdateEnvironment();
        }
    }

    /// Marca como ACTIVA la escena donde ocurre la cinemática, y la devuelve al terminar.
    ///
    /// ── El cielo negro ────────────────────────────────────────────────────────────────────────
    /// Unity coge el skybox, la niebla y la luz ambiente de la escena ACTIVA, no de la que se está
    /// mirando. El prólogo se carga en aditivo mientras la activa sigue siendo la del jugador — que
    /// durante el sueño es `WillHouse`, un interior sin cielo. De ahí el cielo negro del valle en
    /// las tres grabaciones: no le faltaba el skybox, es que estaba usando el de una habitación.
    ///
    /// Ya estaba escrito en la cabecera de esta clase como el motivo nº1 por el que la luz del
    /// prólogo no se podía poner en su propia escena. Lo que faltaba era la otra mitad: marcarla
    /// activa mientras dura.
    public static void ActivarEscena(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded) return;

        Scene actual = SceneManager.GetActiveScene();
        if (actual == escena) return;

        if (!_escenaCambiada)
        {
            _escenaPrevia = actual;
            _escenaCambiada = true;
        }

        SceneManager.SetActiveScene(escena);
    }

    // El override de entorno: ver MostrarExterior.
    private static bool _exteriorForzado;

    /// Hace que se vea el CIELO durante la cinemática, aunque el jugador esté dentro de una casa.
    ///
    /// ── El cielo negro, de verdad esta vez ────────────────────────────────────────────────────
    /// El intento anterior (marcar la escena del valle como activa) no era el problema: al arrancar
    /// el juego, Unity aplica la iluminación de cada escena al cargarla, y en runtime
    /// RenderSettings es UNO solo y global — SetActiveScene no lo vuelve a cambiar. Así que el
    /// valle nunca recuperaba su cielo por ahí.
    ///
    /// Lo que de verdad pasa es esto: Will está dormido en su habitación, y para el juego eso es
    /// un INTERIOR. EnvironmentController, al entrar en un interior, pone
    /// `RenderSettings.skybox = null` y la cámara a color sólido — porque dentro de una casa no se
    /// quiere ver cielo. El prólogo entonces se rueda en un valle a cielo abierto con el skybox
    /// apagado por una habitación que no sale en ningún plano. De ahí el negro de las cuatro
    /// grabaciones: no faltaba ningún material, es que estaba APAGADO a propósito.
    ///
    /// Y no hay que pelearse con eso tampoco: EnvironmentController ya tiene la puerta abierta para
    /// este caso exacto (BeginCinematicOverride + ApplyExteriorForCinematic), que además deja a
    /// DayNightCycle sin tocar el cielo mientras dura (ver IsSkyboxLockedByEnvironment). Se pide y
    /// se devuelve, igual que la hora del día.
    public static void MostrarExterior(Camera camara)
    {
        var entorno = EnvironmentController.Instance;
        if (entorno == null || _exteriorForzado) return;

        // Si YA hay un override puesto por otro sistema, no se abre otro: se aplica el exterior
        // dentro del suyo y no se cierra al terminar. Cerrar un override ajeno devolveria el
        // entorno a mitad de lo que estuviera haciendo ese otro sistema.
        bool nuestro = !entorno.IsCinematicOverrideActive;
        if (nuestro) entorno.BeginCinematicOverride();

        entorno.ApplyExteriorForCinematic(camara);
        _exteriorForzado = nuestro;

        // ── Y ahora la parte que faltaba (19 sep 2026, cuarta grabación) ──────────────────────
        //
        // ApplyExteriorForCinematic recupera el skybox de su SNAPSHOT del exterior. Y ese snapshot
        // aquí no vale nada: se toma la primera vez que hace falta, y en el prólogo eso ocurre con
        // el jugador ya dentro de casa y RenderSettings.skybox ya puesto a null. Resultado: guarda
        // null, "restaura" null, y el valle se sigue rodando sin cielo. En el log de esa grabación
        // se ve tal cual — «Cinematic Override INICIADO - Modo guardado: Unknown», y el cielo negro
        // los tres minutos enteros.
        //
        // Así que el cielo se coge de donde de verdad vive: el material que el ciclo día/noche está
        // tintando ahora mismo.
        if (RenderSettings.skybox == null)
        {
            var cielo = DayNightCycle.Instance != null ? DayNightCycle.Instance.SkyboxEnUso : null;
            if (cielo != null)
            {
                RenderSettings.skybox = cielo;
                DynamicGI.UpdateEnvironment();
            }
        }

        // La cámara tiene que estar en modo Skybox aunque el override no haya podido tocarla (por
        // ejemplo si el corte todavía no ha resuelto cuál es la cámara de la escena).
        if (camara != null) camara.clearFlags = CameraClearFlags.Skybox;

        // La lluvia, la niebla y el viento se cuelgan de ESTA cámara mientras dura la cinemática
        // (ver DayNightCycle.AnclaDeClima). Sin esto caen sobre el jugador, que en el prólogo está
        // dormido en su casa, y además nacen apagadas por estar él en un interior.
        if (camara != null) DayNightCycle.AnclaDeClima = camara.transform;

        Diagnostico("exterior forzado", camara);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// Cuenta CON QUÉ se está rodando, no qué se ha pedido.
    ///
    /// Incluye el tinte y la intensidad reales del material de cielo, que es donde estaba el
    /// problema del «fondo negro»: el skybox y la cámara estaban bien puestos los tres intentos, y
    /// lo que lo apagaba era la multiplicación de la franja horaria por el clima (ver la nota de
    /// MostrarExterior). Sin estos dos números no se podía ver, y con ellos se ve en una línea.
    public static void Diagnostico(string cuando, Camera camara)
    {
        var cielo = RenderSettings.skybox;
        string cieloTxt = "NINGUNO — el fondo saldrá liso";

        if (cielo != null)
        {
            cieloTxt = $"'{cielo.name}' (shader {cielo.shader?.name})";
            if (cielo.HasProperty("_Tint"))      cieloTxt += $", tinte {cielo.GetColor("_Tint")}";
            if (cielo.HasProperty("_Intensity")) cieloTxt += $", intensidad {cielo.GetFloat("_Intensity"):F2}";
        }

        Debug.Log($"[CinematicTimeOfDay] Cielo ({cuando}): {cieloTxt}. " +
            $"Cámara='{(camara != null ? camara.name : "sin resolver")}' " +
            $"clearFlags={(camara != null ? camara.clearFlags.ToString() : "?")}. " +
            $"Niebla={(RenderSettings.fog ? $"sí, {RenderSettings.fogColor}, densidad {RenderSettings.fogDensity:F4}" : "no")}. " +
            $"Ambiente={RenderSettings.ambientMode} {RenderSettings.ambientLight}.");
    }
#else
    public static void Diagnostico(string cuando, Camera camara) { }
#endif


    /// Devuelve la hora del día que había antes de la cinemática. La llama el SequencePlayer al
    /// terminar, al saltar y al fallar — nunca un beat, porque un beat final no se ejecuta cuando
    /// el jugador se salta la escena.
    public static void Restore()
    {
        // Lo primero: el clima vuelve a colgar del jugador (ver DayNightCycle.AnclaDeClima), y
        // vuelve a sortearse (INC-409).
        DayNightCycle.AnclaDeClima = null;
        DayNightCycle.SorteoDeClimaEnPausa = false;

        // El tiempo atmosférico se devuelve SIEMPRE, y antes que nada: una secuencia puede haber
        // encendido una tormenta sin tocar la hora del día, y entonces la guarda de abajo saldría
        // por la puerta dejando al jugador bajo la lluvia del prólogo. Son dos cosas
        // independientes y cada una sabe si tiene algo que restaurar. Ver CinematicWeather.
        CinematicWeather.Restore();

        // El entorno, también siempre y antes de la guarda: si la cinemática forzó el exterior y no
        // se devuelve, el jugador se queda con el cielo abierto dentro de su propia casa.
        if (_exteriorForzado)
        {
            _exteriorForzado = false;
            EnvironmentController.Instance?.EndCinematicOverride();
        }

        if (_escenaCambiada)
        {
            _escenaCambiada = false;
            if (_escenaPrevia.IsValid() && _escenaPrevia.isLoaded)
                SceneManager.SetActiveScene(_escenaPrevia);
        }

        if (!_guardado) return;
        _guardado = false;

        var ciclo = DayNightCycle.Instance;
        if (ciclo == null) return;

        var vuelta = HoraAlVolver ?? _horaPrevia;
        HoraAlVolver = null;

        ciclo.SetTimeOfDay(vuelta, immediate: true);
        ciclo.AutoAdvance = _avanceAutomaticoPrevio;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CinematicTimeOfDay] Fin de cinemática: el mundo se queda en {vuelta} " +
                  $"(antes era {_horaPrevia}).");
#endif
    }
}
