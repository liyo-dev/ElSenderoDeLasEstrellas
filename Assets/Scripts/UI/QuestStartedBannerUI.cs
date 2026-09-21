using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Aviso centrado en la parte superior de pantalla cuando arranca una misión nueva ("Nueva
/// misión: {nombre}"). Petición de Raúl (15 sept 2026): el único aviso que había hasta ahora era
/// el panel rápido de misiones (QuestLogListUI), que aparece de forma discreta por el lateral y es
/// fácil de no ver — "que salga algo centrado chulo" para que el jugador se entere seguro.
///
/// Mismo patrón que AbilityUnlockPopupUI (CanvasGroup + DOTween, auto-dismiss, se oculta si hay un
/// menú/cinemática encima) pero animación de entrada tipo "rebote" (Ease.OutBack) en vez de slide
/// lateral, y sin bloquear nada. NO oculta el minimapa ni el resto de HUD (a diferencia del panel
/// rápido, ver INC-199): este banner vive fuera del sistema de registro de menús de QuestMenuManager
/// a propósito.
///
/// ACTUALIZACIÓN (21 sept 2026, INC-350): Raúl pidió que este banner sea el ÚNICO aviso automático de
/// "nueva misión" -- el panel rápido de misiones (QuestLogListUI/QuickQuestMenu) ya NO se auto-muestra
/// como "toast" al arrancar una misión (se quitó de QuestLogListUI.OnQuestStarted() y de
/// QuestMenuManager.HandleQuestStarted()); ese panel ahora solo se abre a mano (D-pad arriba).
///
/// Debe vivir en el Canvas del HUD persistente (Start.unity), igual que AbilityUnlockPopupUI.
/// </summary>
public class QuestStartedBannerUI : MonoBehaviour, ISceneBoundUIHideGuard
{
    [Header("UI")]
    [SerializeField] private RectTransform bannerRoot;
    [SerializeField] private CanvasGroup bannerCanvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Image icon; // Opcional -- si no se asigna, o el sprite es null, el hueco del icono se oculta.

    [Header("Animación")]
    [Tooltip("Desplazamiento vertical (px) desde el que entra el banner, por encima de su posición final.")]
    [SerializeField] private float dropOffsetY = 40f;
    [SerializeField] private float animInDuration = 0.5f;
    [SerializeField] private float animOutDuration = 0.3f;
    [SerializeField] private float displayDuration = 2.8f;
    [Tooltip("Punch de escala del icono al entrar, para dar más 'chispa' -- 0 lo desactiva.")]
    [SerializeField] private float iconPunchScale = 0.25f;

    [Header("Audio")]
    [Tooltip("Clave de evento en AudioGraphProfile (AudioService.PlaySFX). Vacío = sin sonido. " +
             "Pendiente (15 sept 2026): no existe todavía ninguna clave dedicada a este aviso -- " +
             "añadir una entrada nueva (p.ej. 'UI_QuestStarted') en AudioGraphProfile con el clip " +
             "elegido antes de rellenar este campo. Sin clave asignada, o si la clave no se " +
             "encuentra, esto no falla: simplemente no suena nada (AudioService.PlaySFX ya es así).")]
    [SerializeField] private string sfxEventKey = "";

    private Vector2 _shownAnchoredPos;
    private Coroutine _autoDismissCoroutine;
    private bool _isShowing;
    private bool _hiddenByMenu;
    private QuestManager _lastQuestManager;
    private Coroutine _waitCo;
    private SceneBoundUI _sceneBoundUI;

    /// <summary>ISceneBoundUIHideGuard: mientras el banner esté en pantalla, no se apaga por cambio de escena.</summary>
    public bool BlocksSceneHide() => _isShowing;

    void Awake()
    {
        _sceneBoundUI = GetComponent<SceneBoundUI>();
        if (bannerRoot != null)
        {
            _shownAnchoredPos = bannerRoot.anchoredPosition;
            bannerRoot.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        _waitCo = StartCoroutine(BindWhenReady());
        // Mismo sistema que AbilityUnlockPopupUI/MinimapController: ocultarse mientras haya un
        // menú real abierto encima (pausa, inventario, menú de misiones abierto a mano...).
        // El toast automático del panel rápido de misiones NO cuenta como menú (ver INC-199), así
        // que este banner puede convivir con él sin parpadear entre los dos.
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;
    }

    void OnDisable()
    {
        UnsubscribeQuestManager();
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
        if (_autoDismissCoroutine != null) { StopCoroutine(_autoDismissCoroutine); _autoDismissCoroutine = null; }
        _isShowing = false;
    }

    void OnDestroy()
    {
        if (bannerRoot != null) bannerRoot.DOKill();
        if (bannerCanvasGroup != null) bannerCanvasGroup.DOKill();
        if (icon != null) icon.transform.DOKill();
    }

    // ── Suscripción robusta a QuestManager (puede no existir aún al activarse este componente,
    //    y puede recrearse entre sesiones de test en el Editor) -- mismo patrón que QuestLogListUI.
    IEnumerator BindWhenReady()
    {
        while (QuestManager.Instance == null) yield return null;

        if (_lastQuestManager != QuestManager.Instance)
        {
            UnsubscribeQuestManager();
            _lastQuestManager = QuestManager.Instance;
            _lastQuestManager.OnQuestStarted += HandleQuestStarted;
        }
    }

    void UnsubscribeQuestManager()
    {
        if (_lastQuestManager != null)
            _lastQuestManager.OnQuestStarted -= HandleQuestStarted;
        _lastQuestManager = null;
    }

    void HandleQuestStarted(string questId)
    {
        string questName = questId;
        if (QuestManager.Instance != null && QuestManager.Instance.TryGetQuestData(questId, out var data) && data != null)
            questName = data.GetLocalizedName();

        string format = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get("QUEST_STARTED_BANNER", "Nueva misión: {0}")
            : "Nueva misión: {0}";

        if (titleText != null) titleText.text = string.Format(format, questName);

        AnimateIn();

        if (!string.IsNullOrEmpty(sfxEventKey))
            AudioService.Instance?.PlaySFX(sfxEventKey);
    }

    // ── MenuManager (pausa / menú de misiones abierto a mano / cualquier otro menú real) ────────

    void OnMenuOpened(MenuKind kind)
    {
        if (!_isShowing || _hiddenByMenu || bannerCanvasGroup == null) return;
        _hiddenByMenu = true;
        bannerCanvasGroup.DOKill();
        bannerCanvasGroup.DOFade(0f, 0.15f).SetUpdate(true);
    }

    void OnMenuClosed(MenuKind kind)
    {
        if (!_hiddenByMenu) return;
        if (MenuManager.AnyOpen()) return; // todavía queda otro menú abierto
        _hiddenByMenu = false;

        if (!_isShowing || bannerCanvasGroup == null) return; // se auto-cerró mientras estaba oculto
        bannerCanvasGroup.DOKill();
        bannerCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
    }

    // ── Animación ──────────────────────────────────────────────────────

    void AnimateIn()
    {
        if (bannerRoot == null)
        {
            GameLog.Error("QuestStartedBannerUI", "bannerRoot es null — asignar en el Inspector.");
            return;
        }

        if (_autoDismissCoroutine != null) StopCoroutine(_autoDismissCoroutine);
        bannerRoot.DOKill();
        if (bannerCanvasGroup != null) bannerCanvasGroup.DOKill();

        bannerRoot.gameObject.SetActive(true);
        bannerRoot.anchoredPosition = _shownAnchoredPos + Vector2.up * dropOffsetY;
        if (bannerCanvasGroup != null) bannerCanvasGroup.alpha = 0f;

        _isShowing = true;
        _hiddenByMenu = false;

        bannerRoot.DOAnchorPos(_shownAnchoredPos, animInDuration).SetEase(Ease.OutBack).SetUpdate(true);
        if (bannerCanvasGroup != null)
            bannerCanvasGroup.DOFade(1f, animInDuration * 0.6f).SetUpdate(true);

        if (icon != null && icon.gameObject.activeInHierarchy && iconPunchScale > 0f)
        {
            icon.transform.localScale = Vector3.one;
            icon.transform.DOPunchScale(Vector3.one * iconPunchScale, animInDuration, 6, 0.6f).SetUpdate(true);
        }

        _autoDismissCoroutine = StartCoroutine(AutoDismiss());
    }

    IEnumerator AutoDismiss()
    {
        yield return new WaitForSecondsRealtime(displayDuration);
        HideBanner();
    }

    public void HideBanner()
    {
        if (bannerRoot == null || !_isShowing) return;

        _isShowing = false;
        if (_autoDismissCoroutine != null) { StopCoroutine(_autoDismissCoroutine); _autoDismissCoroutine = null; }

        bannerRoot.DOKill();
        if (bannerCanvasGroup != null) bannerCanvasGroup.DOKill();

        bannerRoot.DOAnchorPos(_shownAnchoredPos + Vector2.up * dropOffsetY, animOutDuration).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() =>
            {
                if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);
                // Mismo motivo que en AbilityUnlockPopupUI: si un cambio de escena intentó apagar
                // este objeto mientras el banner seguía en pantalla, SceneBoundUI lo pospuso
                // (BlocksSceneHide() daba true) -- reintentarlo ahora que ya terminó de mostrarse.
                _sceneBoundUI?.ReapplySceneState();
            });
        if (bannerCanvasGroup != null)
            bannerCanvasGroup.DOFade(0f, animOutDuration).SetUpdate(true);
    }
}
