using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Game.NPC;
using Sendero.Core.Feedback;
using UnityEngine;
using UnityEngine.AI;

/// El Mago Oscuro en la batalla final (GDD § 19). Un duelo implacable estilo KH2 Sephiroth:
/// rápido, letal y directo, que pone a prueba todo lo aprendido por el jugador sin gimmicks ni
/// decoración que entorpezca la cámara o el combate.
///
///  1. Fase 1 (Tierra): salvas de rayos oscuros que se pueden desviar con escudo (B) para aturdirlo
///     y dejarlo expuesto a combos; bombas oscuras flotantes que explotan o se rompen con magia;
///     pozos gravitatorios mortales que matan del tirón si se pisa su centro; novás y grietas.
///  2. Fase 2 (El Mar del Abismo y Combate Aéreo): el suelo entero se vuelve impisable y mortal,
///     obligando a Will a volar; combate aéreo 3D mid-air entre proyectiles, bombas en el aire y
///     lluvia de meteoros abisales.
///  3. Fase 3 (Clímax): el Mago se regenera en el altar; desata la Marea de Sombra; levanta un escudo
///     impenetrable que solo el hechizo Amanecer puede romper, exponiéndolo al remate del grupo.
///
/// Ver INC-509, INC-663, INC-724, INC-761.
[RequireComponent(typeof(Damageable))]
public sealed class MagoOscuroBossAI : MonoBehaviour, IJefeConFases, IExpuestoAlDano, IInicioDeCombate, IFiltroDeDano
{
    [Header("Referencias")]
    [SerializeField] private EscenarioBatallaFinal escenario;
    [SerializeField] private FinalDelConducto final;
    [Tooltip("El rayo que lanza: prefab, velocidad y efectos.")]
    [SerializeField] private MagicSpellSO golpe;
    [Tooltip("Los comentarios de los compañeros durante el combate.")]
    [SerializeField] private GuionDeCombate guion;
    [SerializeField] private bool empezarSolo = false;
    [SerializeField] private float esperaInicial = 1.5f;

    [Header("Efectos")]
    [Tooltip("Sombra que avisa en el suelo antes de un golpe.")]
    [SerializeField] private GameObject vfxAviso;
    [Tooltip("Efecto de una grieta o zona de sombra activa.")]
    [SerializeField] private GameObject vfxZona;
    [SerializeField] private GameObject vfxTeletransporte;
    [Tooltip("Brillo en la mano mientras carga una salva.")]
    [SerializeField] private GameObject vfxCarga;
    [Tooltip("Efecto sobre el Mago mientras el altar le devuelve la vida.")]
    [SerializeField] private GameObject vfxRegeneracion;
    [Tooltip("Aura protectora impenetrable en fase final.")]
    [SerializeField] private GameObject vfxProtegido;

    [Header("Bombas y Abismo")]
    [Tooltip("Visual de la esfera de sombra para las bombas oscuras.")]
    [SerializeField] private GameObject vfxBomba;
    [Tooltip("Efecto de explosión de las bombas oscuras.")]
    [SerializeField] private GameObject vfxExplosionBomba;
    [Tooltip("Efecto del mar de sombras que cubre el suelo en fase 2.")]
    [SerializeField] private GameObject vfxMarAbismo;

