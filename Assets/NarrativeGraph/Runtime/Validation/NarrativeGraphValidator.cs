using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Valida la configuración de los grafos narrativos para detectar errores comunes.
/// </summary>
public static class NarrativeGraphValidator
{
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        
        public void LogResults(string graphName)
        {
            if (IsValid && Warnings.Count == 0)
            {
                NarrativeLog.Info($"[NarrativeGraphValidator] ✅ Grafo '{graphName}' es válido");
                return;
            }
            
            if (Errors.Count > 0)
            {
                Debug.LogError($"[NarrativeGraphValidator] ❌ Grafo '{graphName}' tiene {Errors.Count} error(es):");
                foreach (var error in Errors)
                {
                    Debug.LogError($"  • {error}");
                }
            }
            
            // Los avisos son informativos: solo salen en editor o con instrumentación.
            if (Warnings.Count > 0)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[NarrativeGraphValidator] ℹ️ Grafo '{graphName}' tiene {Warnings.Count} advertencia(s):");
                foreach (var warning in Warnings)
                {
                    Debug.Log($"[NarrativeGraphValidator]   • {warning}");
                }
#endif
            }
        }
    }
    
    /// <summary>
    /// Valida un grafo narrativo completo.
    /// </summary>
    public static ValidationResult ValidateGraph(NarrativeGraph graph)
    {
        var result = new ValidationResult();
        
        if (graph == null)
        {
            result.Errors.Add("El grafo es null");
            return result;
        }
        
        // Validar que tiene un StartNode
        if (string.IsNullOrEmpty(graph.startNodeGuid))
        {
            result.Errors.Add("No hay StartNode definido. Marca un nodo como 'Set as Start'");
        }
        else
        {
            var startNode = graph.FindNode(graph.startNodeGuid);
            if (startNode == null)
            {
                result.Errors.Add($"StartNode con GUID '{graph.startNodeGuid}' no existe");
            }
        }
        
        // Validar que no hay nodos huérfanos (sin conexiones)
        var orphanNodes = FindOrphanNodes(graph);
        if (orphanNodes.Count > 0)
        {
            result.Warnings.Add($"Hay {orphanNodes.Count} nodo(s) huérfano(s) sin conexiones: {string.Join(", ", orphanNodes.Select(n => n.GetType().Name))}");
        }
        
        // Validar que todos los nodos tienen GUIDs únicos
        var duplicateGuids = FindDuplicateGuids(graph);
        if (duplicateGuids.Count > 0)
        {
            result.Errors.Add($"Hay GUIDs duplicados: {string.Join(", ", duplicateGuids)}");
        }
        
        // Lo demás lo declara cada nodo sobre sí mismo; el validador no conoce tipos de nodo.
        ValidateSavePoints(graph, result);
        CollectNodeWarnings(graph, result);
        ValidateQuestStarts(graph, result);

        return result;
    }
    
    /// <summary>Avisos que declara cada nodo sobre su propio contenido (NarrativeNode.CollectWarnings).</summary>
    static void CollectNodeWarnings(NarrativeGraph graph, ValidationResult result)
    {
        var avisos = new List<string>();
        foreach (var node in graph.nodes)
        {
            if (node == null) continue;
            avisos.Clear();
            node.CollectWarnings(avisos);
            foreach (var aviso in avisos)
                result.Warnings.Add($"«{node.displayTitle}»: {aviso}");
        }
    }

    static List<NarrativeNode> FindOrphanNodes(NarrativeGraph graph)
    {
        var orphans = new List<NarrativeNode>();
        var connectedNodes = new HashSet<string>();
        
        // Marcar el StartNode como conectado
        if (!string.IsNullOrEmpty(graph.startNodeGuid))
        {
            connectedNodes.Add(graph.startNodeGuid);
            TraverseFromNode(graph, graph.startNodeGuid, connectedNodes);
        }
        
        // Los nodos no alcanzados son huérfanos
        foreach (var node in graph.nodes)
        {
            if (node == null)
            {
                UnityEngine.Debug.LogWarning($"[NarrativeGraphValidator] Nodo null encontrado en el grafo '{graph.name}', saltando validación de huérfanos para este nodo.");
                continue;
            }
            
            if (!connectedNodes.Contains(node.guid))
            {
                orphans.Add(node);
            }
        }
        
        return orphans;
    }
    
    static void TraverseFromNode(NarrativeGraph graph, string nodeGuid, HashSet<string> visited)
    {
        var node = graph.FindNode(nodeGuid);
        if (node == null || node.outputs == null) return;
        
        foreach (var outputGuid in node.outputs)
        {
            if (string.IsNullOrEmpty(outputGuid)) continue;
            
            if (!visited.Contains(outputGuid))
            {
                visited.Add(outputGuid);
                TraverseFromNode(graph, outputGuid, visited);
            }
        }
    }
    
    static List<string> FindDuplicateGuids(NarrativeGraph graph)
    {
        var guids = new Dictionary<string, int>();
        
        foreach (var node in graph.nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.guid)) continue;
            
            if (!guids.ContainsKey(node.guid))
                guids[node.guid] = 0;
            
            guids[node.guid]++;
        }
        
        return guids.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
    }
    
    /// <summary>
    /// Una misión que inician varios nodos suele ser un despiste (copiar y pegar un tramo). Lo
    /// que inicia cada nodo se sabe proyectando su efecto (INarrativeStateEffect), sin ejecutarlo.
    /// </summary>
    static void ValidateQuestStarts(NarrativeGraph graph, ValidationResult result)
    {
        var recorder = new QuestStartRecorder();
        foreach (var node in graph.nodes)
        {
            if (node is not INarrativeStateEffect effect) continue;
            recorder.Begin();
            try { effect.Project(recorder); }
            catch (System.Exception e)
            {
                result.Warnings.Add($"«{node.displayTitle}»: no se pudo proyectar su efecto ({e.Message})");
                continue;
            }
            foreach (var questId in recorder.StartedByThisNode)
            {
                if (!recorder.StartedBefore.Add(questId))
                    result.Warnings.Add($"La quest '{questId}' la inicia más de un nodo del grafo");
            }
        }
    }

    /// <summary>Escritor de estado que solo anota qué misiones inicia cada nodo.</summary>
    sealed class QuestStartRecorder : INarrativeStateWriter
    {
        public readonly HashSet<string> StartedBefore = new();
        public readonly HashSet<string> StartedByThisNode = new();

        public void Begin() => StartedByThisNode.Clear();

        public void StartQuest(string questId)
        {
            if (!string.IsNullOrWhiteSpace(questId)) StartedByThisNode.Add(questId);
        }

        public bool GetFlag(string key) => false;
        public void SetFlag(string key, bool value) { }
        public void CompleteQuestSteps(string questId, IReadOnlyList<string> stepConditionIds, IReadOnlyList<int> stepIndices) { }
        public void CompleteQuest(string questId) { }
        public void AddItem(string itemId, int amount) { }
        public void PlaceActor(string actorId, NarrativeLocation location) { }
        public void SetActorActive(string actorId, bool active) { }
        public void Note(string message) { }
        public bool TryGetExtension<T>(out T extension) where T : class { extension = null; return false; }
    }

    static void ValidateSavePoints(NarrativeGraph graph, ValidationResult result)
    {
        int safeNodes = 0;
        int unsafeNodes = 0;
        
        foreach (var node in graph.nodes)
        {
            if (node == null) continue;
            
            var nodeType = node.GetType();
            var hasSavePoint = nodeType.GetCustomAttributes(typeof(SavePointAttribute), false).Length > 0;
            var hasUnsafe = nodeType.GetCustomAttributes(typeof(UnsafeForSaveAttribute), false).Length > 0;
            
            if (hasSavePoint) safeNodes++;
            if (hasUnsafe) unsafeNodes++;
        }
        
        if (safeNodes == 0 && graph.nodes.Count > 0)
        {
            result.Warnings.Add($"No hay nodos marcados como [SavePoint]. Considera marcar los nodos de espera como seguros para guardar.");
        }
        
        if (unsafeNodes > 0)
        {
            result.Warnings.Add($"Hay {unsafeNodes} nodo(s) marcados como [UnsafeForSave]. Ten cuidado al guardar durante su ejecución.");
        }
    }
}
