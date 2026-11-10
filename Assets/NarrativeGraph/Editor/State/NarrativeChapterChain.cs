using System;
using System.Collections.Generic;
using UnityEditor;

namespace NarrativeGraphEditor
{
    /// How chapters follow each other. A chapter starts after another when one of its steps waits
    /// for a signal that the other chapter raises; that step is the bridge between both.
    public static class NarrativeChapterChain
    {
        /// Which graphs count as chapters of the game when no filter is given (null = all). A game
        /// can set it from an [InitializeOnLoad] class to leave tests and samples out.
        public static Func<NarrativeGraph, bool> ChapterFilter;

        public sealed class Chapter
        {
            public NarrativeGraph Graph;
            /// Step of that chapter that raises the signal the next chapter waits for.
            public string BridgeGuid;
        }

        /// The chapters that have to be finished before reaching <paramref name="targetGuid"/>, oldest
        /// first. If the target is the entry of the chapter or the bridge itself, it moves to the first
        /// real step after them. <paramref name="include"/> leaves graphs out (null takes them all).
        public static List<Chapter> Before(NarrativeGraph graph, ref string targetGuid, Func<NarrativeGraph, bool> include = null)
        {
            var result = new List<Chapter>();
            if (graph == null || string.IsNullOrEmpty(targetGuid)) return result;

            var raisers = FindRaisers(graph, include);
            bool IsBridge(NarrativeNode n) => WaitsForOtherChapter(n, raisers, out _);

            var node = graph.FindNode(targetGuid);
            bool crossesBridge = false;
            while (node != null && (node.IsGraphEntry || IsBridge(node)) && node.outputs != null && node.outputs.Count == 1)
            {
                crossesBridge |= IsBridge(node);
                node = graph.FindNode(node.outputs[0]);
            }
            if (crossesBridge && node != null) targetGuid = node.guid;

            AddPrevious(graph, targetGuid, raisers, result, new HashSet<NarrativeGraph> { graph });
            return result;
        }

        /// The chapters finished before <paramref name="targetGuid"/>, without moving the target.
        public static List<Chapter> Before(NarrativeGraph graph, string targetGuid, Func<NarrativeGraph, bool> include = null)
        {
            var result = new List<Chapter>();
            if (graph == null || string.IsNullOrEmpty(targetGuid)) return result;
            AddPrevious(graph, targetGuid, FindRaisers(graph, include), result, new HashSet<NarrativeGraph> { graph });
            return result;
        }

        static Dictionary<string, Chapter> FindRaisers(NarrativeGraph graph, Func<NarrativeGraph, bool> include)
        {
            var raisers = new Dictionary<string, Chapter>();
            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var other = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (other == null || other == graph || other.nodes == null) continue;
                var filter = include ?? ChapterFilter;
                if (filter != null && !filter(other)) continue;
                foreach (var node in other.nodes)
                {
                    if (node == null) continue;
                    foreach (var signal in References(node).Signals)
                        if (signal.Role == SignalRole.Emite && !string.IsNullOrEmpty(signal.Key) && !raisers.ContainsKey(signal.Key))
                            raisers.Add(signal.Key, new Chapter { Graph = other, BridgeGuid = node.guid });
                }
            }
            return raisers;
        }

        static void AddPrevious(NarrativeGraph graph, string untilGuid, Dictionary<string, Chapter> raisers,
                                List<Chapter> result, HashSet<NarrativeGraph> seen)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(graph.startNodeGuid);
            while (queue.Count > 0)
            {
                var guid = queue.Dequeue();
                if (string.IsNullOrEmpty(guid) || guid == untilGuid || !visited.Add(guid)) continue;
                var node = graph.FindNode(guid);
                if (node == null) continue;
                if (WaitsForOtherChapter(node, raisers, out var previous) && seen.Add(previous.Graph))
                {
                    AddPrevious(previous.Graph, previous.BridgeGuid, raisers, result, seen);
                    result.Add(previous);
                }
                if (node.outputs != null)
                    foreach (var o in node.outputs) queue.Enqueue(o);
            }
        }

        static bool WaitsForOtherChapter(NarrativeNode node, Dictionary<string, Chapter> raisers, out Chapter previous)
        {
            previous = null;
            if (node == null) return false;
            foreach (var signal in References(node).Signals)
                if (signal.Role == SignalRole.Escucha && !string.IsNullOrEmpty(signal.Key) && raisers.TryGetValue(signal.Key, out previous))
                    return true;
            previous = null;
            return false;
        }

        static NarrativeNodeReferences References(NarrativeNode node)
        {
            try { return NarrativeNodeReferences.Of(node); }
            catch (Exception) { return new NarrativeNodeReferences(); }
        }
    }
}
