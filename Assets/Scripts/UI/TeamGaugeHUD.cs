using UnityEngine;
using TMPro;
using DG.Tweening;
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
            if (star != null) star.transform.DOKill(true);
        }

        private void Update()
        {
            if (_gauge != null || Time.unscaledTime < _nextBindAttempt) return;
            _nextBindAttempt = Time.unscaledTime + 1f;
            TryBind();
            RefreshSolo();
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
