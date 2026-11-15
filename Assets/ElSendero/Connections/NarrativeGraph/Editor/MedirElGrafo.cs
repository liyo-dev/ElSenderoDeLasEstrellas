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

        /// Pasa por todos los pasos de todos los grafos lo que hace el editor con ellos (resumen,
        /// avisos, referencias, rehacer la historia hasta el paso y el plan de arranque) y apunta
        /// lo que falla, los avisos de contenido y los tipos que no declaran lo que dejan hecho.
        /// Resultado en Logs/claude/comprobacion_grafos.txt.
        [MenuItem("El Sendero/Kits/Grafo narrativo: comprobar los grafos")]
        public static void Comprobar()
        {
            var sb = new StringBuilder($"Comprobación de los grafos · {DateTime.Now:yyyy-MM-dd HH:mm}\n");
            var sinDeclarar = NarrativeEffectContract.FindUndeclared(TypeCache.GetTypesDerivedFrom<NarrativeNode>()
                .Concat(TypeCache.GetTypesDerivedFrom<NarrativeRequest>()));
            sb.AppendLine(sinDeclarar.Count == 0 ? "Todos los tipos declaran lo que dejan hecho." : "Sin declarar lo que dejan hecho: " + string.Join(", ", sinDeclarar.Select(t => t.Name)));
            int fallos = 0;
            foreach (var g in AssetDatabase.FindAssets("t:NarrativeGraph").Select(x => AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(x))).Where(x => x != null))
            {
                var nodos = g.nodes.Where(n => n != null).ToList();
                sb.AppendLine($"\n## {g.name}: {nodos.Count} pasos ({nodos.OfType<GameStepNode>().Count()} «Pedir al juego»), {g.nodes.Count(n => n == null)} nulos");
                StoryFlowCheck.Run(g, StoryStructure.Build(g));
                var v = NarrativeGraphValidator.ValidateGraph(g);
                foreach (var e in v.Errors) sb.AppendLine("  ERROR " + e);
                foreach (var n in nodos)
                {
                    string quien = $"{NarrativeNodeCatalog.TitleOf(n)} «{n.displayTitle}» ({n.guid})";
                    try
                    {
                        StepSummary.For(n);
                        foreach (var w in StepSummary.Warnings(n)) sb.AppendLine($"  aviso {quien}: {w}");
                        NarrativeNodeReferences.Of(n);
                        if (!n.IsAnnotation)
                        {
                            NarrativeStateProjector.Project(g, n.guid, new RecordingStateWriter());
                            NarrativeStartPlanner.Plan(g, n.guid, new Dictionary<string, int>());
                        }
                        if (n is GameStepNode paso && paso.HasNamedOutputs && paso.outputs.Count != paso.GetOutputPorts().Length)
                            sb.AppendLine($"  aviso {quien}: {paso.outputs.Count} conexiones para {paso.GetOutputPorts().Length} salidas");
                    }
                    catch (Exception ex)
                    {
                        fallos++;
                        sb.AppendLine($"  FALLA {quien}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            sb.AppendLine($"\nFallos: {fallos}");
            var ruta = Path.GetFullPath("Logs/claude/comprobacion_grafos.txt");
            File.WriteAllText(ruta, sb.ToString());
            UnityEngine.Debug.Log("[Grafo] Comprobación: " + ruta);
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
