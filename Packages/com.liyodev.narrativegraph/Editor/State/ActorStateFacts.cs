using System.Collections.Generic;
using System.Linq;
using NarrativeGraphEditor.Story;
using UnityEditor;

namespace NarrativeGraphEditor
{
    /// Qué pieza «Personaje desde aquí» vale para cada personaje en un punto de la historia, a
    /// partir de lo proyectado (hechos ActorState / ActorUsual). Lo «de siempre» que ponen los
    /// grafos nada más empezar (p. ej. el de secundarias) cuenta aunque no esté en el camino.
    public static class ActorStateFacts
    {
        public sealed class Entry
        {
            public string Actor;
            public ActorStateNode Node;
            public NarrativeGraph Graph;
            /// Paso que la dejó (guid), o null si es de un grafo al empezar.
            public string Source;
        }

        public static List<Entry> From(RecordingStateWriter story)
        {
            var nodes = AllPieces();
            var current = Layer(story, NarrativeCoreFacts.ActorState);
            var usual = Layer(story, NarrativeCoreFacts.ActorUsual);
            var result = new List<Entry>();
            var actors = new SortedSet<string>(current.Keys.Concat(usual.Keys));
            foreach (var s in AtStart(nodes)) actors.Add(s.node.actorId);

            foreach (var actor in actors)
            {
                (string value, string source) pick = default;
                if (current.TryGetValue(actor, out var c) && !string.IsNullOrEmpty(c.value)) pick = c;
                else if (usual.TryGetValue(actor, out var u) && !string.IsNullOrEmpty(u.value)) pick = u;
                if (pick.value != null && nodes.TryGetValue(pick.value, out var found))
                {
                    result.Add(new Entry { Actor = actor, Node = found.node, Graph = found.graph, Source = pick.source });
                    continue;
                }
                var start = AtStart(nodes).LastOrDefault(s => s.node.actorId == actor);
                if (start.node != null) result.Add(new Entry { Actor = actor, Node = start.node, Graph = start.graph });
            }
            return result;
        }

        /// «en Eldran_SalidaDelPueblo · dice: DLG_X, DLG_Y».
        public static string Describe(ActorStateNode n)
        {
            var parts = new List<string>();
            parts.Add(n.presence switch
            {
                ActorPresence.AtMark => StoryText.T($"en {n.markId}", $"at {n.markId}"),
                ActorPresence.Away => StoryText.T("no está", "away"),
                _ => StoryText.T("donde esté", "wherever they are"),
            });
            var lines = n.lines?.Where(l => l != null).Select(l => l.name).ToList();
            if (lines != null && lines.Count > 0) parts.Add(StoryText.T("si le hablas: ", "if you talk: ") + string.Join(", ", lines));
            return string.Join(" · ", parts);
        }

        static Dictionary<string, (string value, string source)> Layer(RecordingStateWriter story, string category)
            => story != null && story.Facts.TryGetValue(category, out var facts)
                ? facts.ToDictionary(f => f.Key, f => f.Value)
                : new Dictionary<string, (string value, string source)>();

        static Dictionary<string, (ActorStateNode node, NarrativeGraph graph)> AllPieces()
            => StoryCache.Get("actorStatePieces", () =>
            {
                var map = new Dictionary<string, (ActorStateNode, NarrativeGraph)>();
                foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
                {
                    var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(guid));
                    if (g?.nodes == null) continue;
                    foreach (var n in g.nodes)
                        if (n is ActorStateNode a && !string.IsNullOrEmpty(a.guid)) map[a.guid] = (a, g);
                }
                return map;
            });

        /// Piezas «lo de siempre» que un grafo pone nada más empezar (antes de cualquier otro paso).
        static List<(ActorStateNode node, NarrativeGraph graph)> AtStart(Dictionary<string, (ActorStateNode node, NarrativeGraph graph)> pieces)
            => StoryCache.Get("actorStateAtStart", () =>
            {
                var list = new List<(ActorStateNode, NarrativeGraph)>();
                foreach (var graph in pieces.Values.Select(p => p.graph).Distinct())
                {
                    var node = graph.FindNode(graph.startNodeGuid);
                    var seen = new HashSet<string>();
                    while (node != null && node.outputs != null && node.outputs.Count == 1 && seen.Add(node.guid))
                    {
                        node = graph.FindNode(node.outputs[0]);
                        if (node is not ActorStateNode a) break;
                        if (a.layer == ActorStateNode.Layer.Usual) list.Add((a, graph));
                    }
                }
                return list;
            });
    }
}
