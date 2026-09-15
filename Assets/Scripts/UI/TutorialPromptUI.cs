using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Core.InputGlyphs;

/// <summary>
/// Línea de tutorial in-world: icono de botón opcional + texto. No bloquea la acción.
/// Vive en el Canvas persistente (Start.unity). Singleton.
/// </summary>
public class TutorialPromptUI : MonoBehaviour
{
    public static TutorialPromptUI Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    [Header("Referencias UI")]
    [SerializeField] CanvasGroup _rootGroup;
    [SerializeField] GameObject _iconContainer;
    [SerializeField] Image _icon;
    [SerializeField] TextMeshProUGUI _label;

    [Header("Animación")]
    [SerializeField] float _fadeInDuration = 0.3f;
    [SerializeField] float _fadeOutDuration = 0.25f;

    // Plantilla de texto activa (puede contener el token {BOTON}) y el nombre de glifo
    // (InputGlyphNames) usados para recalcular texto+icono en caliente. Antes Show() fijaba texto e
    // icono UNA sola vez y se quedaban obsoletos si el jugador cambiaba de mando/teclado con el
    // prompt ya visible (p.ej. soltar el mando y tocar una tecla a mitad de un "Pulsa A...").
    const string ButtonToken = "{BOTON}";
    string _textTemplate;
    string _buttonName;
    Sprite _fallbackIcon;

    // Si hay un Show() activo (independiente del alpha real, que puede estar en 0 mientras
    // _hiddenByMenu/_hiddenByDialogue lo tiene tapado) y si lo ocultamos temporalmente por un
    // menú o un diálogo abierto encima. Son dos flags independientes (no una sola "tapado por
    // algo") porque un diálogo puede empezar y terminar mientras un menú sigue abierto o
    // viceversa — solo se restaura el prompt cuando NINGUNO de los dos sigue tapándolo.
    bool _isShowing;
    bool _hiddenByMenu;
    bool _hiddenByDialogue;

    // Si el aviso activo permite cerrarse desde este botón de la esquina (allowManualClose de
    // Show()). Se guarda aparte del contenido real del botón porque este último también depende
    // de la familia de dispositivo activa (ver RefreshCloseButton): en KeyboardMouse se ve la X de
    // toda la vida (clic de ratón); en mando, al no haber cursor virtual en el proyecto, se
    // sustituye por el icono del botón Cancelar real. INC-201b (15 sept 2026, Raúl): primero la X
    // se quedaba fija sin adaptarse al cambiar de mando/teclado; después, al ocultarla sin más en
    // mando, el jugador se quedaba sin ninguna pista de cómo cerrar el aviso.
    bool _allowManualClose;

    // ── Botón de cerrar (X, esquina superior derecha) ───────────────────────
    //
    // Petición de Raúl (15 sept 2026): los avisos de tutorial se cerraban con el mismo botón que
    // avanza diálogo (Confirmar/A/Espacio) — si el aviso aparecía justo después de una conversación,
    // el jugador lo cerraba sin querer, por inercia, sin llegar a leerlo. Solución de dos partes:
    // TutorialPromptNode puede pedir cerrar con Cancelar (mando) en vez de Confirmar — ver
    // dismissWithCancel ahí — y, para PC/ratón, este botón "X" que aparece en la esquina superior
    // derecha del aviso, como el cierre de un popup normal.
    //
    // Se construye por código en Awake, igual que el patrón ya usado para el botón "X" de
    // BugReportFlyoutPanel/CreditsFlyoutPanel/PatchNotesFlyoutPanel.cs — no hace falta editar
    // TutorialPromptUI.prefab a mano en el Editor para añadirlo.
    [Header("Botón de cerrar (X) — solo clic de ratón, ver TutorialPromptNode.dismissWithCancel")]
    [SerializeField] float _closeButtonSize = 28f;
    [SerializeField] float _closeButtonMargin = 6f;

    Button _closeButton;
    TextMeshProUGUI _closeLabel;
    Image _closeIcon;

    /// <summary>Se dispara al hacer clic en la X. TutorialPromptNode se suscribe solo cuando el
    /// aviso activo usa dismissWithCancel — el resto de avisos no muestran la X (ver Show()).</summary>
    public event Action CloseButtonClicked;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = false;

