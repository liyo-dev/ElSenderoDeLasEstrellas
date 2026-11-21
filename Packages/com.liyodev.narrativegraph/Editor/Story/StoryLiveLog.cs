using System;
using System.Collections.Generic;
using UnityEditor;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// Registro, con hora, de los pasos por los que pasa la partida (los más recientes primero).
    /// Se vacía al entrar en Play; fuera de Play se conserva el de la última partida. Lo enseña la
    /// pestaña «En vivo».
    public static class StoryLiveLog
    {
        public readonly struct Entry
        {
            public readonly string Time;
            public readonly NarrativeGraph Graph;
            public readonly string Guid;
            public readonly string Text;

            public Entry(string time, NarrativeGraph graph, string guid, string text)
            {
                Time = time;
                Graph = graph;
                Guid = guid;
                Text = text;
            }
        }

        const int MaxEntries = 60;
        static readonly List<Entry> s_entries = new();

        public static IReadOnlyList<Entry> Entries => s_entries;

        /// Ha entrado un paso nuevo o se ha vaciado el registro.
        public static event Action Changed;

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
            s_entries.Clear();
            Changed?.Invoke();
        }

        static void OnStep(NarrativeRunner runner, NarrativeNode node)
        {
            if (runner == null || node == null || node.IsAnnotation) return;
            string chapter = runner.graph != null ? runner.graph.name + " · " : string.Empty;
            s_entries.Insert(0, new Entry(DateTime.Now.ToString("HH:mm:ss"), runner.graph, node.Guid, chapter + NarrativeStartPlanner.Title(node)));
            if (s_entries.Count > MaxEntries) s_entries.RemoveAt(s_entries.Count - 1);
            Changed?.Invoke();
        }
    }
}
