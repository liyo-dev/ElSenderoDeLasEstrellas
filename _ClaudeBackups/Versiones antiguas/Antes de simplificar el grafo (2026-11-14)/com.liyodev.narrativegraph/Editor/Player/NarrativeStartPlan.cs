using System;
using System.Collections.Generic;
using System.Linq;
using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// Cómo empieza una prueba desde un paso: la partida nueva de la ficha, la historia hasta el
    /// paso (los capítulos anteriores, terminados) con los puntos de arranque (◆, dónde aparece)
    /// en su sitio, el del propio paso y, al final, el jugador de prueba elegido, si hay uno. Si el
    /// juego no sabe escribir la historia en el jugador, los puntos se aplican en orden sobre la partida nueva.
    public sealed class NarrativeStartPlan
    {
        public sealed class Layer
        {
            public string Label;
            public NarrativeGraph Graph;
            public string NodeGuid;
            public NarrativePlayerValues Values;
            /// El jugador de prueba elegido (no es un punto del grafo).
            public bool IsTestPlayer;
        }

        public NarrativePlayerSheet Sheet;
        public NarrativeGraph Graph;
        /// El paso pedido.
        public string StepGuid;
        /// El paso desde el que de verdad se empieza (salta la entrada y la espera del capítulo anterior).
        public string TargetGuid;
        public readonly List<NarrativeChapterChain.Chapter> Previous = new();
        public readonly List<Layer> Layers = new();
        public readonly List<string> Warnings = new();
        public RecordingStateWriter Story = new();
        public NarrativeStateProjector.Result Route = new();
        public readonly List<NarrativeStateProjector.Decision> Decisions = new();

        public bool ReachedTarget => Route.ReachedTarget;

        /// Los puntos de arranque que cuentan (sin el jugador de prueba).
        public IEnumerable<Layer> Checkpoints => Layers.Where(l => !l.IsTestPlayer);

        /// La capa que deja el valor final de un campo, o null si sale de la partida nueva.
        public Layer SourceOf(string field) => Layers.LastOrDefault(l => l.Values != null && l.Values.Has(field));

        /// Copia la partida nueva de la ficha en <paramref name="destination"/> (conserva su nombre).
        public void ApplyBase(UnityEngine.Object destination)
        {
            var template = Sheet != null ? Sheet.TemplateData : null;
            if (template == null || destination == null || template.GetType() != destination.GetType()) return;
            var name = destination.name;
            EditorUtility.CopySerialized(template, destination);
            destination.name = name;
        }

        /// Aplica lo que va después de la historia: los puntos que ella no ha puesto en su sitio
        /// (todos si <paramref name="storyHasPoints"/> es false; si no, solo el del propio paso) y el jugador de prueba.
        public void ApplyOverrides(UnityEngine.Object destination, bool storyHasPoints)
        {
            foreach (var layer in Layers.Where(l => l.IsTestPlayer || !storyHasPoints || IsOwnStep(l)))
                NarrativePlayerValuesEditor.Apply(layer.Values, destination);
        }

        /// El punto de arranque es el propio paso (la historia se rehace hasta él, sin incluirlo).
        public bool IsOwnStep(Layer layer) => layer.Graph == Graph && (layer.NodeGuid == StepGuid || layer.NodeGuid == TargetGuid);

        /// La historia que escribe el juego ya lleva dentro los puntos de arranque anteriores.
        public bool StoryHasPoints => ReachedTarget && StoryWriter != null;

        /// Copia de trabajo con la partida nueva, los puntos y el jugador de prueba (sin la historia). Hay que liberarla.
        public PlayerScratch Preview() => Build(false, true, true);

        /// Copia de trabajo por capas: partida nueva, [historia, si el juego sabe escribirla],
        /// [puntos de arranque], [jugador de prueba]. Hay que liberarla.
        public PlayerScratch Build(bool story, bool points, bool testPlayer)
        {
            var scratch = new PlayerScratch(Sheet != null ? Sheet.DataType : null);
            if (scratch.Data == null) return scratch;
            ApplyBase(scratch.Data);
            bool storyHasPoints = story && StoryHasPoints;
            if (storyHasPoints) StoryWriter.ApplyStory(this, scratch.Data);
            foreach (var layer in Layers.Where(l => l.IsTestPlayer ? testPlayer : points && (!storyHasPoints || IsOwnStep(l))))
                NarrativePlayerValuesEditor.Apply(layer.Values, scratch.Data);
            return scratch;
        }

        /// Copia de trabajo de la partida nueva con solo lo que fija una capa. Hay que liberarla.
        public PlayerScratch Only(Layer layer)
        {
            var scratch = new PlayerScratch(Sheet != null ? Sheet.DataType : null);
            if (scratch.Data == null) return scratch;
            ApplyBase(scratch.Data);
            NarrativePlayerValuesEditor.Apply(layer.Values, scratch.Data);
            return scratch;
        }

        /// Quién escribe la historia en los datos del jugador (el arranque del juego), o null.
        public static INarrativeStoryOnPlayer StoryWriter => NarrativePlayLaunchers.Current as INarrativeStoryOnPlayer;
    }

    /// Calcula el plan de arranque de un paso.
    public static class NarrativeStartPlanner
    {
        public static NarrativeStartPlan Plan(NarrativeGraph graph, string stepGuid, IReadOnlyDictionary<string, int> choices = null)
        {
            // Para el panel (sin elecciones a mano) se reutiliza hasta que cambie un grafo o el jugador de prueba.
            if (choices == null && graph != null)
                return Story.StoryCache.Get($"plan:{graph.GetEntityId()}:{stepGuid}", () => Build(graph, stepGuid, null));
            return Build(graph, stepGuid, choices);
        }

        static NarrativeStartPlan Build(NarrativeGraph graph, string stepGuid, IReadOnlyDictionary<string, int> choices)
        {
            var plan = new NarrativeStartPlan { Sheet = NarrativePlayerSheet.Find(), Graph = graph, StepGuid = stepGuid, TargetGuid = stepGuid };
            if (graph == null || string.IsNullOrEmpty(stepGuid)) return plan;
            choices ??= StoryRouteChoices.All();

            var target = stepGuid;
            plan.Previous.AddRange(NarrativeChapterChain.Before(graph, ref target));
            plan.TargetGuid = target;

            var route = new List<(NarrativeGraph graph, string guid)>();
            var story = plan.Story;
            foreach (var chapter in plan.Previous)
            {
                story.Graph = chapter.Graph;
                var r = NarrativeStateProjector.Project(chapter.Graph, chapter.BridgeGuid, story, choices);
                plan.Decisions.AddRange(r.Decisions);
                route.AddRange(r.Order.Select(g => (chapter.Graph, g)));
            }
            story.Graph = graph;
            plan.Route = NarrativeStateProjector.Project(graph, target, story, choices);
            plan.Decisions.AddRange(plan.Route.Decisions);
            if (!plan.Route.ReachedTarget)
            {
                // Un paso suelto no tiene pasado: se empieza con la partida nueva.
                plan.Previous.Clear();
                plan.Story = new RecordingStateWriter();
                plan.Warnings.Add(StoryText.T("Este paso no está conectado al inicio: se empieza sin la historia anterior.",
                                              "This step is not connected to the start: it starts without the story before it."));
                route.Clear();
            }
            else route.AddRange(plan.Route.Order.Select(g => (graph, g)));

            // El punto pedido también cuenta si es él mismo un punto de arranque.
            if (graph.FindNode(stepGuid) is INarrativePlayerSetup && !route.Any(r => r.guid == stepGuid)) route.Add((graph, stepGuid));

            foreach (var (g, guid) in route)
            {
                var node = g.FindNode(guid);
                if (node is not INarrativePlayerSetup setup || setup.PlayerValues == null || setup.PlayerValues.IsEmpty) continue;
                plan.Layers.Add(new NarrativeStartPlan.Layer { Label = $"{node.Badge} {Title(node)}".Trim(), Graph = g, NodeGuid = guid, Values = setup.PlayerValues });
            }

            var testPlayer = TestPlayers.Selected;
            if (testPlayer != null && testPlayer.values != null && !testPlayer.values.IsEmpty)
            {
                // El sitio donde aparece lo dicen la historia y sus puntos, no el jugador de prueba.
                var values = testPlayer.values;
                if (plan.Sheet != null && values.Has(plan.Sheet.positionField))
                {
                    values = JsonUtility.FromJson<NarrativePlayerValues>(JsonUtility.ToJson(values));
                    NarrativePlayerValuesEditor.Remove(values, plan.Sheet.positionField);
                }
                plan.Layers.Add(new NarrativeStartPlan.Layer { Label = testPlayer.name, Values = values, IsTestPlayer = true });
            }

            if (plan.Sheet == null)
                plan.Warnings.Add(StoryText.T("Falta la ficha del jugador: créala en ⚙ Ajustes para que la prueba pueda enseñar y cambiar el jugador.",
                                              "There is no player sheet: create one in ⚙ Settings so the test can show and change the player."));
            return plan;
        }

        public static string Title(NarrativeNode node)
            => node == null ? "?" : string.IsNullOrWhiteSpace(node.displayTitle) ? NarrativeNodeCatalog.TitleOf(node.GetType()) : node.displayTitle;
    }

    /// La rama elegida en las decisiones del camino cuando varias llevan al paso (de cualquier
    /// capítulo). En este ordenador, para este proyecto.
    public static class StoryRouteChoices
    {
        [Serializable] sealed class Entry { public string node; public int port; }
        [Serializable] sealed class Store { public List<Entry> entries = new(); }

        static string Key => "NarrativeGraph.Start.Routes." + Application.dataPath;

        public static Dictionary<string, int> All()
        {
            var map = new Dictionary<string, int>();
            var json = EditorPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return map;
            try
            {
                foreach (var e in JsonUtility.FromJson<Store>(json).entries)
                    if (!string.IsNullOrEmpty(e.node)) map[e.node] = e.port;
            }
            catch (Exception) { }
            return map;
        }

        public static void Set(string nodeGuid, int port)
        {
            if (string.IsNullOrEmpty(nodeGuid)) return;
            var map = All();
            map[nodeGuid] = port;
            var store = new Store { entries = map.Select(p => new Entry { node = p.Key, port = p.Value }).ToList() };
            EditorPrefs.SetString(Key, JsonUtility.ToJson(store));
            Story.StoryCache.Invalidate();
        }
    }
}
