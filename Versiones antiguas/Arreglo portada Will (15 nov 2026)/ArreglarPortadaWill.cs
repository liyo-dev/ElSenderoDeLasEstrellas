using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez: la portada "Etapa 1 · Península" tenía esperaAlSalir = 1,4 pero
// actoresQueSalen vacío (Will no se levantaba del banco). Se engancha su ActorDePortada y se
// fuerza la etapa 1 SOLO EN EL EDITOR para comprobarlo. El campo forzarEtapaEnEditor no existe
// en la build: la selección normal por partida no cambia.
internal static class ArreglarPortadaWill
{
    private const string Clave = "ElSendero.PortadaWill.Hecho";
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
            Debug.LogWarning("[Portada Will] No hay PortadaDelMenu en MainMenu.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        var soPortada = new SerializedObject(portada);
        var etapas = soPortada.FindProperty("etapas");
        if (etapas == null || etapas.arraySize == 0)
        {
            Debug.LogWarning("[Portada Will] La portada no tiene etapas.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        var etapa0 = etapas.GetArrayElementAtIndex(0).objectReferenceValue as PortadaEtapa;
        if (etapa0 == null)
        {
            Debug.LogWarning("[Portada Will] La etapa 1 no tiene PortadaEtapa.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        ActorDePortada will = null;
        foreach (var actor in etapa0.GetComponentsInChildren<ActorDePortada>(true))
        {
            if (actor.name == "WILL") { will = actor; break; }
        }

        if (will == null)
        {
            Debug.LogWarning("[Portada Will] No encontré el ActorDePortada 'WILL' dentro de la etapa 1.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        var soEtapa = new SerializedObject(etapa0);
        var actores = soEtapa.FindProperty("actoresQueSalen");
        if (actores != null)
        {
            actores.arraySize = 1;
            actores.GetArrayElementAtIndex(0).objectReferenceValue = will;
            soEtapa.ApplyModifiedProperties();
        }

        var forzar = soPortada.FindProperty("forzarEtapaEnEditor");
        if (forzar != null) forzar.intValue = 0;
        soPortada.ApplyModifiedProperties();

        EditorUtility.SetDirty(etapa0);
        EditorUtility.SetDirty(portada);
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);

        Debug.Log($"[Portada Will] Enganchado '{will.name}' a la etapa 1 ('{etapa0.name}') y forzada la etapa 1 en el editor.");
        if (abrir) EditorSceneManager.CloseScene(menu, true);
    }
}
