using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Game.NPC;
using Sendero.Core.Feedback;
using UnityEngine;
using UnityEngine.AI;

/// El Mago Oscuro en la batalla final (GDD § 19). Es el jefe más difícil: pone a prueba todo lo
/// que el jugador ya sabe, sin enseñar nada nuevo. Al empezar solo está el altar; lo demás lo
/// conjura él (EscenarioBatallaFinal).
///
///  1. Patrones: se teletransporta, lanza salvas de rayos (devolverle uno con la defensa le aturde
///     y le rompe una espina), grietas, una nova si te acercas y el Pozo (el agujero negro del
///     prólogo en pequeño) si estás lejos. Conjura espinas: mientras queden, recibe poco daño y
///     cada una suma un rayo a sus salvas. Rotas todas, queda aturdido un rato y vuelve a
///     conjurarlas.
///  2. Lo levanta todo: conjura pilares con anclas, plataformas y lanzadores, y vuela de pilar en
///     pilar. El suelo se corrompe donde estás y llueve sombra. Rotas las anclas, se desploma y
///     queda expuesto; si sigue en pie, conjura el siguiente juego.
///  3. Al llegar al umbral de regeneración, vuelve al altar y el altar le devuelve toda la vida:
///     desde ahí ningún golpe le hace nada. Lanza la Marea: la primera no tiene hueco, el tiempo se
///     para y Will rebobina (se conservan heridas y cansancio); después Will reúne al grupo y su
///     escudo aguanta la segunda. Nada de esto se puede fallar.
///
/// Expuesto (aturdido o derribado) recibe más daño. Cada fase tiene un suelo de vida: no se salta
/// a golpes. Ver INC-509, INC-663.
[RequireComponent(typeof(Damageable))]
public sealed class MagoOscuroBossAI : MonoBehaviour, IJefeConFases, IExpuestoAlDano, IInicioDeCombate, IFiltroDeDano
{
    [Header("Referencias")]
    [SerializeField] private EscenarioBatallaFinal escenario;
    [SerializeField] private FinalDelConducto final;
    [Tooltip("El rayo que lanza (MagoOscuroGolpe): su prefab, velocidad y efectos.")]
    [SerializeField] private MagicSpellSO golpe;
    [Tooltip("Los comentarios de los compañeros durante el combate. Vacío = nadie.")]
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
    [Tooltip("Efecto sobre Will mientras se rebobina el tiempo.")]
    [SerializeField] private GameObject vfxRebobinado;
    [Tooltip("Efecto sobre el Mago mientras el altar le devuelve la vida.")]
    [SerializeField] private GameObject vfxRegeneracion;
    [Tooltip("Aura en bucle mientras algún cristal protege al Mago.")]
    [SerializeField] private GameObject vfxProtegido;

    [Header("Levitación y desplazamiento")]
    [Tooltip("Altura del modelo sobre su cápsula durante el combate.")]
    [SerializeField, Min(0f)] private float alturaLevitacion = 0.5f;
    [Tooltip("Segundos para recuperar la levitación al dejar de estar expuesto.")]
    [SerializeField, Min(0.05f)] private float transicionLevitacion = 0.35f;
    [Tooltip("Distancias mínima y máxima al jugador durante el deslizamiento en arco.")]
    [SerializeField] private Vector2 distanciaDeslizamiento = new(8f, 11f);
    [Tooltip("Duraciones mínima y máxima del deslizamiento entre ataques.")]
    [SerializeField] private Vector2 duracionDeslizamiento = new(1.2f, 1.8f);
    [Tooltip("Grados que recorre alrededor del jugador entre ataques.")]
    [SerializeField, Range(10f, 90f)] private float arcoDeslizamiento = 35f;
    [Tooltip("Metros que intenta separarse del jugador en una retirada rápida.")]
    [SerializeField, Min(1f)] private float distanciaRetirada = 5f;
    [Tooltip("Segundos que dura la retirada rápida.")]
    [SerializeField, Min(0.1f)] private float duracionRetirada = 0.5f;
    [Tooltip("Segundos de cercanía continua que permiten escapar por teletransporte.")]
    [SerializeField, Min(0.1f)] private float tiempoAcorralado = 1.5f;
    [Tooltip("Separación del cuerpo respecto al altar y los objetos conjurados.")]
    [SerializeField, Min(0.1f)] private float margenObstaculos = 0.8f;
    [Tooltip("Radio de los desplazamientos cortos alrededor del punto de vuelo.")]
    [SerializeField, Min(0.1f)] private float radioDerivaVuelo = 1.2f;

    [Header("Umbrales (vida 0..1)")]
    [SerializeField] private float umbralFase2 = 0.7f;
    [Tooltip("Al llegar aquí, el altar le devuelve toda la vida y empieza la Marea.")]
    [SerializeField] private float umbralRegeneracion = 0.35f;

    [Header("Daño que recibe")]
    [Tooltip("Parte del daño que le llega mientras le protege algún cristal.")]
    [SerializeField, Range(0f, 1f)] private float danoConCristales = 0.25f;
    [Tooltip("Multiplicador del daño mientras está expuesto (aturdido o derribado).")]
    [SerializeField, Min(1f)] private float danoExpuesto = 1.5f;

