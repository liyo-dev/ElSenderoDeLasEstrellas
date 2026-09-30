using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpellRowWidget : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private Image icon;
    [Tooltip("Separación entre el icono y el texto, en píxeles.")]
    [SerializeField] private float labelGap = 16f;

    Action _onClick;
    Action _onSelected;
    bool _selectionCallbacksEnabled = true;
    ColorBlock _defaultColors;
    bool _colorsInitialized;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (label == null)
            label = GetComponentInChildren<Text>();
        if (icon == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                icon = iconTransform.GetComponent<Image>();
        }
        
        if (button != null && !_colorsInitialized)
        {
            _defaultColors = button.colors;
            _colorsInitialized = true;
        }
        AlignLabel();
    }

    /// El texto empieza justo detrás del icono y va alineado a la izquierda, así todas las filas
    /// quedan en columna, tengan o no icono. Si un nombre no cabe, la letra encoge en vez de cortarse.
    void AlignLabel()
    {
        if (label == null) return;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.resizeTextForBestFit = true;
        label.resizeTextMaxSize = label.fontSize;
        label.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(label.fontSize * 0.6f));

        var iconImage = ResolveIconTarget();
        if (iconImage == null) return;
        var iconRt = iconImage.rectTransform;
        var labelRt = label.rectTransform;
        // Solo con el icono anclado al borde izquierdo de la fila, que es como viene en el prefab.
        if (iconRt.parent != labelRt.parent || iconRt.anchorMin.x != 0f || iconRt.anchorMax.x != 0f) return;
        float iconRight = iconRt.anchoredPosition.x + iconRt.rect.width * (1f - iconRt.pivot.x);
        labelRt.offsetMin = new Vector2(iconRight + labelGap, labelRt.offsetMin.y);
    }

    public void SetLabel(string value)
    {
        if (label == null) return;
        label.supportRichText = true;
        label.text = value;
    }

    public void RegisterClickHandler(Action onClick)
    {
        _onClick = onClick;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    public void RegisterSelectedHandler(Action onSelected)
    {
        _onSelected = onSelected;
    }

    public GameObject ButtonGameObject => button != null ? button.gameObject : gameObject;

    public Selectable Selectable => button != null ? button : GetComponent<Selectable>();

    public void Focus()
    {
        var es = EventSystem.current;
        var target = ButtonGameObject;
        if (es == null || target == null)
            return;

        // Evitar recursión: solo llamar a SetSelectedGameObject si no está ya seleccionado
        if (es.currentSelectedGameObject != target)
        {
            es.SetSelectedGameObject(target);
        }
    }

    public void SetSelectionCallbacksEnabled(bool enabled)
    {
        _selectionCallbacksEnabled = enabled;
    }

    public void SetIcon(Sprite sprite)
    {
        var target = ResolveIconTarget();
        if (target == null) return;

        if (sprite == null)
        {
            target.enabled = false;
            target.sprite = null;
            target.gameObject.SetActive(false);
        }
        else
        {
            target.sprite = sprite;
            target.color = Color.white;
            target.enabled = true;
            target.gameObject.SetActive(true);
        }
    }

    Image ResolveIconTarget()
    {
        if (icon != null) return icon;

        if (button != null)
        {
            var graphics = button.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                var candidate = graphics[i];
                if (candidate == null) continue;
                if (candidate == button.targetGraphic) continue;
                icon = candidate;
                break;
            }

            if (icon == null && button.targetGraphic is Image buttonImage)
                icon = buttonImage;
        }

        if (icon == null)
            icon = GetComponentInChildren<Image>(true);

        return icon;
    }

    public void SetHighlighted(bool highlighted, Color highlightColor)
    {
        if (button == null) return;
        
        if (!_colorsInitialized)
        {
            _defaultColors = button.colors;
            _colorsInitialized = true;
        }

        if (highlighted)
        {
            var colors = _defaultColors;
            colors.normalColor = highlightColor;
            colors.highlightedColor = highlightColor;
            colors.selectedColor = highlightColor;
            button.colors = colors;
        }
        else
        {
            button.colors = _defaultColors;
        }
    }

    void HandleClick()
    {
        _onSelected?.Invoke();
        _onClick?.Invoke();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_selectionCallbacksEnabled) return;
        _onSelected?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_selectionCallbacksEnabled) return;
        _onSelected?.Invoke();
    }
}
