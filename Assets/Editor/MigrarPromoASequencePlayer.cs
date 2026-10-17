using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.NPC.Common;

/// <summary>
/// Prepara la promo en una copia independiente del mundo y con un grafo narrativo propio.
/// </summary>
public static class MigrarPromoASequencePlayer
{
    private const string SourceScenePath = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string MarketingFolder = "Assets/Scenes/Marketing";
    private const string MarketingScenePath = MarketingFolder + "/PromoMundo.unity";
    private const string DefinitionPath = "Assets/_SEQUENCES/SEQ_Promo_EncuentroEnElCamino.asset";
    private const string GuionPath = "Assets/_SEQUENCES/Guiones/Promo_EncuentroEnElCamino.guion.txt";
    private const string GraphFolder = "Assets/NarrativeGraph/Marketing";
    private const string GraphPath = GraphFolder + "/PromoNewsletter.asset";
    private const string StageObjectName = "Escenario_Cinematica_Promo";
    private const string CastRootName = "Reparto_Promo";
    private const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";
    private const string LiamPrefabPath = "Assets/Prefabs/_LIAM.prefab";
    private const string EstelaPrefabPath = "Assets/Prefabs/_ESTELA.prefab";
    private const string AnchorName = "Anchor_Eldran_Bosque";
    private const string Player = "PromoWill";
    private const string Liam = "PromoLiam";
    private const string Estela = "PromoEstela";

    [MenuItem("El Sendero/Marketing/Montar escena completa de promo")]
    public static void Migrar()
    {
        try
        {
            Montar();
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogException(ex);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Montaje de promo", "No se pudo montar la escena. Consulta la consola de Unity para ver el detalle.", "Vale");
        }
    }

