using System.Collections.Generic;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// Las líneas de un diálogo en claro (quién, emoción, texto), leídas con DescribeLines: el
    /// kit no conoce el formato de las líneas de cada juego.
    public static class DialoguePreview
    {
        public sealed class Line
        {
            public string Speaker;
            public bool IsPlayer;
            public string Text;
            public string Detail;
        }

        public static List<Line> Of(NarrativeDialogueAsset dialogue)
        {
            var c = new Collector();
            if (dialogue != null) dialogue.DescribeLines(c);
            return c.Lines;
        }

        sealed class Collector : INarrativeNodeDescriber
        {
            public readonly List<Line> Lines = new();

            public void DialogueLine(string speakerId, bool isPlayer, string textId, string text, string detail = null)
                => Lines.Add(new Line
                {
                    Speaker = NarrativeProjectIndex.SpeakerName(speakerId, isPlayer),
                    IsPlayer = isPlayer,
                    Text = NarrativeProjectIndex.LineText(textId, text),
                    Detail = detail,
                });

            public void Quest(string questId, string verb, IReadOnlyList<string> steps = null) { }
            public void Signal(string key, SignalRole role, string label = null) { }
            public void Actor(string actorId, string verb) { }
            public void Flag(string key, string shownAs = null) { }
            public void Anchor(string anchorId) { }
            public void Asset(Object asset, string kind, string detail = null) { }
            public void Item(Object item, int amount, string verb) { }
            public string Text(string locKey, string fallback) => NarrativeProjectIndex.Loc(locKey, fallback);
            public void Line(string text, NarrativeTextTone tone = NarrativeTextTone.Normal) { }
            public void Speaker(string name) { }
            public void Field(string label, string value) { }
            public void Tag(string text) { }
        }
    }
}
