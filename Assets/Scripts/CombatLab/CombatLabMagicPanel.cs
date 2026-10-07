using System.Collections.Generic;
using UnityEngine;
using Core;

/// <summary>
/// Pestaña «Magia» del panel del LAB: selector de hechizos exclusivo del laboratorio. Cambia el loadout en runtime y no escribe
/// en PlayerPresetSO ni en la partida guardada. Al entrar aprende el grimorio entero (todos los
/// hechizos de todos los personajes) en el preset de la sesión (copia en memoria), para poder
/// equipar cualquiera desde el menú de Start.
/// </summary>
public sealed class CombatLabMagicPanel : MonoBehaviour, ISeccionDelLab
{
    // Candidatos por ranura básica; el primero (null) deja la ranura vacía.
    private readonly List<MagicSpellSO> _basicCandidates = new() { null };
    private readonly MagicSpellSO[] _equipped = new MagicSpellSO[MagicCaster.BasicSlotCount];
    private readonly int[] _selection = new int[MagicCaster.BasicSlotCount];

    private MagicCaster _caster;
    private PlayerActionManager _actionManager;
    private PlayerPresetService _presetService;
    private ManaPool _manaPool;
    private SpecialChargeMeter _specialCharge;
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
    }

    private void Update()
    {
        if (!_initialized || PanelDelLab.Abierto) return;
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

    public string Titulo => "Magia";
    public int Orden => 40;

    public void Dibujar()
    {
        if (!_initialized)
        {
            GUILayout.Label(string.IsNullOrEmpty(_status) ? "Preparando la magia…" : _status, EstiloDelLab.Nota);
            return;
        }
        GUILayout.Label("Hechizos básicos de Will (los cambias también en Start ▸ Hechizos). Nada de esto toca tu partida.", EstiloDelLab.Etiqueta);
        for (int i = 0; i < _equipped.Length; i++) DrawSlotRow(i);

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Recargar maná y carga de equipo", EstiloDelLab.Boton)) RefillTestResources();
        if (GUILayout.Button("Aprender el grimorio entero", EstiloDelLab.Boton)) AprenderGrimorioEntero();
        GUILayout.EndHorizontal();

        GUILayout.Label($"Habilidades · Maná {GetManaReadout()}", EstiloDelLab.Titulo);
        if (_actionManager != null)
        {
            GUILayout.BeginHorizontal();
            DrawAbilityToggle("Magia", 0);
            DrawAbilityToggle("Salto", 1);
            DrawAbilityToggle("Vuelo", 2);
            GUILayout.EndHorizontal();
        }
        if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status, EstiloDelLab.Nota);
    }

    private void DrawAbilityToggle(string label, int abilityIndex)
    {
        bool enabled = abilityIndex switch
        {
            0 => _actionManager.AllowMagic,
            1 => _actionManager.AllowJump,
            _ => _actionManager.AllowFly
        };

        if (EstiloDelLab.Opcion($"{label}: {(enabled ? "Sí" : "No")}", enabled))
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

    private void DrawSlotRow(int slotIndex)
    {
        bool isActive = _caster.ActiveBasicIndex == slotIndex && _equipped[slotIndex] != null;
        GUILayout.BeginHorizontal();
        GUILayout.Label(isActive ? $"Básico {slotIndex + 1} ▶" : $"Básico {slotIndex + 1}", EstiloDelLab.Etiqueta, GUILayout.Width(100f));
        if (GUILayout.Button("‹", EstiloDelLab.Boton, GUILayout.Width(40f))) SelectRelative(slotIndex, -1);
        string spellName = _equipped[slotIndex] != null ? _equipped[slotIndex].GetLocalizedName() : "Vacío";
        GUILayout.Label(spellName, EstiloDelLab.Etiqueta, GUILayout.Width(220f));
        if (GUILayout.Button("›", EstiloDelLab.Boton, GUILayout.Width(40f))) SelectRelative(slotIndex, 1);
        GUILayout.EndHorizontal();
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

    private static int FindIndex(List<MagicSpellSO> list, MagicSpellSO spell)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == spell) return i;
        return 0;
    }

}
