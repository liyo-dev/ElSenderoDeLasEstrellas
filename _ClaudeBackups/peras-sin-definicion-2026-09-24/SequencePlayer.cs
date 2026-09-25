using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Reproduce una SequenceDefinition. Es el único sequencer que hace falta escribir: cualquier
/// escena nueva es un asset de datos + este mismo componente, no una clase C# nueva.
///
/// Todo lo que hoy se copia de un sequencer a otro (y se rompe por el camino) vive aquí una sola
/// vez: el candado que saca a los NPCs de su comportamiento ambiental, el cierre limpio con
/// ResetMovement (INC-210), la restauración de los ajustes del NavMeshAgent, y el camino de skip.
/// Arreglar un bug en este archivo lo arregla para todas las secuencias futuras — que es la razón
/// de existir del sistema.
///
/// MONTAJE EN EL EDITOR (lo único que no se puede hacer por escrito):
///   1. Un GameObject propio en la escena — NUNCA dentro del prefab de un NPC (ver
///      incidencia-oliver-saludo-secuencia-en-prefab-trigger-prematuro-2026-09-15.md).
///   2. Asignar el asset de la secuencia (_definition) y el SequenceStage (_stage).
///   3. Asignar el CinematicCameraDriver y, si la escena lleva música propia, el AudioGraphProfile
///      — los dos son campos heredados de CinematicSequencerBase.
///   4. Colocar y nombrar los planos de cámara y las marcas en el SequenceStage.
/// A partir de ahí, el contenido de la escena se edita en el asset, sin tocar la escena ni recompilar.
[AddComponentMenu("Cinematics/Sequence Player")]
public class SequencePlayer : CinematicSequencerBase
{
    [Header("Secuencia")]
    [Tooltip("El asset que describe la escena: fases, beats, señales y música.")]
    [SerializeField] private SequenceDefinition _definition;

    [Tooltip("El escenario: planos de cámara y marcas de posición. Si se deja vacío, se busca un " +
             "SequenceStage en este mismo GameObject.")]
    [SerializeField] private SequenceStage _stage;

    [Header("Cierre")]
    [Tooltip("Segundos que los NPCs de la escena siguen retenidos DESPUÉS de que la pantalla haya " +
             "vuelto al gameplay, antes de devolverles su comportamiento ambiental. Sin este " +
             "margen, un NPC puede echarse a andar 1-3 segundos después de terminar una " +
             "conversación personal (su IdleState sortea ese tiempo antes de pasar a Wander), lo " +
             "que queda raro aunque sea comportamiento normal.")]
    [SerializeField] private float _idleGraceAfterSequence = 3.5f;

    [Tooltip("Duración del fundido que retira el negro al saltar la secuencia con el botón global " +
             "de skip. El cierre genérico de skip de la clase base deja la pantalla cubierta a " +
             "propósito (pensado para secuencias cuyo sistema siguiente gestiona su propio reveal); " +
             "estas secuencias no tienen sistema siguiente, así que revelan ellas.")]
    [SerializeField] private float _skipRevealDuration = 0.25f;

#if UNITY_EDITOR
    [Header("Pruebas (solo Editor)")]
    [Tooltip("Nombre de una fase por la que arrancar, saltándose las anteriores. Vacío = desde el " +
             "principio. Sirve para probar el final de una escena larga sin verla entera cada vez.")]
    [SerializeField] private string _startAtPhase;
#endif

    private SequenceContext _context;

    // Cámara del juego, cacheada: los planos calculados en modo 'live' consultan el aspecto y
    // aplican una pose cada frame, y Camera.main hace una búsqueda por tag cada vez (CLAUDE.md § 2).
    private Camera _gameCamera;

    // Seguimiento de un plano que se recalcula cada frame (ShotBeat con 'live').
    private Coroutine _shotTracking;

    // Corrutinas de módulos lanzadas "de fondo" (ModuleBeat sin esperar). Se guardan para poder
    // pararlas al cerrar: una corrutina de mecánica que sobreviva a la secuencia sigue spawneando
    // cosas ya en pleno gameplay, que es el fallo que documenta Cleanup() del sequencer viejo.
    private readonly List<Coroutine> _backgroundRoutines = new();

    // Petición de terminar antes de tiempo (EndSequenceBeat), con la señal por la que salir.
    private bool _endRequested;
    private string _endSignalOverride;
    private SequenceEndScreen _endScreenOverride = SequenceEndScreen.ComoElAsset;

