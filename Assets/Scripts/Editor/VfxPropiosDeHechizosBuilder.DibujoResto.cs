using UnityEngine;
using static VfxProcedural;

/// Nombres de los efectos de hechizo y ayudas de piezas de dibujo planas que siguen usando los efectos
/// de combate (enemigos) y la conversión del resto del juego. Los hechizos del grupo ya no se montan
/// aquí: los hace HechizosConVolumen con el kit StylizedVfx. Ver INC-1127 e INC-1140
public static partial class VfxPropiosDeHechizosBuilder
{
    // Proyectiles e impactos
    public const string BolaFuegoDibujo = "BolaFuego_Dibujo";
    public const string BolaFuegoDibujoImpacto = "BolaFuego_Dibujo_Impacto";
    public const string ChispaIgneaDibujo = "ChispaIgnea_Dibujo";
    public const string ChispaIgneaDibujoImpacto = "ChispaIgnea_Dibujo_Impacto";
    public const string CorazonEstelarDibujo = "CorazonEstelar_Dibujo";
    public const string CorazonEstelarDibujoImpacto = "CorazonEstelar_Dibujo_Impacto";
    public const string EstrellaFugazDibujo = "EstrellaFugaz_Dibujo";
    public const string EstrellaFugazDibujoImpacto = "EstrellaFugaz_Dibujo_Impacto";
    public const string LluviaDeChispasDibujo = "LluviaDeChispas_Dibujo";
    public const string LluviaDeChispasDibujoImpacto = "LluviaDeChispas_Dibujo_Impacto";
    public const string TornadoDibujo = "Tornado_Dibujo";
    public const string TornadoDibujoImpacto = "Tornado_Dibujo_Impacto";
    public const string AuraEstelarDibujo = "AuraEstelar_Dibujo";
    public const string AuraEstelarDibujoImpacto = "AuraEstelar_Dibujo_Impacto";
    public const string DardoMentalDibujo = "DardoMental_Dibujo";
    public const string DardoMentalDibujoImpacto = "DardoMental_Dibujo_Impacto";
    public const string EcoDibujo = "Eco_Dibujo";
    public const string EcoDibujoImpacto = "Eco_Dibujo_Impacto";
    public const string GarraDibujo = "GarraDelPacto_Dibujo";
    public const string GarraDibujoImpacto = "GarraDelPacto_Dibujo_Impacto";
    public const string MagoOscuroGolpeDibujo = "MagoOscuroGolpe_Dibujo";
    public const string MagoOscuroGolpeDibujoImpacto = "MagoOscuroGolpe_Dibujo_Impacto";

    // Zonas y estados
    public const string ChispaIgneaFuegoDibujoZona = "ChispaIgneaFuego_Dibujo_Zona";
    public const string MuroDeFuegoDibujoZona = "MuroDeFuego_Dibujo_Zona";
    public const string TormentaDeFuegoDibujoZona = "TormentaDeFuego_Dibujo_Zona";
    public const string RemolinoDibujoZona = "Remolino_Dibujo_Zona";
    public const string BrisaSanadoraDibujoZona = "BrisaSanadora_Dibujo_Zona";
    public const string MeteoroDibujoZona = "Meteoro_Dibujo_Zona";
    public const string NovaDeLuzDibujoZona = "NovaDeLuz_Dibujo_Zona";
    public const string CupulaEstelarDibujoZona = "CupulaEstelar_Dibujo_Zona";
    public const string SelloDibujoZona = "SelloDelPacto_Dibujo_Zona";
    public const string CadenasDibujoZona = "CadenasDelPacto_Dibujo_Zona";
    public const string CadenasDibujoEstado = "CadenasDelPacto_Dibujo_Estado";
    public const string JuicioDibujoZona = "JuicioDelPacto_Dibujo_Zona";
    public const string GrietaDibujoZona = "MagoOscuroGrieta_Dibujo_Zona";

    // Levitación y paso sombrío
    public const string LevitacionDibujoMantener = "Levitation_Dibujo_Mantener";
    public const string LevitacionDibujoSoltar = "Levitation_Dibujo_Soltar";
    public const string PasoSombrioDibujoLlegada = "PasoSombrio_Dibujo_Llegada";

    // Tintes de las piezas crema
    private static readonly Color TFuego = new Color(1f, 0.55f, 0.28f);
    private static readonly Color TOro = new Color(1f, 0.74f, 0.30f);   // cálido: con la noche no vira a limón
    private static readonly Color TMente = new Color(0.66f, 0.54f, 0.98f);
    private static readonly Color TLila = new Color(0.86f, 0.80f, 1f);
    private static readonly Color TMenta = new Color(0.66f, 0.93f, 0.84f);
    private static readonly Color TRosa = new Color(1f, 0.62f, 0.70f);
    private static readonly Color TSombra = new Color(0.30f, 0.20f, 0.42f);
    private static readonly Color TOscuro = new Color(0.22f, 0.10f, 0.14f);
    private static readonly Color TRojo = new Color(0.90f, 0.28f, 0.22f);

    // ── Ayudas ───────────────────────────────────────────────────────────

