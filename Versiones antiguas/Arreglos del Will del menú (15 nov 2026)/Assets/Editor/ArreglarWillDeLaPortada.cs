using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez: la portada de la Península se quedó con el Will sin cabeza, sin pelo,
// sin cara y sin ropa. Su tablero de piezas está apagado entero y alguien encendió el cuerpo base
// (Body01), que es el que sale desnudo y descabezado. Se encienden las mismas piezas que lleva el
// Will de las otras portadas (ropa de aventurero, cabeza, pelo, ojos y boca) y se apaga Body01.
internal static class ArreglarWillDeLaPortada
{
    private const string RutaMenu = "Assets/Scenes/Systems/MainMenu.unity";
    private const string NombreDelActor = "SitMedium_Loop";

    private static readonly string[] PiezasQueSeEncienden =
    {
        "Body23_Aventurero", "Head01_Male", "Hair16_Heroe", "Eye30", "Mouth27",
    };

    private const string PiezaQueSeApaga = "Body01";

    [MenuItem("El Sendero/Interfaz/Menú principal/Arreglar el Will de la portada")]
    private static void Arreglar()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Will portada] La escena no se toca en Play: para el Play y vuelve a lanzar el menú.");
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
        if (portada == null)
        {
            Debug.LogWarning("[Will portada] No hay PortadaDelMenu en MainMenu.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        var soPortada = new SerializedObject(portada);
        var etapas = soPortada.FindProperty("etapas");
        var etapa = etapas != null && etapas.arraySize > 0
            ? etapas.GetArrayElementAtIndex(0).objectReferenceValue as PortadaEtapa
            : null;
        if (etapa == null)
        {
            Debug.LogWarning("[Will portada] La primera etapa de la portada no existe.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        ActorDePortada will = null;
        foreach (var actor in etapa.GetComponentsInChildren<ActorDePortada>(true))
        {
            var soActor = new SerializedObject(actor);
            var estado = soActor.FindProperty("estadoEnBucle");
            if (estado != null && estado.stringValue == NombreDelActor)
            {
                will = actor;
                break;
            }
        }
        if (will == null)
        {
            Debug.LogWarning($"[Will portada] No encuentro el actor «{NombreDelActor}» en la primera etapa.");
            if (abrir) EditorSceneManager.CloseScene(menu, true);
            return;
        }

        var piezas = new System.Collections.Generic.Dictionary<string, GameObject>();
        foreach (var t in will.GetComponentsInChildren<Transform>(true))
            if (!piezas.ContainsKey(t.name)) piezas[t.name] = t.gameObject;

        var resumen = new System.Text.StringBuilder();
        foreach (var nombre in PiezasQueSeEncienden)
        {
            if (!piezas.TryGetValue(nombre, out var pieza))
            {
                resumen.Append(" falta ").Append(nombre);
                continue;
            }
            pieza.SetActive(true);
            EditorUtility.SetDirty(pieza);
            resumen.Append(' ').Append(nombre);
        }
        if (piezas.TryGetValue(PiezaQueSeApaga, out var cuerpoBase))
        {
            cuerpoBase.SetActive(false);
            EditorUtility.SetDirty(cuerpoBase);
            resumen.Append(" · apagado ").Append(PiezaQueSeApaga);
        }

        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        AssetDatabase.SaveAssets();

        string foto = null;
        if (etapa.Encuadre != null)
        {
            var desde = etapa.Encuadre.position;
            foto = CapturaDeEscenario.FotoDesde("menu_will_arreglado", desde,
                desde + etapa.Encuadre.forward * 5f, etapa.CampoDeVision);
        }

        Debug.Log($"[Will portada] Encendidas:{resumen}. Foto: {foto ?? "(sin encuadre)"}");
        if (abrir) EditorSceneManager.CloseScene(menu, true);
    }
}
