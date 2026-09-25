using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Instancia los NPCs de uno o varios <see cref="NpcRosterSO"/> sobre los
/// <see cref="NpcSpawnPoint"/> que haya registrados en la escena.
///
/// Se llama desde <c>WorldBootstrap.InitializeWorld()</c>, y es CRÍTICO que sea ANTES de
/// <c>GameBootProfile.ApplyNpcPositionsToScene()</c>: esa función hace un FindObjectsByType en
/// vivo, así que un NPC instanciado unas líneas antes ya entra en la consulta y recibe su
/// posición guardada. Ese es todo el enganche con el guardado — el marcador dice dónde EMPIEZA
/// un NPC, y el save, si existe, manda.
///
/// Ver claude/propuesta-sistema-spawn-npcs-por-datos-2026-09-16.md § 2.3 y § 3.2
/// </summary>
public static class NpcSpawner
{
    /// <summary>Carpeta dentro de Assets/Resources de la que se cargan los rosters.</summary>
    public const string RostersResourcePath = "NpcRosters";

    /// <summary>Radio máximo de búsqueda de NavMesh alrededor del marcador.</summary>
    private const float NavMeshSampleRadius = 3f;

    // spawnId -> instancia, para no duplicar si SpawnAll se llama dos veces (p. ej. modo preset
    // y modo normal, o al recargar una escena aditiva).
    private static readonly Dictionary<string, GameObject> _spawned = new();

    public static IReadOnlyDictionary<string, GameObject> Spawned => _spawned;

    /// <summary>
    /// Carga todos los rosters de Resources y los instancia. Si no hay ninguno, no hace nada
    /// (que es el estado del paso 1: el sistema está puesto pero no cambia nada del juego).
    /// </summary>
    public static int SpawnAllFromResources()
    {
        var rosters = Resources.LoadAll<NpcRosterSO>(RostersResourcePath);
        if (rosters == null || rosters.Length == 0)
            return 0;

        int total = 0;
        foreach (var roster in rosters)
            total += SpawnAll(roster);

        return total;
    }

    /// <summary>Instancia todas las entradas válidas de un roster. Devuelve cuántas se crearon.</summary>
    public static int SpawnAll(NpcRosterSO roster)
    {
        if (roster == null || roster.entries == null || roster.entries.Count == 0)
            return 0;

        // Contenedor DESACTIVADO: instanciar dentro de él deja al NPC inactivo en jerarquía, así
        // que Unity NO ejecuta todavía su Awake(). Eso es lo que permite colocarlo y cambiarle el
        // persistenceId ANTES de que NPCBehaviourManagerV2.Awake() se registre en NPCRegistry con
        // el id que trae el prefab.
        var holder = new GameObject("~NpcSpawnerStaging");
        holder.SetActive(false);

        int created = 0;
        try
        {
            foreach (var entry in roster.entries)
            {
                if (entry == null || !entry.enabled) continue;

                if (!entry.Validate(out string error))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[NpcSpawner] Roster '{roster.rosterId}': entrada inválida — {error}. Se omite.");
#endif
                    continue;
                }

                if (_spawned.TryGetValue(entry.spawnId, out var already) && already != null)
                    continue; // ya instanciado en una llamada anterior

                if (!entry.ConditionMet())
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[NpcSpawner] '{entry.spawnId}' no cumple su condición (flag '{entry.requiredFlag}') — no se instancia.");
#endif
                    continue;
                }

                var point = NpcSpawnRegistry.Get(entry.spawnId);
                if (point == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[NpcSpawner] No hay ningún NpcSpawnPoint con spawnId '{entry.spawnId}' en la escena actual — '{entry.gameObjectName}' no aparece. " +
                                     "(Normal si el marcador vive en otra escena.)");
#endif
                    continue;
                }

                // Si ya hay en el mundo un NPC con esa identidad —colocado a mano en la escena, o
                // de una herramienta de montaje—, no se crea otro encima. Dos iguales en el mismo
                // sitio son dos iconos de interactuar, dos cerebros y un NPCRegistry que se queda
                // con el último (INC-364).
                if (!string.IsNullOrEmpty(entry.persistenceId) && NPCRegistry.HasInstance
                    && NPCRegistry.Instance.GetNPCByID(entry.persistenceId) != null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var ya = NPCRegistry.Instance.GetNPCByID(entry.persistenceId);
                    Debug.LogWarning($"[NpcSpawner] '{entry.spawnId}': ya hay un '{entry.persistenceId}' en el mundo " +
                                     $"('{ya.name}', escena '{ya.gameObject.scene.name}'). No se crea otro: quítalo de la escena " +
                                     "si debe venir del roster.", ya);
#endif
                    continue;
                }

