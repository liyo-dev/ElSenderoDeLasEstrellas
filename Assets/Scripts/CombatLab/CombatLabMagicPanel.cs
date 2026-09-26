using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

/// <summary>
/// Selector de hechizos exclusivo de CombatLab. Cambia el loadout en runtime y no escribe
/// en PlayerPresetSO ni en la partida guardada.
/// </summary>
public sealed class CombatLabMagicPanel : MonoBehaviour
{
    private const float PanelWidth = 390f;
    private const float PanelHeight = 402f;

    private readonly List<MagicSpellSO> _regularSpells = new();
    private readonly List<MagicSpellSO> _specialSpells = new();
    private readonly MagicSpellSO[] _equipped = new MagicSpellSO[3];
    private readonly int[] _selection = new int[3];

    private MagicCaster _caster;
    private PlayerActionManager _actionManager;
    private PlayerPresetService _presetService;
    private ManaPool _manaPool;
    private SpecialChargeMeter _specialCharge;
    private Core.PlayerInputManager _inputManager;
    private bool _isOpen;
    private bool _ownsUiMode;
    private bool _initialized;
    private string _status = "";

    public void Initialize(GameObject player)
    {
        if (player == null) return;

        _caster = player.GetComponentInChildren<MagicCaster>(true);
        _actionManager = player.GetComponentInChildren<PlayerActionManager>(true);
        _presetService = player.GetComponentInChildren<PlayerPresetService>(true);
        _manaPool = player.GetComponentInChildren<ManaPool>(true);
        _specialCharge = player.GetComponentInChildren<SpecialChargeMeter>(true);
        _inputManager = Core.PlayerInputManager.Instance;

        if (_caster == null || _presetService == null || _presetService.SpellLibrary == null)
        {
            _status = "Falta MagicCaster o SpellLibrary en Will.";
            return;
        }

        var spells = _presetService.SpellLibrary.Spells;
        for (int i = 0; i < spells.Count; i++)
        {
            var spell = spells[i];
            if (spell == null || (spell.prefab == null && spell.kind != MagicKind.Levitation)) continue;

            if (spell.slotType == SpellSlotType.SpecialOnly)
                _specialSpells.Add(spell);
            else
                _regularSpells.Add(spell);
        }

        _equipped[0] = ChooseStartingSpell(MagicSlot.Left, _regularSpells);
        _equipped[1] = ChooseStartingSpell(MagicSlot.Right, _regularSpells);
        if (_equipped[1] == _equipped[0])
            _equipped[1] = FindAlternative(_regularSpells, _equipped[0]);
        _equipped[2] = ChooseStartingSpell(MagicSlot.Special, _specialSpells);
        SyncSelectionIndices();
        ApplyLoadout();
        EnableLabAbilities();

        _caster.ResetAllCooldowns();
        RefillTestResources();
        _initialized = true;
        SetOpen(true);
    }

    private MagicSpellSO ChooseStartingSpell(MagicSlot slot, List<MagicSpellSO> candidates)
    {
        var current = _caster.GetSpellForSlot(slot);
        if (current != null && Contains(candidates, current)) return current;
        return candidates.Count > 0 ? candidates[0] : null;
    }

    private void EnableLabAbilities()
    {
        if (_actionManager == null) return;

        _actionManager.ApplyAbilities(new PlayerAbilities
        {
            swim = _actionManager.AllowSwim,
            jump = _actionManager.AllowJump,
            climb = _actionManager.AllowClimb,
            fly = _actionManager.AllowFly,
            sprint = _actionManager.AllowSprint,
            magic = true,
            shield = true
        });
    }

    private void Update()
    {
        if (!_initialized) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            SetOpen(!_isOpen);

        if (_isOpen) return;

        if (GamepadInputReader.AttackMagicLeftPressed)
            ReportMagicInput(MagicSlot.Left, "Clic izquierdo");
        else if (GamepadInputReader.AttackMagicRightPressed)
            ReportMagicInput(MagicSlot.Right, "Clic derecho");
        else if (GamepadInputReader.AttackMagicSpecialPressed)
            ReportMagicInput(MagicSlot.Special, "Q / especial");
    }

