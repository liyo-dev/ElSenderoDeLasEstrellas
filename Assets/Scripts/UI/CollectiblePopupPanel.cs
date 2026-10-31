using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Un aviso de «objeto conseguido» de la pila de <see cref="CollectiblePopupQueue"/>: entra
/// deslizándose desde un lado con una pequeña escala, se ve unos segundos (más si se le suma
/// cantidad) y se desvanece. Su sitio en la pila lo decide la cola (<see cref="ColocarEn"/>) y el
/// aviso se desliza hasta él, así que al irse uno los de detrás suben. Mientras está congelado (la
/// cola ha perdido el turno por un menú o una cinemática), su tiempo no corre. Ver INC-880.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CollectiblePopupPanel : MonoBehaviour
{
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
        if (_rt != null) _rt.anchoredPosition = sitio + desplazamientoDeEntrada;

        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        if (!enabled) enabled = true;

        if (_cg != null) _cg.alpha = 0f;
        StartCoroutine(PlayLifecycle());
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

    private IEnumerator PlayLifecycle()
    {
        var rt = _rt;
        if (rt != null) rt.localScale = Vector3.one * 0.85f;

        float t = 0f;
        while (t < slideInDuration)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(t / slideInDuration);
            if (rt != null) rt.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, Mathf.SmoothStep(0f, 1f, f));
            if (_cg != null) _cg.alpha = f;
            yield return null;
        }

        while (_restante > 0f && _vivido < maxLifetime)
        {
            if (!_congelado)
            {
                _restante -= Time.unscaledDeltaTime;
                _vivido += Time.unscaledDeltaTime;
            }
            yield return null;
        }

        t = 0f;
        float startAlpha = _cg != null ? _cg.alpha : 1f;
        while (t < fadeOutDuration)
        {
            if (!_congelado) t += Time.unscaledDeltaTime;
            if (_cg != null) _cg.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / fadeOutDuration));
            yield return null;
        }

        Destroy(gameObject);
    }
}
