using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Game.NPC;
using Sendero.Core.Feedback;
using UnityEngine;
using UnityEngine.AI;

/// El Mago Oscuro en la batalla final (GDD § 19 y fila 6 de la ruta post-Caja; novela, «Tiempo 1–3»).
/// Tres fases, cada una con una regla nueva que se enseña, se demuestra y se practica:
///
///  1. Patrones: se teletransporta y lanza salvas de tres rayos (se pueden devolver con la B:
///     devolverle su propio rayo le aturde y le deja expuesto), grietas en el suelo, una nova si
///     te acercas y el Pozo del Sendero si estás lejos (el agujero negro del prólogo en pequeño:
///     atrae mientras crece, hay que correr en contra). Tras cada salva o pozo queda agotado un
///     momento: es la ventana.
///  2. El Sendero se deforma: vuela sostenido por cuatro anclas en lo alto de los pilares, el
///     suelo se corrompe por zonas y llueve sombra. Rotas las anclas (Liam las rompe antes), se
///     desploma y queda expuesto. Hay que moverse, subir a las plataformas o volar.
///  3. Los conductos: se funde con el altar y tres nodos le alimentan. Hay que cortarlos casi a
///     la vez: compensa separar al grupo (los aliados drenan el nodo que tienen cerca) mientras él
///     invoca sombras para distraer. Cortados, queda expuesto.
///
/// Entre la 2 y la 3, la Marea de Sombra: la primera vez es inevitable y Will usa el Hechizo del
/// Tiempo (se rebobina todo; se conservan heridas y cansancio); la segunda se ve el resquicio.
/// Al llegar al final (15 %) entra FinalDelConducto: la traición, Liam, Estela y la aguja de luz.
///
/// Solo se le hace daño expuesto (SoloDanoCuandoExpuesto con curación 0, en el mismo objeto), y
/// cada fase tiene un suelo de vida: no se salta una fase a golpes. Ver INC-509.
[RequireComponent(typeof(Damageable))]
public sealed class MagoOscuroBossAI : MonoBehaviour, IJefeConFases, IExpuestoAlDano, IInicioDeCombate, IFiltroDeDano
{
    [Header("Referencias")]
    [SerializeField] private EscenarioBatallaFinal escenario;
    [SerializeField] private FinalDelConducto final;
    [Tooltip("El rayo que lanza (MagoOscuroGolpe): su prefab, velocidad y efectos.")]
    [SerializeField] private MagicSpellSO golpe;
    [Tooltip("Quién guía al jugador (Estela). Vacío = nadie.")]
    [SerializeField] private GuionDeCombate guion;
    [SerializeField] private bool empezarSolo = true;
    [SerializeField] private float esperaInicial = 1.5f;

    [Header("Efectos")]
    [Tooltip("Sombra que avisa en el suelo antes de un golpe.")]
    [SerializeField] private GameObject vfxAviso;
    [Tooltip("Efecto de una grieta o zona de sombra activa.")]
    [SerializeField] private GameObject vfxZona;
    [SerializeField] private GameObject vfxTeletransporte;
    [Tooltip("Brillo en la mano mientras carga una salva.")]
    [SerializeField] private GameObject vfxCarga;
    [Tooltip("Trozo de la ola de la Marea de Sombra.")]
    [SerializeField] private GameObject vfxMarea;
    [Tooltip("Marca de luz a lo largo del resquicio de la Marea.")]
    [SerializeField] private GameObject vfxResquicio;
    [SerializeField] private GameObject vfxInvocacion;
    [Tooltip("Efecto sobre Will mientras se rebobina el tiempo.")]
    [SerializeField] private GameObject vfxRebobinado;

    [Header("Umbrales de fase (vida 0..1)")]
    [SerializeField] private float umbralFase2 = 0.7f;
    [SerializeField] private float umbralFase3 = 0.4f;
    [SerializeField] private float umbralFinal = 0.15f;

    [Header("Fase 1: patrones")]
    [SerializeField] private int balasPorSalva = 3;
    [SerializeField] private float intervaloSalva = 0.3f;
    [SerializeField] private float avisoSalva = 0.65f;
    [SerializeField] private float danoBala = 14f;
    [Tooltip("Segundos expuesto tras cada salva en la fase 1.")]
    [SerializeField] private float agotamiento = 1.4f;
    [SerializeField] private int tramosGrieta = 6;
    [SerializeField] private float danoGrieta = 16f;
    [SerializeField] private float radioNova = 5f;
    [SerializeField] private float danoNova = 22f;
    [SerializeField] private float aturdidoPorContraataque = 3.5f;
    [SerializeField] private float pausaFase1 = 1.1f;

