using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Core;
using Core.InputGlyphs;

/// <summary>
/// Amplía el minimapa circular a un "mapa grande" centrado en pantalla al pulsar el botón
/// ToggleBigMap (tecla M en teclado, botón Select/View/Back/"-" del mando — ver
/// PlayerControls.inputactions e InputGlyphNames.Select). Reutiliza el mismo RectTransform
/// "MinimapRoot" que ya usan MinimapController/MinimapUIController (mismo RawImage, máscara
/// circular, marcadores y flecha del jugador) en vez de crear una UI paralela: solo cambia su
/// ancla/posición/escala y el zoom de la cámara del minimapa mientras está abierto.
///
/// Se registra en <see cref="MenuManager"/> como <see cref="MenuKind.BigMap"/> siguiendo el mismo
/// patrón que <see cref="QuestMenuManager"/>: bloquea que se abra si hay otro menú activo, y usa el
/// mismo InputScope (PushUIMode + PushGameplaySuppression) para congelar el movimiento del jugador
/// mientras el mapa grande está en pantalla.
///
/// Controles con el mapa abierto: stick, cruceta o WASD/flechas para moverlo; LB/RB o la rueda
/// para alejar/acercar; arrastrar con el ratón para moverlo; Confirmar para volver a centrarlo en
/// el jugador; Cancelar o el mismo botón del mapa para cerrarlo. Una línea de ayuda bajo el mapa
/// muestra esos controles con los glifos del dispositivo activo (clave BIGMAP_HINT de other_*.json).
/// </summary>
[DisallowMultipleComponent]
public class BigMapController : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsOpen = false;
#endif

    [Header("Referencias")]
    [Tooltip("RectTransform \"MinimapRoot\" — el mismo que usan MinimapController/MinimapUIController.")]
    [SerializeField] RectTransform minimapRoot;
    [SerializeField] MinimapController minimapController;

    [Header("Mapa grande")]
    [Tooltip("Multiplicador de escala del minimapa al abrir el mapa grande.")]
    [SerializeField] float bigScale = 3f;

    [Header("Controles")]
    [Tooltip("Velocidad al mover el mapa con el stick o las teclas, en radios de mapa por segundo.")]
    [SerializeField] float panSpeed = 1.2f;

    [Tooltip("Velocidad de zoom al mantener LB/RB: factor por segundo.")]
    [SerializeField] float zoomSpeed = 2f;

    [Tooltip("Cuánto acerca o aleja cada paso de la rueda del ratón (0.15 = 15 %).")]
    [Range(0.01f, 0.5f)]
    [SerializeField] float wheelZoomStep = 0.15f;

    [Header("Ayuda de controles")]
    [Tooltip("Fuente del texto de ayuda bajo el mapa grande. Vacío = la fuente por defecto de TextMeshPro.")]
    [SerializeField] TMP_FontAsset hintFont;

    [Tooltip("Tamaño del texto de ayuda.")]
    [SerializeField] float hintFontSize = 26f;

    [Tooltip("Distancia de la ayuda al borde inferior de la pantalla (px).")]
    [SerializeField] float hintBottomMargin = 36f;

    [Tooltip("Color del fondo de la ayuda.")]
    [SerializeField] Color hintBackground = new Color(0f, 0f, 0f, 0.6f);

    const string HintKey = "BIGMAP_HINT";
    const string HintFallback =
        "<sprite name=\"interactable_Joystick\"> Mover    " +
        "<sprite name=\"interactable_lb\"><sprite name=\"interactable_rb\"> Zoom    " +
        "<sprite name=\"interactable_confirm\"> Centrar    " +
        "<sprite name=\"interactable_select\"> Cerrar";

    // Estado original de minimapRoot, para restaurarlo exactamente al cerrar.
    Vector2 _originalAnchorMin;
    Vector2 _originalAnchorMax;
    Vector2 _originalPivot;
    Vector2 _originalAnchoredPosition;
    Vector3 _originalScale;
    bool _originalStateCaptured;

    InputScope _inputScope;
    GameObject _hintRoot;
    TextMeshProUGUI _hintLabel;
    PlayerControls _controls;
    bool _dragging;
    Vector2 _lastPointer;

    void Awake()
    {
        CaptureOriginalState();
        BuildHint();
    }

    void OnEnable()
    {
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleInput;
        InputGlyphService.FamilyChanged += HandleGlyphFamilyChanged;
    }

    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleInput;
        InputGlyphService.FamilyChanged -= HandleGlyphFamilyChanged;

        // Si este componente se desactiva mientras el mapa grande está abierto (cambio de escena,
        // minimapa oculto por interior/batalla, etc.), forzar el cierre para no dejar el input de
        // gameplay suprimido ni el registro de MenuManager huérfano.
        if (IsOpen)
            Close();
    }

    void CaptureOriginalState()
    {
        if (_originalStateCaptured || minimapRoot == null) return;

        _originalAnchorMin = minimapRoot.anchorMin;
        _originalAnchorMax = minimapRoot.anchorMax;
        _originalPivot = minimapRoot.pivot;
        _originalAnchoredPosition = minimapRoot.anchoredPosition;
        _originalScale = minimapRoot.localScale;
        _originalStateCaptured = true;
    }

    void HandleInput(GamepadInputReader.InputEvent input)
    {
        if (input.Phase != InputActionPhase.Performed) return;

        switch (input.Type)
        {
            case GamepadInputReader.InputEventType.ToggleBigMap:
                Toggle();
                break;
            case GamepadInputReader.InputEventType.Cancel:
                if (IsOpen) Close();
                break;
            case GamepadInputReader.InputEventType.Submit:
                if (IsOpen && minimapController != null) minimapController.RecenterBigMap();
                break;
        }
    }

    void Update()
    {
        if (!IsOpen || minimapController == null) return;

        float dt = Time.unscaledDeltaTime;

        // Mover: sticks, cruceta o teclas.
        Vector2 nav = GamepadInputReader.Navigation;
        if (nav.sqrMagnitude > 0.01f)
            minimapController.PanBigMap(nav * (minimapController.OrthoSize * panSpeed * dt));

        var controls = ResolveControls();
        if (controls == null) return;
        var ui = controls.UI;

        // Mover arrastrando con el ratón.
        if (ui.Click.IsPressed())
        {
            Vector2 pointer = ui.Point.ReadValue<Vector2>();
            if (_dragging) minimapController.PanBigMapByScreenDelta(_lastPointer - pointer);
            _lastPointer = pointer;
            _dragging = true;
        }
        else
        {
            _dragging = false;
        }

        // Zoom: rueda del ratón por pasos; LB (aleja) / RB (acerca) mientras se mantienen.
        float wheel = ui.ScrollWheel.ReadValue<Vector2>().y;
        if (wheel > 0f)
            minimapController.ZoomBigMap(1f - wheelZoomStep);
        else if (wheel < 0f)
            minimapController.ZoomBigMap(1f + wheelZoomStep);
        else
        {
            float zoomDir = (IsHeldOnGamepad(ui.LB) ? 1f : 0f) - (IsHeldOnGamepad(ui.RB) ? 1f : 0f);
            if (zoomDir != 0f)
                minimapController.ZoomBigMap(Mathf.Pow(zoomSpeed, zoomDir * dt));
        }
    }

    // LB/RB también están enlazados a la rueda del ratón; esa parte ya la cubre ScrollWheel.
    static bool IsHeldOnGamepad(InputAction action) =>
        action.IsPressed() && !(action.activeControl?.device is Mouse);

    PlayerControls ResolveControls()
    {
        if (_controls == null && ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            _controls = pim.Controls;
        return _controls;
    }

    void Toggle()
    {
        if (IsOpen) Close();
        else TryOpen();
    }

    void TryOpen()
    {
        if (IsOpen || minimapRoot == null) return;

        // Mismas comprobaciones conservadoras que el resto de menús (QuestMenuManager, ShopUI...):
        // no abrir encima de otro menú, diálogo o si el juego no permite abrir "inventario" ahora
        // mismo (cinemática, combate bloqueante, etc.).
        if (MenuManager.AnyOpenExcept(MenuKind.BigMap)) return;
        if (!GameState.CanOpenInventory) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;

        CaptureOriginalState();

        minimapRoot.anchorMin = new Vector2(0.5f, 0.5f);
        minimapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        minimapRoot.pivot = new Vector2(0.5f, 0.5f);
        minimapRoot.anchoredPosition = Vector2.zero;
        minimapRoot.localScale = _originalScale * bigScale;

        if (minimapController != null)
            minimapController.SetBigMapMode(true, bigScale);

        IsOpen = true;
        ShowHint(true);
        MenuManager.TryOpen(MenuKind.BigMap);
        EnsureInputScope();

        GamepadInputReader.PlayUISound("UI_Open");
    }

    void Close()
    {
        if (!IsOpen) return;

        if (minimapRoot != null && _originalStateCaptured)
        {
            minimapRoot.anchorMin = _originalAnchorMin;
            minimapRoot.anchorMax = _originalAnchorMax;
            minimapRoot.pivot = _originalPivot;
            minimapRoot.anchoredPosition = _originalAnchoredPosition;
            minimapRoot.localScale = _originalScale;
        }

        if (minimapController != null)
            minimapController.SetBigMapMode(false, 1f);

        IsOpen = false;
        _dragging = false;
        ShowHint(false);
        MenuManager.Close(MenuKind.BigMap);
        ExitInputScope();

        GamepadInputReader.PlayUISound("UI_Cancel");
    }

    // ── Ayuda de controles ───────────────────────────────────────────────────

    /// <summary>
    /// Crea la línea de ayuda como hermana de minimapRoot (en el mismo Canvas), para que no se
    /// amplíe con el mapa. Queda oculta hasta que se abre el mapa grande.
    /// </summary>
    void BuildHint()
    {
        if (_hintRoot != null || minimapRoot == null || minimapRoot.parent == null) return;

        _hintRoot = new GameObject("BigMapHint", typeof(RectTransform));
        var rootRt = (RectTransform)_hintRoot.transform;
        rootRt.SetParent(minimapRoot.parent, false);
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0f);
        rootRt.pivot = new Vector2(0.5f, 0f);
        rootRt.anchoredPosition = new Vector2(0f, hintBottomMargin);

        var background = _hintRoot.AddComponent<Image>();
        background.color = hintBackground;
        background.raycastTarget = false;

        var layout = _hintRoot.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var fitter = _hintRoot.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var labelGo = new GameObject("Texto", typeof(RectTransform));
        labelGo.transform.SetParent(rootRt, false);
        _hintLabel = labelGo.AddComponent<TextMeshProUGUI>();
        if (hintFont != null) _hintLabel.font = hintFont;
        _hintLabel.fontSize = hintFontSize;
        _hintLabel.color = Color.white;
        _hintLabel.alignment = TextAlignmentOptions.Center;
        _hintLabel.textWrappingMode = TextWrappingModes.NoWrap;
        _hintLabel.raycastTarget = false;

        _hintRoot.SetActive(false);
    }

    void ShowHint(bool show)
    {
        if (_hintRoot == null) return;
        if (show) RefreshHintText();
        if (_hintRoot.activeSelf != show) _hintRoot.SetActive(show);
        if (show) _hintRoot.transform.SetAsLastSibling();
    }

    void RefreshHintText()
    {
        if (_hintLabel == null) return;

        InputGlyphService.UsarIconos(_hintLabel);

        _hintLabel.text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(HintKey, HintFallback)
            : HintFallback;
        _hintLabel.SetAllDirty();
    }

    // Los glifos cambian de imagen al cambiar de mando/teclado; el texto hay que volver a generarlo.
    void HandleGlyphFamilyChanged(InputGlyphDeviceFamily _)
    {
        if (IsOpen) RefreshHintText();
    }

    void EnsureInputScope()
    {
        if (_inputScope != null) return;
        _inputScope = InputScope.Enter();
    }

    void ExitInputScope()
    {
        _inputScope?.Dispose();
        _inputScope = null;
    }

    /// <summary>
    /// Mismo patrón que QuestMenuManager.InputScope: congela el movimiento del jugador
    /// (PushGameplaySuppression) y pasa el input a modo UI centralizado (PushUIMode) mientras el
    /// mapa grande está abierto. Cancel/ToggleBigMap siguen llegando igualmente porque
    /// GamepadInputReader.ShouldSuppress solo filtra el D-Pad durante la supresión de gameplay.
    /// </summary>
    sealed class InputScope : IDisposable
    {
        bool _disposed;

        InputScope()
        {
            GamepadInputReader.PushGameplaySuppression(this);

            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PushUIMode();
        }

        public static InputScope Enter() => new InputScope();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PopUIMode();

            GamepadInputReader.PopGameplaySuppression(this);
        }
    }
}