    [Header("Levitación y desplazamiento")]
    [Tooltip("Altura del modelo sobre su cápsula durante el combate.")]
    [SerializeField, Min(0f)] private float alturaLevitacion = 0.5f;
    [Tooltip("Segundos para recuperar la levitación al dejar de estar expuesto.")]
    [SerializeField, Min(0.05f)] private float transicionLevitacion = 0.35f;
    [Tooltip("Distancias mínima y máxima al jugador durante el deslizamiento en arco.")]
    [SerializeField] private Vector2 distanciaDeslizamiento = new(8f, 12f);
    [Tooltip("Duraciones mínima y máxima del deslizamiento entre ataques.")]
    [SerializeField] private Vector2 duracionDeslizamiento = new(0.9f, 1.5f);
    [Tooltip("Grados que recorre alrededor del jugador entre ataques.")]
    [SerializeField, Range(10f, 90f)] private float arcoDeslizamiento = 45f;
    [Tooltip("Metros que intenta separarse del jugador en una retirada rápida.")]
    [SerializeField, Min(1f)] private float distanciaRetirada = 6f;
    [Tooltip("Segundos que dura la retirada rápida.")]
    [SerializeField, Min(0.1f)] private float duracionRetirada = 0.45f;
    [Tooltip("Segundos de cercanía continua que permiten escapar por teletransporte.")]
    [SerializeField, Min(0.1f)] private float tiempoAcorralado = 1.2f;
    [Tooltip("Separación del cuerpo respecto a los bordes y altar.")]
    [SerializeField, Min(0.1f)] private float margenObstaculos = 0.8f;

    [Header("Umbrales (vida 0..1)")]
    [SerializeField] private float umbralFase2 = 0.7f;
    [Tooltip("Al llegar aquí, el altar le devuelve toda la vida y empieza el clímax.")]
    [SerializeField] private float umbralRegeneracion = 0.35f;

    [Header("Daño que recibe")]
    [Tooltip("Multiplicador del daño mientras está expuesto (aturdido o protección abierta).")]
    [SerializeField, Min(1f)] private float danoExpuesto = 1.5f;

    [Header("Fase 1: Duelo en tierra")]
    [SerializeField] private int balasPorSalva = 4;
    [SerializeField] private float intervaloSalva = 0.25f;
    [SerializeField] private float avisoSalva = 0.5f;
    [SerializeField] private float danoBala = 16f;
    [SerializeField] private float pausaTrasAtaque = 0.6f;
    [SerializeField] private int tramosGrieta = 6;
    [SerializeField] private float danoGrieta = 18f;
    [SerializeField] private float radioNova = 5.5f;
    [SerializeField] private float danoNova = 25f;
    [Tooltip("Segundos aturdido y expuesto cuando le devuelven un rayo con defensa perfecta.")]
    [SerializeField] private float aturdidoPorContraataque = 4.0f;
    [SerializeField] private int bombasPorConjuro = 3;
    [SerializeField] private float cadaBombas = 9f;

    [Header("Pozo Gravitatorio Mortal")]
    [SerializeField] private GameObject vfxPozo;
    [SerializeField] private GameObject vfxImplosionPozo;
    [SerializeField] private float cargaPozo = 3.2f;
    [SerializeField] private float radioAtraccionPozo = 14f;
    [SerializeField] private float velocidadAtraccionPozo = 3.8f;
    [SerializeField] private float radioImplosionPozo = 3.5f;
    [SerializeField] private float danoPozo = 35f;
    [SerializeField] private float empujePozo = 12f;
    [SerializeField] private float cadaPozo = 15f;

    [Header("Fase 2: El Mar del Abismo y Combate Aéreo")]
    [Tooltip("Altura de vuelo en fase 2 (combate aéreo obligatorio).")]
    [SerializeField] private float alturaVuelo = 6.5f;
    [SerializeField] private float tiempoEntrePuntosVuelo = 2.0f;
    [SerializeField] private int gotasLluvia = 7;
    [SerializeField] private float danoLluvia = 18f;
    [SerializeField] private float cadaLluvia = 7f;
    [SerializeField] private float cadaBombasAereas = 8f;
    [SerializeField] private float pausaFase2 = 0.7f;
    [SerializeField] private float aturdidoAereo = 3.5f;

    [Header("Regeneración de Fase 3")]
    [SerializeField] private float duracionRegeneracion = 2.5f;

