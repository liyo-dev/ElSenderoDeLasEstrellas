using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// "World here": what the world looks like when the story reaches the selected step, and which
    /// step left each thing that way. While playing, it compares it with the running game.
    public sealed class StoryWorldPanel : VisualElement
    {
        public event Action<string> NavigateTo;
        public event Action<NarrativeGraph, string> OpenInGraph;

        readonly ScrollView _scroll;
        NarrativeGraph _graph;
        StoryStructure _structure;
        NarrativeNode _node;
        RecordingStateWriter _recorder;
        bool _compare;

        public StoryWorldPanel()
        {
            AddToClassList("story-panel");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("story-panel__scroll");
            Add(_scroll);
        }

        public void Show(NarrativeGraph graph, StoryStructure structure, NarrativeNode node)
        {
            _graph = graph;
            _structure = structure;
            _node = node;
            Rebuild();
        }

        public void Rebuild()
        {
            _scroll.Clear();
            if (_graph == null || _node == null)
            {
                _scroll.Add(Muted(StoryText.T("Elige un paso para ver cómo está el mundo en ese punto.",
                                              "Pick a step to see what the world looks like at that point.")));
                return;
            }

            // Earlier chapters are taken as finished, then this one runs up to the step.
            var recorder = _recorder = new RecordingStateWriter();
            var previous = NarrativeChapterChain.Before(_graph, _node.guid);
            foreach (var chapter in previous)
            {
                recorder.Graph = chapter.Graph;
                NarrativeStateProjector.Project(chapter.Graph, chapter.BridgeGuid, recorder);
            }
            recorder.Graph = _graph;
            var result = NarrativeStateProjector.Project(_graph, _node.guid, recorder);
            if (!result.ReachedTarget)
            {
                // A loose step has no past: show nothing rather than the earlier chapters alone.
                recorder = _recorder = new RecordingStateWriter();
                previous.Clear();
            }

            var head = new VisualElement();
            head.AddToClassList("story-panel__head");
            head.Add(Kicker(StoryText.T("MUNDO AL LLEGAR A", "WORLD WHEN REACHING")));
            var titleRow = new VisualElement();
            titleRow.AddToClassList("story-row");
            var num = new Label(_structure?.Number(_node));
            num.AddToClassList("story-accent-number");
            titleRow.Add(num);
            var title = new Label(string.IsNullOrWhiteSpace(_node.displayTitle) ? NarrativeNodeCatalog.TitleOf(_node.GetType()) : _node.displayTitle);
            title.AddToClassList("story-panel__title");
            titleRow.Add(title);
            head.Add(titleRow);
            head.Add(Muted(result.ReachedTarget
                ? StoryText.T($"{result.Order.Count} pasos antes desde el inicio de este grafo. El camino está marcado en el lienzo.",
                              $"{result.Order.Count} steps before it from the start of this graph. The path is highlighted on the canvas.")
                : StoryText.T("Este paso no está conectado al inicio: no hay camino que reconstruir.",
                              "This step is not connected to the start: there is no path to rebuild.")));
            if (result.ReachedTarget && previous.Count > 0)
            {
                var names = string.Join(", ", previous.Select(c => c.Graph.name));
                head.Add(Muted(StoryText.T($"Antes, terminados: {names}.", $"Finished before: {names}.")));
            }
            _scroll.Add(head);

            var live = LiveGame.For(_graph, _node);
            var toggle = new VisualElement();
            toggle.AddToClassList("story-segmented");
            var byGraph = new Button(() => { _compare = false; Rebuild(); }) { text = StoryText.T("Según el grafo", "By the graph") };
            var byGame = new Button(() => { _compare = true; Rebuild(); }) { text = StoryText.T("En el juego (Play)", "In the game (Play)") };
            byGraph.EnableInClassList("is-on", !_compare);
            byGame.EnableInClassList("is-on", _compare);
            toggle.Add(byGraph);
            toggle.Add(byGame);
            _scroll.Add(toggle);

            bool comparing = _compare && live.Available;
            if (_compare && !live.Available)
                _scroll.Add(Notice(live.Reason, false));

            int differences = 0;

            Section(StoryText.T("Personajes", "Characters"), recorder.Actors.Select(a =>
                Row(a.Key, a.Value.value, a.Value.source, false)));

            foreach (var category in recorder.Facts.OrderBy(c => c.Key))
                Section(CategoryLabel(category.Key), category.Value.Select(f => Row(f.Key, f.Value.value ?? "—", f.Value.source, false)));

            Section(StoryText.T("Misiones", "Quests"), recorder.Quests.Select(q =>
            {
                bool diff = comparing && !live.QuestMatches(q.Key, q.Value.value);
                if (diff) differences++;
                return Row(q.Key, QuestText(q.Value.value) + (diff ? StoryText.T($"  · en el juego: {QuestText(live.QuestState(q.Key))}", $"  · in game: {QuestText(live.QuestState(q.Key))}") : string.Empty), q.Value.source, diff);
            }));

            Section(StoryText.T("Objetos", "Items"), recorder.Items.Where(i => i.Value.value > 0).Select(i =>
                Row(i.Key, "×" + i.Value.value, i.Value.source, false)));

            Section(StoryText.T("Datos de partida", "Story flags"), recorder.Flags.Select(f =>
            {
                bool diff = comparing && NarrativeFlags.Has(f.Key) != f.Value.value;
                if (diff) differences++;
                return Row(f.Key, (f.Value.value ? StoryText.T("Sí", "Yes") : StoryText.T("No", "No")) + (diff ? StoryText.T("  · distinto en el juego", "  · different in game") : string.Empty), f.Value.source, diff);
            }));

            Section(StoryText.T("Ya ha pasado", "Already happened"), recorder.Signals.Select(s =>
            {
                bool diff = comparing && !live.SignalRaised(s.Key);
                if (diff) differences++;
                return Row(s.Key, diff ? StoryText.T("no en el juego", "not in game") : string.Empty, s.Value, diff);
            }));

            if (comparing)
                _scroll.Insert(3, Notice(differences == 0
                    ? StoryText.T("El juego coincide con lo que dice el grafo.", "The game matches the graph.")
                    : StoryText.T($"{differences} diferencias con el juego: algún paso hace algo que no declara, o algo de fuera del grafo lo cambió.",
                                  $"{differences} differences with the game: a step does something it does not declare, or something outside the graph changed it."),
                    differences == 0));

            if (recorder.Notes.Count > 0)
                Section(StoryText.T("A revisar", "To review"), recorder.Notes.Select(n => (VisualElement)Muted(n)));
        }

        static string CategoryLabel(string category) => category switch
        {
            NarrativeCoreFacts.Scene => StoryText.T("Escenas cargadas", "Loaded scenes"),
            NarrativeCoreFacts.Loaded => StoryText.T("Cargado", "Loaded"),
            NarrativeCoreFacts.Counter => StoryText.T("Contadores", "Counters"),
            _ => Humanize(category),
        };

        /// "HoraDelDia" → "Hora del dia", "time_of_day" → "Time of day".
        static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            var sb = new System.Text.StringBuilder(key.Length + 8);
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c == '_' || c == '-') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && char.IsLower(key[i - 1])) { sb.Append(' '); sb.Append(char.ToLowerInvariant(c)); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string QuestText(string state) => state switch
        {
            RecordingStateWriter.QuestDone => StoryText.T("Hecha", "Done"),
            RecordingStateWriter.QuestActive => StoryText.T("En curso", "Active"),
            _ => StoryText.T("Sin empezar", "Not started"),
        };

        void Section(string title, IEnumerable<VisualElement> rows)
        {
            var list = rows.ToList();
            if (list.Count == 0) return;
            var box = new VisualElement();
            box.AddToClassList("story-panel__section");
            box.Add(Kicker(title.ToUpperInvariant()));
            foreach (var r in list) box.Add(r);
            _scroll.Add(box);
        }

        VisualElement Row(string name, string value, string sourceGuid, bool warn)
        {
            var row = new VisualElement();
            row.AddToClassList("story-panel__row");
            row.EnableInClassList("is-warning", warn);
            var n = new Label(name) { tooltip = name };
            n.AddToClassList("story-panel__row-name");
            row.Add(n);
            var v = new Label(value);
            v.AddToClassList("story-panel__row-value");
            row.Add(v);
            if (!string.IsNullOrEmpty(sourceGuid))
            {
                Button link;
                var source = _graph.FindNode(sourceGuid);
                if (source != null)
                {
                    link = new Button(() => NavigateTo?.Invoke(sourceGuid)) { text = _structure?.Number(source), tooltip = source.displayTitle };
                }
                else if (_recorder != null && _recorder.SourceGraph.TryGetValue(sourceGuid, out var other) && other != null)
                {
                    // Left this way by an earlier chapter: open it there.
                    var step = other.FindNode(sourceGuid);
                    link = new Button(() => OpenInGraph?.Invoke(other, sourceGuid)) { text = other.name, tooltip = step?.displayTitle ?? string.Empty };
                }
                else link = new Button { text = "?" };
                link.AddToClassList("story-panel__row-source");
                row.Add(link);
            }
            return row;
        }

        static Label Kicker(string text) { var l = new Label(text); l.AddToClassList("story-kicker"); return l; }
        static Label Muted(string text) { var l = new Label(text); l.AddToClassList("story-muted"); return l; }

        static VisualElement Notice(string text, bool ok)
        {
            var l = new Label(text);
            l.AddToClassList("story-notice");
            l.AddToClassList(ok ? "is-ok" : "is-warning");
            return l;
        }

        /// The running game, when it is at the step being looked at.
        sealed class LiveGame
        {
            public bool Available;
            public string Reason;
            IQuestService _quests;

            public static LiveGame For(NarrativeGraph graph, NarrativeNode node)
            {
                var live = new LiveGame();
                if (!EditorApplication.isPlaying)
                {
                    live.Reason = StoryText.T("Entra en Play desde este paso para comparar con el juego.", "Enter Play from this step to compare with the game.");
                    return live;
                }
                var runner = NarrativeGraphHub.Instance?.GetAllRunners().FirstOrDefault(r => r != null && r.graph == graph);
                var current = runner?.CurrentNode?.guid ?? runner?.Blackboard?.Get<string>(NarrativeRunner.CurrentNodeKey, null);
                if (current != node.guid)
                {
                    live.Reason = StoryText.T("El juego no está en este paso ahora mismo: elige el paso en el que está para comparar.",
                                              "The game is not at this step right now: pick the step it is at to compare.");
                    return live;
                }
                live.Available = true;
                NarrativeServices.TryGet(out live._quests, nameof(StoryWorldPanel));
                return live;
            }

            public string QuestState(string questId)
            {
                if (_quests == null) return null;
                return _quests.GetState(questId) switch
                {
                    NarrativeQuestState.Completed => RecordingStateWriter.QuestDone,
                    NarrativeQuestState.NotStarted => null,
                    _ => RecordingStateWriter.QuestActive,
                };
            }

            public bool QuestMatches(string questId, string expected) => _quests == null || QuestState(questId) == expected;

            public bool SignalRaised(string key) => DefaultNarrativeSignals.Instance == null || DefaultNarrativeSignals.Instance.HasEverRaised(key);
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
