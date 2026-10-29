using System.Collections.Generic;
using UnityEngine;

/// Paso de datos entre el preset de la partida y el archivo de guardado (<see cref="PlayerSaveData"/>).
public static partial class Partida
{
    /// <summary>Copia profunda de todos los campos serializados de un preset en otro.</summary>
    private static void CopiarPreset(PlayerPresetSO origen, PlayerPresetSO destino)
    {
        if (!origen || !destino || origen == destino) return;
        string nombre = destino.name;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(origen), destino);
        destino.name = nombre;
    }

    /// <summary>Junta las entradas repetidas del inventario y descarta las vacías.</summary>
    private static List<InventoryItemSave> SanearInventario(IEnumerable<InventoryItemSave> origen)
    {
        var sumas = new Dictionary<string, int>();
        if (origen != null)
        {
            foreach (var e in origen)
            {
                if (string.IsNullOrWhiteSpace(e.itemId) || e.count <= 0) continue;
                sumas.TryGetValue(e.itemId, out var actual);
                sumas[e.itemId] = actual + e.count;
            }
        }
        var lista = new List<InventoryItemSave>(sumas.Count);
        foreach (var kv in sumas) lista.Add(new InventoryItemSave { itemId = kv.Key, count = kv.Value });
        return lista;
    }

    private static List<T> Copia<T>(List<T> origen) => origen != null ? new List<T>(origen) : new List<T>();

    /// <summary>Vuelca un guardado en el preset de la partida (sustituye lo que hubiera).</summary>
    public static void CargarDesdeGuardado(PlayerSaveData d)
    {
        if (d == null) return;
        var p = AsegurarPreset();

        p.level = d.level;
        p.maxHP = d.maxHp; p.currentHP = Mathf.Clamp(d.currentHp, 0f, d.maxHp);
        p.maxMP = d.maxMp; p.currentMP = Mathf.Clamp(d.currentMp, 0f, d.maxMp);
        p.ataque = d.ataque; p.defensa = d.defensa;
        p.sintoniaPuntos = d.sintoniaPuntos;
        p.sintoniaHitos = d.sintoniaHitos;
        p.sintoniaHitosPendientes = d.sintoniaHitosPendientes;
        p.unlockedAbilities = Copia(d.abilities);
        p.unlockedSpells = Copia(d.spells);
        p.flags = Copia(d.flags);
        p.appearance = Copia(d.appearance);
        p.unlockedWardrobeIds = Copia(d.unlockedWardrobeIds);
        p.inventoryItems = SanearInventario(d.inventory);
        p.defeatedBossIds = Copia(d.defeatedBossIds);
        p.narrativeBlackboards = Copia(d.narrativeBlackboards);
        p.senalesEmitidas = Copia(d.senalesEmitidas);
        p.consumedInteractableIds = Copia(d.consumedInteractables);
        p.objetosDelMundo = Copia(d.objetosDelMundo);
        p.seenLorePopupIds = Copia(d.seenLorePopupIds);
        p.completedInteractiveNarratives = Copia(d.completedInteractiveNarratives);
        p.partyMemberIds = Copia(d.partyMemberIds);
        p.activeCharacterSlot = d.activeCharacterSlot;
        p.unlockedTeleportPoints = Copia(d.unlockedTeleportPoints);
        p.npcRelationships = Copia(d.npcRelationships);
        p.companionBasics = PlayerPresetSO.BasicosDeCompanero.CopiarLista(d.companionBasics);
        p.companionHealth = PlayerPresetSO.SaludDeCompanero.CopiarLista(d.companionHealth);

        p.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
        if (d.npcPositions != null)
        {
            foreach (var e in d.npcPositions)
            {
                p.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
                {
                    npcId = e.npcId,
                    position = e.position,
                    rotation = e.rotation,
                    hasRotation = e.hasRotation,
                    hasActiveState = e.hasActiveState,
                    isActive = e.isActive
                });
            }
        }

        if (!string.IsNullOrEmpty(d.lastSpawnAnchorId)) p.spawnAnchorId = d.lastSpawnAnchorId;

        // Básicos: los del guardado que sigan desbloqueados. Un guardado anterior a INC-486 trae
        // ranuras izquierda/derecha; sin ninguno válido, el primer hechizo desbloqueado.
        var desbloqueados = p.unlockedSpells;
        var guardados = d.basicSpellIds != null && d.basicSpellIds.Count > 0
            ? d.basicSpellIds
            : new List<SpellId> { d.leftSpellId, d.rightSpellId };
        p.basicSpellIds = new List<SpellId>();
        foreach (var id in guardados)
        {
            if (id == SpellId.None || !desbloqueados.Contains(id) || p.basicSpellIds.Contains(id)) continue;
            if (p.basicSpellIds.Count >= MagicCaster.BasicSlotCount) break;
            p.basicSpellIds.Add(id);
        }
        if (p.basicSpellIds.Count == 0 && desbloqueados.Count > 0) p.basicSpellIds.Add(desbloqueados[0]);

        p.abilities ??= new PlayerAbilities();
        p.abilities.swim = d.canSwim;
        p.abilities.jump = d.canJump;
        p.abilities.climb = d.canClimb;
        p.abilities.fly = d.canFly;
        p.abilities.magic = d.canMagic;
    }

    /// <summary>Construye el guardado a partir del preset de la partida.</summary>
    private static PlayerSaveData AGuardado(PlayerPresetSO p)
    {
        if (!p) return GuardadoVacio();

        var d = new PlayerSaveData
        {
            lastSpawnAnchorId = SpawnManager.CurrentAnchorId ?? p.spawnAnchorId ?? AnclaPorDefecto,
            level = p.level,
            maxHp = p.maxHP, currentHp = p.currentHP,
            maxMp = p.maxMP, currentMp = p.currentMP,
            ataque = p.ataque, defensa = p.defensa,
            sintoniaPuntos = p.sintoniaPuntos,
            sintoniaHitos = p.sintoniaHitos,
            sintoniaHitosPendientes = p.sintoniaHitosPendientes,
            abilities = Copia(p.unlockedAbilities),
            spells = Copia(p.unlockedSpells),
            flags = Copia(p.flags),
            appearance = Copia(p.appearance),
            unlockedWardrobeIds = Copia(p.unlockedWardrobeIds),
            consumedInteractables = Copia(p.consumedInteractableIds),
            seenLorePopupIds = Copia(p.seenLorePopupIds),
            objetosDelMundo = Copia(p.objetosDelMundo),
            inventory = SanearInventario(p.inventoryItems),
            defeatedBossIds = Copia(p.defeatedBossIds),
            senalesEmitidas = Copia(p.senalesEmitidas),
            completedInteractiveNarratives = Copia(p.completedInteractiveNarratives),
            basicSpellIds = Copia(p.basicSpellIds),
            companionBasics = PlayerPresetSO.BasicosDeCompanero.CopiarLista(p.companionBasics),
            companionHealth = PlayerPresetSO.SaludDeCompanero.CopiarLista(p.companionHealth),
            narrativeBlackboards = Copia(p.narrativeBlackboards),
            partyMemberIds = Copia(p.partyMemberIds),
            activeCharacterSlot = p.activeCharacterSlot,
            unlockedTeleportPoints = Copia(p.unlockedTeleportPoints),
            npcRelationships = Copia(p.npcRelationships),
        };

        if (p.abilities != null)
        {
            d.canSwim = p.abilities.swim;
            d.canJump = p.abilities.jump;
            d.canClimb = p.abilities.climb;
            d.canFly = p.abilities.fly;
            d.canMagic = p.abilities.magic;
        }

        d.npcPositions = new List<PlayerSaveData.NpcPosEntry>();
        if (p.npcPositions != null)
        {
            foreach (var e in p.npcPositions)
            {
                d.npcPositions.Add(new PlayerSaveData.NpcPosEntry
                {
                    npcId = e.npcId,
                    position = e.position,
                    rotation = e.rotation,
                    hasRotation = e.hasRotation,
                    hasActiveState = e.hasActiveState,
                    isActive = e.isActive
                });
            }
        }
        return d;
    }

    /// <summary>Guardado de una partida sin empezar (para pantallas que necesitan uno de muestra).</summary>
    public static PlayerSaveData GuardadoVacio()
    {
        var plantilla = Config != null && Config.defaultPlayerPreset ? Config.defaultPlayerPreset : s_preset;
        return new PlayerSaveData
        {
            lastSpawnAnchorId = plantilla && !string.IsNullOrEmpty(plantilla.spawnAnchorId) ? plantilla.spawnAnchorId : AnclaPorDefecto,
            inventory = new List<InventoryItemSave>(),
            defeatedBossIds = new List<string>(),
            appearance = new List<AppearanceEntry>(),
        };
    }
}
