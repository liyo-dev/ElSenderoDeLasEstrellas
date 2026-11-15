using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Optional chat inside the editor, with the service each user picks in the settings (⚙).
    /// Changes the assistant proposes are shown first and applied only on "Apply".
    public sealed class StoryAssistantPanel : VisualElement
    {
        public Func<NarrativeGraph> Graph;
        /// El paso elegido en el editor (para el informe de estado que lee el asistente).
        public Func<NarrativeNode> Step;
        public event Action Changed;
        public event Action SettingsRequested;

        readonly ScrollView _log;
        readonly TextField _input;
        readonly Button _send;
        readonly Label _service;
        readonly VisualElement _notReady;
        readonly VisualElement _chat;
        readonly List<StoryAssistantClient.Turn> _turns = new();
        bool _busy;

        static readonly Regex ChangesBlock = new(@"```(?:json)?\s*(\{[\s\S]*?""ops""[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);

        public StoryAssistantPanel()
        {
            AddToClassList("story-panel");
            style.flexGrow = 1;

            _notReady = new VisualElement();
            _notReady.AddToClassList("story-panel__pad");
            var intro = new Label(StoryText.T(
                "Escribe lo que quieres y el asistente lee el capítulo, te contesta y te propone cambios que tú aplicas. Elige el servicio y pon tu clave en los ajustes.",
                "Write what you want: the assistant reads the chapter, answers and proposes changes you apply. Pick the service and enter your key in the settings."));
            intro.AddToClassList("story-muted");
            _notReady.Add(intro);
            var open = new Button(() => SettingsRequested?.Invoke()) { text = "⚙  " + StoryText.T("Abrir ajustes", "Open settings") };
            open.AddToClassList("story-button");
            open.style.marginTop = 10;
            _notReady.Add(open);
            Add(_notReady);

            _chat = new VisualElement { style = { flexGrow = 1 } };
            _service = new Label();
            _service.AddToClassList("story-assistant__service");
            _chat.Add(_service);
            _log = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _log.AddToClassList("story-panel__scroll");
            _log.style.flexGrow = 1;
            _chat.Add(_log);
            var bottom = new VisualElement();
            bottom.AddToClassList("story-assistant__input");
            _input = new TextField { multiline = true };
            _input.textEdition.placeholder = StoryText.T("¿Qué quieres hacer en este capítulo?", "What do you want to do in this chapter?");
            _input.RegisterCallback<KeyDownEvent>(e =>
            {
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && !e.shiftKey) { Send(); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            bottom.Add(_input);
            _send = new Button(Send) { text = StoryText.T("Enviar", "Send") };
            _send.AddToClassList("story-button");
            bottom.Add(_send);
            var reset = new Button(() => { _turns.Clear(); _log.Clear(); })
                { text = StoryText.T("Nueva", "New"), tooltip = StoryText.T("Empezar una conversación nueva", "Start a new conversation") };
            reset.AddToClassList("story-button");
            bottom.Add(reset);
            _chat.Add(bottom);
            Add(_chat);

            Refresh();
            StoryAssistantSettings.Changed += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => StoryAssistantSettings.Changed -= Refresh);
        }

        void Refresh()
        {
            bool ready = StoryAssistantSettings.IsReady;
            _notReady.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            _chat.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            var p = StoryAssistantSettings.Current;
            _service.text = $"{StoryAssistantSettings.Name(p)} · {StoryAssistantSettings.Model(p)}";
        }

        void Send()
        {
            var text = _input.value?.Trim();
            var graph = Graph?.Invoke();
            if (_busy || string.IsNullOrEmpty(text) || graph == null) return;
            _input.value = string.Empty;
            Bubble(text, true);
            _turns.Add(new StoryAssistantClient.Turn(true, text));

            _busy = true;
            _send.SetEnabled(false);
            var thinking = Note(StoryText.T("Pensando…", "Thinking…"));
            StoryAssistantClient.Send(SystemPrompt(graph, Step?.Invoke()), _turns, answer =>
            {
                Done(thinking);
                _turns.Add(new StoryAssistantClient.Turn(false, answer));
                Show(answer);
            }, error =>
            {
                Done(thinking);
                _turns.RemoveAt(_turns.Count - 1);
                Note(StoryText.T("No se pudo contactar con el servicio: ", "Could not reach the service: ") + error, true);
            });
        }

        void Done(VisualElement thinking)
        {
            thinking.RemoveFromHierarchy();
            _busy = false;
            _send.SetEnabled(true);
        }

        void Show(string answer)
        {
            var match = ChangesBlock.Match(answer ?? string.Empty);
            string prose = match.Success ? answer.Remove(match.Index, match.Length).Trim() : answer;
            if (!string.IsNullOrWhiteSpace(prose)) Bubble(prose, false);
            if (!match.Success) return;
            try
            {
                var ops = JObject.Parse(match.Groups[1].Value)["ops"] as JArray;
                if (ops != null && ops.Count > 0) Proposal(ops);
            }
            catch (Exception) { Note(StoryText.T("El asistente propuso cambios que no se pueden leer.", "The assistant proposed changes that cannot be read."), true); }
        }

        void Proposal(JArray ops)
        {
            var box = new VisualElement();
            box.AddToClassList("story-assistant__proposal");
            box.Add(new Label(StoryText.T($"Propone {ops.Count} cambios:", $"Proposes {ops.Count} changes:")) { name = "title" });
            foreach (var op in ops)
            {
                var line = new Label("• " + Describe(op as JObject));
                line.AddToClassList("story-muted");
                box.Add(line);
            }
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            Button apply = null, discard = null;
            apply = new Button(() =>
            {
                var result = StoryBatchEdit.Apply(Graph?.Invoke(), new JObject { ["ops"] = ops }.ToString());
                Note(result.Applied ? StoryText.T("Cambios aplicados (Ctrl+Z los deshace).", "Changes applied (Ctrl+Z undoes them).") : result.ToString(), !result.Applied);
                _turns.Add(new StoryAssistantClient.Turn(true, result.Applied ? "[I applied your changes.]" : "[Your changes could not be applied:\n" + result + "]"));
                _turns.Add(new StoryAssistantClient.Turn(false, "OK."));
                if (result.Applied) Changed?.Invoke();
                apply.SetEnabled(false);
                discard.SetEnabled(false);
            }) { text = StoryText.T("Aplicar", "Apply") };
            apply.AddToClassList("story-button");
            apply.AddToClassList("is-primary");
            discard = new Button(() =>
            {
                _turns.Add(new StoryAssistantClient.Turn(true, "[I discarded those changes.]"));
                _turns.Add(new StoryAssistantClient.Turn(false, "OK."));
                apply.SetEnabled(false);
                discard.SetEnabled(false);
            }) { text = StoryText.T("Descartar", "Discard") };
            discard.AddToClassList("story-button");
            buttons.Add(apply);
            buttons.Add(discard);
            box.Add(buttons);
            _log.Add(box);
            ScrollToEnd();
        }

        static string Describe(JObject op)
        {
            if (op == null) return "?";
            string o = (string)op["op"];
            return o switch
            {
                "add" => StoryText.T($"añadir {(string)op["type"]} «{(string)op["title"]}» tras {(string)op["after"]}", $"add {(string)op["type"]} “{(string)op["title"]}” after {(string)op["after"]}"),
                "set" => StoryText.T($"cambiar {(string)op["step"]}", $"change {(string)op["step"]}"),
                "connect" => StoryText.T($"conectar {(string)op["from"]} → {(string)op["to"]}", $"connect {(string)op["from"]} → {(string)op["to"]}"),
                "disconnect" => StoryText.T($"desconectar {(string)op["from"]}", $"disconnect {(string)op["from"]}"),
                "section" => StoryText.T($"{(string)op["step"]} abre la sección «{(string)op["value"]}»", $"{(string)op["step"]} opens section “{(string)op["value"]}”"),
                "delete" => StoryText.T($"borrar {(string)op["step"]}", $"delete {(string)op["step"]}"),
                _ => o,
            };
        }

        static string SystemPrompt(NarrativeGraph graph, NarrativeNode step)
            => "You help a game writer work on a chapter of a narrative graph in Unity. Answer in the language the user writes in, briefly.\n" +
               "Steps are numbered by section ('2.3'). To propose changes, end your answer with ONE fenced json block {\"ops\":[...]}; the user reviews it and applies it or not. " +
               "Operations: add {id, type, title, after, port, fields:[{name,value}]}, set {step, fields}, connect {from, to, port}, disconnect {from, port}, section {step, value}, delete {step}. " +
               "Steps are named by number, id, or the id of a step added earlier in the same block. Field values are text. Use only step types and fields from the manual.\n\n" +
               "When asked why something is missing or wrong in the game, use the story state report below: where the story is, what it waits for and what does not match.\n\n" +
               NarrativeGraphText.Manual() + "\n\n" + NarrativeGraphText.Chapter(graph) + "\n\n" + StoryLiveReport.Build(graph, step).ToText();

        void Bubble(string text, bool mine)
        {
            var l = new Label(text) { selection = { isSelectable = true } };
            l.AddToClassList("story-assistant__bubble");
            l.EnableInClassList("is-mine", mine);
            _log.Add(l);
            ScrollToEnd();
        }

        Label Note(string text, bool warning = false)
        {
            var l = new Label(text);
            l.AddToClassList(warning ? "story-notice" : "story-muted");
            if (warning) l.AddToClassList("is-warning");
            _log.Add(l);
            ScrollToEnd();
            return l;
        }

        void ScrollToEnd() => _log.schedule.Execute(() => _log.scrollOffset = new Vector2(0, float.MaxValue));
    }
}
