using System.Collections;
using UnityEngine;

/// <summary>
/// Arranque del juego y ciclo de vida de la partida (vive en la escena Start).
///
/// Al despertar prepara la partida en <see cref="Partida"/>: el preset de pruebas si está activo,
/// si no el guardado, y si no hay guardado el preset por defecto. Un frame después dispara
/// <see cref="OnProfileReady"/> para que cada sistema se inicialice desde <see cref="Partida.Preset"/>.
/// También es la puerta de «Continuar», «Nueva partida» y «Recargar preset de pruebas».
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameBootService : MonoBehaviour
{
    private static GameBootService _instance;
    public static bool IsAvailable => _instance != null;

    [SerializeField] private GameBootProfile _bootProfileAsset;

    private static GameBootProfile _profile;
    public static GameBootProfile Profile => _profile;

    private static SaveSystem _saveSystem;
    public static SaveSystem SaveSystem => _saveSystem;

    /// <summary>La partida está lista (arranque, carga, partida nueva o preset recargado).</summary>
    public static event System.Action OnProfileReady;

    /// <summary>El preset de pruebas manda sobre el guardado.</summary>
    public static bool IsPresetOverrideActive => _profile != null && _profile.ShouldBootFromPreset();

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Game/Boot/Force Reload Test Preset")]
    public static void ForceReloadTestPreset()
    {
        if (Application.isPlaying && _instance != null) ReloadTestPreset();
        else Debug.LogWarning("Esta opción solo funciona en Play.");
    }

    [UnityEditor.MenuItem("Game/Boot/Force New Game Reset")]
    public static void ForceNewGameReset()
    {
        if (Application.isPlaying && _instance != null) NewGameReset();
        else Debug.LogWarning("Esta opción solo funciona en Play.");
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        if (Application.isPlaying && _instance == null)
            _instance = FindAnyObjectByType<GameBootService>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _profile = null;
        _saveSystem = null;
        OnProfileReady = null;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // Los Log/Warning ya dicen qué sistema habla: sin traza de pila son mucho más baratos.
        // Error y Exception la conservan para localizar fallos.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _profile = _bootProfileAsset;
#if UNITY_EDITOR
        if (_profile == null)
            _profile = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBootProfile>("Assets/_BootProfile/GameBootProfile.asset");
#endif
        if (_profile == null)
        {
            Debug.LogError("[GameBootService] Falta el GameBootProfile en el GameBootService de la escena Start.");
            return;
        }

        _saveSystem = FindAnyObjectByType<SaveSystem>();
        if (_saveSystem == null) _saveSystem = new GameObject("[SaveSystem]").AddComponent<SaveSystem>();

        PrepararPartidaAlArrancar();
        // Un frame de margen para que todos los OnEnable se hayan suscrito.
        StartCoroutine(AvisarPartidaListaEnElSiguienteFrame());
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private IEnumerator AvisarPartidaListaEnElSiguienteFrame()
    {
        yield return null;
        OnProfileReady?.Invoke();
        var diagnostico = FindAnyObjectByType<ProfileReadyDiagnostics>();
        if (diagnostico != null) diagnostico.HandleProfileReadyForDiagnostics();
    }

    private void PrepararPartidaAlArrancar()
    {
        if (_profile.ShouldBootFromPreset())
        {
            // El preset de pruebas se trata como una partida cargada. Los NPCs aún no existen:
            // sus posiciones las aplica WorldBootstrap al cargar el mundo.
            Partida.CargarDesdePlantilla(_profile.bootPreset);
            Partida.AplicarPartidaCargada(aplicarPosicionesDeNpcs: false);
            return;
        }

        if (!Partida.Cargar(_saveSystem))
            Partida.CargarDesdePlantilla(_profile.defaultPlayerPreset);

        var presetDelJugador = FindAnyObjectByType<PlayerPresetService>();
        if (presetDelJugador != null) presetDelJugador.ApplyCurrentPreset();
    }

    /// <summary>
    /// Olvida el estado de sesión que vive en estáticos y no muere con las escenas (cámara
    /// bloqueada, transición de teletransporte, HUD oculto…). Se llama en los puntos de entrada
    /// seguros: al llegar al menú principal y al arrancar el mundo.
    /// </summary>
    public static void ResetTransientSessionState()
    {
        GameState.ResetAll();
        CameraDirectorService.ForceResetState();
        TeleportService.ForceResetTransitionLock();
        Sendero.UI.PlayerHUDV2.ForceResetHideState();
    }

    /// <summary>En modo pruebas, descarta lo avanzado y vuelve a empezar desde el preset de pruebas.</summary>
    public static void ReloadTestPreset()
    {
        if (_instance == null || !IsPresetOverrideActive) return;
        Partida.CargarDesdePlantilla(_profile.bootPreset);
        Partida.AplicarPartidaCargada(aplicarPosicionesDeNpcs: false);
        OnProfileReady?.Invoke();
    }

    /// <summary>
    /// Partida nueva: borra el guardado y vuelve al preset por defecto. En modo pruebas vuelve al
    /// preset de pruebas.
    /// </summary>
    public static void NewGameReset()
    {
        if (_instance == null) return;

        if (IsPresetOverrideActive) Partida.CargarDesdePlantilla(_profile.bootPreset);
        else Partida.PrepararNueva(_saveSystem);

        if (Game.NPC.PlayerParty.HasInstance) Game.NPC.PlayerParty.Instance.ResetForNewGame();
        OnProfileReady?.Invoke();
    }

    /// <summary>Avisa a los sistemas de que la partida está lista sin tocarla.</summary>
    public static void NotifyProfileReady()
    {
        if (IsAvailable) OnProfileReady?.Invoke();
    }

    /// <summary>Hay algo que continuar: una partida guardada, o el preset de pruebas si está activo.</summary>
    public static bool PuedeContinuar(SaveSystem saveSystem = null)
    {
        if (IsPresetOverrideActive) return true;
        var ss = saveSystem != null ? saveSystem : _saveSystem;
        return ss != null && ss.HasSave();
    }

    /// <summary>
    /// «Continuar» (menú principal, Game Over, abandonar un minijuego): deja los sistemas como en el
    /// último guardado, con la escena del mundo ya descargada. En modo pruebas recarga el preset de
    /// pruebas. Devuelve false si no había nada que cargar.
    /// </summary>
    public static bool PrepararContinuar(SaveSystem saveSystem = null)
    {
        if (IsPresetOverrideActive)
        {
            ReloadTestPreset();
            return true;
        }
        var ss = saveSystem != null ? saveSystem : _saveSystem;
        if (ss == null || _profile == null || !Partida.Cargar(ss)) return false;
        NotifyProfileReady();
        return true;
    }
}
