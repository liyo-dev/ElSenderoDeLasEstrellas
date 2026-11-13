using System.Linq;
using Kits.StylizedVfx;
using Kits.StylizedVfx.Editor;
using UnityEditor;
using UnityEngine;

/// El orbe que el Archimago crea entre las manos en la plegaria del prólogo, y que luego sube sobre
/// su cabeza y crece (lo mueve y lo escala ConjuroEnLasManosBeat). Primera pieza del piloto de VFX
/// «dibujo animado con volumen»: esfera de energía con sombreado de dibujo (brillo blanco hacia la
/// luz, cuerpo de oro, lado en sombra y borde cian) que gira despacio; tres anillos finos que vuelcan
/// alrededor del orbe a distinto ritmo, cada uno con una cuenta de luz que corre por él (se pasan por
/// delante y por detrás: se lee como una esfera, no como un disco); estrellitas 3D que entran en el
/// orbe (se ve que lo está creando) y una luz cálida que late con él. Se rehace en su sitio: el prefab conserva su GUID. Ver INC-1136
public static class OrbeDelArchimago
{
    public const string RutaPrefab = "Assets/_VFX/Prologo/VFX_OrbeProtector.prefab";
    private const string CarpetaMateriales = "Assets/_VFX/Prologo/Materiales";
    private const string RutaAnillo = "Assets/_VFX/Prologo/Mallas/OrbeDelArchimago_Anillo.asset";
    private const string RutaEstrella = "Assets/_VFX/Mallas/VFX_Estrella4.fbx";

    // Colores del Archimago: blanco cálido en el centro, oro en el cuerpo y borde cian.
    private static readonly Color Centro = new Color(2.2f, 2.1f, 1.8f);
    private static readonly Color Oro = new Color(1.5f, 0.9f, 0.18f);
    private static readonly Color Cian = new Color(0.35f, 1.2f, 1.7f);
    private static readonly Color OroEnSombra = new Color(1.0f, 0.45f, 0.1f);

    private const float DiametroDelOrbe = 0.22f;
    private const float RadioDelAnillo = 0.17f;
    private const float GrosorDelAnillo = 0.011f;
    private const float TamDeLaCuenta = 0.035f;

    [MenuItem("El Sendero/Magia/Piloto del prólogo/Orbe del Archimago (INC-1136)")]
    public static void Menu()
    {
        Construir();
        Debug.Log("[VFX] Orbe del Archimago rehecho en " + RutaPrefab);
    }

