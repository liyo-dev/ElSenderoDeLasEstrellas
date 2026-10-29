#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// El aro de runas que llevaba el Demonio al cuello, roto: el que Eldran saca del cofre de sal en
/// «La mañana después», y el trozo que Will se lleva (objeto del inventario con su icono).
public static class Cap2AroDelDemonio
{
    const string Carpeta = "Assets/Art/Items/AroDelDemonio";
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    const string Objeto = "Assets/_ITEMS/IT_TrozoDelAro.asset";
    static readonly Color Hierro = new(0.20f, 0.19f, 0.19f);
    static readonly Color Runas = new(1f, 0.42f, 0.16f);

    [MenuItem("El Sendero/Archivo/Capítulo 2/Crear aro roto del Demonio")]
    public static void Crear()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Hay que salir de Play.");
        var hierro = Material("Hierro del aro", Hierro, 0.75f, 0.35f, Color.black);
        var runas = MaterialSinLuz("Runas del aro", Runas);

        // El aro: le falta un trozo (el que se lleva Will) y tiene las runas a lo largo.
        var aro = Prefab("AroRoto", Arco("Malla aro roto", 0.16f, 0.028f, 35f, 345f), hierro, runas, 35f, 345f, 0.16f);
        var trozo = Prefab("TrozoDelAro", Arco("Malla trozo del aro", 0.16f, 0.028f, -15f, 32f), hierro, runas, -15f, 32f, 0.16f);
        var icono = Icono(trozo);

