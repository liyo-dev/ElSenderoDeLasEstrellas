using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#pragma warning disable 618 // se referencian nodos [Obsolete] a propósito para seguir mostrando grafos viejos
namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Editor visual del grafo narrativo — "el único sitio" del guion.
    ///
    /// Layout: paleta de nodos (izquierda) · lienzo (centro) · inspector del nodo seleccionado con
    /// el contenido resuelto (derecha). Las tarjetas muestran el texto real de diálogos y quests;
    /// los campos de clave llevan desplegables (NarrativeKeyDrawer); los nodos que bifurcan tienen
    /// puertos con nombre; un nodo normal con varias aristas se marca como FORK.
    /// </summary>
    public class NarrativeGraphWindow : EditorWindow
    {
        const string StylesPath = "Assets/NarrativeGraph/Editor/NarrativeGraphStyles.uss";
        const string AllChaptersLabel = "Todos";
        const string NoChapterKey = "__none__";
        const string PrefLastGraph = "Sendero.Narrative.LastGraph";

        NarrativeGraph _graph;
        SerializedObject _so;
        NarrativeGraphView _view;
        NarrativeInspectorPanel _inspector;
        VisualElement _palette;
        readonly Dictionary<string, NodeView> _nodeViews = new();
        bool _suppressGraphCallbacks;

        // toolbar
        ToolbarMenu _graphMenu;
        ToolbarMenu _chapterMenu;
        ToolbarToggle _hideOthersToggle;
        Label _countLabel;
        ToolbarSearchField _searchField;
        Label _searchCountLabel;
        Label _statusLabel;

        string _activeChapter = AllChaptersLabel;
        readonly List<string> _searchMatches = new();
        int _searchMatchIndex = -1;
        NodeView _lastSelected;

        [MenuItem("El Sendero/Narrativa/Abrir Editor %#n")]
        public static void OpenWindow()
        {
            var w = GetWindow<NarrativeGraphWindow>();
            w.titleContent = new GUIContent("Guion narrativo");
            w.minSize = new Vector2(900, 500);
            w.Show();
        }

        public static void OpenGraph(NarrativeGraph graph)
        {
            OpenWindow();
            GetWindow<NarrativeGraphWindow>().LoadGraph(graph);
        }

        [UnityEditor.Callbacks.OnOpenAsset]
        static bool OnOpenAsset(int instanceId, int line)
        {
            var g = EditorUtility.EntityIdToObject(instanceId) as NarrativeGraph;
            if (g == null) return false;
            OpenGraph(g);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        // Construcción de la UI
        // ─────────────────────────────────────────────────────────────────

        void OnEnable()
        {
            rootVisualElement.Clear();
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);
            if (sheet != null) rootVisualElement.styleSheets.Add(sheet);
            rootVisualElement.AddToClassList("narrative-root");

            BuildToolbar();

            var split = new TwoPaneSplitView(0, 210, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("narrative-split");
            rootVisualElement.Add(split);

            _palette = BuildPalette();
            split.Add(_palette);

            var right = new TwoPaneSplitView(1, 360, TwoPaneSplitViewOrientation.Horizontal);
            split.Add(right);

            _view = new NarrativeGraphView { name = "NarrativeGraphView" };
            _view.OnEdgeLinked = OnEdgeLinked;
            _view.OnEdgeUnlinked = OnEdgeUnlinked;
            _view.OnNodeDeleted = OnNodeDeleted;
            _view.OnNodesMoved = OnNodesMoved;
            _view.OnRequestCreateAt = pos => ShowCreateMenu(pos);
            _view.AddManipulator(new ContextualMenuManipulator(BuildCanvasContextMenu));
            if (sheet != null) _view.styleSheets.Add(sheet);
            right.Add(_view);

            _inspector = new NarrativeInspectorPanel();
            _inspector.NodeFieldChanged += OnNodeFieldChanged;
            _inspector.NavigateToNode += FocusNode;
            right.Add(_inspector);

            NodeView.StartChanged += OnStartChanged;
            NarrativeProjectIndex.Rebuilt += OnIndexRebuilt;
            Undo.undoRedoPerformed += OnUndoRedo;

            rootVisualElement.schedule.Execute(PollSelection).Every(150);

            // Restaurar último grafo abierto
            var lastPath = EditorPrefs.GetString(PrefLastGraph, "");
            if (!string.IsNullOrEmpty(lastPath))
            {
                var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(lastPath);
                if (g != null) LoadGraph(g);
            }
            RebuildGraphMenu();
        }

        void OnDisable()
        {
            NodeView.StartChanged -= OnStartChanged;
            NarrativeProjectIndex.Rebuilt -= OnIndexRebuilt;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("narrative-toolbar");

            _graphMenu = new ToolbarMenu { text = "Grafo ▾" };
            _graphMenu.AddToClassList("narrative-toolbar__graph-menu");
            toolbar.Add(_graphMenu);

            var newBtn = new ToolbarButton(CreateNewGraph) { text = "＋ Nuevo grafo", tooltip = "Crea un NarrativeGraph nuevo en Assets/NarrativeGraph" };
            toolbar.Add(newBtn);

            toolbar.Add(Separator());

            _chapterMenu = new ToolbarMenu { text = "Cap: Todos" };
            _chapterMenu.AddToClassList("narrative-toolbar__chapter-menu");
            toolbar.Add(_chapterMenu);

            _hideOthersToggle = new ToolbarToggle { text = "Ocultar otros", tooltip = "Con un capítulo elegido: ocultar (en vez de atenuar) los nodos de otros capítulos." };
            _hideOthersToggle.RegisterValueChangedCallback(_ => ApplyChapterFilter());
            toolbar.Add(_hideOthersToggle);

            _countLabel = new Label();
            _countLabel.AddToClassList("narrative-toolbar__count");
            toolbar.Add(_countLabel);

            toolbar.Add(Separator());

            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("narrative-toolbar__search");
            _searchField.tooltip = "Buscar por etiqueta, tipo, quest, diálogo, señal o texto. Enter = siguiente, Shift+Enter = anterior.";
            _searchField.RegisterValueChangedCallback(evt => UpdateSearchMatches(evt.newValue));
            _searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                if (_searchMatches.Count == 0) return;
                GoToSearchMatch(_searchMatchIndex + (evt.shiftKey ? -1 : 1));
                evt.StopPropagation();
            });
            toolbar.Add(_searchField);
            _searchCountLabel = new Label();
            _searchCountLabel.AddToClassList("narrative-toolbar__search-count");
            toolbar.Add(_searchCountLabel);

            toolbar.Add(Separator());

            toolbar.Add(new ToolbarButton(ValidateGraph) { text = "✓ Validar", tooltip = "Comprueba claves, conexiones y nodos huérfanos" });
            toolbar.Add(new ToolbarButton(AutoLayout) { text = "⇶ Ordenar", tooltip = "Recoloca los nodos por niveles desde el inicio, minimizando cruces de líneas (solo los seleccionados si hay selección)" });
            toolbar.Add(new ToolbarButton(() => _view?.FrameAll()) { text = "⤢ Encuadrar" });
            toolbar.Add(new ToolbarButton(() => { NarrativeProjectIndex.ForceRebuild(); RefreshAllViews(); }) { text = "↻ Índice", tooltip = "Reconstruye el índice de quests/diálogos/señales del proyecto" });

            _statusLabel = new Label();
            _statusLabel.AddToClassList("narrative-toolbar__status");
            toolbar.Add(_statusLabel);

            var saveBtn = new ToolbarButton(Save) { text = "Guardar" };
            saveBtn.AddToClassList("narrative-toolbar__save-btn");
            toolbar.Add(saveBtn);

            rootVisualElement.Add(toolbar);
        }

        static VisualElement Separator()
        {
            var sep = new VisualElement();
            sep.AddToClassList("narrative-toolbar__separator");
            return sep;
        }

        VisualElement BuildPalette()
        {
            var root = new VisualElement();
            root.AddToClassList("narrative-palette");

            var title = new Label("Nodos");
            title.AddToClassList("narrative-palette__title");
            root.Add(title);

            var search = new ToolbarSearchField();
            search.AddToClassList("narrative-palette__search");
            root.Add(search);

            var scroll = new ScrollView();
            scroll.AddToClassList("narrative-palette__scroll");
            root.Add(scroll);

            void Rebuild(string filter)
            {
                scroll.Clear();
                filter = filter?.Trim() ?? "";
                foreach (var group in NarrativeNodeCatalog.All.GroupBy(e => e.category).OrderBy(g => NarrativeNodeCatalog.CategoryOrder(g.Key)))
                {
                    var entries = group.Where(e => filter.Length == 0 || e.title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || e.type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    if (entries.Count == 0) continue;
                    var fold = new Foldout { text = group.Key, value = true };
                    fold.AddToClassList("narrative-palette__group");
                    var stripe = fold.Q<Toggle>();
                    if (stripe != null) stripe.style.borderLeftColor = new StyleColor(NarrativeNodeCatalog.ColorOf(group.Key));
                    foreach (var e in entries)
                    {
                        var captured = e;
                        var b = new Button(() => CreateNode(captured.type)) { text = e.title, tooltip = string.IsNullOrEmpty(e.description) ? e.type.Name : e.description + "\n(" + e.type.Name + ")" };
                        b.AddToClassList("narrative-palette__item");
                        b.style.borderLeftColor = new StyleColor(NarrativeNodeCatalog.ColorOf(group.Key));
                        fold.Add(b);
                    }
                    scroll.Add(fold);
                }
            }
            search.RegisterValueChangedCallback(evt => Rebuild(evt.newValue));
            Rebuild("");

            var help = new Label("Clic = añadir en el centro.\nDoble clic en el lienzo = menú.");
            help.AddToClassList("narrative-palette__help");
            root.Add(help);
            return root;
        }

        void BuildCanvasContextMenu(ContextualMenuPopulateEvent e)
        {
            if (e.target is VisualElement ve && (ve is NodeView || ve.GetFirstAncestorOfType<NodeView>() != null || ve is Edge)) return;
            Vector2 local = _view.contentViewContainer.WorldToLocal(e.mousePosition);
            foreach (var entry in NarrativeNodeCatalog.All)
            {
                var captured = entry;
                e.menu.AppendAction($"Añadir/{entry.category}/{entry.title}", _ => CreateNode(captured.type, local));
            }
            e.menu.AppendSeparator();
            e.menu.AppendAction("Encuadrar todo", _ => _view.FrameAll());
        }

        void ShowCreateMenu(Vector2 localPos)
        {
            if (_graph == null) return;
            var menu = new GenericMenu();
            foreach (var entry in NarrativeNodeCatalog.All)
            {
                var captured = entry;
                menu.AddItem(new GUIContent($"{entry.category}/{entry.title}"), false, () => CreateNode(captured.type, localPos));
            }
            menu.ShowAsContext();
        }

        // ─────────────────────────────────────────────────────────────────
        // Grafos
        // ─────────────────────────────────────────────────────────────────

        void RebuildGraphMenu()
        {
            if (_graphMenu == null) return;
            _graphMenu.menu.ClearItems();
            var graphs = AssetDatabase.FindAssets("t:NarrativeGraph")
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Select(p => (path: p, asset: AssetDatabase.LoadAssetAtPath<NarrativeGraph>(p)))
                .Where(t => t.asset != null)
                .OrderBy(t => t.asset.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var (path, asset) in graphs)
            {
                var captured = asset;
                _graphMenu.menu.AppendAction($"{asset.name}   ({asset.nodes.Count(n => n != null)} nodos)", _ => LoadGraph(captured),
                    _ => captured == _graph ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            _graphMenu.text = _graph != null ? $"Grafo: {_graph.name} ▾" : "Grafo ▾";
        }

        void CreateNewGraph()
        {
            var path = EditorUtility.SaveFilePanelInProject("Nuevo grafo narrativo", "Cap1", "asset", "Elige nombre y carpeta", "Assets/NarrativeGraph");
            if (string.IsNullOrEmpty(path)) return;
            var g = ScriptableObject.CreateInstance<NarrativeGraph>();
            var start = new StartNode { displayTitle = "Inicio", position = Vector2.zero };
            g.nodes.Add(start);
            g.startNodeGuid = start.guid;
            AssetDatabase.CreateAsset(g, path);
            AssetDatabase.SaveAssets();
            NarrativeProjectIndex.Invalidate();
            LoadGraph(g);
        }

        public void LoadGraph(NarrativeGraph g)
        {
            _graph = g;
            _so = g != null ? new SerializedObject(g) : null;
            _suppressGraphCallbacks = true;
            _view.DeleteElements(_view.graphElements.ToList());
            _nodeViews.Clear();
            _lastSelected = null;

            if (_graph == null)
            {
                _suppressGraphCallbacks = false;
                _inspector.SetGraph(null, null);
                RebuildChapterMenu();
                RebuildGraphMenu();
                return;
            }

            EditorPrefs.SetString(PrefLastGraph, AssetDatabase.GetAssetPath(_graph));
            _graph.nodes.RemoveAll(n => n == null);
            if (string.IsNullOrEmpty(_graph.startNodeGuid) && _graph.nodes.Count > 0)
                _graph.startNodeGuid = _graph.nodes[0].guid;

            foreach (var model in _graph.nodes) DrawNode(model);
            foreach (var model in _graph.nodes) DrawEdgesFrom(model);

            _view.FrameAll();
            _suppressGraphCallbacks = false;
            _inspector.SetGraph(_graph, _so);
            RebuildChapterMenu();
            RebuildGraphMenu();
            ApplyChapterFilter();
            RefreshForkBadges();
            if (_searchField != null) _searchField.SetValueWithoutNotify("");
            UpdateSearchMatches("");
            SetStatus($"{_graph.name} cargado");
        }

        void DrawNode(NarrativeNode model)
        {
            var view = new NodeView(model, _graph, _so);
            view.LabelChanged += v => { RefreshSearchIfNeeded(); };
            _view.AddElement(view);
            _nodeViews[model.guid] = view;
        }

        void DrawEdgesFrom(NarrativeNode model)
        {
            if (model.outputs == null) return;
            if (!_nodeViews.TryGetValue(model.guid, out var from)) return;
            var ports = model.GetOutputPorts();
            for (int i = 0; i < model.outputs.Count; i++)
            {
                var outGuid = model.outputs[i];
                if (string.IsNullOrEmpty(outGuid)) continue;
                if (!_nodeViews.TryGetValue(outGuid, out var to) || to.Input == null) continue;
                var port = ports != null ? from.GetOutputPort(i) : from.GetOutputPort(0);
                if (port == null) continue;
                var edge = port.ConnectTo(to.Input);
                edge.AddToClassList("narrative-edge");
                _view.AddElement(edge);
            }
        }

        void CreateNode(Type t, Vector2? pos = null)
        {
            if (_graph == null)
            {
                SetStatus("Abre o crea un grafo primero");
                return;
            }
            var node = (NarrativeNode)Activator.CreateInstance(t);
            node.position = pos ?? GetViewCenter();
            if (_activeChapter != AllChaptersLabel && _activeChapter != NoChapterKey)
                node.chapter = _activeChapter;
            var ports = node.GetOutputPorts();
            if (ports != null)
                for (int i = 0; i < ports.Length; i++) node.outputs.Add("");

            Undo.RecordObject(_graph, "Añadir nodo");
            _graph.nodes.Add(node);
            if (string.IsNullOrEmpty(_graph.startNodeGuid) || (node is StartNode && !_graph.nodes.Any(n => n is StartNode && n != node)))
                _graph.startNodeGuid = node.guid;
            EditorUtility.SetDirty(_graph);
            _so.Update();

            DrawNode(node);
            RebuildChapterMenu();
            ApplyChapterFilter();
            var nv = _nodeViews[node.guid];
            _view.ClearSelection();
            _view.AddToSelection(nv);
        }

        Vector2 GetViewCenter()
        {
            if (_view == null) return Vector2.zero;
            var bound = _view.worldBound;
            if (float.IsNaN(bound.center.x) || bound.width <= 0f) return Vector2.zero;
            var c = _view.contentViewContainer.WorldToLocal(bound.center);
            // pequeño desplazamiento para no apilar exactamente encima de otro
            int n = _graph != null ? _graph.nodes.Count : 0;
            return c + new Vector2((n % 5) * 24f, (n % 5) * 24f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Aristas
        // ─────────────────────────────────────────────────────────────────

        void OnEdgeLinked(Edge e)
        {
            if (_suppressGraphCallbacks || _graph == null) return;
            if (e.output?.node is not NodeView from || e.input?.node is not NodeView to) return;
            e.AddToClassList("narrative-edge");

            Undo.RecordObject(_graph, "Conectar nodos");
            if (from.Model.outputs == null) from.Model.outputs = new List<string>();
            if (from.HasNamedOutputs)
            {
                int idx = from.PortIndex(e.output);
                int count = from.Model.GetOutputPorts().Length;
                while (from.Model.outputs.Count < count) from.Model.outputs.Add("");
                from.Model.outputs[idx] = to.Model.guid;
            }
            else if (!from.Model.outputs.Contains(to.Model.guid))
            {
                from.Model.outputs.Add(to.Model.guid);
            }
            EditorUtility.SetDirty(_graph);
            from.RefreshWarnings();
            RefreshForkBadge(from);
            if (_lastSelected == from || _lastSelected == to) _inspector.Show(_lastSelected);
        }

        void OnEdgeUnlinked(Edge e)
        {
            if (_suppressGraphCallbacks || _graph == null) return;
            if (e.output?.node is not NodeView from || e.input?.node is not NodeView to) return;
            Undo.RecordObject(_graph, "Desconectar nodos");
            if (from.Model.outputs != null)
            {
                if (from.HasNamedOutputs)
                {
                    int idx = from.PortIndex(e.output);
                    if (idx < from.Model.outputs.Count && from.Model.outputs[idx] == to.Model.guid)
                        from.Model.outputs[idx] = "";
                }
                else
                {
                    from.Model.outputs.Remove(to.Model.guid);
                }
            }
            EditorUtility.SetDirty(_graph);
            from.RefreshWarnings();
            RefreshForkBadge(from);
            if (_lastSelected == from || _lastSelected == to) _inspector.Show(_lastSelected);
        }

        void OnNodeDeleted(NodeView nv)
        {
            if (_suppressGraphCallbacks || _graph == null || nv?.Model == null) return;
            Undo.RecordObject(_graph, "Borrar nodo");
            foreach (var n in _graph.nodes)
            {
                if (n?.outputs == null) continue;
                if (n.HasNamedOutputs)
                {
                    for (int i = 0; i < n.outputs.Count; i++)
                        if (n.outputs[i] == nv.Model.guid) n.outputs[i] = "";
                }
                else n.outputs.RemoveAll(g => g == nv.Model.guid);
            }
            _graph.nodes.Remove(nv.Model);
            if (_graph.startNodeGuid == nv.Model.guid) _graph.startNodeGuid = null;
            _nodeViews.Remove(nv.Model.guid);
            EditorUtility.SetDirty(_graph);
            _so.Update();
            if (_lastSelected == nv) { _lastSelected = null; _inspector.Show(null); }
            RebuildChapterMenu();
            RefreshForkBadges();
        }

        void OnNodesMoved(List<NodeView> moved)
        {
            if (_graph == null) return;
            Undo.RecordObject(_graph, "Mover nodos");
            foreach (var nv in moved) nv.Model.position = nv.GetPosition().position;
            EditorUtility.SetDirty(_graph);
        }

        void RefreshForkBadges()
        {
            foreach (var nv in _nodeViews.Values) RefreshForkBadge(nv);
        }

        void RefreshForkBadge(NodeView nv)
        {
            if (nv?.Model == null) return;
            int count = nv.Model.outputs?.Count(g => !string.IsNullOrEmpty(g)) ?? 0;
            nv.SetForkCount(count);
        }

        // ─────────────────────────────────────────────────────────────────
        // Selección e inspector
        // ─────────────────────────────────────────────────────────────────

        void PollSelection()
        {
            if (_view == null) return;
            NodeView sel = null;
            int count = 0;
            foreach (var s in _view.selection)
                if (s is NodeView nv) { sel ??= nv; count++; }
            if (count != 1) sel = null;
            if (sel != _lastSelected)
            {
                _lastSelected = sel;
                _inspector.Show(sel);
            }
        }

        void OnNodeFieldChanged(NodeView view)
        {
            if (view == null) return;
            view.RefreshAll();
            RebuildChapterMenu();
            ApplyChapterFilter();
            RefreshSearchIfNeeded();
        }

        void FocusNode(string guid)
        {
            if (string.IsNullOrEmpty(guid) || !_nodeViews.TryGetValue(guid, out var nv)) return;
            _view.ClearSelection();
            _view.AddToSelection(nv);
            _view.FrameSelection();
        }

        void OnStartChanged()
        {
            foreach (var nv in _nodeViews.Values) nv.RefreshStartBadge();
        }

        void OnIndexRebuilt()
        {
            // El índice cambió (assets nuevos/borrados): refrescar resúmenes y avisos.
            rootVisualElement.schedule.Execute(RefreshAllViews);
        }

        void RefreshAllViews()
        {
            foreach (var nv in _nodeViews.Values) nv.RefreshAll();
            if (_lastSelected != null) _inspector.Show(_lastSelected);
            SetStatus("Índice actualizado");
        }

        void OnUndoRedo()
        {
            if (_graph == null) return;
            // Reconstrucción completa: es la forma segura de reflejar cualquier cambio deshecho.
            var keepSelection = _lastSelected?.Model?.guid;
            LoadGraph(_graph);
            if (keepSelection != null) FocusNode(keepSelection);
        }

        // ─────────────────────────────────────────────────────────────────
        // Capítulos
        // ─────────────────────────────────────────────────────────────────

        List<string> CollectChapters()
        {
            var chapters = new List<string>();
            if (_graph == null) return chapters;
            foreach (var node in _graph.nodes)
            {
                if (node == null) continue;
                var ch = node.chapter;
                if (!string.IsNullOrWhiteSpace(ch) && !chapters.Contains(ch)) chapters.Add(ch);
            }
            chapters.Sort(StringComparer.OrdinalIgnoreCase);
            return chapters;
        }

        void RebuildChapterMenu()
        {
            if (_chapterMenu == null) return;
            _chapterMenu.menu.ClearItems();
            _chapterMenu.menu.AppendAction(AllChaptersLabel, _ => SetChapterFilter(AllChaptersLabel),
                _ => _activeChapter == AllChaptersLabel ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            var chapters = CollectChapters();
            if (chapters.Count > 0) _chapterMenu.menu.AppendSeparator();
            foreach (var ch in chapters)
            {
                var captured = ch;
                _chapterMenu.menu.AppendAction(captured, _ => SetChapterFilter(captured),
                    _ => _activeChapter == captured ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            int noChapter = _graph != null ? _graph.nodes.Count(n => n != null && string.IsNullOrWhiteSpace(n.chapter)) : 0;
            if (noChapter > 0 && chapters.Count > 0)
            {
                _chapterMenu.menu.AppendSeparator();
                _chapterMenu.menu.AppendAction($"Sin capítulo ({noChapter})", _ => SetChapterFilter(NoChapterKey),
                    _ => _activeChapter == NoChapterKey ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            UpdateCountLabel();
        }

        void SetChapterFilter(string chapter)
        {
            _activeChapter = chapter;
            _chapterMenu.text = chapter == AllChaptersLabel ? "Cap: Todos" : chapter == NoChapterKey ? "Cap: Sin capítulo" : $"Cap: {chapter}";
            ApplyChapterFilter();
            UpdateCountLabel();
        }

        bool NodeMatchesChapter(NarrativeNode node)
        {
            if (_activeChapter == AllChaptersLabel) return true;
            if (_activeChapter == NoChapterKey) return string.IsNullOrWhiteSpace(node.chapter);
            return string.Equals(node.chapter, _activeChapter, StringComparison.OrdinalIgnoreCase);
        }

        void UpdateCountLabel()
        {
            if (_countLabel == null || _graph == null) { if (_countLabel != null) _countLabel.text = ""; return; }
            int total = _graph.nodes.Count(n => n != null);
            _countLabel.text = _activeChapter == AllChaptersLabel ? $"{total} nodos" : $"{_graph.nodes.Count(n => n != null && NodeMatchesChapter(n))}/{total} nodos";
        }

        void ApplyChapterFilter()
        {
            bool showAll = _activeChapter == AllChaptersLabel;
            bool hide = _hideOthersToggle != null && _hideOthersToggle.value;
            foreach (var nv in _nodeViews.Values)
            {
                bool match = showAll || NodeMatchesChapter(nv.Model);
                nv.style.display = (!match && hide) ? DisplayStyle.None : DisplayStyle.Flex;
                nv.EnableInClassList("narrative-node--dimmed", !match && !hide);
            }
            _view.edges.ForEach(edge =>
            {
                if (edge?.output?.node is NodeView from && edge?.input?.node is NodeView to)
                {
                    bool ok = showAll || (NodeMatchesChapter(from.Model) && NodeMatchesChapter(to.Model));
                    edge.style.display = (!ok && hide) ? DisplayStyle.None : DisplayStyle.Flex;
                    edge.EnableInClassList("narrative-edge--dimmed", !ok && !hide);
                }
            });
        }

        // ─────────────────────────────────────────────────────────────────
        // Búsqueda
        // ─────────────────────────────────────────────────────────────────

        static bool NodeMatchesSearch(NarrativeNode node, string q)
        {
            bool Has(string s) => !string.IsNullOrEmpty(s) && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            if (Has(node.GetType().Name) || Has(NarrativeNodeCatalog.TitleOf(node.GetType())) || Has(node.displayTitle) || Has(node.chapter)) return true;
            switch (node)
            {
                case PlayDialogueNode pd:
                    if (pd.dialogue != null)
                    {
                        if (Has(pd.dialogue.name)) return true;
                        if (pd.dialogue.lines != null)
                            foreach (var l in pd.dialogue.lines)
                                if (Has(l.textId) || Has(NarrativeProjectIndex.LineText(l))) return true;
                    }
                    return Has(pd.npcId);
                case StartQuestNode s: return Has(s.questId) || Has(NarrativeProjectIndex.FindQuest(s.questId)?.displayName);
                case WaitQuestCompleteNode w: return Has(w.questId) || Has(NarrativeProjectIndex.FindQuest(w.questId)?.displayName);
                case CompleteQuestStepsNode c: return Has(c.questId) || c.stepConditionIds.Any(Has);
                case BranchQuestStateNode b: return Has(b.questId);
                case WaitCustomEventNode we: return Has(we.eventKey);
                case RaiseCustomEventNode re: return Has(re.eventKey);
                case PlayCinematicNode pc: return Has(pc.cinematicName) || Has(pc.signalIn) || Has(pc.signalDone);
                case WaitNpcInteractionNode wn: return Has(wn.npcId);
                case SetFlagNode sf: return Has(sf.flagKey);
                case BranchFlagNode bf: return Has(bf.flagKey);
                case CheckpointNode cp: return Has(cp.checkpointId);
                case ShowSpeechBubbleNode sb: return Has(sb.textId) || Has(sb.text) || Has(NarrativeProjectIndex.Loc(sb.textId));
                case GraphNoteNode gn: return Has(gn.note);
            }
            return false;
        }

        string _lastQuery = "";

        void UpdateSearchMatches(string query)
        {
            _lastQuery = query ?? "";
            _searchMatches.Clear();
            _searchMatchIndex = -1;
            foreach (var kv in _nodeViews) kv.Value.RemoveFromClassList("narrative-node--search-match");

            query = query?.Trim();
            if (string.IsNullOrEmpty(query) || _graph == null) { UpdateSearchCountLabel(); return; }

            foreach (var node in _graph.nodes)
                if (node != null && NodeMatchesSearch(node, query)) _searchMatches.Add(node.guid);

            foreach (var g in _searchMatches)
                if (_nodeViews.TryGetValue(g, out var nv)) nv.AddToClassList("narrative-node--search-match");

            if (_searchMatches.Count > 0) GoToSearchMatch(0); else UpdateSearchCountLabel();
        }

        void RefreshSearchIfNeeded()
        {
            if (!string.IsNullOrEmpty(_lastQuery)) UpdateSearchMatches(_lastQuery);
        }

        void GoToSearchMatch(int index)
        {
            if (_searchMatches.Count == 0) return;
            _searchMatchIndex = ((index % _searchMatches.Count) + _searchMatches.Count) % _searchMatches.Count;
            FocusNode(_searchMatches[_searchMatchIndex]);
            UpdateSearchCountLabel();
        }

        void UpdateSearchCountLabel()
        {
            if (_searchCountLabel == null) return;
            _searchCountLabel.text = _searchMatches.Count == 0 ? (string.IsNullOrEmpty(_lastQuery) ? "" : "0") : $"{_searchMatchIndex + 1}/{_searchMatches.Count}";
        }

        // ─────────────────────────────────────────────────────────────────
        // Validar / ordenar / guardar
        // ─────────────────────────────────────────────────────────────────

        void ValidateGraph()
        {
            if (_graph == null) return;
            int issues = 0;
            string firstGuid = null;
            foreach (var nv in _nodeViews.Values)
            {
                nv.RefreshWarnings();
                var list = NodeSummary.Validate(nv.Model, _graph);
                if (list.Count > 0) { issues += list.Count; firstGuid ??= nv.Model.guid; }
            }
            var result = NarrativeGraphValidator.ValidateGraph(_graph);
            result.LogResults(_graph.name);
            int structural = result.Errors.Count + result.Warnings.Count;
            SetStatus(issues + structural == 0 ? "✓ Sin problemas" : $"⚠ {issues} avisos en nodos · {result.Errors.Count} errores / {result.Warnings.Count} avisos de estructura (ver consola)");
            if (firstGuid != null) FocusNode(firstGuid);
        }

        /// <summary>
        /// Recoloca los nodos por niveles (profundidad desde el inicio) — útil cuando un grafo
        /// importado o crecido a mano se vuelve ilegible. Dentro de cada nivel, el orden vertical
        /// se decide por el método del baricentro (varias pasadas adelante/atrás mirando la Y media
        /// de los nodos con los que conecta en el nivel vecino), para minimizar cruces de líneas
        /// entre columnas en vez de solo apilar por la Y que tuvieran antes. Solo mueve los
        /// seleccionados si hay selección, pero el cálculo de baricentro tiene en cuenta también
        /// a los vecinos no seleccionados, para que el orden siga siendo coherente con el resto.
        /// </summary>
        void AutoLayout()
        {
            if (_graph == null) return;
            var selected = _view.selection.OfType<NodeView>().Select(v => v.Model).ToHashSet();
            bool onlySelected = selected.Count > 1;

            var byGuidForCycles = _graph.nodes.Where(n => n != null).ToDictionary(n => n.guid);

            // ─── Detectar aristas de vuelta (bucles reales, p. ej. un reintento tipo
            // "AWAKEN_FAILED" que vuelve a "AWAKEN_START") con DFS blanco/gris/negro ──────
            // Antes, el cálculo de profundidad de más abajo era un BFS de "camino más largo"
            // con un contador de visitas tope en 50 para cortar ciclos. Con un bucle real en
            // el grafo, eso dejaba dar hasta 50 vueltas completas antes de cortar, e inflaba
            // en cientos de niveles la profundidad de todo lo que viene después del bucle —
            // cada nivel son 380px de separación en X, así que el resto del grafo terminaba
            // clavado a decenas de miles de píxeles a la derecha (visto en Cap1.asset: el
            // bucle de AWAKEN_START/AWAKEN_FAILED empujaba "Fin Cap. 1" a x=46360). Al
            // identificar de antemano las aristas que cierran un ciclo y excluirlas del
            // cálculo de profundidad (siguen dibujándose igual en el lienzo, solo no cuentan
            // para el layout), el grafo restante es acíclico y no hace falta ningún tope.
            var backEdges = new HashSet<(string from, string to)>();
            {
                var state = new Dictionary<string, int>(); // 0 implícito = blanco, 1 = gris (en la pila), 2 = negro
                void Dfs(string guid)
                {
                    state[guid] = 1;
                    if (byGuidForCycles.TryGetValue(guid, out var node) && node.outputs != null)
                    {
                        foreach (var g in node.outputs)
                        {
                            if (string.IsNullOrEmpty(g) || !byGuidForCycles.ContainsKey(g)) continue;
                            state.TryGetValue(g, out var s);
                            if (s == 1) { backEdges.Add((guid, g)); continue; } // arista de vuelta: cierra un ciclo
                            if (s == 0) Dfs(g);
                        }
                    }
                    state[guid] = 2;
                }
                foreach (var n in byGuidForCycles.Values)
                    if (!state.ContainsKey(n.guid)) Dfs(n.guid);
            }

            var depth = new Dictionary<string, int>();
            var queue = new Queue<NarrativeNode>();
            var start = _graph.FindNode(_graph.startNodeGuid);
            if (start != null) { depth[start.guid] = 0; queue.Enqueue(start); }
            // nodos sin entradas también son raíces
            var hasIncoming = new HashSet<string>(_graph.nodes.Where(n => n?.outputs != null).SelectMany(n => n.outputs).Where(g => !string.IsNullOrEmpty(g)));
            foreach (var n in _graph.nodes)
                if (n != null && !hasIncoming.Contains(n.guid) && !depth.ContainsKey(n.guid)) { depth[n.guid] = 0; queue.Enqueue(n); }

            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                if (n.outputs == null) continue;
                foreach (var g in n.outputs)
                {
                    if (string.IsNullOrEmpty(g)) continue;
                    if (backEdges.Contains((n.guid, g))) continue; // no propagar profundidad por un bucle
                    var next = _graph.FindNode(g);
                    if (next == null) continue;
                    int d = depth[n.guid] + 1;
                    if (depth.TryGetValue(next.guid, out var cur) && cur >= d) continue;
                    depth[next.guid] = d;
                    queue.Enqueue(next);
                }
            }
            foreach (var n in _graph.nodes) if (n != null && !depth.ContainsKey(n.guid)) depth[n.guid] = 0;

            // ─── Orden por baricentro dentro de cada nivel (minimizar cruces) ──────────
            // Método estándar de dibujo de grafos por capas (Sugiyama): en vez de apilar cada
            // columna por la Y que tuviera antes, varias pasadas adelante/atrás reordenan cada
            // nivel según la Y media de los nodos con los que conecta en el nivel vecino.
            var byGuid = _graph.nodes.Where(n => n != null).ToDictionary(n => n.guid);
            var preds = new Dictionary<string, List<string>>();
            foreach (var n in _graph.nodes)
            {
                if (n?.outputs == null) continue;
                foreach (var g in n.outputs)
                {
                    if (string.IsNullOrEmpty(g) || !byGuid.ContainsKey(g)) continue;
                    if (!preds.TryGetValue(g, out var list)) preds[g] = list = new List<string>();
                    list.Add(n.guid);
                }
            }

            float HeightOf(string guid) => _nodeViews.TryGetValue(guid, out var nv) ? Mathf.Max(nv.GetPosition().height, 120f) : 120f;

            var levelKeys = byGuid.Values.Select(n => depth[n.guid]).Distinct().OrderBy(k => k).ToList();
            var order = new Dictionary<int, List<string>>();
            foreach (var k in levelKeys)
                order[k] = byGuid.Values.Where(n => depth[n.guid] == k).OrderBy(n => n.position.y).Select(n => n.guid).ToList();

            var yPos = byGuid.Values.ToDictionary(n => n.guid, n => n.position.y);

            void Restack(int level)
            {
                float y = 0f;
                foreach (var guid in order[level]) { yPos[guid] = y; y += HeightOf(guid) + 40f; }
            }

            float Barycenter(string guid, IEnumerable<string> neighbors)
            {
                float sum = 0f; int count = 0;
                foreach (var g in neighbors) { sum += yPos[g]; count++; }
                return count > 0 ? sum / count : yPos[guid];
            }

            const int passes = 4;
            for (int pass = 0; pass < passes; pass++)
            {
                // adelante: cada nivel se ordena mirando a sus predecesores (nivel anterior)
                foreach (var k in levelKeys)
                {
                    order[k] = order[k]
                        .OrderBy(guid => Barycenter(guid, (preds.TryGetValue(guid, out var p) ? p : Enumerable.Empty<string>()).Where(g => depth[g] < k)))
                        .ToList();
                    Restack(k);
                }
                // atrás: cada nivel se ordena mirando a sus sucesores (nivel siguiente)
                for (int i = levelKeys.Count - 1; i >= 0; i--)
                {
                    int k = levelKeys[i];
                    order[k] = order[k]
                        .OrderBy(guid => Barycenter(guid, (byGuid[guid].outputs ?? Enumerable.Empty<string>()).Where(g => byGuid.ContainsKey(g) && depth[g] > k)))
                        .ToList();
                    Restack(k);
                }
            }

            // ─── Aplicar posiciones finales (solo a los nodos a mover) ─────────────────
            Undo.RecordObject(_graph, "Ordenar nodos");
            const float dx = 380f, dy = 40f;
            var toMove = new HashSet<string>(byGuid.Values.Where(n => !onlySelected || selected.Contains(n)).Select(n => n.guid));
            foreach (var k in levelKeys)
            {
                float y = 0f;
                foreach (var guid in order[k].Where(toMove.Contains))
                {
                    if (!_nodeViews.TryGetValue(guid, out var nv)) continue;
                    var rect = nv.GetPosition();
                    float h = Mathf.Max(rect.height, 120f);
                    var pos = new Vector2(k * dx, y);
                    nv.SetPosition(new Rect(pos, rect.size));
                    byGuid[guid].position = pos;
                    y += h + dy;
                }
            }
            EditorUtility.SetDirty(_graph);
            _view.FrameAll();
            SetStatus("Nodos reordenados (cruces minimizados)");
        }

        void Save()
        {
            if (_graph == null) return;
            foreach (var kv in _nodeViews) kv.Value.Model.position = kv.Value.GetPosition().position;
            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            NarrativeProjectIndex.Invalidate();
            SetStatus($"Guardado {DateTime.Now:HH:mm:ss}");
        }

        void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
        }
    }
}
