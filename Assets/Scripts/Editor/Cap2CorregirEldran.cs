#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deja la entrada de Eldran del roster de MainWorld sin condición: aparece siempre en su
/// marcador. Es el mismo actor que el grafo mueve a WillHouse para «La mañana después»
/// (PlaceActorNode), así que condicionarlo a un flag que nadie levanta solo consigue que no
/// aparezca en ninguna parte. Ver INC-830.
/// </summary>
public static class Cap2CorregirEldran
{
    const string RosterPath = "Assets/Resources/NpcRosters/NpcRoster_MainWorld.asset";
    const string SpawnId = "SPAWN_Eldran";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Restaurar a Eldran en MainWorld")]
    public static void Corregir()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play para modificar assets.");

        var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>(RosterPath)
            ?? throw new InvalidOperationException("No se encuentra " + RosterPath);

        var entrada = roster.entries.Find(e => e.spawnId == SpawnId)
            ?? throw new InvalidOperationException($"No hay entrada {SpawnId} en el roster.");

        if (string.IsNullOrEmpty(entrada.requiredFlag) && !entrada.invertCondition)
        {
            Debug.Log($"[Cap2CorregirEldran] {SpawnId} ya aparece siempre; no hay nada que hacer.");
            return;
        }

        entrada.requiredFlag = "";
        entrada.invertCondition = false;
        EditorUtility.SetDirty(roster);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cap2CorregirEldran] {SpawnId} vuelve a aparecer siempre (sin condición).");
    }
}
#endif
