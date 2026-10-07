using System;
using System.Collections.Generic;
using UnityEngine;
using Core;
using Invector.vCharacterController;

/// <summary>
/// Combo mágico en la Y (INC-494). Pulsar Y abre el círculo con el panel vacío; se teclea una
/// secuencia con A, B, X e Y y, en cuanto coincide con la de un hechizo aprendido, sale ese
/// hechizo por <see cref="MagicCaster.Cast"/> (su maná, gesto del centro). Los combos no se equipan:
/// el repertorio son los hechizos desbloqueados que tienen secuencia (<see cref="MagicSpellSO.comboSequence"/>).
/// <list type="bullet">
/// <item>Si lo tecleado ya no puede ser ninguna secuencia, se disipa al momento y cuesta
/// <see cref="fizzleManaCost"/> de maná.</item>
/// <item>Más de <see cref="inputTimeout"/> sin pulsar: se cierra (gratis si no se había tecleado nada).</item>
/// <item>Si golpean al jugador, se rompe el círculo.</item>
/// <item>Tras un combo lanzado hay un enfriamiento compartido (<see cref="comboCooldown"/>).</item>
/// </list>
/// Mientras está abierto (<see cref="IsComposing"/>), los demás lectores de botones de combate
/// (X, LB, B, LT+RT, salto) no hacen nada, y el jugador no se mueve.
/// Lo que se ve y se oye (círculo, elevación, viento, luz, sonidos) lo pone
/// <see cref="PresentacionDelCombo"/> escuchando los eventos de este componente.
/// </summary>
[DisallowMultipleComponent]
public class ComboCastController : MonoBehaviour
{
    public enum Result { Cast, Fizzled, Cancelled, Interrupted }

    /// <summary>
    /// El jugador está tecleando un combo (también el fotograma en que se cierra, para que el último
    /// botón de la secuencia no dispare además su acción normal: un salto, una X, la B).
    /// </summary>
    public static bool IsComposing => s_open || Time.frameCount == s_closedFrame;

    private static bool s_open;
    private static int s_closedFrame = -1;

    private static void SetComposing(bool open)
    {
        if (s_open && !open) s_closedFrame = Time.frameCount;
        s_open = open;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { s_open = false; s_closedFrame = -1; }
#endif

    [Header("Referencias (se buscan solas si están vacías)")]
    [SerializeField] private MagicCaster magicCaster;
    [SerializeField] private vThirdPersonController controller;

    [Header("Reglas")]
    [Tooltip("Segundos máximos entre pulsaciones.")]
    [SerializeField] private float inputTimeout = 1.5f;
    [Tooltip("Maná que se pierde al fallar la secuencia.")]
    [SerializeField] private float fizzleManaCost = 5f;
    [Tooltip("Enfriamiento compartido tras lanzar un combo.")]
    [SerializeField] private float comboCooldown = 4f;
    [Tooltip("Botones máximos de una secuencia.")]
    [SerializeField, Range(2, 8)] private int maxLength = 5;

    [Header("Poses (rutas completas en la capa superior)")]
    [Tooltip("Pose mientras se teclea (en bucle). Los brazos los coloca ManosIK sosteniendo el orbe de PresentacionDelCombo.")]
    [SerializeField] private string enterState = "UpperBody.Magic.ComboIdle";
    [SerializeField] private string exitState = "UpperBody.Magic.ComboExit";
    [SerializeField] private string breakState = "UpperBody.Magic.ComboBreak";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs;

    private readonly List<MagicSpellSO> _repertoire = new List<MagicSpellSO>(8);
    private readonly List<bool> _possible = new List<bool>(8);
    private readonly List<ComboButton> _typed = new List<ComboButton>(8);
    private float _lastInputTime;
    private float _cooldownUntil = -1f;
    private float _cooldownStart;
    private bool _open;

    private ManaPool _mana;
    private PlayerActionManager _actions;
    private PlayerShieldController _shield;
    private PlayerHealthSystem _health;

    /// <summary>Se abrió el círculo con este repertorio.</summary>
    public event Action<IReadOnlyList<MagicSpellSO>> OnOpened;
    /// <summary>Se pulsó un botón: lo tecleado hasta ahora y qué hechizos siguen siendo posibles.</summary>
    public event Action<IReadOnlyList<ComboButton>, IReadOnlyList<bool>> OnInput;
    /// <summary>Se cerró el círculo: cómo y (si salió) qué hechizo.</summary>
    public event Action<Result, MagicSpellSO> OnClosed;
    /// <summary>Se pulsó Y y no se pudo abrir: mensaje ya traducido para el jugador.</summary>
    public event Action<string> OnDenied;

    public bool IsOpen => _open;
    /// <summary>Personaje que conjura el combo.</summary>
    public vThirdPersonController Personaje => controller;
    /// <summary>Botones máximos de una secuencia.</summary>
    public int LongitudMaxima => maxLength;
    public IReadOnlyList<ComboButton> Typed => _typed;

    /// <summary>Enfriamiento restante del combo, de 1 (recién lanzado) a 0 (disponible).</summary>
    public float CooldownNormalized =>
        comboCooldown <= 0f || Time.time >= _cooldownUntil ? 0f : (_cooldownUntil - Time.time) / comboCooldown;

    /// <summary>Segundos que quedan para poder pulsar otra vez sin que se cierre.</summary>
    public float TimeLeftNormalized => _open ? Mathf.Clamp01(1f - (Time.time - _lastInputTime) / inputTimeout) : 0f;

    void Awake()
    {
        if (!magicCaster) magicCaster = GetComponentInParent<MagicCaster>() ?? GetComponentInChildren<MagicCaster>();
        if (!controller) controller = GetComponentInParent<vThirdPersonController>() ?? GetComponentInChildren<vThirdPersonController>();
        _mana = GetComponentInParent<ManaPool>();
        _actions = GetComponentInParent<PlayerActionManager>();
        _shield = GetComponentInParent<PlayerShieldController>() ?? GetComponentInChildren<PlayerShieldController>();
    }

    void Start()
    {
        _health = GetComponentInParent<PlayerHealthSystem>() ?? GetComponentInChildren<PlayerHealthSystem>();
        if (_health == null) PlayerService.TryGetComponent(out _health, allowSceneLookup: false);
        if (_health != null) _health.OnDamageReceived += OnDamaged;
        if (_shield == null && controller != null) _shield = controller.GetComponentInChildren<PlayerShieldController>();
    }

    void OnDisable()
    {
        if (_open) Close(Result.Cancelled, null, playExit: false);
    }

    void OnDestroy()
    {
        if (_health != null) _health.OnDamageReceived -= OnDamaged;
        if (_open) SetComposing(false);
    }

    // ── Entrada ────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_open)
        {
            if (GamepadInputReader.YButtonPressed && GameState.CanProcessGameplayInput) TryOpen();
            return;
        }

        if (!GameState.CanProcessGameplayInput) { Close(Result.Cancelled, null, playExit: true); return; }

        // Quieto mientras teclea (y suspendido si está en el aire).
        if (controller)
        {
            controller.CommitToAction(0.2f, Vector3.zero);
            if (controller.IsAirborne) controller.HoldAirborne(0.2f);
        }

        if (TryReadButton(out ComboButton pressed))
        {
            AddButton(pressed);
            return;
        }

        if (Time.time - _lastInputTime > inputTimeout)
        {
            if (_typed.Count == 0) Close(Result.Cancelled, null, playExit: true);
            else Fizzle("se acabó el tiempo");
        }
    }