    private void ReportMagicInput(MagicSlot slot, string inputName)
    {
        if (_caster == null)
        {
            _status = $"{inputName}: no se encontró MagicCaster.";
        }
        else if (_actionManager == null || !_actionManager.CanCastMagic())
        {
            _status = $"{inputName}: Magia está bloqueada.";
        }
        else
        {
            bool ready = _caster.CanCastSpell(slot, _caster.GetSpellForSlot(slot), out string reason);
            if (ready)
                _status = $"{inputName} detectado · slot listo.";
            else if (reason.StartsWith("Cooldown activo"))
                _status = $"{inputName} detectado · lanzamiento iniciado.";
            else
                _status = $"{inputName} detectado · {reason}.";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CombatLab] {_status}");
#endif
    }

    private void OnGUI()
    {
        if (!_initialized)
        {
            if (!string.IsNullOrEmpty(_status))
                GUI.Box(new Rect(Screen.width - PanelWidth - 18f, 18f, PanelWidth, 46f), "COMBAT LAB · " + _status);
            return;
        }

        float x = Screen.width - PanelWidth - 18f;
        if (!_isOpen)
        {
            var compactBounds = new Rect(x, 18f, PanelWidth, 82f);
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 2 && compactBounds.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                SetOpen(true);
            }

            GUI.Box(compactBounds, "MAGIA LAB");
            GUI.Label(new Rect(x + 14f, 38f, PanelWidth - 28f, 18f),
                $"Izq: {_equipped[0]?.GetLocalizedName() ?? "—"}  ·  Der: {_equipped[1]?.GetLocalizedName() ?? "—"}");
            GUI.Label(new Rect(x + 14f, 55f, PanelWidth - 28f, 18f),
                $"Especial: {_equipped[2]?.GetLocalizedName() ?? "—"}  ·  M o clic central: editar");
            GUI.Label(new Rect(x + 14f, 72f, PanelWidth - 28f, 20f), _status);
            return;
        }

        GUI.Box(new Rect(x, 18f, PanelWidth, PanelHeight), "COMBAT LAB · HECHIZOS Y HABILIDADES");
        GUI.Label(new Rect(x + 16f, 48f, PanelWidth - 32f, 34f),
            "Magia y escudo habilitados solo aquí. Hechizos y desbloqueos no modifican tu perfil ni la partida.");

        DrawSlotRow(x, 89f, 0, "IZQUIERDA", _regularSpells);
        DrawSlotRow(x, 135f, 1, "DERECHA", _regularSpells);
        DrawSlotRow(x, 181f, 2, "ESPECIAL", _specialSpells);

        if (GUI.Button(new Rect(x + 16f, 226f, 172f, 28f), "Recargar maná y especial"))
            RefillTestResources();
        if (GUI.Button(new Rect(x + 198f, 226f, 176f, 28f), "Reiniciar cooldowns"))
            _caster.ResetAllCooldowns();

        GUI.Label(new Rect(x + 16f, 258f, PanelWidth - 32f, 22f),
            $"HABILIDADES DE PRUEBA · Maná: {GetManaReadout()}");
        bool canEditAbilities = _actionManager != null;
        GUI.enabled = canEditAbilities;
        DrawAbilityToggle(new Rect(x + 16f, 282f, 108f, 30f), "Magia", 0);
        DrawAbilityToggle(new Rect(x + 132f, 282f, 108f, 30f), "Salto", 1);
        DrawAbilityToggle(new Rect(x + 248f, 282f, 126f, 30f), "Vuelo", 2);
        GUI.enabled = true;

