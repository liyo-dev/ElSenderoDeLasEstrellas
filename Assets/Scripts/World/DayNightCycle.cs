using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[DisallowMultipleComponent]

[DefaultExecutionOrder(-100)]
/// Coordina las franjas de luz, el fundido del cielo y el clima independiente del periodo.
public class DayNightCycle : MonoBehaviour
{

    public static Transform Sun { get; private set; }

    public static Transform AnclaDeClima
    {
        get => s_anclaDeClima;
        set
        {
            if (s_anclaDeClima == value) return;
            s_anclaDeClima = value;
            if (Instance != null) Instance.ReanclarClima();
        }
    }
    private static Transform s_anclaDeClima;

    private void ReanclarClima()
    {
        foreach (var efecto in new[] { _activeRainInstance, _activeMistInstance, _activeWindInstance, _activeSnowInstance })
        {
            if (efecto == null) continue;
            if (s_anclaDeClima != null)
            {
                efecto.transform.SetParent(s_anclaDeClima, false);
                efecto.transform.localPosition = Vector3.zero;
                efecto.SetActive(true);
            }
            else if (IsSkyboxLockedByEnvironment())
            {
                efecto.SetActive(false);
            }
        }
    }

    public static DayNightCycle Instance { get; private set; }

    public static bool SorteoDeClimaEnPausa;

#if UNITY_EDITOR

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; Sun = null; s_anclaDeClima = null; SorteoDeClimaEnPausa = false; }
#endif

    public bool AutoAdvance
    {
        get => autoAdvance;
        set => autoAdvance = value;
    }

    /// Si el clima se sortea solo. Apagado, solo cambia cuando alguien lo pide (cinemáticas, LAB).
    public bool SorteoAutomatico
    {
        get => sortearClima;
        set => sortearClima = value;
    }

    /// Si ahora puede empezar un clima que nadie ha pedido (sorteo propio o nubes que se cargan).
    public bool PuedeSortearClima => sortearClima && !SorteoDeClimaEnPausa;

    public enum TimeOfDay
    {
        [InspectorName("Día (Tarde)")] AfterNoon,
        [InspectorName("Mañana radiante (no usada)")] BrightMorning,
        [InspectorName("Nublado (no usado)")] Cloudy,
        [InspectorName("Media tarde (no usada)")] EarlyDusk,
        [InspectorName("Halo (no usado)")] HaloSky,
        [InspectorName("Medianoche (no usada)")] Midnight,
        [InspectorName("Amanecer")] Morning,
        [InspectorName("Noche")] Night,
        [InspectorName("Atardecer")] Sunset
    }

    [System.Serializable]
    public class TimeOfDaySettings
    {
        public TimeOfDay timeOfDay;

        [Tooltip("Cielo de esta franja, con el shader El Sendero/Cielo.")]
        public Material skybox;

        [Header("Luz direccional")]
        public Color lightColor = Color.white;
        [Range(0f, 2f)] public float lightIntensity = 1f;

        [Header("Luz ambiental")]
        public Color ambientColor = new Color(0.2f, 0.2f, 0.25f);
        [Range(0f, 2f)] public float ambientIntensity = 1f;

        [Header("Niebla")]
        public Color fogColor = new Color(0.5f, 0.5f, 0.5f);
        [Range(0f, 0.1f)] public float fogDensity = 0.01f;

        [Header("Ciclo")]
        [Tooltip("Duración de este periodo en segundos. El clima (lluvia/niebla) ya NO depende de esto — ver Header(\"Clima — sorteo independiente del periodo\") más abajo en la clase: el clima corre con su propio temporizador y puede empezar o seguir activo en cualquier periodo, incluido a mitad de uno.")]
        public float duration = 60f;
    }

    [Header("Periodos del día")]
    [SerializeField] private TimeOfDaySettings[] timeSettings = new TimeOfDaySettings[]
    {
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Morning,
            duration = 90f,
            lightColor = new Color(1f, 0.88f, 0.72f), lightIntensity = 0.95f,
            ambientColor = new Color(0.3f, 0.31f, 0.4f), ambientIntensity = 0.85f,
            fogColor = new Color(0.7f, 0.79f, 0.9f), fogDensity = 0.007f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.AfterNoon,
            duration = 220f,
            lightColor = new Color(1f, 0.97f, 0.88f), lightIntensity = 1.2f,
            ambientColor = new Color(0.38f, 0.37f, 0.34f), ambientIntensity = 1f,
            fogColor = new Color(0.85f, 0.87f, 0.88f), fogDensity = 0.0045f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Sunset,
            duration = 65f,
            lightColor = new Color(1f, 0.82f, 0.64f), lightIntensity = 0.92f,
            ambientColor = new Color(0.30f, 0.28f, 0.34f), ambientIntensity = 0.80f,
            fogColor = new Color(0.86f, 0.70f, 0.56f), fogDensity = 0.008f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Night,
            duration = 150f,
            lightColor = new Color(0.5f, 0.6f, 0.95f), lightIntensity = 0.4f,
            ambientColor = new Color(0.1f, 0.1f, 0.2f), ambientIntensity = 0.55f,
            fogColor = new Color(0.09f, 0.09f, 0.19f), fogDensity = 0.015f
        }
    };

    [Header("Luz direccional")]
    [SerializeField] private Light directionalLight;

    [Header("Transiciones")]
    [Tooltip("Duración de la transición entre periodos del día en segundos.")]
    [SerializeField] private float transitionDuration = 16f;
    [Tooltip("Usar transiciones suaves entre periodos (requiere más recursos).")]
    [SerializeField] private bool useSmoothTransitions = true;

    [Header("Control de entorno")]
    [Tooltip("Si es true, el ciclo controlará la luz ambiental global.")]
    [SerializeField] private bool controlAmbientLight = true;
    [Tooltip("Si es true, el ciclo controlará la niebla global.")]
    [SerializeField] private bool controlFog = true;

    [Header("Clima - Lluvia")]
    [Tooltip("Prefab del sistema de partículas de lluvia.")]
    [SerializeField] private GameObject rainPrefab;
    [Tooltip("Duración aleatoria (min, max) en segundos de un evento de lluvia sorteado por el temporizador de clima independiente (ver Header(\"Clima — sorteo independiente del periodo\") más abajo). No se usa si la lluvia se arranca a mano con una duración explícita (StartRain(duration), narrativa, tests).")]
    [SerializeField] private Vector2 rainDurationRange = new Vector2(40f, 100f);
    [Tooltip("Segundos que tardan en desaparecer las partículas al detener la lluvia.")]
    [SerializeField] private float rainFadeOutTime = 3f;

    [Header("Clima - Oscurecimiento por lluvia")]
    [Tooltip("Multiplicador de la intensidad de la luz direccional mientras llueve a tope (1 = sin cambio).")]
    [SerializeField, Range(0f, 1f)] private float rainLightIntensityMultiplier = 0.55f;
    [Tooltip("Multiplicador de la densidad de niebla mientras llueve a tope (1 = sin cambio, más alto = más densa).")]
    [SerializeField, Range(1f, 6f)] private float rainFogDensityMultiplier = 2.5f;
    [Tooltip("Suelo ABSOLUTO de intensidad de la luz direccional mientras llueve a tope. Sin esto, rainLightIntensityMultiplier se aplica sobre la intensidad que ya tenga el periodo actual, así que un periodo ya oscuro (Night) puede quedarse en negro casi total. Con este suelo, la luz nunca baja de este valor por mucho que se multiplique.")]
    [SerializeField, Range(0f, 1f)] private float rainMinLightIntensity = 0.28f;

    [Header("Clima - Niebla ocasional")]
    [Tooltip("Prefab opcional de niebla volumétrica (partículas) para el evento de niebla ocasional. Si es null, solo se espesa la niebla global (RenderSettings.fog), sin partículas.")]
    [SerializeField] private GameObject mistPrefab;
    [Tooltip("Duración aleatoria (min, max) en segundos de un evento de niebla ocasional.")]
    [SerializeField] private Vector2 mistDurationRange = new Vector2(20f, 45f);
    [Tooltip("Segundos que tardan en desaparecer las partículas al detener la niebla ocasional.")]
    [SerializeField] private float mistFadeOutTime = 4f;
    [Tooltip("Multiplicador de la densidad de niebla mientras está activa la niebla ocasional a tope (1 = sin cambio).")]
    [SerializeField, Range(1f, 8f)] private float mistFogDensityMultiplier = 4f;
    [Tooltip("Segundos que tarda en espesar/disipar la niebla ocasional.")]
    [SerializeField] private float mistTransitionDuration = 8f;

    [Header("Clima - Viento (30 ago 2026)")]
    [Tooltip("Prefab OPCIONAL de partículas de viento. Vacío = rachas y hojas generadas en código (VfxDeClima.CrearViento), orientadas según DireccionDelViento.")]
    [SerializeField] private GameObject windPrefab;
    [Tooltip("Duración aleatoria (min, max) en segundos de un evento de viento.")]
    [SerializeField] private Vector2 windDurationRange = new Vector2(30f, 90f);
    [Tooltip("Segundos que tardan en desaparecer las partículas (si hay windPrefab asignado) al detener el viento.")]
    [SerializeField] private float windFadeOutTime = 3f;
    [Tooltip("Segundos que tarda el viento en coger toda su fuerza al empezar y en amainar al terminar (FuerzaDelViento de 0 a 1).")]
    [SerializeField] private float windRampDuration = 4f;
    [Tooltip("Velocidad máxima (m/s) del jugador con el viento a toda fuerza, vaya hacia donde vaya. Ver ClimaSobreElJugador.")]
    [SerializeField] private float windVelocidadConViento = 2.6f;
    [Tooltip("Velocidad (m/s) a la que anda el jugador de cara contra el viento con el viento a toda fuerza. Ver ClimaSobreElJugador.")]
    [SerializeField] private float windVelocidadContra = 1.8f;
    [Tooltip("Velocidad (m/s) a la que el viento arrastra al jugador si se queda quieto, con el viento a toda fuerza. 0 = no lo arrastra.")]
    [SerializeField] private float windArrastreQuieto = 0.9f;
    [Tooltip("Volumen del loop de viento (0-1).")]
    [SerializeField, Range(0f, 1f)] private float windLoopVolume = 0.5f;

    [Header("Clima - Nieve")]
    [Tooltip("Prefab OPCIONAL de partículas de nieve. Vacío = copos generados en código (VfxDeClima.CrearNieve).")]
    [SerializeField] private GameObject snowPrefab;
    [Tooltip("Duración aleatoria (min, max) en segundos de una nevada sorteada por el temporizador de clima.")]
    [SerializeField] private Vector2 snowDurationRange = new Vector2(60f, 140f);
    [Tooltip("Segundos que tardan en desaparecer los copos al terminar la nevada.")]
    [SerializeField] private float snowFadeOutTime = 6f;
    [Tooltip("Multiplicador de la intensidad de la luz direccional con la nevada a tope (1 = sin cambio).")]
    [SerializeField, Range(0f, 1f)] private float snowLightIntensityMultiplier = 0.8f;
    [Tooltip("Multiplicador de la densidad de niebla con la nevada a tope (la nieve quita visibilidad a lo lejos).")]
    [SerializeField, Range(1f, 6f)] private float snowFogDensityMultiplier = 2.2f;
    [Tooltip("Event Key del AudioGraphProfile del ambiente de nevada (loop). Vacío = el viento suave de Weather_WindStarted.")]
    [SerializeField] private string snowSfxKey;
    [Tooltip("Volumen del loop de la nevada (0-1). Bajo: la nieve apenas suena.")]
    [SerializeField, Range(0f, 1f)] private float snowLoopVolume = 0.18f;

    [Header("Clima - Tormenta / Rayos (30 ago 2026)")]
    [Tooltip("Una tormenta es lluvia normal (mismas partículas/oscurecimiento/niebla de siempre, ver StartThunderstorm) más rayos periódicos. Rango (min, max) en segundos entre un rayo y el siguiente mientras dura la tormenta.")]
    [SerializeField] private Vector2 thunderstormLightningIntervalRange = new Vector2(8f, 22f);
    [Tooltip("Intensidad (Light.intensity) del destello de cada rayo. Es una luz direccional NUEVA y temporal creada solo para el destello (ver FlashLightningRoutine), no un multiplicador sobre la luz principal — así no interfiere con el oscurecimiento por lluvia (rainLightIntensityMultiplier), que ya recalcula la luz principal cada frame en LateUpdate y se pelearía con cualquier cambio directo sobre directionalLight.intensity.")]
    [SerializeField] private float thunderstormFlashIntensity = 3.5f;
    [Tooltip("Segundos que dura visible cada destello de rayo.")]
    [SerializeField] private float thunderstormFlashDuration = 0.12f;
    [Tooltip("Event Keys de los truenos de los rayos de una tormenta en juego (se elige uno al azar en cada rayo). Truenos con chasquido: los rayos caen a pocas decenas de metros, y un retumbo lejano solo de graves se pierde bajo el loop de lluvia. Si se deja vacío, suena thunderstormThunderSfxKey.")]
    [SerializeField] private string[] truenosDeTormenta = { "SFX_Prologo_Trueno_1", "SFX_Prologo_Trueno_2", "SFX_Prologo_Trueno_3" };
    [Tooltip("Volumen de los truenos de una tormenta en juego (0-1).")]
    [SerializeField, Range(0f, 1f)] private float volumenDelTrueno = 1f;
    [Tooltip("Retraso (min, max) en segundos entre el destello del rayo y el trueno — la luz llega antes que el sonido, igual que en la realidad") ]
    [SerializeField] private Vector2 thunderstormThunderDelayRange = new Vector2(0.3f, 1.8f);
    [Tooltip("Altura (unidades de mundo) desde la que 'cae' el rayo visible, por encima de la posición del jugador. No depende de ningún terreno/collider real (no se hace Raycast): el extremo inferior del rayo se dibuja a la altura actual del jugador, como aproximación razonable del suelo cercano.")]
    [SerializeField] private float thunderstormBoltHeight = 220f;
    [Tooltip("Distancia horizontal (min, max) a la que 'cae' cada rayo respecto al jugador, en una dirección aleatoria — para que se vea caer en el paisaje, no encima del jugador ni siempre en el mismo sitio.")]
    [SerializeField] private Vector2 thunderstormBoltDistanceRange = new Vector2(18f, 55f);
    [Tooltip("Grosor visual del rayo (LineRenderer.widthMultiplier).")]
    [SerializeField] private float thunderstormBoltWidth = 1.4f;
    [Tooltip("Color del rayo — blanco azulado brillante. Canales por encima de 1 a propósito (con Bloom activo aporta el brillo extra del propio rayo), igual que otros elementos emisivos de este proyecto (p.ej. NightSkyStarSpawner.starColor).")]
    [SerializeField] private Color thunderstormBoltColor = new Color(2.2f, 2.3f, 2.7f);
    [Tooltip("Número de segmentos del zigzag del rayo — más segmentos = forma más quebrada/detallada.")]
    [SerializeField, Range(2, 16)] private int thunderstormBoltSegments = 7;
    [Tooltip("Desplazamiento horizontal aleatorio máximo (unidades de mundo) de cada segmento respecto a la línea recta cielo-suelo, para la silueta quebrada característica de un rayo.")]
    [SerializeField] private float thunderstormBoltJitter = 7f;
    [Tooltip("Cuánto tiempo (segundos) queda visible el trazo del rayo en sí, independiente de thunderstormFlashDuration (que solo controla el destello de luz ambiental). 0.12s resultaba casi imperceptible — un solo parpadeo de un frame o dos.")]
    [SerializeField] private float thunderstormBoltVisibleDuration = 0.3f;

    [Header("Clima — sorteo independiente del periodo (30 ago 2026, ver comentario de clase)")]
    [Tooltip("Cada cuánto se vuelve a intentar un cambio de clima (sorteo de lluvia/niebla/viento/tormenta), en segundos — se elige un valor aleatorio dentro de este rango cada vez, para que no sea predecible. Corre en un temporizador propio, totalmente independiente de a qué periodo del día (amanecer/día/atardecer/noche) corresponda ese instante — el clima puede empezar, seguir o terminar en cualquier periodo, incluso a mitad de uno.")]
    [SerializeField] private Vector2 weatherCheckIntervalRange = new Vector2(45f, 110f);
    [Tooltip("Si es falso, el clima no se sortea solo: solo cambia cuando se pide (cinemáticas, LAB).")]
    [SerializeField] private bool sortearClima = true;
    [Tooltip("Probabilidad (0-1) de que arranque una TORMENTA en cada sorteo (lluvia + rayos) — se comprueba ANTES que rainChancePerCheck, así que si sale, no se vuelve a tirar el dado para lluvia normal ese mismo sorteo. Deliberadamente baja: las tormentas deben ser un evento poco frecuente y notable, no una lluvia más.")]
    [SerializeField, Range(0f, 1f)] private float thunderstormChancePerCheck = 0.05f;
    [Tooltip("Probabilidad (0-1) de que arranque a llover (lluvia normal, sin rayos) en cada sorteo — solo se comprueba si el sorteo de tormenta de ese mismo intento no salió.")]
    [SerializeField, Range(0f, 1f)] private float rainChancePerCheck = 0.18f;
    [Tooltip("Probabilidad (0-1) de que empiece a nevar en cada sorteo — solo se comprueba si ni tormenta ni lluvia salieron en ese mismo intento.")]
    [SerializeField, Range(0f, 1f)] private float snowChancePerCheck = 0.08f;
    [Tooltip("Probabilidad (0-1) de que arranque viento en cada sorteo — solo se comprueba si ni tormenta, ni lluvia, ni nieve salieron en ese mismo intento.")]
    [SerializeField, Range(0f, 1f)] private float windChancePerCheck = 0.15f;
    [Tooltip("Probabilidad (0-1) de que arranque niebla ocasional en cada sorteo — la última en probarse (solo si tormenta/lluvia/viento no salieron). No se solapan lluvia y niebla ocasional a propósito (la lluvia ya espesa la niebla por su cuenta).")]
    [SerializeField, Range(0f, 1f)] private float mistChancePerCheck = 0.12f;

    [Header("Clima - Audio (grafo sonoro)")]
    [Tooltip("Event Key del AudioGraphProfile (lista 'Event Sfx') que se reproduce cuando el cielo empieza a nublarse, ANTES de que arranque la lluvia. Déjalo vacío para no reproducir nada.")]
    [SerializeField] private string cloudsBuildingUpSfxKey;
    [Tooltip("Event Key del AudioGraphProfile del SFX/ambiente de lluvia. Se reproduce en LOOP (vía AudioService.PlayLoopingSFX) desde que arranca la lluvia de verdad hasta que para, no como one-shot: así no importa si el clip asignado es más largo o más corto que la lluvia real.")]
    [SerializeField] private string rainStartedSfxKey;
    [Tooltip("Volumen del loop de lluvia (0-1). A 1 tapaba la música y los diálogos del prólogo: «el sfx de lluvia está demasiado fuerte» (prologo20, INC-433).")]
    [SerializeField, Range(0f, 1f)] private float rainLoopVolume = 0.3f;
    [Tooltip("Event Key opcional del AudioGraphProfile para un one-shot adicional cuando para de llover (p.ej. un cue corto de viento amainando). El loop de rainStartedSfxKey se detiene siempre, tenga o no clave este campo.")]
    [SerializeField] private string rainStoppedSfxKey;

    const string RainWeatherSfxLoopId = "Weather_Rain";
    [Tooltip("Event Key del AudioGraphProfile que se reproduce al empezar un evento de niebla ocasional.")]
    [SerializeField] private string mistStartedSfxKey;
    [Tooltip("Event Key del AudioGraphProfile que se reproduce al disiparse la niebla ocasional.")]
    [SerializeField] private string mistStoppedSfxKey;
    [Tooltip("Event Key del AudioGraphProfile del SFX/ambiente de viento. Igual que rainStartedSfxKey, se reproduce en LOOP (AudioService.PlayLoopingSFX) mientras IsWindy es true, no como one-shot. Vacío = Weather_WindStarted.")]
    [SerializeField] private string windStartedSfxKey;
    [Tooltip("Event Key opcional del AudioGraphProfile para un one-shot al terminar el viento. El loop de windStartedSfxKey se detiene siempre, tenga o no clave este campo.")]
    [SerializeField] private string windStoppedSfxKey;
    [Tooltip("Event Key del AudioGraphProfile del trueno — one-shot, se reproduce con retraso tras cada destello de rayo (ver thunderstormThunderDelayRange).")]
    [SerializeField] private string thunderstormThunderSfxKey;

    [Header("Ciclo")]
    [Tooltip("Si es falso, no avanza automáticamente el ciclo.")]
    [SerializeField] private bool autoAdvance = true;
    [Tooltip("Índice del periodo inicial (0 = primero en la lista).")]
    [SerializeField] private int startingTimeIndex = 0;

    [Header("Eventos")]
    [SerializeField] private UnityEvent<TimeOfDay> onTimeOfDayChanged;
    [SerializeField] private UnityEvent onCloudsBuildingUp;
    [SerializeField] private UnityEvent onRainStarted;
    [SerializeField] private UnityEvent onRainStopped;
    [SerializeField] private UnityEvent onMistStarted;
    [SerializeField] private UnityEvent onMistStopped;
    [SerializeField] private UnityEvent onWindStarted;
    [SerializeField] private UnityEvent onWindStopped;
    [SerializeField] private UnityEvent onThunderstormStarted;
    [SerializeField] private UnityEvent onThunderstormStopped;

    public event Action<TimeOfDay> TimeOfDayChanged;

    public event Action CloudsBuildingUp;
    public event Action RainStarted;
    public event Action RainStopped;
    public event Action MistStarted;
    public event Action MistStopped;
    public event Action WindStarted;
    public event Action WindStopped;

    public event Action ThunderstormStarted;
    public event Action ThunderstormStopped;
    public event Action SnowStarted;
    public event Action SnowStopped;
    public event Action CloudsClearing;
    [SerializeField] private UnityEvent onCloudsClearing;

    public TimeOfDay CurrentTimeOfDay { get; private set; }
    public bool IsRaining { get; private set; }
    public bool IsMisty { get; private set; }
    public bool IsWindy { get; private set; }

    public bool IsThunderstorm { get; private set; }
    public bool IsSnowing { get; private set; }

    public Vector3 DireccionDelViento { get; private set; } = Vector3.forward;

    public float FuerzaDelViento { get; private set; }

    public float FuerzaDelVientoAqui => AnclaDeClima == null && IsSkyboxLockedByEnvironment() ? 0f : FuerzaDelViento;
    public float TimeProgress => _currentDuration > 0 ? Mathf.Clamp01(_timeElapsed / _currentDuration) : 1f;

    public float DuracionDeNublarse => duracionDeNublarse;

    [Header("Recorrido de los astros")]
    [Tooltip("Rumbo por el que salen el sol y la luna, en grados sobre el plano horizontal.")]
    [SerializeField] private float rumboDelAmanecer = 90f;
    [SerializeField, Range(1f, 89f)] private float alturaMaximaDelSol = 55f;
    [SerializeField, Range(1f, 89f)] private float alturaMaximaDeLaLuna = 50f;
    [Tooltip("Elevación mínima de la luz que produce sombras, independiente del disco del astro.")]
    [SerializeField, Range(1f, 89f)] private float alturaMinimaDeLaLuz = 18f;

    public float HoraDelDia
    {
        get
        {
            TimeOfDay franja = timeSettings != null && timeSettings.Length > 0
                ? timeSettings[Mathf.Clamp(_currentIndex, 0, timeSettings.Length - 1)].timeOfDay : CurrentTimeOfDay;
            Vector2 horas = HorasDeLaFranja(franja);
            return Mathf.Repeat(Mathf.Lerp(horas.x, horas.y, TimeProgress), 24f);
        }
    }
    public Vector3 DireccionDelSol { get; private set; } = Vector3.right;
    public Vector3 DireccionDeLaLuna { get; private set; } = Vector3.left;

    private static Vector2 HorasDeLaFranja(TimeOfDay franja)
    {
        switch (franja)
        {
            case TimeOfDay.Morning: return new Vector2(6f, 9f);
            case TimeOfDay.AfterNoon: return new Vector2(9f, 18f);
            case TimeOfDay.Sunset: return new Vector2(18f, 20.5f);
            case TimeOfDay.Night: return new Vector2(20.5f, 30f);
            default: return new Vector2(9f, 18f);
        }
    }

    private Vector3 DireccionDelAstro(float fase, float alturaMaxima)
    {
        float elevacion = Mathf.Sin(fase) * alturaMaxima;
        float rumbo = rumboDelAmanecer + fase * Mathf.Rad2Deg;
        return Quaternion.Euler(-elevacion, rumbo, 0f) * Vector3.forward;
    }

    private void ActualizarDireccionesDeAstros()
    {
        float hora = HoraDelDia;
        float horaNocturna = hora < 6f ? hora + 24f : hora;
        // La mitad diurna dura 14 horas; la nocturna dura 10. Ambas cruzan el horizonte sin saltos.
        float faseSolar = hora >= 6f && hora <= 20f ? Mathf.PI * (hora - 6f) / 14f
            : Mathf.PI + Mathf.PI * (horaNocturna - 20f) / 10f;
        float faseLunar = hora >= 6f && hora < 20f ? Mathf.PI + Mathf.PI * (hora - 6f) / 14f
            : Mathf.PI * (horaNocturna - 20f) / 10f;
        DireccionDelSol = DireccionDelAstro(faseSolar, alturaMaximaDelSol);
        DireccionDeLaLuna = DireccionDelAstro(faseLunar, alturaMaximaDeLaLuna);
    }

    private Vector3 DireccionDeLuzRecortada(Vector3 astro)
    {
        float elevacion = Mathf.Max(alturaMinimaDeLaLuz, Mathf.Asin(Mathf.Clamp(astro.y, -1f, 1f)) * Mathf.Rad2Deg);
        float rumbo = Mathf.Atan2(astro.x, astro.z) * Mathf.Rad2Deg;
        return Quaternion.Euler(-elevacion, rumbo, 0f) * Vector3.forward;
    }

    private void ActualizarLuzDeAstros()
    {
        if (directionalLight == null) return;
        Vector3 direccion = Vector3.Slerp(DireccionDeLuzRecortada(DireccionDelSol),
            DireccionDeLuzRecortada(DireccionDeLaLuna), PesoDeNoche).normalized;
        directionalLight.transform.rotation = Quaternion.LookRotation(-direccion, Vector3.up);
    }

    private float AtenuacionDelRelevo => 1f - 0.6f * 4f * PesoDeNoche * (1f - PesoDeNoche);

    private int _currentIndex;
    private float _timeElapsed;
    private float _currentDuration;
    private bool _isTransitioning;

    private Material _runtimeSkybox;

    [Header("Cielo y nubosidad")]
    [SerializeField, Min(0f)] private float duracionDelFundidoDelCielo = 45f;
    [SerializeField, Min(0.01f)] private float duracionDeNublarse = 40f;
    [SerializeField, Min(0.01f)] private float duracionDeDespejarse = 60f;
    [SerializeField] private Color colorDeNubesDeLluvia = new Color(0.52f, 0.58f, 0.66f);
    [SerializeField] private Color colorDeNubesDeTormenta = new Color(0.22f, 0.27f, 0.34f);
    [SerializeField] private Color colorDeNubesDeNieve = new Color(0.86f, 0.88f, 0.92f);
    private Material _cieloDeSalida;
    private Material _destinoDelCielo;
    private float _tiempoDeFundido;
    private bool _actualizarAmbientePendiente;
    private bool _lluviaSolicitada, _nieveSolicitada;
    private float _objetivoAnterior;
    private bool _despejadoPendiente;
    private float _inicioDeNubosidad, _tiempoDeNubosidad;
    private Color _colorDeClima;
    private static readonly int NubosidadId = Shader.PropertyToID("_Nubosidad");
    private static readonly int NubesClimaId = Shader.PropertyToID("_ColorDeNubesDeClima");
    private static readonly int SombraClimaId = Shader.PropertyToID("_ColorDeNubesDeClimaEnSombra");
    private static readonly int FrenteId = Shader.PropertyToID("_AvanceDelFrente");
    private static readonly int HorizonteId = Shader.PropertyToID("_ColorHorizonte");
    private static readonly int VientoCieloId = Shader.PropertyToID("_CieloViento");
    private static readonly int DireccionFrenteId = Shader.PropertyToID("_CieloDireccionDelFrente");
    private Vector3 _direccionDelFrente = Vector3.forward;
    private FundidoDePeso _fundidoDeTormenta, _fundidoDeNieve, _fundidoDePrecipitacion;

    private struct FundidoDePeso
    {
        private float _objetivo, _inicio, _tiempo;
        private bool _iniciado;

        public float Actualizar(float actual, float objetivo, float nublarse, float despejarse)
        {
            if (!_iniciado || objetivo != _objetivo)
            {
                _iniciado = true;
                _inicio = actual;
                _objetivo = objetivo;
                _tiempo = 0f;
            }
            _tiempo += Time.deltaTime;
            float duracion = objetivo >= _inicio ? nublarse : despejarse;
            return Mathf.Lerp(_inicio, objetivo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_tiempo / Mathf.Max(0.01f, duracion))));
        }
    }

    private Material ObtenerCielo(TimeOfDaySettings franja)
    {
        if (franja.skybox != null) return franja.skybox;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[DayNightCycle] La franja {franja.timeOfDay} no tiene cielo; se usa otra franja disponible.", this);
#endif
        for (int i = 0; i < timeSettings.Length; i++)
            if (timeSettings[i].skybox != null) return timeSettings[i].skybox;
        return null;
    }

    private void CrearCielo(TimeOfDaySettings franja)
    {
        if (_runtimeSkybox != null) return;
        Material fuente = ObtenerCielo(franja);
        if (fuente != null) _runtimeSkybox = new Material(fuente);
        else
        {
            Shader shader = Shader.Find("El Sendero/Cielo");
            if (shader == null) return;
            _runtimeSkybox = new Material(shader);
        }
        _runtimeSkybox.hideFlags = HideFlags.HideAndDontSave;
        _cieloDeSalida = new Material(_runtimeSkybox) { hideFlags = HideFlags.HideAndDontSave };
        ReapplyPendingSkybox();
    }

    private void IniciarFundidoDelCielo(TimeOfDaySettings franja, bool inmediato)
    {
        CrearCielo(franja);
        Material destino = ObtenerCielo(franja);
        if (_runtimeSkybox == null || destino == null) return;
        if (inmediato || duracionDelFundidoDelCielo <= 0f)
        {
            _destinoDelCielo = null;
            _runtimeSkybox.CopyPropertiesFromMaterial(destino);
            if (Application.isPlaying) _actualizarAmbientePendiente = true;
            else DynamicGI.UpdateEnvironment();
        }
        else
        {
            // La copia conserva el estado visible si otra franja interrumpe el fundido.
            _cieloDeSalida.CopyPropertiesFromMaterial(_runtimeSkybox);
            _destinoDelCielo = destino;
            _tiempoDeFundido = 0f;
        }
        ReapplyPendingSkybox();
    }

    private void FijarNubosidadCompleta()
    {
        PesoDePrecipitacion = 1f;
        Nublado = _inicioDeNubosidad = _objetivoAnterior = 1f;
        _tiempoDeNubosidad = duracionDeNublarse;
        _fundidoDeTormenta = _fundidoDeNieve = _fundidoDePrecipitacion = default;
        if (!IsWindy) DireccionDelViento = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
        _direccionDelFrente = DireccionDelViento;
        if (IsThunderstorm) PesoDeTormenta = 1f;
    }

    private float ObjetivoDeNubosidad()
    {
        if (_lluviaSolicitada || IsRaining || IsThunderstorm || _nieveSolicitada || IsSnowing) return 1f;
        if (IsMisty) return 0.5f;
        return IsWindy ? 0.3f : 0f;
    }

    private void AvisarDespejado()
    {
        if (!_despejadoPendiente || ObjetivoDeNubosidad() > 0f) return;
        _despejadoPendiente = false;
        onCloudsClearing?.Invoke();
        CloudsClearing?.Invoke();
    }

    private void ActualizarCieloYClima()
    {
        float objetivo = ObjetivoDeNubosidad();
        if (objetivo != _objetivoAnterior)
        {
            _inicioDeNubosidad = Nublado;
            _tiempoDeNubosidad = 0f;
            if (objetivo > _objetivoAnterior)
            {
                if (!IsWindy)
                    DireccionDelViento = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
                _direccionDelFrente = DireccionDelViento;
            }
            // El despejado también llega tras finalizar un clima secundario que mantiene nubes.
            if (objetivo == 0f && _objetivoAnterior > 0f) AvisarDespejado();
            _objetivoAnterior = objetivo;
        }
        _tiempoDeNubosidad += Time.deltaTime;
        float duracion = objetivo >= _inicioDeNubosidad ? duracionDeNublarse : duracionDeDespejarse;
        float progreso = Mathf.Clamp01(_tiempoDeNubosidad / Mathf.Max(0.01f, duracion));
        Nublado = Mathf.Lerp(_inicioDeNubosidad, objetivo, Mathf.SmoothStep(0f, 1f, progreso));
        PesoDeTormenta = _fundidoDeTormenta.Actualizar(PesoDeTormenta, IsThunderstorm ? 1f : 0f, duracionDeNublarse, duracionDeDespejarse);
        PesoDeNieve = _fundidoDeNieve.Actualizar(PesoDeNieve, _nieveSolicitada || IsSnowing ? 1f : 0f, duracionDeNublarse, duracionDeDespejarse);
        PesoDePrecipitacion = _fundidoDePrecipitacion.Actualizar(PesoDePrecipitacion, HayNubesDePrecipitacion ? 1f : 0f, duracionDeNublarse, duracionDeDespejarse);
        if (_runtimeSkybox == null) return;
        if (_destinoDelCielo != null)
        {
            _tiempoDeFundido += Time.deltaTime;
            float t = Mathf.Clamp01(_tiempoDeFundido / Mathf.Max(0.01f, duracionDelFundidoDelCielo));
            _runtimeSkybox.Lerp(_cieloDeSalida, _destinoDelCielo, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f)
            {
                _destinoDelCielo = null;
                _actualizarAmbientePendiente = true;
            }
        }
        Color horizonte = _runtimeSkybox.HasProperty(HorizonteId) ? _runtimeSkybox.GetColor(HorizonteId) : Color.white;
        float brillo = Mathf.Max(0.18f, horizonte.r * 0.2126f + horizonte.g * 0.7152f + horizonte.b * 0.0722f);
        _colorDeClima = Color.Lerp(Color.Lerp(colorDeNubesDeLluvia, colorDeNubesDeTormenta, PesoDeTormenta),
            colorDeNubesDeNieve, PesoDeNieve) * brillo * Mathf.Lerp(1f, 0.75f, PesoDeNoche);
        _colorDeClima.a = 1f;
        float frente = objetivo > _inicioDeNubosidad && objetivo == 1f ? Mathf.Sin(progreso * Mathf.PI) : 0f;
        // Las propiedades meteorológicas se escriben después del Lerp incluso con cielo bloqueado.
        _runtimeSkybox.SetFloat(NubosidadId, Nublado);
        _runtimeSkybox.SetColor(NubesClimaId, _colorDeClima);
        Color sombra = _colorDeClima * 0.65f;
        sombra.a = 1f;
        _runtimeSkybox.SetColor(SombraClimaId, sombra);
        _runtimeSkybox.SetFloat(FrenteId, frente);
        float velocidad = 0.1f + FuerzaDelViento * 0.7f + PesoDeTormenta * 0.5f;
        Shader.SetGlobalVector(VientoCieloId, new Vector4(DireccionDelViento.x * velocidad, DireccionDelViento.z * velocidad, 0f, 0f));
        Shader.SetGlobalVector(DireccionFrenteId, new Vector4(_direccionDelFrente.x, _direccionDelFrente.z, 0f, 0f));
        if (_actualizarAmbientePendiente)
        {
            _actualizarAmbientePendiente = false;
            DynamicGI.UpdateEnvironment();
        }
    }

    private GameObject _activeRainInstance;
    private Coroutine _rainCoroutine;
    private Coroutine _transitionCoroutine;
    private Coroutine _rainFadeCoroutine;

    private float _baseLightIntensity = -1f;

    public float Nublado { get; private set; }
    public float PesoDeTormenta { get; private set; }
    public float PesoDeNieve { get; private set; }
    public float PesoDePrecipitacion { get; private set; }
    public bool HayNubesDePrecipitacion => _lluviaSolicitada || _nieveSolicitada || IsRaining || IsSnowing || IsThunderstorm;
    /// Lluvia pedida, tanto si ya cae como si el cielo aún se está cubriendo (incluye la de la tormenta).
    public bool LluviaPedida => _lluviaSolicitada || IsRaining;
    /// Nieve pedida, tanto si ya cae como si el cielo aún se está cubriendo.
    public bool NievePedida => _nieveSolicitada || IsSnowing;

    public float PesoDeNoche { get; private set; }

    public static float NocheActual => Instance != null ? Instance.PesoDeNoche : 0f;

    private bool _isCloudBuildingUp;

    private GameObject _activeMistInstance;
    private Coroutine _mistCoroutine;
    private Coroutine _mistFadeCoroutine;
    private Coroutine _mistDarkenCoroutine;
    private float _mistAmount;

    private GameObject _activeWindInstance;
    private Coroutine _windCoroutine;
    private Coroutine _windFadeCoroutine;
    const string WindWeatherSfxLoopId = "Weather_Wind";
    const string VientoPorDefecto = "Weather_WindStarted";

    private GameObject _activeSnowInstance;
    private Coroutine _snowCoroutine;
    private Coroutine _snowFadeCoroutine;
    const string SnowWeatherSfxLoopId = "Weather_Snow";

    private Coroutine _thunderstormCoroutine;
    private Coroutine _lightningCoroutine;

    private Material _lightningBoltMaterial;

    private bool _zoneMistForced;

    private float _baseFogDensity;
    private Color _baseFogColor;

    private bool _outdoorWeatherSuppressedIndoors;

    private bool _wasCinematicOverrideActive;

    private bool _wasMinigameActive;

    public Material SkyboxEnUso => _runtimeSkybox;

    void Awake()
    {
        if (timeSettings == null || timeSettings.Length == 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[DayNightCycle] No hay periodos configurados en timeSettings.");
#endif
            enabled = false;
            return;
        }

        _currentIndex = Mathf.Clamp(startingTimeIndex, 0, timeSettings.Length - 1);

        Instance = this;

        Sun = directionalLight != null ? directionalLight.transform : null;

        CrearCielo(timeSettings[_currentIndex]);

        if (controlAmbientLight)
            RenderSettings.ambientMode = AmbientMode.Flat;

        RenderSettings.fog = controlFog;

        _lightningBoltMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (Sun == (directionalLight != null ? directionalLight.transform : null)) Sun = null;

        if (_runtimeSkybox != null)
        {
            Destroy(_runtimeSkybox);
            _runtimeSkybox = null;
        }
        if (_cieloDeSalida != null) Destroy(_cieloDeSalida);
        if (_lightningBoltMaterial != null)
        {
            Destroy(_lightningBoltMaterial);
            _lightningBoltMaterial = null;
        }
    }

    void OnEnable()
    {
        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        var ec = EnvironmentController.Instance;
        _outdoorWeatherSuppressedIndoors = ec != null && ec.CurrentMode == EnvironmentMode.Interior;

        StartCoroutine(InitializeCycleDelayed());

        StartCoroutine(WeatherRollLoop());
    }

    IEnumerator InitializeCycleDelayed()
    {
        yield return null;
        InitializeCycle();
    }

    IEnumerator WeatherRollLoop()
    {
        while (true)
        {
            float wait = UnityEngine.Random.Range(weatherCheckIntervalRange.x, weatherCheckIntervalRange.y);
            yield return new WaitForSeconds(wait);
            TryRollWeather();
        }
    }

    void TryRollWeather()
    {
        if (IsRaining || _isCloudBuildingUp || IsMisty || IsWindy || IsThunderstorm || IsSnowing) return;
        if (TagMinigameController.IsAnyMinigameActive) return;
        if (!PuedeSortearClima) return;

        if (_zoneMistForced) { TryActivateZoneMist(); return; }

        if (UnityEngine.Random.value < thunderstormChancePerCheck)
        {
            StartThunderstorm(UnityEngine.Random.Range(rainDurationRange.x, rainDurationRange.y));
        }
        else if (UnityEngine.Random.value < rainChancePerCheck)
        {
            StartRain(UnityEngine.Random.Range(rainDurationRange.x, rainDurationRange.y));
        }
        else if (UnityEngine.Random.value < snowChancePerCheck)
        {
            StartSnow(UnityEngine.Random.Range(snowDurationRange.x, snowDurationRange.y));
        }
        else if (UnityEngine.Random.value < windChancePerCheck)
        {
            StartWind(UnityEngine.Random.Range(windDurationRange.x, windDurationRange.y));
        }
        else if (UnityEngine.Random.value < mistChancePerCheck)
        {
            StartMist(UnityEngine.Random.Range(mistDurationRange.x, mistDurationRange.y));
        }
    }

    void OnDisable()
    {
        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        StopAllCoroutines();
        IsRaining = false;
        _isCloudBuildingUp = false;
        Nublado = 0f;
        _lluviaSolicitada = _nieveSolicitada = false;
        _destinoDelCielo = null;
        _actualizarAmbientePendiente = false;
        _objetivoAnterior = _inicioDeNubosidad = _tiempoDeNubosidad = 0f;
        _despejadoPendiente = false;
        _rainCoroutine = _rainFadeCoroutine = _transitionCoroutine = null;
        _isTransitioning = false;
        PesoDePrecipitacion = PesoDeNieve = PesoDeTormenta = 0f;
        _fundidoDeTormenta = _fundidoDeNieve = default;

        AudioService.Instance?.StopLoopingSFX(RainWeatherSfxLoopId);

        if (_activeRainInstance != null)
        {
            Destroy(_activeRainInstance);
            _activeRainInstance = null;
        }

        IsMisty = false;
        _mistAmount = 0f;
        _mistDarkenCoroutine = null;
        if (_activeMistInstance != null)
        {
            Destroy(_activeMistInstance);
            _activeMistInstance = null;
        }

        IsWindy = false;
        _windFadeCoroutine = null;
        AudioService.Instance?.StopLoopingSFX(WindWeatherSfxLoopId);
        if (_activeWindInstance != null)
        {
            Destroy(_activeWindInstance);
            _activeWindInstance = null;
        }

        FuerzaDelViento = 0f;
        ClimaSobreElJugador.Soltar();

        IsThunderstorm = false;
        _thunderstormCoroutine = null;
        _lightningCoroutine = null;

        IsSnowing = false;
        _snowCoroutine = null;
        _snowFadeCoroutine = null;
        AudioService.Instance?.StopLoopingSFX(SnowWeatherSfxLoopId);
        if (_activeSnowInstance != null)
        {
            Destroy(_activeSnowInstance);
            _activeSnowInstance = null;
        }
    }

    void LateUpdate()
    {
        ActualizarCieloYClima();
        ClimaSobreElJugador.AplicarFrio(Time.deltaTime);
        bool visible = !(AnclaDeClima == null && IsSkyboxLockedByEnvironment());
        float cobertura = visible ? Nublado : 0f;
        float nieve = visible ? PesoDeNieve : 0f;
        float precipitacion = visible ? PesoDePrecipitacion : 0f;
        float lluvia = precipitacion * (1f - nieve);
        if (visible && directionalLight != null && _baseLightIntensity >= 0f)
        {
            float multiplicador = Mathf.Lerp(rainLightIntensityMultiplier, snowLightIntensityMultiplier, nieve);
            float atenuada = Mathf.Min(_baseLightIntensity, Mathf.Max(_baseLightIntensity * multiplicador, rainMinLightIntensity));
            directionalLight.intensity = Mathf.Lerp(_baseLightIntensity, atenuada, precipitacion)
                * Mathf.Lerp(1f, 0.9f, cobertura) * AtenuacionDelRelevo;
        }
        if (controlFog && visible)
        {
            RenderSettings.fogDensity = _baseFogDensity * Mathf.Lerp(1f, rainFogDensityMultiplier, lluvia)
                * Mathf.Lerp(1f, mistFogDensityMultiplier, _mistAmount)
                * Mathf.Lerp(1f, snowFogDensityMultiplier, precipitacion * nieve);
            RenderSettings.fogColor = Color.Lerp(_baseFogColor, _colorDeClima, precipitacion);
        }
        if (_activeSnowInstance != null || _activeWindInstance != null)
            VfxDeClima.Iluminar(Color.Lerp(RenderSettings.fogColor, Color.white, 0.55f));
    }

    void HandleInteriorEntered()
    {
        _outdoorWeatherSuppressedIndoors = true;
        SetRainVisualActive(false);
        SetMistVisualActive(false);
        SetWindVisualActive(false);
        SetSnowVisualActive(false);
        SetWeatherAudioSuppressed(true);
    }

    void HandleInteriorExited()
    {
        _outdoorWeatherSuppressedIndoors = false;
        SetRainVisualActive(true);
        SetMistVisualActive(true);
        SetWindVisualActive(true);
        SetSnowVisualActive(true);
        SetWeatherAudioSuppressed(false);

        ReapplyPendingSkybox();
    }

    void SetRainVisualActive(bool active)
    {
        if (_activeRainInstance != null)
            _activeRainInstance.SetActive(active);
    }

    void SetMistVisualActive(bool active)
    {
        if (_activeMistInstance != null)
            _activeMistInstance.SetActive(active);
    }

    void SetWindVisualActive(bool active)
    {
        if (_activeWindInstance != null)
            _activeWindInstance.SetActive(active);
    }

    void SetSnowVisualActive(bool active)
    {
        if (_activeSnowInstance != null)
            _activeSnowInstance.SetActive(active);
    }

    void SetWeatherAudioSuppressed(bool suppressed)
    {
        AudioService.Instance?.SetLoopingSFXMuted(RainWeatherSfxLoopId, suppressed);
        AudioService.Instance?.SetLoopingSFXMuted(WindWeatherSfxLoopId, suppressed);
        AudioService.Instance?.SetLoopingSFXMuted(SnowWeatherSfxLoopId, suppressed);
    }

    void PlayWeatherSfx(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) return;
        if (AnclaDeClima == null && ClimaExteriorSilenciado()) return;
        AudioService.Instance?.PlaySFX(eventKey);
    }

    void RaiseCloudsBuildingUp()
    {
        onCloudsBuildingUp?.Invoke();
        CloudsBuildingUp?.Invoke();
        PlayWeatherSfx(cloudsBuildingUpSfxKey);
    }

    void Update()
    {

        var ec = EnvironmentController.Instance;
        bool cinematicActiveNow = ec != null && ec.IsCinematicOverrideActive;
        if (_wasCinematicOverrideActive && !cinematicActiveNow)
            ReapplyPendingSkybox();
        _wasCinematicOverrideActive = cinematicActiveNow;

        bool effectivelyInteriorNow = ec != null && ec.IsEffectivelyInterior && AnclaDeClima == null;
        if (effectivelyInteriorNow != _outdoorWeatherSuppressedIndoors)
        {
            _outdoorWeatherSuppressedIndoors = effectivelyInteriorNow;
            SetRainVisualActive(!effectivelyInteriorNow);
            SetMistVisualActive(!effectivelyInteriorNow);
            SetWindVisualActive(!effectivelyInteriorNow);
            SetSnowVisualActive(!effectivelyInteriorNow);
            SetWeatherAudioSuppressed(effectivelyInteriorNow);
            if (!effectivelyInteriorNow) ReapplyPendingSkybox();
        }

        bool minigameActiveNow = TagMinigameController.IsAnyMinigameActive;
        if (minigameActiveNow && !_wasMinigameActive)
        {

            if (IsThunderstorm) StopThunderstorm();
            else if (IsRaining || _isCloudBuildingUp) StopRain();
        }
        _wasMinigameActive = minigameActiveNow;

        ActualizarViento();

        if (autoAdvance)
        {
            _timeElapsed += Time.deltaTime;
            if (_timeElapsed >= _currentDuration && !_isTransitioning) AdvanceToNextPeriod();
        }
        ActualizarDireccionesDeAstros();
        ActualizarLuzDeAstros();
    }

    bool IsSkyboxLockedByEnvironment()
    {
        return _outdoorWeatherSuppressedIndoors
            || (EnvironmentController.Instance != null && EnvironmentController.Instance.IsCinematicOverrideActive);
    }

    /// El clima de exterior no debe oírse dentro de una casa: ni el loop ni los cues de
    /// arranque/parada. `IsEffectivelyInterior` cubre también el override de cinemática.
    bool ClimaExteriorSilenciado()
    {
        var ec = EnvironmentController.Instance;
        return _outdoorWeatherSuppressedIndoors
            || (ec != null && (ec.IsCinematicOverrideActive || ec.IsEffectivelyInterior));
    }

    void ReapplyPendingSkybox()
    {
        if (!IsSkyboxLockedByEnvironment() && _runtimeSkybox != null) RenderSettings.skybox = _runtimeSkybox;
    }

    void InitializeCycle()
    {
        ApplyTimeOfDay(_currentIndex, immediate: true, invokeEvents: false);
    }

    public void AdvanceToNextPeriod()
    {
        int nextIndex = (_currentIndex + 1) % timeSettings.Length;
        ApplyTimeOfDay(nextIndex, immediate: false, invokeEvents: true);
    }

    public void SetTimeOfDay(TimeOfDay timeOfDay, bool immediate = false)
    {
        for (int i = 0; i < timeSettings.Length; i++)
        {
            if (timeSettings[i].timeOfDay == timeOfDay)
            {
                ApplyTimeOfDay(i, immediate, invokeEvents: true, saltoExplicito: true);
                return;
            }
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[DayNightCycle] TimeOfDay '{timeOfDay}' no encontrado en la configuración.");
#endif
    }

    public void SetNight() => SetTimeOfDay(TimeOfDay.Night, immediate: false);

    public void SetTimeOfDayByIndex(int index, bool immediate = false)
    {
        if (index < 0 || index >= timeSettings.Length)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[DayNightCycle] Índice {index} fuera de rango.");
#endif
            return;
        }
        ApplyTimeOfDay(index, immediate, invokeEvents: true, saltoExplicito: true);
    }

    public void ToggleRain()
    {
        if (IsRaining || _isCloudBuildingUp) StopRain();
        else StartRain();
    }

    public void StartRain(float? duration = null, bool immediate = false)
    {
        if (rainPrefab == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[DayNightCycle] StartRain() no ha hecho nada: rainPrefab no está asignado en este GameObject/escena.");
#endif
            return;
        }
        if (IsRaining || (_isCloudBuildingUp && !_nieveSolicitada))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[DayNightCycle] StartRain() no ha hecho nada: ya está lloviendo o nublándose (IsRaining/_isCloudBuildingUp). Usa StopRain() primero si quieres reiniciarla.");
#endif
            return;
        }

        if (TagMinigameController.IsAnyMinigameActive)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[DayNightCycle] StartRain() no ha hecho nada: hay un minijuego activo (TagMinigameController.IsAnyMinigameActive).");
