using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Salpicaduras de agua hechas por código: gotas (corona y columna), ondas planas sobre la
/// superficie y burbujas. Es un emisor reutilizable: cada llamada emite partículas en el punto
/// pedido (simulación en espacio de mundo), sin instanciar ni destruir nada. Texturas y
/// materiales se generan una vez y se comparten. Ver INC-512.
/// </summary>
[DisallowMultipleComponent]
public sealed class SalpicaduraDeAgua : MonoBehaviour
{
    [SerializeField, Tooltip("Color de las gotas.")]
    private Color colorGotas = new Color(0.86f, 0.95f, 1f, 0.95f);
    [SerializeField, Tooltip("Color de las ondas de la superficie.")]
    private Color colorOndas = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField, Tooltip("Color de las burbujas.")]
    private Color colorBurbujas = new Color(0.9f, 0.97f, 1f, 0.8f);

    private ParticleSystem _gotas;
    private ParticleSystem _ondas;
    private ParticleSystem _burbujas;

    private static Material _matPunto;
    private static Material _matAnillo;

    void Awake()
    {
        _gotas = CrearSistema("Gotas", MaterialPunto(), ParticleSystemRenderMode.Billboard, 1.6f, 400);
        ConfigurarDesvanecido(_gotas, tamanoFinal: 0.5f);

        _ondas = CrearSistema("Ondas", MaterialAnillo(), ParticleSystemRenderMode.HorizontalBillboard, 0f, 60);
        ConfigurarOnda(_ondas);

        _burbujas = CrearSistema("Burbujas", MaterialPunto(), ParticleSystemRenderMode.Billboard, -0.15f, 120);
        ConfigurarDesvanecido(_burbujas, tamanoFinal: 1.2f);
    }

    /// <summary>Salpicadura completa al entrar en el agua. <paramref name="fuerza"/> va de 0 (entrar andando) a 1 (caída desde muy alto).</summary>
    public void Salpicar(Vector3 superficie, float fuerza)
    {
        fuerza = Mathf.Clamp01(fuerza);

        Onda(superficie, Mathf.Lerp(0.7f, 2.2f, fuerza));
        if (fuerza > 0.3f)
            Onda(superficie, Mathf.Lerp(0.5f, 1.3f, fuerza), vida: 1.4f);

        var ep = new ParticleSystem.EmitParams { startColor = colorGotas };
        float escala = 0.6f + fuerza;

        // Corona: gotas que salen en abanico desde el borde del hueco.
        int corona = Mathf.RoundToInt(Mathf.Lerp(8f, 36f, fuerza));
        for (int i = 0; i < corona; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            ep.position = superficie + dir * Random.Range(0.08f, 0.22f) * escala + Vector3.up * 0.03f;
            ep.velocity = dir * Random.Range(0.6f, 1.8f) * escala + Vector3.up * Random.Range(1.4f, 3.2f) * escala;
            ep.startSize = Random.Range(0.05f, 0.12f) * (0.8f + fuerza * 0.6f);
            ep.startLifetime = Random.Range(0.45f, 0.85f);
            _gotas.Emit(ep, 1);
        }

        // Columna: solo en caídas con fuerza, un chorro que sube por el centro y vuelve a caer.
        int columna = Mathf.RoundToInt(Mathf.Lerp(0f, 18f, Mathf.InverseLerp(0.25f, 1f, fuerza)));
        for (int i = 0; i < columna; i++)
        {
            var lateral = Random.insideUnitCircle * 0.35f;
            ep.position = superficie + new Vector3(lateral.x * 0.3f, 0.05f, lateral.y * 0.3f);
            ep.velocity = new Vector3(lateral.x, Random.Range(3f, 5.5f) * fuerza, lateral.y);
            ep.startSize = Random.Range(0.08f, 0.18f);
            ep.startLifetime = Random.Range(0.6f, 1f);
            _gotas.Emit(ep, 1);
        }
    }

    /// <summary>Onda que se abre sobre la superficie. <paramref name="tamano"/> es el diámetro final en metros.</summary>
    public void Onda(Vector3 superficie, float tamano, float vida = 0.9f)
    {
        var ep = new ParticleSystem.EmitParams
        {
            position = superficie + Vector3.up * 0.02f,
            velocity = Vector3.zero,
            startSize = tamano,
            startLifetime = vida * Random.Range(0.9f, 1.1f),
            rotation = Random.Range(0f, 360f),
            startColor = colorOndas,
        };
        _ondas.Emit(ep, 1);
    }

    /// <summary>Unas pocas gotas pequeñas, para chapoteo al nadar o al salir a flote.</summary>
    public void Gotitas(Vector3 superficie, int cantidad)
    {
        var ep = new ParticleSystem.EmitParams { startColor = colorGotas };
        for (int i = 0; i < cantidad; i++)
        {
            var lateral = Random.insideUnitCircle;
            ep.position = superficie + new Vector3(lateral.x * 0.15f, 0.03f, lateral.y * 0.15f);
            ep.velocity = new Vector3(lateral.x * 0.6f, Random.Range(0.9f, 1.8f), lateral.y * 0.6f);
            ep.startSize = Random.Range(0.035f, 0.07f);
            ep.startLifetime = Random.Range(0.3f, 0.5f);
            _gotas.Emit(ep, 1);
        }
    }

    /// <summary>Burbujas que suben desde <paramref name="centro"/> (bajo el agua).</summary>
    public void Burbujas(Vector3 centro, float radio, int cantidad)
    {
        var ep = new ParticleSystem.EmitParams { startColor = colorBurbujas };
        for (int i = 0; i < cantidad; i++)
        {
            ep.position = centro + Random.insideUnitSphere * radio;
            ep.velocity = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.6f, 1.3f), Random.Range(-0.15f, 0.15f));
            ep.startSize = Random.Range(0.03f, 0.08f);
            ep.startLifetime = Random.Range(0.5f, 1.1f);
            _burbujas.Emit(ep, 1);
        }
    }

    // ── Montaje ──────────────────────────────────────────────────────────

    ParticleSystem CrearSistema(string nombre, Material material, ParticleSystemRenderMode modo, float gravedad, int maximo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = maximo;
        main.gravityModifier = gravedad;
        main.startSpeed = 0f;

        var emision = ps.emission;
        emision.enabled = false;
        var forma = ps.shape;
        forma.enabled = false;

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.renderMode = modo;
        render.sharedMaterial = material;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.alignment = modo == ParticleSystemRenderMode.HorizontalBillboard
            ? ParticleSystemRenderSpace.World
            : ParticleSystemRenderSpace.View;

        ps.Play();
        return ps;
    }

    static void ConfigurarDesvanecido(ParticleSystem ps, float tamanoFinal)
    {
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, tamanoFinal));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = g;
    }

    static void ConfigurarOnda(ParticleSystem ps)
    {
        // Se abre rápido al principio y frena, como una onda de verdad.
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        var curva = new AnimationCurve(new Keyframe(0f, 0.2f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));
        tam.size = new ParticleSystem.MinMaxCurve(1f, curva);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = g;
    }

    // ── Texturas y materiales compartidos ────────────────────────────────

    static Material MaterialPunto()
    {
        if (_matPunto == null)
            _matPunto = CrearMaterial("Salpicadura (punto)", CrearTextura(64, r => 1f - Mathf.SmoothStep(0.55f, 1f, r)));
        return _matPunto;
    }

    static Material MaterialAnillo()
    {
        if (_matAnillo == null)
        {
            _matAnillo = CrearMaterial("Salpicadura (anillo)", CrearTextura(128, r =>
            {
                float anillo = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.07f, 2f));
                float interior = r < 0.82f ? 0.12f * r : 0f;
                return Mathf.Clamp01(anillo + interior);
            }));
        }
        return _matAnillo;
    }

    // r = distancia al centro normalizada (0 centro, 1 borde).
    static Texture2D CrearTextura(int lado, System.Func<float, float> alfa)
    {
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            name = "Salpicadura",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[lado * lado];
        float mitad = (lado - 1) * 0.5f;
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float dx = (x - mitad) / mitad;
                float dy = (y - mitad) / mitad;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)Mathf.RoundToInt(255f * (r > 1f ? 0f : alfa(r)));
                px[y * lado + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    static Material CrearMaterial(string nombre, Texture2D textura)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = nombre, hideFlags = HideFlags.DontSave };
        m.mainTexture = textura;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", textura);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);

        // Transparencia alfa normal (URP necesita propiedades, palabra clave y cola).
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }
}
