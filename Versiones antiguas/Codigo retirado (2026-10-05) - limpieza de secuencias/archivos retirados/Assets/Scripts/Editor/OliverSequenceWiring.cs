using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta de un tirón la secuencia de saludo de Oliver sobre el sistema de secuencias por datos
/// (SequenceDefinition + SequenceStage + SequencePlayer), reutilizando las referencias que ya
/// están puestas a mano en el OliverSaludoSequencer viejo: el CinematicCameraDriver, el
/// AudioGraphProfile y las dos TransitionSettings.
///
/// ACTUALIZADO (21 sep 2026, unificación de cámaras): los seis planos de cámara colocados a mano
/// (oliverSpot, sprintWide, oliverClose, spellFailWide, willClose, twoShot) han dejado de hacer
/// falta -- SEQ_OliverSaludo.asset ahora usa ShotBeat (plano calculado, igual que el prólogo) en
/// vez de CutBeat(shotName), así que este script ya no crea ni enlaza ningún SequenceShot. Los
/// seis GameObjects de cámara siguen existiendo en la escena (colgando del OliverSaludoSequencer
/// viejo) por si algún día hace falta volver atrás, pero nadie los referencia ya.
///
/// Existe porque MainWorld.unity está guardada en formato binario y no se puede editar desde fuera
/// del Editor. Mismo patrón que SenderoFinalSceneWiring/CandylandClimaxBuilder.
///
/// QUÉ HACE (todo idempotente — se puede volver a ejecutar sin duplicar nada):
///   1) Busca el OliverSaludoSequencer en las escenas abiertas y lee sus referencias.
///   2) Crea (o reutiliza) el GameObject "SEQ_OliverSaludo" en la misma escena, suelto en la raíz
///      — nunca dentro del prefab de un NPC, que ya dio problemas de trigger prematuro.
///   3) Le añade SequencePlayer + SequenceStage.
///   4) Rellena el SequenceStage: los seis planos con los nombres que espera el asset
///      (oliverSpot, sprintWide, oliverClose, spellFailWide, willClose, twoShot) y el camera driver.
///   5) Rellena el SequencePlayer: la SequenceDefinition, el AudioGraphProfile y las dos
///      transiciones. El resto se deja vacío a propósito (las señales y el ID de música vienen del
///      asset; el action manager y la cámara se resuelven solos).
///   6) DESACTIVA el componente OliverSaludoSequencer viejo — los dos escuchan la misma señal
///      (OLIVER_GREETING_START) y se dispararían a la vez. Se desactiva, no se borra, para poder
///      volver atrás con un clic.
///   7) Comprueba que el Persistence ID de Oliver en la escena coincide con el que usa el asset
///      ("NPC_Oliver") y avisa si no, que es el único dato que no se puede verificar desde fuera.
///
/// Al terminar deja un resumen en consola con todo lo que ha enlazado y lo que no ha podido.
/// </summary>
public static class OliverSequenceWiring
{
    private const string DefinitionPath = "Assets/_SEQUENCES/SEQ_OliverSaludo.asset";
    private const string PlayerObjectName = "SEQ_OliverSaludo";
    private const string ExpectedOliverId = "NPC_Oliver";