    // ¿Se ha levantado ya la señal de salida? Red de seguridad para el caso de que un beat reviente
    // a mitad: sin esto, una excepción devuelve el control al jugador pero deja al grafo narrativo
    // esperando un evento que no va a llegar nunca, y el capítulo no avanza sin ninguna pista.
    private bool _signalRaised;

    // Las señales vienen del asset, no del Inspector: así una secuencia entera (contenido y
    // enganche con el grafo) se puede escribir como texto. Si el asset las deja vacías, se usan
    // las del Inspector heredadas de CinematicSequencerBase.
    /// Si una rama ya había decidido por dónde salir (un EndSequenceBeat con señal propia), saltar
    /// la secuencia debe salir por ahí y no por la señal general del asset. Si no, fallar el panic
    /// input y saltar acto seguido levantaría AWAKEN_DONE y desbloquearía la magia como si se
    /// hubiera acertado.
    protected override string SkipCompletionSignal => _endSignalOverride;

    protected override string SignalInOverride => _definition != null ? _definition.signalIn : null;
    protected override string SignalOutOverride => _definition != null ? _definition.signalOut : null;

    /// La cámara cinemática de esta secuencia. Se resuelve en Awake (Inspector, o la del
    /// SequenceStage si el campo del Inspector está vacío) para que el corte de apertura que hace
    /// la clase base durante el fundido use también la correcta.
    public CinematicCameraDriver ActiveCamera => _cinematicCamera;

    /// La cámara que está pintando, cacheada.
    public Camera CachedCamera
    {
        get
        {
            if (_gameCamera == null) _gameCamera = Camera.main;
            return _gameCamera;
        }
    }

    /// Relación de aspecto de la pantalla. La necesita ShotComposer para saber cuánto cabe a lo
    /// ancho: un two-shot bien encuadrado en 16:9 se sale de cuadro en 4:3.
    public float CameraAspect
    {
        get
        {
            var cam = CachedCamera;
            return cam != null && cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
        }
    }

    /// El plano que está en pantalla ahora mismo, para poder ajustarlo mientras se ve. Lo pone
    /// cada ShotBeat al aplicarse, y lo lee el panel de ajuste del Inspector.
    ///
    /// Se guarda el ShotFraming del asset, no una copia: así mover un deslizador en el Inspector
    /// cambia el plano de verdad, y el valor se queda guardado en el asset al salir del Play —
    /// que es lo que hace que afinar un plano deje de ser un ciclo de recompilar.
    public ShotFraming CurrentFraming { get; private set; }

    /// Nombre legible del plano actual, para el panel de ajuste.
    public string CurrentShotLabel { get; private set; }

    /// Lo llama ShotBeat al aplicar un plano calculado.
    public void NotifyCurrentShot(ShotFraming framing, string label)
    {
        CurrentFraming = framing;
        CurrentShotLabel = label;
    }

    /// Vuelve a aplicar el plano actual. Lo usa el panel de ajuste al mover un deslizador, para
    /// que el cambio se vea sin esperar al siguiente corte.
    public void RefreshCurrentShot()
    {
        if (CurrentFraming == null || _context == null) return;
        var driver = ActiveCamera;
        if (driver == null) return;

        if (ShotComposer.TrySolve(_context, CurrentFraming, CameraAspect, out ShotSolution s, isCut: false))
            driver.SetPose(s.position, s.rotation, s.fieldOfView);
    }

    /// ¿Está puesto el modo de ajuste que refresca todos los planos cada frame? Ver
    /// SequenceStage.LivePreview.
    public bool LivePreviewEnabled => _stage != null && _stage.LivePreview;

    /// La secuencia que reproduce este player y el escenario con el que la resuelve. Son de solo
    /// lectura y existen para las herramientas de Editor (ver SequenceShotCapture), que necesitan
    /// recorrer los beats y resolver los planos sin entrar en Play.
    public SequenceDefinition Definition => _definition;
    public SequenceStage Stage => _stage;

    /// Empieza a recalcular un plano cada frame, para seguir a alguien que se mueve. Lo llama
    /// ShotBeat cuando tiene marcado 'live'.
    public void StartShotTracking(ShotFraming framing)
    {
        StopShotTracking();
        if (framing == null || _context == null) return;
        _shotTracking = StartCoroutine(Co_TrackShot(framing));
    }

    /// Deja de seguir. Lo llama cualquier beat de cámara nuevo y también el cierre de la secuencia.
    public void StopShotTracking()
    {
        if (_shotTracking == null) return;
        StopCoroutine(_shotTracking);
        _shotTracking = null;
    }

    /// Segundos que tarda la cámara de un plano vivo en alcanzar la pose que le toca. Lo bastante
    /// corto para no perder a alguien que corre; lo bastante largo para que un cambio de ángulo
    /// sea un movimiento y no un salto.
    private const float SuavizadoDelPlanoVivo = 0.35f;

