using System;
using Core;
using EasyTransition;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// El menú de Start: lo abre y lo cierra (Start), pausa el juego, oculta el HUD, pone el modo de
/// entrada de UI y pasa la entrada al menú (<see cref="Sendero.UI.MenuDeStart"/>: Inventario,
/// Magia, Equipo y Sintonía, montado por código). Por encima del menú quedan el Grimorio (Select
/// en Magia) y los Controles (X donde la pestaña no usa X). Y sale al menú principal.
/// </summary>
public class PlayerEquipmentMenuController : MonoBehaviour
{
    public static PlayerEquipmentMenuController Instance => _instance;
    public static bool IsOpen => _instance != null && _instance._isOpen;

    [Header("Persistencia")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Escena permitida")]
    [Tooltip("Nombre de la escena donde se permite abrir el menú.")]
    [SerializeField] private string allowedSceneName = "MainWorld";

    [Header("Transición al Main Menu")]
    [Tooltip("TransitionManager para transiciones suaves (opcional, se busca automáticamente si es null)")]
    [SerializeField] private TransitionManager transitionManager;
    [Tooltip("Settings de transición al salir al Main Menu")]
    [SerializeField] private TransitionSettings mainMenuTransitionSettings;
    [Tooltip("Delay antes de iniciar la transición al Main Menu")]
    [SerializeField] private float mainMenuTransitionDelay = 0.1f;

    [Header("Lienzo")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Fuente de los textos del menú. Vacía: la del primer texto del lienzo.")]
    [SerializeField] private TMP_FontAsset fuente;

    [Header("Encima del menú")]
    [Tooltip("Panel de controles. Se abre con X del mando o C del teclado donde la pestaña no usa X.")]
    [SerializeField] private ControlsMenuController controlsMenu;
    [Tooltip("Libro del grimorio. Se abre en la pestaña Magia con Select/View (M en teclado).")]
    [SerializeField] private GrimorioLibroUI grimorioLibro;

    private const float GraciaAlAbrir = 0.3f;

    static PlayerEquipmentMenuController _instance;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _instance = null; }
#endif

    Sendero.UI.MenuDeStart _menu;
    PlayerActionManager _actionManager;
    bool _actionModeActive;
    bool _toggleRequested;
    bool _cancelRequested;
    float _openedAt = -999f;
    float _toggleCooldownUntil;
    int _controlsClosedFrame = -1;
    InputActionMapScope _inputScope;
    bool _isOpen;

    // ─── Ciclo ────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        ServiceLocator.Register(this);
        if (dontDestroyOnLoad && transform.parent == null) DontDestroyOnLoad(gameObject);

        if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (canvas == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"[PlayerEquipmentMenuController] No hay Canvas en '{gameObject.name}': el menú no se puede abrir.");
#endif
            enabled = false;
            return;
        }

