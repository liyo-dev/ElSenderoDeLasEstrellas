using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// El grimorio como libro (INC-506). Se abre desde la pestaña Hechizos del menú de Start (botón
/// «Grimorio» o Select/View; M en teclado, la tecla que da InputGlyphNames.Select) y enseña una
/// página por hechizo:
///  - Izquierda: el icono en su medallón, el nombre, de quién es y dos etiquetas (tipo y elemento).
///  - Derecha: cómo se lanza, con los iconos de los botones del mando activo en dos pasos; su
///    frase; los datos en casillas (daño, cura, escudo, duración, maná); lo que hace de especial;
///    y una etiqueta con si está equipado y en qué ranura.
/// Los hechizos que aún no se conocen salen como página sellada, con la silueta del icono, para
/// que se note lo que falta por descubrir.
///
/// Tiene una sección por personaje (Will, y Estela y Liam cuando están en el grupo), marcada con
/// su cinta. ◄ ► (cruceta, stick, LB/RB o A/D) pasan página; Y (Tab) salta al siguiente
/// personaje; B, Start o Esc cierran. La jerarquía la monta GrimorioLibroBuilder.
/// </summary>
public class GrimorioLibroUI : MonoBehaviour
{
    [Header("Raíz")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform book;

    [Header("Página izquierda")]
    [SerializeField] private RectTransform leftPage;
    [SerializeField] private Image icon;
    [SerializeField] private Image seal;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI ownerText;
    [Tooltip("Etiqueta con el tipo de hechizo (básico, combo, levitación).")]
    [SerializeField] private Image kindTag;
    [SerializeField] private TextMeshProUGUI kindTagText;
    [Tooltip("Etiqueta con el elemento, en su color.")]
    [SerializeField] private Image elementTag;
    [SerializeField] private TextMeshProUGUI elementTagText;

    [Header("Página derecha")]
    [SerializeField] private RectTransform rightPage;
    [Tooltip("Título de la sección de arriba: «Cómo se lanza» o «Página sin descubrir».")]
    [SerializeField] private TextMeshProUGUI castTitleText;
    [Tooltip("Fila con los dos pasos para lanzarlo.")]
    [SerializeField] private GameObject castRow;
    [Tooltip("Iconos de botón de cada paso (texto TMP con <sprite>).")]
    [SerializeField] private TextMeshProUGUI[] stepIcons = new TextMeshProUGUI[2];
    [Tooltip("Qué se hace en cada paso.")]
    [SerializeField] private TextMeshProUGUI[] stepCaptions = new TextMeshProUGUI[2];
    [Tooltip("Flecha entre los dos pasos, solo cuando van uno detrás de otro.")]
    [SerializeField] private GameObject castArrow;
    [SerializeField] private TextMeshProUGUI loreText;
    [Tooltip("Fila de casillas con los datos del hechizo.")]
    [SerializeField] private GameObject statRow;
    [SerializeField] private TextMeshProUGUI[] statValues = new TextMeshProUGUI[5];
    [SerializeField] private TextMeshProUGUI[] statLabels = new TextMeshProUGUI[5];
    [SerializeField] private TextMeshProUGUI effectText;
    [Tooltip("Etiqueta de abajo: equipado y en qué ranura, sin equipar, o que no ocupa ranura.")]
    [SerializeField] private Image equippedTag;
    [SerializeField] private TextMeshProUGUI equippedTagText;

    [Header("Libro")]
    [SerializeField] private TextMeshProUGUI pageCounterText;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Image[] ribbons = new Image[3];   // pestañas de Will, Estela y Liam
    [SerializeField] private RectTransform turningLeaf;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    [Header("Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip pageClip;

    [Header("Colores")]
    [SerializeField] private Color inkColor = new Color(0.24f, 0.14f, 0.08f);
    [SerializeField] private Color lockedIconColor = new Color(0.1f, 0.06f, 0.04f, 0.35f);
    [SerializeField] private Color damageColor = new Color(0.6f, 0.18f, 0.1f);
    [SerializeField] private Color healColor = new Color(0.18f, 0.48f, 0.22f);
    [SerializeField] private Color shieldColor = new Color(0.17f, 0.43f, 0.52f);
    [SerializeField] private Color manaColor = new Color(0.18f, 0.36f, 0.66f);
    [SerializeField] private Color equippedColor = new Color(0.25f, 0.5f, 0.22f);
    [SerializeField] private Color notEquippedColor = new Color(0.5f, 0.42f, 0.34f);
    [SerializeField] private Color comboTagColor = new Color(0.62f, 0.44f, 0.08f);

    private struct Pagina
    {
        public MagicSpellSO spell;
        public bool known;
        public Slot owner;
    }

    private readonly List<Pagina> _pages = new List<Pagina>();
    private int _index;
    private bool _open;
    private bool _turning;
    private float _openedAt;
    private float _nextRepeat;
    private int _lastDir;
    private Vector2[] _ribbonBase;

    public bool IsOpen => _open;
    public int ClosedFrame { get; private set; } = -1;

    private static readonly Slot[] Order = { Slot.Will, Slot.Estela, Slot.Liam };

    void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        _ribbonBase = new Vector2[ribbons.Length];
        for (int i = 0; i < ribbons.Length; i++)
            if (ribbons[i] != null) _ribbonBase[i] = ribbons[i].rectTransform.anchoredPosition;
        if (prevButton) prevButton.onClick.AddListener(() => Turn(-1));
        if (nextButton) nextButton.onClick.AddListener(() => Turn(1));
        if (closeButton) closeButton.onClick.AddListener(Close);
        if (turningLeaf) turningLeaf.gameObject.SetActive(false);
        foreach (var t in stepIcons) Core.InputGlyphs.InputGlyphService.UsarIconos(t);
        Core.InputGlyphs.InputGlyphService.UsarIconos(hintText);
    }

    void OnEnable() => Core.InputGlyphs.InputGlyphService.FamilyChanged += OnFamilyChanged;
    void OnDisable() => Core.InputGlyphs.InputGlyphService.FamilyChanged -= OnFamilyChanged;

    // Los iconos de botón cambian con el mando: se rehace la página y la barra de pistas.
    private void OnFamilyChanged(Core.InputGlyphs.InputGlyphDeviceFamily _)
    {
        if (!_open) return;
        Show(_index);
        UpdateHint();
    }

    // ── Abrir y cerrar ────────────────────────────────────────────────────

    /// <summary>Abre el libro por la página de ese hechizo (o por la primera del personaje al mando).</summary>
    public void Open(SpellId startAt = SpellId.None)
    {
        BuildPages();
        if (_pages.Count == 0) return;

        _index = FindStart(startAt);
        _open = true;
        _openedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        group.DOKill();
        group.alpha = 0f;
        group.blocksRaycasts = true;
        group.interactable = true;
        group.DOFade(1f, 0.2f).SetUpdate(true);
        if (book)
        {
            book.DOKill();
            book.localScale = Vector3.one * 0.92f;
            book.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        Show(_index);
        UpdateHint();
        Play(openClip);
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        ClosedFrame = Time.frameCount;
        group.DOKill();
        group.blocksRaycasts = false;
        group.DOFade(0f, 0.15f).SetUpdate(true).OnComplete(() => gameObject.SetActive(false));
        Play(pageClip);
    }

    /// <summary>Select/View del mando o M (InputGlyphNames.Select): abre el grimorio desde la pestaña de hechizos.</summary>
    public static bool OpenPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null && gp.selectButton.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame) return true;
#endif
        return false;
    }

    // ── Entrada ───────────────────────────────────────────────────────────

    void Update()
    {
        if (!_open) return;
        if (Time.unscaledTime - _openedAt < 0.15f) return;

#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        var kb = Keyboard.current;

        bool close = (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
                  || (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame))
                  || (gp != null && gp.selectButton.wasPressedThisFrame);
        if (close) { Close(); return; }

        bool section = (gp != null && gp.buttonNorth.wasPressedThisFrame) || (kb != null && kb.tabKey.wasPressedThisFrame);
        if (section) { NextSection(); return; }

        int dir = 0;
        bool pressedNow = false;
        if (gp != null)
        {
            if (gp.leftShoulder.wasPressedThisFrame || gp.dpad.left.wasPressedThisFrame) { dir = -1; pressedNow = true; }
            else if (gp.rightShoulder.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame) { dir = 1; pressedNow = true; }
            else
            {
                float x = gp.leftStick.x.ReadValue();
                if (gp.dpad.left.isPressed || x < -0.6f) dir = -1;
                else if (gp.dpad.right.isPressed || x > 0.6f) dir = 1;
            }
        }
        if (dir == 0 && kb != null)
        {
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) { dir = -1; pressedNow = true; }
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) { dir = 1; pressedNow = true; }
            else if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir = -1;
            else if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir = 1;
        }