    private static ParticleSystem Tintar(ParticleSystem ps, Color c)
    {
        var main = ps.main;
        main.startColor = c;
        var render = ps.GetComponent<ParticleSystemRenderer>();
        if (EsLuz3D(render)) render.sharedMaterial = Material3D(c == Color.white ? TOro : c);
        var resplandor = ps.transform.Find(NombreDelResplandor);
        if (resplandor != null)
        {
            var mr = resplandor.GetComponent<ParticleSystem>().main;
            mr.startColor = ColorDelResplandor(c == Color.white ? TOro : c);
        }
        return ps;
    }

    /// El cuerpo de un proyectil: copias de una pieza que se relevan deprisa y tiemblan.
    private static ParticleSystem Cuerpo(Transform p, Texturas t, string nombre, string pieza, float tam, Color c, float giro = 10f)
    {
        var ps = Pieza(p, t, nombre, pieza);
        var m = ps.main;
        m.startLifetime = 0.16f;
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 0.85f, tam);
        m.startColor = c;
        m.maxParticles = 4;
        Emitir(ps, 14f);
        GiroAlAzar(ps, giro);
        Golpe(ps, 0.2f, 0.7f);
        Resplandor(ps, t, tam * 1.8f, 1f, true);
        Tintar(ps, c);
        return ps;
    }

    /// Piezas que se quedan atrás en el mundo y se encogen.
    private static ParticleSystem Rastro(Transform p, Texturas t, string nombre, string pieza, float tam, float ritmo, Color c, float giro = 25f)
    {
        var ps = Pieza(p, t, nombre, pieza);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 0.55f, tam);
        m.startColor = c;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(ps, ritmo);
        Forma(ps, ParticleSystemShapeType.Sphere, 0.15f, 1f);
        GiroAlAzar(ps, giro);
        var tam2 = ps.sizeOverLifetime;
        tam2.enabled = true;
        tam2.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
        return ps;
    }

    /// Una pieza tumbada en el suelo que dura toda la zona: aparece con un rebote y puede girar.
    private static ParticleSystem Suelo(Transform p, Texturas t, string nombre, string pieza, float tam, Color c, float giro, float altura = 0.04f)
    {
        var ps = Sistema(p, nombre, t.Mat("Dibujo_" + pieza, false), ParticleSystemRenderMode.HorizontalBillboard, 30f);
        ps.transform.localPosition = Vector3.up * altura;
        Unico(ps, 30f, tam, c);
        if (giro != 0f) Girar(ps, giro);
        var tam2 = ps.sizeOverLifetime;
        tam2.enabled = true;
        tam2.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.006f, 1.12f),
                                                                           new Keyframe(0.01f, 1f), new Keyframe(1f, 1f)));
        return ps;
    }

    /// Piezas que brotan sin parar repartidas por el círculo de la zona.
    private static ParticleSystem Brotes(Transform p, Texturas t, string nombre, string pieza, float radio, float grosor, float ritmo,
                                         float tam, float vida, Color c, float subida = 0f)
    {
        var ps = Pieza(p, t, nombre, pieza);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.7f, vida);
        m.startSize = new ParticleSystem.MinMaxCurve(tam * 0.6f, tam);
        m.startColor = c;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(ps, ritmo);
        Forma(ps, ParticleSystemShapeType.Circle, radio, grosor);
        GiroAlAzar(ps, 12f);
        if (subida != 0f) Velocidad(ps, new Vector3(0f, subida, 0f));
        Golpe(ps, 0.15f, 0.6f);
        return ps;
    }

    /// Un Pop dentro de una zona: la zona pone sus sistemas en bucle, así que se alarga su ciclo para
    /// que salga una sola vez.
    private static ParticleSystem UnaVez(ParticleSystem ps)
    {
        var m = ps.main;
        m.duration = 30f;
        return ps;
    }

    // ── Fuego ────────────────────────────────────────────────────────────






    // ── Viento ───────────────────────────────────────────────────────────




    // ── Luz ──────────────────────────────────────────────────────────────




    private static void ImpactoDeEstrellas(GameObject root, Texturas t, Color c, float escala)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", 1.5f * escala, 0.18f, 1f), c);
        Tintar(Pop(root.transform, t, "Onda", "anillo", 1.2f * escala, 0.32f, 2.2f), c);
        Tintar(Estallido(root.transform, t, "Estrellas", "estrella", Mathf.RoundToInt(10 * escala), 6f * escala, 0.45f * escala, 0.45f), c);
        Estrellitas(root.transform, t, "Estrellitas", 8, 0.3f * escala, 4.5f, false);
    }

    private static void ZonaMeteoro(GameObject root, Texturas t, float radio)
    {
        Suelo(root.transform, t, "Marca", "anillo", radio * 2f, TOro, 0f);
        var rocas = Brotes(root.transform, t, "Meteoros", "roca", radio * 0.8f, 1f, 4f, 1.3f, 0.8f, Color.white, -10f);
        rocas.transform.localPosition = Vector3.up * 8f;
        Golpe(rocas, 0.05f, 0.95f);
        var cola = Brotes(root.transform, t, "Colas de fuego", "llama", radio * 0.8f, 1f, 10f, 1f, 0.8f, Color.white, -10f);
        cola.transform.localPosition = Vector3.up * 8.6f;
        var golpes = Brotes(root.transform, t, "Golpes", "destello", radio * 0.8f, 1f, 4f, 2f, 0.25f, TOro);
        golpes.transform.localPosition = Vector3.up * 0.3f;
        Brotes(root.transform, t, "Polvo", "humo", radio * 0.8f, 1f, 6f, 1.4f, 0.8f, Color.white, 1f);
        Brotes(root.transform, t, "Estrellitas", "estrella", radio * 0.8f, 1f, 10f, 0.35f, 0.5f, TOro, 3f);
    }



    // ── Mente ────────────────────────────────────────────────────────────










    private static void ZonaJuicio(GameObject root, Texturas t, float radio)
    {
        Suelo(root.transform, t, "Sello", "runa", radio * 2f, TMente, -10f);
        var haz = Pieza(root.transform, t, "Haz", "haz", 30f);
        var mh = haz.main;
        mh.startLifetime = 0.18f;
        mh.startSize3D = true;
        mh.startSizeX = new ParticleSystem.MinMaxCurve(3.2f, 3.6f);
        mh.startSizeY = 14f;
        mh.startSizeZ = 1f;
        mh.startColor = TLila;
        mh.maxParticles = 3;
        Emitir(haz, 12f);
        haz.transform.localPosition = Vector3.up * 7f;
        haz.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        var rayos = Brotes(root.transform, t, "Rayos pequeños", "haz", radio * 0.8f, 1f, 5f, 1f, 0.3f, TLila);
        rayos.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        var mr = rayos.main;
        mr.startSize3D = true;
        mr.startSizeX = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        mr.startSizeY = 6f;
        mr.startSizeZ = 1f;
        rayos.transform.localPosition = Vector3.up * 3f;
        Brotes(root.transform, t, "Estrellitas", "estrella", radio * 0.9f, 1f, 18f, 0.35f, 0.7f, TOro, 2.5f);
    }

    private static void LevitacionMantener(GameObject root, Texturas t)
    {
        var aro = Suelo(root.transform, t, "Aro", "runa", 1.6f, TLila, 60f, 0f);
        var m = aro.main;
        m.loop = true;
        Brotes(root.transform, t, "Estrellitas que suben", "estrella", 0.6f, 1f, 10f, 0.22f, 0.8f, TLila, 1.4f);
        Brotes(root.transform, t, "Aros que suben", "anillo", 0.5f, 1f, 3f, 0.3f, 0.6f, TMente, 1.8f);
    }

    private static void LevitacionSoltar(GameObject root, Texturas t)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", 1.4f, 0.18f, 1f), TLila);
        Tintar(Pop(root.transform, t, "Onda", "anillo", 1.2f, 0.32f, 2.6f), TMente);
        Tintar(Estrellitas(root.transform, t, "Estrellitas", 8, 0.3f, 5f, false), TLila);
    }

    private static void PasoSombrio(GameObject root, Texturas t)
    {
        Tintar(Bocanadas(root.transform, t, "Sombra", 8, 1.1f, 0.6f), TSombra);
        var onda = Tintar(Pop(root.transform, t, "Onda", "anillo", 1f, 0.35f, 2.4f), TMente);
        onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        Tintar(Estrellitas(root.transform, t, "Estrellitas", 8, 0.28f, 4f, false), TLila);
    }

    // ── Mago Oscuro ──────────────────────────────────────────────────────




    // ── Destellos de lanzamiento y de fin, por elemento ─────────────────

    /// Destello en la mano al lanzar (fin = false) o al acabar el hechizo (fin = true).
    public static void DestelloDeDibujo(GameObject root, Texturas t, MagicElement e, bool fin)
    {
        Color c; string pieza; Color cPieza;
        switch (e)
        {
            case MagicElement.Fire:  c = TFuego; pieza = "llama"; cPieza = Color.white; break;
            case MagicElement.Storm: c = TMenta; pieza = "remolino"; cPieza = Color.white; break;
            case MagicElement.Light: c = TOro; pieza = "estrella"; cPieza = TOro; break;
            case MagicElement.Mind:  c = TMente; pieza = "estrella"; cPieza = TLila; break;
            case MagicElement.Dark:  c = TRojo; pieza = "humo"; cPieza = TOscuro; break;
            default: c = Color.white; pieza = "estrella"; cPieza = Color.white; break;
        }
        if (fin)
        {
            Tintar(Bocanadas(root.transform, t, "Bocanada", 4, 0.6f, 0.45f), Color.Lerp(c, Color.white, 0.5f));
            Tintar(Estrellitas(root.transform, t, "Estrellitas", 5, 0.22f, 3f, false), cPieza);
            return;
        }
        Tintar(Pop(root.transform, t, "Destello", "destello", 0.7f, 0.16f, 1f), c);
        Tintar(Estallido(root.transform, t, "Piezas", pieza, 4, 3f, 0.3f, 0.3f), cPieza);
    }
}
