using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Efectos propios de los hechizos (INC-640). Genera en Assets/_VFX/Hechizos/:
/// - VFX a medida (texturas procedurales + partículas): cadenas del pacto, zarpazo, orbe prisma,
///   grieta del Mago Oscuro y estallido estelar.
/// - Variantes retintadas de efectos de los packs (Prefab Variant con colores propios; el original
///   del pack no se toca).
/// - Destellos de lanzamiento y de fin por elemento.
/// Paleta: Will dorado/blanco, Estela naranja (fuego) y blanco (viento), Liam violeta, Mago Oscuro
/// negro con rojo. Lo llama VfxDeHechizos antes de asignar; es idempotente (regenera en el sitio y
/// conserva los GUID).
/// </summary>
public static class VfxPropiosDeHechizosBuilder
{
    public const string Carpeta = "Assets/_VFX/Hechizos";
    private const string CarpetaTexturas = Carpeta + "/Texturas";
    private const string CarpetaMateriales = Carpeta + "/Materiales";

    private const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string Lana = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/";
    private const string Gabriel = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/";

    // Nombres de los efectos generados (Ruta(nombre) da el prefab).
    public const string CadenasZona = "CadenasDelPacto_Zona";
    public const string CadenasEstado = "CadenasDelPacto_Estado";
    public const string Garra = "GarraDelPacto";
    public const string BolaPrisma = "BolaPrisma";
    public const string GrietaZona = "MagoOscuroGrieta_Zona";
    public const string CorazonImpacto = "CorazonEstelar_Impacto";
    public const string JuicioZona = "JuicioDelPacto_Zona";
    public const string SelloZona = "SelloDelPacto_Zona";
    public const string MeteoroZona = "Meteoro_Zona";
    public const string Dardo = "DardoMental";

    // Paleta.
    private static readonly Color Violeta = new Color(0.54f, 0.30f, 1f);
    private static readonly Color Lila = new Color(0.80f, 0.45f, 1f);
    private static readonly Color Dorado = new Color(1f, 0.80f, 0.35f);
    private static readonly Color DoradoPalido = new Color(1f, 0.88f, 0.60f);
    private static readonly Color Naranja = new Color(1f, 0.45f, 0.10f);
    private static readonly Color Crema = new Color(0.94f, 0.96f, 0.88f);
    private static readonly Color RojoOscuro = new Color(0.69f, 0.09f, 0.16f);
    private static readonly Color Negro = new Color(0.05f, 0.02f, 0.03f);

    public static string Ruta(string nombre) => $"{Carpeta}/VFX_Hechizo_{nombre}.prefab";

    /// Destello de lanzamiento (fin = false) o de fin (fin = true) de un elemento; null si el
    /// elemento no tiene destello propio.
    public static string RutaDestello(MagicElement elemento, bool fin)
    {
        string e = NombreElemento(elemento);
        return e == null ? null : Ruta((fin ? "DestelloFin_" : "Destello_") + e);
    }

    private static string NombreElemento(MagicElement e)
    {
        switch (e)
        {
            case MagicElement.Fire: return "Fuego";
            case MagicElement.Storm: return "Viento";
            case MagicElement.Light: return "Luz";
            case MagicElement.Mind: return "Mente";
            case MagicElement.Dark: return "Oscuro";
            default: return null;
        }
    }

