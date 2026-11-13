using UnityEditor;
using UnityEngine;
using static VfxProcedural;

/// Efectos de hechizo con el aspecto de dibujo animado: piezas de color plano con contorno de tinta
/// (llamas, estrellas, esquirlas, remolinos…) que aparecen, crecen y se encogen de golpe, en vez de
/// brillos aditivos. Las piezas son PNG en Texturas/Dibujo (dibujadas fuera, con su color). Aquí están
/// las ayudas y los tres primeros hechizos; el resto, en DibujoResto. Ver INC-1127
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
        Guardar(LlamaAstralDibujo, root => DibujoLlamaAstral(root, t), log);
        Guardar(LlamaAstralDibujoImpacto, root => DibujoImpactoDeFuego(root, t), log);
        Guardar(BolaPrismaDibujo, root => DibujoBolaPrisma(root, t), log);
        Guardar(BolaPrismaDibujoImpacto, root => DibujoImpactoDePrisma(root, t), log);
        Guardar(RafagaDibujo, root => DibujoRafaga(root, t), log);
        Guardar(RafagaDibujoImpacto, root => DibujoImpactoDeViento(root, t), log);
        ConstruirDibujoDelResto(t, log);
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

    private static void DibujoLlamaAstral(GameObject root, Texturas t)
    {
        // La llama que viaja: copias que se relevan muy deprisa, con tamaño y giro distintos, para que
        // tiemble como una llama de dibujo.
        var llama = Pieza(root.transform, t, "Llama", "llama");
        var m = llama.main;
        m.startLifetime = 0.16f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.75f, 0.95f);
        m.maxParticles = 4;
        Emitir(llama, 14f);
        GiroAlAzar(llama, 10f);
        Golpe(llama, 0.2f, 0.7f);

        var rastro = Pieza(root.transform, t, "Rastro de llamitas", "llama");
        var mr = rastro.main;
        mr.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        mr.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        mr.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(rastro, 22f);
        Forma(rastro, ParticleSystemShapeType.Sphere, 0.15f, 1f);
        GiroAlAzar(rastro, 25f);
        var tamR = rastro.sizeOverLifetime;
        tamR.enabled = true;
        tamR.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        Estrellitas(root.transform, t, "Estrellitas", 0, 0.22f, 0.6f, true, 8f);
    }

    private static void DibujoImpactoDeFuego(GameObject root, Texturas t)
    {
        Pop(root.transform, t, "Destello", "destello", 1.6f, 0.18f, 1f);
        Pop(root.transform, t, "Onda", "anillo", 1.2f, 0.32f, 2.4f);
        Estallido(root.transform, t, "Pétalos de fuego", "petalo", 10, 6f, 0.55f, 0.4f);
        Bocanadas(root.transform, t, "Humo", 5, 0.9f, 0.7f);
        Estrellitas(root.transform, t, "Estrellitas", 6, 0.3f, 4f, false);
    }

    // ── Bola prisma (luz) ────────────────────────────────────────────────

    private static void DibujoBolaPrisma(GameObject root, Texturas t)
    {
        var orbe = Pieza(root.transform, t, "Orbe", "orbe");
        var m = orbe.main;
        m.startLifetime = 0.5f;
        m.startSize = 0.75f;
        m.maxParticles = 1;
        Emitir(orbe, 2.2f);
        Girar(orbe, 160f);
        var tam = orbe.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 1.08f), new Keyframe(1f, 1f)));

        var esquirlas = Pieza(root.transform, t, "Esquirlas que orbitan", "esquirla");
        var me = esquirlas.main;
        me.startLifetime = 0.45f;
        me.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
        me.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(esquirlas, 12f);
        Forma(esquirlas, ParticleSystemShapeType.Circle, 0.5f, 0.1f);
        GiroAlAzar(esquirlas, 180f);
        Golpe(esquirlas, 0.15f, 0.5f);

        Estrellitas(root.transform, t, "Estrellitas", 0, 0.2f, 0.4f, true, 10f);
    }

    private static void DibujoImpactoDePrisma(GameObject root, Texturas t)
    {
        Pop(root.transform, t, "Destello", "destello", 1.5f, 0.18f, 1f);
        Pop(root.transform, t, "Onda", "anillo", 1.2f, 0.32f, 2.2f);
        Estallido(root.transform, t, "Esquirlas", "esquirla", 12, 6.5f, 0.4f, 0.45f);
        Estrellitas(root.transform, t, "Estrellitas", 8, 0.32f, 4.5f, false);
    }

    // ── Ráfaga (viento) ──────────────────────────────────────────────────

    private static void DibujoRafaga(GameObject root, Texturas t)
    {
        var remolinos = Pieza(root.transform, t, "Remolinos", "remolino");
        var m = remolinos.main;
        m.startLifetime = 0.3f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        Emitir(remolinos, 16f);
        Forma(remolinos, ParticleSystemShapeType.Circle, 0.35f, 1f);
        GiroAlAzar(remolinos, 180f);
        Girar(remolinos, -300f);
        Golpe(remolinos, 0.2f, 0.55f);

        var lineas = Sistema(root.transform, "Líneas de viento", t.Mat("Dibujo_linea_viento", false), ParticleSystemRenderMode.Billboard, 1f);
        var ml = lineas.main;
        ml.startLifetime = 0.22f;
        ml.startSize3D = true;
        ml.startSizeX = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        ml.startSizeY = new ParticleSystem.MinMaxCurve(0.18f, 0.25f);
        ml.startSizeZ = 1f;
        ml.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(lineas, 24f);
        Forma(lineas, ParticleSystemShapeType.Circle, 0.45f, 1f);
        Golpe(lineas, 0.2f, 0.5f);

        var hojas = Pieza(root.transform, t, "Hojas", "hoja");
        var mh = hojas.main;
        mh.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        mh.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        mh.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(hojas, 5f);
        Forma(hojas, ParticleSystemShapeType.Sphere, 0.35f, 1f);
        GiroAlAzar(hojas, 180f);
        Girar(hojas, 360f);
        Golpe(hojas, 0.15f, 0.6f);
    }

    private static void DibujoImpactoDeViento(GameObject root, Texturas t)
    {
        Pop(root.transform, t, "Onda", "anillo", 1.0f, 0.3f, 2.6f);
        Estallido(root.transform, t, "Remolinos", "remolino", 6, 4.5f, 0.7f, 0.45f);
        Bocanadas(root.transform, t, "Polvo", 6, 0.8f, 0.6f);
        var hojas = Pieza(root.transform, t, "Hojas", "hoja", 1f, false);
        var mh = hojas.main;
        mh.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        mh.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        mh.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        mh.simulationSpace = ParticleSystemSimulationSpace.World;
        Rafaga(hojas, 7);
        Forma(hojas, ParticleSystemShapeType.Sphere, 0.2f, 1f);
        Girar(hojas, 400f);
        Golpe(hojas, 0.1f, 0.6f);
    }
}