    /// Rapidez del giro de la cámara en un plano vivo (1/s). Más alto que la posición: lo que no
    /// puede pasar es que el sujeto se salga del cuadro mientras la cámara se recoloca.
    private const float GiroDelPlanoVivo = 9f;

    /// Metros de diferencia a partir de los cuales una pose nueva NO es el mismo plano refrescado,
    /// sino otro sitio: el buscador ha rodeado un obstáculo o se ha ido al lado contrario.
    private const float SaltoDelPlanoVivo = 2.5f;

    /// Segundos que tiene que MANTENERSE esa pose nueva para aceptarla. Menos de esto y es un
    /// parpadeo —una esquina que entra y sale del rayo de visión— y se ignora: «los planos aéreos
    /// hacen en momentos concretos cambios rápidos que parecen fallos» (23 sep).
    private const float AguantarAntesDeSaltar = 0.6f;

    private IEnumerator Co_TrackShot(ShotFraming framing)
    {
        var driver = ActiveCamera;
        if (driver == null) yield break;

        int fallosSeguidos = 0;

        // (21 sep) El plano vivo se SUAVIZA. Antes se aplicaba la pose calculada tal cual cada
        // fotograma, y cuando el buscador cambiaba de ángulo porque una casa se metía en medio
        // (en un camino entre casas pasa constantemente), la cámara saltaba de un sitio a otro de
        // un fotograma al siguiente: nueve saltos seguidos en la vuelta al pueblo tras el trueno,
        // «la cámara se vuelve loca». Ahora la cámara VA hacia la pose que toca en vez de
        // teletransportarse a ella: un cambio de ángulo se ve como un movimiento, no como un tic.
        Vector3 velocidad = Vector3.zero;

        // Histéresis (23 sep): la pose que se persigue solo cambia de sitio cuando el cambio se
        // mantiene. Si no, un obstáculo que entra y sale del rayo de visión hace ir y venir a la
        // cámara varias veces por segundo, y eso se lee como un fallo, no como un plano.
        bool hayObjetivo = false;
        Vector3 objetivoPos = Vector3.zero;
        Quaternion objetivoRot = Quaternion.identity;
        float objetivoFov = 0f;
        float discrepando = 0f;

        while (true)
        {
            // El driver puede desaparecer si su escena se descarga a mitad de secuencia.
            if (driver == null) yield break;

            // SetPose en vez de Cut: aplicar la pose sin cancelar tweens ni seguimientos, porque
            // el que está al mando de la cámara en este momento es este bucle.
            // isCut: false — esto es el mismo plano refrescándose, no un corte nuevo.
            if (ShotComposer.TrySolve(_context, framing, CameraAspect, out ShotSolution s, isCut: false))
            {
                float dt = Time.unscaledDeltaTime;

                if (!hayObjetivo)
                {
                    objetivoPos = s.position; objetivoRot = s.rotation; objetivoFov = s.fieldOfView;
                    hayObjetivo = true;
                    discrepando = 0f;
                }
                else if (Vector3.Distance(s.position, objetivoPos) > SaltoDelPlanoVivo)
                {
                    // Sitio distinto: solo se acepta si se mantiene. Un parpadeo no mueve la cámara.
                    discrepando += dt;
                    if (discrepando >= AguantarAntesDeSaltar)
                    {
                        objetivoPos = s.position; objetivoRot = s.rotation; objetivoFov = s.fieldOfView;
                        discrepando = 0f;
                    }
                }
                else
                {
                    // El mismo sitio, corregido: se sigue sin más.
                    objetivoPos = s.position; objetivoRot = s.rotation; objetivoFov = s.fieldOfView;
                    discrepando = 0f;
                }

                Vector3 pos = Vector3.SmoothDamp(driver.CurrentPosition, objetivoPos, ref velocidad,
                    SuavizadoDelPlanoVivo, Mathf.Infinity, dt);
                Quaternion rot = Quaternion.Slerp(driver.CurrentRotation, objetivoRot,
                    1f - Mathf.Exp(-GiroDelPlanoVivo * dt));
                driver.SetPose(pos, rot, objetivoFov);
                fallosSeguidos = 0;
            }
            else if (++fallosSeguidos >= 10)
            {
                // El sujeto ha dejado de existir (un proyectil que ya explotó, un NPC despawneado).
                // Insistir solo sirve para repetir el mismo aviso sesenta veces por segundo.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SequencePlayer:{name}] Se deja de seguir el plano " +
                    $"'{framing.Describe()}': su sujeto ya no existe. La cámara se queda donde está.");
#endif
                yield break;
            }

            yield return null;
        }
    }

    // Limpiezas que un beat deja pedidas y que hay que ejecutar sí o sí al cerrar. Existe porque
    // el try/finally de un beat NO es de fiar cuando la secuencia se salta: StopCoroutine sobre la
    // corrutina principal no ejecuta los finally de las anidadas, algo que este proyecto ya
    // documenta en tres sitios. Lo usa DialogueBeat para desuscribirse de un evento ESTÁTICO, que
    // olvidado se queda disparando el resto de la partida.
    private readonly List<System.Action> _pendingCleanups = new();

    public void RegisterCleanup(System.Action cleanup)
    {
        if (cleanup != null) _pendingCleanups.Add(cleanup);
    }

    private void RunPendingCleanups()
    {
        for (int i = 0; i < _pendingCleanups.Count; i++)
        {
            try { _pendingCleanups[i]?.Invoke(); }
            catch (System.Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SequencePlayer:{name}] Una limpieza pendiente ha fallado: {ex.Message}");
#endif
            }
        }
        _pendingCleanups.Clear();
    }

    /// Registra una corrutina de módulo lanzada de fondo, para poder pararla al cerrar.
    public void TrackBackgroundRoutine(Coroutine routine)
    {
        if (routine != null) _backgroundRoutines.Add(routine);
    }

    /// Pide terminar la secuencia en cuanto acabe el beat actual, saltándose las fases que queden.
    /// Con una señal distinta a la del asset, si la rama que se está siguiendo sale por otro sitio.
    public void RequestEnd(string signalOverride = null,
        SequenceEndScreen screen = SequenceEndScreen.ComoElAsset)
    {
        _endRequested = true;
        if (!string.IsNullOrWhiteSpace(signalOverride)) _endSignalOverride = signalOverride.Trim();
        if (screen != SequenceEndScreen.ComoElAsset) _endScreenOverride = screen;
    }

    /// Cambia la música a mitad de secuencia. Usado por MusicBeat.
    public void PlayMusic(string musicId) => PlaySequenceMusic(musicId);

    /// Para la música. Usado por MusicBeat: hace falta para los silencios dramáticos y para dejar
    /// paso limpio a lo que venga detrás (la intro de un jefe, por ejemplo).
    public void StopMusicNow(float fadeOut)
    {
        if (AudioService.Instance != null) AudioService.Instance.StopMusic(Mathf.Max(0f, fadeOut));
    }

    /// Corta a un plano por su nombre en el SequenceStage. Usado por CutBeat.
    public void CutTo(string shotName)
    {
        var shot = _stage != null ? _stage.GetShot(shotName) : null;
        if (shot == null) return; // el aviso ya lo ha dado el stage
        ActiveCamera?.Cut(shot);
    }

    /// Levanta una señal narrativa a mitad de secuencia. Usado por SignalBeat.
    public void Raise(string signal) => RaiseSignal(signal);

    protected override void Awake()
    {
        if (_stage == null) _stage = GetComponent<SequenceStage>();

        if (_stage != null)
        {
            // La escena de la cinematica pasa a ser la ACTIVA mientras dura. Unity coge el skybox, la
            // niebla y la luz ambiente de la escena activa, y el prologo se carga en aditivo mientras la
            // activa sigue siendo la del jugador -- durante el sueno, la habitacion de Will, que es un
            // interior sin cielo. De ahi el cielo negro del valle. Se devuelve sola al terminar.
            CinematicTimeOfDay.ActivarEscena(_stage.gameObject.scene);
        }

        // _cinematicCamera es un campo protegido heredado de CinematicSequencerBase: si no se
        // asignó en el Inspector, se toma el del SequenceStage. Debe quedar resuelto ANTES de
        // base.Awake(), porque el corte de apertura durante el fundido lo usa directamente.
        if (_cinematicCamera == null && _stage != null && _stage.CameraDriver != null)
            _cinematicCamera = _stage.CameraDriver;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_cinematicCamera == null)
            Debug.LogWarning($"[SequencePlayer:{name}] Sin CinematicCameraDriver (ni en el Inspector " +
                "ni en el SequenceStage) — ningún corte de cámara de esta secuencia hará nada.");

        // FIX (17 sep 2026): el driver mueve la cámara con corrutinas suyas (MoveTo, StartFollowing),
        // y StartCoroutine falla si su GameObject está apagado en la jerarquía. El síntoma es
        // traicionero: los cortes secos funcionan, así que la secuencia parece ir bien y sólo
        // desaparecen los planos con movimiento, sin un solo error por consola. Pasó de verdad, con
        // el driver que se quedó en el GameObject del sequencer viejo al migrar el Despertar.
        else if (!_cinematicCamera.gameObject.activeInHierarchy)
            Debug.LogError($"[SequencePlayer:{name}] El CinematicCameraDriver vive en " +
                $"'{_cinematicCamera.gameObject.name}', que está APAGADO en la jerarquía. Los cortes " +
                "secos van a funcionar, pero todo plano con movimiento o con seguimiento va a fallar " +
                "en silencio. Mueve el componente a este GameObject o enciende el suyo.",
                _cinematicCamera.gameObject);
