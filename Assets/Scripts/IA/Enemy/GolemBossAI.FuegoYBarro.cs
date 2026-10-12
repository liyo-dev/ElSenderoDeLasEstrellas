using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// Gólem, reglas de la GDD v3.1 (INC-723):
///  - El fuego de frente le da fuerza (FuegoQueAlimenta): sus golpes llegan más lejos y pegan más.
///  - El barro del arroyo (ZonaDeBarro) lo frena y, al pisarlo, lo hunde unos segundos: quieto,
///    sin coraza (ArmaduraFrontal apagada) y sin daño por contacto. Es la ventana para atacarle
///    por cualquier lado. Tras salir, durante un rato el barro solo lo frena.
public partial class GolemBossAI
{
    [Header("Barro del arroyo (INC-723)")]
    [Tooltip("Segundos que se queda hundido al pisar el barro.")]
    [SerializeField, Min(0.5f)] private float segundosHundido = 4f;
    [Tooltip("Metros que se hunde.")]
    [SerializeField, Min(0f)] private float profundidadHundido = 0.9f;
    [Tooltip("Velocidad al andar por el barro sin hundirse (fracción de la normal).")]
    [SerializeField, Range(0.1f, 1f)] private float frenoEnBarro = 0.5f;
    [Tooltip("Segundos tras salir del barro en los que no vuelve a hundirse (solo le frena).")]
    [SerializeField, Min(0f)] private float respiroTrasHundirse = 8f;
    [Tooltip("Color del cuerpo mientras está embarrado.")]
    [SerializeField] private Color tinteDeBarro = new Color(0.62f, 0.5f, 0.38f);

    private FuegoQueAlimenta _fuego;
    private ArmaduraFrontal _armadura;
    private Rigidbody _rb;
    private bool _hundirPendiente;
    private bool _hundido;
    private bool _rbEraCinematico;
    private float _sinHundirseHasta;
    private ZonaDeBarro _barro;

    /// Está hundido en el barro (ventana de daño).
    public bool Hundido => _hundido;

    private float MultAlcance => _fuego != null ? _fuego.MultAlcance : 1f;
    private float MultDano => _fuego != null ? _fuego.MultDano : 1f;

    private void PrepararFuegoYBarro()
    {
        _fuego = GetComponent<FuegoQueAlimenta>();
        _armadura = GetComponent<ArmaduraFrontal>();
        _rb = GetComponent<Rigidbody>();
    }

    /// Daño de una roca lanzada: el del prefab, multiplicado por la fuerza que le da el fuego.
    /// -1 = el del prefab tal cual (EnemyProjectile.Initialize).
    private float DanoDeRoca(EnemyProjectile proj)
    {
        float mult = MultDano;
        return proj != null && mult > 1.001f ? proj.Damage * mult : -1f;
    }

    /// En cada vuelta de la IA: frena en el barro y, si toca, lo hunde. Durante un ataque solo
    /// lo deja pendiente: la embestida se corta (ver ChargeAttack) y el resto se termina antes.
    private void RevisarBarro()
    {
        if (_hundido || _isDead) return;

        bool enBarro = ZonaDeBarro.Contiene(transform.position, out var zona);
        if (enBarro && Time.time >= _sinHundirseHasta && !_hundirPendiente)
        {
            _barro = zona;
            _hundirPendiente = true;
        }

        if (agent && !_isCharging && !_isReturningHome)
        {
            float velocidad = walkSpeed * (enBarro ? frenoEnBarro : 1f);
            if (!Mathf.Approximately(agent.speed, velocidad)) agent.speed = velocidad;
        }

        if (_hundirPendiente && !_isAttacking) StartCoroutine(Hundirse());
    }

    private IEnumerator Hundirse()
    {
        _hundirPendiente = false;
        _hundido = true;
        _isAttacking = true;
        _currentState = BossState.Idle;
        StopAgent();

        if (_armadura) _armadura.enabled = false;
        _rbEraCinematico = _rb != null && _rb.isKinematic;
        if (_rb) _rb.isKinematic = true; // que la física no lo saque del suelo mientras baja

        PlayAnim(ANIM_GETHIT);
        if (_barro) _barro.Chapotear(transform.position);
        _transicion.Tintar(_transicion.TinteDeFase(Fase) * tinteDeBarro, apagarEmision: false);
        FeedbackService.CameraShake(0.35f, 0.3f);
        AvisosDeCombate.QuedarAtrapado(gameObject);

        float baseOffset = agent ? agent.baseOffset : 0f;
        yield return MoverHundimiento(baseOffset, baseOffset - profundidadHundido, 0.35f);

        float idleEn = Time.time + 0.6f;
        float fin = Time.time + segundosHundido;
        while (Time.time < fin)
        {
            if (_isDead) yield break;
            if (idleEn > 0f && Time.time >= idleEn) { idleEn = -1f; PlayAnim(ANIM_IDLE); }
            yield return null;
        }

        PlayAnim(ANIM_ATTACK02); // se sacude el barro
        yield return MoverHundimiento(baseOffset - profundidadHundido, baseOffset, 0.5f);

        if (_rb) _rb.isKinematic = _rbEraCinematico;
        if (_armadura) _armadura.enabled = true;
        _transicion.Tintar(_transicion.TinteDeFase(Fase), apagarEmision: false);
        if (_fuego != null) _fuego.RefrescarAspecto();

        _sinHundirseHasta = Time.time + respiroTrasHundirse;
        _hundido = false;
        PlayAnim(ANIM_IDLE);
        _currentState = BossState.Idle;
        EndAttack();
    }

    /// Si se desactiva a mitad del hundimiento, la corrutina muere: al volver, sale del barro.
    private void ReiniciarBarro()
    {
        if (!_hundido && !_hundirPendiente) return;
        _hundido = false;
        _hundirPendiente = false;
        if (agent) agent.baseOffset = 0f;
        if (_rb) _rb.isKinematic = _rbEraCinematico;
        if (_armadura) _armadura.enabled = true;
        _sinHundirseHasta = Time.time + respiroTrasHundirse;
    }

    /// Baja o sube el cuerpo: con NavMeshAgent mueve su baseOffset (el agente sigue en su sitio
    /// de la malla); sin él, la posición.
    private IEnumerator MoverHundimiento(float desde, float hasta, float duracion)
    {
        float t = 0f;
        float yInicial = transform.position.y;
        while (t < duracion)
        {
            if (_isDead) yield break;
            t += Time.deltaTime;
            float v = Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / duracion));
            if (agent) agent.baseOffset = v;
            else
            {
                var p = transform.position;
                p.y = yInicial + (v - desde);
                transform.position = p;
            }
            yield return null;
        }
        if (agent) agent.baseOffset = hasta;
    }
}