#endif
            return;
        }

        if (IsSnowing || _nieveSolicitada) StopSnow();

        if (_rainCoroutine != null)
            StopCoroutine(_rainCoroutine);

        float rainDuration = duration ?? 60f;
        _rainCoroutine = StartCoroutine(RainRoutine(rainDuration, immediate));
    }

    public void StopRain()
    {
        if (!IsRaining && !_lluviaSolicitada) return;

        if (_rainCoroutine != null)
        {
            StopCoroutine(_rainCoroutine);
            _rainCoroutine = null;
        }

        if (_isCloudBuildingUp)
        {

            _isCloudBuildingUp = false;
            ReapplyPendingSkybox();
            _lluviaSolicitada = false;
            AvisarDespejado();
            return;
        }

        BeginRainFadeOut();
    }

    public void ToggleMist()
    {
        if (IsMisty) StopMist();
        else StartMist();
    }

    public void StartMist(float? duration = null)
    {
        if (IsMisty) return;

        if (_mistCoroutine != null)
            StopCoroutine(_mistCoroutine);

        float mistDuration = duration ?? UnityEngine.Random.Range(mistDurationRange.x, mistDurationRange.y);
        _mistCoroutine = StartCoroutine(MistRoutine(mistDuration));
    }

    public void StopMist()
    {
        if (!IsMisty) return;

        if (_mistCoroutine != null)
        {
            StopCoroutine(_mistCoroutine);
            _mistCoroutine = null;
        }

        BeginMistFadeOut();
    }

    public void SetZoneMistOverride(bool active)
    {
        if (_zoneMistForced == active) return;
        _zoneMistForced = active;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[DayNightCycle] SetZoneMistOverride({active})");
#endif

        if (active)
        {
            TryActivateZoneMist();
        }
        else if (IsMisty)
        {
            if (_mistCoroutine != null)
            {
                StopCoroutine(_mistCoroutine);
                _mistCoroutine = null;
            }
            BeginMistFadeOut();
        }
    }

    void TryActivateZoneMist()
    {
        if (!_zoneMistForced) return;
        if (IsMisty || IsRaining || _isCloudBuildingUp || IsThunderstorm)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[DayNightCycle] TryActivateZoneMist() bloqueado -- IsMisty={IsMisty} IsRaining={IsRaining} isCloudBuildingUp={_isCloudBuildingUp} IsThunderstorm={IsThunderstorm}");
#endif
            return;
        }
        if (_mistCoroutine != null)
        {
            StopCoroutine(_mistCoroutine);
            _mistCoroutine = null;
        }
        ActivateMist();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[DayNightCycle] TryActivateZoneMist() -> ActivateMist() ejecutado (niebla de zona activa).");
