using System;
using System.Collections.Generic;
using UnityEngine;

/// Volcado del estado vivo del juego (vida, misiones, inventario, NPCs, grafos…) en el preset de
/// la partida, justo antes de guardar.
public static partial class Partida
{
    public static void VolcarEstadoDelJuego()
    {
        var p = AsegurarPreset();

        var ancla = SpawnManager.CurrentAnchorId;
        if (!string.IsNullOrEmpty(ancla)) p.spawnAnchorId = ancla;

        // La vida y el maná de los tres personajes los escribe su dueño (sin lo que suma el equipo).
        SaludDelGrupo.Instance?.VolcarEnPreset(p);

        VolcarMisiones(p);

        if (PlayerService.TryGetComponent<PlayerActionManager>(out var acciones, includeInactive: true, allowSceneLookup: true))
        {
            p.abilities ??= new PlayerAbilities();
            p.abilities.swim = acciones.AllowSwim;
            p.abilities.jump = acciones.AllowJump;
            p.abilities.climb = acciones.AllowClimb;
            p.abilities.fly = acciones.AllowFly;
            p.abilities.magic = acciones.AllowMagic;
        }

        p.inventoryItems = PlayerService.TryGetComponent<Inventory>(out var inventario, includeInactive: true, allowSceneLookup: true)
            ? SanearInventario(inventario.GetSaveSnapshot())
            : new List<InventoryItemSave>();

        p.unlockedWardrobeIds = PlayerService.TryGetComponent<WardrobeInventory>(out var armario, includeInactive: true, allowSceneLookup: true)
            ? armario.GetUnlockedIds()
            : new List<string>();

        VolcarApariencia(p);

        p.defeatedBossIds = BossProgressTracker.TryGetInstance(out var jefes) ? jefes.GetSnapshot() : new List<string>();

        CapturarPosicionesDeNpcs(p);
        ObjetoPersistente.CapturarEstado(p);

        p.narrativeBlackboards = NarrativeGraphHub.Instance != null
            ? NarrativeGraphHub.Instance.CaptureBlackboards()
            : new List<PlayerSaveData.NarrativeBlackboardSnapshot>();

        var senales = DefaultNarrativeSignals.Instance;
        if (senales != null) p.senalesEmitidas = new List<string>(senales.EverRaisedKeys);

        p.partyMemberIds = Game.NPC.PlayerParty.HasInstance
            ? Game.NPC.PlayerParty.Instance.GetMemberIdsForSave()
            : new List<string>();

        // El personaje al mando se lee aquí, al guardar: durante OnProfileReady todavía no es el real.
        if (PartyControlManager.Instance != null)
            p.activeCharacterSlot = PartyControlManager.Instance.ActiveIndex;

        p.unlockedTeleportPoints = TeleportRegistry.ToSaveData();
        p.npcRelationships = NPCRelationshipRegistry.ToSaveEntries();

        GameBootProfileDebugger.Log("Volcar", $"Ancla {p.spawnAnchorId}, vida {p.currentHP}/{p.maxHP}, maná {p.currentMP}/{p.maxMP}", LogType.Log);
    }

    /// Las marcas de misión (QUEST_*) se rehacen desde el QuestManager; el resto de marcas se conserva.
    private static void VolcarMisiones(PlayerPresetSO p)
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;
        var marcas = new List<string>(p.flags?.Count ?? 0);
        if (p.flags != null)
            foreach (var f in p.flags)
                if (!string.IsNullOrEmpty(f) && !f.StartsWith("QUEST_", StringComparison.Ordinal)) marcas.Add(f);
        qm.ExportFlags(marcas);
        p.flags = marcas;
    }

    /// El preset guarda siempre la apariencia de Will, vaya quien vaya al mando.
    private static void VolcarApariencia(PlayerPresetSO p)
    {
        if (!PlayerService.TryGetComponent<ModularAutoBuilder>(out var builder, includeInactive: true, allowSceneLookup: true))
            return;

        Dictionary<PartCategory, string> seleccion;
        var mando = PartyControlManager.Instance?.ActiveSlot ?? PartyControlManager.CharacterSlot.Will;
        if (mando == PartyControlManager.CharacterSlot.Will)
        {
            seleccion = builder.GetSelection();
        }
        else
        {
            seleccion = CharacterAppearanceRegistry.Instance?.GetAppearance(PartyControlManager.CharacterSlot.Will);
            // Sin la apariencia de Will a mano, mejor no tocar lo guardado que guardar la de otro.
            if (seleccion == null || seleccion.Count == 0) return;
        }
        if (seleccion == null) return;

        p.appearance = new List<AppearanceEntry>(seleccion.Count);
        foreach (var kv in seleccion)
            p.appearance.Add(new AppearanceEntry { category = kv.Key, partName = string.IsNullOrEmpty(kv.Value) ? null : kv.Value });
    }
}
