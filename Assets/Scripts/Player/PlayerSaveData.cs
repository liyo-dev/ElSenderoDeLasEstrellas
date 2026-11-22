using System;
using System.Collections.Generic;
using UnityEngine;
using Liyodev.NarrativeGraph;

[Serializable]
public class PlayerSaveData
{
    public string lastSpawnAnchorId;

    /// <summary>
    /// Clave de texto del lugar donde se guardó (la del farol o la de la zona en que estaba el
    /// jugador). La enseña la ranura del menú; vacía en partidas anteriores (se cae al último
    /// lugar conocido).
    /// </summary>
    public string lugarDelGuardado;

    /// <summary>
    /// Dónde estaba el jugador en un auto-guardado: punto del NavMesh, rumbo y escena del suelo que
    /// pisaba. Al cargar se aparece ahí si la escena está cargada y el punto sigue valiendo; si no,
    /// en <see cref="lastSpawnAnchorId"/>. Los guardados en un punto de guardado no la llevan.
    /// </summary>
    [Serializable]
    public struct PosicionGuardada
    {
        public bool valida;
        public Vector3 posicion;
        public float rumbo;
        public string escena;
    }

    public PosicionGuardada posicionDeAutoguardado;

    public int level;
    public float maxHp, currentHp;
    public float maxMp, currentMp;
    public float ataque, defensa; // INC-470; 0 en partidas anteriores (se inicializan al cargar)

    // Sintonía Estelar (INC-847)
    public int sintoniaPuntos;
    public int sintoniaHitos;
    public int sintoniaHitosPendientes;

    // Hechizos/skills
    public List<AbilityId> abilities = new();
    public List<SpellId>   spells    = new();
    public List<string>    flags     = new(); // misiones/estados simples
    public List<InventoryItemSave> inventory = new();
    public List<string> defeatedBossIds = new();
    public List<string> senalesEmitidas = new();
    public List<AppearanceEntry> appearance = new();
    public List<string> unlockedWardrobeIds = new();
    public List<string> consumedInteractables = new();
    public List<ObjetoPersistente.Estado> objetosDelMundo = new(); // INC-540
    public List<string> seenLorePopupIds = new();
    
    // === NUEVO: narrativas interactivas completadas ===
    // Permite persistir qué narrativas de un solo uso ya se han ejecutado
    public List<string> completedInteractiveNarratives = new();
    
    // === NUEVO: miembros del equipo (party) ===
    // IDs narrativos de los NPCs que están en el equipo del jugador
    public List<string> partyMemberIds = new();
    
    public int activeCharacterSlot = 1; // 1 = Will por defecto
    
    // === NUEVO: puntos de teletransporte desbloqueados ===
    public List<string> unlockedTeleportPoints = new();
    
    [Serializable]
    public struct NarrativeBlackboardSnapshot
    {
        public string graphLabel;
        public List<SimpleBlackboard.Entry> blackboardData;
    }
    
    public List<NarrativeBlackboardSnapshot> narrativeBlackboards = new();
    
    [Serializable]
    public struct NpcPosEntry
    {
        public string npcId;
        public Vector3 position;
        public Quaternion rotation;
        public bool hasRotation; // Ver comentario en PlayerPresetSO.NpcPosEntry
        public bool hasActiveState;
        public bool isActive;
    }

    // Posiciones persistidas de NPCs (reflejadas desde/hacia el PlayerPresetSO)
    public List<NpcPosEntry> npcPositions = new();

    // Vínculos sociales forjados en runtime entre NPCs (reflejados desde/hacia NPCRelationshipRegistry
    // vía PlayerPresetSO.npcRelationships). Se reutiliza NPCRelationshipRegistry.SaveEntry directamente.
    public List<NPCRelationshipRegistry.SaveEntry> npcRelationships = new();

    // Hechizos básicos equipados, en orden de rotación (hasta cuatro).
    public List<SpellId> basicSpellIds = new();
    public List<PlayerPresetSO.BasicosDeCompanero> companionBasics = new(); // INC-503
    public List<PlayerPresetSO.EstadoDeCompanero> companionHealth = new(); // INC-717

    // Ranuras del sistema anterior: solo se leen para convertir partidas guardadas antes de
    // INC-486 (GameBootProfile). No se escriben.
    public SpellId leftSpellId  = SpellId.None;
    public SpellId rightSpellId = SpellId.None;
    public SpellId specialSpellId = SpellId.None;

    // Permisos de movimiento y magia. En un guardado que no los tenga quedan en false.
    public bool canSwim;
    public bool canJump;
    public bool canFly;
    public bool canMagic;
    public bool canSprint;
    public bool canShield;
}
