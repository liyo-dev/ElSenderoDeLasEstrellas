using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NarrativeGraphEditor;
using NarrativeGraphEditor.Story;
using UnityEditor;

namespace Sendero.Narrative.Editor
{
    /// Mide lo que cuesta cada operación del editor del grafo con los capítulos reales. Deja el
    /// resultado en Logs/claude/rendimiento_grafo.txt. Herramienta de desarrollo del kit.
    public static class MedirElGrafo
    {
        [MenuItem("El Sendero/Kits/Grafo narrativo: medir rendimiento")]
        public static void Medir()
        {
            var sb = new StringBuilder($"Rendimiento del grafo · {DateTime.Now:yyyy-MM-dd HH:mm}\n(ms; media de varias pasadas)\n\n");
            var grafos = AssetDatabase.FindAssets("t:NarrativeGraph").Select(g => AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(g))).Where(g => g != null).ToList();

            sb.AppendLine($"Índice del proyecto (reconstruir): {Tiempo(() => NarrativeProjectIndex.ForceRebuild(), 3):0.0}");
            foreach (var g in grafos)
            {
                var nodos = g.nodes.Where(n => n != null).ToList();
                var muestra = nodos.Where((_, i) => i % Math.Max(1, nodos.Count / 6) == 0).Take(6).ToList();
                sb.AppendLine($"\n## {g.name} ({nodos.Count} pasos)");
                sb.AppendLine($"Estructura (secciones y números): {Tiempo(() => StoryStructure.Build(g), 10):0.00}");
                var st = StoryStructure.Build(g);
                sb.AppendLine($"Rutas de las líneas: {Tiempo(() => StoryLayout.Routes(g, st), 5):0.00}");
                sb.AppendLine($"Resumen de todas las tarjetas: {Tiempo(() => { foreach (var n in nodos) StepSummary.For(n); }, 5):0.00}");
                sb.AppendLine($"Avisos de todas las tarjetas: {Tiempo(() => { foreach (var n in nodos) StepSummary.Warnings(n); }, 5):0.00}");
                sb.AppendLine($"Referencias de todos los pasos: {Tiempo(() => { foreach (var n in nodos) NarrativeNodeReferences.Of(n); }, 5):0.00}");
                sb.AppendLine($"Camino hasta un paso (por clic): {Media(muestra, n => NarrativeStateProjector.Project(g, n.guid, new RecordingStateWriter())):0.00}");
                sb.AppendLine($"Capítulos anteriores: {Media(muestra, n => NarrativeChapterChain.Before(g, n.guid)):0.00}");
                sb.AppendLine($"Plan de arranque (sin caché): {Media(muestra, n => NarrativeStartPlanner.Plan(g, n.guid, new Dictionary<string, int>())):0.00}");
                sb.AppendLine($"Informe En vivo (sin caché): {Media(muestra, n => { StoryCache.Invalidate(); StoryLiveReport.Build(g, n); }):0.00}");
                sb.AppendLine($"Informe En vivo (con caché): {Media(muestra, n => StoryLiveReport.Build(g, n)):0.00}");
            }
            var ruta = Path.GetFullPath("Logs/claude/rendimiento_grafo.txt");
            File.WriteAllText(ruta, sb.ToString());
            UnityEngine.Debug.Log("[Grafo] Rendimiento medido: " + ruta);
        }

        static double Tiempo(Action a, int veces)
        {
            a();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            return sw.Elapsed.TotalMilliseconds / veces;
        }

        static double Media(List<NarrativeNode> nodos, Action<NarrativeNode> a)
        {
            if (nodos.Count == 0) return 0;
            var sw = Stopwatch.StartNew();
            foreach (var n in nodos) a(n);
            return sw.Elapsed.TotalMilliseconds / nodos.Count;
        }
    }
}
