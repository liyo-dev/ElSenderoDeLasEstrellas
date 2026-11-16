#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Deja «Nueva Partida» cargando MainWorld, que es lo que espera el grafo: el paso «1.- Carga el
/// valle del prologo» añade Prologo_Valle encima y el «3b» lo descarga al empezar el capítulo.
public static class ArreglarNuevaPartidaDelPrologo
{
    const string Ruta = "Assets/Scenes/Systems/MainMenu.unity";

    [MenuItem("El Sendero/Archivo/Nueva partida: cargar MainWorld")]
    public static void Aplicar()
    {
        var escena = SceneManager.GetSceneByPath(Ruta);
        bool abierta = escena.IsValid() && escena.isLoaded;
        if (!abierta) escena = EditorSceneManager.OpenScene(Ruta, OpenSceneMode.Additive);

        MainMenuController menu = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            menu = raiz.GetComponentInChildren<MainMenuController>(true);
            if (menu != null) break;
        }
        if (menu == null) { Debug.LogError("[Nueva partida] No encuentro MainMenuController en " + Ruta + "."); return; }

        var so = new SerializedObject(menu);
        var campo = so.FindProperty("nextSceneNewGame");
        if (campo == null) { Debug.LogError("[Nueva partida] No encuentro nextSceneNewGame."); return; }
        campo.stringValue = "MainWorld";
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Nueva partida] nextSceneNewGame = MainWorld en " + Ruta + ".");
        if (!abierta) EditorSceneManager.CloseScene(escena, true);
    }
}
#endif
