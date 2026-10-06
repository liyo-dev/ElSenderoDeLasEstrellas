using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public enum PresentacionDeTexto { Bocadillo, Subtitulo, SubtituloGrande }

/// Texto cinematográfico paginado, como bocadillo o subtítulo. Vive en la UI de Start.
public class SpeechBubbleUI : MonoBehaviour
{
    PresentacionDeTexto _presentacion;
    CanvasGroup _grupoBocadillo, _grupoSubtitulo;
    RectTransform _rectBocadillo, _rectSubtitulo, _canvasSubtitulo;
    TextMeshProUGUI _textoBocadillo, _textoSubtitulo, _nombreSubtitulo;
    Image _franjaSubtitulo;
    public static SpeechBubbleUI Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    [Header("Referencias UI")]
    [SerializeField] CanvasGroup _rootGroup;
    [SerializeField] RectTransform _bubbleRect;
    [SerializeField] TextMeshProUGUI _label;
    [SerializeField] RectTransform _parentCanvasRect;

    [Header("Sprites")]
    [SerializeField] Sprite _defaultSprite;
    [SerializeField] Sprite _emphasisSprite;

    [Header("Animación")]
    [SerializeField] float _fadeInDuration  = 0.15f;
    [SerializeField] float _fadeOutDuration = 0.15f;
    [SerializeField] float _popInDuration   = 0.35f;
    [SerializeField] float _popOutDuration  = 0.18f;

    [Header("Tamaño")]
    [Tooltip("Ancho mínimo del bocadillo en píxeles de canvas. Aumenta si el texto se corta por los lados.")]
    [SerializeField] float _bubbleMinWidth = 420f;
    [Tooltip("Ancho máximo del bocadillo en píxeles de canvas antes de partir el texto en varias líneas. BUGFIX (Agosto 2026): antes el bocadillo solo tenía un ancho MÍNIMO y nunca crecía con el texto real, así que cualquier línea más larga que ese mínimo se salía por los lados en todas las secuencias (ver captura del bug). Ahora el ancho se calcula a partir del texto real en Show(), entre este máximo y _bubbleMinWidth. El ALTO no se toca aquí — lo calcula solo el ContentSizeFitter/VerticalLayoutGroup del hijo \"Bubble\" en el prefab (ver Show()).\nAJUSTE (Agosto 2026, 2ª pasada): min/max bajados de 560/900 a 420/620. Con los valores viejos, cualquier frase de longitud media cabía en una sola línea muy ancha y el bocadillo salía como un óvalo aplastado y alargado; y las frases cortas se estiraban igual hasta el mínimo de 560, con el mismo efecto. Con el ancho máximo más bajo, el texto envuelve antes en 2-3 líneas más cortas y el bocadillo crece en vertical en su lugar — se ve más redondo y las líneas quedan más parejas entre sí (ayuda también a que el bloque de texto centrado se perciba realmente centrado, en vez de una única línea larga pegada a los bordes).")]
    [SerializeField] float _bubbleMaxWidth = 620f;
    [Tooltip("Margen interno horizontal del texto (izquierda y derecha).")]
    [SerializeField] float _labelHorizontalMargin = 44f;

    [Header("Páginas (INC-435)")]
    [Tooltip("Máximo de líneas por bocadillo. Regla de Raúl (25 sep 2026): «los textos de los " +
             "bocadillos no pueden ser de más de tres líneas; si es así hay que paginar la frase en " +
             "TODO EL JUEGO». Una frase que ocupa más se parte en páginas que salen una detrás de otra " +
             "en el mismo bocadillo, cortando por el final de una frase siempre que se pueda.")]
    [SerializeField, Min(1)] int _maxLineas = 3;
    [Tooltip("Caracteres por segundo de lectura cómoda. Ninguna página dura menos de lo que se tarda " +
             "en leerla, aunque quien llama pida menos tiempo: en prologo/peras, líneas de 130 " +
             "caracteres salían 3 s y no daba tiempo a leerlas.")]
    [SerializeField, Min(1f)] float _caracteresPorSegundo = 15f;
    [Tooltip("Ninguna página dura menos que esto.")]
    [SerializeField, Min(0.5f)] float _lecturaMinima = 1.6f;

    [Tooltip("Caracteres hablados por segundo para estimar el habla sin voz.")]
    [SerializeField, Min(1f)] float _caracteresHabladosPorSegundo = 14f;

    [Header("Posición")]
    [SerializeField] Vector3 _worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Fijo en pantalla (quien habla no está a la vista)")]
    [Tooltip("Dónde se queda el bocadillo cuando se muestra fijo en pantalla, en proporción del " +
             "canvas (0,0 abajo a la izquierda; 1,1 arriba a la derecha). En este modo el bocadillo " +
             "no tiene pico: una fila de circulitos sube de él hasta el nombre de quien habla.")]
    [SerializeField] Vector2 _posicionFija = new Vector2(0f, 0.42f);
    [Tooltip("Separación del borde de la pantalla, en píxeles de canvas.")]
    [SerializeField] float _margenFijo = 40f;
    [SerializeField] Color _colorNombre = new Color(1f, 0.85f, 0.35f);
    [Tooltip("Color del contorno del bocadillo sin pico y de sus circulitos.")]
    [SerializeField] Color _colorContornoADistancia = new Color(0.08f, 0.08f, 0.1f, 1f);

    Image _bubbleImage;
    Camera _cam;
    Transform _target;
    bool _isShowing;
    // Cada Show() abre un turno nuevo: quien lo abrió puede cerrar SOLO el suyo (Hide(turno)) y
    // saber si otro le ha quitado el bocadillo. Ver INC-613.
    int _turno;

