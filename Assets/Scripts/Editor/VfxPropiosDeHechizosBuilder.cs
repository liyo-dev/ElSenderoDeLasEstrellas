using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static VfxProcedural;

/// <summary>
/// Efectos propios de los hechizos (INC-640). Genera en Assets/_VFX/Hechizos/:
/// - VFX a medida (texturas procedurales + partículas): cadenas del pacto, zarpazo, estrellas,
///   grieta del Mago Oscuro y estallido estelar.
/// - Variantes retintadas de efectos de los packs (Prefab Variant con colores propios; el original
///   del pack no se toca).
/// - Destellos de lanzamiento y de fin por elemento.
/// Paleta: Will dorado/blanco, Estela naranja (fuego) y blanco (viento), Liam violeta, Mago Oscuro
/// negro con rojo. Lo llama VfxDeHechizos antes de asignar; es idempotente (regenera en el sitio y
/// conserva los GUID). Las piezas genéricas (partículas, texturas y materiales) están en
/// VfxProcedural.
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
    public const string BolaPrismaClasica = "BolaPrisma_Clasica";
    public const string EstrellaFugazProyectil = "EstrellaFugaz_Proyectil";
    public const string TresEstrellasProyectil = "LluviaDeChispas_Proyectil";
    public const string TresEstrellasImpacto = "LluviaDeChispas_Impacto";
    public const string GrietaZona = "MagoOscuroGrieta_Zona";
    public const string CorazonImpacto = "CorazonEstelar_Impacto";
    public const string JuicioZona = "JuicioDelPacto_Zona";
    public const string SelloZona = "SelloDelPacto_Zona";
    public const string MeteoroZona = "Meteoro_Zona";
    public const string Dardo = "DardoMental";
    public const string RafagaViento = "Rafaga_Viento";
    public const string ChispaFuego = "ChispaIgnea_Fuego";
    public const string AuraEstelarProyectil = "AuraEstelar_Proyectil";
    public const string AuraEstelarImpacto = "AuraEstelar_Impacto";
    public const string EcoProyectil = "Eco_Proyectil";
    public const string EcoImpacto = "Eco_Impacto";
    public const string DardoMentalImpacto = "DardoMental_Impacto";
    public const string GarraImpacto = "GarraDelPacto_Impacto";

    // Paleta.
    private static readonly Color Violeta = new Color(0.54f, 0.30f, 1f);
    private static readonly Color VioletaProfundo = new Color(0.38f, 0.12f, 0.85f);
    private static readonly Color Lila = new Color(0.80f, 0.45f, 1f);
    private static readonly Color BrilloLilaSuave = new Color(0.82f, 0.65f, 1f);
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
        var t = NuevasTexturas();

        Guardar(CadenasZona, root => ConstruirCadenasZona(root, t), log);
        Guardar(CadenasEstado, root => ConstruirCadenasEstado(root, t), log);
        Guardar(Garra, root => ConstruirGarra(root, t), log);
        Guardar(EstrellaFugazProyectil, root => ConstruirEstrellaFugaz(root, t), log);
        Guardar(TresEstrellasProyectil, root => ConstruirEstrellaDeAbanico(root, t), log);
        Guardar(TresEstrellasImpacto, root => ConstruirImpactoDeEstrella(root, t), log);
        Guardar(GrietaZona, root => ConstruirGrieta(root, t), log);
        Guardar(CorazonImpacto, root => ConstruirEstalloEstelar(root, t), log);
        Guardar(RafagaViento, root => ConstruirRafagaViento(root, t), log);
        Guardar(ChispaFuego, root => ConstruirChispaFuego(root, t), log);
        Guardar(AuraEstelarProyectil, root => ConstruirAuraEstelar(root, t), log);
        Guardar(AuraEstelarImpacto, root => ConstruirImpactoAuraEstelar(root, t), log);
        Guardar(EcoProyectil, root => ConstruirEco(root, t), log);
        Guardar(EcoImpacto, root => ConstruirImpactoEco(root, t), log);
        Guardar(Dardo, root => ConstruirDardoMental(root, t), log);
        Guardar(DardoMentalImpacto, root => ConstruirImpactoDardoMental(root, t), log);
        Guardar(GarraImpacto, root => ConstruirImpactoGarra(root, t), log);
        Guardar(SelloZona, root => ConstruirSelloZona(root, t), log);

        Variante(Hovl + "AoE effects/Laser AOE.prefab", JuicioZona, v => Retintar(v, VioletaProfundo, null), log, warnings);
        Variante("Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect1.prefab", BolaPrismaClasica,
            _ => { }, log, warnings);
        Variante(Hovl + "AoE effects/Meteors AOE.prefab", MeteoroZona, v => Retintar(v, DoradoPalido, null), log, warnings);

        foreach (MagicElement e in Enum.GetValues(typeof(MagicElement)))
        {
            string nombre = NombreElemento(e);
            if (nombre == null) continue;
            Color color = ColorDeElemento(e, out Color? oscuro);
            Variante(Lana + "Flash/1 Flash_magic_ellow_blue.prefab", "Destello_" + nombre, v =>
            {
                Retintar(v, color, oscuro);
                ApagarHijosPorNombre(v, "Circle", "Shockwave", "Shadow");
                v.transform.localScale = Vector3.one * (e == MagicElement.Mind ? 0.35f : (e == MagicElement.Storm ? 0.45f : 0.65f));
            }, log, warnings);
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

    /// Garra del Pacto: zarpazos de sombra espectral densos con humo oscuro y zarcillos de pacto.
    private static void ConstruirGarra(GameObject root, Texturas t)
    {
        var zarpazo = Sistema(root.transform, "Zarpazos", t.Mat("GarraEspectral", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainZ = zarpazo.main;
        mainZ.startLifetime = 0.22f;
        mainZ.startSize = 2.6f;
        mainZ.startColor = Violeta * 2.0f;
        Emitir(zarpazo, 55f);
        Desvanecer(zarpazo, 0.12f, 0.7f);

        // Núcleo espectral amatista profundo (sin blanco saturado)
        Nucleo(root.transform, t, "Núcleo espectral", Lila * 1.3f, 0.65f);

        var humo = Sistema(root.transform, "Humo espectral", t.Mat("Humo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainH = humo.main;
        mainH.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        mainH.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        mainH.startColor = new Color(0.24f, 0.05f, 0.40f, 0.85f);
        mainH.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(humo, 65f);
        Forma(humo, ParticleSystemShapeType.Sphere, 0.25f, 1f);
        Desvanecer(humo, 0.1f, 0.6f);

        var estela = Sistema(root.transform, "Rastro sombrío", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainE = estela.main;
        mainE.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
        mainE.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
        mainE.startColor = new ParticleSystem.MinMaxGradient(Lila * 1.3f, VioletaProfundo);
        mainE.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(estela, 80f);
        Forma(estela, ParticleSystemShapeType.Sphere, 0.15f, 1f);
        var tamE = estela.sizeOverLifetime;
        tamE.enabled = true;
        tamE.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(estela, 0.05f, 0.5f);
    }

    /// Impacto de Garra del Pacto: corte violento de sombra espectral, ráfaga de humo sombrío y esquirlas de pacto.
    private static void ConstruirImpactoGarra(GameObject root, Texturas t)
    {
        var corte = Sistema(root.transform, "Zarpazo de impacto", t.Mat("GarraEspectral", true), ParticleSystemRenderMode.Billboard, 0.6f, bucle: false);
        var mainC = corte.main;
        mainC.startLifetime = 0.25f;
        mainC.startSize = 3.2f;
        mainC.startColor = Violeta * 2.2f;
        Rafaga(corte, 1);
        Desvanecer(corte, 0.05f, 0.8f);

        var onda = Sistema(root.transform, "Onda de choque", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 0.6f, bucle: false);
        var mainO = onda.main;
        mainO.startLifetime = 0.35f;
        mainO.startSize = 2.4f;
        mainO.startColor = Lila * 1.5f;
        Rafaga(onda, 1);
        Crecer(onda, 0.8f);
        Desvanecer(onda, 0.08f, 0.6f);

        var humo = Sistema(root.transform, "Humo de impacto", t.Mat("Humo", true), ParticleSystemRenderMode.Billboard, 0.8f, bucle: false);
        var mainH = humo.main;
        mainH.startLifetime = 0.5f;
        mainH.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        mainH.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        mainH.startColor = new Color(0.22f, 0.04f, 0.38f, 0.8f);
        Rafaga(humo, 14);
        Forma(humo, ParticleSystemShapeType.Sphere, 0.3f, 1f);
        Desvanecer(humo, 0.08f, 0.65f);

        var chispas = Sistema(root.transform, "Esquirlas oscuras", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 0.6f, bucle: false);
        var mainCh = chispas.main;
        mainCh.startLifetime = 0.35f;
        mainCh.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        mainCh.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        mainCh.startColor = Lila * 1.8f;
        Rafaga(chispas, 12);
        Forma(chispas, ParticleSystemShapeType.Sphere, 0.2f, 1f);
        Desvanecer(chispas, 0.05f, 0.7f);
    }

    /// Ráfaga: cuchilla curvada de viento cortante tridimensional con estela de vendaval.
    private static void ConstruirRafagaViento(GameObject root, Texturas t)
    {
        var filo = Sistema(root.transform, "Cuchilla cortante", t.Mat("CuchillaViento", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainF = filo.main;
        mainF.startLifetime = 0.18f;
        mainF.startSize = 2.4f;
        mainF.startColor = new Color(0.85f, 1f, 1f) * 2.2f;
        Emitir(filo, 45f);
        Desvanecer(filo, 0.05f, 0.7f);

        Nucleo(root.transform, t, "Centro de vacío", Crema * 1.8f, 1.1f);

        var estela = Sistema(root.transform, "Estela de vacío", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainE = estela.main;
        mainE.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        mainE.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        mainE.startColor = new Color(0.8f, 0.96f, 1f, 0.75f);
        mainE.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(estela, 100f);
        Forma(estela, ParticleSystemShapeType.Sphere, 0.2f, 1f);
        var tamE = estela.sizeOverLifetime;
        tamE.enabled = true;
        tamE.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(estela, 0.05f, 0.45f);

        var remolino = Sistema(root.transform, "Remolino", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainR = remolino.main;
        mainR.startLifetime = 0.25f;
        mainR.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        mainR.startColor = new Color(0.9f, 1f, 0.95f, 0.9f);
        Emitir(remolino, 70f);
        Forma(remolino, ParticleSystemShapeType.Circle, 0.5f, 1f);
        Girar(remolino, 400f);
        Desvanecer(remolino, 0.1f, 0.6f);
    }

    /// Chispa Ígnea: brasa de plasma ardiente concentrada, llamas vivas y estela de chispas calientes.
    private static void ConstruirChispaFuego(GameObject root, Texturas t)
    {
        Nucleo(root.transform, t, "Brasa", new Color(1f, 0.85f, 0.3f) * 2.5f, 1.2f);

        var llamas = Sistema(root.transform, "Llamas", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainL = llamas.main;
        mainL.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        mainL.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        mainL.startColor = Naranja * 2.2f;
        mainL.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(llamas, 120f);
        Forma(llamas, ParticleSystemShapeType.Sphere, 0.25f, 1f);
        Velocidad(llamas, new Vector3(0f, 1.2f, 0f));
        Desvanecer(llamas, 0.08f, 0.6f);

        var estela = Sistema(root.transform, "Cola de fuego", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainE = estela.main;
        mainE.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        mainE.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        mainE.startColor = new ParticleSystem.MinMaxGradient(Naranja * 2f, RojoOscuro * 1.5f);
        mainE.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(estela, 110f);
        Forma(estela, ParticleSystemShapeType.Sphere, 0.15f, 1f);
        var tamE = estela.sizeOverLifetime;
        tamE.enabled = true;
        tamE.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(estela, 0.05f, 0.5f);

        var chispas = Sistema(root.transform, "Chispas", t.Mat("Brillo", true), ParticleSystemRenderMode.Stretch, 1f);
        var mainC = chispas.main;
        mainC.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        mainC.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        mainC.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        mainC.startColor = Color.yellow * 2f;
        mainC.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(chispas, 50f);
        Forma(chispas, ParticleSystemShapeType.Sphere, 0.2f, 1f);
        var renderC = chispas.GetComponent<ParticleSystemRenderer>();
        renderC.velocityScale = 0.05f;
        renderC.lengthScale = 1.2f;
        Desvanecer(chispas, 0.05f, 0.6f);
    }

    /// Aura Estelar: esfera cósmica concentrada, densa y mística, con halo de plasma, anillo orbital y estela cometa.
    private static void ConstruirAuraEstelar(GameObject root, Texturas t)
    {
        // 1. Cuerpo esférico cósmico denso (la esfera principal)
        var esfera = Sistema(root.transform, "Esfera cósmica", t.Mat("EsferaCosmica", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainE = esfera.main;
        mainE.startLifetime = 0.2f;
        mainE.startSize = 1.6f;
        mainE.startColor = Violeta * 2.4f;
        Emitir(esfera, 40f);
        Desvanecer(esfera, 0.05f, 0.75f);

        // 2. Núcleo interior concentrado de energía estelar amatista (sin blanco cegador)
        var nucleo = Sistema(root.transform, "Núcleo estelar", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainN = nucleo.main;
        mainN.startLifetime = 0.18f;
        mainN.startSize = 0.7f;
        mainN.startColor = BrilloLilaSuave * 1.5f;
        Emitir(nucleo, 30f);
        Desvanecer(nucleo, 0.05f, 0.65f);

        // 3. Corona / Halo esférico exterior radiante
        var halo = Sistema(root.transform, "Halo esférico", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainH = halo.main;
        mainH.startLifetime = 0.25f;
        mainH.startSize = 2.2f;
        mainH.startColor = new Color(0.48f, 0.18f, 0.90f, 0.45f);
        Emitir(halo, 25f);
        Desvanecer(halo, 0.08f, 0.6f);

        // 4. Anillos cósmicos orbitales (silueta del icono de Aura Estelar)
        var anillos = Sistema(root.transform, "Anillo cósmico", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainA = anillos.main;
        mainA.startLifetime = 0.28f;
        mainA.startSize = 2.1f;
        mainA.startColor = Lila * 1.8f;
        Emitir(anillos, 30f);
        Girar(anillos, 300f);
        Desvanecer(anillos, 0.06f, 0.65f);

        // 5. Motas estelares orbitando la esfera (violeta brillante y amatista, no blanco)
        var chispas = Sistema(root.transform, "Motas orbitales", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainC = chispas.main;
        mainC.startLifetime = 0.35f;
        mainC.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        mainC.startColor = Lila * 1.6f;
        Emitir(chispas, 35f);
        Forma(chispas, ParticleSystemShapeType.Sphere, 0.5f, 1f);
        Desvanecer(chispas, 0.05f, 0.65f);

        // 6. Estela de cometa cósmico en espacio de mundo
        var estela = Sistema(root.transform, "Estela de esfera", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainEst = estela.main;
        mainEst.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
        mainEst.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        mainEst.startColor = new ParticleSystem.MinMaxGradient(Lila * 1.6f, VioletaProfundo);
        mainEst.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(estela, 115f);
        Forma(estela, ParticleSystemShapeType.Sphere, 0.2f, 1f);
        var tamEst = estela.sizeOverLifetime;
        tamEst.enabled = true;
        tamEst.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(estela, 0.04f, 0.55f);
    }

    /// Detonación de Aura Estelar: explosión cósmica amatista, anillo de choque estelar y lluvia de estrellas púrpuras.
    private static void ConstruirImpactoAuraEstelar(GameObject root, Texturas t)
    {
        var anillo = Sistema(root.transform, "Onda estelar", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 0.8f, bucle: false);
        var mainA = anillo.main;
        mainA.startLifetime = 0.45f;
        mainA.startSize = 3.4f;
        mainA.startColor = Lila * 2.0f;
        Rafaga(anillo, 1);
        Crecer(anillo, 0.8f);
        Desvanecer(anillo, 0.08f, 0.7f);

        var halo = Sistema(root.transform, "Destello central", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 0.6f, bucle: false);
        var mainH = halo.main;
        mainH.startLifetime = 0.3f;
        mainH.startSize = 2.6f;
        mainH.startColor = BrilloLilaSuave * 1.6f;
        Rafaga(halo, 1);
        Desvanecer(halo, 0.05f, 0.75f);

        var estrellas = Sistema(root.transform, "Estrellas dispersas", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 0.8f, bucle: false);
        var mainE = estrellas.main;
        mainE.startLifetime = 0.45f;
        mainE.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
        mainE.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        mainE.startColor = new ParticleSystem.MinMaxGradient(Lila * 2.0f, Violeta * 1.5f);
        Rafaga(estrellas, 20);
        Forma(estrellas, ParticleSystemShapeType.Sphere, 0.25f, 1f);
        Desvanecer(estrellas, 0.05f, 0.7f);

        var humo = Sistema(root.transform, "Nebulosa residual", t.Mat("Humo", true), ParticleSystemRenderMode.Billboard, 0.9f, bucle: false);
        var mainHu = humo.main;
        mainHu.startLifetime = 0.65f;
        mainHu.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        mainHu.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        mainHu.startColor = new Color(0.35f, 0.10f, 0.65f, 0.5f);
        Rafaga(humo, 10);
        Forma(humo, ParticleSystemShapeType.Sphere, 0.4f, 1f);
        Desvanecer(humo, 0.1f, 0.6f);
    }

    /// Eco: ondas de choque psíquicas expansivas concéntricas que avanzan vibrando.
    private static void ConstruirEco(GameObject root, Texturas t)
    {
        var onda = Sistema(root.transform, "Onda de choque", t.Mat("OndaMental", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainO = onda.main;
        mainO.startLifetime = 0.28f;
        mainO.startSize = 2.4f;
        mainO.startColor = Lila * 2.0f;
        Emitir(onda, 35f);
        Crecer(onda, 0.65f);
        Desvanecer(onda, 0.05f, 0.7f);

        // Centro psíquico amatista concentrado (sin blanco quemado)
        Nucleo(root.transform, t, "Centro de eco", Lila * 1.4f, 0.6f);

        var anillos = Sistema(root.transform, "Anillos resonantes", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainA = anillos.main;
        mainA.startLifetime = 0.38f;
        mainA.startSize = 1.8f;
        mainA.startColor = Violeta * 1.6f;
        Emitir(anillos, 32f);
        Crecer(anillos, 0.85f);
        Desvanecer(anillos, 0.05f, 0.65f);

        Estela(root.transform, t, "Resonancia", VioletaProfundo * 1.2f, 0.6f, 85f);
    }

    /// Impacto de Eco: resonancia psíquica expansiva múltiple que estremece el área del impacto.
    private static void ConstruirImpactoEco(GameObject root, Texturas t)
    {
        var onda = Sistema(root.transform, "Onda expansiva", t.Mat("OndaMental", true), ParticleSystemRenderMode.Billboard, 0.7f, bucle: false);
        var mainO = onda.main;
        mainO.startLifetime = 0.4f;
        mainO.startSize = 3.0f;
        mainO.startColor = Lila * 2.2f;
        Rafaga(onda, 1);
        Crecer(onda, 0.9f);
        Desvanecer(onda, 0.06f, 0.7f);

        var anillo = Sistema(root.transform, "Anillo psíquico", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 0.7f, bucle: false);
        var mainA = anillo.main;
        mainA.startLifetime = 0.45f;
        mainA.startSize = 2.5f;
        mainA.startColor = Violeta * 1.8f;
        Rafaga(anillo, 2);
        Crecer(anillo, 0.8f);
        Desvanecer(anillo, 0.08f, 0.65f);

        var motas = Sistema(root.transform, "Motas de resonancia", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 0.7f, bucle: false);
        var mainM = motas.main;
        mainM.startLifetime = 0.4f;
        mainM.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        mainM.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        mainM.startColor = Lila * 1.6f;
        Rafaga(motas, 16);
        Forma(motas, ParticleSystemShapeType.Sphere, 0.2f, 1f);
        Desvanecer(motas, 0.05f, 0.65f);
    }

    /// Dardo Mental: aguja psíquica perforante diamantina con punta amatista brillante, anillos de distorsión y estela.
    private static void ConstruirDardoMental(GameObject root, Texturas t)
    {
        var aguja = Sistema(root.transform, "Aguja psíquica", t.Mat("AgujaMental", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainA = aguja.main;
        mainA.startLifetime = 0.16f;
        mainA.startSize = 2.2f;
        mainA.startColor = Lila * 2.2f;
        Emitir(aguja, 45f);
        Desvanecer(aguja, 0.05f, 0.75f);

        // Punta amatista estilizada y concentrada (sustituye el blanco quemado)
        Nucleo(root.transform, t, "Punta brillante", BrilloLilaSuave * 1.4f, 0.45f);

        var anillos = Sistema(root.transform, "Anillos mentales", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainAn = anillos.main;
        mainAn.startLifetime = 0.24f;
        mainAn.startSize = 1.2f;
        mainAn.startColor = Violeta * 1.7f;
        Emitir(anillos, 35f);
        Crecer(anillos, 0.55f);
        Desvanecer(anillos, 0.05f, 0.65f);

        var estela = Sistema(root.transform, "Rastro mental", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainE = estela.main;
        mainE.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        mainE.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        mainE.startColor = new ParticleSystem.MinMaxGradient(Lila * 1.6f, VioletaProfundo);
        mainE.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(estela, 100f);
        Forma(estela, ParticleSystemShapeType.Sphere, 0.1f, 1f);
        var tamE = estela.sizeOverLifetime;
        tamE.enabled = true;
        tamE.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(estela, 0.04f, 0.5f);
    }

    /// Impacto de Dardo Mental: perforación psíquica concentrada con destello amatista y esquirlas mentales.
    private static void ConstruirImpactoDardoMental(GameObject root, Texturas t)
    {
        var destello = Sistema(root.transform, "Punción mental", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 0.5f, bucle: false);
        var mainD = destello.main;
        mainD.startLifetime = 0.22f;
        mainD.startSize = 1.8f;
        mainD.startColor = BrilloLilaSuave * 1.8f;
        Rafaga(destello, 1);
        Desvanecer(destello, 0.04f, 0.75f);

        var anillo = Sistema(root.transform, "Anillo de perforación", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 0.5f, bucle: false);
        var mainA = anillo.main;
        mainA.startLifetime = 0.32f;
        mainA.startSize = 2.0f;
        mainA.startColor = Lila * 2.0f;
        Rafaga(anillo, 1);
        Crecer(anillo, 0.7f);
        Desvanecer(anillo, 0.06f, 0.65f);

        var esquirlas = Sistema(root.transform, "Esquirlas mentales", t.Mat("AgujaMental", true), ParticleSystemRenderMode.Billboard, 0.6f, bucle: false);
        var mainE = esquirlas.main;
        mainE.startLifetime = 0.35f;
        mainE.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.0f);
        mainE.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        mainE.startColor = Violeta * 2.0f;
        Rafaga(esquirlas, 12);
        Forma(esquirlas, ParticleSystemShapeType.Sphere, 0.15f, 1f);
        Desvanecer(esquirlas, 0.05f, 0.7f);
    }

    /// Sello del Pacto: círculo rúnico ceremonial concéntrico en el suelo, emanación de runas espectrales y velo de sombra.
    private static void ConstruirSelloZona(GameObject root, Texturas t)
    {
        var circulo = Sistema(root.transform, "Circulo Runico", t.Mat("CirculoRunico", true), ParticleSystemRenderMode.HorizontalBillboard, 30f);
        Unico(circulo, 30f, 7.5f, Violeta * 1.8f);
        Girar(circulo, -12f);

        var halo = Sistema(root.transform, "Velo del Pacto", t.Mat("Brillo", true), ParticleSystemRenderMode.HorizontalBillboard, 30f);
        Unico(halo, 30f, 8.5f, new Color(0.35f, 0.10f, 0.65f, 0.4f));

        var anilloInterior = Sistema(root.transform, "Anillo Interior", t.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 30f);
        Unico(anilloInterior, 30f, 4.5f, Lila * 1.6f);
        Girar(anilloInterior, 25f);

        var humo = Sistema(root.transform, "Emanacion sombria", t.Mat("Humo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainH = humo.main;
        mainH.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        mainH.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        mainH.startColor = new Color(0.20f, 0.04f, 0.35f, 0.75f);
        mainH.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(humo, 20f);
        Forma(humo, ParticleSystemShapeType.Circle, 3.2f, 0.1f);
        Velocidad(humo, new Vector3(0f, 0.8f, 0f));
        Desvanecer(humo, 0.2f, 0.7f);

        Motas(root.transform, t, "Runas flotantes", Lila * 1.5f, 3.2f, 25f, 1.5f);
    }

    /// Estrella Fugaz: cometa estelar imponente con núcleo radiante, destello en cruz y cola larga densa.
    private static void ConstruirEstrellaFugaz(GameObject root, Texturas t)
    {
        Nucleo(root.transform, t, "Núcleo", DoradoPalido * 2.2f, 1.4f);

        var estrella = Sistema(root.transform, "Estrella", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainE = estrella.main;
        mainE.startLifetime = 0.22f;
        mainE.startSize = 2.2f;
        mainE.startColor = Color.white * 2.5f;
        Emitir(estrella, 40f);
        Desvanecer(estrella, 0.08f, 0.7f);

        var halo = Sistema(root.transform, "Halo", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainH = halo.main;
        mainH.startLifetime = 0.25f;
        mainH.startSize = 3.2f;
        mainH.startColor = new Color(Dorado.r, Dorado.g, Dorado.b, 0.45f);
        Emitir(halo, 25f);
        Desvanecer(halo, 0.1f, 0.6f);

        var cola = Sistema(root.transform, "Cola de cometa", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainC = cola.main;
        mainC.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
        mainC.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        mainC.startColor = new ParticleSystem.MinMaxGradient(Color.white * 2f, Dorado * 1.6f);
        mainC.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(cola, 140f);
        Forma(cola, ParticleSystemShapeType.Sphere, 0.15f, 1f);
        var tamC = cola.sizeOverLifetime;
        tamC.enabled = true;
        tamC.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(cola, 0.04f, 0.45f);

        var chispas = Sistema(root.transform, "Chispas cometa", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainCh = chispas.main;
        mainCh.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        mainCh.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        mainCh.startColor = DoradoPalido * 1.8f;
        mainCh.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(chispas, 45f);
        Forma(chispas, ParticleSystemShapeType.Sphere, 0.35f, 1f);
        Velocidad(chispas, new Vector3(0.5f, 0.5f, 0.5f));
        Desvanecer(chispas, 0.05f, 0.6f);
    }

    /// Cada estrella del abanico: cuerpo estelar cristalino nítido, halo y estela visible.
    private static void ConstruirEstrellaDeAbanico(GameObject root, Texturas t)
    {
        Nucleo(root.transform, t, "Núcleo", Color.white * 2.2f, 1.0f);
        var estrella = Sistema(root.transform, "Estrella", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = estrella.main;
        main.startLifetime = 0.25f;
        main.startSize = 1.6f;
        main.startColor = DoradoPalido * 2.0f;
        Emitir(estrella, 40f);
        Desvanecer(estrella, 0.08f, 0.7f);

        var rastro = Sistema(root.transform, "Rastro estelar", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainR = rastro.main;
        mainR.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        mainR.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
        mainR.startColor = DoradoPalido * 1.6f;
        mainR.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(rastro, 90f);
        Forma(rastro, ParticleSystemShapeType.Sphere, 0.12f, 1f);
        var tamR = rastro.sizeOverLifetime;
        tamR.enabled = true;
        tamR.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(rastro, 0.05f, 0.5f);
    }

    /// El anillo de cada impacto señala el radio que alcanza esa estrella.
    private static void ConstruirImpactoDeEstrella(GameObject root, Texturas t)
    {
        var anillo = Sistema(root.transform, "Radio de impacto", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 0.8f, bucle: false);
        var main = anillo.main;
        main.startLifetime = 0.45f;
        main.startSize = 2.8f;
        main.startColor = DoradoPalido * 1.6f;
        Rafaga(anillo, 1);
        Crecer(anillo, 0.75f);
        Desvanecer(anillo, 0.1f, 0.65f);

        var estrellas = Sistema(root.transform, "Destellos", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 0.8f, bucle: false);
        main = estrellas.main;
        main.startLifetime = 0.38f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startColor = Color.white * 1.8f;
        Rafaga(estrellas, 16);
        Forma(estrellas, ParticleSystemShapeType.Sphere, 0.3f, 1f);
        Desvanecer(estrellas, 0.05f, 0.7f);
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

    // ── Guardado ──────────────────────────────────────────────────────────

    private static void Guardar(string nombre, Action<GameObject> construir, System.Text.StringBuilder log)
    {
        GuardarPrefab("VFX_Hechizo_" + nombre, Ruta(nombre), construir);
        log.AppendLine($"   VFX nuevo: {nombre}.");
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

    private static void ApagarHijosPorNombre(GameObject root, params string[] nombres)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform) continue;
            foreach (var n in nombres)
            {
                if (string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase))
                {
                    t.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                }
            }
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

    // ── Texturas ──────────────────────────────────────────────────────────

    /// Las genéricas de VfxProcedural más las propias de los efectos a medida de los hechizos.
    private static Texturas NuevasTexturas()
    {
        var t = new Texturas(CarpetaTexturas, CarpetaMateriales, "Hechizo");
        t.Registrar("CirculoRunico", 512, 512, Patrones.CirculoRunico);
        t.Registrar("Cadena", 64, 256, Patrones.Cadena);
        t.Registrar("AnilloDeCadena", 256, 256, Patrones.AnilloDeCadena);
        t.Registrar("Zarpazo", 256, 256, Patrones.Zarpazos);
        t.Registrar("Grieta", tx => Patrones.Grieta(tx, "Grieta", 512));
        t.Registrar("CuchillaViento", 256, 256, Patrones.CuchillaViento);
        t.Registrar("AgujaMental", 128, 256, Patrones.AgujaMental);
        t.Registrar("OndaMental", 256, 256, Patrones.OndaMental);
        t.Registrar("GarraEspectral", 256, 256, Patrones.GarraEspectral);
        t.Registrar("EsferaCosmica", 256, 256, Patrones.EsferaCosmica);
        return t;
    }

    /// Fórmulas de las texturas propias de los hechizos (coordenadas de -1 a 1 con el centro en 0).
    private static class Patrones
    {
        /// Círculo de pacto: dos anillos, marcas, estrella de cinco puntas y runas en sus vértices.
        public static float CirculoRunico(float x, float y)
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
        public static float Cadena(float x, float y)
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
        public static float AnilloDeCadena(float x, float y)
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
        public static float Zarpazos(float x, float y)
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

        /// Grietas que salen del centro y se ramifican (semilla fija: siempre la misma).
        public static Texture2D Grieta(Texturas t, string nombre, int n)
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
            return t.GuardarAlfa(nombre, n, n, a);
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

        /// Media luna / arco afilado de viento cortante.
        public static float CuchillaViento(float x, float y)
        {
            var p = new Vector2(x, y);
            float dExterior = (p - new Vector2(0f, -0.2f)).magnitude;
            float dInterior = (p - new Vector2(0f, -0.45f)).magnitude;
            float bordeExterior = Linea(dExterior - 0.75f, 0.08f);
            float recorteInterior = Mathf.Clamp01((dInterior - 0.70f) / 0.15f);
            float punta = Mathf.Clamp01((0.95f - Mathf.Abs(x)) / 0.25f);
            return Mathf.Clamp01(bordeExterior * recorteInterior * punta);
        }

        /// Aguja / diamante afilado psíquico para Dardo Mental.
        public static float AgujaMental(float x, float y)
        {
            float ancho = Mathf.Clamp01(1f - Mathf.Abs(y)) * 0.28f;
            float cuerpo = Mathf.Clamp01((ancho - Mathf.Abs(x)) / (ancho * 0.4f + 0.001f));
            float brilloEje = Gauss(x, 0.04f) * Mathf.Clamp01(1f - Mathf.Abs(y) * 0.8f);
            return Mathf.Clamp01(cuerpo * 0.6f + brilloEje * 0.9f);
        }

        /// Ondas concéntricas de resonancia psíquica para Eco.
        public static float OndaMental(float x, float y)
        {
            float r = R(x, y);
            float onda1 = Gauss(r - 0.85f, 0.05f);
            float onda2 = Gauss(r - 0.55f, 0.06f) * 0.75f;
            float onda3 = Gauss(r - 0.28f, 0.07f) * 0.5f;
            float adelante = Mathf.Clamp01((y + 0.6f) / 1.2f);
            return Mathf.Clamp01((onda1 + onda2 + onda3) * adelante);
        }

        /// Tres zarpazos de sombra espectral, densos y cortantes.
        public static float GarraEspectral(float x, float y)
        {
            var p = new Vector2(x, y);
            float a = 0f;
            for (int k = -1; k <= 1; k++)
            {
                float ox = k * 0.45f;
                var p0 = new Vector2(ox + 0.32f, 0.9f);
                var p1 = new Vector2(ox + 0.35f, 0f);
                var p2 = new Vector2(ox - 0.32f, -0.9f);
                float mejor = 10f, tMejor = 0f;
                for (int i = 0; i <= 40; i++)
                {
                    float t = i / 40f;
                    Vector2 q = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                    float d = (p - q).magnitude;
                    if (d < mejor) { mejor = d; tMejor = t; }
                }
                float grosor = 0.12f * Mathf.Sin(Mathf.PI * tMejor);
                a = Mathf.Max(a, Mathf.Clamp01((grosor - mejor) / 0.035f + 0.5f));
            }
            return a;
        }

        /// Esfera cósmica tridimensional: cuerpo esférico concentrado, núcleo luminoso y halo de borde Fresnel.
        public static float EsferaCosmica(float x, float y)
        {
            float r = R(x, y);
            if (r >= 1f) return 0f;
            float z = Mathf.Sqrt(1f - r * r);
            float cuerpo = Mathf.Pow(z, 0.65f);
            float borde = Mathf.Pow(1f - z, 2.2f) * 1.3f;
            return Mathf.Clamp01(cuerpo + borde);
        }
    }
}
