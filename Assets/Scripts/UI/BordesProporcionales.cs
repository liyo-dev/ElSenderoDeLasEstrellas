using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escala los bordes de un Image Sliced con el alto del rect, como si el sprite entero se
/// encogiera hasta ese alto. Así las puntas redondeadas de una píldora conservan su forma en
/// botones y chips más bajos que el sprite, en vez de quedarse a tamaño nativo y verse estiradas.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class BordesProporcionales : MonoBehaviour
{
    private Image _imagen;

    private void OnEnable() => Ajustar();

    private void OnRectTransformDimensionsChange() => Ajustar();

#if UNITY_EDITOR
    private void OnValidate() => Ajustar();
#endif

    private void Ajustar()
    {
        if (_imagen == null) _imagen = GetComponent<Image>();
        if (_imagen == null || _imagen.sprite == null) return;
        if (_imagen.type != Image.Type.Sliced && _imagen.type != Image.Type.Tiled) return;
        float alto = ((RectTransform)transform).rect.height;
        if (alto <= 0.5f) return;
        float referencia = _imagen.canvas != null ? _imagen.canvas.referencePixelsPerUnit : 100f;
        float multiplicador = _imagen.sprite.rect.height * referencia / (_imagen.sprite.pixelsPerUnit * alto);
        multiplicador = Mathf.Max(0.01f, multiplicador);
        if (Mathf.Abs(_imagen.pixelsPerUnitMultiplier - multiplicador) < 0.001f) return;
        _imagen.pixelsPerUnitMultiplier = multiplicador;
    }
}