                if (SpawnOne(entry, point, holder.transform))
                    created++;
            }
        }
        finally
        {
            Object.Destroy(holder);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (created > 0)
            Debug.Log($"[NpcSpawner] Roster '{roster.rosterId}': {created} NPC(s) instanciados.");
#endif
        return created;
    }

    private static bool SpawnOne(NpcRosterSO.Entry entry, NpcSpawnPoint point, Transform staging)
    {
        Vector3 position = entry.requireNavMesh
            ? ResolveNavMeshPosition(point, entry.spawnId)
            : point.transform.position;
        Quaternion rotation = point.applyRotation
            ? point.GetSpawnRotation()
            : entry.prefab.transform.rotation;

        // 1) Instanciar DENTRO del contenedor desactivado → Awake() todavía no corre.
        var go = Object.Instantiate(entry.prefab, staging);

        // 2) Nombre exacto. Unity pondría "Oliver(Clone)" y eso rompería el guardado en silencio
        //    (ver el tooltip de NpcRosterSO.Entry.gameObjectName).
        go.name = entry.gameObjectName;

        // 3) Colocarlo YA, antes de que Awake() corra, para que el NavMeshAgent y el FSM
        //    arranquen viéndose en su sitio definitivo y no en el origen.
        go.transform.SetPositionAndRotation(position, rotation);

        // 4) Identidad narrativa, si el roster la sobrescribe.
        if (!string.IsNullOrEmpty(entry.persistenceId))
        {
            var brain = go.GetComponent<NPCBehaviourManagerV2>();
            if (brain != null)
                brain.OverrideIdentityBeforeAwake(entry.persistenceId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
                Debug.LogWarning($"[NpcSpawner] '{entry.gameObjectName}' no tiene NPCBehaviourManagerV2 — no se puede aplicar persistenceId '{entry.persistenceId}'.");
#endif
        }

        // 5) Sacarlo del contenedor conservando la transform de mundo. AQUÍ corre Awake().
        go.transform.SetParent(null, worldPositionStays: true);

        // 6) Meterlo en la MISMA escena que su marcador.
        //
        // Sin esto el NPC se queda en la escena ACTIVA, que durante el arranque puede ser 'Start'
        // (la escena persistente de managers, siempre cargada). Ahí sobreviviría a una recarga de
        // MainWorld y tendríamos dos Olivers: el que sobrevivió y el que instancia el arranque
        // nuevo. El marcador ya nos dice a qué escena pertenece, así que lo movemos ahí.
        var targetScene = point.gameObject.scene;
        if (targetScene.IsValid() && targetScene != go.scene)
            SceneManager.MoveGameObjectToScene(go, targetScene);

        // 7) Estado activo tal y como estaba en la escena.
        if (!entry.startActive)
            go.SetActive(false);

        _spawned[entry.spawnId] = go;
        return true;
    }

    /// <summary>
    /// Proyecta la posición del marcador sobre el NavMesh. Un NPC instanciado fuera de malla se
    /// queda con el agente inutilizable — el mismo fallo que ya avisa SequenceMovement (INC-209).
    /// </summary>
    private static Vector3 ResolveNavMeshPosition(NpcSpawnPoint point, string spawnId)
    {
        Vector3 raw = point.transform.position;

        if (NavMesh.SamplePosition(raw, out var hit, NavMeshSampleRadius, NavMesh.AllAreas))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Vector3.Distance(raw, hit.position) > 0.75f)
            {
                Debug.LogWarning($"[NpcSpawner] El marcador '{spawnId}' está a {Vector3.Distance(raw, hit.position):0.00} m del NavMesh más cercano. " +
                                 "Se corrige el spawn, pero conviene recolocarlo en la escena.");
            }
#endif
            return hit.position;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError($"[NpcSpawner] El marcador '{spawnId}' NO tiene NavMesh en {NavMeshSampleRadius} m. " +
                       "El NPC aparecerá ahí igualmente, pero su NavMeshAgent no funcionará (no podrá caminar).");
#endif
        return raw;
    }

    /// <summary>
    /// Limpia el registro de instanciados. Se llama al cambiar de partida/escena para que un
    /// nuevo arranque vuelva a instanciar desde cero.
    /// </summary>
    public static void Reset()
    {
        _spawned.Clear();
    }
}
