using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Banner centrado arriba cuando arranca una misión nueva («Nueva misión: {nombre}»). Es el único
/// aviso automático de misión nueva: el panel rápido de misiones solo se abre a mano. Ver INC-200
/// e INC-350.
///
/// Es un aviso del canal Cabecera (<see cref="ArbitroDeAvisos"/>): con más prioridad que la barra
/// del jefe, así que si coinciden entra primero el banner y la barra detrás (INC-859). Si arrancan
/// varias misiones seguidas, salen una detrás de otra. No es un menú: no oculta el minimapa ni el
/// HUD.
///
/// Vive en el Canvas del HUD persistente (Start.unity).
/// </summary>
public class QuestStartedBannerUI : MonoBehaviour, IAvisoEnPantalla, ISceneBoundUIHideGuard
{
    [Header("UI")]
    [SerializeField] private RectTransform bannerRoot;
    [SerializeField] private CanvasGroup bannerCanvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("Icono opcional. Sin icono o sin sprite, su hueco se oculta.")]
    [SerializeField] private Image icon;

    [Header("Animación")]
    [Tooltip("Desplazamiento vertical (px) desde el que entra el banner, por encima de su posición final.")]
    [SerializeField] private float dropOffsetY = 40f;
    [SerializeField] private float animInDuration = 0.5f;
    [SerializeField] private float animOutDuration = 0.3f;
    [Tooltip("Segundos que se ve cada misión (solo cuentan mientras el banner está a la vista).")]
    [SerializeField] private float displayDuration = 2.2f;
    [Tooltip("Golpe de escala del icono al entrar; 0 lo desactiva.")]
    [SerializeField] private float iconPunchScale = 0.25f;

    [Header("Turno")]
    [Tooltip("Prioridad dentro del canal Cabecera. Más alta que la de la barra del jefe para entrar antes que ella.")]
    [SerializeField] private int prioridad = 20;

    [Header("Audio")]
    [Tooltip("Clave de evento en AudioGraphProfile (AudioService.PlaySFX). Vacío = sin sonido.")]
    [SerializeField] private string sfxEventKey = "";

    private Vector2 _shownAnchoredPos;
    private QuestManager _lastQuestManager;
    private Coroutine _waitCo;
    private Coroutine _cuentaCo;
    private SceneBoundUI _sceneBoundUI;

    private readonly Queue<string> _pendientes = new();
    // Hay un texto a medio enseñar (entró y aún no ha salido): al recuperar el turno continúa.
    private bool _enCurso;
    // Tiene el turno y nada lo tapa: la cuenta atrás corre.
    private bool _aLaVista;
    // El banner ocupa la pantalla (desde que entra hasta que acaba de salir).
    private bool _enPantalla;

    public CanalDeAviso Canal => CanalDeAviso.Cabecera;
    public int Prioridad => prioridad;
    public bool Pasajero => true;

    /// <summary>ISceneBoundUIHideGuard: mientras el banner esté en pantalla, no se apaga por cambio de escena.</summary>
    public bool BlocksSceneHide() => _enPantalla;

    void Awake()
    {
        _sceneBoundUI = GetComponent<SceneBoundUI>();
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 110;
        }
        if (bannerRoot != null)
        {
            _shownAnchoredPos = bannerRoot.anchoredPosition;
            bannerRoot.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        _waitCo = StartCoroutine(BindWhenReady());
        if (_pendientes.Count > 0) ArbitroDeAvisos.Pedir(this);
    }

    void OnDisable()
    {
        UnsubscribeQuestManager();
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
        ArbitroDeAvisos.Terminar(this);
        PararCuenta();
        Recoger();
    }

    void OnDestroy()
    {
        ArbitroDeAvisos.Terminar(this);
        MatarTweens();
    }

    // ── Suscripción a QuestManager (puede no existir aún al activarse este componente, y puede
    //    recrearse entre sesiones de test en el Editor); mismo patrón que QuestLogListUI.
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

        _pendientes.Enqueue(string.Format(format, questName));
        ArbitroDeAvisos.Pedir(this);
    }

    // ── IAvisoEnPantalla ───────────────────────────────────────────────────────────────────────

    public void AlTenerTurno()
    {
        _aLaVista = true;
        if (_enCurso)
        {
            Fundir(1f, 0.2f);
            return;
        }
        MostrarSiguiente();
    }

    public void AlPerderTurno()
    {
        _aLaVista = false;
        if (_enCurso) Fundir(0f, 0.15f);
    }

    public void AlDescartar()
    {
        _aLaVista = false;
        _pendientes.Clear();
        PararCuenta();
        if (_enCurso) AnimateOut();
    }

    // ── Animación ──────────────────────────────────────────────────────

    void AnimateIn()
    {
        PararCuenta();
        MatarTweens();

        bannerRoot.gameObject.SetActive(true);
        bannerRoot.anchoredPosition = _shownAnchoredPos + Vector2.up * dropOffsetY;
        if (bannerCanvasGroup != null) bannerCanvasGroup.alpha = 0f;

        _enCurso = true;
        _enPantalla = true;

        bannerRoot.DOAnchorPos(_shownAnchoredPos, animInDuration).SetEase(Ease.OutBack).SetUpdate(true);
        Fundir(1f, animInDuration * 0.6f);

        if (icon != null && icon.gameObject.activeInHierarchy && iconPunchScale > 0f)
        {
            icon.transform.localScale = Vector3.one;
            icon.transform.DOPunchScale(Vector3.one * iconPunchScale, animInDuration, 6, 0.6f).SetUpdate(true);
        }

        if (!string.IsNullOrEmpty(sfxEventKey))
            AudioService.Instance?.PlaySFX(sfxEventKey);

        _cuentaCo = StartCoroutine(Co_Cuenta());
    }

    // Saca la siguiente misión pendiente o, si no queda ninguna, deja libre el canal.
    void MostrarSiguiente()
    {
        if (_pendientes.Count == 0 || bannerRoot == null || !isActiveAndEnabled)
        {
            if (bannerRoot == null) GameLog.Error("QuestStartedBannerUI", "bannerRoot es null — asignar en el Inspector.");
            _pendientes.Clear();
            ArbitroDeAvisos.Terminar(this);
            return;
        }
        string texto = _pendientes.Dequeue();
        if (titleText != null) titleText.text = texto;
        AnimateIn();
    }

    // El tiempo en pantalla solo corre mientras el banner está a la vista. Al acabar sale y, si
    // hay otra misión pendiente, entra detrás sin soltar el turno.
    IEnumerator Co_Cuenta()
    {
        float visto = 0f;
        while (visto < displayDuration)
        {
            if (_aLaVista) visto += Time.unscaledDeltaTime;
            yield return null;
        }
        AnimateOut();
        if (_pendientes.Count > 0)
        {
            yield return new WaitForSecondsRealtime(animOutDuration);
            _cuentaCo = null;
            if (_aLaVista) MostrarSiguiente();
        }
        else
        {
            _cuentaCo = null;
            ArbitroDeAvisos.Terminar(this);
        }
    }

    void AnimateOut()
    {
        _enCurso = false;
        if (bannerRoot == null) return;

        MatarTweens();
        bannerRoot.DOAnchorPos(_shownAnchoredPos + Vector2.up * dropOffsetY, animOutDuration).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() =>
            {
                if (_enCurso) return; // ya ha entrado el siguiente
                if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);
                _enPantalla = false;
                // Si un cambio de escena quiso apagar este objeto mientras el banner seguía en
                // pantalla, SceneBoundUI lo pospuso: se reintenta ahora.
                _sceneBoundUI?.ReapplySceneState();
            });
        Fundir(0f, animOutDuration);
    }

    void Fundir(float alfa, float duracion)
    {
        if (bannerCanvasGroup == null) return;
        bannerCanvasGroup.DOKill();
        bannerCanvasGroup.DOFade(alfa, duracion).SetUpdate(true);
    }

    void PararCuenta()
    {
        if (_cuentaCo != null) { StopCoroutine(_cuentaCo); _cuentaCo = null; }
    }

    // Deja el banner recogido sin animación (al desactivarse el objeto).
    void Recoger()
    {
        MatarTweens();
        _enCurso = false;
        _aLaVista = false;
        _enPantalla = false;
        if (bannerRoot != null) bannerRoot.gameObject.SetActive(false);
    }

    void MatarTweens()
    {
        if (bannerRoot != null) bannerRoot.DOKill();
        if (bannerCanvasGroup != null) bannerCanvasGroup.DOKill();
        if (icon != null) icon.transform.DOKill();
    }
}
