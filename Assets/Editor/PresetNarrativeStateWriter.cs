using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Escribe el estado proyectado del grafo (NarrativeStateProjector) en un PlayerPresetSO, con las
/// mismas convenciones de flags que usa el guardado: QUEST_ACTIVE:{id}, QUEST_STEP_DONE:{id}:{paso}
/// y QUEST_COMPLETED:{id}. Implementa también IPlayerLoadoutState (habilidades y hechizos).
///
/// Las colocaciones de actores se acumulan en orden y se vuelcan a npcPositions con
/// ApplyActorPlacements, porque para convertir un lugar en posición hace falta la escena. Manda la
/// última que se pueda resolver en las escenas abiertas.
public sealed class PresetNarrativeStateWriter : INarrativeStateWriter, IPlayerLoadoutState
{
    readonly PlayerPresetSO _dst;
    readonly Dictionary<string, List<NarrativeLocation>> _placements = new();
    readonly List<string> _placementOrder = new();
    readonly Dictionary<string, bool> _activeStates = new();
    Dictionary<string, QuestData> _questCatalog;

    public readonly List<string> Notes = new();

    public PresetNarrativeStateWriter(PlayerPresetSO dst)
    {
        _dst = dst;
        _dst.flags ??= new List<string>();
        _dst.inventoryItems ??= new List<InventoryItemSave>();
        _dst.unlockedSpells ??= new List<SpellId>();
        _dst.abilities ??= new PlayerAbilities();
        _dst.npcPositions ??= new List<PlayerPresetSO.NpcPosEntry>();
    }

    // ── INarrativeStateWriter ────────────────────────────────────────────────

    public bool GetFlag(string key) => _dst.flags.Contains(key);

    public void SetFlag(string key, bool value)
    {
        if (value) { if (!_dst.flags.Contains(key)) _dst.flags.Add(key); }
        else _dst.flags.Remove(key);
    }

    public void StartQuest(string questId)
    {
        if (!GetFlag($"QUEST_COMPLETED:{questId}")) SetFlag($"QUEST_ACTIVE:{questId}", true);
    }

    public void CompleteQuestSteps(string questId, IReadOnlyList<string> stepConditionIds, IReadOnlyList<int> stepIndices)
    {
        StartQuest(questId);

        if (stepConditionIds != null && stepConditionIds.Count > 0)
        {
            var qd = FindQuestData(questId);
            if (qd == null || qd.steps == null)
            {
                Note($"No se encontró el QuestData de '{questId}': no se aplican sus pasos por Condition ID.");
                return;
            }
            foreach (var conditionId in stepConditionIds)
            {
                if (string.IsNullOrWhiteSpace(conditionId)) continue;
                int idx = System.Array.FindIndex(qd.steps, s => s.conditionId == conditionId);
                if (idx < 0) Note($"El Condition ID '{conditionId}' no existe en los pasos de '{questId}'.");
                else SetFlag($"QUEST_STEP_DONE:{questId}:{idx}", true);
            }
        }
        else if (stepIndices != null)
        {
            foreach (var idx in stepIndices)
                if (idx >= 0) SetFlag($"QUEST_STEP_DONE:{questId}:{idx}", true);
        }
    }

    public void CompleteQuest(string questId) => SetFlag($"QUEST_COMPLETED:{questId}", true);

    public void AddItem(string itemId, int amount)
    {
        int idx = _dst.inventoryItems.FindIndex(e => e.itemId == itemId);
        if (idx >= 0)
        {
            var entry = _dst.inventoryItems[idx];
            entry.count += amount;
            _dst.inventoryItems[idx] = entry;
        }
        else _dst.inventoryItems.Add(new InventoryItemSave { itemId = itemId, count = amount });
    }

    public void PlaceActor(string actorId, NarrativeLocation location)
    {
        if (string.IsNullOrWhiteSpace(actorId) || !location.IsValid) return;
        if (!_placements.TryGetValue(actorId, out var history))
        {
            _placements[actorId] = history = new List<NarrativeLocation>();
            _placementOrder.Add(actorId);
        }
        history.Add(location);
    }

    public void SetActorActive(string actorId, bool active)
    {
        if (!string.IsNullOrWhiteSpace(actorId)) _activeStates[actorId] = active;
    }

