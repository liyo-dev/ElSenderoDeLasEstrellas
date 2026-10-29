using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using NarrativeGraphEditor;

/// <summary>
/// Narrative Timeline — vista temporal, estilo editor de vídeo, de toda la historia.
/// Muestra cada NarrativeGraph como un track horizontal con los nodos ordenados
/// topológicamente. Cada nodo toma el color de su categoría ([NarrativeNodeInfo], vía
/// NarrativeNodeCatalog) y su detalle de lo que declara en NarrativeNode.Describe; las
/// dependencias entre grafos (un nodo emite una señal que otro grafo espera) se dibujan como
/// líneas cruzadas. Ningún tipo de nodo se conoce por su nombre.
/// </summary>
public class NarrativeTimelineWindow : EditorWindow
{
    // ─── Constants ───

    private const float TrackHeaderWidth = 180f;
    private const float NodeWidth = 160f;
    private const float NodeHeight = 48f;
    private const float NodeSpacingX = 24f;
    private const float NodeSpacingY = 12f;
    private const float TrackPadding = 16f;
    private const float TrackTitleHeight = 28f;
    private const float TrackGap = 20f;
    private const float TopMargin = 40f;
    private const float ForkVerticalOffset = 56f;

    // ─── Internal data model ───

    private class TimelineNode
    {
        public NarrativeNode node;
        public string category;
        public Color color;
        public string label;
        public string subtitle;
        public int column;
        public int row;
        public Rect rect;
        public int graphIndex;
        /// Señales que el nodo emite y espera (de su Describe).
        public readonly List<string> emits = new List<string>();
        public readonly List<string> waits = new List<string>();
        /// Resumen de sus señales para el menú, el tooltip y la búsqueda («↑ A  ↓ B»).
        public string signals;
    }

    private class GraphTrack
    {
        public NarrativeGraph graph;
        public string assetPath;
        public string displayName;
        public List<TimelineNode> nodes = new List<TimelineNode>();
        public int maxColumn;
        public int maxRow;
        public float yOffset;
        public float trackHeight;
        public bool collapsed;
    }

    private class CrossGraphLink
    {
        public TimelineNode raiser;
        public TimelineNode waiter;
        public string eventKey;
    }

    // ─── State ───

    private List<GraphTrack> _tracks = new List<GraphTrack>();
    private List<CrossGraphLink> _crossLinks = new List<CrossGraphLink>();
    private Vector2 _scrollPos;
    private float _zoom = 1f;
    private bool _hasBuilt;
    private string _searchFilter = "";
    private TimelineNode _selectedNode;
    private TimelineNode _hoveredNode;
    private bool _showLegend = true;
    private bool _showCrossLinks = true;

    // Categorías presentes en los tracks, en el orden de NarrativeNodeCatalog (leyenda).
    private readonly List<string> _usedCategories = new List<string>();
    private readonly NarrativeNodeReferences _refs = new NarrativeNodeReferences();

    // Runtime
    private string _runtimeCurrentNodeGuid;
    private int _runtimeGraphIndex = -1;

    // ─── Menu ───

    [MenuItem("El Sendero/Narrativa/Narrative Timeline")]
    public static void ShowWindow()
    {
        var w = GetWindow<NarrativeTimelineWindow>("Narrative Timeline");
        w.minSize = new Vector2(900, 400);
    }

