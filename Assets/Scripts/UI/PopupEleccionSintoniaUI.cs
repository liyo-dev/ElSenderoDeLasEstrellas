using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Ventana modal de elección de Sintonía Estelar (GDD § 1 y § 6.1, INC-847).
/// Estilo expansión Zelda: el juego se pausa de forma limpia y el jugador elige entre
/// Vigor (+15 Vida Máxima) o Flujo (+10 Maná Máximo).
/// </summary>
public class PopupEleccionSintoniaUI : MonoBehaviour
{
    public static PopupEleccionSintoniaUI Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    [Header("Referencias de UI (opcionales si se monta por Inspector)")]
    [SerializeField] private GameObject panelModal;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform ventanaRect;
    [SerializeField] private TextMeshProUGUI tituloText;
    [SerializeField] private TextMeshProUGUI subtituloText;

    [Header("Cartas de Elección")]
    [SerializeField] private RectTransform cartaVigor;
    [SerializeField] private Image bordeVigor;
    [SerializeField] private RectTransform cartaFlujo;
    [SerializeField] private Image bordeFlujo;

    [Header("Feedback y Sonido")]
    [SerializeField] private string sfxAbrir = "UI_Submit";
    [SerializeField] private string sfxConfirmar = "UI_Submit";
    [SerializeField] private string sfxNavegar = "";

    private bool _estaAbierto;
    private int _indiceSeleccionado; // 0 = Vigor, 1 = Flujo
    private float _tiempoApertura;
    private const float PeriodoDeGraciaInput = 0.2f;
    private bool _bloqueosAdquiridos;

    public static bool EstaAbierto => Instance != null && Instance._estaAbierto;

    // ─── Entrada principal ──────────────────────────────────────────────────

    public static void Abrir()
    {
        if (Instance == null)
        {
            var prefab = Resources.Load<GameObject>("UI/PopupEleccionSintoniaUI");
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                DontDestroyOnLoad(go);
                Instance = go.GetComponent<PopupEleccionSintoniaUI>();
            }
            else
            {
                Instance = ConstruirUIProcedural();
            }
        }

