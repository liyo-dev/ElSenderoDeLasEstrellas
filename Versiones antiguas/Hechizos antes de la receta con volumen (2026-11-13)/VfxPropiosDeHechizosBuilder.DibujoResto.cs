using UnityEngine;
using static VfxProcedural;

/// Efectos de dibujo del resto de hechizos (proyectiles, zonas, estados y destellos por elemento), con
/// las mismas piezas de Texturas/Dibujo que la maqueta. Las piezas de color crema se tiñen con el
/// color de la partícula; las demás (llama, roca, nube, grieta…) traen su color. Las zonas se montan
/// a su radio real: el envoltorio no las escala. Ver INC-1127
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

    private static void ConstruirDibujoDelResto(Texturas t, System.Text.StringBuilder log)
    {
        // Fuego (Estela)
        Guardar(BolaFuegoDibujo, r => ProyectilDeLlama(r, t, 0.95f, 1.2f), log);
        Guardar(BolaFuegoDibujoImpacto, r => ImpactoDeLlama(r, t, 1.3f), log);
        Guardar(ChispaIgneaDibujo, r => ProyectilDeChispa(r, t), log);
        Guardar(ChispaIgneaDibujoImpacto, r => ImpactoDeLlama(r, t, 0.7f), log);
        Guardar(ChispaIgneaFuegoDibujoZona, r => ZonaDeLlamas(r, t, 1.8f, 0.55f, 18f), log);
        Guardar(MuroDeFuegoDibujoZona, r => ZonaDeLlamas(r, t, 3f, 1.1f, 30f), log);
        Guardar(TormentaDeFuegoDibujoZona, r => ZonaTormentaDeFuego(r, t, 6f), log);
        // Viento
        Guardar(TornadoDibujo, r => ProyectilTornado(r, t), log);
        Guardar(TornadoDibujoImpacto, r => DibujoImpactoDeViento(r, t), log);
        Guardar(RemolinoDibujoZona, r => ZonaRemolino(r, t, 6f), log);
        Guardar(BrisaSanadoraDibujoZona, r => ZonaBrisa(r, t, 5f), log);
        // Luz (Will)
        Guardar(CorazonEstelarDibujo, r => ProyectilCorazon(r, t), log);
        Guardar(CorazonEstelarDibujoImpacto, r => ImpactoCorazon(r, t), log);
        Guardar(EstrellaFugazDibujo, r => ProyectilEstrella(r, t, 0.9f, 22f), log);
        Guardar(EstrellaFugazDibujoImpacto, r => ImpactoDeEstrellas(r, t, TOro, 1.2f), log);
        Guardar(LluviaDeChispasDibujo, r => ProyectilEstrella(r, t, 0.5f, 12f), log);
        Guardar(LluviaDeChispasDibujoImpacto, r => ImpactoDeEstrellas(r, t, TOro, 0.8f), log);
        Guardar(MeteoroDibujoZona, r => ZonaMeteoro(r, t, 4f), log);
        Guardar(NovaDeLuzDibujoZona, r => ZonaNova(r, t, 5f), log);
        Guardar(CupulaEstelarDibujoZona, r => ZonaCupula(r, t, 7f), log);
        // Mente (Liam)
        Guardar(AuraEstelarDibujo, r => ProyectilDeMente(r, t, 0.75f, true), log);
        Guardar(AuraEstelarDibujoImpacto, r => ImpactoDeEstrellas(r, t, TMente, 1.1f), log);
        Guardar(DardoMentalDibujo, r => ProyectilDeMente(r, t, 0.45f, false), log);
        Guardar(DardoMentalDibujoImpacto, r => ImpactoDardo(r, t), log);
        Guardar(EcoDibujo, r => ProyectilEco(r, t), log);
        Guardar(EcoDibujoImpacto, r => ImpactoEco(r, t), log);
        Guardar(GarraDibujo, r => ProyectilGarra(r, t), log);
        Guardar(GarraDibujoImpacto, r => ImpactoGarra(r, t), log);
        Guardar(SelloDibujoZona, r => ZonaSello(r, t, 4.5f), log);
        Guardar(CadenasDibujoZona, r => ZonaCadenas(r, t, 4f), log);
        Guardar(CadenasDibujoEstado, r => EstadoCadenas(r, t), log);
        Guardar(JuicioDibujoZona, r => ZonaJuicio(r, t, 6f), log);
        Guardar(LevitacionDibujoMantener, r => LevitacionMantener(r, t), log);
        Guardar(LevitacionDibujoSoltar, r => LevitacionSoltar(r, t), log);
        Guardar(PasoSombrioDibujoLlegada, r => PasoSombrio(r, t), log);
        // Mago Oscuro
        Guardar(MagoOscuroGolpeDibujo, r => ProyectilOscuro(r, t), log);
        Guardar(MagoOscuroGolpeDibujoImpacto, r => ImpactoOscuro(r, t), log);
        Guardar(GrietaDibujoZona, r => ZonaGrieta(r, t, 5.5f), log);
    }

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

    private static void ProyectilDeLlama(GameObject root, Texturas t, float tam, float halo)
    {
        var halo2 = Cuerpo(root.transform, t, "Halo", "destello", halo, TFuego, 30f);
        halo2.GetComponent<ParticleSystemRenderer>().sortingFudge = 1f;
        Cuerpo(root.transform, t, "Llama", "llama", tam, Color.white);
        Rastro(root.transform, t, "Pétalos", "petalo", 0.4f, 20f, Color.white, 180f);
        Rastro(root.transform, t, "Humo", "humo", 0.5f, 6f, Color.white, 15f);
    }

    private static void ProyectilDeChispa(GameObject root, Texturas t)
    {
        Cuerpo(root.transform, t, "Chispa", "estrella", 0.45f, TFuego, 45f);
        Rastro(root.transform, t, "Llamitas", "llama", 0.3f, 22f, Color.white);
    }

    private static void ImpactoDeLlama(GameObject root, Texturas t, float escala)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", 1.6f * escala, 0.18f, 1f), TOro);
        Tintar(Pop(root.transform, t, "Onda", "anillo", 1.2f * escala, 0.32f, 2.4f), TFuego);
        Estallido(root.transform, t, "Llamas", "llama", Mathf.RoundToInt(8 * escala), 5f * escala, 0.6f * escala, 0.45f);
        Bocanadas(root.transform, t, "Humo", Mathf.RoundToInt(5 * escala), 0.9f * escala, 0.7f);
        Estrellitas(root.transform, t, "Estrellitas", 6, 0.3f * escala, 4f, false);
    }

    private static void ZonaDeLlamas(GameObject root, Texturas t, float radio, float tamLlama, float ritmo)
    {
        Suelo(root.transform, t, "Anillo de fuego", "anillo", radio * 2.1f, TFuego, 0f);
        Brotes(root.transform, t, "Llamas del borde", "llama", radio, 0.15f, ritmo, tamLlama, 0.5f, Color.white, 0.6f);
        Brotes(root.transform, t, "Llamitas de dentro", "llama", radio * 0.8f, 1f, ritmo * 0.5f, tamLlama * 0.6f, 0.4f, Color.white, 0.4f);
        Brotes(root.transform, t, "Humo", "humo", radio, 1f, ritmo * 0.15f, tamLlama * 1.2f, 0.9f, new Color(0.85f, 0.78f, 0.70f), 1.2f);
    }

    private static void ZonaTormentaDeFuego(GameObject root, Texturas t, float radio)
    {
        Suelo(root.transform, t, "Sombra de la nube", "anillo", radio * 2f, TRojo, 0f);
        var nubes = Brotes(root.transform, t, "Nubes", "nube", radio * 0.7f, 1f, 2.5f, 3.2f, 4f, Color.white);
        var mn = nubes.main;
        mn.prewarm = true;   // la nube ya está cuando caen las primeras bolas
        nubes.transform.localPosition = Vector3.up * 7f;
        Golpe(nubes, 0.08f, 0.85f);
        var bolas = Brotes(root.transform, t, "Bolas que caen", "llama", radio * 0.85f, 1f, 9f, 0.9f, 0.75f, Color.white, -9f);
        bolas.transform.localPosition = Vector3.up * 6.5f;
        var golpes = Brotes(root.transform, t, "Golpes en el suelo", "destello", radio * 0.85f, 1f, 7f, 1.1f, 0.25f, TFuego);
        golpes.transform.localPosition = Vector3.up * 0.3f;
        Brotes(root.transform, t, "Humo", "humo", radio * 0.85f, 1f, 5f, 1.2f, 0.9f, new Color(0.85f, 0.78f, 0.70f), 1f);
    }

    // ── Viento ───────────────────────────────────────────────────────────

    private static void ProyectilTornado(GameObject root, Texturas t)
    {
        var embudo = Cuerpo(root.transform, t, "Embudo", "embudo", 1.6f, Color.white, 6f);
        embudo.transform.localPosition = Vector3.up * 0.5f;
        var rem = Rastro(root.transform, t, "Remolinos", "remolino", 0.7f, 14f, Color.white, 180f);
        Girar(rem, -300f);
        var hojas = Rastro(root.transform, t, "Hojas", "hoja", 0.22f, 8f, Color.white, 180f);
        Girar(hojas, 360f);
        Forma(hojas, ParticleSystemShapeType.Circle, 0.7f, 0.2f);
    }

    private static void ZonaRemolino(GameObject root, Texturas t, float radio)
    {
        Suelo(root.transform, t, "Espiral", "espiral", radio * 2f, TMenta, -160f);
        var rem = Brotes(root.transform, t, "Remolinos", "remolino", radio * 0.85f, 1f, 16f, 1.1f, 0.5f, Color.white, 1.2f);
        Girar(rem, -300f);
        var lineas = Brotes(root.transform, t, "Líneas de viento", "linea_viento", radio, 0.4f, 14f, 1.2f, 0.3f, Color.white, 0.4f);
        GiroAlAzar(lineas, 180f);
        var hojas = Brotes(root.transform, t, "Hojas", "hoja", radio * 0.7f, 1f, 10f, 0.3f, 0.9f, Color.white, 2.5f);
        Girar(hojas, 400f);
    }

    private static void ZonaBrisa(GameObject root, Texturas t, float radio)
    {
        Suelo(root.transform, t, "Espiral suave", "espiral", radio * 1.6f, TMenta, -60f);
        Suelo(root.transform, t, "Borde", "anillo", radio * 2f, TMenta, 0f, 0.03f);
        Brotes(root.transform, t, "Corazones", "corazon", radio * 0.7f, 1f, 5f, 0.45f, 1.3f, TRosa, 1.4f);
        var hojas = Brotes(root.transform, t, "Hojas", "hoja", radio * 0.8f, 1f, 6f, 0.25f, 1.4f, Color.white, 1.0f);
        Girar(hojas, 200f);
        Brotes(root.transform, t, "Estrellitas", "estrella", radio * 0.8f, 1f, 8f, 0.22f, 0.8f, TMenta, 1.6f);
    }

    // ── Luz ──────────────────────────────────────────────────────────────

    private static void ProyectilCorazon(GameObject root, Texturas t)
    {
        Cuerpo(root.transform, t, "Corazón", "corazon", 0.8f, TMente, 6f);
        var estrella = Cuerpo(root.transform, t, "Estrella", "estrella", 0.42f, TOro, 20f);
        estrella.GetComponent<ParticleSystemRenderer>().sortingFudge = -1f;
        Rastro(root.transform, t, "Estrellitas", "estrella", 0.25f, 18f, TOro, 90f);
    }

    private static void ImpactoCorazon(GameObject root, Texturas t)
    {
        Tintar(Pop(root.transform, t, "Corazón", "corazon", 1.4f, 0.4f, 1.6f), TMente);
        Tintar(Pop(root.transform, t, "Destello", "destello", 2f, 0.2f, 1f), TOro);
        Tintar(Pop(root.transform, t, "Onda", "anillo", 2f, 0.4f, 3.6f), TLila);
        Tintar(Estallido(root.transform, t, "Corazoncitos", "corazon", 10, 6f, 0.5f, 0.5f), TMente);
        Tintar(Estrellitas(root.transform, t, "Estrellitas", 12, 0.4f, 6f, false), TOro);
    }

    private static void ProyectilEstrella(GameObject root, Texturas t, float tam, float ritmoRastro)
    {
        var e = Cuerpo(root.transform, t, "Estrella", "estrella", tam, TOro, 0f);
        Girar(e, 360f);
        Rastro(root.transform, t, "Cola", "estrella", tam * 0.45f, ritmoRastro, Color.white, 90f);
    }

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

    private static void ZonaNova(GameObject root, Texturas t, float radio)
    {
        UnaVez(Tintar(Pop(root.transform, t, "Sol", "destello", radio * 0.9f, 0.5f, 1.2f), TOro));
        var onda = UnaVez(Tintar(Pop(root.transform, t, "Onda", "anillo", radio * 0.5f, 0.6f, 4f), TOro));
        onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        UnaVez(Tintar(Estallido(root.transform, t, "Rayos", "estrella", 18, 10f, 0.7f, 0.6f), TOro));
        UnaVez(Estrellitas(root.transform, t, "Estrellitas", 16, 0.4f, 7f, false));
    }

    private static void ZonaCupula(GameObject root, Texturas t, float radio)
    {
        var cupula = UnaVez(Tintar(Pop(root.transform, t, "Cúpula", "cupula", radio * 1.9f, 1.2f, 1f), TLila));
        cupula.transform.localPosition = Vector3.up * radio * 0.45f;
        var onda = UnaVez(Tintar(Pop(root.transform, t, "Borde", "anillo", radio * 1.9f, 1.2f, 1f), TLila));
        onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        Brotes(root.transform, t, "Estrellitas", "estrella", radio * 0.9f, 1f, 30f, 0.4f, 0.6f, TOro, 2.5f);
    }

    // ── Mente ────────────────────────────────────────────────────────────

    private static void ProyectilDeMente(GameObject root, Texturas t, float tam, bool conAro)
    {
        Cuerpo(root.transform, t, "Núcleo", "destello", tam, TMente, 30f);
        if (conAro)
        {
            var aro = Cuerpo(root.transform, t, "Aro", "anillo", tam * 1.5f, TLila, 0f);
            Golpe(aro, 0.3f, 0.6f);
        }
        Rastro(root.transform, t, "Estrellitas", "estrella", tam * 0.4f, 18f, TLila, 90f);
    }

    private static void ImpactoDardo(GameObject root, Texturas t)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", 1.2f, 0.16f, 1f), TMente);
        Tintar(Estallido(root.transform, t, "Esquirlas", "dardo", 6, 5f, 0.5f, 0.35f), TLila);
        Tintar(Estrellitas(root.transform, t, "Estrellitas", 6, 0.28f, 4f, false), TLila);
    }

    private static void ProyectilEco(GameObject root, Texturas t)
    {
        Cuerpo(root.transform, t, "Núcleo", "destello", 0.4f, TLila, 30f);
        var ondas = Pieza(root.transform, t, "Ondas", "anillo");
        var m = ondas.main;
        m.startLifetime = 0.45f;
        m.startSize = 0.4f;
        m.startColor = TMente;
        Emitir(ondas, 7f);
        var tam = ondas.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.8f, 2.6f), new Keyframe(1f, 0f)));
    }

    private static void ImpactoEco(GameObject root, Texturas t)
    {
        for (int i = 0; i < 3; i++)
        {
            var onda = Tintar(Pop(root.transform, t, "Onda " + (i + 1), "anillo", 1f, 0.35f + i * 0.12f, 2f + i), i % 2 == 0 ? TMente : TLila);
            var m = onda.main;
            m.startDelay = i * 0.08f;
        }
        Tintar(Estrellitas(root.transform, t, "Estrellitas", 8, 0.28f, 4f, false), TLila);
    }

    private static void ProyectilGarra(GameObject root, Texturas t)
    {
        for (int i = 0; i < 3; i++)
        {
            var g = Cuerpo(root.transform, t, "Zarpa " + (i + 1), "garra", 0.9f, i == 1 ? TMente : TLila, 4f);
            g.transform.localPosition = new Vector3((i - 1) * 0.28f, 0f, 0f);
        }
        Rastro(root.transform, t, "Estrellitas", "estrella", 0.2f, 14f, TLila, 90f);
    }

    private static void ImpactoGarra(GameObject root, Texturas t)
    {
        for (int i = 0; i < 3; i++)
        {
            var g = Tintar(Pop(root.transform, t, "Zarpazo " + (i + 1), "garra", 1.6f, 0.3f, 1.2f), i == 1 ? TMente : TLila);
            g.transform.localPosition = new Vector3((i - 1) * 0.35f, 0f, 0f);
            var m = g.main;
            m.startDelay = i * 0.05f;
        }
        Tintar(Pop(root.transform, t, "Destello", "destello", 1.2f, 0.16f, 1f), TMente);
        Tintar(Estrellitas(root.transform, t, "Estrellitas", 8, 0.3f, 4.5f, false), TLila);
    }

    private static void ZonaSello(GameObject root, Texturas t, float radio)
    {
        Suelo(root.transform, t, "Sello", "runa", radio * 2f, TMente, -14f);
        Suelo(root.transform, t, "Sello interior", "runa", radio * 1.1f, TLila, 26f, 0.06f);
        Brotes(root.transform, t, "Eslabones que suben", "eslabon", radio * 0.95f, 0.05f, 10f, 0.5f, 0.9f, TLila, 1.6f);
        Brotes(root.transform, t, "Sombra", "humo", radio * 0.8f, 1f, 8f, 1.2f, 1.2f, TSombra, 0.8f);
    }

    private static void ZonaCadenas(GameObject root, Texturas t, float radio)
    {
        Suelo(root.transform, t, "Sello", "runa", radio * 2.1f, TMente, 18f);
        var cadenas = Brotes(root.transform, t, "Cadenas", "eslabon", radio * 0.92f, 0.05f, 26f, 0.6f, 0.8f, TLila, 2.2f);
        cadenas.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        Brotes(root.transform, t, "Estrellitas", "estrella", radio * 0.9f, 1f, 10f, 0.25f, 0.8f, TLila, 1.5f);
    }

    private static void EstadoCadenas(GameObject root, Texturas t)
    {
        for (int i = 0; i < 2; i++)
        {
            var aro = Pieza(root.transform, t, "Eslabones " + (i + 1), "eslabon");
            aro.transform.localPosition = Vector3.up * (0.5f + i * 0.6f);
            var m = aro.main;
            m.startLifetime = 0.5f;
            m.startSize = 0.32f;
            m.startColor = i == 0 ? TMente : TLila;
            Emitir(aro, 16f);
            Forma(aro, ParticleSystemShapeType.Circle, 0.55f, 0f);
            var forma = aro.shape;
            forma.arcMode = ParticleSystemShapeMultiModeValue.Loop;
            forma.arcSpeed = i == 0 ? 1.2f : -1.2f;
            GiroAlAzar(aro, 30f);
            Golpe(aro, 0.15f, 0.7f);
        }
    }

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

    private static void ProyectilOscuro(GameObject root, Texturas t)
    {
        Cuerpo(root.transform, t, "Núcleo", "destello", 0.7f, TRojo, 30f);
        Rastro(root.transform, t, "Humo negro", "humo", 0.6f, 20f, TOscuro, 30f);
    }

    private static void ImpactoOscuro(GameObject root, Texturas t)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", 1.6f, 0.18f, 1f), TRojo);
        var grieta = Pop(root.transform, t, "Grieta", "grieta", 1.8f, 0.5f, 1.3f);
        grieta.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        Tintar(Bocanadas(root.transform, t, "Humo negro", 6, 1.1f, 0.7f), TOscuro);
    }

    private static void ZonaGrieta(GameObject root, Texturas t, float radio)
    {
        Suelo(root.transform, t, "Borde", "anillo", radio * 2f, TRojo, 0f);
        for (int i = 0; i < 3; i++)
        {
            var g = Suelo(root.transform, t, "Grieta " + (i + 1), "grieta", radio * (1.3f - i * 0.25f), Color.white, 0f, 0.05f + i * 0.01f);
            var m = g.main;
            m.startRotation = i * 60f * Mathf.Deg2Rad;
        }
        Brotes(root.transform, t, "Humo negro", "humo", radio * 0.8f, 1f, 10f, 1.4f, 1.3f, TOscuro, 1.2f);
        Brotes(root.transform, t, "Chispas rojas", "estrella", radio * 0.8f, 1f, 8f, 0.3f, 0.6f, TRojo, 2f);
    }

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
