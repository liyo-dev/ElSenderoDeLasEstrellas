using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mímico: un cofre que muerde al abrirlo. Disfrazado se interactúa con él como con cualquier
/// cofre; al abrirlo despierta con un mordisco por sorpresa (anunciado, se para con la defensa)
/// y pelea cuerpo a cuerpo. Si se le golpea antes de abrirlo, despierta sin la sorpresa: la
/// respuesta es desconfiar del cofre que tiembla cuando te acercas. Con un <see cref="Tesoro"/>
/// en el mismo objeto se engancha a él (<see cref="IAntesDeAbrirTesoro"/>): el tesoro no se abre
/// hasta vencerlo y entonces entrega su recompensa y queda guardado. Sin tesoro, al morir activa
/// <see cref="recompensaAlMorir"/>. Ver INC-722.
/// </summary>
[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class Mimico : EnemigoDelMundo, IAntesDeAbrirTesoro
{
    [Header("Disfraz")]
    [Tooltip("Metros a los que se le escapa un temblor cuando hay alguien cerca.")]
    [SerializeField, Min(0f)] private float alcanceDelTemblor = 5f;
    [Tooltip("Segundos entre temblores (mínimo y máximo).")]
    [SerializeField] private Vector2 cadaCuantoTiembla = new Vector2(4f, 7f);
    [Tooltip("Trigger por el que se le detecta como cofre; se apaga al despertar.")]
    [SerializeField] private Collider zonaDeInteraccion;
    [SerializeField] private GameObject vfxDespertar;
    [SerializeField] private string sfxDespertar = "Chest";

    [Header("Mordisco por sorpresa")]
    [SerializeField, Min(0f)] private float danoSorpresa = 16f;
    [SerializeField, Min(0.5f)] private float alcanceSorpresa = 2.6f;
    [Tooltip("Segundos desde que se abre hasta que muerde.")]
    [SerializeField, Min(0.1f)] private float esperaSorpresa = 0.35f;

    [Header("Combate")]
    [SerializeField, Min(0.5f)] private float alcanceDeAtaque = 2f;
    [SerializeField, Min(0f)] private float dano = 12f;
    [Tooltip("Segundos entre el inicio del mordisco y el golpe.")]
    [SerializeField, Min(0.1f)] private float preparacion = 0.45f;
    [SerializeField, Min(0.1f)] private float enfriamiento = 1.4f;
    [SerializeField, Min(0.1f)] private float velocidad = 3.4f;

    [Header("Recompensa sin tesoro (opcional)")]
    [Tooltip("Objeto que se activa al morir si no hay un Tesoro en el mismo objeto.")]
    [SerializeField] private GameObject recompensaAlMorir;

    [Header("Animaciones (estados del Animator)")]
    [SerializeField] private string animDisfraz = "IdleChest";
    [SerializeField] private string animTemblor = "SenseSomethingST";
    [SerializeField] private string animCombate = "IdleBattle";
    [SerializeField] private string animCorrer = "Run";
    [SerializeField] private string animAtaque1 = "Attack01";
    [SerializeField] private string animAtaque2 = "Attack02";
    [SerializeField] private string animGolpe = "GetHit";

    private enum Estado { Disfrazado, Despertando, Persiguiendo, Atacando }

    private Estado _estado = Estado.Disfrazado;
    private NavMeshAgent _agente;
    private Interactable _interactable;
    private Tesoro _tesoro;
    private GameObject _quienAbrio;
    private float _temporizador, _proximoTemblor, _volverAlDisfraz, _proximoAtaque, _proximoDestino, _quietoHasta;
    private bool _mordiscoPendiente, _golpeado;

    public bool Disfrazado => _estado == Estado.Disfrazado && !Muerto;

    protected override void Awake()
    {
        base.Awake();
        _agente = GetComponent<NavMeshAgent>();
        _agente.enabled = false;
        _agente.speed = velocidad;
        _agente.stoppingDistance = alcanceDeAtaque * 0.8f;
        _interactable = GetComponent<Interactable>();
        _tesoro = GetComponent<Tesoro>();
        if (recompensaAlMorir != null) recompensaAlMorir.SetActive(false);
        Apuntable(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_tesoro != null) _tesoro.alRestaurar.AddListener(AlRestaurarTesoro);
        else _interactable.OnInteract.AddListener(AlAbrir);
    }

    protected override void OnDisable()
    {
        if (_tesoro != null) _tesoro.alRestaurar.RemoveListener(AlRestaurarTesoro);
        else _interactable.OnInteract.RemoveListener(AlAbrir);
        base.OnDisable();
    }

    private void Start()
    {
        if (_estado == Estado.Disfrazado) Animar(animDisfraz);
        _proximoTemblor = Time.time + Random.Range(cadaCuantoTiembla.x, cadaCuantoTiembla.y);
    }

    // ── Despertar ────────────────────────────────────────────────────────

    public bool Interceptar(Tesoro tesoro, GameObject quien)
    {
        if (!Disfrazado) return false;
        Despertar(quien, sorpresa: true);
        return true;
    }

    private void AlAbrir(GameObject quien)
    {
        if (Disfrazado) Despertar(quien, sorpresa: true);
    }

    /// <summary>El tesoro se carga ya abierto: este mímico ya fue vencido.</summary>
    private void AlRestaurarTesoro() => Destroy(gameObject);

    private void Despertar(GameObject quien, bool sorpresa)
    {
        _estado = Estado.Despertando;
        _quienAbrio = quien;
        _interactable.EnableInteraction(false);
        if (zonaDeInteraccion != null) zonaDeInteraccion.enabled = false;
        int capa = LayerMask.NameToLayer("Enemy");
        if (capa >= 0) gameObject.layer = capa;
        Apuntable(true);
        EntrarEnCombate();
        Efecto(vfxDespertar, transform.position + Vector3.up * 0.6f, 2f);
        Sonar(sfxDespertar);

        _mordiscoPendiente = sorpresa;
        if (sorpresa)
        {
            AnimarDeNuevo(animAtaque1);
            AmenazasAlJugador.Anunciar(transform, esperaSorpresa, TipoDeAmenaza.Devolvible);
            _temporizador = esperaSorpresa;
        }
        else
        {
            AnimarDeNuevo(animTemblor);
            _temporizador = 0.8f;
        }
    }

    // ── Comportamiento ───────────────────────────────────────────────────

    protected override void Pensar(float dt)
    {
        float distancia = DistanciaAlObjetivo();
        switch (_estado)
        {
            case Estado.Disfrazado:
                if (HayObjetivo && distancia <= alcanceDelTemblor && Time.time >= _proximoTemblor)
                {
                    AnimarDeNuevo(animTemblor);
                    _volverAlDisfraz = Time.time + 1.1f;
                    _proximoTemblor = Time.time + Random.Range(cadaCuantoTiembla.x, cadaCuantoTiembla.y);
                }
                else if (Time.time >= _volverAlDisfraz) Animar(animDisfraz);
                break;

            case Estado.Despertando:
                if (HayObjetivo) MirarHacia(HaciaElObjetivoPlano(), 540f);
                _temporizador -= dt;
                if (_temporizador > 0f) break;
                if (_mordiscoPendiente)
                {
                    _mordiscoPendiente = false;
                    if (HayObjetivo && distancia <= alcanceSorpresa) Danar(Objetivo, danoSorpresa);
                    _temporizador = 0.5f;
                    break;
                }
                ActivarAgente();
                _estado = Estado.Persiguiendo;
                break;

            case Estado.Persiguiendo:
                if (Time.time < _quietoHasta) break;
                if (!HayObjetivo || distancia > alcanceDePerdida)
                {
                    Parar();
                    Animar(animCombate);
                    break;
                }
                if (distancia <= alcanceDeAtaque && Time.time >= _proximoAtaque)
                {
                    Parar();
                    _estado = Estado.Atacando;
                    _temporizador = preparacion + 0.45f;
                    _golpeado = false;
                    AnimarDeNuevo(Random.value < 0.5f ? animAtaque1 : animAtaque2);
                    AmenazasAlJugador.Anunciar(transform, preparacion, TipoDeAmenaza.Devolvible);
                    break;
                }
                if (distancia <= alcanceDeAtaque)
                {
                    Parar();
                    MirarHacia(HaciaElObjetivoPlano(), 540f);
                    Animar(animCombate);
                    break;
                }
                if (_agente.isOnNavMesh)
                {
                    _agente.isStopped = false;
                    if (Time.time >= _proximoDestino)
                    {
                        _proximoDestino = Time.time + 0.2f;
                        _agente.SetDestination(Objetivo.position);
                    }
                }
                Animar(_agente.velocity.sqrMagnitude > 0.05f ? animCorrer : animCombate);
                break;

            case Estado.Atacando:
                if (HayObjetivo) MirarHacia(HaciaElObjetivoPlano(), 360f);
                _temporizador -= dt;
                if (!_golpeado && _temporizador <= 0.45f)
                {
                    _golpeado = true;
                    if (HayObjetivo && distancia <= alcanceDeAtaque * 1.25f) Danar(Objetivo, dano);
                }
                if (_temporizador <= 0f)
                {
                    _proximoAtaque = Time.time + enfriamiento;
                    _estado = Estado.Persiguiendo;
                }
                break;
        }
    }

    private void ActivarAgente()
    {
        _agente.enabled = true;
        if (_agente.isOnNavMesh) _agente.isStopped = false;
    }

    private void Parar()
    {
        if (_agente.enabled && _agente.isOnNavMesh)
        {
            _agente.isStopped = true;
            _agente.velocity = Vector3.zero;
        }
    }

    protected override void AlCambiarPausa(bool pausado)
    {
        if (_agente.enabled && _agente.isOnNavMesh) _agente.isStopped = pausado;
    }

    protected override void AlRecibirDano(float cantidad, GameObject quien)
    {
        if (_estado == Estado.Disfrazado)
        {
            Despertar(quien, sorpresa: false);
            return;
        }
        if (_estado != Estado.Persiguiendo) return;
        AnimarDeNuevo(animGolpe);
        Parar();
        _quietoHasta = Time.time + 0.3f;
    }

    protected override void AlMorir()
    {
        Parar();
        if (_agente.enabled) _agente.enabled = false;
        if (_tesoro != null) _tesoro.Abrir(_quienAbrio);
        else if (recompensaAlMorir != null) recompensaAlMorir.SetActive(true);
    }
}
