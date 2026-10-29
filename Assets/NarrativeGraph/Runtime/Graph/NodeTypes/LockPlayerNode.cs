using System;
using UnityEngine;

/// <summary>
/// Quita o devuelve el control al jugador (INarrativePlayerControl); mientras está bloqueado
/// tampoco se ve el HUD. Se pone un nodo con bloquear=true antes del momento crítico y otro con
/// bloquear=false después: los dos piden y sueltan con la misma clave (el tipo del nodo). Ver INC-538.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Jugador", "Bloquear / soltar jugador", "Bloquea o libera el control del jugador.")]
public sealed class LockPlayerNode : NarrativeNode
{
    public bool bloquear = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (NarrativeServices.TryGet(out INarrativePlayerControl control, nameof(LockPlayerNode)))
        {
            if (bloquear) control.LockPlayer(typeof(LockPlayerNode));
            else control.UnlockPlayer(typeof(LockPlayerNode));
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[LockPlayerNode] jugador {(bloquear ? "bloqueado" : "desbloqueado")}");
#endif
        onReadyToAdvance?.Invoke();
    }
}
