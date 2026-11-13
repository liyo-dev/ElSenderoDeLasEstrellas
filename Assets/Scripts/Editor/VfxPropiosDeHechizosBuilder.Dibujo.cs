using UnityEditor;
using UnityEngine;
using static VfxProcedural;

/// Efectos de hechizo con el aspecto de dibujo animado: piezas de color plano con contorno de tinta
/// (llamas, estrellas, esquirlas, remolinos…) que aparecen, crecen y se encogen de golpe, en vez de
/// brillos aditivos. Las piezas son PNG en Texturas/Dibujo (dibujadas fuera, con su color). Aquí están
/// las ayudas; los hechizos del grupo los monta HechizosConVolumen. Ver INC-1127 e INC-1140
public static partial class VfxPropiosDeHechizosBuilder
{
    public const string LlamaAstralDibujo = "LlamaAstral_Dibujo";
    public const string LlamaAstralDibujoImpacto = "LlamaAstral_Dibujo_Impacto";
    public const string BolaPrismaDibujo = "BolaPrisma_Dibujo";
    public const string BolaPrismaDibujoImpacto = "BolaPrisma_Dibujo_Impacto";
    public const string RafagaDibujo = "Rafaga_Dibujo";
    public const string RafagaDibujoImpacto = "Rafaga_Dibujo_Impacto";

    private const string CarpetaDibujo = CarpetaTexturas + "/Dibujo";
    private static readonly string[] PiezasDeDibujo =
        { "llama", "estrella", "destello", "anillo", "petalo", "humo", "orbe", "esquirla", "remolino", "linea_viento", "hoja",
          "eslabon", "runa", "haz", "garra", "dardo", "corazon", "cupula", "roca", "nube", "embudo", "espiral", "grieta", "sol", "burbuja", "piedra", "exclamacion" };

    private static void RegistrarPiezas(Texturas t)
    {
        foreach (var pieza in PiezasDeDibujo)
        {
            string ruta = $"{CarpetaDibujo}/T_Dibujo_{pieza}.png";
            t.Registrar("Dibujo_" + pieza, _ => ImportarPieza(ruta));
        }
    }

    private static void ConstruirDibujo(Texturas t, System.Text.StringBuilder log)
    {
        RegistrarPiezas(t);
        // Los hechizos del grupo los monta HechizosConVolumen (INC-1140); aquí quedan los de combate.
        ConstruirDibujoDeCombate(t, log);
    }

    private static Texture2D ImportarPieza(string ruta)
    {
        if (AssetImporter.GetAtPath(ruta) is TextureImporter imp &&
            (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || imp.textureCompression != TextureImporterCompression.CompressedHQ))
        {
            imp.textureType = TextureImporterType.Default;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.sRGBTexture = true;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (tex == null) Debug.LogWarning("[VFX] Falta la pieza de dibujo " + ruta);
        return tex;
    }

    // ── Movimiento con aire de dibujo ────────────────────────────────────

    /// Una pieza que sale de la nada con un pequeño rebote, se queda y se encoge de golpe (sin fundido).
    private static void Golpe(ParticleSystem ps, float creceHasta, float encogeDesde)
    {
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        var curva = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(creceHasta, 1.15f), new Keyframe(creceHasta + 0.08f, 1f),
                                       new Keyframe(encogeDesde, 1f), new Keyframe(1f, 0f));
        tam.size = new ParticleSystem.MinMaxCurve(1f, curva);
    }

    private static void GiroAlAzar(ParticleSystem ps, float grados)
    {
        var main = ps.main;
        main.startRotation = new ParticleSystem.MinMaxCurve(-grados * Mathf.Deg2Rad, grados * Mathf.Deg2Rad);
    }

    private static ParticleSystem Pieza(Transform padre, Texturas t, string nombre, string pieza, float duracion = 1f, bool bucle = true)
        => PiezasEn3D.ContainsKey(pieza)
            ? Malla3D(padre, nombre, pieza, duracion, bucle)
            : Sistema(padre, nombre, t.Mat("Dibujo_" + pieza, false), ParticleSystemRenderMode.Billboard, duracion, bucle);

