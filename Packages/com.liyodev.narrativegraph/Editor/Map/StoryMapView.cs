using System;
using System.Collections.Generic;
using System.Linq;
using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor
{
    /// Mapa de la historia: el mundo en esquema (zonas) con un muñeco por personaje, colocado
    /// según el paso elegido en el editor de la historia. Al pulsar un muñeco se ve lo suyo en ese
    /// paso: dónde está y por qué, si la historia le espera, qué dice si le hablas, sus próximos
    /// diálogos (con emociones) y las misiones que da. Arrastrar un muñeco a otra zona escribe en
    /// el grafo que desde ese paso está allí (pieza «Personaje desde aquí»; el jugador, con un punto
    /// de arranque). La barra de abajo recorre los pasos del capítulo (▶ los reproduce) y una
    /// flecha enseña quién se ha movido respecto al paso anterior. «Editar mapa» deja mover y
    /// crear zonas, asignarles marcas y colocar a cada personaje en su zona de siempre.
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
        const float CanvasW = 1400f, CanvasH = 900f;

        NarrativeWorldMap _map;
        NarrativeGraph _graph;
        string _stepGuid;
        NarrativeNode _step;
        StoryStructure _structure;
        bool _edit;
        bool _onlyMain;
        string _selectedActor;
        string _selectedZone;
        NarrativeDialogueAsset _dialogue;

        VisualElement _canvas;
        VisualElement _card;
        Label _stepLabel;
        ToolbarToggle _editToggle;
        Dictionary<string, Placement> _placements = new();
        Dictionary<string, Placement> _previous = new();
        readonly Dictionary<string, Vector2> _positions = new();
        readonly Dictionary<string, Vector2> _previousPositions = new();
        Label _status;
        SliderInt _slider;
        Label _sliderLabel;
        Button _playButton;
        IVisualElementScheduledItem _playing;

        sealed class Placement
        {
            public NarrativeWorldMap.Zone Zone;
            public bool Away;
            public string Where;
            public string Why;
            public NarrativeGraph WhyGraph;
            public string WhyGuid;
            public ActorStateNode Piece;
            /// Sobre la vista del mundo: el punto exacto en el lienzo.
            public bool HasPoint;
            public Vector2 Point;
        }

        // ── Vista del mundo: marcas en su sitio exacto ──────────────────────

        bool WorldMode => _map != null && _map.background != null && _map.hasWorldRect && _map.worldRect.width > 0;

        /// Dónde queda la foto dentro del lienzo (encajada sin deformar).
        Rect PictureRect()
        {
            float aspect = _map.background.width / (float)_map.background.height;
            float w = CanvasW, h = CanvasW / aspect;
            if (h > CanvasH) { h = CanvasH; w = CanvasH * aspect; }
            return new Rect((CanvasW - w) / 2f, (CanvasH - h) / 2f, w, h);
        }

        Vector2 ToCanvas(Vector3 world)
        {
            var pic = PictureRect();
            var wr = _map.worldRect;
            float u = (world.x - wr.xMin) / wr.width, v = (wr.yMax - world.z) / wr.height;
            return new Vector2(pic.x + u * pic.width, pic.y + v * pic.height);
        }

        List<string> MarksNear(Vector2 at, float radius)
        {
            var list = new List<(string mark, float d)>();
            foreach (var m in NarrativeProjectIndex.Anchors)
                if (NarrativeWorldPositions.Mark(m, out var w)) { float d = Vector2.Distance(ToCanvas(w), at); if (d <= radius) list.Add((m, d)); }
            return list.OrderBy(x => x.d).Take(8).Select(x => x.mark).ToList();
        }

        /// El nombre del sitio de una marca: la zona (si hay) donde cae, o la marca.
        string PlaceName(string mark)
        {
            if (!WorldMode || !NarrativeWorldPositions.Mark(mark, out var w)) return _map.ZoneOfMark(mark)?.name ?? mark;
            var pt = ToCanvas(w);
            return _map.zones.LastOrDefault(z => z.rect.Contains(pt))?.name ?? _map.ZoneOfMark(mark)?.name ?? mark;
        }

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
                if (_map != null) AssetDatabase.SaveAssetIfDirty(_map);
            });
        }

        /// Elige un personaje (como si se pulsara su muñeco).
        public void SelectActor(string actorId)
        {
            _selectedActor = actorId;
            _selectedZone = null;
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
            _status = new Label { style = { alignSelf = Align.Center, color = new Color(1f, 0.8f, 0.5f), marginLeft = 8 } };
            bar.Add(_status);
            bar.Add(new ToolbarSpacer { flex = true });
            _editToggle = new ToolbarToggle { text = StoryText.T("Editar mapa", "Edit map") };
            _editToggle.RegisterValueChangedCallback(e => { _edit = e.newValue; _selectedZone = null; Rebuild(); });
            bar.Add(_editToggle);
            bar.Add(new ToolbarButton(AddZone) { text = "+ " + StoryText.T("Zona", "Zone") });
            bar.Add(new ToolbarButton(BakeBackground) { text = StoryText.T("Hacer vista desde arriba", "Make top-down view"), tooltip = StoryText.T("Fotografía desde arriba la escena abierta y la pone de fondo del mapa.", "Photographs the open scene from above and uses it as the map background.") });
            bar.Add(new ToolbarButton(ShowCharacterMenu) { text = "+ " + StoryText.T("Personaje", "Character") });
            root.Add(bar);

            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { style = { flexGrow = 1 } };
            _canvas = new VisualElement { style = { width = CanvasW, height = CanvasH, backgroundColor = new Color(0.11f, 0.12f, 0.14f) } };
            _canvas.RegisterCallback<PointerDownEvent>(e => { if (e.target == _canvas) { _selectedActor = null; _selectedZone = null; _dialogue = null; Rebuild(); } });
            scroll.Add(_canvas);
            body.Add(scroll);
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
            _canvas.generateVisualContent += DrawMoves;

            if (Compact)
            {
                // Compacta: solo el dibujo, encogido para que quepa entero en el hueco.
                bar.style.display = DisplayStyle.None;
                steps.style.display = DisplayStyle.None;
                _card.style.display = DisplayStyle.None;
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                _canvas.style.transformOrigin = new TransformOrigin(0, 0);
                scroll.RegisterCallback<GeometryChangedEvent>(e =>
                {
                    float k = Mathf.Max(0.05f, Mathf.Min(e.newRect.width / CanvasW, e.newRect.height / CanvasH));
                    _canvas.style.scale = new Scale(new Vector3(k, k, 1));
                });
            }

            _map = NarrativeWorldMap.Find();
            if (_step == null && _graph != null && !string.IsNullOrEmpty(_stepGuid)) _step = _graph.FindNode(_stepGuid);
            if (_graph == null && NarrativeProjectIndex.Graphs.FirstOrDefault(g => g != null) is { } first) _graph = first;
            if (_graph != null && _structure == null) _structure = StoryStructure.Build(_graph);
            Rebuild();
        }

        // ── Dónde está cada uno ─────────────────────────────────────────────

        Dictionary<string, Placement> Compute(NarrativeNode step)
        {
            var placements = new Dictionary<string, Placement>();
            if (_map == null) return placements;
            RecordingStateWriter story = null;
            var pieces = new Dictionary<string, ActorStateFacts.Entry>();
            string playerMark = null;
            if (_graph != null && step != null)
            {
                var plan = NarrativeStartPlanner.Plan(_graph, step.guid);
                story = plan.Story;
                foreach (var e in ActorStateFacts.From(story)) pieces[e.Actor] = e;
                var sheet = plan.Sheet;
                if (sheet != null && sheet.IsReady && !string.IsNullOrEmpty(sheet.positionField))
                    using (var preview = plan.Build(story: true, points: true, testPlayer: true))
                        if (preview.Data != null) playerMark = new SerializedObject(preview.Data).FindProperty(sheet.positionField)?.stringValue;
            }
            string offStage = StoryText.T("fuera de escena", "off stage");

            foreach (var c in _map.characters)
            {
                var p = new Placement();
                string mark = null;
                if (pieces.TryGetValue(c.actorId, out var e) && e.Node.presence != ActorPresence.Unchanged)
                {
                    p.Piece = e.Node;
                    p.Away = e.Node.presence == ActorPresence.Away;
                    mark = e.Node.markId;
                    SetWhy(p, e.Graph, e.Source ?? e.Node.guid);
                }
                else if (c.actorId == "Player" && !string.IsNullOrEmpty(playerMark))
                {
                    // El jugador está donde le deja el arranque de la prueba: la historia con sus
                    // puntos (◆) en su sitio, que es lo que se usa al probar desde este paso.
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
                    if (mark == null && c.actorId == "Player" && !string.IsNullOrEmpty(playerMark)) mark = playerMark;
                }
                if (WorldMode && !p.Away)
                {
                    if (mark != null && NarrativeWorldPositions.Mark(mark, out var wp)) { p.HasPoint = true; p.Point = ToCanvas(wp); }
                    else if (NarrativeWorldPositions.Home(c.actorId, out var hp)) { p.HasPoint = true; p.Point = ToCanvas(hp); mark = null; }
                }
                if (p.HasPoint)
                {
                    p.Where = mark != null ? $"{PlaceName(mark)} · {mark}" : StoryText.T("Su sitio de siempre", "Their usual place");
                    placements[c.actorId] = p;
                    continue;
                }
                p.Zone = _map.ZoneOfMark(mark) ?? _map.ZoneById(c.homeZone);
                p.Where = p.Away ? StoryText.T("No está en escena", "Not on stage")
                        : p.Zone == null ? StoryText.T("Sin zona en el mapa", "No zone on the map") + (mark != null ? $" ({mark})" : "")
                        : p.Zone.name + (mark != null && _map.ZoneOfMark(mark) != null ? $" · {mark}" : StoryText.T(" · de siempre", " · usual place"));
                placements[c.actorId] = p;
            }
            return placements;
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

        void Rebuild()
        {
            var guid = _step != null ? _step.guid : null;
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
                var create = new Button(() => { _map = NarrativeWorldMap.Create(); _editToggle.value = true; }) { text = StoryText.T("Crear el mapa", "Create the map") };
                _card.Add(create);
                return;
            }

            _placements = Compute(_step);
            var previousStep = PreviousStep();
            _previous = previousStep != null ? Compute(previousStep) : new Dictionary<string, Placement>();
            Layout(_placements, _positions, out int away, out int homeless);
            Layout(_previous, _previousPositions, out _, out _);
            RefreshSlider();

            bool picture = _map.background != null;
            _canvas.style.backgroundImage = picture ? new StyleBackground(_map.background) : new StyleBackground(StyleKeyword.None);
            _canvas.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _canvas.style.backgroundColor = picture ? new Color(0.04f, 0.13f, 0.24f) : new Color(0.11f, 0.12f, 0.14f);
            foreach (var zone in _map.zones) _canvas.Add(ZoneElement(zone));
            _canvas.Add(Tray(StoryText.T("No están en escena (suelta aquí a quien se va)", "Not on stage (drop here whoever leaves)"), 800));
            if (homeless > 0) _canvas.Add(Tray(StoryText.T("Sin zona: ponles su zona de siempre (Editar mapa, arrástralos)", "No zone: set their usual zone (Edit map, drag them)"), 720));
            foreach (var c in _map.characters)
                if (_positions.TryGetValue(c.actorId, out var pos)) _canvas.Add(Token(c, _placements[c.actorId], pos));
            _canvas.MarkDirtyRepaint();

            if (_edit && _selectedZone != null && _map.ZoneById(_selectedZone) is { } z) ZoneCard(z);
            else if (_selectedActor != null && _map.CharacterOf(_selectedActor) is { } ch)
            {
                if (_edit) CharacterEditCard(ch);
                else if (_dialogue != null) DialogueCard(ch);
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
            foreach (var c in _map.characters)
            {
                if (_onlyMain && !c.main && c.actorId != _selectedActor) continue;
                if (!placements.TryGetValue(c.actorId, out var p)) continue;
                if (p.HasPoint)
                {
                    // La punta de la chincheta en el punto; si coinciden varios, en fila.
                    var key = $"{Mathf.RoundToInt(p.Point.x / 12f)}:{Mathf.RoundToInt(p.Point.y / 12f)}";
                    byZone.TryGetValue(key, out var k);
                    byZone[key] = k + 1;
                    positions[c.actorId] = p.Point + new Vector2(-TokenW / 2f + k * (TokenW - 8), -50f);
                    continue;
                }
                if (p.Away || p.Zone == null)
                {
                    int i = p.Away ? away++ : homeless++;
                    positions[c.actorId] = new Vector2(30 + i * (TokenW + 10), p.Away ? 820 : 740);
                    continue;
                }
                byZone.TryGetValue(p.Zone.id, out var n);
                byZone[p.Zone.id] = n + 1;
                int perRow = Mathf.Max(1, (int)((p.Zone.rect.width - 20) / (TokenW + 6)));
                positions[c.actorId] = new Vector2(p.Zone.rect.x + 12 + (n % perRow) * (TokenW + 6), p.Zone.rect.y + 34 + (n / perRow) * (TokenH + 6));
            }
        }

        /// Flechas de quien ha cambiado de sitio respecto al paso anterior.
        void DrawMoves(MeshGenerationContext ctx)
        {
            if (_edit || _map == null) return;
            var p2 = ctx.painter2D;
            foreach (var c in _map.characters)
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

        VisualElement ZoneElement(NarrativeWorldMap.Zone zone)
        {
            var el = new VisualElement();
            var r = zone.rect;
            // Sobre la vista del mundo, el nombre de la zona va en medio de sus marcas: sin colocarla a mano.
            if (WorldMode && !_edit)
            {
                var pts = zone.marks.Select(m => NarrativeWorldPositions.Mark(m, out var w) ? (Vector2?)ToCanvas(w) : null).Where(v => v.HasValue).Select(v => v.Value).ToList();
                if (pts.Count > 0)
                {
                    var c = new Vector2(pts.Average(v => v.x), pts.Average(v => v.y));
                    r = new Rect(c.x - 100f, c.y + 6f, 200f, 40f);
                }
            }
            el.style.position = Position.Absolute;
            el.style.left = r.x; el.style.top = r.y; el.style.width = r.width; el.style.height = r.height;
            // Sobre una vista del mundo, la zona es solo su nombre (como en un mapa de juego); su
            // contorno se ve al editar el mapa.
            bool picture = _map.background != null && !_edit;
            var fill = zone.color; fill.a = picture ? 0f : 0.16f;
            el.style.backgroundColor = fill;
            SetBorder(el, picture ? Color.clear : zone.color, _edit && _selectedZone == zone.id ? 3 : picture ? 0 : 2);
            float radius = zone.round ? Mathf.Min(r.width, r.height) * 0.5f : 12f;
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = radius;
            var name = new Label(zone.name) { pickingMode = PickingMode.Ignore };
            name.style.unityTextAlign = TextAnchor.UpperCenter;
            name.style.marginTop = 8;
            name.style.color = picture ? new Color(1f, 0.97f, 0.88f) : Color.Lerp(zone.color, Color.white, 0.55f);
            name.style.unityFontStyleAndWeight = picture ? FontStyle.BoldAndItalic : FontStyle.Bold;
            if (picture)
            {
                name.style.fontSize = 15;
                name.style.textShadow = new TextShadow { offset = new Vector2(1, 1), blurRadius = 3, color = new Color(0, 0, 0, 0.9f) };
            }
            el.Add(name);
            if (_edit)
            {
                foreach (var m in zone.marks)
                {
                    var chip = new Label(m) { pickingMode = PickingMode.Ignore };
                    chip.style.fontSize = 9; chip.style.color = new Color(1, 1, 1, 0.5f); chip.style.unityTextAlign = TextAnchor.MiddleCenter;
                    el.Add(chip);
                }
                var handle = new VisualElement { style = { position = Position.Absolute, right = 2, bottom = 2, width = 12, height = 12, backgroundColor = zone.color } };
                el.Add(handle);
                void Apply() { el.style.left = zone.rect.x; el.style.top = zone.rect.y; el.style.width = zone.rect.width; el.style.height = zone.rect.height; }
                Draggable(handle, d => { zone.rect.width = Mathf.Max(80, zone.rect.width + d.x); zone.rect.height = Mathf.Max(60, zone.rect.height + d.y); Apply(); }, null);
                Draggable(el, d => { zone.rect.x = Mathf.Max(0, zone.rect.x + d.x); zone.rect.y = Mathf.Max(0, zone.rect.y + d.y); Apply(); }, () => { _selectedZone = zone.id; _selectedActor = null; });
            }
            return el;
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
            var face = StoryCardImages.PortraitOf(c.actorId);
            if (face != null) ring.Add(new Image { image = face, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore, style = { flexGrow = 1 } });
            else ring.Add(new Label(string.IsNullOrEmpty(c.name) ? "?" : c.name.Substring(0, 1)) { pickingMode = PickingMode.Ignore, style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter, fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold, color = Color.white } });
            el.Add(ring);
            var tip = new VisualElement { pickingMode = PickingMode.Ignore, style = { width = 0, height = 0, borderLeftWidth = 6, borderRightWidth = 6, borderTopWidth = 8, borderLeftColor = Color.clear, borderRightColor = Color.clear, borderTopColor = sel ? Color.white : col } };
            el.Add(tip);
            if (p.Piece != null && p.Piece.lines != null && p.Piece.lines.Any(l => l != null))
            {
                var talk = new Label("…") { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, right = 0, top = -6, fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.1f, 0.08f, 0.02f), backgroundColor = new Color(0.95f, 0.71f, 0.27f), paddingLeft = 3, paddingRight = 3 } };
                Round(talk, 6);
                el.Add(talk);
            }
            var label = new Label(c.name) { pickingMode = PickingMode.Ignore, style = { fontSize = 10, maxWidth = TokenW + 10, marginTop = 1, paddingLeft = 4, paddingRight = 4, backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.85f), overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, color = Color.white, unityTextAlign = TextAnchor.UpperCenter, whiteSpace = WhiteSpace.NoWrap } };
            Round(label, 4);
            el.Add(label);

            if (Compact)
                el.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) { ActorClicked?.Invoke(c.actorId); e.StopPropagation(); } });
            else if (_edit)
                Draggable(el, d => { el.style.left = el.resolvedStyle.left + d.x; el.style.top = el.resolvedStyle.top + d.y; },
                          () => { _selectedActor = c.actorId; _selectedZone = null; },
                          end => DropCharacter(c, end));
            else
                Draggable(el, d => { el.style.left = el.resolvedStyle.left + d.x; el.style.top = el.resolvedStyle.top + d.y; },
                          () => { _selectedActor = c.actorId; _dialogue = null; },
                          end => MoveInStory(c, p, end));
            return el;
        }

        void DropCharacter(NarrativeWorldMap.Character c, Vector2 at)
        {
            var zone = _map.zones.LastOrDefault(z => z.rect.Contains(at));
            if (zone == null) return;
            Undo.RecordObject(_map, StoryText.T("Zona de siempre", "Usual zone"));
            c.homeZone = zone.id;
            EditorUtility.SetDirty(_map);
        }

        /// Arrastrar con el ratón: <paramref name="move"/> recibe cuánto se ha movido; al soltar
        /// se guarda el mapa. <paramref name="click"/> se llama al pulsar.
        void Draggable(VisualElement el, Action<Vector2> move, Action click, Action<Vector2> drop = null)
        {
            bool dragging = false;
            Vector2 last = default, start = default;
            el.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                Undo.RecordObject(_map, StoryText.T("Mover en el mapa", "Move on the map"));
                dragging = true;
                last = start = e.position;
                el.CapturePointer(e.pointerId);
                click?.Invoke();
                e.StopPropagation();
            });
            el.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return;
                var d = (Vector2)e.position - last;
                last = e.position;
                move(d);
                e.StopPropagation();
            });
            el.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging) return;
                dragging = false;
                el.ReleasePointer(e.pointerId);
                // Un clic sin mover no suelta nada (solo elige).
                if (((Vector2)e.position - start).sqrMagnitude > 36f) drop?.Invoke(_canvas.WorldToLocal(e.position));
                EditorUtility.SetDirty(_map);
                AssetDatabase.SaveAssetIfDirty(_map);
                Rebuild();
                e.StopPropagation();
            });
        }

        // ── Fichas ──────────────────────────────────────────────────────────

        void HelpCard()
        {
            Title(StoryText.T("Mapa de la historia", "Story map"), null);
            Muted(_edit
                ? StoryText.T("Arrastra las zonas para colocarlas (la esquina cambia el tamaño) y pulsa una para ponerle nombre, color y marcas. Arrastra un muñeco a una zona para que sea su sitio de siempre.",
                              "Drag zones to place them (the corner resizes) and click one to set its name, colour and marks. Drag a character onto a zone to make it their usual place.")
                : StoryText.T("Elige un paso en el editor de la historia: los muñecos se colocan donde están en ese momento. Pulsa uno para ver lo suyo. «…» = tiene algo que decir.",
                              "Pick a step in the story editor: characters move to where they are at that moment. Click one to see their things. “…” = has something to say."));
        }

        void CharacterCard(NarrativeWorldMap.Character c)
        {
            var p = _placements[c.actorId];
            Title(c.name, c.color);
            Section(StoryText.T("Dónde está", "Where"));
            Muted(p.Where);
            if (p.Why != null) Link(StoryText.T("Lo dejó ", "Set by ") + p.Why, p.WhyGraph, p.WhyGuid);

            var waits = Ahead<WaitNpcInteractionNode>(n => n.npcId == c.actorId, 1).FirstOrDefault();
            if (waits != null)
            {
                Section(StoryText.T("La historia le espera", "The story is waiting for them"));
                Link(Num(waits) + "«" + NarrativeStartPlanner.Title(waits) + "»", _graph, waits.guid);
            }

            Section(StoryText.T("Si le hablas ahora", "If you talk to them now"));
            var lines = p.Piece?.lines?.Where(l => l != null).ToList();
            if (lines == null || lines.Count == 0) Muted(StoryText.T("No dice nada propio.", "Nothing of their own."));
            else
            {
                Muted(StoryText.T($"«{NarrativeStartPlanner.Title(p.Piece)}» (una por charla, en orden)", $"“{NarrativeStartPlanner.Title(p.Piece)}” (one per chat, in order)"));
                foreach (var l in lines) DialogueButton(l);
            }

            var next = Ahead<PlayDialogueNode>(n => n.npcId == c.actorId && n.dialogue != null, 3).ToList();
            if (next.Count > 0)
            {
                Section(StoryText.T("Próximos diálogos", "Next dialogues"));
                foreach (var n in next) { DialogueButton(n.dialogue, Num(n)); }
            }

            var quests = _graph?.nodes?.OfType<StartQuestNode>().Where(q => q.giverId == c.actorId && !string.IsNullOrEmpty(q.questId)).ToList();
            if (quests != null && quests.Count > 0)
            {
                Section(StoryText.T("Misiones que da", "Quests they give"));
                var story = _step != null ? NarrativeStartPlanner.Plan(_graph, _step.guid).Story : null;
                foreach (var q in quests)
                {
                    string state = story != null && story.Quests.TryGetValue(q.questId, out var s)
                        ? (s.value == RecordingStateWriter.QuestDone ? StoryText.T("hecha", "done") : StoryText.T("en curso", "active"))
                        : StoryText.T("sin empezar", "not started");
                    Link($"{NarrativeProjectIndex.FindQuest(q.questId)?.displayName ?? q.questId} · {state}", _graph, q.guid);
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

        void ZoneCard(NarrativeWorldMap.Zone z)
        {
            Title(StoryText.T("Zona", "Zone"), z.color);
            var so = new SerializedObject(_map);
            int i = _map.zones.IndexOf(z);
            var prop = so.FindProperty("zones").GetArrayElementAtIndex(i);
            foreach (var f in new[] { "name", "color", "round" })
            {
                var pf = new PropertyField(prop.FindPropertyRelative(f));
                pf.Bind(so);
                pf.RegisterValueChangeCallback(_ => { EditorUtility.SetDirty(_map); RebuildCanvasOnly(); });
                _card.Add(pf);
            }
            Section(StoryText.T("Marcas del mundo en esta zona", "World marks in this zone"));
            foreach (var m in z.marks.ToList())
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
                row.Add(new Label(m));
                row.Add(new Button(() => { Undo.RecordObject(_map, "Marca"); z.marks.Remove(m); EditorUtility.SetDirty(_map); Rebuild(); }) { text = "×" });
                _card.Add(row);
            }
            var used = new HashSet<string>(_map.zones.SelectMany(x => x.marks));
            var free = UsedMarks().Concat(NarrativeProjectIndex.Anchors).Where(a => !used.Contains(a)).Distinct().ToList();
            _card.Add(new Button(() =>
            {
                var menu = new GenericMenu();
                foreach (var mark in free.Take(400)) { var a = mark; menu.AddItem(new GUIContent(a.Replace('/', '∕')), false, () => { Undo.RecordObject(_map, "Marca"); z.marks.Add(a); EditorUtility.SetDirty(_map); Rebuild(); }); }
                menu.ShowAsContext();
            }) { text = "+ " + StoryText.T("Marca", "Mark") });
            var del = new Button(() => { Undo.RecordObject(_map, "Quitar zona"); _map.zones.Remove(z); _selectedZone = null; EditorUtility.SetDirty(_map); Rebuild(); }) { text = StoryText.T("Quitar la zona", "Remove the zone") };
            del.style.marginTop = 14;
            _card.Add(del);
        }

        void CharacterEditCard(NarrativeWorldMap.Character c)
        {
            Title(c.name, c.color);
            var so = new SerializedObject(_map);
            var prop = so.FindProperty("characters").GetArrayElementAtIndex(_map.characters.IndexOf(c));
            foreach (var f in new[] { "name", "color", "main" })
            {
                var pf = new PropertyField(prop.FindPropertyRelative(f));
                pf.Bind(so);
                pf.RegisterValueChangeCallback(_ => { EditorUtility.SetDirty(_map); RebuildCanvasOnly(); });
                _card.Add(pf);
            }
            Muted(StoryText.T($"Id: {c.actorId}. Arrástralo a una zona para que sea su sitio de siempre.", $"Id: {c.actorId}. Drag it onto a zone to make it their usual place."));
            var del = new Button(() => { Undo.RecordObject(_map, "Quitar personaje"); _map.characters.Remove(c); _selectedActor = null; EditorUtility.SetDirty(_map); Rebuild(); }) { text = StoryText.T("Quitar del mapa", "Remove from the map") };
            del.style.marginTop = 14;
            _card.Add(del);
        }

        // ── Mover en la historia ────────────────────────────────────────────

        /// Suelta un muñeco en el paso elegido: escribe en el grafo que desde aquí está en esa zona
        /// (o que no está, si se suelta en la bandeja de abajo).
        void MoveInStory(NarrativeWorldMap.Character c, Placement from, Vector2 at)
        {
            if (_graph == null || _step == null) { Status(StoryText.T("Elige antes un paso en el editor de la historia.", "Pick a step in the story editor first.")); return; }
            bool toAway = at.y >= 780;
            if (!toAway && WorldMode)
            {
                // Sobre la vista del mundo: las marcas que hay cerca de donde se suelta.
                var near = MarksNear(at, 90f);
                if (near.Count == 0) { Status(StoryText.T("No hay ninguna marca del mundo cerca de ahí.", "There is no world mark near there.")); return; }
                if (near.Count == 1) { WriteMove(c, PlaceName(near[0]), near[0]); return; }
                var marks = new GenericMenu();
                foreach (var m in near) { var mark = m; marks.AddItem(new GUIContent(mark), false, () => WriteMove(c, PlaceName(mark), mark)); }
                marks.ShowAsContext();
                return;
            }
            var zone = toAway ? null : _map.zones.LastOrDefault(z => z.rect.Contains(at));
            if (!toAway && (zone == null || zone == from.Zone && !from.Away)) return;
            if (toAway && from.Away) return;
            if (toAway && c.actorId == "Player") { Status(StoryText.T("El jugador siempre está en escena.", "The player is always on stage.")); return; }
            if (zone != null && zone.marks.Count == 0)
            {
                Status(StoryText.T($"«{zone.name}» no tiene marcas del mundo: añádele una con «Editar mapa».", $"“{zone.name}” has no world marks: add one with “Edit map”."));
                return;
            }
            if (zone == null || zone.marks.Count == 1) { WriteMove(c, zone?.name, zone?.marks[0]); return; }
            var menu = new GenericMenu();
            foreach (var m in zone.marks) { var mark = m; menu.AddItem(new GUIContent(mark), false, () => WriteMove(c, zone.name, mark)); }
            menu.ShowAsContext();
        }

        void WriteMove(NarrativeWorldMap.Character c, string place, string mark)
        {
            var edit = StoryGraphWindow.EditingOf(_graph);
            if (edit == null) { Status(StoryText.T("Abre este grafo en el editor de la historia para poder cambiarlo.", "Open this graph in the story editor to change it.")); return; }
            string where = mark != null ? place : StoryText.T("fuera de escena", "off stage");
            // Crear, enganchar y rellenar la pieza es un solo paso de deshacer.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            if (c.actorId == "Player") MovePlayer(edit, c, place, mark);
            else MoveCharacter(edit, c, place, mark);
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName(StoryText.T($"Mover a {c.name}", $"Move {c.name}"));
            Status(StoryText.T($"Desde {Num(_step)}: {c.name} → {where}. Se puede deshacer.", $"From {Num(_step)}: {c.name} → {where}. Can be undone."));
            StoryGraphWindow.SelectStep(_graph, _step.guid);
            Rebuild();
        }

        /// El paso justo antes del elegido, si es una pieza que se puede reutilizar.
        T PieceRightBefore<T>(Func<T, bool> match) where T : NarrativeNode
        {
            if (_step is T own && match(own)) return own;
            return _graph.nodes.OfType<T>().FirstOrDefault(n => match(n) && n.outputs != null && n.outputs.Count == 1 && n.outputs[0] == _step.guid);
        }

        void MoveCharacter(StoryGraphEditing edit, NarrativeWorldMap.Character c, string place, string mark)
        {
            var piece = PieceRightBefore<ActorStateNode>(n => n.actorId == c.actorId && n.layer != ActorStateNode.Layer.BackToUsual);
            string title = mark != null ? StoryText.T($"{c.name} pasa a {place}", $"{c.name} goes to {place}") : StoryText.T($"{c.name} se va", $"{c.name} leaves");
            if (piece == null)
            {
                piece = (ActorStateNode)edit.CreateStep(typeof(ActorStateNode), _step.position - new Vector2(StoryLayout.CardWidth + 60f, -40f), _step.section);
                if (piece == null) return;
                // Lo que ya decía si le hablabas, se mantiene.
                if (_placements.TryGetValue(c.actorId, out var p) && p.Piece?.lines != null) piece.lines = new List<NarrativeDialogueAsset>(p.Piece.lines);
                InsertAt(edit, piece);
            }
            var target = piece;
            edit.Modify(StoryText.T("Mover personaje", "Move character"), () =>
            {
                target.actorId = c.actorId;
                target.layer = ActorStateNode.Layer.FromHere;
                target.presence = mark != null ? ActorPresence.AtMark : ActorPresence.Away;
                target.markId = mark ?? string.Empty;
                target.displayTitle = title;
            });
        }

        void MovePlayer(StoryGraphEditing edit, NarrativeWorldMap.Character c, string place, string mark)
        {
            var sheet = NarrativePlayerSheet.Find();
            if (sheet == null || sheet.DataType == null || string.IsNullOrEmpty(sheet.positionField))
            {
                Status(StoryText.T("Falta la ficha del jugador o su campo de posición (⚙ Ajustes).", "The player sheet or its position field is missing (⚙ Settings)."));
                return;
            }
            var point = PieceRightBefore<CheckpointNode>(_ => true);
            if (point == null)
            {
                point = (CheckpointNode)edit.CreateStep(typeof(CheckpointNode), _step.position - new Vector2(StoryLayout.CardWidth + 60f, -40f), _step.section);
                if (point == null) return;
                point.checkpointId = "PUNTO_" + point.guid.Substring(0, 6).ToUpperInvariant();
                InsertAt(edit, point);
            }
            var target = point;
            edit.Modify(StoryText.T("Mover al jugador", "Move the player"), () =>
            {
                target.player ??= new NarrativePlayerValues();
                NarrativePlayerValuesEditor.SetValue(target.player, sheet.DataType, sheet.positionField, prop => prop.stringValue = mark);
                if (string.IsNullOrEmpty(target.displayTitle) || target.displayTitle.StartsWith(c.name + " aparece"))
                    target.displayTitle = StoryText.T($"{c.name} aparece en {place}", $"{c.name} appears at {place}");
            });
        }

        /// La pieza nueva va justo antes del paso elegido («al llegar aquí, ya está así»); si el
        /// paso es la entrada del grafo, justo después.
        void InsertAt(StoryGraphEditing edit, NarrativeNode piece)
        {
            if (_step.IsGraphEntry && _step.outputs != null && _step.outputs.Count == 1 && !_step.HasNamedOutputs)
                edit.InsertBetween(_step, 0, piece);
            else edit.InsertBefore(_step, piece);
        }

        void Status(string text)
        {
            if (_status == null) return;
            _status.text = text;
            schedule.Execute(() => { if (_status.text == text) _status.text = string.Empty; }).StartingIn(6000);
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
            StoryGraphWindow.SelectStep(_graph, _step.guid);
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

        void BakeBackground()
        {
            if (_map == null) _map = NarrativeWorldMap.Create();
            var tex = NarrativeWorldMapBaker.TopDown(_map);
            Status(tex != null ? StoryText.T("Vista desde arriba hecha: coloca las zonas encima con «Editar mapa».", "Top-down view done: place the zones over it with “Edit map”.")
                               : StoryText.T("No hay nada en la escena abierta que fotografiar.", "There is nothing in the open scene to photograph."));
            _editToggle.value = true;
            Rebuild();
        }

        void RebuildCanvasOnly() => schedule.Execute(Rebuild).StartingIn(150);

        // ── Acciones ────────────────────────────────────────────────────────

        void AddZone()
        {
            if (_map == null) _map = NarrativeWorldMap.Create();
            Undo.RecordObject(_map, StoryText.T("Nueva zona", "New zone"));
            var z = new NarrativeWorldMap.Zone { name = StoryText.T("Zona nueva", "New zone"), rect = new Rect(60 + _map.zones.Count * 20, 60 + _map.zones.Count * 20, 220, 140) };
            _map.zones.Add(z);
            EditorUtility.SetDirty(_map);
            _editToggle.value = true;
            _selectedZone = z.id;
            Rebuild();
        }

        void ShowCharacterMenu()
        {
            if (_map == null) _map = NarrativeWorldMap.Create();
            var have = new HashSet<string>(_map.characters.Select(c => c.actorId));
            var ids = NarrativeProjectIndex.Actors.Keys.Concat(UsedActors()).Where(id => !string.IsNullOrEmpty(id) && !have.Contains(id)).Distinct().OrderBy(x => x).ToList();
            var menu = new GenericMenu();
            foreach (var actor in ids)
            {
                var id = actor;
                menu.AddItem(new GUIContent(id), false, () =>
                {
                    Undo.RecordObject(_map, StoryText.T("Nuevo personaje", "New character"));
                    var name = NarrativeProjectIndex.FindActor(id)?.displayName;
                    _map.characters.Add(new NarrativeWorldMap.Character
                    {
                        actorId = id,
                        name = string.IsNullOrEmpty(name) ? (id.StartsWith("NPC_") ? id.Substring(4) : id) : name,
                        color = Color.HSVToRGB(UnityEngine.Random.value, 0.55f, 0.85f),
                    });
                    EditorUtility.SetDirty(_map);
                    _selectedActor = id;
                    Rebuild();
                });
            }
            menu.ShowAsContext();
        }

        IEnumerable<string> UsedActors()
            => NarrativeProjectIndex.Graphs.Where(g => g?.nodes != null).SelectMany(g => g.nodes)
                .Select(n => n switch { ActorStateNode a => a.actorId, PlaceActorNode p => p.actorId, WaitNpcInteractionNode w => w.npcId, PlayDialogueNode d => d.npcId, _ => null });

        IEnumerable<string> UsedMarks()
            => NarrativeProjectIndex.Graphs.Where(g => g?.nodes != null).SelectMany(g => g.nodes)
                .Select(n => n switch { ActorStateNode a => a.markId, PlaceActorNode p => p.markId, _ => null })
                .Where(m => !string.IsNullOrEmpty(m));

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
                if (n == null || !seen.Add(n.guid)) continue;
                if (n is T t && match(t)) { found++; yield return t; }
                if (n.outputs != null) foreach (var o in n.outputs) queue.Enqueue(_graph.FindNode(o));
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
