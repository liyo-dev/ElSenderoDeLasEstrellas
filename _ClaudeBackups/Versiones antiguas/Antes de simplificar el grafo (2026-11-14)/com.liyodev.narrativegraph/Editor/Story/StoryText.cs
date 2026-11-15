using UnityEditor;

namespace NarrativeGraphEditor.Story
{
    /// Language of the story editor (per user). Every visible string goes through T.
    public static class StoryText
    {
        const string PrefKey = "NarrativeGraph.Editor.English";

        public static bool English
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        public static string T(string spanish, string english) => English ? english : spanish;
    }
}