#endif

        base.Awake();
    }

    /// El telón lo suelta el propio bucle de beats, en el primer plano. Ver Co_Play.
    protected override bool SueltaElTelonEnSuPrimerPlano => true;

    private static bool EsDeCamara(SequenceBeat beat)
        => beat is ShotBeat || beat is CutBeat || beat is MoveCameraBeat;

    /// Beats que solo colocan: son instantáneos y no se ve nada de lo que hacen hasta que haya un
    /// plano. Con el telón cerrado se ejecutan a oscuras. Cualquier otro beat destapa antes de
    /// correr — si hay duda, mejor destapar que dejar la pantalla negra mientras pasan cosas.
    private static bool EsPreparacion(SequenceBeat beat)
    {
        switch (beat)
        {
            case PlaceAtMarkBeat _:
            case SetActionAxisBeat _:
            case ResetActionAxisBeat _:
            case SetPropActiveBeat _:
            case EmotionBeat _:
            case SetFlagBeat _:
            case MusicBeat _:
            case PoseBeat _:
            case HoldActorBeat _:
            case StopTrackingBeat _:
                return true;
            case PropMoveBeat m:
                return m.segundos <= 0.001f || !m.esperar;
            case FaceBeat f:
                return f.turnDuration <= 0.001f;
            case TimeOfDayBeat h:
                return !h.waitForTransition;
            default:
                return false;
        }
    }

    protected override IEnumerator Co_Sequence()
    {
        if (_definition == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SequencePlayer:{name}] Sin SequenceDefinition asignada — no hay nada " +
                "que reproducir. La señal de salida se levanta igualmente para no dejar el grafo " +
                "narrativo esperando para siempre.");
