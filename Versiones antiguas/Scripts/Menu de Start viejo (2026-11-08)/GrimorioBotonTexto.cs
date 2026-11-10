using TMPro;
using UnityEngine;

/// <summary>
/// Texto del botón «Grimorio» de la pestaña Hechizos (INC-506): el nombre y el icono del botón
/// que lo abre en el dispositivo activo (InputGlyphNames.Select: View / Share / − ; M en teclado).
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class GrimorioBotonTexto : MonoBehaviour
{
    private TextMeshProUGUI _text;

    void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
        _text.textWrappingMode = TextWrappingModes.NoWrap; // nombre e icono en la misma línea
    }

    void OnEnable()
    {
        Core.InputGlyphs.InputGlyphService.FamilyChanged += Refresh;
        Refresh(Core.InputGlyphs.InputGlyphService.CurrentFamily);
    }

    void OnDisable() => Core.InputGlyphs.InputGlyphService.FamilyChanged -= Refresh;

    private void Refresh(Core.InputGlyphs.InputGlyphDeviceFamily family)
    {
        string label = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get("GRIMOIRE_BUTTON", "Grimorio")
            : "Grimorio";
        if (_text == null) return;
        Core.InputGlyphs.InputGlyphService.UsarIconos(_text);
        _text.text = $"{label}  {Core.InputGlyphs.InputGlyphService.SpriteTag(Core.InputGlyphs.InputGlyphNames.Select)}";
    }
}
