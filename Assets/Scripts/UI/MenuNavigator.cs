using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Core;

/// <summary>
/// Script simple que da feedback visual al botón seleccionado.
/// Unity EventSystem maneja toda la navegación automáticamente.
/// </summary>
[DisallowMultipleComponent]
public class MenuNavigator : MonoBehaviour
{
    [Header("Animación selección")]
    [Tooltip("Desplazamiento horizontal cuando se selecciona un botón")]
    public float nudge = 6f;
    
    [Tooltip("Duración de la animación")]
    public float nudgeTime = 0.08f;

    [Tooltip("Cursor opcional que acompaña al botón seleccionado.")]
    [SerializeField] RectTransform cursorSeleccion;
    [SerializeField, Min(0f)] float duracionCursor = 0.12f;
    [Tooltip("Estrella opcional que respira mientras el cursor está visible.")]
    [SerializeField] RectTransform destelloDelCursor;

    CanvasGroup _cursorGroup;
    RectTransform _cursorParent;
    Vector2 _cursorOrigin;
    Vector3 _starScale;
    bool _cursorVisible;
    Tween _cursorMove, _cursorFade, _starTween;
    RectTransform _cursorTarget;
    float _cursorTargetY;
    readonly System.Collections.Generic.Dictionary<GameObject, Button> _buttonCache = new();
    readonly System.Collections.Generic.Dictionary<Button, RectTransform> _textCache = new();

    void Awake() { CacheReferences(); }

