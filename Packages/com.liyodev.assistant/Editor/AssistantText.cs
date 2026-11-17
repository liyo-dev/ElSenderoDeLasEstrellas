using UnityEditor;

namespace Liyodev.Assistant
{
    /// Idioma de la interfaz del asistente. Lee la misma preferencia que el editor del grafo para
    /// que las dos herramientas hablen igual; si algún día hay una preferencia propia, se pone
    /// delante y esta queda como respaldo.
    public static class AssistantText
    {
        const string Key = "NarrativeGraph.Editor.English";

        public static bool English
        {
            get => EditorPrefs.GetBool(Key, false);
            set => EditorPrefs.SetBool(Key, value);
        }

        public static string T(string spanish, string english) => English ? english : spanish;
    }
}
