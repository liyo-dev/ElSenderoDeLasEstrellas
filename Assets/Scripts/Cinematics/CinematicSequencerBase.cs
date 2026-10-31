using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using EasyTransition;
using Sendero.Core.Feedback;
using Sendero.UI;

/// Ciclo de vida común de una cinemática: escucha su señal de entrada, bloquea al jugador y
/// esconde el HUD, hace las transiciones de entrada y salida (o recoge el telón), activa la
/// cámara cinemática, gestiona la música, el salto («mantener para saltar») y el cierre aunque
/// la secuencia se corte. Su única subclase es <see cref="SequencePlayer"/>; las cinemáticas
/// nuevas se escriben como <see cref="SequenceDefinition"/> o guion, nunca heredando de aquí.
///
/// Los estáticos (<see cref="AnySequenceActive"/>, <see cref="RunningSequences"/>,
/// <see cref="RequestSkipAll"/>) son los que consulta el resto del juego.
[DisallowMultipleComponent]
public abstract class CinematicSequencerBase : MonoBehaviour
{
    [Header("Señales narrativas")]
    [FormerlySerializedAs("signalIn")]
    [SerializeField] private string _signalIn;
    [FormerlySerializedAs("signalOut")]
    [SerializeField] private string _signalOut;

    [Header("Gameplay")]
    [FormerlySerializedAs("actionManager")]
    [SerializeField] private PlayerActionManager _actionManager;

    [Header("Cámara — driver cinemático")]
    [FormerlySerializedAs("cinematicCamera")]
    [SerializeField] protected CinematicCameraDriver _cinematicCamera;

    [Header("Música")]
    [FormerlySerializedAs("audioProfile")]
    [SerializeField] private AudioGraphProfile _audioProfile;
    [FormerlySerializedAs("sequenceMusicId")]
    [SerializeField] private string _sequenceMusicId;

    [Header("Transición")]
    [Tooltip("Transición al entrar: cubre el gameplay, el corte de cámara ocurre en el cut point, luego revela la cinemática.")]
    [SerializeField] private TransitionSettings _entryTransition;
    [Tooltip("Transición al salir: cubre la cinemática, el corte de cámara ocurre en el cut point, luego revela el gameplay.")]
    [SerializeField] private TransitionSettings _exitTransition;

    [Header("Entorno")]
    [Tooltip("Asignar si la cinemática ocurre en un interior. Activa/desactiva el skybox sólido automáticamente.")]
    [SerializeField] private AnchorEnvironment _interiorAnchor;

    /// True si la cinemática ocurre en un interior (tiene AnchorEnvironment asignado).
    protected bool OcurreEnInterior => _interiorAnchor != null;

    protected AudioGraphProfile.SequenceRule MusicRule { get; private set; }

    private Action _signalInHandler;

    // ── Señales por dato en vez de por Inspector (16 sep 2026) ────────────────
    //
    // _signalIn/_signalOut siguen siendo campos del Inspector y no cambia nada para los sequencers
    // existentes. Estas dos propiedades permiten que una subclase los saque de otro sitio — el caso
    // real es SequencePlayer, que los lee de su SequenceDefinition para que una secuencia entera
    // (contenido Y enganche con el grafo narrativo) se pueda escribir como datos, sin tocar la
    // escena. Devolver null o vacío = usar el campo del Inspector, como siempre.
    protected virtual string SignalInOverride => null;
    protected virtual string SignalOutOverride => null;

    // Señal de entrada efectiva, resuelta UNA vez en Awake. Se guarda porque OnDestroy tiene que
    // desuscribir exactamente la misma con la que se suscribió: si la subclase cambiara de
    // definición en caliente, recalcularla en OnDestroy dejaría el handler colgado para siempre.
    private string _resolvedSignalIn;

    /// Señal de entrada efectiva (override de la subclase si la hay, si no la del Inspector).
    protected string ResolvedSignalIn =>
        string.IsNullOrEmpty(SignalInOverride) ? _signalIn : SignalInOverride;

    /// Señal de salida efectiva (override de la subclase si la hay, si no la del Inspector).
    protected string ResolvedSignalOut =>
        string.IsNullOrEmpty(SignalOutOverride) ? _signalOut : SignalOutOverride;

    // FIX INC-059: contador estático de cinemáticas activas (puede haber más de un sequencer
    // encadenado). Otros sistemas (ej: NPCQuestIconManager) lo consultan para ocultar iconos de
    // quest sobre la cabeza de NPCs ajenos a la propia cinemática mientras esta se reproduce.
    private static int s_activeSequenceCount;
    public static bool AnySequenceActive => s_activeSequenceCount > 0;

