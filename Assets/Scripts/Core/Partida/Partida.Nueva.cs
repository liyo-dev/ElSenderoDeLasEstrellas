using System;
using System.Collections.Generic;

/// Partida nueva: borra el guardado y deja el preset y los sistemas como al empezar el juego.
public static partial class Partida
{
    public static void PrepararNueva(SaveSystem guardado)
    {
        if (guardado) guardado.Delete();

        CargarDesdePlantilla(Config != null ? Config.defaultPlayerPreset : null);
        var p = s_preset;

        // La magia empieza bloqueada aunque el preset por defecto traiga restos de pruebas.
        p.abilities ??= new PlayerAbilities();
        p.abilities.magic = false;
        p.unlockedAbilities ??= new List<AbilityId>();
        p.unlockedAbilities.Remove(AbilityId.MagicAttack);
        p.unlockedSpells = new List<SpellId>();
        p.basicSpellIds = new List<SpellId>();
        p.maxMP = 0f;
        p.currentMP = 0f;

        // Nada de la partida anterior: NPCs, cinemáticas vistas, pizarras, narrativas, avisos, grupo.
        p.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
        p.flags ??= new List<string>();
        p.flags.RemoveAll(f => !string.IsNullOrEmpty(f) && f.StartsWith("CINEMATIC_SEEN:", StringComparison.OrdinalIgnoreCase));
        p.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        p.senalesEmitidas = new List<string>();
        p.completedInteractiveNarratives = new List<string>();
        p.seenLorePopupIds = new List<string>();
        p.unlockedTeleportPoints = new List<string>();
        p.partyMemberIds = new List<string>();

        if (BossProgressTracker.TryGetInstance(out var jefes)) jefes.LoadFromSnapshot(p.defeatedBossIds);
        QuestManager.Instance?.ResetAllQuests();
        TeleportRegistry.Clear();
        ReiniciarSesionNarrativa();

        GameBootProfileDebugger.Log("Nueva partida", "Preset y sistemas reiniciados", UnityEngine.LogType.Log);
    }
}
