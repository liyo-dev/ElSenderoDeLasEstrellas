using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="El Sendero/Juego/Player Preset", fileName="PlayerPreset_Default")]
public class PlayerPresetSO : ScriptableObject
{
    [Header("Spawn")]
    [Tooltip("ID del anchor donde debe aparecer el jugador con este preset")]
    public string spawnAnchorId = "Bedroom";

    [Header("Stats")]
    public int   level = 1;
    public float maxHP = 100, currentHP = 100;
    public float maxMP = 50,  currentMP = 50;

    [Tooltip("Ataque y defensa del personaje, sin el equipo (la vida y la magia son maxHP y maxMP). Suben " +
             "al ganar combates. 0 = aún sin inicializar: se pone el valor inicial (ver EstadisticasDelPersonaje, INC-470).")]
    public float ataque, defensa;

    [Header("Desbloqueos")]
    [HideInInspector] public List<AbilityId> unlockedAbilities = new();
    public List<SpellId>   unlockedSpells    = new();

    [Header("Hechizos básicos equipados (X; se rotan con LB)")]
    [Tooltip("Hasta cuatro hechizos básicos, de cualquier elemento, en el orden en que se rotan con LB.")]
    public List<SpellId> basicSpellIds = new();

    /// Básicos equipados de Estela o Liam (INC-503). Sin entrada = los de su ficha.
    [Serializable]
    public class BasicosDeCompanero
    {
        public PartyControlManager.CharacterSlot personaje;
        public List<SpellId> hechizos = new();

        public BasicosDeCompanero Copia() =>
            new BasicosDeCompanero { personaje = personaje, hechizos = new List<SpellId>(hechizos ?? new List<SpellId>()) };

        public static List<BasicosDeCompanero> CopiarLista(List<BasicosDeCompanero> src)
        {
            var dst = new List<BasicosDeCompanero>();
            if (src != null) foreach (var e in src) if (e != null) dst.Add(e.Copia());
            return dst;
        }
    }

    [Tooltip("Básicos equipados de Estela y Liam (hasta cuatro cada uno). Vacío = los de su ficha.")]
    public List<BasicosDeCompanero> companionBasics = new();

    [Header("Flags (misiones/estados simples)")]
    public List<string> flags = new();

    // Usar la clase separada PlayerAbilities para evitar problemas de resolución entre archivos
    [Header("Abilities (Swim, Jump, Climb, Fly)")]
    public PlayerAbilities abilities = new PlayerAbilities();

    [Header("Apariencia")]
    [Tooltip("Aspecto visual del jugador. En defaultPreset define el look inicial, en runtime contiene el aspecto actual.")]
    public List<AppearanceEntry> appearance = new();

    [Header("Vestuario desbloqueado")]
    public List<string> unlockedWardrobeIds = new();

    [Header("Inventario")]
    public List<InventoryItemSave> inventoryItems = new();

    [Header("Progreso de bosses")]
    public List<string> defeatedBossIds = new();

    [Header("Estado de grafos narrativos")]
    public List<PlayerSaveData.NarrativeBlackboardSnapshot> narrativeBlackboards = new();

    [Serializable]
    public struct NpcPosEntry
    {
        public string npcId;        // normalizado: nombre del GameObject
        public Vector3 position;    // última posición persistida
        public Quaternion rotation; // última rotación persistida
        // FIX INC-028: antes se usaba "rotation == Quaternion.identity" como sentinel de "sin
        // persistir", pero identity ES una rotación válida (p.ej. Eldran en la taberna). Eso hacía
        // que al cargar la partida se descartara su rotación guardada y quedara con la del
        // prefab (de espaldas). Ahora se marca explícitamente si hay rotación persistida.
        public bool hasRotation;    // si se ha guardado explícitamente la rotación
        public bool hasActiveState; // si se ha guardado explícitamente el estado activo
        public bool isActive;       // si el NPC debe estar activo o no (solo válido si hasActiveState=true)
    }

    [Header("NPCs (persistencia opcional)")]
    [Tooltip("Lista de posiciones persistidas por NPC. El id es el nombre único del GameObject del NPC.")]
    public List<NpcPosEntry> npcPositions = new();

    [Header("Relaciones sociales dinámicas entre NPCs")]
    [Tooltip("Vínculos forjados en runtime entre NPCs al hablar entre ellos (WanderState/IdleState + " +
             "NPCSocialEncounterState). Se reutiliza directamente NPCRelationshipRegistry.SaveEntry en vez " +
             "de duplicar el struct — ver NPCRelationshipRegistry.cs.")]
    public List<NPCRelationshipRegistry.SaveEntry> npcRelationships = new();

    [Header("Objetos del mundo movidos o retirados")]
    [Tooltip("Objetos de escena que se han movido de su sitio o se han retirado (entregados, consumidos). " +
             "Lo escribe y lo aplica ObjetoPersistente.")]
    public List<ObjetoPersistente.Estado> objetosDelMundo = new();

    [Header("Interactuables consumidos (single-use)")]
    [Tooltip("IDs de Interactable marcados como singleUse que ya han sido consumidos.")]
    public List<string> consumedInteractableIds = new();

    [Header("Narrativas interactivas completadas")]
    [Tooltip("IDs de persistencia de narrativas interactivas de NPCs que ya se han ejecutado.")]
    public List<string> completedInteractiveNarratives = new();

    [Header("Popups de lore vistos (single-use)")]
    [Tooltip("IDs de persistencia de LorePopupEntry que ya se han mostrado.")]
    public List<string> seenLorePopupIds = new();

    [Header("Equipo (Party)")]
    [Tooltip("IDs narrativos de los NPCs que están en el equipo del jugador.")]
    public List<string> partyMemberIds = new();

    [Tooltip("ID del personaje activo actual (int) para CharacterSlot. Por defecto es 1 (Will).")]
    public int activeCharacterSlot = 1;

    [Header("Sistema de Teletransporte")]
    [Tooltip("IDs de los puntos de teletransporte desbloqueados por el jugador.")]
    public List<string> unlockedTeleportPoints = new();
}