using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez: la capa del Will de la portada de la Península también estaba apagada.
// Se enciende y se deja una foto para comprobar cómo queda.
internal static class ProbarCapaWillPortada
{
    private const string RutaMenu = "Assets/Scenes/Systems/MainMenu.unity";

    [MenuItem("El Sendero/Interfaz/Menú principal/Probar la capa del Will de la portada")]
    private static void Probar()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Will portada] La escena no se toca en Play.");
            return;
        }

        Scene menu = SceneManager.GetSceneByName("MainMenu");
        bool abrir = !menu.IsValid() || !menu.isLoaded;
        if (abrir) menu = EditorSceneManager.OpenScene(RutaMenu, OpenSceneMode.Additive);

        PortadaDelMenu portada = null;
        foreach (var raiz in menu.GetRootGameObjects())
        {
            portada = raiz.GetComponentInChildren<PortadaDelMenu>(true);
            if (portada != null) break;
        }

        var soPortada = new SerializedObject(portada);
        var etapas = soPortada.FindProperty("etapas");
        var etapa = etapas.GetArrayElementAtIndex(0).objectReferenceValue as PortadaEtapa;

        ActorDePortada will = null;
        foreach (var actor in etapa.GetComponentsInChildren<ActorDePortada>(true))
        {
            var soActor = new SerializedObject(actor);
            var estado = soActor.FindProperty("estadoEnBucle");
            if (estado != null && estado.stringValue == "SitMedium_Loop") { will = actor; break; }
        }

        GameObject capa = null;
        foreach (var t in will.GetComponentsInChildren<Transform>(true))
            if (t.name == "Cloak02") { capa = t.gameObject; break; }

        if (capa == null)
        {
            Debug.LogWarning("[Will portada] No encuentro Cloak02.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        capa.SetActive(true);
        EditorUtility.SetDirty(capa);
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);

        string foto = null;
        if (etapa.Encuadre != null)
        {
            var desde = etapa.Encuadre.position;
            foto = CapturaDeEscenario.FotoDesde("menu_will_arreglado_capa", desde,
                desde + etapa.Encuadre.forward * 5f, etapa.CampoDeVision);
        }
        Debug.Log($"[Will portada] Capa encendida. Foto: {foto ?? "(sin encuadre)"}");
        if (abrir) EditorSceneManager.CloseScene(menu, true);
    }
}
