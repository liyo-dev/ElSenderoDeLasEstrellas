using System.Collections;
using UnityEngine;

/// <summary>
/// Roca: parece una piedra más hasta que alguien se acerca. Entonces tiembla (aviso de golpe que
/// no se para: hay que apartarse) y sale rodando en línea recta. Si atropella al objetivo, le
/// hace daño y lo derriba. Si choca contra un muro o algo sólido, se aturde unos segundos y en
/// ese tiempo los golpes entran enteros; fuera de eso apenas le hacen nada. Los muros rompibles
/// (<see cref="IRompiblePorEmbestida"/>) se vienen abajo con el choque: provocarla delante de
/// uno es la forma de abrir paso. Ver INC-722.
/// </summary>
public sealed class RocaEmbestidora : EnemigoDelMundo, IFiltroDeDano, IExpuestoAlDano
{
    [Header("Cuerpo")]
    [Tooltip("La malla que rueda y tiembla (hijo). Vacío = no rueda.")]
    [SerializeField] private Transform cuerpo;
    [SerializeField, Min(0.2f)] private float radio = 0.8f;
    [Tooltip("Capas que cuentan como suelo.")]
    [SerializeField] private LayerMask suelo = ~0;
    [Tooltip("Capas contra las que choca al rodar (muros, rocas, el jugador).")]
    [SerializeField] private LayerMask choques = ~0;

    [Header("Embestida")]
    [Tooltip("Segundos de temblor antes de salir rodando (es el aviso).")]
    [SerializeField, Min(0.2f)] private float preparacion = 1f;
    [Tooltip("Últimos segundos del temblor en los que ya no corrige la dirección.")]
    [SerializeField, Min(0f)] private float direccionFijaAntes = 0.3f;
    [SerializeField, Min(1f)] private float velocidadDeEmbestida = 13f;
    [SerializeField, Min(1f)] private float distanciaMaxima = 16f;
    [SerializeField, Min(0f)] private float dano = 18f;
    [SerializeField] private DerriboDelJugador derribo = DerriboDelJugador.Estandar;
    [Tooltip("Descanso tras una embestida que no choca con nada.")]
    [SerializeField, Min(0.1f)] private float descansoSinChoque = 1.2f;

    [Header("Aturdida tras chocar")]
    [SerializeField, Min(0.5f)] private float segundosAturdida = 3f;
    [Tooltip("Parte del daño que entra sin estar aturdida.")]
    [SerializeField, Range(0f, 1f)] private float factorSinAturdir = 0.2f;
    [SerializeField] private GameObject vfxAturdida;
    [SerializeField] private GameObject vfxChoque;
    [SerializeField] private GameObject vfxPolvo;
    [SerializeField] private GameObject vfxRebote;
    [SerializeField] private string sfxChoque;

    private enum Estado { Dormida, Preparando, Embistiendo, Aturdida, Descansando }

    public bool Expuesto => Time.time < _aturdidaHasta;

    private Estado _estado = Estado.Dormida;
    private Vector3 _direccion, _cuerpoBase;
    private float _temporizador, _recorrido, _aturdidaHasta = -1f, _ultimoRebote;
    private Transform _aura;
    private ulong _usoAura;
    private readonly RaycastHit[] _impactos = new RaycastHit[12];

    protected override void Awake()
    {
        base.Awake();
        if (cuerpo != null) _cuerpoBase = cuerpo.localPosition;
    }

    protected override void OnDisable()
    {
        RecogerAura();
        base.OnDisable();
    }

    private Vector3 Centro => transform.position + Vector3.up * radio;

    protected override void Pensar(float dt)
    {
        float distancia = DistanciaAlObjetivo();
        switch (_estado)
        {
            case Estado.Dormida:
                if (HayObjetivo && distancia <= alcanceDeDeteccion)
                {
                    EntrarEnCombate();
                    EmpezarPreparacion();
                }
                break;

            case Estado.Preparando:
                _temporizador -= dt;
                if (_temporizador > direccionFijaAntes && HayObjetivo)
                {
                    Vector3 hacia = HaciaElObjetivoPlano();
                    if (hacia.sqrMagnitude > 0.01f) _direccion = hacia.normalized;
                }
                if (cuerpo != null)
                {
                    float fuerza = Mathf.Lerp(0.02f, 0.08f, 1f - _temporizador / preparacion);
                    cuerpo.localPosition = _cuerpoBase + Random.insideUnitSphere * fuerza;
                }
                if (_temporizador <= 0f)
                {
                    if (cuerpo != null) cuerpo.localPosition = _cuerpoBase;
                    _estado = Estado.Embistiendo;
                    _recorrido = 0f;
                    Efecto(vfxPolvo, transform.position, 1.5f);
                }
                break;

            case Estado.Embistiendo:
                Rodar(dt);
                break;

            case Estado.Aturdida:
                if (!Expuesto) Descansar(0.6f);
                break;

            case Estado.Descansando:
                _temporizador -= dt;
                if (_temporizador > 0f) break;
                if (HayObjetivo && distancia <= alcanceDePerdida) EmpezarPreparacion();
                else
                {
                    SalirDeCombate();
                    _estado = Estado.Dormida;
                }
                break;
        }
    }