    private static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("La escena de marketing se prepara fuera de Play Mode.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
        {
            Debug.LogError($"No se encuentra la escena de origen: {SourceScenePath}.");
            return;
        }

        AsegurarCarpeta("Assets/Scenes", "Marketing");
        AsegurarCarpeta("Assets/NarrativeGraph", "Marketing");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MarketingScenePath) == null
            && !AssetDatabase.CopyAsset(SourceScenePath, MarketingScenePath))
        {
            Debug.LogError($"No se pudo crear la copia independiente de MainWorld en {MarketingScenePath}.");
            return;
        }

        var definition = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(DefinitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<SequenceDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }
        Configurar(definition);
        EditorUtility.SetDirty(definition);

        var graph = PrepararGrafo();
        var scene = EditorSceneManager.OpenScene(MarketingScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"No se pudo abrir {MarketingScenePath}.");
            return;
        }
        SceneManager.SetActiveScene(scene);

        var anchor = BuscarTransform(AnchorName);
        if (anchor == null)
        {
            Debug.LogError($"La copia de MainWorld no contiene '{AnchorName}'. La copia se conserva sin guardar para no inventar una localización de rodaje.");
            return;
        }

        var template = BuscarPlantilla();
        if (template == null)
        {
            Debug.LogError("No se encontró el SequencePlayer 'SEQ_PerasEldran' en PromoMundo; se necesita su rig de cámara y SequenceStage como plantilla.");
            return;
        }

        AislarPromoDelArranqueDeJuego(scene);
        var reparto = ObtenerOCrearHijo(anchor, CastRootName);
        Transform raizReparto = reparto != null ? reparto.transform : null;
        if (raizReparto == null)
        {
            Debug.LogError("No se pudo preparar el contenedor del reparto de la promo.");
            return;
        }

        var estela = ColocarPersonaje(raizReparto, EstelaPrefabPath, "Promo Estela",
            anchor.position + anchor.right * -1.35f, anchor.position);
        var liam = ColocarPersonaje(raizReparto, LiamPrefabPath, "Promo Liam",
            anchor.position + anchor.right * 1.35f, anchor.position);
        var will = ColocarPersonaje(raizReparto, WillPrefabPath, "Promo Will",
            anchor.position + anchor.forward * -1.45f + Vector3.up * 0.02f, anchor.position + anchor.forward * 2f);

        if (estela == null || liam == null || will == null) return;

        var stage = PrepararStage(scene, template, will, liam, estela, anchor);
        var player = PrepararPlayer(scene, template, stage, definition);
        if (player.GetComponent<PromoStudioUISuppressor>() == null)
            player.gameObject.AddComponent<PromoStudioUISuppressor>();
        PrepararRunner(graph, scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!PrepararGuion()) return;
        AssetDatabase.Refresh();
        var horneado = HorneadorDeGuion.Hornear(GuionPath, storyboard: false);
        if (horneado == null)
        {
            Debug.LogError("La escena está montada, pero no se ha podido hornear el guion. Revisa el parte en Guiones/PromoMundo/Promo_EncuentroEnElCamino/informe.md.");
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Montaje de promo", "La escena quedó preparada, pero falló el horneado del guion. Consulta Guiones/PromoMundo/Promo_EncuentroEnElCamino/informe.md.", "Vale");
            return;
        }
        AssetDatabase.Refresh();
        Debug.Log($"Montaje completado en '{scene.name}'. {HorneadorDeGuion.UltimoResumen} Grafo local: '{graph.name}'. SequencePlayer: {player.name}.");
    }

    private static void Configurar(SequenceDefinition definition)
    {
        definition.displayName = "Promo — Encuentro en el camino";
        definition.summary = "Will, Liam y Estela paran un momento en el sendero y bromean sobre la cena antes de invitar a suscribirse a la newsletter. Las primeras veinte suscripciones reciben una copia del juego.";
        definition.signalIn = "PROMO_VIDEO_01_START";
        definition.signalOut = "PROMO_VIDEO_01_END";
        definition.musicId = "";
        definition.presentacionDeTexto = PresentacionDeTexto.Subtitulo;
        definition.protagonistas = new List<string> { Player, Liam, Estela };
        definition.openingShotName = "";
        definition.openingShot = new ShotFraming { type = ShotType.Wide };
        definition.endStayBlack = false;
        definition.modulos = null;
        var baked = AssetDatabase.LoadAssetAtPath<GuionHorneado>(
            "Assets/_SEQUENCES/Guiones/Promo_EncuentroEnElCamino_Horneado.asset");
        definition.phases = new List<SequencePhase>
        {
            new()
            {
                name = "Guion: Promo_EncuentroEnElCamino",
                beats = new List<SequenceBeat>
                {
                    new GuionBeat { guion = baked, note = "Guion: Promo_EncuentroEnElCamino" }
                }
            }
        };
    }

    private static NarrativeGraph PrepararGrafo()
    {
        var graph = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(GraphPath);
        if (graph == null)
        {
            graph = ScriptableObject.CreateInstance<NarrativeGraph>();
            AssetDatabase.CreateAsset(graph, GraphPath);
        }

        var start = new StartNode
        {
            displayTitle = "Inicio de la promo",
            chapter = "Marketing",
            position = new Vector2(0, 0)
        };
        var cinematic = new PlayCinematicNode
        {
            displayTitle = "Encuentro en el camino",
            chapter = "Marketing",
            position = new Vector2(300, 0),
            cinematicName = "Promo — Encuentro en el camino",
            signalIn = "PROMO_VIDEO_01_START",
            signalDone = "PROMO_VIDEO_01_END",
            signalFailed = "PROMO_VIDEO_01_FAILED",
            secuencia = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(DefinitionPath),
            plantillaDeAjustes = "SEQ_PerasEldran",
            acercarActor = ""
        };
        start.outputs = new List<string> { cinematic.guid };
        cinematic.outputs = new List<string> { "", "" };
        graph.startNodeGuid = start.guid;
        graph.nodes = new List<NarrativeNode> { start, cinematic };
        graph.applicableSceneNames = new List<string> { "PromoMundo" };
        EditorUtility.SetDirty(graph);
        return graph;
    }

    private static void PrepararRunner(NarrativeGraph graph, Scene scene)
    {
        var root = ObtenerOCrearRaiz(scene, "Grafo local de marketing");
        var runner = root.GetComponent<NarrativeRunner>() ?? root.AddComponent<NarrativeRunner>();
        var runnerObject = new SerializedObject(runner);
        runnerObject.FindProperty("graph").objectReferenceValue = graph;
        runnerObject.ApplyModifiedPropertiesWithoutUndo();

        var starter = root.GetComponent<NarrativeGraphStarter>() ?? root.AddComponent<NarrativeGraphStarter>();
        var starterObject = new SerializedObject(starter);
        starterObject.FindProperty("localRunner").objectReferenceValue = runner;
        starterObject.FindProperty("startOnAwake").boolValue = true;
        starterObject.FindProperty("requireActiveScene").boolValue = true;
        starterObject.FindProperty("logDebug").boolValue = false;
        starterObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SequencePlayer PrepararPlayer(Scene scene, SequencePlayer template, SequenceStage stage,
        SequenceDefinition definition)
    {
        var root = ObtenerOCrearRaiz(scene, "Reproductor de promo");
        var player = root.GetComponent<SequencePlayer>() ?? root.AddComponent<SequencePlayer>();
        EditorUtility.CopySerialized(template, player);
        var playerObject = new SerializedObject(player);
        playerObject.FindProperty("_definition").objectReferenceValue = definition;
        playerObject.FindProperty("_stage").objectReferenceValue = stage;
        playerObject.ApplyModifiedPropertiesWithoutUndo();
        return player;
    }

    private static SequenceStage PrepararStage(Scene scene, SequencePlayer template, GameObject will,
        GameObject liam, GameObject estela, Transform anchor)
    {
        var root = ObtenerOCrearRaiz(scene, StageObjectName);
        var stage = root.GetComponent<SequenceStage>() ?? root.AddComponent<SequenceStage>();
        var templateObject = new SerializedObject(template);
        var stageReference = templateObject.FindProperty("_stage");
        var templateStage = stageReference != null
            ? stageReference.objectReferenceValue as SequenceStage
            : null;
        if (templateStage == null) templateStage = template.GetComponent<SequenceStage>();
        if (templateStage != null) EditorUtility.CopySerialized(templateStage, stage);

        var serialized = new SerializedObject(stage);
        var actors = serialized.FindProperty("_actors");
        actors.arraySize = 3;
        AsignarActor(actors.GetArrayElementAtIndex(0), Player, will);
        AsignarActor(actors.GetArrayElementAtIndex(1), Liam, liam);
        AsignarActor(actors.GetArrayElementAtIndex(2), Estela, estela);
        serialized.FindProperty("_shots").arraySize = 0;
        serialized.FindProperty("_props").arraySize = 0;
        var marks = serialized.FindProperty("_marks");
        var markNames = new[] { 
            "M_Promo_Will", "M_Promo_Liam", "M_Promo_Estela",
            "M_Promo_Will_Entrada", "M_Promo_Liam_Entrada", "M_Promo_Estela_Entrada",
            "M_Promo_Will_Camara", "M_Promo_Liam_Sentado"
        };
        var markPositions = new[] { 
            will.transform.position, 
            liam.transform.position, 
            estela.transform.position,
            // Entrada andando desde atrás por el camino
            will.transform.position - anchor.forward * 4.2f + anchor.right * 0.2f,
            liam.transform.position - anchor.forward * 4.0f + anchor.right * 0.4f,
            estela.transform.position - anchor.forward * 4.4f - anchor.right * 0.3f,
            // Will corriendo hacia el primer término a cámara
            will.transform.position + anchor.forward * 1.8f,
            // Liam descansando en el lateral
            liam.transform.position + anchor.right * 0.5f - anchor.forward * 0.3f
        };
        marks.arraySize = markNames.Length;
        for (int i = 0; i < markNames.Length; i++)
        {
            var mark = root.transform.Find(markNames[i]);
            if (mark == null)
            {
                mark = new GameObject(markNames[i]).transform;
                mark.SetParent(root.transform, true);
            }
            mark.SetPositionAndRotation(markPositions[i], anchor.rotation);
            var entry = marks.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("name").stringValue = markNames[i];
            entry.FindPropertyRelative("target").objectReferenceValue = mark;
        }
        serialized.FindProperty("_modules").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return stage;
    }

    private static void AsignarActor(SerializedProperty element, string id, GameObject actor)
    {
        element.FindPropertyRelative("id").stringValue = id;
        element.FindPropertyRelative("target").objectReferenceValue = actor.transform;
        element.FindPropertyRelative("eyeHeight").floatValue = MedirAlturaOjos(actor.transform);
        element.FindPropertyRelative("animator").objectReferenceValue = actor.GetComponentInChildren<Animator>(true);
        element.FindPropertyRelative("emotionController").objectReferenceValue =
            EmotionControllerResolver.Resolve(actor);
    }

    private static GameObject ColocarPersonaje(Transform parent, string prefabPath, string nombre,
        Vector3 position, Vector3 lookAt)
    {
        var existing = parent.Find(nombre);
        GameObject actor;
        if (existing != null)
        {
            actor = existing.gameObject;
        }
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"No se encuentra el prefab visual: {prefabPath}.");
                return null;
            }

            var holder = new GameObject("Contenedor temporal inactivo");
            holder.SetActive(false);
            try
            {
                actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                actor.name = nombre;
                actor.transform.SetParent(parent, false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        PrepararSoloVisual(actor);
        actor.transform.position = position;
        var direction = lookAt - position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            actor.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        return actor;
    }

    private static void PrepararSoloVisual(GameObject actor)
    {
        foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour is NPCEmotionController || behaviour is NPCSimpleAnimator || behaviour is PlayerDialogueAnimator) continue;
            behaviour.enabled = false;
        }

        var npcAnim = actor.GetComponentInChildren<NPCSimpleAnimator>(true);
        if (npcAnim != null) npcAnim.enabled = true;
        var playerAnim = actor.GetComponentInChildren<PlayerDialogueAnimator>(true);
        if (playerAnim != null) playerAnim.enabled = true;

        foreach (var collider in actor.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (var agent in actor.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            agent.enabled = false;
        foreach (var listener in actor.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
    }

    private static float MedirAlturaOjos(Transform actor)
    {
        var animator = actor.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman && animator.avatar != null)
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) return Mathf.Max(0.1f, head.position.y - actor.position.y);
        }
        return SequenceActor.FallbackEyeHeight;
    }

    private static SequencePlayer BuscarPlantilla()
    {
        foreach (var player in UnityEngine.Object.FindObjectsByType<SequencePlayer>(FindObjectsInactive.Include))
            if (player != null && player.name == "SEQ_PerasEldran") return player;
        return null;
    }

    private static void AislarPromoDelArranqueDeJuego(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            // La copia conserva el escenario, pero el perfil de arranque no debe cargar WillHouse ni su grafo.
            foreach (var bootstrap in root.GetComponentsInChildren<WorldBootstrap>(true))
                UnityEngine.Object.DestroyImmediate(bootstrap, true);
            foreach (var starter in root.GetComponentsInChildren<NarrativeGraphStarter>(true))
                UnityEngine.Object.DestroyImmediate(starter, true);
            foreach (var setup in root.GetComponentsInChildren<NarrativeAutoSetup>(true))
                UnityEngine.Object.DestroyImmediate(setup, true);
            foreach (var hub in root.GetComponentsInChildren<NarrativeGraphHub>(true))
                UnityEngine.Object.DestroyImmediate(hub, true);
        }
    }

    private static Transform BuscarTransform(string nombre)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == nombre) return root.transform;
            var children = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
                if (child.name == nombre) return child;
        }
        return null;
    }

    private static GameObject ObtenerOCrearRaiz(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static GameObject ObtenerOCrearHijo(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void AsegurarCarpeta(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static bool PrepararGuion()
    {
        if (!File.Exists(GuionPath))
        {
            Debug.LogError($"Falta el guion editable de la promo: {GuionPath}.");
            return false;
        }
        AssetDatabase.ImportAsset(GuionPath, ImportAssetOptions.ForceUpdate);
        return true;
    }
}