    public void Note(string message) => Notes.Add(message);

    public bool TryGetExtension<T>(out T extension) where T : class
    {
        extension = this as T;
        return extension != null;
    }

    // ── IPlayerLoadoutState ─────────────────────────────────────────────────

    public void UnlockAbility(AbilityKey ability)
    {
        switch (ability)
        {
            case AbilityKey.Swim: _dst.abilities.swim = true; break;
            case AbilityKey.Jump: _dst.abilities.jump = true; break;
            case AbilityKey.Climb: _dst.abilities.climb = true; break;
            case AbilityKey.Magic: _dst.abilities.magic = true; break;
            case AbilityKey.Fly: _dst.abilities.fly = true; break;
            case AbilityKey.Sprint: _dst.abilities.sprint = true; break;
            case AbilityKey.Shield: _dst.abilities.shield = true; break;
        }
    }

    public void UnlockSpell(SpellId spell, bool equipInEmptySlot)
    {
        if (!_dst.unlockedSpells.Contains(spell)) _dst.unlockedSpells.Add(spell);
        if (!equipInEmptySlot) return;
        _dst.basicSpellIds ??= new List<SpellId>();
        if (_dst.basicSpellIds.Contains(spell) || _dst.basicSpellIds.Count >= MagicCaster.BasicSlotCount) return;
        _dst.basicSpellIds.Add(spell);
    }

    // ── Actores ─────────────────────────────────────────────────────────────

    /// Vuelca a npcPositions dónde queda cada actor. Devuelve una línea por actor para mostrarla.
    /// El jugador se omite: su aparición la decide el Spawn Anchor de Quick Test.
    public List<string> ApplyActorPlacements(OpenScenesNarrativeWorld world)
    {
        var summary = new List<string>();
        foreach (var actorId in _placementOrder)
        {
            var history = _placements[actorId];
            var last = history[history.Count - 1];
            if (actorId == SequenceActor.PlayerId)
            {
                summary.Add($"Jugador → {last} (no se aplica: aparece en el Spawn Anchor elegido).");
                continue;
            }
            if (!world.TryGetActorObjectName(actorId, out var objectName))
            {
                Note($"'{actorId}' debería estar en {last}, pero no está ni en las escenas abiertas ni en ningún roster de NPCs.");
                continue;
            }

            int used = history.Count - 1;
            Vector3 position = default;
            Quaternion rotation = Quaternion.identity;
            while (used >= 0 && !world.TryResolve(history[used], out position, out rotation)) used--;
            if (used < 0)
            {
                Note($"'{actorId}' debería estar en {last}, pero ninguna de sus marcas está en las escenas abiertas.");
                continue;
            }
            if (used < history.Count - 1)
                Note($"'{actorId}' debería estar en {last}, que no está en las escenas abiertas; se usa {history[used]}.");
            var location = history[used];

            _dst.npcPositions.RemoveAll(e => e.npcId == objectName);
            var entry = new PlayerPresetSO.NpcPosEntry
            {
                npcId = objectName,
                position = position,
                rotation = rotation,
                hasRotation = true,
            };
            if (_activeStates.TryGetValue(actorId, out var active))
            {
                entry.hasActiveState = true;
                entry.isActive = active;
            }
            _dst.npcPositions.Add(entry);
            summary.Add($"{actorId} → {location}");
        }

        foreach (var pair in _activeStates)
            if (!_placements.ContainsKey(pair.Key))
                Note($"'{pair.Key}' queda {(pair.Value ? "activo" : "inactivo")}, pero sin posición no se puede guardar en el preset.");

        return summary;
    }

    QuestData FindQuestData(string questId)
    {
        if (_questCatalog == null)
        {
            _questCatalog = new Dictionary<string, QuestData>();
            foreach (var g in AssetDatabase.FindAssets("t:QuestData"))
            {
                var qd = AssetDatabase.LoadAssetAtPath<QuestData>(AssetDatabase.GUIDToAssetPath(g));
                if (qd != null && !string.IsNullOrEmpty(qd.questId) && !_questCatalog.ContainsKey(qd.questId))
                    _questCatalog[qd.questId] = qd;
            }
        }
        _questCatalog.TryGetValue(questId, out var result);
        return result;
    }
}
