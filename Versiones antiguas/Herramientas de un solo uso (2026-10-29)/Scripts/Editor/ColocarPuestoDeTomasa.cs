#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Coloca (o recoloca) el punto de aparición de Tomasa, la tallista del mercado (INC-436), donde
/// esté mirando la vista de escena. Tomasa sale del roster de MainWorld (NpcRoster_MainWorld,
/// spawnId SPAWN_Tomasa): solo hace falta este punto en la escena para que aparezca.
public static class ColocarPuestoDeTomasa
{
    const string SpawnId = "SPAWN_Tomasa";

    [MenuItem("El Sendero/Tiendas/Colocar a Tomasa donde mira la vista")]
    static void Colocar()
    {
        var vista = SceneView.lastActiveSceneView;
        if (vista == null || vista.camera == null)
        {
            EditorUtility.DisplayDialog("Colocar a Tomasa",
                "Abre la vista de escena (Scene) y mira al sitio del mercado donde va su puesto.", "Vale");
            return;
        }

        Vector3 punto = vista.pivot;
        if (Physics.Raycast(punto + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            punto = hit.point;

        NpcSpawnPoint existente = null;
        foreach (var sp in Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsInactive.Include))
            if (sp != null && sp.spawnId == SpawnId) { existente = sp; break; }

        GameObject go;
        if (existente != null)
        {
            go = existente.gameObject;
            Undo.RecordObject(go.transform, "Mover a Tomasa");
        }
        else
        {
            go = new GameObject(SpawnId);
            Undo.RegisterCreatedObjectUndo(go, "Colocar a Tomasa");
            var sp = go.AddComponent<NpcSpawnPoint>();
            sp.spawnId = SpawnId;
        }

        go.transform.position = punto;
        Vector3 haciaLaVista = vista.camera.transform.position - punto;
        haciaLaVista.y = 0f;
        if (haciaLaVista.sqrMagnitude > 0.01f)
            go.transform.rotation = Quaternion.LookRotation(haciaLaVista.normalized, Vector3.up);

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log($"[Tomasa] Punto de aparición '{SpawnId}' en {punto} (escena '{go.scene.name}'), mirando hacia la vista. " +
                  "Muévelo o gíralo a mano si hace falta y guarda la escena (Ctrl+S).");
    }
}
#endif
