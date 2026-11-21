using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// How a graph reads as a story: its sections in order, the steps of each section in story
    /// order, and the number of every step ("2.3" = third step of the second section).
    public sealed class StoryStructure
    {
        public sealed class Section
        {
            public string Name;
            public int Index;
            public readonly List<NarrativeNode> Steps = new();
        }

        public readonly List<Section> Sections = new();
        public readonly Dictionary<string, string> NumberOf = new();
        public readonly Dictionary<string, Section> SectionOf = new();
        /// All steps, in story order (reachable from the start first, then the loose ones by position).
        public readonly List<NarrativeNode> StoryOrder = new();

        public static string SectionKey(NarrativeNode node) => node?.Section?.Trim() ?? string.Empty;

        public static string DisplayName(string sectionKey)
            => string.IsNullOrEmpty(sectionKey) ? StoryText.T("General", "General") : sectionKey;

        /// The section a step belongs to, written or inherited.
        public string SectionKeyOf(NarrativeNode node)
            => node != null && SectionOf.TryGetValue(node.Guid, out var sec) ? sec.Name : SectionKey(node);

        /// The section a step opens by itself (written, or a named checkpoint); null if it inherits.
        public static string OwnSection(NarrativeNode node)
        {
            var key = SectionKey(node);
            if (key.Length > 0) return key;
            if (node.OpensSection && !string.IsNullOrWhiteSpace(node.DisplayTitle)) return node.DisplayTitle.Trim();
            return null;
        }

        public string Number(NarrativeNode node)
            => node != null && NumberOf.TryGetValue(node.Guid, out var n) ? n : string.Empty;

        public static StoryStructure Build(NarrativeGraph graph)
        {
            var s = new StoryStructure();
            if (graph == null || graph.nodes == null) return s;

            var byGuid = new Dictionary<string, NarrativeNode>();
            foreach (var n in graph.nodes)
                if (n != null && !string.IsNullOrEmpty(n.Guid) && !byGuid.ContainsKey(n.Guid)) byGuid[n.Guid] = n;

            // A step without its own section continues the section of the step that leads to it.
            // A checkpoint without one opens a new section named after it.
            var effective = new Dictionary<string, string>();
            var seen = new HashSet<string>();
            var queue = new Queue<string>();
            if (!string.IsNullOrEmpty(graph.startNodeGuid) && byGuid.ContainsKey(graph.startNodeGuid))
            {
                queue.Enqueue(graph.startNodeGuid);
                seen.Add(graph.startNodeGuid);
                effective[graph.startNodeGuid] = string.Empty;
            }
            while (queue.Count > 0)
            {
                var node = byGuid[queue.Dequeue()];
                if (node.IsAnnotation) continue;
                var key = OwnSection(node) ?? effective[node.Guid];
                effective[node.Guid] = key;
                s.StoryOrder.Add(node);
                if (node.Outputs == null) continue;
                foreach (var g in node.Outputs)
                    if (!string.IsNullOrEmpty(g) && byGuid.ContainsKey(g) && seen.Add(g))
                    {
                        effective[g] = key;
                        queue.Enqueue(g);
                    }
            }
            foreach (var n in byGuid.Values.Where(n => !seen.Contains(n.Guid) && !n.IsAnnotation)
                                           .OrderBy(n => n.Position.y).ThenBy(n => n.Position.x))
            {
                effective[n.Guid] = OwnSection(n) ?? string.Empty;
                s.StoryOrder.Add(n);
            }

            var sections = new Dictionary<string, Section>();
            foreach (var node in s.StoryOrder)
            {
                var key = effective[node.Guid];
                if (!sections.TryGetValue(key, out var section))
                {
                    section = new Section { Name = key, Index = s.Sections.Count };
                    sections[key] = section;
                    s.Sections.Add(section);
                }
                section.Steps.Add(node);
                s.SectionOf[node.Guid] = section;
                s.NumberOf[node.Guid] = $"{section.Index + 1}.{section.Steps.Count}";
            }
            return s;
        }
    }
}
