using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arreglo de una sola vez: el Encuadre de la portada de la Península pasa a ser el plano bueno
// del menú: cámara detrás de Will y a su derecha, mirando al pueblo por encima de las flores,
// con Will asomando abajo en el centro. Son los valores que dejó Raúl en la Main Camera.
internal static class PonerEncuadreDelPueblo
{
    private const string RutaMenu = "Assets/Scenes/Systems/MainMenu.unity";

    private static readonly Vector3 Posicion = new Vector3(22f, 28.1f, -87.5f);
    private static readonly Vector3 Rotacion = new Vector3(7.53f, 193f, 0f);

    [MenuItem("El Sendero/Interfaz/Menú principal/Portada 1: encuadre del pueblo")]
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

        var encuadre = etapa.Encuadre;
        encuadre.SetPositionAndRotation(Posicion, Quaternion.Euler(Rotacion));
        EditorUtility.SetDirty(encuadre);

        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        AssetDatabase.SaveAssets();

        string foto = CapturaDeEscenario.FotoDesde("menu_portada_encuadre_final",
            encuadre.position, encuadre.position + encuadre.forward * 20f, etapa.CampoDeVision);
        Debug.Log($"[Encuadre portada] Encuadre del pueblo en {encuadre.position} {Rotacion}. Foto: {foto ?? "(falló)"}");
        if (abrir) EditorSceneManager.CloseScene(menu, true);
    }
}
