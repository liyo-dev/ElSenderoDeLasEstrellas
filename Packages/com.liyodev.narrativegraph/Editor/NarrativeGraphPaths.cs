namespace NarrativeGraphEditor
{
    /// Dónde está instalado el grafo (como paquete o dentro de Assets), para cargar sus estilos y documentos.
    public static class NarrativeGraphPaths
    {
        public static string Root
        {
            get
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(NarrativeGraphPaths).Assembly);
                return info != null ? info.assetPath : "Assets/NarrativeGraph";
            }
        }

        public static string StoryStyles => Root + "/Editor/Story/StoryEditor.uss";
        public static string FieldStyles => Root + "/Editor/Story/StoryFields.uss";
        public static string Manual => Root + "/Documentation/Manual de usuario.pdf";
        public static string McpReadme => Root + "/Tools/mcp/README.md";
    }
}
