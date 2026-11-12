using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Popup de confirmación reutilizable (singleton). Pausa el juego mientras está abierto.
/// Colocar en Start.unity para que sobreviva cambios de escena.
///
/// Se ve como la caja de diálogo: hoja de papel abajo, en el mismo sitio y tamaño, con la
/// pestaña del nombre de quien pregunta (si lo hay) y los botones en columna a la derecha.
/// La hoja y la pestaña las pone <see cref="PapelDeLaCajaDeDialogo"/>.
/// </summary>
public class ConfirmationPopupUI : MonoBehaviour
{
    public static ConfirmationPopupUI Instance { get; private set; }
    /// El popup está en pantalla esperando respuesta.
    public bool Visible => _isShown;

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

    [Header("Caja")]
    [SerializeField, Tooltip("Tamaño de la caja, el mismo que la de diálogo.")]
    private Vector2 tamanoDeLaCaja = new Vector2(991f, 250f);
    [SerializeField, Tooltip("Altura de la caja sobre el borde inferior, la misma que la de diálogo.")]
    private float alturaDeLaCaja = 50f;
    [SerializeField, Min(0f), Tooltip("Ancho de la columna de botones, a la derecha de la caja.")]
    private float anchoDeLosBotones = 230f;
    [SerializeField, Min(8f)] private float letraDelMensaje = 34f;

    [Header("Input")]
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de aceptar input de confirmar/cancelar. Evita que la misma pulsación que abrió el popup (p.ej. botón Sur del gamepad) lo confirme instantáneamente en el mismo frame.")]
    private float inputGracePeriod = 0.15f;

    private Action _onConfirm;
    private Action _onCancel;
    private bool _cancelWithBack = true;
    private bool _isShown;
    private bool _confirmSelected = false;
    private float _shownAt;
    private GameObject _previousSelected;

    // Ancho y tamaño de letra con los que está diseñado cada botón; son el mínimo al ajustarlo.
    private float _anchoBaseConfirmar, _anchoBaseCancelar;
    private float _letraBaseConfirmar, _letraBaseCancelar;

