using System.Collections.Generic;
using Core.InputGlyphs;
using DG.Tweening;
using UnityEngine;
using TMPro;

namespace Sendero.UI
{
    /// <summary>
    /// Panel del combo mágico (INC-494), sobre la cruz de combate. Al abrir el círculo con la Y:
    /// arriba, los iconos de los combos aprendidos; abajo, los botones que se van tecleando y una
    /// barra con el tiempo que queda para el siguiente. Los combos que ya no encajan se apagan.
    /// Construye sus piezas en tiempo de ejecución; solo necesita los sprites de fondo y relleno.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ComboPanelUI : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite rowBackground;
        [SerializeField] private Sprite timerFill;

        [Header("Medidas (unidades locales)")]
        [SerializeField] private float hintSize = 120f;
        [SerializeField] private float symbolSize = 110f;
        [SerializeField] private float spacing = 18f;
        [SerializeField] private int maxSymbols = 5;

        [Header("Colores")]
        [SerializeField] private Color hintOn = Color.white;
        [SerializeField] private Color hintOff = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color timerColor = new Color(0.95f, 0.76f, 0.31f, 0.9f);
        [SerializeField] private Color failColor = new Color(1f, 0.35f, 0.35f, 1f);

        [Header("Animación")]
        [SerializeField] private float fadeIn = 0.1f;
        [SerializeField] private float fadeOut = 0.3f;
        [SerializeField] private float resultHold = 0.35f;

