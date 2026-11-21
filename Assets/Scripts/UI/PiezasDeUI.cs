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
    /// un solo color sin el degradado de los sprites del menú. Lleva un margen transparente
    /// alrededor: así el borde del dibujo cae dentro de la textura y sale suavizado también cuando
    /// la pieza está girada (si el dibujo llegara al filo, el borde sería el del polígono, con
    /// escalones). Se genera una vez.
    /// </summary>
    public static Sprite RectanguloRedondeado
    {
        get
        {
            if (s_rectanguloRedondeado != null) return s_rectanguloRedondeado;
            const int margen = 2, dibujo = 72, lado = dibujo + 2 * margen, radio = 30, borde = 32 + margen;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                // Distancia al centro de la esquina más cercana (0 en los tramos rectos), dentro del margen.
                float fx = x + 0.5f - margen, fy = y + 0.5f - margen;
                float dx = Mathf.Max(0f, Mathf.Max(radio - fx, fx - (dibujo - radio)));
                float dy = Mathf.Max(0f, Mathf.Max(radio - fy, fy - (dibujo - radio)));
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

    // ─── Papel con borde, filo y sombra dura (cartas, fichas y botones de los menús) ───────────

    /// <summary>Sombra dura que cae bajo el papel de los menús.</summary>
    public static readonly Color SombraDeCarta = new Color(12f / 255f, 8f / 255f, 28f / 255f, 0.7f);
    /// <summary>Tinta casi negra de los bordes de las piezas pequeñas (insignias, casillas, pistas).</summary>
    public static readonly Color TintaOscura = new Color(42f / 255f, 32f / 255f, 24f / 255f, 1f);

    /// <summary>Partes de una carta de papel: la raíz (para colocarla y girarla) y lo que se tiñe.</summary>
    public sealed class Carta
    {
        public RectTransform raiz;
        public RectTransform sombra;
        public Image borde;
        public Image filo;
        public Image relleno;
        /// Donde va el contenido (ocupa toda la carta).
        public RectTransform contenido;

        public void Pintar(Color color) { if (relleno != null) relleno.color = color; }

        /// Cuánto cae la sombra por debajo (en unidades del lienzo).
        public void Caida(float alto)
        {
            if (sombra == null) return;
            sombra.offsetMin = new Vector2(0f, -alto);
            sombra.offsetMax = new Vector2(0f, -alto);
        }
    }

    /// <summary>
    /// Carta de papel como las de las maquetas: esquinas de <paramref name="radio"/>, borde de
    /// <paramref name="grosor"/> en <paramref name="colorBorde"/> (tinta si no se da), un filo claro
    /// del mismo grosor por dentro (si <paramref name="conFilo"/>) y sombra dura que cae
    /// <paramref name="caida"/> unidades. La raíz no tiene imagen: se coloca y se gira como un hueco.
    /// </summary>
    public static Carta NuevaCarta(string nombre, Transform padre, Color relleno, float radio,
        float caida = 7f, Color? colorBorde = null, bool conFilo = true, float grosor = 3f)
    {
        var raiz = new GameObject(nombre, typeof(RectTransform)).transform as RectTransform;
        raiz.SetParent(padre, false);
        var c = new Carta { raiz = raiz };
        if (caida > 0f)
        {
            var sombra = Redondeado("Sombra", raiz, SombraDeCarta, radio);
            c.sombra = sombra.rectTransform;
            Estirar(c.sombra);
            c.Caida(caida);
        }
        c.borde = Redondeado("Borde", raiz, colorBorde ?? PaletaUI.Tinta, radio);
        Estirar(c.borde.rectTransform);
        float dentro = grosor;
        if (conFilo)
        {
            c.filo = Redondeado("Filo", raiz, PaletaUI.PapelClaro, radio - grosor);
            Margen(c.filo.rectTransform, dentro);
            dentro += grosor;
        }
        c.relleno = Redondeado("Relleno", raiz, relleno, radio - dentro);
        Margen(c.relleno.rectTransform, dentro);
        c.contenido = new GameObject("Contenido", typeof(RectTransform)).transform as RectTransform;
        c.contenido.SetParent(raiz, false);
        Estirar(c.contenido);
        return c;
    }

    /// <summary>Rectángulo de esquinas de <paramref name="radio"/> unidades, del color dado.</summary>
    public static Image Redondeado(string nombre, Transform padre, Color color, float radio)
    {
        var img = Imagen(nombre, padre, RectanguloRedondeado, color);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 30f / Mathf.Max(0.5f, radio);
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Ocupa el padre dejando <paramref name="margen"/> por cada lado.</summary>
    public static void Margen(RectTransform rt, float margen)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margen, margen);
        rt.offsetMax = new Vector2(-margen, -margen);
    }

    /// <summary>
    /// Coloca como en una maqueta: (<paramref name="x"/>, <paramref name="y"/>) es la esquina de
    /// arriba a la izquierda dentro del padre, medidas en unidades del lienzo de referencia, y el
    /// giro es alrededor del centro.
    /// </summary>
    public static void ColocarArribaIzquierda(RectTransform rt, float x, float y, float ancho, float alto, float giro = 0f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ancho, alto);
        rt.anchoredPosition = new Vector2(x + ancho * 0.5f, -(y + alto * 0.5f));
        rt.localRotation = Quaternion.Euler(0f, 0f, -giro);
    }

    private static readonly Dictionary<(int, int, int, bool), Sprite> s_velos = new Dictionary<(int, int, int, bool), Sprite>();

    /// <summary>
    /// Velo de degradado del color dado: opacidad <paramref name="a0"/> en el lado de partida
    /// (izquierda, o abajo si <paramref name="vertical"/>), <paramref name="aMedio"/> en
    /// <paramref name="corte"/> (fracción del largo) y transparente al final, en dos tramos rectos
    /// como los degradados de las maquetas.
    /// </summary>
    public static Image Velo(string nombre, Transform padre, Color color, float a0, float aMedio, float corte, bool vertical)
    {
        var clave = (Mathf.RoundToInt(a0 * 100f), Mathf.RoundToInt(aMedio * 100f), Mathf.RoundToInt(corte * 100f), vertical);
        if (!s_velos.TryGetValue(clave, out var sprite) || sprite == null)
        {
            const int n = 256;
            var tex = new Texture2D(vertical ? 1 : n, vertical ? n : 1, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Velo" };
            var px = new Color32[n];
            for (int k = 0; k < n; k++)
            {
                float t = (k + 0.5f) / n;
                float a = t < corte ? Mathf.Lerp(a0, aMedio, t / corte) : Mathf.Lerp(aMedio, 0f, (t - corte) / (1f - corte));
                px[k] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s_velos[clave] = sprite;
        }
        var img = Imagen(nombre, padre, sprite, new Color(color.r, color.g, color.b, 1f));
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    private static Sprite s_estrella, s_estrellaLisa;

    /// <summary>
    /// Estrella de cuatro puntas con las caras curvas hacia dentro (la de las maquetas), rellena de
    /// blanco con un contorno oscuro fino: se tiñe el relleno con el color de la imagen y el
    /// contorno queda oscuro. Se genera una vez.
    /// </summary>
    public static Sprite Estrella => s_estrella != null ? s_estrella : (s_estrella = CrearEstrella(true));

    /// <summary>La misma estrella sin contorno (las del cielo de fondo), para teñir.</summary>
    public static Sprite EstrellaLisa => s_estrellaLisa != null ? s_estrellaLisa : (s_estrellaLisa = CrearEstrella(false));

    private static Sprite CrearEstrella(bool conContorno)
    {
        const int lado = 128;
        // Contorno de la maqueta (viewBox −1.2…1.2): curvas cúbicas entre las cuatro puntas.
        var puntos = new List<Vector2>();
        Vector2[] p =
        {
            new(0f, -1f), new(0.12f, -0.2f), new(0.2f, -0.12f), new(1f, 0f),
            new(0.2f, 0.12f), new(0.12f, 0.2f), new(0f, 1f),
            new(-0.12f, 0.2f), new(-0.2f, 0.12f), new(-1f, 0f),
            new(-0.2f, -0.12f), new(-0.12f, -0.2f), new(0f, -1f),
        };
        for (int seg = 0; seg < 4; seg++)
        {
            Vector2 a = p[seg * 3], b = p[seg * 3 + 1], c = p[seg * 3 + 2], d = p[seg * 3 + 3];
            for (int i = 0; i < 24; i++)
            {
                float t = i / 24f, u = 1f - t;
                puntos.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
            }
        }
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Estrella" };
        var px = new Color32[lado * lado];
        float escala = 2.4f / lado, grosor = conContorno ? 0.09f : 0f;
        Color oscuro = TintaOscura;
        for (int y = 0; y < lado; y++)
        for (int x = 0; x < lado; x++)
        {
            var q = new Vector2((x + 0.5f) * escala - 1.2f, 1.2f - (y + 0.5f) * escala);
            float d = DistanciaAlContorno(q, puntos);
            bool dentro = DentroDe(q, puntos);
            float sd = dentro ? -d : d; // negativa dentro
            float cubre = Mathf.Clamp01((grosor * 0.5f - sd) / escala + 0.5f);
            float relleno = Mathf.Clamp01((-grosor * 0.5f - sd) / escala + 0.5f);
            Color c = Color.Lerp(oscuro, Color.white, relleno);
            c.a = cubre;
            px[y * lado + x] = c;
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "Estrella";
        return sprite;
    }

    private static float DistanciaAlContorno(Vector2 q, List<Vector2> poli)
    {
        float mejor = float.MaxValue;
        for (int i = 0; i < poli.Count; i++)
        {
            Vector2 a = poli[i], b = poli[(i + 1) % poli.Count], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            mejor = Mathf.Min(mejor, (a + ab * t - q).sqrMagnitude);
        }
        return Mathf.Sqrt(mejor);
    }

    private static bool DentroDe(Vector2 q, List<Vector2> poli)
    {
        bool dentro = false;
        for (int i = 0, j = poli.Count - 1; i < poli.Count; j = i++)
            if ((poli[i].y > q.y) != (poli[j].y > q.y) &&
                q.x < (poli[j].x - poli[i].x) * (q.y - poli[i].y) / (poli[j].y - poli[i].y) + poli[i].x)
                dentro = !dentro;
        return dentro;
    }

    /// <summary>Estrella de las maquetas de <paramref name="lado"/> unidades centrada en (cx, cy) desde arriba a la izquierda del padre.</summary>
    public static Image EstrellaEn(string nombre, Transform padre, float cx, float cy, float lado, float giro = 0f, Color? color = null)
    {
        var img = Imagen(nombre, padre, Estrella, color ?? PaletaUI.Oro);
        img.raycastTarget = false;
        ColocarArribaIzquierda(img.rectTransform, cx - lado * 0.5f, cy - lado * 0.5f, lado, lado, giro);
        return img;
    }

    /// <summary>
    /// Icono del botón <paramref name="glifo"/> (<see cref="Core.InputGlyphs.InputGlyphNames"/>) del mando
    /// o teclado activo, de <paramref name="lado"/> unidades; cambia solo al tocar otro dispositivo.
    /// </summary>
    public static RectTransform IconoDeBoton(string nombre, Transform padre, string glifo, float lado)
    {
        var t = Texto(nombre, padre, Core.InputGlyphs.InputGlyphService.SpriteTag(glifo), null, lado * 0.82f, Color.white);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.gameObject.AddComponent<IconoDeBotonVivo>();
        var rt = t.rectTransform;
        rt.sizeDelta = new Vector2(lado, lado);
        return rt;
    }

    private static Sprite s_punteado;

    /// <summary>Raya de puntos para repetir en mosaico (subrayados de enlaces): 3 unidades de punto, 3 de hueco.</summary>
    public static Sprite Punteado
    {
        get
        {
            if (s_punteado != null) return s_punteado;
            var tex = new Texture2D(6, 3, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Punteado" };
            var px = new Color32[18];
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 6; x++)
                px[y * 6 + x] = x < 3 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_punteado = Sprite.Create(tex, new Rect(0, 0, 6, 3), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            return s_punteado;
        }
    }

    private static Sprite s_globo;

    /// <summary>Globo terráqueo de las maquetas (azul claro, ecuador y dos meridianos, contorno oscuro), para el idioma.</summary>
    public static Sprite Globo
    {
        get
        {
            if (s_globo != null) return s_globo;
            const int lado = 128;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Globo" };
            var px = new Color32[lado * lado];
            float k = 64f / lado; // unidades del viewBox de 64 por píxel
            Color azul = new Color32(0x9E, 0xD0, 0xFF, 0xFF), tinta = TintaOscura;
            for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float u = (x + 0.5f) * k - 32f, v = 32f - (y + 0.5f) * k;
                float r = Mathf.Sqrt(u * u + v * v);
                float cubre = Mathf.Clamp01((24f + 1.6f - r) / k + 0.5f);
                // Líneas: contorno (r = 24), ecuador (v = 0) y dos meridianos (elipses de semieje 9).
                float dLinea = Mathf.Abs(r - 24f) - 1.6f;
                if (r < 24f)
                {
                    dLinea = Mathf.Min(dLinea, Mathf.Abs(v) - 1.3f);
                    float h = Mathf.Sqrt(Mathf.Max(0f, 576f - v * v)) / 24f;
                    dLinea = Mathf.Min(dLinea, Mathf.Abs(Mathf.Abs(u) - 9f * h) - 1.3f);
                }
                float linea = Mathf.Clamp01(-dLinea / k + 0.5f);
                Color c = Color.Lerp(azul, tinta, linea);
                c.a = cubre;
                px[y * lado + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_globo = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f);
            s_globo.name = "Globo";
            return s_globo;
        }
    }

    /// <summary>
    /// Hueco con las medidas de una maqueta de 1920 de ancho que cubre justo el lienzo raíz,
    /// metido dentro de <paramref name="dentro"/> (se enciende y se apaga con él). Dentro se coloca
    /// con <see cref="ColocarArribaIzquierda"/> y las medidas de la maqueta.
    /// </summary>
    public static RectTransform LienzoDeMaqueta(RectTransform dentro, string nombre = "Maqueta")
    {
        var rt = new GameObject(nombre, typeof(RectTransform)).transform as RectTransform;
        rt.SetParent(dentro, false);
        rt.gameObject.AddComponent<EscalaDeMaqueta>().Iniciar(dentro);
        return rt;
    }

    private static Sprite s_cieloRadial;

    // Estrellas del cielo de los menús de noche: x, y, lado, opacidad, giro y color (0 lila, 1 crema, 2 oro).
    private static readonly float[] s_estrellasDelCielo =
    {
        683,318,18,.25f,4,0, 1701,202,14,.25f,32,0, 459,86,10,.4f,4,1, 512,195,22,.25f,36,1, 273,467,22,.6f,37,2,
        832,111,12,.6f,8,2, 613,868,12,.25f,36,0, 651,380,10,.6f,40,0, 404,772,10,.6f,4,0, 1175,132,22,.4f,34,2,
        895,653,18,.4f,23,0, 633,518,12,.25f,5,0, 1196,624,22,.4f,28,1, 609,159,10,.4f,10,0, 1570,710,12,.4f,2,1,
        1388,168,22,.4f,21,0, 1443,727,22,.6f,29,1, 160,201,14,.6f,4,1, 144,644,22,.4f,18,0, 1487,800,14,.4f,22,2,
        364,249,18,.25f,18,2, 284,517,18,.4f,5,1, 360,929,18,.4f,8,0, 1697,891,22,.6f,26,1, 754,789,12,.25f,11,2,
        329,485,12,.4f,37,2, 393,548,14,.25f,26,2, 1114,766,22,.4f,8,0, 1434,120,18,.6f,25,0, 835,827,18,.4f,40,2,
        840,137,12,.25f,28,2, 352,235,14,.25f,6,0, 20,319,22,.4f,39,2, 72,154,12,.4f,9,0,
    };

    // Vetas de nube: x, y, ancho, alto, color (hex), opacidad, giro.
    private static readonly (float x, float y, float w, float h, uint color, float a, float giro)[] s_vetas =
    {
        (-120f, 760f, 900f, 120f, 0x4B3B86, 0.35f, -8f), (1150f, 90f, 900f, 110f, 0x4B3B86, 0.3f, 6f),
        (300f, 930f, 1300f, 90f, 0x34285E, 0.55f, 2f), (-80f, 180f, 700f, 90f, 0x34285E, 0.5f, 10f),
        (1300f, 700f, 800f, 120f, 0x3A2E69, 0.45f, -10f),
    };

    private static Color Hex(uint rgb, float a = 1f) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

    /// <summary>
    /// Fondo de noche de los menús a pantalla completa (maquetas del menú de Start y del idioma):
    /// degradado radial morado, vetas de nube y estrellitas. Ocupa 1920×1080 desde arriba a la
    /// izquierda de <paramref name="padre"/> (un <see cref="LienzoDeMaqueta"/>) y tapa lo de detrás.
    /// </summary>
    public static RectTransform FondoDeNoche(Transform padre)
    {
        var raiz = new GameObject("FondoDeNoche", typeof(RectTransform)).transform as RectTransform;
        raiz.SetParent(padre, false);
        ColocarArribaIzquierda(raiz, 0f, 0f, 1920f, 1080f);

        if (s_cieloRadial == null)
        {
            // Elipse de 1100×760 centrada en (50 %, 46 %): #3F3270 → #2B2344 (48 %) → #1B1530.
            const int ancho = 192, alto = 108;
            var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CieloRadial" };
            var px = new Color[ancho * alto];
            Color c0 = Hex(0x3F3270), c1 = Hex(0x2B2344), c2 = Hex(0x1B1530);
            for (int y = 0; y < alto; y++)
            for (int x = 0; x < ancho; x++)
            {
                float dx = ((x + 0.5f) * 10f - 960f) / 1100f, dy = (1080f - (y + 0.5f) * 10f - 1080f * 0.46f) / 760f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * ancho + x] = d < 0.48f ? Color.Lerp(c0, c1, d / 0.48f) : Color.Lerp(c1, c2, Mathf.Clamp01((d - 0.48f) / 0.52f));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            s_cieloRadial = Sprite.Create(tex, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f);
        }
        var cielo = Imagen("Cielo", raiz, s_cieloRadial, Color.white);
        cielo.raycastTarget = true;
        Estirar(cielo.rectTransform);

        foreach (var v in s_vetas)
        {
            var veta = Redondeado("Veta", raiz, Hex(v.color, v.a), Mathf.Min(60f, v.h * 0.5f));
            ColocarArribaIzquierda(veta.rectTransform, v.x, v.y, v.w, v.h, v.giro);
        }
        Color[] colores = { Hex(0xCDBEFF), Hex(0xEEE6D8), PaletaUI.Oro };
        for (int i = 0; i < s_estrellasDelCielo.Length; i += 6)
        {
            float lado = s_estrellasDelCielo[i + 2];
            var c = colores[(int)s_estrellasDelCielo[i + 5]];
            c.a = s_estrellasDelCielo[i + 3];
            var e = Imagen("Estrellita", raiz, EstrellaLisa, c);
            e.raycastTarget = false;
            ColocarArribaIzquierda(e.rectTransform, s_estrellasDelCielo[i], s_estrellasDelCielo[i + 1], lado, lado, s_estrellasDelCielo[i + 4]);
        }
        return raiz;
    }

    /// <summary>
    /// Coloca una imagen cuyo sprite tiene márgenes transparentes de modo que su parte visible
    /// (<paramref name="visible"/>, en fracciones del sprite, desde abajo a la izquierda) ocupe
    /// <paramref name="ancho"/> desde (<paramref name="x"/>, <paramref name="y"/>), como en una maqueta
    /// hecha con la imagen recortada.
    /// </summary>
    public static void ColocarRecortado(Image imagen, Rect visible, float x, float y, float ancho)
    {
        var s = imagen.sprite;
        if (s == null) return;
        if (visible.width <= 0f || visible.height <= 0f) visible = new Rect(0f, 0f, 1f, 1f);
        imagen.preserveAspect = true;
        float anchoTotal = ancho / visible.width;
        float altoTotal = anchoTotal * s.rect.height / s.rect.width;
        ColocarArribaIzquierda(imagen.rectTransform, x - visible.x * anchoTotal, y - (1f - visible.yMax) * altoTotal, anchoTotal, altoTotal);
    }
}