    [Header("Pozo del Sendero (fases 1 y 3)")]
    [Tooltip("El agujero negro del prólogo; aquí se usa en pequeño.")]
    [SerializeField] private GameObject vfxPozo;
    [SerializeField] private GameObject vfxImplosionPozo;
    [SerializeField] private float cargaPozo = 3f;
    [SerializeField] private float radioAtraccionPozo = 13f;
    [Tooltip("Metros por segundo cerca del centro. Menos que correr: se escapa corriendo en contra.")]
    [SerializeField] private float velocidadAtraccionPozo = 3.2f;
    [SerializeField] private float radioImplosionPozo = 3.5f;
    [SerializeField] private float danoPozo = 30f;
    [SerializeField] private float empujePozo = 10f;
    [Tooltip("Segundos mínimos entre dos pozos.")]
    [SerializeField] private float cadaPozo = 14f;

    [Header("Fase 2: el Sendero se deforma")]
    [SerializeField] private float alturaVuelo = 7f;
    [SerializeField] private float tiempoEntrePuntos = 1.6f;
    [SerializeField] private float cadaCorrupcion = 9f;
    [SerializeField] private float radioCorrupcion = 7f;
    [SerializeField] private float danoCorrupcion = 8f;
    [SerializeField] private int gotasLluvia = 6;
    [SerializeField] private float danoLluvia = 14f;
    [Tooltip("Segundos en el suelo, expuesto, cuando caen todas las anclas.")]
    [SerializeField] private float derribado = 6f;
    [SerializeField] private float pausaFase2 = 0.9f;

    [Header("Marea de Sombra (Tiempo 2)")]
    [SerializeField] private float velocidadMarea = 7f;
    [SerializeField] private float anchoResquicio = 34f;
    [SerializeField] private float danoMarea = 45f;
    [Tooltip("Segundos de reloj real que dura el rebobinado a la vista.")]
    [SerializeField] private float duracionRebobinado = 2.5f;

    [Header("Fase 3: los conductos")]
    [SerializeField] private GameObject prefabSombra;
    [SerializeField] private int sombrasPorOleada = 2;
    [SerializeField] private float cadaOleada = 16f;
    [Tooltip("Segundos expuesto cuando se cortan todos los conductos.")]
    [SerializeField] private float expuestoTrasCorte = 8f;
    [SerializeField] private float pausaFase3 = 2.2f;

    // ── Estado ────────────────────────────────────────────────────────────
    private Damageable _vida;
    private NPCSimpleAnimator _anim;
    private TransicionDeFaseDeJefe _transicion;
    private Renderer[] _renderers;
    private float[] _umbrales;
    private int _fase;
    private bool _empezado;
    private float _expuestoHasta = -1f;
    private bool _aturdir;
    private bool _derribar;
    private bool _cortados;
    private int _ataquesDesdeSalto;
    private int _puntoVuelo;
    private float _novaLista, _grietaLista, _pozoListo, _siguienteCorrupcion, _siguienteOleada;
    private readonly List<GameObject> _sombras = new();
    private GuiaDeCombate _guia;
    private PlayerActionManager _accion;
    private bool _cinematicaPuesta;

    // ── Interfaces ────────────────────────────────────────────────────────
    public int Fase => _fase;
    public IReadOnlyList<float> UmbralesDeFase => _umbrales;
    public event Action<int> AlCambiarDeFase;
    public bool Expuesto => Time.time < _expuestoHasta && _vida != null && _vida.IsAlive;
    public float Vida => _vida != null && _vida.Max > 0f ? _vida.Current / _vida.Max : 0f;
    public EscenarioBatallaFinal Escenario => escenario;
    public NPCSimpleAnimator Animador => _anim;

