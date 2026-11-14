using System;
using System.Collections.Generic;

/// <summary>
/// Flags narrativos persistentes: hechos del mundo («la puerta ya está abierta») que sobreviven
/// al guardado. Dónde se guardan lo decide el juego con su <see cref="INarrativeFlagStore"/>;
/// esta clase es la única entrada para los nodos (SetFlagNode, BranchFlagNode…) y avisa de cada
/// cambio con <see cref="Changed"/>.
///
/// No confundir con las señales (DefaultNarrativeSignals): una señal es un evento que ocurre; un
/// flag es un estado que se consulta.
/// </summary>
public static class NarrativeFlags
{
    /// <summary>Se dispara con la clave del flag cuando su valor cambia.</summary>
    public static event Action<string> Changed;

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;
#endif

    /// <summary>
    /// Prefijo con el que un almacén que guarde los flags en una lista de cadenas compartida los
    /// distingue de las demás entradas. Lo usan el almacén del juego y quien lea esa lista.
    /// </summary>
    public const string Prefix = "NARRATIVE_FLAG:";

    /// <summary>Clave de almacenamiento del flag (con <see cref="Prefix"/>).</summary>
    public static string Key(string flag) => Prefix + flag;

    public static bool Has(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag)) return false;
        var store = NarrativeServices.Get<INarrativeFlagStore>();
        return store != null && store.IsAvailable && store.Has(flag);
    }

    public static void Set(string flag, bool value)
    {
        if (string.IsNullOrWhiteSpace(flag)) return;
        if (!NarrativeServices.TryGet(out INarrativeFlagStore store, nameof(NarrativeFlags))) return;
        if (!store.IsAvailable)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            UnityEngine.Debug.LogWarning($"[NarrativeFlags] No hay partida donde guardar; el flag '{flag}' no se guarda. ¿Play sin la escena de arranque?");
#endif
            return;
        }
        bool anterior = store.Has(flag);
        store.Set(flag, value);
        if (anterior != value) Changed?.Invoke(flag);
    }

    /// <summary>Todos los flags narrativos activos (para depuración y editor).</summary>
    public static IEnumerable<string> All()
    {
        var store = NarrativeServices.Get<INarrativeFlagStore>();
        if (store == null || !store.IsAvailable) return Array.Empty<string>();
        return store.All();
    }
}
