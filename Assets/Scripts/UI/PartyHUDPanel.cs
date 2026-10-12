using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Game.NPC;
using Slot = PartyControlManager.CharacterSlot;

namespace Sendero.UI
{
    /// <summary>
    /// Panel del grupo en el HUD: los retratos de busto de Liam, Will y Estela, el nombre del que
    /// va al mando y el aviso de Siguiendo / Libre.
    ///
    /// Carrusel: el personaje al mando va en el centro; a cada lado, el personaje al que se pasaría
    /// con DPad hacia ese lado (<see cref="PartyControlManager.SlotVecino"/>), así lo que se ve
    /// coincide con lo que hace el botón. Quien no está en el grupo no se muestra.
    ///
    /// Este panel decide quién va dónde; cómo se ve y reacciona cada retrato lo hace
    /// <see cref="RetratoDelGrupo"/>, y la vida, las caídas y el trío los da
    /// <see cref="EstadoDelGrupoParaRetratos"/>. Se monta con
    /// <c>El Sendero/UI/Montar retratos del grupo</c>.
    /// </summary>
    public class PartyHUDPanel : MonoBehaviour
    {
        private static readonly Color[] ColoresPorDefecto =
        {
            new Color(0.482f, 0.749f, 0.353f, 1f), // Liam #7BBF5A
            new Color(1f, 0.604f, 0.235f, 1f),     // Will #FF9A3C
            new Color(0.310f, 0.553f, 1f, 1f),     // Estela #4F8DFF
        };

        [Header("Retratos del grupo")]
        [SerializeField] private RetratoDelGrupo retratoDeLiam;
        [SerializeField] private RetratoDelGrupo retratoDeWill;
        [SerializeField] private RetratoDelGrupo retratoDeEstela;
        [SerializeField] private EstadoDelGrupoParaRetratos estado;
        [Tooltip("De dónde salen las imágenes: un JuegoDeRetratos u otro componente o asset que implemente IFuenteDeRetrato.")]
        [SerializeField] private Object fuenteDeRetratos;
        [Tooltip("Imágenes que se usan si la fuente principal no tiene la que toca.")]
        [SerializeField] private JuegoDeRetratos retratosDeRespaldo;
        [Tooltip("Distancia horizontal del centro a los retratos de los lados (unidades del contenedor).")]
        [SerializeField] private float separacionLateral = 290f;
        [Tooltip("Por debajo de esta vida (0-1), el retrato se ve cansado.")]
        [SerializeField, Range(0f, 1f)] private float vidaBaja = 0.25f;

        [Header("Nombre y luz del borde")]
        [SerializeField] private TMP_Text nombreDelActivo;
        [SerializeField] private Graphic lineaDeLuz;

        [Header("Aviso del trío")]
        [SerializeField] private CanvasGroup avisoDelTrio;
        [SerializeField] private float avisoVisible = 1.6f;

        [Header("Panel de estado de seguimiento")]
        [SerializeField] private CanvasGroup followStatusPanel;
        [SerializeField] private TextMeshProUGUI followStatusText;
        [SerializeField] private string followingTextKey = "HUD_FOLLOW_STATUS_FOLLOWING";
        [SerializeField] private string freeTextKey = "HUD_FOLLOW_STATUS_FREE";
        [SerializeField] private float panelFadeInDuration = 0.25f;
        [SerializeField] private float panelVisibleDuration = 1f;
        [SerializeField] private float panelFadeOutDuration = 0.3f;
        [SerializeField] private float panelSlideOffset = 40f;

        [Header("Nombres para identificar compañeros en el party")]
        [SerializeField] private string liamDisplayName = "Liam";
        [SerializeField] private string estelaDisplayName = "Estela";

        private RetratoDelGrupo[] _retratos; // 0=Liam, 1=Will, 2=Estela
        private readonly bool[] _presente = new bool[3];
        private readonly Color[] _colores = new Color[3];
        private readonly string[] _nombres = { "LIAM", "WILL", "ESTELA" };
        private int _activoMostrado = -1;
        private readonly bool[] _saludMostrada = new bool[3];
        private readonly bool[] _cansadoMostrado = new bool[3];
        private readonly bool[] _caidoMostrado = new bool[3];
        private bool _trioMostrado;
        private Sequence _panelSequence;
        private Sequence _avisoSequence;
        private RectTransform _followStatusRect;
        private Vector2 _panelStartPos;
        private bool _configurado;

