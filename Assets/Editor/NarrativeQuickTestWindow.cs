using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Quick Test System — Configura el juego para arrancar desde un nodo específico del grafo narrativo.
/// Genera automáticamente un preset temporal con el blackboard configurado para que el NarrativeRunner
/// continúe desde el nodo seleccionado, y lanza Play Mode.
///
/// FAST-FORWARD: calcula el estado del mundo al llegar al nodo objetivo con NarrativeStateProjector
/// (cada nodo declara su propio efecto con INarrativeStateEffect; esta ventana no conoce tipos de
/// nodo) y lo escribe en el preset temporal con PresetNarrativeStateWriter: quests, objetos,
/// habilidades/hechizos, flags y dónde queda cada actor (resuelto contra las escenas abiertas).
/// El nodo objetivo no se proyecta, porque se va a ejecutar. Si el camino pasa por una decisión
/// real (flag, estado de quest, pregunta, objeto requerido) no se adivina la rama: se avisa.
/// Se puede desactivar con el checkbox correspondiente para volver al comportamiento anterior (progreso
/// vacío/neutro). Además, independientemente del Fast-Forward, el tool siempre:
///   1. Coloca el grafo en el nodo elegido (vía blackboard "__currentNodeGuid"). Si el grafo es
///      la continuación de otro capítulo (empieza esperando la señal final de otro grafo del Hub),
///      los capítulos previos se dan por terminados para que no arranquen desde su inicio, y el
///      inicio/espera de ese puente se salta. Con Fast-Forward también se proyecta su progreso.
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
        "Cap1", "Cap2", "Misiones Secundarias"
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
    private readonly SpellId[] _basicSpellIds = new SpellId[MagicCaster.BasicSlotCount];
    private SpellId _comboSpellId = SpellId.None;

    // Fast-Forward (progreso previo simulado desde el inicio del grafo)
    private bool _fastForward = true;
    // Serializados para que el resultado de la vista previa sobreviva a la recarga de dominio al entrar en Play.
    [SerializeField] private bool _fastForwardPreviewRan;
    [SerializeField] private bool _fastForwardReachedTarget;
    [SerializeField] private List<string> _fastForwardWarnings = new List<string>();
    [SerializeField] private List<string> _fastForwardActors = new List<string>();
    [SerializeField] private List<string> _fastForwardNotes = new List<string>();

    // State
    private int _selectedNodeIndex;
    private Vector2 _scrollPos;
    private bool _autoRestore = true;
    private string _status = "";


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
        if (state == PlayModeStateChange.EnteredEditMode)
            Sendero.Narrative.Editor.NarrativeTestLauncher.RestoreIfNeeded();
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
        for (int i = 0; i < _basicSpellIds.Length; i++)
            _basicSpellIds[i] = (SpellId)EditorGUILayout.EnumPopup($"Básico {i + 1} (X, LB rota)", _basicSpellIds[i]);
        _comboSpellId = (SpellId)EditorGUILayout.EnumPopup("Combo desbloqueado (Y)", _comboSpellId);

        EditorGUILayout.Space(8);

        // Fast-Forward
        EditorGUILayout.LabelField("Fast-Forward (progreso previo)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Simula el camino desde el inicio del grafo hasta el nodo objetivo y aplica automáticamente " +
            "quests iniciadas/completadas, objetos entregados, habilidades/hechizos desbloqueados, flags " +
            "y dónde queda cada actor (necesita abiertas las escenas donde están los actores y sus marcas). " +
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

        if (_fastForwardActors.Count > 0)
            EditorGUILayout.HelpBox("Actores colocados:\n" + string.Join("\n", _fastForwardActors), MessageType.Info);
        if (_fastForwardNotes.Count > 0)
            EditorGUILayout.HelpBox("Revisar:\n\n" + string.Join("\n\n", _fastForwardNotes), MessageType.Warning);

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
        if (Sendero.Narrative.Editor.NarrativeTestLauncher.NeedsRestore)
        {
            GUI.backgroundColor = new Color(0.85f, 0.6f, 0.2f);
            if (GUILayout.Button("Restaurar bootPreset original"))
            {
                Sendero.Narrative.Editor.NarrativeTestLauncher.RestoreOriginalBootPreset();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
    }

    private void LaunchQuickTest()
    {
        var cfg = BuildConfig();
        if (!Sendero.Narrative.Editor.NarrativeTestLauncher.Launch(
                _targetGraph, _targetNodeGuid, cfg, out var status, out var preview))
        {
            _status = status;
            return;
        }

        CopyPreview(preview);
        if (cfg.fastForward)
        {
            if (preview.warnings.Count > 0)
                Debug.LogWarning($"[QuickTest] Fast-Forward encontró {preview.warnings.Count} bifurcación(es):\n" +
                    string.Join("\n", preview.warnings));
            if (preview.notes.Count > 0)
                Debug.LogWarning("[QuickTest] Fast-Forward, a revisar:\n" + string.Join("\n", preview.notes));
            if (!preview.reachedTarget)
                Debug.LogWarning("[QuickTest] Fast-Forward no llegó al nodo objetivo automáticamente " +
                    "(revisa las bifurcaciones). Se lanzará igualmente con el progreso parcial acumulado.");
        }
        _status = status;
    }

    private Sendero.Narrative.Editor.NarrativeTestConfig BuildConfig()
    {
        return new Sendero.Narrative.Editor.NarrativeTestConfig
        {
            graphLabel = _graphLabel,
            spawnAnchorId = _spawnAnchorId,
            level = _level,
            maxHP = _maxHP,
            maxMP = _maxMP,
            abilitySwim = _abilitySwim,
            abilityJump = _abilityJump,
            abilityClimb = _abilityClimb,
            abilityMagic = _abilityMagic,
            abilityFly = _abilityFly,
            abilitySprint = _abilitySprint,
            abilityShield = _abilityShield,
            basicSpellIds = (SpellId[])_basicSpellIds.Clone(),
            comboSpellId = _comboSpellId,
            fastForward = _fastForward,
            autoRestore = _autoRestore
        };
    }

    private void CopyPreview(Sendero.Narrative.Editor.NarrativeFastForwardPreview preview)
    {
        _fastForwardPreviewRan = preview != null && preview.ran;
        _fastForwardReachedTarget = preview != null && preview.reachedTarget;
        _fastForwardWarnings = preview?.warnings ?? new List<string>();
        _fastForwardActors = preview?.actors ?? new List<string>();
        _fastForwardNotes = preview?.notes ?? new List<string>();
    }

    /// <summary>
    /// Busca componentes SpawnAnchor en las escenas actualmente abiertas en el editor (no requiere
    /// Play Mode: son GameObjects normales de la escena) y ofrece sus anchorId como sugerencias.
    /// Si la escena del nodo objetivo no está abierta, no aparecerá aquí — hay que abrirla primero.
    /// </summary>
    private void RefreshDetectedAnchors()
    {
        _detectedAnchors = Sendero.Narrative.Editor.NarrativeTestLauncher.DetectSpawnAnchors();
        if (_detectedAnchors.Length == 0)
            _status = "No se han encontrado SpawnAnchor en las escenas abiertas. Abre la escena del nodo objetivo primero.";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fast-Forward: proyecta el estado del mundo en el nodo objetivo. NUNCA invoca
    // Enter() de un nodo real (dispararía diálogos/señales de verdad fuera de Play Mode).
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

        CopyPreview(Sendero.Narrative.Editor.NarrativeTestLauncher.Preview(_targetGraph, _targetNodeGuid, BuildConfig()));
        Repaint();
    }

    private void TryAutoDetectLabel(NarrativeGraph graph)
    {
        var label = Sendero.Narrative.Editor.NarrativeTestLauncher.DetectGraphLabel(graph);
        if (!string.IsNullOrEmpty(label)) _graphLabel = label;
    }
}
