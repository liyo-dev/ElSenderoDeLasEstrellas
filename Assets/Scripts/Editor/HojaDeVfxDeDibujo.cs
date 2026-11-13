using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Hoja de contactos de los efectos de hechizo con aspecto de dibujo, sin entrar en Play: monta cada
/// prefab en una escena de previsualización, avanza sus partículas y lo fotografía en dos momentos.
/// Deja la hoja en Guiones/_ordenes/hoja_vfx_dibujo.png. Ver INC-1127
public static class HojaDeVfxDeDibujo
{
    private const string Salida = "Guiones/_ordenes/hoja_vfx_dibujo.png";
    private const int Lado = 200;
    private static readonly float[] Momentos = { 0.12f, 0.5f };

    /// Un efecto es de dibujo si alguna de sus partículas usa una pieza de Texturas/Dibujo.
    private static bool EsDeDibujo(string ruta)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        return go != null && go.GetComponentsInChildren<ParticleSystemRenderer>(true)
            .Any(r => r.sharedMaterial != null && r.sharedMaterial.mainTexture != null && r.sharedMaterial.mainTexture.name.StartsWith("T_Dibujo_"));
    }

    [MenuItem("El Sendero/Magia/Hoja de los VFX de dibujo")]
    public static void Hacer()
    {
        var rutas = AssetDatabase.FindAssets("t:Prefab", new[] { VfxPropiosDeHechizosBuilder.Carpeta, "Assets/VFX", "Assets/_VFX", "Assets/Art", "Assets/Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(EsDeDibujo)
            .OrderBy(r => r).ToList();

        var escena = EditorSceneManager.NewPreviewScene();
        var rt = new RenderTexture(Lado, Lado, 24, RenderTextureFormat.ARGB32);
        var camGo = new GameObject("Cámara de la hoja");
        SceneManager.MoveGameObjectToScene(camGo, escena);
        var cam = camGo.AddComponent<Camera>();
        cam.scene = escena;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.17f, 0.14f, 0.27f);
        cam.fieldOfView = 40f;
        cam.targetTexture = rt;
        // Luz de día para que las piezas 3D (que usan el shader del mundo) se vean con su sombreado.
        var solGo = new GameObject("Sol de la hoja");
        SceneManager.MoveGameObjectToScene(solGo, escena);
        var sol = solGo.AddComponent<Light>();
        sol.type = LightType.Directional;
        sol.intensity = 1.2f;
        solGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        int columnas = Momentos.Length * 4;
        int filas = Mathf.CeilToInt(rutas.Count / 4f);
        var hoja = new Texture2D(columnas * Lado, filas * Lado, TextureFormat.RGBA32, false);
        var lectura = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
        var nombres = new List<string>();
        try
        {
            for (int i = 0; i < rutas.Count; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutas[i]);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                nombres.Add($"{i}: {prefab.name.Replace("VFX_Hechizo_", "")}");
                var sistemas = inst.GetComponentsInChildren<ParticleSystem>(true);
                for (int m = 0; m < Momentos.Length; m++)
                {
                    foreach (var ps in sistemas) ps.Simulate(Momentos[m], false, true, true);
                    var b = new Bounds(inst.transform.position, Vector3.one);
                    foreach (var r in inst.GetComponentsInChildren<ParticleSystemRenderer>(true)) b.Encapsulate(r.bounds);
                    float d = Mathf.Max(b.extents.magnitude, 0.8f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    var dir = Quaternion.Euler(30f, -35f, 0f) * Vector3.forward;
                    cam.transform.position = b.center - dir * d;
                    cam.transform.rotation = Quaternion.LookRotation(dir);
                    cam.Render();
                    RenderTexture.active = rt;
                    lectura.ReadPixels(new Rect(0, 0, Lado, Lado), 0, 0);
                    lectura.Apply();
                    int col = (i % 4) * Momentos.Length + m;
                    int fila = filas - 1 - i / 4;
                    hoja.SetPixels(col * Lado, fila * Lado, Lado, Lado, lectura.GetPixels());
                }
                Object.DestroyImmediate(inst);
            }
            hoja.Apply();
            File.WriteAllBytes(Salida, hoja.EncodeToPNG());
            File.WriteAllLines(Path.ChangeExtension(Salida, ".txt"), nombres);
            Debug.Log($"[VFX] Hoja de {rutas.Count} efectos en {Salida}.");
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(hoja);
            Object.DestroyImmediate(lectura);
            EditorSceneManager.ClosePreviewScene(escena);
        }
    }
}
