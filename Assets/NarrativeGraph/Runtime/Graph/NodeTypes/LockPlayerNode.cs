using System;
using UnityEngine;
using Sendero.UI;

/// <summary>
/// Bloquea o desbloquea el movimiento del jugador empujando/sacando ActionMode.Cinematic.
/// Bloquea: Move, Sprint, Jump, Interact.
/// Colocar un nodo con bloquear=true antes del momento crítico y otro con bloquear=false después.
///
/// También oculta el HUD mientras el jugador está bloqueado. El bloqueo y el desbloqueo son nodos
/// distintos, así que piden y sueltan el HUD con la misma clave (el tipo del nodo). Ver INC-538.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Jugador", "Bloquear / soltar jugador", "Bloquea o libera el control del jugador.")]
public sealed class LockPlayerNode : NarrativeNode
{
    public bool bloquear = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (PlayerService.TryGetPlayer(out var playerGo, false))
        {
            var pam = playerGo.GetComponent<PlayerActionManager>();
            if (pam != null)
            {
                if (bloquear)
                    pam.PushMode(ActionMode.Cinematic);
                else
                    pam.PopMode(ActionMode.Cinematic);
            }
        }

        if (bloquear)
            PlayerHUDV2.Instance?.HideHUD(typeof(LockPlayerNode));
        else
            PlayerHUDV2.Instance?.ShowHUD(typeof(LockPlayerNode));

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[LockPlayerNode] jugador {(bloquear ? "bloqueado" : "desbloqueado")} (ActionMode.Cinematic), HUD {(bloquear ? "oculto" : "restaurado")}");
#endif
        onReadyToAdvance?.Invoke();
    }
}