    void CacheReferences()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        _buttonCache.Clear();
        _textCache.Clear();
        foreach (var button in buttons)
        {
            _buttonCache[button.gameObject] = button;
            var text = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            _textCache[button] = text ? text.rectTransform : null;
        }
        if (!cursorSeleccion) return;
        _cursorParent = cursorSeleccion.parent as RectTransform;
        _cursorGroup = cursorSeleccion.GetComponent<CanvasGroup>();
        if (!_cursorGroup) _cursorGroup = cursorSeleccion.gameObject.AddComponent<CanvasGroup>();
        _cursorGroup.interactable = false;
        _cursorGroup.blocksRaycasts = false;
        _cursorOrigin = cursorSeleccion.anchoredPosition;
        if (destelloDelCursor) _starScale = destelloDelCursor.localScale;
        _cursorGroup.alpha = 0f;
    }

    void UpdateCursor(Button button)
    {
        if (!cursorSeleccion || !_cursorParent) return;
        if (!button || !button.transform.IsChildOf(transform))
        {
            _cursorTarget = null;
            if (!_cursorVisible) return;
            _cursorVisible = false;
            _cursorMove?.Kill();
            _cursorFade?.Kill();
            _starTween?.Kill();
            if (destelloDelCursor) destelloDelCursor.localScale = _starScale;
            _cursorFade = _cursorGroup.DOFade(0f, duracionCursor).SetUpdate(true);
            return;
        }
        _cursorTarget = (RectTransform)button.transform;
        _cursorTargetY = AlturaDelObjetivo();
        _cursorMove?.Kill();
        if (!_cursorVisible)
        {
            ColocarCursor(_cursorTargetY);
            _cursorFade?.Kill();
            _cursorGroup.alpha = 1f;
            _cursorVisible = true;
            if (destelloDelCursor)
                _starTween = destelloDelCursor.DOScale(_starScale * 1.12f, 0.6f)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
        }
        else
            _cursorMove = cursorSeleccion.DOLocalMoveY(_cursorTargetY, duracionCursor).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    /// Mantiene el cursor sobre el botón seleccionado aunque este se mueva sin cambiar la
    /// selección (layout que se reconstruye, intro animada, filas que aparecen). Ver INC-631.
    void SeguirAlObjetivo()
    {
        if (!_cursorVisible || !_cursorTarget) return;
        float y = AlturaDelObjetivo();
        if (Mathf.Abs(y - _cursorTargetY) <= 0.5f) return;
        _cursorTargetY = y;
        if (_cursorMove != null && _cursorMove.IsActive() && _cursorMove.IsPlaying())
        {
            _cursorMove.Kill();
            _cursorMove = cursorSeleccion.DOLocalMoveY(y, duracionCursor).SetEase(Ease.OutCubic).SetUpdate(true);
        }
        else
            ColocarCursor(y);
    }

    /// Altura del centro del botón objetivo en el espacio local del padre del cursor.
    float AlturaDelObjetivo() =>
        _cursorParent.InverseTransformPoint(_cursorTarget.TransformPoint(_cursorTarget.rect.center)).y;

    void ColocarCursor(float y)
    {
        var posicion = cursorSeleccion.localPosition;
        posicion.y = y;
        cursorSeleccion.localPosition = posicion;
    }

    [Header("Debug")]
    public bool debugLogs;

    private Button _lastSelected;
    private GameObject _lastSelectedGo;
    private RectTransform _lastNudgedText;

    // Throttle al reintentar selección: SelectFirstButton hace GetComponentsInChildren + Sort.
    private float _nextSelectFirstRetryAt;

    void OnEnable()
    {
        CacheReferences();
        // Seleccionar el primer botón activo e interactable
        Invoke(nameof(SelectFirstButton), 0.1f);
        
        // Suscribirse a eventos de navegación para reproducir sonidos
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleNavigationInput;
    }
    
    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleNavigationInput;
        CancelInvoke(nameof(SelectFirstButton));
        _cursorMove?.Kill();
        _cursorFade?.Kill();
        _starTween?.Kill();
        _cursorMove = _cursorFade = _starTween = null;
        _cursorVisible = false;
        _cursorTarget = null;
        if (cursorSeleccion) cursorSeleccion.anchoredPosition = _cursorOrigin;
        if (_cursorGroup) _cursorGroup.alpha = 0f;
        if (destelloDelCursor) destelloDelCursor.localScale = _starScale;
        
        if (_lastNudgedText != null)
        {
            _lastNudgedText.DOKill();
            _lastNudgedText.anchoredPosition = Vector2.zero;
        }
        _lastNudgedText = null;
        _lastSelected = null;
        _lastSelectedGo = null;
    }
    
    void HandleNavigationInput(GamepadInputReader.InputEvent input)
    {
        // Solo reproducir sonido en navegación vertical (DPad o Navigate)
        if (input.Phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;
        
        bool isVerticalNav = false;
        
        if (input.Type == GamepadInputReader.InputEventType.Navigate)
        {
            // Detectar navegación vertical significativa
            isVerticalNav = Mathf.Abs(input.Value.y) > 0.5f;
        }
        else if (input.Type == GamepadInputReader.InputEventType.DpadUp || 
                 input.Type == GamepadInputReader.InputEventType.DpadDown)
        {
            isVerticalNav = true;
        }
        
        if (isVerticalNav)
        {
            // El sonido ya se reproduce en GamepadInputReader, pero podemos añadir feedback extra aquí si queremos
            if (debugLogs)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[MenuNavigator] Navegación vertical detectada");
                #endif
            }
        }
    }

    void Update()
    {
        var es = EventSystem.current;
        if (!es) { UpdateCursor(null); _lastSelectedGo = null; return; }

        var selected = es.currentSelectedGameObject;
        
        // Si no hay nada seleccionado, seleccionar el primer botón automáticamente
        if (selected == null)
        {
            UpdateCursor(null);
            _lastSelectedGo = null;
            _lastSelected = null;
            if (Time.unscaledTime >= _nextSelectFirstRetryAt)
            {
                _nextSelectFirstRetryAt = Time.unscaledTime + 0.25f;
                SelectFirstButton();
            }
            return;
        }

        if (selected == _lastSelectedGo)
        {
            SeguirAlObjetivo();
            return;
        }
        _lastSelectedGo = selected;

        if (!_buttonCache.TryGetValue(selected, out var btn))
        {
            btn = selected.GetComponent<Button>();
            _buttonCache[selected] = btn;
        }
        UpdateCursor(btn);
        if (btn && btn != _lastSelected)
        {
            _lastSelected = btn;
            ApplyNudge(btn);
        }
    }

    void SelectFirstButton()
    {
        var buttons = GetComponentsInChildren<Button>(false);
        if (buttons.Length == 0) return;

        // Ordenar por posición Y (arriba primero)
        System.Array.Sort(buttons, (a, b) => 
            b.transform.position.y.CompareTo(a.transform.position.y));

        // Seleccionar el primero que esté activo e interactable
        foreach (var btn in buttons)
        {
            if (btn.interactable && btn.gameObject.activeInHierarchy)
            {
                btn.Select();
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(btn.gameObject);
                
                if (debugLogs)
                {
                    #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.Log($"[MenuNavigator] Primer botón seleccionado: {btn.name}");
                    #endif
                }
                return;
            }
        }
    }


    void ApplyNudge(Button button)
    {
        // Resetear el anterior
        if (_lastNudgedText != null)
        {
            _lastNudgedText.DOKill();
            _lastNudgedText.anchoredPosition = Vector2.zero;
        }

        // Buscar el texto hijo
        if (!_textCache.TryGetValue(button, out var textTransform))
        {
            var text = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            textTransform = text ? text.rectTransform : null;
            _textCache[button] = textTransform;
        }
        if (textTransform == null) return;

        _lastNudgedText = textTransform;
        
        // Asegurar que empieza en (0,0)
        textTransform.DOKill();
        textTransform.anchoredPosition = Vector2.zero;
        
        // Aplicar nudge
        textTransform.DOAnchorPosX(nudge, nudgeTime).SetEase(Ease.OutCubic).SetUpdate(true);
        
        if (debugLogs)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[MenuNavigator] Nudge aplicado a: {button.name}");
            #endif
        }
    }
}

