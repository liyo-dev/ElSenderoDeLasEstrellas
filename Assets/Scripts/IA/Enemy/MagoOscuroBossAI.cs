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
/// a golpes. Ver INC-509, INC-661.
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
    [SerializeField] private float pausaFase1 = 1.1f;

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
    private int _ataquesDesdeSalto;
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
        _transicion = GetComponent<TransicionDeFaseDeJefe>();
        if (_transicion == null) _transicion = gameObject.AddComponent<TransicionDeFaseDeJefe>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _umbrales = new[] { umbralFase2, umbralRegeneracion };
        var agente = GetComponent<NavMeshAgent>();
        if (agente) agente.enabled = false;   // se mueve a saltos y volando, no andando
    }

    void OnEnable() => PlayerShieldController.AlDevolverAtaque += AlDevolverAtaque;

    void OnDisable()
    {
        PlayerShieldController.AlDevolverAtaque -= AlDevolverAtaque;
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
            c.Activar(transform);
        }
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
    }

    private void AlRomperseCristal(CristalProtector _)
    {
        if (CristalesActivos() == 0) _sinCristales = true;
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
        if (_aturdir) { _aturdir = false; yield return Co_Aturdido(aturdidoPorContraataque); yield break; }
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
        if (d < radioNova + 1f && Time.time >= _novaLista)
            yield return Co_Nova();
        else if (d > radioNova + 2f && Time.time >= _pozoListo && UnityEngine.Random.value < 0.35f)
            yield return Co_Pozo();
        else if (Time.time >= _grietaLista && UnityEngine.Random.value < 0.4f)
            yield return Co_Grieta();
        else
            yield return Co_Salva(balasPorSalva + CristalesActivos());

        if (_aturdir || _sinCristales) yield break;
        if (++_ataquesDesdeSalto >= 2)
        {
            _ataquesDesdeSalto = 0;
            yield return Co_Teletransporte(PuntoLejosDelJugador(escenario != null ? escenario.puntosDeSalto : null));
        }
        yield return Esperar(pausaFase1);
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

        for (int i = 0; i < balas && !_aturdir; i++)
        {
            Disparar();
            yield return Esperar(intervaloSalva, girarAlJugador: true);
        }
        if (!_aturdir) yield return Esperar(pausaTrasAtaque);
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
        yield return Co_Teletransporte(PuntoLejosDelJugador(escenario != null ? escenario.puntosDeSalto : null));
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
        _anim?.HoldPose("Dizzy_NoWeapon");
        _expuestoHasta = Time.time + segundos;
        FeedbackService.CameraShake(0.3f, 0.3f);
        yield return new WaitForSeconds(segundos);
        _anim?.ReleasePose(true);
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
        _anim?.HoldPose("fly_idle");
        yield return Co_Mover(transform.position + Vector3.up * alturaVuelo, 1.2f);
        _siguienteCorrupcion = Time.time + 4f;
    }

    private IEnumerator Co_TurnoFase2()
    {
        if (_sinCristales) { _sinCristales = false; yield return Co_Derribado(); yield break; }
        if (_aturdir) { _aturdir = false; yield return Esperar(1.2f); }

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

        yield return Esperar(pausaFase2);
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
        _anim?.HoldPose("fly_idle");
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
        if (orden.Count == 0) return transform.position;
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
