using System;
using UnityEngine;

/// Adds to a story counter, or sets it ("visits to the shop", "clues found").
[Serializable]
[NarrativeNodeInfo("Flujo", "Contador", "Suma a un contador de la historia o le da un valor.")]
[SavePoint("Instantáneo")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
public sealed class ChangeCounterNode : NarrativeNode, INarrativeStateEffect
{
    public enum Mode { Add, Set }

    [Tooltip("Name of the counter.")]
    public string counter;

    public Mode mode = Mode.Add;

    [Tooltip("Amount to add (can be negative) or value to set.")]
    public int amount = 1;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (mode == Mode.Add) NarrativeCounters.Add(counter, amount);
        else NarrativeCounters.Set(counter, amount);
        ready?.Invoke();
    }

    public void Project(INarrativeStateWriter state)
    {
        if (string.IsNullOrWhiteSpace(counter)) return;
        state.SetFact(NarrativeCoreFacts.Counter, counter,
            mode == Mode.Add ? NarrativeCoreFacts.CounterAdd(amount) : NarrativeCoreFacts.CounterSet(amount));
    }

    public override void Describe(INarrativeNodeDescriber d)
        => d.Line(mode == Mode.Add ? $"{counter} {(amount >= 0 ? "+" : "−")}{Math.Abs(amount)}" : $"{counter} = {amount}", NarrativeTextTone.Strong);

    public override void CollectWarnings(System.Collections.Generic.List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(counter)) warnings.Add("Falta el nombre del contador");
    }
}
