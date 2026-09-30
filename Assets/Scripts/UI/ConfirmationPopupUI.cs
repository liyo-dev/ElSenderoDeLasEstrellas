using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Popup de confirmación reutilizable (singleton). Pausa el juego mientras está abierto.
/// Colocar en Start.unity para que sobreviva cambios de escena.
/// </summary>
public class ConfirmationPopupUI : MonoBehaviour
{
    public static ConfirmationPopupUI Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmLabel;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI cancelLabel;

    [Header("Botones")]
    [SerializeField, Min(0f), Tooltip("Margen a cada lado del texto dentro de un botón, en unidades del canvas. " +
             "Si el texto más su margen no cabe en el ancho del botón, el botón se ensancha.")]
    private float margenTextoBoton = 26f;

    [Header("Input")]
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de aceptar input de confirmar/cancelar. Evita que la misma pulsación que abrió el popup (p.ej. botón Sur del gamepad) lo confirme instantáneamente en el mismo frame.")]
    private float inputGracePeriod = 0.15f;

    private Action _onConfirm;
    private Action _onCancel;
    private bool _cancelWithBack = true;
    private bool _isShown;
    private float _savedTimeScale;
    private bool _confirmSelected = false;
    private Coroutine _blinkRoutine;
    private float _shownAt;
    private GameObject _previousSelected;