#endif
    }

    void ApplyTimeOfDay(int index, bool immediate, bool invokeEvents, bool saltoExplicito = false)
    {
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
            _isTransitioning = false;
        }

        _currentIndex = index;
        var settings = timeSettings[index];
        _currentDuration = Mathf.Max(1f, settings.duration);
        _timeElapsed = saltoExplicito ? _currentDuration * 0.35f : 0f;
        ActualizarDireccionesDeAstros();

        if (immediate || !useSmoothTransitions || !Application.isPlaying)
        {
            CurrentTimeOfDay = settings.timeOfDay;
            if (!immediate && Application.isPlaying) IniciarFundidoDelCielo(settings, false);
            ApplySettingsImmediate(settings, aplicarCielo: immediate || !Application.isPlaying);
            if (invokeEvents)
            {
                onTimeOfDayChanged?.Invoke(settings.timeOfDay);
                TimeOfDayChanged?.Invoke(settings.timeOfDay);
            }
        }
        else
            _transitionCoroutine = StartCoroutine(TransitionToSettings(settings, invokeEvents));

    }

#if UNITY_EDITOR

    public void PreviewTimeOfDay(TimeOfDay hora)
    {
        if (timeSettings == null) return;

        for (int i = 0; i < timeSettings.Length; i++)
        {
            if (timeSettings[i].timeOfDay != hora) continue;

            CrearCielo(timeSettings[i]);

            if (controlAmbientLight) RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = controlFog;

            _currentIndex = i;
            CurrentTimeOfDay = hora;
            _currentDuration = Mathf.Max(1f, timeSettings[i].duration);
            _timeElapsed = _currentDuration * 0.35f;
            ApplySettingsImmediate(timeSettings[i]);
            ActualizarDireccionesDeAstros();
            ActualizarLuzDeAstros();
            return;
        }

        Debug.LogWarning($"[DayNightCycle] No hay ningún periodo '{hora}' configurado; no se puede previsualizar.");
    }
