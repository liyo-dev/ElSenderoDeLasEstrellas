using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Piezas de texto comunes de los paneles del editor (títulos pequeños, notas, avisos).
    public static class StoryUI
    {
        public static Label Kicker(string text) => Styled(text, "story-kicker");
        public static Label Muted(string text) => Styled(text, "story-muted");

        public static Label Notice(string text, bool ok)
        {
            var l = Styled(text, "story-notice");
            l.AddToClassList(ok ? "is-ok" : "is-warning");
            return l;
        }

        static Label Styled(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            return l;
        }
    }
}
