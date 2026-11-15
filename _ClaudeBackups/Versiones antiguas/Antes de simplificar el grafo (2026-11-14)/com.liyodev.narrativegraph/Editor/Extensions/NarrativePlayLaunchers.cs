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

    /// Arranque básico: da Play en la escena de arranque de los ajustes (o en la abierta), pone la
    /// historia anterior al paso a través de las conexiones del juego y empieza el grafo en el paso.
    /// Si hay ficha del jugador, aplica los puntos de arranque y los retoques: en un ScriptableObject,
    /// antes de empezar (y lo deja como estaba al salir de Play); en un componente, al entrar en Play.
    public sealed class BasicPlayLauncher : INarrativePlayLauncher
    {
        const string StartScenePref = "NarrativeGraph.Play.StartScene";
        const string RestoreKey = "NarrativeGraph.Play.RestoreStartScene";
        const string AssetSnapshotKey = "NarrativeGraph.Play.PlayerSnapshot";
        const string AssetPathKey = "NarrativeGraph.Play.PlayerAsset";
        const string ComponentValuesKey = "NarrativeGraph.Play.PlayerComponent";

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
            if (state == PlayModeStateChange.EnteredPlayMode) ApplyToComponents();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            RestorePlayerAsset();
            SessionState.EraseString(ComponentValuesKey);
            if (!SessionState.GetBool(RestoreKey, false)) return;
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(RestoreKey, false);
        }

        /// Prepara los datos del jugador según los puntos de arranque y los retoques.
        static void PreparePlayer(NarrativeGraph graph, string stepGuid, IReadOnlyDictionary<string, int> choices)
        {
            var plan = NarrativeStartPlanner.Plan(graph, stepGuid, choices);
            var sheet = plan.Sheet;
            if (sheet == null || !sheet.IsReady || plan.Layers.Count == 0) return;
            if (!sheet.IsComponent)
            {
                SessionState.SetString(AssetPathKey, AssetDatabase.GetAssetPath(sheet.template));
                SessionState.SetString(AssetSnapshotKey, EditorJsonUtility.ToJson(sheet.template));
                plan.ApplyOverrides(sheet.template, storyHasPoints: false);
                return;
            }
            var values = new NarrativePlayerValues();
            using (var preview = plan.Preview())
                NarrativePlayerValuesEditor.Set(values, preview.Data, plan.Layers.SelectMany(l => l.Values.fields).Distinct());
            SessionState.SetString(ComponentValuesKey, sheet.componentType + "\n" + JsonUtility.ToJson(values));
        }

        static void RestorePlayerAsset()
        {
            var path = SessionState.GetString(AssetPathKey, string.Empty);
            var json = SessionState.GetString(AssetSnapshotKey, string.Empty);
            SessionState.EraseString(AssetPathKey);
            SessionState.EraseString(AssetSnapshotKey);
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(json)) return;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null) EditorJsonUtility.FromJsonOverwrite(json, asset);
        }

        static void ApplyToComponents()
        {
            var stored = SessionState.GetString(ComponentValuesKey, string.Empty);
            int cut = stored.IndexOf('\n');
            if (cut <= 0) return;
            var typeName = stored.Substring(0, cut);
            var type = TypeCache.GetTypesDerivedFrom<Component>().FirstOrDefault(t => t.FullName == typeName);
            var values = JsonUtility.FromJson<NarrativePlayerValues>(stored.Substring(cut + 1));
            if (type == null || values == null) return;
            foreach (var target in UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include))
                NarrativePlayerValuesEditor.Apply(values, target);
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
            PreparePlayer(graph, stepGuid, choices);

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