        BuildCloseButton();
    }

    void OnEnable()
    {
        InputGlyphService.FamilyChanged += HandleFamilyChanged;

        // INC-076: el prompt de tutorial ("Usa {BOTON} para mover a Will...") no se ocultaba al
        // abrir el menú de pausa, a diferencia del resto de la UI in-world (bocadillos, barra de
        // vida de jefe, minimapa...), que ya usa este mismo sistema. Mismo patrón que
        // SpeechBubbleUI/BossHealthBar/MinimapController: ocultarse mientras haya un menú abierto
        // (pausa incluida) y restaurarse al cerrar el último.
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;

        // INC (12 sep 2026, captura de Raúl): el prompt de movimiento ("Usa WASD para mover a
        // Will. Inspecciona la habitación.") se quedaba superpuesto al mensaje bloqueante de
        // RoomExitBlocker ("Antes de salir necesitas leer la carta que hay sobre la mesita.") al
        // intentar salir de la habitación sin haber leído la carta. RoomExitBlocker muestra ese
        // mensaje vía DialogueManager.StartDialogue(), un sistema totalmente independiente de
        // MenuManager — este prompt no estaba suscrito a los eventos de diálogo, así que el aviso
        // se dibujaba encima en vez de taparlo. Mismo patrón ocultar/restaurar que con los menús,
        // con su propio flag (_hiddenByDialogue) para no interferir si además hay un menú abierto.
        DialogueManager.OnDialogueStarted += HandleDialogueStarted;
        DialogueManager.OnDialogueClosed += HandleDialogueClosed;
    }

    void OnDisable()
    {
        InputGlyphService.FamilyChanged -= HandleFamilyChanged;
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
        DialogueManager.OnDialogueStarted -= HandleDialogueStarted;
        DialogueManager.OnDialogueClosed -= HandleDialogueClosed;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            _rootGroup?.DOKill();
        }
    }

    void HandleFamilyChanged(InputGlyphDeviceFamily _)
    {
        // Solo recalcular si el prompt activo usa resolución dinámica (Show con buttonName) — el
        // Show(text, icon) "de toda la vida" deja _textTemplate a null y no debe tocarse aquí.
        if (_textTemplate != null) RefreshContent();

        // La X (clic de ratón) solo tiene sentido en KeyboardMouse — recalcular su visibilidad
        // cada vez que cambia el dispositivo activo, para que se oculte sola al pasar a mando
        // (y reaparezca al volver a teclado/ratón) en vez de quedarse fija a lo que había cuando
        // se llamó a Show().
        RefreshCloseButton();
    }

    // ── API pública ───────────────────────────────────────────────────────

    public bool IsVisible => gameObject.activeSelf && _rootGroup.alpha > 0f;

    [ContextMenu("TEST Show")]
    void TestShow() => Show("Pulsa {BOTON} para despertar", InputGlyphNames.Confirm);

    [ContextMenu("TEST Hide")]
    void TestHide() => Hide();

    /// <summary>
    /// Prompt con texto e icono FIJOS, sin resolución dinámica por dispositivo. Mantenido por
    /// compatibilidad con llamadas que ya traen su propio sprite resuelto; para prompts que dependen
    /// del botón/tecla real activa (la inmensa mayoría) usar el overload con <c>buttonName</c>.
    /// </summary>
    public void Show(string text, Sprite icon = null, bool allowManualClose = false)
    {
        _textTemplate = null;
        _buttonName = null;
        _fallbackIcon = null;
        _isShowing = true;

        _label.text = text;
        SetIcon(icon);
        _allowManualClose = allowManualClose;
        RefreshCloseButton();
        FadeIn();
    }

    /// <summary>
    /// Prompt dinámico: <paramref name="textTemplate"/> puede contener el token literal "{BOTON}",
    /// que se sustituye por el nombre corto de la tecla/botón real (<see cref="InputGlyphLabels"/>)
    /// según el dispositivo activo; el icono se resuelve con <see cref="InputGlyphService.GetSprite"/>
    /// a partir de <paramref name="buttonName"/> (constantes de <see cref="InputGlyphNames"/>). Si
    /// <paramref name="textTemplate"/> no contiene el token, se muestra tal cual (solo cambia el
    /// icono). Ambos se recalculan solos si el jugador cambia de mando/teclado con el prompt visible.
    /// <paramref name="fallbackIcon"/> se usa únicamente si no hay sprite resuelto para la familia
    /// activa (p.ej. mientras no exista arte de teclado todavía para ese botón concreto).
    /// </summary>
    public void Show(string textTemplate, string buttonName, Sprite fallbackIcon = null, bool allowManualClose = false)
    {
        _textTemplate = textTemplate;
        _buttonName = buttonName;
        _fallbackIcon = fallbackIcon;
        _isShowing = true;

        RefreshContent();
        _allowManualClose = allowManualClose;
        RefreshCloseButton();
        FadeIn();
    }

    public void Hide()
    {
        _isShowing = false;
        _allowManualClose = false;
        _rootGroup.DOKill();
        _rootGroup.DOFade(0f, _fadeOutDuration).SetUpdate(true);
        SetCloseButtonVisible(false);
    }

    // ── MenuManager (pausa / cualquier menú) ────────────────────────────────

    /// <summary>Oculta el prompt de tutorial mientras haya un menú (pausa incluida) abierto encima.</summary>
    void OnMenuOpened(MenuKind kind)
    {
        if (!_isShowing || _hiddenByMenu || _rootGroup == null) return;
        _hiddenByMenu = true;
        _rootGroup.DOKill();
        _rootGroup.DOFade(0f, _fadeOutDuration).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }

    /// <summary>Restaura el prompt al cerrarse el último menú abierto, si seguía activo.</summary>
    void OnMenuClosed(MenuKind kind)
    {
        if (!_hiddenByMenu) return;
        if (MenuManager.AnyOpen()) return; // todavía queda otro menú abierto
        _hiddenByMenu = false;

        if (_hiddenByDialogue) return; // sigue tapado por un diálogo en curso (p.ej. RoomExitBlocker)
        if (!_isShowing || _rootGroup == null) return; // se ocultó por otro motivo (Hide()) mientras tanto
        _rootGroup.DOKill();
        _rootGroup.DOFade(1f, _fadeInDuration).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }

    // ── DialogueManager (mensajes de diálogo, incl. avisos bloqueantes tipo RoomExitBlocker) ──

    /// <summary>Oculta el prompt de tutorial mientras haya un diálogo (incluidos avisos como el de
    /// RoomExitBlocker) en pantalla, para no superponerse con el cuadro de texto.</summary>
    void HandleDialogueStarted(Transform npc)
    {
        if (!_isShowing || _hiddenByDialogue || _rootGroup == null) return;
        _hiddenByDialogue = true;
        _rootGroup.DOKill();
        _rootGroup.DOFade(0f, _fadeOutDuration).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }

    /// <summary>Restaura el prompt al cerrarse el diálogo, si seguía activo y no hay además un menú abierto.</summary>
    void HandleDialogueClosed(Transform npc)
    {
        if (!_hiddenByDialogue) return;
        _hiddenByDialogue = false;

        if (_hiddenByMenu) return; // sigue tapado por un menú abierto
        if (!_isShowing || _rootGroup == null) return;
        _rootGroup.DOKill();
        _rootGroup.DOFade(1f, _fadeInDuration).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }

    // ── Interno ───────────────────────────────────────────────────────────

    void RefreshContent()
    {
        _label.text = ResolveText();

        Sprite resolvedIcon = _fallbackIcon;
        if (!string.IsNullOrEmpty(_buttonName))
        {
            var dynamicIcon = InputGlyphService.GetSprite(_buttonName);
            if (dynamicIcon != null) resolvedIcon = dynamicIcon;
        }
        SetIcon(resolvedIcon);
    }

    string ResolveText()
    {
        if (string.IsNullOrEmpty(_textTemplate)) return _textTemplate;
        if (string.IsNullOrEmpty(_buttonName) || !_textTemplate.Contains(ButtonToken)) return _textTemplate;

        string label = InputGlyphLabels.GetLabel(_buttonName, InputGlyphService.CurrentFamily);
        return _textTemplate.Replace(ButtonToken, label);
    }

    void SetIcon(Sprite icon)
    {
        if (_iconContainer != null) _iconContainer.SetActive(icon != null);
        if (_icon != null) _icon.sprite = icon;
    }

    void FadeIn()
    {
        _rootGroup.DOKill();
        _rootGroup.DOFade(1f, _fadeInDuration).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }

    void SetCloseButtonVisible(bool visible)
    {
        if (_closeButton != null) _closeButton.gameObject.SetActive(visible);
    }

    // La X de toda la vida (texto "X", clic de ratón) solo tiene sentido en KeyboardMouse. En
    // mando no hay cursor virtual en el proyecto para poder pulsarla — así que, en vez de
    // ocultar el botón sin más (versión anterior de este fix, INC-201b), mostramos en su lugar
    // el icono real del botón que SÍ cierra el aviso en mando: Cancelar, que en las 3 familias es
    // físicamente el mismo botón que East/Ataque mágico derecho (B Xbox, Círculo PS, A Switch —
    // ver PlayerControls.inputactions → UI/Cancel y el comentario de dismissWithCancel en
    // TutorialPromptNode). Así el jugador siempre ve QUÉ pulsar, en vez de un hueco vacío sin
    // ninguna pista (reporte de Raúl, 15 sept 2026: "no aparece el boton de cerrar el pop up"
    // jugando con mando).
    void RefreshCloseButton()
    {
        if (_closeButton == null) return;

        _closeButton.gameObject.SetActive(_allowManualClose);
        if (!_allowManualClose) return;

        bool isKeyboardMouse = InputGlyphService.CurrentFamily == InputGlyphDeviceFamily.KeyboardMouse;
        if (isKeyboardMouse)
        {
            if (_closeLabel != null) _closeLabel.gameObject.SetActive(true);
            if (_closeIcon != null) _closeIcon.gameObject.SetActive(false);
            return;
        }

        Sprite cancelSprite = InputGlyphService.GetSprite(InputGlyphNames.East);
        bool hasCancelArt = cancelSprite != null;
        if (_closeIcon != null)
        {
            _closeIcon.sprite = cancelSprite;
            _closeIcon.gameObject.SetActive(hasCancelArt);
        }
        // Sin arte de Cancelar para esta familia todavía: mejor no mostrar ni la X (no se puede
        // pulsar con mando) ni el icono (quedaría en blanco) que confundir con un botón muerto.
        if (_closeLabel != null) _closeLabel.gameObject.SetActive(false);
    }

    void BuildCloseButton()
    {
        var rootRt = (RectTransform)_rootGroup.transform;

        var closeGo = new GameObject("CloseButton", typeof(RectTransform));
        closeGo.transform.SetParent(rootRt, false);
        var closeRt = (RectTransform)closeGo.transform;
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.sizeDelta = new Vector2(_closeButtonSize, _closeButtonSize);
        closeRt.anchoredPosition = new Vector2(-_closeButtonMargin, -_closeButtonMargin);

        closeGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        _closeButton = closeGo.AddComponent<Button>();
        _closeButton.onClick.AddListener(() => CloseButtonClicked?.Invoke());

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(closeRt, false);
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        _closeLabel = labelGo.AddComponent<TextMeshProUGUI>();
        _closeLabel.text = "X";
        _closeLabel.fontSize = _closeButtonSize * 0.6f;
        _closeLabel.color = Color.white;
        _closeLabel.fontStyle = FontStyles.Bold;
        _closeLabel.alignment = TextAlignmentOptions.Center;
        _closeLabel.raycastTarget = false;
        if (_label != null && _label.font != null) _closeLabel.font = _label.font;

        CenterGlyphOnRenderedInk(_closeLabel);

        // Icono alternativo para mando (ver RefreshCloseButton) — mismo rect que el Label de
        // arriba, con un pequeño margen para que el glifo no toque el borde del cuadrado de fondo.
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(closeRt, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        float iconInset = _closeButtonSize * 0.12f;
        iconRt.offsetMin = new Vector2(iconInset, iconInset);
        iconRt.offsetMax = new Vector2(-iconInset, -iconInset);

        _closeIcon = iconGo.AddComponent<Image>();
        _closeIcon.preserveAspect = true;
        _closeIcon.raycastTarget = false;
        iconGo.SetActive(false);

        // Oculto por defecto: solo Show(..., allowManualClose: true) lo activa — en los avisos que
        // piden pulsar el botón de una mecánica real (p.ej. "Pulsa {BOTON} para despertar") cerrar
        // con un clic de ratón se saltaría esa mecánica sin querer.
        closeGo.SetActive(false);
    }

    // Centra un TMP_Text por la tinta que realmente pinta (bounds del mesh generado), no por las
    // métricas de fuente — mismo fix ya aplicado a la "X" de cerrar en
    // BugReportFlyoutPanel/CreditsFlyoutPanel/PatchNotesFlyoutPanel.cs (24 ago 2026): centrar por
    // métricas dejaba el glifo descuadrado según fuente/carácter.
    static void CenterGlyphOnRenderedInk(TextMeshProUGUI label)
    {
        Canvas.ForceUpdateCanvases();
        label.ForceMeshUpdate(true, true);
        Vector3 inkCenter = label.textBounds.center;
        label.rectTransform.anchoredPosition -= new Vector2(inkCenter.x, inkCenter.y);
    }
}
