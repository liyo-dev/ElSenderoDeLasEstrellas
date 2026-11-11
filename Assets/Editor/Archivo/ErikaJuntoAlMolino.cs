#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// El punto de aparición de Erika había quedado dentro del molino y tres metros bajo el suelo: sin
/// NavMesh cerca, aparecía sin poder andar (INC-1025). Lo pone al este del molino, sobre el terreno.
public static class ErikaJuntoAlMolino
{
    [MenuItem("El Sendero/Archivo/NPCs vivos/Erika junto al molino, sobre el suelo (INC-1025)")]
    public static void Colocar()
    {
        var punto = GameObject.Find("SPAWN_Erika");
        if (punto == null) { Debug.LogWarning("[INC-1025] No encuentro SPAWN_Erika: abre MainWorld."); return; }
        var sitio = new Vector3(-14.5f, 0f, -125.5f);
        if (Physics.Raycast(new Vector3(sitio.x, 200f, sitio.z), Vector3.down, out var suelo, 400f, LayerMask.GetMask("Floor"), QueryTriggerInteraction.Ignore))
            sitio.y = suelo.point.y;
        Undo.RecordObject(punto.transform, "Erika junto al molino");
        punto.transform.position = sitio;
        EditorSceneManager.MarkSceneDirty(punto.scene);
        EditorSceneManager.SaveScene(punto.scene);
        Debug.Log($"[INC-1025] SPAWN_Erika en {sitio} (al este del molino).");
    }
}
#endif
