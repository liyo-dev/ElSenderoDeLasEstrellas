using System.IO;
using UnityEditor;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor
{
    /// Abre el manual que viaja con el paquete, sin que el usuario tenga que buscarlo.
    public static class NarrativeHelpMenu
    {
        const string ManualPath = "Packages/com.liyodev.narrativegraph/Documentation/Manual de usuario.pdf";

        [MenuItem("Liyodev/Grafo narrativo/Ayuda/Abrir el manual", priority = 200)]
        public static void OpenManual()
        {
            var full = Path.GetFullPath(ManualPath);
            if (!File.Exists(full))
            {
                EditorUtility.DisplayDialog("Grafo narrativo", "No encuentro el manual en:\n" + full, "Vale");
                return;
            }
            Application.OpenURL("file:///" + full.Replace('\\', '/'));
        }
    }
}
