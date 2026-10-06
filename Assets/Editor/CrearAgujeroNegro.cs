#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Genera un VFX local reutilizable; la animación del disco reside en el material.
public static class CrearAgujeroNegro
{
    public const string Carpeta = "Assets/_VFX/Prologo";
    public const string RutaPrefab = Carpeta + "/VFX_AgujeroNegro.prefab";

    [MenuItem("El Sendero/VFX/Crear agujero negro INC-576")]
    public static void Menu() => Asegurar();

    public static string Asegurar()
    {
        AsegurarCarpeta(Carpeta);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Carpeta + "/Shaders/AgujeroNegro.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("El shader Sendero/AgujeroNegro falta o contiene errores.");
        var nucleo = Material("AgujeroNegro_Nucleo", shader, 0, 6, 1);
        var disco = Material("AgujeroNegro_Disco", shader, 1, 5, 1);
        var humo = Material("AgujeroNegro_Humo", shader, 2, 0, .28f);
        var chispas = Material("AgujeroNegro_Chispas", shader, 3, 7, 1);
        var halo = Material("AgujeroNegro_Halo", shader, 2, 0, .22f);
        Mesh anillo = CrearAnillo();
        var escena = EditorSceneManager.NewPreviewScene();
        try
        {
            var raiz = new GameObject("VFX_AgujeroNegro");
            SceneManager.MoveGameObjectToScene(raiz, escena);
            var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SceneManager.MoveGameObjectToScene(esfera, escena);
            esfera.name = "Nucleo";
            esfera.transform.SetParent(raiz.transform, false);
            esfera.transform.localScale = Vector3.one * 1.2f;
            UnityEngine.Object.DestroyImmediate(esfera.GetComponent<Collider>());
            ConfigurarRenderer(esfera.GetComponent<MeshRenderer>(), nucleo);
            var aro = new GameObject("DiscoDeAcrecion", typeof(MeshFilter), typeof(MeshRenderer));
            aro.transform.SetParent(raiz.transform, false);
            aro.transform.localRotation = Quaternion.Euler(18, 0, 12);
            aro.GetComponent<MeshFilter>().sharedMesh = anillo;
            ConfigurarRenderer(aro.GetComponent<MeshRenderer>(), disco);
            CrearAbsorcion(raiz.transform, "HumoAbsorbido", humo, new Color(.04f,.025f,.055f,.7f), .18f, 45);
            CrearAbsorcion(raiz.transform, "ChispasAbsorbidas", chispas, new Color(1,.27f,.025f,1), .035f, 32);
            CrearHalo(raiz.transform, halo);
            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            if (prefab == null) throw new InvalidOperationException("No se puede guardar " + RutaPrefab);
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            return RutaPrefab;
        }
        finally { EditorSceneManager.ClosePreviewScene(escena); }
    }

    static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        int barra = ruta.LastIndexOf('/');
        string padre = ruta.Substring(0, barra);
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, ruta.Substring(barra + 1));
    }

    static Material Material(string nombre, Shader shader, int modo, float intensidad, float opacidad)
    {
        string ruta = Carpeta + "/" + nombre + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.shader = shader;
        material.SetColor("_Violeta", new Color(.35f,.04f,1));
        material.SetColor("_Magenta", new Color(1,.03f,.45f));
        material.SetColor("_Naranja", new Color(1,.3f,.02f));
        material.SetFloat("_Intensidad", intensidad);
        material.SetFloat("_Fresnel", 8);
        material.SetFloat("_Velocidad", .7f);
        material.SetFloat("_Modo", modo);
        material.SetFloat("_Opacidad", opacidad);
        material.SetFloat("_SrcBlend", (float)(modo == 0 ? BlendMode.One : BlendMode.SrcAlpha));
        material.SetFloat("_DstBlend", (float)(modo == 0 ? BlendMode.Zero : modo == 1 || modo == 3 ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", modo == 0 ? 1 : 0);
        material.renderQueue = modo == 0 ? (int)RenderQueue.Geometry : (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", modo == 0 ? "Opaque" : "Transparent");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ConfigurarRenderer(Renderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static Mesh CrearAnillo()
    {
        const int segmentos = 128;
        var vertices = new Vector3[(segmentos + 1) * 2];
        var uv = new Vector2[vertices.Length];
        var triangulos = new int[segmentos * 6];
        for (int i = 0; i <= segmentos; i++)
        {
            float angulo = i * Mathf.PI * 2 / segmentos;
            var direccion = new Vector3(Mathf.Cos(angulo), 0, Mathf.Sin(angulo));
            vertices[i*2] = direccion * .65f;
            vertices[i*2+1] = direccion * 1.3f;
            uv[i*2] = new Vector2((float)i/segmentos, 0);
            uv[i*2+1] = new Vector2((float)i/segmentos, 1);
            if (i == segmentos) continue;
            int t = i*6, v = i*2;
            triangulos[t] = v; triangulos[t+1] = v+2; triangulos[t+2] = v+1;
            triangulos[t+3] = v+1; triangulos[t+4] = v+2; triangulos[t+5] = v+3;
        }
        string ruta = Carpeta + "/AgujeroNegro_Anillo.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, ruta); }
        mesh.Clear();
        mesh.name = "AgujeroNegro_Anillo";
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangulos;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static ParticleSystem CrearParticulas(Transform padre, string nombre, Material material)
    {
        var go = new GameObject(nombre, typeof(ParticleSystem));
        go.transform.SetParent(padre, false);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = 256;
        main.startSpeed = 0;
        main.duration = 5;
        ConfigurarRenderer(go.GetComponent<ParticleSystemRenderer>(), material);
        return ps;
    }

    static void CrearAbsorcion(Transform padre, string nombre, Material material, Color color, float tamano, float tasa)
    {
        var ps = CrearParticulas(padre, nombre, material);
        var main = ps.main;
        main.startLifetime = .95f;
        main.startSize = new ParticleSystem.MinMaxCurve(tamano*.6f, tamano*1.4f);
        main.startColor = color;
        var emission = ps.emission; emission.rateOverTime = tasa;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.8f;
        shape.radiusThickness = 0;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = -1.5f;
        velocity.orbitalY = .8f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,1,1,0));
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
            new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.15f), new GradientAlphaKey(0,1) });
        fade.color = gradient;
        ps.Play();
    }

    static void CrearHalo(Transform padre, Material material)
    {
        var ps = CrearParticulas(padre, "HaloOscuro", material);
        var main = ps.main;
        main.startLifetime = 1000;
        main.startSize = 4.2f;
        main.startColor = Color.white;
        main.maxParticles = 1;
        var shape = ps.shape; shape.enabled = false;
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)1) });
        // El billboard permanece centrado: el núcleo oculta su centro y el degradado cubre el entorno.
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        ps.Play();
    }
}
#endif