using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Bocadillo de cómic flotante que sigue a un personaje en espacio de pantalla.
/// Vive en el Canvas persistente (Start.unity). Singleton.
/// </summary>
public class SpeechBubbleUI : MonoBehaviour
{
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

    [Header("Posición")]
    [SerializeField] Vector3 _worldOffset = new Vector3(0f, 2.2f, 0f);

    Image _bubbleImage;
    Camera _cam;
    Transform _target;
    bool _isShowing;
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

        if (_parentCanvasRect == null)
            _parentCanvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();

        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = false;
        _bubbleRect.localScale = Vector3.zero;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Mismo sistema que ya usan BossHealthBar/MinimapController: ocultarse mientras
        // hay un menú abierto (pausa incluida) y restaurarse al cerrar el último.
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => _cam = Camera.main;

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _rootGroup?.DOKill();
        _bubbleRect?.DOKill();
    }

    void LateUpdate()
    {
        if (!_isShowing || _target == null || _cam == null || _parentCanvasRect == null) return;

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
    /// Muestra el bocadillo sobre <paramref name="target"/>.
    /// </summary>
    /// <param name="animTrigger">Trigger del Animator del personaje a disparar mientras habla.</param>
    /// <param name="emphasis">Si true, usa el sprite de énfasis (ej: burbuja explosiva).</param>
    /// <param name="speakerName">
    /// Nombre del personaje que habla (ej: "Will", "Estela"). AÑADIDO (Agosto 2026) para
    /// desambiguar quién habla con varios NPCs juntos (taberna), probado como texto dentro del
    /// propio bocadillo. REVERTIDO (Agosto 2026, mismo mes): un bocadillo de cómic no lleva
    /// nombres escritos — la desambiguación correcta es que el PICO señale a quien habla (ver
    /// fix del pico más abajo). Se mantiene el parámetro sin usarlo en el texto (todas las
    /// llamadas ya lo pasan) por si en el futuro sirve para otra cosa, pero ya no se antepone al
    /// texto — no tocar esto de nuevo sin que el usuario lo pida explícitamente.
    /// </param>
    /// <param name="worldOffset">
    /// Sustituye, solo para esta llamada, el offset vertical de siempre (_worldOffset, 2,2 m).
    /// Null (por defecto) = comportamiento de siempre. Pensado para SayBeat.markName (17 sep
    /// 2026): una marca de posición ya colocada a la altura que toca no debería tener que
    /// enterrarse 2,2 m bajo el suelo solo para compensar el offset pensado para cabezas de
    /// personaje.
    /// </param>
    public void Show(Transform target, string text, float duration = 0f,
                     Action onComplete = null, string animTrigger = null, bool emphasis = false,
                     string speakerName = null, Vector3? worldOffset = null)
    {
        if (_autoHideRoutine != null) { StopCoroutine(_autoHideRoutine); _autoHideRoutine = null; }

        _target = target;
        _offsetOverride = worldOffset;
        _isShowing = true;

        // Frases de más de _maxLineas líneas: páginas (INC-435).
        List<string> paginas = Paginar(text);
        if (paginas.Count == 0) paginas.Add(text ?? string.Empty);
        string primera = paginas[0];
        _label.text = primera;

        GameplayEventLog.Log("Dialogo", !string.IsNullOrEmpty(speakerName) ? speakerName : target != null ? target.name : null);

        // Centrado forzado siempre (horizontal Y vertical): no depender solo del valor por
        // defecto del prefab, que podría cambiar sin querer en una variante o en una edición
        // futura del prefab. El bocadillo de cómic siempre debe leerse centrado.
        _label.alignment = TextAlignmentOptions.Center;

        // Margen horizontal para que el texto no roque los bordes del bocadillo
        _label.margin = new Vector4(_labelHorizontalMargin, _label.margin.y,
                                    _labelHorizontalMargin, _label.margin.w);

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

        if (_bubbleImage != null)
        {
            Sprite sprite = emphasis && _emphasisSprite != null ? _emphasisSprite : _defaultSprite;
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
        _bubbleRect.localScale = Vector3.zero;

        _rootGroup.DOFade(1f, _fadeInDuration).SetUpdate(true);
        _bubbleRect.DOScale(Vector3.one, _popInDuration)
                   .SetEase(Ease.OutBack)
                   .SetUpdate(true);

        _pendingOnComplete = onComplete;
        if (paginas.Count > 1)
            _autoHideRoutine = StartCoroutine(Co_Paginas(paginas, duration, onComplete));
        else if (duration > 0f)
            _autoHideRoutine = StartCoroutine(AutoHide(Mathf.Max(duration, TiempoDeLectura(primera)), onComplete));
    }

    /// Cuánto tiene que estar en pantalla una página para poder leerla (INC-435).
    public float TiempoDeLectura(string pagina)
        => Mathf.Max(_lecturaMinima, 0.6f + (pagina?.Length ?? 0) / Mathf.Max(1f, _caracteresPorSegundo));

    /// Parte un texto en páginas de como mucho `_maxLineas` líneas, medidas con el bocadillo de
    /// verdad (fuente, tamaño, márgenes y ancho máximo). Corta por el final de una frase (. ! ? …)
    /// siempre que pueda; si una frase sola ya no cabe, por palabras. Un texto que cabe entero
    /// devuelve una sola página, igual que antes. Lo usa Show() y también SayBeat, que da a cada
    /// página su propio tiempo (INC-435).
    public List<string> Paginar(string text)
    {
        var paginas = new List<string>();
        if (string.IsNullOrWhiteSpace(text) || _label == null || _bubbleRect == null)
        {
            if (!string.IsNullOrWhiteSpace(text)) paginas.Add(text.Trim());
            return paginas;
        }
        text = text.Trim();

        PrepararLabel();
        float anchoGuardado = _bubbleRect.sizeDelta.x;
        AjustarAncho(text);
        float ancho = _label.rectTransform.rect.width - _label.margin.x - _label.margin.z;
        if (ancho < 50f) ancho = _bubbleMaxWidth - _labelHorizontalMargin * 2f;
        float altoMaximo = _label.GetPreferredValues(RenglonesDePrueba(_maxLineas), ancho, 0f).y + 0.5f;
        bool Cabe(string s) => _label.GetPreferredValues(s, ancho, 0f).y <= altoMaximo;

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

        // El ancho de verdad lo pone Show() con la página que toque; aquí solo se ha medido.
        Vector2 sd = _bubbleRect.sizeDelta; sd.x = anchoGuardado; _bubbleRect.sizeDelta = sd;
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

    void PrepararLabel()
    {
        _label.alignment = TextAlignmentOptions.Center;
        _label.margin = new Vector4(_labelHorizontalMargin, _label.margin.y,
                                    _labelHorizontalMargin, _label.margin.w);
        _label.textWrappingMode = TextWrappingModes.Normal;
    }

    /// El ancho del bocadillo para un texto: el que ocuparía en una línea, entre el mínimo y el
    /// máximo (ver el comentario largo de Show()). El alto lo recalcula el layout del prefab.
    void AjustarAncho(string texto)
    {
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
                _bubbleRect.DOPunchScale(Vector3.one * 0.06f, 0.2f, 6, 0.6f).SetUpdate(true);
            }

            bool ultima = i == paginas.Count - 1;
            if (ultima && duration <= 0f) { _autoHideRoutine = null; yield break; }

            float reparto = duration > 0f ? duration * Mathf.Max(1, paginas[i].Length) / total : 0f;
            yield return new WaitForSecondsRealtime(Mathf.Max(reparto, TiempoDeLectura(paginas[i])));
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
        if (_autoHideRoutine == null) return;
        StopCoroutine(_autoHideRoutine);
        _autoHideRoutine = null;
        var callback = _pendingOnComplete;
        _pendingOnComplete = null;
        Hide();
        callback?.Invoke();
    }

    public void Hide()
    {
        if (_autoHideRoutine != null) { StopCoroutine(_autoHideRoutine); _autoHideRoutine = null; }
        _isShowing = false;

        _rootGroup.DOKill();
        _bubbleRect.DOKill();

        _rootGroup.DOFade(0f, _fadeOutDuration).SetUpdate(true);
        _bubbleRect.DOScale(Vector3.zero, _popOutDuration)
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
