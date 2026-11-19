using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Viste el menú principal con el aspecto de papel del juego (el de la maqueta): título grande con su ficha
/// de fase, botones grandes y rechonchos con sombra dura (los dos primeros, mayores), la fila elegida dorada e
/// inclinada, barra de ayuda del mando abajo a la derecha y estrellitas titilando junto al título. Los
/// submenús (Ajustes, Idioma, Controles, Créditos, Notas, Reportar un fallo…) se pasan al mismo papel.
/// Solo cambia la piel y la colocación: botones, cableado y navegación son los del menú de siempre.
/// Quitar el componente devuelve el menú clásico.
/// </summary>
[DisallowMultipleComponent]
public sealed class EstiloDePapelDelMenu : MonoBehaviour
{
    [Tooltip("Contenedor de las filas del menú (cada hijo con un Button es una fila).")]
    [SerializeField] private Transform filas;
    [Tooltip("Piezas del menú clásico que sobran con el papel (el velo lateral).")]
    [SerializeField] private GameObject[] apagar;
    [Tooltip("Título del juego: las estrellitas titilan alrededor de él.")]
    [SerializeField] private RectTransform titulo;
    [Tooltip("Sprite de las estrellitas.")]
    [SerializeField] private Sprite estrella;
    [Tooltip("Grados que se inclina la fila elegida.")]
    [SerializeField] private float inclinacionElegida = -1.4f;
    [Tooltip("Cuánto crece la fila elegida.")]
    [SerializeField, Min(1f)] private float crecimientoElegida = 1.04f;

    // Medidas de la maqueta (lienzo de referencia 1920x1080). Son constantes de diseño, no datos de la escena.
    static readonly Vector2 posicionDelTitulo = new Vector2(102f, -34f);
    const float escalaDelTitulo = 1.9f;
    static readonly Vector2 tamanoVisibleDelTitulo = new Vector2(640f, 380f);
    const float yPrimeraFila = 445f;
    static readonly Vector2 tamanoPrincipal = new Vector2(520f, 84f);
    static readonly Vector2 tamanoSecundario = new Vector2(440f, 50f);
    const float separacion = 9f;
    const float letraPrincipal = 40f;
    const float letraSecundaria = 28f;

    static readonly Color Sombra = new Color(0.047f, 0.031f, 0.11f, 0.7f);
    static readonly Color Verde = new Color(0.18f, 0.49f, 0.31f, 1f);
    static readonly Color Rojo = new Color(0.69f, 0.23f, 0.23f, 1f);

    struct Fila
    {
        public Button boton;
        public Image fondo;
        public RectTransform rect;
        public TMP_Text texto;
        public float foco;
    }

    Fila[] _filas;
    RectTransform[] _chispas;
    float[] _fases;
    readonly HashSet<Graphic> _hechos = new HashSet<Graphic>();
    readonly HashSet<Graphic> _papeles = new HashSet<Graphic>();
    readonly List<Transform> _propios = new List<Transform>();
    int _reescaneos;

    void Start()
    {
        VestirFilas();
        foreach (var go in apagar) if (go != null && go.activeSelf) go.SetActive(false);
        ColocarTitulo();
        CrearChispas();
        CrearAyudaDeMando();
        SondearPaneles();
    }

    // Los submenús (cada uno, un objeto raíz de la escena con su lienzo) se visten la primera vez que se abren y
    // otra vez poco después, por si montan filas al abrirse.
    readonly Dictionary<GameObject, int> _repasos = new Dictionary<GameObject, int>();
    readonly Dictionary<GameObject, float> _proximoRepaso = new Dictionary<GameObject, float>();
    readonly List<GameObject> _raices = new List<GameObject>();
    float _siguienteSondeo;

