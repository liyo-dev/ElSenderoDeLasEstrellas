using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez: el Will de la portada es un cuerpo de jugador modular y todos sus
// Body01..Body20 estaban inactivos; el ModularAutoBuilder conserva lo activo, así que no había
// cuerpo. Se activa el cuerpo por defecto en el Will de la etapa 1 (Península).
internal static class ArreglarCuerpoWillPortada
{
    private const string Clave = "ElSendero.CuerpoWillPortada.Hecho";
    private const string RutaMenu = "Assets/Scenes/Systems/MainMenu.unity";

    [InitializeOnLoadMethod]
    private static void Arrancar()
    {
        if (SessionState.GetBool(Clave, false)) return;
        EditorApplication.update += Intentar;
    }

    private static void Intentar()
    {
        if (SessionState.GetBool(Clave, false))
        {
            EditorApplication.update -= Intentar;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.update -= Intentar;
        SessionState.SetBool(Clave, true);
        Aplicar();
    }

    private static void Aplicar()
    {
        Scene menu = SceneManager.GetSceneByName("MainMenu");
        bool abrir = !menu.IsValid() || !menu.isLoaded;
        if (abrir) menu = EditorSceneManager.OpenScene(RutaMenu, OpenSceneMode.Additive);

        PortadaDelMenu portada = null;
        foreach (var raiz in menu.GetRootGameObjects())
        {
            portada = raiz.GetComponentInChildren<PortadaDelMenu>(true);
            if (portada != null) break;
        }
        if (portada == null)
        {
            Debug.LogWarning("[Will portada] No hay PortadaDelMenu.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        var so = new SerializedObject(portada);
        var etapas = so.FindProperty("etapas");
        var etapa0 = etapas != null && etapas.arraySize > 0
            ? etapas.GetArrayElementAtIndex(0).objectReferenceValue as PortadaEtapa
            : null;
        if (etapa0 == null)
        {
            Debug.LogWarning("[Will portada] No hay etapa 1.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        Transform cuerpo = null;
        foreach (var t in etapa0.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Body01") { cuerpo = t; break; }
        }

        if (cuerpo == null)
        {
            Debug.LogWarning("[Will portada] No encontré Body01 dentro de la etapa 1.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        cuerpo.gameObject.SetActive(true);
        EditorUtility.SetDirty(cuerpo.gameObject);
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        Debug.Log($"[Will portada] Activado '{cuerpo.name}' en la etapa 1.");
        if (abrir) EditorSceneManager.CloseScene(menu, true);
    }
}