        var item = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Objeto) ?? throw new InvalidOperationException("Falta " + Objeto);
        var so = new SerializedObject(item);
        so.FindProperty("icon").objectReferenceValue = icono;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);

        PonerEnLaCasa(aro);
        AssetDatabase.SaveAssets();
        Debug.Log("[AroDelDemonio] Aro roto, trozo e icono creados; PROP_Aro de la casa de Will sustituido.");
    }

    // Las runas brillan por sí mismas: sin luz, no dependen de cómo esté iluminada la escena.
    static Material MaterialSinLuz(string nombre, Color color)
    {
        string ruta = $"{Carpeta}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, ruta); }
        m.shader = Shader.Find("Universal Render Pipeline/Unlit");
        m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Material(string nombre, Color color, float metal, float suavidad, Color emision)
    {
        string ruta = $"{Carpeta}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ruta); }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Smoothness", suavidad);
        if (emision.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emision);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Toro abierto entre dos ángulos (grados), tumbado en el plano XZ, con tapas en los cortes.
    static Mesh Arco(string nombre, float radio, float grosor, float desde, float hasta)
    {
        const int pasos = 48, lados = 10;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        int anillos = Mathf.Max(2, Mathf.CeilToInt(pasos * (hasta - desde) / 360f) + 1);
        for (int i = 0; i < anillos; i++)
        {
            float a = Mathf.Deg2Rad * Mathf.Lerp(desde, hasta, i / (float)(anillos - 1));
            var centro = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radio;
            var fuera = centro.normalized;
            for (int j = 0; j < lados; j++)
            {
                float b = j * Mathf.PI * 2f / lados;
                // Sección algo aplastada, como un aro forjado.
                var normal = fuera * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                v.Add(centro + fuera * Mathf.Cos(b) * grosor + Vector3.up * Mathf.Sin(b) * grosor * 0.75f);
                n.Add(normal);
            }
        }
        for (int i = 0; i < anillos - 1; i++)
            for (int j = 0; j < lados; j++)
            {
                int a0 = i * lados + j, a1 = i * lados + (j + 1) % lados, b0 = a0 + lados, b1 = a1 + lados;
                t.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        // Tapas en los dos extremos rotos.
        foreach (int fila in new[] { 0, anillos - 1 })
        {
            int c = v.Count;
            var centro = Vector3.zero; for (int j = 0; j < lados; j++) centro += v[fila * lados + j]; centro /= lados;
            var dir = (fila == 0 ? -1f : 1f) * Vector3.Cross(Vector3.up, centro.normalized);
            v.Add(centro); n.Add(dir);
            for (int j = 0; j < lados; j++) { v.Add(v[fila * lados + j]); n.Add(dir); }
            for (int j = 0; j < lados; j++)
            {
                int p = c + 1 + j, q = c + 1 + (j + 1) % lados;
                if (fila == 0) t.AddRange(new[] { c, p, q }); else t.AddRange(new[] { c, q, p });
            }
        }
        string ruta = $"{Carpeta}/{nombre}.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, ruta); }
        m.Clear(); m.name = nombre;
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Prefab(string nombre, Mesh malla, Material hierro, Material runas, float desde, float hasta, float radio)
    {
        var raiz = new GameObject(nombre);
        try
        {
            var cuerpo = new GameObject("Hierro");
            cuerpo.transform.SetParent(raiz.transform, false);
            cuerpo.AddComponent<MeshFilter>().sharedMesh = malla;
            cuerpo.AddComponent<MeshRenderer>().sharedMaterial = hierro;
            // Runas: marcas incandescentes en la cara de arriba, cada 24°.
            var cubo = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for (float a = desde + 8f; a < hasta - 4f; a += 24f)
            {
                var r = new GameObject("Runa");
                r.transform.SetParent(raiz.transform, false);
                float rad = a * Mathf.Deg2Rad;
                r.transform.localPosition = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * radio + Vector3.up * 0.019f;
                r.transform.localRotation = Quaternion.Euler(0, -a + ((int)a % 48 == 0 ? 20 : -20), 0);
                r.transform.localScale = new Vector3(0.012f, 0.004f, 0.03f);
                r.AddComponent<MeshFilter>().sharedMesh = cubo;
                r.AddComponent<MeshRenderer>().sharedMaterial = runas;
            }
            string ruta = $"{Carpeta}/{nombre}.prefab";
            return PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        }
        finally { UnityEngine.Object.DestroyImmediate(raiz); }
    }

    /// Foto del trozo, en diagonal y sobre fondo transparente, como icono del inventario.
    static Sprite Icono(GameObject trozo)
    {
        const int lado = 256;
        var escenario = new GameObject("Foto del trozo (temporal)");
        try
        {
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(trozo);
            modelo.transform.SetParent(escenario.transform, false);
            modelo.transform.position = new Vector3(0, -5000, 0);
            var rs = modelo.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var luz = new GameObject("Luz").AddComponent<Light>();
            luz.transform.SetParent(escenario.transform, false);
            luz.type = LightType.Directional; luz.intensity = 1.4f; luz.transform.rotation = Quaternion.Euler(50, -30, 0);
            var cam = new GameObject("Cámara").AddComponent<Camera>();
            cam.transform.SetParent(escenario.transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.z) * 1.25f;
            cam.transform.position = b.center + new Vector3(0, 1f, -0.6f);
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 5f;
            var rt = new RenderTexture(lado, lado, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var anterior = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, lado, lado), 0, 0); tex.Apply();
            RenderTexture.active = anterior;
            cam.targetTexture = null; rt.Release();
            string ruta = $"{Carpeta}/Icono trozo del aro.png";
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ruta);
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            imp.textureType = TextureImporterType.Sprite; imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        }
        finally { UnityEngine.Object.DestroyImmediate(escenario); }
    }

    /// Sustituye el aro genérico de la casa de Will por el aro roto, en el mismo sitio y con el
    /// mismo id de decorado para la secuencia.
    static void PonerEnLaCasa(GameObject aro)
    {
        var escena = SceneManager.GetSceneByPath(Casa);
        if (!escena.IsValid() || !escena.isLoaded) escena = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var viejo = todos.FirstOrDefault(t => t.name == "PROP_Aro") ?? throw new InvalidOperationException("No encuentro PROP_Aro en WillHouse.");
        var stage = viejo.GetComponentInParent<SequenceStage>(true) ?? throw new InvalidOperationException("PROP_Aro no cuelga de un SequenceStage.");
        var nuevo = (GameObject)PrefabUtility.InstantiatePrefab(aro, viejo.parent);
        nuevo.name = "PROP_Aro";
        nuevo.transform.SetPositionAndRotation(viejo.position, Quaternion.Euler(0, 25, 0));
        nuevo.transform.localScale = Vector3.one;
        nuevo.SetActive(false);
        var so = new SerializedObject(stage);
        var props = so.FindProperty("_props");
        for (int i = 0; i < props.arraySize; i++)
        {
            var p = props.GetArrayElementAtIndex(i);
            if (p.FindPropertyRelative("id").stringValue == "PROP_Aro") p.FindPropertyRelative("target").objectReferenceValue = nuevo.transform;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.Object.DestroyImmediate(viejo.gameObject);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }
}
#endif
