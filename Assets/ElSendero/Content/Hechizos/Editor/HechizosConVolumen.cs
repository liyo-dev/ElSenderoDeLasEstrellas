using System;
using System.Collections.Generic;
using System.Linq;
using Kits.StylizedVfx;
using Kits.StylizedVfx.Editor;
using UnityEditor;
using UnityEngine;
using R = Kits.StylizedVfx.Editor.EnergyRecipes;
using Look = Kits.StylizedVfx.Editor.StylizedVfxAssets.EnergyLook;

/// Los hechizos del grupo con la receta del prólogo («dibujo animado con volumen», INC-1136): cada
/// elemento con su paleta (Fuego, Viento, Luz, Mente y Oscuro) y cada efecto montado con las recetas
/// del kit StylizedVfx. Se rehacen EN SU SITIO los prefabs visuales de Assets/_VFX/Hechizos que usan
/// los hechizos (lanzamiento, proyectil, impacto, zona, estado, escudos): se conserva la raíz, así que
/// las variantes de Assets/_SPELLS/Prefabs (que llevan la lógica) siguen enganchadas. Ver INC-1140
public static class HechizosConVolumen
{
    private const string Carpeta = "Assets/_VFX/Hechizos";
    private const string CarpetaPropia = Carpeta + "/ConVolumen";
    private const string RutaEstrella = "Assets/_VFX/Mallas/VFX_Estrella4.fbx";
    private const string RutaBolita = "Assets/_VFX/Mallas/VFX_Bolita.fbx";
    private const string RutaPetalo = "Assets/_VFX/Mallas/VFX_Petalo.fbx";
    public const string RutaAturdidoMental = Carpeta + "/VFX_Hechizo_Mente_Estado_Aturdido.prefab";

    private static EnergyMeshes _m;
    private static readonly Dictionary<string, EnergyPalette> _p = new Dictionary<string, EnergyPalette>();

    [MenuItem("El Sendero/Magia/Hechizos con volumen (INC-1140)")]
    public static void Menu()
    {
        var log = new System.Text.StringBuilder();
        Construir(log);
        Debug.Log("[VFX] Hechizos con volumen: " + log);
    }

