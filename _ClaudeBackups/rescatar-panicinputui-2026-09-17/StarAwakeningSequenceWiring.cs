using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Monta de un tirón el Despertar de la Estrella sobre el sistema de secuencias por datos
/// (SequenceDefinition + SequenceStage + SequencePlayer + StarAwakeningModule), reutilizando las
/// referencias que ya están puestas a mano en el StarAwakeningSequencer viejo.
///
/// Existe porque las escenas están guardadas en formato binario y no se pueden editar desde fuera
/// del Editor. Mismo patrón que OliverSequenceWiring.
///
/// LA DIFERENCIA CON EL MONTAJE DE OLIVER: aquí NO se copia ningún plano de cámara. Los cinco
/// planos del sequencer viejo (camShotEldran, camShotWillProfile, camShotProjectile, camShotTwoShot,
/// camShotWillFinal) y su realineado a mano (AlignSequenceRigToWill) se quedan atrás a propósito:
/// el asset describe los planos y ShotComposer los calcula con los actores donde estén. Por eso el
/// Sequence Stage de esta secuencia queda con la lista de planos VACÍA, y eso es lo correcto.
///
/// QUÉ HACE (todo idempotente — se puede volver a ejecutar sin duplicar nada):
///   1) Busca el StarAwakeningSequencer en las escenas abiertas y lee sus referencias.
///   2) Crea (o reutiliza) el GameObject "SEQ_StarAwakening" en la raíz de esa misma escena.
///   3) Le añade SequencePlayer + SequenceStage + StarAwakeningModule.
///   4) Rellena el Stage: el camera driver y el módulo. Sin planos.
///   5) Rellena el Player: definición, perfil de audio y transiciones. El margen de idle se pone a
///      cero: esta secuencia encadena con el combate y no puede quedarse esperando en negro.
///   6) Rellena el módulo con la mecánica: proyectil, panic input, spawner de Will, shock effects.
///   6-bis) RESCATA los componentes que se quedaron varados en el GameObject viejo. El objeto
///      que alojaba al sequencer está APAGADO en la jerarquía, y tres componentes que la mecánica
///      necesita vivían ahí: PanicInputDetector (su cuenta atrás corre en Update()),
///      ShockEffectsController y CinematicCameraDriver (ambos arrancan corrutinas sobre sí
///      mismos). En un GameObject apagado nada de eso funciona: Update() no se llama y
///      StartCoroutine() falla. Se clonan sobre SEQ_StarAwakening y se reapuntan las
///      referencias. Los originales NO se borran, para no romper la vuelta atrás.
///   7) DESACTIVA el StarAwakeningSequencer viejo — los dos escuchan AWAKEN_START y se dispararían
///      a la vez. Se desactiva, no se borra, para poder volver atrás con un clic.
///   8) Comprueba que el Persistence ID de Eldran en la escena es el que usa el asset.
/// </summary>
public static class StarAwakeningSequenceWiring
{
    private const string DefinitionPath = "Assets/_SEQUENCES/SEQ_StarAwakening.asset";
    private const string PlayerObjectName = "SEQ_StarAwakening";
    private const string ExpectedEldranId = "NPC_Eldran";

    /// Referencias a objetos que se copian tal cual del sequencer viejo al módulo nuevo.
    /// Los nombres coinciden en los dos, así que es la misma cadena a un lado y al otro.
    private static readonly (string field, string description)[] ModuleObjectRefs =
    {
        ("incomingProjectilePrefab", "el prefab del proyectil enemigo (sin él no hay amenaza)"),
        ("explosionVFX",             "el VFX de la explosión"),
        ("playerSpawner",            "el MagicProjectileSpawner de Will (sin él no hay contraataque)"),
        ("cinematicSpellFallback",   "el hechizo de reserva"),
        ("willCastOrigin",           "el punto del que sale el hechizo (la mano de Will)"),
        ("panicInputDetector",       "el PanicInputDetector (sin él no hay prueba que superar)"),
        ("panicInputUI",             "la UI del panic input"),
        ("panicButtonSprite",        "el sprite de respaldo del botón"),
        ("interactIconSet",          "el set de iconos de respaldo"),
        ("shockEffects",             "el ShockEffectsController (aturdimiento y pitido)"),
    };

