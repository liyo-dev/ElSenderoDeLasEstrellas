using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Pestaña «En vivo»: por dónde va la historia y qué espera, qué no cuadra con la partida y cómo
    /// debería estar el mundo al llegar al paso. En Play sigue al paso en el que va el juego; fuera
    /// de Play, al paso elegido. El mismo informe, en texto, es lo que lee el asistente.
    public sealed class StoryWorldPanel : VisualElement
    {
        public event Action<string> NavigateTo;
        public event Action<NarrativeGraph, string> OpenInGraph;

        readonly ScrollView _scroll;
        NarrativeGraph _graph;
        NarrativeNode _selected;
        StoryLiveReport _report;

        public StoryWorldPanel()
        {
            AddToClassList("story-panel");
            _scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _scroll.AddToClassList("story-panel__scroll");
            Add(_scroll);
        }

        public void Show(NarrativeGraph graph, StoryStructure structure, NarrativeNode node)
        {
            bool other = graph != _graph || node != _selected;
            _graph = graph;
            _selected = node;
            Rebuild();
            if (other && !EditorApplication.isPlaying) _scroll.scrollOffset = Vector2.zero;
        }

        public void Rebuild()
        {
            var offset = _scroll.scrollOffset;
            _scroll.Clear();
            // En Play manda el paso del juego; fuera, el elegido.
            _report = StoryLiveReport.Build(_graph, EditorApplication.isPlaying ? null : _selected);
            var r = _report;

            if (r.Playing) BuildRunning(r);
            if (r.Step == null)
            {
                if (!r.Playing)
                    _scroll.Add(StoryUI.Muted(StoryText.T("Elige un paso para ver cómo debería estar todo al llegar a él. En Play, aquí ves por dónde va la historia y qué no cuadra.",
                                                  "Pick a step to see how everything should be when reaching it. In Play, this shows where the story is and what does not match.")));
                return;
            }

            var head = new VisualElement();
            head.AddToClassList("story-panel__head");
            head.Add(StoryUI.Kicker(r.Comparing ? StoryText.T("EL JUEGO ESTÁ AQUÍ", "THE GAME IS HERE") : StoryText.T("AL LLEGAR A", "WHEN REACHING")));
            var titleRow = new VisualElement();
            titleRow.AddToClassList("story-row");
            var num = new Label(r.Number(r.Graph, r.Step));
            num.AddToClassList("story-accent-number");
            titleRow.Add(num);
            var title = new Label(NarrativeStartPlanner.Title(r.Step));
            title.AddToClassList("story-panel__title");
            titleRow.Add(title);
            head.Add(titleRow);
            if (!r.ReachedStep)
                head.Add(StoryUI.Muted(StoryText.T("Este paso no está conectado al inicio: no hay historia anterior.", "This step is not connected to the start: there is no story before it.")));
            if (r.Previous.Count > 0)
                head.Add(StoryUI.Muted(StoryText.T($"Capítulos anteriores, ya terminados: {string.Join(", ", r.Previous.Select(p => p.name))}.",
                                           $"Earlier chapters, already finished: {string.Join(", ", r.Previous.Select(p => p.name))}.")));
            _scroll.Add(head);

            // Lo que no cuadra, arriba del todo.
            var wrong = r.Wrong.ToList();
            if (r.Comparing)
            {
                if (wrong.Count == 0) _scroll.Add(StoryUI.Notice(StoryText.T("✓ La partida coincide con lo que dice el grafo.", "✓ The game matches the graph."), true));
                else
                {
                    var box = Section(StoryText.T($"✗ NO CUADRA ({wrong.Count})", $"✗ DOES NOT MATCH ({wrong.Count})"));
                    foreach (var c in wrong) box.Add(CheckRow(r, c));
                }
            }
            else if (r.Playing)
                _scroll.Add(StoryUI.Muted(StoryText.T("El juego no está en este capítulo: abajo, lo que debería haber al llegar al paso elegido.",
                                              "The game is not in this chapter: below, what there should be when reaching the selected step.")));

            var expected = Section(StoryText.T("AL LLEGAR AQUÍ DEBERÍA…", "WHEN REACHING HERE THERE SHOULD BE…"));
            foreach (var group in r.Checks.Where(c => c.State != StoryLiveReport.Mark.Wrong).GroupBy(c => c.Group))
            {
                var g = new Label(group.Key.ToUpperInvariant());
                g.AddToClassList("story-live__group");
                expected.Add(g);
                foreach (var c in group) expected.Add(CheckRow(r, c));
            }
            foreach (var i in r.Info) expected.Add(StoryUI.Muted(i));

            var tools = new VisualElement();
            tools.AddToClassList("story-start__actions");
            var copy = new Button(() => { EditorGUIUtility.systemCopyBuffer = r.ToText(); })
            {
                text = StoryText.T("Copiar como texto", "Copy as text"),
                tooltip = StoryText.T("El mismo informe que lee el asistente, para pegarlo donde quieras.", "The same report the assistant reads, to paste anywhere."),
            };
            copy.AddToClassList("story-button");
            tools.Add(copy);
            var signals = new Button(() => EditorApplication.ExecuteMenuItem("Tools/Narrative Graph/Monitor de señales"))
            {
                text = StoryText.T("Monitor de señales", "Signal monitor"),
            };
            signals.AddToClassList("story-button");
            tools.Add(signals);
            _scroll.Add(tools);

            if (EditorApplication.isPlaying) _scroll.schedule.Execute(() => _scroll.scrollOffset = offset);
        }

        void BuildRunning(StoryLiveReport r)
        {
            var box = Section(StoryText.T("POR DÓNDE VA", "WHERE IT IS"));
            if (r.Runners.Count == 0) { box.Add(StoryUI.Muted(StoryText.T("Ningún capítulo está en marcha.", "No chapter is running."))); return; }
            foreach (var run in r.Runners)
            {
                var row = new Button(() =>
                {
                    if (run.Graph == _graph) NavigateTo?.Invoke(run.Step.guid);
                    else OpenInGraph?.Invoke(run.Graph, run.Step.guid);
                });
                row.AddToClassList("story-live__running");
                row.EnableInClassList("is-this", run.Graph == _graph);
                var top = new Label($"▶ {run.Graph.name} · {r.Number(run.Graph, run.Step)}  {NarrativeStartPlanner.Title(run.Step)}");
                top.AddToClassList("story-live__running-title");
                row.Add(top);
                var wait = new Label(StoryText.T("Esperando: ", "Waiting for: ") + run.Waiting);
                wait.AddToClassList("story-muted");
                row.Add(wait);
                box.Add(row);
            }
        }

        VisualElement CheckRow(StoryLiveReport r, StoryLiveReport.Check c)
        {
            var row = new VisualElement();
            row.AddToClassList("story-live__check");
            row.EnableInClassList("is-wrong", c.State == StoryLiveReport.Mark.Wrong);
            var mark = new Label(c.State switch { StoryLiveReport.Mark.Ok => "✓", StoryLiveReport.Mark.Wrong => "✗", _ => "·" });
            mark.AddToClassList("story-live__mark");
            row.Add(mark);
            var text = new VisualElement { style = { flexGrow = 1, flexShrink = 1 } };
            var main = new Label(c.Text) { tooltip = c.Text };
            main.AddToClassList("story-live__text");
            text.Add(main);
            if (c.State == StoryLiveReport.Mark.Wrong && !string.IsNullOrEmpty(c.Live))
            {
                var live = new Label(StoryText.T("En la partida: ", "In the game: ") + c.Live);
                live.AddToClassList("story-live__live");
                text.Add(live);
            }
            row.Add(text);
            var source = r.SourceLabel(c);
            if (!string.IsNullOrEmpty(source))
            {
                var graph = c.SourceGraph ?? r.Graph;
                var guid = c.SourceGuid;
                var link = new Button(() =>
                {
                    if (graph == _graph) NavigateTo?.Invoke(guid);
                    else OpenInGraph?.Invoke(graph, guid);
                }) { text = source, tooltip = StoryText.T("El paso que lo deja así", "The step that leaves it so") };
                link.AddToClassList("story-panel__row-source");
                row.Add(link);
            }
            return row;
        }

        VisualElement Section(string title)
        {
            var box = new VisualElement();
            box.AddToClassList("story-panel__section");
            box.Add(StoryUI.Kicker(title));
            _scroll.Add(box);
            return box;
        }

    }

    /// Records what the projection leaves changed, and which step changed each thing.
    public sealed class RecordingStateWriter : INarrativeStateWriter, INarrativeProjectionTrace
    {
        public const string QuestActive = "active";
        public const string QuestDone = "done";

        public readonly SortedDictionary<string, (bool value, string source)> Flags = new();
        public readonly SortedDictionary<string, (string value, string source)> Quests = new();
        public readonly SortedDictionary<string, string> Signals = new();
        public readonly SortedDictionary<string, (int value, string source)> Items = new();
        public readonly SortedDictionary<string, (string value, string source)> Actors = new();
        public readonly SortedDictionary<string, SortedDictionary<string, (string value, string source)>> Facts = new();
        public readonly List<string> Notes = new();

        /// Graph being projected, so each change can point back to the chapter that made it.
        public NarrativeGraph Graph;
        public readonly Dictionary<string, NarrativeGraph> SourceGraph = new();

        string _source;

        public void OnProjectNode(NarrativeNode node)
        {
            _source = node?.guid;
            if (_source != null) SourceGraph[_source] = Graph;
        }

        public bool GetFlag(string key) => Flags.TryGetValue(key, out var f) && f.value;
        public void SetFlag(string key, bool value) { if (!string.IsNullOrWhiteSpace(key)) Flags[key] = (value, _source); }

        public void StartQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return;
            if (Quests.TryGetValue(questId, out var q) && q.value == QuestDone) return;
            Quests[questId] = (QuestActive, _source);
        }

        public void CompleteQuestSteps(string questId, IReadOnlyList<string> stepConditionIds, IReadOnlyList<int> stepIndices) => StartQuest(questId);
        public void CompleteQuest(string questId) { if (!string.IsNullOrWhiteSpace(questId)) Quests[questId] = (QuestDone, _source); }
        public void MarkSignalRaised(string key) { if (!string.IsNullOrWhiteSpace(key)) Signals[key] = _source; }

        public void AddItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            Items.TryGetValue(itemId, out var i);
            Items[itemId] = (i.value + amount, _source);
        }

        public void RemoveItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !Items.TryGetValue(itemId, out var i)) return;
            Items[itemId] = (Math.Max(0, i.value - amount), _source);
        }

        public void EnsureItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            Items.TryGetValue(itemId, out var i);
            if (i.value < amount) Items[itemId] = (amount, _source);
        }

        public void SetFact(string category, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(key)) return;
            if (!Facts.TryGetValue(category, out var facts)) Facts[category] = facts = new SortedDictionary<string, (string, string)>();
            if (category == NarrativeCoreFacts.Counter)
            {
                int current = facts.TryGetValue(key, out var old) && int.TryParse(old.value, out var n) ? n : 0;
                value = NarrativeCoreFacts.ApplyCounter(current, value).ToString();
            }
            facts[key] = (value, _source);
        }

        public void PlaceActor(string actorId, NarrativeLocation location)
        {
            if (!string.IsNullOrWhiteSpace(actorId) && location.IsValid) Actors[actorId] = (location.Mark, _source);
        }

        public void SetActorActive(string actorId, bool active)
        {
            if (string.IsNullOrWhiteSpace(actorId)) return;
            Actors.TryGetValue(actorId, out var a);
            if (!active) Actors[actorId] = (StoryText.T("fuera de escena", "off stage"), _source);
            else if (string.IsNullOrEmpty(a.value)) Actors[actorId] = (StoryText.T("en escena", "on stage"), _source);
        }

        public void Note(string message) { if (!string.IsNullOrWhiteSpace(message)) Notes.Add(message); }

        public bool TryGetExtension<T>(out T extension) where T : class { extension = null; return false; }
    }
}
