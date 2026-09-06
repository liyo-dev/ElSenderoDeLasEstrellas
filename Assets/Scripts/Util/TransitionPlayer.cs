using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using EasyTransition;

[DisallowMultipleComponent]
public class TransitionPlayer : MonoBehaviour
{
    [Header("Efecto")]
    public TransitionSettings settings;   // Asigna p.ej. Fade.asset
    [Min(0f)] public float delay = 0f;

    [Header("Atajos (opcionales)")]
    public bool playOnStart = false;
    public Key debugKey = Key.None;  // p.ej. F9 para probar

    // FIX (5 sept 2026, AGENTS.md; mismo patron que CinematicSequencerBase._simulateHotkey):
    // UnityEngine.Input.GetKeyDown lanza InvalidOperationException con el nuevo Input System
    // activo en exclusiva. Cambiado de KeyCode a Key.
    private bool? _debugKeyValid;

    void Start()
    {
        if (playOnStart) Play();
    }

    void Update()
    {
        if (debugKey == Key.None) return;
        _debugKeyValid ??= System.Enum.IsDefined(typeof(Key), debugKey);
        if (_debugKeyValid != true) return;
        if (Keyboard.current != null && Keyboard.current[debugKey].wasPressedThisFrame)
            Play();
    }

    /// <summary>Lanza la transición (sin cargar escena).</summary>
    public void Play()
    {
        var tm = TransitionManager.Instance();
        if (tm == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[TransitionPlayer] TransitionManager no encontrado. ¿Está Start cargada?");
#endif
            return;
        }
        tm.Transition(settings, delay);
    }

    /// <summary>Lanza la transición y carga una escena por nombre.</summary>
    public void PlayAndLoadScene(string sceneName)
    {
        var tm = TransitionManager.Instance();
        if (tm == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[TransitionPlayer] TransitionManager no encontrado. Cargo escena directa.");
#endif
            SceneManager.LoadScene(sceneName);
            return;
        }
        tm.Transition(sceneName, settings, delay);
    }

    /// <summary>Permite cambiar el efecto por código/Inspector y ejecutar.</summary>
    public void PlayWith(TransitionSettings newSettings)
    {
        settings = newSettings;
        Play();
    }
}