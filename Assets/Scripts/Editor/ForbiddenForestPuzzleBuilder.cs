using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para montar de un tirón el puzle "Sello de las Piedras" propuesto para
/// el Bosque Prohibido (claro secundario, fuera de la ruta obligatoria hacia la escena de
/// Estela): N piedras rúnicas en semicírculo + una roca que bloquea el paso a un cofre, resuelto
/// activando las piedras en el orden que se muestra una vez al acercarse (tipo "Simón dice").
///
/// Deliberadamente NO usa ningún arte nuevo: piedras y roca bloqueadora son props ya presentes
/// en Fantasy_Kingdom_Pack (mismo pack que ya viste el resto del bosque/mundo), y el cofre es
/// Chest01.prefab del mismo pack (con lid propio: Chest01_a01 = base, Chest01_a02 = tapa,
/// confirmado leyendo el .prefab) — hoy sin usar en ningún sitio del proyecto.
///
/// Todo cuelga de un contenedor nuevo bajo el objeto seleccionado (o en el origen de la escena
/// si no hay selección) — volver a pulsar "Generar" borra y recrea ese contenedor, mismo
/// criterio de idempotencia que ArenaHeightVariationBuilder.cs / LiamZoneSpellBuilder.cs.
///
/// PENDIENTE A MANO TRAS EJECUTAR (ver documento de la propuesta para la lista completa):
///   1) Mover el contenedor generado ("SelloDeLasPiedras_BosqueProhibido") a la posición real
///      donde quieras el claro dentro del Bosque Prohibido — se genera en el punto seleccionado
///      o en el origen, no en ningún sitio del bosque en concreto (no hay forma fiable de saber
///      desde aquí dónde hay hueco libre entre los 568 árboles sin abrir el Editor).
///   2) Ajustar a ojo la posición/escala de la roca bloqueadora para que realmente tape el paso
///      hacia el cofre (se coloca con un tamaño de partida razonable, sin ver la geometría real).
///   3) Asignar el ItemData de la recompensa en el WorldPickup del cofre (Effects → Item) — se
///      deja el efecto en tipo "Currency" con cantidad 25 pero SIN ItemData asignado porque no
///      se ha podido localizar con garantías el asset de la moneda del juego desde aquí; con
///      Item vacío, PlayerPickupCollector.ApplyCurrency avisa por consola y no da nada.
///   4) Dar de alta las claves de audio nuevas en AudioService/AudioGraphProfile si quieres SFX
///      real: "RuneActivate", "RuneFail", "RuneSolved" (mismo patrón que castSFXKey en los
///      hechizos de Liam — sin la clave dada de alta, simplemente no suena nada, no rompe nada).
///   5) Playtest: acercarse activa la demostración (las piedras correctas se encienden en orden
///      una vez); repetir el mismo orden interactuando con ellas abre el paso; equivocarse
///      reinicia y repite la demostración.
/// </summary>
public class ForbiddenForestPuzzleBuilder : EditorWindow
{
    const string ContainerName = "SelloDeLasPiedras_BosqueProhibido";

    // Props ya existentes en el proyecto — Fantasy_Kingdom_Pack (mismo pack que el resto del
    // Bosque Prohibido: árboles, montañas de dressing, arenas de combate).
    static readonly string[] StonePrefabPaths =
    {
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Engineering/Stone01_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Engineering/Stone01_a02.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Engineering/Stone02_a01.prefab",
    };
    const string BlockerRockPrefabPath = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Rock/Rock04_a01.prefab";
    const string ChestPrefabPath = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Chest01.prefab";

    GameObject _anchor;
    int _stoneCount = 4;
    float _arcRadius = 4f;
    float _arcSpanDegrees = 150f;
    float _blockerDistance = 6.5f;
    float _chestDistance = 8.5f;
    int _rewardQuantity = 25;
    int _seed = 20260905;