#endif

    void ApplySettingsImmediate(TimeOfDaySettings settings, bool aplicarCielo = true)
    {
        if (aplicarCielo && !_isTransitioning) IniciarFundidoDelCielo(settings, true);

        if (directionalLight != null)
        {
            directionalLight.color = settings.lightColor;
            directionalLight.intensity = settings.lightIntensity;
            _baseLightIntensity = settings.lightIntensity;
        }
        PesoDeNoche = settings.timeOfDay == TimeOfDay.Night ? 1f : 0f;

        if (controlAmbientLight)
            RenderSettings.ambientLight = settings.ambientColor * settings.ambientIntensity;

        if (controlFog)
        {
            RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogDensity = settings.fogDensity;
            _baseFogColor = settings.fogColor;
            _baseFogDensity = settings.fogDensity;
        }
    }

    IEnumerator TransitionToSettings(TimeOfDaySettings target, bool invokeEvents)
    {
        _isTransitioning = true;

        if (_runtimeSkybox != null && RenderSettings.skybox != _runtimeSkybox && !IsSkyboxLockedByEnvironment())
            RenderSettings.skybox = _runtimeSkybox;

        var light = directionalLight;
        Color startLightColor = light ? light.color : Color.white;
        float startIntensity = _baseLightIntensity >= 0f ? _baseLightIntensity : 1f;
        Color startAmbient = RenderSettings.ambientLight;

        Color startFogColor = _baseFogColor;
        float startFogDensity = _baseFogDensity;

        IniciarFundidoDelCielo(target, false);

        float startNoche = PesoDeNoche;
        float targetNoche = target.timeOfDay == TimeOfDay.Night ? 1f : 0f;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration));
            PesoDeNoche = Mathf.Lerp(startNoche, targetNoche, t);

            if (light != null)
            {
                light.color = Color.Lerp(startLightColor, target.lightColor, t);
                light.intensity = Mathf.Lerp(startIntensity, target.lightIntensity, t);
                _baseLightIntensity = light.intensity;
            }

            if (controlAmbientLight)
                RenderSettings.ambientLight = Color.Lerp(startAmbient, target.ambientColor * target.ambientIntensity, t);

            if (controlFog)
            {
                Color lerpedColor   = Color.Lerp(startFogColor, target.fogColor, t);
                float lerpedDensity = Mathf.Lerp(startFogDensity, target.fogDensity, t);

                _baseFogColor   = lerpedColor;
                _baseFogDensity = lerpedDensity;

                RenderSettings.fogColor   = lerpedColor;
                RenderSettings.fogDensity = lerpedDensity;
            }

            yield return null;
        }

        ApplySettingsImmediate(target);
        CurrentTimeOfDay = target.timeOfDay;

        if (invokeEvents)
        {
            onTimeOfDayChanged?.Invoke(target.timeOfDay);
            TimeOfDayChanged?.Invoke(target.timeOfDay);
        }

        _isTransitioning = false;
        _transitionCoroutine = null;
    }

    IEnumerator RainRoutine(float duration, bool immediate)
    {
        _lluviaSolicitada = true;
        if (immediate) FijarNubosidadCompleta();
        yield return CloudBuildUpRoutine();
        ActivateRain();
        yield return new WaitForSeconds(duration);
        BeginRainFadeOut();
        _rainCoroutine = null;
    }

    IEnumerator CloudBuildUpRoutine()
    {
        _isCloudBuildingUp = true;
        _despejadoPendiente = true;
        RaiseCloudsBuildingUp();
        while (Nublado < 0.8f) yield return null;
        _isCloudBuildingUp = false;
    }

    void ActivateRain()
    {
        if (IsRaining || rainPrefab == null) return;

        if (_rainFadeCoroutine != null)
        {
            StopCoroutine(_rainFadeCoroutine);
            _rainFadeCoroutine = null;
            if (_activeRainInstance != null)
            {
                Destroy(_activeRainInstance);
                _activeRainInstance = null;
            }
        }

        Transform parent = AnclaDeClima != null ? AnclaDeClima :
                           PlayerService.Player != null ? PlayerService.Player.transform :
                           Camera.main != null ? Camera.main.transform : null;

        if (parent != null)
        {
            _activeRainInstance = Instantiate(rainPrefab, parent);
            _activeRainInstance.transform.localPosition = Vector3.zero;
        }
        else
        {
            _activeRainInstance = Instantiate(rainPrefab, transform.position, Quaternion.identity);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[DayNightCycle] No se encontró jugador ni cámara, lluvia instanciada sin padre.");
#endif
        }

        if (AnclaDeClima == null && ClimaExteriorSilenciado())
            _activeRainInstance.SetActive(false);

        IsRaining = true;
        onRainStarted?.Invoke();
        RainStarted?.Invoke();
        GameplayEventLog.Log("LluviaInicio");

        AudioService.Instance?.PlayLoopingSFX(RainWeatherSfxLoopId,
            string.IsNullOrWhiteSpace(rainStartedSfxKey) ? "rain" : rainStartedSfxKey, rainLoopVolume);

        if (AnclaDeClima == null && ClimaExteriorSilenciado())
            AudioService.Instance?.SetLoopingSFXMuted(RainWeatherSfxLoopId, true);

    }

    void BeginRainFadeOut()
    {
        if (!IsRaining) return;

        IsRaining = false;
        onRainStopped?.Invoke();
        RainStopped?.Invoke();
        GameplayEventLog.Log("LluviaFin");

        AudioService.Instance?.StopLoopingSFX(RainWeatherSfxLoopId, rainFadeOutTime);
        PlayWeatherSfx(rainStoppedSfxKey);
        _lluviaSolicitada = false;
        AvisarDespejado();
        ReapplyPendingSkybox();

        if (_rainFadeCoroutine != null)
            StopCoroutine(_rainFadeCoroutine);

        if (_activeRainInstance != null)
            _rainFadeCoroutine = StartCoroutine(RainFadeOutRoutine(_activeRainInstance));

        TryActivateZoneMist();
    }

    IEnumerator RainFadeOutRoutine(GameObject rainInstance)
    {

        var particles = rainInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(rainFadeOutTime);

        if (rainInstance != null)
            Destroy(rainInstance);

        if (_activeRainInstance == rainInstance)
            _activeRainInstance = null;

        _rainFadeCoroutine = null;
    }

    IEnumerator MistRoutine(float duration)
    {
        ActivateMist();
        yield return new WaitForSeconds(duration);
        BeginMistFadeOut();
        _mistCoroutine = null;
    }

    void ActivateMist()
    {
        if (IsMisty) return;

        if (_mistFadeCoroutine != null)
        {
            StopCoroutine(_mistFadeCoroutine);
            _mistFadeCoroutine = null;
            if (_activeMistInstance != null)
            {
                Destroy(_activeMistInstance);
                _activeMistInstance = null;
            }
        }

        if (mistPrefab != null)
        {
            Transform parent = AnclaDeClima != null ? AnclaDeClima :
                               PlayerService.Player != null ? PlayerService.Player.transform :
                               Camera.main != null ? Camera.main.transform : null;

            if (parent != null)
            {
                _activeMistInstance = Instantiate(mistPrefab, parent);
                _activeMistInstance.transform.localPosition = Vector3.zero;
            }
            else
            {
                _activeMistInstance = Instantiate(mistPrefab, transform.position, Quaternion.identity);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[DayNightCycle] No se encontró jugador ni cámara, niebla instanciada sin padre.");
#endif
            }

            if (AnclaDeClima == null && ClimaExteriorSilenciado())
                _activeMistInstance.SetActive(false);
        }

        IsMisty = true;
        onMistStarted?.Invoke();
        MistStarted?.Invoke();

        StartMistAmount(1f);
    }

    void BeginMistFadeOut()
    {
        if (!IsMisty) return;

        IsMisty = false;
        onMistStopped?.Invoke();
        MistStopped?.Invoke();

        StartMistAmount(0f);

        if (_mistFadeCoroutine != null)
            StopCoroutine(_mistFadeCoroutine);

        if (_activeMistInstance != null)
            _mistFadeCoroutine = StartCoroutine(MistFadeOutRoutine(_activeMistInstance));
    }

    void StartMistAmount(float target)
    {
        if (_mistDarkenCoroutine != null)
            StopCoroutine(_mistDarkenCoroutine);
        _mistDarkenCoroutine = StartCoroutine(MistAmountRoutine(target));
    }

    IEnumerator MistAmountRoutine(float target)
    {
        float start = _mistAmount;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, mistTransitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _mistAmount = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        _mistAmount = target;
        _mistDarkenCoroutine = null;
    }

    IEnumerator MistFadeOutRoutine(GameObject mistInstance)
    {
        var particles = mistInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(mistFadeOutTime);

        if (mistInstance != null)
            Destroy(mistInstance);

        if (_activeMistInstance == mistInstance)
            _activeMistInstance = null;

        _mistFadeCoroutine = null;
    }

    void ActualizarViento()
    {
        float objetivo = IsWindy ? 1f : 0f;
        if (FuerzaDelViento != objetivo)
            FuerzaDelViento = Mathf.MoveTowards(FuerzaDelViento, objetivo,
                Time.deltaTime / Mathf.Max(0.01f, windRampDuration));

        float fuerza = AnclaDeClima != null ? 0f : FuerzaDelVientoAqui;
        // Frío: con lluvia o nieve cayendo de verdad, al aire libre.
        bool alAireLibre = AnclaDeClima == null && !IsSkyboxLockedByEnvironment();
        float frio = alAireLibre && (IsRaining || IsSnowing) ? PesoDePrecipitacion : 0f;
        if (fuerza > 0f || frio > 0f || _climaSobreElJugador)
        {
            ClimaSobreElJugador.Actualizar(fuerza, DireccionDelViento, windVelocidadConViento,
                windVelocidadContra, windArrastreQuieto, frio, Time.deltaTime);
            _climaSobreElJugador = fuerza > 0f || frio > 0f;
        }
    }
    private bool _climaSobreElJugador;

    public void ToggleWind()
    {
        if (IsWindy) StopWind();
        else StartWind();
    }

    public void StartWind(float? duration = null)
    {
        if (IsWindy) return;

        if (_windCoroutine != null)
            StopCoroutine(_windCoroutine);

        float windDuration = duration ?? UnityEngine.Random.Range(windDurationRange.x, windDurationRange.y);

        if (FuerzaDelViento <= 0f)
            DireccionDelViento = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
        _windCoroutine = StartCoroutine(WindRoutine(windDuration));
    }

    public void StopWind()
    {
        if (!IsWindy) return;

        if (_windCoroutine != null)
        {
            StopCoroutine(_windCoroutine);
            _windCoroutine = null;
        }

        BeginWindFadeOut();
    }

    IEnumerator WindRoutine(float duration)
    {
        ActivateWind();
        yield return new WaitForSeconds(duration);
        BeginWindFadeOut();
        _windCoroutine = null;
    }

    void ActivateWind()
    {
        if (IsWindy) return;

        if (_windFadeCoroutine != null)
        {
            StopCoroutine(_windFadeCoroutine);
            _windFadeCoroutine = null;
            if (_activeWindInstance != null)
            {
                Destroy(_activeWindInstance);
                _activeWindInstance = null;
            }
        }

        Transform parent = AnclaDeClima != null ? AnclaDeClima :
                           PlayerService.Player != null ? PlayerService.Player.transform :
                           Camera.main != null ? Camera.main.transform : null;

        if (windPrefab == null)
        {
            _activeWindInstance = VfxDeClima.CrearViento(parent, DireccionDelViento);
            if (parent == null) _activeWindInstance.transform.position = transform.position;
        }
        else if (parent != null)
        {
            _activeWindInstance = Instantiate(windPrefab, parent);
            _activeWindInstance.transform.localPosition = Vector3.zero;
        }
        else
        {
            _activeWindInstance = Instantiate(windPrefab, transform.position, Quaternion.identity);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[DayNightCycle] No se encontró jugador ni cámara, viento instanciado sin padre.");
#endif
        }

        if (AnclaDeClima == null && ClimaExteriorSilenciado())
            _activeWindInstance.SetActive(false);

        IsWindy = true;
        onWindStarted?.Invoke();
        WindStarted?.Invoke();
        GameplayEventLog.Log("VientoInicio");

        AudioService.Instance?.PlayLoopingSFX(WindWeatherSfxLoopId,
            string.IsNullOrWhiteSpace(windStartedSfxKey) ? VientoPorDefecto : windStartedSfxKey, windLoopVolume);

        if (AnclaDeClima == null && ClimaExteriorSilenciado())
            AudioService.Instance?.SetLoopingSFXMuted(WindWeatherSfxLoopId, true);
    }

    void BeginWindFadeOut()
    {
        if (!IsWindy) return;

        IsWindy = false;
        onWindStopped?.Invoke();
        WindStopped?.Invoke();
        GameplayEventLog.Log("VientoFin");
        AudioService.Instance?.StopLoopingSFX(WindWeatherSfxLoopId, windFadeOutTime);
        PlayWeatherSfx(windStoppedSfxKey);

        if (_windFadeCoroutine != null)
            StopCoroutine(_windFadeCoroutine);

        if (_activeWindInstance != null)
            _windFadeCoroutine = StartCoroutine(WindFadeOutRoutine(_activeWindInstance));
    }

    IEnumerator WindFadeOutRoutine(GameObject windInstance)
    {
        var particles = windInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(windFadeOutTime);

        if (windInstance != null)
            Destroy(windInstance);

        if (_activeWindInstance == windInstance)
            _activeWindInstance = null;

        _windFadeCoroutine = null;
    }

    public void StartSnow(float? duration = null, bool immediate = false)
    {
        if (IsSnowing || _nieveSolicitada || IsRaining || _isCloudBuildingUp || IsThunderstorm) return;

        if (_snowCoroutine != null)
            StopCoroutine(_snowCoroutine);

        float snowDuration = duration ?? UnityEngine.Random.Range(snowDurationRange.x, snowDurationRange.y);
        _snowCoroutine = StartCoroutine(SnowRoutine(snowDuration, immediate));
    }

    public void StopSnow()
    {
        if (!IsSnowing && !_nieveSolicitada) return;

        if (_snowCoroutine != null)
        {
            StopCoroutine(_snowCoroutine);
            _snowCoroutine = null;
        }

        _nieveSolicitada = false;
        _isCloudBuildingUp = false;
        AvisarDespejado();
        BeginSnowFadeOut();
    }

    IEnumerator SnowRoutine(float duration, bool immediate)
    {
        _nieveSolicitada = true;
        if (immediate)
        {
            FijarNubosidadCompleta();
            PesoDeNieve = 1f;
        }
        yield return CloudBuildUpRoutine();
        ActivateSnow();
        yield return new WaitForSeconds(duration);
        BeginSnowFadeOut();
        _snowCoroutine = null;
    }

    void ActivateSnow()
    {
        if (IsSnowing) return;

        if (_snowFadeCoroutine != null)
        {
            StopCoroutine(_snowFadeCoroutine);
            _snowFadeCoroutine = null;
            if (_activeSnowInstance != null)
            {
                Destroy(_activeSnowInstance);
                _activeSnowInstance = null;
            }
        }

        Transform parent = AnclaDeClima != null ? AnclaDeClima :
                           PlayerService.Player != null ? PlayerService.Player.transform :
                           Camera.main != null ? Camera.main.transform : null;

        if (snowPrefab == null)
        {
            _activeSnowInstance = VfxDeClima.CrearNieve(parent);
            if (parent == null) _activeSnowInstance.transform.position = transform.position;
        }
        else if (parent != null)
        {
            _activeSnowInstance = Instantiate(snowPrefab, parent);
            _activeSnowInstance.transform.localPosition = Vector3.zero;
        }
        else
        {
            _activeSnowInstance = Instantiate(snowPrefab, transform.position, Quaternion.identity);
        }

        bool dentro = AnclaDeClima == null && ClimaExteriorSilenciado();
        if (dentro) _activeSnowInstance.SetActive(false);

        IsSnowing = true;
        SnowStarted?.Invoke();
        GameplayEventLog.Log("NieveInicio");
        AudioService.Instance?.PlayLoopingSFX(SnowWeatherSfxLoopId,
            string.IsNullOrWhiteSpace(snowSfxKey) ? VientoPorDefecto : snowSfxKey, snowLoopVolume);
        if (dentro) AudioService.Instance?.SetLoopingSFXMuted(SnowWeatherSfxLoopId, true);
    }

    void BeginSnowFadeOut()
    {
        if (!IsSnowing) return;

        IsSnowing = false;
        _nieveSolicitada = false;
        AvisarDespejado();
        SnowStopped?.Invoke();
        GameplayEventLog.Log("NieveFin");
        AudioService.Instance?.StopLoopingSFX(SnowWeatherSfxLoopId, snowFadeOutTime);

        if (_snowFadeCoroutine != null)
            StopCoroutine(_snowFadeCoroutine);

        if (_activeSnowInstance != null)
            _snowFadeCoroutine = StartCoroutine(SnowFadeOutRoutine(_activeSnowInstance));
    }

    IEnumerator SnowFadeOutRoutine(GameObject snowInstance)
    {
        var particles = snowInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(snowFadeOutTime);

        if (snowInstance != null)
            Destroy(snowInstance);

        if (_activeSnowInstance == snowInstance)
            _activeSnowInstance = null;

        _snowFadeCoroutine = null;
    }

    public void StartThunderstorm(float? duration = null, bool immediate = false)
    {
        if (IsThunderstorm)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[DayNightCycle] StartThunderstorm() no ha hecho nada: ya hay una tormenta activa.");
#endif
            return;
        }
        if (IsRaining || _isCloudBuildingUp)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[DayNightCycle] StartThunderstorm() no ha hecho nada: ya está lloviendo o nublándose por otro motivo (StartRain/StartThunderstorm no se pueden solapar). Usa StopRain() primero si quieres forzar la tormenta.");