    public static void Construir()
    {
        var matOrbe = StylizedVfxAssets.EnergyMaterial($"{CarpetaMateriales}/OrbeDelArchimago_Orbe.mat",
            new StylizedVfxAssets.EnergyLook(Centro, Oro, OroEnSombra, Cian, coreBand: 0.6f, shadowBand: -0.2f, rimBand: 0.72f, noiseAmount: 0.12f, noiseScale: 9f));
        var matAnillo = StylizedVfxAssets.EnergyMaterial($"{CarpetaMateriales}/OrbeDelArchimago_Anillo.mat",
            new StylizedVfxAssets.EnergyLook(Centro, Oro, OroEnSombra, Cian, coreBand: 0.75f, shadowBand: -0.3f, rimBand: 1.01f, noiseAmount: 0.03f, noiseScale: 20f));
        var matChispas = StylizedVfxAssets.EnergyMaterial($"{CarpetaMateriales}/OrbeDelArchimago_Chispas.mat",
            new StylizedVfxAssets.EnergyLook(Centro, Oro, Oro, Oro, coreBand: 0.2f, shadowBand: -1f, rimBand: 1.01f, noiseAmount: 0f));
        var anillo = StylizedVfxAssets.Torus(RutaAnillo, RadioDelAnillo, GrosorDelAnillo);
        var estrella = AssetDatabase.LoadAllAssetsAtPath(RutaEstrella).OfType<Mesh>().FirstOrDefault();
        if (matOrbe == null || matAnillo == null || matChispas == null) return;

        var root = new GameObject("VFX_OrbeProtector");
        try
        {
            var giros = new System.Collections.Generic.List<Spin>();
            var renderers = new System.Collections.Generic.List<Renderer>();

            // El orbe gira despacio: el hervor de su superficie corre por ella y se ve que es una bola.
            var orbe = Esfera(root.transform, "Orbe", DiametroDelOrbe, matOrbe);
            giros.Add(new Spin(orbe.transform, new Vector3(0.3f, 1f, 0.2f).normalized, 40f));
            renderers.Add(orbe);

            // Tres anillos: cada uno vuelca alrededor del orbe (giro del eje) y corre sobre sí mismo (la cuenta).
            Anillo(root.transform, "A", 1.00f, 64f, 0f, 55f, 170f, anillo, matAnillo, matChispas, giros, renderers);
            Anillo(root.transform, "B", 1.10f, 58f, 120f, -70f, -150f, anillo, matAnillo, matChispas, giros, renderers);
            Anillo(root.transform, "C", 1.20f, 76f, 240f, 85f, 200f, anillo, matAnillo, matChispas, giros, renderers);

            if (estrella != null) Chispas(root.transform, estrella, matChispas);
            else Debug.LogWarning("[VFX] Falta la malla " + RutaEstrella + ": el orbe queda sin estrellitas.");

            var luz = new GameObject("Luz").AddComponent<Light>();
            luz.transform.SetParent(root.transform, false);
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.93f, 0.8f);
            luz.shadows = LightShadows.None;

            var pulso = root.AddComponent<EnergyPulse>();
            pulso.Configure(renderers.ToArray(), luz, giros.ToArray());
            pulso.ConfigureTiming(intensity: 1.8f, range: 2f, period: 1.4f, depth: 0.35f, appear: 0.4f);

            PrefabUtility.SaveAsPrefabAsset(root, RutaPrefab);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    /// Anillo fino con su cuenta de luz. «Vuelco» gira alrededor del eje vertical del orbe y lleva la
    /// inclinación; el anillo corre sobre su propio eje y arrastra la cuenta, que es lo que deja ver
    /// que gira (un aro liso girando sobre sí mismo parece quieto).
    private static void Anillo(Transform padre, string letra, float escala, float inclinacion, float rumboInicial,
                               float gradosDeVuelco, float gradosDeLaCuenta, Mesh malla, Material mat, Material matCuenta,
                               System.Collections.Generic.List<Spin> giros, System.Collections.Generic.List<Renderer> renderers)
    {
        var vuelco = new GameObject("Vuelco " + letra).transform;
        vuelco.SetParent(padre, false);
        vuelco.localRotation = Quaternion.Euler(0f, rumboInicial, 0f);
        giros.Add(new Spin(vuelco, Vector3.up, gradosDeVuelco));

        var inclinado = new GameObject("Inclinación " + letra).transform;
        inclinado.SetParent(vuelco, false);
        inclinado.localRotation = Quaternion.Euler(inclinacion, 0f, 0f);

        var go = new GameObject("Anillo " + letra);
        go.transform.SetParent(inclinado, false);
        go.transform.localScale = Vector3.one * escala;
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        renderers.Add(SinSombras(go.AddComponent<MeshRenderer>(), mat));
        giros.Add(new Spin(go.transform, Vector3.up, gradosDeLaCuenta));

        var cuenta = Esfera(go.transform, "Cuenta " + letra, TamDeLaCuenta / escala, matCuenta);
        cuenta.transform.localPosition = new Vector3(RadioDelAnillo, 0f, 0f);
        renderers.Add(cuenta);
    }

    private static MeshRenderer Esfera(Transform padre, string nombre, float diametro, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localScale = Vector3.one * diametro;
        return SinSombras(go.GetComponent<MeshRenderer>(), mat);
    }

    /// Estrellitas que vienen de fuera y se meten en el orbe: la luz se está juntando.
    private static void Chispas(Transform padre, Mesh estrella, Material mat)
    {
        var go = new GameObject("Chispas");
        go.transform.SetParent(padre, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float tamDeLaMalla = Mathf.Max(0.001f, estrella.bounds.size.x, estrella.bounds.size.y, estrella.bounds.size.z);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.useUnscaledTime = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.5f;
        main.startSpeed = -0.8f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f / tamDeLaMalla, 0.1f / tamDeLaMalla);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 30;

        var emision = ps.emission;
        emision.rateOverTime = 14f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.42f;
        forma.radiusThickness = 0f;

        // Aparecen de golpe, se mantienen y se encogen al entrar en el orbe.
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0f)));

        var giro = ps.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = estrella;
        r.alignment = ParticleSystemRenderSpace.Local;
        SinSombras(r, mat);
    }

    private static T SinSombras<T>(T r, Material mat) where T : Renderer
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }
}
