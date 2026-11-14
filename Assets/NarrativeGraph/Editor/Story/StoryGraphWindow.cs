using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// El editor de la historia: índice del capítulo a la izquierda, lienzo en el centro y, a la
    /// derecha, el paso elegido (Paso), cómo empieza la prueba desde él (Arranque) y cómo está el
    /// mundo al llegar (Depuración). El asistente, si está activo, se abre con su botón de la barra.
    public sealed class StoryGraphWindow : EditorWindow
    {
        const string StylesPath = "Assets/NarrativeGraph/Editor/Story/StoryEditor.uss";
        const string FieldStylesPath = "Assets/NarrativeGraph/Editor/Story/StoryFields.uss";
        const string PrefLastGraph = "NarrativeGraph.LastGraph";
        const string PrefLastStep = "NarrativeGraph.LastStep.";
        const string PrefLastTab = "NarrativeGraph.Editor.LastTab";

        NarrativeGraph _graph;
        SerializedObject _so;
        StoryGraphEditing _edit;

        StoryCanvas _canvas;
        StoryOutline _outline;
        NarrativeInspectorPanel _inspector;
        VisualElement _stepTab;
        StoryAssistantPanel _assistant;
        VisualElement _stepWarnings;
        StoryWorldPanel _world;
        StoryStartPanel _start;
        Button _assistantButton;
        Button _settingsButton;
        StoryTour _tour;
        VisualElement _shownTab;
        VisualElement _side;
        VisualElement _tabsBar;
        readonly List<(Button button, VisualElement content)> _tabs = new();
        readonly List<NarrativeGraphSideTab> _gameTabs = new();

        ToolbarMenu _graphMenu;
        TextField _search;
        Label _searchCount;
        Button _status;
        Label _saved;
        Button _play;
        Button _stop;

        HashSet<string> _pathNodes = new();
        bool _savePending;
        bool _wasPlaying;
        bool _editingField;

        sealed class Host : INarrativeNodeHost
        {
            public NarrativeNode Model { get; set; }
        }

        [MenuItem("Tools/Narrative Graph/Abrir editor")]
        public static StoryGraphWindow Open()
        {
            // Opens as a tab next to the Scene view, where there is room for the canvas.
            var w = GetWindow<StoryGraphWindow>(StoryText.T("Historia", "Story"), true, typeof(SceneView));
            w.minSize = new Vector2(980, 560);
            w.Show();
            return w;
        }

        public static void OpenGraph(NarrativeGraph graph) => Open().Load(graph);

        /// Opens <paramref name="graph"/> with <paramref name="stepGuid"/> selected and in view.
        public static StoryGraphWindow OpenAt(NarrativeGraph graph, string stepGuid)
        {
            var w = Open();
            w.Load(graph);
            if (!string.IsNullOrEmpty(stepGuid)) w.Navigate(stepGuid);
            return w;
        }

        /// Opens the step and starts the game from it, as "Play from here" does.
        public static void PlayFromStep(NarrativeGraph graph, string stepGuid)
        {
            var w = OpenAt(graph, stepGuid);
            var node = graph != null ? graph.FindNode(stepGuid) : null;
            if (node != null) w.PlayFrom(node);
        }

        [UnityEditor.Callbacks.OnOpenAsset]
        static bool OnOpenAsset(EntityId entityId, int line)
        {
            if (EditorUtility.EntityIdToObject(entityId) is not NarrativeGraph g) return false;
            OpenGraph(g);
            return true;
        }

        void OnEnable()
        {
            var root = rootVisualElement;
            root.Clear();
            foreach (var path in new[] { FieldStylesPath, StylesPath })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null) root.styleSheets.Add(sheet);
            }
            root.AddToClassList("story-root");

            BuildToolbar(root);

            var body = new VisualElement();
            body.AddToClassList("story-body");
            root.Add(body);

            _outline = new StoryOutline();
            _outline.StepClicked += n => _canvas.Select(n, true);
            _outline.AddClicked += () => _canvas.OpenAddAtCenter();
            _outline.WordClicked += word => _search.value = word;
            body.Add(_outline);
            body.Add(new StorySplitter(_outline, true, "NarrativeGraph.Editor.OutlineWidth", 240f, 180f, 480f));

            _canvas = new StoryCanvas();
            _canvas.SelectionChanged += OnSelectionChanged;
            _canvas.PlayRequested += n => PlayFrom(n);
            _canvas.AutoLayoutRequested += AutoLayout;
            body.Add(_canvas);

            BuildSide(body);
            body.Insert(body.IndexOf(_side), new StorySplitter(_side, false, "NarrativeGraph.Editor.SideWidth", 360f, 280f, 760f));

            Undo.undoRedoPerformed += OnUndoRedo;
            root.schedule.Execute(Poll).Every(300);

            var last = EditorPrefs.GetString(PrefLastGraph, string.Empty);
            if (!string.IsNullOrEmpty(last)) Load(AssetDatabase.LoadAssetAtPath<NarrativeGraph>(last));
            else Load(null);

            if (!StoryTour.Seen) root.schedule.Execute(StartTour).StartingIn(400);
        }

        /// Abre el editor y empieza el recorrido guiado.
        public static void ShowTour() => Open().StartTour();

        void StartTour()
        {
            _tour?.RemoveFromHierarchy();
            VisualElement TabButton(VisualElement content) => _tabs.FirstOrDefault(t => t.content == content).button;
            var stops = new List<StoryTour.Stop>
            {
                new() { Title = StoryText.T("El editor de la historia", "The story editor"),
                        Text = StoryText.T("Aquí escribes la historia de tu juego paso a paso y la pruebas desde cualquier punto. Te enseño cada parte en un minuto.",
                                           "Here you write your game's story step by step and test it from any point. Here is each part in a minute.") },
                new() { Target = () => _graphMenu, Title = StoryText.T("Tu capítulo", "Your chapter"),
                        Text = StoryText.T("Cada capítulo es un grafo. Cámbialo aquí o crea uno nuevo.", "Each chapter is a graph. Switch it here or create a new one.") },
                new() { Target = () => _outline, Title = StoryText.T("El índice", "The index"),
                        Text = StoryText.T("El capítulo por secciones y pasos numerados. Los puntos de arranque llevan ◆. «Añadir paso» (o la tecla A) abre el buscador de pasos.",
                                           "The chapter by sections and numbered steps. Start points carry ◆. “Add step” (or the A key) opens the step finder.") },
                new() { Target = () => _canvas, Title = StoryText.T("El lienzo", "The canvas"),
                        Text = StoryText.T("Doble clic en un hueco para añadir un paso. Arrastra desde el borde de una tarjeta para conectar, y usa el + de una flecha para meter un paso en medio. Clic derecho para más opciones.",
                                           "Double-click an empty spot to add a step. Drag from a card's edge to connect, and use an arrow's + to insert a step. Right-click for more.") },
                new() { Target = () => TabButton(_stepTab), OnShow = () => ShowTab(_stepTab), Title = StoryText.T("Paso", "Step"),
                        Text = StoryText.T("Los campos del paso elegido. Los desplegables proponen lo que ya existe en tu proyecto.", "The selected step's fields. Dropdowns offer what already exists in your project.") },
                new() { Target = () => TabButton(_start), OnShow = () => ShowTab(_start), Title = StoryText.T("Arranque", "Start"),
                        Text = StoryText.T("Cómo empezará la prueba desde el paso elegido y de dónde sale cada dato del jugador. Retócalo para probar y pulsa «◆ Fijar aquí» para guardarlo en el grafo.",
                                           "How the test will start from the selected step and where each player value comes from. Tweak it to test and press “◆ Set here” to save it in the graph.") },
                new() { Target = () => TabButton(_world), OnShow = () => ShowTab(_world), Title = StoryText.T("Depuración", "Debug"),
                        Text = StoryText.T("Cómo está la historia al llegar al paso: personajes, misiones, datos y señales. En Play, lo compara con la partida.",
                                           "What the story looks like when reaching the step: characters, quests, data and signals. In Play, it compares with the game.") },
                new() { Target = () => _play, Title = StoryText.T("Jugar", "Play"),
                        Text = StoryText.T("Elige un paso y pulsa aquí: el juego empieza justo ahí, con todo lo anterior ya hecho.", "Pick a step and press here: the game starts right there, with everything before it already done.") },
                new() { Target = () => _settingsButton, Title = StoryText.T("Ajustes y herramientas", "Settings and tools"),
                        Text = StoryText.T("La ficha del jugador, las conexiones con tu juego, el idioma, el asistente y el manual. Este recorrido se puede repetir desde aquí.",
                                           "The player sheet, your game's connections, language, assistant and the manual. This tour can be replayed from here.") },
            };
            _tour = new StoryTour(stops);
            rootVisualElement.Add(_tour);
            _tour.Begin();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            StoryBreakpoints.Hit -= OnBreakpointHit;
            StoryAssistantSettings.Changed -= OnAssistantSettings;
            SaveNow();
        }

        // ── Layout ───────────────────────────────────────────────────────────

        void BuildToolbar(VisualElement root)
        {
            var bar = new VisualElement();
            bar.AddToClassList("story-toolbar");
            root.Add(bar);

            _graphMenu = new ToolbarMenu();
            _graphMenu.AddToClassList("story-toolbar__graph");
            bar.Add(_graphMenu);

            var searchBox = new VisualElement();
            searchBox.AddToClassList("story-toolbar__search");
            _search = new TextField();
            _search.textEdition.placeholder = StoryText.T("Buscar paso, personaje, misión o frase", "Search step, character, quest or line");
            _search.RegisterValueChangedCallback(e =>
            {
                _canvas.SetSearch(e.newValue);
                _searchCount.text = string.IsNullOrWhiteSpace(e.newValue) ? string.Empty : _canvas.CountMatches().ToString();
            });
            searchBox.Add(_search);
            _searchCount = new Label();
            _searchCount.AddToClassList("story-mono");
            searchBox.Add(_searchCount);
            bar.Add(searchBox);

            bar.Add(new VisualElement { name = "spacer" });

            _status = new Button(GoToFirstWarning);
            _status.AddToClassList("story-toolbar__status");
            bar.Add(_status);

            _saved = new Label();
            _saved.AddToClassList("story-toolbar__saved");
            bar.Add(_saved);

            var layout = new Button(AutoLayout) { text = StoryText.T("Auto-ordenar", "Auto-arrange") };
            layout.AddToClassList("story-button");
            bar.Add(layout);

            _assistantButton = new Button(ToggleAssistant)
            {
                text = "✦ " + StoryText.T("Asistente", "Assistant"),
                tooltip = StoryText.T("Abre o cierra el asistente (se activa en ⚙ Ajustes).", "Opens or closes the assistant (turned on in ⚙ Settings)."),
            };
            _assistantButton.AddToClassList("story-button");
            bar.Add(_assistantButton);

            var settings = _settingsButton = new Button(OpenSettings)
            {
                text = "⚙",
                tooltip = StoryText.T("Ajustes y herramientas: idioma, asistente, exportar textos, conexiones del juego…",
                                      "Settings and tools: language, assistant, export texts, game connections…"),
            };
            settings.AddToClassList("story-button");
            settings.AddToClassList("story-toolbar__settings");
            bar.Add(settings);

            _play = new Button(() => PlayFrom(_canvas.Primary));
            _play.AddToClassList("story-button--primary");
            bar.Add(_play);

            _stop = new Button(() => { if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; }) { text = "■" };
            _stop.tooltip = StoryText.T("Parar la prueba", "Stop the test");
            _stop.AddToClassList("story-button");
            bar.Add(_stop);
        }

        void BuildSide(VisualElement body)
        {
            _side = new VisualElement();
            _side.AddToClassList("story-side");
            body.Add(_side);

            _tabsBar = new VisualElement();
            _tabsBar.AddToClassList("story-tabs");
            _side.Add(_tabsBar);

            _inspector = new NarrativeInspectorPanel();
            _inspector.NodeFieldChanged += _ =>
            {
                _editingField = true;
                _edit?.NotifyFieldChanged();
                _editingField = false;
            };
            _inspector.NavigateToNode += Navigate;
            _stepTab = new VisualElement();
            _stepTab.AddToClassList("story-step-tab");
            _stepWarnings = new VisualElement();
            _stepWarnings.AddToClassList("story-step-tab__warnings");
            _stepTab.Add(_stepWarnings);
            _stepTab.Add(_inspector);
            AddTab(StoryText.T("Paso", "Step"), _stepTab);

            _start = new StoryStartPanel(() => _edit);
            _start.PlayRequested += PlayFrom;
            _start.NavigateTo += Navigate;
            _start.OpenInGraph += (graph, guid) => { Load(graph); Navigate(guid); };
            _start.DebugRequested += () => ShowTab(_world);
            _start.SettingsRequested += OpenSettings;
            AddTab(StoryText.T("Arranque", "Start"), _start);

            _world = new StoryWorldPanel();
            _world.NavigateTo += Navigate;
            StoryBreakpoints.Hit -= OnBreakpointHit;
            StoryBreakpoints.Hit += OnBreakpointHit;
            _world.OpenInGraph += (graph, guid) =>
            {
                Load(graph);
                Navigate(guid);
            };
            AddTab(StoryText.T("Depuración", "Debug"), _world);

            // El asistente no es una pestaña: lo abre su botón de la barra y ocupa el panel.
            _assistant = new StoryAssistantPanel { Graph = () => _graph };
            _assistant.SettingsRequested += OpenSettings;
            StoryAssistantSettings.Changed -= OnAssistantSettings;
            StoryAssistantSettings.Changed += OnAssistantSettings;
            _assistant.Changed += () => { _canvas.Rebuild(); OnSelectionChanged(_canvas.Primary); ScheduleSave(); };
            _assistant.AddToClassList("story-side__content");
            _assistant.style.display = DisplayStyle.None;
            _side.Add(_assistant);
            OnAssistantSettings();

            _gameTabs.Clear();
            foreach (var tab in NarrativeEditorExtensions.CreateAll<NarrativeGraphSideTab>().OrderBy(t => t.Order))
            {
                _gameTabs.Add(tab);
                AddTab(tab.TabTitle, tab);
            }
            // «?»: la página del manual de la pestaña que se ve.
            var help = new Button(() => StoryTour.OpenManual(_shownTab == _start ? 10 : _shownTab == _world ? 13 : 5))
            {
                text = "?",
                tooltip = StoryText.T("Abre el manual en esta parte", "Opens the manual at this part"),
            };
            help.AddToClassList("story-tabs__help");
            _tabsBar.Add(new VisualElement { style = { flexGrow = 1 } });
            _tabsBar.Add(help);

            var last = EditorPrefs.GetInt(PrefLastTab, 0);
            ShowTab(last >= 0 && last < _tabs.Count ? _tabs[last].content : _stepTab);
        }

        void OpenSettings() => StorySettingsWindow.Open(() => _graph, () => { var g = _graph; Close(); OpenGraph(g); });

        void AddTab(string title, VisualElement content)
        {
            var button = new Button(() => ShowTab(content)) { text = title };
            button.AddToClassList("story-tab");
            _tabsBar.Add(button);
            content.AddToClassList("story-side__content");
            _side.Add(content);
            _tabs.Add((button, content));
        }

        void OnAssistantSettings()
        {
            if (_assistant == null) return;
            bool visible = StoryAssistantSettings.Visible;
            _assistantButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible && _assistant.style.display == DisplayStyle.Flex) ShowTab(_stepTab);
        }

        void ToggleAssistant()
        {
            if (_assistant.style.display == DisplayStyle.Flex) { ShowTab(_shownTab ?? _stepTab); return; }
            foreach (var (button, c) in _tabs)
            {
                c.style.display = DisplayStyle.None;
                button.EnableInClassList("is-on", false);
            }
            _assistant.style.display = DisplayStyle.Flex;
            _assistantButton.AddToClassList("is-on");
        }

        void ShowTab(VisualElement content)
        {
            _shownTab = content;
            _assistant.style.display = DisplayStyle.None;
            _assistantButton?.RemoveFromClassList("is-on");
            for (int i = 0; i < _tabs.Count; i++)
            {
                var (button, c) = _tabs[i];
                bool on = c == content;
                c.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                button.EnableInClassList("is-on", on);
                if (on) EditorPrefs.SetInt(PrefLastTab, i);
            }
            RefreshShown();
            if (content is NarrativeGraphSideTab tab)
            {
                tab.SetSelectedNode(_canvas.Primary);
                tab.OnShown();
            }
        }

        /// Arranque y Depuración calculan la historia hasta el paso: solo se rehacen si se ven.
        void RefreshShown()
        {
            var node = _canvas?.Primary;
            if (_shownTab == _world) _world.Show(_graph, _canvas.Structure, node);
            else if (_shownTab == _start) _start.Show(_graph, _canvas.Structure, node);
        }

        // ── Graph ────────────────────────────────────────────────────────────

        public void Load(NarrativeGraph graph)
        {
            SaveNow();
            _graph = graph;
            _so = graph != null ? new SerializedObject(graph) : null;
            if (_edit != null) _edit.Changed -= OnGraphChanged;
            _edit = graph != null ? new StoryGraphEditing(graph) : null;
            if (_edit != null) _edit.Changed += OnGraphChanged;
            if (graph != null) EditorPrefs.SetString(PrefLastGraph, AssetDatabase.GetAssetPath(graph));

            _inspector.SetGraph(graph, _so);
            foreach (var tab in _gameTabs) tab.SetGraph(graph);
            _canvas.SetEditing(_edit);
            _pathNodes.Clear();
            RebuildGraphMenu();
            RefreshPanels();
            _saved.text = string.Empty;
            RestoreLastStep();
        }

        /// Al abrir un capítulo, vuelve al último paso con el que se trabajó en él.
        void RestoreLastStep()
        {
            var graph = _graph;
            if (graph == null) return;
            var guid = EditorPrefs.GetString(PrefLastStep + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph)), string.Empty);
            if (string.IsNullOrEmpty(guid)) return;
            rootVisualElement.schedule.Execute(() =>
            {
                if (_graph == graph && _canvas.Primary == null && graph.FindNode(guid) is { } node) _canvas.Select(node, true);
            }).StartingIn(150);
        }

        void RebuildGraphMenu()
        {
            _graphMenu.menu.ClearItems();
            var graphs = AssetDatabase.FindAssets("t:NarrativeGraph")
                .Select(g => AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(g => g != null)
                .OrderBy(g => g.name, StringComparer.OrdinalIgnoreCase);
            foreach (var g in graphs)
            {
                var captured = g;
                _graphMenu.menu.AppendAction(g.name, _ => Load(captured),
                    _ => captured == _graph ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            _graphMenu.menu.AppendSeparator();
            _graphMenu.menu.AppendAction(StoryText.T("Nuevo grafo…", "New graph…"), _ => CreateGraph());
            _graphMenu.text = _graph != null ? _graph.name : StoryText.T("Elegir grafo", "Pick a graph");
        }

        void CreateGraph()
        {
            var path = EditorUtility.SaveFilePanelInProject(StoryText.T("Nuevo grafo", "New graph"), "NewStory", "asset", string.Empty, "Assets");
            if (string.IsNullOrEmpty(path)) return;
            var g = CreateInstance<NarrativeGraph>();
            var start = new StartNode { displayTitle = StoryText.T("Inicio", "Start") };
            g.nodes.Add(start);
            g.startNodeGuid = start.guid;
            AssetDatabase.CreateAsset(g, path);
            AssetDatabase.SaveAssets();
            Load(g);
        }

        void OnGraphChanged()
        {
            _canvas.Rebuild();
            if (_editingField)
            {
                // The inspector is the one editing: keep it as it is so the field keeps its focus.
                RefreshShown();
                _outline.Refresh(_graph, _canvas.Structure, _canvas.Primary, n => _pathNodes.Contains(n.guid));
                RefreshStatus();
            }
            else
            {
                _so?.Update();
                OnSelectionChanged(_canvas.Primary);
            }
            ScheduleSave();
        }

        void OnUndoRedo()
        {
            if (_graph == null) return;
            _so?.Update();
            _canvas.Rebuild();
            OnSelectionChanged(_canvas.Primary);
            if (EditorUtility.IsDirty(_graph)) ScheduleSave();
        }

        void ScheduleSave()
        {
            _saved.text = StoryText.T("Guardando…", "Saving…");
            if (_savePending) return;
            _savePending = true;
            rootVisualElement.schedule.Execute(SaveNow).StartingIn(800);
        }

        void SaveNow()
        {
            _savePending = false;
            if (_graph != null && EditorUtility.IsDirty(_graph))
            {
                AssetDatabase.SaveAssetIfDirty(_graph);
                NarrativeProjectIndex.Invalidate();
            }
            if (_saved != null) _saved.text = StoryText.T("Guardado", "Saved");
        }

        void AutoLayout()
        {
            if (_graph == null) return;
            _edit.Move(StoryLayout.AutoLayout(_graph, _canvas.Structure));
            _canvas.FrameAll();
        }

        // ── Selection and panels ─────────────────────────────────────────────

        void OnSelectionChanged(NarrativeNode node)
        {
            if (node != null && _graph != null)
            {
                EditorPrefs.SetString(PrefLastStep + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_graph)), node.guid);
                var result = NarrativeStateProjector.Project(_graph, node.guid, new RecordingStateWriter());
                _pathNodes = new HashSet<string>(result.Order) { node.guid };
                _canvas.SetPath(_pathNodes, result.Edges);
            }
            else
            {
                _pathNodes.Clear();
                _canvas.SetPath(null, null);
            }
            RefreshPanels();
        }

        void RefreshPanels()
        {
            var node = _canvas.Primary;
            _inspector.Show(node != null ? new Host { Model = node } : null);
            _stepWarnings.Clear();
            foreach (var warning in StepSummary.Warnings(node))
            {
                var label = new Label("▲ " + warning);
                label.AddToClassList("story-notice");
                label.AddToClassList("is-warning");
                _stepWarnings.Add(label);
            }
            RefreshShown();
            foreach (var tab in _gameTabs) tab.SetSelectedNode(node);
            _outline.Refresh(_graph, _canvas.Structure, node, n => _pathNodes.Contains(n.guid));
            RefreshStatus();
            RefreshPlay();
        }

        void Navigate(string guid)
        {
            var node = _graph?.FindNode(guid);
            if (node != null) _canvas.Select(node, true);
        }

        List<NarrativeNode> NodesWithWarnings()
            => _graph == null ? new List<NarrativeNode>() : _canvas.Structure.StoryOrder.Where(n => StepSummary.Warnings(n).Count > 0).ToList();

        void RefreshStatus()
        {
            int count = NodesWithWarnings().Count;
            _status.text = count == 0 ? "✓ " + StoryText.T("Todo correcto", "All good")
                                      : "▲ " + StoryText.T(count == 1 ? "1 aviso" : $"{count} avisos", count == 1 ? "1 warning" : $"{count} warnings");
            _status.EnableInClassList("is-warning", count > 0);
        }

        void GoToFirstWarning()
        {
            var first = NodesWithWarnings().FirstOrDefault();
            if (first == null) return;
            _canvas.Select(first, true);
            ShowTab(_stepTab);
        }

        void RefreshPlay()
        {
            bool playing = EditorApplication.isPlaying;
            var node = _canvas.Primary;
            _play.text = "▶ " + (node != null ? StoryText.T($"Jugar desde {_canvas.Structure.Number(node)}", $"Play from {_canvas.Structure.Number(node)}")
                                             : StoryText.T("Elige un paso para jugar", "Pick a step to play"));
            _play.SetEnabled(!playing && node != null);
            _stop.SetEnabled(playing);
        }

        void PlayFrom(NarrativeNode node)
        {
            if (node == null || _graph == null) return;
            SaveNow();
            foreach (var tab in _gameTabs)
            {
                if (!tab.TryPlayFrom(_graph, node.guid)) continue;
                ShowTab(tab);
                return;
            }
            ShowTab(_start);
            bool ok = NarrativePlayLaunchers.Current.Launch(_graph, node.guid, StoryRouteChoices.All(), out var message);
            _start.SetStatus(message);
            if (!ok && !string.IsNullOrEmpty(message)) Debug.LogWarning("[Narrative Graph] " + message);
        }

        // ── Play mode ────────────────────────────────────────────────────────

        void OnBreakpointHit(NarrativeGraph graph, NarrativeNode step)
        {
            if (graph != _graph) Load(graph);
            Focus();
            Navigate(step.guid);
            ShowTab(_world);
        }

        void Poll()
        {
            bool playing = EditorApplication.isPlaying;
            if (playing != _wasPlaying)
            {
                _wasPlaying = playing;
                if (playing) _canvas.ClearVisited();
                RefreshPlay();
            }
            if (!playing || _graph == null || NarrativeGraphHub.Instance == null) { _canvas.SetCurrent(null); return; }
            var runner = NarrativeGraphHub.Instance.GetAllRunners().FirstOrDefault(r => r != null && r.graph == _graph);
            _canvas.SetCurrent(runner?.CurrentNode?.guid ?? runner?.Blackboard?.Get<string>(NarrativeRunner.CurrentNodeKey, null));
        }
    }
}
