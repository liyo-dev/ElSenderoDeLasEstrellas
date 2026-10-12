using System.Collections;
using UnityEngine;

/// <summary>
/// Fantasma del mundo abierto: sale de noche, flota y atraviesa paredes, y es intangible hasta
/// que lo alcanza la luz (Bola Prisma u otra magia de Luz; regla en <see cref="ReveladoPorLuz"/>).
/// Revelado se vuelve sólido, brilla y cualquier golpe le hace daño. Ataca con un toque frío
/// que se anuncia (se para con la defensa en el momento justo). Ver INC-722.
/// </summary>
[RequireComponent(typeof(ReveladoPorLuz))]
public sealed class Fantasma : EnemigoDelMundo
{
    [Header("Noche")]
    [Tooltip("Solo aparece de noche (DayNightCycle). Sin ciclo de día en la escena, siempre está.")]
    [SerializeField] private bool soloDeNoche = true;
    [Tooltip("Peso de noche a partir del cual aparece (0 = día, 1 = noche cerrada).")]
    [SerializeField, Range(0f, 1f)] private float umbralDeNoche = 0.5f;
    [Tooltip("Efecto al aparecer y al esfumarse con el día (opcional).")]
    [SerializeField] private GameObject vfxAparecer;

    [Header("Vuelo")]
    [SerializeField, Min(0f)] private float alturaDeVuelo = 1.1f;
    [Tooltip("Amplitud del vaivén arriba y abajo.")]
    [SerializeField, Min(0f)] private float vaiven = 0.2f;
    [SerializeField, Min(0.1f)] private float velocidadVagando = 1.4f;
    [SerializeField, Min(0.1f)] private float velocidadAcecho = 3.2f;
    [Tooltip("Metros alrededor de su sitio por los que vaga sin objetivo.")]
    [SerializeField, Min(0f)] private float radioDeVagar = 6f;
    [Tooltip("Capas que cuentan como suelo para mantener la altura.")]
    [SerializeField] private LayerMask suelo = ~0;

    [Header("Ataque")]
    [SerializeField, Min(0.5f)] private float alcanceDeAtaque = 1.8f;
    [SerializeField, Min(0f)] private float dano = 10f;
    [Tooltip("Segundos de aviso antes del toque.")]
    [SerializeField, Min(0.1f)] private float preparacion = 0.6f;
    [SerializeField, Min(0f)] private float distanciaDeEmbestida = 2.5f;
    [SerializeField, Min(0.1f)] private float enfriamiento = 2f;

    [Header("Aspecto")]
    [Tooltip("Mallas del cuerpo: translúcidas mientras es intangible.")]
    [SerializeField] private Renderer[] cuerpo;
    [Tooltip("Material translúcido de intangible. Vacío = no cambia de aspecto.")]
    [SerializeField] private Material materialIntangible;
    [Tooltip("Luz que se enciende mientras está revelado (opcional).")]
    [SerializeField] private Light luzAlRevelar;

    private enum Estado { Oculto, Vagando, Acechando, Preparando, Embistiendo, Recuperando }

    private Estado _estado = Estado.Vagando;
    private ReveladoPorLuz _luz;
    private Collider[] _colisiones;
    private Material[][] _solidos;
    private Material[][] _intangibles;
    private Vector3 _origen, _destino, _direccion;
    private float _alturaDelSuelo, _proximaRevisionSuelo, _temporizador, _recorrido, _proximoAtaque;
    private float _fase;
    private bool _golpeado;

    protected override void Awake()
    {
        base.Awake();
        _luz = GetComponent<ReveladoPorLuz>();
        _colisiones = GetComponentsInChildren<Collider>(true);
        if (cuerpo == null || cuerpo.Length == 0) cuerpo = GetComponentsInChildren<Renderer>(true);
        _solidos = new Material[cuerpo.Length][];
        _intangibles = new Material[cuerpo.Length][];
        for (int i = 0; i < cuerpo.Length; i++)
        {
            if (cuerpo[i] == null) continue;
            _solidos[i] = cuerpo[i].sharedMaterials;
            _intangibles[i] = new Material[_solidos[i].Length];
            for (int m = 0; m < _intangibles[i].Length; m++)
                _intangibles[i][m] = materialIntangible != null ? materialIntangible : _solidos[i][m];
        }
        _origen = transform.position;
        _alturaDelSuelo = transform.position.y;
        _destino = _origen;
        _fase = Random.value * 10f;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _luz.AlCambiar += AlCambiarLuz;
        AplicarAspecto(_luz.Expuesto);
    }

    protected override void OnDisable()
    {
        _luz.AlCambiar -= AlCambiarLuz;
        base.OnDisable();
    }

    private bool EsDeNoche =>
        !soloDeNoche || DayNightCycle.Instance == null || DayNightCycle.NocheActual >= umbralDeNoche;

    protected override void Pensar(float dt)
    {
        if (!EsDeNoche)
        {
            if (_estado != Estado.Oculto) Ocultarse();
            return;
        }
        if (_estado == Estado.Oculto) Aparecer();

        if (Time.time >= _proximaRevisionSuelo)
        {
            _proximaRevisionSuelo = Time.time + 0.25f;
            if (Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down, out var hit, 12f, suelo, QueryTriggerInteraction.Ignore))
                _alturaDelSuelo = hit.point.y;
        }