    // ── Estado ────────────────────────────────────────────────────────────
    private Damageable _vida;
    private Targetable _apuntable;
    private NPCSimpleAnimator _anim;
    private TransicionDeFaseDeJefe _transicion;
    private ProteccionQueSeAbre _proteccion;
    private Renderer[] _renderers;
    private float[] _umbrales;
    private int _fase;
    private bool _empezado;
    private bool _regenerado;
    private float _expuestoHasta = -1f;
    private bool _aturdir;
    private bool _aturdidoPorAmanecer;
    private ElevacionVisual _elevacion;
    private Transform _auraProtegido;
    private VfxPoolService _poolProtegido;
    private ulong _usoProtegido;
    private float _cercaDesde = -1f;
    private bool _levitando;
    private int _mascaraSuelo;
    private readonly List<Collider> _obstaculos = new();
    private int _puntoVuelo;
    private float _novaLista, _grietaLista, _pozoListo, _bombasLista, _bombasAereasLista, _siguienteLluvia;
    private SueloImpisable _sueloImpisableActivo;
    private GuiaDeCombate _guia;

    // ── Interfaces ────────────────────────────────────────────────────────
    public int Fase => _fase;
    public IReadOnlyList<float> UmbralesDeFase => _umbrales;
    public event Action<int> AlCambiarDeFase;
    public bool Expuesto => (Time.time < _expuestoHasta || ProteccionAbierta) && _vida != null && _vida.IsAlive;
    public float Vida => _vida != null && _vida.Max > 0f ? _vida.Current / _vida.Max : 0f;
    public EscenarioBatallaFinal Escenario => escenario;
    public NPCSimpleAnimator Animador => _anim;
    public bool ProteccionAbierta => _proteccion != null && _proteccion.Abierta;

    /// Regenerado, ningún golpe le hace daño a menos que Amanecer haya abierto su protección.
    /// Expuesto recibe 1.5x daño. Respeta el suelo de fase: no se pasa de fase por puro golpe.
    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (_vida == null) return cantidad;
        bool abierta = ProteccionAbierta;
        if (_regenerado && !abierta)
        {
            AvisosDeCombate.GolpeMalDado(gameObject);
            return 0f;
        }

        if (Expuesto) cantidad *= danoExpuesto;