#endif
            RaiseSignalOut();
            yield break;
        }

        _context = new SequenceContext(this, _stage);

        // Objetos del decorado que esta escena quiere poder encuadrar (el horno, la carreta, el
        // portón). Se registran aquí, antes del primer beat, para que un ShotBeat pueda nombrarlos
        // igual que a un personaje. Ver SequenceStage.Props.
        if (_stage != null && _stage.Props != null)
        {
            foreach (var prop in _stage.Props)
            {
                if (prop.target == null || string.IsNullOrWhiteSpace(prop.id)) continue;
                _context.RegisterActor(prop.id.Trim(), prop.target, prop.eyeHeight);
            }
        }

        // Que se vea el cielo. Va AQUI y no en Awake: entre que el SequencePlayer despierta y que
        // la secuencia arranca, el jugador puede haber entrado en una casa -- que es justo lo que
        // pasa en el prologo, donde Will se va a dormir despues. Ver CinematicTimeOfDay.MostrarExterior.
        CinematicTimeOfDay.MostrarExterior(CachedCamera);

        // La caché de colliders de ShotComposer es estática: se vacía al empezar cada secuencia.
        ShotComposer.ClearColliderCache();

        // FIX (auditoría 17 sep 2026) — ESTO BLOQUEABA EL CAPÍTULO 1.
        //
        // Estos campos los pone EndSequenceBeat para cerrar por una rama concreta, y no se
        // reseteaban nunca. El Despertar de la Estrella se relanza a sí mismo cuando el jugador
        // falla el panic input (en el grafo, AWAKEN_FAILED vuelve a AWAKEN_START), así que en el
        // segundo intento _endRequested seguía en true: la secuencia hacía la transición de
        // entrada, NO ejecutaba un solo beat, y volvía a levantar AWAKEN_FAILED. El grafo la
        // relanzaba otra vez. Bucle infinito de fundidos con el input bloqueado, sin más salida
        // que cerrar el juego — y fallar esa prueba la primera vez que se juega es lo normal.
        _endRequested = false;
        _endSignalOverride = null;
        _endScreenOverride = SequenceEndScreen.ComoElAsset;
        _signalRaised = false;
        CurrentFraming = null;
        CurrentShotLabel = null;

        try
        {
            yield return Co_Play();
        }
        finally
        {
            // Red de seguridad: si la corrutina termina de forma anómala (excepción, destrucción
            // del objeto a mitad), los NPCs no se quedan atrapados en CinematicState para siempre
            // ni con el NavMeshAgent tocado. Mismo patrón que Co_SequenceGuarded de la clase base.
            ReleaseAllActors();

            // Y el grafo narrativo no se queda esperando. Mismo criterio que el caso de "sin
            // definición asignada" de más arriba: es preferible avanzar la historia que dejar el
            // capítulo colgado sin que nadie sepa por qué.
            if (!_signalRaised)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SequencePlayer:{name}] La secuencia terminó de forma anómala " +
                    "(lo normal es que un beat haya lanzado una excepción; mírala más arriba en la " +
                    "consola). Se levanta la señal de salida igualmente para no dejar el grafo " +
                    "narrativo esperando para siempre.");
