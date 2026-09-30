using UnityEngine;
using DG.Tweening;

namespace Sendero.UI
{
    /// <summary>
    /// Cruz de combate del HUD (INC-485): un círculo por botón del mando, en su misma posición.
    ///  - X: hechizo básico activo en grande, los otros básicos equipados en pequeño en el orden en
    ///    que salen con LB, y tres puntos con el golpe de la serie que toca. Se tiñe si falta maná.
    ///  - Y: combo. Icono fijo (los combos no se equipan) y anillo de enfriamiento del combo.
    ///  - B: defensa. Icono fijo de escudo.
    /// Lee <see cref="MagicCaster"/> y <see cref="ManaPool"/> del jugador; se reengancha al aplicar
    /// preset y al cambiar de personaje activo. La visibilidad la lleva el CanvasGroup del canvas
    /// (PlayerHUDV2.HideHUD/ShowHUD).
    /// </summary>
    public class CombatButtonsHUD : MonoBehaviour
    {
        [Header("X · hechizo activo")]
        [SerializeField] private UnityEngine.UI.Image activeIcon;
        [Tooltip("Iconos pequeños de los otros básicos, en el orden en que salen con LB.")]
        [SerializeField] private UnityEngine.UI.Image[] rotationIcons = new UnityEngine.UI.Image[3];
        [Tooltip("Raíz de cada icono pequeño (el aro); se oculta si no hay hechizo en ese hueco.")]
        [SerializeField] private GameObject[] rotationSlots = new GameObject[3];
        [Tooltip("Puntos de la serie de la X: cuántos golpes de la serie de tres llevas (el tercero es el fuerte). Solo se ven con una serie en curso.")]
        [SerializeField] private UnityEngine.UI.Image[] seriesDots = new UnityEngine.UI.Image[3];
        [Tooltip("Etiqueta del botón LB; se oculta si solo hay un básico equipado.")]
        [SerializeField] private GameObject rotateHint;

        [Header("Y · combo")]
        [Tooltip("Anillo de enfriamiento del combo (Image Filled radial). 0 = disponible.")]
        [SerializeField] private UnityEngine.UI.Image comboCooldownFill;

        [Header("B · defensa")]
        [Tooltip("Icono del escudo. Si está vacío se busca en ButtonB/Shield.")]
        [SerializeField] private UnityEngine.UI.Image defenseIcon;
        [SerializeField] private Color defendingColor = new Color(0.75f, 0.95f, 1f, 1f);
        [SerializeField] private Color idleDefenseColor = new Color(1f, 1f, 1f, 0.85f);

        [Header("Colores")]
        [SerializeField] private Color availableColor = Color.white;
        [SerializeField] private Color noManaColor = new Color(1f, 0.35f, 0.35f, 0.85f);
        [SerializeField] private Color dotOnColor = new Color(1f, 0.85f, 0.95f, 1f);
        [SerializeField] private Color dotOffColor = new Color(1f, 1f, 1f, 0.25f);

        [Header("Animación")]
        [SerializeField] private float rotatePunch = 0.15f;
        [SerializeField] private float rotatePunchDuration = 0.25f;

        private MagicCaster _caster;
        private PlayerShieldController _shield;
        private ComboCastController _combo;
        private float _shownCooldown = -1f;
        private int _shownDefending = -1;
        private ManaPool _mana;
        private MagicSpellSO _shownActive;
        private int _shownStep = -1;
        private int _shownNoMana = -1; // -1 = sin pintar, 0 = con maná, 1 = sin maná

        private void Start()
        {
            if (defenseIcon == null)
            {
                var t = transform.Find("ButtonB/Shield");
                if (t != null) defenseIcon = t.GetComponent<UnityEngine.UI.Image>();
            }
            PlayerPresetService.OnPresetApplied += Rebind;
            PartyControlManager.OnActiveCharacterChanged += OnCharacterSwitched;
            Rebind();
            SetComboCooldown(0f);
        }

        private void OnDestroy()
        {
            PlayerPresetService.OnPresetApplied -= Rebind;
            PartyControlManager.OnActiveCharacterChanged -= OnCharacterSwitched;
            Unbind();
            if (activeIcon != null) activeIcon.transform.DOKill(true);
            if (defenseIcon != null) defenseIcon.transform.DOKill(true);
        }

        private void OnCharacterSwitched(int _) => Rebind();

