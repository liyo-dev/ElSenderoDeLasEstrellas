using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lo que se ve al entrar por primera vez en un lugar con nombre: una cinta de papel que se
/// despliega arriba en el centro, una pestaña de noche con el encabezado («Nuevo lugar»), una
/// estrella de oro con dos trazos de tinta que se abren hacia los lados, el nombre que va
/// apareciendo letra a letra y unas chispas de oro alrededor. Después se queda quieta un momento
/// y se va.
///
/// Solo dibuja. Todo lo que enseña es función del tiempo que lleva a la vista
/// (<see cref="Evaluar"/>): quien la usa decide cuándo corre ese tiempo, así que pararla en mitad
/// (un menú, una cinemática) y seguir después no necesita estado aparte. La monta
/// <see cref="DramaticTextOverlayUI"/>, el servicio único de texto en pantalla, sobre un lienzo
/// propio; la usa <see cref="PresentacionDeLugares"/>.
/// </summary>
public sealed class PresentacionDeLugarUI
{
    // Tiempos, en segundos desde que empieza.
    const float FundidoDeEntrada = 0.2f;
    const float Despliegue = 0.45f;
    const float InicioDeLaPestana = 0.25f;
    const float DuracionDeLaPestana = 0.3f;
    const float InicioDeLaEstrella = 0.2f;
    const float DuracionDeLaEstrella = 0.4f;
    const float InicioDeLosTrazos = 0.35f;
    const float DuracionDeLosTrazos = 0.6f;
    const float InicioDeLasLetras = 0.42f;
    const float EntreLetras = 0.055f;
    const float VueloDeLetra = 0.38f;
    const float Quieta = 2.6f;
    const float QuietaBreve = 1.1f;
    const float Salida = 0.5f;
    const float VidaDeUnaChispa = 0.8f;

    // Medidas, en el lienzo de 1920×1080.
    const float AltoDeLaCinta = 124f;
    const float MargenDeLaCinta = 130f;
    const float AnchoMinimo = 560f;
    const float AnchoMaximo = 1500f;
    const float TamanoDelNombre = 64f;
    const float TamanoDelEncabezado = 22f;
    const float AltoDeLaPestana = 46f;
    const float AlturaDeLosTrazos = -38f;
    const float HuecoDeLaEstrella = 30f;
    const float LadoDeLaEstrella = 40f;
    const float Inclinacion = -1f;
    const float InclinacionDeLaPestana = 2f;
    static readonly Vector2 Ancla = new Vector2(0.5f, 0.78f);

    // Dónde salen las chispas (proporción del ancho y el alto de la cinta), cuándo y de qué tamaño.
    static readonly Vector2[] SitiosDeLasChispas =
    {
        new Vector2(-0.47f, 0.62f), new Vector2(0.44f, 0.66f), new Vector2(-0.53f, -0.38f),
        new Vector2(0.51f, -0.46f), new Vector2(-0.18f, 0.78f), new Vector2(0.24f, -0.7f),
        new Vector2(0.08f, 0.74f),
    };
    static readonly float[] MomentosDeLasChispas = { 0.7f, 0.95f, 1.2f, 1.5f, 1.85f, 2.2f, 2.6f };
    static readonly float[] TamanosDeLasChispas = { 34f, 26f, 22f, 30f, 18f, 24f, 20f };

    readonly GameObject _lienzo;
    readonly RectTransform _conjunto;
    readonly CanvasGroup _grupo;
    readonly RectTransform _cinta;
    readonly RectTransform _pestana;
    readonly TextMeshProUGUI _encabezado;
    readonly TextMeshProUGUI _nombre;
    readonly RectTransform _estrella;
    readonly RectTransform _trazoIzquierdo;
    readonly RectTransform _trazoDerecho;
    readonly Image[] _chispas;

    TMP_MeshInfo[] _verticesDelNombre;
    float _largoDeLosTrazos;

    /// <summary>Segundos que dura entera con el nombre de <see cref="Preparar"/>.</summary>
    public float Duracion { get; private set; }

