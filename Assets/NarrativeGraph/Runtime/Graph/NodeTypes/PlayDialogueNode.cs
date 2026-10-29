using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reproduce un diálogo desde el grafo (INarrativeDialoguePlayer) y espera a que se cierre. Si se
/// indica un actor, el diálogo se hace con él (cámara y animaciones de conversación).
/// </summary>
[Serializable]
[NarrativeNodeInfo("Diálogo", "Diálogo", "Reproduce un diálogo (con cámara sobre el actor si se indica uno).")]
public sealed class PlayDialogueNode : NarrativeNode
{
    [Header("Diálogo")]
    [Tooltip("El asset de diálogo a reproducir")]
    public NarrativeDialogueAsset dialogue;

    [Header("Actor (opcional)")]
    [Tooltip("Id persistente del actor interlocutor (para cámara y animaciones). " +
             "Si está vacío, el diálogo se muestra sin cámara cinematográfica.")]
    [NarrativeKey(NarrativeKeyKind.Actor)]
    public string npcId;

    [Header("Una sola vez (opcional)")]
    [Tooltip("Si se establece, este diálogo solo se reproducirá una vez. " +
             "Se guarda en el blackboard del grafo.")]
    public string oneShotFlag;

    private string OneShotKey => $"__dialogue_{guid}_{oneShotFlag}_played";

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Asset(dialogue, "diálogo", dialogue != null ? dialogue.Summary : null);
        if (!string.IsNullOrEmpty(npcId)) d.Actor(npcId, "Con");
        if (dialogue != null) dialogue.DescribeLines(d);
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (dialogue == null) warnings.Add("Sin diálogo asignado");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (dialogue == null || dialogue.LineCount == 0)
        {
            Debug.LogWarning("[PlayDialogueNode] Diálogo vacío o no asignado → avanzando");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (!string.IsNullOrEmpty(oneShotFlag) && ctx.Blackboard.Get<bool>(OneShotKey, false))
        {
            NarrativeLog.Info($"[PlayDialogueNode] Diálogo ya reproducido (flag: {oneShotFlag}) → saltando");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (!NarrativeServices.TryGet(out INarrativeDialoguePlayer player, nameof(PlayDialogueNode)))
        {
            onReadyToAdvance?.Invoke();
            return;
        }

        ctx.Runner.StartCoroutine(PlayAndAdvance(ctx, player, onReadyToAdvance));
    }

    private IEnumerator PlayAndAdvance(NarrativeContext ctx, INarrativeDialoguePlayer player, Action onReadyToAdvance)
    {
        Transform speaker = null;
        if (!string.IsNullOrEmpty(npcId))
        {
            speaker = NarrativeServices.Get<INarrativeActorDirectory>()?.FindActor(npcId);
            if (speaker == null)
                Debug.LogWarning($"[PlayDialogueNode] Actor '{npcId}' no encontrado en las escenas cargadas");
        }

        bool completed = false;
        if (!player.TryPlay(dialogue, speaker, () => completed = true))
        {
            Debug.LogError("[PlayDialogueNode] No se pudo abrir el diálogo → avanzando");
            onReadyToAdvance?.Invoke();
            yield break;
        }

        while (!completed)
            yield return null;

        if (!string.IsNullOrEmpty(oneShotFlag))
            ctx.Blackboard.Set(OneShotKey, true);

        yield return null;

        onReadyToAdvance?.Invoke();
    }
}