    public static void Construir(System.Text.StringBuilder log)
    {
        if (!Preparar()) return;
        var fuego = _p["Fuego"]; var viento = _p["Viento"]; var luz = _p["Luz"]; var mente = _p["Mente"]; var sombra = _p["Oscuro"];
        var polvo = _p["Polvo"]; var polvoOscuro = _p["PolvoOscuro"]; var blanco = _p["Blanco"];

        // Lanzar y desaparecer, por elemento.
        foreach (var (nombre, pal) in new[] { ("Fuego", fuego), ("Viento", viento), ("Luz", luz), ("Mente", mente), ("Oscuro", sombra) })
        {
            Rehacer("Destello_" + nombre, r => R.Flash(r, pal, _m, 0.3f, 6), log);
            Rehacer("DestelloFin_" + nombre, r => R.Flash(r, pal, _m, 0.22f, 4, 1.5f), log);
        }

        // Proyectiles, destellos de lanzar e impactos: cada hechizo con su firma (forma, recorrido y remate).
        Firmas(log);

        // Zonas: se montan a su radio real (el envoltorio no las escala).
        Rehacer("ChispaIgneaFuego_Dibujo_Zona", r => { R.MagicCircle(r, fuego, _m, 1.8f); Hijo(r, "Fuego", g => R.GroundFlames(g, fuego, _m, 1.6f, 0.6f, 18f)); }, log);
        Rehacer("MuroDeFuego_Dibujo_Zona", r => { R.MagicCircle(r, fuego, _m, 3f); Hijo(r, "Fuego", g => R.GroundFlames(g, fuego, _m, 2.8f, 1.2f, 40f)); }, log);
        Rehacer("TormentaDeFuego_Dibujo_Zona", r => { R.MagicCircle(r, fuego, _m, 6f); Hijo(r, "Lluvia", g => R.SkyRain(g, fuego, _m, 6f, 9f, 5f, 0.5f, _m.Sphere)); }, log);
        Rehacer("Remolino_Dibujo_Zona", r => { R.MagicCircle(r, viento, _m, 6f); Hijo(r, "Remolino", g => R.Funnel(g, viento, _m, 4f, 3f, 5)); }, log);
        Rehacer("BrisaSanadora_Dibujo_Zona", r =>
        {
            R.MagicCircle(r, viento, _m, 5f);
            Hijo(r, "Pétalos", g => R.RisingMotes(g, viento, _m, 4.5f, _m.Petal, 20f, 1.2f, 0.25f));
            Hijo(r, "Luces", g => R.RisingMotes(g, viento, _m, 4.5f, _m.Star, 14f, 1.6f, 0.1f));
        }, log);
        Rehacer("Meteoro_Dibujo_Zona", r => { R.MagicCircle(r, luz, _m, 4f); Hijo(r, "Meteoros", g => R.SkyRain(g, luz, _m, 4f, 10f, 3f, 0.9f, _m.Star)); }, log);
        Rehacer("NovaDeLuz_Dibujo_Zona", r => R.Nova(r, luz, _m, 5f), log);
        Rehacer("CupulaEstelar_Dibujo_Zona", r => R.Dome(r, luz, _m, 6.5f), log);
        Rehacer("SelloDelPacto_Dibujo_Zona", r => { R.MagicCircle(r, mente, _m, 4.5f); Hijo(r, "Runas", g => R.RisingMotes(g, mente, _m, 4f, _m.Star, 25f, 1.5f, 0.12f)); }, log);
        Rehacer("CadenasDelPacto_Dibujo_Zona", r => { R.MagicCircle(r, mente, _m, 4f); Hijo(r, "Eslabones", g => R.RisingMotes(g, mente, _m, 3.6f, _m.OrbitRing, 10f, 1.2f, 0.22f)); }, log);
        Rehacer("CadenasDelPacto_Dibujo_Estado", r => R.Chains(r, mente, _m, 1.4f, 0.55f, 10), log);
        Rehacer("JuicioDelPacto_Dibujo_Zona", r => { R.MagicCircle(r, mente, _m, 6f); Hijo(r, "Juicio", g => R.Pillar(g, blanco, mente, _m, 1f, 3.2f, 14f, true)); }, log);
        Rehacer("MagoOscuroGrieta_Dibujo_Zona", r => { R.MagicCircle(r, sombra, _m, 5.5f); Hijo(r, "Pinchos", g => R.Spikes(g, sombra, polvoOscuro, _m, 14, 4.5f, 1.2f, 1138)); }, log);

        // Levitación y paso sombrío.
        Rehacer("Levitation_Dibujo_Mantener", r => Levitacion(r, mente), log);
        Rehacer("Levitation_Dibujo_Soltar", r => R.Impact(r, mente, _m, 0.9f), log);
        Rehacer("PasoSombrio_Dibujo_Llegada", r => R.Implosion(r, mente, _m, 0.6f, polvoOscuro), log);

        // Hechizo que sale mal (petardea): bocanada de humo de dibujo, una onda y pocas chispas.
        // Lo usan las cinemáticas (Cap1_OliverSaludo). Ver INC-1153.
        Rehacer("Fallido", r => R.Impact(r, fuego, _m, new R.ImpactStyle { Scale = 1.3f, Waves = 1, Puffs = polvo, RisingStars = 3 }), log);

        // Escudos del grupo (se sostienen mientras se defiende) y contraataque.
        // Mismo centro y tamaño que tenían (la defensa se mide con ellos).
        Rehacer("Escudo_Will", r => R.Shield(r, _p["Cielo"], _m, 0.95f, 2.6f, true), log);
        Rehacer("Escudo_Npc", r => R.Shield(r, _p["Rosa"], _m, 0.95f, 2.6f, true), log);
        Rehacer("Escudo_Grupo", r => R.Shield(r, luz, _m, 0.95f, 2.6f, true), log);
        Rehacer("Escudo_Contraataque", r => R.Impact(r, luz, _m, 1.0f), log);

        Amanecer(log);

        // Estado del dardo mental (antes, el «Plexus» del paquete): estrellitas que dan vueltas sobre la cabeza.
        Rehacer("Mente_Estado_Aturdido", r => R.OrbitHalo(r, mente, _m, 1.55f, 0.35f, 4), log);
        AsignarEstadoDelDardo(log);

        AssetDatabase.SaveAssets();
    }

