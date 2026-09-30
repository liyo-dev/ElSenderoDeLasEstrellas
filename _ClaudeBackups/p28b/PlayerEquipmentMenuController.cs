using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;
using EasyTransition;

public class PlayerEquipmentMenuController : MonoBehaviour
{
    public static PlayerEquipmentMenuController Instance => _instance;
    public static bool IsOpen => _instance != null && _instance._isOpen;

    public readonly struct InventoryItemUseContext
    {
        public Inventory Inventory { get; }
        public ItemData Item { get; }
        public PlayerPickupCollector Collector { get; }

        public InventoryItemUseContext(Inventory inventory, ItemData item, PlayerPickupCollector collector)
        {
            Inventory = inventory;
            Item = item;
            Collector = collector;
        }
    }

    public struct InventoryItemUseResult
    {
        public bool handled;
        public bool consumed;
        public string message;

        public InventoryItemUseResult(bool handled, bool consumed, string message)
        {
            this.handled = handled;
            this.consumed = consumed;
            this.message = message;
        }

        public static InventoryItemUseResult NotHandled => new InventoryItemUseResult(false, false, null);

        public static InventoryItemUseResult Handled(string message = null, bool consumed = false)
            => new InventoryItemUseResult(true, consumed, message);
    }

    public delegate InventoryItemUseResult InventoryItemUseHandler(InventoryItemUseContext context);
    public static event InventoryItemUseHandler OnInventoryItemUseRequested;

    static InventoryItemUseResult DispatchInventoryUseRequest(InventoryItemUseContext context)
    {
        var handlers = OnInventoryItemUseRequested;
        if (handlers == null) return InventoryItemUseResult.NotHandled;

        var aggregated = InventoryItemUseResult.NotHandled;

        foreach (InventoryItemUseHandler handler in handlers.GetInvocationList())
        {
            try
            {
                var partial = handler(context);
                if (!partial.handled) continue;

                aggregated.handled = true;
                if (partial.consumed)
                    aggregated.consumed = true;
                if (!string.IsNullOrEmpty(partial.message))
                    aggregated.message = partial.message;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        return aggregated;
    }

    [Header("Persistencia")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Escena permitida")]
    [Tooltip("Nombre de la escena donde se permite abrir el menú de equipo.")]
    [SerializeField] private string allowedSceneName = "MainWorld";

    [Header("Transición al Main Menu")]
    [Tooltip("TransitionManager para transiciones suaves (opcional, se busca automáticamente si es null)")]
    [SerializeField] private TransitionManager transitionManager;
    [Tooltip("Settings de transición al salir al Main Menu")]
    [SerializeField] private TransitionSettings mainMenuTransitionSettings;
    [Tooltip("Delay antes de iniciar la transición al Main Menu")]
    [SerializeField] private float mainMenuTransitionDelay = 0.1f;

    [Header("Contenedores UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Objeto raíz del contenido del menú (se activa/desactiva al abrir/cerrar).")]
    [SerializeField] private GameObject windowRoot;

    [Header("Feedback")]
    [SerializeField, Tooltip("Tiempo que se mantiene visible el mensaje de feedback tras usar un objeto.")]
    private float feedbackDuration = 1.5f;

    [Header("Controles (INC-504)")]
    [SerializeField, Tooltip("Panel de controles (copia del del menú principal). Se abre con X (C en teclado) desde el menú.")]
    private ControlsMenuController controlsMenu;
    [SerializeField, Tooltip("Texto de ayuda «X Controles» (opcional).")]
    private TextMeshProUGUI controlsHintText;
    int _controlsClosedFrame = -1;

    [Header("Grimorio (INC-506)")]
    [SerializeField, Tooltip("Libro del grimorio. Se abre desde la pestaña Hechizos con el botón «Grimorio» o Select/View (M en teclado).")]
    private GrimorioLibroUI grimorioLibro;
    [SerializeField, Tooltip("Tarjeta «Grimorio» de la barra de pistas de abajo, con el icono del botón que lo abre. Solo se ve en la pestaña Hechizos.")]
    private GameObject grimorioHintCard;

    [Header("Pestañas")]
    [SerializeField] private Button inventoryTabButton;
    [SerializeField] private Button spellsTabButton;
    [SerializeField] private Button equipmentTabButton;
    [SerializeField] private Color tabActiveColor   = Color.white;
    [SerializeField] private Color tabInactiveColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Panel de jugador")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI mpText;
    [Tooltip("Image con Type=Filled (Horizontal). Opcional: si esta vacio no se hace nada.")]
    [SerializeField] private Image hpBarFill;
    [SerializeField] private Image mpBarFill;

    string _levelLabel = "";
    string _hpLabel = "";
    string _mpLabel = "";
    bool _labelsCached;
    
    // Animaciones de feedback para HP/MP
    private Tween _hpTextTween;
    private Tween _mpTextTween;
    private Color _hpOriginalColor;
    private Color _mpOriginalColor;
    private bool _hpColorCached;
    private bool _mpColorCached;

    [Header("Habilidades")]
    [SerializeField] private GameObject abilitiesRoot;
    [SerializeField] private AbilityEntryReferences abilityEntries = new();

    [Header("Efecto sueño")]
    [Tooltip("Blobs nebulosa que aparecen al abrir el inventario (opcional).")]
    [SerializeField] private DreamBackgroundController dreamBackground;
    [Tooltip("Chispas flotantes al abrir el inventario (opcional).")]
    [SerializeField] private DreamSparkleOverlay dreamSparkles;

    [Header("Selección inicial")]
    [SerializeField] private GameObject initialSelectionOverride;

    [Header("Inventario")]
    [SerializeField] private InventoryBindings inventoryUI = new();

        [Header("Hechizos")]
        [SerializeField] private SpellBindings spellUI = new();

    [Header("Equipamiento")]
    [SerializeField] private EquipmentBindings equipmentUI = new();

    static PlayerEquipmentMenuController _instance;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
    }
    #endif

    readonly List<Button> _tabButtons = new();
    readonly Dictionary<Button, int> _tabButtonIndices = new();
    readonly Dictionary<Button, ColorBlock> _tabOriginalColors = new();

    InventoryView _inventoryView;
    SpellView _spellView;
    EquipmentView _equipmentView;
    [Header("Cámara de equipamiento - órbita de Will")]
    [SerializeField] private float previewOrbitSpeed = 120f;
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de permitir el cierre (para evitar rebotes de input).")]
    private float closeInputGracePeriod = 0.3f;

    [Header("Cámara de equipamiento - desplazamiento de la cámara principal")]
    [SerializeField, Tooltip("Cámara principal en tercera persona (Invector). Se busca automáticamente vía ServiceLocator si es null.")]
    private vThirdPersonCamera mainThirdPersonCamera;
    [SerializeField, Range(0.5f, 1f), Tooltip("Fracción horizontal de pantalla donde debe quedar centrado Will (0.5 = centro, 0.75 = centro de la mitad derecha).")]
    private float equipmentMenuTargetScreenX = 0.75f;
    [SerializeField, Tooltip("Altura aproximada (en metros, desde los pies) del punto al que mira la cámara nivelada. Debe rondar la altura del pecho/cara de Will para que no se vea desde arriba ni desde abajo.")]
    private float equipmentMenuCameraLookHeight = 1.6f;
    [SerializeField, Min(0f), Tooltip("Duración de la transición de la cámara principal al abrir/cerrar el menú.")]
    private float equipmentMenuCameraTransitionDuration = 0.4f;

    // Cámara principal desplazada temporalmente mientras el menú está abierto
    Camera _mainCamera;
    bool _mainCameraOffsetActive;
    Tween _mainCameraTween;

    bool _equipmentCameraActive;
    Transform _playerPreviewTarget;
    Quaternion _storedPlayerRotation;
    float _previewBaseYaw; // Yaw hacia el que Will mira por defecto (mirando a la cámara desplazada)
    float _previewPlayerYaw;
    bool _wasInOrbitMode; // Rastrear si estuvimos en modo orbit en el frame anterior
    PlayerActionManager _actionManager;
    bool _actionModeActive;
    bool _toggleRequested;
    bool _cancelRequested;
    float _openedAt = -999f;
    float _toggleCooldownUntil;
    InputActionMapScope _inputScope;
    
    // Para mantener animaciones del player en el menú
    Animator _playerAnimator;
    AnimatorUpdateMode _storedAnimatorUpdateMode;

    // Hashes cacheados para parámetros del Animator (evitar búsquedas por string)
    static readonly int AnimHash_InputMagnitude = Animator.StringToHash("InputMagnitude");
    static readonly int AnimHash_Speed = Animator.StringToHash("Speed");
    static readonly int AnimHash_VerticalVelocity = Animator.StringToHash("VerticalVelocity");

    bool _isOpen;
    int _activeTab;
    
    // Flag para controlar animaciones de uso de items
    bool _isUsingItem;

    Coroutine _clearFeedbackRoutine;

    bool _warnedInventory;
    bool _warnedSpells;
    bool _warnedEquipment;

    // Cambiado a SubsystemRegistration para que se ejecute antes y busque en todas las escenas
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // Debug.Log("[PlayerEquipmentMenuController] Bootstrap: Buscando instancia existente...");
        
        // Intentar obtener desde ServiceLocator primero
        if (ServiceLocator.TryGet<PlayerEquipmentMenuController>(out var existing) && existing != null)
        {
            // Debug.Log("[PlayerEquipmentMenuController] Bootstrap: Encontrada instancia existente en ServiceLocator");
            _instance = existing;
            return;
        }
    
        
        // Si no hay instancia, no hacer nada - el menú debe estar configurado manualmente en la escena
        // Debug.Log("[PlayerEquipmentMenuController] Bootstrap: No se encontró instancia. El menú debe estar configurado manualmente en la escena.");
    }

    void Awake()
    {
        // Debug.Log($"[PlayerEquipmentMenuController] Awake en GameObject '{gameObject.name}'");
        
        if (_instance != null && _instance != this)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PlayerEquipmentMenuController] Instancia duplicada detectada en '{gameObject.name}', destruyendo...");
#endif
            Destroy(gameObject);
            return;
        }

        _instance = this;
        ServiceLocator.Register(this);

        if (dontDestroyOnLoad && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
            // Debug.Log($"[PlayerEquipmentMenuController] DontDestroyOnLoad aplicado a '{gameObject.name}'");
        }

        // Buscar componentes UI necesarios
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlayerEquipmentMenuController] Canvas encontrado: {(canvas != null ? canvas.gameObject.name : "NULL")}");
#endif
        }
        
        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlayerEquipmentMenuController] CanvasGroup encontrado: {(canvasGroup != null ? "Sí" : "No")}");
#endif
        }
        
        if (windowRoot == null && canvas != null)
        {
            windowRoot = canvas.gameObject;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlayerEquipmentMenuController] WindowRoot asignado automáticamente a Canvas: '{windowRoot.name}'");
#endif
        }
        
        // Verificar si tenemos lo mínimo necesario
        if (canvas == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"[PlayerEquipmentMenuController] âš ï¸ No se encontró Canvas en '{gameObject.name}'");
            Debug.LogError("   El menú de equipamiento NO funcionará correctamente.");
            Debug.LogError("   Asegúrate de que el PlayerEquipmentMenuController esté en un GameObject con Canvas configurado.");
#endif
            // No desactivar el componente para que se pueda configurar después
            enabled = false;
            return;
        }

        if (levelText != null)
            _levelLabel = levelText.text;
        if (hpText != null)
            _hpLabel = hpText.text;
        if (mpText != null)
            _mpLabel = mpText.text;

        SetCanvasState(false);

        RegisterTabButtons();
        
        // EnsureViews retorna false si no hay vistas configuradas
        if (!EnsureViews())
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[PlayerEquipmentMenuController] âš ï¸ No se pudo inicializar ninguna vista del menú");
            Debug.LogError("   El menú no podrá abrirse hasta que se configuren las vistas en el Inspector.");