    [Header("Fase 1: patrones")]
    [SerializeField] private int balasPorSalva = 3;
    [SerializeField] private float intervaloSalva = 0.3f;
    [SerializeField] private float avisoSalva = 0.65f;
    [SerializeField] private float danoBala = 14f;
    [Tooltip("Pausa tras cada salva o pozo.")]
    [SerializeField] private float pausaTrasAtaque = 0.8f;
    [SerializeField] private int tramosGrieta = 6;
    [SerializeField] private float danoGrieta = 16f;
    [SerializeField] private float radioNova = 5f;
    [SerializeField] private float danoNova = 22f;
    [Tooltip("Segundos aturdido y expuesto cuando le devuelven un rayo.")]
    [SerializeField] private float aturdidoPorContraataque = 3.5f;
    [Tooltip("Segundos aturdido y expuesto cuando caen todas sus espinas.")]
    [SerializeField] private float aturdidoSinEspinas = 4.5f;
    [Tooltip("Segundos hasta que vuelve a conjurar las espinas tras perderlas.")]
    [SerializeField] private float cadaEspinas = 10f;

    [Header("Pozo (fase 1)")]
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

    [Header("Fase 2: lo levanta todo")]
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

    [Header("Regeneración")]
    [Tooltip("Segundos que tarda el altar en devolverle toda la vida.")]
    [SerializeField] private float duracionRegeneracion = 2.5f;

    [Header("Marea de Sombra")]
    [SerializeField] private float velocidadMarea = 7f;
    [SerializeField] private float danoMarea = 45f;
    [Tooltip("Segundos de reloj real que dura el rebobinado a la vista.")]
    [SerializeField] private float duracionRebobinado = 2.5f;
    [Tooltip("Metros entre el frente de la segunda ola y Will cuando el tiempo se frena para alzar el escudo.")]
    [SerializeField] private float distanciaDelEscudo = 5f;
    [Tooltip("Segundos de reloj real que espera a que se mantenga la defensa; después sigue solo.")]
    [SerializeField] private float esperaDelEscudo = 3f;

    // ── Estado ────────────────────────────────────────────────────────────
    private Damageable _vida;
    private NPCSimpleAnimator _anim;
    private TransicionDeFaseDeJefe _transicion;
    private Renderer[] _renderers;
    private float[] _umbrales;
    private int _fase;
    private bool _empezado;
    private bool _regenerado;
    private float _expuestoHasta = -1f;
    private bool _aturdir;
    private bool _sinCristales;
    private float _reconjurarEn = -1f;
    private int _juegoFase2;
    private JuegoDeConjuros _juegoActual;
    private ElevacionVisual _elevacion;
    private Transform _auraProtegido;
    private VfxPoolService _poolProtegido;
    private ulong _usoProtegido;
    private float _cercaDesde = -1f;
    private bool _levitando;
    private int _mascaraSuelo;
    private readonly List<Collider> _obstaculos = new();
    private int _puntoVuelo;
    private float _novaLista, _grietaLista, _pozoListo, _siguienteCorrupcion;
    private GuiaDeCombate _guia;
    private PlayerActionManager _accion;
    private bool _cinematicaPuesta;
    private bool _tiempoPedido;
    private readonly List<NPCPartyMember> _reunidos = new();

    // ── Interfaces ────────────────────────────────────────────────────────
    public int Fase => _fase;
    public IReadOnlyList<float> UmbralesDeFase => _umbrales;
    public event Action<int> AlCambiarDeFase;
    public bool Expuesto => Time.time < _expuestoHasta && _vida != null && _vida.IsAlive;
    public float Vida => _vida != null && _vida.Max > 0f ? _vida.Current / _vida.Max : 0f;
    public EscenarioBatallaFinal Escenario => escenario;
    public NPCSimpleAnimator Animador => _anim;

    /// Regenerado, ningún golpe le hace nada. Si no, expuesto recibe más y protegido por cristales
    /// mucho menos. Nunca baja del suelo de la fase actual: el cambio de fase lo hace la IA.
    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (_vida == null) return cantidad;
        if (_regenerado)
        {
            AvisosDeCombate.GolpeMalDado(gameObject);
            return 0f;
        }

        if (Expuesto) cantidad *= danoExpuesto;
        else if (CristalesActivos() > 0)
        {
            cantidad *= danoConCristales;
            AvisosDeCombate.GolpeMalDado(gameObject);
        }