    /// Se dispara cuando AnySequenceActive cambia (false→true al bloquear la primera cinemática
    /// encadenada, true→false al desbloquear la última). Pensado para que un controlador de UI
    /// persistente (ej. el botón global de "saltar cinemática") pueda mostrarse/ocultarse sin
    /// tener que sondear AnySequenceActive en Update() — ver GlobalCinematicSkipController.
    public static event Action<bool> OnAnySequenceActiveChanged;

    // ── Skip ("saltar cinemática") — registro de secuencias en curso ───────────
    // Lista de instancias con Co_Sequence() activo ahora mismo (normalmente una, pero puede haber
    // más de una encadenada, ver s_activeSequenceCount arriba). El botón global de skip llama a
    // RequestSkipAll() sin necesitar saber qué sequencer concreto está sonando.
    private static readonly List<CinematicSequencerBase> s_runningSequences = new();
    public static IReadOnlyList<CinematicSequencerBase> RunningSequences => s_runningSequences;

    /// Solicita saltar TODAS las cinemáticas activas ahora mismo (normalmente una sola). Pensado
    /// para el botón global de skip — no falla si no hay ninguna activa.
    public static void RequestSkipAll()
    {
        if (s_runningSequences.Count == 0) return;
        // Copia defensiva: RequestSkip() modifica s_runningSequences (se auto-elimina de la lista),
        // no se puede iterar la lista original mientras se modifica.
        var running = s_runningSequences.ToArray();
        foreach (var seq in running)
            seq.RequestSkip();
    }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsBase()
    {
        s_activeSequenceCount = 0;
        s_runningSequences.Clear();
        OnAnySequenceActiveChanged = null;
    }
#endif

    // FIX INC-052: si Co_Sequence() lanza una excepción o el objeto se destruye a mitad de la
    // cinemática (ej: cambio de escena, referencia nula puntual de un prefab concreto), el HUD y
    // el minimapa se quedaban ocultos para siempre y el ActionMode.Cinematic nunca se hacía Pop.
    // Como la cinemática de Liam invocando al gólem se ejecuta justo antes de la batalla, un fallo
    // aislado aquí dejaba el HUD desaparecido durante todo el combate. Mismo patrón de fix que
    // BossIntroPresentation.PlayIntroduction() (try/finally que garantiza la restauración).
    private bool _cinematicLocked;

    // FIX (Agosto 2026): guarda contra solapamiento. Si la señal de entrada se dispara dos veces
    // seguidas (p. ej. un bucle de reintento del grafo narrativo que reenvía la señal antes de que
    // el jugador termine el intento anterior — bug reproducido con AWAKEN_FAILED/AWAKEN_START en
    // la secuencia del Despertar), dos Co_Sequence() concurrentes llaman ambos a LockCinematic() pero
    // EndCinematic() solo restaura una vez (por el guard de _cinematicLocked de más abajo): el
    // segundo Push de ActionMode.Cinematic se queda sin su Pop y el jugador se queda pillado en
    // modo Cinematic en vez de volver al gameplay. Ignorar la señal mientras ya hay una secuencia
    // en curso mantiene Push/Pop siempre equilibrados sin importar cuántas veces dispare el grafo.
    private bool _sequenceRunning;

    // Handle de la corrutina Co_SequenceGuarded() activa, para poder detenerla en seco desde
    // RequestSkip() sin depender de que su try/finally se ejecute (StopCoroutine no garantiza
    // Dispose() de los IEnumerator anidados vía "yield return subrutina", a diferencia de
    // StartCoroutine hijas independientes). Todo el cierre limpio al saltar corre en
    // Co_SkipToEnd(), de forma explícita.
    private Coroutine _activeSequenceCoroutine;

    // True mientras se está resolviendo un RequestSkip() para esta instancia (evita disparos dobles
    // si el jugador mantiene pulsado el botón más allá del primer frame en que se completa el hold).
    private bool _skipRequested;

    // FIX A7 (auditoría 2026-08-07): handlers activos de Co_Transition, guardados a nivel de
    // instancia para poder desuscribirlos desde OnDestroy. TransitionManager es persistente
    // (DontDestroyOnLoad); si este objeto se destruye/su corrutina se interrumpe a mitad de una
    // transición (StopAllCoroutines, cambio de escena), los handlers locales de Co_Transition
    // quedaban suscritos para siempre y podían disparar más tarde para una transición de OTRO
    // sistema, ejecutando onCutPoint (p. ej. BeginCinematic()) sobre un sequencer ya destruido.
    private UnityEngine.Events.UnityAction _activeTransitionCutHandler;
    private UnityEngine.Events.UnityAction _activeTransitionEndHandler;

