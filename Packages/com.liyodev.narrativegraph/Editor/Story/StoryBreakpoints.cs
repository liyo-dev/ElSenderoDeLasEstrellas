using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor.Story
{
    /// Steps where Play pauses when the story reaches them. Kept per user and per graph, never in
    /// the graph asset, so they do not end up in version control.
    [InitializeOnLoad]
    public static class StoryBreakpoints
    {
        public static event Action Changed;

        static StoryBreakpoints()
        {
            NarrativeRunner.StepEntered -= OnStepEntered;
            NarrativeRunner.StepEntered += OnStepEntered;
        }

        public static bool Has(NarrativeGraph graph, string stepGuid) => Load(graph).Contains(stepGuid);

        public static void Toggle(NarrativeGraph graph, string stepGuid)
        {
            if (graph == null || string.IsNullOrEmpty(stepGuid)) return;
            var set = Load(graph);
            if (!set.Remove(stepGuid)) set.Add(stepGuid);
            EditorPrefs.SetString(Key(graph), string.Join(";", set));
            Changed?.Invoke();
        }

        public static void Clear(NarrativeGraph graph)
        {
            if (graph == null) return;
            EditorPrefs.DeleteKey(Key(graph));
            Changed?.Invoke();
        }

        /// Raised when Play stops at a breakpoint: the graph and the step.
        public static event Action<NarrativeGraph, NarrativeNode> Hit;

        static void OnStepEntered(NarrativeRunner runner, NarrativeNode step)
        {
            if (runner == null || runner.graph == null || step == null || !Has(runner.graph, step.guid)) return;
            Debug.Log(StoryText.T($"[Historia] Pausa en «{step.displayTitle}».", $"[Story] Paused at “{step.displayTitle}”."));
            Debug.Break();
            Hit?.Invoke(runner.graph, step);
        }

        static HashSet<string> Load(NarrativeGraph graph)
        {
            if (graph == null) return new HashSet<string>();
            var raw = EditorPrefs.GetString(Key(graph), string.Empty);
            return new HashSet<string>(raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        static string Key(NarrativeGraph graph)
            => "NarrativeGraph.Breakpoints." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph));
    }
}
