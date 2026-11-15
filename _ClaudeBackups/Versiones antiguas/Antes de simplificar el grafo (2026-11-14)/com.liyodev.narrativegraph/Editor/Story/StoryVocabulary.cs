using System.Collections.Generic;
using System.Linq;

namespace NarrativeGraphEditor.Story
{
    /// The words of a chapter: characters, quests, items, flags, signals and counters its steps use,
    /// with how many steps use each one.
    public sealed class StoryVocabulary
    {
        public sealed class Group
        {
            public string Title;
            public readonly SortedDictionary<string, int> Uses = new(System.StringComparer.OrdinalIgnoreCase);
        }

        public readonly List<Group> Groups = new();

        public static StoryVocabulary Build(NarrativeGraph graph)
        {
            var actors = new Group { Title = StoryText.T("Personajes", "Characters") };
            var quests = new Group { Title = StoryText.T("Misiones", "Quests") };
            var items = new Group { Title = StoryText.T("Objetos", "Items") };
            var flags = new Group { Title = StoryText.T("Datos de partida", "Story flags") };
            var counters = new Group { Title = StoryText.T("Contadores", "Counters") };
            var signals = new Group { Title = StoryText.T("Señales", "Signals") };

            if (graph?.nodes != null)
                foreach (var node in graph.nodes)
                {
                    if (node == null) continue;
                    NarrativeNodeReferences refs;
                    try { refs = NarrativeNodeReferences.Of(node); }
                    catch (System.Exception) { continue; }

                    var seen = new HashSet<(Group, string)>();
                    void Use(Group g, string key)
                    {
                        if (string.IsNullOrWhiteSpace(key) || !seen.Add((g, key))) return;
                        g.Uses.TryGetValue(key, out var n);
                        g.Uses[key] = n + 1;
                    }

                    foreach (var a in refs.Actors) Use(actors, a.ActorId);
                    foreach (var q in refs.Quests) Use(quests, q.QuestId);
                    foreach (var i in refs.Items) Use(items, i.Item != null ? i.Item.name : null);
                    foreach (var f in refs.Flags) Use(flags, f);
                    foreach (var s in refs.Signals) Use(signals, s.Key);
                    if (node is ChangeCounterNode c) Use(counters, c.counter);
                    if (node is ChooseByMomentNode m)
                        foreach (var cond in m.options.Where(o => o?.conditions != null).SelectMany(o => o.conditions))
                            if (cond != null && cond.kind == NarrativeCondition.Kind.Counter) Use(counters, cond.key);
                }

            var v = new StoryVocabulary();
            foreach (var g in new[] { actors, quests, items, flags, counters, signals })
                if (g.Uses.Count > 0) v.Groups.Add(g);
            return v;
        }
    }
}