    /// Turno del bocadillo en pantalla (o del último que se mostró).
    public int Turno => _turno;
    /// Hay un bocadillo en pantalla (aunque sea saliendo).
    public bool Mostrando => _isShowing;
    Coroutine _autoHideRoutine;
    // Callback pendiente del Show() en curso — solo relevante mientras _autoHideRoutine != null.
    // Guardado aparte (además del parámetro local que ya recibe la propia corrutina AutoHide) para
    // que SkipCurrent() pueda invocarlo desde fuera sin depender de la corrutina.
    Action _pendingOnComplete;

    // Offset por llamada, para cuando el punto de anclaje ya está a la altura que toca (una marca
    // de posición colocada a mano, en vez de la cabeza de un personaje) y sumar el offset de
    // siempre lo dejaría demasiado alto. Null = usar '_worldOffset' de siempre, sin cambiar nada
    // del comportamiento existente. Ver SayBeat.overrideBubbleOffset (17 sep 2026).
    Vector3? _offsetOverride;

    // Oculto temporalmente porque hay un menú (pausa, equipo, tienda...) abierto encima.
    bool _hiddenByMenu;

    // Fijo en pantalla en vez de seguir al personaje, con su nombre encima (ver Show()).
    bool _fijoEnPantalla;
    TextMeshProUGUI _nombre;

    // Estilo «a distancia» del modo fijo: cuerpo sin pico y circulitos hasta el nombre.
    static readonly (Vector2 centro, float diametro)[] Circulitos =
    {
        (new Vector2(46f, 20f), 24f),
        (new Vector2(30f, 46f), 16f),
        (new Vector2(21f, 66f), 10f),
    };
    const float AlturaNombre = 78f;
    Sprite _spriteSinPico;
    Sprite _spriteCirculito;
    readonly List<Image> _circulitos = new();
    VerticalLayoutGroup _layout;
    RectOffset _paddingConPico;

    // Caché de componentes de animación: evita GetComponentInChildren repetido
    readonly Dictionary<Transform, Animator> _animatorCache = new();
    readonly Dictionary<Transform, NPCSimpleAnimator> _npcAnimCache = new();
    readonly Dictionary<Transform, PlayerDialogueAnimator> _playerAnimCache = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        _cam = Camera.main;
        _bubbleImage = _bubbleRect.GetComponent<Image>();
        _layout = _bubbleRect.GetComponent<VerticalLayoutGroup>();
        if (_layout != null)
            _paddingConPico = new RectOffset(_layout.padding.left, _layout.padding.right,
                                             _layout.padding.top, _layout.padding.bottom);

