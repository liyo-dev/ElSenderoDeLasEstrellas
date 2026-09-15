using System;
using UnityEngine;

/// <summary>
/// Bifurca según el estado de una quest. Es el nodo que faltaba para expresar "el NPC dice
/// una cosa u otra según cómo vaya la misión" sin el sistema Interactive (ver TDD § 15.7).
///
/// Puertos: No iniciada / Activa / Pasos listos / Completada.
/// "Pasos listos" = activa con todos los pasos completados pero sin entregar todavía.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Quests", "Según estado de quest", "Bifurca por el estado actual de una quest (no iniciada / activa / pasos listos / completada).")]
[SavePoint("Solo consulta estado; seguro para guardar")]
public sealed class BranchQuestStateNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Quest)]
    [Tooltip("ID de la quest a consultar.")]
    public string questId;

    static readonly string[] Ports = { "No iniciada", "Activa", "Pasos listos", "Completada" };
    public override string[] GetOutputPorts() => Ports;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        var state = NarrativeQuestState.NotStarted;
        if (string.IsNullOrWhiteSpace(questId))
            Debug.LogWarning($"[BranchQuestStateNode:{guid}] questId vacío → salida 'No iniciada'.");
        else if (ctx?.Signals != null)
            state = ctx.Signals.GetQuestState(questId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BranchQuestStateNode:{guid}] {questId} → {state}");
#endif
        AdvanceThrough(ctx, ready, (int)state);
    }
}
