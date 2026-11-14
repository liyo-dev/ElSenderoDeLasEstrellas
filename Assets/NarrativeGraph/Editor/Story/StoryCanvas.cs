using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// The story canvas: pan, zoom, select and drag steps, connect them from any side, add a step
    /// by dropping a connection on empty space, insert one on a connection, sections as headings.
    public sealed class StoryCanvas : VisualElement
    {
        public event Action<NarrativeNode> SelectionChanged;
        public event Action<NarrativeNode> PlayRequested;
        public event Action AutoLayoutRequested;

        const float MinZoom = 0.15f, MaxZoom = 1.4f;

        readonly VisualElement _content;
        readonly VisualElement _frames;
        readonly StoryEdgeLayer _edges;
        readonly VisualElement _labels;
        readonly VisualElement _cards;
        readonly AddStepPopup _popup;
        readonly Label _zoomLabel;
        readonly Label _empty;
        readonly Dictionary<NarrativeNode, StepCard> _cardOf = new();
        readonly HashSet<NarrativeNode> _selection = new();

        StoryGraphEditing _edit;
        StoryStructure _structure = new();
        List<StoryLayout.Route> _routes = new();
        Vector2 _pan = new(60f, 60f);
        float _zoom = 0.85f;

        enum Mode { None, Pan, Drag, Link, Box }
        Mode _mode;
        Vector2 _pointerStart;
        Vector2 _panStart;
        Dictionary<NarrativeNode, Vector2> _dragStart;
        bool _dragMoved;
        StepCard _linkFrom;
        int _linkPort;
        Vector2 _linkStart;
        StepCard _dropTarget;

        HashSet<string> _pathNodes = new();
        HashSet<string> _pathEdges = new();
        string _currentGuid;
        readonly HashSet<string> _visited = new();
        string _search = string.Empty;

        public StoryStructure Structure => _structure;
        public NarrativeNode Primary => _selection.Count == 1 ? _selection.First() : null;

        public StoryCanvas()
        {
            AddToClassList("story-canvas");
            focusable = true;
            generateVisualContent += DrawGrid;

            _content = new VisualElement { pickingMode = PickingMode.Ignore };
            _content.AddToClassList("story-canvas__content");
            _content.style.transformOrigin = new TransformOrigin(0, 0, 0);
            Add(_content);

            _frames = Layer("story-canvas__frames");
            _edges = new StoryEdgeLayer();
            _content.Add(_edges);
            _labels = Layer("story-canvas__labels");
            _cards = Layer("story-canvas__cards");

            _empty = new Label(StoryText.T("Abre un grafo o crea uno nuevo.", "Open a graph or create a new one."));
            _empty.AddToClassList("story-canvas__empty");
            Add(_empty);

            var zoomBar = new VisualElement();
            zoomBar.AddToClassList("story-canvas__zoom");
            zoomBar.Add(Tool("−", StoryText.T("Alejar", "Zoom out"), () => ZoomAround(LayoutCenter(), _zoom - 0.1f)));
            _zoomLabel = new Label();
            _zoomLabel.AddToClassList("story-canvas__zoom-label");
            zoomBar.Add(_zoomLabel);
            zoomBar.Add(Tool("+", StoryText.T("Acercar", "Zoom in"), () => ZoomAround(LayoutCenter(), _zoom + 0.1f)));
            zoomBar.Add(Tool(StoryText.T("Encuadrar", "Frame"), StoryText.T("Ver todo el capítulo (F)", "Show the whole chapter (F)"), FrameAll));
            Add(zoomBar);

            _popup = new AddStepPopup();
            Add(_popup);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => CancelGesture());
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<GeometryChangedEvent>(_ => TryFramePending());
            this.AddManipulator(new ContextualMenuManipulator(BuildCanvasMenu));

            ApplyTransform();
        }

        VisualElement Layer(string cls)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.AddToClassList("story-canvas__layer");
            v.AddToClassList(cls);
            _content.Add(v);
            return v;
        }

        static Button Tool(string text, string tooltip, Action action)
        {
            var b = new Button(action) { text = text, tooltip = tooltip };
            b.AddToClassList("story-canvas__tool");
            return b;
        }

        // ── Graph ────────────────────────────────────────────────────────────

        public void SetEditing(StoryGraphEditing edit)
        {
            _edit = edit;
            _selection.Clear();
            _visited.Clear();
            _currentGuid = null;
            Rebuild();
            _framePending = true;
            schedule.Execute(TryFramePending).StartingIn(30);
        }

        bool _framePending;

        // The canvas may not have a size yet when a graph is opened; frame once it does.
        void TryFramePending()
        {
            if (!_framePending) return;
            if (HasUsableSize()) { _framePending = false; FrameAll(); }
        }

        bool HasUsableSize()
        {
            var size = contentRect.size;
            return size.x >= 10f && size.y >= 10f && !float.IsNaN(size.x) && !float.IsNaN(size.y);
        }

        public void Rebuild()
        {
            _popup.Close();
            _cards.Clear();
            _cardOf.Clear();
            _frames.Clear();
            var graph = _edit?.Graph;
            _empty.style.display = graph == null ? DisplayStyle.Flex : DisplayStyle.None;
            if (graph == null) { _edges.SetRoutes(null); _labels.Clear(); return; }

            graph.nodes.RemoveAll(n => n == null);
            _structure = StoryStructure.Build(graph);
            StoryFlowCheck.Run(graph, _structure);
            _selection.RemoveWhere(n => !graph.nodes.Contains(n));

            foreach (var node in graph.nodes)
            {
                var card = new StepCard(node);
                card.Refresh(_structure);
                card.LinkStarted += OnLinkStarted;
                card.RegisterCallback<PointerDownEvent>(e => OnCardPointerDown(card, e));
                card.AddManipulator(new ContextualMenuManipulator(e => BuildCardMenu(e, card)));
                _cards.Add(card);
                _cardOf[node] = card;
            }
            RebuildFrames();
            RefreshEdges();
            ApplyHighlights();
        }

        void RebuildFrames()
        {
            _frames.Clear();
            foreach (var pair in StoryLayout.SectionFrames(_structure))
            {
                var header = new VisualElement();
                header.AddToClassList("story-section");
                header.style.left = pair.Value.xMin;
                header.style.top = pair.Value.yMin + 4f;
                header.style.width = Mathf.Max(pair.Value.width, 260f);

                var number = new Label((pair.Key.Index + 1).ToString());
                number.AddToClassList("story-section__number");
                header.Add(number);
                var name = new Label(StoryStructure.DisplayName(pair.Key.Name).ToUpperInvariant());
                name.AddToClassList("story-section__name");
                header.Add(name);
                var count = new Label(pair.Key.Steps.Count.ToString());
                count.AddToClassList("story-section__count");
                header.Add(count);
                var rule = new VisualElement();
                rule.AddToClassList("story-section__rule");
                header.Add(rule);
                var sectionName = pair.Key.Name;
                header.tooltip = StoryText.T("Doble clic para renombrar", "Double-click to rename");
                header.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0 || e.clickCount != 2) return;
                    e.StopPropagation();
                    var at = this.WorldToLocal(header.worldBound.position);
                    AskName(at, sectionName, n =>
                    {
                        if (string.IsNullOrEmpty(sectionName)) _edit.SetSection(pair.Key.Steps.Take(1), n);
                        else _edit.RenameSection(sectionName, n);
                    });
                });
                _frames.Add(header);
            }
        }

        void RefreshEdges()
        {
            var graph = _edit?.Graph;
            _labels.Clear();
            if (graph == null) return;
            _routes = StoryLayout.Routes(graph, _structure);
            _edges.SetRoutes(_routes);
            _edges.SetSelected(Primary);
            _edges.SetPath(_pathEdges);

            foreach (var route in _routes)
            {
                var color = _edges.ColorOf(route);
                if (!string.IsNullOrEmpty(route.Label))
                {
                    var label = new Label(route.Label);
                    label.AddToClassList("story-edge-label");
                    label.style.left = route.LabelAt.x;
                    label.style.top = route.LabelAt.y;
                    label.style.borderLeftColor = label.style.borderRightColor = label.style.borderTopColor = label.style.borderBottomColor = color;
                    _labels.Add(label);
                }
                if (route.Length < 70f || _mode != Mode.None) continue;
                var captured = route;
                var insert = new Button(() => OpenPopupOnRoute(captured)) { text = "+", tooltip = StoryText.T("Añadir un paso aquí", "Insert a step here") };
                insert.AddToClassList("story-edge-insert");
                insert.style.left = route.InsertAt.x;
                insert.style.top = route.InsertAt.y;
                insert.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                _labels.Add(insert);
            }
        }

        // ── Highlights ───────────────────────────────────────────────────────

        public void SetPath(IEnumerable<string> nodeGuids, IEnumerable<KeyValuePair<string, string>> edges)
        {
            _pathNodes = new HashSet<string>(nodeGuids ?? Enumerable.Empty<string>());
            _pathEdges = new HashSet<string>();
            if (edges != null)
                foreach (var e in edges) _pathEdges.Add(e.Key + ">" + e.Value);
            _edges.SetPath(_pathEdges);
            RefreshEdges();
            ApplyHighlights();
        }

        public void SetCurrent(string guid)
        {
            if (guid == _currentGuid) return;
            if (!string.IsNullOrEmpty(_currentGuid)) _visited.Add(_currentGuid);
            _currentGuid = guid;
            ApplyHighlights();
        }

        public void ClearVisited()
        {
            _visited.Clear();
            _currentGuid = null;
            ApplyHighlights();
        }

        public void SetSearch(string text)
        {
            _search = text?.Trim() ?? string.Empty;
            ApplyHighlights();
        }

        public int CountMatches() => _cardOf.Keys.Count(Matches);

        bool Matches(NarrativeNode n)
        {
            if (_search.Length == 0) return true;
            bool Has(string s) => !string.IsNullOrEmpty(s) && s.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
            return Has(n.displayTitle) || Has(NarrativeNodeCatalog.TitleOf(n.GetType())) || Has(StepSummary.For(n, 400)) || Has(_structure.Number(n));
        }

        void ApplyHighlights()
        {
            var start = _edit?.Graph?.startNodeGuid;
            foreach (var pair in _cardOf)
            {
                var n = pair.Key;
                var card = pair.Value;
                card.EnableInClassList("is-selected", _selection.Contains(n));
                card.EnableInClassList("is-on-path", _pathNodes.Contains(n.guid));
                // Con un paso elegido, lo que no es su camino se apaga para que se lea la historia.
                card.EnableInClassList("is-off-path", _pathNodes.Count > 0 && !_pathNodes.Contains(n.guid) && !_selection.Contains(n));
                card.EnableInClassList("is-current", n.guid == _currentGuid);
                card.EnableInClassList("has-breakpoint", StoryBreakpoints.Has(_edit.Graph, n.guid));
                card.EnableInClassList("is-visited", _visited.Contains(n.guid));
                card.EnableInClassList("is-start", n.guid == start);
                card.EnableInClassList("is-dimmed", !Matches(n));
                card.EnableInClassList("is-drop-target", card == _dropTarget);
            }
        }

        // ── Selection ────────────────────────────────────────────────────────

        public void Select(NarrativeNode node, bool center = false)
        {
            _selection.Clear();
            if (node != null) _selection.Add(node);
            ApplyHighlights();
            _edges.SetSelected(Primary);
            RefreshEdges();
            if (center && node != null) CenterOn(node);
            SelectionChanged?.Invoke(Primary);
        }

        void Toggle(NarrativeNode node)
        {
            if (!_selection.Remove(node)) _selection.Add(node);
            ApplyHighlights();
            _edges.SetSelected(Primary);
            SelectionChanged?.Invoke(Primary);
        }

        public void CenterOn(NarrativeNode node)
        {
            if (!HasUsableSize()) return;
            var r = StoryLayout.RectOf(node);
            var size = contentRect.size;
            _pan = size * 0.5f - r.center * _zoom;
            ApplyTransform();
        }

        public void FrameAll()
        {
            var graph = _edit?.Graph;
            if (graph == null || graph.nodes.Count == 0) return;
            var bounds = StoryLayout.RectOf(graph.nodes[0]);
            foreach (var n in graph.nodes)
            {
                var r = StoryLayout.RectOf(n);
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin - StoryLayout.SectionHeader),
                                         Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax));
            }
            if (!HasUsableSize()) { _framePending = true; return; }
            var size = contentRect.size;
            if (bounds.width < 1f || bounds.height < 1f) return;
            _zoom = Mathf.Clamp(Mathf.Min((size.x - 80f) / bounds.width, (size.y - 80f) / bounds.height), MinZoom, 1f);
            _pan = size * 0.5f - bounds.center * _zoom;
            ApplyTransform();
        }

        // ── Transform ────────────────────────────────────────────────────────

        Vector2 ToWorld(Vector2 local) => (local - _pan) / _zoom;
        Vector2 ToLocal(Vector2 world) => world * _zoom + _pan;
        Vector2 LayoutCenter() => contentRect.size * 0.5f;

        void ApplyTransform()
        {
            _content.style.translate = new Translate(_pan.x, _pan.y);
            _content.style.scale = new Scale(new Vector2(_zoom, _zoom));
            _zoomLabel.text = Mathf.RoundToInt(_zoom * 100f) + " %";
            EnableInClassList("story-canvas--mid", _zoom < 0.7f && _zoom >= 0.45f);
            EnableInClassList("story-canvas--far", _zoom < 0.45f);
            MarkDirtyRepaint();
        }

        void ZoomAround(Vector2 local, float zoom)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            var world = ToWorld(local);
            _zoom = zoom;
            _pan = local - world * _zoom;
            ApplyTransform();
        }

        void DrawGrid(MeshGenerationContext ctx)
        {
            var size = contentRect.size;
            float step = 40f * _zoom;
            if (step < 8f) return;
            var p = ctx.painter2D;
            p.strokeColor = new Color(1f, 0.94f, 0.86f, 0.035f);
            p.lineWidth = 1f;
            p.BeginPath();
            for (float x = Mod(_pan.x, step); x < size.x; x += step) { p.MoveTo(new Vector2(x, 0)); p.LineTo(new Vector2(x, size.y)); }
            for (float y = Mod(_pan.y, step); y < size.y; y += step) { p.MoveTo(new Vector2(0, y)); p.LineTo(new Vector2(size.x, y)); }
            p.Stroke();
        }

        static float Mod(float a, float b) => ((a % b) + b) % b;

        // ── Pointer ──────────────────────────────────────────────────────────

        void OnPointerDown(PointerDownEvent e)
        {
            if (_edit?.Graph == null) return;
            Focus();
            var local = this.WorldToLocal(e.position);
            if (_popup.IsOpen) _popup.Close();

            if (e.button == 0 && e.clickCount == 2)
            {
                OpenPopup(local, null, -1, null);
                e.StopPropagation();
                return;
            }
            if (e.button != 0 && e.button != 2) return;
            if (e.button == 0 && (e.shiftKey || e.actionKey))
            {
                // Shift/Ctrl + drag on empty space: select everything inside the box.
                _mode = Mode.Box;
                _pointerStart = local;
                _boxBase = new HashSet<NarrativeNode>(_selection);
                UpdateBox(local);
                this.CapturePointer(e.pointerId);
                return;
            }
            if (e.button == 0 && _selection.Count > 0) Select(null);
            _mode = Mode.Pan;
            _pointerStart = local;
            _panStart = _pan;
            this.CapturePointer(e.pointerId);
        }

        void OnCardPointerDown(StepCard card, PointerDownEvent e)
        {
            if (e.button != 0) return;
            Focus();
            if (_popup.IsOpen) _popup.Close();
            if (e.shiftKey || e.actionKey) Toggle(card.Model);
            else if (!_selection.Contains(card.Model)) Select(card.Model);

            _mode = Mode.Drag;
            _dragMoved = false;
            _pointerStart = this.WorldToLocal(e.position);
            _dragStart = _selection.ToDictionary(n => n, n => n.position);
            this.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnLinkStarted(StepCard card, int port, PointerDownEvent e)
        {
            Focus();
            if (_popup.IsOpen) _popup.Close();
            if (!_selection.Contains(card.Model)) Select(card.Model);
            _mode = Mode.Link;
            _linkFrom = card;
            _linkPort = port;
            _linkStart = ToWorld(this.WorldToLocal(e.position));
            this.CapturePointer(e.pointerId);
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (_mode == Mode.None) return;
            var local = this.WorldToLocal(e.position);
            switch (_mode)
            {
                case Mode.Pan:
                    _pan = _panStart + (local - _pointerStart);
                    ApplyTransform();
                    break;
                case Mode.Drag:
                    var delta = (local - _pointerStart) / _zoom;
                    if (!_dragMoved && delta.sqrMagnitude < 9f) break;
                    _dragMoved = true;
                    foreach (var pair in _dragStart)
                    {
                        pair.Key.position = pair.Value + delta;
                        if (_cardOf.TryGetValue(pair.Key, out var c)) c.SetPosition(pair.Key.position);
                    }
                    RefreshEdges();
                    RebuildFrames();
                    break;
                case Mode.Box:
                    UpdateBox(local);
                    break;
                case Mode.Link:
                    var world = ToWorld(local);
                    var hit = HitCard(world, _linkFrom);
                    if (hit != _dropTarget) { _dropTarget = hit; ApplyHighlights(); }
                    _edges.SetDrag(_linkStart, hit != null ? StoryLayout.RectOf(hit.Model).center : world);
                    break;
            }
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (_mode == Mode.None) return;
            var local = this.WorldToLocal(e.position);
            var mode = _mode;
            _mode = Mode.None;
            if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);

            if (mode == Mode.Box)
            {
                _box.style.display = DisplayStyle.None;
                SelectionChanged?.Invoke(Primary);
            }
            else if (mode == Mode.Drag && _dragMoved) FinishDrag();
            else if (mode == Mode.Drag) RefreshEdges();
            else if (mode == Mode.Link) FinishLink(local);
        }

        /// Copies the selected steps (and the connections between them) a little to the side.
        public void Duplicate()
        {
            if (_edit?.Graph == null || _selection.Count == 0) return;
            var source = StoryTemplate.FromSelection(_selection.ToList());
            var origin = new Vector2(_selection.Min(n => n.position.x), _selection.Min(n => n.position.y));
            var entryOffset = StoryTemplate.Entry(source.steps).position;
            var copies = _edit.AddTemplate(source, origin + entryOffset + new Vector2(40f, 40f), string.Empty);
            UnityEngine.Object.DestroyImmediate(source);
            _selection.Clear();
            foreach (var c in copies) _selection.Add(c);
            ApplyHighlights();
            SelectionChanged?.Invoke(Primary);
        }

        HashSet<NarrativeNode> _boxBase;
        VisualElement _box;

        void UpdateBox(Vector2 local)
        {
            if (_box == null)
            {
                _box = new VisualElement { pickingMode = PickingMode.Ignore };
                _box.AddToClassList("story-canvas__box");
                Add(_box);
            }
            var r = Rect.MinMaxRect(Mathf.Min(_pointerStart.x, local.x), Mathf.Min(_pointerStart.y, local.y),
                                    Mathf.Max(_pointerStart.x, local.x), Mathf.Max(_pointerStart.y, local.y));
            _box.style.display = DisplayStyle.Flex;
            _box.style.left = r.x;
            _box.style.top = r.y;
            _box.style.width = r.width;
            _box.style.height = r.height;

            var a = ToWorld(r.min);
            var b = ToWorld(r.max);
            var world = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            _selection.Clear();
            foreach (var n in _boxBase) _selection.Add(n);
            foreach (var n in _edit.Graph.nodes)
                if (n != null && world.Overlaps(StoryLayout.RectOf(n))) _selection.Add(n);
            ApplyHighlights();
        }

        void FinishDrag()
        {
            var finals = new Dictionary<NarrativeNode, Vector2>();
            foreach (var pair in _dragStart)
            {
                var p = pair.Key.position;
                finals[pair.Key] = new Vector2(Mathf.Round(p.x / 10f) * 10f, Mathf.Round(p.y / 10f) * 10f);
                pair.Key.position = pair.Value;
            }
            _edit.Move(finals);
        }

        void FinishLink(Vector2 local)
        {
            var from = _linkFrom;
            int port = _linkPort;
            _linkFrom = null;
            _dropTarget = null;
            _edges.SetDrag(null, null);
            ApplyHighlights();
            if (from == null) return;

            var world = ToWorld(local);
            var target = HitCard(world, from);
            if (target != null)
            {
                if (!_edit.Connect(from.Model, target.Model, port) && from.Model.HasNamedOutputs)
                    Debug.Log(StoryText.T("Todas las salidas de este paso ya están conectadas.", "All outputs of this step are already connected."));
                return;
            }
            if (Vector2.Distance(world, _linkStart) * _zoom < 14f) return;
            OpenPopup(local, from.Model, port, null);
        }

        void CancelGesture()
        {
            if (_mode == Mode.Drag && _dragStart != null && _dragMoved)
                foreach (var pair in _dragStart) pair.Key.position = pair.Value;
            if (_box != null) _box.style.display = DisplayStyle.None;
            if (_mode != Mode.None) { _mode = Mode.None; _edges.SetDrag(null, null); _dropTarget = null; Rebuild(); }
        }

        StepCard HitCard(Vector2 world, StepCard except)
        {
            foreach (var pair in _cardOf)
                if (pair.Value != except && StoryLayout.RectOf(pair.Key).Contains(world)) return pair.Value;
            return null;
        }

        void OnWheel(WheelEvent e)
        {
            var local = this.WorldToLocal(e.mousePosition);
            ZoomAround(local, _zoom * (e.delta.y > 0 ? 0.9f : 1.1f));
            e.StopPropagation();
        }

        void OnKeyDown(KeyDownEvent e)
        {
            if (_edit?.Graph == null || _popup.IsOpen) return;
            if (e.actionKey && e.keyCode == KeyCode.D && _selection.Count > 0)
            {
                Duplicate();
                e.StopPropagation();
                return;
            }
            // Leave other modified keys (Shift+Space maximize, Ctrl+A, ...) to the editor.
            if (e.modifiers != EventModifiers.None) return;
            switch (e.keyCode)
            {
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    if (_selection.Count > 0) { _edit.Delete(_selection.ToList()); e.StopPropagation(); }
                    break;
                case KeyCode.F:
                    if (Primary != null) CenterOn(Primary); else FrameAll();
                    e.StopPropagation();
                    break;
                case KeyCode.A:
                case KeyCode.Space:
                    OpenPopup(LayoutCenter() - new Vector2(160f, 120f), Primary, -1, null);
                    e.StopPropagation();
                    break;
            }
        }

        // ── Adding steps ─────────────────────────────────────────────────────

        public void OpenAddAtCenter() => OpenPopup(LayoutCenter() - new Vector2(160f, 160f), null, -1, null);

        void OpenPopupOnRoute(StoryLayout.Route route) => OpenPopup(ToLocal(route.InsertAt), null, -1, route);

        void OpenPopup(Vector2 local, NarrativeNode from, int port, StoryLayout.Route insert)
        {
            if (_edit?.Graph == null) return;
            var world = ToWorld(local);
            string hint;
            if (insert != null)
                hint = StoryText.T($"Entre {_structure.Number(insert.From)} y {_structure.Number(insert.To)}.",
                                   $"Between {_structure.Number(insert.From)} and {_structure.Number(insert.To)}.");
            else if (from != null)
                hint = StoryText.T($"Irá después de {_structure.Number(from)} «{Title(from)}».", $"Comes after {_structure.Number(from)} “{Title(from)}”.");
            else
                hint = StoryText.T("Paso suelto: conéctalo arrastrando desde su borde.", "Loose step: connect it by dragging from its edge.");

            string section = from != null || insert != null ? string.Empty : SectionAt(world);
            var position = insert != null ? insert.InsertAt - new Vector2(StoryLayout.CardWidth * 0.5f, StoryLayout.CardHeight * 0.5f)
                                          : world - new Vector2(StoryLayout.CardWidth * 0.5f, 20f);
            position = new Vector2(Mathf.Round(position.x / 10f) * 10f, Mathf.Round(position.y / 10f) * 10f);

            _popup.Open(local, contentRect.size, hint, type =>
            {
                var node = _edit.CreateStep(type, position, section);
                if (node == null) return;
                if (insert != null) _edit.InsertBetween(insert.From, insert.OutputIndex, node);
                else if (from != null) _edit.Connect(from, node, port);
                Select(node);
            },
            template =>
            {
                var copies = _edit.AddTemplate(template, position, section);
                if (copies.Count == 0) return;
                var entry = StoryTemplate.Entry(copies);
                if (insert != null) _edit.InsertBetween(insert.From, insert.OutputIndex, entry, StoryTemplate.Exit(copies));
                else if (from != null) _edit.Connect(from, entry, port);
                _selection.Clear();
                foreach (var c in copies) _selection.Add(c);
                ApplyHighlights();
                SelectionChanged?.Invoke(Primary);
            });
        }

        string SectionAt(Vector2 world)
        {
            string best = string.Empty;
            float bestDistance = float.MaxValue;
            foreach (var pair in StoryLayout.SectionFrames(_structure))
            {
                var r = pair.Value;
                float d = world.y < r.yMin ? r.yMin - world.y : world.y > r.yMax ? world.y - r.yMax : 0f;
                if (d < bestDistance) { bestDistance = d; best = pair.Key.Name; }
            }
            return best;
        }

        static string Title(NarrativeNode n) => string.IsNullOrWhiteSpace(n.displayTitle) ? NarrativeNodeCatalog.TitleOf(n.GetType()) : n.displayTitle;

        // ── Menus ────────────────────────────────────────────────────────────

        void BuildCardMenu(ContextualMenuPopulateEvent e, StepCard card)
        {
            var node = card.Model;
            if (!_selection.Contains(node)) Select(node);
            e.menu.ClearItems();
            e.menu.AppendAction(StoryText.T("Jugar desde aquí", "Play from here"), _ => PlayRequested?.Invoke(node));
            e.menu.AppendAction(StoryText.T("Pausar aquí al jugar", "Pause here when playing"),
                _ => { StoryBreakpoints.Toggle(_edit.Graph, node.guid); ApplyHighlights(); },
                _ => StoryBreakpoints.Has(_edit.Graph, node.guid) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            e.menu.AppendAction(StoryText.T("Marcar como inicio", "Set as start"), _ => _edit.SetStart(node),
                _ => node.IsAnnotation ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            foreach (var section in _structure.Sections)
            {
                var name = section.Name;
                e.menu.AppendAction(StoryText.T("Mover a la sección/", "Move to section/") + StoryStructure.DisplayName(name).Replace('/', '-'),
                    _ => _edit.SetSection(_selection.ToList(), name),
                    _ => Structure.SectionKeyOf(node) == name ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            e.menu.AppendAction(StoryText.T("Mover a la sección/Nueva sección desde aquí…", "Move to section/New section from here…"),
                _ => AskName(ToLocal(StoryLayout.RectOf(node).position), string.Empty, n => _edit.SetSection(new[] { node }, n)));
            e.menu.AppendAction(StoryText.T("Duplicar  (Ctrl+D)", "Duplicate  (Ctrl+D)"), _ => Duplicate());
            e.menu.AppendAction(StoryText.T("Guardar como plantilla…", "Save as template…"),
                _ => AskName(ToLocal(StoryLayout.RectOf(node).position), string.Empty, n =>
                {
                    var saved = StoryTemplate.Create(n, _selection.ToList());
                    if (saved != null) EditorGUIUtility.PingObject(saved);
                }));
            e.menu.AppendSeparator();
            e.menu.AppendAction(StoryText.T("Borrar", "Delete"), _ => _edit.Delete(_selection.ToList()));
            e.StopPropagation();
        }

        // ── Naming sections ──────────────────────────────────────────────────

        TextField _nameField;

        void AskName(Vector2 local, string current, Action<string> commit)
        {
            _nameField?.RemoveFromHierarchy();
            var field = new TextField { value = current ?? string.Empty };
            field.AddToClassList("story-name-field");
            field.style.left = local.x;
            field.style.top = local.y;
            _nameField = field;
            bool done = false;
            void Finish(bool accept)
            {
                if (done) return;
                done = true;
                var value = field.value;
                field.RemoveFromHierarchy();
                if (_nameField == field) _nameField = null;
                if (accept && !string.IsNullOrWhiteSpace(value)) commit(value.Trim());
                Focus();
            }
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Finish(true); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Escape) { Finish(false); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<FocusOutEvent>(_ => Finish(true));
            field.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            Add(field);
            field.schedule.Execute(() => { field.Focus(); field.SelectAll(); });
        }

        void BuildCanvasMenu(ContextualMenuPopulateEvent e)
        {
            if (_edit?.Graph == null) return;
            var local = this.WorldToLocal(e.mousePosition);
            e.menu.AppendAction(StoryText.T("Añadir paso aquí", "Add step here"), _ => OpenPopup(local, null, -1, null));
            e.menu.AppendAction(StoryText.T("Auto-ordenar", "Auto-arrange"), _ => AutoLayoutRequested?.Invoke());
            e.menu.AppendAction(StoryText.T("Encuadrar", "Frame all"), _ => FrameAll());
        }
    }
}