    // ── Ciclo de vida Unity ───────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _signalInHandler = () =>
        {
            if (_sequenceRunning)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: señal de entrada '{ResolvedSignalIn}' recibida mientras la secuencia ya está en curso — ignorada para evitar solapamiento y un Push/Pop de ActionMode.Cinematic desbalanceado.");
#endif
                return;
            }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[Secuencia:{name}] Recibida '{_resolvedSignalIn}': empieza.");
#endif
            _activeSequenceCoroutine = StartCoroutine(Co_SequenceGuarded());
        };
        _resolvedSignalIn = ResolvedSignalIn;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        // INC-430: una secuencia sin señal de entrada no se entera de nada y antes no lo decía.
        if (string.IsNullOrEmpty(_resolvedSignalIn))
            Debug.LogWarning($"[Secuencia:{name}] No tiene señal de entrada: no va a arrancar " +
                             "nunca por el grafo narrativo.", this);
#endif
        Suscribir();
        if (isActiveAndEnabled) StartCoroutine(Co_VigilarLaSuscripcion());
    }

    /// Se engancha a la señal de entrada, y se vuelve a enganchar si hace falta (INC-387).
    ///
    /// «La misión de Eldran sigue sin empezar»: en el log del 23 sep se ve PERAS_START emitida con
    /// oyentes (Oliver se va del grupo) y SEQ_PerasEldran sin enterarse. Este sequencer se
    /// suscribe en Awake, y ahí `DefaultNarrativeSignals.EnsureInstance()` puede crear una
    /// instancia automática (en el log sale un objeto llamado «DefaultNarrativeSignals (Auto)»)
    /// que más tarde es sustituida por la de la escena: el handler se queda colgado de la vieja y
    /// no lo llama nadie nunca más. Por eso ahora se comprueba en cada OnEnable y en Start que se
    /// sigue estando enganchado a la instancia VIVA, y si no, se vuelve a enganchar.
    private void Suscribir()
    {
        if (string.IsNullOrEmpty(_resolvedSignalIn)) return;

        var señales = DefaultNarrativeSignals.EnsureInstance();
        if (señales == null || señales == _señalesSuscritas) return;

        if (_señalesSuscritas != null)
            _señalesSuscritas.OffCustom(_resolvedSignalIn, _signalInHandler);

        señales.OnCustom(_resolvedSignalIn, _signalInHandler);
        _señalesSuscritas = señales;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Secuencia:{name}] A la espera de '{_resolvedSignalIn}' " +
                  $"(y avisará con '{ResolvedSignalOut}') en '{señales.name}'.");
