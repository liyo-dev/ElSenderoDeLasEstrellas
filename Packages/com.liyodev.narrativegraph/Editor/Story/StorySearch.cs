using System;
using System.Collections.Generic;
using UnityEditor;

namespace NarrativeGraphEditor.Story
{
    /// Which steps contain a text: title, step type, number and summary. Shared by the canvas,
    /// the outline and the lookup in the other graphs of the project.
    public static class StorySearch
    {
        public readonly struct GraphHits
        {
            public readonly NarrativeGraph Graph;
            public readonly int Count;
            public GraphHits(NarrativeGraph graph, int count) { Graph = graph; Count = count; }
        }

        public static bool Matches(NarrativeNode node, string query, string number = null)
        {
            if (node == null || string.IsNullOrEmpty(query)) return false;
            return Has(node.displayTitle, query)
                || Has(NarrativeNodeCatalog.TitleOf(node), query)
                || Has(number, query)
                || Has(StepSummary.For(node, 400), query);
        }

        /// How many steps of every other graph contain the text, ordered by name.
        /// Step numbers are left out: "2.1" means something different in each graph.
        public static List<GraphHits> InOtherGraphs(NarrativeGraph current, string query)
        {
            var found = new List<GraphHits>();
            if (string.IsNullOrEmpty(query)) return found;
            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var graph = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (graph == null || graph == current) continue;
                int count = 0;
                foreach (var node in graph.nodes)
                    if (Matches(node, query)) count++;
                if (count > 0) found.Add(new GraphHits(graph, count));
            }
            found.Sort((a, b) => string.Compare(a.Graph.name, b.Graph.name, StringComparison.OrdinalIgnoreCase));
            return found;
        }

        static bool Has(string text, string query)
            => !string.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
