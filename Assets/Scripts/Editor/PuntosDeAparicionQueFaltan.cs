using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Crea, en la escena abierta, los <see cref="NpcSpawnPoint"/> que piden los rosters de NPCs y aún
/// no existen, en el punto del suelo que hay en el centro de la vista de escena (lo que se esté
/// mirando). Sirve para colocar un personaje nuevo del reparto: se mira el sitio y se pulsa.
/// Cada punto queda seleccionado para afinarlo con el gizmo si hace falta.
/// </summary>
public static class PuntosDeAparicionQueFaltan
{
    [MenuItem("El Sendero/NPCs/Crear los puntos de aparición que faltan (donde mira la vista de escena)")]
    private static void Crear()
    {
        var vista = SceneView.lastActiveSceneView;
        if (vista == null)
        {
            EditorUtility.DisplayDialog("Puntos de aparición", "Abre la vista de escena y mira al sitio donde debe aparecer el personaje.", "Vale");
            return;
        }
        Scene escena = SceneManager.GetActiveScene();
        var existentes = new HashSet<string>();
        foreach (var p in Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsInactive.Include))
            if (p.gameObject.scene == escena) existentes.Add(p.spawnId);

        var nombreRoster = "NpcRoster_" + escena.name;
        var faltan = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:NpcRosterSO " + nombreRoster))
        {
            var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (roster == null || roster.name != nombreRoster) continue;
            foreach (var e in roster.entries)
                if (e != null && e.enabled && !string.IsNullOrEmpty(e.spawnId) && !existentes.Contains(e.spawnId) && !faltan.Contains(e.spawnId))
                    faltan.Add(e.spawnId);
        }
        if (faltan.Count == 0)
        {
            EditorUtility.DisplayDialog("Puntos de aparición", $"Al roster {nombreRoster} no le falta ningún punto en esta escena.", "Vale");
            return;
        }

        Vector3 centro = vista.pivot;
        if (Physics.Raycast(centro + Vector3.up * 200f, Vector3.down, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            centro = hit.point;
        Vector3 mirar = Vector3.ProjectOnPlane(-vista.camera.transform.forward, Vector3.up);

        var creados = new List<Object>();
        for (int i = 0; i < faltan.Count; i++)
        {
            var go = new GameObject(faltan[i]);
            Undo.RegisterCreatedObjectUndo(go, "Crear punto de aparición");
            go.transform.SetPositionAndRotation(centro + Vector3.right * (1.5f * i),
                mirar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mirar) : Quaternion.identity);
            go.AddComponent<NpcSpawnPoint>().spawnId = faltan[i];
            creados.Add(go);
        }
        Selection.objects = creados.ToArray();
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log($"[Puntos de aparición] Creados en {escena.name}: {string.Join(", ", faltan)}. Guarda la escena.");
    }
}
