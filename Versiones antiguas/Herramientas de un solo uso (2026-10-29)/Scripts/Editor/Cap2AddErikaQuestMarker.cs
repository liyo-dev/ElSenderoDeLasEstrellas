#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Añade QuestObjectiveMarker al prefab de Erika para que salga en el minimapa
/// durante la quest PREPARA_VIAJE (paso 2: entrenamiento con Erika). INC-725.
/// </summary>
public static class Cap2AddErikaQuestMarker
{
    const string ErikaPrefabPath = "Assets/_NPCs/Combat/Erika.prefab";

    [MenuItem("El Sendero/Capítulo 2/Añadir QuestObjectiveMarker a Erika (INC-725)")]
    public static void AddQuestMarker()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play para modificar el prefab.");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErikaPrefabPath)
            ?? throw new InvalidOperationException("No se encuentra " + ErikaPrefabPath);

        // Verificar si ya tiene el componente
        var existing = prefab.GetComponent<QuestObjectiveMarker>();
        if (existing != null)
        {
            // Actualizar valores
            var so = new SerializedObject(existing);
            so.FindProperty("questId").stringValue = "PREPARA_VIAJE";
            so.FindProperty("stepIndex").intValue = 2; // paso 3 (Erika)
            so.FindProperty("showAfterStep").intValue = -1;
            so.ApplyModifiedProperties();
            Debug.Log("[Cap2AddErikaQuestMarker] QuestObjectiveMarker actualizado en Erika.prefab");
        }
        else
        {
            // Añadir componente
            var marker = prefab.AddComponent<QuestObjectiveMarker>();
            var so = new SerializedObject(marker);
            so.FindProperty("questId").stringValue = "PREPARA_VIAJE";
            so.FindProperty("stepIndex").intValue = 2;
            so.FindProperty("showAfterStep").intValue = -1;
            so.FindProperty("color").colorValue = new Color(1f, 0.92f, 0.016f, 1f); // amarillo quest
            so.ApplyModifiedProperties();
            Debug.Log("[Cap2AddErikaQuestMarker] QuestObjectiveMarker añadido a Erika.prefab");
        }

        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();
    }
}
#endif