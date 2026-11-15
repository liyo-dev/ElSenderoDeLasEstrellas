using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Prueba de encuadre de la portada de la Península: saca varias fotos desde posiciones
// distintas alrededor de Will, sin tocar el Encuadre guardado. Will debe quedar entero, en el
// tercio derecho y sin meterse debajo del título ni de los botones.
internal static class ProbarEncuadrePortada
{
    private const string RutaMenu = "Assets/Scenes/Systems/MainMenu.unity";

    // (nombre, distancia, lado, altura sobre el asiento, giro a la izquierda en grados)
    private static readonly (string nombre, float distancia, float lado, float altura, float giro)[] Pruebas =
    {
        ("menu_cam_k", 3.5f, -0.9f, 1.55f, 10f),
        ("menu_cam_l", 3.8f, -1.3f, 1.75f, 11f),
        ("menu_cam_m", 3.2f, 0.8f, 1.45f, 9f),
        ("menu_cam_n", 4.0f, -1.6f, 2.00f, 12f),
        ("menu_cam_o", 3.0f, -0.6f, 1.35f, 8f),
        ("menu_cam_p", 4.4f, 1.6f, 1.90f, 12f),
    };

    [MenuItem("El Sendero/Interfaz/Menú principal/Probar encuadres de la portada")]
    private static void Probar()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Encuadre portada] Las fotos no se sacan en Play.");
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
        float lente = etapa.CampoDeVision > 0f ? etapa.CampoDeVision : 45f;

        var rutas = new System.Text.StringBuilder();
        foreach (var prueba in Pruebas)
        {
            Vector3 camara = t.position + adelante * prueba.distancia + derecha * prueba.lado
                             + Vector3.up * prueba.altura;
            var giro = Quaternion.AngleAxis(-prueba.giro, Vector3.up);
            Vector3 mira = camara + giro * (pecho - camara);
            string ruta = CapturaDeEscenario.FotoDesde(prueba.nombre, camara, mira, lente);
            rutas.Append(' ').Append(ruta ?? "(falló)");
        }

        Debug.Log($"[Encuadre portada] {rutas}");
        if (abrir) EditorSceneManager.CloseScene(menu, true);
    }
}
