using System;
using System.Collections.Generic;

/// Works out how the world looks when the story reaches a node, without playing it.
///
/// Only what happens before the target is replayed, in story order:
///  - A plain node or a fork continues through all its outputs. Parallel branches that do not lead
///    to the target are assumed to have finished before it.
///  - A node with a fixed projection port (a normal path plus an error output) follows that port.
///  - A decision (ProjectionPort == ProjectionStops) follows the branch that leads to the target.
///    When several do, the caller's choice is used, or else the first of them. Every decision met
///    is reported, so a tool can show it and let the user pick another branch.
///  - Nothing is projected through the target, and the target itself is not projected: when the
///    story starts there, it runs for real.
///
/// Effects are applied in topological order of the route (cycles are broken by discovery order),
/// so "the last placement wins" means the last one in story time, not the last one found.
public static class NarrativeStateProjector
{
    public sealed class Decision
    {
        public string NodeGuid;
        public string Title;
        /// Port names, one per output of the node.
        public string[] Options;
        /// Ports whose branch leads to the target.
        public int[] PortsToTarget;
        public int ChosenPort;
        /// Only one branch leads to the target, so there was nothing to choose.
        public bool Forced;
        public string Description;

        public string ChosenOption => ChosenPort >= 0 && Options != null && ChosenPort < Options.Length ? Options[ChosenPort] : "?";

        public override string ToString()
            => Forced ? $"{Description} → «{ChosenOption}» (the target is on that branch)"
                      : $"{Description} → «{ChosenOption}»";
    }

    public sealed class Result
    {
        public bool ReachedTarget;
        /// Guids of the projected nodes, in story order.
        public readonly List<string> Order = new();
        public readonly List<Decision> Decisions = new();
        /// Connections the story went through (from guid, to guid), the target included.
        public readonly List<KeyValuePair<string, string>> Edges = new();
    }

    /// <param name="choices">Branch to follow per decision node (guid → port), when several lead to the target.</param>
    public static Result Project(NarrativeGraph graph, string targetGuid, INarrativeStateWriter state,
                                 IReadOnlyDictionary<string, int> choices = null)
    {
        var result = new Result();
        if (graph == null || graph.nodes == null || state == null
            || string.IsNullOrEmpty(graph.startNodeGuid) || string.IsNullOrEmpty(targetGuid))
            return result;

        var byGuid = IndexNodes(graph);
        if (!byGuid.ContainsKey(graph.startNodeGuid)) return result;
        // Loops (retries, "go back and ask again") are cut at the edge that returns, so they
        // neither pull later steps in nor hide the order of the story.
        // "Comes before" is the connections plus the signals: a step that waits for a signal goes
        // after the steps of this graph that raise it.
        var links = BuildLinks(byGuid);
        var backEdges = FindBackEdges(graph.startNodeGuid, links);
        var leadsToTarget = FindNodesLeadingTo(targetGuid, links, backEdges);
        // Whatever comes after the target cannot have happened yet, even through a parallel branch.
        var afterTarget = FindNodesAfter(targetGuid, links, backEdges);
        // A step the story never reaches has no past to rebuild.
        if (!leadsToTarget.Contains(graph.startNodeGuid)) return result;

        // 1. Walk the route from the start, recording the branches taken.
        var discovery = new Dictionary<string, int>();
        var successors = new Dictionary<string, List<string>>();
        var chosenPorts = new Dictionary<string, int>();
        var queue = new Queue<string>();
        discovery[graph.startNodeGuid] = 0;
        queue.Enqueue(graph.startNodeGuid);

        while (queue.Count > 0)
        {
            var guid = queue.Dequeue();
            if (guid == targetGuid) continue;
            var node = byGuid[guid];
            var next = new List<string>();
            successors[guid] = next;

            foreach (var outGuid in NextGuids(node, targetGuid, leadsToTarget, choices, result, chosenPorts))
            {
                if (!byGuid.ContainsKey(outGuid)) continue;
                if (backEdges.Contains((guid, outGuid)) || afterTarget.Contains(outGuid)) continue;
                if (outGuid == targetGuid) result.ReachedTarget = true;
                next.Add(outGuid);
                result.Edges.Add(new KeyValuePair<string, string>(guid, outGuid));
                if (discovery.ContainsKey(outGuid)) continue;
                discovery[outGuid] = discovery.Count;
                queue.Enqueue(outGuid);
            }
        }

        // 2. Story order: topological over the route, ties and cycles resolved by discovery order.
        discovery.Remove(targetGuid);
        var trace = state as INarrativeProjectionTrace;
        foreach (var guid in TopologicalOrder(discovery, successors))
        {
            result.Order.Add(guid);
            var node = byGuid[guid];
            try
            {
                trace?.OnProjectNode(node);
                if (node is INarrativeStateEffect effect) effect.Project(state);
                if (node is INarrativeRouteEffect routeEffect && chosenPorts.TryGetValue(guid, out var port))
                    routeEffect.ProjectRoute(state, port);
            }
            catch (Exception e)
            {
                state.Note($"«{node.displayTitle}» ({node.GetType().Name}) could not project its effect: {e.Message}");
            }
        }

        return result;
    }