    /// Genera (o regenera) todos los efectos.
    public static void Construir(System.Text.StringBuilder log, List<string> warnings)
    {
        Carpetas();
        var t = new Texturas();

        Guardar(CadenasZona, root => ConstruirCadenasZona(root, t), log);
        Guardar(CadenasEstado, root => ConstruirCadenasEstado(root, t), log);
        Guardar(Garra, root => ConstruirGarra(root, t), log);
        Guardar(BolaPrisma, root => ConstruirBolaPrisma(root, t), log);
        Guardar(GrietaZona, root => ConstruirGrieta(root, t), log);
        Guardar(CorazonImpacto, root => ConstruirEstalloEstelar(root, t), log);

        Variante(Hovl + "AoE effects/Laser AOE.prefab", JuicioZona, v => Retintar(v, Violeta, null), log, warnings);
        Variante(Hovl + "Magic circles/Freeze circle.prefab", SelloZona, v =>
        {
            Retintar(v, Violeta, null);
            ApagarSistemasConMaterial(v, "Snowflake");
        }, log, warnings);
        Variante(Hovl + "AoE effects/Meteors AOE.prefab", MeteoroZona, v => Retintar(v, DoradoPalido, null), log, warnings);
        Variante(Gabriel + "vfx_Projectile_02.prefab", Dardo, v => Retintar(v, Lila, null), log, warnings);

        foreach (MagicElement e in Enum.GetValues(typeof(MagicElement)))
        {
            string nombre = NombreElemento(e);
            if (nombre == null) continue;
            Color color = ColorDeElemento(e, out Color? oscuro);
            Variante(Lana + "Flash/1 Flash_magic_ellow_blue.prefab", "Destello_" + nombre, v => Retintar(v, color, oscuro), log, warnings);
            Variante(Lana + "Flash/2 Flash_round_ellow.prefab", "DestelloFin_" + nombre, v => Retintar(v, color, oscuro), log, warnings);
        }
    }

    private static Color ColorDeElemento(MagicElement e, out Color? oscuro)
    {
        oscuro = null;
        switch (e)
        {
            case MagicElement.Fire: return Naranja;
            case MagicElement.Storm: return Crema;
            case MagicElement.Light: return Dorado;
            case MagicElement.Mind: return Violeta;
            case MagicElement.Dark: oscuro = Negro; return RojoOscuro;
            default: return Color.white;
        }
    }

    // ── VFX a medida ──────────────────────────────────────────────────────

    /// Zona de 4 m: círculo de pacto que gira, cadenas que suben por el borde y motas violetas.
    private static void ConstruirCadenasZona(GameObject root, Texturas t)
    {
        var circulo = Sistema(root.transform, "Circulo", t.Mat("CirculoRunico", true), ParticleSystemRenderMode.HorizontalBillboard, 30f);
        Unico(circulo, 30f, 8.6f, Violeta * 1.6f);
        Girar(circulo, 18f);

        var halo = Sistema(root.transform, "Halo", t.Mat("Brillo", true), ParticleSystemRenderMode.HorizontalBillboard, 30f);
        Unico(halo, 30f, 9.5f, new Color(Violeta.r, Violeta.g, Violeta.b, 0.35f));

        var cadenas = Sistema(root.transform, "Cadenas", t.Mat("Cadena", true), ParticleSystemRenderMode.VerticalBillboard, 1f);
        var main = cadenas.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.32f, 0.42f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(1.4f, 2.0f);
        main.startSizeZ = 1f;
        main.startColor = Lila * 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(cadenas, 22f);
        Forma(cadenas, ParticleSystemShapeType.Circle, 3.7f, 0f);
        Velocidad(cadenas, new Vector3(0f, 1.8f, 0f));
        Desvanecer(cadenas, 0.15f, 0.7f);

        Motas(root.transform, t, "Motas", Lila, 3.8f, 30f, 1.2f);
    }

    /// Cadenas girando alrededor de un personaje inmovilizado (a la altura de piernas y pecho).
    private static void ConstruirCadenasEstado(GameObject root, Texturas t)
    {
        foreach (var (nombre, altura, giro) in new[] { ("Cadena baja", 0.5f, 110f), ("Cadena alta", 1.2f, -90f) })
        {
            var anillo = Sistema(root.transform, nombre, t.Mat("AnilloDeCadena", true), ParticleSystemRenderMode.HorizontalBillboard, 1f);
            anillo.transform.localPosition = new Vector3(0f, altura, 0f);
            var main = anillo.main;
            main.startLifetime = 1f;
            main.startSize = 1.7f;
            main.startColor = Violeta * 1.5f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Emitir(anillo, 2f);
            Girar(anillo, giro);
            Desvanecer(anillo, 0.25f, 0.75f);
        }
        Motas(root.transform, t, "Motas", Lila, 0.6f, 14f, 0.8f);
    }

