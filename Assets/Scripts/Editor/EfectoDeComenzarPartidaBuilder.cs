using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EfectoDeComenzarPartidaBuilder
{
    private const string RutaEscena = "Assets/Scenes/Systems/MainMenu.unity";

    [MenuItem("El Sendero/MainMenu/Montar efecto de Comenzar partida")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Comenzar partida", "Sal del modo Play para montar el efecto.", "Aceptar");
            return;
        }
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>("Assets/_AUDIOPROFILE/AudioGraphProfile.asset");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX_Menu/UI_ComenzarPartida.wav");
        if (!perfil || !clip)
        {
            EditorUtility.DisplayDialog("Comenzar partida", "No se encuentra el perfil de audio o el clip de comienzo.", "Aceptar");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        if (!escena.IsValid() || !escena.isLoaded)
            escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        MainMenuController menu = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            menu = raiz.GetComponentInChildren<MainMenuController>(true);
            if (menu) break;
        }
        if (!menu)
        {
            EditorUtility.DisplayDialog("Comenzar partida", "MainMenu no contiene MainMenuController.", "Aceptar");
            return;
        }
        var efecto = menu.GetComponent<EfectoDeComenzarPartida>();
        bool componenteNuevo = !efecto;
        if (!efecto) efecto = Undo.AddComponent<EfectoDeComenzarPartida>(menu.gameObject);
        var serializado = new SerializedObject(menu);
        serializado.FindProperty("efectoAlComenzar").objectReferenceValue = efecto;
        serializado.ApplyModifiedProperties();
        var entrada = perfil.eventSfx.Find(e => e != null && e.eventKey == "UI_ComenzarPartida");
        bool entradaNueva = entrada == null;
        if (entradaNueva)
        {
            Undo.RecordObject(perfil, "Registrar sonido de comienzo");
            perfil.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = "UI_ComenzarPartida", sfx = clip });
            EditorUtility.SetDirty(perfil);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        bool guardada = EditorSceneManager.SaveScene(escena);
        string resumen = $"Componente: {(componenteNuevo ? "añadido" : "existente")}. Referencia asignada.\nSFX: {(entradaNueva ? "registrado" : "existente")}.\nEscena: {(guardada ? "guardada" : "no se ha podido guardar")}.";
        Debug.Log("[Comenzar partida] " + resumen);
        EditorUtility.DisplayDialog("Comenzar partida", resumen, "Aceptar");
    }
}
