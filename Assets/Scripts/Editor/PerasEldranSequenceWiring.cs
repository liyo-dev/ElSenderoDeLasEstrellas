using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta en MainWorld.unity la secuencia de datos SEQ_PerasEldran (Paso 4 del refactor Tramo 1,
/// análisis claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §4, §8, sobre INC-236).
/// Es la secuencia de escena, en el sitio real del pueblo, donde Will llega hasta donde discutían
/// Eldran y Victoria por las cajas de fruta (el gancho que ya se oye por la ventana en
/// SEQ_DiscusionVentana, WillHouse) -- Eldran cede entre risas, saluda a Will y le pide el favor
/// de la caja que dejó bajo un árbol.
///
/// Igual que OliverSequenceWiring: MainWorld.unity está guardada en formato binario y no se puede
/// editar desde fuera del Editor, así que este script hace lo mismo desde dentro con el
/// serializador del propio Editor.
///
/// DIFERENCIA IMPORTANTE con OliverSequenceWiring/DiscusionVentanaSequenceWiring: Eldran no existe
/// como GameObject estático en la escena -- se instancia en runtime desde
/// Assets/Resources/NpcRosters/NpcRoster_MainWorld.asset (spawnId "SPAWN_Eldran", sistema de spawn
/// por datos, ver claude/propuesta-sistema-spawn-npcs-por-datos-2026-09-16.md). Así que en el
/// Editor (fuera de Play) no se puede localizar a Eldran por su Persistence ID como hace
/// OliverSequenceWiring con Oliver -- en su lugar, este script usa el marcador
/// NpcSpawnPoint("SPAWN_Eldran") de la propia escena, que sí es estático y sí existe siempre, como
/// punto de referencia real de dónde va a aparecer.
///
/// QUÉ HACE (idempotente -- se puede volver a ejecutar sin duplicar nada):
///   1) Busca la escena 'MainWorld' entre las abiertas.
///   2) Localiza el NpcSpawnPoint "SPAWN_Eldran" -- es el punto de referencia real del pueblo
///      (dónde va a aparecer Eldran en Play), aunque Eldran mismo no exista todavía en el Editor.
///   3) Crea (o reutiliza) la instancia de Victoria.prefab junto a ese punto si todavía no está en
///      escena -- su Persistence ID ya viene puesto en el propio prefab (NPC_Victoria, confirmado
///      en Assets/_NPCs/Victoria.prefab). A diferencia de Eldran, Victoria NO está en el roster de
///      NpcRoster_MainWorld.asset -- se coloca como GameObject suelto en la escena, tal y como
///      pedía claude/plan-frentes-abiertos-refactor-y-gameplay-novela-2026-09-17.md ("Vestir/
///      colocar a NPC_Victoria en escena"). Un NPCBehaviourManagerV2 se registra en NPCRegistry
///      igual esté colocado a mano o spawneado por el roster, así que esto basta para que
///      SequenceActor.TryResolve("NPC_Victoria") la encuentre en Play.
///   4) Crea (o reutiliza) el GameObject "SEQ_PerasEldran" con SequencePlayer + SequenceStage.
///   5) Crea (o reutiliza) un trigger físico (SignalEmitter) sobre el punto de spawn de Eldran que
///      emite WILL_REACHED_ELDRAN cuando el Player entra -- así el grafo narrativo sabe cuándo Will
///      ha llegado, para completar la misión "busca a Eldran" y arrancar PERAS_START. Oliver ya no
///      necesita marca propia: llega con Will por el sistema de party (ver OliverSequenceWiring).
///   6) Reutiliza el mismo CinematicCameraDriver que ya usan las otras secuencias del pueblo.
///
/// ACTUALIZADO (21 sep 2026, unificación de cámaras): este script ya NO crea ningún plano de
/// cámara ('perasShot' ha desaparecido, y ya no toma prestado 'willClose' de SEQ_OliverSaludo) --
/// SEQ_PerasEldran.asset usa ahora ShotBeat (plano calculado a partir de la posición real de los
/// actores, igual que el prólogo) en vez de CutBeat(shotName), así que no hace falta colocar ni
/// compartir ninguna Camera entre secuencias. Lo único que sigue haciendo falta de la escena es EL
/// CinematicCameraDriver compartido (el propio Camera que renderiza los planos calculados).
///
/// Deliberadamente FUERA de este script (decisión de diseño pendiente, no wiring):
///   - El puesto del mercado (TUTORIAL_MARKET/MARKET_DONE) -- falta decidir el catálogo de 5
///     ítems (ver análisis §8 pieza 9) antes de poder montarlo, así que no es un problema de
///     wiring como este.
///   - Mover a Victoria (y a Oliver) al roster de spawn por datos para que persistan igual que
///     Eldran entre partida guardada/cargada -- mejora futura, no bloquea que la secuencia
///     funcione hoy.
///
/// Como Eldran no existe en el Editor, el trigger de WILL_REACHED_ELDRAN se coloca en el punto de
/// spawn (a ojo, igual que antes lo era 'perasShot'): en cuanto se vea en Play con Eldran ya
/// spawneado, hace falta ajustar su radio/posición en la Scene View si no coincide bien.
///
/// Al terminar deja un resumen en consola con todo lo que ha enlazado y lo que no ha podido.
/// </summary>
public static class PerasEldranSequenceWiring
{
    private const string DefinitionPath = "Assets/_SEQUENCES/SEQ_PerasEldran.asset";
    private const string SceneName = "MainWorld";
    private const string PlayerObjectName = "SEQ_PerasEldran";
    private const string VictoriaPrefabPath = "Assets/_NPCs/Victoria.prefab";

    private const string EldranSpawnId = "SPAWN_Eldran";
    private const string VictoriaId = "NPC_Victoria";

    private const float VictoriaSideOffset = 1.6f;

    private const string TriggerObjectName = "Trigger_WillReachedEldran";
    private const string TriggerEventKey = "WILL_REACHED_ELDRAN";
    private const float TriggerRadius = 2.5f;

    [MenuItem("El Sendero/Archivo/Secuencias/Montar SEQ_PerasEldran (MainWorld)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        Scene sc = EditorSceneManager.GetSceneByName(SceneName);
        if (!sc.IsValid() || !sc.isLoaded)
        {
            EditorUtility.DisplayDialog("Montar SEQ_PerasEldran",
                $"No encuentro la escena '{SceneName}' abierta.\n\n" +
                $"Ábrela (Assets/Scenes/Worlds/{SceneName}.unity) y vuelve a ejecutar esto desde el menú.",
                "Vale");
            return;
        }
        log.AppendLine($"Escena encontrada: '{sc.name}'.");

        var definition = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(DefinitionPath);
        if (definition == null)
        {
            EditorUtility.DisplayDialog("Montar SEQ_PerasEldran",
                $"No encuentro el asset de la secuencia en:\n{DefinitionPath}\n\nSin él no hay nada que montar.",
                "Vale");
            return;
        }
        log.AppendLine($"Definición: '{definition.displayName}'.");

        // ── 1) Punto de referencia: el marcador de spawn de Eldran ───────────
        Transform eldranSpawn = FindAnchorByStringField<NpcSpawnPoint>(sc, "spawnId", EldranSpawnId);
        if (eldranSpawn == null)
            warnings.Add($"No encuentro el NpcSpawnPoint '{EldranSpawnId}' -- coloco todo en el " +
                         "origen, ajústalo a mano en la Scene View.");

        Vector3 anchorPos = eldranSpawn != null ? eldranSpawn.position : Vector3.zero;
        Vector3 anchorFwd = eldranSpawn != null ? eldranSpawn.forward : Vector3.forward;
        Vector3 sideDir = Vector3.Cross(Vector3.up, anchorFwd).normalized;
        if (sideDir.sqrMagnitude < 0.0001f) sideDir = Vector3.right;

        // ── 2) Victoria: reutilizar si ya está en escena, si no, instanciarla ─
        Transform victoria = FindNpcInSceneByPersistenceId(sc, VictoriaId);
        if (victoria == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VictoriaPrefabPath);
            if (prefab == null)
            {
                warnings.Add($"No encuentro el prefab de Victoria en:\n{VictoriaPrefabPath}\n\n" +
                             "No puedo colocarla en escena -- añádela a mano y vuelve a ejecutar esto.");
            }
            else
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sc);
                Undo.RegisterCreatedObjectUndo(instance, "Montar SEQ_PerasEldran");
                instance.transform.position = anchorPos + sideDir * VictoriaSideOffset;
                instance.transform.rotation = Quaternion.LookRotation(-anchorFwd, Vector3.up);
                victoria = instance.transform;
                log.AppendLine("Victoria.prefab instanciada junto al punto de spawn de Eldran " +
                               "(posición a ojo, fácil de arrastrar en la Scene View una vez Eldran " +
                               "aparezca de verdad en Play).");

                string victoriaActualId = GetPersistenceId(instance);
                if (victoriaActualId != VictoriaId)
                    warnings.Add($"El Persistence ID de la instancia nueva de Victoria es " +
                                 $"'{victoriaActualId}', no '{VictoriaId}' -- el asset no podrá " +
                                 "resolverla en tiempo de ejecución. Corrígelo a mano en su " +
                                 "NPCBehaviourManagerV2.");
            }
        }
        else
        {
            log.AppendLine("Victoria ya estaba en la escena -- se reutiliza tal cual está.");
        }

        // ── 3) GameObject raíz: SequencePlayer + SequenceStage ───────────────
        GameObject go = FindRoot(sc, PlayerObjectName);
        if (go == null)
        {
            go = new GameObject(PlayerObjectName);
            SceneManager.MoveGameObjectToScene(go, sc);
            Undo.RegisterCreatedObjectUndo(go, "Montar SEQ_PerasEldran");
            log.AppendLine($"GameObject '{PlayerObjectName}' creado en la raíz de '{sc.name}'.");
        }
        else
        {
            log.AppendLine($"GameObject '{PlayerObjectName}' ya existía -- se reutiliza y se actualiza.");
        }
        go.transform.position = anchorPos;

        var player = go.GetComponent<SequencePlayer>() ?? Undo.AddComponent<SequencePlayer>(go);
        var stage  = go.GetComponent<SequenceStage>()  ?? Undo.AddComponent<SequenceStage>(go);

        // ── 4) Trigger de llegada: WILL_REACHED_ELDRAN ───────────────────────
        // Oliver ya no necesita una marca aquí -- llega con Will por el sistema de party (ver
        // OliverSequenceWiring.WirePartyMembershipSignals) y se coloca solo junto a él cuando
        // arranca la secuencia. Lo que sí hace falta es que el juego sepa CUÁNDO Will ha llegado
        // hasta Eldran, para completar la misión "busca a Eldran" y arrancar PERAS_START -- un
        // SignalEmitter en modo PhysicsTrigger sobre el punto de spawn de Eldran, mismo mecanismo
        // que ya usa WILL_EXITS_HOUSE (INC-338).
        GameObject trigger = FindRoot(sc, TriggerObjectName);
        if (trigger == null)
        {
            trigger = new GameObject(TriggerObjectName);
            SceneManager.MoveGameObjectToScene(trigger, sc);
            Undo.RegisterCreatedObjectUndo(trigger, "Montar SEQ_PerasEldran");
            log.AppendLine($"GameObject '{TriggerObjectName}' creado.");
        }
        else
        {
            log.AppendLine($"GameObject '{TriggerObjectName}' ya existía -- se reutiliza.");
        }
        trigger.transform.position = anchorPos;

        var triggerCollider = trigger.GetComponent<SphereCollider>() ?? Undo.AddComponent<SphereCollider>(trigger);
        triggerCollider.isTrigger = true;
        triggerCollider.radius = TriggerRadius;

        var emitter = trigger.GetComponent<SignalEmitter>() ?? Undo.AddComponent<SignalEmitter>(trigger);
        emitter.eventKey = TriggerEventKey;
        emitter.trigger = SignalEmitter.TriggerType.PhysicsTrigger;
        emitter.requiredTag = "Player";
        emitter.once = true;
        log.AppendLine($"SignalEmitter enlazado: emite '{TriggerEventKey}' cuando el Player entra en " +
                       $"un radio de {TriggerRadius}m del punto de spawn de Eldran.");

        // ── 5) Marcador de minimapa: ELDRAN_MISSION1 ─────────────────────────
        // FIX (21 sept 2026, Raul: "no esta saliendo el icono en el minimapa" -- INC-350/INC-343).
        // ELDRAN_MISSION1 (la mision "busca a Eldran" de INC-343) no usa WaitNpcInteractionNode --
        // usa este mismo trigger de WILL_REACHED_ELDRAN -- asi que nunca se llamaba a
        // NarrativeActor.ShowQuestIcon() como pasa con otras misiones migradas al grafo. El marcador
        // de MINIMAPA es un componente aparte (QuestObjectiveMarker, no depende de nodos del grafo,
        // solo de QuestManager), asi que se puede colgar aqui mismo, en el punto de spawn de Eldran
        // -- mismo patron y mismos valores que ya usa ELDRAN_MISSION2 en Goods_Interactable.prefab
        // (stepIndex/showAfterStep -1 = visible durante toda la quest activa, sin icono propio
        // asignado, punto amarillo por defecto).
        var objectiveMarker = trigger.GetComponent<QuestObjectiveMarker>() ?? Undo.AddComponent<QuestObjectiveMarker>(trigger);
        var soMarker = new SerializedObject(objectiveMarker);
        soMarker.FindProperty("questId").stringValue = "ELDRAN_MISSION1";
        soMarker.FindProperty("stepIndex").intValue = -1;
        soMarker.FindProperty("showAfterStep").intValue = -1;
        var markerColorProp = soMarker.FindProperty("color");
        markerColorProp.colorValue = Color.yellow;
        soMarker.ApplyModifiedProperties();
        log.AppendLine("QuestObjectiveMarker enlazado: marcador de minimapa para 'ELDRAN_MISSION1' " +
                       "visible mientras la mision esté activa.");

        // ── 6) Enlazar SequenceStage ───────────────────────────────────────────
        var soStage = new SerializedObject(stage);

        var existingDriver = Object.FindAnyObjectByType<CinematicCameraDriver>(FindObjectsInactive.Include);
        soStage.FindProperty("_cameraDriver").objectReferenceValue = existingDriver;
        if (existingDriver == null)
            warnings.Add("No hay ningún CinematicCameraDriver en la escena -- ningún plano calculado " +
                         "hará nada.");
        else
            log.AppendLine($"Camera driver reutilizado de '{existingDriver.name}'.");

        soStage.ApplyModifiedProperties();

        // ── 7) Enlazar SequencePlayer ──────────────────────────────────────────
        var soPlayer = new SerializedObject(player);
        soPlayer.FindProperty("_definition").objectReferenceValue = definition;
        soPlayer.FindProperty("_stage").objectReferenceValue = stage;
        soPlayer.ApplyModifiedProperties();
        log.AppendLine("Sequence Player: definición y stage enlazados. (Sin música ni transiciones -- " +
                       "el asset no las pide.)");

        EditorSceneManager.MarkSceneDirty(sc);
        Selection.activeGameObject = go;

        var final = new StringBuilder();
        final.AppendLine("=== Montaje de SEQ_PerasEldran en MainWorld ===");
        final.Append(log);

        if (warnings.Count == 0)
        {
            final.AppendLine();
            final.AppendLine("Sin avisos: todo enlazado. Guarda la escena (Ctrl+S) y dale a Play.");
            Debug.Log(final.ToString(), go);
        }
        else
        {
            final.AppendLine();
            final.AppendLine($"--- {warnings.Count} aviso(s), revisar: ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            final.AppendLine();
            final.AppendLine("Guarda la escena (Ctrl+S) igualmente: lo demás sí ha quedado montado.");
            Debug.LogWarning(final.ToString(), go);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GameObject FindRoot(Scene sc, string name)
    {
        foreach (var root in sc.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    /// Marca de posición: solo un Transform con nombre, nada de Camera ni CinematicShot -- eso
    /// ya no hace falta desde que SEQ_PerasEldran usa ShotBeat (plano calculado). Mismo patrón
    /// que un CinematicShot en cuanto a idempotencia (reutiliza si ya existe), pero mucho más
    /// simple, así que no comparte el helper viejo de DiscusionVentanaSequenceWiring/INC-335.
    private static void SetNamedTransform(SerializedObject so, string listProp, int index, string name, Transform target)
    {
        var prop = so.FindProperty(listProp);
        if (prop.arraySize <= index) prop.arraySize = index + 1;
        var entry = prop.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("name").stringValue = name;
        entry.FindPropertyRelative("target").objectReferenceValue = target;
    }

    /// Busca un componente de tipo T en la escena cuyo campo serializado 'fieldName' (string)
    /// valga 'wanted', y devuelve su Transform. Mismo helper que
    /// DiscusionVentanaSequenceWiring.FindAnchorByStringField -- duplicado a propósito en vez de
    /// compartido, para no crear un acoplamiento entre dos scripts de Editor de un solo uso cada
    /// uno.
    private static Transform FindAnchorByStringField<T>(Scene sc, string fieldName, string wanted) where T : Component
    {
        foreach (var root in sc.GetRootGameObjects())
        foreach (var comp in root.GetComponentsInChildren<T>(true))
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop != null && prop.propertyType == SerializedPropertyType.String && prop.stringValue == wanted)
                return comp.transform;
        }
        return null;
    }

    /// A diferencia de Eldran (spawneado en runtime desde el roster, no existe en el Editor),
    /// Victoria si está colocada se busca como cualquier NPC estático de la escena: por su
    /// Persistence ID en NPCBehaviourManagerV2.
    private static Transform FindNpcInSceneByPersistenceId(Scene sc, string wantedId)
    {
        foreach (var root in sc.GetRootGameObjects())
        foreach (var m in root.GetComponentsInChildren<Game.NPC.NPCBehaviourManagerV2>(true))
        {
            if (GetPersistenceId(m.gameObject) == wantedId) return m.transform;
        }
        return null;
    }

    private static string GetPersistenceId(GameObject go)
    {
        var manager = go.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (manager == null) return null;
        return new SerializedObject(manager).FindProperty("persistenceId")?.stringValue;
    }
}
