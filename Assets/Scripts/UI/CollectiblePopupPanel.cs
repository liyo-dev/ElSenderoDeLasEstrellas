using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Un aviso de «objeto conseguido» de la pila de <see cref="CollectiblePopupQueue"/>: entra
/// deslizándose desde un lado con una pequeña escala, se ve unos segundos (más si se le suma
/// cantidad) y se desvanece. Su sitio en la pila lo decide la cola (<see cref="ColocarEn"/>) y el
/// aviso se desliza hasta él, así que al irse uno los de detrás suben. Mientras está congelado (la
/// cola ha perdido el turno por un menú o una cinemática), su tiempo no corre. Ver INC-880,
/// INC-1253.
///
/// Su vida avanza en <see cref="Update"/> y no en una corrutina: el lienzo de objetos conseguidos se
/// apaga entero al empezar una cinemática o al salir la pantalla de carga (<see cref="SceneBoundUI"/>),
/// y una corrutina se queda muerta para siempre al desactivarse el objeto, dejando el aviso clavado
/// en pantalla. En Update, el tiempo se para mientras no se ve y sigue al volver.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CollectiblePopupPanel : MonoBehaviour
{
    private enum EtapaDeVida { Entrando, EnPantalla, Saliendo }

    [Header("References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Image iconImage;

    [Header("Animation")]
    [SerializeField] private float fadeOutDuration = 0.2f;

    [Header("Slide")]
    [SerializeField] private float slideInDuration = 0.25f;

    [Tooltip("Desde dónde entra el aviso, respecto a su sitio en la pila (positivo en X: desde la derecha).")]
    [SerializeField] private Vector2 desplazamientoDeEntrada = new Vector2(80f, 0f);

    [Tooltip("Rapidez con la que el aviso se desliza a su sitio cuando la pila se recoloca.")]
    [SerializeField] private float rapidezDeRecolocar = 12f;

    [Header("Safety")]
    [Tooltip("Tiempo máximo (segundos a la vista) antes de retirar el aviso aunque se le siga sumando cantidad.")]
    [SerializeField] private float maxLifetime = 10f;

    private CanvasGroup _cg;
    private RectTransform _rt;
    private Vector2 _sitio;

    private string _itemId;
    private string _itemName;
    private int _currentAmount;
    private float _displayDuration;
    // Segundos a la vista que le quedan y que lleva en total; solo avanzan sin congelar.
    private float _restante;
    private float _vivido;
    private bool _congelado;

    private EtapaDeVida _etapa;
    // Segundos dentro de la etapa actual (entrada, salida) o de la espera.
    private float _etapaVivida;
    // Alfa con el que empezó la salida (puede pillarla a medias si le suman cantidad).
    private float _alfaAlEmpezarLaSalida;

    /// Se dispara al destruirse el aviso (acabado o retirado).
    public event System.Action<CollectiblePopupPanel> Acabado;

    public string ItemId => _itemId;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        _rt = transform as RectTransform;
    }

    void Update()
    {
        DeslizarASuSitio();
        AvanzarVida();
    }

    private void DeslizarASuSitio()
    {
        if (_rt == null) return;
        Vector2 actual = _rt.anchoredPosition;
        if ((actual - _sitio).sqrMagnitude < 0.01f) return;
        float f = 1f - Mathf.Exp(-rapidezDeRecolocar * Time.unscaledDeltaTime);
        _rt.anchoredPosition = Vector2.Lerp(actual, _sitio, f);
    }

    /// <summary>Su sitio en la pila (anchoredPosition); el aviso se desliza hasta él.</summary>
    public void ColocarEn(Vector2 sitio) => _sitio = sitio;

    void OnDestroy()
    {
        Acabado?.Invoke(this);
        Acabado = null;
    }

    public void Init(ItemData item, int amount, float displayDuration, Vector2 sitio)
    {
        _itemId = item != null ? (item.itemId ?? item.displayName) : "__null__";
        _itemName = item != null ? item.GetLocalizedName() : "";
        _currentAmount = amount;
        _displayDuration = displayDuration;
        _restante = displayDuration;
        _vivido = 0f;

        if (nameText != null) nameText.text = _itemName;
        if (iconImage != null) iconImage.sprite = item != null ? item.icon : null;
        ActualizarCantidad();

        _sitio = sitio;
        if (_rt != null)
        {
            _rt.anchoredPosition = sitio + desplazamientoDeEntrada;
            _rt.localScale = Vector3.one * 0.85f;
        }

        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        if (!enabled) enabled = true;

        _etapa = EtapaDeVida.Entrando;
        _etapaVivida = 0f;
        if (_cg != null) _cg.alpha = 0f;
    }

    /// <summary>Suma cantidad al aviso (otra entrada del mismo objeto) y alarga su tiempo en pantalla.</summary>
    public void AddAmount(int extra)
    {
        if (extra <= 0) return;
        _currentAmount += extra;
        ActualizarCantidad();
        _restante = _displayDuration;
    }

    /// <summary>Para (o reanuda) el tiempo en pantalla mientras la pila no está a la vista.</summary>
    public void Congelar(bool congelado) => _congelado = congelado;

    private void ActualizarCantidad()
    {
        if (amountText == null) return;
        string cantidad = _currentAmount > 0 ? $"+{_currentAmount}" : "";
        // Algunos prefabs comparten el mismo TMP para el nombre y la cantidad.
        amountText.text = amountText == nameText && !string.IsNullOrEmpty(_itemName)
            ? $"{_itemName}  {cantidad}"
            : cantidad;
    }

    /// <summary>
    /// Avanza la vida del aviso: entrada, espera y salida. Se para sola mientras el objeto no está
    /// activo (el lienzo apagado por una cinemática o la pantalla de carga), así que al volver sigue
    /// donde estaba en vez de quedarse clavado.
    /// </summary>
    private void AvanzarVida()
    {
        switch (_etapa)
        {
            case EtapaDeVida.Entrando:
            {
                _etapaVivida += Time.unscaledDeltaTime;
                float f = slideInDuration > 0f ? Mathf.Clamp01(_etapaVivida / slideInDuration) : 1f;
                if (_rt != null) _rt.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, Mathf.SmoothStep(0f, 1f, f));
                if (_cg != null) _cg.alpha = f;
                if (f < 1f) return;
                _etapa = EtapaDeVida.EnPantalla;
                _etapaVivida = 0f;
                return;
            }

            case EtapaDeVida.EnPantalla:
            {
                if (_congelado) return;
                _restante -= Time.unscaledDeltaTime;
                _vivido += Time.unscaledDeltaTime;
                if (_restante > 0f && _vivido < maxLifetime) return;
                _etapa = EtapaDeVida.Saliendo;
                _etapaVivida = 0f;
                _alfaAlEmpezarLaSalida = _cg != null ? _cg.alpha : 1f;
                return;
            }

            case EtapaDeVida.Saliendo:
            {
                if (_congelado) return;
                _etapaVivida += Time.unscaledDeltaTime;
                float f = fadeOutDuration > 0f ? Mathf.Clamp01(_etapaVivida / fadeOutDuration) : 1f;
                if (_cg != null) _cg.alpha = Mathf.Lerp(_alfaAlEmpezarLaSalida, 0f, f);
                if (f < 1f) return;
                enabled = false;
                Destroy(gameObject);
                return;
            }
        }
    }
}
