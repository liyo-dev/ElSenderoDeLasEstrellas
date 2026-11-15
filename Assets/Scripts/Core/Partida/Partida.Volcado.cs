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

        // Las habilidades de cada personaje ya están en el preset (las escribe quien las
        // desbloquea, UnlockService); el cuerpo solo las refleja.

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

    /// El preset guarda la apariencia de Will, vaya quien vaya al mando, leída de su cuerpo y sin
    /// atuendos de escena (con uno puesto, la ropa de debajo).
    private static void VolcarApariencia(PlayerPresetSO p)
    {
        var registro = CharacterAppearanceRegistry.Instance;
        if (registro == null) return;
        // Sin la apariencia de Will a mano, mejor no tocar lo guardado.
        if (registro.RopaDeDebajo(PartyControlManager.CharacterSlot.Will).Count == 0) return;
        p.appearance = registro.GetAppearanceAsList(PartyControlManager.CharacterSlot.Will);
    }
}
