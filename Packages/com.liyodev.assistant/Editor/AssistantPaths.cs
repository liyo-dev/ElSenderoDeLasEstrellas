using System.IO;
using UnityEditor;

namespace Liyodev.Assistant
{
    /// Dónde vive el paquete, para cargar su hoja de estilos tanto si está instalado como paquete
    /// embebido o desde el registro, como si se importó como una carpeta normal.
    public static class AssistantPaths
    {
        private static string s_editor;
        private static bool s_buscado;

        public static string Editor
        {
            get
            {
                if (s_buscado) return s_editor;
                s_buscado = true;
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AssistantPaths).Assembly);
                if (package != null && !string.IsNullOrEmpty(package.assetPath))
                {
                    s_editor = package.assetPath.TrimEnd('/') + "/Editor";
                    return s_editor;
                }

                foreach (var guid in AssetDatabase.FindAssets("Assistant t:StyleSheet"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("/Assistant.uss", System.StringComparison.OrdinalIgnoreCase))
                    {
                        s_editor = Path.GetDirectoryName(path)?.Replace('\\', '/');
                        break;
                    }
                }
                return s_editor;
            }
        }

        public static string StyleSheet
        {
            get
            {
                string editor = Editor;
                return string.IsNullOrEmpty(editor) ? null : editor + "/Assistant.uss";
            }
        }
    }
}
