using System.Collections.Generic;
using System.Text;

namespace NarrativeGraphEditor.Story
{
    /// One line, in plain words, of what a step does: built from what the node describes about
    /// itself (NarrativeNode.Describe), so no step type has to be known here.
    public sealed class StepSummary : INarrativeNodeDescriber
    {
        readonly List<string> _parts = new();

        public static string For(NarrativeNode node, int maxLength = 70)
        {
            if (node == null) return string.Empty;
            var d = new StepSummary();
            try { node.Describe(d); }
            catch { return string.Empty; }

            var sb = new StringBuilder();
            foreach (var p in d._parts)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(p.Trim());
                if (sb.Length >= maxLength) break;
            }
            var text = sb.ToString();
            return text.Length > maxLength ? text.Substring(0, maxLength - 1) + "…" : text;
        }

        public static List<string> Warnings(NarrativeNode node)
        {
            var list = new List<string>();
            if (node == null) return list;
            try { node.CollectWarnings(list); }
            catch (System.Exception e) { list.Add(e.Message); }
            list.AddRange(StoryFlowCheck.For(node));
            return list;
        }

        void Add(string text) { if (!string.IsNullOrWhiteSpace(text)) _parts.Add(text); }

        public void Quest(string questId, string verb, IReadOnlyList<string> steps = null) => Add($"{verb} «{questId}»");
        public void Signal(string key, SignalRole role, string label = null) => Add(label ?? key);
        public void Actor(string actorId, string verb) => Add(string.IsNullOrEmpty(verb) ? actorId : $"{actorId} · {verb}");
        public void Flag(string key, string shownAs = null) => Add(shownAs ?? key);
        public void Anchor(string anchorId) => Add(anchorId);
        public void Asset(UnityEngine.Object asset, string kind, string detail = null) => Add(asset != null ? asset.name : detail);
        public void DialogueLine(string speakerId, bool isPlayer, string textId, string text, string detail = null)
            => Add(string.IsNullOrEmpty(speakerId) ? text : $"{speakerId}: {text}");
        public void Item(UnityEngine.Object item, int amount, string verb) => Add(item != null ? $"{verb} {item.name}{(amount > 1 ? $" ×{amount}" : "")}" : verb);
        public string Text(string locKey, string fallback) => string.IsNullOrEmpty(fallback) ? locKey : fallback;
        public void Line(string text, NarrativeTextTone tone = NarrativeTextTone.Normal) => Add(text);
        public void Speaker(string name) => Add(name);
        public void Field(string label, string value) => Add(string.IsNullOrEmpty(label) ? value : $"{label}: {value}");
        public void Tag(string text) { }
    }
}
