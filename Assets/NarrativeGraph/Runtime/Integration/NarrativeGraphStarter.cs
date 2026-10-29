using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Script para iniciar grafos narrativos específicos en una escena de gameplay.
/// Coloca este script en un GameObject de la escena y configura qué grafos deben iniciarse.
/// </summary>
public class NarrativeGraphStarter : MonoBehaviour
{
    [Header("Grafos a Iniciar")]
    [Tooltip("Etiquetas de los grafos del NarrativeGraphHub que se iniciarán al cargar esta escena.")]
    [SerializeField] private string[] graphLabels = new string[] { "Historia Principal" };

    [Header("Configuración")]
    [Tooltip("Si está activado, los grafos se inician en Start(). Si no, debes llamar a StartGraphs() manualmente.")]
    [SerializeField] private bool startOnAwake = true;
    [Tooltip("Si está activo, solo se inician los grafos cuando la escena que contiene este componente es la activa.")]
    [SerializeField] private bool requireActiveScene = true;

    [Header("Grafo local opcional")]
    [Tooltip("Runner de un grafo que pertenece solo a esta escena. Si se asigna, se inicia directamente y no se consulta el Hub.")]
    [SerializeField] private NarrativeRunner localRunner;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    // Los blackboards guardados se restauran como mucho una vez por partida cargada (arranque,
    // continuar o recarga de pruebas). Si se restauraran en cada escena con este componente, el
    // estado vivo retrocedería al último guardado al cambiar de escena sin guardar: un nodo que ya
    // entregó un objeto lo volvería a entregar. Ver INC-020.
    private static int s_restoredLoadCount = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_restoredLoadCount = -1;

    void Start()
    {
        if (startOnAwake)
        {
            StartCoroutine(WaitForHubAndStart());
        }
    }

    /// <summary>
    /// Espera a que NarrativeGraphHub esté disponible antes de iniciar los grafos.
    /// Esto soporta la carga aditiva de la escena Start en el editor.
    /// </summary>
    private System.Collections.IEnumerator WaitForHubAndStart()
    {
        // Un fotograma de espera para que todos los Start() de la escena hayan corrido antes de
        // que el grafo empiece a ejecutar nodos.
        yield return null;

        // Esperar hasta que el Hub esté disponible
        while (NarrativeGraphHub.Instance == null)
        {
            yield return null;
        }

        // Evitar iniciar grafos si la escena aún no es la activa (p.ej. Start cargada aditivamente en el editor)
        if (requireActiveScene)
        {
            while (gameObject != null
                && gameObject.scene.IsValid()
                && gameObject.scene.isLoaded
                && SceneManager.GetActiveScene() != gameObject.scene)
            {
                yield return null;
            }

            if (gameObject == null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                if (logDebug)
                    Debug.LogWarning("[NarrativeGraphStarter] Escena no válida o destruida antes de iniciar grafos.");
                yield break;
            }
        }

        StartGraphs();
    }

    /// <summary>
    /// Inicia todos los grafos configurados en la lista. Antes restaura, una vez por partida
    /// cargada, el estado guardado de sus blackboards.
    /// </summary>
    public void StartGraphs()
    {
        if (requireActiveScene && SceneManager.GetActiveScene() != gameObject.scene)
        {
            return;
        }

        if (localRunner != null)
        {
            localRunner.StartFromStartNode();
            return;
        }

        if (graphLabels == null || graphLabels.Length == 0)
        {
            return;
        }

        var hub = NarrativeGraphHub.Instance;
        if (hub == null)
        {
            Debug.LogError("[NarrativeGraphStarter] NarrativeGraphHub no encontrado. Asegúrate de que existe en la escena.");
            return;
        }

        RestoreSavedBlackboards(hub);

        foreach (var label in graphLabels)
        {
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }

            // El runner se encargará de verificar si hay estado guardado
            // y continuará desde el nodo guardado automáticamente
            hub.StartGraph(label);
        }
    }

    /// <summary>Restaura los blackboards de la partida cargada, si aún no se ha hecho para esta carga.</summary>
    private void RestoreSavedBlackboards(NarrativeGraphHub hub)
    {
        if (!NarrativeServices.TryGet(out INarrativeSaveData save, nameof(NarrativeGraphStarter)))
            return;

        // Sin partida cargada todavía no se marca nada: se reintenta en el próximo StartGraphs().
        if (!save.IsLoaded)
        {
            Debug.LogWarning("[NarrativeGraphStarter] Todavía no hay partida cargada: no hay blackboards que restaurar.");
            return;
        }

        if (save.LoadCount == s_restoredLoadCount)
            return;
        s_restoredLoadCount = save.LoadCount;

        var records = save.GetSavedBlackboards();
        if (records == null)
        {
            Debug.LogWarning("[NarrativeGraphStarter] La partida cargada no tiene datos: los grafos empiezan desde su inicio.");
            return;
        }
        if (records.Count == 0)
        {
            Debug.LogWarning("[NarrativeGraphStarter] La partida no guardó blackboards: los grafos empiezan desde su inicio.");
            return;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[NarrativeGraphStarter] Restaurando blackboards de la partida ({records.Count} grafos)");
#endif
        hub.RestoreBlackboardRecords(records);
    }

    /// <summary>
    /// Inicia un grafo específico por su etiqueta.
    /// </summary>
    public void StartGraph(string label)
    {
        var hub = NarrativeGraphHub.Instance;
        if (hub == null)
        {
            Debug.LogError("[NarrativeGraphStarter] NarrativeGraphHub no encontrado.");
            return;
        }

        hub.StartGraph(label);
    }
}