        float suelo = _vida.Max * (_fase == 0 ? umbralFase2 : umbralRegeneracion);
        return Mathf.Min(cantidad, Mathf.Max(0f, _vida.Current - suelo));
    }

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _vida.SetDestroyOnDeath(false);
        _anim = GetComponentInChildren<NPCSimpleAnimator>();
        _elevacion = GetComponentInChildren<ElevacionVisual>();
        _mascaraSuelo = LayerMask.GetMask("Default", "Floor");
        if (escenario != null)
        {
            if (escenario.altar != null) _obstaculos.AddRange(escenario.altar.GetComponentsInChildren<Collider>(true));
            CachearObstaculos(escenario.espinas);
            if (escenario.fase2 != null) foreach (var juego in escenario.fase2) CachearObstaculos(juego);
        }
        _transicion = GetComponent<TransicionDeFaseDeJefe>();
        if (_transicion == null) _transicion = gameObject.AddComponent<TransicionDeFaseDeJefe>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _umbrales = new[] { umbralFase2, umbralRegeneracion };
        var agente = GetComponent<NavMeshAgent>();
        if (agente) agente.enabled = false;   // la IA controla su posición sin navegación
    }

    void OnEnable() => PlayerShieldController.AlDevolverAtaque += AlDevolverAtaque;

    void Update()
    {
        if (!_empezado || _fase != 0 || Expuesto || DistanciaAlJugador() >= radioNova)
            _cercaDesde = -1f;
        else if (_cercaDesde < 0f)
            _cercaDesde = Time.time;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        PlayerShieldController.AlDevolverAtaque -= AlDevolverAtaque;
        RecogerProteccion();
        SoltarLevitacion(0f);
        _anim?.ReleasePose(true);
        if (_juegoActual != null) Deshacer(_juegoActual);
        ActiveCombatRegistry.UnregisterNPC(gameObject);
        SoltarTiempo();
        SoltarGrupo();
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
        RecuperarLevitacion();
        _pozoListo = Time.time + cadaPozo * 0.5f;
        ActiveCombatRegistry.RegisterNPC(gameObject);
        var bossBar = GetComponent<BossHealthBar>();
        if (bossBar) bossBar.Show();
        StartCoroutine(Co_Combate());
    }

    /// Devolverle su propio rayo le aturde y le rompe un cristal.
    private void AlDevolverAtaque(GameObject origen)
    {
        if (origen != gameObject || _regenerado) return;
        _aturdir = true;
        RomperUnCristal();
    }

    // ── El combate ────────────────────────────────────────────────────────

    private IEnumerator Co_Combate()
    {
        SeguirEnElRegistro();
        yield return Co_Presentacion();
        SostenerLevitacion();

        _guia = GuiaDeCombate.Empezar(gameObject, guion, gameObject);
        if (_guia != null) _guia.LanzarIntervencion();

        _fase = 0;
        yield return Co_Conjurar(escenario != null ? escenario.espinas : null);
        while (Vida > umbralFase2 + 0.001f) yield return Co_TurnoFase1();
        yield return Co_SalirFase1();

        yield return Co_Transicion(1);
        yield return Co_EntrarFase2();
        while (Vida > umbralRegeneracion + 0.001f) yield return Co_TurnoFase2();
        yield return Co_SalirFase2();

        if (_guia != null) _guia.Parar();
        yield return Co_Regeneracion();
        yield return Co_Marea();

        if (final != null) yield return final.Ejecutar(this);
    }

    /// Como en el prólogo: no dice nada al aparecer.
    private IEnumerator Co_Presentacion()
    {
        MirarAlJugador(1f);
        _anim?.PlaySocialGesture("Challenging_NoWeapon");
        yield return new WaitForSeconds(1.4f);
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

    // ── Conjuros ──────────────────────────────────────────────────────────

    /// Hace aparecer un juego de conjuros y, cuando ya están, enciende sus cristales.
    private IEnumerator Co_Conjurar(JuegoDeConjuros juego)
    {
        if (juego == null) yield break;
        _juegoActual = juego;
        _sinCristales = false;
        _anim?.PlaySocialGesture("MagicSpecial");
        if (juego.objetos != null)
            foreach (var o in juego.objetos) if (o != null) o.Aparecer();
        yield return new WaitForSeconds(0.7f);
        if (juego.cristales == null) yield break;
        foreach (var c in juego.cristales)
        {
            if (c == null) continue;
            c.AlRomperse -= AlRomperseCristal;
            c.AlRomperse += AlRomperseCristal;
            c.Activar();
        }
        ActualizarProteccion();
    }

    private void Deshacer(JuegoDeConjuros juego)
    {
        if (juego == null) return;
        if (juego.cristales != null)
            foreach (var c in juego.cristales)
            {
                if (c == null) continue;
                c.AlRomperse -= AlRomperseCristal;
                c.Apagar();
            }
        if (juego.objetos != null)
            foreach (var o in juego.objetos) if (o != null) o.Deshacer();
        if (_juegoActual == juego) _juegoActual = null;
        ActualizarProteccion();
    }

    private void AlRomperseCristal(CristalProtector _)
    {
        if (CristalesActivos() == 0) _sinCristales = true;
        ActualizarProteccion();
    }

    private int CristalesActivos()
    {
        var cristales = _juegoActual != null ? _juegoActual.cristales : null;
        if (cristales == null) return 0;
        int n = 0;
        foreach (var c in cristales) if (c != null && c.Activo) n++;
        return n;
    }

    private void RomperUnCristal()
    {
        var cristales = _juegoActual != null ? _juegoActual.cristales : null;
        if (cristales == null) return;
        foreach (var c in cristales)
        {
            if (c == null || !c.Activo) continue;
            c.Romper();
            return;
        }
    }

    // ── Fase 1 ────────────────────────────────────────────────────────────

    private IEnumerator Co_TurnoFase1()
    {
        if (_aturdir)
        {
            _aturdir = false;
            bool sinEspinas = _sinCristales;
            _sinCristales = false;
            yield return Co_Aturdido(sinEspinas ? aturdidoSinEspinas : aturdidoPorContraataque);
            if (sinEspinas) _reconjurarEn = Time.time + cadaEspinas;
            yield break;
        }
        if (_sinCristales)
        {
            _sinCristales = false;
            yield return Co_Aturdido(aturdidoSinEspinas);
            _reconjurarEn = Time.time + cadaEspinas;
            yield break;
        }
        if (_reconjurarEn > 0f && Time.time >= _reconjurarEn)
        {
            _reconjurarEn = -1f;
            yield return Co_Conjurar(escenario != null ? escenario.espinas : null);
        }

        float d = DistanciaAlJugador();
        if (_cercaDesde >= 0f && Time.time - _cercaDesde >= tiempoAcorralado)
        {
            yield return Co_Escapar();
            yield break;
        }
        if (d < radioNova && Time.time < _novaLista)
        {
            yield return Co_Retirarse();
            if (_aturdir || _sinCristales) yield break;
            d = DistanciaAlJugador();
        }
        if (d < radioNova + 1f && Time.time >= _novaLista)
            yield return Co_Nova();
        else if (d > radioNova + 2f && Time.time >= _pozoListo && UnityEngine.Random.value < 0.35f)
            yield return Co_Pozo();
        else if (Time.time >= _grietaLista && UnityEngine.Random.value < 0.4f)
            yield return Co_Grieta();
        else
            yield return Co_Salva(balasPorSalva + CristalesActivos());

        if (_aturdir || _sinCristales) yield break;
        yield return Co_Deslizar();
    }

    private IEnumerator Co_SalirFase1()
    {
        Deshacer(escenario != null ? escenario.espinas : null);
        _reconjurarEn = -1f;
        _aturdir = false;
        _sinCristales = false;
        _anim?.ReleasePose(true);
        yield return null;
    }

    /// Rayos seguidos al jugador tras un aviso (brillo en la mano). Se pueden devolver.
    private IEnumerator Co_Salva(int balas)
    {
        MirarAlJugador(1f);
        _anim?.PlaySocialGesture("MagicRight");
        if (vfxCarga && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxCarga, PuntoDeLanzamiento(), Quaternion.identity, avisoSalva + 0.2f, transform);
        yield return Esperar(avisoSalva, girarAlJugador: true);

        for (int i = 0; i < balas && !_aturdir && !_sinCristales; i++)
        {
            Disparar();
            yield return Esperar(intervaloSalva, girarAlJugador: true);
        }
        if (!_aturdir && !_sinCristales) yield return Esperar(pausaTrasAtaque);
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
            Zona(origen + dir * (2.5f + i * 2.4f), 1.8f, 0.8f + i * 0.12f, 0.4f, danoGrieta, 1f, 0f);
        yield return Esperar(1.2f);
    }

    private IEnumerator Co_Nova()
    {
        _novaLista = Time.time + 7f;
        _anim?.PlaySocialGesture("MagicSpecial");
        Zona(transform.position, radioNova, 1.1f, 0f, danoNova, 1f, 10f);
        yield return Esperar(1.3f);
        if (!_aturdir && !_sinCristales) yield return Co_Escapar();
    }

    /// Pozo: brazos arriba, nace delante de él el agujero negro y atrae al jugador mientras crece;
    /// al implosionar hace daño cerca. Quien corre en contra escapa.
    private IEnumerator Co_Pozo()
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
        if (!_aturdir) yield return Esperar(pausaTrasAtaque);
    }

    /// Aturdido: queda expuesto mientras dura.
    private IEnumerator Co_Aturdido(float segundos)
    {
        SoltarLevitacion(transicionLevitacion);
        _anim?.HoldPose("Dizzy_NoWeapon");
        _expuestoHasta = Time.time + segundos;
        FeedbackService.CameraShake(0.3f, 0.3f);
        yield return new WaitForSeconds(segundos);
        _anim?.ReleasePose(true);
        RecuperarLevitacion();
        if (_fase == 0) yield return Co_Retirarse();
    }

    // ── Fase 2 ────────────────────────────────────────────────────────────

    private JuegoDeConjuros JuegoFase2()
    {
        var juegos = escenario != null ? escenario.fase2 : null;
        if (juegos == null || juegos.Length == 0) return null;
        return juegos[_juegoFase2 % juegos.Length];
    }

    private IEnumerator Co_EntrarFase2()
    {
        _juegoFase2 = 0;
        yield return Co_Conjurar(JuegoFase2());
        RecuperarLevitacion();
        yield return Co_Mover(transform.position + Vector3.up * alturaVuelo, 1.2f);
        _siguienteCorrupcion = Time.time + 4f;
    }

    private IEnumerator Co_TurnoFase2()
    {
        if (_sinCristales) { _sinCristales = false; yield return Co_Derribado(); yield break; }
        if (_aturdir) { _aturdir = false; yield return Co_Aturdido(1.2f); }

        if (Time.time >= _siguienteCorrupcion)
        {
            Corromper();
            _siguienteCorrupcion = Time.time + cadaCorrupcion;
        }

        var juego = JuegoFase2();
        var puntos = juego != null ? juego.puntosDeVuelo : null;
        if (puntos != null && puntos.Length > 0)
        {
            _puntoVuelo = (_puntoVuelo + 1 + UnityEngine.Random.Range(0, 2)) % puntos.Length;
            if (puntos[_puntoVuelo] != null) yield return Co_Mover(puntos[_puntoVuelo].position, tiempoEntrePuntos);
        }
        if (_sinCristales) yield break;

        if (UnityEngine.Random.value < 0.55f) yield return Co_Salva(balasPorSalva);
        else yield return Co_Lluvia();

        if (_sinCristales) yield break;
        SostenerLevitacion();
        Vector3 centroVuelo = puntos != null && puntos.Length > 0 && puntos[_puntoVuelo] != null
            ? puntos[_puntoVuelo].position : transform.position;
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * radioDerivaVuelo;
            Vector3 destino = centroVuelo + new Vector3(offset.x, 0f, offset.y);
            if (!RecorridoLibre(transform.position, destino)) continue;
            yield return Co_Mover(destino, pausaFase2, reaccionar: true);
            yield break;
        }
        yield return Esperar(pausaFase2, girarAlJugador: true);
    }

    private void Corromper()
    {
        var cuadrantes = escenario != null ? escenario.cuadrantes : null;
        if (cuadrantes == null || cuadrantes.Length == 0 || Jugador() == null) return;
        // El trozo de suelo donde está el jugador: le obliga a moverse, subir o volar.
        Transform mejor = null;
        float mejorD = float.MaxValue;
        foreach (var c in cuadrantes)
        {
            if (c == null) continue;
            float d = (c.position - Jugador().position).sqrMagnitude;
            if (d < mejorD) { mejorD = d; mejor = c; }
        }
        if (mejor != null) Zona(mejor.position, radioCorrupcion, 2f, 5f, danoCorrupcion, 0.5f, 0f);
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

    /// Caen todas las anclas: se desploma, lo conjurado se deshace y queda expuesto en el suelo.
    /// Si sigue por encima del umbral, conjura el siguiente juego y vuelve a subir.
    private IEnumerator Co_Derribado()
    {
        SoltarLevitacion(transicionLevitacion);
        _anim?.HoldPose("fly_dive");
        Vector3 suelo = transform.position;
        suelo.y = SueloEn(suelo);
        yield return Co_Mover(suelo, 0.6f);
        FeedbackService.CameraShake(0.6f, 0.4f);
        Deshacer(JuegoFase2());
        _anim?.HoldPose("Dizzy_NoWeapon");
        _expuestoHasta = Time.time + derribado;
        yield return new WaitForSeconds(derribado);
        _anim?.ReleasePose(true);

        if (Vida <= umbralRegeneracion + 0.001f) yield break;
        _juegoFase2++;
        yield return Co_Conjurar(JuegoFase2());
        RecuperarLevitacion();
        yield return Co_Mover(transform.position + Vector3.up * alturaVuelo, 1f);
    }

    private IEnumerator Co_SalirFase2()
    {
        Deshacer(JuegoFase2());
        _sinCristales = false;
        _expuestoHasta = -1f;
        Vector3 suelo = transform.position;
        suelo.y = SueloEn(suelo);
        if (transform.position.y - suelo.y > 0.2f) yield return Co_Mover(suelo, 0.6f);
        _anim?.ReleasePose(true);
    }

    // ── Regeneración ──────────────────────────────────────────────────────

    /// Vuelve al altar y el altar le devuelve toda la vida a la vista. Desde aquí ningún golpe le
    /// hace nada. La grabación del tiempo empieza después, para que el rebobinado no la repita.
    private IEnumerator Co_Regeneracion()
    {
        _expuestoHasta = -1f;
        PonerCinematica();
        Transform altar = escenario != null ? escenario.puntoDelAltar : null;
        if (altar != null) yield return Co_Teletransporte(altar.position);
        MirarAlJugador(1f);
        _anim?.HoldPose("MagicAttackOmni01_Load");
        if (vfxRegeneracion && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRegeneracion, transform.position + Vector3.up, Quaternion.identity, duracionRegeneracion + 1f, transform);
        FeedbackService.CameraShake(0.25f, duracionRegeneracion);

        const int pasos = 10;
        float porPaso = (_vida.Max - _vida.Current) / pasos;
        for (int i = 0; i < pasos; i++)
        {
            _vida.Heal(porPaso);
            yield return new WaitForSeconds(duracionRegeneracion / pasos);
        }
        _vida.Heal(_vida.Max);
        _regenerado = true;

        yield return Co_Transicion(2);
        _anim?.ReleasePose(true);
        yield return new WaitForSeconds(Bocadillos.Decir(Jugador(), "FINAL_WILL_NADA", "Will", 2.6f, fijo: true));
        SoltarCinematica();
        if (escenario != null && escenario.registro != null) escenario.registro.EmpezarDeNuevo();
    }

    // ── La Marea de Sombra ────────────────────────────────────────────────

    private IEnumerator Co_Marea()
    {
        Transform altar = escenario != null ? escenario.puntoDelAltar : null;
        _anim?.HoldPose("MagicSpecial");
        yield return new WaitForSeconds(Bocadillos.Decir(transform, "FINAL_MAGO_MAREA", "Mago Oscuro"));
        Vector3 centro = transform.position;

        // Primera: no hay hueco. Justo antes de alcanzar a Will, el tiempo se para.
        var primera = MareaDeSombra.Crear(centro, ConfigMarea(null));
        while (primera != null && !primera.Terminada && primera.DistanciaAlJugador() - primera.Radio > 2.2f)
            yield return null;
        if (primera != null) primera.Pausada = true;

        yield return Co_HechizoDelTiempo();
        if (primera != null) Destroy(primera.gameObject);

        // Segunda: Will reúne al grupo y su escudo aguanta.
        if (altar != null) transform.position = altar.position;
        yield return Co_Conmigo();
        _anim?.HoldPose("MagicSpecial");
        yield return new WaitForSeconds(0.8f);

        bool aSalvo = false;
        var segunda = MareaDeSombra.Crear(centro, ConfigMarea(() => aSalvo));
        while (segunda != null && !segunda.Terminada && segunda.DistanciaAlJugador() - segunda.Radio > distanciaDelEscudo)
            yield return null;
        if (segunda != null && !segunda.Terminada) yield return Co_EscudoDelGrupo();
        aSalvo = true;
        while (segunda != null && !segunda.Terminada) yield return null;

        SoltarGrupo();
        _anim?.ReleasePose(true);
    }

    private MareaDeSombra.Config ConfigMarea(Func<bool> aSalvo) => new MareaDeSombra.Config
    {
        radioMax = (escenario != null ? escenario.radio : 20f) * 2.2f,
        velocidad = velocidadMarea,
        anguloHueco = 0f,
        anchoHueco = 0f,
        altura = 14f,
        dano = danoMarea,
        empuje = 12f,
        piezas = 40,
        vfxPieza = vfxMarea,
        escalaPieza = 1.4f,
        mostrarHueco = false,
        aSalvo = aSalvo,
    };

    /// El tiempo se para; Will rebobina (el jugador confirma). No se puede fallar: si no se
    /// confirma, sigue solo al rato.
    private IEnumerator Co_HechizoDelTiempo()
    {
        PonerCinematica();
        PedirTiempo(0.03f);
        FeedbackService.ScreenFlash(new Color(0.6f, 0.7f, 1f, 0.35f), 0.4f);

        Transform will = Jugador();
        yield return new WaitForSecondsRealtime(Bocadillos.Decir(will, "FINAL_TIEMPO_WILL", "Will", 3f, fijo: true));
        Bocadillos.Decir(will, "FINAL_TIEMPO_PULSA", null, 6f, fijo: true);

        float limite = Time.unscaledTime + 8f;
        while (Time.unscaledTime < limite && !GamepadInputReader.SubmitPressed) yield return null;

        FeedbackService.ScreenFlash(new Color(0.4f, 0.6f, 1f, 0.6f), 0.6f);
        if (vfxRebobinado && will != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRebobinado, will.position + Vector3.up, Quaternion.identity, duracionRebobinado + 1f, will);
        if (escenario != null && escenario.registro != null)
            yield return escenario.registro.Rebobinar(duracionRebobinado);

        SoltarTiempo();
        SoltarCinematica();
    }

    /// «¡Conmigo! ¡Ya!»: los compañeros corren a ponerse detrás de Will, de cara al Mago.
    private IEnumerator Co_Conmigo()
    {
        Transform will = Jugador();
        float espera = Bocadillos.Decir(will, "FINAL_WILL_CONMIGO", "Will", 1.6f, fijo: true);
        if (will != null && PlayerParty.HasInstance)
        {
            Vector3 atras = will.position - transform.position;
            atras.y = 0f;
            atras = atras.sqrMagnitude > 0.01f ? atras.normalized : -will.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, atras);
            int i = 0;
            foreach (var m in PlayerParty.Instance.Members)
            {
                if (m == null || !m.IsActiveInParty) continue;
                Vector3 sitio = will.position + atras * 1.4f + lado * (i % 2 == 0 ? -0.9f : 0.9f);
                m.MoveToDialoguePosition(sitio, 2.5f, transform);
                _reunidos.Add(m);
                i++;
            }
        }
        yield return new WaitForSeconds(Mathf.Max(espera, 1.8f));
    }

    /// El frente está a punto de llegar: el tiempo se frena hasta que el jugador mantiene la
    /// defensa (o pasa un rato) y el escudo de Will cubre a los tres.
    private IEnumerator Co_EscudoDelGrupo()
    {
        PedirTiempo(0.08f);
        Bocadillos.Decir(Jugador(), "FINAL_PULSA_ESCUDO", null, esperaDelEscudo + 0.5f, fijo: true);
        float limite = Time.unscaledTime + esperaDelEscudo;
        while (Time.unscaledTime < limite && !GamepadInputReader.AttackMagicRightHeld) yield return null;
        SoltarTiempo();
    }

    private void SoltarGrupo()
    {
        foreach (var m in _reunidos) if (m != null) m.ReleaseDialoguePosition();
        _reunidos.Clear();
    }

    // ── Para el final ─────────────────────────────────────────────────────

    /// Queda de rodillas, como vencido.
    public void Arrodillarse()
    {
        StopAllCoroutines();
        SoltarLevitacion(0f);
        RecogerProteccion();
        _expuestoHasta = -1f;
        _anim?.HoldPose("Beg01");
    }

    public void Gesto(string estado) => _anim?.PlaySocialGesture(estado);

    /// Se deshace (se encoge y se apaga) y el combate termina.
    public IEnumerator Deshacerse(GameObject vfx)
    {
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
        if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 60f, _mascaraSuelo, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return escenario != null && escenario.centro != null ? escenario.centro.position.y : p.y;
    }

    private void ActualizarProteccion()
    {
        if (CristalesActivos() == 0) { RecogerProteccion(); return; }
        if (_auraProtegido != null || vfxProtegido == null) return;
        _poolProtegido = VfxPoolService.Instance;
        if (_poolProtegido == null) return;
        _auraProtegido = _poolProtegido.Play(vfxProtegido, transform.position + Vector3.up * (1.2f + alturaLevitacion),
            Quaternion.identity, float.MaxValue, transform);
        _usoProtegido = _poolProtegido.ObtenerUso(_auraProtegido);
        if (_auraProtegido == null) return;
        // El tinte se aplica al uso del pool, sin cambiar el prefab del pack.
        foreach (var particulas in _auraProtegido.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particulas.main;
            main.startColor = new Color(0.35f, 0.08f, 0.65f, 0.7f);
        }
    }

    private void RecogerProteccion()
    {
        if (_poolProtegido != null && _auraProtegido != null && _usoProtegido != 0)
            _poolProtegido.Recoger(_auraProtegido, _usoProtegido);
        _auraProtegido = null;
        _poolProtegido = null;
        _usoProtegido = 0;
    }

    private void RecuperarLevitacion()
    {
        _elevacion?.Elevar(this, alturaLevitacion, transicionLevitacion);
        _levitando = true;
        SostenerLevitacion();
    }

    private void SoltarLevitacion(float segundos)
    {
        _levitando = false;
        _elevacion?.Soltar(this, segundos);
    }

    private void SostenerLevitacion()
    {
        if (_levitando) _anim?.HoldPose("MagicAttackOmni01_Load", congelarAlFinal: true);
    }

    private void CachearObstaculos(JuegoDeConjuros juego)
    {
        if (juego?.objetos == null) return;
        foreach (var objeto in juego.objetos)
            if (objeto != null) _obstaculos.AddRange(objeto.GetComponentsInChildren<Collider>(true));
    }

    private bool DentroDeArena(Vector3 punto)
    {
        Vector3 centro = escenario != null && escenario.centro != null ? escenario.centro.position : Vector3.zero;
        Vector3 d = punto - centro;
        d.y = 0f;
        float radio = Mathf.Max(1f, (escenario != null ? escenario.radio : 20f) - 2f);
        return d.sqrMagnitude <= radio * radio;
    }

    private bool RecorridoLibre(Vector3 desde, Vector3 hasta)
    {
        if (!DentroDeArena(hasta)) return false;
        // El volumen expandido reserva espacio para el cuerpo, también entre muestras del arco.
        Vector3 origen = desde + Vector3.up;
        Vector3 tramo = hasta - desde;
        float longitud = tramo.magnitude;
        foreach (var col in _obstaculos)
        {
            if (col == null || !col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy) continue;
            Bounds limites = col.bounds;
            limites.Expand(new Vector3(margenObstaculos * 2f, 2f, margenObstaculos * 2f));
            if (limites.Contains(origen) || limites.Contains(hasta + Vector3.up)) return false;
            if (longitud > 0.001f && limites.IntersectRay(new Ray(origen, tramo / longitud), out float distancia)
                && distancia <= longitud) return false;
        }
        return true;
    }

    private static Vector3 PosicionDeRecorrido(Vector3 desde, Vector3 hasta, float k, Vector3? centroArco, float angulo)
    {
        if (!centroArco.HasValue) return Vector3.Lerp(desde, hasta, k);
        Vector3 centro = centroArco.Value;
        Vector3 radial = desde - centro;
        radial.y = 0f;
        Vector3 final = hasta - centro;
        final.y = 0f;
        Vector3 punto = centro + Quaternion.AngleAxis(angulo * k, Vector3.up) * radial.normalized
            * Mathf.Lerp(radial.magnitude, final.magnitude, k);
        punto.y = Mathf.Lerp(desde.y, hasta.y, k);
        return punto;
    }

    private bool ArcoLibre(Vector3 destino, Vector3 centro, float angulo)
    {
        Vector3 anterior = transform.position;
        for (int i = 1; i <= 20; i++)
        {
            Vector3 siguiente = PosicionDeRecorrido(transform.position, destino, i / 20f, centro, angulo);
            siguiente.y = SueloEn(siguiente);
            if (!RecorridoLibre(anterior, siguiente)) return false;
            anterior = siguiente;
        }
        return true;
    }

    private IEnumerator Co_Deslizar()
    {
        var jugador = Jugador();
        if (jugador == null) yield break;
        SostenerLevitacion();
        Vector3 centro = jugador.position;
        Vector3 radial = transform.position - centro;
        radial.y = 0f;
        if (radial.sqrMagnitude < 0.01f) radial = -transform.forward;
        float distancia = UnityEngine.Random.Range(distanciaDeslizamiento.x, distanciaDeslizamiento.y);
        float sentido = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        for (int i = 0; i < 8; i++)
        {
            float angulo = arcoDeslizamiento * sentido * (1f + i / 2 * 0.3f);
            sentido = -sentido;
            Vector3 destino = centro + Quaternion.AngleAxis(angulo, Vector3.up) * radial.normalized * distancia;
            destino.y = SueloEn(destino);
            if (!ArcoLibre(destino, centro, angulo)) continue;
            yield return Co_Mover(destino, UnityEngine.Random.Range(duracionDeslizamiento.x, duracionDeslizamiento.y),
                centro, angulo, reaccionar: true);
            yield break;
        }
        yield return Co_Retirarse();
    }

    private IEnumerator Co_Retirarse()
    {
        var jugador = Jugador();
        if (jugador == null) yield break;
        SostenerLevitacion();
        Vector3 alejamiento = transform.position - jugador.position;
        alejamiento.y = 0f;
        if (alejamiento.sqrMagnitude < 0.01f) alejamiento = -transform.forward;
        for (int i = 0; i < 12; i++)
        {
            float angulo = i == 0 ? 0f : ((i + 1) / 2) * 30f * (i % 2 == 0 ? -1f : 1f);
            Vector3 destino = transform.position + Quaternion.AngleAxis(angulo, Vector3.up) * alejamiento.normalized * distanciaRetirada;
            destino.y = SueloEn(destino);
            if (!RecorridoLibre(transform.position, destino) || (destino - jugador.position).sqrMagnitude
                <= (transform.position - jugador.position).sqrMagnitude) continue;
            yield return Co_Mover(destino, duracionRetirada, reaccionar: true);
            yield break;
        }
        yield return Esperar(duracionRetirada, girarAlJugador: true);
    }

    private IEnumerator Co_Escapar()
    {
        var puntos = escenario != null ? escenario.puntosDeSalto : null;
        var jugador = Jugador();
        Vector3 destino = transform.position;
        float mejor = DistanciaAlJugador();
        if (puntos != null && jugador != null)
            foreach (var punto in puntos)
            {
                if (punto == null) continue;
                Vector3 candidato = punto.position;
                candidato.y = SueloEn(candidato);
                if (!RecorridoLibre(candidato, candidato)) continue;
                Vector3 d = candidato - jugador.position;
                d.y = 0f;
                if (d.magnitude <= mejor) continue;
                mejor = d.magnitude;
                destino = candidato;
            }
        _cercaDesde = -1f;
        if ((destino - transform.position).sqrMagnitude > 0.1f) yield return Co_Teletransporte(destino);
        else yield return Co_Retirarse();
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

    private IEnumerator Co_Mover(Vector3 destino, float segundos, Vector3? centroArco = null, float angulo = 0f, bool reaccionar = false)
    {
        Vector3 desde = transform.position;
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            if (reaccionar && ((_aturdir && _fase == 0) || _sinCristales)) yield break;
            if (reaccionar && centroArco.HasValue && DistanciaAlJugador() < radioNova
                && Time.time < _novaLista) yield break;
            float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, segundos));
            Vector3 siguiente = PosicionDeRecorrido(desde, destino, k, centroArco, angulo);
            if (centroArco.HasValue) siguiente.y = SueloEn(siguiente);
            if (reaccionar && !RecorridoLibre(transform.position, siguiente)) yield break;
            transform.position = siguiente;
            MirarAlJugador(Time.deltaTime * 6f);
            yield return null;
        }
        transform.position = destino;
    }

    /// Espera 'segundos' sin dejar de reaccionar: se corta si le devuelven un rayo en la fase 1 o
    /// si se queda sin cristales.
    private IEnumerator Esperar(float segundos, bool girarAlJugador = false)
    {
        float fin = Time.time + segundos;
        while (Time.time < fin)
        {
            if (_aturdir && _fase == 0) yield break;
            if (_sinCristales) yield break;
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

    private void PedirTiempo(float escala)
    {
        TimeScaleArbiterService.Request(this, escala);
        _tiempoPedido = true;
    }

    private void SoltarTiempo()
    {
        if (!_tiempoPedido) return;
        TimeScaleArbiterService.Release(this);
        _tiempoPedido = false;
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