        float suelo = _regenerado ? 1f : _vida.Max * (_fase == 0 ? umbralFase2 : umbralRegeneracion);
        return Mathf.Min(cantidad, Mathf.Max(0f, _vida.Current - suelo));
    }

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _vida.SetDestroyOnDeath(false);
        _apuntable = GetComponent<Targetable>();
        if (_apuntable != null) _apuntable.isInActiveCombat = false;
        _anim = GetComponentInChildren<NPCSimpleAnimator>();
        _elevacion = GetComponentInChildren<ElevacionVisual>();
        _mascaraSuelo = LayerMask.GetMask("Default", "Floor");

        if (escenario != null && escenario.altar != null)
            _obstaculos.AddRange(escenario.altar.GetComponentsInChildren<Collider>(true));

        _transicion = GetComponent<TransicionDeFaseDeJefe>();
        if (_transicion == null) _transicion = gameObject.AddComponent<TransicionDeFaseDeJefe>();

        _proteccion = GetComponent<ProteccionQueSeAbre>();
        if (_proteccion == null) _proteccion = gameObject.AddComponent<ProteccionQueSeAbre>();

        _renderers = GetComponentsInChildren<Renderer>(true);
        _umbrales = new[] { umbralFase2, umbralRegeneracion };

        var agente = GetComponent<NavMeshAgent>();
        if (agente) agente.enabled = false;
    }

    void OnEnable()
    {
        PlayerShieldController.AlDevolverAtaque += AlDevolverAtaque;
        if (_proteccion != null)
        {
            _proteccion.AlAbrirse += AlAbrirseLaProteccion;
            _proteccion.AlCerrarse += AlCerrarseLaProteccion;
        }
    }

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
        if (_proteccion != null)
        {
            _proteccion.AlAbrirse -= AlAbrirseLaProteccion;
            _proteccion.AlCerrarse -= AlCerrarseLaProteccion;
        }
        if (_sueloImpisableActivo != null)
        {
            _sueloImpisableActivo.Desactivar();
            _sueloImpisableActivo = null;
        }
        RecogerProteccion();
        SoltarLevitacion(0f);
        _anim?.ReleasePose(true);
        if (_apuntable != null) _apuntable.isInActiveCombat = false;
        ActiveCombatRegistry.UnregisterNPC(gameObject);
    }

    IEnumerator Start()
    {
        if (!empezarSolo || CinematicSequencerBase.AnySequenceActive) yield break;
        yield return new WaitForSeconds(esperaInicial);
        if (!_empezado && !CinematicSequencerBase.AnySequenceActive) EmpezarCombate();
    }

    public void EmpezarCombate()
    {
        if (_empezado) return;
        _empezado = true;
        if (_apuntable != null) _apuntable.isInActiveCombat = true;
        RecuperarLevitacion();
        _pozoListo = Time.time + cadaPozo * 0.4f;
        _bombasLista = Time.time + 3f;
        ActiveCombatRegistry.RegisterNPC(gameObject);
        var bossBar = GetComponent<BossHealthBar>();
        if (bossBar) bossBar.Show();
        StartCoroutine(Co_Combate());
    }

    /// Devolverle su propio rayo con el escudo de Will lo aturde y expone.
    private void AlDevolverAtaque(GameObject origen)
    {
        if (origen != gameObject || _regenerado) return;
        _aturdir = true;
    }

    // ── El combate ────────────────────────────────────────────────────────

    private IEnumerator Co_Combate()
    {
        SeguirEnElRegistro();
        yield return Co_Presentacion();
        SostenerLevitacion();

        _guia = GuiaDeCombate.Empezar(gameObject, guion, gameObject);
        if (_guia != null) _guia.LanzarIntervencion();

        // ── FASE 1: Duelo en tierra ───────────────────────────────────────
        _fase = 0;
        while (Vida > umbralFase2 + 0.001f) yield return Co_TurnoFase1();
        yield return Co_SalirFase1();

        // ── FASE 2: El Mar del Abismo (Vuelo Obligatorio) ──────────────────
        yield return Co_Transicion(1);
        yield return Co_EntrarFase2();
        while (Vida > umbralRegeneracion + 0.001f) yield return Co_TurnoFase2();
        yield return Co_SalirFase2();

        // ── FASE 3: Clímax, Regeneración y Ruptura de Barrera con Amanecer ─
        if (_guia != null) _guia.Parar();
        yield return Co_Regeneracion();
        yield return Co_TurnosFase3();

        if (final != null) yield return final.Ejecutar(this);
    }

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

    // ── Fase 1 ────────────────────────────────────────────────────────────

    private IEnumerator Co_TurnoFase1()
    {
        if (_aturdir)
        {
            _aturdir = false;
            yield return Co_Aturdido(aturdidoPorContraataque);
            yield break;
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
            if (_aturdir) yield break;
            d = DistanciaAlJugador();
        }

        if (d < radioNova + 0.5f && Time.time >= _novaLista)
            yield return Co_Nova();
        else if (Time.time >= _bombasLista)
            yield return Co_ConjurarBombas();
        else if (d > radioNova + 2f && Time.time >= _pozoListo && UnityEngine.Random.value < 0.45f)
            yield return Co_Pozo();
        else if (Time.time >= _grietaLista && UnityEngine.Random.value < 0.4f)
            yield return Co_Grieta();
        else
            yield return Co_Salva(balasPorSalva);

        if (_aturdir) yield break;
        yield return Co_Deslizar();
    }

    private IEnumerator Co_SalirFase1()
    {
        _aturdir = false;
        _anim?.ReleasePose(true);
        yield return null;
    }

    /// Salva de rayos seguidos. Se pueden desviar o devolver con contraataque.
    private IEnumerator Co_Salva(int balas)
    {
        MirarAlJugador(1f);
        _anim?.PlaySocialGesture("MagicRight");
        if (vfxCarga && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxCarga, PuntoDeLanzamiento(), Quaternion.identity, avisoSalva + 0.2f, transform);
        yield return Esperar(avisoSalva, girarAlJugador: true);

        for (int i = 0; i < balas && !_aturdir; i++)
        {
            Disparar();
            yield return Esperar(intervaloSalva, girarAlJugador: true);
        }
        if (!_aturdir) yield return Esperar(pausaTrasAtaque);
    }

    /// Conjura 3 bombas oscuras flotantes que zumban y explotan o se rompen con magia.
    private IEnumerator Co_ConjurarBombas()
    {
        _bombasLista = Time.time + cadaBombas;
        MirarAlJugador(1f);
        _anim?.PlaySocialGesture("MagicSpecial");
        if (vfxCarga && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxCarga, PuntoDeLanzamiento(), Quaternion.identity, 0.5f, transform);
        yield return Esperar(0.45f, girarAlJugador: true);

        var j = Jugador();
        Vector3 centro = j != null ? j.position : transform.position + transform.forward * 4f;
        for (int i = 0; i < bombasPorConjuro; i++)
        {
            float angulo = i * (360f / bombasPorConjuro) + UnityEngine.Random.Range(-25f, 25f);
            float dist = UnityEngine.Random.Range(3.5f, 6.5f);
            Vector3 pos = centro + Quaternion.Euler(0f, angulo, 0f) * Vector3.forward * dist;
            pos.y = SueloEn(pos) + 1.2f;
            BombaOscura.Crear(pos, vfxBomba, vfxExplosionBomba);
        }
        yield return Esperar(pausaTrasAtaque);
    }

    /// Grieta en línea recta de explosiones oscuras.
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
            Zona(origen + dir * (2.5f + i * 2.4f), 1.8f, 0.7f + i * 0.1f, 0.4f, danoGrieta, 1f, 0f);
        yield return Esperar(1.0f);
    }

    /// Nova de repulsión oscura si el jugador está a corta distancia.
    private IEnumerator Co_Nova()
    {
        _novaLista = Time.time + 6f;
        _anim?.PlaySocialGesture("MagicSpecial");
        Zona(transform.position, radioNova, 0.9f, 0f, danoNova, 1f, 12f);
        yield return Esperar(1.1f);
        if (!_aturdir) yield return Co_Escapar();
    }

    /// Pozo gravitatorio: atrae al grupo con fuerza creciente y quema a quien pisa el núcleo.
    private IEnumerator Co_Pozo()
    {
        _pozoListo = Time.time + cadaPozo;
        MirarAlJugador(1f);
        _anim?.HoldPose("MagicAttackOmni01_Load");

        Vector3 centro = transform.position + transform.forward * 3.5f;
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
            escalaInicial = 0.25f,
            escalaFinal = 1.1f,
            alturaVfx = 0.04f,
            vfxImplosion = vfxImplosionPozo,
            sfxCarga = "SFX_Prologo_AgujeroNegro_Carga",
            sfxImplosion = "SFX_Prologo_Explosion",
            danoPorSegundoEnElNucleo = PozoGravitatorio.DanoDelNucleoPorDefecto,
            radioDelNucleo = 1.8f
        });

        yield return Esperar(cargaPozo + 0.3f);
        _anim?.ReleasePose(true);
        if (!_aturdir) yield return Esperar(pausaTrasAtaque);
    }

    /// Aturdido en tierra: queda expuesto y vulnerable a daño aumentado.
    private IEnumerator Co_Aturdido(float segundos)
    {
        SoltarLevitacion(transicionLevitacion);
        _anim?.HoldPose("Dizzy_NoWeapon");
        _expuestoHasta = Time.time + segundos;
        FeedbackService.CameraShake(0.35f, 0.3f);
        FeedbackService.HitStop(0.08f, 0.4f);
        yield return new WaitForSeconds(segundos);
        _anim?.ReleasePose(true);
        RecuperarLevitacion();
        if (_fase == 0) yield return Co_Retirarse();
    }

    // ── Fase 2: El Mar del Abismo y Combate Aéreo ──────────────────────────

    private IEnumerator Co_EntrarFase2()
    {
        RecuperarLevitacion();
        yield return Co_Mover(transform.position + Vector3.up * alturaVuelo, 1.2f);

        if (escenario != null && escenario.centro != null)
            _sueloImpisableActivo = SueloImpisable.Activar(escenario.centro.position, escenario.radio, vfxMarAbismo);

        _siguienteLluvia = Time.time + 3.5f;
        _bombasAereasLista = Time.time + 1.5f;
    }

    private IEnumerator Co_TurnoFase2()
    {
        if (_aturdir)
        {
            _aturdir = false;
            yield return Co_AturdidoEnAire(aturdidoAereo);
        }

        var puntos = escenario != null ? escenario.puntosDeVuelo : null;
        if (puntos != null && puntos.Length > 0)
        {
            _puntoVuelo = (_puntoVuelo + 1 + UnityEngine.Random.Range(0, 2)) % puntos.Length;
            if (puntos[_puntoVuelo] != null) yield return Co_Mover(puntos[_puntoVuelo].position, tiempoEntrePuntosVuelo);
        }

        if (_aturdir) yield break;

        float r = UnityEngine.Random.value;
        if (r < 0.35f && Time.time >= _bombasAereasLista)
            yield return Co_ConjurarBombasAereas();
        else if (r < 0.7f && Time.time >= _siguienteLluvia)
            yield return Co_Lluvia();
        else
            yield return Co_Salva(balasPorSalva + 1);

        if (_aturdir) yield break;
        yield return Esperar(pausaFase2, girarAlJugador: true);
    }

    /// Conjura 3 bombas flotantes a la altura del vuelo.
    private IEnumerator Co_ConjurarBombasAereas()
    {
        _bombasAereasLista = Time.time + cadaBombasAereas;
        MirarAlJugador(1f);
        _anim?.PlaySocialGesture("MagicSpecial");
        yield return Esperar(0.4f, girarAlJugador: true);

        var j = Jugador();
        Vector3 centro = j != null ? j.position : transform.position;
        for (int i = 0; i < 3; i++)
        {
            Vector2 circ = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(4f, 8f);
            Vector3 pos = new Vector3(centro.x + circ.x, alturaVuelo + 0.5f, centro.z + circ.y);
            BombaOscura.Crear(pos, vfxBomba, vfxExplosionBomba);
        }
        yield return Esperar(pausaFase2);
    }

    /// Lluvia de meteoros de sombra que caen sobre la posición del jugador.
    private IEnumerator Co_Lluvia()
    {
        _siguienteLluvia = Time.time + cadaLluvia;
        _anim?.PlaySocialGesture("MagicSpecial");
        Transform j = Jugador();
        if (j == null) yield break;
        for (int i = 0; i < gotasLluvia; i++)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * 7f;
            Vector3 p = j.position + new Vector3(r.x, 0f, r.y);
            if (i == 0) p = j.position;
            p.y = SueloEn(p);
            Zona(p, 2.2f, 1.1f + i * 0.1f, 0f, danoLluvia, 1f, 0f);
        }
        yield return Esperar(1.4f);
    }

    /// Aturdido en el aire: permanece suspendido y expuesto a combos aéreos.
    private IEnumerator Co_AturdidoEnAire(float segundos)
    {
        _anim?.HoldPose("Dizzy_NoWeapon");
        _expuestoHasta = Time.time + segundos;
        FeedbackService.CameraShake(0.35f, 0.3f);
        FeedbackService.HitStop(0.08f, 0.4f);
        yield return new WaitForSeconds(segundos);
        _anim?.ReleasePose(true);
        SostenerLevitacion();
    }

    private IEnumerator Co_SalirFase2()
    {
        if (_sueloImpisableActivo != null)
        {
            _sueloImpisableActivo.Desactivar();
            _sueloImpisableActivo = null;
        }
        _expuestoHasta = -1f;
        _aturdir = false;
        Vector3 suelo = transform.position;
        suelo.y = SueloEn(suelo);
        yield return Co_Mover(suelo, 0.8f);
        _anim?.ReleasePose(true);
    }

    // ── Fase 3: Clímax, Regeneración y Ruptura de Barrera con Amanecer ────────

    private IEnumerator Co_Regeneracion()
    {
        _expuestoHasta = -1f;
        Transform altar = escenario != null ? escenario.puntoDelAltar : null;
        if (altar != null) yield return Co_Teletransporte(altar.position);
        MirarAlJugador(1f);
        _anim?.HoldPose("MagicAttackOmni01_Load");
        if (vfxRegeneracion && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRegeneracion, transform.position + Vector3.up, Quaternion.identity, duracionRegeneracion + 1f, transform);
        FeedbackService.CameraShake(0.35f, duracionRegeneracion);

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
        if (escenario != null && escenario.registro != null) escenario.registro.EmpezarDeNuevo();
    }

    /// Combate final: invulnerable tras su barrera de sombra hasta que Amanecer abra su protección.
    private IEnumerator Co_TurnosFase3()
    {
        ActualizarProteccion();
        _bombasLista = Time.time + 2f;
        _pozoListo = Time.time + 4f;

        while (Vida > 0.005f)
        {
            if (ProteccionAbierta)
            {
                if (!_aturdidoPorAmanecer)
                {
                    _aturdidoPorAmanecer = true;
                    FeedbackService.ScreenFlash(new Color(1f, 0.95f, 0.7f, 0.8f), 0.5f);
                    FeedbackService.CameraShake(0.6f, 0.5f);
                    yield return Co_Aturdido(5f);
                }
            }
            else
            {
                _aturdidoPorAmanecer = false;
            }

            if (_aturdir)
            {
                _aturdir = false;
                yield return Co_Aturdido(aturdidoPorContraataque);
            }

            float d = DistanciaAlJugador();
            if (d < radioNova && Time.time >= _novaLista)
                yield return Co_Nova();
            else if (Time.time >= _bombasLista)
                yield return Co_ConjurarBombas();
            else if (Time.time >= _pozoListo)
                yield return Co_Pozo();
            else
                yield return Co_Salva(balasPorSalva + 2);

            yield return Co_Deslizar();
        }
    }

    // ── Para el final ─────────────────────────────────────────────────────

    public void Arrodillarse()
    {
        StopAllCoroutines();
        SoltarLevitacion(0f);
        RecogerProteccion();
        _expuestoHasta = -1f;
        if (_apuntable != null) _apuntable.isInActiveCombat = false;
        ActiveCombatRegistry.UnregisterNPC(gameObject);
        _anim?.HoldPose("Beg01");
    }

    public void Gesto(string estado) => _anim?.PlaySocialGesture(estado);

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

    private void AlAbrirseLaProteccion(ProteccionQueSeAbre _, GameObject __) => ActualizarProteccion();
    private void AlCerrarseLaProteccion(ProteccionQueSeAbre _) => ActualizarProteccion();

    private void ActualizarProteccion()
    {
        if (ProteccionAbierta || !_regenerado) { RecogerProteccion(); return; }
        if (_auraProtegido != null || vfxProtegido == null) return;
        _poolProtegido = VfxPoolService.Instance;
        if (_poolProtegido == null) return;
        _auraProtegido = _poolProtegido.Play(vfxProtegido, transform.position + Vector3.up * (1.2f + alturaLevitacion),
            Quaternion.identity, float.MaxValue, transform);
        _usoProtegido = _poolProtegido.ObtenerUso(_auraProtegido);
        if (_auraProtegido == null) return;
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
            if (reaccionar && _aturdir && _fase == 0) yield break;
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

    private IEnumerator Esperar(float segundos, bool girarAlJugador = false)
    {
        float fin = Time.time + segundos;
        while (Time.time < fin)
        {
            if (_aturdir && _fase == 0) yield break;
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
}