        private void Awake()
        {
            _retratos = new[] { retratoDeLiam, retratoDeWill, retratoDeEstela };
            if (avisoDelTrio != null) { avisoDelTrio.alpha = 0f; avisoDelTrio.gameObject.SetActive(false); }
        }

        private void OnEnable()
        {
            PlayerParty.OnPartyChanged += OnPartyChanged;
            PartyControlManager.OnActiveCharacterChanged += OnActiveCharacterChanged;
            PartyControlManager.OnFollowModeChanged += OnFollowModeChanged;
            if (estado != null)
            {
                estado.Golpeado += AlGolpear;
                estado.SaludCambiada += AlCambiarSalud;
                estado.TrioCambiado += AlCambiarTrio;
            }
            // El HUD es persistente (Start.unity): al reactivarse refresca el estado del grupo.
            Refresh(false);
        }

        private void OnDisable()
        {
            PlayerParty.OnPartyChanged -= OnPartyChanged;
            PartyControlManager.OnActiveCharacterChanged -= OnActiveCharacterChanged;
            PartyControlManager.OnFollowModeChanged -= OnFollowModeChanged;
            if (estado != null)
            {
                estado.Golpeado -= AlGolpear;
                estado.SaludCambiada -= AlCambiarSalud;
                estado.TrioCambiado -= AlCambiarTrio;
            }
            _panelSequence?.Kill();
            _avisoSequence?.Kill();
            if (avisoDelTrio != null) { avisoDelTrio.alpha = 0f; avisoDelTrio.gameObject.SetActive(false); }
        }

        private void Start()
        {
            if (followStatusPanel != null)
            {
                _followStatusRect = followStatusPanel.GetComponent<RectTransform>();
                _panelStartPos = _followStatusRect != null ? _followStatusRect.anchoredPosition : Vector2.zero;
                followStatusPanel.alpha = 0f;
                followStatusPanel.gameObject.SetActive(false);
            }
            Refresh(false);
        }

        // ─── Configuración ─────────────────────────────────────────────────────

        private void Configurar()
        {
            if (_configurado) return;
            _configurado = true;
            var fuente = fuenteDeRetratos as IFuenteDeRetrato;
            if (fuente == null && fuenteDeRetratos is GameObject go) fuente = go.GetComponent<IFuenteDeRetrato>();
            for (int i = 0; i < _retratos.Length; i++)
            {
                var ficha = FichaDePersonaje.Buscar((Slot)i);
                _colores[i] = ficha != null && ficha.ColorDelPersonaje.a > 0f ? ficha.ColorDelPersonaje : ColoresPorDefecto[i];
                if (_retratos[i] != null) _retratos[i].Configurar((Slot)i, _colores[i], fuente, retratosDeRespaldo);
            }
        }

        // ─── Callbacks de eventos ──────────────────────────────────────────────

        private void OnPartyChanged(IReadOnlyList<NPCPartyMember> members) => Recolocar(true);
        private void OnActiveCharacterChanged(int newIndex) => Recolocar(true);

        private void OnFollowModeChanged(bool isFollowing)
        {
            UpdateFollowText(isFollowing);
            ShowFollowStatusPanel();
        }

        private void AlGolpear(Slot personaje)
        {
            var r = Retrato((int)personaje);
            if (r != null) r.Golpe();
        }

        private void AlCambiarSalud(Slot personaje) => PonerSalud((int)personaje);

        private void AlCambiarTrio(bool listo) => PonerTrio(true);

        // ─── Refresco ──────────────────────────────────────────────────────────

        private void Refresh(bool animar)
        {
            if (_retratos == null) return;
            Configurar();
            bool following = PartyControlManager.Instance == null || PartyControlManager.Instance.IsPartyFollowing;
            Recolocar(animar);
            for (int i = 0; i < _retratos.Length; i++) PonerSalud(i);
            _trioMostrado = estado != null && estado.TrioListo && Presentes() == 3;
            PonerTrio(false);
            UpdateFollowText(following);
        }