    /// Amanecer: un sol que viaja, su carga en las manos, el estallido y lo que queda sobre la
    /// protección abierta. Lo llama también AmanecerBuilder.
    public static void ConstruirAmanecer(System.Text.StringBuilder log)
    {
        if (!Preparar()) return;
        Amanecer(log);
        AssetDatabase.SaveAssets();
    }

    private static void Amanecer(System.Text.StringBuilder log)
    {
        var luz = _p["Luz"];
        Rehacer("Amanecer", r => R.Sun(r, luz, _m, 0.9f, 10, 4f), log);
        Rehacer("Amanecer_Carga", r => R.HandCharge(r, luz, _m, 0.18f), log);
        Rehacer("Amanecer_Impacto", r => R.Impact(r, luz, _m, 2.0f, null, _p["Polvo"]), log);
        Rehacer("Amanecer_ProteccionAbierta", r => ProteccionAbierta(r, luz), log);
    }

    // ── Para otros montajes (dúos y trío, vuelo) ────────────────────────────────────────────

    /// Prepara mallas y paletas. Devuelve false si falta algo (ya avisado en la consola).
    public static bool PrepararPaletas() => Preparar();

    /// Mallas preparadas por PrepararPaletas.
    public static EnergyMeshes Mallas => _m;

    /// Paleta por nombre (Fuego, Viento, Luz, Mente, Oscuro, Cielo, Rosa, Blanco, Polvo, PolvoOscuro).
    public static EnergyPalette Paleta(string nombre) => _p.TryGetValue(nombre, out var p) ? p : null;

    // ── Paletas y mallas ────────────────────────────────────────────────────────────────────

    private static bool Preparar()
    {
        StylizedVfxAssets.EnsureFolder(CarpetaPropia);
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var esfera = tmp.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(tmp);
        _m = new EnergyMeshes
        {
            Star = Malla(RutaEstrella), Puff = Malla(RutaBolita), Petal = Malla(RutaPetalo), Sphere = esfera,
            Ring = StylizedVfxAssets.Torus(CarpetaPropia + "/Mallas/Aro.asset", 1f, 0.03f, 64, 10),
            OrbitRing = StylizedVfxAssets.Torus(CarpetaPropia + "/Mallas/AroDeOrbe.asset", 1f, 0.065f, 48, 10),
            Spike = StylizedVfxAssets.Cone(CarpetaPropia + "/Mallas/Pincho.asset", 0.18f, 1f, 6),
            Crystal = StylizedVfxMeshes.Crystal(CarpetaPropia + "/Mallas/Cristal.asset", 0.32f, 1f, 6),
            Arc = StylizedVfxMeshes.Arc(CarpetaPropia + "/Mallas/MediaLuna.asset", 0.5f, 0.09f, 140f),
        };
        if (_m.Star == null || _m.Puff == null || _m.Petal == null)
        {
            Debug.LogError("[VFX] Faltan mallas de estrella, bolita o pétalo en Assets/_VFX/Mallas.");
            return false;
        }
        _p.Clear();
        Paleta("Fuego", new Color(2.4f, 1.9f, 0.9f), new Color(1.8f, 0.65f, 0.12f), new Color(0.9f, 0.15f, 0.05f), new Color(2.2f, 1.6f, 0.3f), new Color(1f, 0.6f, 0.3f));
        Paleta("Viento", new Color(1.9f, 2.2f, 2.1f), new Color(0.5f, 1.35f, 1.0f), new Color(0.12f, 0.5f, 0.45f), new Color(1.7f, 2.0f, 2.1f), new Color(0.6f, 1f, 0.85f));
        Paleta("Luz", new Color(2.2f, 2.1f, 1.8f), new Color(1.5f, 0.9f, 0.18f), new Color(1.0f, 0.45f, 0.1f), new Color(0.35f, 1.2f, 1.7f), new Color(1f, 0.93f, 0.8f));
        Paleta("Mente", new Color(2.1f, 1.9f, 2.4f), new Color(1.0f, 0.75f, 1.9f), new Color(0.4f, 0.25f, 0.95f), new Color(1.9f, 0.9f, 1.7f), new Color(0.8f, 0.65f, 1f));
        Paleta("Oscuro", new Color(1.3f, 0.45f, 1.9f), new Color(0.16f, 0.06f, 0.28f), new Color(0.05f, 0.02f, 0.09f), new Color(1.7f, 0.3f, 1.4f), new Color(0.85f, 0.35f, 1f), corteDelBrillo: 0.82f);
        Paleta("Cielo", new Color(2.2f, 2.3f, 2.4f), new Color(0.55f, 1.1f, 1.9f), new Color(0.2f, 0.4f, 0.9f), new Color(1.6f, 2.0f, 2.3f), new Color(0.6f, 0.8f, 1f));
        Paleta("Rosa", new Color(2.3f, 2.0f, 2.1f), new Color(1.8f, 0.8f, 1.0f), new Color(0.9f, 0.3f, 0.45f), new Color(2.1f, 1.6f, 1.8f), new Color(1f, 0.7f, 0.75f));
        Paleta("Blanco", new Color(3f, 3f, 3f), new Color(2.2f, 2.1f, 2.0f), new Color(1.5f, 1.35f, 1.6f), new Color(1.5f, 1.6f, 2.2f), Color.white);
        Paleta("Polvo", new Color(0.95f, 0.9f, 0.8f), new Color(0.78f, 0.7f, 0.58f), new Color(0.55f, 0.45f, 0.38f), new Color(0.78f, 0.7f, 0.58f), Color.white, sinBorde: true);
        Paleta("PolvoOscuro", new Color(0.5f, 0.38f, 0.6f), new Color(0.25f, 0.18f, 0.32f), new Color(0.11f, 0.07f, 0.15f), new Color(0.25f, 0.18f, 0.32f), Color.white, sinBorde: true);
        return _p.Values.All(p => p.Energy != null && p.Piece != null && p.Spark != null && p.Shell != null);
    }