#endif
                RaiseSignalOut();
            }
        }
    }

    private IEnumerator Co_Play()
    {
// La música de la secuencia se lanza en el CUT POINT de la transición de entrada: con la
        // pantalla ya cubierta, y -- esto es lo importante -- después de que LockCinematic() haya
        // puesto AnySequenceActive a true.
        //
        // Historia de este punto, que se ha movido dos veces:
        //   1. Al principio iba DESPUÉS de la transición. Síntoma (Raúl): "cuando salimos de la
        //      habitación hay un segundo que suena la música del mundo y luego la de la secuencia".
        //      Causa: al cruzar la puerta, EnvironmentController levanta OnInteriorExited y
        //      AudioService.HandleInteriorExited() pone la música de mundo/zona; nuestra música no
        //      entraba hasta bastante después.
        //   2. Se movió a ANTES de la transición, para adelantarla. Eso lo empeoró: pasó a no
        //      sonar nada. Motivo: AnySequenceActive solo se pone a true dentro de LockCinematic(),
        //      que es lo primero que hace Co_BeginCinematicWithTransition -- así que poner la
        //      música ANTES la dejaba en la ventana en la que la cinemática todavía no consta como
        //      activa, y el HandleInteriorExited del portal la pisaba con la música de mundo. O
        //      sea: antes se oía un segundo de mundo y luego la nuestra; después, solo la de mundo.
        //   3. Aquí. El candado se echa al entrar en Co_BeginCinematicWithTransition (primer frame
        //      de la secuencia, guard de AudioService ya activo) y la música arranca en el cut
        //      point, con la pantalla cubierta. Ni hueco audible ni nadie que la pise.
        // El plano de apertura puede venir colocado a mano (por nombre) o descrito para que lo
        // calcule el solver. En los dos casos se aplica en el CUT POINT, con la pantalla ya
        // cubierta, para que la escena aparezca encuadrada en vez de verse el corte.
        Transform opening = _stage != null && !string.IsNullOrWhiteSpace(_definition.openingShotName)
            ? _stage.GetShot(_definition.openingShotName)
            : null;

        yield return Co_BeginCinematicWithTransition(opening, () =>
        {
            if (opening == null) ApplyOpeningShotIfDescribed();

            if (!string.IsNullOrEmpty(_definition.musicId))
                PlaySequenceMusic(_definition.musicId);
        });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Comprobación tardía: si la música arrancó pero alguien la pisó justo después (el portal,
        // una AmbientZone, una batalla...), esto lo dice en vez de dejarnos adivinando. Es
        // fire-and-forget a propósito: no debe retrasar la secuencia.
        if (!string.IsNullOrEmpty(_definition.musicId))
            StartCoroutine(Co_CheckMusicStuck(_definition.musicId));
#endif

        int startPhase = 0;
#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(_startAtPhase))
        {
            int idx = _definition.IndexOfPhase(_startAtPhase);
            if (idx >= 0)
            {
                startPhase = idx;
                Debug.LogWarning($"[SequencePlayer:{name}] Arrancando en la fase '{_definition.phases[idx].name}' " +
                    "(campo de pruebas '_startAtPhase'). Acuérdate de vaciarlo antes de dar la escena por buena.");
            }
            else
            {
                Debug.LogWarning($"[SequencePlayer:{name}] No hay ninguna fase llamada '{_startAtPhase}' " +
                    "— se arranca desde el principio.");
            }
        }
#endif

        for (int p = startPhase; p < _definition.phases.Count; p++)
        {
            var phase = _definition.phases[p];
            if (phase?.beats == null) continue;

            // Fases condicionales: es como una secuencia con más de un final sigue siendo una
            // lista ordenada que se lee de arriba abajo, en vez de un grafo dentro de otro grafo.
            if (!phase.ShouldRun(_context))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SequencePlayer:{name}] Fase {p + 1}/{_definition.phases.Count}: " +
                    $"'{phase.name}' SE SALTA porque {phase.DescribeSkipReason(_context)}.");
#endif
                continue;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SequencePlayer:{name}] Fase {p + 1}/{_definition.phases.Count}: '{phase.name}' " +
                $"({phase.beats.Count} beats).");