        SetCanvasState(false);
        CrearMenu();
        MenuManager.Close(MenuKind.Equipment);
    }

    void OnEnable()
    {
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(PlayerEquipmentMenuController));
    }

    void OnDisable()
    {
        if (_isOpen) CloseMenu();
        else ExitUiInputScope();
    }

    void OnDestroy()
    {
        // Con el menú abierto (cambio de escena) se libera todo lo que se pidió al abrir.
        if (_isOpen)
        {
            if (_actionModeActive && _actionManager != null) _actionManager.PopMode(ActionMode.Inventory);
            _actionModeActive = false;
            if (GameState.Is(GamePhase.Inventory)) GameState.Pop(GamePhase.Inventory);
            if (GameState.Is(GamePhase.Equipment)) GameState.Pop(GamePhase.Equipment);
            MenuManager.Close(MenuKind.Equipment);
            TimeScaleArbiterService.Release(this);
            Sendero.UI.PlayerHUDV2.Instance?.ShowHUD(this);
            _isOpen = false;
        }
        ExitUiInputScope();
        if (_instance == this) _instance = null;
    }

    /// Monta el menú en el lienzo justo debajo del Grimorio y los Controles, que quedan por encima.
    void CrearMenu()
    {
        var lienzo = canvas.transform as RectTransform;
        var letra = fuente != null ? fuente : GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        int indice = lienzo.childCount;
        foreach (var encima in new Component[] { grimorioLibro, controlsMenu })
        {
            if (encima == null) continue;
            var t = encima.transform;
            while (t.parent != null && t.parent != lienzo) t = t.parent;
            if (t.parent == lienzo) indice = Mathf.Min(indice, t.GetSiblingIndex());
        }
        _menu = Sendero.UI.MenuDeStart.Crear(lienzo, indice, letra);
        _menu.UsarObjeto = UsarObjeto;
        _menu.AlCambiarEstadisticas = () => Sendero.UI.PlayerHUDV2.Instance?.ForceRefresh();
    }

    // ─── Entrada ──────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsAllowedInCurrentScene() || (GameOverManager.Instance != null && GameOverManager.Instance.IsShown))
        {
            if (_isOpen) CloseMenu();
            return;
        }
        // Durante un minijuego Start sirve para abortarlo.
        if (TagMinigameController.IsAnyMinigameActive) return;

        if (GamepadInputReader.StartPressed) _toggleRequested = true;

        if (!_isOpen)
        {
            HandleToggleInput();
            return;
        }

        // Grimorio, Controles o la pregunta de salir abiertos encima: el menú espera; el mismo B o
        // Start que los cierra no cierra el menú.
        if (ConfirmationPopupUI.Instance != null && ConfirmationPopupUI.Instance.Visible
            || grimorioLibro != null && (grimorioLibro.IsOpen || Time.frameCount == grimorioLibro.ClosedFrame)
            || controlsMenu != null && (controlsMenu.IsVisible || Time.frameCount == _controlsClosedFrame))
        {
            _toggleRequested = false;
            _cancelRequested = false;
            return;
        }
        if (grimorioLibro != null && _menu.EnMagia && GrimorioDelPersonaje.TieneGrimorio && GrimorioLibroUI.OpenPressed())
        {
            AbrirGrimorio();
            return;
        }
        if (controlsMenu != null && !_menu.UsaX && GamepadInputReader.XButtonPressedUI)
        {
            AbrirControles();
            return;
        }

        if (GamepadInputReader.CancelPressed || GamepadInputReader.StartPressed) _cancelRequested = true;

        if (GamepadInputReader.YButtonPressedUI)
        {
            GamepadInputReader.PlayUISound("UI_Cancel");
            var popup = ConfirmationPopupUI.Instance;
            string msg = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get("CONFIRM_MAINMENU_QUIT", "¿Salir al menú principal?")
                : "¿Salir al menú principal?";
            if (popup != null) popup.Show(msg, onConfirm: OnQuitToMainMenu);
            else OnQuitToMainMenu();
        }

        if (GamepadInputReader.LeftShoulderPressedUI) { GamepadInputReader.PlayUISound("UI_Navigate"); _menu.CambiarPestana(-1); }
        if (GamepadInputReader.RightShoulderPressedUI) { GamepadInputReader.PlayUISound("UI_Navigate"); _menu.CambiarPestana(1); }

        HandleCloseInput();
        if (!_isOpen) return;

        // El menú lleva su propia navegación: nada seleccionado en el EventSystem.
        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
        _menu.Tick();
    }

    void HandleToggleInput()
    {
        bool pressed = _toggleRequested;
        _toggleRequested = false;
        if (!pressed || Time.unscaledTime < _toggleCooldownUntil) return;
        if (!GameState.CanOpenInventory) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;
        OpenMenu();
        _toggleCooldownUntil = Time.unscaledTime + 0.25f;
    }

    void HandleCloseInput()
    {
        if (Time.unscaledTime - _openedAt < GraciaAlAbrir) { _cancelRequested = false; return; }
        if (!_cancelRequested) return;
        _cancelRequested = false;
        if (_menu.Cancelar()) return;
        CloseMenu();
    }

    // ─── Abrir y cerrar ───────────────────────────────────────────────────────

    void OpenMenu()
    {
        if (TagMinigameController.IsAnyMinigameActive || _menu == null) return;
        if (!MenuManager.TryOpen(MenuKind.Equipment)) return;
        GamepadInputReader.PlayUISound("UI_Submit");

        if (_actionManager == null) PlayerService.TryGetComponent(out _actionManager, includeInactive: true, allowSceneLookup: true);
        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Inventory);
            _actionModeActive = true;
        }
        _inputScope?.Dispose();
        _inputScope = InputActionMapScope.EnterUiScope();
        GamepadInputReader.EnsureInputEventsSubscribed();
        TimeScaleArbiterService.Request(this, 0f);

        SetCanvasState(true);
        Sendero.UI.PlayerHUDV2.Instance?.HideHUD(this);

        _isOpen = true;
        GameState.Push(GamePhase.Inventory);
        GameState.Push(GamePhase.Equipment);
        _menu.Abrir();
        EventSystem.current?.SetSelectedGameObject(null);
        _openedAt = Time.unscaledTime;
        _cancelRequested = false;
    }

    void CloseMenu(bool playSound = true)
    {
        if (playSound && _isOpen) GamepadInputReader.PlayUISound("UI_Cancel");
        _menu?.Cerrar();
        SetCanvasState(false);
        Sendero.UI.PlayerHUDV2.Instance?.ShowHUD(this);
        TimeScaleArbiterService.Release(this);

        _isOpen = false;
        ExitUiInputScope();
        GamepadInputReader.IgnoreCancelButton(0.2f);
        if (_actionModeActive && _actionManager != null) _actionManager.PopMode(ActionMode.Inventory);
        _actionModeActive = false;
        _toggleCooldownUntil = Time.unscaledTime + 0.2f;
        if (GameState.Is(GamePhase.Inventory)) GameState.Pop(GamePhase.Inventory);
        if (GameState.Is(GamePhase.Equipment)) GameState.Pop(GamePhase.Equipment);
        MenuManager.Close(MenuKind.Equipment);
    }

    void SetCanvasState(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        else if (canvas != null) canvas.gameObject.SetActive(visible);
    }

    void ExitUiInputScope()
    {
        _inputScope?.Dispose();
        _inputScope = null;
    }

    // ─── Acciones del menú ────────────────────────────────────────────────────

    /// Abre el Grimorio por el último hechizo aprendido o por el resaltado en la pestaña Magia.
    public void AbrirGrimorio()
    {
        if (grimorioLibro == null || grimorioLibro.IsOpen || !GrimorioDelPersonaje.TieneGrimorio) return;
        var start = GrimorioDelPersonaje.UltimoAprendido != SpellId.None ? GrimorioDelPersonaje.UltimoAprendido : _menu.HechizoResaltado;
        GrimorioDelPersonaje.UltimoAprendido = SpellId.None;
        grimorioLibro.Open(start);
    }

    public void AbrirControles()
    {
        if (!_isOpen || controlsMenu == null || controlsMenu.IsVisible) return;
        if (grimorioLibro != null && grimorioLibro.IsOpen) return;
        GamepadInputReader.PlayUISound("UI_Navigate");
        controlsMenu.Show(() => _controlsClosedFrame = Time.frameCount);
    }

    /// Abre la pestaña Sintonía (si el menú está abierto).
    public void AbrirSintoniaEstelar()
    {
        if (!_isOpen) return;
        GamepadInputReader.PlayUISound("UI_Navigate");
        _menu.IrA<Sendero.UI.VistaDeSintoniaDelMenu>();
    }

    /// Usa un objeto del inventario (pociones, plumas…). Devuelve el mensaje para enseñar.
    string UsarObjeto(ItemData item)
    {
        if (item == null) return null;
        PlayerService.TryGetComponent<Inventory>(out var inventario, includeInactive: true, allowSceneLookup: true);
        PlayerService.TryGetComponent<PlayerPickupCollector>(out var recolector, includeInactive: true, allowSceneLookup: true);
        if (inventario == null) return null;
        if (!InventoryUseUtility.TryUseItem(inventario, item, recolector, out var motivo, out _))
            return string.IsNullOrEmpty(motivo) ? "No se pudo usar." : motivo;
        return "Usado: " + item.GetLocalizedName() + ".";
    }

    // ─── Salir al menú principal ──────────────────────────────────────────────

    void OnQuitToMainMenu()
    {
        if (_isOpen) CloseMenu(playSound: false);
        TimeScaleArbiterService.Release(this);
        MainMenuController.RequestInputDebounce();
        var tm = ResolveTransitionManager();
        if (tm != null && mainMenuTransitionSettings != null)
            tm.Transition("MainMenu", mainMenuTransitionSettings, mainMenuTransitionDelay);
        else
            SceneManager.LoadScene("MainMenu");
    }

    TransitionManager ResolveTransitionManager()
    {
        if (transitionManager != null) return transitionManager;
        if (ServiceLocator.TryGet(out TransitionManager cached) && cached != null) return transitionManager = cached;
        try
        {
            transitionManager = TransitionManager.Instance();
            if (transitionManager != null) ServiceLocator.Register(transitionManager);
        }
        catch (Exception
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            ex
#endif
        )
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PlayerEquipmentMenuController] TransitionManager.Instance() falló: {ex.Message}");
#endif
        }
        return transitionManager;
    }

    /// Mientras el menú está abierto: el juego no lee los botones de juego y el mando navega la UI.
    sealed class InputActionMapScope : IDisposable
    {
        bool _disposed;

        InputActionMapScope()
        {
            GamepadInputReader.PushGameplaySuppression(this);
            GamepadInputReader.PushUiNavigationScope();
            if (ServiceLocator.TryGet(out PlayerInputManager pim)) pim.PushUIMode();
        }

        public static InputActionMapScope EnterUiScope() => new InputActionMapScope();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ServiceLocator.TryGet(out PlayerInputManager pim)) pim.PopUIMode();
            GamepadInputReader.PopGameplaySuppression(this);
            GamepadInputReader.PopUiNavigationScope();
        }
    }

    bool IsAllowedInCurrentScene()
    {
        if (string.IsNullOrEmpty(allowedSceneName)) return true;
        var activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() && string.Equals(activeScene.name, allowedSceneName, StringComparison.OrdinalIgnoreCase);
    }
}
