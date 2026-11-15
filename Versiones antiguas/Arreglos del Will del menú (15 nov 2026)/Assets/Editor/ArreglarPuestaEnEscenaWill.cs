using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez de la portada de la Península:
// 1) El Will sentado en el banco no debe bajar al suelo: se apaga su forzarSuelo.
// 2) El Encuadre de la etapa 1 se coloca delante de Will, mirándolo, con el pueblo detrás.
internal static class ArreglarPuestaEnEscenaWill
{
    private const string Clave = "ElSendero.PuestaWill.Hecho";
    private const string RutaMenu = "Assets/Scenes/Systems/MainMenu.unity";
    private const float Distancia = 3.2f;
    private const float Altura = 1.5f;
    private const float AlturaDeLaCara = 1.2f;

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
            Debug.LogWarning("[Puesta Will] No hay PortadaDelMenu.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        var soPortada = new SerializedObject(portada);
        var etapas = soPortada.FindProperty("etapas");
        var etapa0 = etapas != null && etapas.arraySize > 0
            ? etapas.GetArrayElementAtIndex(0).objectReferenceValue as PortadaEtapa
            : null;
        if (etapa0 == null)
        {
            Debug.LogWarning("[Puesta Will] No hay etapa 1.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        ActorDePortada will = null;
        foreach (var actor in etapa0.GetComponentsInChildren<ActorDePortada>(true))
        {
            var soActor = new SerializedObject(actor);
            var estado = soActor.FindProperty("estadoEnBucle");
            if (estado != null && estado.stringValue == "SitMedium_Loop")
            {
                will = actor;
                break;
            }
        }

        if (will == null)
        {
            Debug.LogWarning("[Puesta Will] No encontré el actor con SitMedium_Loop.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        var soWill = new SerializedObject(will);
        var forzar = soWill.FindProperty("forzarSuelo");
        if (forzar != null) forzar.boolValue = false;
        soWill.ApplyModifiedProperties();
        EditorUtility.SetDirty(will);

        var soEtapa = new SerializedObject(etapa0);
        var encuadreProp = soEtapa.FindProperty("encuadre");
        var encuadre = encuadreProp != null ? encuadreProp.objectReferenceValue as Transform : null;
        if (encuadre == null)
        {
            Debug.LogWarning("[Puesta Will] La etapa 1 no tiene Encuadre.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        Vector3 cara = will.transform.position + Vector3.up * AlturaDeLaCara;
        encuadre.position = will.transform.position + will.transform.forward * Distancia + Vector3.up * Altura;
        encuadre.rotation = Quaternion.LookRotation(cara - encuadre.position);
        EditorUtility.SetDirty(encuadre);

        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        Debug.Log($"[Puesta Will] forzarSuelo={forzar?.boolValue} y Encuadre en {encuadre.position} mirando a Will.");
        if (abrir) EditorSceneManager.CloseScene(menu, true);
    }
}
