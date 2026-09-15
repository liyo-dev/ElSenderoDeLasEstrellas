using System;
using UnityEngine;
using Game.NPC;

/// <summary>
/// Espera a que el jugador hable (pulse A) con un NPC concreto. Se apoya en la señal
/// <c>NPC_INTERACT_{persistenceId}</c> que ya emite NPCBrain.HandleInteraction() en cada
/// interacción, así que no necesita ningún componente extra en el NPC.
///
/// Es la pieza que sustituye al "¿qué digo cuando me hablan?" del NPCQuestConfig: el grafo
/// espera aquí, y a la salida decide con BranchQuestStateNode qué diálogo toca. Se puede
/// volver a este nodo tantas veces como haga falta (bucle "habla con Eldran hasta que...").
///
/// La señal se trata como hecho EN VIVO: al entrar se descarta cualquier interacción pendiente
/// anterior (sticky) para no avanzar por una charla que ocurrió antes de llegar aquí.
///
/// Mientras espera puede mostrar el icono de quest (cabeza + minimapa) del NPC, vía
/// <see cref="NarrativeActor.ShowQuestIcon"/> — es el reemplazo, derivado del grafo, del icono
/// que antes pintaba en solitario NPCQuestConfig/NPCQuestIconManager (sistema legacy, congelado).
/// Requiere que el NPC tenga el componente <see cref="NarrativeActor"/>; si no lo tiene, el nodo
/// sigue funcionando igual para la espera, solo sin icono (con un aviso en consola).
/// </summary>
[Serializable]
[NarrativeNodeInfo("NPCs", "Esperar hablar con NPC", "Se queda esperando hasta que el jugador interactúa con el NPC indicado.")]
[SavePoint("Seguro guardar mientras espera")]
public sealed class WaitNpcInteractionNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC (el mismo que usa NPCBehaviourManagerV2).")]
    public string npcId;

    [Tooltip("Si está marcado, una interacción ocurrida ANTES de llegar a este nodo también cuenta (comportamiento sticky). Normalmente NO.")]
    public bool acceptEarlierInteraction = false;

    [Tooltip("Prefab de icono a mostrar sobre la cabeza del NPC y en el minimapa mientras se espera esta interacción, si quieres uno distinto al icono por defecto del NPC. Vacío = usa el 'Default Quest Icon Prefab' que tenga puesto el NarrativeActor del NPC (recomendado: se asigna una vez en el NPC y sirve para cualquier nodo/grafo, en vez de tener que repetirlo aquí cada vez). Requiere que el NPC tenga el componente NarrativeActor.")]
    public GameObject questIcon;

    public static string SignalKeyFor(string npcId) => $"NPC_INTERACT_{npcId}";

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[WaitNpcInteractionNode:{guid}] ✅ El jugador habló con '{npcId}' → avanzando");
#endif
            ready?.Invoke();
        };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        // No cortamos aquí aunque questIcon esté vacío: ResolveActor().ShowQuestIcon(null) cae al
        // icono por defecto del propio NarrativeActor del NPC si lo tiene (ver NarrativeActor.
        // ShowQuestIcon) — así un NPC con su icono por defecto puesto sale bien en cualquier nodo
        // sin tener que asignarlo aquí también.
        ResolveActor()?.ShowQuestIcon(questIcon);
    }

    private void HideIcon()
    {
        ResolveActor()?.HideQuestIcon();
    }

    private NarrativeActor ResolveActor()
    {
        var npc = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(npcId) : null;
        var actor = npc != null ? npc.GetComponent<NarrativeActor>() : null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (actor == null)
            Debug.LogWarning($"[WaitNpcInteractionNode:{guid}] '{npcId}' no tiene NarrativeActor — no se puede mostrar/ocultar el icono de quest.");
#endif
        return actor;
    }
}
