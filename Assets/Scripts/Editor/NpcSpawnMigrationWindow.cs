using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta para migrar NPCs colocados a mano en la escena al sistema de spawn por datos
/// (NpcSpawnPoint + NpcRosterSO + NpcSpawner).
///
/// Existe porque MainWorld.unity está guardada en BINARIO: no se puede editar desde fuera del
/// Editor, así que cada paso de la migración tiene que pasar por aquí.
///
/// El flujo por NPC es de tres pasos y se hace de uno en uno, con prueba en juego entre medias:
///   1. Crear marcador + entrada de roster  (no toca al NPC: sigue en la escena)
///   2. Buscar referencias en escena         (el riesgo real — sequencers viejos)
///   3. Quitar de la escena                  (solo se habilita si 1 está hecho)
///
/// Ver claude/propuesta-sistema-spawn-npcs-por-datos-2026-09-16.md
/// </summary>
public class NpcSpawnMigrationWindow : EditorWindow
{
    private const string TargetSceneName = "MainWorld";
    private const string RosterFolder = "Assets/Resources/NpcRosters";
    private const string MarkersContainerName = "--- NPC SPAWN POINTS ---";

    private NpcRosterSO _roster;
    private Vector2 _scroll;
    private readonly List<Row> _rows = new();
    private string _lastReport;

    // Acción pedida por un botón durante OnGUI. NO se ejecuta en el sitio: crear un marcador o
    // quitar un NPC llama a Scan(), que vacía y rellena _rows — y _rows es justo la lista que el
    // foreach de OnGUI está recorriendo en ese momento. Eso reventaba con
    // "InvalidOperationException: Collection was modified" y dejaba el layout de IMGUI a medias
    // ("Invalid GUILayout state"). Se guarda aquí y se ejecuta al final de OnGUI, con el dibujado
    // ya terminado.
    private System.Action _pendingAction;

    private class Row
    {
        public NPCBehaviourManagerV2 npc;
        public string sceneName;
        public GameObject prefabSource;
        public string prefabPath;
        public NpcSpawnPoint marker;
        public NpcRosterSO.Entry entry;
        public int sceneReferences = -1;   // -1 = sin comprobar
        public string spawnId;
    }

    [MenuItem("El Sendero/Archivo/NPCs/Spawn por datos/Migrar NPCs de la escena")]
    public static void Open()
    {
        var w = GetWindow<NpcSpawnMigrationWindow>("Spawn NPCs");
        w.minSize = new Vector2(760, 420);
        w.AutoResolveRoster();
        w.Scan();
    }

