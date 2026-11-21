using Invector.vCharacterController;
using UnityEngine;
using Liyodev.NarrativeGraph;

/// <summary>
/// Auto-guardado de El Sendero: une el aviso del grafo «paso seguro alcanzado»
/// (<see cref="NarrativeRunner.SafeStepReached"/>) con la partida (<see cref="Partida.Guardar"/>).
///
/// Al llegar un aviso queda un guardado pendiente, que se hace en cuanto se cumple todo: hay
/// auto-guardados (<see cref="GameBootProfile.allowAutoSaves"/>), no es una partida de pruebas,
/// han pasado <see cref="GameBootProfile.segundosEntreAutoguardados"/> desde el último guardado o
/// desde la carga, <see cref="Partida.PuedeGuardarAhora()"/>, todos los grafos están en pasos
/// seguros, no hay diálogo, pregunta ni menú abiertos, y el jugador está vivo, en el suelo, sin
/// nadar, volar ni llevar nada en brazos. Todo eso tiene que cumplirse seguido durante
/// <see cref="SegundosSeguidos"/>: un paso que espera la señal de fin de una escena que otro paso
/// acaba de lanzar («LA DISCUSIÓN SE RESUELVE» → «Espera PERAS_DONE») es seguro solo cuando nada
/// está en marcha; si se guardara justo antes de que la escena arranque, al cargar se esperaría
/// una señal que ya nadie va a dar (INC-1302). Mientras guarda se ve el símbolo de guardado de
/// la esquina (<see cref="IndicadorDeGuardado"/>), sin rótulo.
/// El farol de estrellas sigue siendo el guardado manual.
/// </summary>
public sealed class AutoguardadoDelJuego : MonoBehaviour
{
    const float SegundosEntreComprobaciones = 0.5f;
    const float SegundosSeguidos = 3f;

    // Fases del juego en las que no se auto-guarda (menús, diálogo, cinemática, carga…).
    static readonly GamePhase[] s_fasesQueImpiden =
    {
        GamePhase.PauseMenu, GamePhase.MainMenu, GamePhase.Dialogue, GamePhase.SavePrompt,
        GamePhase.Inventory, GamePhase.Equipment, GamePhase.QuestMenu, GamePhase.Map,
        GamePhase.Shop, GamePhase.Cutscene, GamePhase.Loading, GamePhase.GameOver
    };

    static AutoguardadoDelJuego s_instancia;

    bool _pendiente;
    float _siguienteComprobacion;
    float _tranquiloDesde = -1f;
    float _ultimoGuardado;

    GameObject _jugador;
    vThirdPersonMotor _motor;
    PlayerHealthSystem _salud;
    PlayerSwimmingController _nado;
    PlayerFlyingController _vuelo;
    PlayerCarrySystem _carga;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_instancia = null;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Crear()
    {
        if (s_instancia != null) return;
        var go = new GameObject("[Auto-guardado]");
        DontDestroyOnLoad(go);
        s_instancia = go.AddComponent<AutoguardadoDelJuego>();
    }

    void OnEnable()
    {
        NarrativeRunner.SafeStepReached += AlLlegarAUnPasoSeguro;
        GameBootService.OnProfileReady += AlCargarLaPartida;
        _ultimoGuardado = Time.unscaledTime;
    }

    void OnDisable()
    {
        NarrativeRunner.SafeStepReached -= AlLlegarAUnPasoSeguro;
        GameBootService.OnProfileReady -= AlCargarLaPartida;
        _pendiente = false;
    }

    void OnDestroy()
    {
        if (s_instancia == this) s_instancia = null;
    }

    void AlLlegarAUnPasoSeguro(NarrativeRunner runner, NarrativeNode paso)
    {
        // La historia acaba de moverse: la cuenta de tranquilidad vuelve a empezar.
        _tranquiloDesde = -1f;
        if (HayAutoguardados()) _pendiente = true;
    }

    // Recién cargada (o empezada) una partida no se guarda: los grafos se reanudan en pasos
    // seguros y lo guardado es justo lo que se acaba de cargar.
    void AlCargarLaPartida()
    {
        _pendiente = false;
        _tranquiloDesde = -1f;
        _ultimoGuardado = Time.unscaledTime;
    }

    void Update()
    {
        if (!_pendiente) return;
        float ahora = Time.unscaledTime;
        if (ahora < _siguienteComprobacion) return;
        _siguienteComprobacion = ahora + SegundosEntreComprobaciones;

        if (!HayAutoguardados()) { _pendiente = false; return; }
        var impide = QueImpide();
        if (impide != null)
        {
            _tranquiloDesde = -1f;
            Diagnosticar(impide, ahora);
            return;
        }
        if (_tranquiloDesde < 0f) _tranquiloDesde = ahora;
        if (ahora - _tranquiloDesde < SegundosSeguidos) return;
        if (ahora - _ultimoGuardado < GameBootService.Profile.segundosEntreAutoguardados) return;

        var guardado = GameBootService.SaveSystem;
        if (guardado == null) return;

        _pendiente = false;
        _ultimoGuardado = ahora;
        bool ok = Partida.Guardar(guardado, SaveRequestContext.Auto);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log(ok ? "[Auto-guardado] Partida guardada." : "[Auto-guardado] No se pudo guardar (ver el error anterior).");
        _pendienteDesde = -1f;
#endif
    }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    const float SegundosEntreDiagnosticos = 10f;
    float _pendienteDesde = -1f;
    float _siguienteDiagnostico;
#endif