    /// Proyectil: tres zarpazos violetas que miran a la cámara, con núcleo y estela de chispas.
    private static void ConstruirGarra(GameObject root, Texturas t)
    {
        var zarpazo = Sistema(root.transform, "Zarpazos", t.Mat("Zarpazo", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = zarpazo.main;
        main.startLifetime = 0.12f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.3f);
        main.startColor = Violeta * 1.8f;
        Emitir(zarpazo, 45f);
        Desvanecer(zarpazo, 0.2f, 0.6f);

        Nucleo(root.transform, t, "Nucleo", Lila * 1.5f, 0.6f);
        Estela(root.transform, t, "Estela", Violeta, 0.35f, 60f);
    }

    /// Proyectil: orbe de luz blanca con destellos de arcoíris y estela.
    private static void ConstruirBolaPrisma(GameObject root, Texturas t)
    {
        Nucleo(root.transform, t, "Nucleo", Color.white * 1.6f, 0.8f);

        var estrella = Sistema(root.transform, "Destello", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = estrella.main;
        main.startLifetime = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 1f, 1f, 0.8f);
        Emitir(estrella, 16f);
        Desvanecer(estrella, 0.3f, 0.5f);

        var arcoiris = Sistema(root.transform, "Arcoiris", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        main = arcoiris.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var degradado = new Gradient();
        degradado.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.2f), 0.2f),
                new GradientColorKey(new Color(1f, 1f, 0.4f), 0.4f), new GradientColorKey(new Color(0.4f, 1f, 0.5f), 0.6f),
                new GradientColorKey(new Color(0.4f, 0.7f, 1f), 0.8f), new GradientColorKey(new Color(0.8f, 0.45f, 1f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(degradado) { mode = ParticleSystemGradientMode.RandomColor };
        Emitir(arcoiris, 70f);
        Forma(arcoiris, ParticleSystemShapeType.Sphere, 0.3f, 1f);
        Desvanecer(arcoiris, 0.1f, 0.5f);

        Estela(root.transform, t, "Estela", Color.white, 0.3f, 50f);
    }

    /// Zona de 5,5 m: grieta negra en el suelo con fisuras rojas, humo negro y brasas.
    private static void ConstruirGrieta(GameObject root, Texturas t)
    {
        var grieta = Sistema(root.transform, "Grieta", t.Mat("Grieta", false), ParticleSystemRenderMode.HorizontalBillboard, 30f);
        Unico(grieta, 30f, 11.5f, Negro);
        grieta.GetComponent<ParticleSystemRenderer>().sortingFudge = 10f;

        var fisuras = Sistema(root.transform, "Fisuras", t.Mat("Grieta", true), ParticleSystemRenderMode.HorizontalBillboard, 1.6f);
        var main = fisuras.main;
        main.startLifetime = 1.6f;
        main.startSize = 11.5f;
        main.startColor = RojoOscuro * 2f;
        Emitir(fisuras, 1.25f);
        Desvanecer(fisuras, 0.4f, 0.6f);

        var humo = Sistema(root.transform, "Humo", t.Mat("Humo", false), ParticleSystemRenderMode.Billboard, 1f);
        main = humo.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.04f, 0.02f, 0.03f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(humo, 10f);
        Forma(humo, ParticleSystemShapeType.Circle, 4.8f, 1f);
        Velocidad(humo, new Vector3(0f, 0.5f, 0f));
        Desvanecer(humo, 0.25f, 0.6f);

        Motas(root.transform, t, "Brasas", RojoOscuro * 2.2f, 5f, 35f, 1.6f);
    }

    /// Impacto de una sola vez: estrella dorada que se abre, anillo que se expande y chispas.
    private static void ConstruirEstalloEstelar(GameObject root, Texturas t)
    {
        var estrella = Sistema(root.transform, "Estrella", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1.5f, bucle: false);
        var main = estrella.main;
        main.startLifetime = 0.7f;
        main.startSize = 7f;
        main.startColor = Dorado * 2f;
        Rafaga(estrella, 1);
        Crecer(estrella, 0.15f);
        Desvanecer(estrella, 0.05f, 0.4f);

        var brillo = Sistema(root.transform, "Brillo", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1.5f, bucle: false);
        main = brillo.main;
        main.startLifetime = 0.5f;
        main.startSize = 5f;
        main.startColor = new Color(1f, 0.95f, 0.8f) * 1.5f;
        Rafaga(brillo, 1);
        Desvanecer(brillo, 0.05f, 0.3f);

        var anillo = Sistema(root.transform, "Anillo", t.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 1.5f, bucle: false);
        main = anillo.main;
        main.startLifetime = 0.7f;
        main.startSize = 9f;
        main.startColor = Dorado * 1.6f;
        Rafaga(anillo, 1);
        Crecer(anillo, 0.05f);
        Desvanecer(anillo, 0.05f, 0.3f);

        var chispas = Sistema(root.transform, "Chispas", t.Mat("Brillo", true), ParticleSystemRenderMode.Stretch, 1.5f, bucle: false);
        main = chispas.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(Dorado * 2f, Color.white * 1.5f);
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Rafaga(chispas, 60);
        Forma(chispas, ParticleSystemShapeType.Sphere, 0.3f, 1f);
        var render = chispas.GetComponent<ParticleSystemRenderer>();
        render.velocityScale = 0.06f;
        render.lengthScale = 1f;
        Desvanecer(chispas, 0.05f, 0.6f);
    }

    // ── Piezas comunes ────────────────────────────────────────────────────

    /// Núcleo brillante que viaja con el proyectil.
    private static void Nucleo(Transform padre, Texturas t, string nombre, Color color, float tam)
    {
        var ps = Sistema(padre, nombre, t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = ps.main;
        main.startLifetime = 0.15f;
        main.startSize = tam;
        main.startColor = color;
        Emitir(ps, 30f);
        Desvanecer(ps, 0.2f, 0.6f);
    }

    /// Estela de chispas que se queda atrás (espacio de mundo).
    private static void Estela(Transform padre, Texturas t, string nombre, Color color, float tam, float ritmo)
    {
        var ps = Sistema(padre, nombre, t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.4f, tam);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(ps, ritmo);
        Forma(ps, ParticleSystemShapeType.Sphere, 0.12f, 1f);
        var tam2 = ps.sizeOverLifetime;
        tam2.enabled = true;
        tam2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(ps, 0.05f, 0.4f);
    }

    /// Motas que suben desde un círculo.
    private static void Motas(Transform padre, Texturas t, string nombre, Color color, float radio, float ritmo, float subida)
    {
        var ps = Sistema(padre, nombre, t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = color * 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(ps, ritmo);
        Forma(ps, ParticleSystemShapeType.Circle, radio, 1f);
        Velocidad(ps, new Vector3(0f, subida, 0f));
        Desvanecer(ps, 0.2f, 0.6f);
    }

    private static ParticleSystem Sistema(Transform padre, string nombre, Material mat, ParticleSystemRenderMode modo, float duracion, bool bucle = true)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = bucle;
        main.duration = duracion;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 500;

        var emision = ps.emission;
        emision.rateOverTime = 0f;
        var forma = ps.shape;
        forma.enabled = false;

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.renderMode = modo;
        render.sharedMaterial = mat;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        return ps;
    }

    /// Una sola partícula que dura toda la zona (la zona se destruye antes de que acabe).
    private static void Unico(ParticleSystem ps, float vida, float tam, Color color)
    {
        var main = ps.main;
        main.startLifetime = vida;
        main.startSize = tam;
        main.startColor = color;
        main.maxParticles = 1;
        Rafaga(ps, 1);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.012f), new GradientAlphaKey(1f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void Rafaga(ParticleSystem ps, int cuantas)
    {
        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)cuantas) });
    }