    private void EmpezarPreparacion()
    {
        _estado = Estado.Preparando;
        _temporizador = preparacion;
        _direccion = HayObjetivo && HaciaElObjetivoPlano().sqrMagnitude > 0.01f ? HaciaElObjetivoPlano().normalized : transform.forward;
        float llegada = HayObjetivo ? DistanciaAlObjetivo() / velocidadDeEmbestida : 0f;
        AmenazasAlJugador.Anunciar(transform, preparacion + llegada, TipoDeAmenaza.NoBloqueable);
        Efecto(vfxPolvo, transform.position, 1.5f);
    }

    private void Rodar(float dt)
    {
        float paso = velocidadDeEmbestida * dt;
        int n = Physics.SphereCastNonAlloc(Centro, radio * 0.9f, _direccion, _impactos, paso + 0.05f, choques, QueryTriggerInteraction.Ignore);
        int elegido = -1;
        float cercano = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            var col = _impactos[i].collider;
            if (col == null || col.transform.IsChildOf(transform)) continue;
            // Lo que tiene la cara hacia arriba es suelo o rampa: se rueda por encima.
            if (_impactos[i].normal.y > 0.6f) continue;
            if (_impactos[i].distance < cercano) { cercano = _impactos[i].distance; elegido = i; }
        }

        if (elegido >= 0)
        {
            var golpe = _impactos[elegido];
            var col = golpe.collider;
            Vector3 punto = golpe.distance <= 0f ? Centro + _direccion * radio : golpe.point;
            if (col.GetComponentInParent<PlayerHealthSystem>() != null || AmenazasAlJugador.EsDelGrupoDelJugador(col.gameObject))
            {
                Atropellar(col.transform);
                return;
            }
            var rompible = col.GetComponentInParent<IRompiblePorEmbestida>();
            if (rompible != null) rompible.RecibirEmbestida(punto, _direccion, gameObject);
            Chocar(punto);
            return;
        }

        Vector3 siguiente = transform.position + _direccion * paso;
        // Sin suelo delante (un barranco): se para en el borde.
        if (!Physics.Raycast(siguiente + Vector3.up * (radio + 0.5f), Vector3.down, out var suelo1, radio + 2.5f, suelo, QueryTriggerInteraction.Ignore))
        {
            Descansar(descansoSinChoque);
            return;
        }
        siguiente.y = suelo1.point.y;
        transform.position = siguiente;
        if (cuerpo != null)
            cuerpo.Rotate(Vector3.Cross(Vector3.up, _direccion), paso / radio * Mathf.Rad2Deg, Space.World);
        _recorrido += paso;
        if (_recorrido >= distanciaMaxima) Descansar(descansoSinChoque);
    }

    private void Atropellar(Transform quien)
    {
        AmenazasAlJugador.RetirarAnuncio(transform);
        if (Danar(quien, dano))
        {
            var jugador = quien.GetComponentInParent<PlayerHealthSystem>();
            if (jugador == null || !derribo.Aplicar(jugador.gameObject, transform.position))
                Empujar(quien, _direccion, 8f);
        }
        Descansar(descansoSinChoque);
    }

    private void Chocar(Vector3 punto)
    {
        AmenazasAlJugador.RetirarAnuncio(transform);
        Efecto(vfxChoque, punto, 2f);
        Sonar(sfxChoque);
        _aturdidaHasta = Time.time + segundosAturdida;
        _estado = Estado.Aturdida;
        RecogerAura();
        _aura = Efecto(vfxAturdida, Centro + Vector3.up * (radio + 0.3f), segundosAturdida, transform);
        if (_aura != null) _usoAura = VfxPoolService.Instance.ObtenerUso(_aura);
    }

    private void Descansar(float segundos)
    {
        AmenazasAlJugador.RetirarAnuncio(transform);
        RecogerAura();
        _estado = Estado.Descansando;
        _temporizador = segundos;
    }

    private void RecogerAura()
    {
        if (_aura != null && VfxPoolService.Instance != null) VfxPoolService.Instance.Recoger(_aura, _usoAura);
        _aura = null;
    }

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (Muerto || Expuesto) return cantidad;
        if (vfxRebote != null && Time.time - _ultimoRebote > 0.2f)
        {
            _ultimoRebote = Time.time;
            Efecto(vfxRebote, Centro, 1f);
        }
        return cantidad * factorSinAturdir;
    }

    protected override void AlMorir()
    {
        RecogerAura();
        Efecto(vfxChoque, Centro, 2f);
        StartCoroutine(Desmoronarse());
    }

    private IEnumerator Desmoronarse()
    {
        if (cuerpo == null) yield break;
        Vector3 escala = cuerpo.localScale;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            cuerpo.localScale = escala * (1f - t / 0.6f);
            yield return null;
        }
        cuerpo.localScale = Vector3.zero;
    }
}
