using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto único donde el juego conecta sus sistemas con el grafo narrativo.
///
/// El núcleo del grafo no conoce ninguna clase del juego: los nodos piden lo que necesitan por
/// una interfaz pequeña (los puertos de esta carpeta: <see cref="INarrativeAudio"/>,
/// <see cref="INarrativeScreen"/>, <see cref="IQuestService"/>…) y el juego registra aquí una
/// implementación de cada una al arrancar, normalmente desde un método
/// <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c> o desde el Awake de un componente.
///
/// Si un puerto no tiene implementación, <see cref="TryGet{T}(out T, string)"/> lo avisa una vez
/// (solo en editor o con instrumentación) y el nodo sigue sin colgar el grafo.
/// </summary>
public static class NarrativeServices
{
    static readonly Dictionary<Type, object> s_services = new();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    static readonly HashSet<Type> s_warned = new();
#endif

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_services.Clear();
        s_warned.Clear();
    }
#endif

    /// <summary>Registra la implementación de un puerto. Sustituye a la que hubiera.</summary>
    public static void Register<T>(T service) where T : class
    {
        if (service == null) return;
        s_services[typeof(T)] = service;
    }

    /// <summary>Quita la implementación de un puerto, solo si sigue siendo la indicada.</summary>
    public static void Unregister<T>(T service) where T : class
    {
        if (service == null) return;
        if (s_services.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, service))
            s_services.Remove(typeof(T));
    }

    /// <summary>Implementación registrada del puerto, o null si no hay (o si era un objeto de Unity ya destruido).</summary>
    public static T Get<T>() where T : class
    {
        if (!s_services.TryGetValue(typeof(T), out var service)) return null;
        if (service is UnityEngine.Object unityObject && unityObject == null)
        {
            s_services.Remove(typeof(T));
            return null;
        }
        return service as T;
    }

    /// <summary>
    /// Implementación registrada del puerto. Si no hay, avisa una vez por puerto indicando quién la
    /// pedía y devuelve false: quien llama sigue adelante sin ella.
    /// </summary>
    public static bool TryGet<T>(out T service, string requester) where T : class
    {
        service = Get<T>();
        if (service != null) return true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (s_warned.Add(typeof(T)))
            Debug.LogWarning($"[NarrativeServices] No hay implementación registrada de {typeof(T).Name} " +
                             $"({requester}): esa parte se salta y el grafo sigue.");
#endif
        return false;
    }
}
