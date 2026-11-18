using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.Assistant
{
    /// La caja de pedidos a la IA: una vista que se puede meter en cualquier ventana y que habla con
    /// el contexto que le pasen. No es un chat: cada pedido va solo (con el estado de lo que se mira,
    /// que ya lleva las marcas y el contexto), se enseña lo que contesta y los cambios que propone, y
    /// el siguiente pedido empieza de cero. Así cada petición gasta lo justo.
    public sealed class AssistantView : VisualElement
    {
        private static readonly Regex ChangesBlock =
            new(@"```(?:json)?\s*(\{[\s\S]*?""ops""[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);

        private readonly IAssistantContext _context;
        private readonly ScrollView _log;
        private readonly TextField _input;
        private readonly Button _send;
        private readonly VisualElement _notReady;
        private readonly VisualElement _chat;
        private readonly List<AssistantTurn> _turns = new();
        private string _sessionId = Guid.NewGuid().ToString("N");
        private bool _busy;

        public event Action SettingsRequested;
        public event Action Applied;

        /// True mientras la IA trabaja en un pedido. Quien aloja la caja puede reflejarlo (un botón
        /// que pasa a «trabajando»).
        public bool Busy => _busy;

        public event Action BusyChanged;

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
            var conectar = new Button(AssistantConnectWindow.Open)
                { text = AssistantText.T("Conectar mi IA (ChatGPT, Google o Claude)", "Connect my AI (ChatGPT, Google or Claude)") };
            conectar.AddToClassList("story-button");
            conectar.AddToClassList("is-primary");
            conectar.style.marginTop = 10;
            _notReady.Add(conectar);
            var open = new Button(() => SettingsRequested?.Invoke())
                { text = "⚙  " + AssistantText.T("Usar una clave de API u Ollama", "Use an API key or Ollama") };
            open.AddToClassList("story-button");
            open.style.marginTop = 6;
            _notReady.Add(open);
            Add(_notReady);

            _chat = new VisualElement { style = { flexGrow = 1 } };
            _log = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _log.AddToClassList("story-panel__scroll");
            _log.style.flexGrow = 1;
            _log.style.display = DisplayStyle.None;
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
            _send = new Button(Send) { text = AssistantText.T("Pedir a la IA", "Ask the AI") };
            _send.AddToClassList("story-button");
            _send.AddToClassList("is-primary");
            bottom.Add(_send);
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
            if (!string.IsNullOrWhiteSpace(prompt)) Send(prompt, _context.QuickActionLabel);
        }

        public void Send()
        {
            var text = _input.value?.Trim();
            if (_busy || string.IsNullOrEmpty(text)) return;
            if (_context == null)
            {
                Note(AssistantText.T("No hay ninguna herramienta abierta con la que hablar.", "There is no tool open to talk to."), true);
                return;
            }
            _input.value = string.Empty;
            Send(text);
        }

        /// `etiqueta`: lo que se enseña del pedido cuando el texto es una instrucción interna (la de
        /// una acción rápida); si no, se enseña el texto tal cual.
        private void Send(string text, string etiqueta = null)
        {
            if (_busy || string.IsNullOrEmpty(text)) return;
            if (_context == null)
            {
                Note(AssistantText.T("No hay ninguna herramienta abierta con la que hablar.", "There is no tool open to talk to."), true);
                return;
            }

            // Cada pedido empieza de cero: lo de antes ya está aplicado o descartado.
            _turns.Clear();
            _log.Clear();
            AjustarRegistro();
            _sessionId = Guid.NewGuid().ToString("N");
            Bubble(string.IsNullOrEmpty(etiqueta) ? text : etiqueta, true);
            _turns.Add(new AssistantTurn(true, text));

            _busy = true;
            _send.text = AssistantText.T("Trabajando…", "Working…");
            BusyChanged?.Invoke();
            _send.SetEnabled(false);
            var thinking = Note(AssistantText.T("Trabajando…", "Working…"));
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
                Note(AssistantText.T("La IA no ha contestado. ", "The AI did not answer. ") + error, true);
            }, _sessionId);
        }

        private void Done(VisualElement thinking)
        {
            thinking.RemoveFromHierarchy();
            _busy = false;
            _send.text = AssistantText.T("Pedir a la IA", "Ask the AI");
            BusyChanged?.Invoke();
            _send.SetEnabled(_context != null);
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
                if (ops != null)
                    foreach (var op in ops)
                        NormalizarOp(op as JObject);
                if (ops != null && ops.Count > 0) Proposal(ops);
            }
            catch (Exception)
            {
                Note(AssistantText.T("El asistente propuso cambios que no se pueden leer.",
                                     "The assistant proposed changes that cannot be read."), true);
            }
        }

        /// Algunos modelos llaman a la operación «action» u «operation» en vez de «op».
        private static void NormalizarOp(JObject op)
        {
            if (op == null || op["op"] != null) return;
            var nombre = op["action"] ?? op["operation"];
            if (nombre != null) op["op"] = nombre.ToString();
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
            _send.SetEnabled(!_busy && _context != null);
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
        {
            AjustarRegistro();
            _log.schedule.Execute(() => _log.scrollOffset = new Vector2(0, float.MaxValue));
        }

        /// Sin pedido no hay nada que enseñar: la zona de respuestas solo ocupa sitio cuando la hay.
        private void AjustarRegistro()
            => _log.style.display = _log.contentContainer.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