        private CanvasGroup _group;
        private RectTransform _hintsRow, _symbolsRow;
        private UnityEngine.UI.Image _symbolsBg, _timer;
        private TextMeshProUGUI _message;
        private readonly List<UnityEngine.UI.Image> _hints = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.Image> _symbols = new List<UnityEngine.UI.Image>();
        private IReadOnlyList<MagicSpellSO> _repertoire;
        private ComboCastController _combo;
        private float _nextBind;
        private Sequence _fade;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            Build();
        }

        private void OnEnable()
        {
            PlayerPresetService.OnPresetApplied += Rebind;
            PartyControlManager.OnActiveCharacterChanged += OnCharacterSwitched;
        }

        private void OnDisable()
        {
            PlayerPresetService.OnPresetApplied -= Rebind;
            PartyControlManager.OnActiveCharacterChanged -= OnCharacterSwitched;
            Unbind();
            _fade?.Kill();
        }

        private void OnCharacterSwitched(int _) => Rebind();

        private void Update()
        {
            if (_combo == null)
            {
                if (Time.unscaledTime >= _nextBind) { _nextBind = Time.unscaledTime + 1f; Rebind(); }
                return;
            }
            if (_combo.IsOpen && _timer != null) _timer.fillAmount = _combo.TimeLeftNormalized;
        }

        // ── Enlace ─────────────────────────────────────────────────────────

        private void Rebind()
        {
            Unbind();
            var player = PlayerService.Player;
            if (player == null) return;
            _combo = player.GetComponentInChildren<ComboCastController>(true);
            if (_combo == null) return;
            _combo.OnOpened += HandleOpened;
            _combo.OnInput += HandleInput;
            _combo.OnClosed += HandleClosed;
            _combo.OnDenied += HandleDenied;
        }

        private void Unbind()
        {
            if (_combo == null) return;
            _combo.OnOpened -= HandleOpened;
            _combo.OnInput -= HandleInput;
            _combo.OnClosed -= HandleClosed;
            _combo.OnDenied -= HandleDenied;
            _combo = null;
        }

        // ── Construcción ───────────────────────────────────────────────────

        private void Build()
        {
            var rt = (RectTransform)transform;

            _hintsRow = NewRect("Hints", rt);
            _hintsRow.anchorMin = _hintsRow.anchorMax = new Vector2(0.5f, 1f);
            _hintsRow.pivot = new Vector2(0.5f, 1f);
            _hintsRow.anchoredPosition = Vector2.zero;
            _hintsRow.sizeDelta = new Vector2(rt.sizeDelta.x, hintSize);

            float rowW = maxSymbols * symbolSize + (maxSymbols + 1) * spacing;
            _symbolsRow = NewRect("Symbols", rt);
            _symbolsRow.anchorMin = _symbolsRow.anchorMax = new Vector2(0.5f, 1f);
            _symbolsRow.pivot = new Vector2(0.5f, 1f);
            _symbolsRow.anchoredPosition = new Vector2(0f, -(hintSize + spacing * 1.5f));
            _symbolsRow.sizeDelta = new Vector2(rowW, symbolSize + spacing * 2f);
            _symbolsBg = _symbolsRow.gameObject.AddComponent<UnityEngine.UI.Image>();
            _symbolsBg.sprite = rowBackground;
            _symbolsBg.type = UnityEngine.UI.Image.Type.Sliced;
            _symbolsBg.raycastTarget = false;

            for (int i = 0; i < maxSymbols; i++)
            {
                var s = NewRect("Symbol" + (i + 1), _symbolsRow);
                s.anchorMin = s.anchorMax = new Vector2(0f, 0.5f);
                s.pivot = new Vector2(0.5f, 0.5f);
                s.sizeDelta = new Vector2(symbolSize, symbolSize);
                s.anchoredPosition = new Vector2(spacing + symbolSize * 0.5f + i * (symbolSize + spacing), 0f);
                var img = s.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = false;
                _symbols.Add(img);
            }

            var timerRt = NewRect("Timer", _symbolsRow);
            timerRt.anchorMin = new Vector2(0f, 0f);
            timerRt.anchorMax = new Vector2(1f, 0f);
            timerRt.pivot = new Vector2(0.5f, 1f);
            timerRt.offsetMin = new Vector2(spacing * 2f, -22f);
            timerRt.offsetMax = new Vector2(-spacing * 2f, -8f);
            _timer = timerRt.gameObject.AddComponent<UnityEngine.UI.Image>();
            _timer.sprite = timerFill;
            _timer.color = timerColor;
            _timer.type = UnityEngine.UI.Image.Type.Filled;
            _timer.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _timer.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            _timer.raycastTarget = false;

            var msgRt = NewRect("Message", rt);
            msgRt.anchorMin = new Vector2(0f, 1f);
            msgRt.anchorMax = new Vector2(1f, 1f);
            msgRt.pivot = new Vector2(0.5f, 1f);
            msgRt.anchoredPosition = Vector2.zero;
            msgRt.sizeDelta = new Vector2(0f, hintSize);
            _message = msgRt.gameObject.AddComponent<TextMeshProUGUI>();
            var anyText = GetComponentInParent<Canvas>() != null ? GetComponentInParent<Canvas>().GetComponentInChildren<TMP_Text>(true) : null;
            if (anyText != null && anyText != _message) _message.font = anyText.font;
            _message.fontSize = 56f;
            _message.alignment = TextAlignmentOptions.Center;
            _message.textWrappingMode = TextWrappingModes.Normal;
            _message.color = Color.white;
            _message.outlineWidth = 0.2f;
            _message.raycastTarget = false;
            _message.gameObject.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private UnityEngine.UI.Image GetHint(int i)
        {
            while (_hints.Count <= i)
            {
                var rt = NewRect("Hint" + (_hints.Count + 1), _hintsRow);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(hintSize, hintSize);
                var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                _hints.Add(img);
            }
            return _hints[i];
        }

        // ── Eventos del combo ──────────────────────────────────────────────

        private void HandleDenied(string message)
        {
            _message.text = message;
            _message.gameObject.SetActive(true);
            _hintsRow.gameObject.SetActive(false);
            _symbolsRow.gameObject.SetActive(false);
            _fade?.Kill();
            _fade = DOTween.Sequence().SetUpdate(true)
                .Append(_group.DOFade(1f, fadeIn))
                .AppendInterval(1.2f)
                .Append(_group.DOFade(0f, fadeOut));
        }

        private void HandleOpened(IReadOnlyList<MagicSpellSO> repertoire)
        {
            _message.gameObject.SetActive(false);
            _hintsRow.gameObject.SetActive(true);
            _symbolsRow.gameObject.SetActive(true);
            _repertoire = repertoire;
            int n = repertoire.Count;
            float total = n * hintSize + (n - 1) * spacing;
            for (int i = 0; i < _hints.Count; i++) _hints[i].gameObject.SetActive(false);
            for (int i = 0; i < n; i++)
            {
                var h = GetHint(i);
                h.gameObject.SetActive(true);
                h.sprite = repertoire[i] != null ? repertoire[i].attackIcon : null;
                h.color = hintOn;
                h.transform.DOKill(true);
                h.transform.localScale = Vector3.one;
                ((RectTransform)h.transform).anchoredPosition = new Vector2(-total * 0.5f + hintSize * 0.5f + i * (hintSize + spacing), 0f);
            }

            for (int i = 0; i < _symbols.Count; i++) _symbols[i].enabled = false;
            _symbolsRow.DOKill(true);
            _symbolsBg.color = Color.white;
            _timer.fillAmount = 1f;

            _fade?.Kill();
            _fade = DOTween.Sequence().SetUpdate(true).Append(_group.DOFade(1f, fadeIn));
        }

        private void HandleInput(IReadOnlyList<ComboButton> typed, IReadOnlyList<bool> possible)
        {
            int i = typed.Count - 1;
            if (i >= 0 && i < _symbols.Count)
            {
                var s = _symbols[i];
                s.sprite = InputGlyphService.GetSprite(ComboButtonGlyphs.GlyphName(typed[i]));
                s.enabled = s.sprite != null;
                s.transform.DOKill(true);
                s.transform.localScale = Vector3.one * 1.3f;
                s.transform.DOScale(1f, 0.15f).SetUpdate(true);
            }
            for (int k = 0; k < possible.Count && k < _hints.Count; k++)
                _hints[k].DOColor(possible[k] ? hintOn : hintOff, 0.12f).SetUpdate(true);
        }

        private void HandleClosed(ComboCastController.Result result, MagicSpellSO spell)
        {
            if (result == ComboCastController.Result.Cast && _repertoire != null)
            {
                for (int k = 0; k < _repertoire.Count && k < _hints.Count; k++)
                {
                    if (_repertoire[k] != spell) continue;
                    _hints[k].transform.DOKill(true);
                    _hints[k].transform.DOPunchScale(Vector3.one * 0.35f, 0.3f, 6, 0.6f).SetUpdate(true);
                }
            }
            else if (result == ComboCastController.Result.Fizzled || result == ComboCastController.Result.Interrupted)
            {
                _symbolsBg.color = failColor;
                _symbolsRow.DOKill(true);
                _symbolsRow.DOShakeAnchorPos(0.3f, new Vector2(18f, 0f), 20, 0f).SetUpdate(true);
            }

            _fade?.Kill();
            float hold = result == ComboCastController.Result.Cancelled ? 0f : resultHold;
            _fade = DOTween.Sequence().SetUpdate(true).AppendInterval(hold).Append(_group.DOFade(0f, fadeOut));
        }

    }
}
