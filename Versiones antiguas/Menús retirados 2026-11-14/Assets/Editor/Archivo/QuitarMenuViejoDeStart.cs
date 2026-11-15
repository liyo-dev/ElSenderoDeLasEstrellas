using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quita de Start.unity el menú de Start viejo (pestañas redondas, paneles de Inventario,
/// Hechizos y Equipo, estadísticas, tarjetas de ayuda): todo cuelga de «DreamBackgroundController»
/// dentro del lienzo del menú. El menú nuevo se monta por código (INC-1000/INC-1001). Idempotente.
/// </summary>
public static class QuitarMenuViejoDeStart
{
    private const string StartPath = "Assets/Scenes/Systems/Start.unity";
    private const string Viejo = "DreamBackgroundController";

    [MenuItem("El Sendero/Archivo/UI/Menú de Start · quitar el menú viejo (INC-1001)")]
    public static void Quitar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Fuera de Play."); return; }
        var escena = SceneManager.GetSceneByPath(StartPath);
        if (!escena.isLoaded) escena = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Additive);
        PlayerEquipmentMenuController menu = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            menu = raiz.GetComponentInChildren<PlayerEquipmentMenuController>(true);
            if (menu != null) break;
        }
        if (menu == null) { Debug.LogWarning("[Menú de Start] No está PlayerEquipmentMenuController en Start."); return; }
        int quitados = 0;
        foreach (var t in menu.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t.name != Viejo || t == menu.transform) continue;
            Object.DestroyImmediate(t.gameObject);
            quitados++;
        }
        if (quitados == 0) { Debug.Log("[Menú de Start] El menú viejo ya no estaba."); return; }
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log($"[Menú de Start] Quitado el menú viejo ({quitados}). Start guardada.");
    }
}
