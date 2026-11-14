using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Sendero.Narrative.Editor
{
    /// Exporta el grafo narrativo (sin los capítulos del Sendero) a un .unitypackage y lo prueba en
    /// un proyecto de Unity aparte (`../NarrativeGraphPrueba`, se crea la primera vez), en segundo plano: importa el paquete, monta el ejemplo «El faro» y
    /// deja los logs en Logs/claude/kit_prueba_*.log. Luego el proyecto se abre desde Unity Hub.
    public static class ExportarElKitDelGrafo
    {
        static readonly string[] Carpetas =
        {
            "Assets/NarrativeGraph/Runtime", "Assets/NarrativeGraph/Editor", "Assets/NarrativeGraph/Samples",
            "Assets/NarrativeGraph/Documentation", "Assets/NarrativeGraph/Tools",
        };

        const string Paquete = "Builds/Kits/NarrativeGraph.unitypackage";
        const string ProyectoDePrueba = "../NarrativeGraphPrueba";

        [MenuItem("El Sendero/Kits/Grafo narrativo: exportar el .unitypackage")]
        public static string Exportar()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Paquete));
            AssetDatabase.ExportPackage(Carpetas.Where(AssetDatabase.IsValidFolder).ToArray(), Paquete, ExportPackageOptions.Recurse);
            var ruta = Path.GetFullPath(Paquete);
            Debug.Log("[Kit del grafo] Exportado: " + ruta);
            return ruta;
        }

        [MenuItem("El Sendero/Kits/Grafo narrativo: exportar y probar en un proyecto nuevo")]
        public static void ExportarYProbar()
        {
            var paquete = Exportar();
            // Si el proyecto de prueba ya existe, se reimporta el paquete encima.
            var proyecto = Path.GetFullPath(ProyectoDePrueba);
            if (!Directory.Exists(proyecto))
            {
                Directory.CreateDirectory(Path.Combine(proyecto, "Assets"));
                Directory.CreateDirectory(Path.Combine(proyecto, "Packages"));
                File.WriteAllText(Path.Combine(proyecto, "Packages", "manifest.json"), Manifiesto());
            }

            var logs = Path.GetFullPath("Logs/claude");
            Directory.CreateDirectory(logs);
            var unity = EditorApplication.applicationPath;
            var cmd = new StringBuilder();
            cmd.AppendLine("@echo off");
            cmd.AppendLine($"echo INICIO {proyecto} > \"{logs}\\kit_prueba_estado.txt\"");
            cmd.AppendLine($"\"{unity}\" -batchmode -nographics -projectPath \"{proyecto}\" -importPackage \"{paquete}\" -quit -logFile \"{logs}\\kit_prueba_import.log\"");
            cmd.AppendLine($"echo IMPORTADO %ERRORLEVEL% >> \"{logs}\\kit_prueba_estado.txt\"");
            cmd.AppendLine($"\"{unity}\" -batchmode -nographics -projectPath \"{proyecto}\" -executeMethod NarrativeGraphSamples.ElFaro.ElFaroBuilder.BuildAndOpen -quit -logFile \"{logs}\\kit_prueba_ejemplo.log\"");
            cmd.AppendLine($"echo EJEMPLO %ERRORLEVEL% >> \"{logs}\\kit_prueba_estado.txt\"");
            cmd.AppendLine($"echo FIN >> \"{logs}\\kit_prueba_estado.txt\"");
            var bat = Path.Combine(logs, "kit_prueba.cmd");
            File.WriteAllText(bat, cmd.ToString(), Encoding.Default);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"") { UseShellExecute = false, CreateNoWindow = true });
            Debug.Log($"[Kit del grafo] Probando en {proyecto} (segundo plano). Estado en Logs/claude/kit_prueba_estado.txt");
        }

        /// Las dependencias del kit (UI y TextMeshPro, Timeline, Newtonsoft) y los módulos de Unity
        /// que tiene este proyecto, con sus versiones.
        static string Manifiesto()
        {
            var nuestro = File.ReadAllLines("Packages/manifest.json");
            var lineas = nuestro.Where(l =>
                    l.Contains("\"com.unity.modules.") || l.Contains("\"com.unity.ugui\"") || l.Contains("\"com.unity.timeline\"") ||
                    l.Contains("\"com.unity.nuget.newtonsoft-json\"") || l.Contains("\"com.unity.test-framework\""))
                .Select(l => l.Trim().TrimEnd(','))
                .ToList();
            return "{\n  \"dependencies\": {\n    " + string.Join(",\n    ", lineas) + "\n  }\n}\n";
        }
    }
}
