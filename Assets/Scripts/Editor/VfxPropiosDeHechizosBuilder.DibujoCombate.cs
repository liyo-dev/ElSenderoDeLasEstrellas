using UnityEngine;
using static VfxProcedural;

/// Efectos de dibujo de Amanecer, de los escudos y de lo que lanzan los enemigos (demonio, beholder,
/// golem y Mago Oscuro), con las mismas piezas de Texturas/Dibujo. Los asigna VfxDeCombateDeDibujo.
/// Ver INC-1127
public static partial class VfxPropiosDeHechizosBuilder
{
    // Amanecer (mismas rutas que usa AmanecerBuilder)
    public const string AmanecerProyectil = "Amanecer";
    public const string AmanecerCarga = "Amanecer_Carga";
    public const string AmanecerImpacto = "Amanecer_Impacto";
    public const string AmanecerProteccionAbierta = "Amanecer_ProteccionAbierta";

    // Escudos
    public const string EscudoWill = "Escudo_Will";
    public const string EscudoNpc = "Escudo_Npc";
    public const string EscudoGrupo = "Escudo_Grupo";
    public const string EscudoMagoOscuro = "Escudo_MagoOscuro";
    public const string Contraataque = "Escudo_Contraataque";

    // Enemigos
    public const string PlasmaDibujo = "Enemigo_Plasma";
    public const string PlasmaDibujoImpacto = "Enemigo_Plasma_Impacto";
    public const string DemonioLanzamiento = "Demonio_Lanzamiento";
    public const string DemonioLluviaAviso = "Demonio_Lluvia_Aviso";
    public const string DemonioLluviaGota = "Demonio_Lluvia_Gota";
    public const string DemonioLluviaImpacto = "Demonio_Lluvia_Impacto";
    public const string BeholderAturdido = "Beholder_Aturdido";
    public const string BeholderRebote = "Beholder_Rebote";
    public const string GolemAviso = "Golem_Aviso";
    public const string GolemCogerRoca = "Golem_CogerRoca";
    public const string GolemOnda = "Golem_Onda";
    public const string GolemPolvo = "Golem_Polvo";
    public const string GolemPunetazo = "Golem_Punetazo";
    public const string MagoOscuroCarga = "MagoOscuro_Carga";
    public const string MagoOscuroAparicion = "MagoOscuro_Aparicion";
    public const string MagoOscuroExplosion = "MagoOscuro_Explosion";

    private static readonly Color TCielo = new Color(0.62f, 0.82f, 1f);
    private static readonly Color TPiedra = new Color(0.82f, 0.74f, 0.64f);

    private static void ConstruirDibujoDeCombate(Texturas t, System.Text.StringBuilder log)
    {
        // Amanecer, los escudos y el contraataque los monta HechizosConVolumen (INC-1140); la bola de
        // plasma, la lluvia del demonio y los avisos del suelo, EnemigosConVolumen (INC-1187).
        Guardar(BeholderAturdido, r => Aturdido(r, t), log);
        Guardar(BeholderRebote, r => ImpactoDeEstrellas(r, t, TCielo, 0.8f), log);
        Guardar(GolemCogerRoca, r => PolvoDePiedra(r, t, 0.8f), log);
        Guardar(GolemOnda, r => OndaDeTierra(r, t, 5f), log);
        Guardar(GolemPolvo, r => PolvoDePiedra(r, t, 1.6f), log);
        Guardar(GolemPunetazo, r => Punetazo(r, t), log);
        Guardar(MagoOscuroCarga, r => CargaOscura(r, t), log);
        Guardar(MagoOscuroAparicion, r => PasoSombrio(r, t), log);
        Guardar(MagoOscuroExplosion, r => ExplosionOscura(r, t), log);
    }

    // ── Amanecer ─────────────────────────────────────────────────────────

    /// Texturas con las piezas de dibujo ya registradas, para quien construya fuera de Construir.
    public static Texturas TexturasDeDibujo()
    {
        var t = NuevasTexturas();
        RegistrarPiezas(t);
        return t;
    }





