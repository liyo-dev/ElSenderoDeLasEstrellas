using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class QuestLogListUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform contentRoot;      // ScrollView/Viewport/Content
    [SerializeField] private QuestLogItemUI itemPrefab;  // Prefab de item misión
    [SerializeField] private TextMeshProUGUI headerText; // "Misiones" (opcional)
    [SerializeField] private bool showInactive = false;  // filtrar inactivas
    [SerializeField] private GameObject panelRoot;       // El panel completo para show/hide
    [SerializeField] private GameObject scrollView;      // Solo el ScrollView para ocultar
    [SerializeField] private TextMeshProUGUI helpText;   // Texto de ayuda para cambiar

    [Header("Animación (DOTween)")]
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private float hideAfterSeconds = 4f;
    [SerializeField] private float hideDistance = 420f;
    [SerializeField] private float tweenDuration = 0.35f;
    [SerializeField] private Ease tweenEase = Ease.InOutSine;

    [Header("Animación de Completado")]
    [SerializeField] private Color completionColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] private float completionAnimDuration = 0.8f;
    [SerializeField] private float completionSlideDistance = 100f;
    [SerializeField] private float completionScalePunch = 1.15f;

    bool _bound;                    // ya suscrito al manager
    QuestManager _qm;               // cache del manager suscrito
    Coroutine _waitCo;
    private bool _isPanelVisible = false; // Estado del panel
    private Tween _panelTween;
    private Vector2 _shownPos;
    private Vector2 _hiddenPos;
    private Coroutine _autoHideCo;

    public bool IsVisible => _isPanelVisible;

    void OnEnable()
    {
        // Empieza a esperar al manager si aún no existe
        _waitCo = StartCoroutine(BindWhenReady());

        if (animatedRoot)
        {
            _shownPos = animatedRoot.anchoredPosition;
            _hiddenPos = _shownPos + Vector2.down * hideDistance;
            animatedRoot.anchoredPosition = _hiddenPos;
        }

        // Avoid deactivating the panelRoot if it's the same GameObject that holds this component,
        // because that would disable this component immediately after enabling it.
        if (panelRoot && panelRoot != this.gameObject) panelRoot.SetActive(false);
        if (scrollView) scrollView.SetActive(false);
        if (panelGroup)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        UpdateHelpText();
    }

    void OnDisable()
    {
        Unbind();
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
        KillTween();
        if (_autoHideCo != null) StopCoroutine(_autoHideCo);
        _isPanelVisible = false;
    }

    IEnumerator BindWhenReady()
    {
        // Espera a que QuestManager exista (creado por tu escena Start)
        while (QuestManager.Instance == null) yield return null;

        // Si cambió de instancia (p.ej. reload), re-suscribe limpio
        if (_qm != QuestManager.Instance)
        {
            Unbind();
            _qm = QuestManager.Instance;
            _qm.OnQuestsChanged += Rebuild;
            _qm.OnQuestStarted += OnQuestStarted;
            _qm.OnQuestCompleted += OnQuestCompleted;
            _qm.OnQuestVisibilityChanged += OnQuestVisibilityChanged;
            _bound = true;
        }

        Rebuild();
    }

    void Unbind()
    {
        if (_bound && _qm != null)
        {
            _qm.OnQuestsChanged -= Rebuild;
            _qm.OnQuestStarted -= OnQuestStarted;
            _qm.OnQuestCompleted -= OnQuestCompleted;
            _qm.OnQuestVisibilityChanged -= OnQuestVisibilityChanged;
        }
        _bound = false;
        _qm = null;
    }

    public void Rebuild()
    {
        if (!contentRoot || itemPrefab == null) return;
        if (QuestManager.Instance == null) return; // por si se descargó la escena

        // PERF (7 sept 2026): antes se hacia Destroy+Instantiate de TODAS las filas en
        // cada evento OnQuestsChanged (arranque/paso/archivado de CUALQUIER quest, no
        // solo al abrir un menu -- este tracker esta activo en juego normal todo el
        // rato). Mismo patron de pool que QuestMainMenuUI.Rebuild()/ShopUI.RebuildItemList().
        // QuestLogItemUI.ResetVisualState() ya existia preparada para esto exacto
        // (deshace la animacion de "completado" antes de reutilizar la fila) pero nadie
        // la llamaba porque Rebuild() seguia destruyendo y recreando todo.
        int used = 0;

        foreach (var rq in QuestManager.Instance.GetAll())
        {
            if (QuestManager.Instance.GetVisibility(rq.Id) == QuestVisibility.Hidden) continue;
            if (!showInactive && rq.State == QuestState.Inactive) continue;

            QuestLogItemUI item;
            if (used < contentRoot.childCount)
            {
                var child = contentRoot.GetChild(used);
                child.gameObject.SetActive(true);
                item = child.GetComponent<QuestLogItemUI>();
                KillItemTweens(item);
                item.ResetVisualState();
            }
            else
            {
                item = Instantiate(itemPrefab, contentRoot);
            }
            used++;

            item.Bind(rq); // el propio item gestiona nulls internos
        }

        // Ocultar (no destruir) las filas sobrantes de un rebuild anterior con mas quests.
        for (int i = used; i < contentRoot.childCount; i++)
            contentRoot.GetChild(i).gameObject.SetActive(false);
    }

    /// <summary>
    /// Corta en seco cualquier tween de AnimateQuestCompletion() que pudiera seguir activo
    /// sobre esta fila antes de reutilizarla para otra quest -- sin esto, un Rebuild()
    /// disparado a mitad del fade de salida (p.ej. dos quests completandose casi a la vez)
    /// dejaria el tween antiguo peleando un frame contra los valores que ResetVisualState()
    /// acaba de fijar.
    /// </summary>
    void KillItemTweens(QuestLogItemUI item)
    {
        if (item == null) return;
        item.transform.DOKill();
        var image = item.GetComponent<Image>();
        if (image != null) image.DOKill();
        var canvasGroup = item.GetComponent<CanvasGroup>();
        if (canvasGroup != null) canvasGroup.DOKill();
    }

    void OnQuestStarted(string questId)
    {
        // FIX (21 sept 2026, Raul: "debe salir SOLO el pop up nuevo, no el menu de misiones" --
        // INC-346). Antes este panel se auto-mostraba como "toast" cada vez que arrancaba una
        // mision nueva (ver el resto de este comentario, que documentaba isAutoToast/INC-199).
        // Ahora el UNICO aviso automatico de "nueva mision" es QuestStartedBannerUI (el banner
        // centrado). Este panel (QuickQuestMenu) ya NO se abre solo -- solo lo abre el jugador a
        // mano (D-pad arriba, ver QuestMenuManager). Los datos se refrescan igual sin mostrarse:
        // OnQuestsChanged ya llama a Rebuild() por separado (QuestManager.StartQuest dispara
        // ambos eventos), asi que no hace falta hacer nada aqui.
    }

    void OnQuestCompleted(string questId)
    {
        // Buscar el item UI de esta quest y animarlo antes de archivar
        if (contentRoot == null) return;

        for (int i = 0; i < contentRoot.childCount; i++)
        {
            var itemUI = contentRoot.GetChild(i).GetComponent<QuestLogItemUI>();
            if (itemUI != null && itemUI.QuestId == questId)
            {
                StartCoroutine(AnimateQuestCompletion(itemUI));
                break;
            }
        }
    }

    void OnQuestVisibilityChanged(string questId, QuestVisibility vis)
    {
        Rebuild();
    }

    public void TogglePanel()
    {
        ShowPanel(!_isPanelVisible);
    }

    /// <summary>
    /// True mientras el panel está visible SOLO como aviso automático de "nueva misión" (toast),
    /// no porque el jugador lo haya abierto a mano con el D-pad. QuestMenuManager.RefreshMenuRegistration()
    /// usa esto para no registrar este aviso como un menú real en MenuManager -- si no, el minimapa y
    /// el resto de UI in-world (SpeechBubbleUI, BossHealthBar, LorePopupUI, AbilityUnlockPopupUI,
    /// TutorialPromptUI: todos suscritos a MenuManager.MenuOpened) se ocultaban solos cada vez que
    /// arrancaba una misión nueva, aunque el jugador no hubiera abierto ningún menú (INC-199).
    /// </summary>
    public bool IsAutoToast { get; private set; }

    public void ShowPanel(bool show, bool ignoreRestrictions = false, bool isAutoToast = false)
    {
        if (show && !ignoreRestrictions)
        {
            if (!GameState.CanOpenInventory) return;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;
        }

        _isPanelVisible = show;
        IsAutoToast = show && isAutoToast;

        if (_isPanelVisible)
            AnimateShow();
        else
            AnimateHide();

        UpdateHelpText();
    }

    void AnimateShow()
    {
        if (panelRoot) panelRoot.SetActive(true);
        if (scrollView) scrollView.SetActive(true);
        KillTween();
        if (panelGroup)
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = true;
            panelGroup.interactable = true;
            _panelTween = panelGroup.DOFade(1f, tweenDuration).SetEase(tweenEase).SetUpdate(true);
        }
        if (animatedRoot)
        {
            animatedRoot.anchoredPosition = _hiddenPos;
            _panelTween = animatedRoot.DOAnchorPos(_shownPos, tweenDuration).SetEase(tweenEase).SetUpdate(true);
        }
        RestartAutoHide();
    }

    void AnimateHide()
    {
        KillTween();
        if (panelGroup)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            _panelTween = panelGroup.DOFade(0f, tweenDuration).SetEase(tweenEase).SetUpdate(true)
                .OnComplete(() => { if (panelRoot) panelRoot.SetActive(false); });
        }
        if (animatedRoot)
        {
            _panelTween = animatedRoot.DOAnchorPos(_hiddenPos, tweenDuration).SetEase(tweenEase).SetUpdate(true)
                .OnComplete(() =>
                {
                    if (scrollView) scrollView.SetActive(false);
                    if (panelGroup == null && panelRoot) panelRoot.SetActive(false);
                });
        }
        else
        {
            if (panelRoot) panelRoot.SetActive(false);
            if (scrollView) scrollView.SetActive(false);
        }
        if (_autoHideCo != null) { StopCoroutine(_autoHideCo); _autoHideCo = null; }
    }

    void RestartAutoHide()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_autoHideCo != null) StopCoroutine(_autoHideCo);
        _autoHideCo = StartCoroutine(AutoHideAfterDelay());
    }

    IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(hideAfterSeconds);
        _isPanelVisible = false;
        IsAutoToast = false;
        AnimateHide();
        UpdateHelpText();
    }

    void UpdateHelpText()
    {
        if (!helpText) return;
        string key = _isPanelVisible ? "UI_MISIONES_OCULTAR" : "UI_MISIONES_SHOW";
        string fallback = _isPanelVisible ? "Ocultar misiones" : "Mostrar misiones";
        string text;
        if (LocalizationManager.Instance != null)
        {
            text = LocalizationManager.Instance.Get(key, fallback);
        }
        else
        {
            text = fallback;
        }

        helpText.text = ResolveDeviceTokens(text);
    }

    /// <summary>
    /// Sustituye el token literal "{DPAD_UP}" (si aparece en el texto) por la etiqueta de texto del
    /// botón que abre/oculta el detalle de misiones en el dispositivo activo (D-Pad arriba en mando,
    /// "J" en teclado — ver PlayerControls.inputactions). Antes este texto tenía el literal
    /// "[D-Pad ▲]" fijo en las claves UI_MISIONES_OCULTAR/UI_MISIONES_SHOW de ui_es.json/ui_en.json,
    /// que era incorrecto en teclado+ratón (mismo patrón que Loc() en TagMinigameController con
    /// "{SPRINT}"; ver InputGlyphNames.DpadUp/InputGlyphLabels).
    /// </summary>
    string ResolveDeviceTokens(string text)
    {
        if (!string.IsNullOrEmpty(text) && text.Contains("{DPAD_UP}"))
        {
            string dpadUpLabel = Core.InputGlyphs.InputGlyphLabels.GetLabel(
                Core.InputGlyphs.InputGlyphNames.DpadUp, Core.InputGlyphs.InputGlyphService.CurrentFamily);
            text = text.Replace("{DPAD_UP}", dpadUpLabel);
        }
        return text;
    }

    void KillTween()
    {
        if (_panelTween != null && _panelTween.IsActive()) _panelTween.Kill();
        _panelTween = null;
    }

    IEnumerator AnimateQuestCompletion(QuestLogItemUI itemUI)
    {
        if (itemUI == null) yield break;

        var rect = itemUI.GetComponent<RectTransform>();
        var canvasGroup = itemUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = itemUI.gameObject.AddComponent<CanvasGroup>();

        var originalPos = rect.anchoredPosition;
        var originalScale = rect.localScale;

        // 1. Flash verde + punch scale
        var image = itemUI.GetComponent<Image>();
        if (image != null)
        {
            var originalColor = image.color;
            image.DOColor(completionColor, completionAnimDuration * 0.3f).SetEase(Ease.OutQuad);
        }
        
        rect.DOPunchScale(Vector3.one * (completionScalePunch - 1f), completionAnimDuration * 0.4f, 4, 0.5f);

        yield return new WaitForSeconds(completionAnimDuration * 0.5f);

        // 2. Slide hacia la derecha y fade out
        var sequence = DOTween.Sequence();
        sequence.Append(rect.DOAnchorPosX(originalPos.x + completionSlideDistance, completionAnimDuration * 0.5f).SetEase(Ease.InBack));
        sequence.Join(canvasGroup.DOFade(0f, completionAnimDuration * 0.5f).SetEase(Ease.InQuad));

        yield return sequence.WaitForCompletion();

        // 3. Rebuild para eliminar el item (ahora archivado)
        Rebuild();
    }
}
