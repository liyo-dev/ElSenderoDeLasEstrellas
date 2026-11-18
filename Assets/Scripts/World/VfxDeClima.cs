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
    private static Material _materialRizo;
    private static Material _materialRestos;
    private static Texture2D _punto;
    private static Texture2D _restos;
    private static Texture2D _trazo;
    private static Texture2D _aro;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _material = null; _materialLluvia = null; _materialSalpicadura = null; _materialRizo = null; _materialRestos = null; _restos = null; _punto = null; _trazo = null; _aro = null; }
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

        // Una parte de las gotas choca con el mundo (suelo, tejados, rocas, agua) y, donde choca, salta
        // la salpicadura: un aro plano que se abre y unas gotitas que rebotan. Así salpica en las cuestas
        // y en los tejados, no en un plano a la altura del jugador.
        var queTocan = CrearSistema(go, "Gotas que salpican");
        var m3 = queTocan.main;
        m3.startLifetime = 1.6f;
        m3.startSpeed = 0f;
        m3.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
        m3.startColor = new Color(0.86f, 0.92f, 1f, 0.5f);
        m3.maxParticles = 900;
        m3.gravityModifier = 0.12f;
        var forma3 = queTocan.shape;
        forma3.shapeType = ParticleSystemShapeType.Box;
        forma3.scale = new Vector3(18f, 1f, 18f);
        forma3.position = new Vector3(0f, 9f, 0f);
        var emision3 = queTocan.emission;
        emision3.rateOverTime = 420f;
        var vel3 = queTocan.velocityOverLifetime;
        vel3.enabled = true;
        vel3.space = ParticleSystemSimulationSpace.World;
        vel3.x = new ParticleSystem.MinMaxCurve(-1.2f, 0.2f);
        vel3.y = new ParticleSystem.MinMaxCurve(-18f, -14f);
        vel3.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.3f);
        var choque = queTocan.collision;
        choque.enabled = true;
        choque.type = ParticleSystemCollisionType.World;
        choque.mode = ParticleSystemCollisionMode.Collision3D;
        choque.quality = ParticleSystemCollisionQuality.Medium;
        // Floor para el terreno, Default para props, Obstacle para rocas y árboles, Water para el agua.
        choque.collidesWith = LayerMask.GetMask("Floor", "Default", "Obstacle", "Water");
        choque.lifetimeLoss = 1f;
        choque.radiusScale = 0.1f;
        var r3 = queTocan.GetComponent<ParticleSystemRenderer>();
        r3.renderMode = ParticleSystemRenderMode.Stretch;
        r3.velocityScale = 0.06f;
        r3.lengthScale = 1.0f;
        r3.sharedMaterial = MaterialDeLluvia();
        queTocan.Play();

        var aro = CrearSistema(queTocan.gameObject, "Aro", precalentar: false);
        var ma = aro.main;
        ma.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
        ma.startSpeed = 0f;
        ma.startSize = new ParticleSystem.MinMaxCurve(0.26f, 0.42f);
        ma.startColor = new Color(0.86f, 0.92f, 1f, 0.55f);
        ma.maxParticles = 400;
        var ea = aro.emission;
        ea.rateOverTime = 0f;
        ea.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var formaAro = aro.shape;
        formaAro.enabled = false;
        var crece = aro.sizeOverLifetime;
        crece.enabled = true;
        crece.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f));
        var ra = aro.GetComponent<ParticleSystemRenderer>();
        ra.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        ra.sharedMaterial = MaterialDeSalpicadura();
        AparecerYDesvanecer(aro, 0.05f, 0.5f);

        var gotitas = CrearSistema(queTocan.gameObject, "Gotitas", precalentar: false);
        var mg = gotitas.main;
        mg.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.34f);
        mg.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        mg.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.07f);
        mg.startColor = new Color(0.86f, 0.92f, 1f, 0.7f);
        mg.gravityModifier = 0.9f;
        mg.maxParticles = 1200;
        var eg = gotitas.emission;
        eg.rateOverTime = 0f;
        eg.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 4) });
        var fg = gotitas.shape;
        fg.shapeType = ParticleSystemShapeType.Cone;
        fg.angle = 35f;
        fg.radius = 0.02f;
        fg.rotation = new Vector3(-90f, 0f, 0f);
        AparecerYDesvanecer(gotitas, 0.02f, 0.6f);

        var hijos = queTocan.subEmitters;
        hijos.enabled = true;
        hijos.AddSubEmitter(aro, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
        hijos.AddSubEmitter(gotitas, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);

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

    /// Ricitos de viento (líneas con lazadas que se dibujan) y hojas revoloteando.
    public static GameObject CrearViento(Transform padre, Vector3 direccion)
    {
        direccion.y = 0f;
        direccion = direccion.sqrMagnitude > 0.0001f ? direccion.normalized : Vector3.forward;
        var go = CrearRaiz("[Viento]", padre);

        // Ricitos: cada uno es una cicloide que se dibuja y se desvanece, como el trazo de una
        // racha de aire. Nada de rayas rectas: eso se confundía con la lluvia.
        var rizos = new GameObject("Rizos");
        rizos.transform.SetParent(go.transform, false);
        var viento = rizos.AddComponent<VientoDeRizos>();
        viento.Configurar(direccion);

        // Restos que arrastra el aire: hojas, pétalos, briznas de paja y terrones, de dibujo (color plano
        // y la misma tinta que el contorno). Giran y se dan la vuelta (se estrechan y se ensanchan) mientras vuelan.
        var restos = CrearSistema(go, "Restos");
        var m2 = restos.main;
        m2.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        m2.startSpeed = 0f;
        m2.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
        m2.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m2.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.86f, 0.9f, 0.82f));
        m2.maxParticles = 700;
        var s2 = restos.shape;
        s2.shapeType = ParticleSystemShapeType.Box;
        s2.scale = new Vector3(24f, 3f, 24f);
        s2.position = new Vector3(0f, 1.2f, 0f);
        var e2 = restos.emission;
        e2.rateOverTime = 90f;
        Velocidad(restos, direccion * 7f, 2f);
        var rot = restos.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var vuelta = restos.sizeOverLifetime;
        vuelta.enabled = true;
        vuelta.separateAxes = true;
        vuelta.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        vuelta.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.2f, 0.3f), new Keyframe(0.4f, 1f),
            new Keyframe(0.6f, 0.3f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0.3f)));
        var hoja = restos.textureSheetAnimation;
        hoja.enabled = true;
        hoja.numTilesX = TiposDeResto;
        hoja.numTilesY = 1;
        hoja.animation = ParticleSystemAnimationType.WholeSheet;
        hoja.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        hoja.startFrame = new ParticleSystem.MinMaxCurve(0f, TiposDeResto - 0.01f);
        var ruido = restos.noise;
        ruido.enabled = true;
        ruido.strength = 1.6f;
        ruido.frequency = 0.6f;
        ruido.scrollSpeed = 0.8f;
        restos.GetComponent<ParticleSystemRenderer>().sharedMaterial = MaterialDeRestos();
        AparecerYDesvanecer(restos, 0.1f, 0.8f);
        restos.Play();

        return go;
    }

    /// Tiñe las partículas con la luz del momento: sin esto, de noche brillan como si fuera de
    /// día (el material no recibe luz).
    public static void Iluminar(Color luz)
    {
        if (_material != null) _material.color = luz;
        if (_materialLluvia != null) _materialLluvia.color = Color.Lerp(luz, Color.white, 0.3f);
        if (_materialSalpicadura != null) _materialSalpicadura.color = Color.Lerp(luz, Color.white, 0.3f);
        if (_materialRizo != null) _materialRizo.color = Color.Lerp(luz, Color.white, 0.45f);
        if (_materialRestos != null) _materialRestos.color = Color.Lerp(luz, Color.white, 0.35f);
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

    public static Material MaterialDeRizo()
    {
        if (_materialRizo != null) return _materialRizo;
        _materialRizo = new Material(Shader.Find("Sprites/Default")) { name = "[VfxDeClima] Rizo" };
        return _materialRizo;
    }

    private static Material MaterialDeRestos()
    {
        if (_materialRestos != null) return _materialRestos;
        _materialRestos = new Material(Shader.Find("Sprites/Default")) { name = "[VfxDeClima] Restos" };
        _materialRestos.mainTexture = Restos();
        return _materialRestos;
    }

    private const int TiposDeResto = 4;
    private const int LadoDeResto = 32;
    private static readonly Color Tinta = new Color(0.20f, 0.15f, 0.23f);

    /// Hoja, pétalo, brizna de paja y terrón, en fila. Cada uno es una forma con relleno plano, una
    /// banda de sombra con color y el borde de tinta: el mismo dibujo que los VFX de hechizos.
    private static Texture2D Restos()
    {
        if (_restos != null) return _restos;
        _restos = new Texture2D(LadoDeResto * TiposDeResto, LadoDeResto, TextureFormat.RGBA32, false)
        {
            name = "[VfxDeClima] Restos",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[LadoDeResto * TiposDeResto * LadoDeResto];
        var relleno = new[] { new Color(0.42f, 0.66f, 0.22f), new Color(1f, 0.74f, 0.82f), new Color(0.94f, 0.80f, 0.38f), new Color(0.52f, 0.36f, 0.22f) };
        for (int tipo = 0; tipo < TiposDeResto; tipo++)
            for (int y = 0; y < LadoDeResto; y++)
                for (int x = 0; x < LadoDeResto; x++)
                {
                    // Coordenadas -1..1 dentro de la casilla.
                    float u = (x + 0.5f) / LadoDeResto * 2f - 1f, v = (y + 0.5f) / LadoDeResto * 2f - 1f;
                    float d = DistanciaDeResto(tipo, u, v);
                    float pixel = 2f / LadoDeResto;
                    float dentro = Mathf.Clamp01(-d / pixel + 0.5f);
                    float tinta = Mathf.Clamp01((d + 2.2f * pixel) / pixel);
                    Color c = relleno[tipo];
                    if (v < -0.15f + 0.25f * u) c *= 0.78f;                  // sombra con color, borde duro
                    if (tipo == 0 && Mathf.Abs(u) < pixel * 0.7f && v > -0.7f) c = Color.Lerp(c, Tinta, 0.5f); // nervio
                    c = Color.Lerp(c, Tinta, tinta);
                    c.a = dentro;
                    px[y * LadoDeResto * TiposDeResto + tipo * LadoDeResto + x] = c;
                }
        _restos.SetPixels32(px);
        _restos.Apply(false, true);
        return _restos;
    }

    // Distancia con signo (negativa dentro) de cada forma, en unidades de la casilla (-1..1).
    private static float DistanciaDeResto(int tipo, float u, float v)
    {
        switch (tipo)
        {
            case 0: // hoja: dos arcos que se cortan, con punta arriba y abajo
                return Mathf.Max(new Vector2(u - 0.55f, v).magnitude - 1.15f, new Vector2(u + 0.55f, v).magnitude - 1.15f) * 0.8f;
            case 1: // pétalo: gota redondeada
                return new Vector2(u * 1.25f, (v + 0.1f) * 0.95f).magnitude - 0.62f + Mathf.Max(0f, v) * 0.35f;
            case 2: // brizna de paja: palito fino e inclinado
            {
                Vector2 p = new Vector2(u * 0.8f + v * 0.6f, -u * 0.6f + v * 0.8f);
                return Mathf.Max(Mathf.Abs(p.x) - 0.12f, Mathf.Abs(p.y) - 0.9f);
            }
            default: // terrón: canto rodado irregular
            {
                float a = Mathf.Atan2(v, u);
                return new Vector2(u, v).magnitude - (0.5f + 0.08f * Mathf.Sin(a * 3f) + 0.05f * Mathf.Sin(a * 5f + 1f));
            }
        }
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
}