    [MenuItem("El Sendero/Archivo/Secuencias/Montar secuencia de Oliver (sistema nuevo)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        // ── 1) Localizar el sequencer viejo ──────────────────────────────────
        // ACTUALIZADO (21 sep 2026): el sequencer viejo puede ya no estar en la escena -- si el
        // montaje se ejecutó una vez con éxito y alguien borró el GameObject viejo (ya no hacía
        // falta para nada), este script ya no tiene de dónde copiar referencias. En ese caso no
        // hay que bloquear con un error: se comprueba si "SEQ_OliverSaludo" (el nuevo) ya existe y
        // ya está enlazado, y si no, se intenta recuperar lo que se pueda sin el viejo.
        var legacy = Object.FindAnyObjectByType<OliverSaludoSequencer>(FindObjectsInactive.Include);
        if (legacy == null)
        {
            WireWithoutLegacy(log, warnings);
            return;
        }

        var scene = legacy.gameObject.scene;
        log.AppendLine($"Sequencer viejo encontrado: '{legacy.name}' (escena '{scene.name}').");

        var soLegacy = new SerializedObject(legacy);

        // ── 2) La definición de la secuencia ─────────────────────────────────
        var definition = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(DefinitionPath);
        if (definition == null)
        {
            EditorUtility.DisplayDialog("Montar secuencia de Oliver",
                $"No encuentro el asset de la secuencia en:\n{DefinitionPath}\n\n" +
                "Sin él no hay nada que montar.", "Vale");
            return;
        }
        log.AppendLine($"Definición: '{definition.displayName}' ({definition.phases.Count} fases, " +
                       $"{definition.TotalBeats} beats).");

        // ── 3) El GameObject del reproductor ─────────────────────────────────
        GameObject go = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == PlayerObjectName) { go = root; break; }

        if (go == null)
        {
            go = new GameObject(PlayerObjectName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Montar secuencia de Oliver");
            log.AppendLine($"GameObject '{PlayerObjectName}' creado en la raíz de '{scene.name}'.");
        }
        else
        {
            log.AppendLine($"GameObject '{PlayerObjectName}' ya existía — se reutiliza y se actualiza.");
        }

        // Colocarlo junto al sequencer viejo ayuda a encontrarlo en la Scene View.
        go.transform.position = legacy.transform.position;

        var player = go.GetComponent<SequencePlayer>() ?? Undo.AddComponent<SequencePlayer>(go);
        var stage = go.GetComponent<SequenceStage>() ?? Undo.AddComponent<SequenceStage>(go);

        // ── 4) El escenario: solo la cámara (los planos ya no hacen falta -- ShotBeat calcula
        //      el encuadre solo, ver comentario de cabecera) ────────────────────
        var soStage = new SerializedObject(stage);

        var legacyCamera = soLegacy.FindProperty("_cinematicCamera")?.objectReferenceValue;
        soStage.FindProperty("_cameraDriver").objectReferenceValue = legacyCamera;
        if (legacyCamera == null)
            warnings.Add("El CinematicCameraDriver no se ha podido enlazar (el sequencer viejo lo " +
                         "tenía vacío). Sin él ningún plano calculado hará nada.");

        soStage.ApplyModifiedProperties();
        log.AppendLine("Sequence Stage: camera driver " +
                       (legacyCamera != null ? "enlazado." : "SIN enlazar."));

        // ── 5) El reproductor ────────────────────────────────────────────────
        var soPlayer = new SerializedObject(player);
        soPlayer.FindProperty("_definition").objectReferenceValue = definition;
        soPlayer.FindProperty("_stage").objectReferenceValue = stage;

        CopyReference(soLegacy, soPlayer, "_audioProfile", warnings,
            "el AudioGraphProfile (sin él no sonará la música OLIVER_1)");
        CopyReference(soLegacy, soPlayer, "_entryTransition", warnings,
            "la transición de entrada (sin ella la escena empieza con un corte seco)");
        CopyReference(soLegacy, soPlayer, "_exitTransition", warnings,
            "la transición de salida (sin ella la escena termina con un corte seco)");

        soPlayer.ApplyModifiedProperties();
        log.AppendLine("Sequence Player: definición, stage, perfil de audio y transiciones enlazados.");

        // ── 6) Apagar el viejo ───────────────────────────────────────────────
        if (legacy.enabled)
        {
            Undo.RecordObject(legacy, "Desactivar sequencer viejo de Oliver");
            legacy.enabled = false;
            EditorUtility.SetDirty(legacy);
            log.AppendLine("OliverSaludoSequencer viejo DESACTIVADO (no borrado — para volver atrás " +
                           "basta con volver a marcar su casilla y desactivar el Sequence Player).");
        }
        else
        {
            log.AppendLine("OliverSaludoSequencer viejo ya estaba desactivado.");
        }

        // ── 7) Comprobar el ID de Oliver ─────────────────────────────────────
        CheckOliverId(warnings, log);
        WirePartyMembershipSignals(warnings, log);

        // ── Resumen ──────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;

        var final = new StringBuilder();
        final.AppendLine("=== Montaje de la secuencia de Oliver (sistema nuevo) ===");
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

    /// Copia una referencia de objeto del sequencer viejo al nuevo, avisando si estaba vacía.
    private static void CopyReference(SerializedObject from, SerializedObject to, string field,
        List<string> warnings, string description)
    {
        var src = from.FindProperty(field);
        var dst = to.FindProperty(field);
        if (src == null || dst == null) return;

        dst.objectReferenceValue = src.objectReferenceValue;
        if (src.objectReferenceValue == null)
            warnings.Add($"No se ha podido copiar {description}: el campo '{field}' del sequencer " +
                         "viejo está vacío.");
    }

    /// El asset se refiere a Oliver por su Persistence ID. Es el único dato que no se puede
    /// verificar desde fuera del Editor (la escena está en binario), así que se comprueba aquí.
    /// Enlaza a Oliver los dos PartyMembershipSignal que lo unen/sacan del grupo por guion
    /// (sistema de party, sustituye a la marca "oliverMark" que se usaba antes): uno escucha
    /// OLIVER_JOIN_PARTY (se une nada mas arrancar la mision "busca a Eldran") y otro escucha
    /// PERAS_START (se sale del grupo justo antes de que empiece la secuencia de las peras, donde
    /// se coloca junto a Will por guion). Idempotente: si ya existen, solo revisa que tengan la
    /// clave de evento correcta.
    private static void WirePartyMembershipSignals(List<string> warnings, StringBuilder log)
    {
        Game.NPC.NPCBehaviourManagerV2 oliver = null;
        foreach (var m in Object.FindObjectsByType<Game.NPC.NPCBehaviourManagerV2>(
                     FindObjectsInactive.Include))
        {
            string id = new SerializedObject(m).FindProperty("persistenceId")?.stringValue;
            if (id == ExpectedOliverId) { oliver = m; break; }
        }

        if (oliver == null)
        {
            warnings.Add("No se han podido enlazar los PartyMembershipSignal de Oliver (unirse/" +
                         "salir del grupo) porque no se ha encontrado su Persistence ID en la " +
                         "escena -- ver aviso anterior sobre el ID.");
            return;
        }

        var go = oliver.gameObject;
        var existing = go.GetComponents<Game.NPC.PartyMembershipSignal>();

        EnsurePartyMembershipSignal(go, existing, "OLIVER_JOIN_PARTY",
            Game.NPC.PartyMembershipSignal.SignalAction.Join, log);
        EnsurePartyMembershipSignal(go, existing, "PERAS_START",
            Game.NPC.PartyMembershipSignal.SignalAction.Leave, log);

        EditorUtility.SetDirty(go);
    }

    private static void EnsurePartyMembershipSignal(GameObject go, Game.NPC.PartyMembershipSignal[] existing,
        string eventKey, Game.NPC.PartyMembershipSignal.SignalAction action, StringBuilder log)
    {
        Game.NPC.PartyMembershipSignal target = null;
        foreach (var s in existing)
        {
            var soExisting = new SerializedObject(s);
            if (soExisting.FindProperty("narrativeEventKey")?.stringValue == eventKey) { target = s; break; }
        }

        bool created = false;
        if (target == null)
        {
            target = go.AddComponent<Game.NPC.PartyMembershipSignal>();
            created = true;
        }

        var so = new SerializedObject(target);
        so.FindProperty("narrativeEventKey").stringValue = eventKey;
        so.FindProperty("action").enumValueIndex = (int)action;
        so.ApplyModifiedProperties();

        log.AppendLine((created ? "PartyMembershipSignal creado" : "PartyMembershipSignal ya existía") +
                       $" para '{eventKey}' ({action}) en '{go.name}'.");
    }

    private static void CheckOliverId(List<string> warnings, StringBuilder log)
    {
        var managers = Object.FindObjectsByType<Game.NPC.NPCBehaviourManagerV2>(
            FindObjectsInactive.Include);

        var ids = new List<string>();
        bool found = false;

        foreach (var m in managers)
        {
            string id = new SerializedObject(m).FindProperty("persistenceId")?.stringValue;
            if (string.IsNullOrEmpty(id)) continue;
            ids.Add(id);
            if (id == ExpectedOliverId) found = true;
        }

        if (found)
        {
            log.AppendLine($"Persistence ID '{ExpectedOliverId}' encontrado en la escena — el asset " +
                           "podrá resolver a Oliver en tiempo de ejecución.");
            return;
        }

        ids.Sort();
        warnings.Add($"No hay ningún NPC con el Persistence ID '{ExpectedOliverId}' en las escenas " +
                     "abiertas, que es el que usa el asset para referirse a Oliver. IDs encontrados: " +
                     (ids.Count > 0 ? string.Join(", ", ids) : "(ninguno)") + ". " +
                     "Si Oliver usa otro ID, dímelo y cambio el asset — o ponle ese ID en su " +
                     "componente NPCBehaviourManagerV2.");
    }

    /// Camino de emergencia cuando el OliverSaludoSequencer viejo ya no está en la escena.
    /// Dos casos: (a) "SEQ_OliverSaludo" ya existe y ya está enlazado de un montaje anterior --
    /// no hay nada que hacer, se dice y punto; (b) existe pero le falta el camera driver -- se
    /// intenta recuperar buscando el CinematicCameraDriver compartido de la escena, igual que ya
    /// hace PerasEldranSequenceWiring. El audio profile y las transiciones NO se pueden recuperar
    /// sin el sequencer viejo (no hay otra fuente): si faltan, queda como aviso para rellenarlos a
    /// mano en el Inspector, una vez.
    private static void WireWithoutLegacy(StringBuilder log, List<string> warnings)
    {
        GameObject go = null;
        foreach (var candidate in Object.FindObjectsByType<SequencePlayer>(
                     FindObjectsInactive.Include))
        {
            if (candidate.gameObject.name == PlayerObjectName) { go = candidate.gameObject; break; }
        }

        if (go == null)
        {
            EditorUtility.DisplayDialog("Montar secuencia de Oliver",
                "No he encontrado ningún OliverSaludoSequencer (el viejo) NI ningún " +
                $"'{PlayerObjectName}' (el nuevo) en las escenas abiertas.\n\n" +
                "Abre MainWorld (la escena donde vive la secuencia de Oliver) y vuelve a ejecutar " +
                "esto desde el menú.", "Vale");
            return;
        }

        log.AppendLine($"OliverSaludoSequencer viejo no encontrado, pero '{PlayerObjectName}' ya " +
                       "existe en la escena -- se asume que el montaje ya se hizo antes y se revisa " +
                       "lo que tiene enlazado, sin volver a crear nada.");

        var stage = go.GetComponent<SequenceStage>();
        var player = go.GetComponent<SequencePlayer>();
        if (stage == null || player == null)
        {
            warnings.Add($"'{PlayerObjectName}' existe pero le falta SequenceStage o SequencePlayer " +
                         "-- esto no debería pasar sin el sequencer viejo delante. Dímelo.");
        }
        else
        {
            var soStage = new SerializedObject(stage);
            var cameraProp = soStage.FindProperty("_cameraDriver");
            if (cameraProp.objectReferenceValue == null)
            {
                var sharedDriver = Object.FindAnyObjectByType<CinematicCameraDriver>(
                    FindObjectsInactive.Include);
                if (sharedDriver != null)
                {
                    cameraProp.objectReferenceValue = sharedDriver;
                    soStage.ApplyModifiedProperties();
                    log.AppendLine("Camera driver estaba vacío -- recuperado buscando el " +
                                   "CinematicCameraDriver compartido de la escena.");
                    EditorSceneManager.MarkSceneDirty(go.scene);
                }
                else
                {
                    warnings.Add("El camera driver está vacío y no hay ningún CinematicCameraDriver " +
                                 "en la escena para recuperarlo -- ningún plano calculado hará nada.");
                }
            }
            else
            {
                log.AppendLine("Camera driver: ya estaba enlazado, sin tocar.");
            }

            var soPlayer = new SerializedObject(player);
            if (soPlayer.FindProperty("_definition")?.objectReferenceValue == null)
                warnings.Add("El Sequence Player no tiene definición asignada -- revisa el campo " +
                             "'Definition' en el Inspector.");
            if (soPlayer.FindProperty("_audioProfile")?.objectReferenceValue == null)
                warnings.Add("El AudioGraphProfile está vacío y no se puede recuperar sin el " +
                             "sequencer viejo -- asígnalo a mano en el Inspector si hace falta música.");
            if (soPlayer.FindProperty("_entryTransition")?.objectReferenceValue == null)
                warnings.Add("La transición de entrada está vacía (no se puede recuperar sin el " +
                             "sequencer viejo) -- la escena empezará con un corte seco.");
            if (soPlayer.FindProperty("_exitTransition")?.objectReferenceValue == null)
                warnings.Add("La transición de salida está vacía (no se puede recuperar sin el " +
                             "sequencer viejo) -- la escena terminará con un corte seco.");
        }

        CheckOliverId(warnings, log);
        WirePartyMembershipSignals(warnings, log);

        Selection.activeGameObject = go;

        var final = new StringBuilder();
        final.AppendLine("=== Revisión de la secuencia de Oliver (sin sequencer viejo) ===");
        final.Append(log);

        if (warnings.Count == 0)
        {
            final.AppendLine();
            final.AppendLine("Sin avisos: parece que ya estaba todo enlazado de antes.");
            Debug.Log(final.ToString(), go);
        }
        else
        {
            final.AppendLine();
            final.AppendLine($"--- {warnings.Count} aviso(s), revisar: ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString(), go);
        }
    }
}
