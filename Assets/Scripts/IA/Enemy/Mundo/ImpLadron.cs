using System.Collections;
using UnityEngine;

/// <summary>
/// Imp ladrón: un diablillo volador que revolotea, se acerca, da un tirón (anunciado: la defensa
/// en el momento justo lo para y lo deja aturdido) y, si entra, roba algo del inventario (el
/// primero que tenga de <see cref="botin"/>, normalmente Esencia y luego Monedas) y huye con el
/// botín a la vista. Se recupera si se le alcanza: cualquier golpe mientras lo lleva se lo hace
/// soltar (en orbes que se recogen como los de los enemigos). Si se aleja demasiado o pasa el
/// tiempo de huida, se esfuma con lo robado. Ver INC-722.
/// </summary>
public sealed class ImpLadron : EnemigoDelMundo
{
    [Header("Vuelo")]
    [SerializeField, Min(0f)] private float alturaDeVuelo = 2.2f;
    [Tooltip("Altura sobre el objetivo al dar el tirón.")]
    [SerializeField, Min(0f)] private float alturaDelTiron = 1.1f;
    [SerializeField, Min(0.1f)] private float velocidad = 4.5f;
    [SerializeField, Min(0.1f)] private float velocidadDeHuida = 6.5f;
    [Tooltip("Radio del círculo en el que revolotea sin objetivo.")]
    [SerializeField, Min(0.5f)] private float radioDeRevoloteo = 4f;
    [Tooltip("Capas que cuentan como suelo para mantener la altura.")]
    [SerializeField] private LayerMask suelo = ~0;
    [Tooltip("Capas que esquiva al huir (paredes, rocas).")]
    [SerializeField] private LayerMask obstaculos = ~0;

    [Header("Robo")]
    [Tooltip("Lo que intenta robar, por orden: se lleva el primero que el jugador tenga.")]
    [SerializeField] private ItemData[] botin = new ItemData[0];
    [SerializeField, Min(1)] private int cantidadARobar = 15;
    [SerializeField, Min(0.5f)] private float alcanceDelTiron = 1.6f;
    [Tooltip("Segundos de aviso antes del tirón.")]
    [SerializeField, Min(0.1f)] private float preparacionDelTiron = 0.5f;
    [SerializeField, Min(0f)] private float danoDelTiron = 4f;
    [Tooltip("Segundos tras perder el botín antes de volver a intentarlo.")]
    [SerializeField, Min(0f)] private float enfriamientoTrasRobo = 5f;
    [SerializeField] private GameObject vfxRobo;
    [Tooltip("Lo que lleva a la vista mientras huye con el botín (opcional).")]
    [SerializeField] private GameObject vfxBotin;
    [Tooltip("Orbe en el que suelta lo robado si es la moneda de abajo (como los de los enemigos). Lo demás vuelve directo al inventario.")]
    [SerializeField] private GameObject orbeDelBotin;
    [Tooltip("Moneda que se suelta en orbes (la del aspecto del orbe, normalmente la Esencia).")]
    [SerializeField] private ItemData monedaDelOrbe;
    [SerializeField] private string sfxRobo = "Coin";
    [SerializeField] private string sfxRecuperar = "Coin";

    [Header("Huida")]
    [SerializeField, Min(1f)] private float segundosDeHuida = 10f;
    [Tooltip("Metros desde el objetivo a los que se esfuma con el botín.")]
    [SerializeField, Min(5f)] private float distanciaDeEscape = 28f;

    [Header("Animaciones (estados del Animator)")]
    [SerializeField] private string animVuelo = "Fly Forward In Place";
    [SerializeField] private string animTiron = "Slash Attack";
    [SerializeField] private string animGolpe = "Take Damage";
    [SerializeField] private string animBurla = "Cast Spell";

    private enum Estado { Revoloteando, Acercandose, Tirando, Huyendo, Aturdido }

    private Estado _estado = Estado.Revoloteando;
    private Vector3 _origen;
    private float _angulo, _alturaDelSuelo, _proximaRevisionSuelo, _temporizador, _puedeRobar, _huyendoDesde;
    private ItemData _robado;
    private int _cantidad;
    private Transform _vfxBotin;
    private ulong _usoBotin;
    private readonly RaycastHit[] _impactos = new RaycastHit[8];

    public bool LlevaBotin => _robado != null;

    protected override void Awake()
    {
        base.Awake();
        _origen = transform.position;
        _alturaDelSuelo = transform.position.y - alturaDeVuelo;
        _angulo = Random.value * Mathf.PI * 2f;
    }

    protected override void OnDisable()
    {
        RecogerBotinVisible();
        base.OnDisable();
    }