#endif

            for (int b = 0; b < phase.beats.Count; b++)
            {
                if (_endRequested) break;
                var beat = phase.beats[b];
                if (beat == null) continue;

                // EL TELÓN (21 sep). Si esta secuencia empezó con la pantalla en negro, se queda en
                // negro mientras los beats solo COLOCAN cosas (poner a alguien en su marca, volcar
                // la carreta, la hora del día...), y se destapa con lo primero que se VE: su primer
                // plano, o el primer beat que tarde algo. Antes se destapaba al acabar la transición
                // de entrada, con el Mago Oscuro todavía en mitad de la plaza y la carreta sin volcar.
                if (Telon.Retiene(ClaveTelon) && !EsPreparacion(beat))
                {
                    if (EsDeCamara(beat))
                    {
                        // Se deja aplicar el corte un fotograma y entonces se destapa: la pantalla
                        // se abre ya encuadrada. Si el plano tiene movimiento, se ve moverse.
                        var corte = StartCoroutine(beat.Run(_context));
                        yield return null;
                        Telon.Soltar(ClaveTelon);
                        yield return corte;
                        continue;
                    }
                    Telon.Soltar(ClaveTelon);
                }

                yield return beat.Run(_context);
            }

            if (_endRequested)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SequencePlayer:{name}] La secuencia termina en la fase '{phase.name}': " +
                    "un beat ha pedido cerrar antes de tiempo.");
