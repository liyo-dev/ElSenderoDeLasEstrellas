using Invector.vCharacterController;
using UnityEngine;

/// <summary>
/// Auto-guardado de El Sendero: une el aviso del grafo «paso seguro alcanzado»
/// (<see cref="NarrativeRunner.SafeStepReached"/>) con la partida (<see cref="Partida.Guardar"/>).
///
/// Al llegar un aviso queda un guardado pendiente, que se hace en cuanto se cumple todo: hay
/// auto-guardados (<see cref="GameBootProfile.allowAutoSaves"/>), no es una partida de pruebas,
/// han pasado <see cref="GameBootProfile.segundosEntreAutoguardados"/> desde el último guardado o
/// desde la carga, <see cref="Partida.PuedeGuardarAhora()"/>, todos los grafos están en pasos
/// seguros, no hay diálogo, pregunta ni menú abiertos, y el jugador está vivo, en el suelo, sin
/// nadar, volar ni llevar nada en brazos. Al guardar enseña un aviso breve.
/// El farol de estrellas sigue siendo el guardado manual.
/// </summary>
public sealed class AutoguardadoDelJuego : MonoBehaviour
{
    const float SegundosEntreComprobaciones = 0.5f;
    const float DuracionDelAviso = 1.5f;
    const string ClaveDelAviso = "AUTOGUARDADO";

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
        if (HayAutoguardados()) _pendiente = true;
    }

    // Recién cargada (o empezada) una partida no se guarda: los grafos se reanudan en pasos
    // seguros y lo guardado es justo lo que se acaba de cargar.
    void AlCargarLaPartida()
    {
        _pendiente = false;
        _ultimoGuardado = Time.unscaledTime;
    }

    void Update()
    {
        if (!_pendiente) return;
        float ahora = Time.unscaledTime;
        if (ahora < _siguienteComprobacion) return;
        _siguienteComprobacion = ahora + SegundosEntreComprobaciones;

        if (!HayAutoguardados()) { _pendiente = false; return; }
        if (ahora - _ultimoGuardado < GameBootService.Profile.segundosEntreAutoguardados) return;
        if (!SePuedeGuardarYa()) return;

        var guardado = GameBootService.SaveSystem;
        if (guardado == null) return;

        _pendiente = false;
        _ultimoGuardado = ahora;
        if (Partida.Guardar(guardado, SaveRequestContext.Auto))
            HudToastService.Instance?.Show(ClaveDelAviso, DuracionDelAviso);
    }

    static bool HayAutoguardados()
    {
        var config = GameBootService.Profile;
        return config != null && config.allowAutoSaves && !GameBootService.IsPresetOverrideActive;
    }

    bool SePuedeGuardarYa()
    {
        if (!Partida.PuedeGuardarAhora()) return false;

        var hub = NarrativeGraphHub.Instance;
        if (hub == null || !hub.IsAtSafeStep()) return false;

        for (int i = 0; i < s_fasesQueImpiden.Length; i++)
            if (GameState.Is(s_fasesQueImpiden[i])) return false;

        var dialogo = DialogueManager.Instance;
        if (dialogo != null && dialogo.IsOpen) return false;
        var pregunta = ConfirmationPopupUI.Instance;
        if (pregunta != null && pregunta.Visible) return false;

        return JugadorQuieto();
    }

    /// Vivo, en el suelo, fuera del agua, sin volar y sin nada en brazos.
    bool JugadorQuieto()
    {
        var jugador = PlayerService.Player;
        if (jugador == null) return false;
        if (jugador != _jugador) EnlazarJugador(jugador);

        if (_salud != null && !_salud.IsAlive) return false;
        if (_motor == null || !_motor.PisandoSuelo) return false;
        if (_nado != null && _nado.EstaEnElAgua) return false;
        if (_vuelo != null && _vuelo.IsFlying) return false;
        if (_carga != null && (_carga.IsCarrying || _carga.IsPickingUp)) return false;
        return true;
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
