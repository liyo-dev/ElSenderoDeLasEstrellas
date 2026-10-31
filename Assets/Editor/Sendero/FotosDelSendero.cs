#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Fotos del Sendero desde puntos fijos del recorrido, a la altura de la cámara de juego, para
/// revisar el arte sin entrar en Play. Se guardan en Guiones/Sendero/fotos/. Abre la escena en
/// aditivo y la pone activa solo mientras fotografía (el cielo y la luz son de la escena activa).
public static class FotosDelSendero
{
    const string RutaEscena = "Assets/Scenes/Worlds/Sendero.unity";

    static readonly (string nombre, Vector3 desde, Vector3 mira)[] Vistas =
    {
        ("01_llegada", new(0, 2.6f, -3), new(0, 2.2f, 40)),
        ("02_cielos_cosidos", new(0, 2.8f, 30), new(-20, 6f, 70)),
        ("03_puerta_will", new(-3, 3.8f, 132), new(5.6f, 3.4f, 150)),
        ("04_camino_largo", new(0, 4.5f, 175), new(0, 3f, 280)),
        ("05_puerta_estela", new(-4, 5.2f, 284), new(5.6f, 4.8f, 300)),
        ("06_ruptura", new(0, 7f, 470), new(0, 4.5f, 500)),
        ("07_altar", new(0, 8.5f, 615), new(0, 7f, 656)),
        ("08_desde_arriba", new(30, 60f, 120), new(0, 0f, 250)),
    };

    [MenuItem("El Sendero/Sendero/3. Fotos del Sendero")]
    public static void Fotografiar()
    {
        if (Application.isPlaying) return;
        var escena = SceneManager.GetSceneByPath(RutaEscena);
        bool cerrar = !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        var activa = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(escena);
        // Lo de otras escenas abiertas (p. ej. la Prueba de Will, en las mismas coordenadas) no sale.
        var apagados = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var otra = SceneManager.GetSceneAt(i);
            if (otra == escena || !otra.isLoaded) continue;
            foreach (var r in otra.GetRootGameObjects())
                if (r.activeSelf) { r.SetActive(false); apagados.Add(r); }
        }

        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "Sendero", "fotos");
        Directory.CreateDirectory(carpeta);
        var go = new GameObject("Cámara de fotos (temporal)");
        SceneManager.MoveGameObjectToScene(go, escena);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 2000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var datos = go.AddComponent<UniversalAdditionalCameraData>();
        datos.renderPostProcessing = true;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            foreach (var v in Vistas)
            {
                go.transform.position = v.desde;
                go.transform.rotation = Quaternion.LookRotation(v.mira - v.desde);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(carpeta, v.nombre + ".png"), tex.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            foreach (var r in apagados) if (r != null) r.SetActive(true);
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
            if (cerrar) EditorSceneManager.CloseScene(escena, true);
        }
        Debug.Log("[FotosDelSendero] Fotos en " + carpeta);
    }
}
#endif
