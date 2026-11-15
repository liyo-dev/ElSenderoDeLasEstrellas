using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Deja las tres escenas de las pruebas del Sendero juntas en Scenes/Worlds y en la build
/// (Laberinto para Will, Chuchelandia para Estela, Parque para Liam) y apunta a ellas el
/// SenderoProgressionController de Sendero.unity. El parque es la escena de demostración del pack
/// Amusement Park, que se mueve (no se copia) para que solo haya una. Idempotente. Ver INC-884.
/// </summary>
public static class EscenasDeLasPruebasDelSendero
{
    private const string ParqueOriginal = "Assets/Art/World/ithappy/Amusement_Park/Scenes/Demonstration_Day.unity";
    private const string Parque = "Assets/Scenes/Worlds/Parque.unity";
    private const string Sendero = "Assets/Scenes/Worlds/Sendero.unity";

    [MenuItem("El Sendero/Archivo/Sendero: escenas de las pruebas (Laberinto, Chuchelandia, Parque)")]
    public static void Ordenar()
    {
        var hecho = new List<string>();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Parque) == null && AssetDatabase.LoadAssetAtPath<SceneAsset>(ParqueOriginal) != null)
        {
            string error = AssetDatabase.MoveAsset(ParqueOriginal, Parque);
            if (!string.IsNullOrEmpty(error)) { Debug.LogError("[Pruebas del Sendero] No se pudo mover el parque: " + error); return; }
            hecho.Add("parque movido a " + Parque);
        }
        if (AsegurarEnBuild(Parque)) hecho.Add("Parque en la build");

        var sendero = SceneManager.GetSceneByPath(Sendero);
        bool abierta = sendero.isLoaded;
        if (abierta && sendero.isDirty)
        {
            Debug.LogWarning("[Pruebas del Sendero] Sendero.unity tiene cambios sin guardar: guárdala y vuelve a ejecutar el menú para apuntar las escenas.");
        }
        else
        {
            if (!abierta) sendero = EditorSceneManager.OpenScene(Sendero, OpenSceneMode.Additive);
            int cambiados = 0;
            foreach (var raiz in sendero.GetRootGameObjects())
                foreach (var c in raiz.GetComponentsInChildren<SenderoProgressionController>(true))
                {
                    var so = new SerializedObject(c);
                    so.FindProperty("_escenaPruebaWill").stringValue = "Laberinto";
                    so.FindProperty("_escenaPruebaEstela").stringValue = "Chuchelandia";
                    so.FindProperty("_escenaPruebaLiam").stringValue = "Parque";
                    if (so.ApplyModifiedPropertiesWithoutUndo()) cambiados++;
                }
            if (cambiados > 0) { EditorSceneManager.SaveScene(sendero); hecho.Add($"Sendero apunta a las tres escenas ({cambiados} controlador)"); }
            if (!abierta) EditorSceneManager.CloseScene(sendero, true);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Pruebas del Sendero] " + (hecho.Count > 0 ? string.Join(" · ", hecho) : "ya estaba todo en su sitio"));
    }

    private static bool AsegurarEnBuild(string ruta)
    {
        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var e in escenas) if (e.path == ruta) return false;
        escenas.Add(new EditorBuildSettingsScene(ruta, true));
        EditorBuildSettings.scenes = escenas.ToArray();
        return true;
    }
}