    // Ancho y tamaño de letra con los que está diseñado cada botón; son el mínimo al ajustarlo.
    private float _anchoBaseConfirmar, _anchoBaseCancelar;
    private float _letraBaseConfirmar, _letraBaseCancelar;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        PrepararBoton(confirmButton, confirmLabel, out _anchoBaseConfirmar, out _letraBaseConfirmar);
        PrepararBoton(cancelButton, cancelLabel, out _anchoBaseCancelar, out _letraBaseCancelar);
        if (panel) panel.SetActive(false);
    }

    /// <param name="confirmText">Texto del botón de confirmar. Vacío = «Sí».</param>
    /// <param name="cancelText">Texto del botón de cancelar. Vacío = «No».</param>
    /// <param name="selectConfirm">Abrir con el foco en confirmar en vez de en cancelar.</param>
    /// <param name="cancelWithBack">Si el botón Atrás (Este / Escape) elige cancelar. Apagarlo cuando
    /// cancelar no es «no hacer nada» y ese botón se usa para otra cosa justo antes de abrir.</param>
    public void Show(string message, Action onConfirm, Action onCancel = null,
                     string confirmText = null, string cancelText = null,
                     bool selectConfirm = false, bool cancelWithBack = true)
    {
        if (_isShown) return;

        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _cancelWithBack = cancelWithBack;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // Sin esto, el mapa de acciones GamePlay seguía activo mientras el popup estaba
        // abierto: el D-Pad usado para navegar Sí/No también cambiaba de personaje
        // (PartyControlManager) y el botón Sur usado para confirmar también hacía saltar
        // al jugador (GamepadInputReader.JumpPressed no está suprimido). Mismo patrón que
        // usan DialogueManager, QuestMenuManager, TeleportUI, etc.
        Core.PlayerInputManager.Instance?.PushUIMode();

        // FIX (recurrente — INC-048 y bug de "el player no se detiene al guardar"): PushUIMode()
        // solo deshabilita el mapa de Input GamePlay, pero NO detiene el movimiento/momentum ya
        // en curso del Invector Controller (root motion residual, CharacterController, etc.).
        // StartDialogueWithOptions() sí lo hacía porque DialogueManager.ActivateDialogueMode()
        // empuja ActionMode.Cinematic en PlayerActionManager, que a su vez adquiere
        // PlayerLockService (lockMovement=true + ResetInputSmoothing + CharacterController.enabled
        // = false). Este popup (usado por SavePoint/CampfireRestInteractable/TagMinigame/etc. vía
        // StartConfirmationPopup) nunca pasaba por ahí, así que el jugador seguía andando o
        // deslizándose tras pulsar A en un punto de guardado aunque el popup ya estuviera abierto.
        // PlayerLockService.Acquire() es un no-op seguro si no hay Player en la escena (menú
        // principal, etc.) y soporta múltiples owners simultáneos por referencia.
        PlayerLockService.Instance?.Acquire(this);

        // Guardamos qué botón tenía el foco antes de abrir el popup para poder
        // restaurar la selección al cerrarlo. Sin esto, el EventSystem se queda
        // sin objeto seleccionado y el menú deja de responder a mando/teclado
        // (INC-047: controles bloqueados tras confirmar/cancelar "Nueva Partida").
        _previousSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        if (messageText) messageText.text = message;

        var loc = LocalizationManager.Instance;
        if (confirmLabel) confirmLabel.text = !string.IsNullOrEmpty(confirmText) ? confirmText
            : loc != null ? loc.Get("COMMON_YES", "Sí") : "Sí";
        if (cancelLabel)  cancelLabel.text  = !string.IsNullOrEmpty(cancelText) ? cancelText
            : loc != null ? loc.Get("COMMON_NO",  "No") : "No";

        if (confirmButton) { confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm); }
        if (cancelButton)  { cancelButton.onClick.RemoveAllListeners();  cancelButton.onClick.AddListener(Cancel);  }

        _isShown = true;
        _confirmSelected = selectConfirm;
        _shownAt = Time.unscaledTime;
        if (panel) panel.SetActive(true);
        AjustarBotonesAlTexto();

        SelectButton(_confirmSelected);
        StartBlink();
    }

    // ── Botones que se ajustan a su texto ─────────────────────────────────────
    // Cada llamada puede traer sus propios textos («Continuar», «Salir al menú»...). El botón
    // conserva su tamaño de diseño como mínimo y se ensancha lo que haga falta para que el texto
    // quepa con su margen; si ni ocupando su mitad de la fila cabe, la letra se encoge. Ver INC-531.

    static void PrepararBoton(Button boton, TextMeshProUGUI texto, out float anchoBase, out float letraBase)
    {
        anchoBase = 0f;
        letraBase = texto != null ? texto.fontSize : 0f;
        if (boton == null) return;

        var rt = (RectTransform)boton.transform;
        anchoBase = rt.rect.width > 0f ? rt.rect.width : rt.sizeDelta.x;

        // Para ensancharse sin deformar los extremos redondeados, la imagen pasa a 9-slice con la
        // misma escala que tiene a su tamaño de diseño (así, a ese tamaño se ve idéntica).
        var imagen = boton.targetGraphic as Image;
        if (imagen == null) imagen = boton.GetComponent<Image>();
        float alto = rt.rect.height > 0f ? rt.rect.height : rt.sizeDelta.y;
        if (imagen == null || imagen.sprite == null || imagen.type != Image.Type.Simple || alto <= 0f) return;
        if (imagen.sprite.border.x <= 0f && imagen.sprite.border.z <= 0f) return;

        var canvas = boton.GetComponentInParent<Canvas>(true);
        float refPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
        float altoSpriteEnUnidades = imagen.sprite.rect.height * refPpu / imagen.sprite.pixelsPerUnit;
        imagen.type = Image.Type.Sliced;
        imagen.fillCenter = true;
        imagen.pixelsPerUnitMultiplier = altoSpriteEnUnidades / alto;
    }

    void AjustarBotonesAlTexto()
    {
        var fila = confirmButton != null ? confirmButton.transform.parent as RectTransform : null;
        float anchoMax = 0f;
        if (fila != null)
        {
            Canvas.ForceUpdateCanvases();
            float espacio = 0f, relleno = 0f;
            var grupo = fila.GetComponent<HorizontalLayoutGroup>();
            if (grupo != null) { espacio = grupo.spacing; relleno = grupo.padding.horizontal; }
            anchoMax = (fila.rect.width - relleno - espacio) * 0.5f;
        }

        AjustarBoton(confirmButton, confirmLabel, _anchoBaseConfirmar, _letraBaseConfirmar, anchoMax);
        AjustarBoton(cancelButton, cancelLabel, _anchoBaseCancelar, _letraBaseCancelar, anchoMax);

        if (fila != null) LayoutRebuilder.ForceRebuildLayoutImmediate(fila);
    }

    void AjustarBoton(Button boton, TextMeshProUGUI texto, float anchoBase, float letraBase, float anchoMax)
    {
        if (boton == null || texto == null || anchoBase <= 0f) return;

        texto.enableAutoSizing = false;
        if (letraBase > 0f) texto.fontSize = letraBase;
        texto.margin = Vector4.zero;
        float anchoTexto = texto.GetPreferredValues(texto.text, float.PositiveInfinity, float.PositiveInfinity).x;
        texto.margin = new Vector4(margenTextoBoton, 0f, margenTextoBoton, 0f);

        float ancho = Mathf.Max(anchoBase, anchoTexto + margenTextoBoton * 2f);
        if (anchoMax > 0f && ancho > anchoMax)
        {
            ancho = Mathf.Max(anchoBase, anchoMax);
            texto.enableAutoSizing = true;
            texto.fontSizeMax = letraBase;
            texto.fontSizeMin = Mathf.Min(letraBase, 14f);
        }

        ((RectTransform)boton.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ancho);
    }

    void Update()
    {
        if (!_isShown) return;

        // Ignorar input mientras dure el periodo de gracia: evita que la misma pulsación que
        // abrió el popup (p.ej. botón Sur del gamepad usado para pulsar "Nueva Partida") se lea
        // de nuevo aquí en el mismo frame y confirme el popup antes de que el jugador lo vea.
        if (Time.unscaledTime - _shownAt < inputGracePeriod) return;

#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonSouth.wasPressedThisFrame)  { ActivateSelected(); return; }
            if (_cancelWithBack && gp.buttonEast.wasPressedThisFrame) { Cancel(); return; }
            if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame)
                ToggleSelection();
            if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame)
                ToggleSelection();
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { ActivateSelected(); return; }
            if (_cancelWithBack && kb.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
                ToggleSelection();
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { ActivateSelected(); return; }
        if (_cancelWithBack && Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Tab))
            ToggleSelection();
