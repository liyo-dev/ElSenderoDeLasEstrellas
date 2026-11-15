#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// La torre de Liam se carga en aditivo sobre MainWorld para «Los planes de Liam», y su
/// PostprocesoDeEscena (exclusivo) silenciaba los volúmenes de MainWorld nada más cargarse, con la
/// pantalla aún en la celebración contra el demonio. Se le marca «Solo con su escena activa»: manda
/// desde el corte de la cinemática, cuando el SequencePlayer marca la torre como activa. Ver INC-956.
/// Se puede ejecutar varias veces.
public static class Cap2PostprocesoDeLaTorre
{
    const string RutaDeLaEscena = "Assets/Scenes/Interior/TorreDeLiam.unity";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Torre de Liam: post-procesado solo en la cinemática (INC-956)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Post-procesado de la torre", "Sal de Play antes de ejecutarlo.");
            return;
        }

        Scene escena = SceneManager.GetSceneByPath(RutaDeLaEscena);
        bool laHeAbierto = !escena.IsValid() || !escena.isLoaded;
        if (laHeAbierto) escena = EditorSceneManager.OpenScene(RutaDeLaEscena, OpenSceneMode.Additive);
        else if (escena.isDirty)
        {
            OrdenesDelEditor.Avisar("Post-procesado de la torre",
                "TorreDeLiam está abierta con cambios sin guardar; no la toco para no guardarlos con los míos.");
            return;
        }

        int cambiados = 0;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var post in raiz.GetComponentsInChildren<PostprocesoDeEscena>(true))
            {
                var so = new SerializedObject(post);
                var campo = so.FindProperty(nameof(PostprocesoDeEscena.soloConSuEscenaActiva));
                if (campo.boolValue) continue;
                campo.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                cambiados++;
            }
        }

        if (cambiados > 0)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }
        if (laHeAbierto) EditorSceneManager.CloseScene(escena, true);

        OrdenesDelEditor.Avisar("Post-procesado de la torre",
            cambiados > 0 ? $"TorreDeLiam: {cambiados} PostprocesoDeEscena con «Solo con su escena activa». Escena guardada."
                          : "TorreDeLiam ya lo tenía puesto; nada que cambiar.");
    }
}
#endif