#endif
    }

    private DefaultNarrativeSignals _señalesSuscritas;

    /// Durante el arranque la instancia de señales puede cambiar (la automática por la de la
    /// escena), así que los primeros segundos se comprueba de vez en cuando. Después, nunca más.
    private IEnumerator Co_VigilarLaSuscripcion()
    {
        var espera = new WaitForSecondsRealtime(0.25f);
        for (int i = 0; i < 12; i++)
        {
            yield return espera;
            Suscribir();
        }
    }

    /// Envuelve Co_Sequence() para garantizar que el HUD/minimapa/modo Cinematic se restauran
    /// aunque la subclase termine de forma anómala (excepción o destrucción del objeto).
    private IEnumerator Co_SequenceGuarded()
    {
        _sequenceRunning = true;
        s_runningSequences.Add(this);
        try
        {
            yield return Co_Sequence();
        }
        finally
        {
            _sequenceRunning = false;
            _activeSequenceCoroutine = null;
            s_runningSequences.Remove(this);
            Telon.Soltar(ClaveTelon);   // red de seguridad: idempotente
            if (_cinematicLocked)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name} terminó de forma anómala con la cinemática aún bloqueada (HUD oculto). Restaurando estado.");
#endif
                EndCinematic();
            }
        }
    }

    // Al salir del juego (o del Play en el Editor) no se restaura nada: los servicios ya se
    // están destruyendo y no hay partida que proteger.
    private static bool s_saliendoDelJuego;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void EscucharSalidaDelJuego()
    {
        s_saliendoDelJuego = false;
        Application.quitting -= MarcarSalida;
        Application.quitting += MarcarSalida;
    }

    private static void MarcarSalida() => s_saliendoDelJuego = true;

    /// Si el objeto se apaga o se destruye con la secuencia en marcha (se descarga su escena, se
    /// carga partida), Unity para sus corrutinas SIN ejecutar el finally de Co_SequenceGuarded:
    /// el cierre se hace aquí. Idempotente, igual que el camino de skip (INC-448).
    protected virtual void OnDisable()
    {
        if (s_saliendoDelJuego || (!_sequenceRunning && !_cinematicLocked)) return;

        _sequenceRunning = false;
        _activeSequenceCoroutine = null;
        s_runningSequences.Remove(this);
        try { OnSkipCleanup(); }
        catch (Exception e) { Debug.LogException(e); }
        Telon.Soltar(ClaveTelon);
        ClearTransitionHandlers();
        if (_cinematicLocked) EndCinematic();
    }

    protected virtual void OnDestroy()
    {
        FeedbackService.CancelAllShakes();
        if (_señalesSuscritas != null) _señalesSuscritas.OffCustom(_resolvedSignalIn, _signalInHandler);
        else DefaultNarrativeSignals.Instance?.OffCustom(_resolvedSignalIn, _signalInHandler);
        _señalesSuscritas = null;

        // FIX A7 (auditoría 2026-08-07): ver comentario de _activeTransitionCutHandler/_activeTransitionEndHandler.
        ClearTransitionHandlers();
    }

    /// Suelta los handlers que esta secuencia tenga enganchados al TransitionManager.
    ///
    /// FIX (auditoría 17 sep 2026): esto solo se hacía en OnDestroy, y eso deja un agujero grave en
    /// el camino de SKIP. Los handlers se enganchan al empezar una transición y solo se quitan
    /// solos al dispararse; si el jugador salta la secuencia mientras la transición está a medias,
    /// se quedan pegados a un TransitionManager que es DontDestroyOnLoad — es decir, para toda la
    /// partida. La siguiente transición de CUALQUIER otro sistema (cruzar una puerta, cambiar de
    /// escena) ejecutaría el handler viejo, que llama a BeginCinematic() y con él a
    /// CinematicCameraDriver.Activate(): la cámara se queda secuestrada en pleno gameplay y nadie
    /// la suelta, porque el EndCinematic() correspondiente ya no va a llegar nunca.
    ///
    /// Idempotente: se puede llamar las veces que haga falta.
    protected void ClearTransitionHandlers()
    {
        var tm = TransitionManager.Instance();
        if (tm != null)
        {
            if (_activeTransitionCutHandler != null) tm.onTransitionCutPointReached -= _activeTransitionCutHandler;
            if (_activeTransitionEndHandler != null) tm.onTransitionEnd -= _activeTransitionEndHandler;
        }
        _activeTransitionCutHandler = null;
        _activeTransitionEndHandler = null;
    }

    // ── Punto de entrada de la subclase ──────────────────────────────────────

    protected abstract IEnumerator Co_Sequence();

    // ── Ciclo de vida de la cinemática ────────────────────────────────────────

    /// Bloquea el input del jugador y oculta el HUD y el minimapa. Debe llamarse ANTES de la transición de entrada.
    private void LockCinematic()
    {
        _cinematicLocked = true;
        bool wasInactive = s_activeSequenceCount == 0;
        s_activeSequenceCount++;
        if (wasInactive)
        {
            // INC-601: el occlusion culling horneado se equivoca con las cámaras de las secuencias.
            SinOcclusionEnSecuencias.Activar();
            OnAnySequenceActiveChanged?.Invoke(true);
        }
        ResolveActionManager()?.PushMode(ActionMode.Cinematic);
        // El HUD, el minimapa y el indicador de la hora se esconden solos: el reproductor no
        // los conoce, solo pide que se escondan los que estén apuntados al registro.
        int ocultos = OcultablesDeCinematica.OcultarTodos(this);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (ocultos == 0)
            Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: no hay ningún sistema que se " +
                "esconda durante una cinemática, así que el HUD se quedará en pantalla. " +
                "¿La escena se ha arrancado por Start.unity?");
