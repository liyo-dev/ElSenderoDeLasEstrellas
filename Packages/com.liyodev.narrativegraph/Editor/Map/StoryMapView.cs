using System;
using System.Collections.Generic;
using System.Linq;
using Liyodev.NarrativeGraph.Editor.Story;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.NarrativeGraph.Editor
{
    /// Mapa de la historia: el mundo en esquema con un muñeco por personaje, colocado según el
    /// paso elegido en el editor de la historia. Solo enseña lo que el grafo sabe: al pulsar un
    /// muñeco se ve dónde está y qué paso lo dejó así, si la historia le espera, qué dice si le
    /// hablas, lo que le espera más adelante (sus próximas piezas «Personaje desde aquí»), sus
    /// próximos diálogos y las misiones que da. Para cambiar algo se abre su paso: el mapa no
    /// escribe en el grafo. La barra de abajo recorre los pasos del capítulo (▶ los reproduce) y
    /// una flecha enseña quién se ha movido respecto al paso anterior.
    ///
    /// Navegación: rueda del ratón = zoom hacia el cursor; botón central (o el izquierdo sobre el
    /// fondo) = desplazar; F o «Encuadrar» = ver el mapa entero.
    ///
    /// Es una vista: vive dentro del editor de la historia (en grande o, compacta, en el panel
    /// de abajo) y en una ventana aparte (StoryMapWindow) cuando se saca.
    public sealed class StoryMapView : VisualElement
    {
        /// Compacta: solo el dibujo, ajustado al hueco; al pulsar un muñeco se avisa (ActorClicked).
        public readonly bool Compact;
        public event Action<string> ActorClicked;
        /// El paso que enseña ha cambiado (para guardarlo en la ventana que la contiene).
        public event Action<NarrativeGraph, string> StepChanged;

        const float TokenW = 58f, TokenH = 70f;
        const float CanvasW = 1400f;

        float _canvasH = 900f;
        readonly Dictionary<string, Rect> _zoneRects = new();
        readonly List<(NarrativeWorldMap.Scene scene, Rect rect)> _sceneBands = new();

        NarrativeWorldMap _map;
        NarrativeGraph _graph;
        string _stepGuid;
        NarrativeNode _step;
        StoryStructure _structure;
        bool _onlyMain;
        string _selectedActor;
        NarrativeDialogueAsset _dialogue;

        VisualElement _viewport;
        VisualElement _canvas;
        VisualElement _card;
        Label _stepLabel;
        Dictionary<string, Placement> _placements = new();
        Dictionary<string, Placement> _previous = new();
        readonly Dictionary<string, Vector2> _positions = new();
        readonly Dictionary<string, Vector2> _previousPositions = new();
        SliderInt _slider;
        Label _sliderLabel;
        Button _playButton;
        IVisualElementScheduledItem _playing;

        // Vista: desplazamiento y zoom del lienzo dentro del hueco.
        const float MinZoom = 0.05f, MaxZoom = 4f, PanThreshold = 4f;
        float _zoom = 1f;
        Vector2 _pan;
        bool _viewTouched;
        int _panPointer = -1;
        bool _panByBackground;
        bool _panMoved;
        Vector2 _panFrom, _panStartPan;
        Label _zoomLabel;

        // Reconstrucción: se agrupan las peticiones en una por fotograma y solo se dibuja si se ve.
        bool _dirty;
        bool _rebuildQueued;

        sealed class Placement
        {
            public NarrativeWorldMap.Zone Zone;
            public bool Away;
            public string Where;
            public string Why;
            public NarrativeGraph WhyGraph;
            public string WhyGuid;
            public ActorStateNode Piece;
        }

        // ── Escenas y sitios ─────────────────────────────────────────────────

        /// El nombre del sitio de una marca: el sitio donde cae, o la marca.
        // (El mapa ya no coloca por coordenadas: el sitio sale de las marcas del asset.)

        public StoryMapView(bool compact, NarrativeGraph graph = null, string stepGuid = null)
        {
            Compact = compact;
            _graph = graph;
            _stepGuid = stepGuid;
            style.flexGrow = 1;
            BuildGUI();
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                StoryGraphWindow.StepShown -= OnStepShown;
                StoryGraphWindow.StepShown += OnStepShown;
                StoryCardImages.Changed -= Rebuild;
                StoryCardImages.Changed += Rebuild;
                Undo.undoRedoPerformed -= Rebuild;
                Undo.undoRedoPerformed += Rebuild;
                StoryGraphWindow.RequestStep();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                StoryGraphWindow.StepShown -= OnStepShown;
                StoryCardImages.Changed -= Rebuild;
                Undo.undoRedoPerformed -= Rebuild;
                StopPlaying();
            });
        }

        /// Elige un personaje (como si se pulsara su muñeco).
        public void SelectActor(string actorId)
        {
            _selectedActor = actorId;
            _dialogue = null;
            Rebuild();
        }

        void OnStepShown(NarrativeGraph graph, NarrativeNode step, StoryStructure structure)
        {
            // Sin paso elegido en el editor (p. ej. tras recompilar), el mapa sigue en el suyo.
            if (step == null && _step != null) return;
            _graph = graph;
            _step = step;
            _structure = structure;
            _dialogue = null;
            Rebuild();
        }

        void BuildGUI()
        {
            var root = this;
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(NarrativeGraphPaths.Root + "/Editor/Story/StoryEditor.uss");
            if (sheet != null) root.styleSheets.Add(sheet);
            root.style.flexDirection = FlexDirection.Column;

            var bar = new Toolbar();
            _stepLabel = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 6, marginRight = 12, alignSelf = Align.Center } };
            bar.Add(_stepLabel);
            bar.Add(new ToolbarButton(() =>
            {
                var menu = new GenericMenu();
                foreach (var g in NarrativeProjectIndex.Graphs.Where(g => g != null).OrderBy(g => g.name))
                {
                    var graph = g;
                    menu.AddItem(new GUIContent(g.name), g == _graph, () => ShowGraph(graph));
                }
                menu.ShowAsContext();
            }) { text = StoryText.T("Capítulo ▾", "Chapter ▾") });
            var main = new ToolbarToggle { text = StoryText.T("Solo principales", "Main only") };
            main.RegisterValueChangedCallback(e => { _onlyMain = e.newValue; Rebuild(); });
            bar.Add(main);
            bar.Add(new ToolbarSpacer { flex = true });
            root.Add(bar);

            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            BuildViewport();
            body.Add(_viewport);
            _card = new ScrollView { style = { width = 320, paddingLeft = 12, paddingRight = 12, paddingTop = 10, borderLeftWidth = 1, borderLeftColor = new Color(0, 0, 0, 0.4f) } };
            body.Add(_card);
            root.Add(body);

            var steps = new Toolbar { style = { height = 26 } };
            steps.Add(new ToolbarButton(() => StepBy(-1)) { text = "◀", tooltip = StoryText.T("Paso anterior", "Previous step") });
            _playButton = new ToolbarButton(TogglePlay) { text = "▶", tooltip = StoryText.T("Recorrer los pasos", "Play through the steps") };
            steps.Add(_playButton);
            steps.Add(new ToolbarButton(() => StepBy(1)) { text = "▶|", tooltip = StoryText.T("Paso siguiente", "Next step") });
            _slider = new SliderInt(0, 1) { style = { flexGrow = 1, marginLeft = 8, marginRight = 8 } };
            _slider.RegisterValueChangedCallback(e => { if (_structure != null && e.newValue != IndexOfStep()) GoTo(e.newValue); });
            steps.Add(_slider);
            _sliderLabel = new Label { style = { minWidth = 60, alignSelf = Align.Center } };
            steps.Add(_sliderLabel);
            root.Add(steps);

            if (Compact)
            {
                // Compacta: solo el dibujo, encogido para que quepa entero en el hueco.
                bar.style.display = DisplayStyle.None;
                steps.style.display = DisplayStyle.None;
                _card.style.display = DisplayStyle.None;
            }

            _map = NarrativeWorldMap.Find();
            if (_step == null && _graph != null && !string.IsNullOrEmpty(_stepGuid)) _step = _graph.FindNode(_stepGuid);
            if (_graph == null && NarrativeProjectIndex.Graphs.FirstOrDefault(g => g != null) is { } first) _graph = first;
            if (_graph != null && _structure == null) _structure = StoryStructure.Build(_graph);
            Rebuild();
        }

        // ── Vista: desplazar y hacer zoom ───────────────────────────────────

        void BuildViewport()
        {
            _viewport = new VisualElement { focusable = true, style = { flexGrow = 1, overflow = Overflow.Hidden } };
            _canvas = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, left = 0, top = 0, width = CanvasW, height = _canvasH,
                    transformOrigin = new TransformOrigin(0, 0),
                    backgroundColor = new Color(0.11f, 0.12f, 0.14f),
                },
            };
            _canvas.generateVisualContent += DrawMoves;
            _viewport.Add(_canvas);

            // El botón central se coge antes que nadie (TrickleDown): sobre un muñeco o una zona
            // también desplaza. El izquierdo solo llega aquí si se pulsa el fondo.
            _viewport.RegisterCallback<PointerDownEvent>(OnMiddleDown, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerDownEvent>(OnBackgroundDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnPanMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnPanUp);
            _viewport.RegisterCallback<PointerCaptureOutEvent>(_ => _panPointer = -1);
            _viewport.RegisterCallback<WheelEvent>(OnWheel);
            _viewport.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.F) { FitView(); e.StopPropagation(); } });
            _viewport.RegisterCallback<GeometryChangedEvent>(_ => { if (!_viewTouched) FitView(); });
            RegisterCallback<GeometryChangedEvent>(_ => { if (_dirty && IsShown()) RebuildNow(); });

            var bar = new VisualElement { style = { position = Position.Absolute, right = 6, bottom = 6, flexDirection = FlexDirection.Row, alignItems = Align.Center, backgroundColor = new Color(0.07f, 0.09f, 0.12f, 0.85f), paddingLeft = 2, paddingRight = 2 } };
            Round(bar, 5);
            bar.Add(ViewButton("−", StoryText.T("Alejar", "Zoom out"), () => ZoomAround(ViewCenter(), _zoom / 1.25f)));
            _zoomLabel = new Label { style = { minWidth = 38, unityTextAlign = TextAnchor.MiddleCenter, fontSize = 10, color = new Color(0.73f, 0.77f, 0.83f) } };
            bar.Add(_zoomLabel);
            bar.Add(ViewButton("+", StoryText.T("Acercar", "Zoom in"), () => ZoomAround(ViewCenter(), _zoom * 1.25f)));
            bar.Add(ViewButton("⤢", StoryText.T("Ver todo el mapa (F)", "Show the whole map (F)"), FitView));
            _viewport.Add(bar);
            ApplyView();
        }

        static Button ViewButton(string text, string tooltip, Action action)
            => new(action) { text = text, tooltip = tooltip, style = { width = 22, height = 20, marginLeft = 0, marginRight = 0, paddingLeft = 0, paddingRight = 0, backgroundColor = Color.clear, borderLeftWidth = 0, borderRightWidth = 0, borderTopWidth = 0, borderBottomWidth = 0 } };

        Vector2 ViewCenter() => _viewport.contentRect.size * 0.5f;

        void OnMiddleDown(PointerDownEvent e)
        {
            if (e.button != 2) return;
            BeginPan(e, background: false);
            e.StopPropagation();
        }

        /// Izquierdo sobre el fondo: arrastrar desplaza; un clic sin mover quita la selección.
        void OnBackgroundDown(PointerDownEvent e)
        {
            if (e.button != 0 || _panPointer >= 0) return;
            BeginPan(e, background: true);
        }

        void BeginPan(PointerDownEvent e, bool background)
        {
            _viewport.Focus();
            _panPointer = e.pointerId;
            _panByBackground = background;
            _panMoved = false;
            _panFrom = _viewport.WorldToLocal(e.position);
            _panStartPan = _pan;
            _viewport.CapturePointer(e.pointerId);
        }

        void OnPanMove(PointerMoveEvent e)
        {
            if (e.pointerId != _panPointer) return;
            var delta = (Vector2)_viewport.WorldToLocal(e.position) - _panFrom;
            if (!_panMoved && delta.sqrMagnitude < PanThreshold * PanThreshold) return;
            _panMoved = true;
            _viewTouched = true;
            _pan = _panStartPan + delta;
            ApplyView();
            e.StopPropagation();
        }

        void OnPanUp(PointerUpEvent e)
        {
            if (e.pointerId != _panPointer) return;
            _panPointer = -1;
            if (_viewport.HasPointerCapture(e.pointerId)) _viewport.ReleasePointer(e.pointerId);
            if (_panByBackground && !_panMoved && (_selectedActor != null || _dialogue != null))
            {
                _selectedActor = null;
                _dialogue = null;
                Rebuild();
            }
            e.StopPropagation();
        }

        void OnWheel(WheelEvent e)
        {
            ZoomAround(_viewport.WorldToLocal(e.mousePosition), _zoom * (e.delta.y > 0 ? 1f / 1.15f : 1.15f));
            e.StopPropagation();
        }

        /// Cambia el zoom dejando quieto el punto del lienzo que hay bajo <paramref name="local"/>.
        void ZoomAround(Vector2 local, float zoom)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            var onCanvas = (local - _pan) / _zoom;
            _zoom = zoom;
            _pan = local - onCanvas * _zoom;
            _viewTouched = true;
            ApplyView();
        }

        /// Encuadra el contenido del mapa.
        void FitView()
        {
            if (_viewport == null) return;
            var size = _viewport.contentRect.size;
            if (size.x < 2f || size.y < 2f) return;
            var target = new Rect(0, 0, CanvasW, _canvasH);
            _zoom = Mathf.Clamp(Mathf.Min(size.x / target.width, size.y / target.height) * 0.98f, MinZoom, MaxZoom);
            _pan = size * 0.5f - target.center * _zoom;
            _viewTouched = false;
            ApplyView();
        }

        void ApplyView()
        {
            _canvas.style.translate = new Translate(_pan.x, _pan.y);
            _canvas.style.scale = new Scale(new Vector2(_zoom, _zoom));
            var text = Mathf.RoundToInt(_zoom * 100f) + " %";
            if (_zoomLabel.text != text) _zoomLabel.text = text;
        }

        // ── Dónde está cada uno ─────────────────────────────────────────────

        Dictionary<string, Placement> Compute(NarrativeNode step)
        {
            var placements = new Dictionary<string, Placement>();
            if (_map == null) return placements;
            var facts = _graph != null && step != null
                ? StoryCache.Get($"map:{_graph.GetEntityId()}:{step.Guid}", () => StepFacts.Of(_graph, step))
                : StepFacts.Empty;
            var story = facts.Story;
            var pieces = facts.Pieces;
            var playerMark = facts.PlayerMark;
            string offStage = StoryText.T("fuera de escena", "off stage");

            foreach (var c in Cast())
            {
                pieces.TryGetValue(c.actorId, out var e);
                // «Lo de siempre» con presencia sin tocar no coloca a nadie: solo cuenta lo que la
                // historia dice de verdad (una marca, un «no está» o un hecho de posición).
                bool placedPiece = e != null && (e.Node.Presence != ActorPresence.Unchanged
                                                 || e.Node.PieceLayer != ActorStateNode.Layer.Usual);
                bool named = placedPiece || (story != null && story.Actors.ContainsKey(c.actorId));
                bool playerAtStart = c.actorId == "Player" && !string.IsNullOrEmpty(playerMark);
                if (!named && !playerAtStart) continue;   // la historia no lo coloca: no sale

                var p = new Placement();
                string mark = null;
                if (e != null && e.Node.Presence != ActorPresence.Unchanged)
                {
                    p.Piece = e.Node;
                    p.Away = e.Node.Presence == ActorPresence.Away;
                    mark = e.Node.MarkId;
                    SetWhy(p, e.Graph, e.Source ?? e.Node.Guid);
                }
                else if (playerAtStart)
                {
                    // El jugador está donde le deja el arranque de la prueba: la historia con sus
                    // puntos (◆) en su sitio, que es lo que se usa al probar desde este paso.
                    if (e != null) p.Piece = e.Node;
                    mark = playerMark;
                }
                else
                {
                    if (e != null) p.Piece = e.Node;
                    if (story != null && story.Actors.TryGetValue(c.actorId, out var a))
                    {
                        if (a.value == offStage) p.Away = true;
                        else mark = a.value;
                        SetWhy(p, story.SourceGraph.TryGetValue(a.source ?? "", out var g) ? g : _graph, a.source);
                    }
                }
                p.Zone = _map.ZoneOfMark(mark) ?? (mark == null ? _map.ZoneById(c.homeZone) : null);
                p.Where = p.Away ? StoryText.T("No está en escena", "Not on stage")
                        : p.Zone == null ? StoryText.T("Sin sitio en el mapa", "No place on the map") + (mark != null ? $" ({mark})" : "")
                        : p.Zone.name + (mark != null ? $" · {mark}" : StoryText.T(" · de siempre", " · usual place"));
                placements[c.actorId] = p;
            }
            return placements;
        }

        /// Lo que la historia deja hecho hasta un paso (caro: rehace la historia y prepara el
        /// arranque). Se guarda en StoryCache hasta que cambie un grafo; el mapa (zonas,
        /// personajes) se aplica encima en cada dibujo, así que editarlo no lo invalida.
        sealed class StepFacts
        {
            public static readonly StepFacts Empty = new();
            public RecordingStateWriter Story;
            public readonly Dictionary<string, ActorStateFacts.Entry> Pieces = new();
            public string PlayerMark;

            public static StepFacts Of(NarrativeGraph graph, NarrativeNode step)
            {
                var facts = new StepFacts();
                var plan = NarrativeStartPlanner.Plan(graph, step.Guid);
                facts.Story = plan.Story;
                foreach (var e in ActorStateFacts.From(plan.Story)) facts.Pieces[e.Actor] = e;
                var sheet = plan.Sheet;
                if (sheet != null && sheet.IsReady && !string.IsNullOrEmpty(sheet.positionField))
                    using (var preview = plan.Build(story: true, points: true, testPlayer: true))
                        if (preview.Data != null) facts.PlayerMark = new SerializedObject(preview.Data).FindProperty(sheet.positionField)?.stringValue;
                return facts;
            }
        }

        void SetWhy(Placement p, NarrativeGraph graph, string guid)
        {
            var node = graph != null && !string.IsNullOrEmpty(guid) ? graph.FindNode(guid) : null;
            if (node == null) return;
            p.WhyGraph = graph;
            p.WhyGuid = guid;
            p.Why = (graph == _graph && _structure != null ? _structure.Number(node) + " " : graph.name + " · ") + "«" + NarrativeStartPlanner.Title(node) + "»";
        }

        // ── Dibujo ──────────────────────────────────────────────────────────

        /// Pide redibujar. Varias peticiones seguidas (cambio de paso, deshacer, retratos) se juntan
        /// en una sola al siguiente fotograma, y si el mapa está oculto espera a que se vea.
        void Rebuild()
        {
            _dirty = true;
            if (_rebuildQueued || panel == null) return;
            _rebuildQueued = true;
            schedule.Execute(() =>
            {
                _rebuildQueued = false;
                if (_dirty && IsShown()) RebuildNow();
            });
        }

        /// true si el mapa se ve: ni él ni ningún contenedor suyo está oculto.
        bool IsShown()
        {
            for (VisualElement e = this; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return panel != null;
        }

        void RebuildNow()
        {
            using var medida = StoryTiming.Measure(StoryText.T("Mapa: dibujar", "Map: draw"));
            _dirty = false;
            var guid = _step != null ? _step.Guid : null;
            if (guid != _stepGuid) { _stepGuid = guid; StepChanged?.Invoke(_graph, guid); }
            if (_canvas == null) return;
            _canvas.Clear();
            _card.Clear();
            _stepLabel.text = _step != null
                ? $"{_graph?.name} · {_structure?.Number(_step)} {NarrativeStartPlanner.Title(_step)}"
                : StoryText.T("Elige un paso en el editor de la historia", "Pick a step in the story editor");
            if (_map == null)
            {
                _card.Add(new Label(StoryText.T("Aún no hay mapa de la historia.", "There is no story map yet.")) { style = { whiteSpace = WhiteSpace.Normal } });
                var create = new Button(() => { _map = NarrativeWorldMap.Create(); Rebuild(); }) { text = StoryText.T("Crear el mapa", "Create the map") };
                _card.Add(create);
                return;
            }

            _placements = Compute(_step);
            var previousStep = PreviousStep();
            _previous = previousStep != null ? Compute(previousStep) : new Dictionary<string, Placement>();
            LayoutMap();
            Layout(_placements, _positions, out int away, out int homeless);
            Layout(_previous, _previousPositions, out _, out _);
            RefreshSlider();

            _canvas.style.width = CanvasW;
            _canvas.style.height = _canvasH;
            _canvas.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            _canvas.style.backgroundColor = new Color(0.11f, 0.12f, 0.14f);
            foreach (var band in _sceneBands) _canvas.Add(SceneElement(band.scene, band.rect));
            foreach (var zone in _map.zones) _canvas.Add(ZoneElement(zone));
            _canvas.Add(Tray(StoryText.T("No están en escena", "Not on stage"), _canvasH - 196));
            if (homeless > 0) _canvas.Add(Tray(StoryText.T("Sin sitio en el mapa (la historia no dice dónde están)", "No place on the map (the story does not say where they are)"), _canvasH - 116));
            foreach (var c in Cast())
                if (_positions.TryGetValue(c.actorId, out var pos)) _canvas.Add(Token(c, _placements[c.actorId], pos));
            _canvas.MarkDirtyRepaint();
            if (!_viewTouched) FitView();

            // La compacta no enseña la ficha: no se monta.
            if (Compact) return;
            if (_selectedActor != null && _placements.ContainsKey(_selectedActor)
                && Cast().FirstOrDefault(x => x.actorId == _selectedActor) is { } ch)
            {
                if (_dialogue != null) DialogueCard(ch);
                else CharacterCard(ch);
            }
            else HelpCard();
            MarkDirtyRepaint();
        }

        /// Dónde va cada muñeco: en su zona, en fila; los que no están, en la bandeja de abajo.
        void Layout(Dictionary<string, Placement> placements, Dictionary<string, Vector2> positions, out int away, out int homeless)
        {
            positions.Clear();
            var byZone = new Dictionary<string, int>();
            away = homeless = 0;
            foreach (var c in Cast())
            {
                if (_onlyMain && !c.main && c.actorId != _selectedActor) continue;
                if (!placements.TryGetValue(c.actorId, out var p)) continue;
                if (p.Away || p.Zone == null)
                {
                    int i = p.Away ? away++ : homeless++;
                    positions[c.actorId] = new Vector2(30 + i * (TokenW + 10), p.Away ? _canvasH - 160 : _canvasH - 80);
                    continue;
                }
                if (!_zoneRects.TryGetValue(p.Zone.id, out var rect))
                {
                    int i = homeless++;
                    positions[c.actorId] = new Vector2(30 + i * (TokenW + 10), _canvasH - 80);
                    continue;
                }
                byZone.TryGetValue(p.Zone.id, out var n);
                byZone[p.Zone.id] = n + 1;
                int perRow = Mathf.Max(1, (int)((rect.width - 20) / (TokenW + 6)));
                positions[c.actorId] = new Vector2(rect.x + 12 + (n % perRow) * (TokenW + 6), rect.y + 34 + (n / perRow) * (TokenH + 6));
            }
        }

        /// Flechas de quien ha cambiado de sitio respecto al paso anterior.
        void DrawMoves(MeshGenerationContext ctx)
        {
            if (_map == null) return;
            var p2 = ctx.painter2D;
            foreach (var c in Cast())
            {
                if (!_positions.TryGetValue(c.actorId, out var now) || !_previousPositions.TryGetValue(c.actorId, out var before)) continue;
                if (!_placements.TryGetValue(c.actorId, out var a) || !_previous.TryGetValue(c.actorId, out var b)) continue;
                if (a.Away == b.Away && a.Zone == b.Zone) continue;
                var from = before + new Vector2(TokenW / 2, 20);
                var to = now + new Vector2(TokenW / 2, 20);
                var col = c.color; col.a = 0.8f;
                p2.strokeColor = col;
                p2.lineWidth = 2.5f;
                p2.BeginPath(); p2.MoveTo(from); p2.LineTo(to); p2.Stroke();
                var dir = (to - from).normalized;
                var side = new Vector2(-dir.y, dir.x);
                p2.fillColor = col;
                p2.BeginPath(); p2.MoveTo(to - dir * 4); p2.LineTo(to - dir * 16 + side * 7); p2.LineTo(to - dir * 16 - side * 7); p2.ClosePath(); p2.Fill();
                col.a = 0.35f;
                p2.strokeColor = col;
                p2.lineWidth = 1.5f;
                p2.BeginPath(); p2.Arc(from, 12, 0, 360); p2.Stroke();
            }
        }

        static Label Tray(string text, float y)
            => new(text) { style = { position = Position.Absolute, left = 30, top = y - 2, color = new Color(1, 1, 1, 0.45f), fontSize = 11 } };

        VisualElement SceneElement(NarrativeWorldMap.Scene scene, Rect r)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            var fill = scene != null ? scene.color : new Color(0.16f, 0.20f, 0.27f);
            fill.a = 0.65f;
            el.style.position = Position.Absolute;
            el.style.left = r.x; el.style.top = r.y; el.style.width = r.width; el.style.height = r.height;
            el.style.backgroundColor = fill;
            SetBorder(el, new Color(1f, 1f, 1f, 0.10f), 1);
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = 10f;
            var title = new Label(scene != null ? scene.name : StoryText.T("Sin escena", "No scene")) { pickingMode = PickingMode.Ignore };
            title.style.marginLeft = 14;
            title.style.marginTop = 10;
            title.style.color = new Color(0.82f, 0.86f, 0.92f);
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            el.Add(title);
            return el;
        }

        VisualElement ZoneElement(NarrativeWorldMap.Zone zone)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            if (!_zoneRects.TryGetValue(zone.id, out var r)) return el;
            var fill = zone.color; fill.a = 0.12f;
            el.style.position = Position.Absolute;
            el.style.left = r.x; el.style.top = r.y; el.style.width = r.width; el.style.height = r.height;
            el.style.backgroundColor = fill;
            SetBorder(el, zone.color, 2);
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = 12f;
            var name = new Label(zone.name) { pickingMode = PickingMode.Ignore };
            name.style.unityTextAlign = TextAnchor.UpperLeft;
            name.style.marginLeft = 12;
            name.style.marginTop = 8;
            name.style.color = Color.Lerp(zone.color, Color.white, 0.55f);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            el.Add(name);
            return el;
        }

        /// Coloca las escenas en bandas y los sitios dentro, y calcula el alto del lienzo.
        void LayoutMap()
        {
            _zoneRects.Clear();
            _sceneBands.Clear();
            if (_map == null) { _canvasH = 900f; return; }

            var scenes = new List<NarrativeWorldMap.Scene>(_map.scenes ?? new List<NarrativeWorldMap.Scene>());
            scenes.Sort((a, b) => a.order.CompareTo(b.order));
            bool hasUnknown = _map.zones.Any(z => string.IsNullOrEmpty(z.scene) || scenes.All(s => s.id != z.scene));
            if (hasUnknown)
                scenes.Add(new NarrativeWorldMap.Scene { id = string.Empty, name = StoryText.T("Sin escena", "No scene"), order = int.MaxValue });

            const float bandX = 40f, bandW = CanvasW - 80f, headerH = 40f, gap = 18f, placeW = 300f;
            int columns = Mathf.Max(1, Mathf.FloorToInt((bandW - gap) / (placeW + gap)));
            int maxCount = _map.zones.Count == 0 ? 0 : _map.zones.Max(z =>
                _placements.Values.Count(p => !p.Away && p.Zone != null && p.Zone.id == z.id));
            int maxRows = Mathf.Max(1, Mathf.CeilToInt(maxCount / (float)columns));
            float placeH = Mathf.Max(170f, 34f + maxRows * (TokenH + 6) + 10f);
            float cursor = 30f;
            foreach (var scene in scenes)
            {
                var zones = _map.zones.Where(z => string.IsNullOrEmpty(scene.id)
                        ? string.IsNullOrEmpty(z.scene) || scenes.All(s => s.id != z.scene)
                        : z.scene == scene.id).ToList();
                if (zones.Count == 0) continue;

                int cols = Mathf.Max(1, Mathf.FloorToInt((bandW - gap) / (placeW + gap)));
                int rows = Mathf.CeilToInt(zones.Count / (float)cols);
                float bandH = headerH + rows * (placeH + gap) + gap;
                var band = new Rect(bandX, cursor, bandW, bandH);
                _sceneBands.Add((scene, band));

                for (int i = 0; i < zones.Count; i++)
                {
                    int col = i % cols, row = i / cols;
                    _zoneRects[zones[i].id] = new Rect(
                        bandX + gap + col * (placeW + gap),
                        cursor + headerH + row * (placeH + gap),
                        placeW, placeH);
                }
                cursor += bandH + 22f;
            }

            _canvasH = Mathf.Max(600f, cursor + 220f);
        }

        VisualElement Token(NarrativeWorldMap.Character c, Placement p, Vector2 pos)
        {
            var el = new VisualElement { tooltip = $"{c.name}\n{p.Where}" };
            el.style.position = Position.Absolute;
            el.style.left = pos.x; el.style.top = pos.y; el.style.width = TokenW; el.style.height = TokenH;
            el.style.alignItems = Align.Center;
            var col = c.color;
            bool sel = c.actorId == _selectedActor;
            if (p.Away) el.style.opacity = 0.45f;
            // Chincheta con el retrato del personaje (o su inicial) y un pico abajo.
            var ring = new VisualElement { pickingMode = PickingMode.Ignore, style = { width = 40, height = 40, backgroundColor = col, overflow = Overflow.Hidden } };
            Round(ring, 20);
            SetBorder(ring, sel ? Color.white : col, 3);
            var face = NarrativeIconImages.ActorArt(c.actorId) ?? StoryCardImages.PortraitOf(c.actorId, prefabFallback: false);
            if (face != null) ring.Add(new Image { image = face, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore, style = { flexGrow = 1 } });
            else ring.Add(new Label(string.IsNullOrEmpty(c.name) ? "?" : c.name.Substring(0, 1)) { pickingMode = PickingMode.Ignore, style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter, fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white } });
            el.Add(ring);
            var tip = new VisualElement { pickingMode = PickingMode.Ignore, style = { width = 0, height = 0, borderLeftWidth = 6, borderRightWidth = 6, borderTopWidth = 8, borderLeftColor = Color.clear, borderRightColor = Color.clear, borderTopColor = sel ? Color.white : col } };
            el.Add(tip);
            if (p.Piece != null && p.Piece.Lines != null && p.Piece.Lines.Any(l => l != null))
            {
                var talk = new Label("…") { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, right = 0, top = -6, fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.1f, 0.08f, 0.02f), backgroundColor = new Color(0.95f, 0.71f, 0.27f), paddingLeft = 3, paddingRight = 3 } };
                Round(talk, 6);
                el.Add(talk);
            }
            var label = new Label(c.name) { pickingMode = PickingMode.Ignore, style = { fontSize = 10, maxWidth = TokenW + 10, marginTop = 1, paddingLeft = 4, paddingRight = 4, backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.85f), overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, color = Color.white, unityTextAlign = TextAnchor.UpperCenter, whiteSpace = WhiteSpace.NoWrap } };
            Round(label, 4);
            el.Add(label);

            el.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (Compact) ActorClicked?.Invoke(c.actorId);
                else { _selectedActor = c.actorId; _dialogue = null; Rebuild(); }
                e.StopPropagation();
            });
            return el;
        }

        // ── Fichas ──────────────────────────────────────────────────────────

        void HelpCard()
        {
            Title(StoryText.T("Mapa de la historia", "Story map"), null);
            Muted(StoryText.T("El mapa es un esquema por escenas: cada sitio es un rectángulo y cada personaje cae en el sitio que le da el paso. Pulsa uno para ver lo suyo. «…» = tiene algo que decir. El mapa solo enseña lo que dice el grafo: para cambiarlo, abre el paso.",
                              "Pick a step in the story editor: the map is a scene schema; each place is a rectangle and each character falls into the place the step gives them. Click one to see their things. “…” = has something to say. The map only shows what the graph says: to change it, open the step."));
        }

        void CharacterCard(NarrativeWorldMap.Character c)
        {
            var p = _placements[c.actorId];
            Title(c.name, c.color);
            Section(StoryText.T("Dónde está", "Where"));
            Muted(p.Where);
            if (p.Why != null) Link(StoryText.T("Lo dejó ", "Set by ") + p.Why, p.WhyGraph, p.WhyGuid);
            else Muted(StoryText.T("Ningún paso lo dice: está en su sitio de siempre.", "No step says so: they are in their usual place."));

            var waits = Ahead<WaitNpcInteractionNode>(n => n.NpcId == c.actorId, 1).FirstOrDefault();
            if (waits != null)
            {
                Section(StoryText.T("La historia le espera", "The story is waiting for them"));
                Link(Num(waits) + "«" + NarrativeStartPlanner.Title(waits) + "»", _graph, waits.Guid);
            }

            Section(StoryText.T("Si le hablas ahora", "If you talk to them now"));
            var lines = p.Piece?.Lines?.Where(l => l != null).ToList();
            if (lines == null || lines.Count == 0) Muted(StoryText.T("No dice nada propio.", "Nothing of their own."));
            else
            {
                Muted(StoryText.T($"«{NarrativeStartPlanner.Title(p.Piece)}» (una por charla, en orden)", $"“{NarrativeStartPlanner.Title(p.Piece)}” (one per chat, in order)"));
                foreach (var l in lines) DialogueButton(l);
            }

            var agenda = Ahead<ActorStateNode>(n => n.ActorId == c.actorId && n != _step, 4).ToList();
            if (agenda.Count > 0)
            {
                Section(StoryText.T("Lo que le espera", "What lies ahead"));
                foreach (var n in agenda) Link(Num(n) + AgendaLine(n), _graph, n.Guid);
            }

            var next = Ahead<PlayDialogueNode>(n => n.NpcId == c.actorId && n.Dialogue != null, 3).ToList();
            if (next.Count > 0)
            {
                Section(StoryText.T("Próximos diálogos", "Next dialogues"));
                foreach (var n in next) { DialogueButton(n.Dialogue, Num(n)); }
            }

            var quests = _graph?.nodes?.OfType<StartQuestNode>().Where(q => q.GiverId == c.actorId && !string.IsNullOrEmpty(q.QuestId)).ToList();
            if (quests != null && quests.Count > 0)
            {
                Section(StoryText.T("Misiones que da", "Quests they give"));
                var story = _step != null ? NarrativeStartPlanner.Plan(_graph, _step.Guid).Story : null;
                foreach (var q in quests)
                {
                    string state = story != null && story.Quests.TryGetValue(q.QuestId, out var s)
                        ? (s.value == RecordingStateWriter.QuestDone ? StoryText.T("hecha", "done") : StoryText.T("en curso", "active"))
                        : StoryText.T("sin empezar", "not started");
                    Link($"{NarrativeProjectIndex.FindQuest(q.QuestId)?.displayName ?? q.QuestId} · {state}", _graph, q.Guid);
                }
            }
        }

        void DialogueCard(NarrativeWorldMap.Character c)
        {
            var back = new Button(() => { _dialogue = null; Rebuild(); }) { text = "← " + c.name };
            back.style.alignSelf = Align.FlexStart;
            _card.Add(back);
            Title(_dialogue.name, c.color);
            foreach (var line in DialoguePreview.Of(_dialogue))
            {
                var box = new VisualElement { style = { marginTop = 8, paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 6, backgroundColor = new Color(1, 1, 1, line.IsPlayer ? 0.04f : 0.08f) } };
                Round(box, 8);
                var head = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
                head.Add(new Label(line.Speaker) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                if (!string.IsNullOrEmpty(line.Detail))
                {
                    var chip = new Label(line.Detail.Replace("emoción: ", "")) { style = { fontSize = 10, paddingLeft = 6, paddingRight = 6, backgroundColor = new Color(0.98f, 0.78f, 0.46f, 0.25f), color = new Color(0.98f, 0.82f, 0.55f) } };
                    Round(chip, 6);
                    head.Add(chip);
                }
                box.Add(head);
                box.Add(new Label(line.Text) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 2 } });
                _card.Add(box);
            }
            var select = new Button(() => { Selection.activeObject = _dialogue; EditorGUIUtility.PingObject(_dialogue); }) { text = StoryText.T("Abrir el diálogo", "Open the dialogue") };
            select.style.marginTop = 10;
            _card.Add(select);
        }

        // ── Barra de pasos ──────────────────────────────────────────────────

        List<NarrativeNode> Steps()
        {
            if (_structure == null && _graph != null) _structure = StoryStructure.Build(_graph);
            return _structure?.StoryOrder ?? new List<NarrativeNode>();
        }

        /// Enseña un capítulo desde su primer paso (y lo abre en el editor de la historia).
        void ShowGraph(NarrativeGraph graph)
        {
            _graph = graph;
            _structure = StoryStructure.Build(graph);
            _step = null;
            GoTo(0);
        }

        int IndexOfStep() => _step == null ? -1 : Steps().IndexOf(_step);

        NarrativeNode PreviousStep()
        {
            int i = IndexOfStep();
            return i > 0 ? Steps()[i - 1] : null;
        }

        void RefreshSlider()
        {
            if (_slider == null) return;
            var steps = Steps();
            _slider.highValue = Mathf.Max(1, steps.Count - 1);
            _slider.SetValueWithoutNotify(Mathf.Max(0, IndexOfStep()));
            _sliderLabel.text = steps.Count > 0 && _step != null ? $"{IndexOfStep() + 1} / {steps.Count}" : string.Empty;
        }

        void StepBy(int delta) => GoTo(IndexOfStep() + delta);

        void GoTo(int index)
        {
            var steps = Steps();
            if (_graph == null || steps.Count == 0) return;
            index = Mathf.Clamp(index, 0, steps.Count - 1);
            if (steps[index] == _step) { StopPlaying(); return; }
            _step = steps[index];
            _dialogue = null;
            StoryGraphWindow.SelectStep(_graph, _step.Guid);
            Rebuild();
        }

        void TogglePlay()
        {
            if (_playing != null) { StopPlaying(); return; }
            _playButton.text = "❚❚";
            _playing = schedule.Execute(() =>
            {
                if (IndexOfStep() >= Steps().Count - 1) StopPlaying();
                else StepBy(1);
            }).Every(1200);
        }

        void StopPlaying()
        {
            _playing?.Pause();
            _playing = null;
            if (_playButton != null) _playButton.text = "▶";
        }

        // ── Acciones ────────────────────────────────────────────────────────

        /// Quién sale en el mapa: los personajes del mapa (con su color y su zona de siempre) y,
        /// detrás, cualquier otro al que la historia diga dónde está o qué dice, con un color fijo
        /// sacado de su id. Así nadie tiene que darlos de alta a mano.
        List<NarrativeWorldMap.Character> Cast()
            => StoryCache.Get("map:cast", () =>
            {
                var list = new List<NarrativeWorldMap.Character>(_map != null ? _map.characters : new List<NarrativeWorldMap.Character>());
                var have = new HashSet<string>(list.Select(c => c.actorId));
                var ids = NarrativeProjectIndex.Graphs.Where(g => g?.nodes != null).SelectMany(g => g.nodes)
                    .Select(n => n switch { ActorStateNode a => a.ActorId, PlaceActorNode pl => pl.ActorId, _ => null })
                    .Where(id => !string.IsNullOrEmpty(id) && have.Add(id)).OrderBy(id => id);
                foreach (var id in ids)
                {
                    var name = NarrativeProjectIndex.FindActor(id)?.displayName;
                    float hue = (id.GetHashCode() & 0x7fffffff) % 1000 / 1000f;
                    list.Add(new NarrativeWorldMap.Character
                    {
                        actorId = id,
                        name = string.IsNullOrEmpty(name) ? (id.StartsWith("NPC_") ? id.Substring(4) : id) : name,
                        color = Color.HSVToRGB(hue, 0.55f, 0.85f),
                    });
                }
                return list;
            });

        /// Una pieza «Personaje desde aquí» en una línea: dónde pasa a estar y si dice algo.
        static string AgendaLine(ActorStateNode n)
        {
            string where = n.PieceLayer == ActorStateNode.Layer.BackToUsual ? StoryText.T("vuelve a lo de siempre", "back to usual")
                : n.Presence == ActorPresence.Away ? StoryText.T("se va", "leaves")
                : n.Presence == ActorPresence.AtMark ? StoryText.T($"pasa a {n.MarkId}", $"moves to {n.MarkId}")
                : StoryText.T("sigue donde está", "stays where they are");
            int lines = n.Lines?.Count(l => l != null) ?? 0;
            return lines > 0 ? StoryText.T($"{where} · {lines} frase(s)", $"{where} · {lines} line(s)") : where;
        }

        /// Pasos de este capítulo que vienen después del elegido y cumplen la condición, por orden de cercanía.
        IEnumerable<T> Ahead<T>(Func<T, bool> match, int max) where T : NarrativeNode
        {
            if (_graph == null || _step == null) yield break;
            var seen = new HashSet<string>();
            var queue = new Queue<NarrativeNode>();
            queue.Enqueue(_step);
            int found = 0;
            while (queue.Count > 0 && found < max && seen.Count < 400)
            {
                var n = queue.Dequeue();
                if (n == null || !seen.Add(n.Guid)) continue;
                if (n is T t && match(t)) { found++; yield return t; }
                if (n.Outputs != null) foreach (var o in n.Outputs) queue.Enqueue(_graph.FindNode(o));
            }
        }

        string Num(NarrativeNode n) => _structure != null ? _structure.Number(n) + " " : string.Empty;

        // ── Piezas de la ficha ──────────────────────────────────────────────

        void Title(string text, Color? color)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            if (color.HasValue) { var dot = new VisualElement { style = { width = 12, height = 12, marginRight = 8, backgroundColor = color.Value } }; Round(dot, 6); row.Add(dot); }
            row.Add(new Label(text) { style = { fontSize = 15, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal } });
            _card.Add(row);
        }

        void Section(string text) => _card.Add(new Label(text.ToUpperInvariant()) { style = { marginTop = 12, fontSize = 10, color = new Color(1, 1, 1, 0.5f), unityFontStyleAndWeight = FontStyle.Bold } });

        void Muted(string text) => _card.Add(new Label(text) { style = { whiteSpace = WhiteSpace.Normal, color = new Color(1, 1, 1, 0.75f) } });

        void Link(string text, NarrativeGraph graph, string guid)
        {
            var b = new Button(() => { if (graph != null && !string.IsNullOrEmpty(guid)) StoryGraphWindow.OpenAt(graph, guid); }) { text = text };
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            _card.Add(b);
        }

        void DialogueButton(NarrativeDialogueAsset d, string prefix = null)
        {
            var first = DialoguePreview.Of(d).FirstOrDefault();
            string preview = first != null ? $"{first.Speaker}: {first.Text}" : d.name;
            if (preview.Length > 70) preview = preview.Substring(0, 67) + "…";
            var b = new Button(() => { _dialogue = d; Rebuild(); }) { text = $"▸ {prefix}{preview}", tooltip = d.name };
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            _card.Add(b);
        }

        static void Round(VisualElement el, float r)
            => el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = r;

        static void SetBorder(VisualElement el, Color c, float w)
        {
            el.style.borderTopWidth = el.style.borderBottomWidth = el.style.borderLeftWidth = el.style.borderRightWidth = w;
            el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = c;
        }
    }
}
