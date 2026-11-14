using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor.Story
{
    /// Cómo está la historia, contado igual para la persona (pestaña «En vivo») y para el
    /// asistente o las herramientas externas (ToText): por dónde va, qué espera, cómo debería
    /// estar el mundo al llegar al paso y, en Play, qué no cuadra con la partida.
    public sealed class StoryLiveReport
    {
        public enum Mark { Expected, Ok, Wrong }

        public sealed class Check
        {
            public string Group;
            public string Text;
            public Mark State;
            /// Lo que hay en la partida, si no cuadra.
            public string Live;
            public string SourceGuid;
            public NarrativeGraph SourceGraph;
        }

        public sealed class Running
        {
            public NarrativeGraph Graph;
            public NarrativeNode Step;
            public string Waiting;
        }

        public NarrativeGraph Graph;
        public NarrativeNode Step;
        public bool Playing;
        /// En Play y el juego está justo en este paso: las comprobaciones comparan con la partida.
        public bool Comparing;
        public bool ReachedStep;
        public readonly List<NarrativeGraph> Previous = new();
        public readonly List<Running> Runners = new();
        public readonly List<Check> Checks = new();
        public readonly List<string> Info = new();
        public RecordingStateWriter Story;
        readonly Dictionary<NarrativeGraph, StoryStructure> _structures = new();

        public string Number(NarrativeGraph g, NarrativeNode node)
        {
            if (g == null || node == null) return string.Empty;
            if (!_structures.TryGetValue(g, out var st)) _structures[g] = st = StoryStructure.Build(g);
            return st.Number(node);
        }

        public IEnumerable<Check> Wrong => Checks.Where(c => c.State == Mark.Wrong);

        /// El informe del paso. En Play, si el capítulo está en marcha y no se pide un paso, el
        /// del juego.
        public static StoryLiveReport Build(NarrativeGraph graph, NarrativeNode step)
        {
            var r = new StoryLiveReport { Graph = graph, Playing = EditorApplication.isPlaying };
            if (r.Playing) CollectRunners(r);
            var here = r.Runners.FirstOrDefault(x => x.Graph == graph);
            if (step == null && here != null) step = here.Step;
            r.Step = step;
            if (graph == null || step == null) return r;
            r.Comparing = here != null && here.Step == step;

            // La historia hasta el paso no cambia mientras no cambie el grafo: en Play solo se
            // vuelve a mirar la partida.
            var story = StoryCache.Get($"live:{graph.GetEntityId()}:{step.guid}", () => Project(graph, step));
            r.ReachedStep = story.Reached;
            r.Previous.AddRange(story.Previous);
            var recorder = story.Recorder;
            r.Story = recorder;
            BuildChecks(r, recorder);
            return r;
        }

        sealed class Projected
        {
            public RecordingStateWriter Recorder;
            public bool Reached;
            public List<NarrativeGraph> Previous;
        }

        static Projected Project(NarrativeGraph graph, NarrativeNode step)
        {
            var recorder = new RecordingStateWriter();
            var previous = NarrativeChapterChain.Before(graph, step.guid);
            foreach (var chapter in previous)
            {
                recorder.Graph = chapter.Graph;
                NarrativeStateProjector.Project(chapter.Graph, chapter.BridgeGuid, recorder);
            }
            recorder.Graph = graph;
            var result = NarrativeStateProjector.Project(graph, step.guid, recorder);
            if (!result.ReachedTarget) { recorder = new RecordingStateWriter(); previous.Clear(); }
            return new Projected { Recorder = recorder, Reached = result.ReachedTarget, Previous = previous.Select(c => c.Graph).ToList() };
        }

        static void CollectRunners(StoryLiveReport r)
        {
            IEnumerable<NarrativeRunner> runners = NarrativeGraphHub.Instance != null
                ? NarrativeGraphHub.Instance.GetAllRunners()
                : UnityEngine.Object.FindObjectsByType<NarrativeRunner>(FindObjectsInactive.Exclude);
            foreach (var runner in runners)
            {
                if (runner == null || runner.graph == null) continue;
                var node = runner.CurrentNode ?? runner.graph.FindNode(runner.Blackboard?.Get<string>(NarrativeRunner.CurrentNodeKey, null));
                if (node == null) continue;
                r.Runners.Add(new Running { Graph = runner.graph, Step = node, Waiting = Waiting(node) });
            }
        }

        /// Qué espera un paso para seguir, en una frase.
        public static string Waiting(NarrativeNode node)
        {
            if (node == null) return string.Empty;
            var listens = NarrativeNodeReferences.Of(node).Signals.Where(s => s.Role == SignalRole.Escucha && !string.IsNullOrEmpty(s.Key)).Select(s => s.Key).ToList();
            const string Talk = "NPC_INTERACT_";
            var talk = listens.FirstOrDefault(k => k.StartsWith(Talk, StringComparison.Ordinal));
            if (talk != null) return StoryText.T($"que hables con {talk.Substring(Talk.Length)}", $"you to talk to {talk.Substring(Talk.Length)}");
            if (listens.Count > 0) return StoryText.T($"la señal {string.Join(" o ", listens)}", $"the signal {string.Join(" or ", listens)}");
            var summary = StepSummary.For(node, 90);
            return string.IsNullOrEmpty(summary) ? NarrativeNodeCatalog.TitleOf(node.GetType()) : summary;
        }

        static void BuildChecks(StoryLiveReport r, RecordingStateWriter s)
        {
            bool live = r.Comparing;
            NarrativeServices.TryGet(out IQuestService quests, nameof(StoryLiveReport));
            var actors = NarrativeServices.Get<INarrativeActorDirectory>();
            var signals = DefaultNarrativeSignals.Instance;
            string offStage = StoryText.T("fuera de escena", "off stage");

            foreach (var a in s.Actors)
            {
                bool shouldBe = a.Value.value != offStage;
                var c = Add(r, s, StoryText.T("Personajes", "Characters"),
                    shouldBe ? StoryText.T($"{a.Key} en {a.Value.value}", $"{a.Key} at {a.Value.value}") : StoryText.T($"{a.Key} fuera de escena", $"{a.Key} off stage"), a.Value.source);
                if (live && actors != null)
                {
                    bool isThere = actors.FindActor(a.Key) != null;
                    Judge(c, isThere == shouldBe, isThere ? StoryText.T("está en la escena", "is in the scene") : StoryText.T("no está en la escena", "is not in the scene"));
                }
            }
            foreach (var q in s.Quests)
            {
                var c = Add(r, s, StoryText.T("Misiones", "Quests"), StoryText.T($"Misión {q.Key}: {QuestText(q.Value.value)}", $"Quest {q.Key}: {QuestText(q.Value.value)}"), q.Value.source);
                if (live && quests != null)
                {
                    var now = quests.GetState(q.Key) switch
                    {
                        NarrativeQuestState.Completed => RecordingStateWriter.QuestDone,
                        NarrativeQuestState.NotStarted => null,
                        _ => RecordingStateWriter.QuestActive,
                    };
                    Judge(c, now == q.Value.value, QuestText(now));
                }
            }
            foreach (var f in s.Flags)
            {
                var c = Add(r, s, StoryText.T("Datos de partida", "Story flags"),
                    f.Value.value ? f.Key : StoryText.T($"{f.Key} (sin poner)", $"{f.Key} (not set)"), f.Value.source);
                if (live) Judge(c, NarrativeFlags.Has(f.Key) == f.Value.value, NarrativeFlags.Has(f.Key) ? StoryText.T("puesto", "set") : StoryText.T("sin poner", "not set"));
            }
            foreach (var i in s.Items.Where(i => i.Value.value > 0))
                Add(r, s, StoryText.T("Objetos", "Items"), i.Value.value > 1 ? $"{i.Key} ×{i.Value.value}" : i.Key, i.Value.source);
            foreach (var e in ActorStateFacts.From(s))
            {
                var c = Add(r, s, StoryText.T("Qué hace cada personaje", "What each character does"),
                    $"{e.Actor}: {ActorStateFacts.Describe(e.Node)} («{NarrativeStartPlanner.Title(e.Node)}»)", e.Source);
                if (c.SourceGraph == null) c.SourceGraph = e.Graph;
                if (live)
                {
                    var now = NarrativeActorStates.Active(e.Actor);
                    Judge(c, now == e.Node.guid, string.IsNullOrEmpty(now) ? StoryText.T("sin pieza", "no piece") : StoryText.T("otra pieza", "another piece"));
                }
            }
            foreach (var category in s.Facts.Where(f => f.Key != NarrativeCoreFacts.ActorState && f.Key != NarrativeCoreFacts.ActorUsual))
                foreach (var f in category.Value)
                    Add(r, s, ObjectNames.NicifyVariableName(category.Key), string.IsNullOrEmpty(f.Value.value) ? f.Key : $"{f.Key}: {f.Value.value}", f.Value.source);
            int signalsOk = 0;
            foreach (var sig in s.Signals)
            {
                if (!live || signals == null) { signalsOk++; continue; }
                if (signals.HasEverRaised(sig.Key)) { signalsOk++; continue; }
                var c = Add(r, s, StoryText.T("Señales", "Signals"), StoryText.T($"Señal {sig.Key} emitida", $"Signal {sig.Key} raised"), sig.Value);
                Judge(c, false, StoryText.T("no se ha emitido", "not raised"));
            }
            if (signalsOk > 0) r.Info.Add(StoryText.T($"{signalsOk} señales ya emitidas{(live ? " ✓" : string.Empty)}", $"{signalsOk} signals already raised{(live ? " ✓" : string.Empty)}"));
            r.Info.AddRange(s.Notes);
        }

        static Check Add(StoryLiveReport r, RecordingStateWriter s, string group, string text, string source)
        {
            var c = new Check { Group = group, Text = text, SourceGuid = source };
            if (source != null && s.SourceGraph.TryGetValue(source, out var g)) c.SourceGraph = g;
            r.Checks.Add(c);
            return c;
        }

        static void Judge(Check c, bool ok, string live)
        {
            c.State = ok ? Mark.Ok : Mark.Wrong;
            if (!ok) c.Live = live;
        }

        static string QuestText(string state) => state switch
        {
            RecordingStateWriter.QuestDone => StoryText.T("hecha", "done"),
            RecordingStateWriter.QuestActive => StoryText.T("en curso", "active"),
            _ => StoryText.T("sin empezar", "not started"),
        };

        /// Dónde se hizo algo: «2.15» en este capítulo, «Cap2 · título» en otro.
        public string SourceLabel(Check c)
        {
            if (string.IsNullOrEmpty(c.SourceGuid)) return string.Empty;
            var g = c.SourceGraph ?? Graph;
            var node = g?.FindNode(c.SourceGuid);
            if (node == null) return string.Empty;
            var number = Number(g, node);
            return g == Graph ? number : $"{g.name} · {number}";
        }

        /// El informe en texto, para el asistente y las herramientas externas.
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Estado de la historia · {Graph?.name}");
            if (Playing)
            {
                if (Runners.Count == 0) sb.AppendLine("En Play, pero ningún capítulo está en marcha.");
                foreach (var run in Runners)
                    sb.AppendLine($"- En marcha: {run.Graph.name} · {Number(run.Graph, run.Step)} «{NarrativeStartPlanner.Title(run.Step)}». Esperando: {run.Waiting}.");
            }
            else sb.AppendLine("Fuera de Play: lo que sigue es lo que deberían haber hecho los pasos, sin comparar con una partida.");
            if (Step == null) return sb.ToString();
            sb.AppendLine($"Paso: {Number(Graph, Step)} «{NarrativeStartPlanner.Title(Step)}».");
            if (!ReachedStep) sb.AppendLine("Este paso no está conectado al inicio: no hay historia anterior.");
            if (Previous.Count > 0) sb.AppendLine("Capítulos anteriores, terminados: " + string.Join(", ", Previous.Select(p => p.name)) + ".");
            var wrong = Wrong.ToList();
            if (Comparing)
            {
                sb.AppendLine(wrong.Count == 0 ? "La partida coincide con lo que dice el grafo." : $"## No cuadra con la partida ({wrong.Count})");
                foreach (var c in wrong) sb.AppendLine($"- {c.Text} (lo dejó {SourceLabel(c)}); en la partida: {c.Live}.");
            }
            sb.AppendLine("## Al llegar aquí debería");
            foreach (var group in Checks.Where(c => c.State != Mark.Wrong).GroupBy(c => c.Group))
            {
                sb.AppendLine($"{group.Key}:");
                foreach (var c in group) sb.AppendLine($"- {c.Text} ({SourceLabel(c)})");
            }
            foreach (var i in Info) sb.AppendLine("- " + i);
            return sb.ToString();
        }
    }
}
