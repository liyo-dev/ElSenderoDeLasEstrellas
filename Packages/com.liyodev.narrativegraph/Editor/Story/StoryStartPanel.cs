using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Pestaña «Arranque»: cómo va a empezar la prueba desde el paso elegido, fila a fila y con de
    /// dónde sale cada cosa (partida nueva, historia, punto de arranque o jugador de prueba), y el
    /// botón de probar. Aquí se elige el jugador de prueba; el grafo no guarda cómo está el jugador.
    public sealed class StoryStartPanel : VisualElement
    {
        public event Action<NarrativeNode> PlayRequested;
        public event Action<string> NavigateTo;
        public event Action<NarrativeGraph, string> OpenInGraph;
        public event Action DebugRequested;
        public event Action SettingsRequested;

        readonly ScrollView _scroll;
        readonly Button _play;
        readonly Label _status;
        NarrativeGraph _graph;
        StoryStructure _structure;
        NarrativeNode _node;
        public StoryStartPanel()
        {
            AddToClassList("story-panel");
            AddToClassList("story-start");
            _scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _scroll.AddToClassList("story-panel__scroll");
            Add(_scroll);

            var foot = new VisualElement();
            foot.AddToClassList("story-start__foot");
            _play = new Button(() =>
            {
                if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
                if (_node != null) PlayRequested?.Invoke(_node);
            });
            _play.AddToClassList("story-button--primary");
            _play.AddToClassList("story-start__play");
            foot.Add(_play);
            _status = new Label();
            _status.AddToClassList("story-muted");
            foot.Add(_status);
            Add(foot);

            schedule.Execute(RefreshPlay).Every(300);
        }

        public void SetStatus(string text) => _status.text = text ?? string.Empty;

        public void Show(NarrativeGraph graph, StoryStructure structure, NarrativeNode node)
        {
            bool other = node != _node || graph != _graph;
            _graph = graph;
            _structure = structure;
            _node = node;
            if (other) _status.text = string.Empty;
            Rebuild();
            if (other) _scroll.scrollOffset = Vector2.zero;
        }

        void RefreshPlay()
        {
            bool playing = EditorApplication.isPlaying;
            _play.text = playing ? "■ " + StoryText.T("Parar la prueba", "Stop the test")
                                 : "▶ " + (_node != null ? StoryText.T($"Probar desde {_structure?.Number(_node)}", $"Play from {_structure?.Number(_node)}")
                                                         : StoryText.T("Elige un paso para jugar", "Pick a step to play"));
            _play.SetEnabled(playing || _node != null);
        }

        public void Rebuild()
        {
            _scroll.Clear();
            RefreshPlay();
            if (_graph == null || _node == null)
            {
                _scroll.Add(StoryUI.Muted(StoryText.T("Elige un paso para ver cómo empezará la prueba desde él.",
                                              "Pick a step to see how the test will start from it.")));
                return;
            }

            var plan = NarrativeStartPlanner.Plan(_graph, _node.guid);
            var sheet = plan.Sheet;

            var head = new VisualElement();
            head.AddToClassList("story-panel__head");
            head.Add(StoryUI.Kicker(StoryText.T("ASÍ EMPIEZA LA PRUEBA", "THIS IS HOW THE TEST STARTS")));
            var titleRow = new VisualElement();
            titleRow.AddToClassList("story-row");
            var num = new Label(_structure?.Number(_node));
            num.AddToClassList("story-accent-number");
            titleRow.Add(num);
            var title = new Label(NarrativeStartPlanner.Title(_node));
            title.AddToClassList("story-panel__title");
            titleRow.Add(title);
            head.Add(titleRow);
            if (plan.TargetGuid != plan.StepGuid)
                head.Add(StoryUI.Muted(StoryText.T($"Se empieza en «{NarrativeStartPlanner.Title(_graph.FindNode(plan.TargetGuid))}», tras la espera del capítulo anterior.",
                                           $"It starts at “{NarrativeStartPlanner.Title(_graph.FindNode(plan.TargetGuid))}”, after waiting for the previous chapter.")));
            if (plan.Previous.Count > 0)
                head.Add(StoryUI.Muted(StoryText.T($"Capítulos anteriores, ya terminados: {string.Join(", ", plan.Previous.Select(c => c.Graph.name))}.",
                                           $"Earlier chapters, already finished: {string.Join(", ", plan.Previous.Select(c => c.Graph.name))}.")));
            _scroll.Add(head);

            foreach (var warning in plan.Warnings) _scroll.Add(StoryUI.Notice(warning, false));

            // ── Jugador ──
            using (var preview = plan.Build(story: true, points: true, testPlayer: true))
            {
                if (sheet == null || !sheet.IsReady)
                {
                    var box = Section(StoryText.T("Jugador", "Player"));
                    box.Add(StoryUI.Muted(StoryText.T("Sin ficha del jugador, la prueba empieza con lo que tenga tu juego. Con ella, los puntos de arranque (◆) dicen dónde aparece el jugador y cómo está.",
                                              "Without a player sheet the test starts with whatever your game has. With one, start points (◆) say where the player appears and how they are.")));
                    var open = new Button(() => SettingsRequested?.Invoke()) { text = "⚙  " + StoryText.T("Configurar la ficha del jugador", "Set up the player sheet") };
                    open.AddToClassList("story-button");
                    open.style.alignSelf = Align.FlexStart;
                    box.Add(open);
                }
                else
                {
                    using var bare = plan.Build(story: false, points: false, testPlayer: false);
                    BuildPlayer(plan, preview.Data, bare.Data);
                }

                // ── Lo que añade el juego (escena, entrada…) ──
                if (NarrativePlayLaunchers.Current is INarrativeLaunchDetails details)
                {
                    var lines = new List<StartLine>();
                    try { details.Describe(plan, preview.Data, lines); }
                    catch (Exception e) { lines.Add(new StartLine { Label = "?", Value = e.Message, Warning = true }); }
                    if (lines.Count > 0)
                    {
                        var box = Section(StoryText.T("Partida", "Game"));
                        foreach (var l in lines) box.Add(Row(l.Label, l.Value, SourceLabel(l.Source, false), l.Warning));
                    }
                }
            }

            BuildStory(plan);
            BuildDecisions(plan);
        }

        // ── Jugador ─────────────────────────────────────────────────────────

        void BuildPlayer(NarrativeStartPlan plan, UnityEngine.Object player, UnityEngine.Object bare)
        {
            var bareObject = new SerializedObject(bare);
            var layerValues = new Dictionary<NarrativeStartPlan.Layer, PlayerScratch>();
            var sheet = plan.Sheet;
            var box = Section(StoryText.T("Jugador", "Player"));
            BuildTestPlayer(box, sheet, player);

            var so = new SerializedObject(player);
            try
            {
                foreach (var field in sheet.fields)
                {
                    var p = so.FindProperty(field);
                    box.Add(Row(sheet.LabelOf(field), NarrativePlayerValuesEditor.Text(p), SourceOfField(plan, field, so, bareObject, layerValues)(),
                                false, field == sheet.positionField));
                }
            }
            finally
            {
                foreach (var scratch in layerValues.Values) scratch.Dispose();
            }
        }

        /// Con qué jugador se prueba: el que deja la historia, o uno de prueba (vale en cualquier paso).
        void BuildTestPlayer(VisualElement box, NarrativePlayerSheet sheet, UnityEngine.Object current)
        {
            var players = TestPlayers.All();
            var selected = TestPlayers.Selected;
            string fromStory = StoryText.T("El de la historia", "The one from the story");
            var choices = new List<string> { fromStory };
            choices.AddRange(players.Select(p => p.name));
            var row = new VisualElement();
            row.AddToClassList("story-start__actions");
            var pick = new DropdownField(StoryText.T("Probar con", "Test with"), choices, selected != null ? selected.name : fromStory)
            {
                tooltip = StoryText.T("El de la historia: lo conseguido hasta este paso. Un jugador de prueba cambia solo lo que tenga marcado, en cualquier paso.",
                                      "The one from the story: what has been earned up to this step. A test player changes only what it has ticked, at any step."),
            };
            pick.style.flexGrow = 1;
            pick.RegisterValueChangedCallback(e =>
            {
                TestPlayers.Selected = players.FirstOrDefault(p => p.name == e.newValue);
                Rebuild();
            });
            row.Add(pick);
            if (selected != null)
            {
                var edit = new Button(() => { Selection.activeObject = selected; EditorGUIUtility.PingObject(selected); }) { text = StoryText.T("Editar", "Edit") };
                edit.AddToClassList("story-button");
                row.Add(edit);
            }
            var create = new Button(() => CreateTestPlayer(sheet, current, null))
            {
                text = StoryText.T("Nuevo…", "New…"),
                tooltip = StoryText.T("Crea un jugador de prueba (sin cambios: marca en él lo que quieras cambiar).", "Creates a test player (no changes: tick in it what you want to change)."),
            };
            create.AddToClassList("story-button");
            row.Add(create);
            var live = EditorApplication.isPlaying ? NarrativeEditorExtensions.CreateFirst<INarrativePlayerLive>() : null;
            if (live != null)
            {
                var take = new Button(() => CreateTestPlayer(sheet, live.CaptureLive(), sheet.fields.Where(f => f != sheet.positionField)))
                {
                    text = StoryText.T("Desde la partida", "From the game"),
                    tooltip = StoryText.T("Crea un jugador de prueba con cómo va el jugador ahora mismo.", "Creates a test player with how the player is right now."),
                };
                take.AddToClassList("story-button");
                row.Add(take);
            }
            box.Add(row);
        }

        void CreateTestPlayer(NarrativePlayerSheet sheet, UnityEngine.Object from, IEnumerable<string> fields)
        {
            if (from != null && from.GetType() != sheet.DataType) { SetStatus(StoryText.T("El juego no ha dado los datos del jugador.", "The game gave no player data.")); return; }
            var player = TestPlayers.Create(StoryText.T("Jugador de prueba", "Test player"), from, fields);
            TestPlayers.Selected = player;
            Selection.activeObject = player;
            EditorGUIUtility.PingObject(player);
            SetStatus(StoryText.T($"Creado «{player.name}»: márcale en el Inspector lo que cambia.", $"Created “{player.name}”: tick in the Inspector what it changes."));
            Rebuild();
        }

        /// De dónde sale el valor final de un campo: del último punto (o del jugador de prueba) que lo fija si
        /// sigue valiendo eso; de la historia si la historia lo cambia (también después de un
        /// punto); si no, de la partida nueva.
        Func<Label> SourceOfField(NarrativeStartPlan plan, string field, SerializedObject final, SerializedObject bare,
                                  Dictionary<NarrativeStartPlan.Layer, PlayerScratch> layerValues)
        {
            var layer = plan.SourceOf(field);
            var finalProp = final.FindProperty(field);
            if (finalProp == null) return () => SourceChipFor(layer, false);
            bool fromNewGame = SerializedProperty.DataEquals(finalProp, bare.FindProperty(field));
            if (layer == null) return () => SourceChipFor(null, !fromNewGame);
            if (!layerValues.TryGetValue(layer, out var only)) layerValues[layer] = only = plan.Only(layer);
            var layerProp = only.Data != null ? new SerializedObject(only.Data).FindProperty(field) : null;
            if (layerProp == null || SerializedProperty.DataEquals(finalProp, layerProp)) return () => SourceChipFor(layer, false);
            var layerText = NarrativePlayerValuesEditor.Text(layerProp);
            return () =>
            {
                var chip = SourceChipFor(null, true);
                chip.tooltip = StoryText.T($"{layer.Label} lo deja en «{layerText}»; después la historia lo cambia.",
                                           $"{layer.Label} sets it to “{layerText}”; later the story changes it.");
                return chip;
            };
        }

        Label SourceChipFor(NarrativeStartPlan.Layer layer, bool byStory)
        {
            if (layer == null && byStory) return SourceLabel(StoryText.T("Historia", "Story"), false);
            if (layer == null) return SourceLabel(StoryText.T("Partida nueva", "New game"), false);
            if (layer.IsTestPlayer) return SourceLabel(layer.Label, true);
            var point = layer.Graph != null ? layer.Graph.FindNode(layer.NodeGuid) : null;
            var chip = SourceLabel("◆ " + (layer.Graph == _graph && point != null ? _structure?.Number(point) : layer.Graph?.name), false);
            chip.AddToClassList("is-link");
            chip.tooltip = layer.Graph != null ? $"{layer.Graph.name} · {layer.Label}" : layer.Label;
            var graph = layer.Graph;
            var guid = layer.NodeGuid;
            chip.RegisterCallback<ClickEvent>(_ =>
            {
                if (graph == _graph) NavigateTo?.Invoke(guid);
                else OpenInGraph?.Invoke(graph, guid);
            });
            return chip;
        }

        static Label SourceLabel(string text, bool tweak)
        {
            var l = new Label(text ?? string.Empty);
            l.AddToClassList("story-start__source");
            l.EnableInClassList("is-tweak", tweak);
            l.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            return l;
        }

        // ── Historia ────────────────────────────────────────────────────────

        void BuildStory(NarrativeStartPlan plan)
        {
            if (!plan.ReachedTarget) return;
            var story = plan.Story;
            var box = Section(StoryText.T("Historia hasta aquí", "Story so far"));
            box.Add(Row(StoryText.T("Pasos", "Steps"), plan.Route.Order.Count.ToString(), null, false));
            int characters = ActorStateFacts.From(story).Count;
            if (characters > 0) box.Add(Row(StoryText.T("Personajes con algo que hacer", "Characters with something to do"), characters.ToString(), null, false));
            foreach (var category in story.Facts.Where(c => c.Key != NarrativeCoreFacts.Counter && c.Key != NarrativeCoreFacts.ActorState && c.Key != NarrativeCoreFacts.ActorUsual).OrderBy(c => c.Key))
                box.Add(Row(Humanize(category.Key), string.Join(", ", category.Value.Select(f => string.IsNullOrEmpty(f.Value.value) ? f.Key : $"{f.Key} {f.Value.value}")), null, false));
            int active = story.Quests.Count(q => q.Value.value == RecordingStateWriter.QuestActive);
            int done = story.Quests.Count(q => q.Value.value == RecordingStateWriter.QuestDone);
            if (active + done > 0) box.Add(Row(StoryText.T("Misiones", "Quests"), StoryText.T($"{active} en curso · {done} hechas", $"{active} active · {done} done"), null, false));
            int items = story.Items.Count(i => i.Value.value > 0);
            if (items > 0) box.Add(Row(StoryText.T("Objetos", "Items"), items.ToString(), null, false));
            if (story.Flags.Count > 0) box.Add(Row(StoryText.T("Datos de partida", "Story flags"), story.Flags.Count.ToString(), null, false));
            var more = new Button(() => DebugRequested?.Invoke()) { text = StoryText.T("Verlo entero en Depuración", "See it all in Debug") };
            more.AddToClassList("story-button");
            more.style.alignSelf = Align.FlexStart;
            box.Add(more);
        }

        /// Si varias ramas llevan al paso, se elige por cuál se viene.
        void BuildDecisions(NarrativeStartPlan plan)
        {
            var open = plan.Decisions.Where(d => !d.Forced && d.PortsToTarget != null && d.PortsToTarget.Length > 1).ToList();
            if (open.Count == 0) return;
            var box = Section(StoryText.T("Por dónde vienes", "Which way you come"));
            foreach (var d in open)
            {
                box.Add(StoryUI.Muted(StoryText.T($"Varias ramas llegan aquí desde «{d.Title}»:", $"Several branches lead here from “{d.Title}”:")));
                var row = new VisualElement();
                row.AddToClassList("story-start__actions");
                foreach (var port in d.PortsToTarget)
                {
                    int captured = port;
                    var b = new Button(() => { StoryRouteChoices.Set(d.NodeGuid, captured); Rebuild(); }) { text = d.Options[port] };
                    b.AddToClassList("story-button");
                    b.EnableInClassList("is-on", port == d.ChosenPort);
                    row.Add(b);
                }
                box.Add(row);
            }
        }

        // ── Piezas ──────────────────────────────────────────────────────────

        VisualElement Section(string title)
        {
            var box = new VisualElement();
            box.AddToClassList("story-panel__section");
            box.Add(StoryUI.Kicker(title.ToUpperInvariant()));
            _scroll.Add(box);
            return box;
        }

        static VisualElement Row(string name, string value, Label source, bool warn, bool strong = false)
        {
            var row = new VisualElement();
            row.AddToClassList("story-panel__row");
            row.EnableInClassList("is-warning", warn);
            var n = new Label(name) { tooltip = name };
            n.AddToClassList("story-panel__row-name");
            row.Add(n);
            var v = new Label(value) { tooltip = value };
            v.AddToClassList("story-panel__row-value");
            v.EnableInClassList("is-strong", strong);
            row.Add(v);
            if (source != null) row.Add(source);
            return row;
        }

        static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (key == NarrativeCoreFacts.Scene) return StoryText.T("Escenas cargadas", "Loaded scenes");
            return ObjectNames.NicifyVariableName(key);
        }

    }
}