    private static void Paleta(string nombre, Color brillo, Color cuerpo, Color sombra, Color borde, Color luz, bool sinBorde = false, float corteDelBrillo = 0.6f)
    {
        string m = $"{CarpetaPropia}/Materiales/{nombre}_";
        _p[nombre] = new EnergyPalette
        {
            Name = nombre, Light = luz,
            Energy = StylizedVfxAssets.EnergyMaterial(m + "Energia.mat",
                new Look(brillo, cuerpo, sombra, borde, coreBand: corteDelBrillo, shadowBand: -0.2f, rimBand: sinBorde ? 1.01f : 0.72f, noiseAmount: 0.12f, noiseScale: 9f)),
            Piece = StylizedVfxAssets.EnergyMaterial(m + "Pieza.mat",
                new Look(brillo, cuerpo, sombra, borde, coreBand: 0.75f, shadowBand: -0.3f, rimBand: 1.01f, noiseAmount: 0.03f, noiseScale: 20f)),
            Spark = StylizedVfxAssets.EnergyMaterial(m + "Chispa.mat",
                new Look(brillo, sinBorde ? cuerpo : Color.Lerp(cuerpo, brillo, 0.35f), cuerpo, cuerpo, coreBand: 0.25f, shadowBand: -0.6f, rimBand: 1.01f, noiseAmount: 0f)),
            Shell = StylizedVfxAssets.ShellMaterial(m + "Burbuja.mat",
                new Look(brillo, cuerpo, sombra, borde, coreBand: 0.82f, rimBand: 0.75f, noiseAmount: 0.08f, noiseScale: 4f), 0.1f, 0.3f, 0.45f),
        };
    }

    private static Mesh Malla(string ruta) => AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Mesh>().FirstOrDefault();

    // ── Montajes propios ────────────────────────────────────────────────────────────────────

    private static void Proyectil(string hechizo, EnergyPalette p, R.ProjectileStyle s, System.Text.StringBuilder log)
        => Rehacer(hechizo + "_Dibujo", r => R.Projectile(r, p, _m, s), log);

    private static void Impacto(string hechizo, EnergyPalette p, float escala, Mesh piezas, EnergyPalette bolas, System.Text.StringBuilder log)
        => Rehacer(hechizo + "_Dibujo_Impacto", r => R.Impact(r, p, _m, escala, piezas, bolas), log);