#endif
                break;
            }
        }

        // La cámara deja de seguir a nadie, y la mecánica lanzada de fondo se para: a partir de
        // aquí manda otra vez el juego.
        StopShotTracking();
        StopBackgroundRoutines();

        // Los módulos se cierran AQUÍ, no en ReleaseAllActors. Aquel corre después del margen de
        // gracia de los NPCs (3,5 s por defecto), y ese margen existe para que un NPC no eche a
        // andar nada más acabar la escena — no tiene nada que ver con la mecánica. Cerrarlos tarde
        // dejaba al módulo del Despertar con el lanzador de hechizos del jugador APAGADO durante
        // tres segundos y medio de juego real.
        if (_stage != null) _stage.CleanupModules();
        CinematicTimeOfDay.Restore();

        // Red de seguridad del tiempo: si la escena iba en cámara lenta y su beat de vuelta no se
        // llegó a ejecutar (una rama que salió antes, un beat mal puesto), la partida se quedaría
        // al 20% de velocidad para siempre. Es un fallo caro y silencioso, así que no se confía en
        // que el asset se acuerde.
        Time.timeScale = 1f;

        // Dejar a todos los actores parados y en Idle antes de devolver el control a la FSM.
        foreach (var actor in _context.ResolvedActors)
        {
            actor.EndAgentOverride();
            actor.StopMovement();
        }

        bool stayBlack = _endScreenOverride switch
        {
            SequenceEndScreen.Revelar => false,
            SequenceEndScreen.QuedarseEnNegro => true,
            _ => _definition.endStayBlack,
        };

        if (stayBlack) yield return Co_EndCinematicStayBlack(RestoreMusic);
        else yield return Co_EndCinematicWithTransition(RestoreMusic);

        // La señal de salida va EN CUANTO la pantalla vuelve al gameplay (INC-359). Antes iba
        // detrás de la gracia de 3,5 s de abajo, y todo lo que el grafo hace al acabar una escena
        // —el cartel de «Nueva misión», el tutorial, que Oliver se una al grupo— llegaba tres
        // segundos y medio tarde: «el pop-up de nueva misión tarda muchísimo en salir».
        // Una secuencia con dos finales sale por la señal de su rama, no por la del asset.
        _signalRaised = true;
        if (!string.IsNullOrEmpty(_endSignalOverride)) Raise(_endSignalOverride);
        else RaiseSignalOut();

        // Los NPCs siguen retenidos un poco más, con la pantalla ya de vuelta al gameplay, para que
        // no se les vea echarse a andar justo al terminar la escena. Esto ya no retrasa al grafo.
        if (_idleGraceAfterSequence > 0f)
            yield return new WaitForSeconds(_idleGraceAfterSequence);

        ReleaseAllActors();
    }

    /// Resuelve y aplica el plano de apertura descrito en el asset. Se llama desde el cut point de
    /// la transición de entrada, así que el corte no se ve.
    private void ApplyOpeningShotIfDescribed()
    {
        var framing = _definition != null ? _definition.openingShot : null;
        if (framing == null || string.IsNullOrWhiteSpace(framing.subjectId)) return;

        var driver = ActiveCamera;
        if (driver == null) return;

        if (ShotComposer.TrySolve(_context, framing, CameraAspect, out ShotSolution s))
            driver.Cut(s.position, s.rotation, s.fieldOfView);
    }

    private void StopBackgroundRoutines()
    {
        for (int i = 0; i < _backgroundRoutines.Count; i++)
            if (_backgroundRoutines[i] != null) StopCoroutine(_backgroundRoutines[i]);
        _backgroundRoutines.Clear();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// Mira, un par de segundos después de arrancar, qué música está sonando de verdad. Si no es la
    /// de la secuencia, alguien la ha pisado — y el aviso dice cuál suena, que es justo el dato que
    /// hace falta para saber quién.
    private IEnumerator Co_CheckMusicStuck(string musicId)
    {
        yield return new WaitForSeconds(2f);

        if (AudioService.Instance == null) yield break;

        var esperada = ResolveAudioProfile()?.GetSequenceRule(musicId)?.music;
        var sonando = AudioService.Instance.CurrentMusicClip;

        if (esperada == null) yield break;

        if (sonando != esperada)
            Debug.LogWarning($"[SequencePlayer:{name}] La música de la secuencia ('{esperada.name}') " +
                $"NO es la que está sonando dos segundos después: suena " +
                $"'{(sonando != null ? sonando.name : "nada")}'. Alguien la ha pisado — mira qué otro " +
                "sistema tocó la música en esa ventana (portal de interior, AmbientZone, batalla).");
        else
            Debug.Log($"[SequencePlayer:{name}] Música de la secuencia sonando correctamente: '{esperada.name}'.");
    }
#endif

    /// Suelta a todos los actores que la secuencia haya tocado: restaura los ajustes de su
    /// NavMeshAgent, los deja parados en Idle y les devuelve su comportamiento ambiental.
    /// Idempotente — se llama desde el final normal, desde el finally de Co_Sequence y desde el
    /// camino de skip.
    private void ReleaseAllActors()
    {
        // Todo lo de aquí es idempotente y se llama desde los tres caminos de cierre (final
        // normal, finally de seguridad y skip), porque ninguno de los tres puede dar por hecho que
        // los otros hayan pasado.
        StopShotTracking();
        StopBackgroundRoutines();
        RunPendingCleanups();
        Time.timeScale = 1f;
        if (_stage != null) _stage.CleanupModules();
        CinematicTimeOfDay.Restore();

        if (_context == null) return;
        foreach (var actor in _context.ResolvedActors)
        {
            actor.EndAgentOverride();
            actor.StopMovement();
            // Cierra cualquier animación de cuerpo entero que se haya quedado puesta — ver
            // PlayerDialogueAnimator.ReturnToLocomotion (el caso de Dizzy_NoWeapon en Will).
            actor.ReturnToNormalPose();
            actor.Release();
        }
    }

    /// Ver CinematicSequencerBase.OnSkipCleanup: StopCoroutine no garantiza que se ejecute el
    /// try/finally de las corrutinas anidadas, así que todo lo que hay que limpiar siempre se
    /// repite aquí a mano.
    protected override void OnSkipCleanup()
    {
        // Un DialogueBeat no es una corrutina nuestra: el DialogueManager tiene su propio panel, su
        // propio modo de input y su propia cámara. Sin esto, saltar durante el diálogo del tutorial
        // dejaba la caja de diálogo encima del gameplay y la cámara congelada en el encuadre de la
        // conversación — o sea, saltar la escena no la saltaba.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
            DialogueManager.Instance.Close();

        ReleaseAllActors();

        // El cierre genérico de skip deja la pantalla cubierta a propósito. Estas secuencias no
        // tienen ningún sistema siguiente que la revele, así que la revelan ellas — si no, saltar
        // una secuencia deja la pantalla en negro para siempre (INC-208).
        StartCoroutine(Co_RevealAfterSkip());
    }

    private IEnumerator Co_RevealAfterSkip()
    {
        yield return Sendero.Core.Feedback.FeedbackService.ScreenFadeAsync(
            Color.black, _skipRevealDuration, fadeIn: false);
    }
}
