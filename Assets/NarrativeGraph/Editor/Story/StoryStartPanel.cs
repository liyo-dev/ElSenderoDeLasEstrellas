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
    /// dónde sale cada cosa (partida nueva, historia, punto de arranque o retoque), y el botón de
    /// probar. Desde aquí se retoca la prueba y se fijan puntos de arranque en el grafo.
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
        Func<StoryGraphEditing> _edit;
        bool _editing;
        PlayerScratch _editScratch;
        SerializedObject _editObject;

        public StoryStartPanel(Func<StoryGraphEditing> edit)
        {
            _edit = edit;
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

            StartTweaks.Changed += OnTweaksChanged;
            RegisterCallback<DetachFromPanelEvent>(_ => { StartTweaks.Changed -= OnTweaksChanged; EndEditing(); });
            schedule.Execute(RefreshPlay).Every(300);
        }

        public void SetStatus(string text) => _status.text = text ?? string.Empty;

        public void Show(NarrativeGraph graph, StoryStructure structure, NarrativeNode node)
        {
            bool other = node != _node || graph != _graph;
            _graph = graph;
            _structure = structure;
            _node = node;
            if (other) { EndEditing(); _status.text = string.Empty; }
            Rebuild();
            if (other) _scroll.scrollOffset = Vector2.zero;
        }

        void OnTweaksChanged()
        {
            if (!_editing) Rebuild();
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
                _scroll.Add(Muted(StoryText.T("Elige un paso para ver cómo empezará la prueba desde él.",
                                              "Pick a step to see how the test will start from it.")));
                return;
            }

            var plan = NarrativeStartPlanner.Plan(_graph, _node.guid);
            var sheet = plan.Sheet;

            var head = new VisualElement();
            head.AddToClassList("story-panel__head");
            head.Add(Kicker(StoryText.T("ASÍ EMPIEZA LA PRUEBA", "THIS IS HOW THE TEST STARTS")));
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
                head.Add(Muted(StoryText.T($"Se empieza en «{NarrativeStartPlanner.Title(_graph.FindNode(plan.TargetGuid))}», tras la espera del capítulo anterior.",
                                           $"It starts at “{NarrativeStartPlanner.Title(_graph.FindNode(plan.TargetGuid))}”, after waiting for the previous chapter.")));
            if (plan.Previous.Count > 0)
                head.Add(Muted(StoryText.T($"Capítulos anteriores, ya terminados: {string.Join(", ", plan.Previous.Select(c => c.Graph.name))}.",
                                           $"Earlier chapters, already finished: {string.Join(", ", plan.Previous.Select(c => c.Graph.name))}.")));
            _scroll.Add(head);

            foreach (var warning in plan.Warnings) _scroll.Add(Notice(warning, false));

            // ── Jugador ──
            using (var preview = plan.Preview())
            {
                if (sheet == null || !sheet.IsReady)
                {
                    var box = Section(StoryText.T("Jugador", "Player"));
                    box.Add(Muted(StoryText.T("Sin ficha del jugador, la prueba empieza con lo que tenga tu juego. Con ella, los puntos de arranque (◆) dicen dónde aparece el jugador y cómo está.",
                                              "Without a player sheet the test starts with whatever your game has. With one, start points (◆) say where the player appears and how they are.")));
                    var open = new Button(() => SettingsRequested?.Invoke()) { text = "⚙  " + StoryText.T("Configurar la ficha del jugador", "Set up the player sheet") };
                    open.AddToClassList("story-button");
                    open.style.alignSelf = Align.FlexStart;
                    box.Add(open);
                }
                else BuildPlayer(plan, preview.Data);

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

        void BuildPlayer(NarrativeStartPlan plan, UnityEngine.Object player)
        {
            var sheet = plan.Sheet;
            var box = Section(StoryText.T("Jugador", "Player"));
            var tweaks = StartTweaks.Load();

            if (_editing)
            {
                box.Add(Muted(StoryText.T("Lo que cambies aquí vale solo para probar; no toca el grafo. «◆ Fijar aquí» lo guarda como punto de arranque.",
                                          "What you change here is only for testing and does not touch the graph. “◆ Set here” saves it as a start point.")));
                _editScratch ??= plan.Preview();
                _editObject ??= _editScratch.Data != null ? new SerializedObject(_editScratch.Data) : null;
            }

            var so = new SerializedObject(player);
            foreach (var field in sheet.fields)
            {
                var layer = plan.SourceOf(field);
                if (_editing && _editObject != null)
                {
                    var prop = _editObject.FindProperty(field);
                    if (prop == null) continue;
                    var row = new VisualElement();
                    row.AddToClassList("story-start__field");
                    var pf = new PropertyField(prop.Copy());
                    pf.Bind(_editObject);
                    pf.style.flexGrow = 1;
                    row.Add(pf);
                    var chip = SourceChip(layer);
                    row.Add(chip);
                    string captured = field;
                    pf.TrackPropertyValue(prop, _ =>
                    {
                        var t = StartTweaks.Load();
                        NarrativePlayerValuesEditor.Set(t, _editScratch.Data, new[] { captured });
                        StartTweaks.Save(t);
                        chip.text = StoryText.T("Retoque", "Tweak");
                        chip.AddToClassList("is-tweak");
                    });
                    box.Add(row);
                }
                else
                {
                    var p = so.FindProperty(field);
                    box.Add(Row(sheet.LabelOf(field), NarrativePlayerValuesEditor.Text(p), SourceChip(layer), false,
                                field == sheet.positionField));
                }
            }

            var actions = new VisualElement();
            actions.AddToClassList("story-start__actions");
            var edit = new Button(() => { if (_editing) EndEditing(); else _editing = true; Rebuild(); })
            {
                text = _editing ? StoryText.T("Listo", "Done") : StoryText.T("Retocar para esta prueba", "Tweak for this test"),
            };
            edit.AddToClassList("story-button");
            actions.Add(edit);
            if (!tweaks.IsEmpty)
            {
                var clear = new Button(() => { EndEditing(); StartTweaks.Clear(); Rebuild(); }) { text = StoryText.T("Quitar retoques", "Clear tweaks") };
                clear.AddToClassList("story-button");
                actions.Add(clear);
                var fix = new Button(() => FixHere(plan, tweaks))
                {
                    text = "◆ " + StoryText.T("Fijar aquí", "Set here"),
                    tooltip = _node is CheckpointNode
                        ? StoryText.T("Guarda los retoques en este punto de arranque.", "Saves the tweaks in this start point.")
                        : StoryText.T("Crea un punto de arranque justo antes de este paso con los retoques.", "Creates a start point right before this step with the tweaks."),
                };
                fix.AddToClassList("story-button");
                actions.Add(fix);
            }
            var live = EditorApplication.isPlaying ? NarrativeEditorExtensions.CreateFirst<INarrativePlayerLive>() : null;
            if (live != null)
            {
                var take = new Button(() => TakeFromGame(sheet, live))
                {
                    text = StoryText.T("Tomar de la partida", "Take from the game"),
                    tooltip = StoryText.T("Pone como retoque cómo va el jugador ahora mismo.", "Sets how the player is right now as a tweak."),
                };
                take.AddToClassList("story-button");
                actions.Add(take);
            }
            box.Add(actions);
        }

        void TakeFromGame(NarrativePlayerSheet sheet, INarrativePlayerLive live)
        {
            var current = live.CaptureLive();
            if (current == null || current.GetType() != sheet.DataType) { SetStatus(StoryText.T("El juego no ha dado los datos del jugador.", "The game gave no player data.")); return; }
            var t = new NarrativePlayerValues();
            NarrativePlayerValuesEditor.Set(t, current, sheet.fields);
            EndEditing();
            StartTweaks.Save(t);
            SetStatus(StoryText.T("Retoques tomados de la partida. «◆ Fijar aquí» los guarda en el grafo.", "Tweaks taken from the game. “◆ Set here” saves them in the graph."));
        }

        void FixHere(NarrativeStartPlan plan, NarrativePlayerValues tweaks)
        {
            var edit = _edit?.Invoke();
            if (edit == null || _node == null || plan.Sheet?.DataType == null) return;
            using var scratch = new PlayerScratch(plan.Sheet.DataType);
            NarrativePlayerValuesEditor.Apply(tweaks, scratch.Data);

            CheckpointNode point = _node as CheckpointNode;
            if (point == null)
            {
                var before = _node;
                if (before.IsGraphEntry && before.outputs != null && before.outputs.Count > 0 && _graph.FindNode(before.outputs[0]) is { } next) before = next;
                point = (CheckpointNode)edit.CreateStep(typeof(CheckpointNode), before.position - new Vector2(StoryLayout.CardWidth + 60f, 0f), before.section);
                if (point == null) return;
                point.displayTitle = StoryText.T("Punto de arranque", "Start point");
                point.checkpointId = "PUNTO_" + point.guid.Substring(0, 6).ToUpperInvariant();
                edit.InsertBefore(before, point);
            }
            var target = point;
            edit.Modify(StoryText.T("Fijar punto de arranque", "Set start point"), () =>
            {
                target.player ??= new NarrativePlayerValues();
                NarrativePlayerValuesEditor.Set(target.player, scratch.Data, tweaks.fields);
            });
            EndEditing();
            StartTweaks.Clear();
            SetStatus(StoryText.T($"Guardado en «{NarrativeStartPlanner.Title(target)}».", $"Saved in “{NarrativeStartPlanner.Title(target)}”."));
            NavigateTo?.Invoke(target.guid);
        }

        void EndEditing()
        {
            _editing = false;
            _editObject = null;
            _editScratch?.Dispose();
            _editScratch = null;
        }

        Label SourceChip(NarrativeStartPlan.Layer layer)
        {
            if (layer == null) return SourceLabel(StoryText.T("Partida nueva", "New game"), false);
            if (layer.IsTweak) return SourceLabel(StoryText.T("Retoque", "Tweak"), true);
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
            foreach (var category in story.Facts.Where(c => c.Key != NarrativeCoreFacts.Counter).OrderBy(c => c.Key))
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
                box.Add(Muted(StoryText.T($"Varias ramas llegan aquí desde «{d.Title}»:", $"Several branches lead here from “{d.Title}”:")));
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
            box.Add(Kicker(title.ToUpperInvariant()));
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

        static Label Kicker(string text) { var l = new Label(text); l.AddToClassList("story-kicker"); return l; }
        static Label Muted(string text) { var l = new Label(text); l.AddToClassList("story-muted"); return l; }

        static VisualElement Notice(string text, bool ok)
        {
            var l = new Label(text);
            l.AddToClassList("story-notice");
            l.AddToClassList(ok ? "is-ok" : "is-warning");
            return l;
        }
    }
}
