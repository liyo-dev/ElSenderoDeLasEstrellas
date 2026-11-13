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

    // Centro y radio de la esfera de los escudos (los de antes median lo mismo).
    private static readonly Vector3 CentroDelEscudo = new Vector3(0f, 0.95f, 0f);
    private const float RadioDelEscudo = 1.4f;

    private static void ConstruirDibujoDeCombate(Texturas t, System.Text.StringBuilder log)
    {
        ConstruirAmanecer(t, log);

        Guardar(EscudoWill, r => Escudo(r, t, TCielo, true), log);
        Guardar(EscudoNpc, r => Escudo(r, t, TRosa, true), log);
        Guardar(EscudoGrupo, r => Escudo(r, t, TOro, false), log);
        Guardar(EscudoMagoOscuro, r => EscudoOscuro(r, t), log);
        Guardar(Contraataque, r => ImpactoDeEstrellas(r, t, TOro, 1f), log);

        Guardar(PlasmaDibujo, r => ProyectilPlasma(r, t), log);
        Guardar(PlasmaDibujoImpacto, r => ImpactoPlasma(r, t, 2.2f), log);
        Guardar(DemonioLanzamiento, r => LanzamientoDemonio(r, t), log);
        Guardar(DemonioLluviaAviso, r => Aviso(r, t, 1.8f, TRojo), log);
        Guardar(DemonioLluviaGota, r => GotaDeSombra(r, t), log);
        Guardar(DemonioLluviaImpacto, r => ImpactoPlasma(r, t, 1.8f), log);
        Guardar(BeholderAturdido, r => Aturdido(r, t), log);
        Guardar(BeholderRebote, r => ImpactoDeEstrellas(r, t, TCielo, 0.8f), log);
        Guardar(GolemAviso, r => Aviso(r, t, 1.6f, TRojo), log);
        Guardar(GolemCogerRoca, r => PolvoDePiedra(r, t, 0.8f), log);
        Guardar(GolemOnda, r => OndaDeTierra(r, t, 5f), log);
        Guardar(GolemPolvo, r => PolvoDePiedra(r, t, 1.6f), log);
        Guardar(GolemPunetazo, r => Punetazo(r, t), log);
        Guardar(MagoOscuroCarga, r => CargaOscura(r, t), log);
        Guardar(MagoOscuroAparicion, r => PasoSombrio(r, t), log);
        Guardar(MagoOscuroExplosion, r => ExplosionOscura(r, t), log);
    }

    // ── Amanecer ─────────────────────────────────────────────────────────

    /// Construye los cuatro efectos de Amanecer. Lo llama también AmanecerBuilder.
    public static void ConstruirAmanecer(Texturas t, System.Text.StringBuilder log)
    {
        Guardar(AmanecerProyectil, r => AmanecerQueViaja(r, t), log);
        Guardar(AmanecerCarga, r => AmanecerQueSeCarga(r, t), log);
        Guardar(AmanecerImpacto, r => AmanecerQueEstalla(r, t), log);
        Guardar(AmanecerProteccionAbierta, r => AmanecerSobreLaProteccion(r, t), log);
    }

    /// Texturas con las piezas de dibujo ya registradas, para quien construya fuera de Construir.
    public static Texturas TexturasDeDibujo()
    {
        var t = NuevasTexturas();
        RegistrarPiezas(t);
        return t;
    }

    /// Un sol con cara de dibujo que gira, con rayos que relevan y una cola de estrellas y pétalos.
    private static void AmanecerQueViaja(GameObject root, Texturas t)
    {
        var halo = Cuerpo(root.transform, t, "Halo", "destello", 2.6f, TOro, 30f);
        halo.GetComponent<ParticleSystemRenderer>().sortingFudge = 2f;
        var sol = Cuerpo(root.transform, t, "Sol", "sol", 1.9f, Color.white, 0f);
        Girar(sol, 120f);
        var corona = Cuerpo(root.transform, t, "Corona", "anillo", 2.4f, TLila, 0f);
        Golpe(corona, 0.3f, 0.6f);
        Rastro(root.transform, t, "Estrellas", "estrella", 0.6f, 30f, TOro, 90f);
        Rastro(root.transform, t, "Pétalos", "petalo", 0.5f, 18f, Color.white, 180f);
        Rastro(root.transform, t, "Estrellitas lila", "estrella", 0.35f, 14f, TLila, 90f);
    }

    /// Mientras Will lo prepara: runa solar a sus pies que gira, sol que crece en las manos, rayos y
    /// estrellas que suben.
    private static void AmanecerQueSeCarga(GameObject root, Texturas t)
    {
        UnaVez(Suelo(root.transform, t, "Runa solar", "runa", 3.6f, TOro, 50f));
        UnaVez(Suelo(root.transform, t, "Anillo", "anillo", 4.2f, TFuego, 0f, 0.03f));
        var sol = Pieza(root.transform, t, "Sol que crece", "sol", 1.2f, false);
        sol.transform.localPosition = Vector3.up * 1.2f;
        var ms = sol.main;
        ms.startLifetime = 1.2f;
        ms.startSize = 1.6f;
        ms.maxParticles = 1;
        Rafaga(sol, 1);
        Girar(sol, 160f);
        var tam = sol.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(0.85f, 1f), new Keyframe(0.92f, 1.15f), new Keyframe(1f, 0f)));
        var rayos = Brotes(root.transform, t, "Rayos que suben", "haz", 1.6f, 0.2f, 10f, 1f, 0.5f, TOro, 3f);
        rayos.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        var mr = rayos.main;
        mr.startSize3D = true;
        mr.startSizeX = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        mr.startSizeY = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        mr.startSizeZ = 1f;
        Brotes(root.transform, t, "Estrellas que suben", "estrella", 1.8f, 1f, 24f, 0.35f, 0.8f, TOro, 2.4f);
        Brotes(root.transform, t, "Pétalos", "petalo", 1.4f, 1f, 8f, 0.4f, 0.7f, Color.white, 1.8f);

        var luz = Luz(root.transform, "Luz de la carga", new Color(1f, 0.80f, 0.35f), 5.5f, 12f);
        var fb = root.AddComponent<VfxImpactFeedback>();
        fb.shakeIntensity = 0.12f;
        fb.shakeDuration = 0.6f;
        fb.enableScreenFlash = false;
        fb.enableHitStop = false;
        fb.enableRumble = true;
        fb.rumbleLow = 0.25f;
        fb.rumbleHigh = 0.4f;
        fb.rumbleDuration = 0.6f;
        fb.dynamicLight = luz;
        fb.lightFadeDuration = 0.8f;
    }

    /// El golpe: un sol enorme que estalla, columna de luz, ondas en el suelo, rayos, estrellas y
    /// pétalos por todas partes; con temblor, destello de pantalla y un parón muy corto.
    private static void AmanecerQueEstalla(GameObject root, Texturas t)
    {
        Tintar(Pop(root.transform, t, "Sol", "sol", 4.5f, 0.7f, 1.3f), Color.white);
        Tintar(Pop(root.transform, t, "Destello", "destello", 7f, 0.25f, 1f), TOro);
        var pilar = Pieza(root.transform, t, "Columna de luz", "haz", 1f, false);
        var mp = pilar.main;
        mp.startLifetime = 0.9f;
        mp.startSize3D = true;
        mp.startSizeX = 9f;
        mp.startSizeY = 30f;
        mp.startSizeZ = 1f;
        mp.startColor = TOro;
        mp.maxParticles = 1;
        Rafaga(pilar, 1);
        pilar.transform.localPosition = Vector3.up * 14f;
        pilar.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        var tamP = pilar.sizeOverLifetime;
        tamP.enabled = true;
        tamP.separateAxes = true;
        tamP.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(0.12f, 1.1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
        tamP.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        for (int i = 0; i < 3; i++)
        {
            var onda = Tintar(Pop(root.transform, t, "Onda " + (i + 1), "anillo", 2f, 0.6f + i * 0.15f, 5f + i * 2f), i == 1 ? TFuego : TOro);
            onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            onda.transform.localPosition = Vector3.up * 0.1f;
            var m = onda.main;
            m.startDelay = i * 0.1f;
        }
        var runa = Tintar(Pop(root.transform, t, "Runa", "runa", 6f, 1.2f, 1.1f), TOro);
        runa.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        Girar(runa, 60f);
        Tintar(Estallido(root.transform, t, "Rayos", "estrella", 22, 14f, 1.1f, 0.7f), TOro);
        Estallido(root.transform, t, "Pétalos", "petalo", 16, 10f, 0.8f, 0.8f);
        Tintar(Estrellitas(root.transform, t, "Estrellitas", 26, 0.6f, 9f, false), TOro);
        Tintar(Estrellitas(root.transform, t, "Estrellitas lila", 14, 0.45f, 7f, false), TLila);
        Tintar(Bocanadas(root.transform, t, "Polvo dorado", 10, 2.2f, 1.1f), new Color(1f, 0.93f, 0.75f));

        var luz = Luz(root.transform, "Luz del golpe", new Color(1f, 0.90f, 0.62f), 15f, 32f);
        var fb = root.AddComponent<VfxImpactFeedback>();
        fb.shakeIntensity = 0.75f;
        fb.shakeDuration = 0.5f;
        fb.enableScreenFlash = true;
        fb.flashColor = new Color(1f, 0.96f, 0.85f, 0.85f);
        fb.flashDuration = 0.25f;
        fb.enableHitStop = true;
        fb.hitStopTimeScale = 0.04f;
        fb.hitStopDuration = 0.10f;
        fb.enableRumble = true;
        fb.rumbleLow = 0.85f;
        fb.rumbleHigh = 0.95f;
        fb.rumbleDuration = 0.6f;
        fb.dynamicLight = luz;
        fb.lightFadeDuration = 0.65f;
    }

    /// En bucle sobre el enemigo mientras su protección está abierta: un sol detrás que gira, rayos
    /// que le caen encima, runa en el suelo y estrellas que suben.
    private static void AmanecerSobreLaProteccion(GameObject root, Texturas t)
    {
        var sol = Pieza(root.transform, t, "Sol", "sol");
        sol.transform.localPosition = Vector3.up * 1.4f;
        var ms = sol.main;
        ms.startLifetime = 1f;
        ms.startSize = 3.2f;
        ms.maxParticles = 2;
        Emitir(sol, 1f);
        Girar(sol, 40f);
        sol.GetComponent<ParticleSystemRenderer>().sortingFudge = 5f;
        var tam = sol.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.85f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.85f)));
        Suelo(root.transform, t, "Runa", "runa", 4f, TOro, 30f);
        var rayos = Brotes(root.transform, t, "Rayos que caen", "haz", 1.2f, 1f, 6f, 1f, 0.6f, TOro);
        rayos.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        rayos.transform.localPosition = Vector3.up * 3f;
        var mr = rayos.main;
        mr.startSize3D = true;
        mr.startSizeX = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        mr.startSizeY = 6f;
        mr.startSizeZ = 1f;
        Brotes(root.transform, t, "Estrellas que suben", "estrella", 1.4f, 1f, 14f, 0.35f, 0.9f, TOro, 1.8f);
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

    // ── Escudos ──────────────────────────────────────────────────────────

    /// Escudo: flor de luz en 3D que aparece con rebote y gira, con estrellitas alrededor. Con
    /// 'conColision', lleva la esfera de disparo que usan los escudos de Will y de los NPC.
    private static void Escudo(GameObject root, Texturas t, Color color, bool conColision)
    {
        if (conColision)
        {
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = CentroDelEscudo;
            col.radius = RadioDelEscudo;
        }
        // Flor de luz en 3D en lugar de burbuja (INC-1127): tiene volumen y la pinta el mundo.
        var aparece = Tintar(Pop(root.transform, t, "Aparece", "anillo", RadioDelEscudo, 0.3f, 1.8f), color);
        aparece.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        aparece.transform.localPosition = Vector3.up * 0.05f;
        FlorDeLuz(root.transform, color);
        Luz(root.transform, "Luz", color, 1.5f, 3.5f).transform.localPosition = CentroDelEscudo;
        var estrellas = Pieza(root.transform, t, "Estrellitas", "estrella");
        estrellas.transform.localPosition = CentroDelEscudo;
        var m = estrellas.main;
        m.startLifetime = 0.6f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        m.startColor = color;
        Emitir(estrellas, 6f);
        Forma(estrellas, ParticleSystemShapeType.Sphere, RadioDelEscudo * 0.95f, 0f);
        GiroAlAzar(estrellas, 45f);
        Golpe(estrellas, 0.2f, 0.6f);
    }

    private static void EscudoOscuro(GameObject root, Texturas t)
    {
        FlorDeLuz(root.transform, TRojo, 0.6f, 1f, 30f);
        Suelo(root.transform, t, "Runa", "runa", RadioDelEscudo * 1.6f, TRojo, -40f);
        Brotes(root.transform, t, "Humo", "humo", 1f, 1f, 5f, 0.8f, 1f, TOscuro, 0.8f);
    }

    // ── Enemigos ─────────────────────────────────────────────────────────

    /// Bola del demonio y del beholder: núcleo rojo que tiembla, aro oscuro y humo que se queda atrás.
    /// El envoltorio la tiene a escala 2, así que se monta a la mitad.
    private static void ProyectilPlasma(GameObject root, Texturas t)
    {
        root.transform.localScale = Vector3.one * 0.5f;
        Cuerpo(root.transform, t, "Núcleo", "destello", 1.1f, TRojo, 30f);
        var aro = Cuerpo(root.transform, t, "Aro", "anillo", 1.5f, TSombra, 0f);
        Golpe(aro, 0.3f, 0.6f);
        Rastro(root.transform, t, "Humo", "humo", 0.8f, 16f, TOscuro, 30f);
        Rastro(root.transform, t, "Chispas", "estrella", 0.35f, 10f, TRojo, 90f);
    }

    private static void ImpactoPlasma(GameObject root, Texturas t, float radio)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", radio * 1.1f, 0.2f, 1f), TRojo);
        var onda = Tintar(Pop(root.transform, t, "Onda", "anillo", radio * 0.6f, 0.35f, 3.4f), TRojo);
        onda.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        onda.transform.localPosition = Vector3.up * 0.1f;
        Tintar(Bocanadas(root.transform, t, "Humo negro", 7, radio * 0.6f, 0.8f), TOscuro);
        Tintar(Estallido(root.transform, t, "Chispas", "estrella", 8, 5f, 0.4f, 0.4f), TRojo);
    }

    private static void LanzamientoDemonio(GameObject root, Texturas t)
    {
        Tintar(Pop(root.transform, t, "Destello", "destello", 1.4f, 0.18f, 1f), TRojo);
        Tintar(Bocanadas(root.transform, t, "Humo", 4, 0.8f, 0.5f), TOscuro);
    }

    /// Aviso en el suelo de dónde va a caer algo: círculo que late con la señal de peligro en medio.
    /// Va en horizontal y no depende del giro con que lo pongan.
    private static void Aviso(GameObject root, Texturas t, float radio, Color color)
    {
        var anillo = Pieza(root.transform, t, "Círculo", "anillo", 0.5f, true);
        anillo.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        var ma = anillo.main;
        ma.startLifetime = 0.5f;
        ma.startSize = radio * 2f;
        ma.startColor = color;
        ma.maxParticles = 2;
        Emitir(anillo, 2f);
        Rafaga(anillo, 1);
        Emitir(anillo, 2f);
        var tam = anillo.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.85f), new Keyframe(0.3f, 1.05f), new Keyframe(1f, 1f)));
        Suelo(root.transform, t, "Sombra", "humo", radio * 1.7f, TOscuro, 0f, 0.02f);
        var signo = Pieza(root.transform, t, "Peligro", "exclamacion", 0.5f, true);
        signo.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        signo.transform.localPosition = Vector3.up * 0.05f;
        var ms = signo.main;
        ms.startLifetime = 0.5f;
        ms.startSize = radio * 0.6f;
        ms.maxParticles = 2;
        Emitir(signo, 2f);
        Rafaga(signo, 1);
        Emitir(signo, 2f);
        Golpe(signo, 0.2f, 0.85f);
    }

    private static void GotaDeSombra(GameObject root, Texturas t)
    {
        Cuerpo(root.transform, t, "Gota", "destello", 0.9f, TSombra, 30f);
        Rastro(root.transform, t, "Humo", "humo", 0.7f, 14f, TOscuro, 30f);
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