#endif
    }

    /// Activa la cámara cinemática y prepara la regla de música. Se llama en el cut point de la transición.
    protected void BeginCinematic()
    {
        MusicRule = ResolveAudioProfile()?.GetSequenceRule(_sequenceMusicId);
        if (_interiorAnchor != null) EnvironmentController.Instance?.BeginCinematicOverride();
        // Null-safe: secuencias que no transcurren en el mundo (una secuencia de sueño, un
        // "sueño" fuera de cualquier localización real) no necesitan controlar la cámara de mundo
        // y no asignan _cinematicCamera en el Inspector.
        _cinematicCamera?.Activate();
    }

    /// Restaura el estado de gameplay: cancela shakes, desactiva la cámara, muestra el HUD/minimapa y desbloquea input.
    protected void EndCinematic()
    {
        // Si se cierra sin haber llegado a enseñar nada (un skip antes del primer plano), el
        // telón no puede quedarse retenido por ella.
        Telon.Soltar(ClaveTelon);

        if (!_cinematicLocked) return; // Ya restaurado (evita Pop/ShowHUD duplicados si se llama dos veces)
        _cinematicLocked = false;
        s_activeSequenceCount = Mathf.Max(0, s_activeSequenceCount - 1);
        if (s_activeSequenceCount == 0)
        {
            SinOcclusionEnSecuencias.Desactivar();
            OnAnySequenceActiveChanged?.Invoke(false);
        }

        FeedbackService.CancelAllShakes();
        _cinematicCamera?.Deactivate();
        PresentadorDeLinea.EsconderTodo();
        OcultablesDeCinematica.MostrarTodos(this);
        ResolveActionManager()?.PopMode(ActionMode.Cinematic);
        if (_interiorAnchor != null) EnvironmentController.Instance?.EndCinematicOverride();
    }

    /// Devuelve _actionManager si está asignado en el Inspector; si no, lo resuelve vía
    /// ServiceLocator (y lo cachea ahí mismo para las siguientes llamadas). Esto permite que
    /// secuencias que no viven junto al jugador en la escena (la del sueño, que no
    /// transcurre en ningún lugar del mundo) no necesiten arrastrar la referencia a mano — antes,
    /// dejarla vacía provocaba un NullReferenceException aquí mismo que abortaba la coroutine sin
    /// restaurar nada ni levantar la señal de salida: la pantalla se quedaba en negro para siempre
    /// y el grafo narrativo nunca avanzaba tras el WaitCustomEventNode correspondiente.
    private PlayerActionManager ResolveActionManager()
    {
        if (_actionManager == null)
            _actionManager = ServiceLocator.Get<PlayerActionManager>(logIfMissing: false);
        return _actionManager;
    }

    // ── Transiciones ─────────────────────────────────────────────────────────

    /// Cubre la pantalla, llama a BeginCinematic() en el cut point y revela la cinemática.
    /// additionalOnCut: acciones extra en el mismo fotograma, con la pantalla cubierta.
    protected IEnumerator Co_BeginCinematicWithTransition(Action additionalOnCut = null)
    {
        LockCinematic();
        yield return Co_Transition(_entryTransition, recogerPantallaTapada: true, () =>
        {
            BeginCinematic();
            // El interior se aplica en el cut point, con la pantalla cubierta:
            // si se aplica tras el reveal se ve el skybox exterior un instante
            ApplyInteriorAtCutPoint();
            additionalOnCut?.Invoke();
        });
    }

    /// Aplica el entorno interior con la pantalla cubierta. Debe llamarse después
    /// de BeginCinematic() (que activa el override en EnvironmentController).
    private void ApplyInteriorAtCutPoint()
    {
        if (_interiorAnchor != null)
            EnvironmentController.Instance?.ApplyInteriorForCinematic(env: _interiorAnchor);
    }

    /// Cubre la pantalla, llama a EndCinematic() en el cut point y revela el gameplay.
    /// additionalOnCut: acciones extra que deben ocurrir junto con EndCinematic (mismo frame, pantalla cubierta).
    protected IEnumerator Co_EndCinematicWithTransition(Action additionalOnCut = null)
        => Co_Transition(_exitTransition, recogerPantallaTapada: false, () => { additionalOnCut?.Invoke(); EndCinematic(); });

    /// Cubre la pantalla con negro y llama a EndCinematic(), pero NO revela después.
    /// Usar cuando el sistema siguiente (ej: BossIntroPresentation) maneja su propia revelación,
    /// para evitar el parpadeo de la cámara del jugador entre la secuencia y la intro del boss.
    protected IEnumerator Co_EndCinematicStayBlack(Action additionalOnCut = null, float fadeDuration = 0.3f)
    {
        // Si la escena ya dejó la pantalla cubierta (el prólogo acaba en BLANCO, INC-424), se deja
        // como está: repintarla de negro borraba el blanco del que sale el despertar de Will.
        if (!FeedbackService.IsScreenFaded)
            yield return FeedbackService.ScreenFadeAsync(Color.black, fadeDuration, fadeIn: true);
        additionalOnCut?.Invoke();
        EndCinematic();
    }

    /// Ejecuta una transición via TransitionManager. onCutPoint se llama cuando la pantalla está cubierta.
    /// Si settings es null, llama onCutPoint de inmediato sin animación.
    /// recogerPantallaTapada: al EMPEZAR, si alguien dejó la pantalla tapada (una escena anterior
    /// que acaba en negro, un rótulo), la cinemática la recoge como si fuera el telón y la destapa
    /// en su primer plano. Al acabar no: la pantalla tapada es de quien venga después.
    private IEnumerator Co_Transition(TransitionSettings settings, bool recogerPantallaTapada, Action onCutPoint)
    {
        // (21 sep) EL TELÓN. Si la pantalla ya está en negro y retenida (arranque de partida, una
        // carga de escena, otra escena cargándose en aditivo), esta cinemática NO hace su propia
        // transición: tapar desde lo que hay en pantalla es enseñar lo que hay en pantalla, y
        // destapar al acabar la transición es enseñar la escena antes de que los primeros beats
        // la hayan montado. Recoge el telón, hace el cut point a oscuras, y lo suelta cuando ya
        // hay algo que ver (ver SueltaElTelonEnSuPrimerPlano).
        if (Telon.Cerrado || (recogerPantallaTapada && FeedbackService.IsScreenFaded))
        {
            Telon.Cerrar(ClaveTelon);
            onCutPoint?.Invoke();
            if (!SueltaElTelonEnSuPrimerPlano) StartCoroutine(Co_SoltarTelonTrasUnFotograma());
            yield break;
        }

        var tm = TransitionManager.Instance();
        // Las cámaras tienen que estar encendidas en la jerarquía: confirmado
        // con log de consola real — "se queda en la vista normal" del jugador). Causa raíz: esta
        // corrutina se suscribe a tm.onTransitionCutPointReached/onTransitionEnd y LUEGO llama
        // tm.Transition(settings, 0f) — pero si TransitionManager.Transition() encuentra
        // runningTransition == true (otra transición ya en marcha, p. ej. la del teletransporte
        // al salir de casa, que puede solaparse con el trigger que arranca esta cinemática justo
        // al cruzar la puerta) se limita a hacer `return;` y loguear "Transition already running
        // — ignoring new request", SIN disparar ninguno de los dos eventos. Los handlers de abajo
        // quedan suscritos para siempre esperando un evento que nunca llega, `done` nunca pasa a
        // true, y `yield return new WaitUntil(() => done);` bloquea esta corrutina — y por tanto
        // TODA la cinemática que la llama (Co_BeginCinematicWithTransition) — para siempre: la
        // cámara nunca corta, el resto de la secuencia nunca se ejecuta, y el input del jugador
        // (ya bloqueado por LockCinematic() antes de este punto) se queda bloqueado sin remedio.
        // Mismo fallback ya usado dos líneas más abajo para "no hay TransitionSettings/Manager":
        // si ya hay una transición en marcha, no intentamos encolar otra — aplicamos el cut point
        // al instante, sin el fundido de pantalla (se pierde la suavidad visual en ese caso raro
        // de colisión, pero nunca nos quedamos colgados).
        if (settings == null || tm == null || tm.IsRunning)
        {
            onCutPoint?.Invoke();
            yield break;
        }

        bool done = false;

        UnityEngine.Events.UnityAction cutHandler = null;
        UnityEngine.Events.UnityAction endHandler = null;

        cutHandler = () =>
        {
            tm.onTransitionCutPointReached -= cutHandler;
            _activeTransitionCutHandler = null;
            onCutPoint?.Invoke();
        };
        endHandler = () =>
        {
            tm.onTransitionEnd -= endHandler;
            _activeTransitionEndHandler = null;
            done = true;
        };

        // FIX A7 (auditoría 2026-08-07): guardar referencia a nivel de instancia (ver OnDestroy).
        _activeTransitionCutHandler = cutHandler;
        _activeTransitionEndHandler = endHandler;
        tm.onTransitionCutPointReached += cutHandler;
        tm.onTransitionEnd += endHandler;
        tm.Transition(settings, 0f);

        yield return new WaitUntil(() => done);
    }

    // ── Telón ────────────────────────────────────────────────────────────────

    /// La clave con la que esta cinemática retiene el telón.
    protected string ClaveTelon => "cine:" + name;

    /// Si la cinemática recogió el telón al empezar, ¿lo suelta ella misma al llegar a su primer
    /// plano? Si no (lo normal en los sequencers escritos a mano), se suelta un fotograma después
    /// del cut point. SequencePlayer lo sobreescribe: sabe cuál es su primer plano.
    protected virtual bool SueltaElTelonEnSuPrimerPlano => false;

    private IEnumerator Co_SoltarTelonTrasUnFotograma()
    {
        yield return null;
        Telon.Soltar(ClaveTelon);
    }

    // ── Skip ("saltar cinemática") ───────────────────────────────────────────

    [Header("Skip")]
    [Tooltip("Duración del fundido a negro al saltar esta cinemática con el botón global de skip.")]
    [SerializeField] private float _skipFadeDuration = 0.25f;

    /// Solicita saltar la cinemática en curso de ESTA instancia. No hace nada si no tiene una
    /// secuencia activa ahora mismo o si ya se solicitó un skip para ella. Detiene Co_Sequence()
    /// en el punto exacto en que esté (StopCoroutine, sin depender de que el try/finally de
    /// Co_SequenceGuarded se ejecute) y cierra la cinemática de forma determinista vía
    /// Co_SkipToEnd(): limpieza propia de la subclase (OnSkipCleanup) + fundido a negro +
    /// EndCinematic()/RestoreMusic()/señal de salida — el mismo patrón que ya usan varios
    /// sequencers para su cierre normal (Co_EndCinematicStayBlack).
    public void RequestSkip()
    {
        if (!_sequenceRunning || _skipRequested) return;
        _skipRequested = true;

        if (_activeSequenceCoroutine != null)
            StopCoroutine(_activeSequenceCoroutine);
        _activeSequenceCoroutine = null;

        // Ver ClearTransitionHandlers: si la transición estaba a medias, sus handlers se quedarían
        // enganchados al TransitionManager persistente para el resto de la partida.
        ClearTransitionHandlers();

        _sequenceRunning = false;
        s_runningSequences.Remove(this);

        StartCoroutine(Co_SkipToEnd());
    }

    private IEnumerator Co_SkipToEnd()
    {
        yield return Co_EndCinematicStayBlack(() =>
        {
            try
            {
                OnSkipCleanup();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            RestoreMusic();
        }, _skipFadeDuration);

        string customSignal = SkipCompletionSignal;
        if (!string.IsNullOrEmpty(customSignal))
            RaiseSignal(customSignal);
        else
            RaiseSignalOut();

        _skipRequested = false;
    }

    /// Hook de limpieza específico de cada sequencer, llamado UNA VEZ con la pantalla ya cubierta
    /// de negro (dentro del fundido de Co_SkipToEnd), antes de EndCinematic(). Debe ser síncrono
    /// (no una corrutina): congelar/liberar NPCs, destruir overlays u otros VFX/objetos runtime,
    /// resetear Time.timeScale, parar corrutinas fire-and-forget propias (bucles, locks de
    /// rotación, pulsos...), etc. — todo lo que en el flujo normal solo se limpia al llegar al
    /// final natural de Co_Sequence() y que StopCoroutine() del punto medio NO limpia por sí solo.
    /// Cada sequencer con ese tipo de estado DEBE sobrescribir esto reutilizando su propia lógica
    /// de emergencia existente (Cleanup()/OnDestroy si ya la tiene) — por defecto no hace nada.
    protected virtual void OnSkipCleanup() { }

    /// Señal narrativa a levantar al saltar, en vez de RaiseSignalOut() (_signalOut heredado).
    /// Usar cuando la secuencia no emite _signalOut en su flujo normal (la del Despertar,
    /// que levanta señales propias de éxito/fallo) — sin este override el WaitCustomEventNode real
    /// del grafo nunca recibiría nada y el juego se quedaría bloqueado tras el skip. Vacío/null
    /// (por defecto) = usar RaiseSignalOut() normal.
    protected virtual string SkipCompletionSignal => null;

    // ── Música ────────────────────────────────────────────────────────────────

    /// Perfil de audio efectivo: el del Inspector de este sequencer si lo tiene, y si no el que ya
    /// usa el propio AudioService.
    ///
    /// FIX 16 sep 2026 (Raúl: "sigue sin sonar la música de la secuencia de Oliver"): _audioProfile
    /// es un campo de Inspector que hay que arrastrar a mano en CADA sequencer, aunque siempre
    /// acabe siendo el mismo asset global — el que AudioService ya tiene puesto y expone como
    /// público. Olvidarlo (o, como aquí, que se copie vacío al montar un componente nuevo) deja la
    /// música muda sin ningún error. No tiene sentido que una referencia global dependa de que
    /// alguien se acuerde: si el campo está vacío, se usa el del AudioService.
    protected AudioGraphProfile ResolveAudioProfile()
        => _audioProfile != null ? _audioProfile
         : (AudioService.Instance != null ? AudioService.Instance.profile : null);

    /// Reproduce la música de una regla de secuencia del AudioGraphProfile.
    ///
    /// Antes fallaba EN SILENCIO de tres formas distintas (sin perfil, sin regla con ese id, sin
    /// AudioService) y las tres se veían igual desde fuera: no suena nada. Ahora cada una lo dice.
    protected void PlaySequenceMusic(string sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId)) return;

        var profile = ResolveAudioProfile();
        if (profile == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: no hay AudioGraphProfile " +
                $"(ni en el Inspector de este componente ni en AudioService) — la música '{sequenceId}' " +
                "no puede sonar.");
