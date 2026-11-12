using System.Collections.Generic;
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

    /// <summary>
    /// Escala los bordes de una imagen troceada para que quepan en <paramref name="alto"/>: un sprite
    /// de 240 px de alto puesto a 64 conserva las esquinas redondas en vez de aplastarlas.
    /// </summary>
    public static void AjustarBordesAlAlto(Image imagen, float alto)
    {
        if (imagen.sprite == null || imagen.type != Image.Type.Sliced || alto <= 0f) return;
        imagen.pixelsPerUnitMultiplier = Mathf.Max(1f, imagen.sprite.rect.height / alto);
    }

    private static Sprite s_rectanguloRedondeado;

    /// <summary>
    /// Rectángulo blanco de esquinas redondas (troceado), para marcos y resaltes que se tiñen de
    /// un solo color sin el degradado de los sprites del menú. Se genera una vez.
    /// </summary>
    public static Sprite RectanguloRedondeado
    {
        get
        {
            if (s_rectanguloRedondeado != null) return s_rectanguloRedondeado;
            const int lado = 72, radio = 30, borde = 32;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                // Distancia al centro de la esquina más cercana (0 en los tramos rectos).
                float dx = Mathf.Max(0f, Mathf.Max(radio - (x + 0.5f), (x + 0.5f) - (lado - radio)));
                float dy = Mathf.Max(0f, Mathf.Max(radio - (y + 0.5f), (y + 0.5f) - (lado - radio)));
                float alfa = Mathf.Clamp01(radio - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_rectanguloRedondeado = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f,
                                                   0, SpriteMeshType.FullRect, new Vector4(borde, borde, borde, borde));
            return s_rectanguloRedondeado;
        }
    }

    private static Sprite s_circulo;

    /// <summary>Círculo blanco relleno con el borde suavizado, para teñir (marcos, aros, máscaras). Se genera una vez.</summary>
    public static Sprite Circulo
    {
        get
        {
            if (s_circulo != null) return s_circulo;
            const int lado = 256;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Circulo" };
            var px = new Color32[lado * lado];
            float mitad = lado * 0.5f;
            for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - mitad) * (x + 0.5f - mitad) + (y + 0.5f - mitad) * (y + 0.5f - mitad));
                float alfa = Mathf.Clamp01(mitad - 1f - d + 0.5f);
                px[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_circulo = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f);
            s_circulo.name = "Circulo";
            return s_circulo;
        }
    }

    private static Sprite s_resplandor;

    /// <summary>
    /// Mancha de luz redonda y blanca que se apaga hacia el borde, para teñir (el halo tras un
    /// personaje, un foco). Se genera una vez.
    /// </summary>
    public static Sprite Resplandor
    {
        get
        {
            if (s_resplandor != null) return s_resplandor;
            const int lado = 128;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Resplandor" };
            var px = new Color32[lado * lado];
            float mitad = lado * 0.5f;
            for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - mitad) * (x + 0.5f - mitad) + (y + 0.5f - mitad) * (y + 0.5f - mitad)) / mitad;
                float alfa = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
                px[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * alfa * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_resplandor = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f);
            return s_resplandor;
        }
    }

    /// <summary>Lo que cuelga la sombra por debajo del papel de <see cref="Papel"/>, en unidades del sprite.</summary>
    public const float SombraDelPapel = 10f;

    private static readonly Dictionary<(Color32, Color32, Color32, int), Sprite> s_papeles
        = new Dictionary<(Color32, Color32, Color32, int), Sprite>();

    /// <summary>
    /// Hoja de papel troceada: esquinas redondas, borde de <paramref name="borde"/>, un filo de
    /// <paramref name="filo"/> por dentro y una sombra dura que cuelga <see cref="SombraDelPapel"/>
    /// por debajo (la imagen que la lleve baja su borde inferior esa medida para que el papel
    /// ocupe justo su hueco). Con <c>pixelsPerUnitMultiplier</c> mayor que 1, esquinas, borde y
    /// sombra se afinan en proporción. Se genera una vez por combinación de colores.
    /// </summary>
    public static Sprite Papel(Color relleno, Color borde, Color filo, float sombra = 0.55f)
    {
        var clave = ((Color32)relleno, (Color32)borde, (Color32)filo, Mathf.RoundToInt(sombra * 100f));
        if (s_papeles.TryGetValue(clave, out var hecho) && hecho != null) return hecho;

        const int ancho = 128, lado = 128, caida = (int)SombraDelPapel, alto = lado + caida, radio = 34;
        const float grosor = 6f, anchoDelFilo = 4f;
        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Papel" };
        var px = new Color32[ancho * alto];
        Color colorDeSombra = new Color(borde.r * 0.45f, borde.g * 0.45f, borde.b * 0.45f, 1f);
        float mitad = lado * 0.5f;
        for (int y = 0; y < alto; y++)
        for (int x = 0; x < ancho; x++)
        {
            float cx = x + 0.5f - mitad;
            float d = DistanciaAlRedondeado(cx, y + 0.5f - (caida + mitad), mitad, radio);
            float dSombra = DistanciaAlRedondeado(cx, y + 0.5f - mitad, mitad, radio);
            float cubre = Mathf.Clamp01(0.5f - d);
            float dentro = Mathf.Clamp01(0.5f - (d + grosor));
            float pasadoElFilo = Mathf.Clamp01(0.5f - (d + grosor + anchoDelFilo));
            Color hoja = Color.Lerp(borde, Color.Lerp(filo, relleno, pasadoElFilo), dentro);
            float aSombra = Mathf.Clamp01(0.5f - dSombra) * sombra * (1f - cubre);
            float alfa = cubre + aSombra;
            Color c = alfa > 0f ? (hoja * cubre + colorDeSombra * aSombra) / alfa : hoja;
            c.a = alfa;
            px[y * ancho + x] = c;
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        const float corte = 52f;
        var sprite = Sprite.Create(tex, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f,
                                   0, SpriteMeshType.FullRect, new Vector4(corte, corte + caida, corte, corte));
        sprite.name = "Papel";
        s_papeles[clave] = sprite;
        return sprite;
    }

    /// Distancia con signo al borde de un cuadrado de esquinas redondas centrado en el origen (negativa dentro).
    private static float DistanciaAlRedondeado(float x, float y, float mitad, float radio)
    {
        float qx = Mathf.Abs(x) - (mitad - radio);
        float qy = Mathf.Abs(y) - (mitad - radio);
        float fuera = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return fuera + Mathf.Min(Mathf.Max(qx, qy), 0f) - radio;
    }

    /// <summary>Viste una imagen como hoja de papel troceada (paneles, ventanas).</summary>
    public static void VestirHoja(Image imagen)
    {
        imagen.sprite = Papel(PaletaUI.Papel, PaletaUI.Tinta, PaletaUI.PapelClaro);
        imagen.type = Image.Type.Sliced;
        imagen.color = Color.white;
    }

    /// <summary>
    /// Viste una imagen como ficha de papel teñible (botones, campos, filas): borde de tinta y el
    /// relleno del color que se le dé; afina el borde en piezas pequeñas.
    /// </summary>
    public static void VestirFicha(Image imagen, Color color)
    {
        imagen.sprite = Papel(Color.white, PaletaUI.Tinta, Color.white, 0.5f);
        imagen.type = Image.Type.Sliced;
        imagen.pixelsPerUnitMultiplier = 1.6f;
        imagen.color = color;
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
