using UnityEngine;
using TMPro;
using DG.Tweening;
using Core.InputGlyphs;
using Game.NPC;
using System.Collections.Generic;

namespace Sendero.UI
{
    /// <summary>
    /// Barra de la carga de equipo en el panel de estado (INC-491): tres tramos y una estrella.
    /// Cada tramo lleno permite un dúo (LT/RT); con los tres llenos la estrella se enciende y se
    /// puede lanzar el trío (LT+RT). Sin compañeros en el grupo se atenúa (no hay con quién).
    /// Lee <see cref="DuoSpecialAttackSystem.TeamGauge"/> del grupo del jugador.
    /// </summary>
    public class TeamGaugeHUD : MonoBehaviour
    {
        [Tooltip("Relleno de cada tramo (Image Filled horizontal), de izquierda a derecha.")]
        [SerializeField] private UnityEngine.UI.Image[] segmentFills = new UnityEngine.UI.Image[3];
        [Tooltip("Estrella del trío.")]
        [SerializeField] private UnityEngine.UI.Image star;
        [Tooltip("Para atenuar la barra sin compañeros.")]
        [SerializeField] private CanvasGroup group;

        [Header("Colores")]
        [SerializeField] private Color fillingColor = new Color(0.95f, 0.76f, 0.31f, 0.75f);
        [SerializeField] private Color fullColor = new Color(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color starOffColor = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] private Color starOnColor = new Color(1f, 0.85f, 0.35f, 1f);

        [Header("Atenuado sin compañeros")]
        [SerializeField, Range(0f, 1f)] private float soloAlpha = 0.35f;

        private DuoSpecialAttackSystem _team;
        private SpecialChargeMeter _gauge;
        private int _fullSegments = -1;
        private bool _starOn;
        private float _nextBindAttempt;
        private TextMeshProUGUI _message;
        private Sequence _messageSeq;
        private TextMeshProUGUI _readyText;
        private GameObject _ready;
        private SpecialAttackSO _announcedAttack;
        private float _nextReadyCheck;

        private void OnEnable()
        {
            PlayerParty.OnPartyChanged += OnPartyChanged;
            TryBind();
            RefreshSolo();
        }

        private void OnDisable()
        {
            PlayerParty.OnPartyChanged -= OnPartyChanged;
            Unbind();
            _messageSeq?.Kill();
            if (_message != null) _message.gameObject.SetActive(false);
            if (_ready != null) _ready.SetActive(false);
            _announcedAttack = null;
            if (star != null) star.transform.DOKill(true);
        }

        private void Update()
        {
            if (_gauge == null && Time.unscaledTime >= _nextBindAttempt)
            {
                _nextBindAttempt = Time.unscaledTime + 1f;
                TryBind();
                RefreshSolo();
            }
            if (Time.unscaledTime < _nextReadyCheck) return;
            _nextReadyCheck = Time.unscaledTime + 0.1f;
            RefreshReady();
        }

        private void RefreshReady()
        {
            SpecialAttackSO attack = null;
            bool ready = _team != null && _team.TryGetAvailableAttack(out attack);
            if (ready && _ready == null) CreateReadyHint();
            if (_ready == null) return;
            if (_ready.activeSelf != ready) _ready.SetActive(ready);
            if (ready && _announcedAttack != attack) _readyText.text = attack.displayName;
            _announcedAttack = ready ? attack : null;
        }