        Instance.Mostrar();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (panelModal != null)
            panelModal.SetActive(false);
    }

    private void OnDisable()
    {
        if (_estaAbierto)
        {
            CerrarInternal(aplicarEleccion: false);
        }
    }

    public void Mostrar()
    {
        if (_estaAbierto) return;
        _estaAbierto = true;
        _indiceSeleccionado = 0;
        _tiempoApertura = Time.unscaledTime;

        AdquirirBloqueos();

        if (panelModal != null)
        {
            panelModal.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
        }

        if (ventanaRect != null)
        {
            ventanaRect.DOKill();
            ventanaRect.localScale = Vector3.one * 0.82f;
            ventanaRect.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        ActualizarVisualSeleccion(instantaneo: true);

        if (!string.IsNullOrEmpty(sfxAbrir) && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(sfxAbrir);
        }
    }

    private void AdquirirBloqueos()
    {
        if (_bloqueosAdquiridos) return;
        _bloqueosAdquiridos = true;

        TimeScaleArbiterService.Request(this, 0f);

        if (PlayerService.TryGetComponent(out PlayerActionManager am))
        {
            am.PushMode(ActionMode.Inventory);
        }

        PlayerLockService.Instance?.Acquire(this);
        Core.PlayerInputManager.Instance?.PushUIMode();
    }

    private void LiberarBloqueos()
    {
        if (!_bloqueosAdquiridos) return;
        _bloqueosAdquiridos = false;

        TimeScaleArbiterService.Release(this);

        if (PlayerService.TryGetComponent(out PlayerActionManager am))
        {
            am.PopMode(ActionMode.Inventory);
        }

        if (PlayerLockService.HasInstance)
        {
            PlayerLockService.Instance.Release(this);
        }

        Core.PlayerInputManager.Instance?.PopUIMode();
        Core.GamepadInputReader.IgnoreCancelButton(0.3f);
    }

    private void Update()
    {
        if (!_estaAbierto) return;

        if (Time.unscaledTime - _tiempoApertura < PeriodoDeGraciaInput) return;

#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame)
                MoverSeleccion(-1);
            else if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame)
                MoverSeleccion(1);

            if (gp.buttonSouth.wasPressedThisFrame)
            {
                Confirmar();
                return;
            }

            if (gp.buttonEast.wasPressedThisFrame)
            {
                Posponer();
                return;
            }
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                MoverSeleccion(-1);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                MoverSeleccion(1);

            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                Confirmar();
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Posponer();
                return;
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            MoverSeleccion(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            MoverSeleccion(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Confirmar();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Posponer();
            return;
        }
#endif
    }

    private void MoverSeleccion(int delta)
    {
        int nuevo = Mathf.Clamp(_indiceSeleccionado + delta, 0, 1);
        if (nuevo != _indiceSeleccionado)
        {
            _indiceSeleccionado = nuevo;
            ActualizarVisualSeleccion(instantaneo: false);
            if (!string.IsNullOrEmpty(sfxNavegar) && AudioService.Instance != null)
            {
                AudioService.Instance.PlaySFX(sfxNavegar);
            }
        }
    }

    public void Seleccionar(int indice)
    {
        if (!_estaAbierto) return;
        int nuevo = Mathf.Clamp(indice, 0, 1);
        if (nuevo != _indiceSeleccionado)
        {
            _indiceSeleccionado = nuevo;
            ActualizarVisualSeleccion(instantaneo: false);
        }
    }

    public void Confirmar()
    {
        if (!_estaAbierto) return;

        var eleccion = _indiceSeleccionado == 0
            ? SintoniaEstelarService.TipoEleccionSintonia.Vigor
            : SintoniaEstelarService.TipoEleccionSintonia.Flujo;

        if (SintoniaEstelarService.Instance != null)
        {
            SintoniaEstelarService.Instance.AplicarEleccion(eleccion);
        }

        if (!string.IsNullOrEmpty(sfxConfirmar) && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(sfxConfirmar);
        }

        if (HudToastService.Instance != null)
        {
            string msg = eleccion == SintoniaEstelarService.TipoEleccionSintonia.Vigor
                ? "★ ¡Vigor aumentado! (+15 Vida Máxima)"
                : "✦ ¡Flujo aumentado! (+10 Maná Máximo)";
            HudToastService.Instance.ShowRaw(msg, 3.0f);
        }

        PlayerEquipmentMenuController.Instance?.RefreshPlayerInfo();

        CerrarInternal(aplicarEleccion: true);
    }

    public void Posponer()
    {
        if (!_estaAbierto) return;
        CerrarInternal(aplicarEleccion: false);
    }

    private void CerrarInternal(bool aplicarEleccion)
    {
        _estaAbierto = false;
        LiberarBloqueos();

        if (cartaVigor != null) cartaVigor.DOKill();
        if (cartaFlujo != null) cartaFlujo.DOKill();

        if (ventanaRect != null)
        {
            ventanaRect.DOKill();
            ventanaRect.DOScale(Vector3.one * 0.85f, 0.2f).SetEase(Ease.InCubic).SetUpdate(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
            {
                if (panelModal != null) panelModal.SetActive(false);
            });
        }
        else
        {
            if (panelModal != null) panelModal.SetActive(false);
        }
    }

    private void ActualizarVisualSeleccion(bool instantaneo)
    {
        bool selVigor = _indiceSeleccionado == 0;
        bool selFlujo = _indiceSeleccionado == 1;

        if (cartaVigor != null)
        {
            cartaVigor.DOKill();
            Vector3 targetScale = selVigor ? Vector3.one * 1.06f : Vector3.one;
            if (instantaneo) cartaVigor.localScale = targetScale;
            else cartaVigor.DOScale(targetScale, 0.18f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        if (bordeVigor != null)
        {
            Color c = bordeVigor.color;
            c.a = selVigor ? 1.0f : 0.35f;
            bordeVigor.color = c;
        }

        if (cartaFlujo != null)
        {
            cartaFlujo.DOKill();
            Vector3 targetScale = selFlujo ? Vector3.one * 1.06f : Vector3.one;
            if (instantaneo) cartaFlujo.localScale = targetScale;
            else cartaFlujo.DOScale(targetScale, 0.18f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        if (bordeFlujo != null)
        {
            Color c = bordeFlujo.color;
            c.a = selFlujo ? 1.0f : 0.35f;
            bordeFlujo.color = c;
        }
    }

    // ─── Construcción Procedural Elegante ────────────────────────────────────

    private static PopupEleccionSintoniaUI ConstruirUIProcedural()
    {
        var root = new GameObject("[PopupEleccionSintoniaUI]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // Backdrop oscuro
        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(root.transform, false);
        var backdropRect = backdropGo.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        var backdropImg = backdropGo.AddComponent<Image>();
        backdropImg.color = new Color(0.04f, 0.03f, 0.08f, 0.82f);

        // Panel Modal Centrado
        var panelModalGo = new GameObject("PanelModal");
        panelModalGo.transform.SetParent(root.transform, false);
        var panelRect = panelModalGo.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(860, 520);
        panelRect.anchoredPosition = Vector2.zero;

        var canvasGroup = panelModalGo.AddComponent<CanvasGroup>();

        // Fondo del panel modal (cristal oscuro con paleta del juego)
        var panelBg = panelModalGo.AddComponent<Image>();
        panelBg.color = PaletaUI.FondoPanel;

        // Marco / Borde exterior del panel
        var marcoGo = new GameObject("MarcoLavanda");
        marcoGo.transform.SetParent(panelModalGo.transform, false);
        var marcoRect = marcoGo.AddComponent<RectTransform>();
        marcoRect.anchorMin = Vector2.zero;
        marcoRect.anchorMax = Vector2.one;
        marcoRect.offsetMin = new Vector2(-2, -2);
        marcoRect.offsetMax = new Vector2(2, 2);
        var marcoImg = marcoGo.AddComponent<Image>();
        marcoImg.color = new Color(PaletaUI.Lavanda.r, PaletaUI.Lavanda.g, PaletaUI.Lavanda.b, 0.55f);
        marcoImg.raycastTarget = false;
        marcoGo.transform.SetAsFirstSibling();

        // Título
        var tituloGo = new GameObject("Titulo");
        tituloGo.transform.SetParent(panelModalGo.transform, false);
        var tituloRect = tituloGo.AddComponent<RectTransform>();
        tituloRect.anchorMin = new Vector2(0.5f, 1f);
        tituloRect.anchorMax = new Vector2(0.5f, 1f);
        tituloRect.anchoredPosition = new Vector2(0, -50);
        tituloRect.sizeDelta = new Vector2(700, 50);

        var tituloTmp = tituloGo.AddComponent<TextMeshProUGUI>();
        tituloTmp.text = "SINTONÍA ESTELAR";
        tituloTmp.alignment = TextAlignmentOptions.Center;
        tituloTmp.fontSize = 32f;
        tituloTmp.fontStyle = FontStyles.Bold;
        tituloTmp.color = new Color(0.96f, 0.94f, 1f, 1f);

        // Subtítulo
        var subtituloGo = new GameObject("Subtitulo");
        subtituloGo.transform.SetParent(panelModalGo.transform, false);
        var subtituloRect = subtituloGo.AddComponent<RectTransform>();
        subtituloRect.anchorMin = new Vector2(0.5f, 1f);
        subtituloRect.anchorMax = new Vector2(0.5f, 1f);
        subtituloRect.anchoredPosition = new Vector2(0, -95);
        subtituloRect.sizeDelta = new Vector2(750, 45);

        var subtituloTmp = subtituloGo.AddComponent<TextMeshProUGUI>();
        subtituloTmp.text = "Has alcanzado una nueva comunión con las estrellas.\nElige tu expansión celestial:";
        subtituloTmp.alignment = TextAlignmentOptions.Center;
        subtituloTmp.fontSize = 17f;
        subtituloTmp.color = new Color(0.80f, 0.75f, 0.95f, 0.95f);

        // Carta 1: VIGOR (Izquierda)
        var cartaVigorGo = CrearCarta(panelModalGo.transform,
            posX: -195,
            icono: "<color=#4DF07A>★</color>",
            nombre: "VIGOR",
            aumento: "+15 VIDA MÁXIMA",
            colorAcento: new Color(0.30f, 0.88f, 0.45f, 1f),
            colorFondo: new Color(0.08f, 0.16f, 0.10f, 0.95f),
            descripcion: "Aumenta permanentemente la barra de vida del grupo y la resistencia ante los embates oscuros.",
            out Image bordeVigorImg);

        // Carta 2: FLUJO (Derecha)
        var cartaFlujoGo = CrearCarta(panelModalGo.transform,
            posX: 195,
            icono: "<color=#4BB3FA>✦</color>",
            nombre: "FLUJO",
            aumento: "+10 MANÁ MÁXIMO",
            colorAcento: new Color(0.28f, 0.70f, 0.98f, 1f),
            colorFondo: new Color(0.08f, 0.12f, 0.22f, 0.95f),
            descripcion: "Amplía permanentemente la reserva mágica del grupo para sostener escudos, combos y conjuros potentes.",
            out Image bordeFlujoImg);

        // Footer / Ayuda
        var footerGo = new GameObject("FooterPrompt");
        footerGo.transform.SetParent(panelModalGo.transform, false);
        var footerRect = footerGo.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0, 30);
        footerRect.sizeDelta = new Vector2(700, 35);

        var footerTmp = footerGo.AddComponent<TextMeshProUGUI>();
        footerTmp.text = "D-Pad / Stick / Flechas: Seleccionar      [A / Espacio / Intro] Confirmar";
        footerTmp.alignment = TextAlignmentOptions.Center;
        footerTmp.fontSize = 15f;
        footerTmp.color = new Color(0.75f, 0.70f, 0.90f, 0.90f);

        // Componente controlador
        var controller = root.AddComponent<PopupEleccionSintoniaUI>();
        controller.panelModal = backdropGo; // Incluye backdrop para ocultar todo junto
        controller.canvasGroup = canvasGroup;
        controller.ventanaRect = panelRect;
        controller.tituloText = tituloTmp;
        controller.subtituloText = subtituloTmp;
        controller.cartaVigor = (RectTransform)cartaVigorGo.transform;
        controller.bordeVigor = bordeVigorImg;
        controller.cartaFlujo = (RectTransform)cartaFlujoGo.transform;
        controller.bordeFlujo = bordeFlujoImg;

        // Conectar eventos de ratón
        var slotVigor = cartaVigorGo.AddComponent<CardInteractionSlot>();
        slotVigor.Popup = controller;
        slotVigor.Indice = 0;

        var slotFlujo = cartaFlujoGo.AddComponent<CardInteractionSlot>();
        slotFlujo.Popup = controller;
        slotFlujo.Indice = 1;

        backdropGo.SetActive(false);
        return controller;
    }

    private static GameObject CrearCarta(Transform padre, float posX, string icono, string nombre, string aumento,
        Color colorAcento, Color colorFondo, string descripcion, out Image bordeImg)
    {
        var cartaGo = new GameObject($"Carta_{nombre}");
        cartaGo.transform.SetParent(padre, false);
        var cartaRect = cartaGo.AddComponent<RectTransform>();
        cartaRect.sizeDelta = new Vector2(360, 250);
        cartaRect.anchoredPosition = new Vector2(posX, -25);

        // Borde exterior con color de acento
        bordeImg = cartaGo.AddComponent<Image>();
        bordeImg.color = colorAcento;

        // Fondo interior oscuro inset 3px
        var interiorGo = new GameObject("Interior");
        interiorGo.transform.SetParent(cartaGo.transform, false);
        var interiorRect = interiorGo.AddComponent<RectTransform>();
        interiorRect.anchorMin = Vector2.zero;
        interiorRect.anchorMax = Vector2.one;
        interiorRect.offsetMin = new Vector2(3, 3);
        interiorRect.offsetMax = new Vector2(-3, -3);
        var bg = interiorGo.AddComponent<Image>();
        bg.color = colorFondo;

        // Icono
        var iconoGo = new GameObject("Icono");
        iconoGo.transform.SetParent(interiorGo.transform, false);
        var iconoRect = iconoGo.AddComponent<RectTransform>();
        iconoRect.anchoredPosition = new Vector2(0, 75);
        iconoRect.sizeDelta = new Vector2(100, 45);
        var iconoTmp = iconoGo.AddComponent<TextMeshProUGUI>();
        iconoTmp.text = icono;
        iconoTmp.alignment = TextAlignmentOptions.Center;
        iconoTmp.fontSize = 32f;

        // Nombre
        var nombreGo = new GameObject("Nombre");
        nombreGo.transform.SetParent(interiorGo.transform, false);
        var nombreRect = nombreGo.AddComponent<RectTransform>();
        nombreRect.anchoredPosition = new Vector2(0, 35);
        nombreRect.sizeDelta = new Vector2(320, 35);
        var nombreTmp = nombreGo.AddComponent<TextMeshProUGUI>();
        nombreTmp.text = nombre;
        nombreTmp.alignment = TextAlignmentOptions.Center;
        nombreTmp.fontSize = 24f;
        nombreTmp.fontStyle = FontStyles.Bold;
        nombreTmp.color = colorAcento;

        // Aumento
        var aumentoGo = new GameObject("Aumento");
        aumentoGo.transform.SetParent(interiorGo.transform, false);
        var aumentoRect = aumentoGo.AddComponent<RectTransform>();
        aumentoRect.anchoredPosition = new Vector2(0, 5);
        aumentoRect.sizeDelta = new Vector2(320, 25);
        var aumentoTmp = aumentoGo.AddComponent<TextMeshProUGUI>();
        aumentoTmp.text = aumento;
        aumentoTmp.alignment = TextAlignmentOptions.Center;
        aumentoTmp.fontSize = 17f;
        aumentoTmp.fontStyle = FontStyles.Bold;
        aumentoTmp.color = Color.white;

        // Descripción
        var descGo = new GameObject("Descripcion");
        descGo.transform.SetParent(interiorGo.transform, false);
        var descRect = descGo.AddComponent<RectTransform>();
        descRect.anchoredPosition = new Vector2(0, -55);
        descRect.sizeDelta = new Vector2(320, 75);
        var descTmp = descGo.AddComponent<TextMeshProUGUI>();
        descTmp.text = descripcion;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.fontSize = 14f;
        descTmp.color = new Color(0.90f, 0.88f, 0.95f, 0.90f);

        return cartaGo;
    }

    private class CardInteractionSlot : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public PopupEleccionSintoniaUI Popup;
        public int Indice;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Popup != null) Popup.Seleccionar(Indice);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Popup != null)
            {
                Popup.Seleccionar(Indice);
                Popup.Confirmar();
            }
        }
    }
}
