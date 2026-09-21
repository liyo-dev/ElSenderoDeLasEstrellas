using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Migra a NPC_Archimago, NPC_Liora y NPC_MagoOscuro (colocados a mano en Prologo_Valle.unity)
/// al sistema de spawn por datos (NpcRosterSO + NpcSpawnPoint) que ya usa MainWorld para Oliver,
/// _LIAM, Eldran y Tabernera.
///
/// Motivo (18 sep 2026, a petición de Raúl, tras la incidencia de Archimago apareciendo
/// desplazado exactamente +6000/+100/+6000 respecto a su posición autorada en la escena — ver
/// claude/incidencia-*-archimago-*.md si ya existe ese doc): tener los NPCs colocados a mano en
/// la escena obliga a tocar los GameObjects de Prologo_Valle.unity para cualquier cambio de
/// posición o de reparto, y es más frágil que el sistema de datos que ya usa MainWorld — un
/// NpcRosterSO es un asset de texto que se puede editar sin abrir la escena binaria.
///
/// Al terminar, Prologo_Valle.unity ya NO tiene estos tres NPCs colocados directamente: solo un
/// NpcSpawnPoint por cada uno (en el mismo sitio exacto donde estaban), y un NpcRosterSO nuevo
/// (NpcRoster_PrologoValle, en Assets/Resources/NpcRosters/) es quien decide qué prefab e
/// identidad va en cada punto — mismo patrón que NpcRoster_MainWorld.asset.
///
/// Uso: con Prologo_Valle.unity abierta (sola, o aditivamente junto a otras escenas — no importa
/// el orden), fuera de Play mode: menú Sendero/Prólogo/Migrar NPCs del prólogo a NpcRosterSO.
/// Revisa el diff de la escena y del roster antes de hacer commit; no se guarda solo.
/// </summary>
public static class MigrarNpcsPrologoASpawnSystem
{
    private const string SceneName = "Prologo_Valle";
    private const string RosterPath = "Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset";

    private class NpcInfo
    {
        public readonly string gameObjectName;
        public readonly string persistenceId;
        public readonly bool requireNavMesh;

        public NpcInfo(string gameObjectName, string persistenceId, bool requireNavMesh)
        {
            this.gameObjectName = gameObjectName;
            this.persistenceId = persistenceId;
            this.requireNavMesh = requireNavMesh;
        }
    }

    // Los tres viven en NavMeshAgent y caminan durante la cinemática (SEQ_Prologo_UltimaNoche),
    // así que requireNavMesh = true para los tres, igual que Oliver/Eldran en MainWorld.
    private static readonly NpcInfo[] Npcs =
    {
        new NpcInfo("NPC_Archimago", "NPC_Archimago", true),
        new NpcInfo("NPC_Liora", "NPC_Liora", true),
        new NpcInfo("NPC_MagoOscuro", "NPC_MagoOscuro", true),
    };

    [MenuItem("El Sendero/Prólogo/Migrar NPCs del prólogo a NpcRosterSO")]
    public static void Migrate()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MigrarNpcsPrologo] Esto hay que ejecutarlo FUERA de Play mode (los cambios tienen que quedar guardados en la escena).");
            return;
        }

        Scene scene = FindScene(SceneName);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MigrarNpcsPrologo] No encuentro la escena '{SceneName}' entre las abiertas. Ábrela (sola o aditivamente) y vuelve a intentarlo.");
            return;
        }

        var roster = LoadOrCreateRoster();
        int migrated = 0;

        foreach (var info in Npcs)
        {
            GameObject go = FindRootInScene(scene, info.gameObjectName);
            if (go == null)
            {
                Debug.LogWarning($"[MigrarNpcsPrologo] No encuentro '{info.gameObjectName}' como raíz de '{SceneName}' — ¿ya estaba migrado? Se omite.");
                continue;
            }

            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(go) as GameObject;
            if (prefabSource == null)
            {
                Debug.LogError($"[MigrarNpcsPrologo] '{info.gameObjectName}' no es una instancia de prefab (o no se encuentra su fuente) — no puedo saber qué prefab asignar en el roster. Se omite, no se toca.");
                continue;
            }

            Vector3 pos = go.transform.position;
            Quaternion rot = go.transform.rotation;
            string spawnId = "SPAWN_" + info.gameObjectName;

            // 1) Crear el NpcSpawnPoint exactamente donde estaba colocado el NPC a mano.
            var marker = new GameObject($"SpawnPoint_{info.gameObjectName}");
            Undo.RegisterCreatedObjectUndo(marker, "Crear NpcSpawnPoint");
            if (marker.scene != scene)
                SceneManager.MoveGameObjectToScene(marker, scene);
            marker.transform.SetPositionAndRotation(pos, rot);
            var spawnPoint = marker.AddComponent<NpcSpawnPoint>();
            spawnPoint.spawnId = spawnId;
            spawnPoint.applyRotation = true;

            // 2) Añadir o actualizar la entrada del roster (idempotente: si ya existe una entrada
            // con este spawnId de una ejecución anterior, se sobreescribe en vez de duplicarse).
            UpsertEntry(roster, new NpcRosterSO.Entry
            {
                enabled = true,
                spawnId = spawnId,
                prefab = prefabSource,
                gameObjectName = info.gameObjectName,
                persistenceId = info.persistenceId,
                startActive = true,
                requireNavMesh = info.requireNavMesh,
                requiredFlag = "",
                invertCondition = false,
            });

            // 3) Quitar al NPC colocado a mano — a partir de ahora lo instancia NpcSpawner desde
            // el roster, usando este mismo marcador.
            Undo.DestroyObjectImmediate(go);

            migrated++;
            Debug.Log($"[MigrarNpcsPrologo] '{info.gameObjectName}' migrado: spawnId='{spawnId}', pos={pos}, prefab='{AssetDatabase.GetAssetPath(prefabSource)}'.");
        }

        EditorUtility.SetDirty(roster);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[MigrarNpcsPrologo] Hecho: {migrated}/{Npcs.Length} NPC(s) migrados. Revisa el diff de '{SceneName}.unity' y del roster, y guarda la escena (Ctrl+S) cuando estés conforme — esta herramienta NO guarda sola. Roster en '{RosterPath}'.");
    }

    private static Scene FindScene(string name)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.IsValid() && s.isLoaded && s.name == name)
                return s;
        }
        return default;
    }

    private static GameObject FindRootInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }
        return null;
    }

    private static NpcRosterSO LoadOrCreateRoster()
    {
        var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>(RosterPath);
        if (roster != null) return roster;

        roster = ScriptableObject.CreateInstance<NpcRosterSO>();
        roster.rosterId = "PrologoValle";
        roster.entries = new List<NpcRosterSO.Entry>();

        Directory.CreateDirectory(Path.GetDirectoryName(RosterPath));
        AssetDatabase.CreateAsset(roster, RosterPath);
        Debug.Log($"[MigrarNpcsPrologo] Creado roster nuevo en '{RosterPath}'.");
        return roster;
    }

    private static void UpsertEntry(NpcRosterSO roster, NpcRosterSO.Entry entry)
    {
        int existingIndex = roster.entries.FindIndex(e => e.spawnId == entry.spawnId);
        if (existingIndex >= 0)
            roster.entries[existingIndex] = entry;
        else
            roster.entries.Add(entry);
    }
}