        if (dir == 0) { _lastDir = 0; return; }
        if (pressedNow || dir != _lastDir) { _nextRepeat = Time.unscaledTime + 0.4f; _lastDir = dir; Turn(dir); }
        else if (Time.unscaledTime >= _nextRepeat) { _nextRepeat = Time.unscaledTime + 0.18f; Turn(dir); }
#endif
    }

    // ── Páginas ───────────────────────────────────────────────────────────

    private void BuildPages()
    {
        _pages.Clear();
        var lib = GrimorioDelPersonaje.Biblioteca;
        foreach (var slot in Order)
        {
            if (!CharacterAvailable(slot)) continue;

            var known = new List<MagicSpellSO>();
            known.AddRange(GrimorioDelPersonaje.BasicosDisponibles(slot));
            foreach (var c in GrimorioDelPersonaje.CombosDisponibles(slot)) if (!known.Contains(c)) known.Add(c);

            var all = new List<MagicSpellSO>(known);
            if (lib != null)
                foreach (var s in lib.Spells)
                    if (s != null && s.caster == slot && GrimorioDelPersonaje.EsDelGrimorio(s) && !all.Contains(s)) all.Add(s);

            all.Sort((a, b) =>
            {
                int ka = a.HasCombo ? 1 : 0, kb = b.HasCombo ? 1 : 0;
                if (ka != kb) return ka.CompareTo(kb);
                if (a.HasCombo && a.comboSequence.Length != b.comboSequence.Length) return a.comboSequence.Length.CompareTo(b.comboSequence.Length);
                return string.CompareOrdinal(a.GetLocalizedName(), b.GetLocalizedName());
            });

            foreach (var s in all)
                _pages.Add(new Pagina { spell = s, known = known.Contains(s), owner = slot });
        }
    }

    private static bool CharacterAvailable(Slot slot)
    {
        if (slot == Slot.Will) return true;
        var party = Game.NPC.PlayerParty.HasInstance ? Game.NPC.PlayerParty.Instance : null;
        return party != null && party.GetMemberByName(slot == Slot.Liam ? "Liam" : "Estela") != null;
    }

    private int FindStart(SpellId startAt)
    {
        if (startAt != SpellId.None)
            for (int i = 0; i < _pages.Count; i++)
                if (_pages[i].spell.spellId == startAt && _pages[i].known) return i;

        Slot active = PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : Slot.Will;
        for (int i = 0; i < _pages.Count; i++)
            if (_pages[i].owner == active) return i;
        return 0;
    }

    private void Turn(int dir)
    {
        if (_turning || _pages.Count <= 1) return;
        int target = Mathf.Clamp(_index + dir, 0, _pages.Count - 1);
        if (target == _index) { Nudge(dir); return; }
        GoTo(target, dir);
    }

    private void NextSection()
    {
        if (_pages.Count == 0 || _turning) return;
        Slot current = _pages[_index].owner;
        for (int k = 1; k <= _pages.Count; k++)
        {
            int i = (_index + k) % _pages.Count;
            if (_pages[i].owner != current) { GoTo(i, i > _index ? 1 : -1); return; }
        }
        Nudge(1);
    }

    private void GoTo(int target, int dir)
    {
        _turning = true;
        Play(pageClip);

        var seq = DOTween.Sequence().SetUpdate(true);
        var leafImage = turningLeaf != null ? turningLeaf.GetComponent<Image>() : null;
        // Las dos páginas se desvanecen mientras la hoja se pliega sobre el lomo desde el lado
        // desde el que se pasa, y se despliega sobre el otro con el contenido nuevo debajo.
        seq.Append(FadePages(0f, 0.12f));
        if (turningLeaf)
        {
            turningLeaf.gameObject.SetActive(true);
            turningLeaf.pivot = new Vector2(dir > 0 ? 0f : 1f, 0.5f);
            turningLeaf.anchoredPosition = new Vector2(0f, turningLeaf.anchoredPosition.y);
            turningLeaf.localScale = Vector3.one;
            if (leafImage) { var c = leafImage.color; c.a = 1f; leafImage.color = c; }
            seq.Join(turningLeaf.DOScaleX(0f, 0.16f).SetEase(Ease.InQuad));
        }
        seq.AppendCallback(() =>
        {
            _index = target;
            Show(_index);
            if (turningLeaf) turningLeaf.pivot = new Vector2(dir > 0 ? 1f : 0f, 0.5f);
        });
        if (turningLeaf) seq.Append(turningLeaf.DOScaleX(1f, 0.16f).SetEase(Ease.OutQuad));
        seq.Append(FadePages(1f, 0.14f));
        if (leafImage) seq.Join(leafImage.DOFade(0f, 0.14f));
        seq.OnComplete(() =>
        {
            if (turningLeaf) turningLeaf.gameObject.SetActive(false);
            _turning = false;
        });
    }

    private Tween FadePages(float to, float time)
    {
        var s = DOTween.Sequence().SetUpdate(true);
        foreach (var page in new[] { leftPage, rightPage })
        {
            if (page == null) continue;
            var cg = page.GetComponent<CanvasGroup>();
            if (cg == null) cg = page.gameObject.AddComponent<CanvasGroup>();
            s.Join(cg.DOFade(to, time));
        }
        return s;
    }

    private void Nudge(int dir)
    {
        if (book == null) return;
        book.DOKill(true);
        book.DOPunchAnchorPos(new Vector2(12f * dir, 0f), 0.25f, 8, 0.5f).SetUpdate(true);
    }

    // ── Contenido de una página ───────────────────────────────────────────

    private void Show(int i)
    {
        if (i < 0 || i >= _pages.Count) return;
        var p = _pages[i];
        var s = p.spell;

        if (icon)
        {
            icon.sprite = s.attackIcon;
            icon.enabled = s.attackIcon != null;
            icon.color = p.known ? Color.white : lockedIconColor;
            icon.preserveAspect = true;
        }
        if (seal) seal.gameObject.SetActive(!p.known);

        string owner = GrimorioDelPersonaje.Nombre(p.owner);
        SetText(nameText, p.known ? s.GetLocalizedName() : "???");
        SetText(ownerText, string.Format(Loc("GRIMOIRE_OWNER", "Hechizo de {0}"), owner));
        if (ownerText) ownerText.color = OwnerColor(p.owner);
        SetTag(kindTag, kindTagText, p.known ? KindName(s) : null, KindColor(s));
        SetTag(elementTag, elementTagText, p.known ? ElementName(s.element) : null, ElementColor(s.element));

        if (!p.known)
        {
            SetText(castTitleText, Loc("GRIMOIRE_LOCKED_TITLE", "Página sin descubrir"));
            if (castRow) castRow.SetActive(false);
            SetText(loreText, Loc("GRIMOIRE_LOCKED_TEXT", "Esta página aún está sellada. Quizá la guarde un maestro, una misión o algún rincón al que solo se llega volando, levitando o buceando."));
            if (statRow) statRow.SetActive(false);
            SetText(effectText, "");
            SetTag(equippedTag, equippedTagText, null, Color.clear);
        }
        else
        {
            SetText(castTitleText, Loc("GRIMOIRE_CAST_TITLE", "Cómo se lanza"));
            ShowCast(s);
            string lore = !string.IsNullOrEmpty(s.loreId) ? Loc(s.loreId, s.lore) : s.lore;
            SetText(loreText, string.IsNullOrEmpty(lore) ? "" : $"<i>«{lore}»</i>");
            ShowStats(s);
            SetText(effectText, GrimorioDelPersonaje.Efectos(s));
            ShowEquipped(s, p.owner);
        }

        // Contador y cintas.
        int inSection = 0, knownInSection = 0, posInSection = 0;
        for (int k = 0; k < _pages.Count; k++)
        {
            if (_pages[k].owner != p.owner) continue;
            inSection++;
            if (_pages[k].known) knownInSection++;
            if (k == i) posInSection = inSection;
        }
        SetText(pageCounterText, string.Format(Loc("GRIMOIRE_PAGE_COUNTER", "{0} · página {1} de {2} · {3} descubiertas"),
            owner, posInSection, inSection, knownInSection));

        for (int r = 0; r < ribbons.Length && r < Order.Length; r++)
        {
            var rib = ribbons[r];
            if (rib == null) continue;
            bool available = false;
            foreach (var pg in _pages) if (pg.owner == Order[r]) { available = true; break; }
            rib.gameObject.SetActive(available);
            bool current = Order[r] == p.owner;
            rib.rectTransform.DOKill();
            rib.rectTransform.DOAnchorPos(_ribbonBase[r] + new Vector2(current ? 26f : 0f, 0f), 0.2f).SetUpdate(true);
            var col = rib.color; col.a = current ? 1f : 0.6f; rib.color = col;
        }

        if (prevButton) prevButton.gameObject.SetActive(i > 0);
        if (nextButton) nextButton.gameObject.SetActive(i < _pages.Count - 1);
    }

    // Dos pasos con los iconos de los botones del mando activo. La flecha solo sale cuando uno va
    // detrás del otro (combo, levitación); en un básico son dos cosas sueltas.
    private void ShowCast(MagicSpellSO s)
    {
        if (castRow) castRow.SetActive(true);
        string x = Core.InputGlyphs.ComboButtonGlyphs.SpriteTag(ComboButton.X);
        if (s.kind == MagicKind.Levitation)
        {
            SetStep(0, x, Loc("GRIMOIRE_STEP_HOLD", "Mantén: levanta"));
            SetStep(1, x, Loc("GRIMOIRE_STEP_RELEASE", "Suelta: lanza"));
            if (castArrow) castArrow.SetActive(true);
        }
        else if (s.HasCombo)
        {
            SetStep(0, Core.InputGlyphs.ComboButtonGlyphs.SpriteTag(ComboButton.Y), Loc("GRIMOIRE_STEP_OPEN", "Abre el círculo"));
            SetStep(1, Core.InputGlyphs.ComboButtonGlyphs.SequenceSprites(s.comboSequence), Loc("GRIMOIRE_STEP_TYPE", "Teclea"));
            if (castArrow) castArrow.SetActive(true);
        }
        else
        {
            SetStep(0, x, Loc("GRIMOIRE_STEP_CAST", "Lanzar"));
            SetStep(1, Core.InputGlyphs.InputGlyphService.SpriteTag(Core.InputGlyphs.InputGlyphNames.ShoulderLeft),
                Loc("GRIMOIRE_STEP_ROTATE", "Cambiar de básico"));
            if (castArrow) castArrow.SetActive(false);
        }
    }

    private void SetStep(int i, string icons, string caption)
    {
        if (i < stepIcons.Length && stepIcons[i] != null) stepIcons[i].text = icons;
        if (i < stepCaptions.Length && stepCaptions[i] != null) stepCaptions[i].text = caption;
    }

    private void ShowStats(MagicSpellSO s)
    {
        int n = 0;
        if (s.damage > 0f)
            SetStat(ref n, Num(s.damage), s.kind == MagicKind.Zone
                ? Loc("GRIMOIRE_STAT_DAMAGE_TICK", "Daño por golpe")
                : Loc("GRIMOIRE_STAT_DAMAGE", "Daño"), damageColor);
        if (s.healPerTick > 0f) SetStat(ref n, Num(s.healPerTick), Loc("GRIMOIRE_STAT_HEAL_TICK", "Cura por golpe"), healColor);
        if (s.groupShieldSeconds > 0f) SetStat(ref n, Num(s.groupShieldSeconds) + " s", Loc("GRIMOIRE_STAT_SHIELD", "Escudo"), shieldColor);
        if (s.kind == MagicKind.Zone && s.zoneDuration > 0f && s.zoneTickInterval < s.zoneDuration)
            SetStat(ref n, Num(s.zoneDuration) + " s", Loc("GRIMOIRE_STAT_DURATION", "Duración"), inkColor);
        SetStat(ref n, Num(s.manaCost), Loc("GRIMOIRE_STAT_MANA", "Maná"), manaColor);

        for (int i = n; i < statValues.Length; i++)
            if (statValues[i] != null) statValues[i].transform.parent.gameObject.SetActive(false);
        if (statRow) statRow.SetActive(n > 0);
    }

    private void SetStat(ref int n, string value, string label, Color color)
    {
        if (n >= statValues.Length || statValues[n] == null) return;
        statValues[n].transform.parent.gameObject.SetActive(true);
        statValues[n].text = value;
        statValues[n].color = color;
        if (n < statLabels.Length && statLabels[n] != null) statLabels[n].text = label;
        n++;
    }

    private void ShowEquipped(MagicSpellSO s, Slot owner)
    {
        if (s.HasCombo)
        {
            SetTag(equippedTag, equippedTagText, Loc("GRIMOIRE_COMBO_ALWAYS", "No ocupa ranura · siempre a mano"), comboTagColor);
            return;
        }
        if (s.slotType == SpellSlotType.SpecialOnly) { SetTag(equippedTag, equippedTagText, null, Color.clear); return; }

        int slot = -1;
        if (owner == Slot.Will)
        {
            var preset = UnlockService.GetActivePreset();
            if (preset != null && preset.basicSpellIds != null) slot = preset.basicSpellIds.IndexOf(s.spellId);
        }
        else
        {
            slot = GrimorioDelPersonaje.BasicosEquipados(owner).IndexOf(s);
        }
        if (slot >= 0) SetTag(equippedTag, equippedTagText, string.Format(Loc("GRIMOIRE_EQUIPPED", "Equipado · ranura {0}"), slot + 1), equippedColor);
        else SetTag(equippedTag, equippedTagText, Loc("GRIMOIRE_NOT_EQUIPPED", "Sin equipar"), notEquippedColor);
    }

    private void UpdateHint()
    {
        if (hintText == null) return;
        string lb = Core.InputGlyphs.InputGlyphService.SpriteTag(Core.InputGlyphs.InputGlyphNames.ShoulderLeft);
        string rb = Core.InputGlyphs.InputGlyphService.SpriteTag(Core.InputGlyphs.InputGlyphNames.ShoulderRight);
        string y = Core.InputGlyphs.ComboButtonGlyphs.SpriteTag(ComboButton.Y);
        string b = Core.InputGlyphs.ComboButtonGlyphs.SpriteTag(ComboButton.B);
        bool kb = Core.InputGlyphs.InputGlyphService.CurrentFamily == Core.InputGlyphs.InputGlyphDeviceFamily.KeyboardMouse;
        hintText.text = kb
            ? Loc("GRIMOIRE_HINT_KB", "A / D  Pasar página      Tab  Otro personaje      Esc  Cerrar")
            : string.Format(Loc("GRIMOIRE_HINT", "{0} {1}  Pasar página      {2}  Otro personaje      {3}  Cerrar"), lb, rb, y, b);
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource != null)
        {
            audioSource.ignoreListenerPause = true;
            audioSource.PlayOneShot(clip);
        }
    }

    private static void SetText(TextMeshProUGUI t, string s)
    {
        if (t == null) return;
        t.text = s;
        t.gameObject.SetActive(!string.IsNullOrEmpty(s));
    }

    private static void SetTag(Image bg, TextMeshProUGUI text, string label, Color color)
    {
        if (bg == null) return;
        bool on = !string.IsNullOrEmpty(label);
        if (bg.gameObject.activeSelf != on) bg.gameObject.SetActive(on);
        if (!on) return;
        bg.color = color;
        if (text != null) text.text = label;
    }

    private static string Num(float v) =>
        Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#");

    private static string KindName(MagicSpellSO s) =>
        s.kind == MagicKind.Levitation ? Loc("GRIMOIRE_KIND_LEVITATION", "Levitación")
        : s.HasCombo ? Loc("GRIMOIRE_KIND_COMBO", "Combo")
        : Loc("GRIMOIRE_KIND_BASIC", "Básico");

    private static Color KindColor(MagicSpellSO s) =>
        s.kind == MagicKind.Levitation ? new Color(0.24f, 0.46f, 0.5f)
        : s.HasCombo ? new Color(0.62f, 0.44f, 0.08f)
        : new Color(0.42f, 0.29f, 0.17f);

    private static Color ElementColor(MagicElement e) => e switch
    {
        MagicElement.Fire => new Color(0.75f, 0.29f, 0.17f),
        MagicElement.Ice => new Color(0.25f, 0.55f, 0.72f),
        MagicElement.Storm => new Color(0.3f, 0.6f, 0.48f),
        MagicElement.Light => new Color(0.79f, 0.59f, 0.16f),
        MagicElement.Mind => new Color(0.48f, 0.29f, 0.66f),
        _ => new Color(0.42f, 0.29f, 0.17f)
    };

    private static Color OwnerColor(Slot slot) => slot switch
    {
        Slot.Estela => new Color(0.66f, 0.2f, 0.12f),
        Slot.Liam => new Color(0.42f, 0.2f, 0.6f),
        _ => new Color(0.62f, 0.44f, 0.08f)
    };

    private static string ElementName(MagicElement e) => e switch
    {
        MagicElement.Fire => Loc("ELEMENT_FIRE", "Fuego"),
        MagicElement.Ice => Loc("ELEMENT_ICE", "Hielo"),
        MagicElement.Storm => Loc("ELEMENT_STORM", "Viento"),
        MagicElement.Light => Loc("ELEMENT_LIGHT", "Luz"),
        MagicElement.Mind => Loc("ELEMENT_MIND", "Mente"),
        _ => e.ToString()
    };

    private static string Loc(string key, string fallback) =>
        LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, fallback) : fallback;
}
