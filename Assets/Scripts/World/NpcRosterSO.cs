using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// El "reparto": qué NPC va en cada <see cref="NpcSpawnPoint"/>, con qué prefab y con qué
/// identidad. Es un asset de TEXTO, así que se puede añadir, mover y configurar un NPC sin
/// abrir el Editor — que es justo lo que no se puede hacer hoy, porque MainWorld.unity está
/// guardada en binario.
///
/// Mismo patrón que <c>BattleEncounterSO</c> ya usa para los enemigos (prefab + perfiles de
/// spawn en un asset, no en la escena).
///
/// Ver claude/propuesta-sistema-spawn-npcs-por-datos-2026-09-16.md § 2.2
/// </summary>
[CreateAssetMenu(fileName = "NpcRoster", menuName = "Sendero/NPC/Npc Roster")]
public class NpcRosterSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Desactívalo para que esta entrada se ignore, sin tener que borrarla.")]
        public bool enabled = true;

        [Tooltip("Id del NpcSpawnPoint de la escena donde debe aparecer este NPC.")]
        public string spawnId;

        [Tooltip("Prefab del NPC a instanciar.")]
        public GameObject prefab;

        [Tooltip("⚠️ OBLIGATORIO. Nombre EXACTO que tendrá el GameObject en la jerarquía.\n\n" +
                 "No es cosmético: el sistema de guardado identifica a cada NPC por el nombre de " +
                 "su GameObject (GameBootProfile.CaptureNpcPositionsFromScene usa " +
                 "npc.gameObject.name), NO por el persistenceId. Si esto no coincide con el " +
                 "nombre que tenía el NPC cuando estaba colocado en la escena, el juego funciona " +
                 "igual pero los NPCs vuelven a su sitio inicial al cargar partida, SIN ningún " +
                 "error en consola.")]
        public string gameObjectName;

        [Tooltip("Id narrativo con el que se registra en NPCRegistry (lo que buscan el grafo y " +
                 "las secuencias). Si se deja vacío, se respeta el que traiga el prefab.")]
        public string persistenceId;

        [Tooltip("Estado activo con el que aparece. Se captura del objeto que había en la escena: " +
                 "un NPC que estaba desactivado (porque aparece más adelante en la historia) debe " +
                 "seguir apareciendo desactivado, o se vería desde el primer minuto.")]
        public bool startActive = true;

        [Tooltip("Desactívalo para los NPCs que NO caminan (un tendero tras su mostrador, un " +
                 "personaje colocado para una escena concreta). Con esto puesto, el spawner exige " +
                 "que el marcador caiga sobre NavMesh y da error si no; sin él, lo coloca en el " +
                 "sitio exacto del marcador y calla.")]
        public bool requireNavMesh = true;

        [Header("Condición (opcional)")]
        [Tooltip("Si se rellena, este NPC solo aparece cuando el flag existe en el preset activo. " +
                 "Vacío = aparece siempre.")]
        public string requiredFlag;

        [Tooltip("Invierte la condición: el NPC aparece solo si el flag NO está puesto.")]
        public bool invertCondition;

        /// <summary>¿Se cumplen las condiciones para instanciar esta entrada ahora mismo?</summary>
        public bool ConditionMet()
        {
            if (string.IsNullOrEmpty(requiredFlag)) return true;
            bool has = UnlockService.HasFlag(requiredFlag);
            return invertCondition ? !has : has;
        }

        /// <summary>Validación previa al spawn. Devuelve false y explica el motivo si está mal.</summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(spawnId))        { error = "spawnId vacío"; return false; }
            if (prefab == null)                       { error = $"'{spawnId}': prefab sin asignar"; return false; }
            if (string.IsNullOrEmpty(gameObjectName)) { error = $"'{spawnId}': gameObjectName vacío (ver tooltip — rompe el guardado en silencio)"; return false; }
            error = null;
            return true;
        }
    }

    [Tooltip("Nombre descriptivo del roster (ej: 'Cap1'). Solo informativo, para los logs.")]
    public string rosterId = "Cap1";

    public List<Entry> entries = new();
}
