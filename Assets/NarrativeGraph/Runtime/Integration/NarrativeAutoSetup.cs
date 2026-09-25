using System.Reflection;
using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────
// CONGELADO (12 sept 2026) — no usar para grafos nuevos. Sustituto:
// NarrativeGraphHub + NarrativeGraphStarter (patrón multi-grafo por etiqueta,
// el único de los dos que soporta más de un grafo activo a la vez — necesario
// en cuanto haya un grafo de Cap1 de la maqueta además de "Historia Principal").
// SIGUE VIVO: es el arrancador real de "Historia Principal" hoy — no se puede
// retirar todavía sin migrar antes ese grafo al Hub (fuera de alcance aquí).
// Nota aparte: inyecta signalsProvider por reflection — contra CLAUDE.md § 2
// (no reflection en runtime); no tocar sin plan de retirada completo.
// Ver claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md § 2.
// ───────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class NarrativeAutoSetup : MonoBehaviour
{
    [Header("Config obligatoria")]
    public NarrativeGraph graph;

    [Header("Debug opcional")]
    public bool debugLogs;

    private static NarrativeAutoSetup _instance;
    DefaultNarrativeSignals _signals;
    QuestServiceAdapter _questService;
    NarrativeRunner _runner;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (debugLogs) Debug.Log("[NarrativeAutoSetup] Duplicado detectado. Destruyendo este.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _runner = GetComponent<NarrativeRunner>() ?? gameObject.AddComponent<NarrativeRunner>();
        _signals = GetComponent<DefaultNarrativeSignals>() ?? gameObject.AddComponent<DefaultNarrativeSignals>();
        _questService = GetComponent<QuestServiceAdapter>() ?? gameObject.AddComponent<QuestServiceAdapter>();

        if (!graph)
            Debug.LogWarning("[NarrativeAutoSetup] Graph no asignado. Asigna uno en el inspector.");
        _runner.graph = graph;

        _signals.questServiceProvider = _questService;

        var fi = typeof(NarrativeRunner).GetField("signalsProvider",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null) fi.SetValue(_runner, _signals);
        else Debug.LogWarning("[NarrativeAutoSetup] No se encontró 'signalsProvider' en NarrativeRunner.");

        // Snapshot narrativo eliminado; no hay pending que aplicar

        if (debugLogs)
            Debug.Log("[NarrativeAutoSetup] Listo: runner + signals + questService conectados.");
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        // NO iniciar el grafo aquí.
        // El NarrativeGraphStarter en la escena se encargará de:
        // 1. Restaurar blackboards desde el runtimePreset (sea de JSON o preset de testeo)
        // 2. Iniciar el grafo, que verificará __currentNodeGuid y continuará desde ahí
        //
        // Esto garantiza que el sistema es agnóstico al origen de los datos.
        if (debugLogs) Debug.Log("[NarrativeAutoSetup] Start() - grafo NO iniciado aquí (lo hará NarrativeGraphStarter)");
    }

    void TryBootstrapQuestSignals()
    {
        // MÉTODO DESHABILITADO - Causaba emisiones duplicadas de eventos
        // Los eventos deben ser emitidos por los Interactables cuando corresponda
        
        /*
        if (_signals == null) return;
        var qm = QuestManager.Instance;
        if (qm == null) return;

        var questState = qm.GetState("ELDRAN_MISSION1");
        if (questState >= QuestState.Completed)
        {
            if (debugLogs) Debug.Log("[NarrativeAutoSetup] Quest ELDRAN_MISSION1 completed; raising LETTER_START");
            _signals.RaiseCustom("LETTER_START");
        }
        */
    }

    public static void ResetForNewGame()
    {
        if (_instance == null)
        {
            // Este componente ya no está montado en ninguna escena (el arranque lo hace
            // NarrativeGraphHub + NarrativeGraphStarter), así que antes esta llamada no hacía nada
            // y la partida nueva heredaba las señales de la anterior. Al menos eso se limpia aquí.
            var senales = DefaultNarrativeSignals.Instance;
            if (senales != null) senales.OlvidarSenalesDePartida();
            return;
        }
        _instance.HandleReset("ResetForNewGame", clearBlackboard: true, restartGraph: true);
    }

    public static void ResetForLoadedProfile()
    {
        if (_instance == null) return;
        // Al cargar partida, NO limpiamos el blackboard ni reiniciamos el grafo
        // El estado del grafo se restaurará desde los flags del preset
        // Solo reseteamos los servicios de señales y quests para que se re-sincronicen
        // IMPORTANTE: Preservamos eventos pendientes porque el trigger puede activarse antes que el grafo
        _instance.HandleReset("ResetForLoadedProfile", clearBlackboard: false, restartGraph: false, preservePending: true);
    }

    void HandleReset(string reason, bool clearBlackboard = false, bool restartGraph = false, bool preservePending = false)
    {
        if (debugLogs) Debug.Log($"[NarrativeAutoSetup] {reason}() - clearBlackboard={clearBlackboard}, restartGraph={restartGraph}, preservePending={preservePending}");

        _signals?.ResetState(preservePending);
        _questService?.ResetState();

        if (_runner != null)
        {
            if (clearBlackboard)
            {
                _runner.Blackboard?.Clear();

                // Los runners del NarrativeGraphHub son independientes del runner de NarrativeAutoSetup.
                // Sus blackboards persisten en memoria entre partidas y contienen __currentNodeGuid
                // de la sesión anterior. Sin esta limpieza, StartFromStartNode() salta al nodo
                // incorrecto en la nueva partida.
                var hub = NarrativeGraphHub.Instance;
                if (hub != null)
                {
                    hub.StopAllRunners();
                    hub.ClearAllBlackboards();
                }
            }

            if (restartGraph)
            {
                _runner.StartFromStartNode();
            }
        }
    }
}
