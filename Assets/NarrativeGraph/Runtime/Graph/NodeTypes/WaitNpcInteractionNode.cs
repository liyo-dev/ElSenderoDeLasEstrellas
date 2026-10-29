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
/// </summary>
[Serializable]
[NarrativeNodeInfo("NPCs", "Esperar hablar con NPC", "Se queda esperando hasta que el jugador interactúa con el NPC indicado.")]
[SavePoint("Seguro guardar mientras espera")]
public sealed class WaitNpcInteractionNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("Id persistente del actor.")]
    public string npcId;

    [Tooltip("Si está marcado, una interacción ocurrida ANTES de llegar a este nodo también cuenta (comportamiento sticky). Normalmente NO.")]
    public bool acceptEarlierInteraction = false;

    [Tooltip("Marca propia de este nodo mientras se espera la charla, si se quiere una distinta de la del actor. Vacío = la marca por defecto del actor (recomendado: se pone una vez en el actor y vale para cualquier nodo).")]
    public GameObject questIcon;

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

        _handler = () =>
        {
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

    private void Unsubscribe()
    {
        if (_signals != null && _handler != null && !string.IsNullOrEmpty(_key))
        {
            try { _signals.OffCustom(_key, _handler); } catch { }
        }
        _signals = null;
        _handler = null;
    }

    private void ShowIcon()
    {
        if (NarrativeServices.TryGet(out INarrativeAttentionMarkers markers, nameof(WaitNpcInteractionNode)))
            markers.ShowMarker(npcId, questIcon);
    }

    private void HideIcon()
    {
        NarrativeServices.Get<INarrativeAttentionMarkers>()?.HideMarker(npcId);
    }
}