    /// Suelo de vida de la fase actual: no se salta una fase a golpes (el cambio lo hace la IA).
    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (_vida == null) return cantidad;
        float suelo = _vida.Max * UmbralActual();
        return Mathf.Min(cantidad, Mathf.Max(0f, _vida.Current - suelo));
    }

    private float UmbralActual() => _fase switch { 0 => umbralFase2, 1 => umbralFase3, _ => umbralFinal };

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _vida.SetDestroyOnDeath(false);
        _anim = GetComponentInChildren<NPCSimpleAnimator>();
        _transicion = GetComponent<TransicionDeFaseDeJefe>();
        if (_transicion == null) _transicion = gameObject.AddComponent<TransicionDeFaseDeJefe>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _umbrales = new[] { umbralFase2, umbralFase3 };
        var agente = GetComponent<NavMeshAgent>();
        if (agente) agente.enabled = false;   // se mueve a saltos y volando, no andando
    }

    void OnEnable() => PlayerShieldController.AlDevolverAtaque += AlDevolverAtaque;

    void OnDisable()
    {
        PlayerShieldController.AlDevolverAtaque -= AlDevolverAtaque;
        SoltarCinematica();
    }

    IEnumerator Start()
    {
        if (!empezarSolo) yield break;
        yield return new WaitForSeconds(esperaInicial);
        EmpezarCombate();
    }

    public void EmpezarCombate()
    {
        if (_empezado) return;
        _empezado = true;
        _pozoListo = Time.time + cadaPozo * 0.5f;
        ActiveCombatRegistry.RegisterNPC(gameObject);
        var bossBar = GetComponent<BossHealthBar>();
        if (bossBar) bossBar.Show();
        StartCoroutine(Co_Combate());
    }

    private void AlDevolverAtaque(GameObject origen)
    {
        if (origen == gameObject) _aturdir = true;
    }

    void LateUpdate()
    {
        var conducto = escenario != null ? escenario.conductoPrincipal : null;
        if (conducto == null || escenario.altar == null || !conducto.enabled) return;
        conducto.SetPosition(0, escenario.altar.position + Vector3.up * 1.5f);
        conducto.SetPosition(1, transform.position + Vector3.up * 1.2f);
    }

    // ── El combate ────────────────────────────────────────────────────────

    private IEnumerator Co_Combate()
    {
        SeguirEnElRegistro();
        yield return Co_Presentacion();

        _guia = GuiaDeCombate.Empezar(gameObject, guion, gameObject);
        if (_guia != null) _guia.LanzarIntervencion();

        _fase = 0;
        while (Vida > umbralFase2 + 0.001f) yield return Co_TurnoFase1();

        yield return Co_Transicion(1);
        yield return Co_EntrarFase2();
        while (Vida > umbralFase3 + 0.001f) yield return Co_TurnoFase2();
        yield return Co_SalirFase2();

        yield return Co_Marea();

        yield return Co_Transicion(2);
        yield return Co_EntrarFase3();
        while (Vida > umbralFinal + 0.001f) yield return Co_TurnoFase3();
        yield return Co_SalirFase3();

        if (_guia != null) _guia.Parar();
        if (final != null) yield return final.Ejecutar(this);
    }

    private IEnumerator Co_Presentacion()
    {
        MirarAlJugador(1f);
        _anim?.PlaySocialGesture("Challenging_NoWeapon");
        yield return new WaitForSeconds(Bocadillos.Decir(transform, "FINAL_MAGO_INTRO_01", "Mago Oscuro"));
        yield return new WaitForSeconds(Bocadillos.Decir(transform, "FINAL_MAGO_INTRO_02", "Mago Oscuro"));
    }

    private IEnumerator Co_Transicion(int faseNueva)
    {
        _fase = faseNueva;
        AlCambiarDeFase?.Invoke(_fase);
        _expuestoHasta = -1f;
        _anim?.PlaySocialGesture("MagicSpecial");
        _transicion.Cargar();
        yield return new WaitForSeconds(0.8f);
        _transicion.Estallar(_fase, faseNueva == 2);
        yield return new WaitForSeconds(1f);
    }

    // ── Fase 1 ────────────────────────────────────────────────────────────

    private IEnumerator Co_TurnoFase1()
    {
        if (_aturdir) { yield return Co_Aturdido(aturdidoPorContraataque, exponer: true); yield break; }

        float d = DistanciaAlJugador();
        if (d < radioNova + 1f && Time.time >= _novaLista)
            yield return Co_Nova();
        else if (d > radioNova + 2f && Time.time >= _pozoListo && UnityEngine.Random.value < 0.35f)
            yield return Co_Pozo(exponerDespues: true);
        else if (Time.time >= _grietaLista && UnityEngine.Random.value < 0.4f)
            yield return Co_Grieta();
        else
            yield return Co_Salva(exponerDespues: true);

        if (_aturdir) yield break;
        if (++_ataquesDesdeSalto >= 2)
        {
            _ataquesDesdeSalto = 0;
            yield return Co_Teletransporte(PuntoLejosDelJugador(escenario != null ? escenario.puntosDeSalto : null));
        }
        yield return Esperar(pausaFase1);
    }

    /// Tres rayos seguidos al jugador tras un aviso (brillo en la mano). Se pueden devolver (B).
    private IEnumerator Co_Salva(bool exponerDespues)
    {
        MirarAlJugador(1f);
        _anim?.PlaySocialGesture("MagicRight");
        if (vfxCarga && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxCarga, PuntoDeLanzamiento(), Quaternion.identity, avisoSalva + 0.2f, transform);
        yield return Esperar(avisoSalva, girarAlJugador: true);

        for (int i = 0; i < balasPorSalva && !_aturdir; i++)
        {
            Disparar();
            yield return Esperar(intervaloSalva, girarAlJugador: true);
        }

        if (exponerDespues && !_aturdir)
        {
            _expuestoHasta = Time.time + agotamiento;
            yield return Esperar(agotamiento);
        }
    }

    private IEnumerator Co_Grieta()
    {
        _grietaLista = Time.time + 6f;
        MirarAlJugador(1f);
        _anim?.PlaySocialGesture("MagicSpecial");
        yield return Esperar(0.4f);

        Vector3 origen = transform.position;
        Vector3 dir = Jugador() != null ? Jugador().position - origen : transform.forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
        for (int i = 0; i < tramosGrieta; i++)
        {
            Zona(origen + dir * (2.5f + i * 2.4f), 1.8f, 0.8f + i * 0.12f, 0.4f, danoGrieta, 1f, 0f);
        }
        yield return Esperar(1.2f);
    }

    private IEnumerator Co_Nova()
    {
        _novaLista = Time.time + 7f;
        _anim?.PlaySocialGesture("MagicSpecial");
        Zona(transform.position, radioNova, 1.1f, 0f, danoNova, 1f, 10f);
        yield return Esperar(1.3f);
        _expuestoHasta = Time.time + 1f;
        yield return Esperar(1f);
        yield return Co_Teletransporte(PuntoLejosDelJugador(escenario != null ? escenario.puntosDeSalto : null));
    }

    /// Pozo del Sendero: brazos arriba, nace delante de él el agujero negro y atrae al jugador
    /// mientras crece; al implosionar hace daño cerca. Quien corre en contra escapa.
    private IEnumerator Co_Pozo(bool exponerDespues)
    {
        _pozoListo = Time.time + cadaPozo;
        MirarAlJugador(1f);
        _anim?.HoldPose("MagicAttackOmni01_Load");

        Vector3 centro = transform.position + transform.forward * 2f;
        centro.y = SueloEn(centro);
        PozoGravitatorio.Crear(centro, new PozoGravitatorio.Config
        {
            carga = cargaPozo,
            radioAtraccion = radioAtraccionPozo,
            velocidadAtraccion = velocidadAtraccionPozo,
            radioImplosion = radioImplosionPozo,
            dano = danoPozo,
            empuje = empujePozo,
            vfxPozo = vfxPozo,
            escalaInicial = 0.2f,
            escalaFinal = 1f,
            alturaVfx = 1.8f,
            vfxImplosion = vfxImplosionPozo,
            sfxCarga = "SFX_Prologo_AgujeroNegro_Carga",
            sfxImplosion = "SFX_Prologo_Explosion",
        });

        yield return Esperar(cargaPozo + 0.3f);
        _anim?.ReleasePose(true);

        if (exponerDespues && !_aturdir)
        {
            _expuestoHasta = Time.time + agotamiento;
            yield return Esperar(agotamiento);
        }
    }

    private IEnumerator Co_Aturdido(float segundos, bool exponer)
    {
        _aturdir = false;
        _anim?.HoldPose("Dizzy_NoWeapon");
        if (exponer) _expuestoHasta = Time.time + segundos;
        FeedbackService.CameraShake(0.3f, 0.3f);
        yield return new WaitForSeconds(segundos);
        _anim?.ReleasePose(true);
    }

    // ── Fase 2 ────────────────────────────────────────────────────────────

    private IEnumerator Co_EntrarFase2()
    {
        _anim?.HoldPose("fly_idle");
        yield return Co_Mover(transform.position + Vector3.up * alturaVuelo, 1.2f);
        ActivarAnclas();
        _siguienteCorrupcion = Time.time + 4f;
    }

    private void ActivarAnclas()
    {
        if (escenario == null || escenario.anclas == null) return;
        foreach (var a in escenario.anclas)
        {
            if (a == null) continue;
            a.AlRomperse -= AlRomperseAncla;
            a.AlRomperse += AlRomperseAncla;
            a.Activar(transform);
        }
    }

    private void AlRomperseAncla(AnclaDelSendero _)
    {
        foreach (var a in escenario.anclas) if (a != null && a.Activa) return;
        _derribar = true;
    }

    private IEnumerator Co_TurnoFase2()
    {
        if (_derribar) { _derribar = false; yield return Co_Derribado(); yield break; }
        if (_aturdir) { _aturdir = false; yield return Esperar(1.2f); }

        if (Time.time >= _siguienteCorrupcion)
        {
            Corromper();
            _siguienteCorrupcion = Time.time + cadaCorrupcion;
        }

        var puntos = escenario != null ? escenario.puntosDeVuelo : null;
        if (puntos != null && puntos.Length > 0)
        {
            _puntoVuelo = (_puntoVuelo + 1 + UnityEngine.Random.Range(0, 2)) % puntos.Length;
            yield return Co_Mover(puntos[_puntoVuelo].position, tiempoEntrePuntos);
        }
        if (_derribar) yield break;

        if (UnityEngine.Random.value < 0.55f) yield return Co_Salva(exponerDespues: false);
        else yield return Co_Lluvia();

        yield return Esperar(pausaFase2);
    }

    private void Corromper()
    {
        var cuadrantes = escenario != null ? escenario.cuadrantes : null;
        if (cuadrantes == null || cuadrantes.Length == 0 || Jugador() == null) return;
        // El trozo de suelo donde está el jugador: le obliga a moverse (o a volar).
        Transform mejor = cuadrantes[0];
        float mejorD = float.MaxValue;
        foreach (var c in cuadrantes)
        {
            if (c == null) continue;
            float d = (c.position - Jugador().position).sqrMagnitude;
            if (d < mejorD) { mejorD = d; mejor = c; }
        }
        Zona(mejor.position, radioCorrupcion, 2f, 5f, danoCorrupcion, 0.5f, 0f);
    }

    private IEnumerator Co_Lluvia()
    {
        _anim?.PlaySocialGesture("MagicSpecial");
        Transform j = Jugador();
        if (j == null) yield break;
        for (int i = 0; i < gotasLluvia; i++)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * 6f;
            Vector3 p = j.position + new Vector3(r.x, 0f, r.y);
            if (i == 0) p = j.position;   // una siempre donde estás: hay que moverse
            p.y = SueloEn(p);
            Zona(p, 1.8f, 1.2f + i * 0.1f, 0f, danoLluvia, 1f, 0f);
        }
        yield return Esperar(1.6f);
    }

    /// Caen todas las anclas: se desploma y queda expuesto en el suelo.
    private IEnumerator Co_Derribado()
    {
        _anim?.HoldPose("fly_dive");
        Vector3 suelo = transform.position;
        suelo.y = SueloEn(suelo);
        yield return Co_Mover(suelo, 0.6f);
        FeedbackService.CameraShake(0.6f, 0.4f);
        _anim?.HoldPose("Dizzy_NoWeapon");
        _expuestoHasta = Time.time + derribado;
        yield return new WaitForSeconds(derribado);
        _anim?.ReleasePose(true);

        if (Vida <= umbralFase3 + 0.001f) yield break;
        _anim?.HoldPose("fly_idle");
        yield return Co_Mover(transform.position + Vector3.up * alturaVuelo, 1f);
        ActivarAnclas();
    }

    private IEnumerator Co_SalirFase2()
    {
        if (escenario != null && escenario.anclas != null)
            foreach (var a in escenario.anclas) if (a != null) a.Apagar();
        _anim?.ReleasePose(true);
        _expuestoHasta = -1f;
        yield return null;
    }

    // ── La Marea de Sombra (Tiempo 2) ─────────────────────────────────────

    private IEnumerator Co_Marea()
    {
        _expuestoHasta = -1f;
        Transform altar = escenario != null ? escenario.puntoDelAltar : null;
        if (altar != null) yield return Co_Teletransporte(altar.position);
        _anim?.HoldPose("MagicSpecial");
        yield return new WaitForSeconds(Bocadillos.Decir(transform, "FINAL_MAGO_MAREA", "Mago Oscuro"));

        float angulo = UnityEngine.Random.Range(0f, 360f);
        Vector3 centro = transform.position;

        // Primera vez: no hay resquicio. Justo antes de alcanzar a Will, el tiempo se para.
        var primera = MareaDeSombra.Crear(centro, ConfigMarea(angulo, ancho: 0f, mostrar: false));
        while (!primera.Terminada && primera.DistanciaAlJugador() - primera.Radio > 2.2f)
            yield return null;
        primera.Pausada = true;

        yield return Co_HechizoDelTiempo();
        if (primera != null) Destroy(primera.gameObject);

        // Segunda vez: Will ya sabe dónde está el resquicio.
        if (altar != null) transform.position = altar.position;
        _anim?.HoldPose("MagicSpecial");
        yield return new WaitForSeconds(1.2f);
        var segunda = MareaDeSombra.Crear(centro, ConfigMarea(angulo, anchoResquicio, mostrar: true));
        while (!segunda.Terminada) yield return null;
        _anim?.ReleasePose(true);
    }

    private MareaDeSombra.Config ConfigMarea(float angulo, float ancho, bool mostrar) => new MareaDeSombra.Config
    {
        radioMax = (escenario != null ? escenario.radio : 20f) * 2.2f,
        velocidad = velocidadMarea,
        anguloHueco = angulo,
        anchoHueco = ancho,
        altura = 14f,
        dano = danoMarea,
        empuje = 12f,
        piezas = 40,
        vfxPieza = vfxMarea,
        escalaPieza = 1.4f,
        mostrarHueco = mostrar,
        vfxHueco = vfxResquicio,
    };

    /// El tiempo se para; Will lanza el Hechizo del Tiempo (el jugador confirma); se rebobina.
    /// No se puede fallar: si no se confirma, se lanza solo al rato.
    private IEnumerator Co_HechizoDelTiempo()
    {
        PonerCinematica();
        TimeScaleArbiterService.Request(this, 0.03f);
        FeedbackService.ScreenFlash(new Color(0.6f, 0.7f, 1f, 0.35f), 0.4f);

        Transform will = Jugador();
        float espera = Bocadillos.Decir(will, "FINAL_TIEMPO_WILL", "Will", 3f, fijo: true);
        yield return new WaitForSecondsRealtime(espera);
        Bocadillos.Decir(will, "FINAL_TIEMPO_PULSA", "Will", 6f, fijo: true);

        float limite = Time.unscaledTime + 8f;
        while (Time.unscaledTime < limite && !GamepadInputReader.SubmitPressed) yield return null;

        FeedbackService.ScreenFlash(new Color(0.4f, 0.6f, 1f, 0.6f), 0.6f);
        if (vfxRebobinado && will != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRebobinado, will.position + Vector3.up, Quaternion.identity, duracionRebobinado + 1f, will);
        if (escenario != null && escenario.registro != null)
            yield return escenario.registro.Rebobinar(duracionRebobinado);

        TimeScaleArbiterService.Release(this);
        SoltarCinematica();
        yield return new WaitForSeconds(Bocadillos.Decir(will, "FINAL_TIEMPO_RESQUICIO", "Will", 2.8f, fijo: true) * 0.5f);
    }

    // ── Fase 3 ────────────────────────────────────────────────────────────

    private IEnumerator Co_EntrarFase3()
    {
        Transform altar = escenario != null ? escenario.puntoDelAltar : null;
        if (altar != null) yield return Co_Teletransporte(altar.position);
        _anim?.HoldPose("MagicSpecial");
        var red = escenario != null ? escenario.red : null;
        if (red != null)
        {
            red.AlCortarseTodos -= AlCortarseConductos;
            red.AlCortarseTodos += AlCortarseConductos;
            red.Activar(escenario.altar);
        }
        _siguienteOleada = Time.time + 6f;
    }

    private void AlCortarseConductos() => _cortados = true;

    private IEnumerator Co_TurnoFase3()
    {
        if (_cortados) { _cortados = false; yield return Co_ConductosCortados(); yield break; }
        if (_aturdir) { _aturdir = false; yield return Esperar(1f); }

        if (Time.time >= _siguienteOleada)
        {
            Invocar();
            _siguienteOleada = Time.time + cadaOleada;
        }

        if (Time.time >= _pozoListo && UnityEngine.Random.value < 0.3f)
            yield return Co_Pozo(exponerDespues: false);
        else
            yield return Co_Salva(exponerDespues: false);
        _anim?.HoldPose("MagicSpecial");
        yield return Esperar(pausaFase3 * UnityEngine.Random.Range(0.8f, 1.3f));
    }

    private IEnumerator Co_ConductosCortados()
    {
        if (escenario != null && escenario.conductoPrincipal) escenario.conductoPrincipal.enabled = false;
        _anim?.HoldPose("Dizzy_NoWeapon");
        FeedbackService.CameraShake(0.7f, 0.5f);
        _expuestoHasta = Time.time + expuestoTrasCorte;
        yield return new WaitForSeconds(expuestoTrasCorte);
        _anim?.HoldPose("MagicSpecial");
        if (escenario != null && escenario.conductoPrincipal) escenario.conductoPrincipal.enabled = true;
        if (Vida > umbralFinal + 0.001f && escenario != null && escenario.red != null)
            escenario.red.Activar(escenario.altar);
    }

    private void Invocar()
    {
        if (prefabSombra == null || escenario == null || escenario.puntosDeInvocacion == null) return;
        var puntos = escenario.puntosDeInvocacion;
        for (int i = 0; i < sombrasPorOleada && puntos.Length > 0; i++)
        {
            Transform p = puntos[UnityEngine.Random.Range(0, puntos.Length)];
            Vector3 pos = p.position;
            if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) pos = hit.position;
            if (vfxInvocacion && VfxPoolService.Instance != null)
                VfxPoolService.Instance.Play(vfxInvocacion, pos, Quaternion.identity, 2f);
            var sombra = Instantiate(prefabSombra, pos, Quaternion.LookRotation(transform.position - pos));
            var tinte = sombra.GetComponent<TransicionDeFaseDeJefe>();
            if (tinte == null) tinte = sombra.AddComponent<TransicionDeFaseDeJefe>();
            tinte.Tintar(new Color(0.25f, 0.2f, 0.35f), apagarEmision: false);
            foreach (var inicio in sombra.GetComponentsInChildren<IInicioDeCombate>(true)) inicio.EmpezarCombate();
            _sombras.Add(sombra);
        }
    }

    private IEnumerator Co_SalirFase3()
    {
        if (escenario != null && escenario.red != null) escenario.red.Apagar();
        foreach (var s in _sombras)
        {
            if (s == null) continue;
            var d = s.GetComponent<Damageable>();
            if (d != null && d.IsAlive) d.Kill(); else Destroy(s);
        }
        _sombras.Clear();
        _anim?.ReleasePose(true);
        _expuestoHasta = -1f;
        yield return null;
    }

    // ── Para el final ─────────────────────────────────────────────────────

    /// Queda de rodillas, como vencido.
    public void Arrodillarse()
    {
        StopAllCoroutines();
        _expuestoHasta = -1f;
        _anim?.HoldPose("Beg01");
    }

    public void Gesto(string estado) => _anim?.PlaySocialGesture(estado);

    /// Pierde el vínculo con el altar: se deshace (se encoge y se apaga) y el combate termina.
    public IEnumerator Deshacerse(GameObject vfx)
    {
        if (escenario != null && escenario.conductoPrincipal) escenario.conductoPrincipal.enabled = false;
        if (vfx && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfx, transform.position + Vector3.up, Quaternion.identity, 3f);
        _anim?.HoldPose("Die01Stay_NoWeapon");
        Vector3 escala = transform.localScale;
        float t = 0f;
        while (t < 1.6f)
        {
            t += Time.deltaTime;
            float k = t / 1.6f;
            transform.localScale = Vector3.Lerp(escala, escala * 0.05f, k * k);
            transform.position += Vector3.up * Time.deltaTime * 0.6f;
            yield return null;
        }
        MostrarCuerpo(false);
        var bossBar = GetComponent<BossHealthBar>();
        if (bossBar) bossBar.Hide();
        ActiveCombatRegistry.UnregisterNPC(gameObject);
        _vida.Kill();
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    private Transform Jugador() => PlayerService.Player != null ? PlayerService.Player.transform : null;

    private float DistanciaAlJugador()
    {
        var j = Jugador();
        if (j == null) return float.MaxValue;
        Vector3 d = j.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private Vector3 PuntoDeLanzamiento() => transform.position + Vector3.up * 1.5f + transform.forward * 0.8f;

    private void Disparar()
    {
        var j = Jugador();
        if (golpe == null || golpe.prefab == null || j == null) return;

        Vector3 origen = PuntoDeLanzamiento();
        Vector3 dir = (j.position + Vector3.up * 1f - origen).normalized;
        var go = Instantiate(golpe.prefab, origen, Quaternion.LookRotation(dir));
        int capa = LayerMask.NameToLayer("ProjectileEnemy");
        if (capa >= 0) foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = capa;

        var mp = go.GetComponent<MagicProjectile>();
        if (mp == null) return;
        var cfg = new MagicProjectile.ProjectileConfig
        {
            damage = danoBala,
            knockbackForce = golpe.knockbackForce,
            hitLayers = LayerMask.GetMask("Player"),
            collisionLayers = LayerMask.GetMask("Player", "Default", "Floor"),
            destroyOnHit = true,
            lifeTime = 4f,
            maxRange = 50f,
            initialSpeed = golpe.initialSpeed,
            useGravity = false,
            impactVFX = golpe.impactVFX,
            despawnVFX = golpe.despawnVFX,
            vfxLifetime = golpe.vfxLifetime,
            impactSFXKey = golpe.impactSFXKey,
            element = golpe.element,
        };
        mp.Configure(cfg, gameObject);
        mp.Launch(dir, golpe.initialSpeed, false);
    }

    private void Zona(Vector3 centro, float radio, float aviso, float duracion, float dano, float intervalo, float empuje)
    {
        centro.y = SueloEn(centro);
        ZonaDanina.Crear(centro, new ZonaDanina.Config
        {
            radio = radio, aviso = aviso, duracion = duracion, dano = dano, intervalo = intervalo,
            empuje = empuje, vfxAviso = vfxAviso, vfxActiva = vfxZona, escalaVfx = 1f,
        });
    }

    private float SueloEn(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 60f, LayerMask.GetMask("Default", "Floor"), QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return escenario != null && escenario.centro != null ? escenario.centro.position.y : p.y;
    }

    private Vector3 PuntoLejosDelJugador(Transform[] puntos)
    {
        var j = Jugador();
        if (puntos == null || puntos.Length == 0 || j == null) return transform.position;
        // Uno de los tres más lejanos al jugador, al azar: se aleja, pero no siempre al mismo sitio.
        var orden = new List<Transform>(puntos);
        orden.RemoveAll(p => p == null);
        orden.Sort((a, b) => (b.position - j.position).sqrMagnitude.CompareTo((a.position - j.position).sqrMagnitude));
        return orden[UnityEngine.Random.Range(0, Mathf.Min(3, orden.Count))].position;
    }

    private IEnumerator Co_Teletransporte(Vector3 destino)
    {
        var pool = VfxPoolService.Instance;
        if (vfxTeletransporte && pool != null) pool.Play(vfxTeletransporte, transform.position + Vector3.up, Quaternion.identity, 1.5f);
        MostrarCuerpo(false);
        yield return new WaitForSeconds(0.35f);
        transform.position = destino;
        if (vfxTeletransporte && pool != null) pool.Play(vfxTeletransporte, destino + Vector3.up, Quaternion.identity, 1.5f);
        MostrarCuerpo(true);
        MirarAlJugador(1f);
    }

    private IEnumerator Co_Mover(Vector3 destino, float segundos)
    {
        Vector3 desde = transform.position;
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(desde, destino, Mathf.SmoothStep(0f, 1f, t / segundos));
            MirarAlJugador(Time.deltaTime * 6f);
            yield return null;
        }
        transform.position = destino;
    }

    /// Espera 'segundos' sin dejar de reaccionar: se corta si le devuelven un rayo (fase 1) o si
    /// caen las anclas (fase 2).
    private IEnumerator Esperar(float segundos, bool girarAlJugador = false)
    {
        float fin = Time.time + segundos;
        while (Time.time < fin)
        {
            if (_aturdir && _fase == 0) yield break;
            if (_derribar || _cortados) yield break;
            if (girarAlJugador) MirarAlJugador(Time.deltaTime * 8f);
            yield return null;
        }
    }

    private void MirarAlJugador(float factor)
    {
        var j = Jugador();
        if (j == null) return;
        Vector3 d = j.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Mathf.Clamp01(factor));
        _anim?.SyncTargetRotation();
    }

    private void MostrarCuerpo(bool visible)
    {
        foreach (var r in _renderers) if (r) r.enabled = visible;
    }

    private void SeguirEnElRegistro()
    {
        var registro = escenario != null ? escenario.registro : null;
        if (registro == null) return;
        if (Jugador() != null) registro.Seguir(Jugador());
        registro.Seguir(transform);
        if (PlayerParty.HasInstance)
            foreach (var m in PlayerParty.Instance.Members) if (m != null) registro.Seguir(m.transform);
    }

    private void PonerCinematica()
    {
        if (_cinematicaPuesta) return;
        if (_accion == null) _accion = ServiceLocator.Get<PlayerActionManager>(logIfMissing: false);
        if (_accion == null) return;
        _accion.PushMode(ActionMode.Cinematic);
        _cinematicaPuesta = true;
    }

    private void SoltarCinematica()
    {
        if (!_cinematicaPuesta || _accion == null) return;
        _accion.PopMode(ActionMode.Cinematic);
        _cinematicaPuesta = false;
    }
}