    static Dictionary<string, NarrativeNode> IndexNodes(NarrativeGraph graph)
    {
        var byGuid = new Dictionary<string, NarrativeNode>();
        foreach (var n in graph.nodes)
            if (n != null && !string.IsNullOrEmpty(n.guid) && !byGuid.ContainsKey(n.guid))
                byGuid.Add(n.guid, n);
        return byGuid;
    }

    static Dictionary<string, List<string>> BuildLinks(Dictionary<string, NarrativeNode> byGuid)
    {
        var links = new Dictionary<string, List<string>>();
        var raisers = new Dictionary<string, List<string>>();
        var listeners = new List<(string guid, string key)>();
        foreach (var node in byGuid.Values)
        {
            var next = new List<string>();
            if (node.outputs != null)
                foreach (var g in node.outputs)
                    if (!string.IsNullOrEmpty(g) && byGuid.ContainsKey(g)) next.Add(g);
            links[node.guid] = next;

            NarrativeNodeReferences refs;
            try { refs = NarrativeNodeReferences.Of(node); }
            catch (Exception) { continue; }
            foreach (var sig in refs.Signals)
            {
                if (string.IsNullOrWhiteSpace(sig.Key)) continue;
                if (sig.Role == SignalRole.Emite)
                {
                    if (!raisers.TryGetValue(sig.Key, out var list)) raisers[sig.Key] = list = new List<string>();
                    list.Add(node.guid);
                }
                else if (sig.Role == SignalRole.Escucha) listeners.Add((node.guid, sig.Key));
            }
        }
        foreach (var (guid, key) in listeners)
            if (raisers.TryGetValue(key, out var from))
                foreach (var r in from)
                    if (r != guid && !links[r].Contains(guid)) links[r].Add(guid);
        return links;
    }

    /// Links that close a loop when the graph is walked depth-first from the start.
    static HashSet<(string, string)> FindBackEdges(string startGuid, Dictionary<string, List<string>> links)
    {
        var back = new HashSet<(string, string)>();
        var state = new Dictionary<string, int>(); // 1 = on the current path, 2 = finished
        var stack = new Stack<(string guid, int next)>();
        stack.Push((startGuid, 0));
        state[startGuid] = 1;
        while (stack.Count > 0)
        {
            var (guid, next) = stack.Pop();
            var outs = links[guid];
            if (next >= outs.Count)
            {
                state[guid] = 2;
                continue;
            }
            stack.Push((guid, next + 1));
            var outGuid = outs[next];
            if (!state.TryGetValue(outGuid, out var seen)) { state[outGuid] = 1; stack.Push((outGuid, 0)); }
            else if (seen == 1) back.Add((guid, outGuid));
        }
        return back;
    }

    static HashSet<string> FindNodesAfter(string targetGuid, Dictionary<string, List<string>> links,
                                          HashSet<(string, string)> backEdges)
    {
        var found = new HashSet<string>();
        if (!links.ContainsKey(targetGuid)) return found;
        var stack = new Stack<string>();
        stack.Push(targetGuid);
        while (stack.Count > 0)
        {
            var guid = stack.Pop();
            foreach (var outGuid in links[guid])
            {
                if (outGuid == targetGuid || backEdges.Contains((guid, outGuid))) continue;
                if (found.Add(outGuid)) stack.Push(outGuid);
            }
        }
        return found;
    }

