using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

/// <summary>
/// Selector de hechizos exclusivo de CombatLab. Cambia el loadout en runtime y no escribe
/// en PlayerPresetSO ni en la partida guardada. Al entrar aprende el grimorio entero (todos los
/// hechizos de todos los personajes) en el preset de la sesión (copia en memoria), para poder
/// equipar cualquiera desde el menú de Start.
/// </summary>
public sealed class CombatLabMagicPanel : MonoBehaviour
{
    private const float PanelWidth = 390f;
    private const float PanelHeight = 520f;
    private const float RowHeight = 40f;

    // Candidatos por ranura básica; el primero (null) deja la ranura vacía.
    private readonly List<MagicSpellSO> _basicCandidates = new() { null };
    private readonly MagicSpellSO[] _equipped = new MagicSpellSO[MagicCaster.BasicSlotCount];
    private readonly int[] _selection = new int[MagicCaster.BasicSlotCount];

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
        PlayerService.TryGetComponent(out _specialCharge, allowSceneLookup: false);   // del grupo, INC-484
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
            if (spell.slotType == SpellSlotType.SpecialOnly || !GrimorioDelPersonaje.EsDelGrimorio(spell)) continue;
            _basicCandidates.Add(spell);
        }

        // Empieza con lo que ya lleva Will; si no lleva nada, con los primeros hechizos de la lista.
        var current = _caster.BasicSpells;
        bool any = false;
        for (int i = 0; i < _equipped.Length; i++)
        {
            _equipped[i] = i < current.Count ? current[i] : null;
            any |= _equipped[i] != null;
        }
        if (!any)
            for (int i = 0; i < _equipped.Length && i + 1 < _basicCandidates.Count; i++)
                _equipped[i] = _basicCandidates[i + 1];
        SyncSelectionIndices();
        ApplyLoadout();

        RefillTestResources();
        AprenderGrimorioEntero();   // en el laboratorio todo viene aprendido (INC-494)
        _initialized = true;
        SetOpen(true);
    }

    private void Update()
    {
        if (!_initialized) return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            SetOpen(!_isOpen);

        if (_isOpen) return;

        if (GamepadInputReader.AttackMagicLeftPressed)
            ReportMagicInput("X / clic izquierdo");
    }

    private void ReportMagicInput(string inputName)
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
            var spell = _caster.ActiveBasic;
            bool ready = _caster.CanCast(spell, out string reason);
            string name = spell != null ? spell.GetLocalizedName() : "sin hechizo";
            if (ready)
                _status = $"{inputName} · {name} listo.";
            else
                _status = $"{inputName} · {name}: {reason}.";
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
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
            var active = _caster.ActiveBasic;
            GUI.Label(new Rect(x + 14f, 38f, PanelWidth - 28f, 18f),
                $"Activo (X): {(active != null ? active.GetLocalizedName() : "—")}  ·  LB: siguiente");
            GUI.Label(new Rect(x + 14f, 55f, PanelWidth - 28f, 18f),
                $"Serie: golpe {_caster.NextSeriesStep + 1} de 3  ·  M o clic central: editar");
            GUI.Label(new Rect(x + 14f, 72f, PanelWidth - 28f, 20f), _status);
            return;
        }

        GUI.Box(new Rect(x, 18f, PanelWidth, PanelHeight), "COMBAT LAB · HECHIZOS Y HABILIDADES");
        GUI.Label(new Rect(x + 16f, 48f, PanelWidth - 32f, 34f),
            "Magia y escudo habilitados solo aquí. Hechizos y desbloqueos no modifican tu perfil ni la partida.");

        float y = 89f;
        for (int i = 0; i < _equipped.Length; i++, y += RowHeight)
            DrawSlotRow(x, y, i, $"BÁSICO {i + 1}");

        if (GUI.Button(new Rect(x + 16f, y + 4f, 175f, 28f), "Recargar maná y carga"))
            RefillTestResources();
        if (GUI.Button(new Rect(x + 199f, y + 4f, 175f, 28f), "Aprender grimorio entero"))
            AprenderGrimorioEntero();
        y += 36f;

        GUI.Label(new Rect(x + 16f, y, PanelWidth - 32f, 22f),
            $"HABILIDADES DE PRUEBA · Maná: {GetManaReadout()}");
        bool canEditAbilities = _actionManager != null;
        GUI.enabled = canEditAbilities;
        DrawAbilityToggle(new Rect(x + 16f, y + 24f, 108f, 30f), "Magia", 0);
        DrawAbilityToggle(new Rect(x + 132f, y + 24f, 108f, 30f), "Salto", 1);
        DrawAbilityToggle(new Rect(x + 248f, y + 24f, 126f, 30f), "Vuelo", 2);
        GUI.enabled = true;
        y += 68f;

        if (GUI.Button(new Rect(x + 16f, y, 358f, 26f), "Cerrar panel y probar habilidades"))
            SetOpen(false);
        GUI.Label(new Rect(x + 16f, y + 30f, PanelWidth - 32f, 96f),
            "En juego: X / clic izq. = serie de tres; mantener = preciso. LB / rueda abajo = siguiente básico. " +
            "Y / Q = combo (teclea la secuencia con A B X Y). B / clic der.: en el momento justo = contraataque; mantener = escudo. LT+RT / Ctrl = ataque de equipo con quien esté cerca (uno: dúo, 1 tramo; los dos y carga llena: trío). " +
            "ESPACIO salta. M abre/cierra.");
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

    /// Las habilidades se guardan también en el preset de la sesión: si no, la siguiente
    /// re-aplicación del preset (p. ej. equipar en el menú de Start) las devolvería a lo de antes.
    private void ApplyLabAbilities(bool magic, bool jump, bool fly)
    {
        var abilities = new PlayerAbilities
        {
            swim = _actionManager.AllowSwim,
            jump = jump,
            climb = _actionManager.AllowClimb,
            fly = fly,
            sprint = _actionManager.AllowSprint,
            magic = magic,
            shield = _actionManager.AllowShield
        };
        _actionManager.ApplyAbilities(abilities);

        var preset = UnlockService.GetActivePreset();
        if (preset != null) preset.abilities = abilities;

        _status = $"Habilidades de prueba: Magia {(magic ? "SI" : "NO")}, Salto {(jump ? "SI" : "NO")}, Vuelo {(fly ? "SI" : "NO")}.";
    }

    private void DrawSlotRow(float x, float y, int slotIndex, string label)
    {
        bool isActive = _caster.ActiveBasicIndex == slotIndex && _equipped[slotIndex] != null;
        GUI.Label(new Rect(x + 16f, y + 5f, 80f, 24f), isActive ? label + " ▶" : label);

        if (GUI.Button(new Rect(x + 96f, y, 30f, 28f), "‹"))
            SelectRelative(slotIndex, -1);

        string spellName = _equipped[slotIndex] != null ? _equipped[slotIndex].GetLocalizedName() : "Vacío";
        GUI.Label(new Rect(x + 130f, y + 4f, 200f, 22f), spellName);

        if (GUI.Button(new Rect(x + 334f, y, 40f, 28f), "›"))
            SelectRelative(slotIndex, 1);
    }

    private void SelectRelative(int slotIndex, int direction)
    {
        int count = _basicCandidates.Count;
        int index = (_selection[slotIndex] + direction + count) % count;
        _selection[slotIndex] = index;
        _equipped[slotIndex] = _basicCandidates[index];
        ApplyLoadout();
        _status = $"Básico {slotIndex + 1}: {(_equipped[slotIndex] != null ? _equipped[slotIndex].GetLocalizedName() : "vacío")}";
    }

    /// Equipa los básicos en Will y los apunta en el preset de la sesión, para que el menú de
    /// Start enseñe lo mismo y una re-aplicación del preset no los cambie.
    private void ApplyLoadout()
    {
        _caster.SetBasicSpells(_equipped);

        var preset = UnlockService.GetActivePreset();
        if (preset == null) return;
        preset.basicSpellIds ??= new List<SpellId>();
        preset.basicSpellIds.Clear();
        for (int i = 0; i < _equipped.Length; i++)
            if (_equipped[i] != null && !preset.basicSpellIds.Contains(_equipped[i].spellId))
                preset.basicSpellIds.Add(_equipped[i].spellId);
    }

    private void SyncSelectionIndices()
    {
        for (int i = 0; i < _equipped.Length; i++)
            _selection[i] = FindIndex(_basicCandidates, _equipped[i]);
    }

    /// Aprende todos los hechizos de la biblioteca, de Will, Estela y Liam, como si el grimorio
    /// estuviera completo: básicos para equipar desde el menú de Start y combos para la Y. No
    /// equipa nada (lo equipado se elige en el menú o en este panel). Solo toca el preset de la
    /// sesión, que es una copia en memoria (INC-494).
    private void AprenderGrimorioEntero()
    {
        if (_presetService == null || _presetService.SpellLibrary == null || _presetService.SpellLibrary.Spells == null) return;
        int n = 0;
        foreach (var spell in _presetService.SpellLibrary.Spells)
        {
            if (!GrimorioDelPersonaje.EsDelGrimorio(spell)) continue;
            UnlockService.UnlockSpell(spell.spellId, assignToEmptySlot: false);
            n++;
        }
        _status = n > 0 ? $"Grimorio entero: {n} hechizos. Equípalos en Start ▸ Hechizos." : "La biblioteca de hechizos está vacía.";
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

        // La carga de equipo es del grupo, que puede registrarse después que el panel (INC-491).
        if (_specialCharge == null) PlayerService.TryGetComponent(out _specialCharge, allowSceneLookup: true);
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

}
