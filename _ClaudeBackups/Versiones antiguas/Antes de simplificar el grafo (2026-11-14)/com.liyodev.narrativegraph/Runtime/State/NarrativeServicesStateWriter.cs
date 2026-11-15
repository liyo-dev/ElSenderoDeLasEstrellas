using System.Collections.Generic;
using UnityEngine;

/// Writes a projected story state straight into the running game through its connections
/// (NarrativeServices). Used by the basic quick start; games with their own save format usually
/// write their save data instead.
public sealed class NarrativeServicesStateWriter : INarrativeStateWriter
{
    public readonly List<string> Notes = new();

    public bool GetFlag(string key) => NarrativeFlags.Has(key);
    public void SetFlag(string key, bool value) => NarrativeFlags.Set(key, value);

    public void StartQuest(string questId) => NarrativeServices.Get<IQuestService>()?.StartQuest(questId);

    public void CompleteQuestSteps(string questId, IReadOnlyList<string> stepConditionIds, IReadOnlyList<int> stepIndices)
    {
        var quests = NarrativeServices.Get<IQuestService>();
        if (quests == null) return;
        quests.StartQuest(questId);
        if (stepConditionIds != null) foreach (var id in stepConditionIds) quests.CompleteStepByConditionId(questId, id);
        if (stepIndices != null) foreach (var i in stepIndices) quests.CompleteStep(questId, i);
    }

    public void CompleteQuest(string questId) => NarrativeServices.Get<IQuestService>()?.Complete(questId);

    // Marks it as already raised without firing it: nobody should react twice to the past.
    public void MarkSignalRaised(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        DefaultNarrativeSignals.EnsureInstance().RestoreEverRaised(new[] { key });
    }

    public void AddItem(string itemId, int amount) => Note($"Item '{itemId}' ×{amount}: give it in the game's own start if the test needs it.");
    public void RemoveItem(string itemId, int amount) { }
    public void EnsureItem(string itemId, int amount) => AddItem(itemId, amount);

    public void SetFact(string category, string key, string value)
    {
        if (category == NarrativeCoreFacts.Counter) NarrativeCounters.Set(key, NarrativeCoreFacts.ApplyCounter(NarrativeCounters.Get(key), value));
        else if (category == NarrativeCoreFacts.ActorState || category == NarrativeCoreFacts.ActorUsual) NarrativeActorStates.SetFromFact(category, key, value);
        else Note($"{category}/{key} = {value}: the basic start does not apply it.");
    }

    public void PlaceActor(string actorId, NarrativeLocation location)
    {
        var placement = NarrativeServices.Get<INarrativeActorPlacement>();
        if (placement == null || !location.IsValid || !placement.TryPlace(actorId, location.Mark, false, true))
            Note($"'{actorId}' could not be placed at '{location.Mark}'.");
    }

    public void SetActorActive(string actorId, bool active) => NarrativeServices.Get<INarrativeActorPresence>()?.TrySetPresent(actorId, active);

    public void Note(string message) { if (!string.IsNullOrWhiteSpace(message)) Notes.Add(message); }

    public bool TryGetExtension<T>(out T extension) where T : class { extension = null; return false; }
}