    private PapelDeLaCajaDeDialogo _papel;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        VestirDePapel();
        PrepararBoton(confirmButton, confirmLabel, out _anchoBaseConfirmar, out _letraBaseConfirmar);
        PrepararBoton(cancelButton, cancelLabel, out _anchoBaseCancelar, out _letraBaseCancelar);
        if (panel) panel.SetActive(false);
    }

    void OnDisable()
    {
        StopSelectionAnimation();
        // Si el objeto se apaga con el popup abierto, la pausa no se queda puesta.
        TimeScaleArbiterService.Release(this);
    }

    /// <param name="confirmText">Texto del botón de confirmar. Vacío = «Sí».</param>
    /// <param name="cancelText">Texto del botón de cancelar. Vacío = «No».</param>
    /// <param name="selectConfirm">Abrir con el foco en confirmar en vez de en cancelar.</param>
    /// <param name="cancelWithBack">Si el botón Atrás (Este / Escape) elige cancelar. Apagarlo cuando
    /// cancelar no es «no hacer nada» y ese botón se usa para otra cosa justo antes de abrir.</param>
    /// <param name="quien">Nombre de quien pregunta, en la pestaña de la caja. Vacío = sin pestaña.</param>
    /// <param name="centrado">La caja en el centro de la pantalla en vez de abajo (momentos que lo
    /// ocupan todo, como caer en combate).</param>
    public void Show(string message, Action onConfirm, Action onCancel = null,
                     string confirmText = null, string cancelText = null,
                     bool selectConfirm = false, bool cancelWithBack = true, string quien = null,
                     bool centrado = false)
    {
        if (_isShown) return;

        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _cancelWithBack = cancelWithBack;
        // El juego se para mientras se decide; el árbitro reparte el tiempo con los demás
        // sistemas que lo piden a la vez (cerrar el popup no anula otra pausa en curso).
        TimeScaleArbiterService.Request(this, 0f);

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
        if (_papel != null)
        {
            ColoresDeLaPestana.De(quien, out var fondoDelNombre, out var letraDelNombre);
            _papel.PonerNombre(quien, fondoDelNombre, letraDelNombre);
        }

        var loc = LocalizationManager.Instance;
        string textoSi = !string.IsNullOrEmpty(confirmText) ? confirmText.Trim()
            : loc != null ? loc.Get("COMMON_YES", "Sí").Trim() : "Sí";
        string textoNo = !string.IsNullOrEmpty(cancelText) ? cancelText.Trim()
            : loc != null ? loc.Get("COMMON_NO",  "No").Trim() : "No";

        if (confirmLabel) confirmLabel.text = textoSi;
        if (cancelLabel)  cancelLabel.text  = textoNo;

        if (confirmButton) { confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm); }
        if (cancelButton)  { cancelButton.onClick.RemoveAllListeners();  cancelButton.onClick.AddListener(Cancel);  }

        _isShown = true;
        _confirmSelected = selectConfirm;
        _shownAt = Time.unscaledTime;
        if (panel) panel.SetActive(true);
        AjustarBotonesAlTexto();
        PonerEnElCentro(centrado);

        // Sonido de abrir, el mismo que los demás paneles (TeleportUI, mapa). Va aquí y no en quien
        // lo abre para que todos los popups suenen igual; quien lo abre no pone el suyo (INC-931).
        Core.GamepadInputReader.PlayUISound("UI_Open");

        SelectButton(_confirmSelected);
        AnimateSelection(_confirmSelected, instant: true);
    }

    // ── Aspecto de papel ──────────────────────────────────────────────────────

    /// Pone la caja abajo como la de diálogo, sobre una hoja de papel con pestaña para el nombre,
    /// el mensaje a la izquierda y los botones en columna a la derecha. Sin oscurecer la pantalla:
    /// el fondo sigue tapando los clics, pero no se ve.
    void VestirDePapel()
    {
        if (messageText == null || confirmButton == null) return;
        var caja = messageText.transform.parent as RectTransform;
        var fila = confirmButton.transform.parent as RectTransform;
        if (caja == null || fila == null) return;

        if (panel != null && panel.TryGetComponent<Image>(out var velo)) velo.color = new Color(0f, 0f, 0f, 0f);
        if (caja.TryGetComponent<Image>(out var fondoViejo)) fondoViejo.enabled = false;

        caja.anchorMin = caja.anchorMax = new Vector2(0.5f, 0f);
        caja.pivot = new Vector2(0.5f, 0f);
        caja.sizeDelta = tamanoDeLaCaja;
        caja.anchoredPosition = new Vector2(0f, alturaDeLaCaja);

        var nombreGo = new GameObject("Nombre", typeof(RectTransform));
        nombreGo.transform.SetParent(caja, false);
        var nombre = nombreGo.AddComponent<TextMeshProUGUI>();
        nombre.font = messageText.font;
        nombre.fontSize = 40f;
        nombre.fontStyle = FontStyles.Bold;
        nombre.raycastTarget = false;

        messageText.fontSize = letraDelMensaje;
        messageText.enableAutoSizing = true;
        messageText.fontSizeMax = letraDelMensaje;
        messageText.fontSizeMin = Mathf.Min(letraDelMensaje, 22f);
        _papel = new PapelDeLaCajaDeDialogo(caja, nombre, messageText);
        var rtMensaje = messageText.rectTransform;
        rtMensaje.offsetMax = new Vector2(-(anchoDeLosBotones + 40f), rtMensaje.offsetMax.y);
        messageText.alignment = TextAlignmentOptions.MidlineLeft;

        fila.anchorMin = new Vector2(1f, 0f);
        fila.anchorMax = new Vector2(1f, 1f);
        fila.pivot = new Vector2(1f, 0.5f);
        fila.sizeDelta = new Vector2(anchoDeLosBotones, -48f);
        fila.anchoredPosition = new Vector2(-36f, 0f);
        if (fila.TryGetComponent<HorizontalLayoutGroup>(out var horizontal)) DestroyImmediate(horizontal);
        if (!fila.TryGetComponent<VerticalLayoutGroup>(out var columna)) columna = fila.gameObject.AddComponent<VerticalLayoutGroup>();
        columna.childAlignment = TextAnchor.MiddleCenter;
        columna.spacing = 16f;
        columna.childControlWidth = false;
        columna.childControlHeight = false;
        columna.childForceExpandWidth = false;
        columna.childForceExpandHeight = false;

        foreach (var etiqueta in new[] { confirmLabel, cancelLabel })
            if (etiqueta != null) etiqueta.color = PaletaUI.TintaDeTexto;
    }

    /// Papel dorado el botón con el foco, apagado el otro.
    void PintarBotones(Button elegido)
    {
        foreach (var boton in new[] { confirmButton, cancelButton })
        {
            if (boton == null) continue;
            var imagen = boton.targetGraphic as Image;
            if (imagen == null) imagen = boton.GetComponent<Image>();
            if (imagen != null) imagen.color = boton == elegido ? PaletaUI.Oro : PaletaUI.PapelApagado;
        }
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

        if (texto != null)
        {
            texto.alignment = TextAlignmentOptions.Center;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
        }

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
            var grupo = fila.GetComponent<HorizontalOrVerticalLayoutGroup>();
            float relleno = grupo != null ? grupo.padding.horizontal : 0f;
            // En fila, cada botón cabe en su mitad; en columna, en todo el ancho.
            anchoMax = grupo is HorizontalLayoutGroup
                ? (fila.rect.width - relleno - grupo.spacing) * 0.5f
                : fila.rect.width - relleno;
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
        CentrarTintaEnBoton(texto);
    }

    // El centrado de TMP va por el avance de cada letra, no por lo que se ve: con letras de
    // trazo asimétrico («Sí», la tilde) el texto quedaba visiblemente corrido dentro del botón.
    // Se desplaza la etiqueta para que el centro del dibujo coincida con el centro del botón.
    static void CentrarTintaEnBoton(TextMeshProUGUI texto)
    {
        var rt = texto.rectTransform;
        var pos = rt.anchoredPosition;
        pos.x = 0f;
        rt.anchoredPosition = pos;
        texto.ForceMeshUpdate();
        if (texto.textInfo == null || texto.textInfo.characterCount == 0) return;
        // textBounds está en el espacio local de la etiqueta; con pivote centrado y márgenes
        // simétricos, el centro del botón es x = (0.5 - pivot.x) * ancho.
        float centroCaja = (0.5f - rt.pivot.x) * rt.rect.width;
        pos.x = centroCaja - texto.textBounds.center.x;
        rt.anchoredPosition = pos;
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
            if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame
                || gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame
                || gp.dpad.up.wasPressedThisFrame || gp.leftStick.up.wasPressedThisFrame
                || gp.dpad.down.wasPressedThisFrame || gp.leftStick.down.wasPressedThisFrame)
                ToggleSelection();
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { ActivateSelected(); return; }
            if (_cancelWithBack && kb.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame
                || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
                ToggleSelection();
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { ActivateSelected(); return; }
        if (_cancelWithBack && Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)
            || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Tab))
            ToggleSelection();
