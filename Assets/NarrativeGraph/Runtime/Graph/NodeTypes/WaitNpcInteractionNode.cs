using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Espera a que el jugador hable con un actor concreto. Escucha la señal
/// <c>NPC_INTERACT_{actorId}</c>, que el juego emite cada vez que el jugador interactúa con un
/// personaje, así que no necesita nada más en el actor.
///
/// El grafo espera aquí y a la salida decide (p. ej. con BranchQuestStateNode) qué toca decir. Se
/// puede volver a este nodo tantas veces como haga falta (bucle «habla con él hasta que...»).
///
/// La señal se trata como hecho en vivo: al entrar se descarta cualquier interacción pendiente
/// anterior para no avanzar por una charla que ocurrió antes de llegar aquí.
///
/// Mientras espera, el actor lleva la marca de «tiene algo para ti» (INarrativeAttentionMarkers).
///
/// Si varios grafos esperan a la vez hablar con el mismo actor, la charla con marca (la de la
/// historia) va antes que las de sin marca (servicios que se repiten, como pedir una revancha):
/// una misma interacción solo hace avanzar a las esperas con marca, y las demás siguen esperando
/// a la siguiente. Así no salen dos conversaciones a la vez del mismo personaje.
/// </summary>
[Serializable]
[NarrativeNodeInfo("NPCs", "Esperar hablar con NPC", "Se queda esperando hasta que el jugador interactúa con el NPC indicado.")]
[SavePoint("Seguro guardar mientras espera")]
public sealed class WaitNpcInteractionNode : NarrativeNode, INarrativeStateEffect
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("Id persistente del actor.")]
    public string npcId;

    [Tooltip("Si está marcado, una interacción ocurrida ANTES de llegar a este nodo también cuenta (comportamiento sticky). Normalmente NO.")]
    public bool acceptEarlierInteraction = false;

    [Tooltip("Marca propia de este nodo mientras se espera la charla, si se quiere una distinta de la del actor. Vacío = la marca por defecto del actor (recomendado: se pone una vez en el actor y vale para cualquier nodo).")]
    public GameObject questIcon;

    [Tooltip("Enseñar la marca de «tiene algo para ti» (cabeza y minimapa) mientras se espera. Apagarlo para charlas que el jugador busca por su cuenta (p. ej. un servicio que se puede repetir siempre).")]
    public bool showMarker = true;

    public static string SignalKeyFor(string npcId) => $"NPC_INTERACT_{npcId}";

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Actor(npcId, "Espera a que el jugador hable con");
        if (!string.IsNullOrWhiteSpace(npcId)) d.Signal(SignalKeyFor(npcId), SignalRole.Escucha, "señal");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(npcId)) warnings.Add("npcId vacío");
    }

    [NonSerialized] private INarrativeSignals _signals;
    [NonSerialized] private Action _handler;
    [NonSerialized] private string _key;

    // Esperas activas por actor, para dar prioridad a las que llevan marca.
    private static readonly Dictionary<string, List<WaitNpcInteractionNode>> s_esperando = new();
    // Último fotograma en que una espera con marca se quedó la charla de cada actor.
    private static readonly Dictionary<string, int> s_tomadaConMarca = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_esperando.Clear(); s_tomadaConMarca.Clear(); }
#endif

    /// <summary>true si algún paso del grafo espera ahora que el jugador hable con este actor.</summary>
    public static bool IsWaitingFor(string npcId)
        => !string.IsNullOrEmpty(npcId) && s_esperando.TryGetValue(npcId, out var lista) && lista.Count > 0;

    private static bool HayOtraConMarca(string npcId, WaitNpcInteractionNode yo)
    {
        if (s_tomadaConMarca.TryGetValue(npcId, out int f) && f == Time.frameCount) return true;
        if (!s_esperando.TryGetValue(npcId, out var lista)) return false;
        foreach (var w in lista) if (w != yo && w.showMarker) return true;
        return false;
    }

    public void Project(INarrativeStateWriter state)
    {
        if (!string.IsNullOrWhiteSpace(npcId)) state.MarkSignalRaised(SignalKeyFor(npcId));
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning($"[WaitNpcInteractionNode:{guid}] npcId vacío → avanzando para no bloquear.");
            ready?.Invoke();
            return;
        }
        if (ctx?.Signals == null)
        {
            Debug.LogWarning($"[WaitNpcInteractionNode:{guid}] Sin proveedor de señales → avanzando.");
            ready?.Invoke();
            return;
        }

        _key = SignalKeyFor(npcId);
        _signals = ctx.Signals;

        if (!acceptEarlierInteraction)
            _signals.ClearPendingCustom(_key);

        ShowIcon();

        Apuntar(true);
        _handler = () =>
        {
            // Una espera sin marca cede la charla a la de la historia (ver cabecera).
            if (!showMarker && HayOtraConMarca(npcId, this)) return;
            if (showMarker) s_tomadaConMarca[npcId] = Time.frameCount;
            Unsubscribe();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[WaitNpcInteractionNode:{guid}] ✅ El jugador habló con '{npcId}' → avanzando");
#endif
            ready?.Invoke();
        };

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[WaitNpcInteractionNode:{guid}] Esperando '{_key}'...");
#endif
        _signals.OnCustom(_key, _handler);
    }

    public override void Exit(NarrativeContext ctx)
    {
        Unsubscribe();
        HideIcon();
    }

    private void Apuntar(bool esperando)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return;
        if (!s_esperando.TryGetValue(npcId, out var lista))
        {
            if (!esperando) return;
            s_esperando[npcId] = lista = new List<WaitNpcInteractionNode>();
        }
        if (esperando) { if (!lista.Contains(this)) lista.Add(this); }
        else lista.Remove(this);
    }

    private void Unsubscribe()
    {
        Apuntar(false);
        if (_signals != null && _handler != null && !string.IsNullOrEmpty(_key))
        {
            try { _signals.OffCustom(_key, _handler); } catch { }
        }
        _signals = null;
        _handler = null;
    }

    private void ShowIcon()
    {
        if (!showMarker) return;
        if (NarrativeServices.TryGet(out INarrativeAttentionMarkers markers, nameof(WaitNpcInteractionNode)))
            markers.ShowMarker(npcId, questIcon);
    }

    private void HideIcon()
    {
        NarrativeServices.Get<INarrativeAttentionMarkers>()?.HideMarker(npcId);
    }
}