    void SondearPaneles()
    {
        if (Time.unscaledTime < _siguienteSondeo) return;
        _siguienteSondeo = Time.unscaledTime + 0.25f;
        if (_raices.Count == 0)
        {
            foreach (var go in gameObject.scene.GetRootGameObjects())
                if (go != null && go.GetComponentInChildren<Graphic>(true) != null) _raices.Add(go);
        }
        for (int i = 0; i < _raices.Count; i++)
        {
            var go = _raices[i];
            if (go == null || !go.activeInHierarchy) continue;
            _repasos.TryGetValue(go, out int n);
            if (n >= 6) continue;
            if (n > 0 && _proximoRepaso.TryGetValue(go, out float t) && Time.unscaledTime < t) continue;
            _repasos[go] = n + 1;
            _proximoRepaso[go] = Time.unscaledTime + 1f;
            RestilarSubmenus(go.transform);
        }
    }

    static bool EsPrincipal(Button b, TMP_Text texto)
    {
        string s = (b.name + " " + (texto != null ? texto.text : "")).ToLowerInvariant();
        return s.Contains("continu") || s.Contains("nuev") || s.Contains("new game");
    }

    void VestirFilas()
    {
        var raiz = filas != null ? filas : transform;
        ColocarPanel(raiz as RectTransform);
        var botones = raiz.GetComponentsInChildren<Button>(true);
        _filas = new Fila[botones.Length];
        for (int i = 0; i < botones.Length; i++)
        {
            var b = botones[i];
            var f = new Fila { boton = b, rect = (RectTransform)b.transform, fondo = b.GetComponent<Image>() };
            f.texto = b.GetComponentInChildren<TMP_Text>(true);
            bool principal = EsPrincipal(b, f.texto);
            var tam = principal ? tamanoPrincipal : tamanoSecundario;

            var elemento = b.GetComponent<LayoutElement>();
            if (elemento == null) elemento = b.gameObject.AddComponent<LayoutElement>();
            elemento.minWidth = tam.x; elemento.preferredWidth = tam.x; elemento.flexibleWidth = 0f;
            elemento.minHeight = tam.y; elemento.preferredHeight = tam.y; elemento.flexibleHeight = 0f;
            f.rect.sizeDelta = tam;

            if (f.fondo != null)
            {
                PiezasDeUI.VestirFicha(f.fondo, PaletaUI.Papel);
                PonerSombra(f.fondo.gameObject, 6f);
            }
            if (f.texto != null)
            {
                f.texto.color = PaletaUI.TintaDeTexto;
                f.texto.fontStyle = FontStyles.Bold;
                f.texto.enableAutoSizing = false;
                f.texto.fontSize = principal ? letraPrincipal : letraSecundaria;
                f.texto.alignment = TextAlignmentOptions.MidlineLeft;
                f.texto.margin = new Vector4(principal ? 36f : 30f, 0f, 16f, 0f);
                f.texto.textWrappingMode = TextWrappingModes.NoWrap;
                f.texto.overflowMode = TextOverflowModes.Ellipsis;
                // El resalte clásico (texto amarillo sobre fondo oscuro) no se lee sobre papel: lo lleva el fondo.
                var resalte = f.texto.GetComponent<MenuTextHighlight>();
                if (resalte != null) { resalte.normalColor = PaletaUI.TintaDeTexto; resalte.starColor = PaletaUI.TintaDeTexto; }
            }
            var brillo = b.transform.Find("SelectionGlow");
            if (brillo != null) brillo.gameObject.SetActive(false);
            _filas[i] = f;
        }
    }