    /// <param name="lienzo">Lienzo de pantalla completa, solo para esta presentación.</param>
    /// <param name="fuente">Letra del nombre y del encabezado (null = la de TMP por defecto).</param>
    public PresentacionDeLugarUI(GameObject lienzo, TMP_FontAsset fuente)
    {
        _lienzo = lienzo;

        var go = new GameObject("Conjunto", typeof(RectTransform));
        _conjunto = (RectTransform)go.transform;
        _conjunto.SetParent(lienzo.transform, false);
        _conjunto.localRotation = Quaternion.Euler(0f, 0f, Inclinacion);
        _grupo = go.AddComponent<CanvasGroup>();
        _grupo.blocksRaycasts = false;
        _grupo.interactable = false;

        var hoja = PiezasDeUI.Imagen("Cinta", _conjunto, PiezasDeUI.Papel(PaletaUI.Papel, PaletaUI.Tinta, PaletaUI.PapelClaro), Color.white);
        hoja.raycastTarget = false;
        _cinta = hoja.rectTransform;
        PiezasDeUI.Estirar(_cinta);
        _cinta.offsetMin = new Vector2(0f, -PiezasDeUI.SombraDelPapel);

        _trazoIzquierdo = Trazo("TrazoIzquierdo", new Vector2(1f, 0.5f), -HuecoDeLaEstrella);
        _trazoDerecho = Trazo("TrazoDerecho", new Vector2(0f, 0.5f), HuecoDeLaEstrella);

        var estrella = PiezasDeUI.Imagen("Estrella", _conjunto, Estrella, Color.white);
        estrella.raycastTarget = false;
        _estrella = estrella.rectTransform;
        PiezasDeUI.Colocar(_estrella, new Vector2(0.5f, 0.5f), new Vector2(0f, AlturaDeLosTrazos), new Vector2(LadoDeLaEstrella, LadoDeLaEstrella));

        _nombre = PiezasDeUI.Texto("Nombre", _conjunto, "", fuente, TamanoDelNombre, PaletaUI.TintaDeTexto);
        _nombre.textWrappingMode = TextWrappingModes.NoWrap;
        _nombre.overflowMode = TextOverflowModes.Overflow;
        _nombre.characterSpacing = 3f;
        PiezasDeUI.Colocar(_nombre.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(AnchoMinimo, AltoDeLaCinta));

        var pestana = new GameObject("Pestana", typeof(RectTransform));
        _pestana = (RectTransform)pestana.transform;
        _pestana.SetParent(_conjunto, false);
        PiezasDeUI.Colocar(_pestana, new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(220f, AltoDeLaPestana));
        _pestana.localRotation = Quaternion.Euler(0f, 0f, InclinacionDeLaPestana);
        var fondoDeLaPestana = PiezasDeUI.Imagen("Fondo", _pestana, PiezasDeUI.Papel(PaletaUI.Noche, PaletaUI.Tinta, PaletaUI.Noche), Color.white);
        fondoDeLaPestana.raycastTarget = false;
        PiezasDeUI.Estirar(fondoDeLaPestana.rectTransform);
        PiezasDeUI.AjustarBordesAlAlto(fondoDeLaPestana, AltoDeLaPestana);
        fondoDeLaPestana.rectTransform.offsetMin = new Vector2(0f, -PiezasDeUI.SombraDelPapel / Mathf.Max(1f, fondoDeLaPestana.pixelsPerUnitMultiplier));
        _encabezado = PiezasDeUI.Texto("Encabezado", _pestana, "", fuente, TamanoDelEncabezado, PaletaUI.Oro);
        _encabezado.textWrappingMode = TextWrappingModes.NoWrap;
        _encabezado.overflowMode = TextOverflowModes.Overflow;
        _encabezado.characterSpacing = 8f;
        _encabezado.fontStyle = FontStyles.UpperCase;
        PiezasDeUI.Estirar(_encabezado.rectTransform);

        _chispas = new Image[SitiosDeLasChispas.Length];
        for (int i = 0; i < _chispas.Length; i++)
        {
            var chispa = PiezasDeUI.Imagen("Chispa", _conjunto, PiezasDeUI.Resplandor, PaletaUI.Oro);
            chispa.raycastTarget = false;
            PiezasDeUI.Colocar(chispa.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TamanosDeLasChispas[i], TamanosDeLasChispas[i]));
            _chispas[i] = chispa;
        }