        float distancia = DistanciaAlObjetivo();
        switch (_estado)
        {
            case Estado.Vagando:
                if (HayObjetivo && distancia <= alcanceDeDeteccion)
                {
                    EntrarEnCombate();
                    _estado = Estado.Acechando;
                    break;
                }
                if ((Plano(_destino) - Plano(transform.position)).sqrMagnitude < 0.25f)
                {
                    Vector2 c = Random.insideUnitCircle * radioDeVagar;
                    _destino = _origen + new Vector3(c.x, 0f, c.y);
                }
                Volar(_destino, velocidadVagando, dt);
                break;

            case Estado.Acechando:
                if (!HayObjetivo || distancia > alcanceDePerdida)
                {
                    SalirDeCombate();
                    _estado = Estado.Vagando;
                    break;
                }
                if (distancia <= alcanceDeAtaque && Time.time >= _proximoAtaque)
                {
                    _estado = Estado.Preparando;
                    _temporizador = 0f;
                    _direccion = HaciaElObjetivoPlano().normalized;
                    AmenazasAlJugador.Anunciar(transform, preparacion + 0.12f, TipoDeAmenaza.Devolvible);
                    break;
                }
                if (distancia > alcanceDeAtaque * 0.8f) Volar(Objetivo.position, velocidadAcecho, dt);
                else Flotar(dt);
                MirarHacia(HaciaElObjetivoPlano(), 360f);
                break;

            case Estado.Preparando:
                _temporizador += dt;
                // Se echa atrás para coger impulso: el gesto que avisa del toque.
                Volar(transform.position - _direccion, 0.8f, dt);
                MirarHacia(_direccion, 360f);
                if (_temporizador >= preparacion)
                {
                    _estado = Estado.Embistiendo;
                    _recorrido = 0f;
                    _golpeado = false;
                }
                break;

            case Estado.Embistiendo:
                float paso = distanciaDeEmbestida / 0.25f * dt;
                _recorrido += paso;
                Volar(transform.position + _direccion * 2f, distanciaDeEmbestida / 0.25f, dt);
                if (!_golpeado && HayObjetivo && DistanciaAlObjetivo() <= 1.1f)
                {
                    _golpeado = true;
                    Danar(Objetivo, dano);
                }
                if (_recorrido >= distanciaDeEmbestida) Recuperarse(0.5f);
                break;

            case Estado.Recuperando:
                _temporizador -= dt;
                Flotar(dt);
                if (_temporizador <= 0f) _estado = Estado.Acechando;
                break;
        }
    }

    private void Recuperarse(float segundos)
    {
        AmenazasAlJugador.RetirarAnuncio(transform);
        _estado = Estado.Recuperando;
        _temporizador = segundos;
        _proximoAtaque = Time.time + enfriamiento;
    }

    private static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private void Volar(Vector3 hacia, float velocidad, float dt)
    {
        Vector3 actual = transform.position;
        Vector3 plano = Vector3.MoveTowards(Plano(actual), Plano(hacia), velocidad * dt);
        Vector3 mover = plano - Plano(actual);
        if (mover.sqrMagnitude > 0.0001f && _estado != Estado.Preparando) MirarHacia(mover, 300f);
        transform.position = new Vector3(plano.x, AlturaDeseada(actual.y, dt), plano.z);
    }

    private void Flotar(float dt)
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, AlturaDeseada(p.y, dt), p.z);
    }

    private float AlturaDeseada(float actual, float dt)
    {
        float meta = _alturaDelSuelo + alturaDeVuelo + Mathf.Sin((Time.time + _fase) * 2f) * vaiven;
        return Mathf.Lerp(actual, meta, 1f - Mathf.Exp(-4f * dt));
    }

    private void Ocultarse()
    {
        Efecto(vfxAparecer, transform.position + Vector3.up, 2f);
        AmenazasAlJugador.RetirarAnuncio(transform);
        SalirDeCombate();
        _estado = Estado.Oculto;
        Visible(false);
    }

    private void Aparecer()
    {
        _estado = Estado.Vagando;
        Visible(true);
        Efecto(vfxAparecer, transform.position + Vector3.up, 2f);
    }

    private void Visible(bool si)
    {
        foreach (var r in cuerpo) if (r != null) r.enabled = si;
        foreach (var c in _colisiones) if (c != null) c.enabled = si;
        Apuntable(si);
        if (luzAlRevelar != null) luzAlRevelar.enabled = si && _luz.Expuesto;
    }

    private void AlCambiarLuz(bool revelado) => AplicarAspecto(revelado);

    private void AplicarAspecto(bool revelado)
    {
        if (materialIntangible != null)
            for (int i = 0; i < cuerpo.Length; i++)
                if (cuerpo[i] != null) cuerpo[i].sharedMaterials = revelado ? _solidos[i] : _intangibles[i];
        if (luzAlRevelar != null) luzAlRevelar.enabled = revelado && _estado != Estado.Oculto;
    }

    protected override void AlRecibirDano(float cantidad, GameObject quien)
    {
        if (_estado == Estado.Preparando || _estado == Estado.Embistiendo) Recuperarse(0.6f);
    }

    protected override void AlMorir()
    {
        if (luzAlRevelar != null) luzAlRevelar.enabled = false;
        StartCoroutine(Encogerse());
    }

    private IEnumerator Encogerse()
    {
        Vector3 escala = transform.localScale;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            transform.localScale = escala * (1f - t / 0.8f);
            transform.position += Vector3.up * Time.deltaTime * 0.6f;
            yield return null;
        }
        transform.localScale = Vector3.zero;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? _origen : transform.position, radioDeVagar);
    }
}