    /// Con un guardado pendiente que no se puede hacer, dice en el registro cada pocos segundos qué
    /// lo impide (y, si es un paso de un grafo, cuál).
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("UNITY_INCLUDE_INSTRUMENTATION")]
    void Diagnosticar(string impide, float ahora)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (_pendienteDesde < 0f) _pendienteDesde = ahora;
        if (ahora < _siguienteDiagnostico) return;
        _siguienteDiagnostico = ahora + SegundosEntreDiagnosticos;

        string detalle = impide;
        var hub = NarrativeGraphHub.Instance;
        if (ReferenceEquals(impide, ImpidePasoNoSeguro) && hub != null)
        {
            var paso = hub.FindUnsafeStep(out var grafo);
            if (paso != null) detalle = $"{impide}: «{paso.DisplayTitle}» ({paso.GetType().Name}) en {(grafo != null ? grafo.name : "?")}";
        }
        else if (ReferenceEquals(impide, ImpideGuardarAhora))
        {
            Partida.PuedeGuardarAhora(out var motivo);
            detalle = $"{impide}: {motivo}";
        }
        Debug.LogWarning($"[Auto-guardado] Pendiente desde hace {ahora - _pendienteDesde:0} s; no se guarda: {detalle}.");
#endif
    }

    static bool HayAutoguardados()
    {
        var config = GameBootService.Profile;
        return config != null && config.allowAutoSaves && !GameBootService.IsPresetOverrideActive;
    }

    // Motivos (textos fijos: comprobarlos no reserva memoria).
    const string ImpideGuardarAhora = "Partida.PuedeGuardarAhora";
    const string ImpidePasoNoSeguro = "un grafo está en un paso no seguro";
    const string ImpideSinHub = "no hay NarrativeGraphHub";
    const string ImpideSecuencia = "secuencia en marcha";
    const string ImpideTelon = "pantalla tapada (telón)";
    const string ImpideFase = "fase de juego (menú, diálogo, cinemática, carga…)";
    const string ImpideDialogo = "diálogo abierto";
    const string ImpidePregunta = "pregunta en pantalla";
    const string ImpideSinJugador = "no hay jugador";
    const string ImpideCaido = "el jugador está caído";
    const string ImpideEnElAire = "el jugador no pisa suelo";
    const string ImpideNadando = "el jugador nada";
    const string ImpideVolando = "el jugador vuela";
    const string ImpideLlevando = "el jugador lleva algo en brazos";

    /// Qué impide guardar ahora mismo, o null si nada.
    string QueImpide()
    {
        if (!Partida.PuedeGuardarAhora()) return ImpideGuardarAhora;

        var hub = NarrativeGraphHub.Instance;
        if (hub == null) return ImpideSinHub;
        if (!hub.IsAtSafeStep()) return ImpidePasoNoSeguro;

        // Ninguna escena en marcha ni la pantalla tapada (fundido antes de una cinemática).
        if (CinematicSequencerBase.RunningSequences.Count > 0) return ImpideSecuencia;
        if (Telon.Cerrado) return ImpideTelon;

        for (int i = 0; i < s_fasesQueImpiden.Length; i++)
            if (GameState.Is(s_fasesQueImpiden[i])) return ImpideFase;

        var dialogo = DialogueManager.Instance;
        if (dialogo != null && dialogo.IsOpen) return ImpideDialogo;
        var pregunta = ConfirmationPopupUI.Instance;
        if (pregunta != null && pregunta.Visible) return ImpidePregunta;

        return QueImpideAlJugador();
    }

    /// Vivo, en el suelo, fuera del agua, sin volar y sin nada en brazos.
    string QueImpideAlJugador()
    {
        var jugador = PlayerService.Player;
        if (jugador == null) return ImpideSinJugador;
        if (jugador != _jugador) EnlazarJugador(jugador);

        if (_salud != null && !_salud.IsAlive) return ImpideCaido;
        if (_motor == null || !_motor.PisandoSuelo) return ImpideEnElAire;
        if (_nado != null && _nado.EstaEnElAgua) return ImpideNadando;
        if (_vuelo != null && _vuelo.IsFlying) return ImpideVolando;
        if (_carga != null && (_carga.IsCarrying || _carga.IsPickingUp)) return ImpideLlevando;
        return null;
    }

    void EnlazarJugador(GameObject jugador)
    {
        _jugador = jugador;
        _motor = jugador.GetComponentInChildren<vThirdPersonMotor>(true);
        _salud = jugador.GetComponentInChildren<PlayerHealthSystem>(true);
        _nado = jugador.GetComponentInChildren<PlayerSwimmingController>(true);
        _vuelo = jugador.GetComponentInChildren<PlayerFlyingController>(true);
        _carga = jugador.GetComponentInChildren<PlayerCarrySystem>(true);
    }
}
