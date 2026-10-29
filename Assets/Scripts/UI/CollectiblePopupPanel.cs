using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Un aviso de «objeto conseguido» de la pila de <see cref="CollectiblePopupQueue"/>: entra con
/// una pequeña escala, se ve unos segundos (más si se le suma cantidad) y se desvanece. Mientras
/// está congelado (la cola ha perdido el turno por un menú o una cinemática), su tiempo no corre.
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
    [SerializeField] private float slideInDuration = 0.18f;

    [Header("Layout")]
    [SerializeField] private float minPreferredHeight = 100f;

    [Header("Safety")]
    [Tooltip("Tiempo máximo (segundos a la vista) antes de retirar el aviso aunque se le siga sumando cantidad.")]
    [SerializeField] private float maxLifetime = 10f;

    private CanvasGroup _cg;

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
    }

    void OnDestroy()
    {
        Acabado?.Invoke(this);
        Acabado = null;
    }

    public void Init(ItemData item, int amount, float displayDuration)
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

        // El VerticalLayoutGroup del contenedor necesita una altura preferida para colocarlo.
        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = Mathf.Max(100f, minPreferredHeight);
        if (transform.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

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
        // Un frame para que el layout del contenedor lo coloque.
        yield return null;

        var rt = transform as RectTransform;
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
