using UnityEngine;

/// <summary>
/// Singleton vacío — conservado para no romper la referencia en Start.unity.
/// La vida y el maná de cada personaje los lleva SaludDelGrupo (en GrupoDelJugador, INC-717).
/// </summary>
[DefaultExecutionOrder(-100)]
public class PartyStatsManager : MonoBehaviour
{
    public static PartyStatsManager Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
