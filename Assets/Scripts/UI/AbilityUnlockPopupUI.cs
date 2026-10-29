using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Popup que entra por la derecha cuando se desbloquea una habilidad o un hechizo y se cierra
/// solo. Cada uno sale una vez por partida (el flag se guarda en el preset).
///
/// Es un aviso del canal Lateral (<see cref="ArbitroDeAvisos"/>): si llegan varios desbloqueos a
/// la vez salen uno detrás de otro, y mientras hay un menú, una cinemática o la intro de un jefe
/// esperan con la cuenta atrás parada.
///
/// Implementa ISceneBoundUIHideGuard: mientras el popup está en pantalla, el SceneBoundUI de este
/// GameObject no lo apaga por un cambio de escena o de cámara; es información que el jugador
/// tiene que poder leer.
/// </summary>
public class AbilityUnlockPopupUI : MonoBehaviour, IAvisoEnPantalla, ISceneBoundUIHideGuard
{
    [Header("UI")]
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private TextMeshProUGUI abilityTitleText;
    [SerializeField] private TextMeshProUGUI abilityDescriptionText;
    [SerializeField] private Image abilityIcon;

    [Header("Datos")]
    [SerializeField] private List<AbilityPresentation> abilityPresentations = new();
    [SerializeField] private List<AbilityPresentationForKey> abilityKeyPresentations = new();
    [SerializeField] private List<SpellPresentation> spellPresentations = new();

    [Header("Animación")]
    [SerializeField] private float slideOffscreenX = 450f;
    [SerializeField] private float animInDuration = 0.45f;
    [SerializeField] private float animOutDuration = 0.3f;
    [Tooltip("Segundos que se ve cada popup (solo cuentan mientras está a la vista).")]
    [SerializeField] private float displayDuration = 3.5f;

    [Header("Turno")]
    [Tooltip("Prioridad dentro del canal Lateral.")]
    [SerializeField] private int prioridad = 10;

    private struct Desbloqueo
    {
        public string titulo;
        public string descripcion;
        public Sprite icono;
    }

    private readonly Queue<Desbloqueo> _pendientes = new();
    private readonly HashSet<string> _shownFlags = new();
    private bool _flagsLoaded;
    private Coroutine _cuentaCo;
    // Hay un popup a medio enseñar: al recuperar el turno continúa donde estaba.
    private bool _enCurso;
    // Tiene el turno y nada lo tapa: la cuenta atrás corre.
    private bool _aLaVista;
    // El popup ocupa la pantalla (desde que entra hasta que acaba de salir).
    private bool _enPantalla;
    // SceneBoundUI del mismo GameObject: al terminar se le pide que aplique un ocultado por cambio
    // de escena que quedó pospuesto mientras BlocksSceneHide() devolvía true.
    private SceneBoundUI _sceneBoundUI;

    const string AbilityFlagPrefix = "ABILITY_POPUP_ID:";
    const string AbilityKeyFlagPrefix = "ABILITY_POPUP_KEY:";
    const string SpellFlagPrefix = "SPELL_POPUP_ID:";

    public CanalDeAviso Canal => CanalDeAviso.Lateral;
    public int Prioridad => prioridad;
    public bool Pasajero => true;

    /// <summary>ISceneBoundUIHideGuard: mientras el popup esté en pantalla, no se apaga por cambio de escena.</summary>
    public bool BlocksSceneHide() => _enPantalla;

