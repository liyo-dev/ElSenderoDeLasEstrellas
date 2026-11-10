using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Basic "play from this step": when the editor asks for it, on entering Play the story before
/// the step is applied to the game and the graph starts at the step. Games with their own boot
/// replace it from the editor (INarrativePlayLauncher) and never reach this.
public static class NarrativeQuickStart
{
    [Serializable]
    public sealed class Request
    {
        public string graphPath;
        public string stepGuid;
        public List<string> previousGraphPaths = new();
        public List<string> previousBridgeGuids = new();
        public List<string> choiceNodes = new();
        public List<int> choicePorts = new();
    }

    const string Key = "NarrativeGraph.QuickStart.Request";

#if UNITY_EDITOR
    public static void Schedule(Request request) => UnityEditor.SessionState.SetString(Key, JsonUtility.ToJson(request));

    static Request Take()
    {
        var json = UnityEditor.SessionState.GetString(Key, string.Empty);
        UnityEditor.SessionState.EraseString(Key);
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Request>(json);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnPlay()
    {
        var request = Take();
        if (request == null) return;
        var host = new GameObject("[Narrative quick start]");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Host>().Run(request);
    }

    sealed class Host : MonoBehaviour
    {
        public void Run(Request request) => StartCoroutine(Start(request));

        IEnumerator Start(Request request)
        {
            // One frame so the game's own Awake/Start register their connections first.
            yield return null;

            var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<NarrativeGraph>(request.graphPath);
            if (graph == null) { Debug.LogWarning($"[Narrative] Quick start: graph not found ({request.graphPath})."); Destroy(gameObject); yield break; }

            var choices = new Dictionary<string, int>();
            for (int i = 0; i < request.choiceNodes.Count && i < request.choicePorts.Count; i++) choices[request.choiceNodes[i]] = request.choicePorts[i];

            var writer = new NarrativeServicesStateWriter();
            for (int i = 0; i < request.previousGraphPaths.Count; i++)
            {
                var previous = UnityEditor.AssetDatabase.LoadAssetAtPath<NarrativeGraph>(request.previousGraphPaths[i]);
                if (previous != null) NarrativeStateProjector.Project(previous, request.previousBridgeGuids[i], writer, choices);
            }
            NarrativeStateProjector.Project(graph, request.stepGuid, writer, choices);
            foreach (var note in writer.Notes) Debug.Log("[Narrative] Quick start: " + note);

            var runner = FindRunner(graph);
            if (runner == null)
            {
                runner = new GameObject($"[Narrative] {graph.name}").AddComponent<NarrativeRunner>();
                runner.graph = graph;
            }
            runner.StartFromNode(request.stepGuid);
            Destroy(gameObject);
        }

        static NarrativeRunner FindRunner(NarrativeGraph graph)
        {
            var hub = NarrativeGraphHub.Instance;
            var fromHub = hub != null ? hub.GetAllRunners().FirstOrDefault(r => r != null && r.graph == graph) : null;
            return fromHub != null ? fromHub : FindObjectsByType<NarrativeRunner>(FindObjectsSortMode.None).FirstOrDefault(r => r.graph == graph);
        }
    }
#endif
}