#endif
    }

    // FIX: el botón Sur/Enter confirmaba siempre, incluso con "No" resaltado (el jugador
    // navegaba hasta "No" con el D-Pad y al pulsar Sur igualmente se ejecutaba Confirm()).
    // Ahora respeta cuál de los dos botones está seleccionado en ese momento.
    void ActivateSelected()
    {
        if (_confirmSelected) Confirm();
        else Cancel();
    }

    void ToggleSelection()
    {
        _confirmSelected = !_confirmSelected;
        SelectButton(_confirmSelected);
        StopBlink();
        StartBlink();
    }

    void SelectButton(bool confirm)
    {
        var target = confirm ? confirmButton : cancelButton;
        if (target) EventSystem.current?.SetSelectedGameObject(target.gameObject);
    }

    void StartBlink()
    {
        if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
        _blinkRoutine = StartCoroutine(BlinkSelected());
    }

    void StopBlink()
    {
        if (_blinkRoutine != null) { StopCoroutine(_blinkRoutine); _blinkRoutine = null; }
        ResetButtonScale(confirmButton);
        ResetButtonScale(cancelButton);
    }

    void ResetButtonScale(Button btn)
    {
        if (btn) btn.transform.localScale = Vector3.one;
    }

    System.Collections.IEnumerator BlinkSelected()
    {
        float t = 0f;
        const float speed = 4f;
        const float amplitude = 0.07f;
        var btn = _confirmSelected ? confirmButton : cancelButton;
        while (_isShown && btn != null)
        {
            t += Time.unscaledDeltaTime * speed;
            float s = 1f + Mathf.Sin(t) * amplitude;
            btn.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (btn) btn.transform.localScale = Vector3.one;
    }

    void Confirm()
    {
        if (!_isShown) return;
        var cb = _onConfirm;
        Hide();
        cb?.Invoke();
    }

    void Cancel()
    {
        if (!_isShown) return;
        var cb = _onCancel;
        Hide();
        cb?.Invoke();
    }

    void Hide()
    {
        _isShown = false;
        StopBlink();
        Time.timeScale = _savedTimeScale;
        Core.PlayerInputManager.Instance?.PopUIMode();

        // Libera el freeze de movimiento adquirido en Show(). Seguro de llamar aunque Show()
        // no llegara a bloquear nada (owner no registrado => PlayerLockService.Release() es no-op).
        if (PlayerLockService.HasInstance)
            PlayerLockService.Instance.Release(this);

        // El botón Sur (confirmar) es el mismo botón que Jump/Interact, y el botón Este
        // (cancelar) es el mismo botón que la magia derecha/levitación. PopUIMode() reactiva
        // el mapa GamePlay de forma síncrona, así que si el jugador todavía tiene el botón
        // físicamente pulsado en este frame, el gameplay lo interpreta como una pulsación
        // nueva (p.ej. "No" hacía levitar al personaje). Mismo patrón que ShopUI/TeleportUI/
        // PlayerEquipmentMenuController al cerrar con Cancel.
        Core.GamepadInputReader.IgnoreCancelButton(0.3f);
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);

        if (panel) panel.SetActive(false);
        if (confirmButton) confirmButton.onClick.RemoveAllListeners();
        if (cancelButton)  cancelButton.onClick.RemoveAllListeners();
        _onConfirm = null;
        _onCancel  = null;

        // Restauramos el foco al botón que lo tenía antes de abrir el popup (p.ej. "Nueva
        // Partida" en el menú principal). Antes se limpiaba con SetSelectedGameObject(null)
        // y nunca se reasignaba, dejando el menú sin objeto seleccionado: con mando/teclado
        // los controles parecían "muertos" al volver (INC-047).
        EventSystem.current?.SetSelectedGameObject(null);
        if (_previousSelected != null && _previousSelected.activeInHierarchy)
        {
            var selectable = _previousSelected.GetComponent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
                EventSystem.current?.SetSelectedGameObject(_previousSelected);
        }
        _previousSelected = null;
    }

    /// <summary>
    /// Descarga la escena aditiva y la vuelve a cargar. Útil para reiniciar un nivel sin destruir Start.unity.
    /// </summary>
    public void ReloadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        StartCoroutine(ReloadSceneRoutine(sceneName));
    }

    private IEnumerator ReloadSceneRoutine(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded)
        {
            var unload = SceneManager.UnloadSceneAsync(sceneName);
            while (unload != null && !unload.isDone) yield return null;
        }
        var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (load != null && !load.isDone) yield return null;
    }
}
