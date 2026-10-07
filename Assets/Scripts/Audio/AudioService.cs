using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Game.Player;

[DisallowMultipleComponent]
public sealed class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        MuteNextBaseSceneMusic = false;
    }
    #endif

    [Header("Perfil de reglas")]
    [SerializeField] public AudioGraphProfile profile;

    [Header("Mixer (grupos opcionales)")]
    public AudioMixer mixer;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup uiGroup;
    public AudioMixerGroup ambienceGroup;
    public AudioMixerGroup dialogueGroup;

    [Header("Parámetros del Mixer")]
    [SerializeField] private string masterVolumeParam = "MasterVol";
    [SerializeField] private string musicVolumeParam = "MusicVol";
    [SerializeField] private string sfxVolumeParam = "SfxVol";
    [SerializeField] private string dialogueVolumeParam = "DialogVol";

    [Header("Música")]
    [Min(0f)] public float defaultFade = 0.75f;

    [Header("Pool SFX")]
    [Min(1)] public int pool2DSize = 16;
    [Min(1)] public int pool3DSize = 16;

    // --- motor interno ---
    AudioSource _musicA, _musicB;
    AudioSource _voiceSource;
    AudioReverbFilter _reverbDeVoz;
    AudioEchoFilter _ecoDeVoz;
    bool _gananciaDeVozAplicada;
    float _dialogoDbOriginal;
    float _gananciaDeVozDb;
    readonly AudioGraphProfile.VoiceDuckSettings _duckCinematico = new()
    {
        musicDb = -8f, ambienceDb = -5f, sfxDb = -3f,
        attackSeconds = 0.15f, releaseSeconds = 0.6f, holdAfterVoiceSeconds = 0.25f
    };
    AudioGraphProfile.VoiceDuckSettings _ajustesDeVozActiva;
    readonly Dictionary<string, Vector3> _mezclas = new();
    readonly float[] _muestrasDeVoz = new float[256];
    bool _musicATurn; // false => current=_musicA, true => current=_musicB
    readonly Queue<AudioSource> _pool2D = new();
    readonly Queue<AudioSource> _pool3D = new();

    // --- señales / handlers ---
    DefaultNarrativeSignals _signals;
    readonly Dictionary<string, Action> _sfxHandlers = new();          // key → handler (OnCustom)
    readonly Dictionary<string, Action> _battleStartHandlers = new();  // $"BATTLE_START:{id}" → handler (OnCustom)
    readonly Dictionary<object, Action> _battleWinHandlers = new();    // battleId → handler (OnBattleWon)
    readonly Dictionary<string, Action> _minigameStartHandlers = new(); // $"MINIGAME_START:{id}" → handler (OnCustom)
    readonly Dictionary<string, Action> _minigameEndHandlers = new();   // $"MINIGAME_{id}_WON" → handler (OnCustom)

    // --- estado cinemáticas / ducking / stack de música ---
    struct MusicStackItem { public AudioClip clip; }
    readonly Stack<MusicStackItem> _musicStack = new();
    bool _isCinematicMode;

    // --- control de música de victoria ---
    // Jingle de victoria en curso: al acabar devuelve la música del lugar (ver PlayVictoryForBattle).
    Coroutine _victoryRestoreCoroutine;
    // El jingle que sigue sonando; se olvida si otro sistema pone o para la música mientras suena.
    AudioClip _jingleSonando;
    Vector3 _duckTarget = Vector3.one;
    Vector3 _duckMultiplier = Vector3.one;
    Vector3 _duckStart = Vector3.one;
    float _duckElapsed, _duckDuration;
    readonly Dictionary<string, float> _duckRequests = new();
    readonly Dictionary<AudioSource, float> _sourceVolumes = new();
    readonly HashSet<AudioSource> _ambienceSources = new();
    readonly AudioGraphProfile.VoiceDuckSettings _defaultVoiceDuck = new();
    Coroutine _voiceDuckRoutine;
    uint _voiceToken;
    bool _voiceDuckActive, _applicationPaused;
    AudioGraphProfile.VoiceDuckSettings VoiceDuck => _ajustesDeVozActiva ??
        (profile != null && profile.voiceDuck != null ? profile.voiceDuck : _defaultVoiceDuck);
    bool _battleActive;
    // FIX (5 sep 2026): id de la batalla cuya música está activa ahora mismo. Ver guard
    // en BeginBattleMusic() — BATTLE_START:{id} llega dos veces por diseño (señal narrativa
    // ya cableada desde el arranque + fallback directo de BossArenaController.StartBattleInternal()
    // "por si el wiring llega tarde"), y sin este guard BeginBattleMusic() se ejecutaba las dos
    // veces: empujaba _musicStack dos veces (una nunca se saca, deja el stack desbalanceado para
    // el resto de la partida — lo comparten batallas y cinemáticas, ver PlaySequenceMusic/RestoreMusic)
    // y reiniciaba el crossfade de música a mitad de camino (síntoma: se oye la música equivocada
    // un instante justo al empezar el combate).
    string _activeBattleId;
    bool _minigameActive;

    // Coroutines de música rastreadas individualmente para no matar el pool SFX
    Coroutine _crossfadeRoutine;
    Coroutine _fadeOutRoutine;
    // FIX INC-056: durante un crossfade ambas fuentes (_musicA y _musicB) pueden estar sonando a
    // la vez. _fadeOutRoutine solo cubría una; esta segunda referencia permite parar la otra
    // también (ver StopMusic).
    Coroutine _fadeOutRoutineB;
    Coroutine _setVolumeRoutine;

    // Recuerdo qué clip pidió la última escena base (no aditiva)
    AudioClip _lastRequestedSceneClip;

    // Cuando se sabe que una escena aditiva con música propia va a cargarse justo después,
    // se activa este flag para que OnSceneLoaded(Single) registre el clip pero no lo reproduzca.
    public static bool MuteNextBaseSceneMusic;
    
    // --- Footstep alternation ---
    int _footstepIndex = 0;

    // --- reintentos de wiring de señales ---
    bool _signalsWired = false;
    Coroutine _ensureSignalsCoro;

    // ===========================================================
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Fuentes
        _musicA = CreateChildSource("MusicA", musicGroup, spatial:false, loop:true);
        _musicB = CreateChildSource("MusicB", musicGroup, spatial:false, loop:true);
        _voiceSource = CreateChildSource("Voice", dialogueGroup, spatial:false, loop:false);
        for (int i = 0; i < pool2DSize; i++) _pool2D.Enqueue(CreateChildSource($"SFX2D_{i}", sfxGroup, spatial:false));
        for (int i = 0; i < pool3DSize; i++) _pool3D.Enqueue(CreateChildSource($"SFX3D_{i}", sfxGroup, spatial:true));

        // Escenas
        SceneManager.sceneLoaded   += OnSceneLoaded;

        // FIX (12 sep 2026, reporte de Raúl — "he añadido música para WillHouse pero sigue sonando
        // la de MainWorld"): OnSceneLoaded ignora a propósito las escenas cargadas en aditivo (ver
        // comentario ahí — "feature de cinemáticas aditivas eliminada"), pero los interiores
        // cargados en aditivo (InteriorPortalTrigger, p.ej. WillHouse.unity) SÍ deben poder tener
        // música de escena propia (AudioGraphProfile.sceneMusic), igual que cualquier escena base.
        // En vez de tocar esa guarda de OnSceneLoaded (y arriesgarnos a resucitar el bug que la
        // motivó), nos enganchamos a los eventos de EnvironmentController.OnInteriorEntered/
        // OnInteriorExited — ya existen, los dispara TeleportService.ApplyEnvironmentForAnchor en
        // cualquier flujo de entrada/salida de interior (andando o por InteriorPortalTrigger), y ya
        // los consume MinimapController para lo mismo (activar/desactivar el minimapa). Escalable a
        // cualquier interior futuro con su propia música, sin parche específico de WillHouse.
        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;
        Telon.AlAbrirse += HandleTelonAbierto;

        // Pisadas del jugador: FootstepHandler detecta la pisada (huesos de los pies) y solo
        // levanta un evento — el propio AudioService es quien decide qué suena, igual que con las
        // señales del grafo narrativo. Antes nadie escuchaba este evento y las pisadas eran mudas
        // (StarWorldFootprintPool solo generaba la huella visual).
        FootstepHandler.OnFootstep += HandlePlayerFootstep;

        // Señales (incluye inactivos)
        _signals = DefaultNarrativeSignals.Instance
                   ?? ServiceLocator.Get<DefaultNarrativeSignals>(false)
                   ?? DefaultNarrativeSignals.EnsureInstance();

        EnsureSignalsAndWireNow();
        if (!_signalsWired && _ensureSignalsCoro == null)
            _ensureSignalsCoro = StartCoroutine(EnsureSignalsRoutine());

        // Música para la escena actual
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        // Aplicar preferencias persistidas
        PlayerSettings.ApplyAudioToService(this);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded   -= OnSceneLoaded;
        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;
        Telon.AlAbrirse -= HandleTelonAbierto;
        FootstepHandler.OnFootstep -= HandlePlayerFootstep;

        if (_signals != null)
        {
            foreach (var kv in _sfxHandlers)         _signals.OffCustom(kv.Key, kv.Value);
            foreach (var kv in _battleStartHandlers) _signals.OffCustom(kv.Key, kv.Value);
            foreach (var kv in _battleWinHandlers)   _signals.OffBattleWon(kv.Key, kv.Value);
            foreach (var kv in _minigameStartHandlers) _signals.OffCustom(kv.Key, kv.Value);
            foreach (var kv in _minigameEndHandlers)   _signals.OffCustom(kv.Key, kv.Value);
        }

        _sfxHandlers.Clear();
        _battleStartHandlers.Clear();
        _battleWinHandlers.Clear();
        _minigameStartHandlers.Clear();
        _minigameEndHandlers.Clear();
    }

    // ===========================================================
    // Señales (wiring robusto)
    void EnsureSignalsAndWireNow()
    {
        if (_signals == null)
        {
            _signals = DefaultNarrativeSignals.Instance
                       ?? ServiceLocator.Get<DefaultNarrativeSignals>(false)
                       ?? DefaultNarrativeSignals.EnsureInstance();
            if (_signals == null) return; // aún no disponible
        }
        if (_signalsWired) return;

        WireEventSfx();
        WireBattleStarts();
        WireBattleWins();
        WireMinigames();
        _signalsWired = true;
        // Debug.Log("[AudioService] Señales conectadas (SFX, BattleStarts, BattleWins, Minigames).");
    }

    IEnumerator EnsureSignalsRoutine()
    {
        const float timeout = 5f;
        float t = 0f;
        while (!_signalsWired && t < timeout)
        {
            EnsureSignalsAndWireNow();
            if (_signalsWired) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void WireEventSfx()
    {
        if (_signals == null || profile == null || profile.eventSfx == null) return;
        if (_sfxHandlers.Count > 0) return;

        for (int i = 0; i < profile.eventSfx.Count; i++)
        {
            var r = profile.eventSfx[i];
            if (r == null || string.IsNullOrWhiteSpace(r.eventKey) || r.sfx == null) continue;

            string key = r.eventKey;
            Action h = () => PlaySfxForKey(key);
            _signals.OnCustom(key, h);
            _sfxHandlers[key] = h;
        }
    }

    void WireBattleStarts()
    {
        if (_signals == null || profile == null || profile.battles == null) return;
        if (_battleStartHandlers.Count > 0) return;

        for (int i = 0; i < profile.battles.Count; i++)
        {
            var r = profile.battles[i];
            if (r == null || string.IsNullOrWhiteSpace(r.battleId) || r.music == null) continue;

            string key = $"BATTLE_START:{r.battleId}";
            Action h = () => BeginBattleMusic(r);
            _signals.OnCustom(key, h);
            _battleStartHandlers[key] = h;
        }
    }

    void WireBattleWins()
    {
        if (_signals == null || profile == null || profile.battles == null) return;
        if (_battleWinHandlers.Count > 0) return;

        for (int i = 0; i < profile.battles.Count; i++)
        {
            var r = profile.battles[i];
            if (r == null || string.IsNullOrWhiteSpace(r.battleId)) continue;

            object key = r.battleId; // coincide con RaiseBattleWon(battleId)
            Action h = () => OnBattleWonRestoreMusic(r);
            _signals.OnBattleWon(key, h);
            _battleWinHandlers[key] = h;
        }
    }

    void WireMinigames()
    {
        if (_signals == null || profile == null || profile.minigames == null) return;
        if (_minigameStartHandlers.Count > 0) return;

        for (int i = 0; i < profile.minigames.Count; i++)
        {
            var r = profile.minigames[i];
            if (r == null || string.IsNullOrWhiteSpace(r.minigameId)) continue;

            // Evento de inicio: MINIGAME_START:{id}
            string startKey = $"MINIGAME_START:{r.minigameId}";
            Action startHandler = () => BeginMinigameMusic(r);
            _signals.OnCustom(startKey, startHandler);
            _minigameStartHandlers[startKey] = startHandler;

            // Evento de victoria/fin: MINIGAME_{id}_WON (el TagMinigameController emite esto)
            string endKey = $"MINIGAME_{r.minigameId}_WON";
            Action endHandler = () => OnMinigameEndRestoreMusic(r);
            _signals.OnCustom(endKey, endHandler);
            _minigameEndHandlers[endKey] = endHandler;
        }
    }

    // ===========================================================
    // Escenas
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSignalsAndWireNow();
        if (mode == LoadSceneMode.Single) _posicionesDeMusica.Clear();
        if (profile == null) return;

        // Las escenas aditivas no disparan música propia (feature de cinemáticas aditivas eliminada).
        if (mode == LoadSceneMode.Additive)
        {
            return;
        }

        // Escena base (no aditiva): elige la primera coincidencia
        _lastRequestedSceneClip = null;
        bool suppressMusic = MuteNextBaseSceneMusic;
        MuteNextBaseSceneMusic = false; // consumir siempre, haya o no coincidencia

        for (int i = 0; i < profile.sceneMusic.Count; i++)
        {
            var r = profile.sceneMusic[i];
            if (r != null &&
                !string.IsNullOrEmpty(r.sceneName) &&
                r.music != null &&
                scene.name.IndexOf(r.sceneName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _lastRequestedSceneClip = r.music;
                // Con el telón cerrado (arranque, carga) la música de la escena espera a que se
                // vea la escena. Ver HandleTelonAbierto.
                if (!suppressMusic && Telon.Cerrado) { _musicaEsperandoAlTelon = true; return; }
                if (!suppressMusic && GetCurrentMusicClip() != r.music) PlayMusic(r.music);
                return;
            }
        }
        // si ninguna regla coincide, _lastRequestedSceneClip se queda null
    }

    /// <summary>
    /// Busca en profile.sceneMusic una regla cuyo sceneName aparezca en sceneName (mismo criterio
    /// de coincidencia por subcadena que OnSceneLoaded/RestoreSceneMusic).
    /// </summary>
    bool TryGetSceneMusicRule(string sceneName, out AudioClip clip)
    {
        clip = null;
        if (profile == null || string.IsNullOrEmpty(sceneName)) return false;

        foreach (var rule in profile.sceneMusic)
        {
            if (rule != null && !string.IsNullOrEmpty(rule.sceneName) && rule.music != null &&
                sceneName.IndexOf(rule.sceneName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                clip = rule.music;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Fija la música de la escena activa desde la propia escena, en vez de tomarla de
    /// profile.sceneMusic (la portada del menú principal cambia de música según la partida).
    /// Queda como música de la escena para RestoreSceneMusic y espera al telón como las demás.
    /// </summary>
    public void FijarMusicaDeEscena(AudioClip clip, float fade = -1f)
    {
        if (clip == null) return;
        _lastRequestedSceneClip = clip;
        if (Telon.Cerrado) { _musicaEsperandoAlTelon = true; return; }
        if (CinematicSequencerBase.AnySequenceActive) return;
        if (GetCurrentMusicClip() != clip) PlayMusic(clip, fade);
    }

    // ── Telón ──────────────────────────────────────────────────────────────────
    // Mientras la pantalla está en negro retenida (ver Telon) no arranca música de escena, de
    // interior ni de zona: se apunta que alguien la pidió y se pone cuando el telón se abre, a la
    // vez que se destapa la imagen. Si para entonces hay una cinemática, manda la suya.
    bool _musicaEsperandoAlTelon;

    // (6 oct 2026) La pantalla también puede quedar tapada sin telón: una cinemática que acaba
    // en negro (FeedbackService) y lo que viene detrás (un rótulo «A la mañana siguiente…», la
    // siguiente cinemática). Al final de «Los planes de Liam» se teletransporta a la casa de Will
    // con la pantalla así, sonaba la música de la habitación durante el rótulo y, a los pocos
    // segundos, la de la secuencia siguiente la pisaba. Con la pantalla tapada la música del
    // lugar espera igual que con el telón, y lo que sonaba se apaga.
    static bool PantallaTapada => Telon.Cerrado || Sendero.Core.Feedback.FeedbackService.IsScreenFaded;
    Coroutine _esperaPantallaDestapada;

    void EsperarPantallaDestapada()
    {
        _musicaEsperandoAlTelon = true;
        if (_esperaPantallaDestapada == null) _esperaPantallaDestapada = StartCoroutine(Co_EsperarPantallaDestapada());
    }

    IEnumerator Co_EsperarPantallaDestapada()
    {
        while (PantallaTapada) yield return null;
        _esperaPantallaDestapada = null;
        HandleTelonAbierto();
    }

    /// Para quien quiere poner música de escena/zona con el telón cerrado (AmbientZone): se apunta
    /// y se resuelve al abrirse, con la misma prioridad de siempre (interior, zona, escena).
    public void PedirMusicaAlAbrirseElTelon() => _musicaEsperandoAlTelon = true;

    void HandleTelonAbierto()
    {
        if (!_musicaEsperandoAlTelon) return;
        // El telón se ha abierto pero el fundido de pantalla sigue tapando: se sigue esperando.
        if (PantallaTapada) { EsperarPantallaDestapada(); return; }
        _musicaEsperandoAlTelon = false;
        if (CinematicSequencerBase.AnySequenceActive) return;

        var env = EnvironmentController.Instance ? EnvironmentController.Instance.CurrentInterior : null;
        if (env) HandleInteriorEntered();
        else HandleInteriorExited();
    }

    /// <summary>
    /// Al entrar en un interior (andando o vía InteriorPortalTrigger): si ese interior vive en su
    /// propia escena con música configurada en AudioGraphProfile.sceneMusic (p. ej. "WillHouse"),
    /// la reproduce. Para los interiores "clásicos" que viven dentro de MainWorld.unity (mismo
    /// nombre de escena que el mundo), esto resuelve a la propia música de MainWorld — no-op si ya
    /// está sonando, gracias al guard de PlayMusic más abajo.
    /// </summary>
    void HandleInteriorEntered()
    {
        // FIX (12 sep 2026, reporte de Raúl — "ha sonado la música de la casa de will durante la
        // secuencia del prólogo"): el jugador puede empezar la partida directamente dentro de un
        // interior (WorldBootstrap resuelve el anchor de arranque 'Bedroom' → WillHouse), y ese
        // mismo instante coincide con el arranque de una cinemática (PrologueDreamSequencer) que
        // gestiona su propia música (heartbeat, "MAGOOSCURO_VISION"...). Si HandleInteriorEntered
        // reproduce la música del interior ahí mismo, pisa por completo lo que la cinemática está
        // montando. Mismo guard que ya usa el resto del archivo (Co_MusicaDelLugar) para no chocar con una cinemática en curso — cuando termine, su propio
        // RestoreMusic()/RestoreSceneMusic() se encargará de poner la música del interior (ver FIX
        // gemelo en RestoreSceneMusic(), más abajo).
        if (CinematicSequencerBase.AnySequenceActive) return;
        if (DialogueCinematicController.Instance != null && DialogueCinematicController.Instance.IsInCinematicMode) return;

        // (21 sep) Con la pantalla en negro no suena la música del interior: es lo que hacía sonar
        // la habitación de Will al empezar partida nueva, antes del prólogo. Espera al telón
        // (o a que se retire el fundido de pantalla, ver PantallaTapada).
        if (PantallaTapada) { EsperarPantallaDestapada(); return; }

        var env = EnvironmentController.Instance ? EnvironmentController.Instance.CurrentInterior : null;
        if (!env) return;

        string interiorSceneName = env.gameObject.scene.name;
        if (TryGetSceneMusicRule(interiorSceneName, out var clip) && GetCurrentMusicClip() != clip)
        {
            PlayMusic(clip, FundidoDeLugar);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[AudioService] Música de interior '{interiorSceneName}' → '{clip.name}'");
#endif
        }
    }

    /// <summary>
    /// Al salir de un interior: mismo criterio de prioridad que el resto de puntos de restauración
    /// del archivo (RestoreAfterBattle/RestoreAfterMinigame/CinematicSequencerBase.RestoreMusic) —
    /// si el punto de salida cae dentro de una AmbientZone activa, su música manda; si no, se
    /// restaura la música de la escena base (RestoreSceneMusic ya resuelve esto sin verse afectado
    /// por el interior aditivo, porque _lastRequestedSceneClip nunca lo tocó — ver HandleInteriorEntered).
    /// </summary>
    void HandleInteriorExited()
    {
        if (profile == null) return;

        // FIX 16 sep 2026: mismo guard que ya tenía su gemelo HandleInteriorEntered y que faltaba
        // aquí. Si hay una cinemática en curso, es ella quien manda sobre la música — al terminar,
        // su propio RestoreMusic()/RestoreSceneMusic() pondrá lo que toque. Sin esto, salir de un
        // interior a mitad de una secuencia le pisaba la música.
        //
        // OJO, esto NO arregla por sí solo el caso de "salgo de casa y suena un segundo la música
        // del mundo antes que la de la secuencia": ahí el orden es al revés (la puerta levanta
        // OnInteriorExited ANTES de que el grafo dispare la cinemática, así que todavía no hay
        // cinemática activa que detectar). Eso se ataca desde el otro lado, arrancando la música
        // de la secuencia en su primer frame — ver SequencePlayer.Co_Play().
        if (CinematicSequencerBase.AnySequenceActive)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[AudioService] Al salir del interior: música omitida, hay una cinemática activa (manda la suya).");
#endif
            return;
        }

        if (PantallaTapada) { EsperarPantallaDestapada(); return; }

        var activeAmbientZone = AmbientZone.CurrentActiveZone;
        if (activeAmbientZone != null && !string.IsNullOrEmpty(activeAmbientZone.MusicZoneId))
        {
            var zoneRule = profile.GetAmbientZoneRule(activeAmbientZone.MusicZoneId);
            if (zoneRule?.music != null)
            {
                PlayMusic(zoneRule.music, FundidoDeLugar);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[AudioService] Al salir del interior: música de AmbientZone '{activeAmbientZone.MusicZoneId}'");
#endif
                return;
            }
        }

        if (!RestoreSceneMusic(FundidoDeLugar))
            StopMusic(FundidoDeLugar);
    }

    /// Fundido al cambiar de música por entrar o salir de un sitio (INC-421): «las canciones no
    /// pueden acabar en seco». defaultFade (0,75 s) se queda para lo demás.
    float FundidoDeLugar => Mathf.Max(defaultFade, 1.8f);

    // ── Música del lugar al terminar algo ────────────────────────────────────────────────────
    // Mismo criterio que CameraDirectorService con la cámara: quien termina (una cinemática, un
    // combate) no pone la música del lugar al instante, la pide. Si dentro de la ventana de gracia
    // otro sistema pone su música (el jefe que viene detrás de la cinemática, otra cinemática), la
    // petición se cancela y la música de gameplay no llega a colarse entre las dos. Cualquier
    // PlayMusic/StopMusic la cancela: quien la llama ya ha decidido qué suena. Ver INC-486.
    const float GraciaMusicaDelLugar = 0.3f;
    Coroutine _musicaDelLugarPendiente;

    /// Pide la música del sitio donde está el jugador (zona, interior o escena) para dentro de
    /// <see cref="GraciaMusicaDelLugar"/> segundos, salvo que alguien ponga otra antes.
    /// Con <paramref name="incluirEscena"/> a false solo vuelve la de la zona, si la hay.
    public void PedirMusicaDelLugar(float fade, bool incluirEscena = true)
    {
        CancelarMusicaDelLugarPendiente();
        _musicaDelLugarPendiente = StartCoroutine(Co_MusicaDelLugar(fade, incluirEscena));
    }

    void CancelarMusicaDelLugarPendiente()
    {
        if (_musicaDelLugarPendiente == null) return;
        StopCoroutine(_musicaDelLugarPendiente);
        _musicaDelLugarPendiente = null;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[AudioService] Música del lugar cancelada: otro sistema ha puesto la suya en la " +
                  "ventana de gracia (relevo directo, sin música de gameplay en medio).");
#endif
    }

    IEnumerator Co_MusicaDelLugar(float fade, bool incluirEscena)
    {
        yield return new WaitForSecondsRealtime(GraciaMusicaDelLugar);
        _musicaDelLugarPendiente = null;

        // Una cinemática que arranca pone su música en el cut point de su transición de entrada,
        // que puede llegar después de la gracia; al acabar, ella misma pedirá la del lugar.
        if (CinematicSequencerBase.AnySequenceActive) yield break;

        // Con la pantalla en negro, espera al telón o al fundido (HandleTelonAbierto). Lo que
        // sonaba (la música de la cinemática que acaba de terminar) se apaga mientras tanto: así
        // no se pisa con lo que venga detrás (rótulo, otra cinemática con su propio tema).
        if (PantallaTapada)
        {
            StopMusic(fade);
            EsperarPantallaDestapada();
            yield break;
        }

        var zona = AmbientZone.CurrentActiveZone;
        if (zona != null && !string.IsNullOrEmpty(zona.MusicZoneId))
        {
            var zoneRule = profile?.GetAmbientZoneRule(zona.MusicZoneId);
            if (zoneRule?.music != null)
            {
                PlayMusic(zoneRule.music, fade);
                yield break;
            }
        }

        if (!incluirEscena) yield break;
        if (!RestoreSceneMusic(fade)) StopMusic(fade);
    }

    // ===========================================================
    // Batallas
    void BeginBattleMusic(AudioGraphProfile.BattleRule r)
    {
        // FIX (5 sep 2026): BATTLE_START:{id} puede llegar a este método dos veces en el mismo
        // frame (señal narrativa en vivo + fallback directo de BossArenaController) — sin este
        // guard, la segunda llamada volvía a empujar _musicStack (quedando desbalanceado para
        // siempre) y reiniciaba el crossfade a mitad de camino. Idempotente por battleId: si esta
        // misma batalla ya está activa, no hace nada.
        if (_battleActive && _activeBattleId == r.battleId) return;

        var current = GetCurrentMusicClip();
        _musicStack.Push(new MusicStackItem { clip = current });
        _battleActive = true;
        _activeBattleId = r.battleId;
        PlayMusic(r.music, r.fade);
    }

    void OnBattleWonRestoreMusic(AudioGraphProfile.BattleRule r)
    {
        // Con el jingle de victoria sonando, es él quien cierra el combate al acabar.
        if (_victoryRestoreCoroutine != null) return;
        // Solo cierra el combate que está sonando: llega por la señal BattleWon y por
        // EndBattleById, y la pila de música es compartida con cinemáticas y minijuegos.
        if (!_battleActive || !string.Equals(_activeBattleId, r.battleId, StringComparison.Ordinal)) return;

        _musicA.loop = true;
        _musicB.loop = true;
        if (_musicStack.Count > 0) _musicStack.Pop();
        _battleActive = false;
        _activeBattleId = null;

        // Si detrás del combate viene una cinemática, suena la suya: la del lugar se pide, no
        // se pone (ver PedirMusicaDelLugar).
        PedirMusicaDelLugar(FundidoDeLugar);
    }
    
    // ===========================================================
    // Minijuegos
    void BeginMinigameMusic(AudioGraphProfile.MinigameRule r)
    {
        if (r.music == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] Minigame '{r.minigameId}' no tiene música configurada");
#endif
            return;
        }

        // Si el minijuego ya está activo (reinicio de ronda), no apilar de nuevo:
        // simplemente reiniciar la pista desde el inicio.
        if (_minigameActive)
        {
            _musicA.loop = r.loop;
            _musicB.loop = r.loop;
            RestartMusicClipFromBeginning(r.music, r.fade);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[AudioService] 🔁 Música de minijuego '{r.minigameId}' reiniciada desde el inicio");
#endif
            return;
        }
        
        var current = GetCurrentMusicClip();
        _musicStack.Push(new MusicStackItem { clip = current });
        _minigameActive = true;
        
        // Configurar loop según la regla
        _musicA.loop = r.loop;
        _musicB.loop = r.loop;
        
        PlayMusic(r.music, r.fade);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[AudioService] 🎮 Música de minijuego '{r.minigameId}' iniciada");
#endif
    }

    // Para las corrutinas de música se usan referencias explícitas y nunca StopAllCoroutines,
    // porque éste mataría las corrutinas ReturnWhenDone del pool SFX.
    void StopMusicCoroutines()
    {
        if (_crossfadeRoutine != null) { StopCoroutine(_crossfadeRoutine); _crossfadeRoutine = null; }
        if (_fadeOutRoutine   != null) { StopCoroutine(_fadeOutRoutine);   _fadeOutRoutine   = null; }
        if (_fadeOutRoutineB  != null) { StopCoroutine(_fadeOutRoutineB);  _fadeOutRoutineB  = null; }
        if (_setVolumeRoutine != null) { StopCoroutine(_setVolumeRoutine); _setVolumeRoutine = null; }
    }

    void RestartMusicClipFromBeginning(AudioClip clip, float fadeSeconds)
    {
        if (clip == null) return;
        if (fadeSeconds < 0f) fadeSeconds = defaultFade;

        var current = _musicATurn ? _musicB : _musicA;
        var other = _musicATurn ? _musicA : _musicB;

        // Seleccionar la fuente que está realmente sonando ahora.
        AudioSource active = current.isPlaying ? current : (other.isPlaying ? other : current);

        // Si por algún motivo no sonaba el clip esperado, delegar al flujo estándar.
        if (active.clip != clip)
        {
            PlayMusic(clip, fadeSeconds);
            return;
        }

        StopMusicCoroutines();
        active.Stop();
        active.timeSamples = 0;
        SetSourceVolume(active, 1f);
        active.Play();

        // Evitar duplicados en la otra fuente.
        if (other != active && other.isPlaying && other.clip == clip)
        {
            other.Stop();
            other.timeSamples = 0;
        }
    }

    void OnMinigameEndRestoreMusic(AudioGraphProfile.MinigameRule r)
    {
        if (!_minigameActive)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[AudioService] Minijuego '{r.minigameId}' no estaba activo, ignorando restauración");
#endif
            return;
        }
        
        // Restaurar loop
        _musicA.loop = true;
        _musicB.loop = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[AudioService] 🔄 Loop restaurado en AudioSources de música después de minijuego");
#endif
        
        // PRIORIDAD 1: Si hay una AmbientZone activa, restaurar su música
        var activeAmbientZone = AmbientZone.CurrentActiveZone;
        if (activeAmbientZone != null && !string.IsNullOrEmpty(activeAmbientZone.MusicZoneId))
        {
            var zoneRule = profile?.GetAmbientZoneRule(activeAmbientZone.MusicZoneId);
            if (zoneRule?.music != null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[AudioService] Restaurando música de AmbientZone '{activeAmbientZone.MusicZoneId}' después de minijuego");
#endif
                PlayMusic(zoneRule.music, r.fade);
                if (_musicStack.Count > 0) _musicStack.Pop();
                _minigameActive = false;
                return;
            }
        }
        
        // PRIORIDAD 2: Restaurar música del stack o de la escena
        if (_musicStack.Count > 0) _musicStack.Pop();
        
        if (!RestoreSceneMusic(r.fade))
        {
            StopMusic(r.fade);
        }
        _minigameActive = false;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[AudioService] 🎮 Música de minijuego '{r.minigameId}' finalizada, música restaurada");
#endif
    }
    
    /// <summary>
    /// Inicia la música de un minijuego por ID (llamado manualmente si no se usa señales)
    /// </summary>
    public void BeginMinigameById(string minigameId)
    {
        var rule = profile?.GetMinigameRule(minigameId);
        if (rule != null)
        {
            BeginMinigameMusic(rule);
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] No se encontró regla de música para minijuego '{minigameId}'");
#endif
        }
    }
    
    /// <summary>
    /// Finaliza la música de un minijuego por ID (llamado manualmente si no se usa señales)
    /// </summary>
    public void EndMinigameById(string minigameId)
    {
        var rule = profile?.GetMinigameRule(minigameId);
        if (rule != null)
        {
            OnMinigameEndRestoreMusic(rule);
        }
    }
    
    // --- Helpers para lookup de batallas por id (exacto y fallback substring) ---
    AudioGraphProfile.BattleRule FindBattleRuleForId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || profile == null || profile.battles == null) return null;
        string key = id.Trim();

        // 1) match exacto (case-insensitive)
        for (int i = 0; i < profile.battles.Count; i++)
        {
            var r = profile.battles[i];
            if (r == null || string.IsNullOrWhiteSpace(r.battleId)) continue;
            if (string.Equals(r.battleId.Trim(), key, StringComparison.OrdinalIgnoreCase))
                return r;
        }

        // 2) fallback: substring por si el id viene con prefijos/sufijos
        for (int i = 0; i < profile.battles.Count; i++)
        {
            var r = profile.battles[i];
            if (r == null || string.IsNullOrWhiteSpace(r.battleId)) continue;
            if (key.IndexOf(r.battleId.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                return r;
        }

        return null;
    }

// --- API opcional llamada desde BossArenaController ---
    public void BeginBattleById(string id)
    {
        var rule = FindBattleRuleForId(id);
        if (rule != null)
        {
            BeginBattleMusic(rule); // apila la música actual y pone la del boss
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] BeginBattleById: no hay BattleRule para id='{id}'.");
#endif
        }
    }

    public void EndBattleById(string id)
    {
        var rule = FindBattleRuleForId(id);
        if (rule != null)
        {
            OnBattleWonRestoreMusic(rule); // restaura la música previa a la batalla
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] EndBattleById: no hay BattleRule para id='{id}'.");
#endif
        }
    }

    /// <summary>
    /// Restaura la música después de una batalla sin necesitar una BattleRule específica.
    /// Úsalo cuando el NPC no tiene battleMusicId configurado pero sí reproduce música de victoria.
    /// </summary>
    public void RestoreAfterBattle(float fade = -1f)
    {
        // Con el jingle de victoria sonando, es él quien cierra el combate al acabar.
        if (_victoryRestoreCoroutine != null) return;
        if (fade < 0f) fade = defaultFade;

        _musicA.loop = true;
        _musicB.loop = true;

        // BUGFIX: solo tocar la pila si ESTE combate llegó a apilar algo. Los enemigos
        // sin battleMusicId nunca pasan por BeginBattleMusic (no cambian de música al
        // empezar), así que _battleActive sigue en false aquí; hacer Pop() igualmente
        // robaba la entrada de otro sistema (cinemática aditiva, minijuego) que sí la
        // había apilado legítimamente y aún no le tocaba restaurarse.
        bool wasBattleActive = _battleActive;
        if (wasBattleActive && _musicStack.Count > 0) _musicStack.Pop();
        _battleActive = false;

        // Un combate sin música propia no cambió nada al empezar: al acabar solo vuelve la de la
        // zona, si la hay. Se pide, no se pone (ver PedirMusicaDelLugar).
        PedirMusicaDelLugar(fade, incluirEscena: wasBattleActive);
    }

    // ===========================================================
    // Alerta (no inicia estado de batalla; solo cambia música)
    public void BeginAlertById(string id)
    {
        var rule = FindBattleRuleForId(id);
        if (rule != null && rule.music != null)
        {
            // No alterar _battleActive ni apilar stack: es solo una alerta temporal
            PlayMusic(rule.music, rule.fade);
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] BeginAlertById: no hay BattleRule/music para id='{id}'.");
#endif
        }
    }

    /// Jingle de victoria: suena una vez (sin loop) en lugar de la música del combate y, al
    /// acabar, cierra el combate (pila de música) y pide la música del lugar con fundido. Mientras
    /// suena, EndBattleById, RestoreAfterBattle y la señal BattleWon no hacen nada: así ni se
    /// cuela la música del lugar debajo del jingle ni queda silencio después. Ver INC-500.
    public void PlayVictoryForBattle(string battleId, string victoryId)
    {
        var victoryRule = FindBattleRuleForId(victoryId);
        if (victoryRule == null || victoryRule.music == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] PlayVictoryForBattle: no hay música de victoria para id='{victoryId}' (combate '{battleId}').");
#endif
            return;
        }

        if (_victoryRestoreCoroutine != null) StopCoroutine(_victoryRestoreCoroutine);

        // El jingle nunca hace loop: si lo que viene detrás dura más que él, sigue la música
        // del lugar, no el jingle repetido.
        _musicA.loop = false;
        _musicB.loop = false;
        PlayMusic(victoryRule.music, victoryRule.fade);
        _jingleSonando = victoryRule.music;
        _victoryRestoreCoroutine = StartCoroutine(Co_TrasElJingleDeVictoria(victoryRule.music));
    }

    IEnumerator Co_TrasElJingleDeVictoria(AudioClip jingle)
    {
        yield return new WaitForSecondsRealtime(jingle.length);
        _victoryRestoreCoroutine = null;
        bool sigueSonando = _jingleSonando == jingle;
        _jingleSonando = null;

        _musicA.loop = true;
        _musicB.loop = true;
        if (_battleActive)
        {
            if (_musicStack.Count > 0) _musicStack.Pop();
            _battleActive = false;
            _activeBattleId = null;
        }

        // Si mientras sonaba otro sistema ha puesto su música (una cinemática), manda la suya.
        if (sigueSonando) PedirMusicaDelLugar(FundidoDeLugar);
    }


    // ===========================================================
    // Música
    private readonly Dictionary<AudioClip, int> _posicionesDeMusica = new();
    private void RecordarMusica(AudioSource fuente)
    {
        if (fuente != null && fuente.clip != null && fuente.isPlaying)
            _posicionesDeMusica[fuente.clip] = fuente.timeSamples;
    }
    public void PlayMusic(AudioClip clip, float fadeSeconds = -1f, bool continuarDondeIba = true)
    {
        if (!clip) return;
        CancelarMusicaDelLugarPendiente();
        if (clip != _jingleSonando) _jingleSonando = null;
        if (fadeSeconds < 0f) fadeSeconds = defaultFade;

        // Fuente "actual": la que está sonando ahora mismo
        var current = _musicATurn ? _musicB : _musicA;
        var other   = _musicATurn ? _musicA : _musicB;
        RecordarMusica(current);
        RecordarMusica(other);
        if (!continuarDondeIba)
        {
            _posicionesDeMusica.Remove(clip);
            if (current.clip == clip) current.timeSamples = 0;
            if (other.clip == clip) other.timeSamples = 0;
        }

        // 1) Si YA está sonando este mismo clip, no reiniciamos.
        //    Solo aseguramos volumen (con ducking aplicado) y salimos.
        if (current.clip == clip)
        {
            float target = 1f;

            // si por lo que sea está parado (pausa/crossfade previo), reanudar sin resetear tiempo
            if (!current.isPlaying)
                current.Play();

            // llevar al volumen objetivo suavemente (sin cambiar de fuente)
            StopMusicCoroutines();

            // BUGFIX: StopMusicCoroutines() solo mata la corrutina de crossfade/fade-out en
            // curso, no la fuente en sí. Si 'other' venía de un crossfade interrumpido a medias
            // se queda sonando su clip anterior indefinidamente, mezclado con 'current' (esto es
            // lo que producía "suena la música de gameplay Y la de la zona a la vez"). Si 'other'
            // no comparte el clip que queremos, hay que silenciarla explícitamente aquí.
            if (other.isPlaying && other.clip != clip)
            {
                RecordarMusica(other);
                other.Stop();
                SetSourceVolume(other, 0f);
            }

            _setVolumeRoutine = StartCoroutine(SetMusicVolumeTo(target, fadeSeconds));
            return;
        }

        // 2) Si estaba en la otra fuente el mismo clip (por un crossfade previo a medias),
        //    también evitamos reiniciar y nos quedamos con esa.
        if (other.clip == clip && other.isPlaying)
        {
            float target = 1f;
            StopMusicCoroutines();

            // BUGFIX: 'other' es la fuente que de verdad queremos activa a partir de ahora.
            // Antes no se actualizaba _musicATurn, así que SetMusicVolumeTo seguía tratando a
            // 'current' (el clip viejo) como la fuente "actual" y la subía al volumen objetivo
            // en vez de pararla — dejando el clip viejo y el nuevo sonando a la vez. Alineamos
            // el turno con la realidad y silenciamos 'current' si quedó con un clip distinto.
            if (current.isPlaying && current.clip != clip)
            {
                RecordarMusica(current);
                current.Stop();
                SetSourceVolume(current, 0f);
            }
            _musicATurn = !_musicATurn;

            _setVolumeRoutine = StartCoroutine(SetMusicVolumeTo(target, fadeSeconds));
            return;
        }

        // 3) Clip distinto → crossfade. Si llega a mitad de otro fundido suenan las dos fuentes:
        //    se reutiliza la que menos se oye y se apaga la que más, para que la canción que de
        //    verdad está sonando no se corte en seco al cambiarle el clip.
        var from = current;
        var to   = other;
        if (current.isPlaying && other.isPlaying && BaseVolume(other) > BaseVolume(current))
        {
            from = other;
            to   = current;
        }

        StopMusicCoroutines();
        RecordarMusica(to);
        to.Stop();
        to.clip = clip;
        SetSourceVolume(to, 0f);
        to.timeSamples = continuarDondeIba && _posicionesDeMusica.TryGetValue(clip, out int muestra)
            ? Mathf.Clamp(muestra, 0, Mathf.Max(0, clip.samples - 1)) : 0;
        if (!to.isPlaying) to.Play();

        if (from.isPlaying)
        {
            StopMusicCoroutines();
            _crossfadeRoutine = StartCoroutine(Crossfade(from, to, fadeSeconds));
        }
        else
        {
            _crossfadeRoutine = StartCoroutine(Crossfade(from, to, fadeSeconds));
        }

        // La fuente activa pasa a ser la que entra.
        _musicATurn = to == _musicB;
    }

    public void StopMusic(float fadeOut = -1f)
    {
        CancelarMusicaDelLugarPendiente();
        _jingleSonando = null;
        if (fadeOut < 0f) fadeOut = defaultFade;

        // FIX INC-056: antes solo se paraba la fuente "current" según el flag _musicATurn. Si
        // había un crossfade en curso (ej: música de batalla del Golem empezando a sonar mientras
        // la anterior aún no había terminado de apagarse) las DOS fuentes (_musicA y _musicB)
        // podían estar sonando a la vez, y esta función dejaba la otra sonando de fondo — al
        // morir, la música principal seguía escuchándose mezclada con la de Game Over. Ahora se
        // paran ambas fuentes si están sonando, en vez de asumir que solo una lo está.
        if (!_musicA.isPlaying && !_musicB.isPlaying) return;

        StopMusicCoroutines();

        if (_musicA.isPlaying) _fadeOutRoutine  = StartCoroutine(FadeOutAndStop(_musicA, fadeOut, isSecondary: false));
        if (_musicB.isPlaying) _fadeOutRoutineB = StartCoroutine(FadeOutAndStop(_musicB, fadeOut, isSecondary: true));
    }

    /// Lo mínimo que tarda en apagarse la canción que sale (INC-421: «las canciones no pueden
    /// acabar en seco»). La que entra respeta su fundido: con 0 entra de golpe (el arranque de un
    /// combate), pero la anterior se apaga por debajo.
    const float FundidoMinimoDeSalida = 1f;

    IEnumerator Crossfade(AudioSource from, AudioSource to, float seconds)
    {
        float entrada = Mathf.Max(0f, seconds);
        float salida  = Mathf.Max(entrada, FundidoMinimoDeSalida);
        float startFrom = BaseVolume(from);
        float targetTo  = 1f;
        float t = 0f;
        while (t < salida)
        {
            t += Time.unscaledDeltaTime;
            float kIn  = entrada > 0f ? Mathf.Clamp01(t / entrada) : 1f;
            float kOut = Mathf.Clamp01(t / salida);
            // Fundido de potencia constante (INC-421): con dos rectas, a mitad de camino las dos
            // pistas suenan a la mitad y se oye un hueco entre canción y canción.
            SetSourceVolume(from, startFrom * Mathf.Cos(kOut * Mathf.PI * 0.5f));
            SetSourceVolume(to, targetTo  * Mathf.Sin(kIn  * Mathf.PI * 0.5f));
            yield return null;
        }
        RecordarMusica(from);
        from.Stop();
        SetSourceVolume(to, targetTo);
        _crossfadeRoutine = null;
    }

    IEnumerator FadeOutAndStop(AudioSource src, float seconds, bool isSecondary = false)
    {
        if (seconds <= 0f)
        {
            RecordarMusica(src);
            src.Stop();
            if (isSecondary) _fadeOutRoutineB = null; else _fadeOutRoutine = null;
            yield break;
        }
        float start = BaseVolume(src), t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            SetSourceVolume(src, Mathf.Lerp(start, 0f, t / seconds));
            yield return null;
        }
        RecordarMusica(src);
        src.Stop();
        SetSourceVolume(src, 1f);
        if (isSecondary) _fadeOutRoutineB = null; else _fadeOutRoutine = null;
    }

    /// Atenúa la música mientras el propietario mantiene su petición; repetir actualiza los dB.
    public void BeginDuck(string source, float db)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        _duckRequests[source] = DbMultiplier(db);
        RefreshDuck(VoiceDuck.attackSeconds);
    }

    /// Libera únicamente la petición del propietario indicado.
    public void EndDuck(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || !_duckRequests.Remove(source)) return;
        RefreshDuck(VoiceDuck.releaseSeconds);
    }

    /// Atenúa música, SFX y ambiente por propietario, con fundido en tiempo real.
    public void BeginDuck(string source, float musicaDb, float sfxDb, float ambienteDb, float fundido)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        _mezclas[source] = new Vector3(DbMultiplier(musicaDb), DbMultiplier(ambienteDb), DbMultiplier(sfxDb));
        RefreshDuck(fundido);
    }

    public void EndDuck(string source, float fundido)
    {
        if (string.IsNullOrWhiteSpace(source) || !_mezclas.Remove(source)) return;
        RefreshDuck(fundido);
    }

    static float DbMultiplier(float db) => Mathf.Pow(10f, Mathf.Clamp(db, -80f, 0f) / 20f);

    void RefreshDuck(float duration)
    {
        var settings = VoiceDuck;
        Vector3 target = Vector3.one;
        foreach (var request in _duckRequests.Values) target.x = Mathf.Min(target.x, request);
        foreach (var request in _mezclas.Values) target = Vector3.Min(target, request);
        if (_voiceDuckActive && settings.enabled)
        {
            target.x = Mathf.Min(target.x, DbMultiplier(settings.musicDb));
            target.y = Mathf.Min(target.y, DbMultiplier(settings.ambienceDb));
            target.z = Mathf.Min(target.z, DbMultiplier(settings.sfxDb));
        }
        _duckStart = _duckMultiplier;
        _duckTarget = target;
        _duckElapsed = 0f;
        _duckDuration = Mathf.Max(0f, duration);
        if (_duckDuration == 0f)
        {
            _duckMultiplier = _duckTarget;
            foreach (var entry in _sourceVolumes)
                if (entry.Key != null) entry.Key.volume = entry.Value * SourceMultiplier(entry.Key);
        }
    }

    float GetDuckedVolume(float baseVol) => baseVol * _duckMultiplier.x;

    private readonly HashSet<AudioSource> _truenos = new();
    float SourceMultiplier(AudioSource src)
    {
        if (_truenos.Contains(src)) return 1f;
        var group = src.outputAudioMixerGroup;
        if (src == _voiceSource || (uiGroup != null && group == uiGroup)
            || (dialogueGroup != null && group == dialogueGroup)) return 1f;
        if (src == _musicA || src == _musicB) return GetDuckedVolume(1f);
        return _ambienceSources.Contains(src) || (ambienceGroup != null && group == ambienceGroup)
            ? _duckMultiplier.y : _duckMultiplier.z;
    }

    float BaseVolume(AudioSource src) => _sourceVolumes.TryGetValue(src, out float volume) ? volume : src.volume;

    // Los fundidos escriben el volumen base; la atenuación se compone sin alterar el mixer.
    void SetSourceVolume(AudioSource src, float volume)
    {
        _sourceVolumes[src] = Mathf.Clamp01(volume);
        src.volume = Mathf.Clamp01(volume) * SourceMultiplier(src);
    }

    void LateUpdate()
    {
        if (!_applicationPaused && !AudioListener.pause)
        {
            _duckElapsed += Time.unscaledDeltaTime;
            float progress = _duckDuration > 0f ? Mathf.Clamp01(_duckElapsed / _duckDuration) : 1f;
            _duckMultiplier = Vector3.Lerp(_duckStart, _duckTarget, Mathf.SmoothStep(0f, 1f, progress));
        }
        foreach (var entry in _sourceVolumes)
            if (entry.Key != null) entry.Key.volume = entry.Value * SourceMultiplier(entry.Key);
    }

    void OnApplicationPause(bool paused) => _applicationPaused = paused;

    void CancelVoiceMonitor()
    {
        ++_voiceToken;
        if (_voiceDuckRoutine != null) StopCoroutine(_voiceDuckRoutine);
        _voiceDuckRoutine = null;
    }

    IEnumerator MonitorVoice(uint token, bool waitForVoice = true)
    {
        // Un frame permite que una voz recién lanzada empiece a reproducirse.
        yield return null;
        while (token == _voiceToken && _voiceSource != null
            && (_applicationPaused || AudioListener.pause || (waitForVoice
                && (_voiceSource.isPlaying || (_voiceSource.clip != null
                    && _voiceSource.clip.loadState == AudioDataLoadState.Loading)))))
            yield return null;
        float elapsed = 0f;
        while (token == _voiceToken && elapsed < Mathf.Max(0f, VoiceDuck.holdAfterVoiceSeconds))
        {
            if (!_applicationPaused && !AudioListener.pause) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (token != _voiceToken) yield break;
        _voiceDuckActive = false;
        RefreshDuck(VoiceDuck.releaseSeconds);
        _ajustesDeVozActiva = null;
        _voiceDuckRoutine = null;
    }

    void OnDisable()
    {
        QuitarEfectoDeVoz();
        _mezclas.Clear();
        _ajustesDeVozActiva = null;
        CancelVoiceMonitor();
        if (_voiceSource != null) _voiceSource.Stop();
        _voiceDuckActive = false;
        _duckRequests.Clear();
        _duckMultiplier = _duckStart = _duckTarget = Vector3.one;
        foreach (var entry in _sourceVolumes)
            if (entry.Key != null) entry.Key.volume = entry.Value;
    }
    IEnumerator SetMusicVolumeTo(float target, float fade)
    {
        var current = _musicATurn ? _musicB : _musicA;
        var other   = _musicATurn ? _musicA : _musicB;

        float t = 0f;
        float a0 = BaseVolume(current);
        float b0 = BaseVolume(other);
        if (fade <= 0f) fade = 0.0001f;

        // BUGFIX: antes 'other' se interpolaba desde a0 (volumen de 'current') hacia el mismo
        // target que 'current', sin usar b0 nunca. Si 'other' tenía un clip distinto sonando
        // (p.ej. un crossfade interrumpido) esto la subía al mismo volumen que la música actual
        // en vez de apagarla — dos pistas distintas sonando a la vez indefinidamente. Ahora
        // 'other' solo comparte el target si de verdad es el mismo clip (ducking normal);
        // si no, se apaga hacia 0 desde su propio volumen real (b0) y se para al terminar.
        bool sameClip = other.clip == current.clip;
        float otherTarget = sameClip ? target : 0f;

        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fade);
            SetSourceVolume(current, Mathf.Lerp(a0, target, k));
            SetSourceVolume(other, Mathf.Lerp(b0, otherTarget, k));
            yield return null;
        }
        SetSourceVolume(current, target);
        SetSourceVolume(other, otherTarget);
        if (!sameClip && other.isPlaying) other.Stop();
        _setVolumeRoutine = null;
    }

    // ===========================================================
    // SFX - API pública para reproducir efectos de sonido
    
    /// <summary>
    /// Reproduce un SFX por clave de evento configurada en el AudioGraphProfile.
    /// Ejemplo: PlaySFX("Ambience_Cave"), PlaySFX("Spell01"), PlaySFX("FootStep00")
    /// </summary>
    public void PlaySFX(string eventKey, float volume = 1f, Vector3? worldPosition = null, float tono = 1f)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) return;
        
        AudioClip clip = FindSfxClipByKey(eventKey);
        if (clip != null)
        {
            PlayEffect(clip, volume, worldPosition,
                eventKey.StartsWith("Ambience", StringComparison.OrdinalIgnoreCase),
                eventKey.StartsWith("UI_", StringComparison.OrdinalIgnoreCase), pitch: Mathf.Clamp(tono, 0.01f, 3f));
        }
        else
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] SFX no encontrado para clave '{eventKey}'.");
#endif
        }
    }
    
    /// <summary>
    /// Reproduce un footstep alternando automáticamente entre FootStep00-04.
    /// Llama desde el controlador de movimiento cada vez que el pie toca el suelo.
    /// </summary>
    public void PlayFootstep(float volume = 1f, Vector3? worldPosition = null)
    {
        string key = $"FootStep0{_footstepIndex}";
        _footstepIndex = (_footstepIndex + 1) % 5; // Alterna entre 0-4
        PlaySFX(key, volume, worldPosition);
    }

    /// Handler de FootstepHandler.OnFootstep (ver suscripción en Awake). worldPos ya viene
    /// calculado por FootstepHandler (punto de impacto del raycast al suelo bajo el pie).
    void HandlePlayerFootstep(Vector3 worldPos) => PlayFootstep(1f, worldPos);
    
    /// <summary>
    /// Reproduce un spell SFX por número.
    /// Ejemplo: PlaySpell(2) → reproduce Spell_02
    /// </summary>
    public void PlaySpell(int spellNumber, float volume = 1f, Vector3? worldPosition = null)
    {
        string key = $"Spell0{spellNumber}";
        PlaySFX(key, volume, worldPosition);
    }
    
    /// <summary>
    /// Reproduce un SFX de ambiente por clave.
    /// Ejemplo: PlayAmbience("Ambience_Cave")
    /// </summary>
    public void PlayAmbience(string ambienceKey, float volume = 1f, Vector3? worldPosition = null)
    {
        var clip = FindSfxClipByKey(ambienceKey);
        if (clip != null) PlayEffect(clip, volume, worldPosition, true);
    }
    
    /// <summary>
    /// Busca un AudioClip en el profile por clave de evento.
    /// </summary>
    AudioClip FindSfxClipByKey(string key)
    {
        if (profile == null || string.IsNullOrWhiteSpace(key)) return null;
        
        for (int i = 0; i < profile.eventSfx.Count; i++)
        {
            var r = profile.eventSfx[i];
            if (r != null &&
                !string.IsNullOrWhiteSpace(r.eventKey) &&
                string.Equals(r.eventKey, key, StringComparison.OrdinalIgnoreCase) &&
                r.sfx != null)
            {
                return r.sfx;
            }
        }
        return null;
    }
    
    // ===========================================================
    // SFX en loop con clave propia (loopId) — para ambientes que deben poder
    // pararse antes de que termine el clip. PlaySFX/PlaySFXAt son "dispara y
    // olvida": el AudioSource vuelve solo al pool cuando el clip termina
    // (ReturnWhenDone), así que si el clip es una pista de ambiente larga
    // (p. ej. rain-sfx.mp3) suena hasta agotarse aunque el evento lógico
    // (IsRaining) ya haya terminado. PlayLoopingSFX usa una fuente dedicada
    // por loopId, fuera del pool, que solo se detiene cuando se llama
    // explícitamente a StopLoopingSFX.
    readonly Dictionary<string, AudioSource> _loopingSfxSources = new();
    // FIX M4 (auditoría 2026-08-07): fade-out en curso por loopId, para poder cancelarlo si el
    // mismo loop se reinicia (PlayLoopingSFX) o si se pide otro StopLoopingSFX antes de que
    // termine el anterior. Antes, un fade-out viejo seguía corriendo tras rearrancar el loop y
    // acababa cortando en seco el loop nuevo cuando el temporizador viejo llegaba a cero.
    readonly Dictionary<string, Coroutine> _loopFadeRoutines = new();

    /// <summary>
    /// Arranca (o reinicia) en loop el SFX asociado a eventKey en el AudioGraphProfile, bajo la
    /// clave lógica loopId. Llamar a StopLoopingSFX con el mismo loopId para detenerlo.
    /// </summary>
    public void PlayLoopingSFX(string loopId, string eventKey, float volume = 1f)
    {
        if (string.IsNullOrWhiteSpace(loopId) || string.IsNullOrWhiteSpace(eventKey)) return;

        AudioClip clip = FindSfxClipByKey(eventKey);
        if (clip == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] SFX en loop no encontrado para clave '{eventKey}'.");
#endif
            return;
        }

        if (!_loopingSfxSources.TryGetValue(loopId, out var src) || src == null)
        {
            src = CreateChildSource($"LoopSFX_{loopId}", sfxGroup, spatial: false, loop: true);
            _loopingSfxSources[loopId] = src;
        }

        // FIX M4: si había un fade-out en curso para este loopId (StopLoopingSFX con fadeOut>0
        // seguido de un reinicio), cancelarlo — si no, el fade viejo termina llamando src.Stop()
        // sobre el loop recién reiniciado.
        if (_loopFadeRoutines.TryGetValue(loopId, out var pendingFade) && pendingFade != null)
        {
            StopCoroutine(pendingFade);
            _loopFadeRoutines.Remove(loopId);
        }

        if (eventKey.StartsWith("Ambience", StringComparison.OrdinalIgnoreCase)) _ambienceSources.Add(src);
        else _ambienceSources.Remove(src);
        src.outputAudioMixerGroup = eventKey.StartsWith("UI_", StringComparison.OrdinalIgnoreCase)
            ? uiGroup : _ambienceSources.Contains(src) ? ambienceGroup : sfxGroup;
        src.loop = true;
        src.clip = clip;
        SetSourceVolume(src, volume);
        src.Play();
    }

    /// <summary>
    /// Detiene el SFX en loop asociado a loopId (ver PlayLoopingSFX). Con fadeOut > 0 hace un
    /// fundido de salida antes de pararlo; con 0 (o por defecto) lo corta en seco.
    /// </summary>
    public void StopLoopingSFX(string loopId, float fadeOut = 0f)
    {
        if (string.IsNullOrWhiteSpace(loopId)) return;
        if (!_loopingSfxSources.TryGetValue(loopId, out var src) || src == null) return;
        if (!src.isPlaying) return;

        // FIX M4: cancelar cualquier fade-out previo de este mismo loopId antes de arrancar uno
        // nuevo (o un Stop en seco), para no dejar dos corrutinas escribiendo src.volume a la vez.
        if (_loopFadeRoutines.TryGetValue(loopId, out var existingFade) && existingFade != null)
        {
            StopCoroutine(existingFade);
            _loopFadeRoutines.Remove(loopId);
        }

        if (fadeOut > 0f)
            _loopFadeRoutines[loopId] = StartCoroutine(FadeOutAndStopLoop(loopId, src, fadeOut));
        else
            src.Stop();
    }

    /// <summary>
    /// Silencia (o restaura) en el sitio el SFX en loop asociado a loopId, sin detenerlo ni
    /// perder su posición de reproducción — a diferencia de StopLoopingSFX, pensado para
    /// suspensiones temporales y reversibles (p.ej. lluvia/viento de ambiente mientras el
    /// jugador está en un interior, ver DayNightCycle.SetWeatherAudioSuppressed). No hace nada
    /// si el loopId no tiene una fuente activa todavía (PlayLoopingSFX aún no se ha llamado).
    /// </summary>
    public void SetLoopingSFXMuted(string loopId, bool muted)
    {
        if (string.IsNullOrWhiteSpace(loopId)) return;
        if (!_loopingSfxSources.TryGetValue(loopId, out var src) || src == null) return;
        src.mute = muted;
    }

    IEnumerator FadeOutAndStopLoop(string loopId, AudioSource src, float duration)
    {
        float startVolume = BaseVolume(src);
        float elapsed = 0f;
        while (elapsed < duration && src != null)
        {
            elapsed += Time.unscaledDeltaTime;
            SetSourceVolume(src, Mathf.Lerp(startVolume, 0f, elapsed / duration));
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            SetSourceVolume(src, startVolume);
        }
        _loopFadeRoutines.Remove(loopId);
    }

    // ===========================================================
    // SFX (métodos internos y legacy)
    /// Configura filtros reutilizables y suma ganancia al volumen de diálogo del usuario.
    public void AplicarEfectoDeVoz(PresetDeVoz preset, float gananciaDb = 0f)
    {
        if (_voiceSource == null) return;
        QuitarEfectoDeVoz();
        if (preset != PresetDeVoz.Ninguno)
        {
            if (_reverbDeVoz == null)
            {
                _reverbDeVoz = _voiceSource.gameObject.AddComponent<AudioReverbFilter>();
                _ecoDeVoz = _voiceSource.gameObject.AddComponent<AudioEchoFilter>();
            }
            // Valores en milibelios salvo tiempos en segundos; la señal seca permanece íntegra.
            float cola = 0.45f, sala = -2200f, nivel = -2600f, reflexiones = -3000f;
            switch (preset)
            {
                case PresetDeVoz.ExteriorNoche:
                    cola = 0.9f; sala = -1800f; nivel = -2000f; reflexiones = -2600f;
                    break;
                case PresetDeVoz.Plegaria:
                    cola = 3.2f; sala = -1000f; nivel = -1100f; reflexiones = -1800f;
                    break;
                case PresetDeVoz.Epico:
                    cola = 6.5f; sala = -500f; nivel = -500f; reflexiones = -1200f;
                    break;
            }
            _reverbDeVoz.reverbPreset = AudioReverbPreset.User;
            _reverbDeVoz.dryLevel = 0f;
            _reverbDeVoz.room = sala;
            _reverbDeVoz.roomHF = -600f;
            _reverbDeVoz.roomLF = 0f;
            _reverbDeVoz.decayTime = cola;
            _reverbDeVoz.decayHFRatio = 0.65f;
            _reverbDeVoz.reflectionsLevel = reflexiones;
            _reverbDeVoz.reflectionsDelay = 0.025f;
            _reverbDeVoz.reverbLevel = nivel;
            _reverbDeVoz.reverbDelay = 0.04f;
            _reverbDeVoz.hfReference = 5000f;
            _reverbDeVoz.lfReference = 250f;
            _reverbDeVoz.diffusion = 90f;
            _reverbDeVoz.density = 100f;
            _reverbDeVoz.enabled = true;
            _ecoDeVoz.delay = 320f;
            _ecoDeVoz.decayRatio = 0.35f;
            _ecoDeVoz.wetMix = 0.22f;
            _ecoDeVoz.dryMix = 1f;
            _ecoDeVoz.enabled = preset == PresetDeVoz.Epico;
        }
        float ganancia = Mathf.Clamp(gananciaDb, 0f, 20f);
        if (ganancia <= 0f) return;
        if (mixer != null && dialogueGroup != null && dialogueGroup.audioMixer == mixer
            && mixer.GetFloat(dialogueVolumeParam, out _dialogoDbOriginal))
        {
            _gananciaDeVozAplicada = mixer.SetFloat(dialogueVolumeParam,
                Mathf.Clamp(_dialogoDbOriginal + ganancia, -80f, 20f));
            _gananciaDeVozDb = ganancia;
        }
        // Sin bus expuesto, la misma relación señal/fondo se obtiene atenuando el resto.
        if (!_gananciaDeVozAplicada)
            BeginDuck("efectoDeVoz", -ganancia, -ganancia, -ganancia, 0.15f);
    }

    /// Retira filtros y ganancia incluso al deshabilitar el servicio o saltar una secuencia.
    public void QuitarEfectoDeVoz()
    {
        if (_reverbDeVoz != null) _reverbDeVoz.enabled = false;
        if (_ecoDeVoz != null) _ecoDeVoz.enabled = false;
        if (_gananciaDeVozAplicada && mixer != null)
            mixer.SetFloat(dialogueVolumeParam, _dialogoDbOriginal);
        _gananciaDeVozAplicada = false;
        _gananciaDeVozDb = 0f;
        EndDuck("efectoDeVoz", 0f);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f, float tono = 1f)
        => PlayEffect(clip, volume, null, false, pitch: Mathf.Clamp(tono, 0.01f, 3f));

    public void PlaySFXAt(AudioClip clip, Vector3 worldPos, float volume = 1f, float tono = 1f)
        => PlayEffect(clip, volume, worldPos, false, pitch: Mathf.Clamp(tono, 0.01f, 3f));

    /// Reproduce una reacción sobre el diálogo mediante una fuente independiente del pool.
    /// El sonido del rayo sobrevive a detener la tormenta y usa el volumen SFX del usuario.
    public void ProgramarTrueno(string clave, float retraso, float volumen = 1f)
    {
        if (!isActiveAndEnabled) return;
        var clip = FindSfxClipByKey(clave);
        if (clip == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[AudioService] El trueno '{clave}' no tiene clip asignado.");
#endif
            return;
        }
        clip.LoadAudioData();
        StartCoroutine(SonarTrueno(clip, Mathf.Clamp(retraso, 0.2f, 0.9f), volumen));
    }
    private IEnumerator SonarTrueno(AudioClip clip, float retraso, float volumen)
    {
        yield return new WaitForSecondsRealtime(retraso);
        while (clip != null && clip.loadState == AudioDataLoadState.Loading) yield return null;
        if (clip == null || clip.loadState == AudioDataLoadState.Failed) yield break;
        var fuente = Rent2D();
        _truenos.Add(fuente);
        fuente.outputAudioMixerGroup = sfxGroup;
        fuente.pitch = 1f;
        fuente.priority = 32;
        SetSourceVolume(fuente, volumen);
        fuente.clip = clip;
        fuente.Play();
        StartCoroutine(ReturnWhenDone(fuente, _pool2D));
    }
    private readonly Dictionary<SequencePlayer, VocalReactions.EstadoDeSecuencia> _reaccionesPorSecuencia = new();
    private readonly VocalReactions.EstadoDeSecuencia _reaccionesFueraDeSecuencia = new();
    private readonly List<float> _finDeReacciones = new();
    public void PlayReaction(string character, string kind, float volume = 1f, Vector3? worldPosition = null,
        string emisor = null, SequencePlayer secuencia = null, bool risaSiAnimoGastado = false)
    {
        if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(kind)) return;
        character = character.Trim();
        kind = kind.Trim().ToLowerInvariant();
        // Los beats existentes comparten la memoria sin modificar sus llamadas.
        if (secuencia == null)
        {
            var activas = CinematicSequencerBase.RunningSequences;
            for (int i = activas.Count - 1; i >= 0; i--)
                if (activas[i] is SequencePlayer player) { secuencia = player; break; }
        }
        var estado = _reaccionesFueraDeSecuencia;
        if (secuencia != null && !_reaccionesPorSecuencia.TryGetValue(secuencia, out estado))
        {
            estado = new VocalReactions.EstadoDeSecuencia();
            _reaccionesPorSecuencia.Add(secuencia, estado);
            var propietaria = secuencia;
            secuencia.RegisterCleanup(() => _reaccionesPorSecuencia.Remove(propietaria));
        }
        if (risaSiAnimoGastado && kind == "cheer" && estado.AnimoGastado(character)) kind = "laugh";
        float ahora = Time.unscaledTime;
        if (!estado.PuedeSonar(character, kind, ahora, secuencia != null)) return;
        _finDeReacciones.RemoveAll(fin => fin <= ahora);
        if (_finDeReacciones.Count >= 3) return;
        if (!VocalReactions.TryGet(character, kind, out var clip))
        {
            if (kind != "cheer") return;
            kind = "laugh";
            if (!VocalReactions.TryGet(character, kind, out clip)) return;
        }
        float pitch = UnityEngine.Random.Range(0.96f, 1.04f);
        estado.Registrar(character, kind, ahora);
        _finDeReacciones.Add(ahora + clip.length / pitch);
        PlayEffect(clip, volume, worldPosition, false, dialogue: true, pitch: pitch);
    }

    void PlayEffect(AudioClip clip, float volume, Vector3? worldPosition, bool ambience, bool ui = false,
        bool dialogue = false, float pitch = 1f)
    {
        if (!clip) return;
        var src = worldPosition.HasValue ? Rent3D() : Rent2D();
        src.outputAudioMixerGroup = dialogue ? dialogueGroup : ui ? uiGroup : ambience ? ambienceGroup : sfxGroup;
        src.pitch = pitch;
        if (worldPosition.HasValue) src.transform.position = worldPosition.Value;
        else src.transform.localPosition = Vector3.zero;
        if (ambience)
        {
            _ambienceSources.Add(src);
            SetSourceVolume(src, volume);
        }
        else
        {
            _ambienceSources.Remove(src);
            SetSourceVolume(src, volume);
        }
        src.clip = clip;
        src.Play();
        StartCoroutine(ReturnWhenDone(src, worldPosition.HasValue ? _pool3D : _pool2D));
    }
    void PlaySfxForKey(string key)
    {
        if (profile == null || string.IsNullOrEmpty(key)) return;
        for (int i = 0; i < profile.eventSfx.Count; i++)
        {
            var r = profile.eventSfx[i];
            if (r != null &&
                !string.IsNullOrWhiteSpace(r.eventKey) &&
                string.Equals(r.eventKey, key, StringComparison.OrdinalIgnoreCase) &&
                r.sfx != null)
            {
                PlaySFX(r.sfx);
                break;
            }
        }
    }

    // ===========================================================
    // Mixer + utilidades
    public void SetExposedVolume(string exposedParam, float linear01)
    {
        if (!mixer || string.IsNullOrEmpty(exposedParam)) return;
        float dB = Mathf.Lerp(-80f, 0f, Mathf.Clamp01(linear01));
        if (_gananciaDeVozAplicada && exposedParam == dialogueVolumeParam)
        {
            _dialogoDbOriginal = dB;
            dB = Mathf.Clamp(dB + _gananciaDeVozDb, -80f, 20f);
        }
        mixer.SetFloat(exposedParam, dB);
    }

    public float GetExposedVolume01(string exposedParam, float def01 = 1f)
    {
        if (!mixer || string.IsNullOrEmpty(exposedParam)) return def01;
        if (_gananciaDeVozAplicada && exposedParam == dialogueVolumeParam)
            return Mathf.InverseLerp(-80f, 0f, _dialogoDbOriginal);
        return mixer.GetFloat(exposedParam, out float dB) ? Mathf.InverseLerp(-80f, 0f, dB) : def01;
    }

    AudioSource CreateChildSource(string name, AudioMixerGroup group, bool spatial, bool loop=false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.outputAudioMixerGroup = group ? group : null;
        src.spatialBlend = spatial ? 1f : 0f;
        if (spatial) { src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 2f; src.maxDistance = 30f; }
        return src;
    }

    AudioSource Rent2D() => _pool2D.Count > 0 ? _pool2D.Dequeue() : CreateChildSource("SFX2D_dyn", sfxGroup, spatial:false);
    AudioSource Rent3D() => _pool3D.Count > 0 ? _pool3D.Dequeue() : CreateChildSource("SFX3D_dyn", sfxGroup, spatial:true);

    IEnumerator ReturnWhenDone(AudioSource src, Queue<AudioSource> pool)
    {
        // FIX M4 (auditoría 2026-08-07): WaitForSeconds está escalado por Time.timeScale. En
        // pausa (timeScale=0) esta corrutina nunca avanza, así que la fuente SFX nunca vuelve al
        // pool y Rent2D/Rent3D siguen creando "SFX2D_dyn"/"SFX3D_dyn" sin límite mientras dure la
        // pausa. WaitForSecondsRealtime no depende de timeScale.
        float wait = src.clip ? Mathf.Max(0.02f, src.clip.length / Mathf.Max(0.01f, src.pitch)) : 1f;
        yield return new WaitForSecondsRealtime(wait);
        _truenos.Remove(src); src.priority = 128;
        src.Stop(); src.clip = null; src.pitch = 1f; _sourceVolumes.Remove(src); _ambienceSources.Remove(src); pool.Enqueue(src);
    }

    AudioClip GetCurrentMusicClip()
    {
        var c = _musicATurn ? _musicB : _musicA;
        return c ? c.clip : null;
    }
    
    /// <summary>
    /// Propiedad pública para obtener el clip de música actual
    /// </summary>
    public AudioClip CurrentMusicClip => GetCurrentMusicClip();

    /// <summary>
    /// True mientras la música de combate está activa (entre BeginBattleMusic y EndBattleMusic).
    /// Usado por sistemas externos (p. ej. AmbientZone) para no pisar la música de combate
    /// con transiciones ambientales mientras el combate sigue en curso.
    /// </summary>
    public bool IsBattleActive => _battleActive;

    /// <summary>
    /// Fuerza el fin de cualquier estado de batalla pendiente (flag _battleActive + stack de
    /// música). Necesario en salidas anómalas del combate (Game Over) donde nunca se llega a
    /// llamar a EndBattleById/RestoreAfterBattle/OnBattleWonRestoreMusic porque el jugador
    /// murió en vez de ganar. Sin esto, _battleActive se queda a true indefinidamente —
    /// AudioService es DontDestroyOnLoad y sobrevive al viaje Game Over → MainMenu → Continuar—
    /// y AmbientZone.TransitionToZoneMusic/RestorePreviousMusic usan IsBattleActive como guard
    /// para no pisar música de combate, bloqueando la música de zona para siempre tras morir.
    /// </summary>
    public void ForceEndBattleState()
    {
        _musicStack.Clear();
        _battleActive = false;
        _activeBattleId = null;
    }

    /// <summary>
    /// Activa o desactiva el loop de las dos fuentes de música. Útil para temas que deben sonar
    /// una sola vez y terminar (p.ej. el tema de créditos, ver CreditsSceneController), en vez de
    /// heredar el loop=true por defecto que usa la música de ambiente/gameplay.
    /// </summary>
    public void SetMusicLooping(bool loop)
    {
        if (_musicA != null) _musicA.loop = loop;
        if (_musicB != null) _musicB.loop = loop;
    }

    /// <summary>
    /// Segundos que quedan del clip de música actualmente en reproducción (fuente activa).
    /// Devuelve -1 si no hay música sonando o si esa fuente tiene loop activo (no tiene un
    /// "final" con sentido). Pensado para sincronizar UI con el final de un tema no-loop.
    /// </summary>
    /// Segundos reproducidos de la música que suena (fuente activa), o -1 si no suena nada.
    public float GetMusicTime()
    {
        var active = _musicATurn ? _musicB : _musicA;
        if (active == null || active.clip == null || !active.isPlaying) return -1f;
        return active.time;
    }

    public float GetMusicRemainingSeconds()
    {
        var active = _musicATurn ? _musicB : _musicA;
        if (active == null || active.clip == null || !active.isPlaying || active.loop)
            return -1f;
        return Mathf.Max(0f, active.clip.length - active.time);
    }
    
    /// <summary>
    /// Restaura la música de la escena actual (según las reglas del profile)
    /// </summary>
    public bool RestoreSceneMusic(float fadeDuration = -1f)
    {
        if (fadeDuration < 0f) fadeDuration = defaultFade;

        // FIX (12 sep 2026, reporte de Raúl — "la música de MainWorld sonó dentro de la casa de
        // Will"): RestoreSceneMusic() es el punto de restauración compartido por
        // CinematicSequencerBase.RestoreMusic()/RestoreAfterBattle/RestoreAfterMinigame — todos
        // ellos, hasta ahora, ignoraban por completo si el jugador sigue dentro de un interior con
        // música propia (AudioGraphProfile.sceneMusic para la escena de ese interior, ver
        // HandleInteriorEntered) y restauraban sin más la última música de la escena BASE
        // (MainWorld) — que nunca cambia mientras un interior aditivo está cargado encima (ver
        // AudioService.OnSceneLoaded). Esto no se notaba porque, hasta ahora, ningún interior tenía
        // música propia — con WillHouse ya configurada, hacía falta esta prioridad extra, más
        // específica que la escena base: si seguimos en modo Interior, su música manda.
        var ec = EnvironmentController.Instance;
        if (ec != null && ec.CurrentMode == EnvironmentMode.Interior && ec.CurrentInterior)
        {
            string interiorScene = ec.CurrentInterior.gameObject.scene.name;
            if (TryGetSceneMusicRule(interiorScene, out var interiorClip))
            {
                PlayMusic(interiorClip, fadeDuration);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[AudioService] RestoreSceneMusic: seguimos en interior '{interiorScene}' → '{interiorClip.name}'");
#endif
                return true;
            }
        }

        // Intentar restaurar la última música de escena solicitada
        if (_lastRequestedSceneClip != null)
        {
            PlayMusic(_lastRequestedSceneClip, fadeDuration);
            return true;
        }
        
        // Si no hay, buscar en las reglas del profile para la escena actual
        if (profile != null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            foreach (var rule in profile.sceneMusic)
            {
                if (!string.IsNullOrEmpty(rule.sceneName) && currentScene.Contains(rule.sceneName))
                {
                    if (rule.music != null)
                    {
                        PlayMusic(rule.music, fadeDuration);
                        return true;
                    }
                }
            }
        }
        
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[AudioService] No se encontró música para restaurar en la escena actual");
#endif
        return false;
    }

    // ===========================================================
    // API compatible con AudioManager (para narrative nodes)

    public void SetVolume(AudioBus bus, float volume01)
    {
        if (mixer == null) return;
        string param = bus switch
        {
            AudioBus.Master => masterVolumeParam,
            AudioBus.Music => musicVolumeParam,
            AudioBus.Sfx => sfxVolumeParam,
            AudioBus.Dialogue => dialogueVolumeParam,
            _ => null
        };
        if (!string.IsNullOrEmpty(param))
            SetExposedVolume(param, volume01);
    }

    public float GetVolume(AudioBus bus)
    {
        if (mixer == null) return 1f;
        string param = bus switch
        {
            AudioBus.Master => masterVolumeParam,
            AudioBus.Music => musicVolumeParam,
            AudioBus.Sfx => sfxVolumeParam,
            AudioBus.Dialogue => dialogueVolumeParam,
            _ => null
        };
        return !string.IsNullOrEmpty(param) ? GetExposedVolume01(param) : 1f;
    }

    public void Mute(AudioBus bus, bool mute)
    {
        SetVolume(bus, mute ? 0f : 1f);
    }

    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (_voiceSource == null || clip == null) return;
        CancelVoiceMonitor();
        _voiceSource.Stop();
        _voiceSource.clip = clip;
        _voiceSource.volume = Mathf.Clamp01(volume);
        _voiceSource.Play();
        _ajustesDeVozActiva = CinematicSequencerBase.AnySequenceActive
            ? (profile != null && profile.cinematicVoiceDuck != null ? profile.cinematicVoiceDuck : _duckCinematico)
            : (profile != null && profile.voiceDuck != null ? profile.voiceDuck : _defaultVoiceDuck);
        _voiceDuckActive = VoiceDuck.enabled;
        RefreshDuck(VoiceDuck.attackSeconds);
        _voiceDuckRoutine = StartCoroutine(MonitorVoice(_voiceToken));
    }

    /// Indica si la fuente de voz está sonando.
    public bool HayVozSonando => _voiceSource != null && _voiceSource.isPlaying;

    /// <summary>Mide el volumen RMS con un buffer reutilizable.</summary>
    public float NivelDeVoz()
    {
        if (!HayVozSonando) return 0f;
        _voiceSource.GetOutputData(_muestrasDeVoz, 0);
        float suma = 0f;
        for (int i = 0; i < _muestrasDeVoz.Length; i++)
            suma += _muestrasDeVoz[i] * _muestrasDeVoz[i];
        return Mathf.Sqrt(suma / _muestrasDeVoz.Length);
    }

    public bool IsVoicePlaying(AudioClip clip)
        => clip != null && _voiceSource != null && _voiceSource.clip == clip && _voiceSource.isPlaying;

    /// Detiene la voz actual.
    public void StopVoice()
    {
        if (_voiceSource != null) _voiceSource.Stop();
        if (!_voiceDuckActive || _voiceSource == null) return;
        CancelVoiceMonitor();
        _voiceDuckRoutine = StartCoroutine(MonitorVoice(_voiceToken, false));
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        PlaySFX(clip, volume);
    }
}