    /// Lo que rodea a quien levita: dos anillos que vuelcan y luces que suben.
    private static void Levitacion(GameObject r, EnergyPalette mente)
    {
        var giros = new List<Kits.StylizedVfx.Spin>();
        var rs = new List<Renderer>();
        R.OrbitingRing(r.transform, mente, _m, "A", 0.65f, 70f, 0f, 90f, 220f, 0.06f, giros, rs);
        R.OrbitingRing(r.transform, mente, _m, "B", 0.7f, 50f, 120f, -80f, -200f, 0.06f, giros, rs);
        Hijo(r, "Luces", g => R.RisingMotes(g, mente, _m, 0.45f, _m.Star, 10f, 0.8f, 0.08f));
        R.Pulse(r, rs.ToArray(), StylizedVfxBuilder.PointLight(r.transform, mente.Light), giros.ToArray(), 1.4f, 2f, 0.7f, 0.35f, 0.2f);
    }

    /// Sobre la protección abierta de un jefe: tres anillos de luz que la rodean y estrellitas que suben.
    private static void ProteccionAbierta(GameObject r, EnergyPalette luz)
    {
        var giros = new List<Kits.StylizedVfx.Spin>();
        var rs = new List<Renderer>();
        var g = StylizedVfxBuilder.Group(r.transform, "Anillos", new Vector3(0f, 1.2f, 0f));
        StylizedVfxBuilder.Animate(g, StylizedVfxBuilder.PopIn(), 0.35f);
        R.OrbitingRing(g, luz, _m, "A", 1.3f, 70f, 0f, 60f, 160f, 0.1f, giros, rs, _m.Ring);
        R.OrbitingRing(g, luz, _m, "B", 1.35f, 40f, 90f, -50f, -140f, 0.1f, giros, rs, _m.Ring);
        R.OrbitingRing(g, luz, _m, "C", 1.4f, 15f, 200f, 70f, 180f, 0.1f, giros, rs, _m.Ring);
        Hijo(r, "Estrellitas", h => R.RisingMotes(h, luz, _m, 1.2f, _m.Star, 16f, 1.4f, 0.1f));
        R.Pulse(r, rs.ToArray(), StylizedVfxBuilder.PointLight(g, luz.Light), giros.ToArray(), 2f, 4f, 0.8f, 0.3f, 0.2f);
    }

    // ── Firma de cada proyectil ─────────────────────────────────────────────────────────────

