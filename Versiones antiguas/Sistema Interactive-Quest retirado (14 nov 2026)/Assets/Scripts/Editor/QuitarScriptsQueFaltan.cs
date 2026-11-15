using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Quita de los prefabs los componentes cuyo script ya no existe (los del sistema Interactive/Quest
/// retirado). Menú de un solo uso: ver INC-1206.
/// </summary>
public static class QuitarScriptsQueFaltan
{
    private static readonly string[] Prefabs =
    {
        "Assets/_NPCs/Combat/Erika.prefab",
        "Assets/_NPCs/_GrafoNarrativo/Selene.prefab",
        "Assets/_NPCs/Eldran.prefab",
        "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab",
    };

    [MenuItem("El Sendero/Archivo/Quitar scripts que faltan (sistema Interactive retirado)")]
    private static void Quitar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[QuitarScriptsQueFaltan] Sal de Play antes de usarlo.");
            return;
        }
        var informe = new StringBuilder();
        foreach (var ruta in Prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null) { informe.Append($"{ruta}: no existe. "); continue; }
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            int quitados = 0;
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                quitados += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (quitados > 0) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            PrefabUtility.UnloadPrefabContents(raiz);
            informe.Append($"{System.IO.Path.GetFileNameWithoutExtension(ruta)}: {quitados}. ");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[QuitarScriptsQueFaltan] " + informe);
    }
}
