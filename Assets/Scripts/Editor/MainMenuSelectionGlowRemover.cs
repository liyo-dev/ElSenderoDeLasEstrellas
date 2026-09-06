using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Revierte el "brillo de selección" del Menú Principal (halo detrás de la fila seleccionada) que
/// añadía MainMenuSelectionGlowBuilder — Raúl lo probó (4 sep 2026) y no le convenció, ver INC-171
/// en TRACKER.md. Quita únicamente los GameObjects "SelectionGlow" ya presentes en la escena, sin
/// tocar nada más (fondo de cristal, tamaños de botón, etc.).
///
/// Uso: Assets → menú "El Sendero → Controles → Quitar Brillo de Selección (revertir)". Es un
/// script de un solo uso — bórralo del proyecto después de ejecutarlo una vez.
/// </summary>
public static class MainMenuSelectionGlowRemover
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";

    [MenuItem("El Sendero/Controles/Quitar Brillo de Selección (revertir)")]
    public static void RemoveSelectionGlow()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuSelectionGlowRemover] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuSelectionGlowRemover] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MainMenuSelectionGlowRemover] No se pudo abrir {ScenePath}.");
            return;
        }

        var buttonPanel = FindByNameIncludingInactive("ButtonPanel");
        if (buttonPanel == null)
        {
            Debug.LogWarning("[MainMenuSelectionGlowRemover] No se encontró 'ButtonPanel' — nada que revertir.");
            return;
        }

        var buttons = buttonPanel.GetComponentsInChildren<Button>(true);
        int removed = 0;
        foreach (var b in buttons)
        {
            var glow = b.transform.Find("SelectionGlow");
            if (glow != null)
            {
                Object.DestroyImmediate(glow.gameObject);
                removed++;
            }
        }

        if (removed == 0)
        {
            Debug.Log("[MainMenuSelectionGlowRemover] No había ningún 'SelectionGlow' en la escena — nada que hacer.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MainMenuSelectionGlowRemover] ✅ {removed} 'SelectionGlow' eliminado(s) y escena guardada. " +
                  "El resto del menú (fondo de cristal, tamaños de botón, etc.) no se ha tocado.");
    }

    static GameObject FindByNameIncludingInactive(string name)
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var t in all)
            if (t.name == name)
                return t.gameObject;
        return null;
    }
}
