using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Herramienta de Editor para colocar el "cofre mímico" (enemigo nuevo, asset
/// RPGMonsterPartnersPBRPolyart ya importado en Assets/Art/Characters/RPGMonsterPartnersPBRPolyart)
/// en el punto seleccionado de la escena, montando de un tirón todos los componentes que necesita
/// para funcionar (ChestMonsterAI.cs) — el prefab importado tal cual solo trae Transform + Animator
/// + malla (confirmado leyendo el .prefab: ni collider, ni Rigidbody, ni NavMeshAgent, ni nada de
/// combate).
///
/// Mismo criterio que ForbiddenForestPuzzleBuilder.cs: vuelve a pulsar "Generar" y se borra/rehace
/// solo el último objeto creado por esta ventana (no toca nada más de la escena).
///
/// PENDIENTE A MANO TRAS EJECUTAR:
///   1) Reasignar el shader de los materiales del asset (Materials/PBRDefault.mat y
///      Materials/PolyartDefault.mat) — vienen con el Standard de Unity (built-in), que en este
///      proyecto URP se ve magenta/roto. Hay que pasarlos a Quibli/Stylized Lit a mano desde el
///      Editor (los nombres de las texturas no coinciden 1:1 entre shaders, así que esto no se
///      puede hacer a ciegas por archivo — mejor verlo en vivo). El resto de personajes/props del
///      juego ya usan ese shader, así que quedará coherente con el resto una vez hecho.
///   2) Mover el objeto generado a la posición final dentro del Bosque Prohibido (se coloca en el
///      punto de anclaje elegido, o en el origen si no se indica ninguno) y comprobar que el
///      NavMeshAgent tiene NavMesh horneado debajo (si no, no podrá perseguir al despertar).
///   3) Ajustar a ojo el tamaño/centro del BoxCollider (se genera con un tamaño de partida
///      razonable para un cofre, sin ver la malla real en el Editor).
///   4) Opcional: cambiar la recompensa del cofre (por defecto IT_Coin.asset x cantidad indicada
///      abajo) por otro ItemData desde el Inspector (Reward → WorldPickup → Effects → Item).
///   5) Opcional (lore): la idea de Raúl es que algún NPC advierta antes de esto de que "los
///      cofres están embrujados" — eso se resuelve aparte (ver documento de la propuesta), no
///      depende de esta herramienta.
/// </summary>
public class ChestMonsterBuilder : EditorWindow
{
    const string PolyartPrefabPath =
        "Assets/Art/Characters/RPGMonsterPartnersPBRPolyart/Prefabs/Character/ChestMonsterPolyartDefault.prefab";
    const string PBRPrefabPath =
        "Assets/Art/Characters/RPGMonsterPartnersPBRPolyart/Prefabs/Character/ChestMonsterPBRDefault.prefab";
    const string CoinItemPath = "Assets/_ITEMS/IT_Coin.asset";

    GameObject _anchor;
    bool _usePolyartVariant = true;
    string _instanceName = "CofreEmbrujado_BosqueProhibido";
    float _maxHealth = 35f;
    float _damage = 14f;
    float _moveSpeed = 3.2f;
    int _rewardQuantity = 40;

    [MenuItem("El Sendero/Mundo/Colocar Cofre Embrujado (Bosque Prohibido)...")]
    public static void ShowWindow()
    {
        var window = GetWindow<ChestMonsterBuilder>("Cofre Embrujado");
        window._anchor = Selection.activeGameObject;
        window.minSize = new Vector2(380, 340);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Coloca el cofre mímico (RPGMonsterPartnersPBRPolyart) en el punto indicado y le monta " +
            "todo lo necesario para funcionar como enemigo disfrazado (ChestMonsterAI: collider, " +
            "Rigidbody, NavMeshAgent, Damageable, Interactable, recompensa). No toca los materiales " +
            "(shader) ni el NavMesh — revisa la lista de pendientes en la cabecera del script.",
            MessageType.Info);

        EditorGUILayout.Space();
        _anchor = (GameObject)EditorGUILayout.ObjectField(
            "Punto de anclaje (opcional)", _anchor, typeof(GameObject), true);
        EditorGUILayout.HelpBox(
            "Si se deja vacío, se genera en el origen de la escena (0,0,0) — reposiciónalo a mano " +
            "dentro del Bosque Prohibido después.",
            MessageType.None);

        EditorGUILayout.Space();
        _instanceName = EditorGUILayout.TextField("Nombre del objeto", _instanceName);
        _usePolyartVariant = EditorGUILayout.Toggle(
            new GUIContent("Usar variante Polyart", "Si se desactiva, usa la variante PBRDefault en su lugar."),
            _usePolyartVariant);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Combate", EditorStyles.boldLabel);
        _maxHealth = EditorGUILayout.FloatField("Vida máxima", _maxHealth);
        _damage = EditorGUILayout.FloatField("Daño por golpe", _damage);
        _moveSpeed = EditorGUILayout.FloatField("Velocidad al perseguir", _moveSpeed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recompensa", EditorStyles.boldLabel);
        _rewardQuantity = EditorGUILayout.IntField("Cantidad de moneda al morir", _rewardQuantity);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generar / Colocar", GUILayout.Height(30)))
        {
            Generate();
        }
    }

    void Generate()
    {
        string prefabPath = _usePolyartVariant ? PolyartPrefabPath : PBRPrefabPath;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ChestMonsterBuilder] No se pudo cargar el prefab en '{prefabPath}'.");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = string.IsNullOrEmpty(_instanceName) ? prefab.name : _instanceName;
        Undo.RegisterCreatedObjectUndo(instance, "Colocar Cofre Embrujado");

        Vector3 pos = _anchor != null ? _anchor.transform.position : Vector3.zero;
        Quaternion rot = _anchor != null ? _anchor.transform.rotation : Quaternion.identity;
        instance.transform.SetPositionAndRotation(pos, rot);

        // Empieza disfrazado de cofre normal: vive en el layer "Interactable" (mismo layer que
        // cualquier otro cofre/prop interactuable — ChestMonsterAI.WakeUp() lo cambia a "Enemy"
        // al despertar).
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0) instance.layer = interactableLayer;

        // Collider físico único (bloquea el paso y sirve de detección para InteractionDetector a
        // la vez — mismo criterio que el resto de interactuables del proyecto, ver comentario en
        // ForbiddenForestPuzzleBuilder.cs). Punto de partida razonable para un cofre; ajustar a
        // ojo a la malla real.
        var box = instance.AddComponent<BoxCollider>();
        box.size = new Vector3(1f, 1f, 0.7f);
        box.center = new Vector3(0f, 0.5f, 0f);

        // Rigidbody cinemático — mismo patrón que Spider1.prefab (no lo mueve la física, pero
        // permite que golpes/proyectiles registren el impacto correctamente contra un collider no
        // trigger).
        var rb = instance.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // NavMeshAgent — deshabilitado hasta despertar (ChestMonsterAI.Awake() ya lo desactiva en
        // runtime; aquí se deja con parámetros razonables calcados de Spider1.prefab).
        var agent = instance.AddComponent<NavMeshAgent>();
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.acceleration = 8f;
        agent.angularSpeed = 120f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        // Damageable — destroyOnDeath se deja en false a nivel de componente (ChestMonsterAI
        // también lo fuerza en runtime por si acaso; ver comentario en Start()).
        var damageable = instance.AddComponent<Damageable>();
        SetSerializedField(damageable, "maxHealth", _maxHealth);
        SetSerializedField(damageable, "destroyOnDeath", false);
        SetSerializedField(damageable, "invulnerabilitySeconds", 0.5f);

        // Interactable — sin configurar nada más (mismo criterio que RuneStone en
        // ForbiddenForestPuzzleBuilder.cs: con los valores por defecto ya dispara OnInteract).
        instance.AddComponent<Interactable>();

        // Recompensa: hijo oculto con WorldPickup, igual que el cofre del puzle de runas.
        var rewardGO = new GameObject("Reward");
        rewardGO.transform.SetParent(instance.transform, false);
        Undo.RegisterCreatedObjectUndo(rewardGO, "Colocar Cofre Embrujado");
        var rewardPickup = rewardGO.AddComponent<WorldPickup>();
        ConfigureCurrencyEffect(rewardPickup, _rewardQuantity);
        rewardGO.SetActive(false);

        // IA — al final, para que ya encuentre el resto de componentes vía GetComponent en Awake().
        var chestAI = instance.AddComponent<ChestMonsterAI>();
        SetSerializedField(chestAI, "damage", _damage);
        SetSerializedField(chestAI, "moveSpeed", _moveSpeed);
        SetSerializedField(chestAI, "rewardOnDeath", rewardGO);

        Selection.activeGameObject = instance;
        Debug.Log($"[ChestMonsterBuilder] '{instance.name}' colocado en {pos}. Revisa la lista de " +
                  "pendientes en la cabecera del script (shader de los materiales, NavMesh, tamaño " +
                  "del collider) antes de darlo por terminado.");
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

        var coinItem = AssetDatabase.LoadAssetAtPath<ItemData>(CoinItemPath);
        if (coinItem != null)
        {
            elem.FindPropertyRelative("item").objectReferenceValue = coinItem;
        }
        else
        {
            Debug.LogWarning($"[ChestMonsterBuilder] No se pudo cargar '{CoinItemPath}' — el efecto " +
                              "de recompensa se deja sin ItemData, asígnalo a mano.");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedField(Object target, string fieldName, object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[ChestMonsterBuilder] No se encontró el campo '{fieldName}' en {target.GetType().Name}.");
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
            case SerializedPropertyType.Boolean:
                prop.boolValue = (bool)value;
                break;
            default:
                Debug.LogWarning($"[ChestMonsterBuilder] Tipo de campo no soportado para '{fieldName}'.");
                break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