        private void Recolocar(bool animar)
        {
            if (_retratos == null) return;
            Configurar();
            var pcm = PartyControlManager.Instance;
            int activo = pcm != null ? pcm.ActiveIndex : (int)Slot.Will;
            _presente[(int)Slot.Liam] = IsInParty(liamDisplayName);
            _presente[(int)Slot.Will] = true;
            _presente[(int)Slot.Estela] = IsInParty(estelaDisplayName);

            int izquierda = pcm != null ? pcm.SlotVecino(activo, -1) : -1;
            int derecha = pcm != null ? pcm.SlotVecino(activo, 1) : -1;
            if (izquierda >= 0 && izquierda == derecha)
            {
                // Con dos en el grupo los dos lados llevan al mismo: va al lado de su hueco.
                if (izquierda < activo) derecha = -1; else izquierda = -1;
            }
            for (int i = 0; i < 3; i++)
            {
                if (i == activo || i == izquierda || i == derecha || !_presente[i]) continue;
                if (izquierda < 0) izquierda = i; else if (derecha < 0) derecha = i;
            }

            for (int i = 0; i < 3; i++)
            {
                var r = Retrato(i);
                if (r == null) continue;
                if (!_presente[i]) { r.Colocar(PosicionDeRetrato.Oculto, 0f, false); continue; }
                if (i == activo) r.Colocar(PosicionDeRetrato.Centro, 0f, animar);
                else if (i == izquierda) r.Colocar(PosicionDeRetrato.Izquierda, -separacionLateral, animar);
                else if (i == derecha) r.Colocar(PosicionDeRetrato.Derecha, separacionLateral, animar);
                else r.Colocar(PosicionDeRetrato.Oculto, 0f, false);
            }
            var central = Retrato(activo);
            if (central != null) central.transform.SetAsLastSibling();

            if (activo != _activoMostrado)
            {
                _activoMostrado = activo;
                if (nombreDelActivo != null && activo >= 0 && activo < _nombres.Length) nombreDelActivo.text = _nombres[activo];
                if (lineaDeLuz != null && activo >= 0 && activo < _colores.Length)
                {
                    var c = _colores[activo];
                    lineaDeLuz.DOKill();
                    lineaDeLuz.DOColor(new Color(c.r, c.g, c.b, lineaDeLuz.color.a), animar ? 0.35f : 0f).SetUpdate(true);
                }
            }
            PonerTrio(false);
        }

        private void PonerSalud(int slot)
        {
            var r = Retrato(slot);
            if (r == null || estado == null) return;
            var s = (Slot)slot;
            bool caido = estado.Caido(s);
            bool cansado = !caido && estado.Vida(s) <= vidaBaja;
            r.PonerBarras(estado.Vida(s), estado.Mana(s), cansado);
            // El maná cambia a menudo (se regenera): el aspecto del retrato solo si cambia la vida baja o la caída.
            if (_saludMostrada[slot] && _cansadoMostrado[slot] == cansado && _caidoMostrado[slot] == caido) return;
            _saludMostrada[slot] = true;
            _cansadoMostrado[slot] = cansado;
            _caidoMostrado[slot] = caido;
            r.PonerSalud(cansado, caido);
        }

        /// Halo dorado en todos con el trío listo (y los tres en el grupo); el aviso, solo al llenarse.
        private void PonerTrio(bool avisar)
        {
            bool listo = estado != null && estado.TrioListo && Presentes() == 3;
            for (int i = 0; i < 3; i++)
            {
                var r = Retrato(i);
                if (r != null) r.PonerTrioListo(listo);
            }
            if (listo && !_trioMostrado && avisar) MostrarAvisoDelTrio();
            _trioMostrado = listo;
        }

