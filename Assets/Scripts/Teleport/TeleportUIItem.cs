using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Item individual en la lista de destinos de teletransporte.
/// </summary>
public class TeleportUIItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image backgroundImage;
    
    private TeleportPointData _pointData;
    private Action<TeleportPointData> _onClick;
    private bool _isSelected;
    
    public TeleportPointData PointData => _pointData;

    // Ficha de papel: apagada en reposo y dorada con el foco, como las opciones del diálogo.
    void Awake()
    {
        if (backgroundImage != null) PiezasDeUI.VestirFicha(backgroundImage, PaletaUI.PapelApagado);
        if (nameText != null) nameText.color = PaletaUI.TintaDeTexto;
    }
    
    /// <summary>
    /// Configura el item con los datos del punto de teletransporte.
    /// </summary>
    public void Setup(TeleportPointData data, Action<TeleportPointData> onClick)
    {
        _pointData = data;
        _onClick = onClick;
        
        if (nameText != null)
            nameText.text = data.displayName;
        
        SetSelected(false);
    }
    
    /// <summary>
    /// Establece si este item está seleccionado.
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        
        if (backgroundImage != null)
            backgroundImage.color = selected ? PaletaUI.Oro : PaletaUI.PapelApagado;
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        _onClick?.Invoke(_pointData);
    }
}

