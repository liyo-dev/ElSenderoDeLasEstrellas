using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Liyodev.NarrativeGraph.Editor.Story;
using UnityEditor;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor
{
    /// Comprobación del kit: que todos los pasos se presenten, que los puertos tengan
    /// implementación en este proyecto y que no queden rutas de menú viejas. Deja el informe en
    /// Logs/NarrativeGraph/qa.txt.
    public static class NarrativeKitQa
    {
        const string PackageRoot = "Packages/com.liyodev.narrativegraph";

        [MenuItem("Liyodev/Grafo narrativo/Herramientas/Comprobar el kit", priority = 105)]
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
                // Código, manifiesto, textos y el servidor MCP: la ruta vieja también engaña en un texto.
                string oldText = "Tools › " + "Narrative Graph";
                string oldArrow = "Tools > " + "Narrative Graph";
                foreach (var file in Directory.GetFiles(PackageRoot, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".cs" && ext != ".json" && ext != ".md" && ext != ".mjs") continue;
                    var text = File.ReadAllText(file);
                    if (!text.Contains(oldTools) && !text.Contains(oldWindow) && !text.Contains(oldText) && !text.Contains(oldArrow)) continue;
                    errors++;
                    report.AppendLine($"ERROR {file.Replace(PackageRoot + "/", "").Replace('\\', '/')}: ruta de menú vieja");
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
            report.AppendLine("## Mapa");
            var map = NarrativeWorldMap.Find();
            if (map == null)
            {
                warnings++;
                report.AppendLine("AVISO no hay mapa de la historia");
            }
            else
            {
                report.AppendLine($"Escenas: {map.scenes.Count} ({string.Join(", ", map.scenes.Select(s => s.name))})");
                report.AppendLine($"Sitios: {map.zones.Count}");
                int withPortrait = 0;
                foreach (var c in map.characters)
                {
                    var tex = StoryCardImages.PortraitOf(c.actorId, prefabFallback: false);
                    if (tex != null) withPortrait++;
                    report.AppendLine($"- {c.name} ({c.actorId}): {(tex != null ? "retrato " + tex.name : "sin retrato (inicial)")}");
                }
                report.AppendLine($"Con retrato: {withPortrait} / {map.characters.Count}");

                var graphActors = NarrativeProjectIndex.Graphs.Where(g => g?.nodes != null)
                    .SelectMany(g => g.nodes)
                    .Select(n => n switch { ActorStateNode a => a.ActorId, PlaceActorNode p => p.ActorId, _ => null })
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct().OrderBy(id => id).ToList();
                var missing = graphActors.Where(id => map.CharacterOf(id) == null).ToList();
                report.AppendLine($"Actores que nombra el grafo: {graphActors.Count}; sin ficha en el mapa: {missing.Count}" +
                                  (missing.Count > 0 ? " (" + string.Join(", ", missing) + ")" : string.Empty));
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