    private static void Emitir(ParticleSystem ps, float ritmo)
    {
        var emision = ps.emission;
        emision.rateOverTime = ritmo;
    }

    private static void Forma(ParticleSystem ps, ParticleSystemShapeType tipo, float radio, float grosor)
    {
        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = tipo;
        forma.radius = radio;
        forma.radiusThickness = grosor;
        if (tipo == ParticleSystemShapeType.Circle) forma.rotation = new Vector3(-90f, 0f, 0f);
    }

    private static void Velocidad(ParticleSystem ps, Vector3 v)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        // Los tres ejes tienen que ir en el mismo modo (dos constantes).
        vel.x = new ParticleSystem.MinMaxCurve(v.x, v.x);
        vel.y = new ParticleSystem.MinMaxCurve(v.y * 0.7f, v.y * 1.3f);
        vel.z = new ParticleSystem.MinMaxCurve(v.z, v.z);
    }

    private static void Girar(ParticleSystem ps, float gradosPorSegundo)
    {
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(gradosPorSegundo * Mathf.Deg2Rad);
    }

    /// Crece de casi nada a su tamaño en la fracción de vida indicada.
    private static void Crecer(ParticleSystem ps, float hasta)
    {
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        var curva = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(hasta, 0.8f), new Keyframe(1f, 1f));
        tam.size = new ParticleSystem.MinMaxCurve(1f, curva);
    }

    /// Alfa: entra hasta 'entra' y empieza a salir en 'sale' (fracciones de la vida).
    private static void Desvanecer(ParticleSystem ps, float entra, float sale)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, entra), new GradientAlphaKey(1f, sale), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void Guardar(string nombre, Action<GameObject> construir, System.Text.StringBuilder log)
    {
        var root = new GameObject("VFX_Hechizo_" + nombre);
        try
        {
            construir(root);
            PrefabUtility.SaveAsPrefabAsset(root, Ruta(nombre));
            log.AppendLine($"   VFX nuevo: {nombre}.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ── Variantes retintadas ──────────────────────────────────────────────

    private static void Variante(string origen, string nombre, Action<GameObject> ajustar, System.Text.StringBuilder log, List<string> warnings)
    {
        var fuente = AssetDatabase.LoadAssetAtPath<GameObject>(origen);
        if (fuente == null) { warnings.Add($"No encuentro {origen} (variante {nombre})."); return; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fuente);
        try
        {
            inst.name = "VFX_Hechizo_" + nombre;
            ajustar(inst);
            PrefabUtility.SaveAsPrefabAsset(inst, Ruta(nombre));
            log.AppendLine($"   Variante: {nombre} (de {fuente.name}).");
        }
        finally
        {
            Object.DestroyImmediate(inst);
        }
    }

    /// Cambia el tono de todas las partículas y luces al del color dado, respetando brillo y alfa.
    /// Con 'oscuro', las partículas de mezcla alfa toman ese color (el negro no se ve en aditivo).
    private static void Retintar(GameObject root, Color color, Color? oscuro)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var render = ps.GetComponent<ParticleSystemRenderer>();
            Color destino = oscuro.HasValue && EsMezclaAlfa(render) ? oscuro.Value : color;

            var main = ps.main;
            main.startColor = Tintar(main.startColor, destino);
            var col = ps.colorOverLifetime;
            if (col.enabled) col.color = Tintar(col.color, destino);
            var trails = ps.trails;
            if (trails.enabled) trails.colorOverLifetime = Tintar(trails.colorOverLifetime, destino);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
        }
        foreach (var luz in root.GetComponentsInChildren<Light>(true))
        {
            luz.color = Tintar(luz.color, color);
            PrefabUtility.RecordPrefabInstancePropertyModifications(luz);
        }
    }

    private static bool EsMezclaAlfa(ParticleSystemRenderer render)
    {
        if (render == null || render.sharedMaterial == null) return false;
        string n = render.sharedMaterial.name;
        if (n.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return n.Contains("AB") || n.IndexOf("Alpha", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApagarSistemasConMaterial(GameObject root, string parteDelNombre)
    {
        foreach (var render in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            if (render.sharedMaterial != null && render.sharedMaterial.name.Contains(parteDelNombre))
            {
                render.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(render.gameObject);
            }
    }

    private static ParticleSystem.MinMaxGradient Tintar(ParticleSystem.MinMaxGradient g, Color d)
    {
        switch (g.mode)
        {
            case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(Tintar(g.color, d));
            case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(Tintar(g.colorMin, d), Tintar(g.colorMax, d));
            case ParticleSystemGradientMode.Gradient: return new ParticleSystem.MinMaxGradient(Tintar(g.gradient, d));
            case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(Tintar(g.gradientMin, d), Tintar(g.gradientMax, d));
            case ParticleSystemGradientMode.RandomColor: return new ParticleSystem.MinMaxGradient(Tintar(g.gradient, d)) { mode = ParticleSystemGradientMode.RandomColor };
            default: return g;
        }
    }

    private static Gradient Tintar(Gradient g, Color d)
    {
        if (g == null) return null;
        var nuevo = new Gradient { mode = g.mode };
        nuevo.SetKeys(g.colorKeys.Select(k => new GradientColorKey(Tintar(k.color, d), k.time)).ToArray(), g.alphaKeys);
        return nuevo;
    }

    private static Color Tintar(Color c, Color d)
    {
        Color.RGBToHSV(c, out _, out float s, out float v);
        Color.RGBToHSV(d, out float dh, out float ds, out float dv);
        Color r;
        if (dv < 0.2f) r = d;                                        // destino oscuro: el color tal cual
        else if (s < 0.12f) r = Color.Lerp(c, d * Mathf.Max(v, 0.01f), 0.3f); // blancos: se quedan casi blancos
        else r = Color.HSVToRGB(dh, Mathf.Lerp(s, ds, 0.6f), v, true);
        r.a = c.a;
        return r;
    }

    // ── Carpetas, texturas y materiales ───────────────────────────────────

    private static void Carpetas()
    {
        Crear("Assets", "_VFX");
        Crear("Assets/_VFX", "Hechizos");
        Crear(Carpeta, "Texturas");
        Crear(Carpeta, "Materiales");
    }

    private static void Crear(string padre, string nombre)
    {
        if (!AssetDatabase.IsValidFolder(padre + "/" + nombre)) AssetDatabase.CreateFolder(padre, nombre);
    }

    /// Texturas procedurales (blanco con alfa; el color lo ponen las partículas) y sus materiales.
    private sealed class Texturas
    {
        private readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();

        public Material Mat(string textura, bool aditivo)
        {
            string ruta = $"{CarpetaMateriales}/M_Hechizo_{textura}_{(aditivo ? "Aditivo" : "Alfa")}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, ruta);
            }
            else if (shader != null && mat.shader != shader) mat.shader = shader;

            mat.SetTexture("_BaseMap", Tex(textura));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", aditivo ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(aditivo ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private Texture2D Tex(string nombre)
        {
            if (_tex.TryGetValue(nombre, out var t)) return t;
            switch (nombre)
            {
                case "Brillo": t = Pintar(nombre, 128, 128, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - R(x, y)), 2f)); break;
                case "Anillo": t = Pintar(nombre, 256, 256, (x, y) => Gauss(R(x, y) - 0.86f, 0.045f) + 0.25f * Gauss(R(x, y) - 0.8f, 0.12f)); break;
                case "Estrella": t = Pintar(nombre, 256, 256, Estrella); break;
                case "Humo": t = Pintar(nombre, 128, 128, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - R(x, y)), 1.5f) * (0.55f + 0.45f * Mathf.PerlinNoise(x * 5f + 3.1f, y * 5f + 7.7f))); break;
                case "CirculoRunico": t = Pintar(nombre, 512, 512, CirculoRunico); break;
                case "Cadena": t = Pintar(nombre, 64, 256, Cadena); break;
                case "AnilloDeCadena": t = Pintar(nombre, 256, 256, AnilloDeCadena); break;
                case "Zarpazo": t = Pintar(nombre, 256, 256, Zarpazos); break;
                case "Grieta": t = PintarGrieta(nombre, 512); break;
            }
            _tex[nombre] = t;
            return t;
        }

        // Coordenadas de -1 a 1 con el centro en 0.
        private static float R(float x, float y) => Mathf.Sqrt(x * x + y * y);
        private static float Gauss(float d, float ancho) => Mathf.Exp(-(d * d) / (ancho * ancho));
        private static float Linea(float d, float grosor) => Mathf.Clamp01((grosor - Mathf.Abs(d)) / (grosor * 0.5f) + 0.5f);

        private static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Estrella(float x, float y)
        {
            float r = R(x, y);
            float nucleo = Mathf.Pow(Mathf.Clamp01(1f - r * 1.6f), 3f);
            float rayos = Mathf.Max(Gauss(y, 0.035f) * Mathf.Clamp01(1f - Mathf.Abs(x)), Gauss(x, 0.035f) * Mathf.Clamp01(1f - Mathf.Abs(y)));
            float dx = (x + y) * 0.7071f, dy = (x - y) * 0.7071f;
            float diag = Mathf.Max(Gauss(dy, 0.03f) * Mathf.Clamp01(1f - Mathf.Abs(dx) * 1.6f), Gauss(dx, 0.03f) * Mathf.Clamp01(1f - Mathf.Abs(dy) * 1.6f)) * 0.6f;
            return Mathf.Clamp01(nucleo + rayos + diag);
        }

        /// Círculo de pacto: dos anillos, marcas, estrella de cinco puntas y runas en sus vértices.
        private static float CirculoRunico(float x, float y)
        {
            var p = new Vector2(x, y);
            float r = p.magnitude;
            float a = Mathf.Max(Linea(r - 0.95f, 0.012f), Linea(r - 0.82f, 0.01f));
            a = Mathf.Max(a, Linea(r - 0.45f, 0.008f));
            float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            if (r > 0.84f && r < 0.93f)
            {
                float paso = Mathf.Repeat(ang, 15f);
                if (paso < 2.2f) a = Mathf.Max(a, 0.9f);
            }
            var v = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float t = (90f + i * 72f) * Mathf.Deg2Rad;
                v[i] = new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * 0.8f;
            }
            for (int i = 0; i < 5; i++)
                a = Mathf.Max(a, Linea(DistSegmento(p, v[i], v[(i + 2) % 5]), 0.009f));
            for (int i = 0; i < 5; i++)
            {
                Vector2 c = v[i] * 0.66f;
                a = Mathf.Max(a, Linea((p - c).magnitude - 0.05f, 0.008f));
            }
            a = Mathf.Max(a, 0.18f * Gauss(r - 0.88f, 0.08f));
            return Mathf.Clamp01(a);
        }

        /// Tira vertical de eslabones: uno de frente (aro) y otro de canto (barra), alternos.
        private static float Cadena(float x, float y)
        {
            // x en [-1,1] cubre 64 px y y en [-1,1] cubre 256 px: se pasa a unidades iguales.
            var p = new Vector2(x, y * 4f);
            float a = 0f;
            for (int k = 0; k < 5; k++)
            {
                float cy = -4f + k * 2f;
                bool frente = k % 2 == 0;
                var arriba = new Vector2(0f, cy + 0.55f);
                var abajo = new Vector2(0f, cy - 0.55f);
                float d = DistSegmento(p, abajo, arriba);
                a = frente ? Mathf.Max(a, Linea(d - 0.55f, 0.13f)) : Mathf.Max(a, Mathf.Clamp01((0.2f - d) / 0.08f));
            }
            return a * Mathf.Clamp01((4f - Mathf.Abs(p.y)) / 0.6f);
        }

        /// Aro de catorce eslabones alrededor del centro.
        private static float AnilloDeCadena(float x, float y)
        {
            var p = new Vector2(x, y);
            float a = 0f;
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float t = i * Mathf.PI * 2f / n;
                var c = new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * 0.78f;
                var tan = new Vector2(-Mathf.Sin(t), Mathf.Cos(t));
                bool frente = i % 2 == 0;
                float largo = frente ? 0.09f : 0.11f;
                float d = DistSegmento(p, c - tan * largo, c + tan * largo);
                a = frente ? Mathf.Max(a, Linea(d - 0.06f, 0.022f)) : Mathf.Max(a, Mathf.Clamp01((0.025f - d) / 0.012f));
            }
            return a;
        }

        /// Tres zarpazos curvos en paralelo, más gruesos en el centro que en las puntas.
        private static float Zarpazos(float x, float y)
        {
            var p = new Vector2(x, y);
            float a = 0f;
            for (int k = -1; k <= 1; k++)
            {
                float ox = k * 0.42f;
                var p0 = new Vector2(ox + 0.28f, 0.85f);
                var p1 = new Vector2(ox + 0.3f, 0f);
                var p2 = new Vector2(ox - 0.28f, -0.85f);
                float mejor = 10f, tMejor = 0f;
                for (int i = 0; i <= 40; i++)
                {
                    float t = i / 40f;
                    Vector2 q = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                    float d = (p - q).magnitude;
                    if (d < mejor) { mejor = d; tMejor = t; }
                }
                float grosor = 0.085f * Mathf.Sin(Mathf.PI * tMejor);
                a = Mathf.Max(a, Mathf.Clamp01((grosor - mejor) / 0.02f + 0.5f));
            }
            return a;
        }

        private static Texture2D Pintar(string nombre, int w, int h, Func<float, float, float> f)
        {
            var a = new float[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    a[j * w + i] = f((i + 0.5f) / w * 2f - 1f, (j + 0.5f) / h * 2f - 1f);
            return Guardar(nombre, w, h, a);
        }

        /// Grietas que salen del centro y se ramifican (semilla fija: siempre la misma).
        private static Texture2D PintarGrieta(string nombre, int n)
        {
            var a = new float[n * n];
            var rnd = new System.Random(640);
            var pendientes = new Stack<(Vector2 p, float ang, float grosor, int pasos)>();
            for (int i = 0; i < 7; i++)
                pendientes.Push((Vector2.zero, i * Mathf.PI * 2f / 7f + (float)rnd.NextDouble() * 0.5f, 0.022f, 26));
            while (pendientes.Count > 0)
            {
                var (p, ang, grosor, pasos) = pendientes.Pop();
                for (int s = 0; s < pasos; s++)
                {
                    ang += ((float)rnd.NextDouble() - 0.5f) * 0.9f;
                    var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.034f;
                    if (q.magnitude > 0.96f) break;
                    float g = grosor * (1f - s / (float)pasos * 0.7f);
                    Trazo(a, n, p, q, g);
                    if (rnd.NextDouble() < 0.14 && grosor > 0.008f)
                        pendientes.Push((q, ang + ((float)rnd.NextDouble() < 0.5 ? 0.8f : -0.8f), grosor * 0.55f, pasos / 2));
                    p = q;
                }
            }
            return Guardar(nombre, n, n, a);
        }

        private static void Trazo(float[] a, int n, Vector2 p, Vector2 q, float grosor)
        {
            float margen = grosor * 2f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(((Mathf.Min(p.x, q.x) - margen) + 1f) * 0.5f * n));
            int x1 = Mathf.Min(n - 1, Mathf.CeilToInt(((Mathf.Max(p.x, q.x) + margen) + 1f) * 0.5f * n));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(((Mathf.Min(p.y, q.y) - margen) + 1f) * 0.5f * n));
            int y1 = Mathf.Min(n - 1, Mathf.CeilToInt(((Mathf.Max(p.y, q.y) + margen) + 1f) * 0.5f * n));
            for (int j = y0; j <= y1; j++)
                for (int i = x0; i <= x1; i++)
                {
                    var c = new Vector2((i + 0.5f) / n * 2f - 1f, (j + 0.5f) / n * 2f - 1f);
                    float d = DistSegmento(c, p, q);
                    float v = Mathf.Clamp01((grosor - d) / (grosor * 0.6f) + 0.4f);
                    int k = j * n + i;
                    if (v > a[k]) a[k] = v;
                }
        }

        private static Texture2D Guardar(string nombre, int w, int h, float[] a)
        {
            string ruta = $"{CarpetaTexturas}/T_Hechizo_{nombre}.png";
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(a[i]) * 255f);
                px[i] = new Color32(b, b, b, b);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(ruta) is TextureImporter imp)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.sRGBTexture = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }
    }
}