    /// <summary>
    /// La única escena con la que trabaja esta herramienta. Por decisión de diseño la migración
    /// se limita a MainWorld: es la escena grande, la binaria y la que hay que limpiar. Las demás
    /// (interiores, Sendero, WillHouse…) se quedan como están.
    /// </summary>
    private static Scene GetTargetScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name == TargetSceneName) return s;
        }
        return default;
    }

    private static bool TargetSceneOpen => GetTargetScene().IsValid();

    // ── Roster ───────────────────────────────────────────────────────────────

    private void AutoResolveRoster()
    {
        if (_roster != null) return;
        var guids = AssetDatabase.FindAssets("t:NpcRosterSO");
        if (guids.Length > 0)
            _roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private void CreateRoster()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(RosterFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "NpcRosters");

        string path = AssetDatabase.GenerateUniqueAssetPath($"{RosterFolder}/NpcRoster_{TargetSceneName}.asset");

        var roster = CreateInstance<NpcRosterSO>();
        roster.rosterId = TargetSceneName;
        AssetDatabase.CreateAsset(roster, path);
        AssetDatabase.SaveAssets();

        _roster = roster;
        Debug.Log($"[SpawnNPCs] Roster creado en {path}. Al estar bajo Resources/NpcRosters, NpcSpawner lo carga solo.");
        Scan();
    }

    // ── Escaneo ──────────────────────────────────────────────────────────────

    private void Scan()
    {
        _rows.Clear();

        var target = GetTargetScene();
        if (!target.IsValid())
        {
            Repaint();
            return;
        }

        var npcs = Object.FindObjectsByType<NPCBehaviourManagerV2>(FindObjectsInactive.Include);
        foreach (var npc in npcs.OrderBy(n => n.gameObject.name))
        {
            if (npc == null) continue;
            if (EditorUtility.IsPersistent(npc)) continue;             // es un prefab, no una instancia
            if (npc.gameObject.scene != target) continue;              // solo MainWorld

            var prefabSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(npc.gameObject);

            var row = new Row
            {
                npc = npc,
                sceneName = npc.gameObject.scene.name,
                prefabSource = prefabSource,
                prefabPath = prefabSource != null ? AssetDatabase.GetAssetPath(prefabSource) : null,
                spawnId = "SPAWN_" + npc.gameObject.name,
            };

            row.entry = FindEntryFor(npc.gameObject.name);
            if (row.entry != null) row.spawnId = row.entry.spawnId;
            row.marker = FindMarker(row.spawnId);

            _rows.Add(row);
        }

        Repaint();
    }

    private NpcRosterSO.Entry FindEntryFor(string goName)
    {
        if (_roster == null || _roster.entries == null) return null;
        return _roster.entries.FirstOrDefault(e => e != null && e.gameObjectName == goName);
    }

    private static NpcSpawnPoint FindMarker(string spawnId)
    {
        if (string.IsNullOrEmpty(spawnId)) return null;
        var target = GetTargetScene();
        var all = Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsInactive.Include);
        return all.FirstOrDefault(p => p != null && p.spawnId == spawnId && p.gameObject.scene == target);
    }

    // ── Paso 1: marcador + entrada ───────────────────────────────────────────

    private void CreateMarkerAndEntry(Row row)
    {
        if (_roster == null)
        {
            EditorUtility.DisplayDialog("Falta el roster", "Crea primero el NpcRosterSO (botón de arriba).", "Vale");
            return;
        }

        if (row.prefabSource == null)
        {
            EditorUtility.DisplayDialog(
                "NPC sin prefab",
                $"'{row.npc.gameObject.name}' no viene de ningún prefab, así que no se puede instanciar por datos.\n\n" +
                "Conviértelo antes en prefab (arrástralo a la carpeta de prefabs de NPC).",
                "Vale");
            return;
        }

        var go = row.npc.gameObject;
        var scene = go.scene;

        // 1) Marcador, dentro de un contenedor para no ensuciar la raíz de la escena.
        if (row.marker == null)
        {
            var container = FindOrCreateContainer(scene);   // scene == MainWorld (ver Scan)

            var markerGo = new GameObject("SP_" + go.name);
            Undo.RegisterCreatedObjectUndo(markerGo, "Crear NpcSpawnPoint");
            markerGo.transform.SetParent(container.transform, false);
            markerGo.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);

            var marker = markerGo.AddComponent<NpcSpawnPoint>();
            marker.spawnId = row.spawnId;
            marker.applyRotation = true;
            row.marker = marker;

            EditorSceneManager.MarkSceneDirty(scene);
        }
        else
        {
            // Ya existía: resincronizar posición y rotación con las del NPC actual.
            Undo.RecordObject(row.marker.transform, "Actualizar NpcSpawnPoint");
            row.marker.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // 2) Entrada del roster.
        if (row.entry == null)
        {
            row.entry = new NpcRosterSO.Entry();
            Undo.RecordObject(_roster, "Añadir entrada al roster");
            _roster.entries.Add(row.entry);
        }
        else
        {
            Undo.RecordObject(_roster, "Actualizar entrada del roster");
        }

        row.entry.enabled = true;
        row.entry.spawnId = row.spawnId;
        row.entry.prefab = row.prefabSource;
        // ⚠️ El nombre EXACTO, tal cual está hoy en la escena. Es lo que usa el sistema de
        // guardado (GameBootProfile lo identifica por gameObject.name, no por persistenceId),
        // así que se copia del objeto real y nunca se escribe a mano.
        row.entry.gameObjectName = go.name;
        row.entry.persistenceId = row.npc.PersistenceId;   // vacío = se respeta el del prefab
        row.entry.startActive = go.activeSelf;             // un NPC desactivado debe seguir desactivado

        EditorUtility.SetDirty(_roster);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SpawnNPCs] '{go.name}' → marcador '{row.spawnId}' + entrada en el roster. " +
                  $"prefab: {row.prefabPath} · gameObjectName: '{go.name}' · persistenceId: '{row.entry.persistenceId}'");

        Scan();
    }

    private static GameObject FindOrCreateContainer(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == MarkersContainerName) return root;

        var container = new GameObject(MarkersContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Crear contenedor de marcadores");
        SceneManager.MoveGameObjectToScene(container, scene);
        return container;
    }

    // ── Paso 2: referencias en escena ────────────────────────────────────────

    /// <summary>
    /// Busca qué componentes de las escenas abiertas apuntan a este NPC (a su GameObject, a su
    /// Transform o a cualquiera de sus hijos). Es el riesgo del §3.4: los sequencers viejos
    /// guardan Transforms directos y se quedarían en null al sacar el NPC de la escena.
    /// </summary>
    private void FindSceneReferences(Row row)
    {
        var targets = new HashSet<Object>();
        foreach (var t in row.npc.GetComponentsInChildren<Transform>(true))
        {
            targets.Add(t);
            targets.Add(t.gameObject);
        }
        foreach (var c in row.npc.GetComponentsInChildren<Component>(true))
            targets.Add(c);

        var scene = GetTargetScene();
        var sb = new StringBuilder($"=== Referencias a '{row.npc.gameObject.name}' en {TargetSceneName} ===\n\n");
        int hits = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                if (targets.Contains(comp)) continue;                  // el propio NPC no cuenta

                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var val = it.objectReferenceValue;
                    if (val == null || !targets.Contains(val)) continue;

                    hits++;
                    sb.AppendLine($"  • {GetPath(comp.transform)}  ({comp.GetType().Name}.{it.propertyPath})");
                    sb.AppendLine($"      → apunta a: {val.name} [{val.GetType().Name}]");
                }
                so.Dispose();
            }
        }

        row.sceneReferences = hits;

        if (hits == 0)
        {
            sb.AppendLine($"  Ninguna en {TargetSceneName}. Se puede sacar de la escena sin romper referencias directas.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"  ⚠️ {hits} referencia(s). Cada una se quedará en NULL al quitar el NPC de la escena.");
            sb.AppendLine("  Hay que migrarlas a resolución por id (como hace SequencePlayer) antes de seguir.");
        }

        _lastReport = sb.ToString();
        Debug.Log(_lastReport);
        Repaint();
    }

    private static string GetPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }

    // ── Paso 3: quitar de la escena ──────────────────────────────────────────

    private void RemoveFromScene(Row row)
    {
        string name = row.npc.gameObject.name;

        string warn = row.sceneReferences > 0
            ? $"\n\n⚠️ Este NPC tiene {row.sceneReferences} referencia(s) directa(s) en escena que se quedarán en NULL."
            : row.sceneReferences < 0
                ? "\n\n⚠️ Todavía NO has buscado referencias (paso 2)."
                : "";

        if (!EditorUtility.DisplayDialog(
                $"Quitar '{name}' de la escena",
                $"Se borrará '{name}' de '{row.sceneName}'.\n\n" +
                $"A partir de ahora lo instanciará NpcSpawner sobre el marcador '{row.spawnId}'." + warn +
                "\n\nSe puede deshacer con Ctrl+Z mientras no guardes la escena.",
                "Quitar", "Cancelar"))
            return;

        var scene = row.npc.gameObject.scene;
        Undo.DestroyObjectImmediate(row.npc.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[SpawnNPCs] '{name}' quitado de '{scene.name}'. Prueba en Play y GUARDA la escena solo si va bien.");
        Scan();
    }

    // ── GUI ──────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            _roster = (NpcRosterSO)EditorGUILayout.ObjectField("Roster", _roster, typeof(NpcRosterSO), false);
            if (GUILayout.Button("Crear roster", GUILayout.Width(110))) _pendingAction = CreateRoster;
            if (GUILayout.Button("Reescanear", GUILayout.Width(90))) _pendingAction = Scan;
        }

        if (_roster != null)
        {
            string path = AssetDatabase.GetAssetPath(_roster);
            if (!path.Replace('\\', '/').Contains("/Resources/"))
                EditorGUILayout.HelpBox(
                    "El roster NO está bajo una carpeta Resources, así que NpcSpawner no lo encontrará en runtime.\n" +
                    $"Muévelo a {RosterFolder}/.", MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "PRIMER PASO: no hay ningún NpcRosterSO en el proyecto. Pulsa «Crear roster» arriba a la derecha.\n" +
                "Hasta entonces el botón 1 de cada NPC está desactivado.", MessageType.Warning);
        }

        EditorGUILayout.Space(2);

        if (!TargetSceneOpen)
        {
            EditorGUILayout.HelpBox(
                $"'{TargetSceneName}' no está abierta. Esta herramienta solo trabaja sobre esa escena — " +
                "ábrela y pulsa Reescanear.", MessageType.Warning);

            if (GUILayout.Button("Reescanear")) _pendingAction = Scan;

            if (_pendingAction != null) { var a = _pendingAction; _pendingAction = null; a(); Repaint(); }
            return;
        }

        EditorGUILayout.LabelField($"NPCs en {TargetSceneName}: {_rows.Count}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Orden por NPC:  1 crear marcador+entrada  →  2 buscar referencias  →  probar en Play  →  3 quitar de la escena.",
            EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var row in _rows) DrawRow(row);
        EditorGUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(_lastReport))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Último informe de referencias (también en consola):", EditorStyles.miniBoldLabel);
            EditorGUILayout.TextArea(_lastReport, GUILayout.Height(110));
        }

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!TargetSceneOpen))
            {
                if (GUILayout.Button($"Guardar {TargetSceneName}"))
                {
                    EditorSceneManager.SaveScene(GetTargetScene());
                    Debug.Log($"[SpawnNPCs] '{TargetSceneName}' guardada.");
                }
            }
            if (GUILayout.Button("Seleccionar roster") && _roster != null)
                Selection.activeObject = _roster;
        }

        // Dibujado terminado: ya es seguro tocar _rows.
        if (_pendingAction != null)
        {
            var action = _pendingAction;
            _pendingAction = null;
            action();
            Repaint();
        }
    }

    private void DrawRow(Row row)
    {
        if (row.npc == null) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(row.npc.gameObject.name, EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField($"escena: {row.sceneName}", GUILayout.Width(180));
                if (GUILayout.Button("Seleccionar", GUILayout.Width(90)))
                {
                    Selection.activeGameObject = row.npc.gameObject;
                    EditorGUIUtility.PingObject(row.npc.gameObject);
                }
            }

            EditorGUILayout.LabelField(
                $"gameObjectName: '{row.npc.gameObject.name}'   ·   persistenceId: " +
                (string.IsNullOrEmpty(row.npc.PersistenceId) ? "(vacío)" : $"'{row.npc.PersistenceId}'") +
                $"   ·   persistLastPosition: {(row.npc.persistLastPosition ? "sí" : "NO")}" +
                $"   ·   activo en escena: {(row.npc.gameObject.activeSelf ? "sí" : "NO")}",
                EditorStyles.miniLabel);

            EditorGUILayout.LabelField(
                "prefab: " + (row.prefabPath ?? "⚠️ ninguno (no se puede migrar)"),
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(row.marker != null ? "✅ marcador" : "— sin marcador", GUILayout.Width(120));
                EditorGUILayout.LabelField(row.entry != null ? "✅ en roster" : "— sin entrada", GUILayout.Width(120));
                EditorGUILayout.LabelField(
                    row.sceneReferences < 0 ? "— referencias sin comprobar"
                    : row.sceneReferences == 0 ? "✅ 0 referencias"
                    : $"⚠️ {row.sceneReferences} referencia(s)");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                string paso1 =
                    _roster == null            ? "1. Crear marcador + entrada  ←  falta pulsar «Crear roster»"
                    : row.prefabSource == null ? "1. Crear marcador + entrada  ←  este NPC no viene de un prefab"
                    : row.entry == null        ? "1. Crear marcador + entrada"
                                               : "1. Actualizar marcador + entrada";

                using (new EditorGUI.DisabledScope(_roster == null || row.prefabSource == null))
                {
                    if (GUILayout.Button(paso1)) _pendingAction = () => CreateMarkerAndEntry(row);
                }

                if (GUILayout.Button("2. Buscar referencias"))
                    _pendingAction = () => FindSceneReferences(row);

                using (new EditorGUI.DisabledScope(row.marker == null || row.entry == null))
                {
                    if (GUILayout.Button("3. Quitar de la escena"))
                        _pendingAction = () => RemoveFromScene(row);
                }
            }
        }
    }
}