    // ─── Lifecycle ───

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        Repaint();
    }

    // ─── Build ───

    private void RebuildTimeline()
    {
        _tracks.Clear();
        _crossLinks.Clear();
        _usedCategories.Clear();
        _selectedNode = null;
        _hoveredNode = null;

        var guids = AssetDatabase.FindAssets("t:NarrativeGraph");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var graph = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(path);
            if (graph == null || graph.nodes == null || graph.nodes.Count == 0) continue;

            var track = new GraphTrack
            {
                graph = graph,
                assetPath = path,
                displayName = graph.name
            };

            BuildTrackNodes(track);
            _tracks.Add(track);
        }

        BuildCrossGraphLinks();
        ComputeLayout();
        _usedCategories.Sort((a, b) => NarrativeNodeCatalog.CategoryOrder(a).CompareTo(NarrativeNodeCatalog.CategoryOrder(b)));
        _hasBuilt = true;
    }

    private void BuildTrackNodes(GraphTrack track)
    {
        var graph = track.graph;
        var visited = new HashSet<string>();
        var nodeMap = new Dictionary<string, NarrativeNode>();

        foreach (var n in graph.nodes)
        {
            if (n != null && !string.IsNullOrEmpty(n.guid))
                nodeMap[n.guid] = n;
        }

        // Topological order via BFS from start node
        var queue = new Queue<(string guid, int col, int row)>();
        var startGuid = graph.startNodeGuid;
        if (string.IsNullOrEmpty(startGuid) && graph.nodes.Count > 0)
            startGuid = graph.nodes[0].guid;

        if (!string.IsNullOrEmpty(startGuid) && nodeMap.ContainsKey(startGuid))
            queue.Enqueue((startGuid, 0, 0));

        int maxCol = 0;
        int maxRow = 0;
        int graphIndex = _tracks.Count;

        while (queue.Count > 0)
        {
            var (guid, col, row) = queue.Dequeue();
            if (visited.Contains(guid)) continue;
            visited.Add(guid);

            if (!nodeMap.TryGetValue(guid, out var node)) continue;

            var tn = CreateTimelineNode(node, col, row, graphIndex);
            track.nodes.Add(tn);
            if (col > maxCol) maxCol = col;
            if (row > maxRow) maxRow = row;

            if (node.outputs != null)
            {
                for (int i = 0; i < node.outputs.Count; i++)
                {
                    var outGuid = node.outputs[i];
                    if (string.IsNullOrEmpty(outGuid) || visited.Contains(outGuid)) continue;
                    int nextRow = (node.outputs.Count > 1) ? row + i : row;
                    queue.Enqueue((outGuid, col + 1, nextRow));
                }
            }
        }

        // Add unvisited nodes (disconnected)
        foreach (var n in graph.nodes)
        {
            if (n == null || visited.Contains(n.guid)) continue;
            maxCol++;
            track.nodes.Add(CreateTimelineNode(n, maxCol, 0, graphIndex));
        }

        track.maxColumn = maxCol;
        track.maxRow = maxRow;
    }

    private void BuildCrossGraphLinks()
    {
        var raisersByKey = new Dictionary<string, List<TimelineNode>>();
        var waitersByKey = new Dictionary<string, List<TimelineNode>>();

        foreach (var track in _tracks)
        {
            foreach (var tn in track.nodes)
            {
                foreach (var key in tn.emits)
                {
                    if (!raisersByKey.TryGetValue(key, out var raisers))
                        raisersByKey[key] = raisers = new List<TimelineNode>();
                    raisers.Add(tn);
                }
                foreach (var key in tn.waits)
                {
                    if (!waitersByKey.TryGetValue(key, out var waiters))
                        waitersByKey[key] = waiters = new List<TimelineNode>();
                    waiters.Add(tn);
                }
            }
        }

        foreach (var kvp in raisersByKey)
        {
            if (!waitersByKey.TryGetValue(kvp.Key, out var waiters)) continue;
            foreach (var raiser in kvp.Value)
            {
                foreach (var waiter in waiters)
                {
                    if (raiser.graphIndex == waiter.graphIndex) continue;
                    _crossLinks.Add(new CrossGraphLink
                    {
                        raiser = raiser,
                        waiter = waiter,
                        eventKey = kvp.Key
                    });
                }
            }
        }
    }

    private void ComputeLayout()
    {
        float yOffset = TopMargin;
        foreach (var track in _tracks)
        {
            track.yOffset = yOffset;
            float rowCount = track.maxRow + 1;
            track.trackHeight = TrackTitleHeight + TrackPadding * 2 + rowCount * (NodeHeight + NodeSpacingY);

            foreach (var tn in track.nodes)
            {
                float x = TrackHeaderWidth + TrackPadding + tn.column * (NodeWidth + NodeSpacingX);
                float y = track.yOffset + TrackTitleHeight + TrackPadding + tn.row * (NodeHeight + NodeSpacingY);
                tn.rect = new Rect(x, y, NodeWidth, NodeHeight);
            }

            yOffset += track.trackHeight + TrackGap;
        }
    }

    // ─── Helpers ───

    /// <summary>Tarjeta de la línea temporal con lo que el nodo declara de sí mismo.</summary>
    private TimelineNode CreateTimelineNode(NarrativeNode node, int col, int row, int graphIndex)
    {
        var type = node.GetType();
        var category = NarrativeNodeCatalog.CategoryOf(type);
        if (!_usedCategories.Contains(category)) _usedCategories.Add(category);

        var tn = new TimelineNode
        {
            node = node,
            category = category,
            color = NarrativeNodeCatalog.ColorOf(category),
            label = GetNodeLabel(node),
            column = col,
            row = row,
            graphIndex = graphIndex,
        };

        _refs.Collect(node);
        foreach (var signal in _refs.Signals)
        {
            if (string.IsNullOrEmpty(signal.Key)) continue;
            if (signal.Role == SignalRole.Emite) tn.emits.Add(signal.Key);
            else if (signal.Role == SignalRole.Escucha) tn.waits.Add(signal.Key);
        }
        var parts = tn.emits.Select(k => "↑ " + k).Concat(tn.waits.Select(k => "↓ " + k)).ToList();
        tn.signals = parts.Count > 0 ? string.Join("  ", parts) : null;
        tn.subtitle = GetNodeSubtitle(node, _refs);
        return tn;
    }

    private static string GetNodeLabel(NarrativeNode node)
    {
        if (node == null) return "?";
        if (!string.IsNullOrEmpty(node.displayTitle))
        {
            var title = node.displayTitle;
            return title.Length > 22 ? title.Substring(0, 19) + "..." : title;
        }
        return NarrativeNodeCatalog.TitleOf(node.GetType());
    }

    /// <summary>
    /// Detalle de la tarjeta: su señal principal, si no la misión que toca, si no el asset que
    /// usa, si no cualquier señal; sin nada de eso, el nombre del tipo de nodo.
    /// </summary>
    private static string GetNodeSubtitle(NarrativeNode node, NarrativeNodeReferences refs)
    {
        foreach (var s in refs.Signals)
            if (!string.IsNullOrEmpty(s.Key) && s.Label == null) return s.Key;
        foreach (var q in refs.Quests)
            if (!string.IsNullOrEmpty(q.QuestId)) return q.QuestId;
        foreach (var a in refs.Assets)
            if (a != null) return a.name;
        foreach (var s in refs.Signals)
            if (!string.IsNullOrEmpty(s.Key)) return s.Key;
        return NarrativeNodeCatalog.TitleOf(node.GetType());
    }

    // ─── Runtime ───

    private void UpdateRuntimeState()
    {
        _runtimeCurrentNodeGuid = null;
        _runtimeGraphIndex = -1;

        if (!Application.isPlaying) return;
        if (NarrativeGraphHub.Instance == null) return;

        var runners = NarrativeGraphHub.Instance.GetAllRunners();
        if (runners == null) return;

        foreach (var runner in runners)
        {
            if (runner == null || runner.CurrentNode == null) continue;
            _runtimeCurrentNodeGuid = runner.CurrentNode.guid;

            for (int i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i].graph == runner.graph)
                {
                    _runtimeGraphIndex = i;
                    break;
                }
            }
            break;
        }
    }

    // ─── GUI ───

    private void OnGUI()
    {
        DrawToolbar();

        if (!_hasBuilt)
        {
            EditorGUILayout.HelpBox(
                "Pulsa 'Construir Timeline' para escanear todos los NarrativeGraph del proyecto.",
                MessageType.Info);
            return;
        }

        if (_tracks.Count == 0)
        {
            EditorGUILayout.HelpBox("No se encontraron NarrativeGraph assets.", MessageType.Warning);
            return;
        }

        if (Application.isPlaying)
            UpdateRuntimeState();

        DrawTimeline();

        if (_showLegend)
            DrawLegend();

        // Tooltip for hovered node
        if (_hoveredNode != null)
            DrawTooltip(_hoveredNode);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Construir Timeline", EditorStyles.toolbarButton, GUILayout.Width(130)))
            RebuildTimeline();

        GUILayout.Space(8);

        EditorGUILayout.LabelField("Buscar:", GUILayout.Width(45));
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(180));

        GUILayout.Space(8);

        EditorGUILayout.LabelField("Zoom:", GUILayout.Width(38));
        _zoom = GUILayout.HorizontalSlider(_zoom, 0.4f, 2f, GUILayout.Width(100));

        GUILayout.Space(8);

        _showCrossLinks = GUILayout.Toggle(_showCrossLinks, "Dependencias", EditorStyles.toolbarButton, GUILayout.Width(95));
        _showLegend = GUILayout.Toggle(_showLegend, "Leyenda", EditorStyles.toolbarButton, GUILayout.Width(65));

        GUILayout.FlexibleSpace();

        if (Application.isPlaying)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(0.3f, 0.9f, 0.3f);
            GUILayout.Label("● PLAY MODE", EditorStyles.toolbarButton);
            GUI.color = oldColor;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTimeline()
    {
        float totalWidth = 0;
        float totalHeight = TopMargin;
        foreach (var track in _tracks)
        {
            float w = TrackHeaderWidth + TrackPadding * 2 + (track.maxColumn + 1) * (NodeWidth + NodeSpacingX) + 60;
            if (w > totalWidth) totalWidth = w;
            totalHeight += track.trackHeight + TrackGap;
        }

        totalWidth *= _zoom;
        totalHeight *= _zoom;

        _scrollPos = GUI.BeginScrollView(
            new Rect(0, 20, position.width, position.height - 20),
            _scrollPos,
            new Rect(0, 0, totalWidth + 40, totalHeight + 40));

        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(_zoom, _zoom, 1f));

        _hoveredNode = null;

        // Draw tracks
        for (int i = 0; i < _tracks.Count; i++)
            DrawTrack(_tracks[i], i);

        // Draw intra-graph edges
        foreach (var track in _tracks)
            DrawTrackEdges(track);

        // Draw cross-graph links
        if (_showCrossLinks)
            DrawCrossGraphLinks();

        GUI.matrix = oldMatrix;
        GUI.EndScrollView();
    }

    private void DrawTrack(GraphTrack track, int trackIndex)
    {
        if (track.collapsed)
        {
            DrawTrackHeader(track, trackIndex, true);
            return;
        }

        // Track background
        var bgRect = new Rect(0, track.yOffset, TrackHeaderWidth + TrackPadding * 2 + (track.maxColumn + 1) * (NodeWidth + NodeSpacingX) + 60, track.trackHeight);
        var bgColor = trackIndex % 2 == 0
            ? new Color(0.22f, 0.22f, 0.22f, 0.6f)
            : new Color(0.26f, 0.26f, 0.26f, 0.6f);
        EditorGUI.DrawRect(bgRect, bgColor);

        DrawTrackHeader(track, trackIndex, false);

        // Draw nodes
        foreach (var tn in track.nodes)
        {
            if (!PassesFilter(tn)) continue;
            DrawTimelineNode(tn);
        }
    }

    private void DrawTrackHeader(GraphTrack track, int trackIndex, bool collapsed)
    {
        var headerRect = new Rect(4, track.yOffset + 2, TrackHeaderWidth - 8, TrackTitleHeight - 4);
        EditorGUI.DrawRect(headerRect, new Color(0.18f, 0.18f, 0.18f, 0.9f));

        // Collapse toggle
        var foldRect = new Rect(8, track.yOffset + 6, 16, 16);
        if (GUI.Button(foldRect, collapsed ? "►" : "▼", EditorStyles.miniLabel))
            track.collapsed = !track.collapsed;

        // Graph name
        var labelRect = new Rect(26, track.yOffset + 4, TrackHeaderWidth - 34, TrackTitleHeight - 4);
        var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, normal = { textColor = Color.white } };
        GUI.Label(labelRect, track.displayName, style);

        // Node count
        var countRect = new Rect(8, track.yOffset + TrackTitleHeight - 2, TrackHeaderWidth - 16, 14);
        var countStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
        GUI.Label(countRect, $"{track.nodes.Count} nodos", countStyle);

        // Click header to select graph asset
        if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
        {
            var asset = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(track.assetPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
            Event.current.Use();
        }
    }

    private void DrawTimelineNode(TimelineNode tn)
    {
        var rect = tn.rect;
        bool isSelected = _selectedNode == tn;
        bool isRuntime = Application.isPlaying && tn.node.guid == _runtimeCurrentNodeGuid;
        bool isHovered = rect.Contains(Event.current.mousePosition);

        if (isHovered) _hoveredNode = tn;

        // Node background
        var catColor = tn.color;
        if (isSelected)
            catColor = Color.Lerp(catColor, Color.white, 0.3f);
        else if (isHovered)
            catColor = Color.Lerp(catColor, Color.white, 0.15f);

        EditorGUI.DrawRect(rect, catColor);

        // Runtime indicator - pulsing border
        if (isRuntime)
        {
            float pulse = Mathf.PingPong(Time.realtimeSinceStartup * 2f, 1f);
            var runtimeColor = Color.Lerp(new Color(0f, 1f, 0f, 0.6f), new Color(0f, 1f, 0f, 1f), pulse);
            DrawRectBorder(rect, runtimeColor, 3f);
            Repaint();
        }
        else if (isSelected)
        {
            DrawRectBorder(rect, Color.white, 2f);
        }

        // Category bar (left edge)
        var barRect = new Rect(rect.x, rect.y, 4, rect.height);
        EditorGUI.DrawRect(barRect, Color.Lerp(catColor, Color.black, 0.3f));

        // Label
        var labelRect = new Rect(rect.x + 8, rect.y + 4, rect.width - 12, 18);
        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 10,
            normal = { textColor = GetTextColor(catColor) },
            clipping = TextClipping.Clip
        };
        GUI.Label(labelRect, tn.label, labelStyle);

        // Subtitle
        if (!string.IsNullOrEmpty(tn.subtitle))
        {
            var subRect = new Rect(rect.x + 8, rect.y + 22, rect.width - 12, 18);
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                normal = { textColor = Color.Lerp(GetTextColor(catColor), catColor, 0.3f) },
                clipping = TextClipping.Clip
            };
            GUI.Label(subRect, tn.subtitle, subStyle);
        }

        // Search highlight
        if (!string.IsNullOrEmpty(_searchFilter) && PassesFilter(tn))
        {
            var highlightRect = new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2);
            DrawRectBorder(highlightRect, new Color(1f, 1f, 0f, 0.8f), 2f);
        }

        // Click handling
        HandleNodeClick(tn, rect);
    }

    private void HandleNodeClick(TimelineNode tn, Rect rect)
    {
        if (Event.current.type != EventType.MouseDown) return;
        if (!rect.Contains(Event.current.mousePosition)) return;

        if (Event.current.button == 0)
        {
            _selectedNode = tn;
            Event.current.Use();
        }
        else if (Event.current.button == 1)
        {
            _selectedNode = tn;
            ShowNodeContextMenu(tn);
            Event.current.Use();
        }
    }

    private void ShowNodeContextMenu(TimelineNode tn)
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("Abrir en Graph Editor"), false, () =>
        {
            var track = _tracks.FirstOrDefault(t => t.nodes.Contains(tn));
            if (track != null)
                OpenInGraphEditor(track.graph, tn.node);
        });

        menu.AddItem(new GUIContent("Seleccionar Asset"), false, () =>
        {
            var track = _tracks.FirstOrDefault(t => t.nodes.Contains(tn));
            if (track != null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(track.assetPath);
                if (asset != null) Selection.activeObject = asset;
            }
        });

        menu.AddSeparator("");

        // Quick Test option
        var track2 = _tracks.FirstOrDefault(t => t.nodes.Contains(tn));
        if (track2 != null)
        {
            menu.AddItem(new GUIContent("Quick Test desde aquí"), false, () =>
            {
                LaunchQuickTest(track2.graph, tn.node, track2.displayName);
            });
        }

        if (!string.IsNullOrEmpty(tn.signals))
        {
            menu.AddSeparator("");
            menu.AddDisabledItem(new GUIContent($"Señales: {tn.signals}"));
        }

        menu.ShowAsContext();
    }

    private void OpenInGraphEditor(NarrativeGraph graph, NarrativeNode node)
    {
        var window = GetWindow<NarrativeGraphWindow>();
        window.LoadGraph(graph);
        window.Focus();
    }

    private void LaunchQuickTest(NarrativeGraph graph, NarrativeNode node, string graphLabel)
    {
        var quickTestType = typeof(NarrativeQuickTestWindow);
        var method = quickTestType.GetMethod("OpenWithNode",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (method != null)
        {
            method.Invoke(null, new object[] { graph, node, graphLabel });
        }
        else
        {
            Debug.LogWarning("[NarrativeTimeline] NarrativeQuickTestWindow.OpenWithNode no encontrado.");
        }
    }

    // ─── Edges ───

    private void DrawTrackEdges(GraphTrack track)
    {
        var nodeByGuid = new Dictionary<string, TimelineNode>();
        foreach (var tn in track.nodes)
        {
            if (tn.node != null && !string.IsNullOrEmpty(tn.node.guid))
                nodeByGuid[tn.node.guid] = tn;
        }

        foreach (var tn in track.nodes)
        {
            if (tn.node == null || tn.node.outputs == null) continue;
            if (!PassesFilter(tn) && string.IsNullOrEmpty(_searchFilter)) continue;

            foreach (var outGuid in tn.node.outputs)
            {
                if (string.IsNullOrEmpty(outGuid)) continue;
                if (!nodeByGuid.TryGetValue(outGuid, out var target)) continue;
                if (!PassesFilter(target) && !string.IsNullOrEmpty(_searchFilter)) continue;

                DrawEdge(tn.rect, target.rect, new Color(0.6f, 0.6f, 0.6f, 0.5f), 1.5f);
            }
        }
    }

    private void DrawCrossGraphLinks()
    {
        foreach (var link in _crossLinks)
        {
            var color = new Color(1f, 0.6f, 0.2f, 0.7f);
            DrawEdge(link.raiser.rect, link.waiter.rect, color, 2.5f, true);

            // Label on the middle
            var midX = (link.raiser.rect.center.x + link.waiter.rect.center.x) * 0.5f;
            var midY = (link.raiser.rect.center.y + link.waiter.rect.center.y) * 0.5f;
            var labelRect = new Rect(midX - 50, midY - 10, 100, 16);
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = new Color(1f, 0.7f, 0.3f, 0.9f) }
            };

            // Background for readability
            EditorGUI.DrawRect(new Rect(labelRect.x - 2, labelRect.y - 1, labelRect.width + 4, labelRect.height + 2),
                new Color(0.1f, 0.1f, 0.1f, 0.8f));
            GUI.Label(labelRect, link.eventKey, labelStyle);
        }
    }

    private void DrawEdge(Rect from, Rect to, Color color, float width, bool dashed = false)
    {
        var start = new Vector2(from.xMax, from.center.y);
        var end = new Vector2(to.xMin, to.center.y);

        // If target is to the left (back-edge), route differently
        if (end.x < start.x)
        {
            start = new Vector2(from.center.x, from.yMax);
            end = new Vector2(to.center.x, to.yMin);
        }

        Handles.BeginGUI();
        var oldColor = Handles.color;
        Handles.color = color;

        if (dashed)
        {
            Handles.DrawDottedLine(start, end, 4f);
        }
        else
        {
            // Bezier curve
            float tangentStrength = Mathf.Min(Mathf.Abs(end.x - start.x) * 0.4f, 60f);
            var startTangent = start + Vector2.right * tangentStrength;
            var endTangent = end + Vector2.left * tangentStrength;
            Handles.DrawBezier(start, end, startTangent, endTangent, color, null, width);
        }

        Handles.color = oldColor;
        Handles.EndGUI();
    }

    // ─── Legend ───

    private void DrawLegend()
    {
        float legendWidth = 160;
        float legendHeight = _usedCategories.Count * 18 + 28;
        float x = position.width / _zoom - legendWidth - 20;
        float y = position.height / _zoom - legendHeight - 20;

        // Adjust for scroll
        x = position.width - legendWidth - 16;
        y = position.height - legendHeight - 16;

        // Draw outside scroll view, in screen space
        var legendRect = new Rect(x, y, legendWidth, legendHeight);
        GUI.BeginGroup(legendRect);

        EditorGUI.DrawRect(new Rect(0, 0, legendWidth, legendHeight), new Color(0.15f, 0.15f, 0.15f, 0.92f));
        DrawRectBorder(new Rect(0, 0, legendWidth, legendHeight), new Color(0.4f, 0.4f, 0.4f, 0.6f), 1f);

        var titleStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        GUI.Label(new Rect(8, 4, legendWidth - 16, 16), "Leyenda", titleStyle);

        int i = 0;
        foreach (var category in _usedCategories)
        {
            float iy = 24 + i * 18;
            EditorGUI.DrawRect(new Rect(8, iy + 2, 12, 12), NarrativeNodeCatalog.ColorOf(category));
            GUI.Label(new Rect(24, iy, legendWidth - 32, 16), category, EditorStyles.miniLabel);
            i++;
        }

        GUI.EndGroup();
    }

    // ─── Tooltip ───

    private void DrawTooltip(TimelineNode tn)
    {
        var mousePos = Event.current.mousePosition;
        float tooltipWidth = 260;
        float tooltipHeight = 80;

        // Build tooltip text
        var lines = new List<string>();
        lines.Add(tn.node.displayTitle ?? tn.node.GetType().Name);
        lines.Add($"Tipo: {NarrativeNodeCatalog.TitleOf(tn.node.GetType())} ({tn.category})");
        if (!string.IsNullOrEmpty(tn.signals))
            lines.Add($"Señales: {tn.signals}");
        if (!string.IsNullOrEmpty(tn.subtitle) && tn.subtitle != NarrativeNodeCatalog.TitleOf(tn.node.GetType()))
            lines.Add($"Detalle: {tn.subtitle}");
        lines.Add($"GUID: {tn.node.guid.Substring(0, Mathf.Min(8, tn.node.guid.Length))}...");

        tooltipHeight = lines.Count * 16 + 12;

        float tx = mousePos.x + 16;
        float ty = mousePos.y + 16;

        // Keep on screen
        if (tx + tooltipWidth > position.width) tx = mousePos.x - tooltipWidth - 8;
        if (ty + tooltipHeight > position.height) ty = mousePos.y - tooltipHeight - 8;

        var tooltipRect = new Rect(tx, ty, tooltipWidth, tooltipHeight);
        EditorGUI.DrawRect(tooltipRect, new Color(0.12f, 0.12f, 0.12f, 0.95f));
        DrawRectBorder(tooltipRect, new Color(0.5f, 0.5f, 0.5f, 0.7f), 1f);

        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
        for (int i = 0; i < lines.Count; i++)
        {
            if (i == 0) style.fontStyle = FontStyle.Bold;
            else style.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(tx + 8, ty + 4 + i * 16, tooltipWidth - 16, 16), lines[i], style);
        }

        Repaint();
    }

    // ─── Utilities ───

    private bool PassesFilter(TimelineNode tn)
    {
        if (string.IsNullOrEmpty(_searchFilter)) return true;
        var filter = _searchFilter.ToLowerInvariant();
        if (tn.label != null && tn.label.ToLowerInvariant().Contains(filter)) return true;
        if (tn.subtitle != null && tn.subtitle.ToLowerInvariant().Contains(filter)) return true;
        if (tn.signals != null && tn.signals.ToLowerInvariant().Contains(filter)) return true;
        if (tn.node.displayTitle != null && tn.node.displayTitle.ToLowerInvariant().Contains(filter)) return true;
        if (tn.node.GetType().Name.ToLowerInvariant().Contains(filter)) return true;
        return false;
    }

    private static Color GetTextColor(Color bg)
    {
        float luminance = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
        return luminance > 0.5f ? Color.black : Color.white;
    }

    private static void DrawRectBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