    /// Cada proyectil se reconoce por su forma y su recorrido, no solo por el color: proyectil,
    /// destello de lanzar propio (se asigna como spawnVFX del hechizo) e impacto propio. Ver INC-1154
    private static void Firmas(System.Text.StringBuilder log)
    {
        var fuego = _p["Fuego"]; var viento = _p["Viento"]; var luz = _p["Luz"]; var mente = _p["Mente"]; var sombra = _p["Oscuro"];
        var polvo = _p["Polvo"]; var polvoOscuro = _p["PolvoOscuro"]; var cielo = _p["Cielo"]; var rosa = _p["Rosa"];
        var arcoiris = new[] { fuego, viento, cielo, mente, rosa };

        // Will. Llama Astral: su primera magia, fuego con chispas de estrella azul; tiembla como una llama.
        Firma("LlamaAstral", fuego,
            new R.ProjectileStyle { Core = 0.3f, Flames = true, TrailMesh = _m.Star, TrailColors = new[] { cielo },
                                    Wobble = WobbleMode.Corkscrew, WobbleAmplitude = 0.05f, WobbleFrequency = 7f },
            new R.CastStyle { Size = 0.28f, Rings = 1, Sparks = 8, Colors = new[] { fuego, cielo } },
            new R.ImpactStyle { Scale = 0.9f, Debris = _m.Spike, RisingStars = 4 }, log);
        // Bola Prisma: un cristal que gira y separa la luz en colores; se rompe en esquirlas.
        Firma("BolaPrisma", luz,
            new R.ProjectileStyle { Core = 0.42f, Shape = R.ProjectileShape.Crystal, Rings = 1, TrailColors = arcoiris, TrailRate = 10f, Light = 2.6f },
            new R.CastStyle { Size = 0.3f, Sparks = 10, SparkMesh = _m.Crystal, Colors = arcoiris },
            new R.ImpactStyle { Scale = 1.1f, Shards = arcoiris, Waves = 1 }, log);
        // Estrella Fugaz: punta brillante con cola larga y fina; acaba en un destello en cruz.
        Firma("EstrellaFugaz", luz,
            new R.ProjectileStyle { Core = 0.22f, StarCore = true, CometTail = 1.6f, TrailMesh = _m.Star, TrailRate = 26f },
            new R.CastStyle { Size = 0.22f, Sparks = 5, Slashes = 1 },
            new R.ImpactStyle { Scale = 1.0f, Cross = true }, log);
        // Lluvia de Chispas: estrellitas que bailan en el aire, cada una a su ritmo.
        Firma("LluviaDeChispas", luz,
            new R.ProjectileStyle { Core = 0.15f, StarCore = true, TrailMesh = _m.Star, TrailRate = 14f,
                                    Wobble = WobbleMode.Serpentine, WobbleAmplitude = 0.28f, WobbleFrequency = 2.2f },
            new R.CastStyle { Size = 0.24f, Sparks = 9 },
            new R.ImpactStyle { Scale = 0.6f, Waves = 1, RisingStars = 3 }, log);
        // Corazón Estelar: un corazón de luz que late al viajar y se abre en ondas, como un latido.
        Firma("CorazonEstelar", luz,
            new R.ProjectileStyle { Core = 0.62f, Shape = R.ProjectileShape.Heart, Rings = 2, TrailMesh = _m.Star, TrailColors = new[] { rosa }, Light = 3.5f },
            new R.CastStyle { Size = 0.4f, Rings = 2, Sparks = 10, Colors = new[] { luz, rosa } },
            new R.ImpactStyle { Scale = 1.7f, Waves = 3, RisingStars = 10, Shards = new[] { rosa, luz } }, log);

        // Estela. Bola de Fuego: el clásico, grande y contundente; tiembla y suelta humo de dibujo.
        Firma("BolaFuego", fuego,
            new R.ProjectileStyle { Core = 0.52f, Flames = true, TrailMesh = _m.Puff, TrailRate = 9f, Light = 3.2f,
                                    Wobble = WobbleMode.Corkscrew, WobbleAmplitude = 0.04f, WobbleFrequency = 9f },
            new R.CastStyle { Size = 0.36f, Cones = 8, Rings = 1 },
            new R.ImpactStyle { Scale = 1.5f, Debris = _m.Spike, Puffs = polvo, Waves = 2 }, log);
        // Chispa Ígnea: una chispa pequeña que va a botes.
        Firma("ChispaIgnea", fuego,
            new R.ProjectileStyle { Core = 0.18f, StarCore = true, Flames = true, TrailMesh = _m.Star,
                                    Wobble = WobbleMode.Hop, WobbleAmplitude = 0.35f, WobbleFrequency = 2.6f },
            new R.CastStyle { Size = 0.2f, Sparks = 8 },
            new R.ImpactStyle { Scale = 0.7f, Debris = _m.Spike }, log);
        // Ráfaga: media luna de viento, ancha y plana, que corta el aire y suelta hojas.
        Firma("Rafaga", viento,
            new R.ProjectileStyle { Core = 1.0f, Shape = R.ProjectileShape.Crescent, TrailMesh = _m.Petal, TrailRate = 8f },
            new R.CastStyle { Size = 0.26f, Slashes = 2, Sparks = 5, SparkMesh = _m.Petal },
            new R.ImpactStyle { Scale = 1.0f, Marks = 2, Debris = _m.Petal }, log);
        // Ciclón: torbellino pequeño que avanza en zigzag.
        Rehacer("Tornado_Dibujo", r =>
        {
            Hijo(r, "Vaivén", g => R.Funnel(g, viento, _m, 0.6f, 1.3f, 3, 420f));
            r.transform.Find("Vaivén").gameObject.AddComponent<PathWobble>().Configure(WobbleMode.Zigzag, 0.45f, 1.4f, true);
            Bajar(r, 0.65f);
        }, log);
        Rehacer("Tornado_Dibujo_Lanzar", r => R.Cast(r, viento, _m, new R.CastStyle { Size = 0.26f, Rings = 1, Sparks = 6, SparkMesh = _m.Petal }), log);
        Rehacer("Tornado_Dibujo_Impacto", r => R.Impact(r, viento, _m, new R.ImpactStyle { Scale = 1.3f, Waves = 2, Debris = _m.Petal, Puffs = polvo }), log);
        AsignarLanzar("Tornado", log);

        // Liam. Dardo Mental: aguja fina, casi instantánea; pincha con un destello en cruz.
        Firma("DardoMental", mente,
            new R.ProjectileStyle { Core = 0.16f, Shape = R.ProjectileShape.Needle, TrailRate = 22f },
            new R.CastStyle { Size = 0.18f, Rings = 1 },
            new R.ImpactStyle { Scale = 0.6f, Cross = true }, log);
        // Eco: un aro seguido de dos copias más débiles; al dar, tres ondas seguidas.
        Firma("Eco", mente,
            new R.ProjectileStyle { Core = 0.16f, Echoes = 3, TrailMesh = _m.OrbitRing, TrailRate = 5f },
            new R.CastStyle { Size = 0.22f, Rings = 3 },
            new R.ImpactStyle { Scale = 0.9f, Waves = 3, Debris = _m.OrbitRing }, log);
        // Aura Estelar: bola lenta con cuentas en órbita, que se dispersan al dar.
        Firma("AuraEstelar", mente,
            new R.ProjectileStyle { Core = 0.34f, Rings = 1, Beads = 4, TrailMesh = _m.Star },
            new R.CastStyle { Size = 0.3f, Sparks = 8 },
            new R.ImpactStyle { Scale = 1.1f, Debris = _m.Sphere, RisingStars = 6 }, log);
        // Garra del Pacto: tres tajos curvos que viajan juntos y dejan tres marcas.
        Firma("GarraDelPacto", mente,
            new R.ProjectileStyle { Core = 0.75f, Shape = R.ProjectileShape.Claws, TrailRate = 10f },
            new R.CastStyle { Size = 0.26f, Slashes = 3, VerticalSlashes = true },
            new R.ImpactStyle { Scale = 1.1f, Marks = 3, Debris = _m.Spike }, log);

        // Mago Oscuro (arsenal enemigo): se queda como estaba.
        Proyectil("MagoOscuroGolpe", sombra, new R.ProjectileStyle { Core = 0.32f, Rings = 2 }, log);
        Impacto("MagoOscuroGolpe", sombra, 1.1f, null, polvoOscuro, log);

        Huracan(viento, polvo, log);
    }