    void Awake()
    {
        _sceneBoundUI = GetComponent<SceneBoundUI>();
        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(AbilityUnlockPopupUI));
        GameBootService.OnProfileReady += HandleProfileReady;
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;
        UnlockService.OnSpellUnlocked += HandleSpellUnlocked;
        ReloadShownFlags();
        if (_pendientes.Count > 0) ArbitroDeAvisos.Pedir(this);
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
        UnlockService.OnSpellUnlocked -= HandleSpellUnlocked;
        ArbitroDeAvisos.Terminar(this);
        PararCuenta();
        Recoger();
    }

    void OnDestroy()
    {
        ArbitroDeAvisos.Terminar(this);
        MatarTweens();
    }

    // ── Recarga de flags al cambiar de preset ──────────────────────────────────

    private void HandleProfileReady()
    {
        _flagsLoaded = false;
        ReloadShownFlags();
    }

    // ── Handlers de desbloqueo ─────────────────────────────────────────────────

    private void HandleAbilityUnlocked(AbilityId ability)
    {
        string flag = GetAbilityFlag(ability);
        if (!MarcarSiEsNuevo(flag)) return;
        var p = AbilityPresentationLookup.Resolve(ability, abilityPresentations);
        Encolar(p.title, p.description, p.icon);
    }

    private void HandleAbilityUnlockedKey(AbilityKey key)
    {
        string flag = GetAbilityKeyFlag(key);
        if (!MarcarSiEsNuevo(flag)) return;
        var p = AbilityPresentationKeyLookup.Resolve(key, abilityKeyPresentations);
        Encolar(p.title, p.description, p.icon);
    }

    private void HandleSpellUnlocked(SpellId spell)
    {
        string flag = GetSpellFlag(spell);
        if (!MarcarSiEsNuevo(flag)) return;
        var p = SpellPresentationLookup.Resolve(spell, spellPresentations);
        Encolar(p.title, p.description, p.icon);
    }

    /// True si el popup de este flag no había salido nunca (y lo marca como visto).
    private bool MarcarSiEsNuevo(string flag)
    {
        if (HasSeenFlag(flag))
        {
            GameLog.Log("AbilityUnlockPopupUI", $"Ya mostrado ({flag}), saltando.");
            return false;
        }
        MarkFlag(flag);
        return true;
    }

    private void Encolar(string titulo, string descripcion, Sprite icono)
    {
        _pendientes.Enqueue(new Desbloqueo { titulo = titulo, descripcion = descripcion, icono = icono });
        ArbitroDeAvisos.Pedir(this);
    }

    // ── IAvisoEnPantalla ───────────────────────────────────────────────────────

    public void AlTenerTurno()
    {
        _aLaVista = true;
        if (_enCurso)
        {
            // Vuelve a entrar deslizándose, como la primera vez.
            Entrar();
            return;
        }
        MostrarSiguiente();
    }

    public void AlPerderTurno()
    {
        _aLaVista = false;
        if (!_enCurso || popupCanvasGroup == null) return;
        popupCanvasGroup.DOKill();
        popupCanvasGroup.DOFade(0f, 0.15f).SetUpdate(true);
        popupCanvasGroup.blocksRaycasts = false;
    }

    public void AlDescartar()
    {
        _aLaVista = false;
        _pendientes.Clear();
        PararCuenta();
        if (_enCurso) AnimateOut();
    }

    private void MostrarSiguiente()
    {
        if (_pendientes.Count == 0 || popupRoot == null || !isActiveAndEnabled)
        {
            if (popupRoot == null) GameLog.Error("AbilityUnlockPopupUI", "popupRoot es null — asignar en el Inspector.");
            _pendientes.Clear();
            ArbitroDeAvisos.Terminar(this);
            return;
        }
        var d = _pendientes.Dequeue();
        SetTexts(d.titulo, d.descripcion, d.icono);
        AnimateIn();
    }

    private void SetTexts(string title, string description, Sprite icon)
    {
        if (abilityTitleText != null) abilityTitleText.text = title;
        if (abilityDescriptionText != null) abilityDescriptionText.text = description;
        if (abilityIcon != null) { abilityIcon.sprite = icon; abilityIcon.enabled = icon != null; }
        AjustarAlTexto();
    }

    // ── Alto del panel según el texto ─────────────────────────────────────────
    //
    // La descripción ocupa todo el hueco que queda debajo del título, y si el texto no cabe (una
    // descripción de varias líneas) el panel crece hacia arriba. Con el tamaño fijo del diseño, un
    // texto de varias líneas centrado en el panel se montaba encima del título. Ver INC-542.

    private const float MargenDelTexto = 10f;

    private RectTransform _panelDelTexto;
    private RectTransform _rectDescripcion;
    private RectTransform _rectTitulo;
    private Vector2 _tamanoPanelDeDiseno;
    private bool _medidasGuardadas;

    private void GuardarMedidasDeDiseno()
    {
        if (_medidasGuardadas || abilityDescriptionText == null) return;
        _rectDescripcion = abilityDescriptionText.rectTransform;
        _panelDelTexto = _rectDescripcion.parent as RectTransform;
        _rectTitulo = abilityTitleText != null ? abilityTitleText.rectTransform : null;
        if (_panelDelTexto == null) return;
        _tamanoPanelDeDiseno = _panelDelTexto.sizeDelta;
        _medidasGuardadas = true;
    }

    private void AjustarAlTexto()
    {
        GuardarMedidasDeDiseno();
        if (!_medidasGuardadas) return;

        float altoTitulo = _rectTitulo != null && _rectTitulo.parent == _panelDelTexto ? _rectTitulo.rect.height : 0f;
        float ancho = _rectDescripcion.rect.width;
        float altoTexto = abilityDescriptionText.GetPreferredValues(abilityDescriptionText.text, ancho, 0f).y;

        float alto = Mathf.Max(_tamanoPanelDeDiseno.y, altoTitulo + altoTexto + 2f * MargenDelTexto);
        _panelDelTexto.sizeDelta = new Vector2(_tamanoPanelDeDiseno.x, alto);

        // Descripción: el hueco entre el borde de abajo y el título, con el texto centrado en él.
        _rectDescripcion.anchorMin = _rectDescripcion.anchorMax = new Vector2(0.5f, 0.5f);
        _rectDescripcion.pivot = new Vector2(0.5f, 0.5f);
        _rectDescripcion.sizeDelta = new Vector2(_rectDescripcion.sizeDelta.x, alto - altoTitulo - 2f * MargenDelTexto);
        _rectDescripcion.anchoredPosition = new Vector2(_rectDescripcion.anchoredPosition.x, -altoTitulo * 0.5f);
    }

    private void AnimateIn()
    {
        PararCuenta();
        popupRoot.gameObject.SetActive(true);
        _enCurso = true;
        _enPantalla = true;
        Entrar();
        _cuentaCo = StartCoroutine(Co_Cuenta());
    }

    // Desliza el popup desde fuera de la pantalla (derecha) hasta su sitio.
    private void Entrar()
    {
        MatarTweens();
        popupRoot.anchoredPosition = new Vector2(slideOffscreenX, popupRoot.anchoredPosition.y);
        popupRoot.DOAnchorPosX(0f, animInDuration).SetEase(Ease.OutBack).SetUpdate(true);
        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.alpha = 0f;
            popupCanvasGroup.blocksRaycasts = true;
            popupCanvasGroup.DOFade(1f, animInDuration * 0.7f).SetUpdate(true);
        }
    }

    // El tiempo en pantalla solo corre mientras el popup está a la vista. Al acabar sale y, si
    // hay otro desbloqueo pendiente, entra detrás sin soltar el turno.
    private IEnumerator Co_Cuenta()
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

    private void AnimateOut()
    {
        _enCurso = false;
        if (popupRoot == null) return;

        MatarTweens();
        popupRoot.DOAnchorPosX(slideOffscreenX, animOutDuration).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() =>
            {
                if (_enCurso) return; // ya ha entrado el siguiente
                if (popupRoot != null) popupRoot.gameObject.SetActive(false);
                _enPantalla = false;
                // Si un cambio de escena quiso apagar este objeto mientras el popup seguía en
                // pantalla, SceneBoundUI lo pospuso: se reintenta ahora.
                _sceneBoundUI?.ReapplySceneState();
            });
        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.blocksRaycasts = false;
            popupCanvasGroup.DOFade(0f, animOutDuration).SetUpdate(true);
        }
    }

    private void PararCuenta()
    {
        if (_cuentaCo != null) { StopCoroutine(_cuentaCo); _cuentaCo = null; }
    }

    // Deja el popup recogido sin animación (al desactivarse el objeto).
    private void Recoger()
    {
        MatarTweens();
        _enCurso = false;
        _aLaVista = false;
        _enPantalla = false;
        if (popupRoot != null) popupRoot.gameObject.SetActive(false);
    }

    private void MatarTweens()
    {
        if (popupRoot != null) popupRoot.DOKill();
        if (popupCanvasGroup != null) popupCanvasGroup.DOKill();
    }

    // ── Flags (persistencia de "ya mostrado") ──────────────────────────────────

    void ReloadShownFlags()
    {
        if (_flagsLoaded) return;
        _shownFlags.Clear();
        var preset = UnlockService.GetActivePreset();
        var flags = preset?.flags;
        if (flags != null)
        {
            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (string.IsNullOrEmpty(flag)) continue;
                if (flag.StartsWith(AbilityFlagPrefix) || flag.StartsWith(AbilityKeyFlagPrefix) || flag.StartsWith(SpellFlagPrefix))
                    _shownFlags.Add(flag);
            }
        }
        _flagsLoaded = true;
    }

    string GetAbilityFlag(AbilityId id) => $"{AbilityFlagPrefix}{id}";
    string GetAbilityKeyFlag(AbilityKey key) => $"{AbilityKeyFlagPrefix}{key}";
    string GetSpellFlag(SpellId id) => $"{SpellFlagPrefix}{id}";

    bool HasSeenFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        ReloadShownFlags();
        return _shownFlags.Contains(flag);
    }

    public static void ResetFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        var preset = UnlockService.GetActivePreset();
        if (preset?.flags != null) preset.flags.Remove(flag);
    }

    void MarkFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        ReloadShownFlags();
        if (!_shownFlags.Add(flag)) return;
        var preset = UnlockService.GetActivePreset();
        if (preset == null) return;
        if (preset.flags == null) preset.flags = new List<string>();
        if (!preset.flags.Contains(flag)) preset.flags.Add(flag);
    }
}
