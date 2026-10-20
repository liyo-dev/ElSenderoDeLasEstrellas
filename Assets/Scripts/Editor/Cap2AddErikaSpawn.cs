#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Anade spawn point de Erika en MainWorld y entrada en NpcRoster_MainWorld. INC-725.
/// </summary>
public static class Cap2AddErikaSpawn
{
    const string MainWorldPath = "Assets/Scenes/Worlds/MainWorld.unity";
    const string RosterPath = "Assets/Resources/NpcRosters/NpcRoster_MainWorld.asset";
    const string ErikaPrefabPath = "Assets/_NPCs/Combat/Erika.prefab";

    // Cerca de Eldran (pueblo de Will): Eldran está en ~X:80, Y:24, Z:-180
    // Campo de entrenamiento un poco aparte del centro del pueblo
    static readonly Vector3 SpawnPosition = new Vector3(70f, 24f, -200f);
    static readonly Vector3 SpawnRotation = new Vector3(0, 90, 0);

    [MenuItem("El Sendero/Capitulo 2/Anadir spawn point y roster de Erika (INC-725)")]
    public static void AddErikaSpawn()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play para modificar la escena/roster.");

        AddSpawnPointToScene();
        AddToRoster();

        Debug.Log("[Cap2AddErikaSpawn] Spawn point SPAWN_Erika creado en MainWorld\n" +
                  "Entrada anadida a NpcRoster_MainWorld.asset\n" +
                  "Guarda la escena (Ctrl+S) y el roster se guardara solo.");
    }

    static void AddSpawnPointToScene()
    {
        var scene = EditorSceneManager.OpenScene(MainWorldPath, OpenSceneMode.Additive);
        if (!scene.IsValid())
            throw new InvalidOperationException("No se pudo abrir MainWorld.unity");

        var existing = GameObject.Find("SPAWN_Erika");
        if (existing != null)
        {
            Debug.Log("[Cap2AddErikaSpawn] SPAWN_Erika ya existe, actualizando posicion...");
            existing.transform.position = SpawnPosition;
            existing.transform.eulerAngles = SpawnRotation;
        }
        else
        {
            var go = new GameObject("SPAWN_Erika");
            go.transform.position = SpawnPosition;
            go.transform.eulerAngles = SpawnRotation;

            var spawnPoint = go.AddComponent<NpcSpawnPoint>();
            var so = new SerializedObject(spawnPoint);
            so.FindProperty("spawnId").stringValue = "SPAWN_Erika";
            so.ApplyModifiedProperties();

            Debug.Log("[Cap2AddErikaSpawn] SPAWN_Erika creado en " + SpawnPosition);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void AddToRoster()
    {
        var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>(RosterPath)
            ?? throw new InvalidOperationException("No se encuentra " + RosterPath);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErikaPrefabPath)
            ?? throw new InvalidOperationException("No se encuentra " + ErikaPrefabPath);

        var existingEntry = roster.entries.Find(e => e.spawnId == "SPAWN_Erika");
        if (existingEntry != null)
        {
            existingEntry.prefab = prefab;
            existingEntry.gameObjectName = "Erika";
            existingEntry.persistenceId = "NPC_Erika";
            existingEntry.vozDeReacciones = "Vecina";
            existingEntry.startActive = true;
            existingEntry.requireNavMesh = true;
            Debug.Log("[Cap2AddErikaSpawn] Entrada de Erika actualizada en roster.");
        }
        else
        {
            var entry = new NpcRosterSO.Entry
            {
                enabled = true,
                spawnId = "SPAWN_Erika",
                prefab = prefab,
                gameObjectName = "Erika",
                persistenceId = "NPC_Erika",
                vozDeReacciones = "Vecina",
                startActive = true,
                requireNavMesh = true,
                requiredFlag = "NARRATIVE_FLAG:CAP2_INICIADO",
                invertCondition = false
            };
            roster.entries.Add(entry);
            Debug.Log("[Cap2AddErikaSpawn] Entrada de Erika anadida al roster.");
        }

        EditorUtility.SetDirty(roster);
        AssetDatabase.SaveAssets();
    }
}
#endif