        if (_parentCanvasRect == null)
            _parentCanvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();

        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = false;
        _bubbleRect.localScale = Vector3.zero;
        _grupoBocadillo = _rootGroup;
        _rectBocadillo = _bubbleRect;
        _textoBocadillo = _label;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Mismo sistema que ya usan BossHealthBar/MinimapController: ocultarse mientras
        // hay un menú abierto (pausa incluida) y restaurarse al cerrar el último.
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;
        Canvas.willRenderCanvases += ColocarBocadillo;
    }

    void OnDisable()
    {
        Hide();
        _rootGroup.DOKill();
        _bubbleRect.DOKill();
        _rootGroup.alpha = 0f;
        _pendingOnComplete = null;
        SenalesDeHabla.Para(_target);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
        Canvas.willRenderCanvases -= ColocarBocadillo;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => _cam = Camera.main;

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _rootGroup?.DOKill();
        _bubbleRect?.DOKill();
        _grupoBocadillo?.DOKill();
        _rectBocadillo?.DOKill();
        _grupoSubtitulo?.DOKill();
        _rectSubtitulo?.DOKill();
        if (_canvasSubtitulo != null) Destroy(_canvasSubtitulo.gameObject);
        DestruirSprite(_spriteSinPico);
        DestruirSprite(_spriteCirculito);
    }

    static void DestruirSprite(Sprite s)
    {
        if (s == null) return;
        if (s.texture != null) Destroy(s.texture);
        Destroy(s);
    }

    /// Coloca el bocadillo justo antes de dibujar el canvas, cuando todas las cámaras ya se han
    /// movido en su LateUpdate. No va en un LateUpdate propio: el orden entre LateUpdates no está
    /// garantizado y, si se colocase antes que la cámara, iría un fotograma por detrás y temblaría
    /// al andar. Ver INC-541.
    void ColocarBocadillo()
    {
        if (_presentacion != PresentacionDeTexto.Bocadillo)
        {
            if (_isShowing) ColocarSubtitulo();
            return;
        }
        if (!_isShowing || _parentCanvasRect == null) return;

        if (_fijoEnPantalla)
        {
            Rect canvas = _parentCanvasRect.rect;
            var fija = new Vector2(
                canvas.xMin + canvas.width * _posicionFija.x + _bubbleRect.rect.width * 0.5f + _margenFijo,
                canvas.yMin + canvas.height * _posicionFija.y);
            _bubbleRect.anchoredPosition = ClampToCanvas(fija);
            return;
        }

        if (_target == null || _cam == null) return;

        Vector3 offset = _offsetOverride ?? _worldOffset;
        Vector3 screenPos = _cam.WorldToScreenPoint(_target.position + offset);
        if (screenPos.z < 0f) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentCanvasRect, screenPos, null, out Vector2 local))
            _bubbleRect.anchoredPosition = ClampToCanvas(local);
    }

    /// FIX (17 sep 2026, Raúl: "los bocadillos se cortan por arriba" -- prólogo, planos cerrados
    /// nuevos generados por ShotComposer). Antes esta posición se aplicaba sin límites: con poco
    /// headroom (un CloseUp, por ejemplo) la cabeza del personaje queda cerca del borde superior
    /// de la pantalla, y el bocadillo -- que crece HACIA ARRIBA desde el punto de anclaje, con el
    /// pico apuntando hacia abajo a la cabeza -- se salía por encima del canvas. Nunca se había
    /// visto porque las secuencias antiguas (Discusión Ventana, Despertar de la Estrella) siempre
    /// enmarcan con más aire por encima; el prólogo es el primer sitio que pide planos cerrados de
    /// verdad con este sistema. Se calcula el margen a partir del tamaño real del bocadillo (que ya
    /// cambia con el texto, ver Show()) y de su propio pivote, así que funciona igual sin importar
    /// si el pivote está abajo (como parece, por el pico) o en cualquier otro punto.
    Vector2 ClampToCanvas(Vector2 local)
    {
        if (_parentCanvasRect == null || _bubbleRect == null) return local;

        Rect canvasRect = _parentCanvasRect.rect;
        float halfWidth    = _bubbleRect.rect.width * 0.5f;
        float bottomMargin = _bubbleRect.rect.height * _bubbleRect.pivot.y;
        float topMargin    = _bubbleRect.rect.height * (1f - _bubbleRect.pivot.y);

        float minX = canvasRect.xMin + halfWidth;
        float maxX = canvasRect.xMax - halfWidth;
        float minY = canvasRect.yMin + bottomMargin;
        float maxY = canvasRect.yMax - topMargin;

        // Si el bocadillo es más grande que el propio canvas (no debería pasar nunca, pero por si
        // acaso) min > max invertiría el clamp -- se deja sin tocar ese eje en vez de forzarlo.
        if (minX <= maxX) local.x = Mathf.Clamp(local.x, minX, maxX);
        if (minY <= maxY) local.y = Mathf.Clamp(local.y, minY, maxY);
        return local;
    }

    // ── API pública ───────────────────────────────────────────────────────────

    [ContextMenu("TEST Show (Player)")]
    void TestShow()
    {
        var player = GameObject.FindWithTag("Player");
        Show(player != null ? player.transform : transform, "¡Otra vez ese sueño...!", 4f);
    }

    [ContextMenu("TEST Hide")]
    void TestHide() => Hide();

    /// <summary>
    /// Muestra una línea con la presentación elegida y mantiene el habla de <paramref name="target"/>.
    /// </summary>
    /// <param name="animTrigger">Trigger del Animator del personaje a disparar mientras habla.</param>
    /// <param name="emphasis">Si true, usa el sprite de énfasis (ej: burbuja explosiva).</param>
    /// <param name="speakerName">
    /// Nombre que aparece encima del subtítulo o del bocadillo fijo. El bocadillo anclado
    /// identifica al personaje con su pico; el subtítulo grande no muestra nombre.
    /// </param>
    /// <param name="worldOffset">
    /// Sustituye, solo para esta llamada, el offset vertical de siempre (_worldOffset, 2,2 m).
    /// Null (por defecto) = comportamiento de siempre. Pensado para SayBeat.markName (17 sep
    /// 2026): una marca de posición ya colocada a la altura que toca no debería tener que
    /// enterrarse 2,2 m bajo el suelo solo para compensar el offset pensado para cabezas de
    /// personaje.
    /// </param>
    /// <param name="fijoEnPantalla">
    /// En vez de seguir al personaje, el bocadillo se queda quieto en un lado de la pantalla
    /// (_posicionFija), sin pico y con una fila de circulitos que sube hasta el nombre de quien
    /// habla (speakerName): se lee como una voz que llega de alguien que no está a la vista. Para
    /// comentarios durante el juego (Eldran guiando un combate, INC-480 e INC-534). En cinemáticas
    /// se deja en false: ahí el pico señala al que habla.
    /// </param>
    /// Devuelve el turno de este bocadillo (ver Hide(int)).
    public int Show(Transform target, string text, float duration = 0f,
                     Action onComplete = null, string animTrigger = null, bool emphasis = false,
                     string speakerName = null, Vector3? worldOffset = null,
                     bool fijoEnPantalla = false,
                     PresentacionDeTexto presentacion = PresentacionDeTexto.Bocadillo,
                     Color colorDelNombre = default)
    {
        CambiarPresentacion(presentacion, speakerName, colorDelNombre);
        if (_autoHideRoutine != null) { StopCoroutine(_autoHideRoutine); _autoHideRoutine = null; }
        _turno++;

        SenalesDeHabla.Para(_target);
        _target = target;
        _offsetOverride = worldOffset;
        _isShowing = true;
        _fijoEnPantalla = fijoEnPantalla;
        if (presentacion == PresentacionDeTexto.Bocadillo)
        {
            PonerNombre(fijoEnPantalla ? speakerName : null);
            PonerEstiloADistancia(fijoEnPantalla);
        }

        // Frases de más de _maxLineas líneas: páginas (INC-435).
        List<string> paginas = Paginar(text, presentacion);
        if (paginas.Count == 0) paginas.Add(text ?? string.Empty);
        string primera = paginas[0];
        // Los mismos iconos que los diálogos: «presiona <sprite name="interactable_x">» sin esto
        // salía como «?» en el bocadillo (INC-468).
        Core.InputGlyphs.InputGlyphService.UsarIconos(_label);
        _label.text = primera;

        GameplayEventLog.Log("Dialogo", !string.IsNullOrEmpty(speakerName) ? speakerName : target != null ? target.name : null);

        // Centrado forzado siempre (horizontal Y vertical): no depender solo del valor por
        // defecto del prefab, que podría cambiar sin querer en una variante o en una edición
        // futura del prefab. El bocadillo de cómic siempre debe leerse centrado.
        _label.alignment = TextAlignmentOptions.Center;

        // Margen horizontal para que el texto no roque los bordes del bocadillo
        _label.margin = presentacion == PresentacionDeTexto.Bocadillo
            ? new Vector4(_labelHorizontalMargin, _label.margin.y, _labelHorizontalMargin, _label.margin.w)
            : Vector4.zero;

        // BUGFIX (Agosto 2026): el bocadillo solo tenía un ancho MÍNIMO fijo (_bubbleMinWidth) y
        // nunca se adaptaba al texto real, así que cualquier línea más larga que ese mínimo se
        // salía por los lados — pasaba en todas las secuencias, no era un caso puntual. Ahora se
        // mide el ancho que ocuparía el texto en una sola línea (GetPreferredValues) y el
        // bocadillo crece hasta _bubbleMaxWidth para acomodarlo antes de partir el texto en
        // varias líneas (word wrap).
        //
        // OJO — el ALTO no se toca aquí a mano. El GameObject "Bubble" (padre de _label, ver
        // prefab SpeechBubbleUI/Bubble) ya tiene un VerticalLayoutGroup (padding real: 18 arriba,
        // 58 abajo para dejar sitio al pico del bocadillo) + ContentSizeFitter con
        // m_VerticalFit=PreferredSize — es decir, el alto SIEMPRE lo calcula ese sistema a partir
        // del preferredHeight del texto ya envuelto, no nosotros. El primer intento de este
        // arreglo calculaba también el alto a mano con un margen inventado que no coincidía con
        // ese padding real (18/58, asimétrico por el pico) — el resultado quedaba más bajo que el
        // texto de verdad y la primera línea se salía por ARRIBA del bocadillo (bug reportado tras
        // el primer pase). Ahora solo tocamos el ancho y forzamos un rebuild de layout inmediato
        // para que el ContentSizeFitter recalcule el alto ANTES del pop-in — si no, se ve un frame
        // con el alto del texto anterior mientras el ancho ya ha cambiado.
        if (_bubbleRect != null && _label != null)
        {
            _label.textWrappingMode = TextWrappingModes.Normal;

            AjustarAncho(primera);
        }

        if (_bubbleImage != null && presentacion == PresentacionDeTexto.Bocadillo)
        {
            Sprite sprite = fijoEnPantalla ? SpriteSinPico()
                          : emphasis && _emphasisSprite != null ? _emphasisSprite : _defaultSprite;
            if (sprite != null) _bubbleImage.sprite = sprite;
        }

        // Animación del personaje: preferir el wrapper de alto nivel para respetar el state machine
        if (!string.IsNullOrEmpty(animTrigger) && target != null)
        {
            var npcAnim = GetCachedNPCAnimator(target);
            if (npcAnim != null)
            {
                npcAnim.PlaySocialGesture(animTrigger);
            }
            else
            {
                var playerAnim = GetCachedPlayerAnimator(target);
                if (playerAnim != null)
                    playerAnim.PlayGesture(animTrigger);
                else
                {
                    var anim = GetCachedAnimator(target);
                    if (anim != null) anim.Play(animTrigger);
                }
            }
        }

        // Pop-in: escala desde 0 con rebote + fade
        _rootGroup.DOKill();
        _bubbleRect.DOKill();

        _rootGroup.alpha = 0f;
        _bubbleRect.localScale = presentacion == PresentacionDeTexto.Bocadillo ? Vector3.zero
            : Vector3.one * (presentacion == PresentacionDeTexto.SubtituloGrande ? 1.15f : 1f);

        _rootGroup.DOFade(1f, presentacion == PresentacionDeTexto.Bocadillo ? _fadeInDuration : 0.12f).SetUpdate(true);
        _bubbleRect.DOScale(Vector3.one, presentacion == PresentacionDeTexto.Bocadillo ? _popInDuration : 0.15f)
                   .SetEase(presentacion == PresentacionDeTexto.Bocadillo ? Ease.OutBack : Ease.OutQuad)
                   .SetUpdate(true);

        _pendingOnComplete = onComplete;
        if (paginas.Count > 1)
            _autoHideRoutine = StartCoroutine(Co_Paginas(paginas, duration, onComplete));
        else
        {
            float tiempo = duration > 0f ? Mathf.Max(duration, TiempoDeLectura(primera)) : 0f;
            EmpezarHablaDePagina(primera, tiempo);
            if (duration > 0f) _autoHideRoutine = StartCoroutine(AutoHide(tiempo, onComplete));
        }
        return _turno;
    }

    /// Cuánto tiene que estar en pantalla una página para poder leerla (INC-435).
    public float TiempoDeLectura(string pagina)
        => Mathf.Max(_lecturaMinima, 0.6f + (pagina?.Length ?? 0) / Mathf.Max(1f, _caracteresPorSegundo));

    /// Parte el texto con la fuente y ancho reales de su presentación: como máximo _maxLineas
    /// en bocadillo o dos en subtítulo. Corta por frases y, si no caben, por palabras.
    /// Show y SayBeat comparten esta medición para temporizar cada página.
    public List<string> Paginar(string text, PresentacionDeTexto presentacion = PresentacionDeTexto.Bocadillo)
    {
        var paginas = new List<string>();
        if (string.IsNullOrWhiteSpace(text) || _label == null || _bubbleRect == null)
        {
            if (!string.IsNullOrWhiteSpace(text)) paginas.Add(text.Trim());
            return paginas;
        }
        text = text.Trim();

        TextMeshProUGUI medida = _textoBocadillo;
        int lineas = _maxLineas;
        float ancho;
        float anchoGuardado = _rectBocadillo.sizeDelta.x;
        if (presentacion != PresentacionDeTexto.Bocadillo)
        {
            CrearSubtitulo();
            medida = _textoSubtitulo;
            medida.fontSize = TamanoDeSubtitulo(presentacion);
            ancho = _canvasSubtitulo.rect.width * 0.7f;
            lineas = 2;
        }
        else
        {
            medida.alignment = TextAlignmentOptions.Center;
            medida.textWrappingMode = TextWrappingModes.Normal;
            medida.margin = new Vector4(_labelHorizontalMargin, medida.margin.y, _labelHorizontalMargin, medida.margin.w);
            float preferido = medida.GetPreferredValues(text, 0f, 0f).x + _labelHorizontalMargin * 2f;
            Vector2 tamano = _rectBocadillo.sizeDelta;
            tamano.x = Mathf.Clamp(preferido, _bubbleMinWidth, _bubbleMaxWidth);
            _rectBocadillo.sizeDelta = tamano;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectBocadillo);
            ancho = medida.rectTransform.rect.width - medida.margin.x - medida.margin.z;
        }
        if (ancho < 50f) ancho = _bubbleMaxWidth - _labelHorizontalMargin * 2f;
        float altoMaximo = medida.GetPreferredValues(RenglonesDePrueba(lineas), ancho, 0f).y + 0.5f;
        bool Cabe(string s) => medida.GetPreferredValues(s, ancho, 0f).y <= altoMaximo;

        if (Cabe(text))
        {
            paginas.Add(text);
        }
        else
        {
            string actual = "";
            foreach (string frase in Frases(text))
            {
                string junto = actual.Length == 0 ? frase : actual + " " + frase;
                if (Cabe(junto)) { actual = junto; continue; }
                if (actual.Length > 0) { paginas.Add(actual); actual = ""; }
                if (Cabe(frase)) { actual = frase; continue; }

                // Una frase que sola ya pasa de las líneas: por palabras.
                foreach (string palabra in frase.Split(' '))
                {
                    if (palabra.Length == 0) continue;
                    string prueba = actual.Length == 0 ? palabra : actual + " " + palabra;
                    if (actual.Length > 0 && !Cabe(prueba)) { paginas.Add(actual); actual = palabra; }
                    else actual = prueba;
                }
            }
            if (actual.Length > 0) paginas.Add(actual);
        }

        if (presentacion == PresentacionDeTexto.Bocadillo)
        {
            Vector2 tamano = _rectBocadillo.sizeDelta;
            tamano.x = anchoGuardado;
            _rectBocadillo.sizeDelta = tamano;
        }
        return paginas;
    }

    static string RenglonesDePrueba(int n)
    {
        var sb = new System.Text.StringBuilder("Ágj");
        for (int i = 1; i < n; i++) sb.Append("\nÁgj");
        return sb.ToString();
    }

    /// Frases de un texto, con su puntuación final pegada («¿Qué?», «Vale...», «¡Ya!»).
    static List<string> Frases(string text)
    {
        var frases = new List<string>();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            sb.Append(c);
            bool fin = c == '.' || c == '!' || c == '?' || c == '…' || c == '\n';
            bool siguienteEsEspacio = i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]);
            if (fin && siguienteEsEspacio)
            {
                string f = sb.ToString().Trim();
                if (f.Length > 0) frases.Add(f);
                sb.Clear();
            }
        }
        string resto = sb.ToString().Trim();
        if (resto.Length > 0) frases.Add(resto);
        return frases;
    }

    /// Nombre de quien habla encima del bocadillo. Solo en el modo fijo en pantalla: siguiendo al
    /// personaje, el pico ya dice quién es. No cuenta como línea del bocadillo (va fuera del layout).
    void PonerNombre(string nombre)
    {
        if (string.IsNullOrEmpty(nombre))
        {
            if (_nombre != null) _nombre.gameObject.SetActive(false);
            return;
        }

        if (_nombre == null)
        {
            var go = new GameObject("Nombre", typeof(RectTransform));
            go.transform.SetParent(_bubbleRect, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(4f, AlturaNombre);
            rt.sizeDelta = new Vector2(400f, 40f);

            _nombre = go.AddComponent<TextMeshProUGUI>();
            _nombre.font = _label.font;
            _nombre.fontSize = _label.fontSize * 0.8f;
            _nombre.fontStyle = FontStyles.Bold;
            _nombre.alignment = TextAlignmentOptions.BottomLeft;
            _nombre.textWrappingMode = TextWrappingModes.NoWrap;
            _nombre.raycastTarget = false;
            var sombra = go.AddComponent<Shadow>();
            sombra.effectColor = new Color(0f, 0f, 0f, 0.8f);
            sombra.effectDistance = new Vector2(2f, -2f);
        }

        _nombre.color = _colorNombre;
        _nombre.text = nombre;
        _nombre.gameObject.SetActive(true);
    }

    /// Cuerpo sin pico (el relleno de abajo reservado al pico se iguala al de arriba) y los
    /// circulitos que llevan al nombre. Con fijo = false deja el bocadillo con pico de siempre.
    void PonerEstiloADistancia(bool fijo)
    {
        if (_layout != null && _paddingConPico != null)
        {
            int arriba = _paddingConPico.top;
            _layout.padding = fijo
                ? new RectOffset(_paddingConPico.left, _paddingConPico.right, arriba + 6, arriba + 6)
                : new RectOffset(_paddingConPico.left, _paddingConPico.right, arriba, _paddingConPico.bottom);
        }

        if (fijo && _circulitos.Count == 0)
        {
            foreach (var (centro, diametro) in Circulitos)
            {
                var go = new GameObject("Circulito", typeof(RectTransform));
                go.transform.SetParent(_bubbleRect, false);
                go.AddComponent<LayoutElement>().ignoreLayout = true;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = centro;
                rt.sizeDelta = new Vector2(diametro, diametro);
                var img = go.AddComponent<Image>();
                img.sprite = SpriteCirculito();
                img.raycastTarget = false;
                _circulitos.Add(img);
            }
        }
        foreach (var c in _circulitos)
            if (c != null && c.gameObject.activeSelf != fijo) c.gameObject.SetActive(fijo);
    }

    /// Rectángulo redondeado blanco con contorno, en 9-slice: el bocadillo sin pico.
    Sprite SpriteSinPico()
    {
        if (_spriteSinPico != null) return _spriteSinPico;
        const int lado = 128, radio = 44, borde = 48;
        var tex = PintarRedondeado(lado, lado, radio, 3.5f);
        _spriteSinPico = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f,
                                       0, SpriteMeshType.FullRect, new Vector4(borde, borde, borde, borde));
        _spriteSinPico.name = "BocadilloSinPico";
        return _spriteSinPico;
    }

    Sprite SpriteCirculito()
    {
        if (_spriteCirculito != null) return _spriteCirculito;
        const int lado = 64;
        var tex = PintarRedondeado(lado, lado, lado * 0.5f, 5f);
        _spriteCirculito = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f);
        _spriteCirculito.name = "Circulito";
        return _spriteCirculito;
    }

    /// Relleno blanco con contorno de _colorContornoADistancia, con los bordes suavizados.
    Texture2D PintarRedondeado(int ancho, int alto, float radio, float grosor)
    {
        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "BocadilloADistancia",
        };
        var px = new Color32[ancho * alto];
        Color contorno = _colorContornoADistancia;
        for (int y = 0; y < alto; y++)
        for (int x = 0; x < ancho; x++)
        {
            // Distancia con signo al borde del rectángulo redondeado (negativa dentro).
            float qx = Mathf.Abs(x + 0.5f - ancho * 0.5f) - (ancho * 0.5f - radio);
            float qy = Mathf.Abs(y + 0.5f - alto * 0.5f) - (alto * 0.5f - radio);
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                      + Mathf.Min(Mathf.Max(qx, qy), 0f) - radio;
            float fuera = Mathf.Clamp01(d + 0.5f);                // 1 = fuera del todo
            float relleno = Mathf.Clamp01(-(d + grosor) + 0.5f);   // 1 = dentro del contorno
            Color c = Color.Lerp(contorno, Color.white, relleno);
            c.a = Mathf.Max((1f - fuera) * contorno.a, relleno);
            px[y * ancho + x] = c;
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    // La presentación cambia las referencias visuales; el ciclo de habla y cierre es compartido.
    void CambiarPresentacion(PresentacionDeTexto presentacion, string nombre, Color color)
    {
        _rootGroup.DOKill();
        _bubbleRect.DOKill();
        _rootGroup.alpha = 0f;
        _presentacion = presentacion;
        if (presentacion == PresentacionDeTexto.Bocadillo)
        {
            _rootGroup = _grupoBocadillo;
            _bubbleRect = _rectBocadillo;
            _label = _textoBocadillo;
            return;
        }
        CrearSubtitulo();
        _rootGroup = _grupoSubtitulo;
        _bubbleRect = _rectSubtitulo;
        _label = _textoSubtitulo;
        _label.fontSize = TamanoDeSubtitulo(presentacion);
        _nombreSubtitulo.fontSize = TamanoDeSubtitulo(PresentacionDeTexto.Subtitulo) * 0.75f;
        _nombreSubtitulo.text = presentacion == PresentacionDeTexto.SubtituloGrande ? string.Empty : nombre;
        _nombreSubtitulo.color = color.a > 0f ? color : new Color(0.91f, 0.91f, 0.91f);
        ColocarSubtitulo();
    }

    void CrearSubtitulo()
    {
        if (_grupoSubtitulo != null) return;
        var go = new GameObject("Subtitulos", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(_parentCanvasRect, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9996;
        _canvasSubtitulo = (RectTransform)go.transform;
        _canvasSubtitulo.anchorMin = Vector2.zero;
        _canvasSubtitulo.anchorMax = Vector2.one;
        _canvasSubtitulo.offsetMin = _canvasSubtitulo.offsetMax = Vector2.zero;
        var panel = new GameObject("Linea", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panel.transform.SetParent(go.transform, false);
        _rectSubtitulo = (RectTransform)panel.transform;
        _grupoSubtitulo = panel.GetComponent<CanvasGroup>();
        _grupoSubtitulo.alpha = 0f;
        _grupoSubtitulo.blocksRaycasts = false;
        _grupoSubtitulo.interactable = false;
        // Los grupos del HUD no ocultan el texto de la cinemática.
        _grupoSubtitulo.ignoreParentGroups = true;
        _franjaSubtitulo = panel.GetComponent<Image>();
        _franjaSubtitulo.raycastTarget = false;
        _textoSubtitulo = CrearTextoDeSubtitulo("Texto", _rectSubtitulo);
        _nombreSubtitulo = CrearTextoDeSubtitulo("Hablante", _rectSubtitulo);
        _nombreSubtitulo.fontSize = _textoBocadillo.fontSize * 0.75f;
        _nombreSubtitulo.fontStyle = FontStyles.Bold;
        _nombreSubtitulo.textWrappingMode = TextWrappingModes.NoWrap;
    }

    TextMeshProUGUI CrearTextoDeSubtitulo(string nombre, RectTransform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var texto = go.GetComponent<TextMeshProUGUI>();
        texto.font = _textoBocadillo.font;
        texto.fontSize = _textoBocadillo.fontSize;
        texto.color = Color.white;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.raycastTarget = false;
        texto.outlineColor = new Color32(0, 0, 0, 220);
        texto.outlineWidth = 0.15f;
        return texto;
    }

    float TamanoDeSubtitulo(PresentacionDeTexto presentacion)
    {
        float altura = BandasDeCineUI.Instance != null && BandasDeCineUI.Instance.AlturaObjetivo > 0f
            ? BandasDeCineUI.Instance.AlturaObjetivo : 0.12f;
        float normal = Mathf.Min(_textoBocadillo.fontSize, Mathf.Max(1f, (_canvasSubtitulo.rect.height * altura - 12f) / 3.5f));
        return normal * (presentacion == PresentacionDeTexto.SubtituloGrande ? 2.2f : 1f);
    }

    void ColocarSubtitulo()
    {
        bool grande = _presentacion == PresentacionDeTexto.SubtituloGrande;
        var bandas = BandasDeCineUI.Instance;
        float banda = bandas != null ? bandas.AlturaVisible : 0f;
        float altoCanvas = _canvasSubtitulo.rect.height;
        float ancho = _canvasSubtitulo.rect.width * 0.7f;
        float altoTexto = _textoSubtitulo.fontSize * 2.5f;
        float altoNombre = string.IsNullOrEmpty(_nombreSubtitulo.text) ? 0f : _nombreSubtitulo.fontSize * 1.3f;
        float alto = altoTexto + altoNombre + 12f;
        bool dentroDeBanda = banda * altoCanvas >= alto;
        _rectSubtitulo.anchorMin = _rectSubtitulo.anchorMax = new Vector2(0.5f, grande ? 0.5f : 0f);
        _rectSubtitulo.pivot = new Vector2(0.5f, grande ? 0.5f : 0f);
        _rectSubtitulo.sizeDelta = new Vector2(ancho, alto);
        _rectSubtitulo.anchoredPosition = grande ? Vector2.zero : new Vector2(0f, dentroDeBanda ? (altoCanvas * banda - alto) * 0.5f : altoCanvas * 0.025f);
        _textoSubtitulo.rectTransform.anchorMin = _textoSubtitulo.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        _textoSubtitulo.rectTransform.pivot = new Vector2(0.5f, 0f);
        _textoSubtitulo.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        _textoSubtitulo.rectTransform.sizeDelta = new Vector2(ancho, altoTexto);
        _nombreSubtitulo.rectTransform.anchorMin = _nombreSubtitulo.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        _nombreSubtitulo.rectTransform.pivot = new Vector2(0.5f, 0f);
        _nombreSubtitulo.rectTransform.anchoredPosition = new Vector2(0f, altoTexto + 6f);
        _nombreSubtitulo.rectTransform.sizeDelta = new Vector2(ancho, altoNombre);
        _franjaSubtitulo.color = new Color(0f, 0f, 0f, grande || dentroDeBanda ? 0f : 0.45f);
    }

    /// El ancho del bocadillo para un texto: el que ocuparía en una línea, entre el mínimo y el
    /// máximo (ver el comentario largo de Show()). El alto lo recalcula el layout del prefab.
    void AjustarAncho(string texto)
    {
        if (_presentacion != PresentacionDeTexto.Bocadillo) { ColocarSubtitulo(); return; }
        Vector2 singleLineSize = _label.GetPreferredValues(texto, 0f, 0f);
        float desiredWidth = singleLineSize.x + _labelHorizontalMargin * 2f;
        float bubbleWidth = Mathf.Clamp(desiredWidth, _bubbleMinWidth, _bubbleMaxWidth);

        Vector2 sd = _bubbleRect.sizeDelta;
        sd.x = bubbleWidth;
        _bubbleRect.sizeDelta = sd;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_bubbleRect);
    }

    /// Las páginas de una frase larga, una detrás de otra en el mismo bocadillo: cambia el texto
    /// con un pequeño rebote (no se cierra y se vuelve a abrir). El tiempo total se reparte según
    /// lo largo de cada página, y ninguna dura menos de lo que se tarda en leerla. Sin duración
    /// (duration <= 0, el bocadillo lo cierra quien lo abrió) las páginas pasan solas a ritmo de
    /// lectura y la última se queda.
    IEnumerator Co_Paginas(List<string> paginas, float duration, Action onComplete)
    {
        int total = 0;
        foreach (var p in paginas) total += Mathf.Max(1, p.Length);

        for (int i = 0; i < paginas.Count; i++)
        {
            if (i > 0)
            {
                _label.text = paginas[i];
                AjustarAncho(paginas[i]);
                _bubbleRect.DOKill();
                _bubbleRect.localScale = Vector3.one;
                if (_presentacion == PresentacionDeTexto.Bocadillo)
                    _bubbleRect.DOPunchScale(Vector3.one * 0.06f, 0.2f, 6, 0.6f).SetUpdate(true);
            }

            bool ultima = i == paginas.Count - 1;
            float reparto = duration > 0f ? duration * Mathf.Max(1, paginas[i].Length) / total : 0f;
            float tiempo = Mathf.Max(reparto, TiempoDeLectura(paginas[i]));
            EmpezarHablaDePagina(paginas[i], ultima && duration <= 0f ? 0f : tiempo);
            if (ultima && duration <= 0f) { _autoHideRoutine = null; yield break; }

            yield return new WaitForSecondsRealtime(tiempo);
        }

        _autoHideRoutine = null;
        Hide();
        _pendingOnComplete = null;
        onComplete?.Invoke();
    }

    /// Fuerza el cierre inmediato del bocadillo con auto-hide en curso, como si su duración ya
    /// hubiera terminado: para el temporizador, lo oculta y dispara el mismo onComplete que se le
    /// pasó a Show() — así quien esperaba ese callback (típicamente ShowSpeechBubbleNode, para
    /// avanzar el grafo narrativo) lo recibe igual, solo que antes. No-op seguro si no hay ningún
    /// bocadillo con auto-hide en curso (duration <= 0, o ya se ocultó solo). Pensado para el botón
    /// global de "saltar" — ver ShowSpeechBubbleNode.RegisterSkipHandler.
    public void SkipCurrent()
    {
        SenalesDeHabla.Para(_target);
        if (_autoHideRoutine == null) return;
        StopCoroutine(_autoHideRoutine);
        _autoHideRoutine = null;
        var callback = _pendingOnComplete;
        _pendingOnComplete = null;
        Hide();
        callback?.Invoke();
    }

    void EmpezarHablaDePagina(string pagina, float duracion)
    {
        float segundos = (pagina?.Length ?? 0) / Mathf.Max(1f, _caracteresHabladosPorSegundo);
        if (segundos > 0f) SenalesDeHabla.Empieza(_target, duracion > 0f ? Mathf.Min(duracion, segundos) : segundos);
        else SenalesDeHabla.Para(_target);
    }

    /// Cierra el bocadillo solo si sigue siendo el de ese turno: quien limpia lo suyo no se lleva
    /// por delante el bocadillo que otro sistema haya puesto después.
    public void Hide(int turno)
    {
        if (turno == _turno) Hide();
    }

    public void Hide()
    {
        SenalesDeHabla.Para(_target);
        if (_autoHideRoutine != null) { StopCoroutine(_autoHideRoutine); _autoHideRoutine = null; }
        _isShowing = false;

        _rootGroup.DOKill();
        _bubbleRect.DOKill();

        _rootGroup.DOFade(0f, _fadeOutDuration).SetUpdate(true);
        _bubbleRect.DOScale(_presentacion == PresentacionDeTexto.Bocadillo ? Vector3.zero : Vector3.one, _popOutDuration)
                   .SetEase(Ease.InBack)
                   .SetUpdate(true);
    }

    // ── MenuManager (pausa / cualquier menú) ────────────────────────────────────

    /// <summary>Oculta el bocadillo mientras haya un menú (pausa incluida) abierto encima.</summary>
    void OnMenuOpened(MenuKind kind)
    {
        if (!_isShowing || _hiddenByMenu || _rootGroup == null) return;
        _hiddenByMenu = true;
        _rootGroup.DOKill();
        _rootGroup.DOFade(0f, 0.15f).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }

    /// <summary>Restaura el bocadillo al cerrarse el último menú abierto, si seguía en pantalla.</summary>
    void OnMenuClosed(MenuKind kind)
    {
        if (!_hiddenByMenu) return;
        if (MenuManager.AnyOpen()) return; // todavía queda otro menú abierto
        _hiddenByMenu = false;

        if (!_isShowing || _rootGroup == null) return; // se ocultó por otro motivo mientras tanto
        _rootGroup.DOKill();
        _rootGroup.DOFade(1f, 0.2f).SetUpdate(true);
        _rootGroup.blocksRaycasts = false; // este bocadillo nunca bloquea clics, ver Awake
    }

    // ── Interno ───────────────────────────────────────────────────────────────

    IEnumerator AutoHide(float duration, Action onComplete)
    {
        yield return new WaitForSecondsRealtime(duration);
        Hide();
        _autoHideRoutine = null;
        _pendingOnComplete = null;
        onComplete?.Invoke();
    }

    Animator GetCachedAnimator(Transform target)
    {
        if (!_animatorCache.TryGetValue(target, out Animator anim))
        {
            anim = target.GetComponentInChildren<Animator>();
            _animatorCache[target] = anim;
        }
        return anim;
    }

    NPCSimpleAnimator GetCachedNPCAnimator(Transform target)
    {
        if (!_npcAnimCache.TryGetValue(target, out NPCSimpleAnimator npcAnim))
        {
            npcAnim = target.GetComponentInChildren<NPCSimpleAnimator>();
            _npcAnimCache[target] = npcAnim;
        }
        return npcAnim;
    }

    PlayerDialogueAnimator GetCachedPlayerAnimator(Transform target)
    {
        if (!_playerAnimCache.TryGetValue(target, out PlayerDialogueAnimator playerAnim))
        {
            playerAnim = target.GetComponentInChildren<PlayerDialogueAnimator>();
            _playerAnimCache[target] = playerAnim;
        }
        return playerAnim;
    }
}