    private static bool TryReadButton(out ComboButton button)
    {
        if (GamepadInputReader.AttackMagicLeftPressed) { button = ComboButton.X; return true; }
        if (GamepadInputReader.YButtonPressed) { button = ComboButton.Y; return true; }
        if (GamepadInputReader.AttackMagicRightPressed) { button = ComboButton.B; return true; }
        if (GamepadInputReader.JumpPressed) { button = ComboButton.A; return true; }
        button = default;
        return false;
    }

    // ── Apertura ───────────────────────────────────────────────────────────

    private void TryOpen()
    {
        if (magicCaster == null) { Deny("sin MagicCaster", "COMBO_DENIED_BUSY", "Ahora no se puede."); return; }
        if (Time.time < _cooldownUntil) { Deny("en enfriamiento", "COMBO_DENIED_COOLDOWN", "El círculo aún no está listo."); return; }
        if (magicCaster.IsCasting) { Deny("está lanzando otro hechizo", "COMBO_DENIED_BUSY", "Ahora no se puede."); return; }
        if (_actions != null && !_actions.CanCastMagic()) { Deny("la magia está bloqueada (PlayerActionManager)", "COMBO_DENIED_BUSY", "Ahora no se puede."); return; }
        if (_actions != null && _actions.IsInMode(ActionMode.Flying)) { Deny("volando", "COMBO_DENIED_FLYING", "No puedes concentrarte volando."); return; }
        if (_shield != null && _shield.IsDefending) { Deny("con el escudo levantado", "COMBO_DENIED_BUSY", "Ahora no se puede."); return; }

        BuildRepertoire();
        if (_repertoire.Count == 0) { Deny("no hay combos aprendidos", "COMBO_DENIED_NONE", "Aún no conoces ningún combo."); return; }

        _open = true;
        SetComposing(true);
        _typed.Clear();
        _possible.Clear();
        for (int i = 0; i < _repertoire.Count; i++) _possible.Add(true);
        _lastInputTime = Time.time;

        if (controller) controller.HoldUpperBodyPose(enterState);
        OnOpened?.Invoke(_repertoire);
        Log($"Círculo abierto ({_repertoire.Count} combos)");
    }

