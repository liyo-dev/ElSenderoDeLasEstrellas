#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Un solo uso (30 oct 2026): el primer «Vestir el Sendero» puso el cielo, la luz ambiente y la
/// niebla del Sendero en la escena activa, que era el Laberinto (Prueba de Will). Esto le devuelve
/// los valores de PruebaDeWillBuilder.Ambiente sin guardar la escena.
public static class RepararAmbienteDelLaberinto
{
    [MenuItem("El Sendero/Archivo/Reparar el ambiente del Laberinto (30 oct)")]
    public static void Reparar()
    {
        var lab = SceneManager.GetSceneByName("Laberinto");
        if (!lab.IsValid() || !lab.isLoaded) { Debug.Log("[Reparar] El Laberinto no está abierto: no hay nada que reparar."); return; }
        var activa = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(lab);
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Sendero/PruebaWill/Mat_CieloDeLuz.mat");
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.82f, 0.83f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.76f, 0.78f);
        RenderSettings.ambientGroundColor = new Color(0.74f, 0.7f, 0.64f);
        RenderSettings.fog = false;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lab);
        if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
        Debug.Log("[Reparar] Ambiente del Laberinto devuelto a sus valores.");
    }
}
#endif
