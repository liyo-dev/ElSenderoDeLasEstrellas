using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Pone <see cref="NPCObstacleAvoidance"/> en los NavMeshAgent de la escena activa que aún no lo
/// tienen: los que se colocan sueltos en la escena y no como instancia de un prefab que ya lo
/// lleve. Se puede repetir; los que ya lo tienen no se tocan. Ver INC-173.
/// </summary>
public static class NPCObstacleAvoidanceSetup
{
    [MenuItem("El Sendero/Personajes/NPCs/Esquiva de obstáculos en los NavMeshAgent sueltos de la escena")]
    public static void AddToLooseSceneAgents()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[NPCObstacleAvoidanceSetup] No hay ninguna escena abierta.");
            return;
        }

        var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude);
        int added = 0, alreadyHad = 0;

        foreach (var agent in agents)
        {
            if (agent.GetComponent<NPCObstacleAvoidance>() != null)
            {
                alreadyHad++;
                continue;
            }

            Undo.AddComponent<NPCObstacleAvoidance>(agent.gameObject);
            EditorUtility.SetDirty(agent.gameObject);
            added++;
        }

        if (added > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[NPCObstacleAvoidanceSetup] Escena '{scene.name}': {added} NavMeshAgent nuevos con esquiva, {alreadyHad} ya la tenían.");
    }
}
