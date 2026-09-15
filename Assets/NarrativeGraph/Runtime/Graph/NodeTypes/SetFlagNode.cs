using System;
using UnityEngine;

/// <summary>
/// Marca (o borra) un flag narrativo persistente. Se guarda en preset.flags con prefijo
/// NARRATIVE_FLAG:, así que sobrevive al guardado/carga sin configurar nada más.
/// Consultable con BranchFlagNode.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Flujo", "Poner flag", "Guarda un hecho persistente ('CAP1_TERMINADO') consultable con 'Según flag'.")]
[SavePoint("Instantáneo")]
public sealed class SetFlagNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Flag)]
    [Tooltip("Clave del flag. Convención: MAYÚSCULAS_CON_GUIONES, sin prefijo.")]
    public string flagKey;

    [Tooltip("true = activar el flag; false = borrarlo.")]
    public bool value = true;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
            Debug.LogWarning($"[SetFlagNode:{guid}] flagKey vacío; no se guarda nada.");
        else
        {
            NarrativeFlags.Set(flagKey, value);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SetFlagNode:{guid}] {flagKey} = {value}");
#endif
        }
        ready?.Invoke();
    }
}