    private static void Firma(string hechizo, EnergyPalette p, R.ProjectileStyle proyectil, R.CastStyle lanzar, R.ImpactStyle impacto,
                              System.Text.StringBuilder log)
    {
        Rehacer(hechizo + "_Dibujo", r => R.Projectile(r, p, _m, proyectil), log);
        Rehacer(hechizo + "_Dibujo_Lanzar", r => R.Cast(r, p, _m, lanzar), log);
        Rehacer(hechizo + "_Dibujo_Impacto", r => R.Impact(r, p, _m, impacto), log);
        AsignarLanzar(hechizo, log);
    }

    /// El hechizo sale con su propio destello en vez del de su elemento.
    private static void AsignarLanzar(string hechizo, System.Text.StringBuilder log)
    {
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"Assets/_SPELLS/{hechizo}.asset");
        var lanzar = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/VFX_Hechizo_{hechizo}_Dibujo_Lanzar.prefab");
        if (spell == null || lanzar == null || spell.spawnVFX == lanzar) return;
        spell.spawnVFX = lanzar;
        EditorUtility.SetDirty(spell);
        log.Append("(").Append(hechizo).Append(": destello propio) ");
    }

    /// Huracán: un tornado grande que avanza serpenteando, con hojas que giran y polvo en la base.
    /// Tenía el prefab del paquete como hechizo; ahora tiene su prefab de lógica, copia del de Ciclón.
    private static void Huracan(EnergyPalette viento, EnergyPalette polvo, System.Text.StringBuilder log)
    {
        Rehacer("Huracan_Dibujo", r =>
        {
            Hijo(r, "Vaivén", g =>
            {
                R.Funnel(g, viento, _m, 1.2f, 2.8f, 6, 300f);
                Hijo(g, "Hojas", h => R.RisingMotes(h, viento, _m, 1.1f, _m.Petal, 28f, 1.8f, 0.2f));
            });
            r.transform.Find("Vaivén").gameObject.AddComponent<PathWobble>().Configure(WobbleMode.Serpentine, 0.5f, 0.7f, true);
            Hijo(r, "Polvo", g => R.RisingMotes(g, polvo, _m, 1.3f, _m.Puff, 10f, 0.4f, 0.3f));
            Bajar(r, 0.65f);
        }, log);
        Rehacer("Huracan_Dibujo_Lanzar", r => R.Cast(r, viento, _m, new R.CastStyle { Size = 0.34f, Rings = 2, Slashes = 2, Sparks = 8, SparkMesh = _m.Petal }), log);
        Rehacer("Huracan_Dibujo_Impacto", r => R.Impact(r, viento, _m, new R.ImpactStyle { Scale = 1.7f, Waves = 3, Debris = _m.Petal, Puffs = polvo }), log);

        const string logica = "Assets/_SPELLS/Prefabs/Huracan.prefab";
        const string ciclon = "Assets/_SPELLS/Prefabs/Tornado.prefab";
        var visual = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/VFX_Hechizo_Huracan_Dibujo.prefab");
        if (visual == null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(logica) == null && !AssetDatabase.CopyAsset(ciclon, logica))
        {
            Debug.LogError("[VFX] No se pudo crear " + logica);
            return;
        }
        var raiz = PrefabUtility.LoadPrefabContents(logica);
        try
        {
            raiz.name = "Huracan";
            for (int i = raiz.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(raiz.transform.GetChild(i).gameObject);
            PrefabUtility.InstantiatePrefab(visual, raiz.transform);
            PrefabUtility.SaveAsPrefabAsset(raiz, logica);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }

        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>("Assets/_SPELLS/Huracan.asset");
        if (spell == null) return;
        spell.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(logica);
        spell.visualRotationOffsetEuler = Vector3.zero;
        spell.spawnVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/VFX_Hechizo_Huracan_Dibujo_Lanzar.prefab");
        spell.impactVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/VFX_Hechizo_Huracan_Dibujo_Impacto.prefab");
        spell.despawnVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/VFX_Hechizo_DestelloFin_Viento.prefab");
        EditorUtility.SetDirty(spell);
        log.Append("(Huracán: prefab propio) ");
    }

    // ── Ayudas ──────────────────────────────────────────────────────────────────────────────

    private static void Rehacer(string nombre, Action<GameObject> montar, System.Text.StringBuilder log)
    {
        string ruta = $"{Carpeta}/VFX_Hechizo_{nombre}.prefab";
        R.RebuildInPlace(ruta, "VFX_Hechizo_" + nombre, montar);
        log.Append(nombre).Append(' ');
    }

    /// Monta una receta dentro de un hijo (cada receta pone su propio EnergyPulse en la raíz que recibe).
    private static void Hijo(GameObject r, string nombre, Action<GameObject> montar)
    {
        var g = new GameObject(nombre);
        g.transform.SetParent(r.transform, false);
        montar(g);
    }

    private static void Bajar(GameObject r, float metros)
    {
        foreach (Transform h in r.transform) h.localPosition += Vector3.down * metros;
    }

    /// El dardo mental deja su estado con la pieza nueva en vez del aura «Plexus» del paquete.
    private static void AsignarEstadoDelDardo(System.Text.StringBuilder log)
    {
        var dardo = AssetDatabase.LoadAssetAtPath<MagicSpellSO>("Assets/_SPELLS/DardoMental.asset");
        var estado = AssetDatabase.LoadAssetAtPath<GameObject>(RutaAturdidoMental);
        if (dardo == null || estado == null) return;
        if (dardo.statusVFX == estado) return;
        dardo.statusVFX = estado;
        EditorUtility.SetDirty(dardo);
        log.Append("(DardoMental: estado nuevo) ");
    }
}
