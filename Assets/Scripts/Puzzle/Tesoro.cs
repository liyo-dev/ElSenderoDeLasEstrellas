using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Algo que hacer ante un tesoro antes de abrirlo (un mímico que despierta, un guardián…).
/// Si <see cref="Interceptar"/> devuelve true, el tesoro no se abre: quien lo intercepta llama
/// a <see cref="Tesoro.Abrir"/> cuando toque (por ejemplo, al vencerlo).
/// Va en el mismo objeto que el tesoro o en uno de sus hijos.
/// </summary>
public interface IAntesDeAbrirTesoro
{
    bool Interceptar(Tesoro tesoro, GameObject quien);
}

/// <summary>
/// Tesoro del mundo: un cofre o un punto de tesoro (algo enterrado, un brillo). Declara lo que
/// hace falta para abrirlo (<see cref="RequisitoDeAcceso"/>: nada, una habilidad, un objeto, un
/// hechizo como Amanecer) y, si falta, lo dice. Al abrirlo entrega su recompensa por las vías
/// oficiales (efectos de recogida para Esencia, objetos y prendas; página del grimorio para un
/// hechizo) y lo guarda en la partida (flag TESORO_&lt;id&gt;). Cuenta en el registro de tesoros
/// de su región. Se abre con el botón de interactuar (Interactable).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class Tesoro : MonoBehaviour
{
    /// <summary>Prefijo de los flags de los tesoros abiertos.</summary>
    public const string Prefijo = "TESORO_";
    /// <summary>Región por defecto (texto TESORO_REGION_MUNDO).</summary>
    public const string RegionPorDefecto = "MUNDO";

    public enum Forma
    {
        /// <summary>Cofre con tapa que se abre.</summary>
        Cofre,
        /// <summary>Punto de tesoro: lo que se ve (montículo, brillo) desaparece al cogerlo.</summary>
        Punto
    }

    [Tooltip("Id fijo. Vacío = automático (escena + nombre + posición).")]
    [SerializeField] private string idManual;
    [Tooltip("Región del registro de tesoros (texto TESORO_REGION_<región>).")]
    [SerializeField] private string region = RegionPorDefecto;
    [SerializeField] private Forma forma = Forma.Cofre;

    [Header("Qué hace falta")]
    [SerializeField] private RequisitoDeAcceso requisito = new RequisitoDeAcceso();
    [SerializeField, Min(0.5f)] private float duracionDelAviso = 3f;

    [Header("Recompensa")]
    [Tooltip("Esencia, objetos y prendas (efectos de recogida, como los pickups del mundo).")]
    [SerializeField] private PickupEffect[] recompensas = Array.Empty<PickupEffect>();
    [Tooltip("Hechizo que enseña, como una página del grimorio (None = ninguno).")]
    [SerializeField] private SpellId paginaDelGrimorio = SpellId.None;

    [Header("Aspecto")]
    [Tooltip("Tapa del cofre (gira al abrirse).")]
    [SerializeField] private Transform tapa;
    [SerializeField] private Vector3 tapaAbierta = new Vector3(-100f, 0f, 0f);
    [SerializeField, Min(0.05f)] private float duracionDeApertura = 0.5f;
    [Tooltip("Lo que deja de verse al abrirlo (el montículo de un punto de tesoro, un brillo…).")]
    [SerializeField] private GameObject[] ocultarAlAbrir = Array.Empty<GameObject>();
    [SerializeField] private string sfxAlAbrir = "Chest";

    [Header("Eventos")]
    [Tooltip("Al abrirlo (no al cargar la partida con él ya abierto).")]
    public UnityEvent alAbrirse;
    [Tooltip("Al cargar la partida con él ya abierto: deja el mundo como quedó.")]
    public UnityEvent alRestaurar;

    /// <summary>Cualquier tesoro abierto por el jugador (para el registro, logros, la brújula…).</summary>
    public static event Action<Tesoro> Abierto;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Abierto = null;
#endif

    private Interactable _interactable;
    private FlagDePartida _flag;
    private string _id;
    private bool _abierto;
    private bool _empezado;

    public bool EstaAbierto => _abierto;
    public string Id => _id ??= string.IsNullOrWhiteSpace(idManual) ? IdDePersistencia.DeObjeto(gameObject) : idManual.Trim();
    public string Region => string.IsNullOrWhiteSpace(region) ? RegionPorDefecto : region.Trim();
    public string Clave => ClaveDe(Id);
    public RequisitoDeAcceso Requisito => requisito;
    public Forma Tipo => forma;

    public static string ClaveDe(string id) => Prefijo + id;

    private void Awake()
    {
        _interactable = GetComponent<Interactable>();
        _flag = new FlagDePartida(nameof(Tesoro), () => Clave, Restaurar);
    }

    private void OnEnable()
    {
        _interactable.OnInteract.AddListener(AlInteractuar);
        RegistroDeTesoros.Conocer(Id, Region);
        if (_empezado) _flag.Escuchar();
    }

    // La primera comprobación espera a Start, como EstadoPersistente: lo que se restaura ya ha
    // hecho su Awake.
    private void Start()
    {
        _empezado = true;
        _flag.Escuchar();
    }

    private void OnDisable()
    {
        _interactable.OnInteract.RemoveListener(AlInteractuar);
        _flag.DejarDeEscuchar();
        if (tapa != null) tapa.DOKill();
    }

    private void AlInteractuar(GameObject quien)
    {
        if (_abierto) return;
        if (!requisito.Cumple())
        {
            requisito.AvisarDeLoQueFalta(duracionDelAviso);
            return;
        }
        foreach (var guardian in GetComponentsInChildren<IAntesDeAbrirTesoro>())
            if (guardian.Interceptar(this, quien)) return;
        Abrir(quien);
    }

    /// <summary>
    /// Abre el tesoro y entrega la recompensa, sin comprobar el requisito ni a los guardianes
    /// (lo llama el propio tesoro o quien lo interceptó, al terminar).
    /// </summary>
    public void Abrir(GameObject quien = null)
    {
        if (_abierto) return;
        _abierto = true;
        _flag.Marcar();
        _interactable.EnableInteraction(false);

        if (!string.IsNullOrEmpty(sfxAlAbrir))
            AudioService.Instance?.PlaySFX(sfxAlAbrir, 1f, transform.position);
        if (tapa != null)
        {
            tapa.DOKill();
            tapa.DOLocalRotate(tapaAbierta, duracionDeApertura).SetEase(Ease.OutBack);
        }
        Ocultar();
        Entregar();

        alAbrirse.Invoke();
        Abierto?.Invoke(this);
    }

    private void Entregar()
    {
        if (recompensas != null && recompensas.Length > 0)
        {
            if (PlayerService.TryGetComponent(out PlayerPickupCollector recogedor, allowSceneLookup: false) && recogedor != null)
            {
                for (int i = 0; i < recompensas.Length; i++)
                    recogedor.TryCollect(recompensas[i], out _);
            }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            else Debug.LogWarning($"[Tesoro] {name}: no encuentro el PlayerPickupCollector; la recompensa no se entrega.");
#endif
        }
        PaginaDelGrimorio.Aprender(paginaDelGrimorio);
    }

    private void Restaurar()
    {
        if (_abierto) return;
        _abierto = true;
        _interactable.EnableInteraction(false);
        if (tapa != null)
        {
            tapa.DOKill();
            tapa.localRotation = Quaternion.Euler(tapaAbierta);
        }
        Ocultar();
        alRestaurar.Invoke();
    }

    private void Ocultar()
    {
        if (ocultarAlAbrir == null) return;
        for (int i = 0; i < ocultarAlAbrir.Length; i++)
            if (ocultarAlAbrir[i] != null && ocultarAlAbrir[i].activeSelf) ocultarAlAbrir[i].SetActive(false);
    }
}
