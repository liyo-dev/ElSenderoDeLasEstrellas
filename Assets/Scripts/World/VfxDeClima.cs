using UnityEngine;

/// Partículas de clima construidas en código para cuando la escena no asigna prefab propio
/// (DayNightCycle.snowPrefab / windPrefab vacíos): nieve cayendo y viento (rachas + hojas).
/// Viven colgadas del ancla del clima igual que la lluvia, pero simulan en espacio de mundo, así
/// que lo ya emitido no se mueve con el jugador. Material Sprites/Default (siempre incluido en la
/// build) con una textura de punto difuminado generada aquí.
public static class VfxDeClima
{
    private static Material _material;
    private static Material _materialLluvia;
    private static Material _materialSalpicadura;
    private static Material _materialViento;
    private static Texture2D _punto;
    private static Texture2D _trazo;
    private static Texture2D _aro;
    private static Texture2D _onda;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _material = null; _materialLluvia = null; _materialSalpicadura = null; _materialViento = null; _punto = null; _trazo = null; _aro = null; _onda = null; }
#endif

    /// Lluvia de dibujo: trazos que caen en una caja alrededor del ancla y aros de salpicadura
    /// planos en el suelo. Nada de texturas de gota real ni de anillos con refracción.
    public static GameObject CrearLluvia(Transform padre)
    {
        var go = CrearRaiz("[Lluvia]", padre);

        var gotas = CrearSistema(go, "Gotas");
        var m = gotas.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.0f);
        // Sin velocidad inicial: en una caja, startSpeed sale hacia un lado y la lluvia caía de lado.
        m.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
        m.startColor = new Color(0.86f, 0.92f, 1f, 0.5f);
        m.maxParticles = 3200;
        m.gravityModifier = 0.12f;
        var forma = gotas.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(26f, 1f, 26f);
        forma.position = new Vector3(0f, 9f, 0f);
        var emision = gotas.emission;
        emision.rateOverTime = 1400f;
        var vel = gotas.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-1.2f, 0.2f);
        vel.y = new ParticleSystem.MinMaxCurve(-18f, -14f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.3f);
        var r = gotas.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.06f;
        r.lengthScale = 1.0f;
        r.sharedMaterial = MaterialDeLluvia();
        AparecerYDesvanecer(gotas, 0.05f, 0.9f);
        gotas.Play();

        // Aros planos a ras de suelo: la salpicadura se lee desde arriba y no parece una burbuja.
        var salpicaduras = CrearSistema(go, "Salpicaduras");
        var m2 = salpicaduras.main;
        m2.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.34f);
        m2.startSpeed = 0f;
        m2.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.20f);
        m2.startColor = new Color(0.86f, 0.92f, 1f, 0.45f);
        m2.maxParticles = 420;
        var forma2 = salpicaduras.shape;
        forma2.shapeType = ParticleSystemShapeType.Box;
        forma2.scale = new Vector3(18f, 0.04f, 18f);
        forma2.position = new Vector3(0f, 0.08f, 0f);
        var emision2 = salpicaduras.emission;
        emision2.rateOverTime = 150f;
        var r2 = salpicaduras.GetComponent<ParticleSystemRenderer>();
        r2.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        r2.sharedMaterial = MaterialDeSalpicadura();
        AparecerYDesvanecer(salpicaduras, 0.08f, 0.75f);
        salpicaduras.Play();

        var alSuelo = go.AddComponent<SalpicadurasDeLluvia>();
        alSuelo.Configurar(salpicaduras.transform);

        return go;
    }

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

        // Rachas: líneas onduladas, largas y tenues. Distintas de la lluvia a propósito: la lluvia
        // cae en trazos rectos y verticales; el viento cruza en ondas horizontales.
        var rachas = CrearSistema(go, "Rachas");
        var m1 = rachas.main;
        m1.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
        m1.startSpeed = 0f;
        m1.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
        m1.startColor = new Color(1f, 0.98f, 0.9f, 0.6f);
        m1.maxParticles = 320;
        var s1 = rachas.shape;
        s1.shapeType = ParticleSystemShapeType.Box;
        s1.scale = new Vector3(40f, 6f, 40f);
        s1.position = new Vector3(0f, 2.5f, 0f);
        var e1 = rachas.emission;
        e1.rateOverTime = 60f;
        Velocidad(rachas, direccion * 9f, 1.0f);
        var r1 = rachas.GetComponent<ParticleSystemRenderer>();
        r1.renderMode = ParticleSystemRenderMode.Stretch;
        r1.velocityScale = 0.10f;
        r1.lengthScale = 6f;
        r1.sharedMaterial = MaterialDelViento();
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
        if (_materialLluvia != null) _materialLluvia.color = Color.Lerp(luz, Color.white, 0.3f);
        if (_materialSalpicadura != null) _materialSalpicadura.color = Color.Lerp(luz, Color.white, 0.3f);
        if (_materialViento != null) _materialViento.color = Color.Lerp(luz, Color.white, 0.45f);
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

    private static Material MaterialDeLluvia()
    {
        if (_materialLluvia != null) return _materialLluvia;
        _materialLluvia = new Material(Shader.Find("Sprites/Default")) { name = "[VfxDeClima] Lluvia" };
        _materialLluvia.mainTexture = Trazo();
        return _materialLluvia;
    }

    private static Material MaterialDeSalpicadura()
    {
        if (_materialSalpicadura != null) return _materialSalpicadura;
        _materialSalpicadura = new Material(Shader.Find("Sprites/Default")) { name = "[VfxDeClima] Salpicadura" };
        _materialSalpicadura.mainTexture = Aro();
        return _materialSalpicadura;
    }

    private static Material MaterialDelViento()
    {
        if (_materialViento != null) return _materialViento;
        _materialViento = new Material(Shader.Find("Sprites/Default")) { name = "[VfxDeClima] Viento" };
        _materialViento.mainTexture = Onda();
        return _materialViento;
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
                // Borde duro (1 px de antialias): un copo de dibujo, no una mota difuminada.
                byte a = (byte)(Mathf.Clamp01((0.9f - d) * 9f) * 255f);
                px[y * lado + x] = new Color32(255, 255, 255, a);
            }
        _punto.SetPixels32(px);
        _punto.Apply(false, true);
        return _punto;
    }

    private static Texture2D Trazo()
    {
        if (_trazo != null) return _trazo;
        const int ancho = 8, alto = 64;
        _trazo = new Texture2D(ancho, alto, TextureFormat.RGBA32, false)
        {
            name = "[VfxDeClima] Trazo",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[ancho * alto];
        for (int y = 0; y < alto; y++)
        {
            float t = y / (alto - 1f);                       // 0 arriba, 1 en la punta
            float radio = Mathf.Lerp(0.46f, 0.14f, t);       // se afila hacia abajo
            for (int x = 0; x < ancho; x++)
            {
                float d = Mathf.Abs((x + 0.5f) / ancho - 0.5f);
                float a = Mathf.Clamp01((radio - d) * ancho * 0.9f);
                a *= Mathf.Clamp01((1f - t) * 10f);
                px[y * ancho + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        _trazo.SetPixels32(px);
        _trazo.Apply(false, true);
        return _trazo;
    }

    private static Texture2D Aro()
    {
        if (_aro != null) return _aro;
        const int lado = 32;
        _aro = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            name = "[VfxDeClima] Aro",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[lado * lado];
        float centro = (lado - 1) * 0.5f;
        for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float d = Mathf.Sqrt((x - centro) * (x - centro) + (y - centro) * (y - centro)) / centro;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.66f, d));
                a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.80f, 0.96f, d));
                px[y * lado + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        _aro.SetPixels32(px);
        _aro.Apply(false, true);
        return _aro;
    }

    /// Trazo ondulado del viento: una línea que serpentea a lo largo de la textura. Al estirarla
    /// con la velocidad, sale una racha curva; la lluvia, en cambio, es un trazo recto y vertical.
    private static Texture2D Onda()
    {
        if (_onda != null) return _onda;
        const int ancho = 16, alto = 64;
        _onda = new Texture2D(ancho, alto, TextureFormat.RGBA32, false)
        {
            name = "[VfxDeClima] Onda",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[ancho * alto];
        for (int y = 0; y < alto; y++)
        {
            float t = y / (alto - 1f);
            float centro = 0.5f + Mathf.Sin(t * Mathf.PI * 3f) * 0.30f;
            for (int x = 0; x < ancho; x++)
            {
                float d = Mathf.Abs((x + 0.5f) / ancho - centro);
                float a = Mathf.Clamp01((0.11f - d) * ancho * 1.3f);
                a *= Mathf.Clamp01(t * 10f) * Mathf.Clamp01((1f - t) * 10f);
                px[y * ancho + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        _onda.SetPixels32(px);
        _onda.Apply(false, true);
        return _onda;
    }
}