#endif
            // No desactivar el componente para que se pueda configurar después
        }
        
        SetEquipmentCameraActive(false);
        // Unregister from MenuManager
        MenuManager.Close(MenuKind.Equipment);
        
        // Debug.Log($"[PlayerEquipmentMenuController] Awake completado. Vistas configuradas: {(_inventoryView != null || _spellView != null || _equipmentView != null)}");
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(PlayerEquipmentMenuController));
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        if (_isOpen)
            CloseMenu();
        else
            ExitUiInputScope();
    }

    private void HandleProfileReady()
    {
        // El menú de equipamiento accede al Profile para obtener el preset activo
        // No necesita inicialización especial, solo necesita que el Profile esté disponible
        // cuando accede a él en RefreshInventoryTab() y otros métodos
    }

    void OnDestroy()
    {
        // IMPORTANTE: limpiar todo el estado de bloqueo de input antes de destruir el objeto.
        // Si el objeto se destruye con el menú abierto (p.ej. cambio de escena), el PopMode
        // y MenuManager.Close nunca se llamarían desde CloseMenu(), dejando el stack de modos
        // y el MenuManager en estado corrupto para siempre.
        if (_isOpen)
        {
            // Limpiar ActionMode sin depender de _actionManager (puede ya estar destruido)
            if (_actionModeActive && _actionManager != null)
            {
                _actionManager.PopMode(ActionMode.Inventory);
                _actionModeActive = false;
            }
            // Restaurar GameState
            if (GameState.Is(GamePhase.Inventory)) GameState.Pop(GamePhase.Inventory);
            if (GameState.Is(GamePhase.Equipment)) GameState.Pop(GamePhase.Equipment);
            // Limpiar registro de MenuManager
            MenuManager.Close(MenuKind.Equipment);
            // Restaurar timeScale (FIX INC-2026-09-01: vía TimeScaleArbiterService en vez de
            // pisar Time.timeScale a pelo, ver OpenMenu()/CloseMenu())
            TimeScaleArbiterService.Release(this);
            _isOpen = false;
        }
        ExitUiInputScope();
        // Si el objeto se destruye con el menú abierto (p.ej. cambio de escena), devolver la
        // cámara al gameplay para no dejar el vThirdPersonCamera deshabilitado para siempre.
        RestoreEquipmentMenuCamera();
        _inventoryView?.Dispose();
        _equipmentView?.Dispose();
        if (_instance == this)
            _instance = null;
    }

    void Update()
    {
        if (!IsAllowedInCurrentScene())
        {
            if (_isOpen) CloseMenu();
            return;
        }


        if (GameOverManager.Instance != null && GameOverManager.Instance.IsShown)
        {
            if (_isOpen) CloseMenu();
            return;
        }

        // Durante el minijuego el Start se usa para abortar — no abrir el menú
        if (TagMinigameController.IsAnyMinigameActive) return;

        // Detectar botón Start para abrir/cerrar el menú usando GamepadInputReader
        if (GamepadInputReader.StartPressed)
        {
            _toggleRequested = true;
        }

        // Si el menú ya está abierto, evita leer el input de apertura para que el D-Pad
        // no interfiera con la navegación UI (el toggle se maneja al cerrarse).
        if (!_isOpen)
        {
            HandleToggleInput();
        }
        else
        {
            // Controles abiertos encima (INC-504): el menú no hace nada hasta que se cierren; el mismo
            // B o Start que los cierra tampoco cierra el menú.
            // Grimorio abierto encima (INC-506): igual que los controles.
            if (grimorioLibro != null && (grimorioLibro.IsOpen || Time.frameCount == grimorioLibro.ClosedFrame))
            {
                _toggleRequested = false;
                _cancelRequested = false;
                return;
            }
            if (grimorioLibro != null && _activeTab == 1 && GrimorioLibroUI.OpenPressed())
            {
                AbrirGrimorio();
                return;
            }

            if (controlsMenu != null && (controlsMenu.IsVisible || Time.frameCount == _controlsClosedFrame))
            {
                _toggleRequested = false;
                _cancelRequested = false;
                return;
            }
            if (controlsMenu != null && GamepadInputReader.XButtonPressedUI)
            {
                GamepadInputReader.PlayUISound("UI_Navigate");
                controlsMenu.Show(() =>
                {
                    _controlsClosedFrame = Time.frameCount;
                    ShowTab(_activeTab);
                });
                return;
            }
            UpdateControlsHint();

            // Detectar botones del gamepad usando GamepadInputReader
            
            // Botón B (Cancel) o Start para cerrar el menú
            if (GamepadInputReader.CancelPressed || GamepadInputReader.StartPressed)
            {
                _cancelRequested = true;
            }
            
            // Botón Y para volver al MainMenu
            // Leer directamente del gamepad porque GamepadInputReader suprime estos botones en UI
            if (IsYButtonPressed())
            {
                GamepadInputReader.PlayUISound("UI_Cancel");
                var popup = ConfirmationPopupUI.Instance;
                if (popup != null)
                {
                    string msg = LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.Get("CONFIRM_MAINMENU_QUIT", "¿Salir al menú principal?")
                        : "¿Salir al menú principal?";
                    popup.Show(msg, onConfirm: OnQuitToMainMenu);
                }
                else
                {
                    OnQuitToMainMenu();
                }
            }
            
            // LB (Left Bumper) para pestaña anterior
            // Leer directamente del gamepad porque GamepadInputReader suprime estos botones en UI
            if (IsLeftShoulderPressed())
            {
                GamepadInputReader.PlayUISound("UI_Navigate");
                ChangeTab(-1);
            }
            
            // RB (Right Bumper) para pestaña siguiente
            // Leer directamente del gamepad porque GamepadInputReader suprime estos botones en UI
            if (IsRightShoulderPressed())
            {
                GamepadInputReader.PlayUISound("UI_Navigate");
                ChangeTab(1);
            }
            
            HandleCloseInput();
            UpdatePlayerInfoPanel();
            
            // Mantener el Animator en idle continuamente
            MaintainAnimatorIdle();
            
            // Manejar inputs específicos de cada tab
            if (_activeTab == 0) // Inventario
            {

                // Manejar Submit (A button)
                if (GamepadInputReader.SubmitPressed)
                {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.Log("[PlayerEquipmentMenu] â­ Submit detectado en inventario!");
#endif
                    bool handled = _inventoryView?.TryHandleSubmit() ?? false;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.Log($"[PlayerEquipmentMenu] Submit handled: {handled}");
#endif
                }

                // Manejar Cancel (B button) - pero solo si el inventario no lo maneja primero
                if (GamepadInputReader.CancelPressed)
                {
                    bool handled = _inventoryView?.TryHandleCancel() ?? false;
                    if (handled)
                        _cancelRequested = false; // Evitar que cierre el menú
                }
            }
            else if (_activeTab == 1) // Hechizos
            {
                _spellView?.HandleInput();
            }
        }
    }

    // Métodos auxiliares simplificados - usan GamepadInputReader centralizado
    // Estos leen del Action Map UI para navegación de menús
    bool IsLeftShoulderPressed()
    {
        return GamepadInputReader.LeftShoulderPressedUI;
    }

    bool IsRightShoulderPressed()
    {
        return GamepadInputReader.RightShoulderPressedUI;
    }

    /// Abre el libro del grimorio por el hechizo resaltado (o el último aprendido). Lo llama el
    /// botón «Grimorio» de la pestaña Hechizos. INC-506.
    public void AbrirGrimorio()
    {
        if (grimorioLibro == null || grimorioLibro.IsOpen) return;
        var start = GrimorioDelPersonaje.UltimoAprendido != SpellId.None
            ? GrimorioDelPersonaje.UltimoAprendido
            : (_spellView != null ? _spellView.HighlightedSpell : SpellId.None);
        GrimorioDelPersonaje.UltimoAprendido = SpellId.None;
        grimorioLibro.Open(start);
    }

    void UpdateControlsHint()
    {
        if (controlsHintText == null) return;
        var family = Core.InputGlyphs.InputGlyphService.CurrentFamily;
        string key = family == Core.InputGlyphs.InputGlyphDeviceFamily.KeyboardMouse
            ? "C"
            : Core.InputGlyphs.InputGlyphLabels.GetLabel(Core.InputGlyphs.InputGlyphNames.West, family);
        string label = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get("MENU_CONTROLS_HINT", "Controles")
            : "Controles";
        controlsHintText.text = $"{key}  {label}";
    }

    bool IsYButtonPressed()
    {
        return GamepadInputReader.YButtonPressedUI;
    }

    void RegisterTabButtons()
    {
        if (inventoryTabButton != null)
        {
            inventoryTabButton.onClick.AddListener(() => ShowTab(0));
            _tabButtons.Add(inventoryTabButton);
            _tabButtonIndices[inventoryTabButton] = 0;

            if (inventoryTabButton.GetComponent<UIButtonAudio>() == null)
                inventoryTabButton.gameObject.AddComponent<UIButtonAudio>();
        }
        if (spellsTabButton != null)
        {
            spellsTabButton.onClick.AddListener(() => ShowTab(1));
            _tabButtons.Add(spellsTabButton);
            _tabButtonIndices[spellsTabButton] = 1;

            if (spellsTabButton.GetComponent<UIButtonAudio>() == null)
                spellsTabButton.gameObject.AddComponent<UIButtonAudio>();
        }
        if (equipmentTabButton != null)
        {
            equipmentTabButton.onClick.AddListener(() => ShowTab(2));
            _tabButtons.Add(equipmentTabButton);
            _tabButtonIndices[equipmentTabButton] = 2;

            if (equipmentTabButton.GetComponent<UIButtonAudio>() == null)
                equipmentTabButton.gameObject.AddComponent<UIButtonAudio>();
        }
    }

    void HandleToggleInput()
    {
        bool pressed = _toggleRequested;
        _toggleRequested = false;

        if (Time.unscaledTime < _toggleCooldownUntil)
            return;

        if (!pressed) return;
        if (TagMinigameController.IsAnyMinigameActive) return;

        if (_isOpen)
        {
            CloseMenu();
        }
        else
        {
            if (!GameState.CanOpenInventory) return;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;
            OpenMenu();
            _toggleCooldownUntil = Time.unscaledTime + 0.25f;
        }
    }


    void HandleCloseInput()
    {
        // Evitar cerrar inmediatamente si todavía estamos procesando el input que abrió el menú.
        if (Time.unscaledTime - _openedAt < closeInputGracePeriod)
            return;

        bool cancel = _cancelRequested;
        _cancelRequested = false;

        if (cancel)
        {
            bool handled = false;
            if (_activeTab == 0 && _inventoryView != null)
                handled = _inventoryView.TryHandleCancel();
            else if (_activeTab == 1 && _spellView != null)
                handled = _spellView.TryHandleCancel();
            else if (_activeTab == 2 && _equipmentView != null)
                handled = _equipmentView.TryHandleCancel();

            if (handled)
                return;

            CloseMenu();
        }
    }

    void ChangeTab(int delta)
    {
        if (delta == 0) return;

        var availableTabs = GetAvailableTabs();
        if (availableTabs.Count == 0) return;

        int currentIndex = availableTabs.IndexOf(_activeTab);
        if (currentIndex < 0) currentIndex = 0;

        int nextIndex = (currentIndex + delta + availableTabs.Count) % availableTabs.Count;
        int nextTab = availableTabs[nextIndex];
        bool forceRebuild = nextTab == 0 && nextTab != _activeTab;
        ShowTab(nextTab, forceRebuild);
    }



    List<int> GetAvailableTabs()
    {
        var tabs = new List<int>(3);
        if (_inventoryView != null) tabs.Add(0);
        if (_spellView != null) tabs.Add(1);
        if (_equipmentView != null) tabs.Add(2);
        if (tabs.Count == 0)
            tabs.Add(_activeTab);
        return tabs;
    }

    void OpenMenu()
    {
        if (TagMinigameController.IsAnyMinigameActive)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerEquipmentMenu] OpenMenu() bloqueado — minijuego activo");
            #endif
            return;
        }

        // Reproducir sonido de apertura de menú
        GamepadInputReader.PlayUISound("UI_Submit");

        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] OpenMenu() llamado");
        #endif
        
        // Verificación temprana: Â¿tenemos Canvas?
        if (canvas == null)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[PlayerEquipmentMenu] âŒ No se puede abrir - Canvas es NULL");
            Debug.LogError("   El PlayerEquipmentMenuController no está correctamente configurado.");
            Debug.LogError("   Debe estar en un GameObject con un Canvas configurado.");
            #endif
            return;
        }
        
        // Verificación temprana: Â¿hay al menos una vista configurada?
        if (_inventoryView == null && _spellView == null && _equipmentView == null)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[PlayerEquipmentMenu] âŒ No se puede abrir - NINGUNA VISTA CONFIGURADA");
            Debug.LogError("   Configura al menos una vista (Inventory, Spell o Equipment) en el Inspector.");
            Debug.LogError("   Revisa los logs anteriores de EnsureViews() para más detalles.");
            #endif
            return;
        }
        
        if (!GameState.CanOpenInventory)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerEquipmentMenu] No se puede abrir - GameState.CanOpenInventory = false");
            #endif
            return;
        }
        
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerEquipmentMenu] No se puede abrir - Diálogo activo");
            #endif
            return;
        }

        // Ask central manager for permission to open
        if (!MenuManager.TryOpen(MenuKind.Equipment))
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerEquipmentMenuController] Apertura denegada por MenuManager");
            #endif
            return;
        }

        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] MenuManager permitió la apertura, verificando vistas...");
        #endif
        
        if (!EnsureViews())
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[PlayerEquipmentMenu] EnsureViews() retornó false - cerrando menú");
            #endif
            MenuManager.Close(MenuKind.Equipment);
            return;
        }

        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] Vistas verificadas, inicializando ActionManager...");
        #endif
        
        EnsureActionManager();
        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Inventory);
            _actionModeActive = true;
        }
        
        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] Llamando a EnterUiInputScope()");
        #endif
        EnterUiInputScope();

        // FIX INC-2026-09-01: antes se capturaba Time.timeScale y se pisaba directo a 0, sin
        // coordinarse con TimeScaleArbiterService. Si el menu se abria mientras un slowmotion
        // (p.ej. DeathCameraEffect al ganar una batalla) ya habia bajado Time.timeScale, se
        // capturaba ese valor bajo como "el normal"; si el slowmotion se liberaba mientras el
        // menu seguia abierto, el arbitro reponia timeScale=1 por su cuenta (sin que este menu se
        // enterase), y al cerrar el menu se volvia a pisar con el valor bajo ya obsoleto -> la
        // camara lenta se quedaba para siempre, desincronizada del propio arbitro. Pedir la pausa
        // como una peticion mas (0 = la mas lenta posible, siempre gana) deja que sea el arbitro
        // quien decida el timeScale efectivo en todo momento.
        TimeScaleArbiterService.Request(this, 0f);
        
        // Cambiar el Animator a Unscaled Time para que las animaciones sigan funcionando
        if (_playerAnimator != null)
        {
            _storedAnimatorUpdateMode = _playerAnimator.updateMode;
            _playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerEquipmentMenu] Animator cambiado a UnscaledTime para mantener animaciones en el menú");
            #endif
        }

        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] Configurando canvas y pestañas...");
        #endif
        SetCanvasState(true);
        dreamBackground?.StartDream();
        dreamSparkles?.StartSparkles();

        // Ocultar el HUD y el icono de estado del tiempo mientras el menú está abierto
        // (ahora se ve el mundo real detrás de Will, así que estorbarían en pantalla).
        Sendero.UI.PlayerHUDV2.Instance?.HideHUD();
        Sendero.UI.TimeOfDayIndicator.Instance?.Hide();

        // Cachear colores originales de HP/MP si no se han cacheado aún
        if (!_hpColorCached && hpText != null)
        {
            _hpOriginalColor = hpText.color;
            _hpColorCached = true;
        }
        if (!_mpColorCached && mpText != null)
        {
            _mpOriginalColor = mpText.color;
            _mpColorCached = true;
        }

        int defaultTab = GetDefaultTab();
        bool forceRebuild = defaultTab == 0;
        ShowTab(defaultTab, forceRebuild);
        UpdatePlayerInfoPanel();

        _isOpen = true;
        GameState.Push(GamePhase.Inventory);
        GameState.Push(GamePhase.Equipment);
        SelectInitial();
        
        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] Activando cámara de equipamiento...");
        #endif
        // Activar la cámara de equipamiento siempre que el menú esté abierto
        SetEquipmentCameraActive(true);

        // Marcar el instante de apertura para filtrar cierres accidentales en el mismo frame.
        _openedAt = Time.unscaledTime;
        _cancelRequested = false; // Limpiar cualquier cancel previo para evitar cierres inmediatos.
        
        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] Menú abierto completamente");
        #endif
    }

    void CloseMenu(bool playSound = true)
    {
        // Solo reproducir sonido si el menú realmente estaba abierto
        if (playSound && _isOpen)
        {
            GamepadInputReader.PlayUISound("UI_Cancel");
        }
        
        // Limpiar animaciones de HP/MP
        _hpTextTween?.Kill();
        _hpTextTween = null;
        _mpTextTween?.Kill();
        _mpTextTween = null;
        
        // Restaurar colores originales si están cacheados
        if (_hpColorCached && hpText != null)
            hpText.color = _hpOriginalColor;
        if (_mpColorCached && mpText != null)
            mpText.color = _mpOriginalColor;
        
        dreamBackground?.StopDream();
        dreamSparkles?.StopSparkles();
        SetCanvasState(false);

        // Restaurar el HUD y el icono de estado del tiempo al cerrar el menú
        Sendero.UI.PlayerHUDV2.Instance?.ShowHUD();
        Sendero.UI.TimeOfDayIndicator.Instance?.Show();
        _spellView?.CancelSlotSelection(true);
        TimeScaleArbiterService.Release(this);
        
        // Restaurar el AnimatorUpdateMode original
        if (_playerAnimator != null)
        {
            _playerAnimator.updateMode = _storedAnimatorUpdateMode;
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerEquipmentMenu] Animator restaurado a su UpdateMode original");
            #endif
        }
        
        // Resetear estado de órbita para que se recalcule la próxima vez
        _wasInOrbitMode = false;
        
        _isOpen = false;
        ExitUiInputScope();
        // Evitar que el botón B que cerró el menú dispare acciones de gameplay en el mismo frame.
        GamepadInputReader.IgnoreCancelButton(0.2f);
        if (_actionModeActive && _actionManager != null)
        {
            _actionManager.PopMode(ActionMode.Inventory);
            _actionModeActive = false;
        }
        _toggleCooldownUntil = Time.unscaledTime + 0.2f;
        if (GameState.Is(GamePhase.Inventory)) GameState.Pop(GamePhase.Inventory);
        if (GameState.Is(GamePhase.Equipment)) GameState.Pop(GamePhase.Equipment);
        SetEquipmentCameraActive(false);
        MenuManager.Close(MenuKind.Equipment);
    }

    void OnQuitToMainMenu()
    {
        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenuController] Iniciando transición al Main Menu");
        #endif

        // Cerrar el menú SIN reproducir sonido (ya sonó UI_Cancel arriba)
        if (_isOpen)
        {
            CloseMenu(playSound: false);
        }
        
        // Asegurar que no queda ninguna peticion de pausa de ESTE menu activa (no-op si
        // CloseMenu() ya la libero arriba). FIX INC-2026-09-01: ya no se fuerza Time.timeScale=1
        // a pelo aqui, pisaria cualquier otro efecto de timeScale activo en ese instante.
        TimeScaleArbiterService.Release(this);
        
        // Debounce de input para MainMenu (similar a GameOverManager)
        MainMenuController.RequestInputDebounce();
        
        // Usar transición si está disponible, sino carga directa
        var tm = ResolveTransitionManager();
        if (tm != null && mainMenuTransitionSettings != null)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerEquipmentMenuController] Usando transición con settings configurados");
            #endif
            tm.Transition("MainMenu", mainMenuTransitionSettings, mainMenuTransitionDelay);
        }
        else
        {
            if (tm == null)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerEquipmentMenuController] TransitionManager no disponible, cargando escena directamente");
                #endif
            }
            else
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerEquipmentMenuController] MainMenuTransitionSettings no configurado, cargando escena directamente");
                #endif
            }
            
            SceneManager.LoadScene("MainMenu");
        }
    }

    TransitionManager ResolveTransitionManager()
    {
        // Si ya tenemos referencia serializada, usarla
        if (transitionManager != null) return transitionManager;

        // Intentar obtener del ServiceLocator
        if (ServiceLocator.TryGet(out TransitionManager cached) && cached != null)
        {
            transitionManager = cached;
            return transitionManager;
        }

        // Intentar obtener la instancia singleton
        try
        {
            transitionManager = TransitionManager.Instance();
            if (transitionManager != null)
            {
                ServiceLocator.Register(transitionManager);
            }
        }
        catch (Exception ex)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PlayerEquipmentMenuController] TransitionManager.Instance() falló: {ex.Message}");
            #endif
        }

        return transitionManager;
    }

    void SetCanvasState(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (windowRoot != null)
            windowRoot.SetActive(visible);

        if (canvas != null && canvasGroup == null)
            canvas.gameObject.SetActive(visible);
    }

    void ShowTab(int index, bool forceRebuild = false)
    {
        int previousTab = _activeTab;
        _activeTab = Mathf.Clamp(index, 0, 2);

        if (_spellView != null && _activeTab != 1)
            _spellView.CancelSlotSelection(true);

        if (_inventoryView != null)
        {
            _inventoryView.SetVisible(_activeTab == 0);
            if (_activeTab == 0)
            {
                _inventoryView.Refresh(forceRebuild);
                StartCoroutine(_inventoryView.EnsureSelectionDelayed());
            }
        }

        if (_spellView != null)
        {
            _spellView.SetVisible(_activeTab == 1);
            if (_activeTab == 1) _spellView.Refresh();
        }
        if (grimorioHintCard != null && grimorioHintCard.activeSelf != (_activeTab == 1))
            grimorioHintCard.SetActive(_activeTab == 1);

        if (_equipmentView != null)
        {
            _equipmentView.SetVisible(_activeTab == 2);
            if (_activeTab == 2)
            {
                _equipmentView.Refresh();
                _equipmentView.EnsureSelection();
            }
        }

        UpdateTabButtonStates();
        if (_isOpen && previousTab != _activeTab)
            SelectInitial();
        
        // Mantener la cámara activa en todas las pestañas mientras el menú esté abierto
        SetEquipmentCameraActive(_isOpen);
    }

    void EnterUiInputScope()
    {
        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] EnterUiInputScope() - Cambiando a modo UI");
        #endif
        _inputScope?.Dispose();
        _inputScope = InputActionMapScope.EnterUiScope();
        
        // Asegurar que los eventos de input están suscritos (para sonidos automáticos de LB/RB)
        GamepadInputReader.EnsureInputEventsSubscribed();
        
        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log("[PlayerEquipmentMenu] InputScope creado");
        #endif
    }

    void ExitUiInputScope()
    {
        _inputScope?.Dispose();
        _inputScope = null;
    }

    void EnsureActionManager()
    {
        if (_actionManager != null) return;
        PlayerService.TryGetComponent(out _actionManager, includeInactive: true, allowSceneLookup: true);
    }

    void LateUpdate()
    {
        if (!_equipmentCameraActive || _playerPreviewTarget == null) return;

        // Solo permitir órbita en la pestaña de Equipamiento (index 2)
        bool allowOrbit = _activeTab == 2;

        if (_wasInOrbitMode && !allowOrbit)
        {
            // Salir de órbita: resetear yaw y volver a mirar hacia la cámara
            _previewPlayerYaw = 0f;
            ApplyPreviewFacingRotation();
        }
        _wasInOrbitMode = allowOrbit;

        if (allowOrbit)
        {
            // Leer directamente del hardware para evitar restricciones de supresión
            float rotateInput = GamepadInputReader.CameraLookRaw.x;

            if (Mathf.Abs(rotateInput) > 0.01f)
            {
                _previewPlayerYaw += rotateInput * previewOrbitSpeed * Time.unscaledDeltaTime;
            }

            // Rotar a Will sobre sí mismo: parte mirando hacia la cámara desplazada
            // y gira según el input del joystick para inspeccionar el equipo puesto.
            _playerPreviewTarget.rotation = Quaternion.Euler(0f, _previewBaseYaw - _previewPlayerYaw, 0f);
        }
        else if (!_isUsingItem)
        {
            // En el resto de pestañas, resetear el yaw y mantener a Will mirando a la cámara
            _previewPlayerYaw = 0f;
            ApplyPreviewFacingRotation();
        }
    }

    /// <summary>
    /// Orienta a Will hacia la posición actual de la cámara principal (desplazada para el menú),
    /// usando el yaw base calculado al abrir el menú.
    /// </summary>
    void ApplyPreviewFacingRotation()
    {
        if (_playerPreviewTarget == null) return;
        _playerPreviewTarget.rotation = Quaternion.Euler(0f, _previewBaseYaw, 0f);
    }

    bool TrySetupPreviewTarget()
    {
        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
        {
            return false;
        }

        _playerPreviewTarget = player.transform;
        _storedPlayerRotation = _playerPreviewTarget.rotation;

        // Buscar el Animator del player para poder mantener sus animaciones activas en el menú
        if (_playerAnimator == null)
        {
            _playerAnimator = _playerPreviewTarget.GetComponentInChildren<Animator>();
            if (_playerAnimator != null)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[PlayerEquipmentMenuController] Animator del player encontrado: {_playerAnimator.name}");
                #endif
            }
            else
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerEquipmentMenuController] No se encontró Animator en el player. Las animaciones no funcionarán en el menú.");
                #endif
            }
        }

        // Forzar al Animator a ir a idle (detener animaciones de movimiento)
        if (_playerAnimator != null)
        {
            // Resetear parámetros comunes de movimiento a 0 para forzar idle (solo si existen)
            TrySetAnimatorFloat(AnimHash_InputMagnitude, 0f);
            TrySetAnimatorFloat(AnimHash_Speed, 0f);
            TrySetAnimatorFloat(AnimHash_VerticalVelocity, 0f);

            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerEquipmentMenuController] Animator forzado a idle");
            #endif
        }

        _previewPlayerYaw = 0f;
        // La rotación final hacia la cámara se aplica en ApplyEquipmentMenuCameraOffset(),
        // una vez calculada la posición desplazada de la cámara principal.

        return true;
    }

    void SetEquipmentCameraActive(bool value)
    {
        if (_equipmentCameraActive == value) return;

        _equipmentCameraActive = value;

        if (_equipmentCameraActive)
        {
            // IMPORTANTE: Forzar reset del preview target para garantizar posicionamiento consistente
            // Esto asegura que _previewPlayerYaw se recalcule desde cero
            _playerPreviewTarget = null;
            if (TrySetupPreviewTarget())
                ApplyEquipmentMenuCameraOffset();
        }
        else
        {
            if (_playerPreviewTarget != null)
                _playerPreviewTarget.rotation = _storedPlayerRotation;

            _playerPreviewTarget = null;
            RestoreEquipmentMenuCamera();
        }
    }

    void EnsureMainCameraRefs()
    {
        if (mainThirdPersonCamera == null)
            mainThirdPersonCamera = ServiceLocator.Get<vThirdPersonCamera>(false);
        if (mainThirdPersonCamera != null && _mainCamera == null)
            _mainCamera = mainThirdPersonCamera.GetComponent<Camera>();
    }

    /// <summary>
    /// Desplaza lateralmente la cámara principal (Invector) para dejar a Will centrado en la mitad
    /// derecha de la pantalla mientras el menú de equipamiento está abierto. Desactiva el seguimiento
    /// normal de la cámara mientras dure el desplazamiento y restaura su posición exacta al cerrar.
    /// </summary>
    void ApplyEquipmentMenuCameraOffset()
    {
        EnsureMainCameraRefs();
        if (mainThirdPersonCamera == null || _mainCamera == null)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerEquipmentMenuController] No se encontró la cámara principal (vThirdPersonCamera). No se puede desplazar para el menú de equipamiento.");
            #endif
            return;
        }
        if (_mainCameraOffsetActive) return;

        Transform camT = _mainCamera.transform;
        Vector3 originalPosition = camT.position;

        Vector3 targetWorldPos = _playerPreviewTarget != null ? _playerPreviewTarget.position : camT.position + camT.forward * 3f;
        Vector3 lookPoint = targetWorldPos + Vector3.up * equipmentMenuCameraLookHeight;

        // Nivelar la cámara: conservar el yaw horizontal original pero eliminar cualquier
        // inclinación (pitch/roll) para que Will se vea recto en vez de "desde arriba".
        //
        // FIX INC-065: en una AmbientZone con ZoneCameraMode.TopDown la cámara mira casi en
        // vertical (camT.forward casi paralelo a Vector3.up), así que su componente horizontal
        // es minúscula y está dominada por ruido numérico. Al normalizarla, "nivelar" a partir de
        // ese vector casi degenerado producía un giro horizontal prácticamente aleatorio cada vez
        // que se abría/cerraba el menú de equipamiento en esas zonas ("la cámara se vuelve loca").
        // Si la cámara está casi vertical, usamos la orientación del propio personaje (siempre
        // bien definida, no depende del ángulo de la cámara) como base en su lugar.
        Vector3 flatForward = Vector3.zero;
        bool cameraNearVertical = Mathf.Abs(camT.forward.y) > 0.85f;
        if (!cameraNearVertical)
            flatForward = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f && _playerPreviewTarget != null)
            flatForward = Vector3.ProjectOnPlane(_playerPreviewTarget.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.ProjectOnPlane(targetWorldPos - originalPosition, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        Quaternion levelRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        Vector3 rightDir = levelRotation * Vector3.right;

        // Posición nivelada: mismo desplazamiento horizontal (X/Z) que la cámara original respecto
        // a Will, pero a la altura del punto de mira, ya sin inclinación.
        Vector3 levelPosition = new Vector3(originalPosition.x, lookPoint.y, originalPosition.z);

        Vector3 toTarget = lookPoint - levelPosition;
        float depth = Vector3.Dot(toTarget, flatForward);
        if (depth < 0.1f) depth = 3f; // Fallback de seguridad si el cálculo da un valor degenerado

        float halfWidthAtDepth = Mathf.Tan(_mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth * _mainCamera.aspect;
        float xOld = Vector3.Dot(toTarget, rightDir);
        float xDesired = (equipmentMenuTargetScreenX - 0.5f) * 2f * halfWidthAtDepth;
        float lateralShift = xDesired - xOld;

        Vector3 targetPos = levelPosition - rightDir * lateralShift;

        mainThirdPersonCamera.enabled = false;
        _mainCameraOffsetActive = true;

        _mainCameraTween?.Kill();
        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(camT.DOMove(targetPos, equipmentMenuCameraTransitionDuration).SetEase(Ease.OutCubic));
        seq.Join(camT.DORotateQuaternion(levelRotation, equipmentMenuCameraTransitionDuration).SetEase(Ease.OutCubic));
        _mainCameraTween = seq;

        // Orientar a Will hacia la nueva posición de la cámara para que quede mirando de frente
        if (_playerPreviewTarget != null)
        {
            Vector3 dirToCamera = targetPos - _playerPreviewTarget.position;
            dirToCamera.y = 0f;
            if (dirToCamera.sqrMagnitude > 0.001f)
            {
                Quaternion faceRot = Quaternion.LookRotation(dirToCamera.normalized);
                _previewBaseYaw = faceRot.eulerAngles.y;
                _playerPreviewTarget.rotation = faceRot;
            }
        }
    }

    /// <summary>
    /// Devuelve la cámara principal al gameplay al cerrar el menú de equipamiento. Se reactiva
    /// vThirdPersonCamera al instante y es ella la que vuelve suave a su sitio (misma vuelta que
    /// tras un diálogo o una cinemática). Antes el menú la devolvía con su propio tween de 0,4 s
    /// hacia la pose de ANTES de abrir, con la cámara apagada mientras el juego ya corría: si Will
    /// se movía en ese margen, al terminar el tween la cámara estaba en un sitio viejo y pegaba el
    /// salto para alcanzarlo.
    /// </summary>
    void RestoreEquipmentMenuCamera()
    {
        if (!_mainCameraOffsetActive) return;

        _mainCameraTween?.Kill();
        _mainCameraTween = null;
        _mainCameraOffsetActive = false;
        if (mainThirdPersonCamera != null)
            mainThirdPersonCamera.enabled = true;
    }

    // NO forzar idle si se está reproduciendo una animación de uso de item (beber poción, etc.)
    static bool ClipInfoMentionsItemUse(AnimatorClipInfo[] clipInfo)
    {
        if (clipInfo == null || clipInfo.Length == 0) return false;
        var clipName = clipInfo[0].clip.name;
        return clipName.Contains("DrinkPotion") || clipName.Contains("UseItem") || clipName.Contains("Consume");
    }

    void MaintainAnimatorIdle()
    {
        if (!_isOpen || _playerAnimator == null)
            return;
            
        // NUEVO: Si se está usando un item, no forzar idle
        if (_isUsingItem) return;

        // Verificar si se está reproduciendo una animación específica (como beber poción).
        // DrinkPotion_NoWeapon vive en la UpperBody layer (torso/brazos), no en la layer 0 —
        // se comprueba también esa capa para no perder esta salvaguarda (aunque en la práctica
        // _isUsingItem ya es la guardia principal, comprobada más arriba).
        var currentClipInfo = _playerAnimator.GetCurrentAnimatorClipInfo(0);
        bool playingItemClip = ClipInfoMentionsItemUse(currentClipInfo);
        if (!playingItemClip && _playerAnimator.layerCount > 1)
        {
            var upperClipInfo = _playerAnimator.GetCurrentAnimatorClipInfo(1);
            playingItemClip = ClipInfoMentionsItemUse(upperClipInfo);
        }
        if (playingItemClip)
        {
            return;
        }

        // Forzar parámetros a 0 para mantener idle (solo si existen en el Animator)
        TrySetAnimatorFloat(AnimHash_InputMagnitude, 0f);
        TrySetAnimatorFloat(AnimHash_Speed, 0f);
        TrySetAnimatorFloat(AnimHash_VerticalVelocity, 0f);

        // Asegurar que el AnimatorUpdateMode esté en UnscaledTime
        if (_playerAnimator.updateMode != AnimatorUpdateMode.UnscaledTime)
        {
            _playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }
    
    // NUEVO: Método público para establecer el flag
    public void SetUsingItem(bool value, float duration = 0f)
    {
        _isUsingItem = value;
        if (value && duration > 0f)
        {
            // Usar string para StopCoroutine por seguridad si la corrutina no estaba corriendo
            StopCoroutine("ResetUsingItemFlag"); 
            StartCoroutine(ResetUsingItemFlag(duration));
        }
    }

    System.Collections.IEnumerator ResetUsingItemFlag(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        _isUsingItem = false;
    }

    /// <summary>
    /// Intenta establecer un parámetro float del Animator solo si existe.
    /// </summary>
    void TrySetAnimatorFloat(int paramHash, float value)
    {
        if (_playerAnimator == null) return;
        
        // Verificar si el parámetro existe en el Animator
        foreach (var param in _playerAnimator.parameters)
        {
            if (param.nameHash == paramHash && param.type == AnimatorControllerParameterType.Float)
            {
                _playerAnimator.SetFloat(paramHash, value);
                return;
            }
        }
    }

    // Scope simple para gestionar el cambio UI/Gameplay usando PlayerInputManager centralizado
    sealed class InputActionMapScope : IDisposable
    {
        bool _disposed;

        InputActionMapScope()
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[InputActionMapScope] Constructor - Iniciando");
            #endif
            
            GamepadInputReader.PushGameplaySuppression(this);
            GamepadInputReader.PushUiNavigationScope();

            // Cambiar a modo UI centralizado
            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[InputActionMapScope] PlayerInputManager encontrado, llamando a PushUIMode()");
                #endif
                pim.PushUIMode();
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[InputActionMapScope] PushUIMode ejecutado. IsInUIMode: {pim.IsInUIMode}");
                #endif
            }
            else
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogError("[InputActionMapScope] PlayerInputManager NO encontrado en ServiceLocator!");
                #endif
            }
        }

        public static InputActionMapScope EnterUiScope()
        {
            return new InputActionMapScope();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Restaurar modo Gameplay centralizado
            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PopUIMode();

            GamepadInputReader.PopGameplaySuppression(this);
            GamepadInputReader.PopUiNavigationScope();
        }
    }

    int GetDefaultTab()
    {
        if (_inventoryView != null) return 0;
        if (_spellView != null) return 1;
        if (_equipmentView != null) return 2;
        return 0;
    }

    void UpdateTabButtonStates()
    {
        foreach (var button in _tabButtons)
        {
            if (button == null) continue;

            // Usar el índice real del tab registrado para este botón (no el índice en la lista)
            int tabIndex = _tabButtonIndices.TryGetValue(button, out var idx) ? idx : -1;
            bool isActive = tabIndex == _activeTab;
            button.interactable = !isActive;

            var colors = button.colors;
            if (isActive)
            {
                // Activo (interactable=false): disabledColor blanco para que se vea brillante
                colors.disabledColor  = Color.white;
                colors.colorMultiplier = 1f;
            }
            else
            {
                // Inactivos: tenues pero visibles
                colors.normalColor    = Color.white;
                colors.colorMultiplier = 0.45f;
            }
            button.colors = colors;
        }
    }

    void SelectInitial()
    {
        var finalTarget = ResolveInitialTarget();
        #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PlayerEquipmentMenu] SelectInitial tab={_activeTab} default={finalTarget?.name ?? "null"} rows={_inventoryView?.RowCount.ToString() ?? "-"} override={initialSelectionOverride?.name ?? "null"} -> target={finalTarget?.name ?? "null"}");
        #endif

        if (finalTarget != null)
            StartCoroutine(SelectOnNextFrame(finalTarget));
    }

    GameObject ResolveInitialTarget()
    {
        GameObject tabDefault = null;
        switch (_activeTab)
        {
            case 0:
                tabDefault = _inventoryView?.DefaultSelection;
                break;
            case 1:
                tabDefault = _spellView?.DefaultSelection;
                break;
            case 2:
                tabDefault = _equipmentView?.DefaultSelection;
                break;
        }

        return tabDefault ?? initialSelectionOverride ?? inventoryTabButton?.gameObject;
    }

    System.Collections.IEnumerator SelectOnNextFrame(GameObject target)
    {
        yield return null;
        if (target != null)
        {
            SelectGameObjectImmediate(target);
        }
    }

    void SelectGameObjectImmediate(GameObject target)
    {
        if (target == null) return;
        var selectable = target.GetComponent<Selectable>();
        if (selectable != null)
            selectable.Select();
    }

    bool IsInsideMenu(GameObject go)
    {
        if (go == null) return false;
        if (windowRoot != null)
            return go == windowRoot || go.transform.IsChildOf(windowRoot.transform);
        return go == gameObject || go.transform.IsChildOf(transform);
    }

    void UpdatePlayerInfoPanel()
    {
        bool hasStatsText = levelText != null || hpText != null || mpText != null
                            || hpBarFill != null || mpBarFill != null;
        bool hasAbilityUI = abilitiesRoot != null || abilityEntries.HasAnyEntry;
        if (!hasStatsText && !hasAbilityUI) return;

        PlayerPresetSO preset = null;
        if (GameBootService.IsAvailable && GameBootService.Profile != null)
            preset = GameBootService.Profile.GetActivePresetResolved();

        if (hasStatsText)
        {
            CacheBaseLabelsIfNeeded();

            if (levelText != null)
            {
                var value = preset != null ? preset.level.ToString() : "?";
                levelText.text = string.IsNullOrEmpty(_levelLabel) ? value : $"{_levelLabel} {value}";
            }

            // Vida: se resuelve una sola vez y alimenta tanto el texto como la barra.
            {
                float cur = -1f, max = -1f;
                if (PlayerService.TryGetComponent<PlayerHealthSystem>(out var health, includeInactive: true, allowSceneLookup: true))
                {
                    cur = health.CurrentHealth; max = health.MaxHealth;
                }
                else if (preset != null)
                {
                    cur = preset.currentHP; max = preset.maxHP;
                }

                if (hpText != null)
                {
                    string hpValue = max > 0f ? $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}" : "?";
                    hpText.text = string.IsNullOrEmpty(_hpLabel) ? hpValue : $"{_hpLabel} {hpValue}";
                }
                if (hpBarFill != null)
                    hpBarFill.fillAmount = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
            }

            // Mana: mismo patron.
            {
                float cur = -1f, max = -1f;
                if (PlayerService.TryGetComponent<ManaPool>(out var mana, includeInactive: true, allowSceneLookup: true))
                {
                    cur = mana.Current; max = mana.Max;
                }
                else if (preset != null)
                {
                    cur = preset.currentMP; max = preset.maxMP;
                }

                if (mpText != null)
                {
                    string mpValue = max > 0f ? $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}" : "?";
                    mpText.text = string.IsNullOrEmpty(_mpLabel) ? mpValue : $"{_mpLabel} {mpValue}";
                }
                if (mpBarFill != null)
                    mpBarFill.fillAmount = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
            }
        }

        UpdateAbilitiesPanel(preset);
    }

    void CacheBaseLabelsIfNeeded()
    {
        if (_labelsCached) return;

        // Capturar las etiquetas ya traducidas por LocalizedText una sola vez
        if (levelText != null)
            _levelLabel = levelText.text;
        if (hpText != null)
            _hpLabel = hpText.text;
        if (mpText != null)
            _mpLabel = mpText.text;

        _labelsCached = true;
    }

    void UpdateAbilitiesPanel(PlayerPresetSO preset)
    {
        if (!abilityEntries.HasAnyEntry)
        {
            if (abilitiesRoot != null)
                abilitiesRoot.SetActive(false);
            return;
        }

        var abilities = preset?.abilities ?? new PlayerAbilities();

        SetAbilityEntryActive(AbilityKey.Swim, abilities.swim);
        SetAbilityEntryActive(AbilityKey.Jump, abilities.jump);
        SetAbilityEntryActive(AbilityKey.Climb, abilities.climb);
        SetAbilityEntryActive(AbilityKey.Magic, abilities.magic);
        SetAbilityEntryActive(AbilityKey.Fly, abilities.fly);

        if (abilitiesRoot != null)
        {
            abilitiesRoot.SetActive(abilityEntries.HasAnyEntry);
        }
    }

    void SetAbilityEntryActive(AbilityKey key, bool active)
    {
        var entry = abilityEntries.Get(key);
        if (entry != null)
            entry.SetActive(active);
    }

    void AnimateHealthRestoreFeedback()
    {
        if (hpText == null) return;

        // Cachear el color original
        if (!_hpColorCached)
        {
            _hpOriginalColor = hpText.color;
            _hpColorCached = true;
        }

        // Matar animación previa si existe
        _hpTextTween?.Kill();

        // Color verde para indicar curación
        var healColor = new Color(0.2f, 1f, 0.3f, 1f);

        // Secuencia de animación: escala + color + regreso
        var sequence = DOTween.Sequence();
        sequence.Append(hpText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, vibrato: 8, elasticity: 0.6f).SetUpdate(true));
        sequence.Join(hpText.DOColor(healColor, 0.15f).SetUpdate(true));
        sequence.Append(hpText.DOColor(_hpOriginalColor, 0.25f).SetUpdate(true));
        
        _hpTextTween = sequence;
    }

    void AnimateManaRestoreFeedback()
    {
        if (mpText == null) return;

        // Cachear el color original
        if (!_mpColorCached)
        {
            _mpOriginalColor = mpText.color;
            _mpColorCached = true;
        }

        // Matar animación previa si existe
        _mpTextTween?.Kill();

        // Color azul/cyan para indicar restauración de maná
        var manaColor = new Color(0.3f, 0.7f, 1f, 1f);

        // Secuencia de animación: escala + color + regreso
        var sequence = DOTween.Sequence();
        sequence.Append(mpText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, vibrato: 8, elasticity: 0.6f).SetUpdate(true));
        sequence.Join(mpText.DOColor(manaColor, 0.15f).SetUpdate(true));
        sequence.Append(mpText.DOColor(_mpOriginalColor, 0.25f).SetUpdate(true));
        
        _mpTextTween = sequence;
    }

    bool EnsureViews()
    {
        bool anyViewConfigured = false;
        
        // Debug.Log($"[PlayerEquipmentMenuController] EnsureViews() - Verificando vistas...");
        // Debug.Log($"  - _inventoryView: {(_inventoryView != null ? "EXISTS" : "NULL")}");
        // Debug.Log($"  - _spellView: {(_spellView != null ? "EXISTS" : "NULL")}");
        // Debug.Log($"  - _equipmentView: {(_equipmentView != null ? "EXISTS" : "NULL")}");

        if (_inventoryView == null)
        {
            // Debug.Log($"[PlayerEquipmentMenuController] Verificando inventoryUI.IsConfigured...");
            if (inventoryUI.IsConfigured)
            {
                _inventoryView = new InventoryView(inventoryUI);
                anyViewConfigured = true;
                // Debug.Log("[PlayerEquipmentMenuController] Vista de inventario creada");
            }
            else if (!_warnedInventory)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerEquipmentMenuController] Inventario no configurado:");
                Debug.LogWarning($"  - root: {(inventoryUI.root != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - rowsParent: {(inventoryUI.rowsParent != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - rowPrefab: {(inventoryUI.rowPrefab != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - itemName: {(inventoryUI.itemName != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - itemDescription: {(inventoryUI.itemDescription != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - itemCount: {(inventoryUI.itemCount != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - feedbackText: {(inventoryUI.feedbackText != null ? "OK" : "FALTA")}");
                #endif
                _warnedInventory = true;
            }
        }
        else
        {
            anyViewConfigured = true;
            // Debug.Log("[PlayerEquipmentMenuController] Vista de inventario ya existe");
        }

        if (_spellView == null)
        {
            // Debug.Log($"[PlayerEquipmentMenuController] Verificando spellUI.IsConfigured...");
            if (spellUI.IsConfigured)
            {
                _spellView = new SpellView(spellUI);
                anyViewConfigured = true;
                // Debug.Log("[PlayerEquipmentMenuController] Vista de hechizos creada");
            }
            else if (!_warnedSpells)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerEquipmentMenuController] Vista de hechizos no configurada: asigna root, botones de slots, contenedor y prefab de filas.");
                #endif
                _warnedSpells = true;
            }
        }
        else
        {
            anyViewConfigured = true;
            // Debug.Log("[PlayerEquipmentMenuController] Vista de hechizos ya existe");
        }

        if (_equipmentView == null)
        {
            // Debug.Log($"[PlayerEquipmentMenuController] Verificando equipmentUI.IsConfigured...");
            if (equipmentUI.IsConfigured)
            {
                _equipmentView = new EquipmentView(equipmentUI);
                anyViewConfigured = true;
                // Debug.Log("[PlayerEquipmentMenuController] Vista de equipamiento creada");
                
                // CRÍTICO: Refrescar la vista para suscribirla a eventos
                _equipmentView.Refresh();
                // Debug.Log("[PlayerEquipmentMenuController] Vista de equipamiento refrescada y suscrita");
            }
            else if (!_warnedEquipment)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerEquipmentMenuController] Vista de equipamiento no configurada: añade filas con categoría y botones.");
                #endif
                _warnedEquipment = true;
            }
        }
        else
        {
            anyViewConfigured = true;
            // Debug.Log("[PlayerEquipmentMenuController] Vista de equipamiento ya existe");
        }

        // Debug.Log($"[PlayerEquipmentMenuController] EnsureViews() retornando: {anyViewConfigured}");
        
        if (!anyViewConfigured)
        {
            #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[PlayerEquipmentMenuController] âŒ NINGUNA VISTA ESTÃ CONFIGURADA");
            Debug.LogError("â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—");
            Debug.LogError("â•‘ SOLUCIÃ“N: El PlayerEquipmentMenuController necesita un Canvas UI  â•‘");
            Debug.LogError("â•‘ correctamente configurado con las siguientes vistas:               â•‘");
            Debug.LogError("â• â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•£");
            Debug.LogError("â•‘ 1. Crea un prefab 'PlayerEquipmentMenuCanvas' en la escena        â•‘");
            Debug.LogError("â•‘ 2. Asigna en el Inspector:                                         â•‘");
            Debug.LogError("â•‘    â€¢ Inventory UI: root, rowsParent, rowPrefab, etc.               â•‘");
            Debug.LogError("â•‘    â€¢ Spell UI: root, slotsContainer, etc.                          â•‘");
            Debug.LogError("â•‘    â€¢ Equipment UI: root y categorías configuradas                  â•‘");
            Debug.LogError("â•‘ 3. Añade el componente PlayerEquipmentMenuController al Canvas    â•‘");
            Debug.LogError("â•‘ 4. El controller debe estar en la escena Start o como DontDestroy â•‘");
            Debug.LogError("â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
            Debug.LogError($"GameObject actual: '{gameObject.name}' (Canvas: {(canvas != null ? "Sí" : "No")}, WindowRoot: {(windowRoot != null ? "Sí" : "No")})");
            #endif
        }
        
        return anyViewConfigured;
    }

    bool IsAllowedInCurrentScene()
    {
        if (string.IsNullOrEmpty(allowedSceneName)) return true;

        var activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() &&
               string.Equals(activeScene.name, allowedSceneName, StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    struct AbilityEntryReferences
    {
        public GameObject swim;
        public GameObject jump;
        public GameObject climb;
        public GameObject magic;
        public GameObject fly;

        public bool HasAnyEntry => swim != null || jump != null || climb != null || magic != null || fly != null;

        public GameObject Get(AbilityKey key)
        {
            return key switch
            {
                AbilityKey.Swim => swim,
                AbilityKey.Jump => jump,
                AbilityKey.Climb => climb,
                AbilityKey.Magic => magic,
                AbilityKey.Fly => fly,
                _ => null
            };
        }
    }

    [Serializable]
    class InventoryBindings
    {
        public GameObject root;
        public Transform rowsParent;
        public InventoryRowWidget rowPrefab;
        public Text itemName;
        public Text itemDescription;
        public Text itemCount;
        public Text feedbackText;
        public Button useButton;
        
        [Header("Scroll (opcional - se busca automáticamente si no se asigna)")]
        [Tooltip("ScrollRect del inventario. Si no se asigna, se busca automáticamente desde rowsParent.")]
        public ScrollRect scrollRect;
        
        [Header("Feedback visual")]
        public Color slotSelectionColor = new Color(1f, 0.82f, 0.16f, 1f); // Amarillo para resaltado

        public bool IsConfigured =>
            root != null &&
            rowsParent != null &&
            rowPrefab != null &&
            itemName != null &&
            itemDescription != null &&
            itemCount != null &&
            feedbackText != null;
    }

        class InventoryView
        {
            readonly InventoryBindings _ui;
            readonly List<InventoryRowWidget> _rows = new();
            Inventory _inventory;
            Inventory _boundInventory;
            PlayerPickupCollector _collector;
            ItemData _selectedItem;
            InventoryRowWidget _lastSelectedRow;
            InventoryRowWidget _highlightedRow; // Fila actualmente resaltada (navegación)
            readonly ScrollRect _scrollRect;
            enum InventoryInteractionState { Browsing, UseButtonFocused }
            InventoryInteractionState _interactionState = InventoryInteractionState.Browsing;
            Vector3 _useButtonBaseScale;
            bool _useButtonVisualCached;
            DG.Tweening.Tween _useButtonTween; // Tween del botón para poder cancelarlo al cerrar

        public InventoryView(InventoryBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            // Intentar usar el ScrollRect asignado manualmente, o buscarlo automáticamente
            if (_ui.scrollRect != null)
            {
                _scrollRect = _ui.scrollRect;
                // Debug.Log($"[InventoryView] ✅ ScrollRect asignado manualmente: {_scrollRect.name}");
            }
            else if (_ui.rowsParent != null)
            {
                _scrollRect = _ui.rowsParent.GetComponentInParent<ScrollRect>();
                if (_scrollRect != null)
                {
                    #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.Log($"[InventoryView] ✅ ScrollRect encontrado automáticamente: {_scrollRect.name}");
                    #endif
                }
                else
                {
                    #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.LogWarning($"[InventoryView] ⚠️ ScrollRect NO encontrado. Asigna manualmente el ScrollRect en el Inspector (Inventory UI → Scroll Rect) o verifica que '{_ui.rowsParent.name}' esté bajo un GameObject con ScrollRect.");
                    #endif
                }
            }

            if (_ui.useButton != null)
            {
                _ui.useButton.onClick.AddListener(UseSelectedItem);
                _useButtonBaseScale = _ui.useButton.transform.localScale;
                _useButtonVisualCached = true;

                var nav = _ui.useButton.navigation;
                nav.mode = Navigation.Mode.None;
                _ui.useButton.navigation = nav;
            }
        }

        public GameObject DefaultSelection => _rows.Count > 0 ? _rows[0].ButtonGameObject : null;
        public int RowCount => _rows.Count;

        public void Dispose()
        {
            if (_boundInventory != null)
                _boundInventory.OnInventoryChanged -= HandleInventoryChanged;

            if (_ui.useButton != null)
                _ui.useButton.onClick.RemoveListener(UseSelectedItem);
        }


        public void SetVisible(bool value)
        {
            if (_ui.root != null)
                _ui.root.SetActive(value);

            if (!value)
            {
                ExitUseButtonFocus(false);
                ResetUseButtonFeedback();
                if (_ui.useButton != null)
                {
                    _ui.useButton.interactable = false;
                    _ui.useButton.gameObject.SetActive(false);
                }

                var selected = EventSystem.current?.currentSelectedGameObject;
                if (_ui.root != null && selected != null && selected.transform.IsChildOf(_ui.root.transform))
                    EventSystem.current?.SetSelectedGameObject(null);

                // Matar tweens pendientes al ocultar
                _useButtonTween?.Kill();
                _useButtonTween = null;
                
                if (_boundInventory != null)
                {
                    _boundInventory.OnInventoryChanged -= HandleInventoryChanged;
                    _boundInventory = null;
                }
            }
        }

        bool IsInventoryInputContextValid()
        {
            if (Instance == null) return false;
            if (Instance._activeTab != 0) return false;
            if (_ui.root == null) return false;
            return _ui.root.activeInHierarchy;
        }

        public void Refresh(bool rebuildList)
        {
            if (!PlayerService.TryGetComponent(out _inventory, includeInactive: true, allowSceneLookup: true))
                _inventory = null;

            PlayerService.TryGetComponent(out _collector, includeInactive: true, allowSceneLookup: true);

            if (_boundInventory != _inventory)
            {
                if (_boundInventory != null)
                    _boundInventory.OnInventoryChanged -= HandleInventoryChanged;
                if (_inventory != null)
                    _inventory.OnInventoryChanged += HandleInventoryChanged;
                _boundInventory = _inventory;
            }

            if (_inventory == null)
            {
                ClearList();
                UpdateEmptyState(LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("INVENTORY_UNAVAILABLE", "Inventario no disponible")
                    : "Inventario no disponible");
                return;
            }

            if (rebuildList)
                BuildList();
            else
                UpdateRowTexts();

            // Priorizar restaurar la selección previa; si no existe, enfocar la primera fila para permitir la navegación inmediata
            if (_selectedItem != null)
            {
                UpdateSelectedItemDetails();
            }
            else
            {
                // Limpiar detalles si no hay selección
                if (_ui.itemName != null) _ui.itemName.text = "";
                if (_ui.itemDescription != null) _ui.itemDescription.text = "";
                if (_ui.useButton != null) _ui.useButton.gameObject.SetActive(false);
                _interactionState = InventoryInteractionState.Browsing;
                ResetUseButtonFeedback();
            }
        }

        void BuildList()
        {
            ClearList();

            var items = _inventory.GetAllItems();
            items.Sort((a, b) => string.Compare(a.item ? a.item.displayName : string.Empty,
                                                b.item ? b.item.displayName : string.Empty,
                                                StringComparison.OrdinalIgnoreCase));

            // PERF (revisión rendimiento 24/08): antes esto era ClearList() (Destroy de TODAS las
            // filas) + un Instantiate por objeto, en CADA apertura del inventario con la pestaña por
            // defecto — no solo la primera vez. El coste crecía con el número de objetos acumulados,
            // que es justo el patrón "se nota cuando llevas jugando un rato" reportado. Ahora se
            // reutilizan como pool los GameObjects ya hijos de rowsParent (mismo patrón que
            // ShopUI.RebuildItemList) y solo se Instancia cuando el pool se queda corto.
            int usedChildren = 0;

            foreach (var entry in items)
            {
                InventoryRowWidget widget;
                if (usedChildren < _ui.rowsParent.childCount)
                {
                    var child = _ui.rowsParent.GetChild(usedChildren);
                    widget = child.GetComponent<InventoryRowWidget>();
                    if (widget == null)
                        widget = UnityEngine.Object.Instantiate(_ui.rowPrefab, _ui.rowsParent);
                    else
                        child.gameObject.SetActive(true);
                }
                else
                {
                    widget = UnityEngine.Object.Instantiate(_ui.rowPrefab, _ui.rowsParent);
                }
                usedChildren++;

                widget.Configure(entry.item);
                widget.RefreshLabel(_inventory);

                // Garantizar auto-scroll al seleccionar: añadir/configurar ScrollOnSelectRelay
                var rect = widget.GetComponent<RectTransform>();
                if (rect != null && _scrollRect != null)
                {
                    var relay = widget.GetComponent<ScrollOnSelectRelay>();
                    if (relay == null)
                        relay = widget.gameObject.AddComponent<ScrollOnSelectRelay>();
                    relay.scrollRect = _scrollRect;
                    relay.target = rect;
                }
                else if (_scrollRect == null)
                {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.LogWarning("[InventoryView] ⚠️ No se puede añadir ScrollOnSelectRelay: ScrollRect es null");
#endif
                }

                // RegisterClickHandler/RegisterSelectedHandler reasignan el delegate (y hacen
                // RemoveListener antes de AddListener sobre el mismo método estático) en vez de
                // acumular con +=, así que reutilizar una fila del pool con handlers nuevos es seguro.
                var capturedWidget = widget;
                var capturedItem = entry.item;
                widget.RegisterClickHandler(() => HandleRowActivated(capturedWidget, capturedItem, true));
                widget.RegisterSelectedHandler(() => HandleRowActivated(capturedWidget, capturedItem, false));

                _rows.Add(widget);
            }

            // Ocultar (no destruir) las filas sobrantes de una construcción anterior con más objetos
            // — quedan listas para reutilizarse la próxima vez que el inventario tenga más items.
            for (int i = usedChildren; i < _ui.rowsParent.childCount; i++)
                _ui.rowsParent.GetChild(i).gameObject.SetActive(false);

            UpdateRowNavigation();
            
            // Inicializar sin selección
            _highlightedRow = null;
            _selectedItem = null;
            UpdateRowVisuals();

            if (_rows.Count == 0)
                UpdateEmptyState(LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("INVENTORY_EMPTY", "Inventario vacío")
                    : "Inventario vacío");
        }

        void HandleRowActivated(InventoryRowWidget widget, ItemData item, bool focus)
        {
            bool selectionChanged = _selectedItem != item;
            _selectedItem = item;
            _highlightedRow = widget;
            _lastSelectedRow = widget; // Asignar también para que TryHandleSubmit funcione
            
            // Actualizar resaltado visual de todas las filas SIEMPRE (para que se vea al navegar)
            UpdateRowVisuals();
            
            // Solo hacer focus si es necesario
            if (focus)
                FocusRow(widget, true);
            
            UpdateSelectedItemDetails();

            // Si cambió la selección, limpiar feedback
            if (selectionChanged)
            {
                ClearFeedbackImmediate();
                ExitUseButtonFocus(false);
            }

            // NO llamar a HandleRowSubmit automáticamente - solo con Submit del gamepad
        }

        /// <summary>
        /// Actualiza el resaltado visual de todas las filas (amarillo para la seleccionada)
        /// </summary>
        void UpdateRowVisuals()
        {
            foreach (var row in _rows)
            {
                if (row == null) continue;
                bool isHighlighted = row == _highlightedRow;
                row.SetHighlighted(isHighlighted, _ui.slotSelectionColor);
            }
        }

        void FocusRow(InventoryRowWidget widget, bool forceFocus)
        {
            if (widget == null || !widget.gameObject.activeInHierarchy) return;

            if (forceFocus)
                widget.Focus();

            ScrollToRow(widget);
        }

        void ScrollToRow(InventoryRowWidget widget)
        {
            if (widget == null) return;
            if (_scrollRect == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[InventoryView] ScrollRect no encontrado en el padre de rowsParent. Verifica que el contenedor esté bajo un ScrollRect.");
#endif
                return;
            }
            var rect = widget.GetComponent<RectTransform>();
            ScrollRectAutoScroller.ScrollTo(_scrollRect, rect, 10f);
        }

        void UpdateRowTexts()
        {
            foreach (var widget in _rows)
                widget?.RefreshLabel(_inventory);
        }

        void ClearList()
        {
            // PERF (revisión rendimiento 24/08): antes se hacía Destroy() de cada fila; ahora se
            // desactivan para reutilizarlas como pool en la próxima BuildList() (ver comentario allí).
            foreach (var widget in _rows)
            {
                if (widget != null)
                    widget.gameObject.SetActive(false);
            }
            _rows.Clear();
            _selectedItem = null;
            _lastSelectedRow = null;
            _interactionState = InventoryInteractionState.Browsing;
            ResetUseButtonFeedback();
        }

        void UpdateSelectedItemDetails()
        {
            if (_selectedItem == null)
            {
                UpdateEmptyState(LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("INVENTORY_SELECT_ITEM", "Selecciona un objeto")
                    : "Selecciona un objeto");
                return;
            }

            if (_ui.itemName != null)
                _ui.itemName.text = _selectedItem.GetLocalizedName();

            if (_ui.itemDescription != null)
            {
                string localizedDesc = _selectedItem.GetLocalizedDescription();
                _ui.itemDescription.text = string.IsNullOrEmpty(localizedDesc)
                    ? (LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.Get("INVENTORY_NO_DESCRIPTION", "Sin descripción.")
                        : "Sin descripción.")
                    : localizedDesc;
            }

            if (_ui.itemCount != null)
            {
                int count = _inventory != null ? _inventory.Count(_selectedItem.itemId) : 0;
                string countFmt = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("INVENTORY_QUANTITY_LABEL", "Cantidad: {0}")
                    : "Cantidad: {0}";
                _ui.itemCount.text = string.Format(countFmt, count);
            }

            if (_ui.useButton != null)
            {
                _ui.useButton.gameObject.SetActive(true);
                // El botón permanece deshabilitado hasta que se haga Submit en el item
                _ui.useButton.interactable = false;
            }
        }

        void UpdateEmptyState(string message)
        {
            if (_ui.itemName != null) _ui.itemName.text = message;
            if (_ui.itemDescription != null) _ui.itemDescription.text = string.Empty;
            if (_ui.itemCount != null) _ui.itemCount.text = string.Empty;
            ClearFeedbackImmediate();
            if (_ui.useButton != null)
            {
                _ui.useButton.interactable = false;
                ResetUseButtonFeedback();
            }
            _interactionState = InventoryInteractionState.Browsing;
        }

        void UseSelectedItem()
        {
            if (!IsInventoryInputContextValid())
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[InventoryView] Ignorando UseSelectedItem fuera del tab de inventario.");
#endif
                return;
            }

            if (_inventory == null || _selectedItem == null) return;
            
            // NUEVO: Activar flag INMEDIATAMENTE
            if (Instance != null)
            {
                Instance.SetUsingItem(true, 2.5f); // Aumentado un poco por seguridad
            }
            
            // Cambiar estado pero NO resetear visualmente todavía
            _interactionState = InventoryInteractionState.Browsing;

            // Detectar qué efectos tiene el item para animar después
            bool hasHealthRestore = false;
            bool hasManaRestore = false;
            
            if (_selectedItem.useEffects != null)
            {
                foreach (var effect in _selectedItem.useEffects)
                {
                    if (effect.effectType == PickupEffectType.HealthRestore)
                        hasHealthRestore = true;
                    else if (effect.effectType == PickupEffectType.ManaRestore)
                        hasManaRestore = true;
                }
            }

            var context = new InventoryItemUseContext(_inventory, _selectedItem, _collector);
            var result = DispatchInventoryUseRequest(context);

            if (!result.handled)
            {
                if (!InventoryUseUtility.TryUseItem(_inventory, _selectedItem, _collector, out var reason, out var consumed))
                {
                    ShowFeedback(string.IsNullOrEmpty(reason) ? "No se pudo usar." : reason);
                    // Resetear el botón porque falló
                    ResetUseButtonAfterUse(false);
                    return;
                }

                result.handled = true;
                result.consumed = consumed;
            }

            if (result.consumed && _inventory.Count(_selectedItem.itemId) == 0)
                _selectedItem = null;

            Refresh(true);

            EnsureSelection();

            if (string.IsNullOrEmpty(result.message))
                result.message = "Usado correctamente.";

            ShowFeedback(result.message);

            // Refrescar panel de estadísticas inmediatamente (especialmente al usar pociones)
            Instance?.UpdatePlayerInfoPanel();
            
            // Animar feedback visual según el tipo de efecto
            if (hasHealthRestore)
                Instance?.AnimateHealthRestoreFeedback();
            if (hasManaRestore)
                Instance?.AnimateManaRestoreFeedback();
            
            // Resetear el botón después de un breve delay para que se vea el efecto
            ResetUseButtonAfterUse(true);
        }
        
        void ResetUseButtonAfterUse(bool restoreSelection)
        {
            // Matar cualquier tween previo
            _useButtonTween?.Kill();
            _useButtonTween = null;
            
            // Pequeño delay para que se vean las animaciones antes de resetear
            if (_ui.useButton != null)
            {
                _useButtonTween = _ui.useButton.transform
                    .DOScale(_useButtonBaseScale, 0.2f)
                    .SetDelay(0.3f)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true)
                    .OnComplete(() => {
                        _useButtonTween = null;
                        
                        // Verificar que el UI sigue activo antes de hacer cualquier cosa
                        if (_ui.root == null || !_ui.root.activeInHierarchy)
                            return;
                        
                        if (_ui.useButton != null)
                        {
                            _ui.useButton.interactable = false;
                            // Restaurar color del Image
                            var buttonImage = _ui.useButton.GetComponent<UnityEngine.UI.Image>();
                            if (buttonImage != null)
                            {
                                buttonImage.color = Color.white;
                            }
                            _ui.useButton.transform.localScale = _useButtonBaseScale;
                        }
                        
                        if (restoreSelection && _lastSelectedRow != null)
                            FocusRow(_lastSelectedRow, true);
                    });
            }
            else if (restoreSelection && _lastSelectedRow != null)
            {
                FocusRow(_lastSelectedRow, true);
            }
        }

        static void ShowFeedback(string message)
        {
            var instance = Instance;
            var view = instance?._inventoryView;
            if (view == null || view._ui.feedbackText == null)
                return;

            if (instance._clearFeedbackRoutine != null)
                instance.StopCoroutine(instance._clearFeedbackRoutine);

            view._ui.feedbackText.text = message ?? string.Empty;

            if (instance.feedbackDuration > 0f)
                instance._clearFeedbackRoutine = instance.StartCoroutine(view.ClearFeedbackAfterDelay(instance.feedbackDuration));
        }

        static void ClearFeedbackImmediate()
        {
            var instance = Instance;
            var view = instance?._inventoryView;
            if (view == null)
                return;

            if (view._ui.feedbackText != null)
                view._ui.feedbackText.text = string.Empty;

            if (instance._clearFeedbackRoutine != null)
            {
                instance.StopCoroutine(instance._clearFeedbackRoutine);
                instance._clearFeedbackRoutine = null;
            }
        }

        System.Collections.IEnumerator ClearFeedbackAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            ClearFeedbackImmediate();
        }

        void HandleInventoryChanged(ItemData item, int newAmount)
        {
            Refresh(false);
        }

        public bool EnsureSelection()
        {
            if (_rows.Count == 0) return false;

            // Solo restaurar el foco si ya había una selección previa
            // No forzar selección automática al abrir el menú
            if (_lastSelectedRow != null)
            {
                _lastSelectedRow.Focus();
                return true;
            }

            var first = _rows[0];
            HandleRowActivated(first, first.Item, true);
            return true;
        }

        public System.Collections.IEnumerator EnsureSelectionDelayed()
        {
            // Esperar un frame para que Unity cree los elementos
            yield return null;
            
            if (_rows.Count == 0) yield break;

            // Restaurar la selección previa si existe
            if (_lastSelectedRow != null)
            {
                yield return null;
                _lastSelectedRow.Focus();
                yield break;
            }

            // Seleccionar el primer elemento
            var first = _rows[0];
            if (first != null && first.ButtonGameObject != null)
            {
                yield return null;
                HandleRowActivated(first, first.Item, true);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[PlayerEquipmentMenu] Inventario - Seleccionado: {first.Item?.displayName}");
#endif
            }
        }

        void UpdateRowNavigation()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var button = _rows[i] != null ? _rows[i].GetComponent<Button>() : null;
                if (button == null) continue;

                var nav = button.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? _rows[i - 1]?.GetComponent<Button>() : button;
                nav.selectOnDown = i < _rows.Count - 1 ? _rows[i + 1]?.GetComponent<Button>() : button;
                button.navigation = nav;
            }
        }

        bool CanUseSelectedItem()
        {
            if (_selectedItem == null || !_selectedItem.usableFromInventory)
                return false;
            if (_inventory == null)
                return false;
            return _inventory.Count(_selectedItem.itemId) > 0;
        }

        void HandleRowSubmit()
        {
            if (!CanUseSelectedItem())
                return;

            FocusUseButton();
        }

        void FocusUseButton()
        {
            if (_ui.useButton == null) return;
            if (_selectedItem == null || !_selectedItem.usableFromInventory) return;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[InventoryView] FocusUseButton - Cambiando estado a UseButtonFocused");
#endif
            _interactionState = InventoryInteractionState.UseButtonFocused;

            // Habilitar el botón si no lo está
            if (!_ui.useButton.interactable)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[InventoryView] Habilitando botón useButton");
#endif
                _ui.useButton.interactable = true;
            }

            // Aplicar el feedback visual inmediatamente (sin esperar frames)
            PlayUseButtonFeedback();
            
            // Reproducir sonido de selección/confirmación
            GamepadInputReader.PlayUISound("UI_Select");
            
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[InventoryView] FocusUseButton completado - Estado final: {_interactionState}");
#endif
        }

        void ExitUseButtonFocus(bool restoreSelection)
        {
            if (_interactionState != InventoryInteractionState.UseButtonFocused)
                return;

            _interactionState = InventoryInteractionState.Browsing;
            ResetUseButtonFeedback();
            
            // Deshabilitar el botón al volver a la lista
            if (_ui.useButton != null)
                _ui.useButton.interactable = false;

            if (restoreSelection && _lastSelectedRow != null)
                FocusRow(_lastSelectedRow, true);
        }

        void PlayUseButtonFeedback()
        {
            if (_ui.useButton == null) return;

            // Color amarillo/dorado
            Color yellowColor = new Color(1f, 0.85f, 0.2f, 1f);
            
            // Obtener el Image del botón y cambiar su color directamente
            var buttonImage = _ui.useButton.GetComponent<UnityEngine.UI.Image>();
            if (buttonImage != null)
            {
                // ⭐ SOLUCIÓN SIMPLE: Cambiar el color del Image directamente
                buttonImage.color = yellowColor;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[InventoryView] Color amarillo aplicado directamente al Image");
#endif
            }

            // Animación de escala simple
            if (!_useButtonVisualCached)
            {
                _useButtonBaseScale = _ui.useButton.transform.localScale;
                _useButtonVisualCached = true;
            }
            
            var targetScale = _useButtonBaseScale * 1.1f;
            _ui.useButton.transform
                .DOScale(targetScale, 0.2f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
                
            // Seleccionar el botón
            _ui.useButton.Select();
        }

        void ResetUseButtonFeedback()
        {
            if (_ui.useButton == null || !_useButtonVisualCached)
                return;

            // Restaurar escala
            _ui.useButton.transform.localScale = _useButtonBaseScale;
            
            // Restaurar color del Image a blanco (el color por defecto)
            var buttonImage = _ui.useButton.GetComponent<UnityEngine.UI.Image>();
            if (buttonImage != null)
            {
                buttonImage.color = Color.white; // Color por defecto
            }
        }

        public bool TryHandleCancel()
        {
            if (!IsInventoryInputContextValid())
                return false;

            if (_interactionState == InventoryInteractionState.UseButtonFocused)
            {
                ExitUseButtonFocus(true);
                return true;
            }
            return false;
        }

        public bool TryHandleSubmit()
        {
            if (!IsInventoryInputContextValid())
                return false;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[InventoryView] TryHandleSubmit - Estado: {_interactionState}, SelectedRow: {(_lastSelectedRow != null ? "OK" : "NULL")}, SelectedItem: {(_selectedItem != null ? _selectedItem.displayName : "NULL")}");
#endif
            
            if (_interactionState == InventoryInteractionState.UseButtonFocused)
            {
                // Segunda pulsación: Usar el item
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[InventoryView] Segunda pulsación - Usando item");
#endif
                UseSelectedItem();
                return true;
            }

            if (_lastSelectedRow != null && _selectedItem != null)
            {
                // Primera pulsación: Enfocar botón de usar
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[InventoryView] Primera pulsación - Enfocando botón de usar");
#endif
                HandleRowSubmit();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[InventoryView] Después de HandleRowSubmit - Estado: {_interactionState}");
#endif
                return true;
            }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[InventoryView] TryHandleSubmit - No hay nada que hacer");
#endif
            return false;
        }
    }

    [Serializable]
    class SpellBindings
    {
        public GameObject root;
        
        // Los nombres de campo son los de antes (izquierdo/derecho/especial) para no perder las
        // referencias de la escena; desde INC-485 son los básicos 1, 2 y 3, y 'fourthSlot' el 4.
        [Header("Básico 1")]
        public Button leftSlotButton;
        public Text leftSlotLabel;

        [Header("Básico 2")]
        public Button rightSlotButton;
        public Text rightSlotLabel;

        [Header("Básico 3")]
        public Button specialSlotButton;
        public Text specialSlotLabel;

        [Header("Básico 4 (opcional)")]
        public Button fourthSlotButton;
        public Text fourthSlotLabel;

        [Header("Iconos de las ranuras")]
        [Tooltip("Imagen de ranura vacía. Si se deja vacía, la ranura conserva su imagen.")]
        public Sprite emptySlotSprite;
        
        [Header("Lista de hechizos")]
        public Transform rowsParent;
        public SpellRowWidget rowPrefab;
        public Text detailsText;
        [Tooltip("Línea de abajo con lo que hace A en cada momento (equipar, quitar, mover...). Lleva iconos de botón.")]
        public TMP_Text hintLabel;
        
        [Header("Scroll (opcional - se busca automáticamente si no se asigna)")]
        [Tooltip("ScrollRect de hechizos. Si no se asigna, se busca automáticamente desde rowsParent.")]
        public ScrollRect scrollRect;
        
        [Header("Feedback visual")]
        public Color slotSelectionColor = new Color(1f, 0.82f, 0.16f, 1f);

        public bool IsConfigured =>
            root != null &&
            leftSlotButton != null &&
            rightSlotButton != null &&
            specialSlotButton != null &&
            leftSlotLabel != null &&
            rightSlotLabel != null &&
            specialSlotLabel != null &&
            rowsParent != null &&
            rowPrefab != null &&
            detailsText != null;
    }

    class SpellView
    {
        // Pestaña Hechizos (INC-515). Cada personaje lleva hasta 4 básicos, seguidos y en el orden en
        // que rotan con LB:
        // - A sobre un básico de la lista lo pone en el primer hueco libre, o lo quita si ya lo lleva.
        //   Con los 4 ocupados, pregunta cuál cambiar.
        // - A sobre un hueco lleno lo coge para cambiarlo de sitio (el orden de LB).
        // - La lista solo trae lo que se equipa en la X. Los combos (Y) y la levitación se ven en el grimorio.
        // Will guarda sus básicos en PlayerPresetSO.basicSpellIds; Estela y Liam, en su entrada de
        // companionBasics (GrimorioDelPersonaje).

        enum Mode { Browse, ReplaceSlot, MoveSlot }

        class RowEntry
        {
            public MagicSpellSO spell;
            public SpellRowWidget widget;
        }

        const string Gold = "#FFD54A";
        const string Muted = "#9A94C8";
        const string RowNumber = "#C98B00";          // número de ranura sobre la fila clara
        const string RowNumberSelected = "#5A3A00";  // y sobre la fila resaltada (fondo dorado)

        readonly SpellBindings _ui;
        readonly Button[] _slotButtons;
        readonly Text[] _slotLabels;
        readonly ScrollRect _scrollRect;
        readonly List<RowEntry> _rows = new();
        readonly List<SpellId> _equipped = new(MagicCaster.BasicSlotCount);
        readonly Dictionary<Button, ColorBlock> _slotDefaultColors = new();
        readonly Dictionary<Button, Vector3> _slotBaseScales = new();
        readonly Dictionary<Button, Tween> _slotTweens = new();

        PartyControlManager.CharacterSlot _slot = PartyControlManager.CharacterSlot.Will;
        PlayerPresetSO _preset;
        PlayerPresetService _presetService;

        Mode _mode = Mode.Browse;
        SpellId _pendingSpell = SpellId.None; // ReplaceSlot: el que entra
        int _pendingSlot = -1;                // MoveSlot: el hueco que se mueve
        int _focusedSlot = -1;                // hueco con el foco; -1 = el foco está en la lista
        RowEntry _highlightedRow;
        string _notice;                       // aviso de una línea hasta el siguiente movimiento
        Core.InputGlyphs.InputGlyphDeviceFamily _hintFamily;

        bool EsWill => _slot == PartyControlManager.CharacterSlot.Will;
        int SlotCount => Mathf.Min(_slotButtons.Length, MagicCaster.BasicSlotCount);

        static string Loc(string key, string fallback) =>
            LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, fallback) : fallback;

        public SpellView(SpellBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            var buttons = new List<Button> { _ui.leftSlotButton, _ui.rightSlotButton, _ui.specialSlotButton };
            var labels = new List<Text> { _ui.leftSlotLabel, _ui.rightSlotLabel, _ui.specialSlotLabel };
            if (_ui.fourthSlotButton != null && _ui.fourthSlotLabel != null)
            {
                buttons.Add(_ui.fourthSlotButton);
                labels.Add(_ui.fourthSlotLabel);
            }
            _slotButtons = buttons.ToArray();
            _slotLabels = labels.ToArray();
            for (int i = 0; i < _slotButtons.Length; i++)
                ConfigureSlotButton(_slotButtons[i], i);

            _scrollRect = _ui.scrollRect != null
                ? _ui.scrollRect
                : (_ui.rowsParent != null ? _ui.rowsParent.GetComponentInParent<ScrollRect>() : null);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (_scrollRect == null)
                Debug.LogWarning("[SpellView] Sin ScrollRect: asígnalo en Spell UI → Scroll Rect.");
#endif
        }

        // ── API para el menú ──────────────────────────────────────────────

        public GameObject DefaultSelection
        {
            get
            {
                if (_rows.Count > 0 && _rows[0]?.widget != null) return _rows[0].widget.ButtonGameObject;
                foreach (var b in _slotButtons)
                    if (b != null) return b.gameObject;
                return _ui.root;
            }
        }

        /// El hechizo resaltado en la lista (el grimorio se abre por él, INC-506).
        public SpellId HighlightedSpell => _highlightedRow?.spell != null ? _highlightedRow.spell.spellId : SpellId.None;

        public void SetVisible(bool value)
        {
            if (_ui.root != null) _ui.root.SetActive(value);
            if (!value)
            {
                ResetMode();
                KillAllSlotFeedback();
            }
        }

        public void Refresh()
        {
            _preset = GameBootService.IsAvailable && GameBootService.Profile != null
                ? GameBootService.Profile.GetActivePresetResolved()
                : null;
            if (_preset != null)
                PlayerService.TryGetComponent(out _presetService, includeInactive: true, allowSceneLookup: true);
            _slot = PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : PartyControlManager.CharacterSlot.Will;

            ResetMode();
            LoadEquipped();
            BuildList();
            UpdateSlots();
            UpdateNavigation();
            ShowDetails(null, -1);

            // Recién aprendido uno: la lista se abre por él (INC-503).
            var nuevo = GrimorioDelPersonaje.UltimoAprendido;
            if (nuevo != SpellId.None && FocusRow(nuevo))
                GrimorioDelPersonaje.UltimoAprendido = SpellId.None;
            UpdateHint();
        }

        public void HandleInput()
        {
            if (_ui.root == null || !_ui.root.activeInHierarchy) return;
            if (Core.InputGlyphs.InputGlyphService.CurrentFamily != _hintFamily) UpdateHint();
        }

        public void CancelSlotSelection(bool silent)
        {
            ResetMode();
            if (!silent) UpdateHint();
        }

        /// B: sale del modo en curso, o vuelve de los huecos a la lista. False si no había nada que cancelar.
        public bool TryHandleCancel()
        {
            if (_mode != Mode.Browse)
            {
                int back = _mode == Mode.MoveSlot ? _pendingSlot : -1;
                var spell = _pendingSpell;
                ResetMode();
                if (back >= 0) FocusSlot(back);
                else if (!FocusRow(spell)) FocusList();
                UpdateHint();
                return true;
            }
            if (_focusedSlot >= 0)
            {
                FocusList();
                return true;
            }
            return false;
        }

        // ── Lo que lleva equipado ─────────────────────────────────────────

        bool Equipable(MagicSpellSO s) => s != null && s.caster == _slot && GrimorioDelPersonaje.EsBasico(s);

        /// Lee los básicos del personaje, sin huecos ni repetidos ni hechizos que no puede llevar. Si
        /// había algo que limpiar, lo guarda ya.
        void LoadEquipped()
        {
            _equipped.Clear();
            if (_preset == null) return;

            var src = EsWill ? _preset.basicSpellIds : GrimorioDelPersonaje.IdsEquipados(_preset, _slot, crear: false);
            if (src == null)
            {
                foreach (var s in GrimorioDelPersonaje.BasicosEquipados(_slot))
                    if (s != null) _equipped.Add(s.spellId);
                return;
            }

            foreach (var id in src)
                if (_equipped.Count < SlotCount && !_equipped.Contains(id) && Equipable(GrimorioDelPersonaje.Hechizo(id)))
                    _equipped.Add(id);

            bool igual = src.Count == _equipped.Count;
            for (int i = 0; igual && i < src.Count; i++) igual = src[i] == _equipped[i];
            if (!igual) Save();
        }

        void Save()
        {
            if (_preset == null) return;
            List<SpellId> dst;
            if (EsWill) dst = _preset.basicSpellIds ??= new List<SpellId>();
            else dst = GrimorioDelPersonaje.IdsEquipados(_preset, _slot, crear: true);
            if (dst == null) return;
            dst.Clear();
            dst.AddRange(_equipped);

            if (EsWill) _presetService?.ApplyCurrentPreset(includeInventory: false, includeAbilities: false);
            else GrimorioDelPersonaje.Aplicar(_slot);
        }

        // ── Lista ─────────────────────────────────────────────────────────

        void BuildList()
        {
            foreach (var entry in _rows)
                if (entry?.widget != null) entry.widget.gameObject.SetActive(false);
            _rows.Clear();
            _highlightedRow = null;
            if (_preset == null || _ui.rowsParent == null) return;

            // Solo lo que se equipa en la X; los combos (Y) tienen su página en el grimorio.
            foreach (var s in GrimorioDelPersonaje.BasicosDisponibles(_slot))
                if (Equipable(s)) AddRow(s);

            // Las filas del pool que sobran se esconden (no se destruyen, ver AddRow).
            for (int i = _rows.Count; i < _ui.rowsParent.childCount; i++)
                _ui.rowsParent.GetChild(i).gameObject.SetActive(false);

            UpdateRowVisuals();
        }

        void AddRow(MagicSpellSO spell)
        {
            // Pool: se reutilizan los hijos de rowsParent en vez de destruir y crear en cada apertura.
            SpellRowWidget widget = null;
            int poolIndex = _rows.Count;
            if (poolIndex < _ui.rowsParent.childCount)
            {
                var child = _ui.rowsParent.GetChild(poolIndex);
                widget = child.GetComponent<SpellRowWidget>();
                if (widget != null) child.gameObject.SetActive(true);
            }
            if (widget == null) widget = UnityEngine.Object.Instantiate(_ui.rowPrefab, _ui.rowsParent);

            var entry = new RowEntry { spell = spell, widget = widget };
            widget.SetIcon(spell.attackIcon);
            widget.RegisterClickHandler(() => OnRowPressed(entry));
            widget.RegisterSelectedHandler(() => OnRowFocused(entry));

            if (_scrollRect != null)
            {
                var relay = widget.GetComponent<ScrollOnSelectRelay>();
                if (relay == null) relay = widget.gameObject.AddComponent<ScrollOnSelectRelay>();
                relay.scrollRect = _scrollRect;
                relay.target = widget.GetComponent<RectTransform>();
            }

            _rows.Add(entry);
        }

        /// Resalte y texto de cada fila. El número de ranura ocupa siempre su sitio (transparente si no
        /// va equipado), así los nombres empiezan todos a la misma altura.
        void UpdateRowVisuals()
        {
            foreach (var row in _rows)
            {
                if (row?.widget == null) continue;
                bool highlighted = row == _highlightedRow;
                row.widget.SetHighlighted(highlighted, _ui.slotSelectionColor);

                int idx = _equipped.IndexOf(row.spell.spellId);
                string number = idx >= 0
                    ? $"<b><color={(highlighted ? RowNumberSelected : RowNumber)}>{idx + 1}</color></b>"
                    : "<b><color=#00000000>0</color></b>";
                row.widget.SetLabel($"{number}   {row.spell.GetLocalizedName()}");
            }
        }

        void OnRowFocused(RowEntry entry)
        {
            if (entry == null) return;
            // Mientras se elige hueco, pasar el ratón por la lista no cambia nada.
            if (_mode != Mode.Browse) return;
            _focusedSlot = -1;
            _highlightedRow = entry;
            _notice = null;
            UpdateRowVisuals();
            UpdateNavigation();
            ShowDetails(entry.spell, -1);
            ScrollTo(entry);
            UpdateHint();
        }

        void OnRowPressed(RowEntry entry)
        {
            if (entry == null) return;
            // Con el ratón se puede pinchar otra fila mientras se elige hueco: cuenta como cambiar de idea.
            if (_mode != Mode.Browse) ResetMode();
            var id = entry.spell.spellId;

            int idx = _equipped.IndexOf(id);
            if (idx >= 0)
            {
                if (_equipped.Count == 1)
                {
                    _notice = Loc("SPELL_KEEP_ONE", "Tiene que llevar al menos un básico.");
                    UpdateHint();
                    return;
                }
                _equipped.RemoveAt(idx);
                AfterChange(-1);
                return;
            }

            if (_equipped.Count < SlotCount)
            {
                _equipped.Add(id);
                AfterChange(_equipped.Count - 1);
                return;
            }

            // Los 4 ocupados: ¿cuál cambia?
            _mode = Mode.ReplaceSlot;
            _pendingSpell = id;
            UpdateNavigation();
            FocusSlot(0);
            UpdateHint();
        }

        // ── Huecos ────────────────────────────────────────────────────────

        void ConfigureSlotButton(Button button, int slot)
        {
            if (button == null) return;
            button.onClick.AddListener(() => OnSlotPressed(slot));
            var listener = button.gameObject.GetComponent<SlotSelectListener>();
            if (listener == null) listener = button.gameObject.AddComponent<SlotSelectListener>();
            listener.onSelect = () => OnSlotFocused(slot);
            if (!_slotDefaultColors.ContainsKey(button)) _slotDefaultColors[button] = button.colors;
            if (!_slotBaseScales.ContainsKey(button)) _slotBaseScales[button] = button.transform.localScale;
        }

        void OnSlotFocused(int slot)
        {
            _focusedSlot = slot;
            _notice = null;
            ShowDetails(slot < _equipped.Count ? GrimorioDelPersonaje.Hechizo(_equipped[slot]) : null, slot);
            UpdateSlotVisuals();
            UpdateHint();
        }

        void OnSlotPressed(int slot)
        {
            switch (_mode)
            {
                case Mode.ReplaceSlot:
                    if (slot < _equipped.Count)
                    {
                        var entra = _pendingSpell;
                        _equipped[slot] = entra;
                        ResetMode();
                        AfterChange(slot);
                        FocusRow(entra);
                    }
                    return;

                case Mode.MoveSlot:
                    if (slot < _equipped.Count && slot != _pendingSlot)
                    {
                        (_equipped[slot], _equipped[_pendingSlot]) = (_equipped[_pendingSlot], _equipped[slot]);
                        ResetMode();
                        AfterChange(slot);
                    }
                    else ResetMode();
                    FocusSlot(slot);
                    UpdateHint();
                    return;
            }

            if (slot >= _equipped.Count)
            {
                FocusList();
                return;
            }
            if (_equipped.Count < 2)
            {
                _notice = Loc("SPELL_NOTHING_TO_REORDER", "Solo lleva uno: no hay orden que cambiar.");
                UpdateHint();
                return;
            }
            _mode = Mode.MoveSlot;
            _pendingSlot = slot;
            UpdateNavigation();
            UpdateSlotVisuals();
            UpdateHint();
        }

        void UpdateSlots()
        {
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                var id = i < _equipped.Count ? _equipped[i] : SpellId.None;
                var spell = id != SpellId.None ? GrimorioDelPersonaje.Hechizo(id) : null;
                bool usable = i < SlotCount;
                if (_slotButtons[i] != null) _slotButtons[i].gameObject.SetActive(usable);
                if (_slotLabels[i] != null)
                {
                    _slotLabels[i].gameObject.SetActive(usable);
                    _slotLabels[i].supportRichText = true;
                    _slotLabels[i].text = spell != null
                        ? $"<b><color={Gold}>{i + 1}</color></b>   {spell.GetLocalizedName()}"
                        : $"<b><color={Muted}>{i + 1}</color></b>   <color={Muted}>{Loc("SPELL_SLOT_EMPTY", "vacío")}</color>";
                }
                var image = _slotButtons[i] != null ? _slotButtons[i].image : null;
                if (image != null)
                {
                    var icon = spell != null ? spell.attackIcon : null;
                    if (icon != null) image.sprite = icon;
                    else if (_ui.emptySlotSprite != null) image.sprite = _ui.emptySlotSprite;
                    image.preserveAspect = true;
                }
            }
            UpdateSlotVisuals();
        }

        void UpdateSlotVisuals()
        {
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                var button = _slotButtons[i];
                if (button == null) continue;
                bool pulse = (_mode == Mode.ReplaceSlot && i == _focusedSlot && i < _equipped.Count)
                          || (_mode == Mode.MoveSlot && i == _focusedSlot && i != _pendingSlot);
                if (pulse) PlayPulse(button);
                else KillTween(button);

                if (!_slotDefaultColors.TryGetValue(button, out var colors)) continue;
                if (_mode == Mode.MoveSlot && i == _pendingSlot)
                {
                    colors.normalColor = colors.highlightedColor = colors.selectedColor = _ui.slotSelectionColor;
                }
                button.colors = colors;
            }
        }

        /// Tras poner, quitar o mover: guarda, repinta y da un golpe de escala al hueco que cambió.
        void AfterChange(int punchSlot)
        {
            Save();
            UpdateSlots();
            UpdateRowVisuals();
            if (punchSlot >= 0 && punchSlot < _slotButtons.Length) PlayPunch(_slotButtons[punchSlot]);
            if (_focusedSlot >= 0) OnSlotFocused(_focusedSlot);
            else if (_highlightedRow != null) ShowDetails(_highlightedRow.spell, -1);
            UpdateHint();
        }

        void ResetMode()
        {
            _mode = Mode.Browse;
            _pendingSpell = SpellId.None;
            _pendingSlot = -1;
            _notice = null;
            UpdateNavigation();
            UpdateSlotVisuals();
        }

        // ── Foco y navegación ─────────────────────────────────────────────

        void FocusSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            var button = slot < _slotButtons.Length ? _slotButtons[slot] : null;
            if (button == null) return;
            _focusedSlot = slot;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != button.gameObject) es.SetSelectedGameObject(button.gameObject);
            else OnSlotFocused(slot);
        }

        void FocusList()
        {
            if (_rows.Count == 0) return;
            (_highlightedRow ?? _rows[0]).widget?.Focus();
        }

        bool FocusRow(SpellId id)
        {
            foreach (var entry in _rows)
            {
                if (entry?.spell == null || entry.spell.spellId != id) continue;
                entry.widget?.Focus();
                OnRowFocused(entry);
                return true;
            }
            return false;
        }

        void ScrollTo(RowEntry entry)
        {
            if (_scrollRect == null || entry?.widget == null) return;
            ScrollRectAutoScroller.ScrollTo(_scrollRect, entry.widget.GetComponent<RectTransform>(), 10f);
        }

        /// La lista está a la izquierda y los huecos a la derecha. Mientras se elige hueco, el foco se
        /// queda en los huecos.
        void UpdateNavigation()
        {
            Selectable rowTarget = (_highlightedRow ?? (_rows.Count > 0 ? _rows[0] : null))?.widget?.Selectable;
            int n = SlotCount;
            Selectable slotTarget = n > 0 ? _slotButtons[Mathf.Clamp(_focusedSlot, 0, n - 1)] : null;

            for (int i = 0; i < _rows.Count; i++)
            {
                var sel = _rows[i]?.widget?.Selectable;
                if (sel == null) continue;
                var nav = sel.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? _rows[i - 1].widget.Selectable : sel;
                nav.selectOnDown = i < _rows.Count - 1 ? _rows[i + 1].widget.Selectable : sel;
                nav.selectOnLeft = sel;
                nav.selectOnRight = slotTarget != null ? slotTarget : sel;
                sel.navigation = nav;
            }

            for (int i = 0; i < n; i++)
            {
                var b = _slotButtons[i];
                if (b == null) continue;
                var nav = b.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = _slotButtons[(i + n - 1) % n];
                nav.selectOnDown = _slotButtons[(i + 1) % n];
                nav.selectOnLeft = _mode == Mode.Browse && rowTarget != null ? rowTarget : b;
                nav.selectOnRight = b;
                b.navigation = nav;
            }
        }

        // ── Textos ────────────────────────────────────────────────────────

        static string Key(ComboButton b) => Bold(Core.InputGlyphs.ComboButtonGlyphs.Label(b));
        static string Key(string glyphName) =>
            Bold(Core.InputGlyphs.InputGlyphLabels.GetLabel(glyphName, Core.InputGlyphs.InputGlyphService.CurrentFamily));
        static string Bold(string s) => $"<b><color={Gold}>{s}</color></b>";

        /// Icono del botón, para la línea de ayuda (TMP).
        static string Icon(string glyphName) => Core.InputGlyphs.InputGlyphService.SpriteTag(glyphName);

        /// Botón de aceptar y de volver en la línea de ayuda: A/B en mando; en teclado, la tecla de
        /// Selección (la misma que muestra la barra de abajo) y Esc.
        static string AcceptKey() => Icon(Core.InputGlyphs.InputGlyphNames.South);
        static string BackKey() =>
            Core.InputGlyphs.InputGlyphService.CurrentFamily == Core.InputGlyphs.InputGlyphDeviceFamily.KeyboardMouse
                ? Bold("Esc")
                : Icon(Core.InputGlyphs.InputGlyphNames.East);

        /// Tres líneas: nombre y tipo, daño y maná, lo que hace de especial.
        void ShowDetails(MagicSpellSO spell, int slot)
        {
            if (_ui.detailsText == null) return;
            _ui.detailsText.supportRichText = true;

            if (spell == null)
            {
                _ui.detailsText.text = slot >= 0
                    ? string.Format(Loc("SPELL_SLOT_FREE", "Hueco {0} libre.\nElige un básico de la lista para llenarlo."), slot + 1)
                    : string.Format(Loc("SPELL_BASICS_INTRO", "Lleva hasta 4 básicos. En combate se lanzan con {0}\ny {1} pasa de uno a otro, en este orden."),
                        Key(ComboButton.X), Key(Core.InputGlyphs.InputGlyphNames.ShoulderLeft));
                return;
            }

            string tipo = Loc("SPELL_TYPE_BASIC", "Básico");
            string linea3 = GrimorioDelPersonaje.Efectos(spell);

            _ui.detailsText.text =
                $"{spell.GetLocalizedName()}  <size=80%><color={Muted}>{tipo}</color></size>\n" +
                string.Format(Loc("SPELL_DAMAGE_LABEL", "Daño: {0}"), spell.damage) + "   ·   " +
                string.Format(Loc("SPELL_MANA_COST_LABEL", "Coste de maná: {0}"), spell.manaCost) +
                (string.IsNullOrEmpty(linea3) ? "" : "\n" + linea3);
        }

        /// La línea de abajo: qué hace A ahora mismo, según dónde está el foco.
        void UpdateHint()
        {
            _hintFamily = Core.InputGlyphs.InputGlyphService.CurrentFamily;
            if (_ui.hintLabel == null) return;
            _ui.hintLabel.richText = true;
            Core.InputGlyphs.InputGlyphService.UsarIconos(_ui.hintLabel);

            string a = AcceptKey(), b = BackKey();
            string text;
            if (!string.IsNullOrEmpty(_notice)) text = _notice;
            else if (_mode == Mode.ReplaceSlot)
            {
                var entra = GrimorioDelPersonaje.Hechizo(_pendingSpell);
                text = string.Format(Loc("SPELL_HINT_REPLACE", "Lleva 4. ¿Cuál cambias por {0}?   {1} Este   ·   {2} Cancelar"),
                    entra != null ? entra.GetLocalizedName() : "", a, b);
            }
            else if (_mode == Mode.MoveSlot)
                text = string.Format(Loc("SPELL_HINT_MOVE", "¿A qué hueco lo llevas?   {0} Aquí   ·   {1} Cancelar"), a, b);
            else if (_focusedSlot >= 0)
                text = _focusedSlot < _equipped.Count
                    ? string.Format(Loc("SPELL_HINT_SLOT", "{0} Cambiar de sitio   ·   {1} Volver a la lista"), a, b)
                    : string.Format(Loc("SPELL_HINT_SLOT_EMPTY", "{0} Elegir un básico de la lista"), a);
            else if (_highlightedRow == null)
                text = string.Format(Loc("SPELL_HINT_LIST", "{0} Equipar o quitar"), a);
            else if (_equipped.Contains(_highlightedRow.spell.spellId))
                text = string.Format(Loc("SPELL_HINT_UNEQUIP", "{0} Quitar"), a);
            else if (_equipped.Count < SlotCount)
                text = string.Format(Loc("SPELL_HINT_EQUIP", "{0} Equipar en el hueco {1}"), a, _equipped.Count + 1);
            else
                text = string.Format(Loc("SPELL_HINT_EQUIP_FULL", "{0} Equipar (lleva 4: elegirás cuál cambiar)"), a);

            _ui.hintLabel.text = text;
        }

        // ── Animación de los huecos ───────────────────────────────────────

        void PlayPulse(Button button)
        {
            if (_slotTweens.ContainsKey(button)) return;
            var baseScale = _slotBaseScales.TryGetValue(button, out var s) ? s : button.transform.localScale;
            _slotTweens[button] = button.transform
                .DOScale(baseScale * 1.12f, 0.35f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }

        void PlayPunch(Button button)
        {
            if (button == null) return;
            KillTween(button);
            _slotTweens[button] = button.transform
                .DOPunchScale(Vector3.one * 0.15f, 0.3f, vibrato: 8, elasticity: 0.6f)
                .SetUpdate(true)
                .OnComplete(() => KillTween(button));
        }

        void KillTween(Button button)
        {
            if (button == null) return;
            if (_slotTweens.TryGetValue(button, out var tween))
            {
                _slotTweens.Remove(button);
                tween?.Kill();
            }
            if (_slotBaseScales.TryGetValue(button, out var baseScale)) button.transform.localScale = baseScale;
        }

        void KillAllSlotFeedback()
        {
            foreach (var b in _slotButtons) KillTween(b);
        }

        class SlotSelectListener : MonoBehaviour, ISelectHandler
        {
            public System.Action onSelect;
            public void OnSelect(BaseEventData eventData) => onSelect?.Invoke();
        }
    }

    [Serializable]
    class EquipmentBindings
    {
        public GameObject root;
        public EquipmentBindings.RowBinding[] rows;
        [Header("Feedback visual")]
        public Color rowSelectionColor = new Color(1f, 0.82f, 0.16f, 1f);

        public bool IsConfigured => root != null && rows != null && rows.Length > 0;

        [Serializable]
        public class RowBinding
        {
            public bool enabled = true;
            public PartCategory category;
            public Text label;
            public Image icon;
            public Button previousButton;
            public Button nextButton;
            public Button clearButton;
        }
    }

    class EquipmentView
    {
        readonly EquipmentBindings _ui;
        readonly Dictionary<PartCategory, EquipmentBindings.RowBinding> _rows = new();
        readonly List<EquipmentBindings.RowBinding> _orderedRows = new();
        bool _rowOrderDirty = true;
        ModularAutoBuilder _builder;
        WardrobeInventory _wardrobe;
        WardrobeInventory _boundWardrobe;
        PlayerPresetService _presetService;

        public EquipmentView(EquipmentBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            if (_ui.rows != null)
            {
                foreach (var row in _ui.rows)
                {
                    if (row == null || !row.enabled) continue;
                    _rows[row.category] = row;
                    var capturedCategory = row.category;
                    if (row.previousButton != null)
                        row.previousButton.onClick.AddListener(() => Cycle(capturedCategory, -1));
                    if (row.nextButton != null)
                        row.nextButton.onClick.AddListener(() => Cycle(capturedCategory, +1));
                    if (row.clearButton != null)
                        row.clearButton.onClick.AddListener(() => Clear(capturedCategory));
                }
            }
        }

        public GameObject DefaultSelection
        {
            get
            {
                var ordered = GetOrderedRows();
                int idx = 0;
                Button btn = null;
                while (btn == null && idx < 3 && idx < ordered.Count)
                {
                    var row = ordered[idx];
                    for (int columnIndex = 0; columnIndex < 3; columnIndex++)
                    {
                        btn = GetButtonByColumn(row, columnIndex);
                        if (IsSelectable(btn)) return btn.gameObject;
                    }
                    idx++;
                }
                return _ui.root;
            }
        }

        public void SetVisible(bool value)
        {
            if (!value)
            {
                ResetAllHighlights();

                // FIX M10 (auditoría 2026-08-07): a diferencia de InventoryView, EquipmentView no
                // se desuscribía de OnWardrobeChanged al ocultarse — solo Dispose() lo hacía. Con
                // el canvas oculto (otra pestaña activa) HandleWardrobeChanged seguía disparándose
                // y refrescando la UI de esta vista sin que nadie la viera ("refrescos fantasma").
                // Al limpiar _boundWardrobe aquí, Refresh() vuelve a suscribirse solo la próxima
                // vez que la pestaña se muestre (ver el bloque `if (_activeTab == 2)` que llama a
                // Refresh() justo después de SetVisible(true)).
                if (_boundWardrobe != null)
                {
                    _boundWardrobe.OnWardrobeChanged -= HandleWardrobeChanged;
                    _boundWardrobe = null;
                }
            }
            if (_ui.root != null)
                _ui.root.SetActive(value);
        }

        public void Refresh()
        {
            PlayerService.TryGetComponent(out _builder, includeInactive: true, allowSceneLookup: true);
            PlayerService.TryGetComponent(out _wardrobe, includeInactive: true, allowSceneLookup: true);

            // Debug.Log($"[EquipmentView.Refresh] Builder: {(_builder != null ? "Found" : "NULL")}, Wardrobe: {(_wardrobe != null ? "Found" : "NULL")}");

            if (_boundWardrobe != _wardrobe)
            {
                if (_boundWardrobe != null)
                {
                    _boundWardrobe.OnWardrobeChanged -= HandleWardrobeChanged;
                    // Debug.Log($"[EquipmentView.Refresh] Desuscrito de OnWardrobeChanged del wardrobe anterior");
                }

                if (_wardrobe != null)
                {
                    _wardrobe.OnWardrobeChanged += HandleWardrobeChanged;
                    // Debug.Log($"[EquipmentView.Refresh] ✅ Suscrito exitosamente a OnWardrobeChanged");
                }

                _boundWardrobe = _wardrobe;
            }

            PlayerService.TryGetComponent(out _presetService, includeInactive: true, allowSceneLookup: true);

            foreach (var kvp in _rows)
            {
                var row = kvp.Value;
                var category = kvp.Key;

                bool hasOptions = false;
                if (_wardrobe != null)
                {
                    var options = _wardrobe.GetUnlockedOptions(category);
                    hasOptions = options != null && options.Count > 0;
                    
                    // Log para depuración de todas las categorías
                    // Debug.Log($"[EquipmentView.Refresh] Categoría {category} tiene {options?.Count ?? 0} opciones desbloqueadas");
                    // if (hasOptions)
                    // {
                    //     foreach (var entry in options)
                    //     {
                    //         Debug.Log($"  [{category}] Item: {entry.partName} ({entry.displayName})");
                    //     }
                    // }
                }
                else
                {
                    // Debug.LogWarning($"[EquipmentView.Refresh] No hay wardrobe disponible para verificar categoría {category}");
                }

                bool allowClear = _wardrobe == null ? _builder != null : hasOptions;
                SetInteractable(row, _builder != null || hasOptions, allowClear);
            }

            UpdateLabels();
            ConfigureRowNavigation();
        }

        void ConfigureRowNavigation()
        {
            for (int rowIndex = 0; rowIndex < GetOrderedRows().Count; rowIndex++)
            {
                var row = GetOrderedRows()[rowIndex];
                ConfigureButtonNavigation(row, row?.previousButton, rowIndex, 0);
                ConfigureButtonNavigation(row, row?.nextButton, rowIndex, 1);
                ConfigureButtonNavigation(row, row?.clearButton, rowIndex, 2);
            }
        }

        void ConfigureButtonNavigation(EquipmentBindings.RowBinding row, Button button, int rowIndex, int columnIndex)
        {
            if (row == null || button == null) return;

            var nav = button.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = ResolveHorizontal(rowIndex, columnIndex, -1);
            nav.selectOnRight = ResolveHorizontal(rowIndex, columnIndex, +1);
            nav.selectOnUp = ResolveVertical(rowIndex, columnIndex, -1);
            nav.selectOnDown = ResolveVertical(rowIndex, columnIndex, +1);
            button.navigation = nav;
            ApplyButtonHighlight(button);
        }

        void ResetAllHighlights()
        {
            foreach (var row in _rows.Values)
            {
                ResetButtonColor(row?.previousButton);
                ResetButtonColor(row?.nextButton);
                ResetButtonColor(row?.clearButton);
            }
        }

        void ResetButtonColor(Button button)
        {
            if (button == null) return;
            var highlight = button.GetComponent<ButtonHighlight>();
            if (highlight != null)
            {
                highlight.ResetColor();
            }
        }

        void ApplyButtonHighlight(Button button)
        {
            if (button == null) return;
            var highlight = button.GetComponent<ButtonHighlight>();
            if (highlight == null)
                highlight = button.gameObject.AddComponent<ButtonHighlight>();
            highlight.Configure(_ui.rowSelectionColor);
        }

        Button ResolveHorizontal(int rowIndex, int columnIndex, int step)
        {
            var row = GetRow(rowIndex);
            if (row == null) return null;
            int idx = columnIndex;
            while (true)
            {
                idx += step;
                if (idx < 0 || idx > 2)
                    break;
                var btn = GetButtonByColumn(row, idx);
                if (IsSelectable(btn)) return btn;
            }
            var current = GetButtonByColumn(row, columnIndex);
            if (IsSelectable(current)) return current;
            return GetDefaultButton(row);
        }

        Button ResolveVertical(int rowIndex, int columnIndex, int step)
        {
            var ordered = GetOrderedRows();
            int idx = rowIndex;
            while (true)
            {
                idx += step;
                if (idx < 0 || idx >= ordered.Count)
                    break;
                var row = ordered[idx];
                if (row == null) continue;
                var btn = GetButtonByColumn(row, columnIndex);
                if (IsSelectable(btn)) return btn;
                var fallback = GetDefaultButton(row);
                if (IsSelectable(fallback)) return fallback;
            }

            var currentRow = GetRow(rowIndex);
            var current = GetButtonByColumn(currentRow, columnIndex);
            if (IsSelectable(current)) return current;
            return GetDefaultButton(currentRow);
        }

        EquipmentBindings.RowBinding GetRow(int index)
        {
            var ordered = GetOrderedRows();
            if (index < 0 || index >= ordered.Count) return null;
            return ordered[index];
        }

        static Button GetButtonByColumn(EquipmentBindings.RowBinding row, int columnIndex)
        {
            if (row == null) return null;
            return columnIndex switch
            {
                0 => row.previousButton,
                1 => row.nextButton,
                2 => row.clearButton,
                _ => null
            };
        }

        static Button GetDefaultButton(EquipmentBindings.RowBinding row)
        {
            if (row == null) return null;
            if (IsSelectable(row.previousButton)) return row.previousButton;
            if (IsSelectable(row.nextButton)) return row.nextButton;
            if (IsSelectable(row.clearButton)) return row.clearButton;
            return null;
        }

        static bool IsSelectable(Button button)
        {
            return button != null && button.IsInteractable();
        }

        void Cycle(PartCategory category, int step)
        {
            if (_builder == null) return;

            // Resetear highlights de otros botones para evitar que queden múltiples amarillos
            ResetOtherHighlights();

            bool changed = false;

            if (_wardrobe != null)
            {
                changed = TryCycleWithWardrobe(category, step);
                if (!changed)
                    return;
            }
            else
            {
                CycleBuilderPart(category, step);
                changed = true;
            }

            Snapshot();
            UpdateLabels();
        }

        void Clear(PartCategory category)
        {
            if (_builder == null) return;
            
            // Guardar referencia al botón previous/next de esta fila ANTES de hacer cambios
            Button fallbackButton = null;
            if (_rows.TryGetValue(category, out var row) && row != null)
            {
                if (row.previousButton != null && row.previousButton.IsInteractable())
                    fallbackButton = row.previousButton;
                else if (row.nextButton != null && row.nextButton.IsInteractable())
                    fallbackButton = row.nextButton;
            }
            
            // Resetear highlights de otros botones
            ResetOtherHighlights();
            
            SetBuilderPart(category, null);
            Snapshot();
            UpdateLabels();
            
            // Actualizar interactividad de los botones después de Clear
            UpdateRowInteractivity(category);
            
            // Forzar selección al botón previous/next de la misma fila
            if (fallbackButton != null && fallbackButton.IsInteractable())
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[EquipmentView.Clear] Moviendo selección a {fallbackButton.name}");
#endif
                EventSystem.current?.SetSelectedGameObject(null);
                EventSystem.current?.SetSelectedGameObject(fallbackButton.gameObject);
                
                // Forzar el highlight visual
                var highlight = fallbackButton.GetComponent<ButtonHighlight>();
                if (highlight != null)
                {
                    highlight.OnSelect(null);
                }
            }
            else
            {
                // Buscar cualquier botón válido
                EnsureValidSelection(category);
            }
        }
        
        /// <summary>
        /// Actualiza la interactividad de una fila específica después de un cambio
        /// </summary>
        void UpdateRowInteractivity(PartCategory category)
        {
            if (!_rows.TryGetValue(category, out var row) || row == null) return;
            
            bool hasOptions = false;
            if (_wardrobe != null)
            {
                var options = _wardrobe.GetUnlockedOptions(category);
                hasOptions = options != null && options.Count > 0;
            }
            
            bool allowClear = _wardrobe == null ? _builder != null : hasOptions;
            
            // Verificar si hay algo seleccionado actualmente en esta categoría
            string currentSelection = GetSelectionFor(category);
            bool hasSomethingEquipped = !string.IsNullOrEmpty(currentSelection);
            
            // Solo permitir Clear si hay algo equipado
            allowClear = allowClear && hasSomethingEquipped;
            
            SetInteractable(row, _builder != null || hasOptions, allowClear);
        }
        
        /// <summary>
        /// Asegura que la selección actual sea un botón válido/interactuable
        /// </summary>
        void EnsureValidSelection(PartCategory category)
        {
            var currentSelected = EventSystem.current?.currentSelectedGameObject;
            if (currentSelected == null) return;
            
            // Verificar si el botón actual sigue siendo interactuable
            var currentButton = currentSelected.GetComponent<Button>();
            if (currentButton != null && currentButton.IsInteractable())
                return; // Todo bien, el botón actual es válido
            
            // El botón actual no es interactuable, buscar uno válido en la misma fila
            if (_rows.TryGetValue(category, out var row) && row != null)
            {
                // Intentar seleccionar previous o next primero (para poder seguir ciclando)
                Button newSelection = null;
                if (row.previousButton != null && row.previousButton.IsInteractable())
                    newSelection = row.previousButton;
                else if (row.nextButton != null && row.nextButton.IsInteractable())
                    newSelection = row.nextButton;
                
                if (newSelection != null)
                {
                    EventSystem.current.SetSelectedGameObject(newSelection.gameObject);
                    return;
                }
            }
            
            // Si no hay botón válido en la fila actual, buscar en otras filas
            foreach (var kvp in _rows)
            {
                var r = kvp.Value;
                if (r == null) continue;
                
                if (r.previousButton != null && r.previousButton.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(r.previousButton.gameObject);
                    return;
                }
                if (r.nextButton != null && r.nextButton.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(r.nextButton.gameObject);
                    return;
                }
                if (r.clearButton != null && r.clearButton.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(r.clearButton.gameObject);
                    return;
                }
            }
        }
        
        /// <summary>
        /// Resetea el highlight de todos los botones excepto el actualmente seleccionado
        /// </summary>
        void ResetOtherHighlights()
        {
            var currentSelected = EventSystem.current?.currentSelectedGameObject;
            foreach (var row in _rows.Values)
            {
                ResetButtonIfNotSelected(row?.previousButton, currentSelected);
                ResetButtonIfNotSelected(row?.nextButton, currentSelected);
                ResetButtonIfNotSelected(row?.clearButton, currentSelected);
            }
        }
        
        void ResetButtonIfNotSelected(Button button, GameObject currentSelected)
        {
            if (button == null) return;
            if (button.gameObject == currentSelected) return; // No resetear el seleccionado
            
            var highlight = button.GetComponent<ButtonHighlight>();
            if (highlight != null)
            {
                highlight.ResetColor();
            }
        }

        void Snapshot()
        {
            _presetService?.SnapshotAppearanceToPreset();
            // No restaurar inventario al cambiar apariencia (solo actualizar appearance)
            _presetService?.ApplyCurrentPreset(includeInventory: false, includeAbilities: false);
        }

        void UpdateLabels()
        {
            if (_builder == null) return;
            var selection = _builder.GetSelection();

            foreach (var kvp in _rows)
            {
                var row = kvp.Value;
                if (row?.label == null) continue;

                string value = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("SPELL_UNASSIGNED_SHORT", "Sin asignar")
                    : "Sin asignar";
                string partName = null;
                if (selection != null && selection.TryGetValue(kvp.Key, out var part) && !string.IsNullOrEmpty(part))
                {
                    partName = part;
                    value = ResolveDisplayName(kvp.Key, part);
                }

                row.label.text = $"{FormatCategory(kvp.Key)}: {value}";
                UpdateRowIcon(row, kvp.Key, partName);
            }
        }

        // Muestra el icono del item equipado en la fila (si el item tiene uno asignado en su WardrobeItemSO).
        // Items desbloqueados vía AutoUnlockAll() no tienen icono propio, así que se oculta el Image en ese caso.
        void UpdateRowIcon(EquipmentBindings.RowBinding row, PartCategory category, string partName)
        {
            if (row.icon == null) return;

            Sprite iconSprite = null;
            if (!string.IsNullOrEmpty(partName) && _wardrobe != null && _wardrobe.TryGetEntry(category, partName, out var entry))
                iconSprite = entry.icon;

            row.icon.sprite = iconSprite;
            row.icon.enabled = iconSprite != null;
        }

        void SetInteractable(EquipmentBindings.RowBinding row, bool canCycle, bool allowClear)
        {
            if (row == null) return;
            if (row.previousButton != null) row.previousButton.interactable = canCycle;
            if (row.nextButton != null) row.nextButton.interactable = canCycle;
            if (row.clearButton != null) row.clearButton.interactable = allowClear;
        }

        bool TryCycleWithWardrobe(PartCategory category, int step)
        {
            if (_wardrobe == null) return false;

            var options = _wardrobe.GetUnlockedOptions(category);
            if (options == null || options.Count == 0) return false;

            string current = GetSelectionFor(category);
            int currentIndex = -1;

            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].partName, current, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
                currentIndex = step > 0 ? 0 : options.Count - 1;

            int nextIndex = (currentIndex + step) % options.Count;
            if (nextIndex < 0) nextIndex += options.Count;

            var nextPartName = options[nextIndex].partName;
            if (string.IsNullOrEmpty(nextPartName)) return false;

            SetBuilderPart(category, nextPartName);
            return true;
        }

        void ClearSelection(PartCategory category)
        {
            if (_builder == null) return;
            SetBuilderPart(category, null);
        }

        string GetSelectionFor(PartCategory category)
        {
            if (_builder == null) return null;
            var selection = _builder.GetSelection();
            if (selection != null && selection.TryGetValue(category, out var part))
                return part;
            return null;
        }

        string ResolveDisplayName(PartCategory category, string partName)
        {
            if (string.IsNullOrEmpty(partName))
            {
                return LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("SPELL_UNASSIGNED_SHORT", "Sin asignar")
                    : "Sin asignar";
            }
            if (_wardrobe != null && _wardrobe.TryGetEntry(category, partName, out var entry))
            {
                return string.IsNullOrEmpty(entry.displayName) ? partName : entry.displayName;
            }
            return partName;
        }

        void HandleWardrobeChanged()
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[EquipmentView] 📢 HandleWardrobeChanged - Evento recibido, refrescando opciones disponibles");
#endif
            
            // Log del wardrobe actual
            if (_wardrobe != null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[EquipmentView] Wardrobe encontrado: {_wardrobe.GetType().Name}");
#endif
            }
            else
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[EquipmentView] ⚠️ Wardrobe es NULL en HandleWardrobeChanged!");
#endif
            }
            
            Refresh();
            // Forzar actualización de la UI
            UpdateAllRowsUI();
        }

        void UpdateAllRowsUI()
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[EquipmentView] UpdateAllRowsUI - Actualizando todas las filas visualmente");
#endif
            UpdateLabels();
            
            // Forzar recálculo de interactividad de todos los botones
            foreach (var kvp in _rows)
            {
                var row = kvp.Value;
                var category = kvp.Key;
                
                bool hasOptions = false;
                if (_wardrobe != null)
                {
                    var options = _wardrobe.GetUnlockedOptions(category);
                    hasOptions = options != null && options.Count > 0;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.Log($"[EquipmentView.UpdateAllRowsUI] Categoría {category}: {options?.Count ?? 0} opciones, hasOptions={hasOptions}");
#endif
                }

                bool allowClear = _wardrobe == null ? _builder != null : hasOptions;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[EquipmentView.UpdateAllRowsUI] Categoría {category}: Builder={(_builder != null)}, hasOptions={hasOptions}, allowClear={allowClear}");
#endif
                SetInteractable(row, _builder != null || hasOptions, allowClear);
            }
        }

        public void EnsureSelection()
        {
            var target = DefaultSelection;
            if (target == null) return;
            var es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(null);
                es.SetSelectedGameObject(target);
            }
            var selectable = target.GetComponent<Selectable>();
            selectable?.Select();
        }

        public bool TryHandleCancel() => false;

        public void Dispose()
        {
            if (_boundWardrobe != null)
            {
                _boundWardrobe.OnWardrobeChanged -= HandleWardrobeChanged;
            }
        }

        // Antes: switch con los 12 literales en español a pelo, sin pasar nunca por
        // LocalizationManager — por eso el inventario/vestuario se veía siempre en español
        // aunque el idioma del juego estuviera en inglés (ver incidencia
        // "literales-inventario-sin-traducir"). Mismo patrón de claves que el resto del
        // catálogo (ui_es.json/ui_en.json): EQUIP_CATEGORY_<NOMBRE>.
        string FormatCategory(PartCategory cat)
        {
            string EquipLoc(string key, string fallbackEs) =>
                LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, fallbackEs) : fallbackEs;

            return cat switch
            {
                PartCategory.WeaponR => EquipLoc("EQUIP_CATEGORY_WEAPONR", "Arma Mano Derecha"),
                PartCategory.ShieldR => EquipLoc("EQUIP_CATEGORY_SHIELDR", "Escudo Mano Izquierda"),
                PartCategory.Bow => EquipLoc("EQUIP_CATEGORY_BOW", "Arco"),
                PartCategory.Body => EquipLoc("EQUIP_CATEGORY_BODY", "Vestuario"),
                PartCategory.Cloak => EquipLoc("EQUIP_CATEGORY_CLOAK", "Capa"),
                PartCategory.Head => EquipLoc("EQUIP_CATEGORY_HEAD", "Cabeza"),
                PartCategory.Hair => EquipLoc("EQUIP_CATEGORY_HAIR", "Pelo"),
                PartCategory.Eyes => EquipLoc("EQUIP_CATEGORY_EYES", "Ojos"),
                PartCategory.Mouth => EquipLoc("EQUIP_CATEGORY_MOUTH", "Boca"),
                PartCategory.Hat => EquipLoc("EQUIP_CATEGORY_HAT", "Casco"),
                PartCategory.Eyebrow => EquipLoc("EQUIP_CATEGORY_EYEBROW", "Ceja"),
                PartCategory.Accessory => EquipLoc("EQUIP_CATEGORY_ACCESSORY", "Accesorio"),
                _ => cat.ToString()
            };
        }

        // Llama al método Next/Prev del builder directamente
        void CycleBuilderPart(PartCategory category, int step)
        {
            if (_builder == null) return;
            _builder.Next(category, step);
        }

        void SetBuilderPart(PartCategory category, string nameOrNull)
        {
            if (_builder == null) return;
            _builder.SetByName(category, nameOrNull);
        }

        List<EquipmentBindings.RowBinding> GetOrderedRows()
        {
            if (!_rowOrderDirty)
                return _orderedRows;

            _orderedRows.Clear();
            if (_ui.rows != null)
            {
                foreach (var row in _ui.rows)
                {
                    // Filtrar solo las filas habilitadas
                    if (row != null && row.enabled)
                        _orderedRows.Add(row);
                }
                _orderedRows.Sort((a, b) => GetRowSortValue(a).CompareTo(GetRowSortValue(b)));
            }

            _rowOrderDirty = false;
            return _orderedRows;
        }

        float GetRowSortValue(EquipmentBindings.RowBinding row)
        {
            Transform t = null;
            if (row.label != null) t = row.label.transform;
            else if (row.previousButton != null) t = row.previousButton.transform;
            else if (row.nextButton != null) t = row.nextButton.transform;
            else if (row.clearButton != null) t = row.clearButton.transform;
            return t != null ? -t.position.y : float.MinValue;
        }

        class ButtonHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
        {
            public Graphic target;
            public Color highlightColor = Color.white; // ya no se usa para colorear, se mantiene por compatibilidad
            Vector3 _baseScale;
            bool _baseScaleCaptured;
            Tween _pulseTween;

            void Awake()
            {
                if (target == null)
                    target = GetComponent<Graphic>();
                if (target == null)
                    target = GetComponentInChildren<Graphic>();
            }

            public void Configure(Color color)
            {
                highlightColor = color;
                if (!_baseScaleCaptured)
                {
                    _baseScale = transform.localScale;
                    _baseScaleCaptured = true;
                }
            }

            public void OnSelect(BaseEventData eventData)
            {
                if (!_baseScaleCaptured)
                {
                    _baseScale = transform.localScale;
                    _baseScaleCaptured = true;
                }
                _pulseTween?.Kill();
                _pulseTween = transform
                    .DOScale(_baseScale * 1.12f, 0.35f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }

            public void OnDeselect(BaseEventData eventData)
            {
                StopPulse();
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                StartCoroutine(ResetAfterFrame());
            }

            public void OnSubmit(BaseEventData eventData)
            {
                StartCoroutine(ResetAfterFrame());
            }

            System.Collections.IEnumerator ResetAfterFrame()
            {
                yield return null;
                if (UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject != gameObject)
                    StopPulse();
            }

            public void ResetColor()
            {
                StopPulse();
            }

            void StopPulse()
            {
                _pulseTween?.Kill();
                _pulseTween = null;
                if (_baseScaleCaptured)
                    transform.localScale = _baseScale;
            }
        }
    }
}