    protected override void Pensar(float dt)
    {
        if (Time.time >= _proximaRevisionSuelo)
        {
            _proximaRevisionSuelo = Time.time + 0.25f;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var hit, 30f, suelo, QueryTriggerInteraction.Ignore))
                _alturaDelSuelo = hit.point.y;
        }

        float distancia = DistanciaAlObjetivo();
        switch (_estado)
        {
            case Estado.Revoloteando:
            {
                _angulo += velocidad * 0.5f / radioDeRevoloteo * dt;
                Vector3 punto = _origen + new Vector3(Mathf.Cos(_angulo), 0f, Mathf.Sin(_angulo)) * radioDeRevoloteo;
                Volar(punto, _alturaDelSuelo + alturaDeVuelo, velocidad * 0.6f, dt);
                Animar(animVuelo);
                if (HayObjetivo && distancia <= alcanceDeDeteccion && Time.time >= _puedeRobar)
                {
                    EntrarEnCombate();
                    _estado = Estado.Acercandose;
                }
                break;
            }

            case Estado.Acercandose:
                if (!HayObjetivo || distancia > alcanceDePerdida)
                {
                    SalirDeCombate();
                    _estado = Estado.Revoloteando;
                    break;
                }
                Volar(Objetivo.position, Objetivo.position.y + alturaDelTiron, velocidad, dt);
                Animar(animVuelo);
                if (distancia <= alcanceDelTiron)
                {
                    _estado = Estado.Tirando;
                    _temporizador = preparacionDelTiron;
                    AnimarDeNuevo(animTiron);
                    AmenazasAlJugador.Anunciar(transform, preparacionDelTiron, TipoDeAmenaza.Devolvible);
                }
                break;

            case Estado.Tirando:
                if (HayObjetivo) MirarHacia(HaciaElObjetivoPlano(), 540f);
                _temporizador -= dt;
                if (_temporizador > 0f) break;
                if (!HayObjetivo || distancia > alcanceDelTiron * 1.5f)
                {
                    _estado = Estado.Acercandose;
                    break;
                }
                if (!Danar(Objetivo, danoDelTiron))
                {
                    // Parado con la defensa (o el objetivo no puede recibir daño): se lleva un susto.
                    Aturdir(1f);
                    _puedeRobar = Time.time + 1.5f;
                    break;
                }
                Robar();
                Huir();
                break;

            case Estado.Huyendo:
                ActualizarHuida(dt, distancia);
                break;

            case Estado.Aturdido:
                _temporizador -= dt;
                Volar(transform.position, _alturaDelSuelo + alturaDeVuelo, velocidad, dt);
                if (_temporizador <= 0f) _estado = Estado.Revoloteando;
                break;
        }
    }

    private void Volar(Vector3 hacia, float altura, float rapidez, float dt)
    {
        Vector3 actual = transform.position;
        Vector3 plano = Vector3.MoveTowards(new Vector3(actual.x, 0f, actual.z), new Vector3(hacia.x, 0f, hacia.z), rapidez * dt);
        Vector3 mover = plano - new Vector3(actual.x, 0f, actual.z);
        if (mover.sqrMagnitude > 0.00001f) MirarHacia(mover, 540f);
        float y = Mathf.MoveTowards(actual.y, Mathf.Max(altura, _alturaDelSuelo + 0.6f), rapidez * dt);
        transform.position = new Vector3(plano.x, y, plano.z);
    }

    // ── Robo y huida ─────────────────────────────────────────────────────

    private void Robar()
    {
        _robado = null;
        _cantidad = 0;
        if (Objetivo != null && Objetivo.GetComponentInParent<PlayerHealthSystem>() != null &&
            PlayerService.TryGetComponent(out Inventory inventario, allowSceneLookup: false) && inventario != null)
        {
            foreach (var item in botin)
            {
                if (item == null) continue;
                int tiene = inventario.Count(item.itemId);
                if (tiene <= 0) continue;
                int cuanto = Mathf.Min(cantidadARobar, tiene);
                if (!inventario.TryConsume(item, cuanto, true)) continue;
                _robado = item;
                _cantidad = cuanto;
                break;
            }
        }
        Efecto(vfxRobo, transform.position, 1.5f);
        if (_robado == null) return;
        Sonar(sfxRobo);
        if (vfxBotin != null && VfxPoolService.Instance != null)
        {
            _vfxBotin = VfxPoolService.Instance.Play(vfxBotin, transform.position + Vector3.down * 0.4f,
                Quaternion.identity, segundosDeHuida + 5f, transform);
            if (_vfxBotin != null) _usoBotin = VfxPoolService.Instance.ObtenerUso(_vfxBotin);
        }
    }

    private void Huir()
    {
        _estado = Estado.Huyendo;
        _huyendoDesde = Time.time;
        AmenazasAlJugador.RetirarAnuncio(transform);
        if (_robado == null) AnimarDeNuevo(animBurla);
    }

    private void ActualizarHuida(float dt, float distancia)
    {
        float tiempo = Time.time - _huyendoDesde;
        if (_robado == null)
        {
            // Sin botín: se burla un momento y vuelve a lo suyo.
            Volar(transform.position, _alturaDelSuelo + alturaDeVuelo, velocidad, dt);
            if (tiempo >= 2.5f) { _estado = Estado.Revoloteando; _puedeRobar = Time.time + enfriamientoTrasRobo; }
            return;
        }
        if (tiempo >= segundosDeHuida || (HayObjetivo && distancia >= distanciaDeEscape) || !HayObjetivo)
        {
            Esfumarse();
            return;
        }
        Vector3 lejos = -HaciaElObjetivoPlano();
        lejos = lejos.sqrMagnitude > 0.01f ? lejos.normalized : transform.forward;
        int n = Physics.SphereCastNonAlloc(transform.position, 0.5f, lejos, _impactos, 2.5f, obstaculos, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (_impactos[i].collider == null || _impactos[i].collider.transform.IsChildOf(transform)) continue;
            // Algo delante: lo rodea por un lado y sube.
            lejos = Vector3.Cross(Vector3.up, lejos) + lejos * 0.3f;
            break;
        }
        Volar(transform.position + lejos * 3f, _alturaDelSuelo + alturaDeVuelo + 1f, velocidadDeHuida, dt);
        Animar(animVuelo);
    }

    private void SoltarBotin()
    {
        if (_robado == null) return;
        var item = _robado;
        int cantidad = _cantidad;
        _robado = null;
        _cantidad = 0;
        RecogerBotinVisible();

        if (orbeDelBotin != null && item == monedaDelOrbe && item.usageKind == ItemData.ItemUsageKind.Currency)
        {
            int piezas = Mathf.Clamp(cantidad, 1, 3);
            int parte = cantidad / piezas, resto = cantidad % piezas;
            for (int i = 0; i < piezas; i++)
                SoltarOrbe(item, parte + (i < resto ? 1 : 0));
            return;
        }
        if (PlayerService.TryGetComponent(out Inventory inventario, allowSceneLookup: false) && inventario != null)
            inventario.Add(item, cantidad);
        Sonar(sfxRecuperar);
    }

    private void SoltarOrbe(ItemData item, int cantidad)
    {
        Vector2 c = Random.insideUnitCircle * 0.4f;
        var go = Instantiate(orbeDelBotin, transform.position + new Vector3(c.x, 0f, c.y), Quaternion.identity);
        if (!go.TryGetComponent<BattleOrb>(out var orbe))
        {
            // El prefab es arte puro, como en OrbDropper: la lógica del orbe se añade aquí.
            if (!go.TryGetComponent<Collider>(out _))
            {
                var col = go.AddComponent<SphereCollider>();
                col.radius = 0.3f;
                col.isTrigger = true;
            }
            if (!go.TryGetComponent<Rigidbody>(out _)) go.AddComponent<Rigidbody>();
            orbe = go.AddComponent<BattleOrb>();
            orbe.Configure(OrbType.Currency, 0f);
        }
        orbe.ConfigureCurrency(item, cantidad, sfxRecuperar);
    }

    private void RecogerBotinVisible()
    {
        if (_vfxBotin != null && VfxPoolService.Instance != null) VfxPoolService.Instance.Recoger(_vfxBotin, _usoBotin);
        _vfxBotin = null;
    }

    private void Aturdir(float segundos)
    {
        AmenazasAlJugador.RetirarAnuncio(transform);
        _estado = Estado.Aturdido;
        _temporizador = segundos;
        AnimarDeNuevo(animGolpe);
    }

    protected override void AlRecibirDano(float cantidad, GameObject quien)
    {
        if (_robado != null)
        {
            SoltarBotin();
            _puedeRobar = Time.time + enfriamientoTrasRobo;
            Aturdir(1.2f);
            return;
        }
        Aturdir(_estado == Estado.Tirando ? 0.8f : 0.4f);
    }

    protected override void AlMorir()
    {
        SoltarBotin();
        RecogerBotinVisible();
        StartCoroutine(Caer());
    }

    private IEnumerator Caer()
    {
        float vy = 0f;
        while (transform.position.y > _alturaDelSuelo + 0.05f)
        {
            vy -= 9.8f * Time.deltaTime;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, Mathf.Max(_alturaDelSuelo, p.y + vy * Time.deltaTime), p.z);
            yield return null;
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(Application.isPlaying ? _origen : transform.position, radioDeRevoloteo);
    }
}
