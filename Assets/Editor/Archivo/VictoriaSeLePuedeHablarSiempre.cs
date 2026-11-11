#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Victoria llevaba su Interactable como «de un solo uso»: tras la primera charla dejaba de ofrecer
/// el botón de hablar, también cuando la historia espera que se le hable. Ver INC-1045.
/// Se puede ejecutar varias veces.
public static class VictoriaSeLePuedeHablarSiempre
{
    const string Prefab = "Assets/_NPCs/Victoria.prefab";

    [MenuItem("El Sendero/Archivo/NPCs vivos/Victoria: se le puede hablar siempre (INC-1045)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Victoria", "Sal del modo Play antes de ejecutarlo.");
            return;
        }

        var raiz = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (raiz == null)
        {
            OrdenesDelEditor.Avisar("Victoria", "No se encuentra " + Prefab);
            return;
        }

        int cambiados = 0;
        foreach (var interactable in raiz.GetComponentsInChildren<Interactable>(true))
        {
            var datos = new SerializedObject(interactable);
            var unSoloUso = datos.FindProperty("singleUse");
            if (unSoloUso == null || !unSoloUso.boolValue) continue;
            unSoloUso.boolValue = false;
            datos.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(interactable);
            cambiados++;
        }
        if (cambiados > 0)
        {
            EditorUtility.SetDirty(raiz);
            AssetDatabase.SaveAssets();
        }

        string texto = cambiados > 0
            ? "Victoria ya no es de un solo uso: se le puede hablar siempre."
            : "Victoria ya estaba bien: no era de un solo uso.";
        Debug.Log("[INC-1045] " + texto);
        OrdenesDelEditor.Avisar("Victoria", texto);
    }
}
#endif
