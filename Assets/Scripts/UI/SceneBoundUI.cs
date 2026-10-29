using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Implementado por componentes que viven en el mismo GameObject que un SceneBoundUI y que
/// necesitan posponer su ocultado automático por cambio de escena mientras enseñan algo que el
/// jugador todavía no ha tenido tiempo de leer (p. ej. AbilityUnlockPopupUI con el popup de un
/// hechizo en pantalla). Mientras BlocksSceneHide() devuelva true, SceneBoundUI no apaga el
/// GameObject aunque la escena activa ya no esté en allowedScenes: un corte de cámara o una carga
/// de escena no deben tragarse un aviso a medio leer.
/// </summary>
public interface ISceneBoundUIHideGuard
{
    bool BlocksSceneHide();
}

/// <summary>
/// UI persistente (Start.unity) que solo se ve en ciertas escenas: se enciende y se apaga sola al
/// cambiar de escena o al aparecer la pantalla de carga. Esconderla durante una cinemática o la
/// intro de un jefe no es cosa suya: eso va por <see cref="OcultablesDeCinematica"/>.
/// </summary>
public class SceneBoundUI : MonoBehaviour
{
    [SerializeField] private string uniqueId = string.Empty;
    [SerializeField] private List<string> allowedScenes = new();
    [SerializeField] private bool allowWhenListEmpty = true;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool detachFromParent = true;

    private static readonly Dictionary<string, SceneBoundUI> Instances = new();
    private string instanceKey;
    private ISceneBoundUIHideGuard _hideGuard;
    // True cuando un cambio de escena habría ocultado este objeto pero _hideGuard lo bloqueó.
    // El guard llama a ReapplySceneState() en cuanto deja de bloquear para que el ocultado
    // pendiente (si sigue aplicando) se ejecute entonces.
    private bool _pendingHide;

    /// <summary>Permite una escena solo durante la sesión actual y reaplica su visibilidad.</summary>
    public static bool AllowSceneFor(string id, string sceneName)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(sceneName)) return false;
        if (!Instances.TryGetValue(id, out var inst) || inst == null) return false;

        if (!inst.allowedScenes.Contains(sceneName))
            inst.allowedScenes.Add(sceneName);

        inst.ApplySceneState();
        return true;
    }

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instances.Clear();
    }
    #endif


    private bool IsAllowedInCurrentScene()
    {
        if (allowedScenes.Count == 0) return allowWhenListEmpty;

        // UI ligada al mundo: no se pinta encima de la pantalla de carga (INC-532). La que no tiene
        // escenas asignadas (diálogos, popups, texto dramático) gestiona su propia visibilidad.
        if (SceneTransitionLoader.PantallaDeCargaVisible) return false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (allowedScenes.Contains(SceneManager.GetSceneAt(i).name))
                return true;
        return false;
    }

    private void Awake()
    {
        instanceKey = string.IsNullOrEmpty(uniqueId) ? name : uniqueId;
        if (Instances.TryGetValue(instanceKey, out var existing) && existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        Instances[instanceKey] = this;
        _hideGuard = GetComponent<ISceneBoundUIHideGuard>();

        if (detachFromParent && transform.parent != null)
            transform.SetParent(null, worldPositionStays: false);

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneTransitionLoader.PantallaDeCargaCambiada += ApplySceneState;
        ApplySceneState();
    }

    private void OnDestroy()
    {
        if (Instances.TryGetValue(instanceKey, out var existing) && existing == this)
            Instances.Remove(instanceKey);

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneTransitionLoader.PantallaDeCargaCambiada -= ApplySceneState;
    }

    private void OnActiveSceneChanged(Scene _, Scene newScene) => ApplySceneState();
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySceneState();
    private void OnSceneUnloaded(Scene scene) => ApplySceneState();

    private void ApplySceneState()
    {
        bool allowed = IsAllowedInCurrentScene();

        // No cortar en seco algo que el jugador todavía está leyendo: se deja el GameObject activo
        // y se marca el ocultado como pendiente; el propio guard llama a ReapplySceneState() en
        // cuanto termina.
        if (!allowed && gameObject.activeSelf && _hideGuard != null && _hideGuard.BlocksSceneHide())
        {
            _pendingHide = true;
            return;
        }

        _pendingHide = false;
        if (gameObject.activeSelf != allowed)
            gameObject.SetActive(allowed);
    }

    /// <summary>
    /// Reintenta aplicar el estado de escena si había un ocultado pendiente (ver ApplySceneState).
    /// La llama el ISceneBoundUIHideGuard en cuanto BlocksSceneHide() deja de devolver true.
    /// </summary>
    public void ReapplySceneState()
    {
        if (_pendingHide) ApplySceneState();
    }
}