#endif
            return;
        }
        if (TagMinigameController.IsAnyMinigameActive)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[DayNightCycle] StartThunderstorm() no ha hecho nada: hay un minijuego activo.");
#endif
            return;
        }

        if (_thunderstormCoroutine != null)
            StopCoroutine(_thunderstormCoroutine);

        float stormDuration = duration ?? UnityEngine.Random.Range(rainDurationRange.x, rainDurationRange.y);
        _thunderstormCoroutine = StartCoroutine(ThunderstormRoutine(stormDuration, immediate));
    }

    public void StopThunderstorm()
    {
        if (!IsThunderstorm) return;

        if (_thunderstormCoroutine != null)
        {
            StopCoroutine(_thunderstormCoroutine);
            _thunderstormCoroutine = null;
        }

        FinishThunderstorm();
        StopRain();
    }

    IEnumerator ThunderstormRoutine(float duration, bool immediate)
    {
        IsThunderstorm = true;
        onThunderstormStarted?.Invoke();
        ThunderstormStarted?.Invoke();
        GameplayEventLog.Log("TormentaInicio");

        StartRain(duration, immediate);

        if (_lightningCoroutine != null)
            StopCoroutine(_lightningCoroutine);

        _lightningCoroutine = StartCoroutine(LightningLoop(firstStrikeImmediate: true));

        while (_isCloudBuildingUp) yield return null;
        yield return new WaitForSeconds(duration);
        FinishThunderstorm();
        _thunderstormCoroutine = null;
    }

    void FinishThunderstorm()
    {
        if (!IsThunderstorm) return;

        IsThunderstorm = false;
        onThunderstormStopped?.Invoke();
        ThunderstormStopped?.Invoke();
        GameplayEventLog.Log("TormentaFin");

        if (_lightningCoroutine != null)
        {
            StopCoroutine(_lightningCoroutine);
            _lightningCoroutine = null;
        }
    }

    IEnumerator LightningLoop(bool firstStrikeImmediate = false)
    {
        bool first = true;
        while (true)
        {
            if (first && firstStrikeImmediate)
            {

            }
            else
            {

                float wait = AnclaDeClima != null ? UnityEngine.Random.Range(5f, 11f)
                           : UnityEngine.Random.Range(thunderstormLightningIntervalRange.x, thunderstormLightningIntervalRange.y);
                yield return new WaitForSeconds(wait);
            }
            first = false;

            if (AnclaDeClima == null && ClimaExteriorSilenciado()) continue;
            yield return FlashLightningRoutine();
        }
    }

    void SpawnLightningBolt(Vector3? punto = null, bool destello = true)
    {
        if (_lightningBoltMaterial == null)
            return;

        GenerarRayo(destello, punto);
    }

    private void GenerarRayo(bool destello = true, Vector3? punto = null)
    {
        Camera cam = SolYLunaEnElCielo.CamaraActual();
        if (cam == null) cam = Camera.main;

        Vector3 arriba, abajo;
        bool enCuadro = false;

        if (punto.HasValue)
        {
            // Rayo dirigido a un punto concreto en el mundo (suelo, marca, etc.)
            abajo = punto.Value;
            arriba = punto.Value + Vector3.up * thunderstormBoltHeight + UnityEngine.Random.insideUnitSphere * 4f;
            if (arriba.y < abajo.y + 15f) arriba.y = abajo.y + 15f;
        }
        else if (cam != null && cam.isActiveAndEnabled && cam.transform.forward.y > -0.65f)
        {
            // Rayo de fondo en el encuadre de la cámara (técnica cinematográfica con halo y ramas)
            float lejos = Mathf.Min(UnityEngine.Random.Range(120f, 190f), cam.farClipPlane * 0.85f);
            if (lejos >= 30f)
            {
                float x = UnityEngine.Random.value < 0.5f ? UnityEngine.Random.Range(0.10f, 0.36f)
                                                          : UnityEngine.Random.Range(0.64f, 0.90f);
                arriba = cam.ViewportToWorldPoint(new Vector3(x + UnityEngine.Random.Range(-0.05f, 0.05f), 1.10f, lejos));
                abajo = cam.ViewportToWorldPoint(new Vector3(x + UnityEngine.Random.Range(-0.12f, 0.12f),
                                                             UnityEngine.Random.Range(0.28f, 0.46f), lejos));
                enCuadro = arriba.y >= abajo.y + 15f;
            }
        }

        if (!punto.HasValue && !enCuadro)
        {
            // Fallback en espacio de mundo en frente del jugador/cámara
            Transform reference = AnclaDeClima != null ? AnclaDeClima :
                                  PlayerService.Player != null ? PlayerService.Player.transform :
                                  cam != null ? cam.transform : transform;

            float yawBase = cam != null ? cam.transform.eulerAngles.y : reference.eulerAngles.y;
            float boltYaw = yawBase + (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(15f, 55f);
            float distance = UnityEngine.Random.Range(thunderstormBoltDistanceRange.x, thunderstormBoltDistanceRange.y);
            Vector3 offset = (Quaternion.Euler(0f, boltYaw, 0f) * Vector3.forward) * distance;

            abajo = reference.position + offset;
            arriba = abajo + Vector3.up * thunderstormBoltHeight + UnityEngine.Random.insideUnitSphere * 6f;
            if (arriba.y < abajo.y + 15f) arriba.y = abajo.y + 15f;
        }

        Vector3 dir = (abajo - arriba).normalized;
        float largo = Vector3.Distance(arriba, abajo);
        Vector3 lado = cam != null ? cam.transform.right : Vector3.Cross(dir, Vector3.up).normalized;
        if (lado.sqrMagnitude < 0.01f) lado = Vector3.right;

        var raiz = new GameObject("[ThunderstormLightningBolt]");

        const int tramos = 12;
        var puntos = new Vector3[tramos + 1];
        float desvio = 0f;
        for (int i = 0; i <= tramos; i++)
        {
            float t = (float)i / tramos;
            if (i != 0 && i != tramos) desvio = desvio * 0.35f + UnityEngine.Random.Range(-1f, 1f) * largo * 0.06f;
            else desvio = 0f;
            puntos[i] = Vector3.Lerp(arriba, abajo, t) + lado * desvio;
        }

        float distCam = cam != null ? Vector3.Distance(cam.transform.position, (arriba + abajo) * 0.5f) : 100f;
        float grosor = Mathf.Clamp(distCam * 0.0065f, 0.25f, 2.5f);

        // Doble trazo cinematográfico: halo translúcido + núcleo brillante puro
        TrazoDelRayo(raiz, puntos, grosor * 4.5f, new Color(0.55f, 0.65f, 1f, 0.22f), -1);   // halo
        TrazoDelRayo(raiz, puntos, grosor, thunderstormBoltColor, 0);                         // núcleo

        // Ramificación lateral del rayo
        int desde = UnityEngine.Random.Range(tramos / 4, tramos / 2);
        float haciaFuera = (cam != null && cam.WorldToViewportPoint(abajo).x < 0.5f) ? -1f : 1f;
        var rama = new Vector3[6];
        rama[0] = puntos[desde];
        Vector3 bajada = dir * (largo * 0.07f);
        for (int i = 1; i < rama.Length; i++)
            rama[i] = rama[i - 1] + bajada + lado * (haciaFuera * largo * UnityEngine.Random.Range(0.02f, 0.06f));
        TrazoDelRayo(raiz, rama, grosor * 2.6f, new Color(0.55f, 0.65f, 1f, 0.15f), -1);
        TrazoDelRayo(raiz, rama, grosor * 0.55f, thunderstormBoltColor * 0.9f, 0);

        // Parpadeo estroboscópico realista (5 pulsos)
        StartCoroutine(Co_ParpadeoDelRayoEnCuadro(raiz));

        // Fogonazo en pantalla completa
        if (destello) Sendero.Core.Feedback.FeedbackService.ScreenFlash(new Color(0.82f, 0.88f, 1f, 0.16f), 0.12f);
    }

    void TrazoDelRayo(GameObject raiz, Vector3[] puntos, float ancho, Color color, int orden)
    {
        var go = new GameObject("Trazo");
        go.transform.SetParent(raiz.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.material = _lightningBoltMaterial;
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = ancho;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f));
        line.numCapVertices = 2;
        line.numCornerVertices = 1;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = orden;
        line.positionCount = puntos.Length;
        line.SetPositions(puntos);
    }

    IEnumerator Co_ParpadeoDelRayoEnCuadro(GameObject raiz)
    {
        float[] pasos = { 0.08f, 0.05f, 0.13f, 0.06f, 0.16f };
        bool on = true;
        foreach (float p in pasos)
        {
            if (raiz == null) yield break;
            raiz.SetActive(on);
            yield return new WaitForSecondsRealtime(p);
            on = !on;
        }
        if (raiz != null) Destroy(raiz);
    }

    public string ClaveDeTrueno => string.IsNullOrWhiteSpace(thunderstormThunderSfxKey) ? TruenoPorDefecto : thunderstormThunderSfxKey;

    public void LanzarRayo(Vector3 punto, bool conTrueno = true, string trueno = null, float volumen = 1f, bool destello = true)
    {
        if (!isActiveAndEnabled) return;
        SpawnLightningBolt(punto, destello);
        if (conTrueno) ProgramarTrueno(trueno, volumen);
        if (destello) StartCoroutine(DestelloDelRayo());
    }
    private void ProgramarTrueno(string clave = null, float volumen = 1f)
    {
        float minimo = Mathf.Clamp(thunderstormThunderDelayRange.x, 0.2f, 0.9f);
        float maximo = Mathf.Clamp(thunderstormThunderDelayRange.y, minimo, 0.9f);
        AudioService.Instance?.ProgramarTrueno(string.IsNullOrWhiteSpace(clave) ? ClaveDeTrueno : clave,
            UnityEngine.Random.Range(minimo, maximo), volumen);
    }
    IEnumerator FlashLightningRoutine()
    {
        SpawnLightningBolt();

        if (AnclaDeClima == null && truenosDeTormenta != null && truenosDeTormenta.Length > 0)
            ProgramarTrueno(truenosDeTormenta[UnityEngine.Random.Range(0, truenosDeTormenta.Length)], volumenDelTrueno);
        else
            ProgramarTrueno();
        yield return DestelloDelRayo();
    }
    private IEnumerator DestelloDelRayo()
    {
        var flashObj = new GameObject("[ThunderstormLightningFlash]");
        var flash = flashObj.AddComponent<Light>();
        flash.type = LightType.Directional;
        flash.color = Color.white;
        flash.intensity = thunderstormFlashIntensity;
        flash.shadows = LightShadows.None;
        if (directionalLight != null) flashObj.transform.rotation = directionalLight.transform.rotation;

        Destroy(flashObj, Mathf.Max(0.01f, thunderstormFlashDuration));
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, thunderstormFlashDuration));
        if (flashObj != null) Destroy(flashObj);
    }
    const string TruenoPorDefecto = "Weather_Thunder";

    [ContextMenu("Testeo: Ir a Amanecer")]
    public void DebugGoToMorning() => SetTimeOfDay(TimeOfDay.Morning, immediate: true);

    [ContextMenu("Testeo: Ir a Día")]
    public void DebugGoToDay() => SetTimeOfDay(TimeOfDay.AfterNoon, immediate: true);

    [ContextMenu("Testeo: Ir a Atardecer")]
    public void DebugGoToSunset() => SetTimeOfDay(TimeOfDay.Sunset, immediate: true);

    [ContextMenu("Testeo: Ir a Noche")]
    public void DebugGoToNight() => SetTimeOfDay(TimeOfDay.Night, immediate: true);

    [ContextMenu("Testeo: Avanzar al siguiente periodo")]
    public void DebugAdvanceTime() => AdvanceToNextPeriod();

    [ContextMenu("Testeo: Lluvia (iniciar)")]
    public void DebugStartRain() => StartRain();

    [ContextMenu("Testeo: Lluvia (detener)")]
    public void DebugStopRain() => StopRain();

    [ContextMenu("Testeo: Niebla (iniciar)")]
    public void DebugStartMist() => StartMist();

    [ContextMenu("Testeo: Niebla (detener)")]
    public void DebugStopMist() => StopMist();

    [ContextMenu("Testeo: Viento (iniciar)")]
    public void DebugStartWind() => StartWind();

    [ContextMenu("Testeo: Viento (detener)")]
    public void DebugStopWind() => StopWind();

    [ContextMenu("Testeo: Nieve (iniciar)")]
    public void DebugStartSnow() => StartSnow();

    [ContextMenu("Testeo: Nieve (detener)")]
    public void DebugStopSnow() => StopSnow();

    [ContextMenu("Testeo: Tormenta (iniciar)")]
    public void DebugStartThunderstorm() => StartThunderstorm(immediate: true);

    [ContextMenu("Testeo: Tormenta (detener)")]
    public void DebugStopThunderstorm() => StopThunderstorm();

    [ContextMenu("Testeo: Rayo ya (un solo destello + trueno)")]
    public void DebugStrikeLightning() => StartCoroutine(FlashLightningRoutine());

    [ContextMenu("Testeo: Forzar sorteo de clima ya (sin esperar weatherCheckIntervalRange)")]
    public void DebugForceWeatherRoll() => TryRollWeather();
}