        private void CreateReadyHint()
        {
            _ready = new GameObject("Ataque de equipo disponible", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            _ready.layer = gameObject.layer;
            var rt = (RectTransform)_ready.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(35f, 0f);
            rt.sizeDelta = new Vector2(620f, 100f);
            var background = _ready.GetComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.07f, 0.04f, 0.14f, 0.9f);
            background.raycastTarget = false;
            CreateGlyph(InputGlyphNames.TriggerLeft, 8f);
            CreateGlyph(InputGlyphNames.TriggerRight, 94f);
            var plus = new GameObject("Más", typeof(RectTransform), typeof(TextMeshProUGUI));
            plus.layer = gameObject.layer;
            var plusRect = (RectTransform)plus.transform;
            plusRect.SetParent(rt, false);
            plusRect.anchorMin = plusRect.anchorMax = new Vector2(0f, 0.5f);
            plusRect.pivot = new Vector2(0f, 0.5f);
            plusRect.anchoredPosition = new Vector2(66f, 0f);
            plusRect.sizeDelta = new Vector2(28f, 70f);
            var plusText = plus.GetComponent<TextMeshProUGUI>();
            plusText.text = "+";
            plusText.fontSize = 42f;
            plusText.alignment = TextAlignmentOptions.Center;
            plusText.color = fullColor;
            plusText.raycastTarget = false;
            var label = new GameObject("Nombre", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = gameObject.layer;
            var rect = (RectTransform)label.transform;
            rect.SetParent(rt, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(166f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);
            _readyText = label.GetComponent<TextMeshProUGUI>();
            var existing = GetComponentInParent<Canvas>()?.GetComponentInChildren<TMP_Text>(true);
            if (existing != null) _readyText.font = existing.font;
            _readyText.fontSize = 44f;
            _readyText.alignment = TextAlignmentOptions.MidlineLeft;
            _readyText.color = fullColor;
            _readyText.raycastTarget = false;
        }

        private void CreateGlyph(string glyph, float x)
        {
            var go = new GameObject(glyph, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_ready.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(70f, 70f);
            rt.anchoredPosition = new Vector2(x, 0f);
            go.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            go.AddComponent<InputGlyphIcon>().SetGlyphName(glyph);
        }

        private void TryBind()
        {
            if (_gauge != null) return;
            if (!PlayerService.TryGetComponent(out _team, allowSceneLookup: false) || _team == null) return;
            _gauge = _team.TeamGauge;
            if (_gauge == null) return;
            _team.OnDenied += ShowMessage;
            _gauge.OnChargeUpdated += OnChargeUpdated;
            _fullSegments = -1;
            OnChargeUpdated(_gauge.Normalized);
        }

        private void Unbind()
        {
            if (_gauge != null) _gauge.OnChargeUpdated -= OnChargeUpdated;
            if (_team != null) _team.OnDenied -= ShowMessage;
            _gauge = null;
            _team = null;
        }

        /// Aviso breve encima de la barra cuando LT+RT no puede lanzar nada («falta carga», «nadie cerca»).
        private void ShowMessage(string text)
        {
            if (_message == null)
            {
                var go = new GameObject("Message", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(900f, 70f);
                rt.anchoredPosition = new Vector2(0f, 8f);
                _message = go.AddComponent<TextMeshProUGUI>();
                var canvas = GetComponentInParent<Canvas>();
                var anyText = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
                if (anyText != null && anyText != _message) _message.font = anyText.font;
                _message.fontSize = 44f;
                _message.alignment = TextAlignmentOptions.Center;
                _message.color = new Color(1f, 0.92f, 0.7f, 1f);
                _message.outlineWidth = 0.2f;
                _message.raycastTarget = false;
            }
            _message.text = text;
            _message.alpha = 1f;
            _message.gameObject.SetActive(true);
            _messageSeq?.Kill();
            _messageSeq = DOTween.Sequence().SetUpdate(true).AppendInterval(1.3f)
                .Append(_message.DOFade(0f, 0.35f))
                .OnComplete(() => { if (_message != null) _message.gameObject.SetActive(false); });
        }

        private void OnPartyChanged(IReadOnlyList<NPCPartyMember> _) => RefreshSolo();

        private void RefreshSolo()
        {
            if (group == null) return;
            bool hasCompanion = _team != null && _team.HasAnyCompanion;
            group.alpha = hasCompanion ? 1f : soloAlpha;
        }

        private void OnChargeUpdated(float _)
        {
            if (_gauge == null) return;
            float charge = _gauge.CurrentCharge;
            int full = 0;
            for (int i = 0; i < segmentFills.Length; i++)
            {
                var fill = segmentFills[i];
                float amount = Mathf.Clamp01(charge - i);
                if (amount >= 0.999f) full++;
                if (fill == null) continue;
                fill.fillAmount = amount;
                fill.color = amount >= 0.999f ? fullColor : fillingColor;
            }

            if (_fullSegments >= 0 && full > _fullSegments)
            {
                var t = segmentFills[Mathf.Clamp(full - 1, 0, segmentFills.Length - 1)];
                if (t != null)
                {
                    t.transform.DOKill(true);
                    t.transform.DOPunchScale(new Vector3(0.12f, 0.35f, 0f), 0.3f, 6, 0.6f).SetUpdate(true);
                }
            }
            _fullSegments = full;

            bool starOn = _gauge.IsFullyCharged;
            if (star != null && starOn != _starOn)
            {
                _starOn = starOn;
                star.color = starOn ? starOnColor : starOffColor;
                var st = star.transform;
                st.DOKill(true);
                st.localScale = Vector3.one;
                if (starOn)
                    st.DOScale(1.18f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
        }
    }
}
