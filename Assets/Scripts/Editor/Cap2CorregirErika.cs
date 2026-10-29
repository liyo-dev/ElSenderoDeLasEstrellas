#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Quita a Erika el comportamiento interactivo legacy (Q_ELDRAN_MISSION5: la frase «solo magos» y
/// el combate, que ningún grafo activo arranca) y coloca SPAWN_Erika sobre el suelo real y el
/// NavMesh. INC-829.
/// </summary>
public static class Cap2CorregirErika
{
    const string ErikaPrefabPath = "Assets/_NPCs/Combat/Erika.prefab";
    const string ErikaConfigPath = "Assets/_NPCs/Narrative/NPC_InteractiveNarrative_Config_Erika.asset";
    const string MainWorldPath = "Assets/Scenes/Worlds/MainWorld.unity";
    const string SpawnName = "SPAWN_Erika";
    static readonly Vector2 SpawnXZ = new Vector2(70f, -200f);
    const float AlturaDeBusqueda = 200f;
    const float DistanciaDeBusqueda = 400f;
    const float RadioNavMesh = 2f;

    [MenuItem("El Sendero/Archivo/Capítulo 2/Corregir Erika (INC-829)")]
    public static void Corregir()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play para modificar assets y escenas.");

        QuitarInteractivoDeErika();
        ColocarSpawnSobreElSuelo();
    }

    static void QuitarInteractivoDeErika()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErikaPrefabPath)
            ?? throw new InvalidOperationException("No se encuentra " + ErikaPrefabPath);
        var npc = prefab.GetComponent<Game.NPC.NPCBehaviourManagerV2>()
            ?? throw new InvalidOperationException("Erika.prefab no tiene NPCBehaviourManagerV2");

        var so = new SerializedObject(npc);
        var comportamiento = so.FindProperty("configuration.behaviourType");
        comportamiento.intValue &= ~(int)Game.NPC.Common.NPCBehaviourType.InteractiveNarrative;
        so.FindProperty("configuration.interactiveNarrativeConfig").objectReferenceValue = null;
        so.ApplyModifiedProperties();

        int capaInteractable = LayerMask.NameToLayer("Interactable");
        if (capaInteractable < 0)
            throw new InvalidOperationException("No existe la capa 'Interactable' en el proyecto.");
        prefab.layer = capaInteractable;

        // NarrativeActor: WaitNpcInteractionNode lo necesita para mostrar el icono sobre la cabeza.
        if (prefab.GetComponent<NarrativeActor>() == null)
            prefab.AddComponent<NarrativeActor>();

        PrefabUtility.SavePrefabAsset(prefab);

        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(ErikaConfigPath) != null)
            AssetDatabase.DeleteAsset(ErikaConfigPath);

        AssetDatabase.SaveAssets();
        Debug.Log("[Cap2CorregirErika] Erika sin comportamiento InteractiveNarrative; config legacy retirada.");
    }

    static void ColocarSpawnSobreElSuelo()
    {
        ColocarSpawnSobreElSuelo(SpawnName, SpawnXZ);
    }

    /// <summary>
    /// Lleva un SpawnPoint de MainWorld a la altura del suelo y del NavMesh en su XZ.
    /// Lo usan Erika y Eldran (INC-829, INC-830).
    /// </summary>
    public static void ColocarSpawnSobreElSuelo(string nombreSpawn, Vector2 xz)
    {
        var scene = EditorSceneManager.OpenScene(MainWorldPath, OpenSceneMode.Additive);
        if (!scene.IsValid())
            throw new InvalidOperationException("No se pudo abrir MainWorld.unity");

        var spawn = GameObject.Find(nombreSpawn)
            ?? throw new InvalidOperationException($"No hay ningún {nombreSpawn} en MainWorld.");

        var origen = new Vector3(xz.x, AlturaDeBusqueda, xz.y);
        if (!Physics.Raycast(origen, Vector3.down, out var suelo, DistanciaDeBusqueda, ~0, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException($"No hay suelo bajo {nombreSpawn} en {xz}. Revisa la posición a mano.");

        Vector3 destino = suelo.point;
        if (NavMesh.SamplePosition(suelo.point, out var sobreMalla, RadioNavMesh, NavMesh.AllAreas))
            destino = sobreMalla.position;
        else
            Debug.LogWarning($"[Cap2CorregirErika] Sin NavMesh a {RadioNavMesh} m de {nombreSpawn}; se usa el suelo del raycast.");

        Vector3 anterior = spawn.transform.position;
        spawn.transform.position = destino;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Cap2CorregirErika] {nombreSpawn}: {anterior} → {destino}.");
    }
}
#endif
