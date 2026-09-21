using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Limpieza y alta de Victoria en MainWorld.unity (Paso 0 + parte de Pasos 2-4 del refactor
/// Tramo 1, análisis claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §2 y §7.2).
/// Todo esto quedó pendiente en INC-225 (Paso 0) porque `MainWorld.unity` está en binario y no se
/// puede tocar a ciegas por archivo -- necesita el serializador del Editor, igual que
/// OliverSequenceWiring/StarAwakeningSequenceWiring/DiscusionVentanaSequenceWiring.
///
/// QUÉ HACE (idempotente -- se puede volver a ejecutar sin duplicar ni romper nada):
///
///   1) Marca de spawn de Victoria. El roster `NpcRoster_MainWorld.asset` ya tiene una entrada
///      para ella (spawnId "SPAWN_Victoria"), pero el marcador `NpcSpawnPoint` con ese id no
///      existe todavía en la escena -- sin él, `NpcSpawner` no tiene dónde instanciarla. Se busca
///      el `NpcSpawnPoint` de Eldran ("SPAWN_Eldran") y se crea el de Victoria a su lado (1,5 m a
///      un lado, mirando hacia él), para que la discusión de las peras los tenga ya juntos.
///
///   2) `PortalTrigger`/`InteriorPortalTrigger` roto (deuda #8 del análisis). El GameObject
///      "House_FrontDoor_Teleport_To_Bedroom" tiene `targetAnchorId` vacío -- por eso entrar a
///      casa no lleva a ningún sitio. Se rellena con "Bedroom" (el mismo `SpawnAnchor` que ya usa
///      `SpawnManager` para el despertar, confirmado en `WillHouse.unity`).
///
///   3) `RoomExitBlocker` (deuda #7, decisión confirmada en INC-225: "se quita"). Bloqueaba salir
///      de la habitación hasta completar `ELDRAN_MISSION1`, que ya no existe en el flujo nuevo.
///      Se borra el GameObject entero (no solo se desactiva: es justo el mismo tipo de fallo
///      silencioso que ya pasó con `StarAwakeningSequencer`, más abajo).
///
///   4) Instancia legacy de `StarAwakeningSequencer` (deuda #2). Escucha `AWAKEN_START` igual que
///      el `SequencePlayer` nuevo de `SEQ_StarAwakening` -- si se queda activa, los dos reaccionan
///      a la vez. El análisis pide explícitamente BORRARLA, no desactivarla, así que se borra.
///
/// Cada paso va en su propio try/catch con su propio resumen en consola: si alguno falla (objeto
/// no encontrado, nombre distinto al esperado...) no se lleva por delante a los demás. Todo lo que
/// se borra pasa por `Undo.DestroyObjectImmediate`, así que un Ctrl+Z lo devuelve si hace falta.
/// </summary>
public static class Tramo1WorldWiring
{
    private const string SceneName = "MainWorld";

    private const string EldranSpawnId = "SPAWN_Eldran";
    private const string VictoriaSpawnId = "SPAWN_Victoria";
    private const float VictoriaSideOffset = 1.5f;

    private const string FrontDoorPortalNameHint = "House_FrontDoor_Teleport_To_Bedroom";
    private const string BedroomAnchorId = "Bedroom";

    private const string RoomExitBlockerNameHint = "RoomExitBlocker";

    [MenuItem("El Sendero/Tramo 1/Limpieza y Victoria (MainWorld)")]
    public static void Wire()
    {
        Scene sc = EditorSceneManager.GetSceneByName(SceneName);
        if (!sc.IsValid() || !sc.isLoaded)
        {
            EditorUtility.DisplayDialog("Limpieza y Victoria (MainWorld)",
                $"No encuentro la escena '{SceneName}' abierta.\n\n" +
                $"Ábrela (Assets/Scenes/Worlds/{SceneName}.unity) y vuelve a ejecutar esto desde el menú.",
                "Vale");
            return;
        }

        var log = new StringBuilder();
        var warnings = new System.Collections.Generic.List<string>();
        log.AppendLine($"Escena encontrada: '{sc.name}'.");

        CreateVictoriaSpawnPoint(sc, log, warnings);
        FixFrontDoorPortal(sc, log, warnings);
        RemoveRoomExitBlocker(sc, log, warnings);
        RemoveLegacyStarAwakeningSequencer(sc, log, warnings);

        EditorSceneManager.MarkSceneDirty(sc);

        var final = new StringBuilder();
        final.AppendLine("=== Limpieza y alta de Victoria en MainWorld ===");
        final.Append(log);
        if (warnings.Count == 0)
        {
            final.AppendLine();
            final.AppendLine("Sin avisos: todo aplicado. Guarda la escena (Ctrl+S) y prueba en Play.");
            Debug.Log(final.ToString());
        }
        else
        {
            final.AppendLine();
            final.AppendLine($"--- {warnings.Count} aviso(s), revisar: ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            final.AppendLine();
            final.AppendLine("Guarda la escena (Ctrl+S) igualmente: lo demás sí ha quedado aplicado.");
            Debug.LogWarning(final.ToString());
        }
    }

    // ── 1) Marca de spawn de Victoria ────────────────────────────────────────

    private static void CreateVictoriaSpawnPoint(Scene sc, StringBuilder log, System.Collections.Generic.List<string> warnings)
    {
        try
        {
            if (FindSpawnPointById(sc, VictoriaSpawnId) != null)
            {
                log.AppendLine($"'{VictoriaSpawnId}' ya existía -- no se toca.");
                return;
            }

            var eldranSpawn = FindSpawnPointById(sc, EldranSpawnId);
            if (eldranSpawn == null)
            {
                warnings.Add($"No encuentro ningún NpcSpawnPoint con spawnId '{EldranSpawnId}' -- no sé " +
                             "dónde colocar a Victoria. Créala a mano con un NpcSpawnPoint de spawnId " +
                             $"'{VictoriaSpawnId}' donde quieras que aparezca.");
                return;
            }

            Transform e = eldranSpawn.transform;
            Vector3 side = Vector3.Cross(Vector3.up, e.forward).normalized;
            if (side.sqrMagnitude < 0.0001f) side = e.right;
            Vector3 pos = e.position + side * VictoriaSideOffset;

            var go = new GameObject(VictoriaSpawnId);
            Undo.RegisterCreatedObjectUndo(go, "Limpieza y Victoria (MainWorld)");
            SceneManager.MoveGameObjectToScene(go, sc);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(-side, Vector3.up); // mirando hacia Eldran

            var point = Undo.AddComponent<NpcSpawnPoint>(go);
            point.spawnId = VictoriaSpawnId;
            point.applyRotation = true;

            log.AppendLine($"'{VictoriaSpawnId}' creado junto a '{eldranSpawn.name}' " +
                           "(la entrada del roster ya existía en NpcRoster_MainWorld.asset).");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo creando el spawn point de Victoria: {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }
    }

    // ── 2) PortalTrigger roto ────────────────────────────────────────────────

    private static void FixFrontDoorPortal(Scene sc, StringBuilder log, System.Collections.Generic.List<string> warnings)
    {
        try
        {
            GameObject target = FindByNameContains(sc, FrontDoorPortalNameHint);
            if (target == null)
            {
                warnings.Add($"No encuentro ningún objeto que contenga '{FrontDoorPortalNameHint}' -- revisa " +
                             "el nombre a mano y rellena su targetAnchorId con 'Bedroom'.");
                return;
            }

            bool fixedAny = false;

            var interior = target.GetComponent<InteriorPortalTrigger>();
            if (interior != null && string.IsNullOrEmpty(interior.targetAnchorId))
            {
                Undo.RecordObject(interior, "Limpieza y Victoria (MainWorld)");
                interior.targetAnchorId = BedroomAnchorId;
                EditorUtility.SetDirty(interior);
                fixedAny = true;
            }

            var legacy = target.GetComponent<PortalTrigger>();
            if (legacy != null && string.IsNullOrEmpty(legacy.targetAnchorId))
            {
                Undo.RecordObject(legacy, "Limpieza y Victoria (MainWorld)");
                legacy.targetAnchorId = BedroomAnchorId;
                EditorUtility.SetDirty(legacy);
                fixedAny = true;
            }

            if (fixedAny)
                log.AppendLine($"'{target.name}': targetAnchorId puesto a '{BedroomAnchorId}'.");
            else if (interior == null && legacy == null)
                warnings.Add($"'{target.name}' no tiene ni PortalTrigger ni InteriorPortalTrigger -- revisar a mano.");
            else
                log.AppendLine($"'{target.name}' ya tenía targetAnchorId puesto -- no se toca.");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo arreglando el portal de la puerta: {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }
    }

    // ── 3) RoomExitBlocker -- se quita ───────────────────────────────────────

    private static void RemoveRoomExitBlocker(Scene sc, StringBuilder log, System.Collections.Generic.List<string> warnings)
    {
        try
        {
            GameObject target = FindByNameContains(sc, RoomExitBlockerNameHint);
            if (target == null)
            {
                log.AppendLine($"'{RoomExitBlockerNameHint}' no está en la escena -- nada que quitar " +
                               "(puede que ya se haya borrado antes).");
                return;
            }

            string path = GetPath(target.transform);
            Undo.DestroyObjectImmediate(target);
            log.AppendLine($"'{path}' borrado (decisión confirmada: ya no hace falta con el flujo nuevo).");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo borrando RoomExitBlocker: {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }
    }

    // ── 4) StarAwakeningSequencer legacy -- se borra, no se desactiva ───────

    private static void RemoveLegacyStarAwakeningSequencer(Scene sc, StringBuilder log, System.Collections.Generic.List<string> warnings)
    {
        try
        {
            StarAwakeningSequencer legacy = null;
            foreach (var root in sc.GetRootGameObjects())
            {
                legacy = root.GetComponentInChildren<StarAwakeningSequencer>(true);
                if (legacy != null) break;
            }

            if (legacy == null)
            {
                log.AppendLine("No hay ninguna instancia de StarAwakeningSequencer en la escena -- nada " +
                               "que borrar (puede que ya se haya borrado antes).");
                return;
            }

            string path = GetPath(legacy.transform);
            Undo.DestroyObjectImmediate(legacy.gameObject);
            log.AppendLine($"Instancia legacy de StarAwakeningSequencer ('{path}') borrada -- ya no compite " +
                           "con el SequencePlayer nuevo de SEQ_StarAwakening por AWAKEN_START.");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo borrando StarAwakeningSequencer legacy: {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static NpcSpawnPoint FindSpawnPointById(Scene sc, string spawnId)
    {
        foreach (var root in sc.GetRootGameObjects())
        foreach (var p in root.GetComponentsInChildren<NpcSpawnPoint>(true))
            if (p.spawnId == spawnId) return p;
        return null;
    }

    private static GameObject FindByNameContains(Scene sc, string nameHint)
    {
        foreach (var root in sc.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains(nameHint)) return t.gameObject;
        return null;
    }

    private static string GetPath(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }
}