        if (GUI.Button(new Rect(x + 16f, 326f, 358f, 26f), "Cerrar panel y probar habilidades"))
            SetOpen(false);
        GUI.Label(new Rect(x + 16f, 356f, PanelWidth - 32f, 40f),
            "En juego: ESPACIO salta; púlsalo otra vez en el aire para volar. Clic izq./der. lanza hechizos y Q usa el especial. M abre/cierra este panel.");
    }

    private void DrawAbilityToggle(Rect bounds, string label, int abilityIndex)
    {
        bool enabled = abilityIndex switch
        {
            0 => _actionManager.AllowMagic,
            1 => _actionManager.AllowJump,
            _ => _actionManager.AllowFly
        };

        if (GUI.Button(bounds, $"{label}: {(enabled ? "SI" : "NO")}"))
        {
            bool magic = _actionManager.AllowMagic;
            bool jump = _actionManager.AllowJump;
            bool fly = _actionManager.AllowFly;
            if (abilityIndex == 0) magic = !magic;
            else if (abilityIndex == 1) jump = !jump;
            else fly = !fly;
            ApplyLabAbilities(magic, jump, fly);
        }
    }

    private void ApplyLabAbilities(bool magic, bool jump, bool fly)
    {
        _actionManager.ApplyAbilities(new PlayerAbilities
        {
            swim = _actionManager.AllowSwim,
            jump = jump,
            climb = _actionManager.AllowClimb,
            fly = fly,
            sprint = _actionManager.AllowSprint,
            magic = magic,
            shield = _actionManager.AllowShield
        });

        _status = $"Habilidades de prueba: Magia {(magic ? "SI" : "NO")}, Salto {(jump ? "SI" : "NO")}, Vuelo {(fly ? "SI" : "NO")}.";
    }

    private void DrawSlotRow(float x, float y, int slotIndex, string label, List<MagicSpellSO> candidates)
    {
        GUI.Label(new Rect(x + 16f, y + 5f, 74f, 24f), label);
        bool hasChoices = candidates.Count > 0;
        GUI.enabled = hasChoices;

        if (GUI.Button(new Rect(x + 92f, y, 30f, 28f), "‹"))
            SelectRelative(slotIndex, candidates, -1);

        string spellName = _equipped[slotIndex] != null
            ? _equipped[slotIndex].GetLocalizedName()
            : "Sin hechizo compatible";
        GUI.Label(new Rect(x + 126f, y + 4f, 205f, 22f), spellName);

        if (GUI.Button(new Rect(x + 334f, y, 40f, 28f), "›"))
            SelectRelative(slotIndex, candidates, 1);

        GUI.enabled = true;
    }

    private void SelectRelative(int slotIndex, List<MagicSpellSO> candidates, int direction)
    {
        if (candidates.Count == 0) return;

        int index = _selection[slotIndex];
        index = (index + direction + candidates.Count) % candidates.Count;
        _selection[slotIndex] = index;
        _equipped[slotIndex] = candidates[index];
        ApplyLoadout();
        _status = $"{SlotName(slotIndex)}: {_equipped[slotIndex].GetLocalizedName()}";
    }

    private void ApplyLoadout()
    {
        _caster.SetSpells(_equipped[0], _equipped[1], _equipped[2]);
    }

    private void SyncSelectionIndices()
    {
        _selection[0] = FindIndex(_regularSpells, _equipped[0]);
        _selection[1] = FindIndex(_regularSpells, _equipped[1]);
        _selection[2] = FindIndex(_specialSpells, _equipped[2]);
    }

    private void RefillTestResources()
    {
        if (_manaPool != null)
        {
            if (_manaPool.Max <= 0f)
            {
                float highestEquippedCost = 0f;
                for (int i = 0; i < _equipped.Length; i++)
                {
                    if (_equipped[i] != null)
                        highestEquippedCost = Mathf.Max(highestEquippedCost, _equipped[i].manaCost);
                }

                float testMaxMana = Mathf.Max(50f, highestEquippedCost * 2f);
                _manaPool.Init(testMaxMana, testMaxMana);
            }
            else
            {
                _manaPool.Refill(_manaPool.Max);
            }
        }

        if (_specialCharge != null) _specialCharge.SetCharge(_specialCharge.MaxCharge);
        _status = $"Recursos listos · Maná {GetManaReadout()}.";
    }

    private string GetManaReadout()
    {
        if (_manaPool == null) return "sin ManaPool";
        return $"{Mathf.CeilToInt(_manaPool.Current)}/{Mathf.CeilToInt(_manaPool.Max)}";
    }

    private void SetOpen(bool open)
    {
        if (_isOpen == open) return;
        _isOpen = open;

        if (_inputManager == null) _inputManager = Core.PlayerInputManager.Instance;
        if (_isOpen && _inputManager != null)
        {
            _inputManager.PushUIMode();
            _ownsUiMode = true;
        }
        else if (!_isOpen && _ownsUiMode && _inputManager != null)
        {
            _inputManager.PopUIMode();
            _ownsUiMode = false;
        }
    }

    private void OnDisable()
    {
        if (_ownsUiMode && _inputManager != null)
        {
            _inputManager.PopUIMode();
            _ownsUiMode = false;
        }
    }

    private static int FindIndex(List<MagicSpellSO> list, MagicSpellSO spell)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == spell) return i;
        return 0;
    }

    private static MagicSpellSO FindAlternative(List<MagicSpellSO> list, MagicSpellSO current)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != current) return list[i];
        return current;
    }

    private static bool Contains(List<MagicSpellSO> list, MagicSpellSO spell)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == spell) return true;
        return false;
    }

    private static string SlotName(int slotIndex) => slotIndex switch
    {
        0 => "Izquierda",
        1 => "Derecha",
        _ => "Especial"
    };
}