    /// Valores sueltos (números y enums) que se copian tal cual.
    private static readonly string[] ModuleValues =
    {
        "castSlot", "castAnimState", "willUpperBodyLayer", "castAnimDelay",
        "collisionWaitUnscaled",
        "approachShakeMin", "approachShakeMax", "approachShakeRampSeconds", "approachShakeInterval",
    };

    [MenuItem("El Sendero/Secuencias/Montar Despertar de la Estrella (sistema nuevo)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        // ── 1) Localizar el sequencer viejo ──────────────────────────────────
        var legacy = Object.FindFirstObjectByType<StarAwakeningSequencer>(FindObjectsInactive.Include);
        if (legacy == null)
        {
            EditorUtility.DisplayDialog("Montar el Despertar de la Estrella",
                "No he encontrado ningún StarAwakeningSequencer en las escenas abiertas.\n\n" +
                "Abre la escena donde vive la cinemática del despertar y vuelve a ejecutar esto " +
                "desde el menú.", "Vale");
            return;
        }

        var scene = legacy.gameObject.scene;
        log.AppendLine($"Sequencer viejo encontrado: '{legacy.name}' (escena '{scene.name}').");

        var soLegacy = new SerializedObject(legacy);

        // ── 2) La definición ─────────────────────────────────────────────────
        var definition = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(DefinitionPath);
        if (definition == null)
        {
            EditorUtility.DisplayDialog("Montar el Despertar de la Estrella",
                $"No encuentro el asset de la secuencia en:\n{DefinitionPath}\n\n" +
                "Sin él no hay nada que montar.", "Vale");
            return;
        }
        log.AppendLine($"Definición: '{definition.displayName}' ({definition.phases.Count} fases, " +
                       $"{definition.TotalBeats} beats).");

        // ── 3) El GameObject ─────────────────────────────────────────────────
        GameObject go = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == PlayerObjectName) { go = root; break; }

        if (go == null)
        {
            go = new GameObject(PlayerObjectName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Montar Despertar de la Estrella");
            log.AppendLine($"GameObject '{PlayerObjectName}' creado en la raíz de '{scene.name}'.");
        }
        else
        {
            log.AppendLine($"GameObject '{PlayerObjectName}' ya existía — se reutiliza y se actualiza.");
        }

        go.transform.position = legacy.transform.position;

        var player = go.GetComponent<SequencePlayer>() ?? Undo.AddComponent<SequencePlayer>(go);
        var stage = go.GetComponent<SequenceStage>() ?? Undo.AddComponent<SequenceStage>(go);
        var module = go.GetComponent<StarAwakeningModule>() ?? Undo.AddComponent<StarAwakeningModule>(go);

        // ── 4) El escenario: cámara y módulo, SIN planos ─────────────────────
        var soStage = new SerializedObject(stage);

        // Deliberadamente vacío. Ver la nota de cabecera: los planos los calcula el solver.
        soStage.FindProperty("_shots").arraySize = 0;
        soStage.FindProperty("_marks").arraySize = 0;

        var legacyCamera = soLegacy.FindProperty("_cinematicCamera")?.objectReferenceValue
                           as CinematicCameraDriver;
        if (legacyCamera == null)
            warnings.Add("El CinematicCameraDriver no se ha podido enlazar (el sequencer viejo lo " +
                         "tenía vacío). Sin él ningún plano de esta secuencia hará nada.");

        // MoveTo() y StartFollowing() arrancan corrutinas sobre el propio driver: si vive en el
        // GameObject apagado del sequencer viejo, los cortes funcionan pero cualquier plano con
        // movimiento o seguimiento falla en silencio.
        var camera = RescueIfStranded(legacyCamera, go, log, warnings,
            "el CinematicCameraDriver (los planos con movimiento y los de seguimiento)");
        soStage.FindProperty("_cameraDriver").objectReferenceValue = camera;

        var modules = soStage.FindProperty("_modules");
        modules.arraySize = 1;
        modules.GetArrayElementAtIndex(0).objectReferenceValue = module;

        soStage.ApplyModifiedProperties();
        log.AppendLine("Sequence Stage: camera driver y módulo enlazados. Lista de planos VACÍA a " +
                       "propósito — los calcula ShotComposer con los actores donde estén.");

        // ── 5) El reproductor ────────────────────────────────────────────────
        var soPlayer = new SerializedObject(player);
        soPlayer.FindProperty("_definition").objectReferenceValue = definition;
        soPlayer.FindProperty("_stage").objectReferenceValue = stage;

        // El reproductor toma el driver del stage cuando su propio campo está vacío, pero si alguien
        // arrastró ahí el driver viejo a mano, ese arrastre gana y el rescate del paso 4 no serviría
        // de nada. Se escribe explícitamente el driver bueno.
        var playerCamera = soPlayer.FindProperty("_cinematicCamera");
        if (playerCamera != null) playerCamera.objectReferenceValue = camera;

        // Esta secuencia encadena con el combate: su señal de salida arranca la intro del jefe. Un
        // margen de espera aquí sería tiempo muerto con la pantalla ya en negro.
        soPlayer.FindProperty("_idleGraceAfterSequence").floatValue = 0f;

        CopyReference(soLegacy, soPlayer, "_audioProfile", warnings,
            "el AudioGraphProfile (sin él no sonará la música de la secuencia)");
        CopyReference(soLegacy, soPlayer, "_entryTransition", warnings,
            "la transición de entrada (sin ella la escena empieza con un corte seco)");
        CopyReference(soLegacy, soPlayer, "_exitTransition", warnings,
            "la transición de salida");

        soPlayer.ApplyModifiedProperties();
        log.AppendLine("Sequence Player: definición, stage, perfil de audio y transiciones enlazados. " +
                       "Margen de idle a 0 (encadena con el combate).");

        // ── 6) El módulo: la mecánica ────────────────────────────────────────
        var soModule = new SerializedObject(module);

        int copied = 0;
        foreach (var (field, description) in ModuleObjectRefs)
        {
            var src = soLegacy.FindProperty(field);
            var dst = soModule.FindProperty(field);
            if (src == null || dst == null) continue;

            dst.objectReferenceValue = src.objectReferenceValue;
            if (src.objectReferenceValue != null) copied++;
            else warnings.Add($"No se ha podido copiar {description}: el campo '{field}' del " +
                              "sequencer viejo está vacío.");
        }

        foreach (string field in ModuleValues)
        {
            var src = soLegacy.FindProperty(field);
            var dst = soModule.FindProperty(field);
            if (src != null && dst != null) dst.boxedValue = src.boxedValue;
        }

        // ── 6-bis) Rescatar lo que se quedó en el GameObject apagado ────────
        // Ver la nota de cabecera. Estos dos componentes hacen su trabajo en Update() o en
        // corrutinas propias, así que en un GameObject apagado no hacen nada: el botón del panic
        // input nunca aparece (y la secuencia se queda esperando para siempre) y el aturdimiento
        // y el pitido se pierden sin dar un solo aviso por consola.
        RescueModuleReference<PanicInputDetector>(soModule, "panicInputDetector", go, log, warnings,
            "el PanicInputDetector (sin él el botón de la X no llega a salir)");
        RescueModuleReference<ShockEffectsController>(soModule, "shockEffects", go, log, warnings,
            "el ShockEffectsController (el aturdimiento y el pitido tras el impacto)");

        // El punto de aparición del proyectil se deja VACÍO a propósito: vacío significa
        // "calcúlalo alrededor de Will", que es lo que hace que la escena funcione dondequiera que
        // esté el jugador. El punto fijo del sequencer viejo es justo lo que se está retirando.
        soModule.FindProperty("projectileSpawnPoint").objectReferenceValue = null;

        soModule.ApplyModifiedProperties();
        log.AppendLine($"Módulo de mecánica: {copied}/{ModuleObjectRefs.Length} referencias copiadas, " +
                       $"{ModuleValues.Length} ajustes. Punto de aparición del proyectil vacío a " +
                       "propósito (se calcula alrededor de Will).");

        // ── 7) Apagar el viejo ───────────────────────────────────────────────
        if (legacy.enabled)
        {
            Undo.RecordObject(legacy, "Desactivar sequencer viejo del despertar");
            legacy.enabled = false;
            EditorUtility.SetDirty(legacy);
            log.AppendLine("StarAwakeningSequencer viejo DESACTIVADO (no borrado — para volver atrás " +
                           "basta con volver a marcar su casilla y desactivar el Sequence Player).");
        }
        else
        {
            log.AppendLine("StarAwakeningSequencer viejo ya estaba desactivado.");
        }

        // ── 8) Comprobar el ID de Eldran ─────────────────────────────────────
        CheckEldranId(warnings, log);

        // ── Resumen ──────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;

        var final = new StringBuilder();
        final.AppendLine("=== Montaje del Despertar de la Estrella (sistema nuevo) ===");
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

    /// Clona sobre 'host' un componente que haya quedado varado en un GameObject apagado y
    /// devuelve la copia. Si el original está en un objeto vivo, lo devuelve tal cual: esto no
    /// mueve nada que ya funcione.
    ///
    /// Los originales se dejan donde están a propósito. El GameObject viejo sigue apagado, así que
    /// las copias que quedan ahí no hacen nada, y conservarlas mantiene intacta la vuelta atrás
    /// descrita en el paso 7.
    private static T RescueIfStranded<T>(T original, GameObject host, StringBuilder log,
        List<string> warnings, string description) where T : Component
    {
        if (original == null) return null;
        if (original.gameObject == host) return original;
        if (original.gameObject.activeInHierarchy) return original;

        string strandedName = original.gameObject.name;

        var existing = host.GetComponent<T>();
        if (existing != null)
        {
            log.AppendLine($"{typeof(T).Name}: ya había una copia en '{host.name}' — se reutiliza " +
                           $"(el original sigue varado en '{strandedName}', apagado).");
            return existing;
        }

        if (!ComponentUtility.CopyComponent(original))
        {
            warnings.Add($"No he podido copiar {description} desde '{strandedName}'. Ese GameObject " +
                         "está APAGADO en la jerarquía, así que el componente no va a funcionar: " +
                         $"muévelo a mano a '{host.name}'.");
            return original;
        }

        Undo.RegisterCompleteObjectUndo(host, "Rescatar componentes del despertar");
        if (!ComponentUtility.PasteComponentAsNew(host))
        {
            warnings.Add($"No he podido pegar {description} en '{host.name}'. Muévelo a mano desde " +
                         $"'{strandedName}'.");
            return original;
        }

        var clone = host.GetComponent<T>();
        if (clone == null)
        {
            warnings.Add($"He pegado {description} en '{host.name}' pero no lo encuentro después. " +
                         "Revísalo a mano.");
            return original;
        }

        EditorUtility.SetDirty(host);
        log.AppendLine($"{typeof(T).Name} RESCATADO: vivía en '{strandedName}', que está apagado en " +
                       $"la jerarquía, y ahí {description} no funcionaba. Copiado a '{host.name}' " +
                       "con sus ajustes; el original se deja donde estaba.");
        return clone;
    }

    /// Lo mismo, pero leyendo y reescribiendo un campo del módulo.
    private static void RescueModuleReference<T>(SerializedObject soModule, string field,
        GameObject host, StringBuilder log, List<string> warnings, string description)
        where T : Component
    {
        var prop = soModule.FindProperty(field);
        if (prop == null) return;

        var rescued = RescueIfStranded(prop.objectReferenceValue as T, host, log, warnings, description);
        if (rescued != null) prop.objectReferenceValue = rescued;
    }

    /// El asset se refiere a Eldran por su Persistence ID. Es el único dato que no se puede
    /// verificar desde fuera del Editor, porque la escena está en binario.
    private static void CheckEldranId(List<string> warnings, StringBuilder log)
    {
        var managers = Object.FindObjectsByType<Game.NPC.NPCBehaviourManagerV2>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var ids = new List<string>();
        bool found = false;

        foreach (var m in managers)
        {
            string id = new SerializedObject(m).FindProperty("persistenceId")?.stringValue;
            if (string.IsNullOrEmpty(id)) continue;
            ids.Add(id);
            if (id == ExpectedEldranId) found = true;
        }

        if (found)
        {
            log.AppendLine($"Persistence ID '{ExpectedEldranId}' encontrado en la escena.");
        }
        else
        {
            warnings.Add($"No hay ningún NPC con el Persistence ID '{ExpectedEldranId}' en las " +
                         "escenas abiertas, que es el que usa el asset para Eldran. Sus frases y " +
                         "sus caras no harán nada. IDs presentes: " +
                         (ids.Count > 0 ? string.Join(", ", ids) : "(ninguno)") + ".");
        }
    }
}
