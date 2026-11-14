using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Picks what happens according to the state of the story: each option lists what has to be true,
/// and the step takes the most specific option that holds (the one that asks for most). If none
/// holds, it goes out through "Otherwise". Good for an NPC who greets you differently as the story
/// moves on, or a scene with variations.
[Serializable]
[NarrativeNodeInfo("Flujo", "Según el momento", "Elige la salida cuyas condiciones se cumplen; si varias se cumplen, la más concreta.")]
[SavePoint("Solo consulta estado; seguro para guardar")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
public sealed class ChooseByMomentNode : NarrativeNode, INarrativeRouteEffect
{
    [Serializable]
    public sealed class Option
    {
        [Tooltip("Name of this way out, shown on the card.")]
        public string label = "Option";
        public List<NarrativeCondition> conditions = new();
    }

    public List<Option> options = new() { new Option { label = "A" } };

    public const string OtherwiseLabel = "Si no";

    public override string[] GetOutputPorts()
        => options.Select((o, i) => string.IsNullOrWhiteSpace(o?.label) ? $"{i + 1}" : o.label)
                  .Append(OtherwiseLabel).ToArray();

    int OtherwisePort => options.Count;

    /// Index of the option that applies now, or the "otherwise" port.
    public int Choose(NarrativeContext ctx)
    {
        int best = OtherwisePort, bestCount = -1;
        for (int i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (option == null || !NarrativeCondition.All(option.conditions, ctx)) continue;
            int count = option.conditions?.Count ?? 0;
            if (count > bestCount) { best = i; bestCount = count; }
        }
        return best;
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        int port = Choose(ctx);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ChooseByMomentNode:{guid}] → {GetOutputPorts()[port]}");
#endif
        AdvanceThrough(ctx, ready, port);
    }

    /// The option taken tells what was true at that point.
    public void ProjectRoute(INarrativeStateWriter state, int chosenPort)
    {
        if (chosenPort < 0 || chosenPort >= options.Count) return;
        var option = options[chosenPort];
        if (option?.conditions == null) return;
        foreach (var c in option.conditions) c?.Project(state);
    }

    public override void Describe(INarrativeNodeDescriber d)
    {
        foreach (var option in options)
        {
            if (option == null) continue;
            var text = option.conditions == null || option.conditions.Count == 0
                ? "siempre"
                : string.Join(" y ", option.conditions.Where(c => c != null).Select(c => c.ToString()));
            d.Field(option.label, text);
            if (option.conditions != null)
                foreach (var c in option.conditions) c?.Describe(d);
        }
    }

    public override string DescribeDecision()
        => $"Según el momento \"{displayTitle}\" (guid {guid}): elige la opción más concreta que se cumple. Salidas: {string.Join(" / ", GetOutputPorts())}.";

    public override void CollectWarnings(List<string> warnings)
    {
        if (options.Count == 0) warnings.Add("Sin opciones: siempre sale por «Si no»");
        foreach (var option in options)
            if (option?.conditions != null && option.conditions.Any(c => c != null && c.kind != NarrativeCondition.Kind.HasItem && string.IsNullOrWhiteSpace(c.key)))
                warnings.Add($"«{option.label}»: hay una condición sin clave");
    }
}