        private void MostrarAvisoDelTrio()
        {
            if (avisoDelTrio == null) return;
            _avisoSequence?.Kill();
            var t = avisoDelTrio.transform;
            avisoDelTrio.gameObject.SetActive(true);
            avisoDelTrio.alpha = 0f;
            t.localScale = Vector3.one * 0.8f;
            _avisoSequence = DOTween.Sequence().SetUpdate(true)
                .Append(avisoDelTrio.DOFade(1f, 0.2f))
                .Join(t.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
                .AppendInterval(avisoVisible)
                .Append(avisoDelTrio.DOFade(0f, 0.3f))
                .OnComplete(() =>
                {
                    if (avisoDelTrio != null) avisoDelTrio.gameObject.SetActive(false);
                    _avisoSequence = null;
                });
        }

        private int Presentes()
        {
            int n = 0;
            for (int i = 0; i < 3; i++) if (_presente[i]) n++;
            return n;
        }

        private RetratoDelGrupo Retrato(int slot) =>
            _retratos != null && slot >= 0 && slot < _retratos.Length ? _retratos[slot] : null;

        // ─── Siguiendo / Libre ─────────────────────────────────────────────────

        private void ShowFollowStatusPanel()
        {
            if (followStatusPanel == null) return;

            _panelSequence?.Kill();

            followStatusPanel.gameObject.SetActive(true);
            followStatusPanel.alpha = 0f;

            if (_followStatusRect != null)
                _followStatusRect.anchoredPosition = _panelStartPos + Vector2.up * panelSlideOffset;

            _panelSequence = DOTween.Sequence().SetUpdate(true);
            _panelSequence.Append(followStatusPanel.DOFade(1f, panelFadeInDuration).SetEase(Ease.OutQuad));
            if (_followStatusRect != null)
                _panelSequence.Join(_followStatusRect.DOAnchorPos(_panelStartPos, panelFadeInDuration).SetEase(Ease.OutBack));
            _panelSequence.AppendInterval(panelVisibleDuration);
            _panelSequence.Append(followStatusPanel.DOFade(0f, panelFadeOutDuration).SetEase(Ease.InQuad));
            if (_followStatusRect != null)
                _panelSequence.Join(_followStatusRect.DOAnchorPos(_panelStartPos + Vector2.up * panelSlideOffset, panelFadeOutDuration).SetEase(Ease.InQuad));
            _panelSequence.OnComplete(() =>
            {
                followStatusPanel.gameObject.SetActive(false);
                _panelSequence = null;
            });
        }

        private void UpdateFollowText(bool isFollowing)
        {
            if (followStatusText == null) return;
            string key = isFollowing ? followingTextKey : freeTextKey;
            string fallback = isFollowing ? "Siguiendo" : "Libre";
            followStatusText.text = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get(key, fallback)
                : fallback;
        }

        private bool IsInParty(string displayName)
        {
            return PlayerParty.Instance?.GetMemberByName(displayName) != null;
        }

#if UNITY_EDITOR
        // Para ver las reacciones en Play sin tener que provocarlas (menú ⋮ del componente).
        private int ActivoActual => PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveIndex : (int)Slot.Will;

        [ContextMenu("Retratos: probar golpe al que va al mando")]
        private void ProbarGolpe()
        {
            var r = Retrato(ActivoActual);
            if (r != null) r.Golpe();
        }

        [ContextMenu("Retratos: probar vida baja del que va al mando")]
        private void ProbarVidaBaja()
        {
            var r = Retrato(ActivoActual);
            if (r != null) r.PonerSalud(true, false);
        }

        [ContextMenu("Retratos: probar un compañero caído")]
        private void ProbarCaido()
        {
            for (int i = 0; i < 3; i++)
            {
                var r = Retrato(i);
                if (i == ActivoActual || r == null || !_presente[i]) continue;
                r.PonerSalud(false, true);
                return;
            }
        }

        [ContextMenu("Retratos: probar trío listo")]
        private void ProbarTrio()
        {
            for (int i = 0; i < 3; i++)
            {
                var r = Retrato(i);
                if (r != null) r.PonerTrioListo(true);
            }
            MostrarAvisoDelTrio();
        }

        [ContextMenu("Retratos: volver al estado real")]
        private void VolverAlEstadoReal()
        {
            System.Array.Clear(_saludMostrada, 0, _saludMostrada.Length);
            Refresh(false);
        }
#endif
    }
}
