using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

/// <summary>
/// Hub simplificado para ejecutar múltiples grafos narrativos.
/// Todos los grafos están siempre activos y esperando eventos.
/// No hay auto-start: los grafos se activan mediante señales/eventos.
/// </summary>
[DefaultExecutionOrder(-450)]
public sealed class NarrativeGraphHub : MonoBehaviour
{
    static NarrativeGraphHub _instance;
    public static NarrativeGraphHub Instance => _instance;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _instance = null; }
#endif

    [Serializable]
    public class GraphSlot
    {
        public string label = "Graph";
        public NarrativeGraph graph;
        [Tooltip("Valores iniciales del blackboard (opcional).")]
        public List<SimpleBlackboard.Entry> initialBlackboardValues = new();
    }

    [SerializeField] private GraphSlot[] graphs;

    private readonly Dictionary<string, NarrativeRunner> _runnersByLabel = new();
    private readonly List<NarrativeRunner> _allRunners = new();
    private readonly Dictionary<NarrativeRunner, List<SimpleBlackboard.Entry>> _initialValues = new();
    private readonly HashSet<string> _validatedLabels = new();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (graphs == null || graphs.Length == 0)
            return;

        var signals = ResolveSignals();

        foreach (var slot in graphs)
        {
            if (slot == null || slot.graph == null)
                continue;

            var runnerGo = new GameObject(string.IsNullOrEmpty(slot.label) ? slot.graph.name : slot.label);
            runnerGo.transform.SetParent(transform, false);

            var runner = runnerGo.AddComponent<NarrativeRunner>();
            var blackboard = new SimpleBlackboard();
            blackboard.ImportFromSerializable(slot.initialBlackboardValues);

            // Se configura sin ejecutarlo: los grafos arrancan con NarrativeGraphStarter.
            runner.graph = slot.graph;
            runner.Blackboard = blackboard;
            runner.SetSignalsProvider(signals);

            _allRunners.Add(runner);
            if (slot.initialBlackboardValues != null && slot.initialBlackboardValues.Count > 0)
                _initialValues[runner] = slot.initialBlackboardValues;
            
            if (!_runnersByLabel.ContainsKey(slot.label ?? string.Empty))
                _runnersByLabel.Add(slot.label ?? string.Empty, runner);

        }

        if (signals == null)
            Debug.LogWarning("[NarrativeGraphHub] No hay DefaultNarrativeSignals. Los nodos que dependan de señales no funcionarán.");

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ValidateGraphsForScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (_instance == this)
            _instance = null;
    }

    DefaultNarrativeSignals ResolveSignals() => DefaultNarrativeSignals.EnsureInstance();

    /// <summary>Devuelve el runner asociado a la etiqueta proporcionada.</summary>
    public NarrativeRunner GetRunner(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        _runnersByLabel.TryGetValue(label, out var runner);
        return runner;
    }

    /// <summary>Devuelve todos los runners registrados.</summary>
    public List<NarrativeRunner> GetAllRunners() => _allRunners;

    void OnActiveSceneChanged(Scene current, Scene next)
    {
        ValidateGraphsForScene(next.name);
    }

    void ValidateGraphsForScene(string sceneName)
    {
        if (graphs == null || graphs.Length == 0)
            return;

        foreach (var slot in graphs)
        {
            if (slot == null || slot.graph == null)
                continue;

            var label = slot.label ?? slot.graph.name;
            if (_validatedLabels.Contains(label))
                continue;

            if (!GraphAppliesToScene(slot.graph, sceneName))
            {
                continue;
            }

            var validation = NarrativeGraphValidator.ValidateGraph(slot.graph);
            validation.LogResults(label);
            _validatedLabels.Add(label);
        }
    }

    static bool GraphAppliesToScene(NarrativeGraph graph, string sceneName)
    {
        if (graph == null) return false;
        if (graph.applicableSceneNames == null || graph.applicableSceneNames.Count == 0)
            return true;

        foreach (var scene in graph.applicableSceneNames)
        {
            if (string.Equals(scene, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Detiene la ejecución de todos los runners. Llamar antes de recargar una partida
    /// para evitar que corrutinas de la sesión anterior interfieran con la nueva.
    /// </summary>
    public void StopAllRunners()
    {
        foreach (var runner in _allRunners)
        {
            if (runner != null)
                runner.StopExecution();
        }
        NarrativeLog.Info("[NarrativeGraphHub] Todos los runners detenidos.");
    }

    /// <summary>
    /// Deja cada blackboard como al arrancar el juego (vacío más los valores iniciales de su
    /// grafo). Útil al iniciar nueva partida o al cargar otra.
    /// Los grafos se deben reiniciar manualmente usando NarrativeGraphStarter en cada escena.
    /// </summary>
    public void ClearAllBlackboards()
    {
        foreach (var runner in _allRunners)
        {
            if (runner == null || runner.Blackboard == null) continue;
            runner.Blackboard.Clear();
            if (_initialValues.TryGetValue(runner, out var initial))
                runner.Blackboard.ImportFromSerializable(initial);
        }
    }

    /// <summary>
    /// true si en todos los grafos los pasos en curso son seguros para guardar
    /// (<see cref="NarrativeRunner.IsAtSafeStep"/>): es lo que pide un guardado automático.
    /// </summary>
    public bool IsAtSafeStep()
    {
        foreach (var runner in _allRunners)
            if (runner != null && !runner.IsAtSafeStep) return false;
        return true;
    }

    /// <summary>
    /// Primer paso en curso, en cualquier grafo, que no deja guardar la partida
    /// (<see cref="NarrativeNode.blockSaving"/>). Null si se puede guardar.
    /// </summary>
    public NarrativeNode FindSaveBlocker()
    {
        foreach (var runner in _allRunners)
        {
            var blocker = runner != null ? runner.SaveBlocker : null;
            if (blocker != null) return blocker;
        }
        return null;
    }
    
    /// <summary>
    /// Inicia un grafo específico por su etiqueta.
    /// </summary>
    public void StartGraph(string label)
    {
        var runner = GetRunner(label);
        if (runner == null)
        {
            Debug.LogWarning($"[NarrativeGraphHub] No se encontró grafo con etiqueta '{label}'");
            return;
        }
        
        runner.StartFromStartNode();
    }
    
    /// <summary>
    /// Inicia múltiples grafos por sus etiquetas.
    /// </summary>
    public void StartGraphs(params string[] labels)
    {
        foreach (var label in labels)
        {
            StartGraph(label);
        }
    }
    
    /// <summary>
    /// Estado actual de los blackboards con datos, para guardar la partida. Uno por grafo, con la
    /// etiqueta con la que está registrado aquí.
    /// </summary>
    public List<NarrativeBlackboardRecord> CaptureBlackboardRecords()
    {
        var records = new List<NarrativeBlackboardRecord>();
        foreach (var kvp in _runnersByLabel)
        {
            var runner = kvp.Value;
            if (runner == null || runner.Blackboard == null) continue;

            var data = runner.Blackboard.ExportToSerializable();
            if (data != null && data.Count > 0)
                records.Add(new NarrativeBlackboardRecord(kvp.Key, data));
        }
        return records;
    }

    /// <summary>Vuelca en cada grafo el blackboard guardado con su etiqueta.</summary>
    public void RestoreBlackboardRecords(IEnumerable<NarrativeBlackboardRecord> records)
    {
        if (records == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[NarrativeGraphHub] No hay blackboards guardados para restaurar");
#endif
            return;
        }

        int total = 0;
        int restored = 0;
        foreach (var record in records)
        {
            total++;
            if (string.IsNullOrEmpty(record.GraphLabel))
            {
                Debug.LogWarning("[NarrativeGraphHub] Blackboard guardado sin etiqueta de grafo - saltando");
                continue;
            }

            var runner = GetRunner(record.GraphLabel);
            if (runner == null)
            {
                Debug.LogWarning($"[NarrativeGraphHub] No se encontró runner para grafo '{record.GraphLabel}'");
                continue;
            }

            if (runner.Blackboard == null)
            {
                Debug.LogWarning($"[NarrativeGraphHub] Runner '{record.GraphLabel}' no tiene blackboard");
                continue;
            }

            runner.Blackboard.ImportFromSerializable(record.Entries);
            restored++;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            var currentNodeGuid = runner.Blackboard.Get<string>(NarrativeRunner.CurrentNodeKey, null);
            var node = string.IsNullOrEmpty(currentNodeGuid) ? null : runner.graph?.FindNode(currentNodeGuid);
            Debug.Log($"[NarrativeGraphHub] Blackboard restaurado para grafo '{record.GraphLabel}' | Nodo guardado: {(node != null ? node.GetType().Name : "N/A")} ({ShortGuid(currentNodeGuid)}...)");
            if (!string.IsNullOrEmpty(currentNodeGuid) && node == null)
                Debug.LogWarning($"[NarrativeGraphHub] El blackboard de '{record.GraphLabel}' guarda el paso '{currentNodeGuid}', que no existe en ese grafo: empezará desde el principio.");
#endif
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[NarrativeGraphHub] Restaurados {restored}/{total} blackboards");
#endif
    }

    static string ShortGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return "null";

        const int previewLength = 8;
        return guid.Length <= previewLength ? guid : guid.Substring(0, previewLength);
    }
}