    void ColocarPanel(RectTransform panel)
    {
        if (panel == null) return;
        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.spacing = separacion;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 0, 0);
        }
        // Anclado arriba a la izquierda del contenedor; la esquina de la primera fila queda en pantalla.
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        var padre = panel.parent as RectTransform;
        float izquierdaPadre = 0f, arribaPadre = 0f;
        if (padre != null)
        {
            // Esquina superior izquierda del padre en el lienzo de referencia (1920x1080).
            var esquinas = new Vector3[4];
            padre.GetWorldCorners(esquinas);
            var lienzo = padre.GetComponentInParent<Canvas>();
            if (lienzo != null)
            {
                var rl = (RectTransform)lienzo.rootCanvas.transform;
                var a = rl.InverseTransformPoint(esquinas[1]);
                float w = rl.rect.width, h = rl.rect.height;
                izquierdaPadre = a.x + w * rl.pivot.x;
                arribaPadre = h * (1f - rl.pivot.y) - a.y;
                float k = 1920f / Mathf.Max(1f, w);
                izquierdaPadre *= k; arribaPadre *= k;
            }
        }
        panel.anchoredPosition = new Vector2(posicionDelTitulo.x - izquierdaPadre, -(yPrimeraFila - arribaPadre));
        panel.sizeDelta = new Vector2(tamanoPrincipal.x, 620f);
    }

    // El sprite del título tiene mucho margen transparente: se agranda su rectángulo hasta que lo visible mide
    // lo que pide la maqueta. La ficha de fase y las chispas cuelgan de una zona propia, sin escalar.
    RectTransform _zona;

    void ColocarTitulo()
    {
        if (titulo == null) return;
        var padre = titulo.parent as RectTransform;
        _zona = new GameObject("ZonaDelTitulo", typeof(RectTransform)).transform as RectTransform;
        _zona.SetParent(padre, false);
        _zona.SetSiblingIndex(titulo.GetSiblingIndex() + 1);
        _zona.anchorMin = _zona.anchorMax = new Vector2(0f, 1f);
        _zona.pivot = new Vector2(0f, 1f);
        _zona.anchoredPosition = posicionDelTitulo;
        _zona.sizeDelta = tamanoVisibleDelTitulo;
        _propios.Add(_zona);

        // El centro visible del título coincide con el centro de la zona.
        titulo.localScale = new Vector3(escalaDelTitulo, escalaDelTitulo, 1f);
        titulo.anchorMin = titulo.anchorMax = new Vector2(0f, 1f);
        titulo.pivot = new Vector2(0.5f, 0.5f);
        titulo.anchoredPosition = posicionDelTitulo + new Vector2(tamanoVisibleDelTitulo.x * 0.5f, -tamanoVisibleDelTitulo.y * 0.5f);

        var fase = titulo.Find("PhaseLabel") as RectTransform;
        if (fase == null) return;
        var ficha = new GameObject("FichaDeFase", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)ficha.transform;
        rt.SetParent(_zona, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(8f, 6f);
        rt.sizeDelta = new Vector2(250f, 46f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 3f);
        var img = ficha.GetComponent<Image>();
        img.raycastTarget = false;
        PiezasDeUI.VestirFicha(img, PaletaUI.Oro);
        PonerSombra(ficha, 4f);

        fase.SetParent(rt, false);
        fase.anchorMin = Vector2.zero; fase.anchorMax = Vector2.one;
        fase.offsetMin = Vector2.zero; fase.offsetMax = Vector2.zero;
        fase.localRotation = Quaternion.identity;
        var t = fase.GetComponent<TMP_Text>();
        if (t != null)
        {
            t.color = PaletaUI.TintaDeTexto;
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = false;
            t.fontSize = 26f;
            t.alignment = TextAlignmentOptions.Center;
        }
    }

    static void PonerSombra(GameObject go, float alto)
    {
        var sombra = go.GetComponent<Shadow>();
        if (sombra == null) sombra = go.AddComponent<Shadow>();
        sombra.effectColor = Sombra;
        sombra.effectDistance = new Vector2(0f, -alto);
    }

    void CrearAyudaDeMando()
    {
        var lienzo = GetComponentInParent<Canvas>();
        if (lienzo == null) return;
        TMP_FontAsset fuente = null;
        bool espanol = true;
        if (_filas != null && _filas.Length > 0 && _filas[0].texto != null)
        {
            fuente = _filas[0].texto.font;
            string s = _filas[0].texto.text.ToLowerInvariant();
            espanol = !(s.Contains("continue") || s.Contains("new game"));
        }
        var go = new GameObject("AyudaDeMando", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(lienzo.rootCanvas.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-48f, 40f);
        rt.sizeDelta = new Vector2(400f, 62f);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        PiezasDeUI.VestirFicha(img, PaletaUI.PapelClaro);
        PonerSombra(go, 5f);
        string txt = "<color=#" + ColorUtility.ToHtmlStringRGB(Verde) + "><b>A</b></color> " + (espanol ? "Elegir" : "Select")
            + "      <color=#" + ColorUtility.ToHtmlStringRGB(Rojo) + "><b>B</b></color> " + (espanol ? "Atrás" : "Back");
        var texto = PiezasDeUI.Texto("Texto", rt, txt, fuente, 28f, PaletaUI.TintaDeTexto);
        texto.alignment = TextAlignmentOptions.Center;
        texto.fontStyle = FontStyles.Bold;
        texto.raycastTarget = false;
        PiezasDeUI.Estirar((RectTransform)texto.transform);
        _propios.Add(rt);
    }

    bool EsPropio(Transform t)
    {
        if (filas != null && t.IsChildOf(filas)) return true;
        if (titulo != null && t.IsChildOf(titulo)) return true;
        for (int i = 0; i < _propios.Count; i++) if (_propios[i] != null && t.IsChildOf(_propios[i])) return true;
        var cursor = transform.Find("CursorSeleccion");
        if (cursor != null && t.IsChildOf(cursor)) return true;
        return false;
    }

    bool EstaSobrePapel(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent)
        {
            var g = p.GetComponent<Graphic>();
            if (g != null && _papeles.Contains(g)) return true;
        }
        return false;
    }

    // Pasa al papel todo lo que no es del menú principal: velos, hojas, fichas, deslizadores, interruptores y su texto.
    void RestilarSubmenus(Transform raiz)
    {
        Canvas.ForceUpdateCanvases();

        foreach (var img in raiz.GetComponentsInChildren<Image>(true))
        {
            if (img == null || _hechos.Contains(img) || EsPropio(img.transform)) continue;
            // El contenedor del menú principal (y los que lo envuelven) no es una hoja de papel.
            if (img.transform == transform || (filas != null && filas.IsChildOf(img.transform))) continue;
            string n = img.name;
            if (n.Contains("Chispa") || n.Contains("Glow") || n.Contains("Cursor")
                || n.Contains("Cinta") || n.Contains("Estrella") || n.Contains("Cielo") || n.Contains("Resplandor")) continue;

            var r = ((RectTransform)img.transform).rect;
            // Velos que cubren la pantalla: noche translúcida en vez de negro.
            if (r.width >= 1700f)
            {
                var c = PaletaUI.Noche; c.a = 0.62f;
                img.color = c;
                _hechos.Add(img);
                continue;
            }

            // Las columnas de Ajustes llegan con su imagen apagada y solapadas (960 de ancho cada una): se encienden
            // como hojas de papel y se reparten lado a lado, como en la maqueta.
            if (n.StartsWith("Content"))
            {
                img.enabled = true;
                PiezasDeUI.VestirHoja(img);
                _hechos.Add(img); _papeles.Add(img);
                ColocarColumna((RectTransform)img.transform);
                continue;
            }

            var slider = img.GetComponentInParent<Slider>(true);
            var toggle = img.GetComponentInParent<Toggle>(true);
            if (slider != null)
            {
                var relleno = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
                var mando = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
                if (img == relleno) img.color = PaletaUI.OroSobrePapel;
                else if (img == mando) PiezasDeUI.VestirFicha(img, PaletaUI.PapelClaro);
                else img.color = PaletaUI.PapelApagado;
                _hechos.Add(img);
                continue;
            }
            if (toggle != null)
            {
                if (toggle.graphic == img) img.color = PaletaUI.OroSobrePapel;
                else PiezasDeUI.VestirFicha(img, PaletaUI.PapelClaro);
                _hechos.Add(img);
                continue;
            }

            if (img.GetComponent<Button>() != null)
            {
                PiezasDeUI.VestirFicha(img, PaletaUI.Papel);
                PonerSombra(img.gameObject, 5f);
                _hechos.Add(img); _papeles.Add(img);
            }
            else if (EstaSobrePapel(img.transform))
            {
                if (r.width >= 60f && r.height >= 24f) { img.color = PaletaUI.PapelApagado; _hechos.Add(img); }
            }
            else if (r.width >= 160f && r.height >= 60f)
            {
                PiezasDeUI.VestirHoja(img);
                _hechos.Add(img); _papeles.Add(img);
            }
        }

        // Los textos se repintan en cada repaso: otros scripts (localización, ajustes) los vuelven a colorear.
        foreach (var t in raiz.GetComponentsInChildren<Text>(true))
        {
            if (t == null || EsPropio(t.transform)) continue;
            if (!EstaSobrePapel(t.transform) && t.GetComponentInParent<Button>(true) == null) continue;
            bool titulo = EsTituloDeColumna(t.transform);
            t.color = titulo ? PaletaUI.OroSobrePapel : PaletaUI.TintaDeTexto;
            if (t.GetComponentInParent<Button>(true) == null) { t.verticalOverflow = VerticalWrapMode.Overflow; t.fontSize = titulo ? 28 : Mathf.Max(t.fontSize, 24); }
        }

        foreach (var t in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || EsPropio(t.transform)) continue;
            if (!EstaSobrePapel(t.transform) && t.GetComponentInParent<Button>(true) == null) continue;
            bool titulo = EsTituloDeColumna(t.transform);
            t.color = titulo ? PaletaUI.OroSobrePapel : PaletaUI.TintaDeTexto;
            if (t.GetComponentInParent<Button>(true) == null && !t.enableAutoSizing)
                t.fontSize = titulo ? 28f : Mathf.Max(t.fontSize, 24f);
            var resalte = t.GetComponent<MenuTextHighlight>();
            if (resalte != null) { resalte.normalColor = t.color; resalte.starColor = t.color; }
        }
    }

    static bool EsTituloDeColumna(Transform t) => t.parent != null && t.parent.name.StartsWith("Content");

    static void ColocarColumna(RectTransform rt)
    {
        float x = rt.name.Contains("Left") ? -600f : rt.name.Contains("Right") ? 640f : 0f;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(rt.name.Contains("Right") ? 640f : 570f, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2(x, -150f);
        var vlg = rt.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.childAlignment = TextAnchor.UpperCenter;
    }

    void CrearChispas()
    {
        if (_zona == null || estrella == null) return;
        // Posición (fracción del título), tamaño en píxeles.
        var sitios = new[] { new Vector3(0.02f, 0.92f, 30f), new Vector3(0.97f, 0.78f, 22f), new Vector3(0.88f, 0.02f, 18f) };
        _chispas = new RectTransform[sitios.Length];
        _fases = new float[sitios.Length];
        for (int i = 0; i < sitios.Length; i++)
        {
            var go = new GameObject("Chispa", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_zona, false);
            rt.anchorMin = rt.anchorMax = new Vector2(sitios[i].x, sitios[i].y);
            rt.sizeDelta = new Vector2(sitios[i].z, sitios[i].z);
            var img = go.GetComponent<Image>();
            img.sprite = estrella;
            img.color = PaletaUI.Oro;
            img.raycastTarget = false;
            _chispas[i] = rt;
            _fases[i] = i * 2.1f;
        }
    }

    void Update()
    {
        SondearPaneles();
        if (_filas == null) return;
        var elegido = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        for (int i = 0; i < _filas.Length; i++)
        {
            ref var f = ref _filas[i];
            if (f.boton == null) continue;
            bool conFoco = f.boton.gameObject == elegido && f.boton.interactable;
            f.foco = Mathf.Lerp(f.foco, conFoco ? 1f : 0f, k);
            if (f.fondo != null)
            {
                var c = Color.Lerp(PaletaUI.Papel, PaletaUI.Oro, f.foco);
                c.a = f.boton.interactable ? 1f : 0.55f;
                f.fondo.color = c;
            }
            f.rect.localRotation = Quaternion.Euler(0f, 0f, inclinacionElegida * f.foco);
            float escala = Mathf.Lerp(1f, crecimientoElegida, f.foco);
            f.rect.localScale = new Vector3(escala, escala, 1f);
        }

        if (_chispas == null) return;
        float t = Time.unscaledTime;
        for (int i = 0; i < _chispas.Length; i++)
        {
            float pulso = 0.5f + 0.5f * Mathf.Sin(t * 2.4f + _fases[i]);
            float e = 0.55f + 0.6f * pulso;
            _chispas[i].localScale = new Vector3(e, e, 1f);
            _chispas[i].localRotation = Quaternion.Euler(0f, 0f, 12f * Mathf.Sin(t * 0.9f + _fases[i]));
        }
    }
}