    [MenuItem("El Sendero/Mundo/Crear Puzle de Runas (Bosque Prohibido)...")]
    public static void ShowWindow()
    {
        var window = GetWindow<ForbiddenForestPuzzleBuilder>("Puzle de Runas");
        window._anchor = Selection.activeGameObject;
        window.minSize = new Vector2(380, 380);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Genera el puzle 'Sello de las Piedras' (piedras rúnicas en semicírculo + roca que " +
            "bloquea el paso a un cofre) alrededor del punto indicado. No toca NavMesh ni nada " +
            "de la ruta principal — pensado como contenido 100% opcional. Tras generar, hay que " +
            "reposicionar el conjunto a mano dentro del Bosque Prohibido (ver comentario de " +
            "cabecera del script).",
            MessageType.Info);

        EditorGUILayout.Space();
        _anchor = (GameObject)EditorGUILayout.ObjectField(
            "Punto de anclaje (opcional)", _anchor, typeof(GameObject), true);
        EditorGUILayout.HelpBox(
            "Si se deja vacío, se genera en el origen de la escena (0,0,0) — sin tener acceso al " +
            "Editor no hay forma fiable de saber dónde hay un hueco libre entre los árboles.",
            MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Piedras", EditorStyles.boldLabel);
        _stoneCount = EditorGUILayout.IntSlider("Nº de piedras", _stoneCount, 3, 6);
        _arcRadius = EditorGUILayout.FloatField("Radio del semicírculo", _arcRadius);
        _arcSpanDegrees = EditorGUILayout.FloatField("Ángulo total del arco (grados)", _arcSpanDegrees);
        _seed = EditorGUILayout.IntField("Semilla (orden de la solución)", _seed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bloqueo y recompensa", EditorStyles.boldLabel);
        _blockerDistance = EditorGUILayout.FloatField("Distancia a la roca bloqueadora", _blockerDistance);
        _chestDistance = EditorGUILayout.FloatField("Distancia al cofre", _chestDistance);
        _rewardQuantity = EditorGUILayout.IntField("Cantidad de moneda en el cofre", _rewardQuantity);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generar / Regenerar", GUILayout.Height(30)))
        {
            Generate();
        }

        Transform existing = FindExistingContainer();
        using (new EditorGUI.DisabledScope(existing == null))
        {
            if (GUILayout.Button("Limpiar (borrar lo generado)"))
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
        }
    }

    Transform FindExistingContainer()
    {
        if (_anchor != null)
        {
            var child = _anchor.transform.Find(ContainerName);
            if (child != null) return child;
        }
        var go = GameObject.Find(ContainerName);
        return go != null ? go.transform : null;
    }

    void Generate()
    {
        var existing = FindExistingContainer();
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var stonePrefabs = LoadPrefabs(StonePrefabPaths);
        var blockerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockerRockPrefabPath);
        var chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);

        if (stonePrefabs.Count == 0 || blockerPrefab == null || chestPrefab == null)
        {
            Debug.LogError("[ForbiddenForestPuzzleBuilder] No se pudieron cargar todos los prefabs " +
                            "necesarios — revisa las rutas (Stone*/Rock04_a01/Chest01 de Fantasy_Kingdom_Pack).");
            return;
        }

        var container = new GameObject(ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Generar Puzle de Runas");

        Vector3 originPos = _anchor != null ? _anchor.transform.position : Vector3.zero;
        Quaternion originRot = _anchor != null ? _anchor.transform.rotation : Quaternion.identity;
        container.transform.SetPositionAndRotation(originPos, originRot);

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");

        // ── Piedras en semicírculo, mirando hacia +Z (hacia el "altar"/paso bloqueado) ──
        var stoneComponents = new List<RuneStone>();
        float startAngle = -_arcSpanDegrees * 0.5f;
        for (int i = 0; i < _stoneCount; i++)
        {
            var prefab = stonePrefabs[i % stonePrefabs.Count];
            float t = _stoneCount <= 1 ? 0.5f : (float)i / (_stoneCount - 1);
            float angleDeg = startAngle + _arcSpanDegrees * t;
            var rot = Quaternion.Euler(0f, angleDeg, 0f);
            var localPos = rot * (Vector3.forward * _arcRadius);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            instance.name = $"RuneStone_{i}";
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = Quaternion.LookRotation(-localPos.normalized, Vector3.up);
            Undo.RegisterCreatedObjectUndo(instance, "Generar Puzle de Runas");

            if (interactableLayer >= 0) instance.layer = interactableLayer;

            // Collider de detección para InteractionDetector (usa OverlapSphere sobre el layer
            // "Interactable" con QueryTriggerInteraction.Collide — confirmado en InteractionDetector.cs).
            var col = instance.GetComponent<Collider>();
            if (col == null)
            {
                var sphere = instance.AddComponent<SphereCollider>();
                sphere.radius = 0.8f;
                sphere.center = Vector3.up * 0.6f;
                col = sphere;
            }
            col.isTrigger = true;

            instance.AddComponent<Interactable>();
            var rune = instance.AddComponent<RuneStone>();
            stoneComponents.Add(rune);
        }

        // ── Roca bloqueadora, delante de las piedras ──
        var blockerInstance = (GameObject)PrefabUtility.InstantiatePrefab(blockerPrefab, container.transform);
        blockerInstance.name = "PuzzleBlockerRock";
        blockerInstance.transform.localPosition = Vector3.forward * _blockerDistance;
        blockerInstance.transform.localScale *= 1.8f; // punto de partida — ajustar a ojo a la anchura real del paso
        if (obstacleLayer >= 0) blockerInstance.layer = obstacleLayer;
        Undo.RegisterCreatedObjectUndo(blockerInstance, "Generar Puzle de Runas");
        if (blockerInstance.GetComponent<Collider>() == null)
        {
            var box = blockerInstance.AddComponent<BoxCollider>();
            box.size = new Vector3(3f, 3f, 1.5f); // punto de partida, ajustar a la malla real
        }

        // ── Cofre premio, detrás de la roca ──
        var chestInstance = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, container.transform);
        chestInstance.name = "ChestReward";
        chestInstance.transform.localPosition = Vector3.forward * _chestDistance;
        if (interactableLayer >= 0) chestInstance.layer = interactableLayer;
        Undo.RegisterCreatedObjectUndo(chestInstance, "Generar Puzle de Runas");

        if (chestInstance.GetComponent<Collider>() == null)
        {
            var sphere = chestInstance.AddComponent<SphereCollider>();
            sphere.radius = 1.2f;
            sphere.isTrigger = true;
        }
        var chestInteractable = chestInstance.AddComponent<Interactable>();
        var chestPickup = chestInstance.AddComponent<WorldPickup>();
        var chestScript = chestInstance.AddComponent<ChestInteractable>();

        ConfigureCurrencyEffect(chestPickup, _rewardQuantity);

        // Intento de encontrar la tapa real del prefab (Chest01_a02, confirmado leyendo el
        // .prefab) para la animación de apertura de ChestInteractable — si el nombre no coincide
        // en otra variante de cofre, el campo se queda vacío y ChestInteractable simplemente
        // abre sin animación de tapa (comportamiento ya contemplado en su propio código).
        var lid = FindChildRecursive(chestInstance.transform, "Chest01_a02");
        if (lid != null) SetPrivateField(chestScript, "lidTransform", lid);

        // ── Roca que se aparta al resolver el puzle ──
        var gateGO = new GameObject("PuzzleRewardGate");
        gateGO.transform.SetParent(container.transform, false);
        Undo.RegisterCreatedObjectUndo(gateGO, "Generar Puzle de Runas");
        var gate = gateGO.AddComponent<PuzzleRewardGate>();
        SetPrivateField(gate, "blocker", blockerInstance.transform);

        // ── Controlador del puzle ──
        var puzzleGO = new GameObject("RuneSequencePuzzle");
        puzzleGO.transform.SetParent(container.transform, false);
        Undo.RegisterCreatedObjectUndo(puzzleGO, "Generar Puzle de Runas");
        var puzzle = puzzleGO.AddComponent<RuneSequencePuzzle>();

        int[] solution = BuildShuffledSolution(_stoneCount, _seed);
        puzzle.Configure(stoneComponents.ToArray(), solution);
        SetPrivateField(puzzle, "triggerRadius", _arcRadius + 3f);

        UnityEventTools.AddPersistentListener(puzzle.OnSolved, gate.Open);

        Selection.activeGameObject = container;
        Debug.Log($"[ForbiddenForestPuzzleBuilder] Puzle generado con {_stoneCount} piedras. " +
                  $"Orden de la solución (índices en 'stones'): [{string.Join(", ", solution)}]. " +
                  "Reposiciona el contenedor dentro del Bosque Prohibido y revisa la lista de " +
                  "pendientes en la cabecera del script antes de dar esto por terminado.");
    }

