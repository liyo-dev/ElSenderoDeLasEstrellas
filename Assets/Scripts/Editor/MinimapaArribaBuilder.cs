using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sube el minimapa a la esquina superior derecha para dejar la de abajo a la cruz de combate
/// (INC-496). El aviso de teletransporte de los puntos de guardado, que ocupaba esa esquina,
/// baja justo debajo del minimapa para no taparse con él.
/// Se puede ejecutar varias veces: solo toca lo que aún no está en su sitio.
/// </summary>
public static class MinimapaArribaBuilder
{
    private const string MinimapPrefabPath = "Assets/Prefabs/Minimap.prefab";
    private const string StartScenePath = "Assets/Scenes/Systems/Start.unity";

    // Mismo margen que tenía abajo: el aro del minimapa queda a ~45 px del borde (a 1920×1080).
    private static readonly Vector2 MinimapPosition = new Vector2(-100f, -100f);

    // El aro del minimapa acaba a ~255 px del borde superior; el aviso empieza 25 px más abajo.
    private static readonly Vector2 TeleportHintPosition = new Vector2(-50f, -280f);

    private static readonly Vector2 TopRight = new Vector2(1f, 1f);

    [MenuItem("El Sendero/Archivo/UI/Subir el minimapa arriba a la derecha (INC-496)")]
    public static void Build()
    {
        var log = new StringBuilder("=== Minimapa arriba (INC-496) ===\n");
        bool ok = MoveMinimap(log) & MoveTeleportHint(log);
        if (ok) Debug.Log(log.ToString());
        else Debug.LogWarning(log.ToString());
    }

    private static bool MoveMinimap(StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MinimapPrefabPath);
        try
        {
            var rt = FindDeep(root.transform, "MinimapRoot") as RectTransform;
            if (rt == null)
            {
                log.AppendLine("  • No encuentro MinimapRoot en Minimap.prefab.");
                return false;
            }

            if (rt.anchorMin == TopRight && rt.anchorMax == TopRight && rt.pivot == TopRight
                && rt.anchoredPosition == MinimapPosition)
            {
                log.AppendLine("Minimapa: ya estaba arriba a la derecha.");
                return true;
            }

            rt.anchorMin = rt.anchorMax = rt.pivot = TopRight;
            rt.anchoredPosition = MinimapPosition;
            PrefabUtility.SaveAsPrefabAsset(root, MinimapPrefabPath);
            log.AppendLine("Minimapa: movido arriba a la derecha (Minimap.prefab guardado).");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool MoveTeleportHint(StringBuilder log)
    {
        Scene scene = SceneManager.GetSceneByPath(StartScenePath);
        bool openedHere = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            RectTransform panel = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name != "TeleportSavePoint") continue;
                panel = go.transform.Find("QuickPanel") as RectTransform;
                break;
            }
            if (panel == null)
            {
                log.AppendLine("  • No encuentro TeleportSavePoint/QuickPanel en Start.unity.");
                return false;
            }

            if (panel.anchoredPosition == TeleportHintPosition)
            {
                log.AppendLine("Aviso de teletransporte: ya estaba debajo del minimapa.");
                return true;
            }

            Undo.RecordObject(panel, "Bajar aviso de teletransporte");
            panel.anchoredPosition = TeleportHintPosition;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("Aviso de teletransporte: bajado debajo del minimapa (Start.unity guardada).");
            return true;
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var t = FindDeep(child, name);
            if (t != null) return t;
        }
        return null;
    }
}
