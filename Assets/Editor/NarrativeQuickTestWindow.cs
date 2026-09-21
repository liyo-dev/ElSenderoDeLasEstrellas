using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Quick Test System — Configura el juego para arrancar desde un nodo específico del grafo narrativo.
/// Genera automáticamente un preset temporal con el blackboard configurado para que el NarrativeRunner
/// continúe desde el nodo seleccionado, y lanza Play Mode.
///
/// FAST-FORWARD (2026-09-15): el tool SIMULA el camino desde el inicio del grafo hasta el nodo
/// objetivo (sin ejecutar Enter() de ningún nodo real, solo leyendo sus campos serializados) y aplica
/// al preset temporal los efectos de los nodos que cambian estado: StartQuestNode, CompleteQuestStepsNode,
/// GiveInventoryItemNode, UnlockAbilitiesNode y SetFlagNode. Los nodos puramente presentacionales
/// (Wait*, diálogos, cinemáticas, textos, etc.) se ignoran. Si el camino pasa por una bifurcación real
/// (BranchFlagNode, BranchQuestStateNode, DialogueChoiceNode, RequireInventoryItemNode, o cualquier nodo
/// con varias salidas sin nombrar que no sea un ForkNode explícito) la simulación se PARA ahí y lo avisa
/// en vez de adivinar qué rama seguir — hay que resolverlo a mano (flag, quest, etc.) antes de lanzar.
/// Se puede desactivar con el checkbox correspondiente para volver al comportamiento anterior (progreso
/// vacío/neutro). Además, independientemente del Fast-Forward, el tool siempre:
///   1. Coloca el grafo en el nodo elegido (vía blackboard "__currentNodeGuid").
///   2. Coloca físicamente al jugador en el Spawn Anchor indicado (independiente del nodo del
///      grafo — si no coinciden, el grafo estará en el punto correcto pero aparecerás en otro sitio).
///   3. Aplica el estado de habilidades/hechizos que marques a mano ADEMÁS de lo que aporte el
///      Fast-Forward (para forzar algo que la historia todavía no te habría dado).
///
/// Flujo:
/// 1. Seleccionar grafo + nodo objetivo + habilidades/hechizos equipados
/// 2. (Opcional) "Vista previa Fast-Forward" para revisar bifurcaciones sin entrar en Play Mode
/// 3. Click "Play desde aquí"
/// 4. Se crea/actualiza un PlayerPresetSO temporal (progreso previo + ajustes manuales)
/// 5. Se configura GameBootProfile con usePresetInsteadOfSave = true
/// 6. Se entra en Play Mode
/// 7. Al salir de Play Mode, se restaura el bootPreset original
/// </summary>
public class NarrativeQuickTestWindow : EditorWindow
{
    // Selection
    private NarrativeGraph _targetGraph;
    private string _targetNodeGuid;
    private string _graphLabel = "Cap1";

    // Graph label options — deben coincidir con los labels reales del NarrativeGraphHub en Start.unity.
    // Actualizado tras dividir "Historia Principal" en capítulos (ChapterSplitWindow).
    // ⚠️ Estos labels deben coincidir EXACTAMENTE con los "label" reales de los GraphSlot del
    // NarrativeGraphHub en Assets/Scenes/Systems/Start.unity (búscalos ahí, no los inventes aquí).
    // Hasta el 15 sept 2026 solo hay 2 grafos registrados de verdad en el Hub: "Cap1" y
    // "Misiones Secundarias" (Cap 2-6 todavía no existen como grafos nuevos, ver
    // claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md § 5). Antes esta lista tenía
    // "Historia Principal - Cap 1".."Cap 6", que NO EXISTEN en el Hub — eso hacía que
    // RestoreBlackboards() nunca encontrara el runner (label no coincide), así que __currentNodeGuid
    // nunca se restauraba: el grafo arrancaba desde su StartNode real (prólogo, tutorial...) en vez
    // de desde el nodo elegido, y se quedaba esperando el primer WaitCustomEventNode del camino real
    // (p.ej. "Recoge la caja") sin que Quick Test avisara del problema.
    private static readonly string[] KnownGraphLabels = {
        "Cap1", "Misiones Secundarias"
    };

    // Punto de aparición física (independiente del nodo del grafo)
    private string _spawnAnchorId = "Bedroom";
    private string[] _detectedAnchors = new string[0];

    // Estado del jugador para la prueba (sustituye a "Base Preset")
    private int _level = 1;
    private float _maxHP = 100;
    private float _maxMP = 50;
    private bool _abilitySwim;
    private bool _abilityJump;
    private bool _abilityClimb;
    private bool _abilityMagic;
    private bool _abilityFly;
    private bool _abilitySprint;
    private bool _abilityShield;
    private SpellId _leftSpellId = SpellId.None;
    private SpellId _rightSpellId = SpellId.None;
    private SpellId _specialSpellId = SpellId.None;