        private void Rebind()
        {
            Unbind();
            var player = PlayerService.Player;
            if (player == null) return;
            _caster = player.GetComponentInChildren<MagicCaster>(true);
            _mana = player.GetComponentInChildren<ManaPool>(true);
            if (_caster != null) _caster.OnLoadoutChanged += OnLoadoutChanged;
            _shield = player.GetComponentInChildren<PlayerShieldController>(true);
            if (_shield != null) _shield.OnCounter += OnCounter;
            _combo = player.GetComponentInChildren<ComboCastController>(true);
            _shownDefending = -1;
            _shownActive = null;
            RefreshLoadout(false);
        }

        private void Unbind()
        {
            if (_caster != null) _caster.OnLoadoutChanged -= OnLoadoutChanged;
            if (_shield != null) _shield.OnCounter -= OnCounter;
            _caster = null;
            _combo = null;
            _shield = null;
            _mana = null;
        }

        private void OnLoadoutChanged() => RefreshLoadout(true);

        private void RefreshLoadout(bool animate)
        {
            MagicSpellSO active = _caster != null ? _caster.ActiveBasic : null;
            bool changed = active != _shownActive;
            _shownActive = active;

            if (activeIcon != null)
            {
                activeIcon.sprite = active != null ? active.attackIcon : null;
                activeIcon.enabled = active != null && active.attackIcon != null;
            }

            int shownMinis = 0;
            for (int k = 0; k < rotationIcons.Length; k++)
            {
                MagicSpellSO spell = null;
                if (_caster != null)
                {
                    var basics = _caster.BasicSpells;
                    if (basics != null && basics.Count > 0)
                        spell = basics[(_caster.ActiveBasicIndex + k + 1) % basics.Count];
                    if (spell == active) spell = null; // con menos de 4 básicos no repetimos el activo
                }
                bool has = spell != null && spell.attackIcon != null;
                if (has) shownMinis++;
                if (rotationIcons[k] != null) rotationIcons[k].sprite = has ? spell.attackIcon : null;
                if (k < rotationSlots.Length && rotationSlots[k] != null) rotationSlots[k].SetActive(has);
            }
            if (rotateHint != null) rotateHint.SetActive(shownMinis > 0);

            _shownStep = -1;
            _shownNoMana = -1; // fuerza repintar el color en el siguiente Update

            if (animate && changed && activeIcon != null && rotatePunch > 0f)
            {
                var t = activeIcon.transform;
                t.DOKill(true);
                t.DOPunchScale(Vector3.one * rotatePunch, rotatePunchDuration, 6, 0.6f).SetUpdate(true);
            }
        }

        private void OnCounter()
        {
            if (defenseIcon == null) return;
            var t = defenseIcon.transform;
            t.DOKill(true);
            t.DOPunchScale(Vector3.one * 0.3f, 0.3f, 8, 0.7f).SetUpdate(true);
        }

        private void Update()
        {
            if (_shield != null && defenseIcon != null)
            {
                int defending = _shield.IsDefending ? 1 : 0;
                if (defending != _shownDefending)
                {
                    _shownDefending = defending;
                    defenseIcon.color = defending == 1 ? defendingColor : idleDefenseColor;
                }
            }

            if (_combo != null)
            {
                float cd = _combo.CooldownNormalized;
                if (!Mathf.Approximately(cd, _shownCooldown)) { _shownCooldown = cd; SetComboCooldown(cd); }
            }

            if (_caster == null) return;

            int step = _caster.NextSeriesStep;
            if (step != _shownStep)
            {
                _shownStep = step;
                // Solo durante una serie en curso: sin serie (o recién rota) no dicen nada útil.
                for (int i = 0; i < seriesDots.Length; i++)
                {
                    if (seriesDots[i] == null) continue;
                    seriesDots[i].enabled = step > 0;
                    seriesDots[i].color = i < step ? dotOnColor : dotOffColor;
                }
            }

            bool noMana = _shownActive != null && _mana != null && _mana.Current < _shownActive.manaCost;
            int state = noMana ? 1 : 0;
            if (state != _shownNoMana)
            {
                _shownNoMana = state;
                if (activeIcon != null) activeIcon.color = noMana ? noManaColor : availableColor;
            }
        }

        /// <summary>Enfriamiento del combo de la Y, de 1 (recién usado) a 0 (disponible).</summary>
        public void SetComboCooldown(float normalized)
        {
            if (comboCooldownFill == null) return;
            comboCooldownFill.fillAmount = Mathf.Clamp01(normalized);
            comboCooldownFill.enabled = normalized > 0f;
        }
    }
}
