using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La partida en curso: el preset en memoria que todos los sistemas leen y escriben mientras se
/// juega, y las operaciones sobre él (guardar, cargar, partida nueva, volcar el estado del juego).
///
/// <see cref="GameBootProfile"/> solo aporta la configuración (preset por defecto, preset de
/// pruebas, auto-guardados); <see cref="GameBootService"/> decide cuándo se prepara cada cosa.
/// El preset en memoria es una copia: nunca se escribe en un asset del proyecto.
/// </summary>
public static partial class Partida
{
    private const string AnclaPorDefecto = "Bedroom";

    private static PlayerPresetSO s_preset;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_preset = null; }
#endif

    private static GameBootProfile Config => GameBootService.Profile;

    /// <summary>
    /// Preset de la partida en curso. Si todavía no hay ninguno, se prepara desde el preset de
    /// pruebas (si está activo) o desde el preset por defecto. Null solo si no hay configuración.
    /// </summary>
    public static PlayerPresetSO Preset
    {
        get
        {
            if (s_preset) return s_preset;
            var config = Config;
            if (config == null) return null;
            CargarDesdePlantilla(config.ShouldBootFromPreset() ? config.bootPreset : config.defaultPlayerPreset);
            return s_preset;
        }
    }

    /// <summary>Preset en memoria tal cual, sin prepararlo si falta (para inspectores y depuración).</summary>
    public static PlayerPresetSO PresetEnMemoria => s_preset;

    /// <summary>Ancla donde aparece el jugador al empezar.</summary>
    public static string AnclaDeInicio
    {
        get
        {
            var ancla = Preset != null ? Preset.spawnAnchorId : null;
            return string.IsNullOrEmpty(ancla) ? AnclaPorDefecto : ancla;
        }
    }

    /// <summary>Garantiza que existe el preset en memoria (vacío si se crea ahora).</summary>
    public static PlayerPresetSO AsegurarPreset()
    {
        if (!s_preset)
        {
            s_preset = ScriptableObject.CreateInstance<PlayerPresetSO>();
            s_preset.name = "Partida en curso";
            s_preset.hideFlags = HideFlags.DontSave;
        }
        return s_preset;
    }

    /// <summary>Sustituye el preset en memoria por una copia de <paramref name="plantilla"/>.</summary>
    public static void CargarDesdePlantilla(PlayerPresetSO plantilla)
    {
        var p = AsegurarPreset();
        if (plantilla) CopiarPreset(plantilla, p);
        else VaciarPreset(p);
    }

    /// <summary>
    /// Deja la parte narrativa de la sesión como recién arrancada: para los grafos, borra sus
    /// pizarras en memoria y olvida las señales emitidas. Las pizarras de la partida se vuelven a
    /// restaurar desde el preset al arrancar los grafos de la escena.
    /// </summary>
    public static void ReiniciarSesionNarrativa()
    {
        var hub = NarrativeGraphHub.Instance;
        if (hub != null)
        {
            hub.StopAllRunners();
            hub.ClearAllBlackboards();
        }
        DefaultNarrativeSignals.Instance?.OlvidarSenalesDePartida();
        Game.NPC.Modules.NPCInteractiveNarrativeRegistry.Clear(fullClear: true);
    }

    private static void VaciarPreset(PlayerPresetSO p)
    {
        p.spawnAnchorId = AnclaPorDefecto;
        p.level = 1;
        p.maxHP = 100f; p.currentHP = 100f;
        p.maxMP = 50f; p.currentMP = 50f;
        p.ataque = 0f; p.defensa = 0f;
        p.sintoniaPuntos = 0; p.sintoniaHitos = 0; p.sintoniaHitosPendientes = 0;
        p.unlockedAbilities = new List<AbilityId>();
        p.unlockedSpells = new List<SpellId>();
        p.basicSpellIds = new List<SpellId>();
        p.companionBasics = new List<PlayerPresetSO.BasicosDeCompanero>();
        p.companionHealth = new List<PlayerPresetSO.SaludDeCompanero>();
        p.flags = new List<string>();
        p.appearance = new List<AppearanceEntry>();
        p.unlockedWardrobeIds = new List<string>();
        p.inventoryItems = new List<InventoryItemSave>();
        p.defeatedBossIds = new List<string>();
        p.senalesEmitidas = new List<string>();
        p.consumedInteractableIds = new List<string>();
        p.objetosDelMundo = new List<ObjetoPersistente.Estado>();
        p.completedInteractiveNarratives = new List<string>();
        p.seenLorePopupIds = new List<string>();
        p.partyMemberIds = new List<string>();
        p.activeCharacterSlot = 1;
        p.unlockedTeleportPoints = new List<string>();
        p.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        p.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
        p.npcRelationships = new List<NPCRelationshipRegistry.SaveEntry>();
        p.abilities = new PlayerAbilities();
    }
}
