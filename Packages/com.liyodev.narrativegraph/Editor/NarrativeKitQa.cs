using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// Comprobación del kit: que todos los pasos se presenten, que los puertos tengan
    /// implementación en este proyecto y que no queden rutas de menú viejas. Deja el informe en
    /// Logs/NarrativeGraph/qa.txt.
    public static class NarrativeKitQa
    {
        const string PackageRoot = "Packages/com.liyodev.narrativegraph";

        [MenuItem("Liyodev/Grafo narrativo/Comprobar el kit", priority = 201)]
        public static void Check()
        {
            var report = new StringBuilder();
            int errors = 0, warnings = 0;
            report.AppendLine("# Grafo narrativo: comprobación del kit");
            report.AppendLine();

            var nodes = TypeCache.GetTypesDerivedFrom<NarrativeNode>()
                .Where(t => !t.IsAbstract && t.IsClass && !t.IsNested)
                .OrderBy(t => t.FullName).ToList();
            report.AppendLine($"## Pasos ({nodes.Count})");
            foreach (var t in nodes)
            {
                if (t.GetCustomAttribute<SerializableAttribute>() == null)
                {
                    errors++;
                    report.AppendLine($"ERROR {t.FullName}: sin [Serializable]");
                }
                var info = t.GetCustomAttribute<NarrativeNodeInfoAttribute>();
                if (info == null)
                {
                    errors++;
                    report.AppendLine($"ERROR {t.FullName}: sin [NarrativeNodeInfo]");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(info.Title))
                {
                    warnings++;
                    report.AppendLine($"AVISO {t.FullName}: título vacío");
                }
            }

            var requests = TypeCache.GetTypesDerivedFrom<NarrativeRequest>()
                .Where(t => !t.IsAbstract && t != typeof(NarrativeRequestValues) && !t.IsNested)
                .OrderBy(t => t.FullName).ToList();
            report.AppendLine();
            report.AppendLine($"## Peticiones ({requests.Count})");
            foreach (var t in requests)
            {
                if (t.GetCustomAttribute<SerializableAttribute>() == null)
                {
                    errors++;
                    report.AppendLine($"ERROR {t.FullName}: sin [Serializable]");
                }
                if (t.GetCustomAttribute<NarrativeNodeInfoAttribute>() == null)
                {
                    errors++;
                    report.AppendLine($"ERROR {t.FullName}: sin [NarrativeNodeInfo]");
                }
            }

            report.AppendLine();
            report.AppendLine("## Paleta");
            var all = NarrativeNodeCatalog.All;
            report.AppendLine($"Historia: {all.Count(e => e.family != NarrativeStepFamily.Juego)}");
            report.AppendLine($"Del juego: {all.Count(e => e.family == NarrativeStepFamily.Juego)}");

            report.AppendLine();
            report.AppendLine("## Puertos");
            var ports = typeof(NarrativeServices).Assembly.GetTypes()
                .Where(t => t.IsInterface && t.Name.StartsWith("INarrative"))
                .OrderBy(t => t.Name).ToList();
            var classes = new List<Type>();
            // Solo para el informe del editor: no corre en builds.
#pragma warning disable UAC0005
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
#pragma warning restore UAC0005
            {
                try { classes.AddRange(asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract)); }
                catch (ReflectionTypeLoadException) { }
            }
            foreach (var port in ports)
            {
                if (classes.Any(t => port.IsAssignableFrom(t))) continue;
                warnings++;
                report.AppendLine($"AVISO {port.Name}: sin implementación en este proyecto (usará su respaldo; si no tiene, el paso no hará nada)");
            }

            report.AppendLine();
            report.AppendLine("## Menús, muestras y manual");
            if (!Directory.Exists(PackageRoot))
            {
                warnings++;
                report.AppendLine($"AVISO no encuentro {PackageRoot}");
            }
            else
            {
                // Las agujas se montan aquí para que este fichero no se delate a sí mismo.
                string oldTools = "\"Tools/" + "Narrative Graph";
                string oldWindow = "\"Window/" + "Narrative Graph";
                foreach (var file in Directory.GetFiles(PackageRoot, "*.cs", SearchOption.AllDirectories))
                {
                    var text = File.ReadAllText(file);
                    if (!text.Contains(oldTools) && !text.Contains(oldWindow)) continue;
                    errors++;
                    report.AppendLine($"ERROR {file.Replace(PackageRoot + "/", "")}: ruta de menú vieja");
                }

                var samples = Directory.GetDirectories(Path.Combine(PackageRoot, "Samples~"));
                report.AppendLine($"Muestras: {samples.Length} ({string.Join(", ", samples.Select(Path.GetFileName))})");
                if (!File.Exists(Path.Combine(PackageRoot, "Documentation", "Manual de usuario.pdf")))
                {
                    warnings++;
                    report.AppendLine("AVISO falta el manual PDF");
                }
            }

            report.AppendLine();
            report.AppendLine($"FIN errores={errors} avisos={warnings}");

            var outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "NarrativeGraph");
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, "qa.txt");
            File.WriteAllText(outPath, report.ToString());

            var summary = $"[Grafo narrativo] QA: {errors} errores, {warnings} avisos. Informe: {outPath}";
            if (errors > 0) Debug.LogError(summary + "\n" + report);
            else Debug.Log(summary + "\n" + report);
        }
    }
}
