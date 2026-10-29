#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Ajustes de assets y escenas que acompañan al rediseño del 29 oct 2026. Se ejecuta una vez desde
/// el buzón de órdenes y después se archiva. Deja su informe en Logs/claude/herramienta.txt.
public static class HerramientaDeUnSoloUso
{
    private const string Start = "Assets/Scenes/Systems/Start.unity";

    [MenuItem("El Sendero/Archivo/Rediseño 29 oct/Fase 3")]
    public static void Fase3()
    {
        var informe = new StringBuilder();

        // Los SpecialAttackSO toman su coreografía en OnEnable; guardarlos la deja escrita.
        foreach (var guid in AssetDatabase.FindAssets("t:SpecialAttackSO"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<SpecialAttackSO>(ruta);
            if (so == null) continue;
            EditorUtility.SetDirty(so);
            informe.AppendLine($"guardado {ruta} coreografia={so.coreografia}");
        }
        AssetDatabase.SaveAssets();

        // PartyStatsManager era un singleton vacío: fuera de Start.
        var escena = SceneManager.GetSceneByPath(Start);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(Start, OpenSceneMode.Additive);
        int quitados = 0;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var c in raiz.GetComponentsInChildren<PartyStatsManager>(true))
            {
                var go = c.gameObject;
                if (go.GetComponents<Component>().Length <= 2 && go.transform.childCount == 0) Object.DestroyImmediate(go);
                else Object.DestroyImmediate(c);
                quitados++;
            }
        }
        if (quitados > 0)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }
        informe.AppendLine($"PartyStatsManager quitados de Start: {quitados}");
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);

        File.WriteAllText(Path.Combine("Logs", "claude", "herramienta.txt"), informe.ToString());
    }
}
#endif