    private void BuildRepertoire()
    {
        _repertoire.Clear();

        // Con Estela o Liam al mando, sus combos son los de su ficha (p. ej. Sello del Pacto de Liam).
        var pcm = PartyControlManager.Instance;
        if (pcm != null && pcm.ActiveSlot != PartyControlManager.CharacterSlot.Will)
        {
            string name = pcm.ActiveSlot == PartyControlManager.CharacterSlot.Liam ? "Liam" : "Estela";
            var party = Game.NPC.PlayerParty.HasInstance ? Game.NPC.PlayerParty.Instance : null;
            var member = party != null ? party.GetMemberByName(name) : null;
            if (member != null)
                foreach (var s in member.EffectiveSpells)
                    if (s != null && s.HasCombo && !_repertoire.Contains(s)) _repertoire.Add(s);
            // Los de su ficha (INC-496) y los que haya aprendido del grimorio (INC-503).
            foreach (var s in GrimorioDelPersonaje.CombosDisponibles(pcm.ActiveSlot))
                if (!_repertoire.Contains(s)) _repertoire.Add(s);
            return;
        }

        var preset = UnlockService.GetActivePreset();
        if (preset == null || preset.unlockedSpells == null) return;
        if (!PlayerService.TryGetComponent<PlayerPresetService>(out var presets, includeInactive: false, allowSceneLookup: true)) return;
        var library = presets != null ? presets.SpellLibrary : null;
        if (library == null) return;

        foreach (var id in preset.unlockedSpells)
        {
            if (library.TryGet(id, out var spell) && spell != null && spell.HasCombo
                && spell.caster == PartyControlManager.CharacterSlot.Will && !_repertoire.Contains(spell))
                _repertoire.Add(spell);
        }
    }

    // ── Secuencia ──────────────────────────────────────────────────────────

    private void AddButton(ComboButton button)
    {
        _typed.Add(button);
        _lastInputTime = Time.time;

        MagicSpellSO match = null;
        int stillPossible = 0;
        for (int i = 0; i < _repertoire.Count; i++)
        {
            bool ok = _possible[i] && IsPrefix(_typed, _repertoire[i].comboSequence);
            _possible[i] = ok;
            if (!ok) continue;
            stillPossible++;
            if (_repertoire[i].comboSequence.Length == _typed.Count) match = _repertoire[i];
        }

        OnInput?.Invoke(_typed, _possible);

        if (match != null) { CastMatch(match); return; }
        if (stillPossible == 0 || _typed.Count >= maxLength) Fizzle("la secuencia no es de ningún combo");
    }

    private static bool IsPrefix(List<ComboButton> typed, ComboButton[] sequence)
    {
        if (sequence == null || typed.Count > sequence.Length) return false;
        for (int i = 0; i < typed.Count; i++)
            if (typed[i] != sequence[i]) return false;
        return true;
    }

    private void CastMatch(MagicSpellSO spell)
    {
        // Se cierra antes de lanzar: Cast comprueba permisos y el gesto sustituye a la pose.
        _open = false;
        SetComposing(false);
        bool cast = magicCaster.Cast(spell, CastHand.Center, 1f);
        if (cast)
        {
            _cooldownStart = Time.time;
            _cooldownUntil = Time.time + comboCooldown;
            OnClosed?.Invoke(Result.Cast, spell);
            Log($"Combo lanzado: {spell.displayName}");
        }
        else
        {
            if (controller) controller.PlayUpperBodyAction(exitState);
            OnClosed?.Invoke(Result.Fizzled, spell);
            Log($"Combo {spell.displayName} correcto, pero no se pudo lanzar (maná o permisos)");
        }
    }

    private void Fizzle(string why)
    {
        if (_mana != null && fizzleManaCost > 0f) _mana.TrySpend(Mathf.Min(fizzleManaCost, _mana.Current));
        Close(Result.Fizzled, null, playExit: true);
        Log("Se disipa: " + why);
    }

    private void OnDamaged(float _)
    {
        if (!_open) return;
        _open = false;
        SetComposing(false);
        if (controller) controller.PlayUpperBodyAction(breakState);
        OnClosed?.Invoke(Result.Interrupted, null);
        Log("Círculo roto por un golpe");
    }

    private void Close(Result result, MagicSpellSO spell, bool playExit)
    {
        _open = false;
        SetComposing(false);
        if (playExit && controller) controller.PlayUpperBodyAction(exitState);
        OnClosed?.Invoke(result, spell);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void Deny(string why, string messageKey, string fallback)
    {
#if UNITY_EDITOR
        Debug.Log("[Combo] No se abre: " + why, this);
#endif
        string msg = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(messageKey, fallback) : fallback;
        OnDenied?.Invoke(msg);
    }

    private void Log(string message)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (showDebugLogs) Debug.Log($"[Combo] {message}", this);
#endif
    }
}