    // Fast-Forward (progreso previo simulado desde el inicio del grafo)
    private bool _fastForward = true;
    private bool _fastForwardPreviewRan;
    private bool _fastForwardReachedTarget;
    private readonly List<string> _fastForwardWarnings = new List<string>();
    private Dictionary<string, QuestData> _questCatalogCache;

    // State
    private int _selectedNodeIndex;
    private Vector2 _scrollPos;
    private bool _autoRestore = true;
    private string _status = "";

    // Saved original state for restore
    private static PlayerPresetSO _originalBootPreset;
    private static bool _originalUsePreset;
    private static bool _needsRestore;

    private const string QuickTestPresetPath = "Assets/Editor/QuickTestPreset_Temp.asset";

    [MenuItem("El Sendero/Narrativa/Quick Test from Node")]
    public static void ShowWindow()
    {
        var w = GetWindow<NarrativeQuickTestWindow>();
        w.titleContent = new GUIContent("Quick Test");
        w.minSize = new Vector2(400, 480);
        w.Show();
    }

    /// <summary>
    /// Opens the Quick Test window pre-configured with a specific graph and node.
    /// Called from the narrative graph editor context menu.
    /// </summary>
    public static void OpenWithNode(NarrativeGraph graph, NarrativeNode node, string graphLabel = null)
    {
        var w = GetWindow<NarrativeQuickTestWindow>();
        w.titleContent = new GUIContent("Quick Test");
        w._targetGraph = graph;
        w._targetNodeGuid = node?.guid;
        if (!string.IsNullOrEmpty(graphLabel))
            w._graphLabel = graphLabel;
        else
            w.TryAutoDetectLabel(graph);
        w.Show();
        w.Repaint();
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && _needsRestore)
        {
            RestoreOriginalBootPreset();
        }
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField("Quick Test — Play desde un nodo", titleStyle);
        EditorGUILayout.Space(8);

        // Graph selection
        _targetGraph = (NarrativeGraph)EditorGUILayout.ObjectField(
            "Grafo Narrativo", _targetGraph, typeof(NarrativeGraph), false);

