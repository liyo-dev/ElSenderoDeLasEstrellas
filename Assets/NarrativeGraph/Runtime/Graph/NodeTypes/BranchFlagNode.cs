using System;
using UnityEngine;

/// <summary>
/// Bifurca según un flag narrativo persistente (SetFlagNode) o, si no existe como flag, según
/// si la señal con esa misma clave se disparó alguna vez (Signals.HasEverRaised).
/// Sustituye al BranchBoolNode legacy (que nunca bifurcaba de verdad).
/// </summary>
[Serializable]
[NarrativeNodeInfo("Flujo", "Según flag", "Bifurca Sí/No según un flag narrativo (o una señal ya disparada).")]
[SavePoint("Solo consulta estado; seguro para guardar")]
public sealed class BranchFlagNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Flag)]
    [Tooltip("Clave del flag (SetFlagNode) o de la señal a comprobar.")]
    public string flagKey;

    [Tooltip("Si está marcado, invierte el resultado (Sí ↔ No).")]
    public bool invert;

    static readonly string[] Ports = { "Sí", "No" };
    public override string[] GetOutputPorts() => Ports;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        bool value = false;
        if (string.IsNullOrWhiteSpace(flagKey))
        {
            Debug.LogWarning($"[BranchFlagNode:{guid}] flagKey vacío → 'No'.");
        }
        else
        {
            value = NarrativeFlags.Has(flagKey);
            if (!value && ctx?.Signals != null)
                value = ctx.Signals.HasEverRaised(flagKey);
        }
        if (invert) value = !value;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BranchFlagNode:{guid}] {flagKey} → {(value ? "Sí" : "No")}");
#endif
        AdvanceThrough(ctx, ready, value ? 0 : 1);
    }
}
