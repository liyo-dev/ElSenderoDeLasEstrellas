using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Sendero.Narrative.Editor
{
    /// El grafo narrativo es el paquete embebido Packages/com.liyodev.narrativegraph. Desde aquí se
    /// exporta a .unitypackage y se prueba en un proyecto de Unity aparte (`../NarrativeGraphPrueba`,
    /// se crea la primera vez) que usa el paquete directamente (file:), en segundo plano: importa el
    /// ejemplo «El faro» y lo monta. Logs en Logs/claude/kit_prueba_*.log.
    public static class ExportarElKitDelGrafo
    {
        const string Raiz = "Packages/com.liyodev.narrativegraph";
        const string Paquete = "Builds/Kits/NarrativeGraph.unitypackage";
        const string ProyectoDePrueba = "../NarrativeGraphPrueba";

        [MenuItem("El Sendero/Kits/Grafo narrativo: exportar el .unitypackage")]
        public static string Exportar()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Paquete));
            AssetDatabase.ExportPackage(new[] { Raiz }, Paquete, ExportPackageOptions.Recurse);
            var ruta = Path.GetFullPath(Paquete);
            Debug.Log("[Kit del grafo] Exportado: " + ruta);
            return ruta;
        }

        [MenuItem("El Sendero/Kits/Grafo narrativo: probar en el proyecto de prueba")]
        public static void Probar()
        {
            var proyecto = Path.GetFullPath(ProyectoDePrueba);
            Directory.CreateDirectory(Path.Combine(proyecto, "Assets"));
            Directory.CreateDirectory(Path.Combine(proyecto, "Packages"));
            File.WriteAllText(Path.Combine(proyecto, "Packages", "manifest.json"), Manifiesto(Path.GetFullPath(Raiz)));
            // Lo que dejó una importación anterior del .unitypackage chocaría con el paquete.
            var viejo = Path.Combine(proyecto, "Assets", "NarrativeGraph");
            if (Directory.Exists(viejo)) Directory.Delete(viejo, true);
            if (File.Exists(viejo + ".meta")) File.Delete(viejo + ".meta");

            var logs = Path.GetFullPath("Logs/claude");
            Directory.CreateDirectory(logs);
            var unity = EditorApplication.applicationPath;
            var cmd = new StringBuilder();
            cmd.AppendLine("@echo off");
            cmd.AppendLine($"echo INICIO {proyecto} > \"{logs}\\kit_prueba_estado.txt\"");
            cmd.AppendLine($"\"{unity}\" -batchmode -nographics -projectPath \"{proyecto}\" -executeMethod NarrativeGraphEditor.NarrativeSamples.ImportAll -quit -logFile \"{logs}\\kit_prueba_import.log\"");
            cmd.AppendLine($"echo IMPORTADO %ERRORLEVEL% >> \"{logs}\\kit_prueba_estado.txt\"");
            cmd.AppendLine($"\"{unity}\" -batchmode -nographics -projectPath \"{proyecto}\" -executeMethod NarrativeGraphSamples.ElFaro.ElFaroBuilder.BuildAndOpen -quit -logFile \"{logs}\\kit_prueba_ejemplo.log\"");
            cmd.AppendLine($"echo EJEMPLO %ERRORLEVEL% >> \"{logs}\\kit_prueba_estado.txt\"");
            cmd.AppendLine($"echo FIN >> \"{logs}\\kit_prueba_estado.txt\"");
            var bat = Path.Combine(logs, "kit_prueba.cmd");
            File.WriteAllText(bat, cmd.ToString(), Encoding.Default);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"") { UseShellExecute = false, CreateNoWindow = true });
            Debug.Log($"[Kit del grafo] Probando en {proyecto} (segundo plano). Estado en Logs/claude/kit_prueba_estado.txt");
        }

        /// El paquete del grafo (por ruta), sus dependencias y los módulos de Unity de este proyecto.
        static string Manifiesto(string paquete)
        {
            var lineas = File.ReadAllLines("Packages/manifest.json").Where(l =>
                    l.Contains("\"com.unity.modules.") || l.Contains("\"com.unity.ugui\"") || l.Contains("\"com.unity.timeline\"") ||
                    l.Contains("\"com.unity.nuget.newtonsoft-json\"") || l.Contains("\"com.unity.test-framework\""))
                .Select(l => l.Trim().TrimEnd(','))
                .ToList();
            lineas.Insert(0, $"\"com.liyodev.narrativegraph\": \"file:{paquete.Replace('\\', '/')}\"");
            return "{\n  \"dependencies\": {\n    " + string.Join(",\n    ", lineas) + "\n  }\n}\n";
        }
    }
}
