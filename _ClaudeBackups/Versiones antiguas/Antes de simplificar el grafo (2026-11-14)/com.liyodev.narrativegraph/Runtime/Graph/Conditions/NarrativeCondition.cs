using System;
using System.Collections.Generic;
using UnityEngine;

/// One thing that must be true about the story: a flag, a quest, a counter, an item or a signal.
/// Steps that choose (such as "By the moment") combine several of these.
[Serializable]
public sealed class NarrativeCondition
{
    public enum Kind
    {
        FlagIsSet,
        FlagIsNotSet,
        QuestIs,
        Counter,
        HasItem,
        SignalRaised,
    }

    public enum Comparison { AtLeast, AtMost, Exactly }

    public Kind kind = Kind.FlagIsSet;

    [Tooltip("Flag, quest, counter or signal this condition looks at.")]
    public string key;

    [Tooltip("Quest state required (QuestIs).")]
    public NarrativeQuestState questState = NarrativeQuestState.Completed;

    [Tooltip("How the counter or the item amount is compared.")]
    public Comparison comparison = Comparison.AtLeast;

    [Tooltip("Number to compare with (Counter, HasItem).")]
    public int number = 1;

    [Tooltip("Item to look for (HasItem).")]
    public NarrativeItemAsset item;

    public bool Evaluate(NarrativeContext ctx)
    {
        switch (kind)
        {
            case Kind.FlagIsSet: return NarrativeFlags.Has(key);
            case Kind.FlagIsNotSet: return !NarrativeFlags.Has(key);
            case Kind.QuestIs:
                var quests = NarrativeServices.Get<IQuestService>();
                var state = quests != null && !string.IsNullOrWhiteSpace(key) ? quests.GetState(key) : NarrativeQuestState.NotStarted;
                return state == questState;
            case Kind.Counter: return Compare(NarrativeCounters.Get(key));
            case Kind.HasItem:
                var inventory = NarrativeServices.Get<INarrativeInventory>();
                return item != null && inventory != null && inventory.IsAvailable && Compare(inventory.Count(item));
            case Kind.SignalRaised: return ctx?.Signals != null && !string.IsNullOrWhiteSpace(key) && ctx.Signals.HasEverRaised(key);
            default: return false;
        }
    }

    bool Compare(int value) => comparison switch
    {
        Comparison.AtLeast => value >= number,
        Comparison.AtMost => value <= number,
        _ => value == number,
    };

    /// Leaves the projected world as this condition says it was (used when a route went through it).
    public void Project(INarrativeStateWriter state)
    {
        if (state == null) return;
        switch (kind)
        {
            case Kind.FlagIsSet: if (!string.IsNullOrWhiteSpace(key)) state.SetFlag(key, true); break;
            case Kind.FlagIsNotSet: if (!string.IsNullOrWhiteSpace(key)) state.SetFlag(key, false); break;
            case Kind.QuestIs:
                if (string.IsNullOrWhiteSpace(key)) break;
                if (questState == NarrativeQuestState.Completed) state.CompleteQuest(key);
                else if (questState != NarrativeQuestState.NotStarted) state.StartQuest(key);
                break;
            case Kind.Counter:
                if (!string.IsNullOrWhiteSpace(key) && comparison != Comparison.AtMost)
                    state.SetFact(NarrativeCoreFacts.Counter, key, NarrativeCoreFacts.CounterSet(number));
                break;
            case Kind.HasItem:
                if (item != null && comparison != Comparison.AtMost) state.EnsureItem(item.ItemId, number);
                break;
            case Kind.SignalRaised: if (!string.IsNullOrWhiteSpace(key)) state.MarkSignalRaised(key); break;
        }
    }

    public void Describe(INarrativeNodeDescriber d)
    {
        switch (kind)
        {
            case Kind.FlagIsSet:
            case Kind.FlagIsNotSet: d.Flag(key, ToString()); break;
            case Kind.QuestIs: d.Quest(key, ToString()); break;
            case Kind.HasItem: d.Item(item, number, ToString()); break;
            case Kind.SignalRaised: d.Signal(key, SignalRole.Ninguno); break;
            default: d.Line(ToString()); break;
        }
    }

    public override string ToString()
    {
        string cmp = comparison switch { Comparison.AtLeast => "≥", Comparison.AtMost => "≤", _ => "=" };
        return kind switch
        {
            Kind.FlagIsSet => $"{key}",
            Kind.FlagIsNotSet => $"no {key}",
            Kind.QuestIs => $"{key}: {questState}",
            Kind.Counter => $"{key} {cmp} {number}",
            Kind.HasItem => $"{(item != null ? item.name : "?")} {cmp} {number}",
            Kind.SignalRaised => $"{key} ✓",
            _ => string.Empty,
        };
    }

    public static bool All(IReadOnlyList<NarrativeCondition> conditions, NarrativeContext ctx)
    {
        if (conditions == null) return true;
        foreach (var c in conditions)
            if (c != null && !c.Evaluate(ctx)) return false;
        return true;
    }
}