    private static Light Luz(Transform padre, string nombre, Color color, float intensidad, float alcance)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.intensity = intensidad;
        luz.range = alcance;
        luz.shadows = LightShadows.None;
        return luz;
    }

    // ── Enemigos ─────────────────────────────────────────────────────────

    private static void ImpactoPlasma(GameObject root, Texturas t, float radio)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", radio * 1.1f, 0.2f, 1f), TRojo);
        var onda = Tintar(Pop(root.transform, t, "Onda", "anillo", radio * 0.6f, 0.35f, 3.4f), TRojo);
        onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        onda.transform.localPosition = Vector3.up * 0.1f;
        Tintar(Bocanadas(root.transform, t, "Humo negro", 7, radio * 0.6f, 0.8f), TOscuro);
        Tintar(Estallido(root.transform, t, "Chispas", "estrella", 8, 5f, 0.4f, 0.4f), TRojo);
    }

    /// Estrellitas que dan vueltas sobre la cabeza, como en los dibujos.
    private static void Aturdido(GameObject root, Texturas t)
    {
        var ps = Pieza(root.transform, t, "Estrellitas", "estrella");
        ps.transform.localPosition = Vector3.up * 0.4f;
        var m = ps.main;
        m.startLifetime = 0.35f;
        m.startSize = 0.32f;
        m.startColor = TOro;
        Emitir(ps, 18f);
        Forma(ps, ParticleSystemShapeType.Circle, 0.5f, 0f);
        var forma = ps.shape;
        forma.arcMode = ParticleSystemShapeMultiModeValue.Loop;
        forma.arcSpeed = 1.5f;
        Girar(ps, 300f);
        Golpe(ps, 0.2f, 0.7f);
        var espirales = Pieza(root.transform, t, "Espirales", "espiral");
        espirales.transform.localPosition = Vector3.up * 0.4f;
        var mp = espirales.main;
        mp.startLifetime = 0.5f;
        mp.startSize = 0.25f;
        Emitir(espirales, 5f);
        Forma(espirales, ParticleSystemShapeType.Circle, 0.5f, 0f);
        var fp = espirales.shape;
        fp.arcMode = ParticleSystemShapeMultiModeValue.Loop;
        fp.arcSpeed = -1.5f;
        Girar(espirales, -400f);
        Golpe(espirales, 0.2f, 0.7f);
    }

    private static void PolvoDePiedra(GameObject root, Texturas t, float escala)
    {
        Tintar(Bocanadas(root.transform, t, "Polvo", Mathf.RoundToInt(8 * escala), 1.2f * escala, 0.8f), TPiedra);
        Estallido(root.transform, t, "Piedrecitas", "piedra", Mathf.RoundToInt(7 * escala), 5f * escala, 0.35f * escala, 0.6f);
    }

    /// Onda de choque del golem a radio 5 (el golem la escala a su alcance): anillo de tierra que se
    /// abre, polvo por el borde y piedras que saltan.
    private static void OndaDeTierra(GameObject root, Texturas t, float radio)
    {
        var onda = Tintar(Pop(root.transform, t, "Onda", "anillo", radio * 0.4f, 0.6f, 5.5f), TPiedra);
        onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        onda.transform.localPosition = Vector3.up * 0.1f;
        var grieta = Pop(root.transform, t, "Grietas", "grieta", radio * 1.2f, 1.2f, 1.1f);
        grieta.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        Tintar(grieta, new Color(0.75f, 0.60f, 0.45f));
        var polvo = Tintar(Estallido(root.transform, t, "Polvo del borde", "humo", 18, radio * 2.2f, 1.4f, 0.8f), TPiedra);
        polvo.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
        var forma = polvo.shape;
        forma.shapeType = ParticleSystemShapeType.Circle;
        forma.rotation = new Vector3(-90f, 0f, 0f);
        Estallido(root.transform, t, "Piedras", "piedra", 10, 7f, 0.5f, 0.7f);
    }

    private static void Punetazo(GameObject root, Texturas t)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", 2f, 0.18f, 1f), TOro);
        Tintar(Estrellitas(root.transform, t, "Estrellitas", 10, 0.4f, 6f, false), TOro);
        PolvoDePiedra(root, t, 1f);
    }

    private static void CargaOscura(GameObject root, Texturas t)
    {
        UnaVez(Suelo(root.transform, t, "Runa", "runa", 3f, TRojo, -60f));
        Brotes(root.transform, t, "Humo que sube", "humo", 1.2f, 1f, 12f, 0.9f, 0.9f, TOscuro, 1.6f);
        Brotes(root.transform, t, "Chispas", "estrella", 1.2f, 1f, 10f, 0.3f, 0.6f, TRojo, 2.4f);
    }

    private static void ExplosionOscura(GameObject root, Texturas t)
    {
        ImpactoPlasma(root, t, 3f);
        var grieta = Pop(root.transform, t, "Grieta", "grieta", 3.5f, 0.8f, 1.2f);
        grieta.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
    }
}