    static HashSet<string> FindNodesLeadingTo(string targetGuid, Dictionary<string, List<string>> links,
                                              HashSet<(string, string)> backEdges)
    {
        var predecessors = new Dictionary<string, List<string>>();
        foreach (var pair in links)
            foreach (var outGuid in pair.Value)
            {
                if (backEdges.Contains((pair.Key, outGuid))) continue;
                if (!predecessors.TryGetValue(outGuid, out var list)) predecessors[outGuid] = list = new List<string>();
                list.Add(pair.Key);
            }

        var found = new HashSet<string> { targetGuid };
        var stack = new Stack<string>();
        stack.Push(targetGuid);
        while (stack.Count > 0)
        {
            if (!predecessors.TryGetValue(stack.Pop(), out var list)) continue;
            foreach (var p in list)
                if (found.Add(p)) stack.Push(p);
        }
        return found;
    }

    static IEnumerable<string> NextGuids(NarrativeNode node, string targetGuid, HashSet<string> leadsToTarget,
                                         IReadOnlyDictionary<string, int> choices, Result result,
                                         Dictionary<string, int> chosenPorts)
    {
        if (node.outputs == null || node.outputs.Count == 0) yield break;

        int port = node.ProjectionPort;
        if (port == NarrativeNode.ProjectAllOutputs)
        {
            foreach (var g in node.outputs)
                if (!string.IsNullOrEmpty(g)) yield return g;
            yield break;
        }

        if (port >= 0)
        {
            chosenPorts[node.guid] = port;
            var g = node.GetOutputGuid(port);
            if (g != null) yield return g;
            yield break;
        }

        var decision = Decide(node, leadsToTarget, choices);
        if (decision == null) yield break;
        result.Decisions.Add(decision);
        chosenPorts[node.guid] = decision.ChosenPort;
        var chosen = node.GetOutputGuid(decision.ChosenPort);
        if (chosen != null) yield return chosen;
    }

    static Decision Decide(NarrativeNode node, HashSet<string> leadsToTarget, IReadOnlyDictionary<string, int> choices)
    {
        var connected = new List<int>();
        var toTarget = new List<int>();
        for (int i = 0; i < node.outputs.Count; i++)
        {
            var g = node.GetOutputGuid(i);
            if (g == null) continue;
            connected.Add(i);
            if (leadsToTarget.Contains(g)) toTarget.Add(i);
        }
        if (connected.Count == 0) return null;

        var candidates = toTarget.Count > 0 ? toTarget : connected;
        int chosen = candidates[0];
        if (choices != null && choices.TryGetValue(node.guid, out var wanted) && candidates.Contains(wanted))
            chosen = wanted;

        var names = node.GetOutputPorts();
        var options = new string[node.outputs.Count];
        for (int i = 0; i < options.Length; i++)
            options[i] = names != null && i < names.Length ? names[i] : $"Output {i + 1}";

        return new Decision
        {
            NodeGuid = node.guid,
            Title = node.displayTitle,
            Options = options,
            PortsToTarget = toTarget.ToArray(),
            ChosenPort = chosen,
            Forced = toTarget.Count == 1,
            Description = node.DescribeDecision(),
        };
    }

    static List<string> TopologicalOrder(Dictionary<string, int> discovery, Dictionary<string, List<string>> successors)
    {
        var indegree = new Dictionary<string, int>();
        foreach (var guid in discovery.Keys) indegree[guid] = 0;
        foreach (var pair in successors)
            foreach (var s in pair.Value)
                if (indegree.ContainsKey(s)) indegree[s]++;

        var pending = new List<string>(discovery.Keys);
        pending.Sort((a, b) => discovery[a].CompareTo(discovery[b]));
        var order = new List<string>(pending.Count);
        var done = new HashSet<string>();

        while (order.Count < pending.Count)
        {
            string next = null;
            foreach (var guid in pending)
                if (!done.Contains(guid) && indegree[guid] == 0) { next = guid; break; }

            // A cycle (a retry loop): release the earliest discovered node still waiting.
            if (next == null)
                foreach (var guid in pending)
                    if (!done.Contains(guid)) { next = guid; break; }

            done.Add(next);
            order.Add(next);
            if (!successors.TryGetValue(next, out var outs)) continue;
            foreach (var s in outs)
                if (indegree.ContainsKey(s) && !done.Contains(s)) indegree[s]--;
        }
        return order;
    }
}