#endif
            return;
        }

        var rule = profile.GetSequenceRule(sequenceId);
        if (rule?.music == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: el AudioGraphProfile " +
                $"'{profile.name}' no tiene ninguna regla de secuencia con id '{sequenceId}' (o la " +
                "tiene sin clip asignado). Revisa la lista de Sequence Rules del perfil.");
#endif
            return;
        }

        if (AudioService.Instance == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: AudioService.Instance es " +
                $"null — la música '{sequenceId}' no puede sonar. ¿Arrancaste desde Start.unity?");
#endif
            return;
        }

        AudioService.Instance.PlayMusic(rule.music, rule.fadeIn);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[CinematicSequencerBase] {GetType().Name}: música de secuencia '{sequenceId}' → " +
            $"'{rule.music.name}' (fadeIn {rule.fadeIn}s).");
#endif
    }

    protected void RestoreMusic()
    {
        if (AudioService.Instance == null) return;
        // Nunca en seco (INC-421): al acabar una cinemática la música del lugar entra fundida,
        // por lo menos dos segundos, aunque la regla pida menos.
        float fadeDur = Mathf.Max(MusicRule?.fadeOut ?? 0.8f, 2f);

        // Se pide, no se pone: si lo siguiente es un jefe u otra cinemática, su música entra
        // directamente sobre la de esta escena (ver AudioService.PedirMusicaDelLugar).
        AudioService.Instance.PedirMusicaDelLugar(fadeDur);
    }

    // ── Señales ───────────────────────────────────────────────────────────────

    protected void RaiseSignalOut() =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(ResolvedSignalOut);

    protected void RaiseSignal(string signal) =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signal);

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    [Header("Debug")]
    [Tooltip("Tecla que dispara 'Simular secuencia' en Play Mode — mismo efecto que el menú contextual " +
        "del componente (click derecho en la cabecera → 'Simular secuencia'), sin tener que entrar en el " +
        "Inspector cada vez. Key.None la desactiva (valor por defecto: no interfiere con ningún " +
        "sequencer existente salvo que se asigne una tecla a mano o desde un builder de escena).")]
    [SerializeField] private Key _simulateHotkey = Key.None;

    // Si alguna subclase necesita su propio Update(), debe declararlo como "protected override void
    // Update()" y llamar a "base.Update();" para no perder el atajo — de lo contrario el suyo oculta
    // (no sobrescribe) este por ausencia de "new"/"override", y el warning del compilador (CS0114) lo
    // avisaría en cuanto se intente.
    //
    // FIX (24 ago 2026): la primera versión usaba UnityEngine.Input.GetKeyDown(KeyCode) — el proyecto
    // tiene "Active Input Handling" puesto a "Input System Package" en Player Settings (todo el resto
    // del juego ya usa UnityEngine.InputSystem, ver PlayerInputManager/HoldToSkipUI), así que la clase
    // Input legacy está desactivada y cualquier lectura suya lanza InvalidOperationException en
    // tiempo real. Corregido leyendo el teclado del nuevo Input System directamente.
    //
    // FIX (24 ago 2026, mismo día): el campo pasó de KeyCode a Key JUSTO DESPUÉS de que el builder de
    // la escena de estudio ya lo hubiera rellenado con el KeyCode.F6 antiguo (entero 287) — Unity no
    // valida un enum al deserializarlo, así que ese 287 se quedó grabado en la escena tal cual, y no
    // es un miembro real de Key. Keyboard.current[key] con un Key fuera de rango lanza
    // ArgumentOutOfRangeException en vez de simplemente "tecla no encontrada", así que sin este guard
    // el atajo rompía Update() en cada frame. _simulateHotkeyValid cachea el resultado de
    // Enum.IsDefined la primera vez (evita repetir esa comprobación, que usa reflection, en cada
    // Update() — ver regla de no-Reflection-en-runtime-frecuente de CLAUDE.md); si el valor no es
    // válido, el atajo queda desactivado con un aviso en consola en vez de reventar. El builder
    // que monta la escena también se autocorrige en el próximo re-run (ver
    // SetInputKeyIfNoneOrInvalid).
    private bool? _simulateHotkeyValid;
    private bool _invalidHotkeyWarned;

    protected virtual void Update()
    {
        if (_simulateHotkey == Key.None) return;

        _simulateHotkeyValid ??= System.Enum.IsDefined(typeof(Key), _simulateHotkey);
        if (_simulateHotkeyValid != true)
        {
            if (!_invalidHotkeyWarned)
            {
                _invalidHotkeyWarned = true;
                Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: _simulateHotkey ({(int)_simulateHotkey}) " +
                    "no es un valor válido del enum Key — probablemente arrastrado de una versión antigua del campo " +
                    "(era KeyCode). Selecciona la tecla de nuevo a mano en el Inspector, o vuelve a ejecutar el " +
                    "builder de la escena si tiene uno. Atajo desactivado mientras tanto (aviso único, no se repite).");
            }
            return;
        }

        if (Keyboard.current != null && Keyboard.current[_simulateHotkey].wasPressedThisFrame)
            SimulateSequence();
    }

    [ContextMenu("Simular secuencia")]
    protected void SimulateSequence() =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(ResolvedSignalIn);
#endif
}