        if (_targetGraph == null)
        {
            EditorGUILayout.HelpBox("Selecciona un NarrativeGraph para comenzar.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        // Graph label
        // FIX (15 sept 2026): antes, si _graphLabel no estaba en KnownGraphLabels (p.ej. porque
        // TryAutoDetectLabel() lo había puesto correctamente a "Cap1" leyendo el Hub real, o
        // porque el Hub tiene un label nuevo que esta lista aún no conoce), labelIdx daba -1 y
        // el código lo pisaba silenciosamente con KnownGraphLabels[0] en el siguiente repintado —
        // exactamente el bug que hacía que Quick Test lanzara con un label que no existe en el
        // Hub. Ahora, si _graphLabel no está en la lista conocida, se añade como opción extra en
        // vez de descartarlo.
        var popupOptions = KnownGraphLabels;
        int labelIdx = System.Array.IndexOf(popupOptions, _graphLabel);
        if (labelIdx < 0 && !string.IsNullOrEmpty(_graphLabel))
        {
            popupOptions = KnownGraphLabels.Append(_graphLabel).ToArray();
            labelIdx = popupOptions.Length - 1;
        }
        else if (labelIdx < 0)
        {
            labelIdx = 0;
        }
        labelIdx = EditorGUILayout.Popup("Graph Label", labelIdx, popupOptions);
        _graphLabel = popupOptions[labelIdx];
        if (!KnownGraphLabels.Contains(_graphLabel))
        {
            EditorGUILayout.HelpBox(
                $"'{_graphLabel}' no está en la lista de labels conocidos del Hub (revisa " +
                "Start.unity o actualiza KnownGraphLabels si es un grafo nuevo legítimo).",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4);

        // Node selection dropdown
        var nodes = _targetGraph.nodes?.Where(n => n != null).ToList() ?? new List<NarrativeNode>();
        if (nodes.Count == 0)
        {
            EditorGUILayout.HelpBox("El grafo no tiene nodos.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        var nodeNames = nodes.Select(n =>
        {
            string label = n.GetType().Name;
            if (!string.IsNullOrWhiteSpace(n.displayTitle))
                label += $" — \"{n.displayTitle}\"";
            return label;
        }).ToArray();

        // Find current selection
        if (!string.IsNullOrEmpty(_targetNodeGuid))
        {
            int idx = nodes.FindIndex(n => n.guid == _targetNodeGuid);
            if (idx >= 0) _selectedNodeIndex = idx;
        }

        _selectedNodeIndex = Mathf.Clamp(_selectedNodeIndex, 0, nodes.Count - 1);
        _selectedNodeIndex = EditorGUILayout.Popup("Nodo objetivo", _selectedNodeIndex, nodeNames);
        _targetNodeGuid = nodes[_selectedNodeIndex].guid;

        // Show node info
        var selectedNode = nodes[_selectedNodeIndex];
        EditorGUILayout.BeginVertical("helpBox");
        EditorGUILayout.LabelField($"Tipo: {selectedNode.GetType().Name}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"GUID: {selectedNode.guid}", EditorStyles.miniLabel);
        if (!string.IsNullOrEmpty(selectedNode.displayTitle))
            EditorGUILayout.LabelField($"Etiqueta: {selectedNode.displayTitle}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // Punto de aparición
        EditorGUILayout.LabelField("Punto de aparición (Spawn Anchor)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Independiente del nodo del grafo: decide dónde aparece Will físicamente. Si no coincide " +
            "con la zona del nodo, el grafo estará en el punto correcto pero aparecerás en otro sitio.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal();
        _spawnAnchorId = EditorGUILayout.TextField("Spawn Anchor ID", _spawnAnchorId);
        if (GUILayout.Button("Detectar en escena abierta", GUILayout.Width(170)))
        {
            RefreshDetectedAnchors();
        }
        EditorGUILayout.EndHorizontal();

        if (_detectedAnchors.Length > 0)
        {
            int detectedIdx = System.Array.IndexOf(_detectedAnchors, _spawnAnchorId);
            int newIdx = EditorGUILayout.Popup("Anchors detectados", detectedIdx < 0 ? 0 : detectedIdx, _detectedAnchors);
            if (newIdx >= 0 && newIdx < _detectedAnchors.Length)
                _spawnAnchorId = _detectedAnchors[newIdx];
        }

        EditorGUILayout.Space(8);

        // Estado del jugador (sustituye a "Base Preset")
        EditorGUILayout.LabelField("Estado del jugador (manual, además del Fast-Forward)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Esto se aplica SIEMPRE, tanto si el Fast-Forward está activo como si no. Úsalo para forzar " +
            "algo extra que la historia todavía no te habría dado en este punto (o, si desactivas el " +
            "Fast-Forward, para marcar a mano todo lo que necesites).",
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        _level = EditorGUILayout.IntField("Nivel", _level, GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
        _maxHP = EditorGUILayout.FloatField("Vida máxima", _maxHP);
        _maxMP = EditorGUILayout.FloatField("Maná máximo", _maxMP);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Habilidades de movimiento / combate", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _abilityJump = EditorGUILayout.ToggleLeft("Saltar", _abilityJump, GUILayout.Width(120));
        _abilitySwim = EditorGUILayout.ToggleLeft("Nadar", _abilitySwim, GUILayout.Width(120));
        _abilityClimb = EditorGUILayout.ToggleLeft("Trepar", _abilityClimb, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        _abilityFly = EditorGUILayout.ToggleLeft("Volar", _abilityFly, GUILayout.Width(120));
        _abilitySprint = EditorGUILayout.ToggleLeft("Sprint", _abilitySprint, GUILayout.Width(120));
        _abilityShield = EditorGUILayout.ToggleLeft("Escudo", _abilityShield, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();
        _abilityMagic = EditorGUILayout.ToggleLeft("Magia (casts)", _abilityMagic, GUILayout.Width(150));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Hechizos equipados", EditorStyles.boldLabel);
        _leftSpellId = (SpellId)EditorGUILayout.EnumPopup("Slot Izquierdo", _leftSpellId);
        _rightSpellId = (SpellId)EditorGUILayout.EnumPopup("Slot Derecho", _rightSpellId);
        _specialSpellId = (SpellId)EditorGUILayout.EnumPopup("Slot Especial", _specialSpellId);

        EditorGUILayout.Space(8);

        // Fast-Forward
        EditorGUILayout.LabelField("Fast-Forward (progreso previo)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Simula el camino desde el inicio del grafo hasta el nodo objetivo y aplica automáticamente " +
            "quests iniciadas/completadas, objetos entregados, habilidades/hechizos desbloqueados y flags. " +
            "Si el camino pasa por una bifurcación (flag, estado de quest, diálogo con opciones, objeto " +
            "requerido...) la simulación se para ahí y te lo avisa en vez de adivinar qué rama seguir.",
            EditorStyles.wordWrappedMiniLabel);
        _fastForward = EditorGUILayout.ToggleLeft("Autocompletar progreso previo (Fast-Forward)", _fastForward);

        if (GUILayout.Button("Vista previa Fast-Forward (sin lanzar Play)"))
        {
            PreviewFastForward();
        }

        if (_fastForwardWarnings.Count > 0)
        {
            var msg = (_fastForwardReachedTarget
                ? "Se alcanzó el nodo objetivo, pero se encontraron bifurcaciones en el camino explorado " +
                  "(puede que alguna no afecte a este nodo en concreto — revisa si están antes o después " +
                  "del punto que quieres probar):\n\n"
                : "No se pudo llegar automáticamente al nodo objetivo: el camino pasa por una bifurcación " +
                  "que no se puede resolver sola. Resuélvela a mano (pon el flag, avanza la quest, etc.) " +
                  "antes de lanzar, o el progreso aplicado será parcial:\n\n") +
                string.Join("\n\n", _fastForwardWarnings);
            EditorGUILayout.HelpBox(msg, _fastForwardReachedTarget ? MessageType.Warning : MessageType.Error);
        }
        else if (_fastForwardPreviewRan)
        {
            EditorGUILayout.HelpBox(_fastForwardReachedTarget
                ? "Camino simulado sin bifurcaciones. El progreso previo se aplicará automáticamente al lanzar."
                : "El nodo objetivo no parece alcanzable desde el inicio del grafo (revisa las conexiones).",
                _fastForwardReachedTarget ? MessageType.Info : MessageType.Warning);
        }

        EditorGUILayout.Space(4);
        _autoRestore = EditorGUILayout.Toggle("Restaurar bootPreset al salir", _autoRestore);

        EditorGUILayout.Space(12);

        // Status
        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        // Launch button
        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
        if (GUILayout.Button("Play desde aquí", GUILayout.Height(36)))
        {
            LaunchQuickTest();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);

        // Manual restore button
        if (_needsRestore)
        {
            GUI.backgroundColor = new Color(0.85f, 0.6f, 0.2f);
            if (GUILayout.Button("Restaurar bootPreset original"))
            {
                RestoreOriginalBootPreset();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
    }

    private void LaunchQuickTest()
    {
        if (EditorApplication.isPlaying)
        {
            _status = "Ya estás en Play Mode. Sal primero.";
            return;
        }

        // 1. Find GameBootProfile
        var profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>("Assets/_BootProfile/GameBootProfile.asset");
        if (profile == null)
        {
            var guids = AssetDatabase.FindAssets("t:GameBootProfile");
            if (guids.Length > 0)
                profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (profile == null)
        {
            _status = "GameBootProfile no encontrado.";
            return;
        }

        // 2. Save original state for restore
        _originalBootPreset = profile.bootPreset;
        _originalUsePreset = profile.usePresetInsteadOfSave;
        _needsRestore = _autoRestore;

        // 3. Create/update temp preset
        var tempPreset = AssetDatabase.LoadAssetAtPath<PlayerPresetSO>(QuickTestPresetPath);
        if (tempPreset == null)
        {
            tempPreset = ScriptableObject.CreateInstance<PlayerPresetSO>();
            AssetDatabase.CreateAsset(tempPreset, QuickTestPresetPath);
        }

        // 4. Rellenar el preset temporal solo con lo que se configura en esta ventana.
        //    Deliberadamente NO se copia ningún "Base Preset": el progreso de historia previo
        //    (flags, misiones, items, bosses, etc.) se deja vacío/neutro. La apariencia/vestuario
        //    se hereda del defaultPlayerPreset del profile solo por motivos cosméticos (para no
        //    aparecer con el aspecto por defecto/sin ropa), nunca su progreso narrativo.
        ApplyPlayerStateToPreset(tempPreset, profile.defaultPlayerPreset);
        tempPreset.name = "QuickTest_Temp";

        // 4b. Fast-Forward: simula el camino desde el inicio del grafo y aplica el progreso previo
        //     (quests, items, habilidades/hechizos, flags) ENCIMA del estado manual de arriba.
        if (_fastForward)
        {
            RunFastForward(tempPreset);
            _fastForwardPreviewRan = true;

            if (_fastForwardWarnings.Count > 0)
            {
                Debug.LogWarning($"[QuickTest] Fast-Forward encontró {_fastForwardWarnings.Count} bifurcación(es):\n" +
                    string.Join("\n", _fastForwardWarnings));
            }
            if (!_fastForwardReachedTarget)
            {
                Debug.LogWarning("[QuickTest] Fast-Forward no llegó al nodo objetivo automáticamente " +
                    "(revisa las bifurcaciones). Se lanzará igualmente con el progreso parcial acumulado.");
            }
        }

        // 5. Set up narrative blackboard to start from the target node
        var bbSnapshot = new PlayerSaveData.NarrativeBlackboardSnapshot
        {
            graphLabel = _graphLabel,
            blackboardData = new List<SimpleBlackboard.Entry>
            {
                new SimpleBlackboard.Entry
                {
                    key = "__currentNodeGuid",
                    type = "string",
                    value = _targetNodeGuid
                }
            }
        };

        tempPreset.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot> { bbSnapshot };

        EditorUtility.SetDirty(tempPreset);

        // 6. Configure GameBootProfile
        profile.usePresetInsteadOfSave = true;
        profile.bootPreset = tempPreset;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var selectedNode = _targetGraph.FindNode(_targetNodeGuid);
        string nodeDesc = selectedNode != null
            ? $"{selectedNode.GetType().Name} \"{selectedNode.displayTitle}\""
            : _targetNodeGuid;

        _status = $"Lanzando Play desde {nodeDesc} en {_graphLabel} (anchor: {tempPreset.spawnAnchorId})...";
        Debug.Log($"[QuickTest] Configurado: grafo='{_graphLabel}', nodo='{nodeDesc}', anchor='{tempPreset.spawnAnchorId}', " +
            $"habilidades=(swim:{_abilitySwim}, jump:{_abilityJump}, climb:{_abilityClimb}, magic:{_abilityMagic}, fly:{_abilityFly}, sprint:{_abilitySprint}, shield:{_abilityShield}), " +
            $"hechizos=(L:{_leftSpellId}, R:{_rightSpellId}, Esp:{_specialSpellId})");

        // 7. Enter Play Mode
        EditorApplication.isPlaying = true;
    }

    /// <summary>
    /// Aplica a <paramref name="dst"/> el nivel/vida/maná/habilidades/hechizos configurados en la
    /// ventana. El progreso de historia (flags, misiones, inventario de quest, bosses, etc.) se deja
    /// vacío a propósito — este tool no simula partidas avanzadas, solo coloca el grafo en un nodo y
    /// te da el "cuerpo" (habilidades/hechizos) necesario para probarlo.
    /// <paramref name="cosmeticBase"/> (normalmente el defaultPlayerPreset del profile) se usa
    /// únicamente para apariencia/vestuario, nunca para progreso.
    /// </summary>
    private void ApplyPlayerStateToPreset(PlayerPresetSO dst, PlayerPresetSO cosmeticBase)
    {
        dst.spawnAnchorId = string.IsNullOrEmpty(_spawnAnchorId) ? "Bedroom" : _spawnAnchorId;

        dst.level = _level;
        dst.maxHP = _maxHP;
        dst.currentHP = _maxHP;
        dst.maxMP = _maxMP;
        dst.currentMP = _maxMP;

        dst.abilities = new PlayerAbilities
        {
            swim = _abilitySwim,
            jump = _abilityJump,
            climb = _abilityClimb,
            magic = _abilityMagic,
            fly = _abilityFly,
            sprint = _abilitySprint,
            shield = _abilityShield
        };
        dst.unlockedAbilities = new List<AbilityId>();

        dst.leftSpellId = _leftSpellId;
        dst.rightSpellId = _rightSpellId;
        dst.specialSpellId = _specialSpellId;
        dst.unlockedSpells = new[] { _leftSpellId, _rightSpellId, _specialSpellId }
            .Where(id => id != SpellId.None)
            .Distinct()
            .ToList();

        // Cosmético únicamente — apariencia/vestuario del preset por defecto, para no aparecer
        // con el aspecto en blanco. No incluye progreso.
        dst.appearance = new List<AppearanceEntry>(cosmeticBase?.appearance ?? new List<AppearanceEntry>());
        dst.unlockedWardrobeIds = new List<string>(cosmeticBase?.unlockedWardrobeIds ?? new List<string>());

        // Progreso de historia: siempre vacío/neutro en Quick Test.
        dst.flags = new List<string>();
        dst.inventoryItems = new List<InventoryItemSave>();
        dst.defeatedBossIds = new List<string>();
        dst.consumedInteractableIds = new List<string>();
        dst.completedInteractiveNarratives = new List<string>();
        dst.seenLorePopupIds = new List<string>();
        dst.partyMemberIds = new List<string>();
        dst.activeCharacterSlot = 1;
        dst.unlockedTeleportPoints = new List<string>();
        dst.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
    }

    private static void RestoreOriginalBootPreset()
    {
        var profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>("Assets/_BootProfile/GameBootProfile.asset");
        if (profile == null)
        {
            var guids = AssetDatabase.FindAssets("t:GameBootProfile");
            if (guids.Length > 0)
                profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (profile != null)
        {
            profile.bootPreset = _originalBootPreset;
            profile.usePresetInsteadOfSave = _originalUsePreset;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[QuickTest] bootPreset restaurado al original.");
        }

        _needsRestore = false;
    }

    /// <summary>
    /// Busca componentes SpawnAnchor en las escenas actualmente abiertas en el editor (no requiere
    /// Play Mode: son GameObjects normales de la escena) y ofrece sus anchorId como sugerencias.
    /// Si la escena del nodo objetivo no está abierta, no aparecerá aquí — hay que abrirla primero.
    /// </summary>
    private void RefreshDetectedAnchors()
    {
        var anchors = Object.FindObjectsByType<SpawnAnchor>(FindObjectsSortMode.None);
        _detectedAnchors = anchors
            .Select(a => a.anchorId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        if (_detectedAnchors.Length == 0)
        {
            _status = "No se han encontrado SpawnAnchor en las escenas abiertas. Abre la escena del nodo objetivo primero.";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fast-Forward: simula el camino Start → nodo objetivo y aplica el progreso
    // previo (quests/items/habilidades/flags) a un PlayerPresetSO. NUNCA invoca
    // Enter() de un nodo real (dispararía diálogos/señales de verdad fuera de
    // Play Mode) — solo lee los campos serializados de cada nodo.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta el Fast-Forward sobre un preset "de mentira" solo para revisar si hay bifurcaciones,
    /// sin tocar el preset real ni entrar en Play Mode.
    /// </summary>
    private void PreviewFastForward()
    {
        if (_targetGraph == null || string.IsNullOrEmpty(_targetNodeGuid))
        {
            _status = "Selecciona grafo y nodo objetivo antes de previsualizar.";
            return;
        }

        var scratch = ScriptableObject.CreateInstance<PlayerPresetSO>();
        scratch.flags = new List<string>();
        scratch.inventoryItems = new List<InventoryItemSave>();
        scratch.unlockedSpells = new List<SpellId>();
        scratch.abilities = new PlayerAbilities();
        try
        {
            RunFastForward(scratch);
            _fastForwardPreviewRan = true;
        }
        finally
        {
            DestroyImmediate(scratch);
        }
        Repaint();
    }

    /// <summary>
    /// Recorre el grafo en anchura desde <see cref="NarrativeGraph.startNodeGuid"/> aplicando a
    /// <paramref name="dst"/> el efecto de los nodos que cambian estado (StartQuestNode,
    /// CompleteQuestStepsNode, GiveInventoryItemNode, UnlockAbilitiesNode, SetFlagNode). El resto de
    /// nodos son presentacionales y se ignoran a propósito.
    ///
    /// Qué cuenta como bifurcación y qué como fork — MISMA REGLA QUE EL RUNTIME (corregido 16 sept 2026,
    /// INC-219). El criterio lo manda NarrativeRunner.RunSubGraph(), que trata cualquier nodo con varias
    /// salidas SIN NOMBRE como un fork implícito y lanza todas sus ramas en paralelo:
    ///   - ForkNode y cualquier nodo con varias salidas sin nombre → fork: se expanden TODAS las salidas.
    ///   - Nodos con puertos con nombre (BranchFlagNode, BranchQuestStateNode, DialogueChoiceNode,
    ///     PlayCinematicNode...) → bifurcación real: no se sigue explorando por ahí, se registra un aviso.
    ///   - RequireInventoryItemNode → bifurcación real pese a no tener puertos con nombre: es el único
    ///     nodo del proyecto que redirige el flujo por índice (ForceJumpToOutput) desde Enter().
    /// Nunca se adivina una rama de una bifurcación real.
    ///
    /// Antes de este arreglo, CUALQUIER nodo con varias salidas sin nombre se trataba como bifurcación
    /// irresoluble. Como un StartQuestNode en mitad del camino principal de Cap1 tiene una segunda salida
    /// hacia una rama paralela (el saludo de Oliver), el Fast-Forward abandonaba el recorrido ahí y el
    /// preset salía con una sola flag (QUEST_ACTIVE de la primera misión): el grafo arrancaba en el nodo
    /// correcto pero la UI de misiones mostraba una misión atrasada, y los CompleteQuestStepsNode
    /// posteriores no hacían nada porque esas quests nunca se habían iniciado en el QuestManager.
    /// </summary>
    private void RunFastForward(PlayerPresetSO dst)
    {
        _fastForwardWarnings.Clear();
        _fastForwardReachedTarget = false;
        _questCatalogCache = null;

        if (_targetGraph == null || string.IsNullOrEmpty(_targetGraph.startNodeGuid) || string.IsNullOrEmpty(_targetNodeGuid))
            return;

        var byGuid = _targetGraph.nodes
            .Where(n => n != null && !string.IsNullOrEmpty(n.guid))
            .GroupBy(n => n.guid)
            .ToDictionary(g => g.Key, g => g.First());

        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(_targetGraph.startNodeGuid);

        while (queue.Count > 0)
        {
            var guid = queue.Dequeue();
            if (!visited.Add(guid)) continue;
            if (!byGuid.TryGetValue(guid, out var node) || node == null) continue;

            ApplyNodeEffect(node, dst);

            if (guid == _targetNodeGuid)
            {
                _fastForwardReachedTarget = true;
                break;
            }

            // Nodo terminal: no hay nada que seguir ni que decidir. Sin este guard, un nodo con puertos
            // con nombre y ninguna salida conectada (p.ej. el PlayCinematicNode final de una rama
            // paralela) se contaba como "bifurcación sin resolver" y ensuciaba el aviso al usuario.
            bool hasAnyOutput = node.outputs != null && node.outputs.Any(o => !string.IsNullOrEmpty(o));
            if (!hasAnyOutput) continue;

            // Bifurcación real = puertos con nombre, o RequireInventoryItemNode (única excepción sin
            // nombrar que redirige por índice). Todo lo demás con varias salidas es un fork implícito,
            // igual que en NarrativeRunner.RunSubGraph() — ver comentario de RunFastForward.
            bool isDecision = node.HasNamedOutputs || node is RequireInventoryItemNode;

            if (isDecision)
            {
                _fastForwardWarnings.Add(DescribeBifurcation(node));
                continue; // no se sigue explorando por este nodo; el resto de la cola continúa
            }

            foreach (var outGuid in node.outputs)
            {
                if (!string.IsNullOrEmpty(outGuid) && !visited.Contains(outGuid))
                    queue.Enqueue(outGuid);
            }
        }
    }

    private static string DescribeBifurcation(NarrativeNode node)
    {
        switch (node)
        {
            case BranchFlagNode bf:
                return $"Bifurcación \"Según flag\" (\"{bf.displayTitle}\", guid {bf.guid}): comprueba el flag " +
                    $"'{bf.flagKey}'{(bf.invert ? " (invertido)" : "")}. Salidas: Sí / No.";
            case BranchQuestStateNode bq:
                return $"Bifurcación \"Según estado de quest\" (\"{bq.displayTitle}\", guid {bq.guid}): " +
                    $"quest '{bq.questId}'. Salidas: No iniciada / Activa / Pasos listos / Completada.";
            case DialogueChoiceNode dc:
                return $"Pregunta con respuestas (\"{dc.displayTitle}\", guid {dc.guid}): " +
                    $"\"{(string.IsNullOrEmpty(dc.promptText) ? dc.promptTextId : dc.promptText)}\" → " +
                    $"opciones '{dc.optionAText}' / '{dc.optionBText}'.";
            case RequireInventoryItemNode ri:
                return $"Requiere objeto (\"{ri.displayTitle}\", guid {ri.guid}): necesita " +
                    $"'{(ri.item != null ? ri.item.itemId : "<sin item>")}' x{ri.requiredAmount}.";
            default:
                return $"{node.GetType().Name} (\"{node.displayTitle}\", guid {node.guid}) tiene " +
                    $"{node.outputs?.Count ?? 0} salidas sin nombrar — no se puede decidir automáticamente qué rama seguir.";
        }
    }

    private void ApplyNodeEffect(NarrativeNode node, PlayerPresetSO dst)
    {
        switch (node)
        {
            case StartQuestNode sq:
                if (!string.IsNullOrEmpty(sq.questId) && !HasFlag(dst, $"QUEST_COMPLETED:{sq.questId}"))
                    AddFlagIfMissing(dst, $"QUEST_ACTIVE:{sq.questId}");
                break;

            case CompleteQuestStepsNode cs:
                ApplyCompleteQuestSteps(cs, dst);
                break;

            case GiveInventoryItemNode gi:
                ApplyGiveItem(gi, dst);
                break;

            case UnlockAbilitiesNode ua:
                ApplyUnlockAbilities(ua, dst);
                break;

            case SetFlagNode sf:
                if (!string.IsNullOrWhiteSpace(sf.flagKey))
                {
                    if (sf.value) AddFlagIfMissing(dst, sf.flagKey);
                    else RemoveFlagIfPresent(dst, sf.flagKey);
                }
                break;

            // Resto de tipos (Wait*, diálogos, cinemáticas, cámara, audio, etc.): puramente
            // presentacionales para el propósito del Fast-Forward — se ignoran a propósito.
        }
    }

    private void ApplyCompleteQuestSteps(CompleteQuestStepsNode node, PlayerPresetSO dst)
    {
        if (string.IsNullOrWhiteSpace(node.questId)) return;

        bool alreadyCompleted = HasFlag(dst, $"QUEST_COMPLETED:{node.questId}");
        if (!alreadyCompleted)
            AddFlagIfMissing(dst, $"QUEST_ACTIVE:{node.questId}");

        if (node.stepConditionIds != null && node.stepConditionIds.Count > 0)
        {
            var qd = FindQuestData(node.questId);
            if (qd == null || qd.steps == null)
            {
                _fastForwardWarnings.Add($"CompleteQuestStepsNode (\"{node.displayTitle}\", guid {node.guid}): " +
                    $"no se encontró el QuestData de '{node.questId}' en el proyecto; no se pudieron aplicar " +
                    "los pasos por Condition ID.");
            }
            else
            {
                foreach (var conditionId in node.stepConditionIds)
                {
                    if (string.IsNullOrWhiteSpace(conditionId)) continue;
                    int idx = System.Array.FindIndex(qd.steps, s => s.conditionId == conditionId);
                    if (idx < 0)
                    {
                        _fastForwardWarnings.Add($"CompleteQuestStepsNode (\"{node.displayTitle}\", guid {node.guid}): " +
                            $"el Condition ID '{conditionId}' no existe en los steps de '{node.questId}'.");
                        continue;
                    }
                    AddFlagIfMissing(dst, $"QUEST_STEP_DONE:{node.questId}:{idx}");
                }
            }
        }
        else if (node.steps != null && node.steps.Count > 0)
        {
            foreach (var idx in node.steps)
                if (idx >= 0) AddFlagIfMissing(dst, $"QUEST_STEP_DONE:{node.questId}:{idx}");
        }

        if (node.completeQuest)
            AddFlagIfMissing(dst, $"QUEST_COMPLETED:{node.questId}");
    }

    private static void ApplyGiveItem(GiveInventoryItemNode node, PlayerPresetSO dst)
    {
        if (node.item == null || node.amount <= 0) return;

        dst.inventoryItems ??= new List<InventoryItemSave>();
        int idx = dst.inventoryItems.FindIndex(e => e.itemId == node.item.itemId);
        if (idx >= 0)
        {
            var entry = dst.inventoryItems[idx];
            entry.count += node.amount;
            dst.inventoryItems[idx] = entry;
        }
        else
        {
            dst.inventoryItems.Add(new InventoryItemSave { itemId = node.item.itemId, count = node.amount });
        }
    }

    private static void ApplyUnlockAbilities(UnlockAbilitiesNode node, PlayerPresetSO dst)
    {
        dst.abilities ??= new PlayerAbilities();

        if (node.abilityKeysToUnlock != null)
        {
            foreach (var key in node.abilityKeysToUnlock)
            {
                switch (key)
                {
                    case AbilityKey.Swim: dst.abilities.swim = true; break;
                    case AbilityKey.Jump: dst.abilities.jump = true; break;
                    case AbilityKey.Climb: dst.abilities.climb = true; break;
                    case AbilityKey.Magic: dst.abilities.magic = true; break;
                    case AbilityKey.Fly: dst.abilities.fly = true; break;
                    case AbilityKey.Sprint: dst.abilities.sprint = true; break;
                    case AbilityKey.Shield: dst.abilities.shield = true; break;
                }
            }
        }

        if (node.spellsToUnlock != null)
        {
            dst.unlockedSpells ??= new List<SpellId>();
            foreach (var spell in node.spellsToUnlock)
            {
                if (spell == SpellId.None) continue;
                if (!dst.unlockedSpells.Contains(spell)) dst.unlockedSpells.Add(spell);

                if (node.assignSpellsToEmptySlot)
                {
                    bool alreadyEquipped = dst.leftSpellId == spell || dst.rightSpellId == spell || dst.specialSpellId == spell;
                    if (!alreadyEquipped)
                    {
                        if (dst.leftSpellId == SpellId.None) dst.leftSpellId = spell;
                        else if (dst.rightSpellId == SpellId.None) dst.rightSpellId = spell;
                        else if (dst.specialSpellId == SpellId.None) dst.specialSpellId = spell;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(node.oneShotFlag))
            AddFlagIfMissing(dst, node.oneShotFlag);
    }

    private static void AddFlagIfMissing(PlayerPresetSO dst, string flag)
    {
        dst.flags ??= new List<string>();
        if (!dst.flags.Contains(flag)) dst.flags.Add(flag);
    }

    private static void RemoveFlagIfPresent(PlayerPresetSO dst, string flag)
    {
        dst.flags?.Remove(flag);
    }

    private static bool HasFlag(PlayerPresetSO dst, string flag) => dst.flags != null && dst.flags.Contains(flag);

    /// <summary>Busca el QuestData de un questId en todo el proyecto (cacheado por ejecución de Fast-Forward).</summary>
    private QuestData FindQuestData(string questId)
    {
        if (_questCatalogCache == null)
        {
            _questCatalogCache = new Dictionary<string, QuestData>();
            var guids = AssetDatabase.FindAssets("t:QuestData");
            foreach (var g in guids)
            {
                var qd = AssetDatabase.LoadAssetAtPath<QuestData>(AssetDatabase.GUIDToAssetPath(g));
                if (qd != null && !string.IsNullOrEmpty(qd.questId) && !_questCatalogCache.ContainsKey(qd.questId))
                    _questCatalogCache[qd.questId] = qd;
            }
        }
        _questCatalogCache.TryGetValue(questId, out var result);
        return result;
    }

    private void TryAutoDetectLabel(NarrativeGraph graph)
    {
        if (graph == null) return;
        var graphPath = AssetDatabase.GetAssetPath(graph);
        var graphGuid = AssetDatabase.AssetPathToGUID(graphPath);

        // Search Start.unity scene for NarrativeGraphHub configuration
        var sceneGuids = AssetDatabase.FindAssets("t:Scene Start");
        foreach (var sceneGuid in sceneGuids)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (!scenePath.Contains("Start")) continue;

            var sceneText = System.IO.File.ReadAllText(scenePath);
            int idx = sceneText.IndexOf(graphGuid, System.StringComparison.Ordinal);
            if (idx < 0) continue;

            // Look backwards for "label:" line
            int searchStart = Mathf.Max(0, idx - 200);
            string chunk = sceneText.Substring(searchStart, idx - searchStart);
            int labelIdx = chunk.LastIndexOf("label:", System.StringComparison.Ordinal);
            if (labelIdx >= 0)
            {
                int lineEnd = chunk.IndexOf('\n', labelIdx);
                if (lineEnd < 0) lineEnd = chunk.Length;
                string labelLine = chunk.Substring(labelIdx + 6, lineEnd - labelIdx - 6).Trim();
                if (!string.IsNullOrEmpty(labelLine))
                {
                    _graphLabel = labelLine;
                    return;
                }
            }
        }
    }
}