#endif

        if (EventSystem.current != null)
        {
            var cur = EventSystem.current.currentSelectedGameObject;
            if (confirmButton != null && cur == confirmButton.gameObject && !_confirmSelected)
            {
                _confirmSelected = true;
                AnimateSelection(true, instant: false);
            }
            else if (cancelButton != null && cur == cancelButton.gameObject && _confirmSelected)
            {
                _confirmSelected = false;
                AnimateSelection(false, instant: false);
            }
        }
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
        AnimateSelection(_confirmSelected, instant: false);
    }

    void SelectButton(bool confirm)
    {
        var target = confirm ? confirmButton : cancelButton;
        if (target && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    void AnimateSelection(bool confirm, bool instant = false)
    {
        var selectedBtn = confirm ? confirmButton : cancelButton;
        var unselectedBtn = confirm ? cancelButton : confirmButton;
        PintarBotones(selectedBtn);

        // Cambiar de botón suena como en el resto de menús (mando, teclado o ratón).
        // Al abrir (instant) no suena: ahí no ha habido cambio.
        if (!instant) Core.GamepadInputReader.PlayUISound("UI_Navigate");

        if (unselectedBtn != null)
        {
            unselectedBtn.transform.DOKill();
            if (instant)
                unselectedBtn.transform.localScale = Vector3.one;
            else
                unselectedBtn.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        if (selectedBtn != null)
        {
            selectedBtn.transform.DOKill();
            if (instant)
                selectedBtn.transform.localScale = new Vector3(1.08f, 1.08f, 1f);
            else
                selectedBtn.transform.DOScale(new Vector3(1.08f, 1.08f, 1f), 0.15f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    void StopSelectionAnimation()
    {
        if (confirmButton != null)
        {
            confirmButton.transform.DOKill();
            confirmButton.transform.localScale = Vector3.one;
        }
        if (cancelButton != null)
        {
            cancelButton.transform.DOKill();
            cancelButton.transform.localScale = Vector3.one;
        }
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

    // Dónde va la caja abajo, para devolverla tras enseñarla centrada.
    private bool _guardadaLaPosicion;
    private Vector2 _anclaMin, _anclaMax, _pivote, _posicion;

    private void PonerEnElCentro(bool centrado)
    {
        var caja = messageText != null ? messageText.transform.parent as RectTransform : null;
        if (caja == null) return;
        if (!_guardadaLaPosicion)
        {
            _anclaMin = caja.anchorMin; _anclaMax = caja.anchorMax; _pivote = caja.pivot; _posicion = caja.anchoredPosition;
            _guardadaLaPosicion = true;
        }
        if (centrado)
        {
            caja.anchorMin = caja.anchorMax = caja.pivot = new Vector2(0.5f, 0.5f);
            caja.anchoredPosition = Vector2.zero;
        }
        else
        {
            caja.anchorMin = _anclaMin; caja.anchorMax = _anclaMax; caja.pivot = _pivote; caja.anchoredPosition = _posicion;
        }
    }

    void Hide()
    {
        _isShown = false;
        StopSelectionAnimation();
        TimeScaleArbiterService.Release(this);
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
