using System;
using System.Collections.Generic;
using System.Linq;
using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// How "Play from this step" starts the game. The graph ships a basic one; a game with its own
    /// boot (save slots, loading screens...) implements this and wins by having a higher Priority.
    public interface INarrativePlayLauncher
    {
        /// Name shown in the settings.
        string Name { get; }

        /// The launcher with the highest priority is used. The basic one is 0.
        int Priority { get; }

        /// Prepares the game so it starts at <paramref name="stepGuid"/> and enters Play.
        /// <paramref name="choices"/>: the branch picked at decisions that several branches pass.
        bool Launch(NarrativeGraph graph, string stepGuid, IReadOnlyDictionary<string, int> choices, out string message);
    }

    public static class NarrativePlayLaunchers
    {
        /// The launcher in use: the game's if there is one, otherwise the basic one.
        public static INarrativePlayLauncher Current
            => TypeCache.GetTypesDerivedFrom<INarrativePlayLauncher>()
                .Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (INarrativePlayLauncher)Activator.CreateInstance(t))
                .OrderByDescending(l => l.Priority)
                .FirstOrDefault() ?? new BasicPlayLauncher();
    }

    /// Basic start: plays the start scene chosen in the settings (or the open scene), applies the
    /// story before the step through the game's connections and starts the graph at the step.
    public sealed class BasicPlayLauncher : INarrativePlayLauncher
    {
        const string StartScenePref = "NarrativeGraph.Play.StartScene";
        const string RestoreKey = "NarrativeGraph.Play.RestoreStartScene";

        public string Name => StoryText.T("Básico (incluido)", "Basic (built in)");
        public int Priority => 0;

        /// Scene the test starts from (empty = the scene open in the editor).
        public static string StartScene
        {
            get => EditorPrefs.GetString(StartScenePref, string.Empty);
            set => EditorPrefs.SetString(StartScenePref, value ?? string.Empty);
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreKey, false)) return;
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(RestoreKey, false);
        }

        public bool Launch(NarrativeGraph graph, string stepGuid, IReadOnlyDictionary<string, int> choices, out string message)
        {
            message = null;
            if (EditorApplication.isPlaying) { message = StoryText.T("Ya estás en Play.", "Already playing."); return false; }
            if (graph == null || string.IsNullOrEmpty(stepGuid)) { message = StoryText.T("Elige un paso.", "Pick a step."); return false; }

            var request = new NarrativeQuickStart.Request { graphPath = AssetDatabase.GetAssetPath(graph), stepGuid = stepGuid };
            foreach (var chapter in NarrativeChapterChain.Before(graph, stepGuid))
            {
                request.previousGraphPaths.Add(AssetDatabase.GetAssetPath(chapter.Graph));
                request.previousBridgeGuids.Add(chapter.BridgeGuid);
            }
            if (choices != null)
                foreach (var pair in choices) { request.choiceNodes.Add(pair.Key); request.choicePorts.Add(pair.Value); }
            NarrativeQuickStart.Schedule(request);

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScene);
            if (scene != null)
            {
                EditorSceneManager.playModeStartScene = scene;
                SessionState.SetBool(RestoreKey, true);
            }
            EditorApplication.isPlaying = true;
            return true;
        }
    }
}