    /// Estallido radial de piezas alargadas hacia fuera (pétalos, esquirlas).
    private static ParticleSystem Estallido(Transform padre, Texturas t, string nombre, string pieza, int cuantas, float velocidad, float tam, float vida)
    {
        bool tresDe = PiezasEn3D.ContainsKey(pieza);
        var ps = tresDe ? Malla3D(padre, nombre, pieza, vida, false)
                        : Sistema(padre, nombre, t.Mat("Dibujo_" + pieza, false), ParticleSystemRenderMode.Stretch, vida, false);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.8f, vida);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velocidad * 0.7f, velocidad);
        main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.7f, tam);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Rafaga(ps, cuantas);
        Forma(ps, ParticleSystemShapeType.Sphere, 0.15f, 1f);
        if (!tresDe)
        {
            var render = ps.GetComponent<ParticleSystemRenderer>();
            render.lengthScale = 1.6f;
            render.velocityScale = 0.04f;
        }
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = 0.5f;
        lim.dampen = 0.25f;
        Golpe(ps, 0.12f, 0.55f);
        return ps;
    }

    /// Una sola pieza que aparece y crece (anillo de onda, destello).
    private static ParticleSystem Pop(Transform padre, Texturas t, string nombre, string pieza, float tam, float vida, float creceA)
    {
        var ps = Pieza(padre, t, nombre, pieza, vida, false);
        var main = ps.main;
        main.startLifetime = vida;
        main.startSize = tam;
        main.maxParticles = 1;
        Rafaga(ps, 1);
        var tamC = ps.sizeOverLifetime;
        tamC.enabled = true;
        tamC.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.5f, creceA), new Keyframe(1f, creceA * 1.1f)));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        if (pieza == "destello" || pieza == "sol") Resplandor(ps, t, tam * 1.7f, vida, false);
        return ps;
    }

    /// Resplandor suave (aditivo) detrás de una pieza que es luz: hace que la magia brille y la
    /// coja el bloom como a los faroles, sin cambiar el dibujo. Va de hijo y se tiñe con ella.
    private static ParticleSystem Resplandor(ParticleSystem de, Texturas t, float tam, float vida, bool bucle)
    {
        var ps = Sistema(de.transform, NombreDelResplandor, t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, vida, bucle);
        var m = ps.main;
        m.startLifetime = vida;
        m.startSize = tam;
        m.startColor = ColorDelResplandor(de.main.startColor.color);
        m.maxParticles = 2;
        m.simulationSpace = de.main.simulationSpace;
        Rafaga(ps, 1);
        if (bucle) Emitir(ps, 1f / vida);
        Desvanecer(ps, 0.1f, 0.6f);
        ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 4f;
        return ps;
    }

    private const string NombreDelResplandor = "Resplandor";
    private static Color ColorDelResplandor(Color c) => new Color(c.r, c.g, c.b, 0.4f);

    /// Bocanadas de humo de dibujo que suben y se encogen.
    private static ParticleSystem Bocanadas(Transform padre, Texturas t, string nombre, int cuantas, float tam, float vida)
    {
        var ps = Pieza(padre, t, nombre, "humo", vida, false);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.7f, vida);
        main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.6f, tam);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Rafaga(ps, cuantas);
        Forma(ps, ParticleSystemShapeType.Hemisphere, 0.3f, 1f);
        GiroAlAzar(ps, 15f);
        Golpe(ps, 0.15f, 0.5f);
        return ps;
    }

    /// Estrellitas que saltan y giran.
    private static ParticleSystem Estrellitas(Transform padre, Texturas t, string nombre, int cuantas, float tam, float velocidad, bool bucle, float ritmo = 0f)
    {
        var ps = Pieza(padre, t, nombre, "estrella", 1f, bucle);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.5f, tam);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velocidad * 0.5f, velocidad);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        if (bucle) Emitir(ps, ritmo); else Rafaga(ps, cuantas);
        Forma(ps, ParticleSystemShapeType.Sphere, 0.2f, 1f);
        GiroAlAzar(ps, 45f);
        Girar(ps, 240f);
        Golpe(ps, 0.15f, 0.45f);
        return ps;
    }

    // ── Llama astral (fuego) ─────────────────────────────────────────────



    // ── Bola prisma (luz) ────────────────────────────────────────────────



    // ── Ráfaga (viento) ──────────────────────────────────────────────────



}
