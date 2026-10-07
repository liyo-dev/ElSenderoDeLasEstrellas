using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Componente para cada card de item en la lista de la tienda.
/// </summary>
public class ShopItemCard : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI stockText;
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [Tooltip("Icono de la moneda junto al precio. Toma el icono de la moneda de la tienda; si está vacío, se busca el hijo «CoinIcon».")]
    [SerializeField] private Image currencyIcon;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.82f, 0.16f, 1f);

    [Header("Rediseño visual - chip de stock (icono real, ver coin.png)")]
    [Tooltip("Fondo tipo 'chip' detrás de stockText. Solo se muestra cuando el item tiene stock limitado.")]
    [SerializeField] private Image stockChipBackground;
    [SerializeField] private Sprite stockChipSpriteAvailable;
    [SerializeField] private Sprite stockChipSpriteUnavailable;

    private System.Action _onSelect;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        
        if (button != null)
            button.onClick.AddListener(() => _onSelect?.Invoke());

        if (currencyIcon == null)
        {
            var hijo = transform.Find("CoinIcon");
            if (hijo != null) currencyIcon = hijo.GetComponent<Image>();
        }
    }

    /// <param name="moneda">Moneda con la que cobra la tienda: su icono acompaña al precio.</param>
    public void Setup(ShopController.ShopItemEntry entry, int index, System.Action onSelect, ItemData moneda = null)
    {
        _onSelect = onSelect;
        if (currencyIcon != null && moneda != null && moneda.icon != null)
            currencyIcon.sprite = moneda.icon;
        
        if (entry == null || entry.item == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ShopItemCard] Setup: entry o item es null");
#endif
            return;
        }
        
        var item = entry.item;
        int price = entry.GetBuyPrice();
        
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ShopItemCard] Setup: {item.displayName}, precio={price}");
#endif
        
        if (iconImage != null)
            iconImage.sprite = item.icon;
        else
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ShopItemCard] iconImage es null");
            #endif
        }
        
        if (nameText != null)
            nameText.text = item.GetLocalizedName();
        else
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ShopItemCard] nameText es null");
            #endif
        }
        
        if (priceText != null)
        {
            priceText.text = $"{price}";
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[ShopItemCard] PriceText actualizado a: {priceText.text}");
#endif
        }
        else
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[ShopItemCard] priceText es NULL - no está asignado en el inspector");
            #endif
        }

        if (stockText != null)
        {
            if (entry.limitedStock)
            {
                string key = entry.HasStock ? "SHOP_ITEM_AVAILABLE" : "SHOP_STOCK_OUT";
                string fallback = entry.HasStock ? "Disponible" : "Agotado";
                stockText.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get(key, fallback)
                    : fallback;
            }
            else
                stockText.text = "";
        }

        // Chip visual detrás de stockText (rediseño "glass"): solo se muestra si el item
        // tiene stock limitado (igual criterio que el texto de arriba), y cambia de sprite
        // según haya o no stock disponible. Puramente visual, no toca la lógica de compra.
        if (stockChipBackground != null)
        {
            stockChipBackground.gameObject.SetActive(entry.limitedStock);
            if (entry.limitedStock)
            {
                var chipSprite = entry.HasStock ? stockChipSpriteAvailable : stockChipSpriteUnavailable;
                if (chipSprite != null)
                    stockChipBackground.sprite = chipSprite;
            }
        }

        if (button != null)
            button.interactable = entry.HasStock;
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
    
    public Button GetButton() => button;
}
