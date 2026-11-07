using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Piezas de interfaz montada por código (menús sencillos, marcadores de minijuegos): un lienzo en
/// pantalla, imágenes, textos y cómo colocarlos. Las usan <see cref="ColeccionUI"/> y la vista de
/// los concursos, con los sprites y fuentes de un <see cref="EstiloDeMenuSencillo"/>.
/// </summary>
public static class PiezasDeUI
{
    /// <summary>Convierte el objeto en un lienzo de pantalla escalado a 1920×1080 (con raycaster si hace falta clic).</summary>
    public static Canvas Lienzo(GameObject go, int orden, bool conClic)
    {
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;
        var escalador = go.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;
        if (conClic) go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static Image Imagen(string nombre, Transform padre, Sprite sprite, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var imagen = go.GetComponent<Image>();
        imagen.sprite = sprite;
        imagen.color = color;
        if (sprite != null && sprite.border != Vector4.zero) imagen.type = Image.Type.Sliced;
        return imagen;
    }

    public static TextMeshProUGUI Texto(string nombre, Transform padre, string texto, TMP_FontAsset fuente, float tamano, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Ancla en un punto del padre, con el pivote en el centro.</summary>
    public static void Colocar(RectTransform rt, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }

    /// <summary>Ocupa todo el padre.</summary>
    public static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
