using UnityEngine;

/// <summary>
/// Mantiene un hueco con las medidas de una maqueta de 1920 de ancho cubriendo justo el lienzo
/// raíz (ver <see cref="PiezasDeUI.LienzoDeMaqueta"/>): si el lienzo cambia de tamaño (el
/// escalador aún no se había aplicado al montarlo, otra resolución), se recoloca y se reescala.
/// </summary>
[DisallowMultipleComponent]
public sealed class EscalaDeMaqueta : MonoBehaviour
{
    private RectTransform _propio, _dentro, _raiz;
    private Vector2 _tamanoVisto;
    private readonly Vector3[] _esquinas = new Vector3[4];

    public void Iniciar(RectTransform dentro)
    {
        _propio = (RectTransform)transform;
        _dentro = dentro;
        _raiz = (RectTransform)dentro.GetComponentInParent<Canvas>(true).rootCanvas.transform;
        Ajustar();
    }

    private void LateUpdate()
    {
        if (_raiz != null && _raiz.rect.size != _tamanoVisto) Ajustar();
    }

    private void Ajustar()
    {
        _tamanoVisto = _raiz.rect.size;
        _raiz.GetWorldCorners(_esquinas);
        Vector2 abajo = _dentro.InverseTransformPoint(_esquinas[0]), arriba = _dentro.InverseTransformPoint(_esquinas[2]);
        _propio.anchorMin = _propio.anchorMax = _dentro.pivot;
        _propio.pivot = Vector2.zero;
        _propio.anchoredPosition = abajo;
        float k = (arriba.x - abajo.x) / 1920f;
        _propio.sizeDelta = new Vector2(1920f, (arriba.y - abajo.y) / Mathf.Max(0.0001f, k));
        _propio.localScale = new Vector3(k, k, 1f);
    }
}
