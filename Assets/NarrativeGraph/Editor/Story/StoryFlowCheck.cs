using System.Collections.Generic;
using System.Linq;

namespace NarrativeGraphEditor.Story
{
    /// Problems in how a chapter flows, as opposed to a step's own fields: steps the story never
    /// reaches and decisions with a way out that leads nowhere.
    public static class StoryFlowCheck
    {
        static readonly Dictionary<string, List<string>> s_issues = new();
        static NarrativeGraph s_graph;

        /// Checks the graph again. The editor calls it after every change.
        public static void Run(NarrativeGraph graph, StoryStructure structure)
        {
            s_graph = graph;
            s_issues.Clear();
            if (graph?.nodes == null || structure == null) return;

            var reachable = Reachable(graph);
            bool hasStart = !string.IsNullOrEmpty(graph.startNodeGuid) && graph.FindNode(graph.startNodeGuid) != null;

            foreach (var node in graph.nodes)
            {
                if (node == null || node.IsAnnotation) continue;

                if (hasStart && !reachable.Contains(node.guid))
                    Add(node, StoryText.T("La historia nunca llega a este paso: no sale de ningún otro conectado al inicio.",
                                          "The story never reaches this step: no step connected to the start leads here."));

                // Only real decisions: a fixed way out (a "Failed" output, say) may stay unconnected.
                var ports = node.GetOutputPorts();
                if (ports != null && ports.Length > 1 && node.ProjectionPort == NarrativeNode.ProjectionStops)
                {
                    var loose = ports.Where((p, i) => node.GetOutputGuid(i) == null).ToList();
                    if (loose.Count > 0 && loose.Count < ports.Length)
                        Add(node, StoryText.T($"La salida «{string.Join("», «", loose)}» no lleva a ningún paso: ahí acaba la historia.",
                                              $"The “{string.Join("”, “", loose)}” way out leads nowhere: the story ends there."));
                }
            }
        }

        /// Flow problems of a step (empty if none, or if the step belongs to another graph).
        public static IReadOnlyList<string> For(NarrativeNode node)
            => node != null && s_issues.TryGetValue(node.guid, out var list) ? list : (IReadOnlyList<string>)System.Array.Empty<string>();

        public static bool IsFor(NarrativeGraph graph) => graph == s_graph;

        static void Add(NarrativeNode node, string text)
        {
            if (!s_issues.TryGetValue(node.guid, out var list)) s_issues[node.guid] = list = new List<string>();
            list.Add(text);
        }

        static HashSet<string> Reachable(NarrativeGraph graph)
        {
            var seen = new HashSet<string>();
            if (string.IsNullOrEmpty(graph.startNodeGuid)) return seen;
            var stack = new Stack<string>();
            stack.Push(graph.startNodeGuid);
            while (stack.Count > 0)
            {
                var guid = stack.Pop();
                if (string.IsNullOrEmpty(guid) || !seen.Add(guid)) continue;
                var node = graph.FindNode(guid);
                if (node?.outputs == null) continue;
                foreach (var o in node.outputs) stack.Push(o);
            }
            return seen;
        }
    }
}
