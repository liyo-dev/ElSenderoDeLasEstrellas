using UnityEngine;

/// Partículas de clima construidas en código para cuando la escena no asigna prefab propio
/// (DayNightCycle.snowPrefab / windPrefab vacíos): nieve cayendo y viento (rachas + hojas).
/// Viven colgadas del ancla del clima igual que la lluvia, pero simulan en espacio de mundo, así
/// que lo ya emitido no se mueve con el jugador. Material Sprites/Default (siempre incluido en la
/// build) con una textura de punto difuminado generada aquí.
public static class VfxDeClima
{
    private static Material _material;
    private static Texture2D _punto;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _material = null; _punto = null; }
#endif

    /// Copos cayendo despacio en una caja de 44 × 44 m sobre el ancla.
    public static GameObject CrearNieve(Transform padre)
    {
        var go = CrearRaiz("[Nieve]", padre);
        // Sin precalentar: la primera nevada tarda unos segundos en llegar al suelo, como de verdad.
        var ps = CrearSistema(go, "Copos", precalentar: false);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(11f, 14f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.startColor = new Color(1f, 1f, 1f, 0.9f);
        main.maxParticles = 3500;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(44f, 1f, 44f);
        shape.position = new Vector3(0f, 14f, 0f);

        var emision = ps.emission;
        emision.rateOverTime = 260f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        // Las tres curvas en el mismo modo (dos constantes): Unity no admite mezclarlos.
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.4f, -0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.5f;
        ruido.frequency = 0.35f;
        ruido.scrollSpeed = 0.2f;

        AparecerYDesvanecer(ps, 0.08f, 0.85f);
        ps.Play();
        return go;
    }

    /// Rachas (trazos estirados que cruzan rápido) y hojas revoloteando, en la dirección del viento.
    public static GameObject CrearViento(Transform padre, Vector3 direccion)
    {
        direccion.y = 0f;
        direccion = direccion.sqrMagnitude > 0.0001f ? direccion.normalized : Vector3.forward;
        var go = CrearRaiz("[Viento]", padre);

        // Rachas: trazos blancos, finos y casi transparentes.
        var rachas = CrearSistema(go, "Rachas");
        var m1 = rachas.main;
        m1.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        m1.startSpeed = 0f;
        m1.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        m1.startColor = new Color(1f, 1f, 1f, 0.22f);
        m1.maxParticles = 400;
        var s1 = rachas.shape;
        s1.shapeType = ParticleSystemShapeType.Box;
        s1.scale = new Vector3(40f, 6f, 40f);
        s1.position = new Vector3(0f, 2.5f, 0f);
        var e1 = rachas.emission;
        e1.rateOverTime = 70f;
        Velocidad(rachas, direccion * 16f, 1.5f);
        var r1 = rachas.GetComponent<ParticleSystemRenderer>();
        r1.renderMode = ParticleSystemRenderMode.Stretch;
        r1.velocityScale = 0.18f;
        r1.lengthScale = 4f;
        AparecerYDesvanecer(rachas, 0.25f, 0.6f);
        rachas.Play();

        // Hojas: motas pequeñas de tonos otoñales y verdes, girando y con turbulencia.
        var hojas = CrearSistema(go, "Hojas");
        var m2 = hojas.main;
        m2.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        m2.startSpeed = 0f;
        m2.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
        m2.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m2.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.42f, 0.16f), new Color(0.36f, 0.5f, 0.2f));
        m2.maxParticles = 300;
        var s2 = hojas.shape;
        s2.shapeType = ParticleSystemShapeType.Box;
        s2.scale = new Vector3(30f, 3f, 30f);
        s2.position = new Vector3(0f, 1.2f, 0f);
        var e2 = hojas.emission;
        e2.rateOverTime = 22f;
        Velocidad(hojas, direccion * 7f, 2f);
        var rot = hojas.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var ruido = hojas.noise;
        ruido.enabled = true;
        ruido.strength = 1.6f;
        ruido.frequency = 0.6f;
        ruido.scrollSpeed = 0.8f;
        AparecerYDesvanecer(hojas, 0.1f, 0.8f);
        hojas.Play();

        return go;
    }

    /// Tiñe las partículas con la luz del momento: sin esto, de noche brillan como si fuera de
    /// día (el material no recibe luz).
    public static void Iluminar(Color luz)
    {
        if (_material != null) _material.color = luz;
    }

    private static GameObject CrearRaiz(string nombre, Transform padre)
    {
        var go = new GameObject(nombre);
        if (padre != null)
        {
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
        }
        return go;
    }

    private static ParticleSystem CrearSistema(GameObject raiz, string nombre, bool precalentar = true)
    {
        var go = new GameObject(nombre);
        // Gira con el jugador sin que se note: las cajas son cuadradas y la velocidad va en mundo.
        go.transform.SetParent(raiz.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = precalentar;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = MaterialCompartido();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    private static void Velocidad(ParticleSystem ps, Vector3 v, float variacion)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(v.x - variacion, v.x + variacion);
        vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.z = new ParticleSystem.MinMaxCurve(v.z - variacion, v.z + variacion);
    }

    private static void AparecerYDesvanecer(ParticleSystem ps, float entrada, float salida)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, entrada),
                    new GradientAlphaKey(1f, salida), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static Material MaterialCompartido()
    {
        if (_material != null) return _material;
        _material = new Material(Shader.Find("Sprites/Default")) { name = "[VfxDeClima]" };
        _material.mainTexture = Punto();
        return _material;
    }

    private static Texture2D Punto()
    {
        if (_punto != null) return _punto;
        const int lado = 32;
        _punto = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            name = "[VfxDeClima] Punto",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[lado * lado];
        float centro = (lado - 1) * 0.5f;
        for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float d = Mathf.Sqrt((x - centro) * (x - centro) + (y - centro) * (y - centro)) / centro;
                byte a = (byte)(Mathf.Clamp01(1f - d * d) * 255f);
                px[y * lado + x] = new Color32(255, 255, 255, a);
            }
        _punto.SetPixels32(px);
        _punto.Apply(false, true);
        return _punto;
    }
}
