using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.Assistant
{
    /// El chat compartido: una vista que se puede meter en cualquier ventana y que habla con el
    /// contexto que le pasen. El contexto pone el manual, el estado y la forma de aplicar cambios;
    /// aquí solo está la conversación y la caja de «Propone N cambios».
    public sealed class AssistantView : VisualElement
    {
        private static readonly Regex ChangesBlock =
            new(@"```(?:json)?\s*(\{[\s\S]*?""ops""[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);

        private readonly IAssistantContext _context;
        private readonly ScrollView _log;
        private readonly TextField _input;
        private readonly Button _send;
        private readonly Label _service;
        private readonly VisualElement _notReady;
        private readonly VisualElement _chat;
        private readonly List<AssistantTurn> _turns = new();
        private string _sessionId = Guid.NewGuid().ToString("N");
        private bool _busy;

        public event Action SettingsRequested;
        public event Action Applied;

        public AssistantView(IAssistantContext context)
        {
            _context = context;
            AddToClassList("story-panel");
            style.flexGrow = 1;

            _notReady = new VisualElement();
            _notReady.AddToClassList("story-panel__pad");
            var intro = new Label(context != null ? context.Intro : string.Empty);
            intro.AddToClassList("story-muted");
            _notReady.Add(intro);
            var open = new Button(() => SettingsRequested?.Invoke())
                { text = "⚙  " + AssistantText.T("Abrir ajustes", "Open settings") };
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
            _input.textEdition.placeholder = context != null ? context.Placeholder : string.Empty;
            _input.RegisterCallback<KeyDownEvent>(e =>
            {
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && !e.shiftKey)
                {
                    Send();
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            bottom.Add(_input);
            _send = new Button(Send) { text = AssistantText.T("Enviar", "Send") };
            _send.AddToClassList("story-button");
            bottom.Add(_send);
            var reset = new Button(() =>
            {
                _turns.Clear();
                _log.Clear();
                _sessionId = Guid.NewGuid().ToString("N");
            })
            {
                text = AssistantText.T("Nueva", "New"),
                tooltip = AssistantText.T("Empezar una conversación nueva", "Start a new conversation"),
            };
            reset.AddToClassList("story-button");
            bottom.Add(reset);
            _chat.Add(bottom);
            Add(_chat);

            AssistantSettings.Changed += Refresh;
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                AssistantSettings.Changed -= Refresh;
                AssistantSettings.Changed += Refresh;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => AssistantSettings.Changed -= Refresh);
            Refresh();
        }

        /// Envía el prompt de la acción rápida del contexto, si tiene una.
        public void SendQuickAction()
        {
            if (!AssistantSettings.IsReady) return;
            string prompt = _context?.QuickActionPrompt();
            if (!string.IsNullOrWhiteSpace(prompt)) Send(prompt);
        }

        public void Send()
        {
            var text = _input.value?.Trim();
            if (_busy || string.IsNullOrEmpty(text)) return;
            if (_context == null || !_context.HasTarget)
            {
                Note(AssistantText.T("No hay nada abierto sobre lo que trabajar.", "There is nothing open to work on."), true);
                return;
            }
            _input.value = string.Empty;
            Send(text);
        }

        private void Send(string text)
        {
            if (_busy || string.IsNullOrEmpty(text)) return;
            if (_context == null || !_context.HasTarget)
            {
                Note(AssistantText.T("No hay nada abierto sobre lo que trabajar.", "There is nothing open to work on."), true);
                return;
            }

            Bubble(text, true);
            _turns.Add(new AssistantTurn(true, text));

            _busy = true;
            _send.SetEnabled(false);
            var thinking = Note(AssistantText.T("Pensando…", "Thinking…"));
            string system = _context.RolePrompt() + "\n\n" + _context.Manual() + "\n\n" + _context.State();
            AssistantClient.Send(system, _turns, answer =>
            {
                Done(thinking);
                _turns.Add(new AssistantTurn(false, answer));
                Show(answer);
            }, error =>
            {
                Done(thinking);
                if (_turns.Count > 0) _turns.RemoveAt(_turns.Count - 1);
                Note(AssistantText.T("No se pudo contactar con el servicio: ", "Could not reach the service: ") + error, true);
            }, _sessionId);
        }

        private void Done(VisualElement thinking)
        {
            thinking.RemoveFromHierarchy();
            _busy = false;
            _send.SetEnabled(_context != null && _context.HasTarget);
        }

        private void Show(string answer)
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
            catch (Exception)
            {
                Note(AssistantText.T("El asistente propuso cambios que no se pueden leer.",
                                     "The assistant proposed changes that cannot be read."), true);
            }
        }

        private void Proposal(JArray ops)
        {
            var box = new VisualElement();
            box.AddToClassList("story-assistant__proposal");
            box.Add(new Label(AssistantText.T($"Propone {ops.Count} cambios:", $"Proposes {ops.Count} changes:")) { name = "title" });
            foreach (var op in ops)
            {
                var line = new Label("• " + (_context?.DescribeOp(op as JObject) ?? "?"));
                line.AddToClassList("story-muted");
                box.Add(line);
            }
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            Button apply = null, discard = null;
            apply = new Button(() =>
            {
                var result = _context != null ? _context.Apply(new JObject { ["ops"] = ops }.ToString()) : null;
                bool applied = result != null && result.Applied;
                Note(applied
                        ? AssistantText.T("Cambios aplicados (Ctrl+Z los deshace).", "Changes applied (Ctrl+Z undoes them).") + "\n" + result
                        : result != null ? result.ToString() : "No hay contexto.",
                    !applied);
                _turns.Add(new AssistantTurn(true, applied
                    ? "[I applied your changes.]"
                    : "[Your changes could not be applied:\n" + result + "]"));
                _turns.Add(new AssistantTurn(false, "OK."));
                if (applied)
                {
                    _context?.NotifyChanged();
                    Applied?.Invoke();
                }
                apply.SetEnabled(false);
                discard.SetEnabled(false);
            })
            { text = AssistantText.T("Aplicar", "Apply") };
            apply.AddToClassList("story-button");
            apply.AddToClassList("is-primary");
            discard = new Button(() =>
            {
                _turns.Add(new AssistantTurn(true, "[I discarded those changes.]"));
                _turns.Add(new AssistantTurn(false, "OK."));
                apply.SetEnabled(false);
                discard.SetEnabled(false);
            })
            { text = AssistantText.T("Descartar", "Discard") };
            discard.AddToClassList("story-button");
            buttons.Add(apply);
            buttons.Add(discard);
            box.Add(buttons);
            _log.Add(box);
            ScrollToEnd();
        }

        /// Vuelve a leer si hay contexto y si el servicio está listo. La ventana la llama cuando el
        /// Director cambia de cinemática o de plano.
        public void RefreshState() => Refresh();

        private void Refresh()
        {
            bool ready = AssistantSettings.IsReady;
            _notReady.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            _chat.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            var p = AssistantSettings.Current;
            _service.text = $"{AssistantSettings.Name(p)} · {AssistantSettings.Model(p)}";
            _send.SetEnabled(!_busy && _context != null && _context.HasTarget);
        }

        private void Bubble(string text, bool mine)
        {
            var label = new Label(text) { selection = { isSelectable = true } };
            label.AddToClassList("story-assistant__bubble");
            label.EnableInClassList("is-mine", mine);
            _log.Add(label);
            ScrollToEnd();
        }

        private Label Note(string text, bool warning = false)
        {
            var label = new Label(text);
            label.AddToClassList(warning ? "story-notice" : "story-muted");
            if (warning) label.AddToClassList("is-warning");
            _log.Add(label);
            ScrollToEnd();
            return label;
        }

        private void ScrollToEnd()
            => _log.schedule.Execute(() => _log.scrollOffset = new Vector2(0, float.MaxValue));
    }
}
