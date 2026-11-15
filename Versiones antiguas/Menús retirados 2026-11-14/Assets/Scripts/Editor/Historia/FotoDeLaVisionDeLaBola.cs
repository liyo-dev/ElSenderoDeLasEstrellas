using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Saca una foto de lo que enseña la visión de la bola de la torre de Liam (con Tobi puesto en su
/// cama), para revisar el encuadre sin dar a Play. Ver INC-1122.
public static class FotoDeLaVisionDeLaBola
{
    [MenuItem("El Sendero/Archivo/Foto de la visión de la bola (INC-1122)")]
    private static void Foto()
    {
        const string torre = "Assets/Scenes/Interior/TorreDeLiam.unity";
        var escena = SceneManager.GetSceneByPath(torre);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(torre, OpenSceneMode.Additive);
        GameObject tobi = null;
        try
        {
            var todo = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            var vision = todo.FirstOrDefault(t => t.name == "PROP_VisionDeLaBola");
            var cam = vision != null ? vision.GetComponentInChildren<Camera>(true) : null;
            if (cam == null) { OrdenesDelEditor.Avisar("Foto", "No hay visión montada."); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_NPCs/_GrafoNarrativo/Tobi.prefab");
            if (prefab != null)
            {
                tobi = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                tobi.transform.SetPositionAndRotation(new Vector3(-5988f, 100.519f, 6000.42f), Quaternion.Euler(0f, 180f, 0f));
            }
            var rt = new RenderTexture(1024, 768, 24);
            var antes = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = antes;
            string ruta = Path.GetFullPath("Guiones/_fotos/vision_de_la_bola.png");
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            OrdenesDelEditor.Avisar("Foto", ruta);
        }
        finally
        {
            if (tobi != null) Object.DestroyImmediate(tobi);
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
    }
}
