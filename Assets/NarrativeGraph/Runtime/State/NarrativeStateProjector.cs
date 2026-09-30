using System.Collections.Generic;

/// Calcula cómo está el mundo al llegar a un nodo, sin jugar la historia.
///
/// Recorre el grafo en anchura desde su StartNode y, por cada nodo que implementa
/// INarrativeStateEffect, llama a Project() sobre el escritor. El nodo objetivo NO se proyecta:
/// cuando se arranca desde él, se va a ejecutar de verdad.
///
/// Por dónde sigue en cada nodo lo decide el propio nodo (NarrativeNode.ProjectionPort):
///  - ProjectAllOutputs: todas las salidas (paso normal o fork, la misma regla que el runner).
///  - un índice: solo esa salida (el camino normal de un nodo con salida de error).
///  - ProjectionStops: decisión real (depende del jugador o del estado): no se adivina; se
///    registra en Decisions con NarrativeNode.DescribeDecision() y se sigue con el resto de la cola.
public static class NarrativeStateProjector
{
    public sealed class Result
    {
        public bool ReachedTarget;
        public readonly List<string> Decisions = new();
    }

    public static Result Project(NarrativeGraph graph, string targetGuid, INarrativeStateWriter state)
    {
        var result = new Result();
        if (graph == null || graph.nodes == null || state == null
            || string.IsNullOrEmpty(graph.startNodeGuid) || string.IsNullOrEmpty(targetGuid))
            return result;

        var byGuid = new Dictionary<string, NarrativeNode>();
        foreach (var n in graph.nodes)
            if (n != null && !string.IsNullOrEmpty(n.guid) && !byGuid.ContainsKey(n.guid))
                byGuid.Add(n.guid, n);

        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(graph.startNodeGuid);

        while (queue.Count > 0)
        {
            var guid = queue.Dequeue();
            if (!visited.Add(guid)) continue;
            if (!byGuid.TryGetValue(guid, out var node)) continue;

            if (guid == targetGuid)
            {
                result.ReachedTarget = true;
                break;
            }

            if (node is INarrativeStateEffect effect)
                effect.Project(state);

            if (node.outputs == null || node.outputs.Count == 0) continue;

            int port = node.ProjectionPort;
            if (port == NarrativeNode.ProjectionStops)
            {
                if (HasAnyOutput(node)) result.Decisions.Add(node.DescribeDecision());
                continue;
            }

            if (port >= 0)
            {
                Enqueue(node.GetOutputGuid(port), visited, queue);
                continue;
            }

            foreach (var outGuid in node.outputs)
                Enqueue(outGuid, visited, queue);
        }

        return result;
    }

    static bool HasAnyOutput(NarrativeNode node)
    {
        foreach (var o in node.outputs)
            if (!string.IsNullOrEmpty(o)) return true;
        return false;
    }

    static void Enqueue(string guid, HashSet<string> visited, Queue<string> queue)
    {
        if (!string.IsNullOrEmpty(guid) && !visited.Contains(guid)) queue.Enqueue(guid);
    }
}
