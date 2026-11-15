#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Genera la esfera de luz con la que el Archimago protege el valle en el clímax del prólogo.
/// Es geometría (no partículas) para que FusionDeHechizosBeat pueda medirla y hacerla crecer
/// hasta llenar la pantalla. Ver INC-585.
public static class CrearLuzProtectora
{
    public const string RutaPrefab = CrearAgujeroNegro.Carpeta + "/VFX_LuzProtectora.prefab";

    [MenuItem("El Sendero/VFX/Crear luz protectora INC-585")]
    public static void Menu() => Asegurar();

    public static string Asegurar()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(CrearAgujeroNegro.Carpeta + "/Shaders/LuzProtectora.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("El shader Sendero/LuzProtectora falta o contiene errores.");
        var nucleo = Material("LuzProtectora_Nucleo", shader, 2.6f, .9f);
        var halo = Material("LuzProtectora_Halo", shader, 1.4f, .32f);
        var escena = EditorSceneManager.NewPreviewScene();
        try
        {
            var raiz = new GameObject("VFX_LuzProtectora");
            SceneManager.MoveGameObjectToScene(raiz, escena);
            Esfera(raiz.transform, escena, "Nucleo", 1.4f, nucleo);
            Esfera(raiz.transform, escena, "Halo", 2.1f, halo);
            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            if (prefab == null) throw new InvalidOperationException("No se puede guardar " + RutaPrefab);
            AssetDatabase.SaveAssets();
            return RutaPrefab;
        }
        finally { EditorSceneManager.ClosePreviewScene(escena); }
    }

    static void Esfera(Transform padre, Scene escena, string nombre, float tamano, Material material)
    {
        var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        SceneManager.MoveGameObjectToScene(esfera, escena);
        esfera.name = nombre;
        esfera.transform.SetParent(padre, false);
        esfera.transform.localScale = Vector3.one * tamano;
        UnityEngine.Object.DestroyImmediate(esfera.GetComponent<Collider>());
        var renderer = esfera.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Material Material(string nombre, Shader shader, float intensidad, float opacidad)
    {
        string ruta = CrearAgujeroNegro.Carpeta + "/" + nombre + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.shader = shader;
        material.SetFloat("_Intensidad", intensidad);
        material.SetFloat("_Opacidad", opacidad);
        EditorUtility.SetDirty(material);
        return material;
    }
}
#endif