        PiezasDeUI.Colocar(_conjunto, Ancla, Vector2.zero, new Vector2(AnchoMinimo, AltoDeLaCinta));
        _lienzo.SetActive(false);
    }

    /// <summary>Pone el nombre y el encabezado (vacío = sin pestaña) y la deja al principio, sin enseñarla.
    /// <paramref name="breve"/>: se queda menos rato (para un lugar que ya se conoce).</summary>
    public void Preparar(string nombre, string encabezado, bool breve = false)
    {
        nombre ??= "";
        _nombre.text = nombre;
        float ancho = Mathf.Clamp(_nombre.GetPreferredValues(nombre, 10000f, AltoDeLaCinta).x + MargenDeLaCinta * 2f, AnchoMinimo, AnchoMaximo);
        _conjunto.sizeDelta = new Vector2(ancho, AltoDeLaCinta);
        _nombre.rectTransform.sizeDelta = new Vector2(ancho - MargenDeLaCinta, AltoDeLaCinta);
        _largoDeLosTrazos = ancho * 0.5f - HuecoDeLaEstrella - MargenDeLaCinta * 0.6f;

        bool conEncabezado = !string.IsNullOrEmpty(encabezado);
        _pestana.gameObject.SetActive(conEncabezado);
        if (conEncabezado)
        {
            _encabezado.text = encabezado;
            float anchoDeLaPestana = _encabezado.GetPreferredValues(encabezado, 10000f, AltoDeLaPestana).x + 56f;
            _pestana.sizeDelta = new Vector2(Mathf.Max(180f, anchoDeLaPestana), AltoDeLaPestana);
        }

        for (int i = 0; i < _chispas.Length; i++)
            _chispas[i].rectTransform.anchoredPosition = Vector2.Scale(SitiosDeLasChispas[i], new Vector2(ancho, AltoDeLaCinta));

        // La malla del nombre recién hecha es la referencia para mover cada letra.
        _nombre.ForceMeshUpdate(true, true);
        _verticesDelNombre = _nombre.textInfo.CopyMeshInfoVertexData();

        int letras = Mathf.Max(1, _nombre.textInfo.characterCount);
        Duracion = InicioDeLasLetras + (letras - 1) * EntreLetras + VueloDeLetra + (breve ? QuietaBreve : Quieta) + Salida;
        Evaluar(0f);
    }

    /// <summary>Enseña o esconde el lienzo sin tocar el punto en el que va.</summary>
    public void Ver(bool ver)
    {
        if (_lienzo.activeSelf != ver) _lienzo.SetActive(ver);
    }

    /// <summary>Deja todo como se ve a los <paramref name="t"/> segundos de empezar.</summary>
    public void Evaluar(float t)
    {
        float desdeLaSalida = Duracion - Salida;
        float salida = Mathf.Clamp01((t - desdeLaSalida) / Salida);
        float despliegue = Mathf.Clamp01(t / Despliegue);

        _grupo.alpha = Mathf.Min(Suave(t / FundidoDeEntrada), 1f - Suave(salida));
        _conjunto.anchoredPosition = new Vector2(0f, -14f * (1f - Suave(despliegue)) + 12f * Suave(salida));
        _cinta.localScale = new Vector3(Mathf.Max(0.001f, ConRebote(despliegue) * (1f - 0.08f * salida)), 1f, 1f);

        float pestana = Mathf.Clamp01((t - InicioDeLaPestana) / DuracionDeLaPestana);
        _pestana.localScale = Vector3.one * Mathf.Max(0f, ConRebote(pestana));

        float estrella = Mathf.Clamp01((t - InicioDeLaEstrella) / DuracionDeLaEstrella);
        _estrella.localScale = Vector3.one * Mathf.Max(0f, ConRebote(estrella));
        _estrella.localRotation = Quaternion.Euler(0f, 0f, -150f * (1f - Frenada(estrella)) + 6f * Mathf.Sin(t * 2.2f));

        float trazos = Frenada(Mathf.Clamp01((t - InicioDeLosTrazos) / DuracionDeLosTrazos));
        var trazo = new Vector2(_largoDeLosTrazos * trazos, 3f);
        _trazoIzquierdo.sizeDelta = trazo;
        _trazoDerecho.sizeDelta = trazo;

        for (int i = 0; i < _chispas.Length; i++)
        {
            float vida = Mathf.Clamp01((t - MomentosDeLasChispas[i]) / VidaDeUnaChispa);
            float brillo = vida > 0f && vida < 1f ? Mathf.Sin(vida * Mathf.PI) : 0f;
            var c = PaletaUI.Oro;
            c.a = brillo;
            _chispas[i].color = c;
            _chispas[i].rectTransform.localScale = Vector3.one * (0.4f + 0.8f * brillo);
        }

        MoverLasLetras(t);
    }

    /// <summary>Destruye el lienzo (lo llama quien la montó al irse).</summary>
    public void Destruir()
    {
        if (_lienzo != null) Object.Destroy(_lienzo);
    }

    // Cada letra entra en su momento: sube un poco, se encoge hasta su tamaño y aparece.
    void MoverLasLetras(float t)
    {
        if (_verticesDelNombre == null) return;
        var info = _nombre.textInfo;
        for (int i = 0; i < info.characterCount; i++)
        {
            var letra = info.characterInfo[i];
            if (!letra.isVisible) continue;
            int m = letra.materialReferenceIndex;
            int v = letra.vertexIndex;
            if (m >= _verticesDelNombre.Length || m >= info.meshInfo.Length) continue;
            var origen = _verticesDelNombre[m].vertices;
            var destino = info.meshInfo[m].vertices;
            var colores = info.meshInfo[m].colors32;
            if (origen == null || destino == null || v + 3 >= origen.Length || v + 3 >= destino.Length) continue;

            float k = Mathf.Clamp01((t - (InicioDeLasLetras + i * EntreLetras)) / VueloDeLetra);
            float frenada = Frenada(k);
            float escala = Mathf.Lerp(1.35f, 1f, frenada);
            var subida = new Vector3(0f, Mathf.Lerp(-22f, 0f, frenada), 0f);
            var centro = (origen[v] + origen[v + 2]) * 0.5f;
            byte alfa = (byte)(255f * Suave(k));
            for (int j = 0; j < 4; j++)
            {
                destino[v + j] = centro + (origen[v + j] - centro) * escala + subida;
                colores[v + j].a = alfa;
            }
        }
        _nombre.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    RectTransform Trazo(string nombre, Vector2 pivote, float x)
    {
        var imagen = PiezasDeUI.Imagen(nombre, _conjunto, null, PaletaUI.Tinta);
        imagen.raycastTarget = false;
        var rt = imagen.rectTransform;
        PiezasDeUI.Colocar(rt, new Vector2(0.5f, 0.5f), new Vector2(x, AlturaDeLosTrazos), new Vector2(0f, 3f));
        rt.pivot = pivote;
        return rt;
    }

    static float Suave(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    static float Frenada(float x)
    {
        x = 1f - Mathf.Clamp01(x);
        return 1f - x * x * x;
    }

    // Llega un poco más allá y vuelve (como un papel que se abre de golpe).
    static float ConRebote(float x)
    {
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }

    static Sprite s_estrella;

    // Estrella de cinco puntas de oro con borde de tinta. Se genera una vez.
    static Sprite Estrella
    {
        get
        {
            if (s_estrella != null) return s_estrella;
            const int lado = 96, muestras = 4;
            var fuera = Puntas(lado * 0.48f, 0.45f);
            var dentro = Puntas(lado * 0.48f - 7f, 0.42f);
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Estrella" };
            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                int enFuera = 0, enDentro = 0;
                for (int sy = 0; sy < muestras; sy++)
                for (int sx = 0; sx < muestras; sx++)
                {
                    var p = new Vector2(x + (sx + 0.5f) / muestras - lado * 0.5f, y + (sy + 0.5f) / muestras - lado * 0.5f);
                    if (Dentro(fuera, p)) enFuera++;
                    if (Dentro(dentro, p)) enDentro++;
                }
                float total = muestras * muestras;
                float cubre = enFuera / total;
                float relleno = enFuera > 0 ? enDentro / (float)enFuera : 0f;
                var c = Color.Lerp(PaletaUI.Tinta, PaletaUI.Oro, relleno);
                c.a = cubre;
                px[y * lado + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_estrella = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f);
            s_estrella.name = "Estrella";
            return s_estrella;
        }
    }

    static Vector2[] Puntas(float radio, float hundido)
    {
        var p = new Vector2[10];
        for (int k = 0; k < 10; k++)
        {
            float a = Mathf.Deg2Rad * (90f + k * 36f);
            float r = k % 2 == 0 ? radio : radio * hundido;
            p[k] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - radio * 0.06f);
        }
        return p;
    }

    static bool Dentro(Vector2[] poligono, Vector2 p)
    {
        bool dentro = false;
        for (int i = 0, j = poligono.Length - 1; i < poligono.Length; j = i++)
        {
            var a = poligono[i];
            var b = poligono[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                dentro = !dentro;
        }
        return dentro;
    }
}
