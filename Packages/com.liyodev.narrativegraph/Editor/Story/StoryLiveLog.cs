using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Panel «En vivo» de abajo: por dónde va cada capítulo en marcha y qué espera, lo que no
    /// cuadra con el grafo (de StoryLiveReport) y el registro, con hora, de los pasos por los que
    /// ha pasado la partida. Fuera de Play enseña el último registro.
    public sealed class StoryLiveLog : VisualElement
    {
        public event Action<NarrativeGraph, string> Navigate;

        struct Entry { public string Time; public NarrativeGraph Graph; public string Guid; public string Text; }

        static readonly List<Entry> s_log = new();
        static event Action s_changed;
        const int MaxEntries = 60;

        readonly VisualElement _status;
        readonly ScrollView _list;
        Func<NarrativeGraph> _graph;

        [InitializeOnLoadMethod]
        static void Hook()
        {
            NarrativeRunner.StepEntered -= OnStep;
            NarrativeRunner.StepEntered += OnStep;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            s_log.Clear();
            s_changed?.Invoke();
        }

        static void OnStep(NarrativeRunner runner, NarrativeNode node)
        {
            if (runner == null || node == null || node.IsAnnotation) return;
            s_log.Insert(0, new Entry
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Graph = runner.graph,
                Guid = node.guid,
                Text = $"{(runner.graph != null ? runner.graph.name + " · " : string.Empty)}{NarrativeStartPlanner.Title(node)}",
            });
            if (s_log.Count > MaxEntries) s_log.RemoveAt(s_log.Count - 1);
            s_changed?.Invoke();
        }

        public StoryLiveLog(Func<NarrativeGraph> graph)
        {
            _graph = graph;
            style.flexGrow = 1;
            _status = new VisualElement { style = { paddingLeft = 10, paddingRight = 10, paddingTop = 6 } };
            Add(_status);
            _list = new ScrollView { style = { flexGrow = 1, paddingLeft = 10, paddingRight = 10 } };
            Add(_list);
            schedule.Execute(RefreshStatus).Every(1000);
            RegisterCallback<AttachToPanelEvent>(_ => { s_changed -= RefreshList; s_changed += RefreshList; RefreshList(); RefreshStatus(); });
            RegisterCallback<DetachFromPanelEvent>(_ => s_changed -= RefreshList);
        }

        void RefreshStatus()
        {
            _status.Clear();
            if (!EditorApplication.isPlaying)
            {
                _status.Add(Line(StoryText.T("Fuera de Play. Pulsa «Probar desde aquí» y verás aquí por dónde va la historia.", "Not playing. Press “Play from here” to see where the story is."), new Color(1, 1, 1, 0.55f)));
                return;
            }
            var report = StoryLiveReport.Build(_graph?.Invoke(), null);
            if (report.Runners.Count == 0) _status.Add(Line(StoryText.T("● Jugando · ningún capítulo en marcha", "● Playing · no chapter running"), StoryColors.Ok));
            foreach (var run in report.Runners.Take(3))
                _status.Add(Line($"● {run.Graph.name} · {NarrativeStartPlanner.Title(run.Step)}  —  {StoryText.T("esperando", "waiting")}: {run.Waiting}", StoryColors.Ok));
            foreach (var wrong in report.Wrong.Take(3))
                _status.Add(Line($"! {wrong.Text}: {wrong.Live}", new Color(0.95f, 0.64f, 0.62f)));
        }

        void RefreshList()
        {
            _list.Clear();
            if (s_log.Count == 0) { _list.Add(Line(StoryText.T("Aún no hay registro.", "No log yet."), new Color(1, 1, 1, 0.4f))); return; }
            foreach (var e in s_log)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingTop = 2, paddingBottom = 2 } };
                row.Add(new Label(e.Time) { style = { color = new Color(1, 1, 1, 0.45f), marginRight = 10, unityFontStyleAndWeight = FontStyle.Bold } });
                var text = new Label(e.Text) { style = { flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap } };
                row.Add(text);
                var entry = e;
                row.RegisterCallback<ClickEvent>(_ => Navigate?.Invoke(entry.Graph, entry.Guid));
                row.tooltip = StoryText.T("Ir a este paso", "Go to this step");
                _list.Add(row);
            }
        }

        static Label Line(string text, Color color) => new(text) { style = { color = color, whiteSpace = WhiteSpace.Normal, marginBottom = 2 } };
    }
}
