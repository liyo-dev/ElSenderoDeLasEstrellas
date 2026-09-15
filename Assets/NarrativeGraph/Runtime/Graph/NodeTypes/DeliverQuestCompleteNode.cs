// DeliverQuestCompleteNode.cs
using System;

[Obsolete("DeliverQuestCompleteNode está obsoleto. Usa la lógica de QuestService/Signals directamente o nodos alternativos.")]
[Serializable]
[NarrativeNodeInfo("Quests", "Entregar quest (legacy)", "")]
public sealed class DeliverQuestCompleteNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Quest)]
    public string questId;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        ctx.Signals.CompleteQuest(questId);
        onReadyToAdvance?.Invoke();
    }
}
