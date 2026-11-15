using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez: el Encuadre de la portada de la Península se quedó en primer plano
// (Will ocupaba el centro y tapaba el menú). Se pone un plano medio desde delante y algo a su
// izquierda, con Will entero en el tercio derecho y el banco y la farola a la vista.
internal static class AplicarEncuadrePortada
{
    private const string RutaMenu = "Assets/Scenes/Systems/MainMenu.unity";
    private const float Distancia = 3.5f;
    private const float Lado = -0.9f;
    private const float Altura = 1.55f;
    private const float Giro = 10f;

    [MenuItem("El Sendero/Interfaz/Menú principal/Portada 1: poner el encuadre elegido")]
    private static void Aplicar()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Encuadre portada] La escena no se toca en Play.");
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

        var t = will.transform;
        Vector3 adelante = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
        Vector3 derecha = Vector3.Cross(Vector3.up, adelante);
        Vector3 pecho = t.position + Vector3.up * 0.62f;

        Vector3 camara = t.position + adelante * Distancia + derecha * Lado + Vector3.up * Altura;
        var giro = Quaternion.AngleAxis(-Giro, Vector3.up);
        Vector3 mira = camara + giro * (pecho - camara);

        var encuadre = etapa.Encuadre;
        encuadre.position = camara;
        encuadre.rotation = Quaternion.LookRotation(mira - camara, Vector3.up);
        EditorUtility.SetDirty(encuadre);

        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        AssetDatabase.SaveAssets();

        string foto = CapturaDeEscenario.FotoDesde("menu_portada_encuadre_final", camara, mira, etapa.CampoDeVision);
        Debug.Log($"[Encuadre portada] Encuadre en {camara} mirando a {mira}. Foto: {foto ?? "(falló)"}");
        if (abrir) EditorSceneManager.CloseScene(menu, true);
    }
}
