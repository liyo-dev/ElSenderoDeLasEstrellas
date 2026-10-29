using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AjustarOrdenBannerMision
{
    const string RutaStart = "Assets/Scenes/Systems/Start.unity";

    [MenuItem("El Sendero/Archivo/Ajustar Orden Banner Mision")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlaying) return;

        bool startYaAbierta = false;
        Scene startScene = default;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (s.path == RutaStart)
            {
                startYaAbierta = true;
                startScene = s;
                break;
            }
        }

        bool abrirStart = !startYaAbierta;
        if (abrirStart)
        {
            startScene = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);
        }

        if (startScene.IsValid() && startScene.isLoaded)
        {
            var banners = Object.FindObjectsByType<QuestStartedBannerUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int mod = 0;
            foreach (var b in banners)
            {
                if (b.gameObject.scene == startScene)
                {
                    var canvas = b.GetComponentInChildren<Canvas>(true);
                    if (canvas != null)
                    {
                        var so = new SerializedObject(canvas);
                        so.FindProperty("m_OverrideSorting").boolValue = true;
                        so.FindProperty("m_SortingOrder").intValue = 110;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(canvas);
                        mod++;
                    }
                }
            }
            if (mod > 0)
            {
                EditorSceneManager.MarkSceneDirty(startScene);
                EditorSceneManager.SaveScene(startScene);
                Debug.Log($"[AjustarOrdenBannerMision] ✓ {RutaStart}: {mod} Canvas de QuestStartedBannerUI actualizado con sortingOrder 110.");
            }
            if (abrirStart)
            {
                EditorSceneManager.CloseScene(startScene, true);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
