using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro de los pasos del juego por id (ver <see cref="INarrativeGameStep"/>). Lo usa el nodo
/// «Pedir al juego»; quien sabe hacer algo se apunta aquí: un <see cref="NarrativeGameStepHook"/>
/// de la escena al activarse, o el código del juego con <see cref="Register"/>.
/// </summary>
public static class NarrativeGameSteps
{
    static readonly Dictionary<string, INarrativeGameStep> s_steps = new(StringComparer.Ordinal);

    /// <summary>Se ha registrado un paso (lo escucha quien esperaba a que apareciera).</summary>
    public static event Action<string> Registered;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_steps.Clear();
        Registered = null;
    }
#endif

    /// <summary>Apunta un paso con su id. Si ya había otro con ese id, lo sustituye (y lo avisa).</summary>
    public static void Register(string id, INarrativeGameStep step)
    {
        if (string.IsNullOrWhiteSpace(id) || step == null) return;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (TryGet(id, out var previous) && !ReferenceEquals(previous, step))
            Debug.LogWarning($"[NarrativeGameSteps] Dos pasos del juego con el id «{id}»: se queda el último ({step}).");
#endif
        s_steps[id] = step;
        Registered?.Invoke(id);
    }

    /// <summary>Quita el paso, solo si sigue siendo el indicado.</summary>
    public static void Unregister(string id, INarrativeGameStep step)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (s_steps.TryGetValue(id, out var current) && ReferenceEquals(current, step)) s_steps.Remove(id);
    }

    /// <summary>El paso con ese id, si hay alguno vivo.</summary>
    public static bool TryGet(string id, out INarrativeGameStep step)
    {
        step = null;
        if (string.IsNullOrWhiteSpace(id) || !s_steps.TryGetValue(id, out step)) return false;
        if (step is UnityEngine.Object unityObject && unityObject == null)
        {
            s_steps.Remove(id);
            step = null;
            return false;
        }
        return true;
    }

    /// <summary>Ids registrados ahora mismo (para depurar).</summary>
    public static IEnumerable<string> Ids => s_steps.Keys;
}