    static int[] BuildShuffledSolution(int stoneCount, int seed)
    {
        var indices = new List<int>();
        for (int i = 0; i < stoneCount; i++) indices.Add(i);

        var rng = new System.Random(seed);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        return indices.ToArray();
    }

    static void ConfigureCurrencyEffect(WorldPickup pickup, int quantity)
    {
        var so = new SerializedObject(pickup);
        var effects = so.FindProperty("effects");
        effects.ClearArray();
        effects.InsertArrayElementAtIndex(0);
        var elem = effects.GetArrayElementAtIndex(0);
        elem.FindPropertyRelative("effectType").enumValueIndex = (int)PickupEffectType.Currency;
        elem.FindPropertyRelative("quantity").intValue = quantity;
        // 'item' (ItemData) se deja sin asignar a propósito — ver pendiente #3 en la cabecera.
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetPrivateField(Object target, string fieldName, object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[ForbiddenForestPuzzleBuilder] No se encontró el campo '{fieldName}' en {target.GetType().Name}.");
            return;
        }

        switch (prop.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                prop.objectReferenceValue = value as Object;
                break;
            case SerializedPropertyType.Float:
                prop.floatValue = (float)value;
                break;
            case SerializedPropertyType.Integer:
                prop.intValue = (int)value;
                break;
            default:
                Debug.LogWarning($"[ForbiddenForestPuzzleBuilder] Tipo de campo no soportado para '{fieldName}'.");
                break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static List<GameObject> LoadPrefabs(string[] paths)
    {
        var list = new List<GameObject>();
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) list.Add(prefab);
            else Debug.LogWarning($"[ForbiddenForestPuzzleBuilder] No se pudo cargar el prefab en '{path}'.");
        }
        return list;
    }
}
