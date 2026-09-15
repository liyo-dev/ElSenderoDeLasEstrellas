using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de Editor para dar de alta <see cref="NPCObstacleAvoidance"/> (INC-173 — NPCs del
/// Bosque Prohibido metiéndose en los árboles, recurrencia de INC-001) en los prefabs de NPC del
/// proyecto de un tirón, sin tener que abrir cada prefab a mano y añadir el componente uno a uno.
///
/// Dos acciones, cada una idempotente (se puede volver a ejecutar sin duplicar nada):
///   1) "Añadir a Prefabs de NPC conocidos" — edita directamente los assets de prefab (Spider1 y
///      los miembros del party) con <c>PrefabUtility.LoadPrefabContents/SaveAsPrefabAsset</c>, así
///      que el cambio llega automáticamente a TODAS las instancias ya colocadas en las escenas
///      (las 59 arañas del bosque, por ejemplo) sin tocar ningún .unity.
///   2) "Añadir a NavMeshAgent sueltos de la escena abierta" — red de seguridad para cualquier
///      GameObject con NavMeshAgent colocado directamente en la escena (no como instancia de un
///      prefab de la lista de arriba) que también deba esquivar árboles/props.
///
/// Tras ejecutar: revisar en el Inspector que <c>NPCObstacleAvoidance</c> aparece en Spider1 y en
/// los NPCs del party, y probar en el bosque que ya no se quedan empujando contra los árboles.
/// </summary>
public static class NPCObstacleAvoidanceSetup
{
    // Prefabs conocidos que ya recorren el Bosque Prohibido (arañas) o pueden atravesarlo
    // siguiendo a Will como miembros del party. Ampliar esta lista si se añaden más NPCs que
    // deban pasar por zonas con árboles/props sin Carve bien configurado.
    private static readonly string[] KnownNpcPrefabPaths =
    {
        "Assets/Prefabs/Enemy/Spider1.prefab",
        "Assets/_NPCs/Eldran.prefab",
        "Assets/Prefabs/_LIAM.prefab",
        "Assets/Prefabs/_ESTELA.prefab",
        "Assets/Prefabs/_WILL_NPC.prefab",
    };

    [MenuItem("El Sendero/NPCs/Esquiva de Obstáculos/Añadir a Prefabs de NPC conocidos")]
    public static void AddToKnownPrefabs()
    {
        int added = 0, alreadyHad = 0, missing = 0;

        foreach (var path in KnownNpcPrefabPaths)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[NPCObstacleAvoidanceSetup] ⚠️ No se encontró el prefab en: {path}");
                missing++;
                continue;
            }

            if (prefabAsset.GetComponent<NavMeshAgent>() == null)
            {
                Debug.LogWarning($"[NPCObstacleAvoidanceSetup] ⚠️ {prefabAsset.name} no tiene NavMeshAgent, se omite: {path}");
                continue;
            }

            if (prefabAsset.GetComponent<NPCObstacleAvoidance>() != null)
            {
                alreadyHad++;
                continue;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            if (contents.GetComponent<NPCObstacleAvoidance>() == null)
            {
                contents.AddComponent<NPCObstacleAvoidance>();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            Debug.Log($"[NPCObstacleAvoidanceSetup] ➕ Añadido a {prefabAsset.name} ({path})");
            added++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[NPCObstacleAvoidanceSetup] ✅ Prefabs: {added} añadidos, {alreadyHad} ya lo tenían, {missing} no encontrados.");
    }

    [MenuItem("El Sendero/NPCs/Esquiva de Obstáculos/Añadir a NavMeshAgent sueltos de la escena abierta")]
    public static void AddToLooseSceneAgents()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[NPCObstacleAvoidanceSetup] ⚠️ No hay ninguna escena abierta.");
            return;
        }

        var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

        Debug.Log($"[NPCObstacleAvoidanceSetup] ✅ Escena '{scene.name}': {added} NavMeshAgent nuevos con esquiva, {alreadyHad} ya la tenían.");
    }
}
