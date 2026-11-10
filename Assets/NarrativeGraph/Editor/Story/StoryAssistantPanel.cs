using System;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Optional chat inside the editor. Each user brings their own API key; nothing is sent until
    /// they write. Changes the assistant proposes are shown first and applied only on "Apply".
    public sealed class StoryAssistantPanel : VisualElement
    {
        const string KeyPref = "NarrativeGraph.Assistant.Key";
        const string ModelPref = "NarrativeGraph.Assistant.Model";
        const string Endpoint = "https://api.anthropic.com/v1/messages";

        public Func<NarrativeGraph> Graph;
        public event Action Changed;

        readonly ScrollView _log;
        readonly TextField _input;
        readonly Button _send;
        readonly VisualElement _setup;
        readonly VisualElement _chat;
        JArray _messages = new();
        string _pendingToolId;
        string _pendingResult;
        bool _busy;

        public StoryAssistantPanel()
        {
            AddToClassList("story-panel");
            style.flexGrow = 1;

            _setup = new VisualElement();
            _setup.AddToClassList("story-panel__scroll");
            var intro = new Label(StoryText.T(
                "Escribe lo que quieres y el asistente lee el capítulo, te contesta y te propone cambios que tú aplicas. Usa tu propia clave de API de Anthropic; se guarda solo en este ordenador.",
                "Write what you want: the assistant reads the chapter, answers and proposes changes you apply. It uses your own Anthropic API key, stored only on this computer."));
            intro.AddToClassList("story-muted");
            _setup.Add(intro);
            var key = new TextField(StoryText.T("Clave de API", "API key")) { isPasswordField = true, value = EditorPrefs.GetString(KeyPref, string.Empty) };
            var model = new TextField(StoryText.T("Modelo", "Model")) { value = EditorPrefs.GetString(ModelPref, "claude-sonnet-4-5") };
            _setup.Add(key);
            _setup.Add(model);
            var save = new Button(() =>
            {
                EditorPrefs.SetString(KeyPref, key.value.Trim());
                EditorPrefs.SetString(ModelPref, model.value.Trim());
                Refresh();
            }) { text = StoryText.T("Guardar", "Save") };
            save.AddToClassList("story-button");
            _setup.Add(save);
            Add(_setup);

            _chat = new VisualElement { style = { flexGrow = 1 } };
            _log = new ScrollView();
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
            var reset = new Button(() => { _messages = new JArray(); _pendingToolId = null; _log.Clear(); })
                { text = StoryText.T("Nueva", "New"), tooltip = StoryText.T("Empezar una conversación nueva", "Start a new conversation") };
            reset.AddToClassList("story-button");
            bottom.Add(reset);
            var settings = new Button(() => { EditorPrefs.DeleteKey(KeyPref); Refresh(); })
                { text = "⚙", tooltip = StoryText.T("Cambiar la clave", "Change the key") };
            settings.AddToClassList("story-button");
            bottom.Add(settings);
            _chat.Add(bottom);
            Add(_chat);

            Refresh();
        }

        void Refresh()
        {
            bool hasKey = !string.IsNullOrEmpty(EditorPrefs.GetString(KeyPref, string.Empty));
            _setup.style.display = hasKey ? DisplayStyle.None : DisplayStyle.Flex;
            _chat.style.display = hasKey ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Send()
        {
            var text = _input.value?.Trim();
            var graph = Graph?.Invoke();
            if (_busy || string.IsNullOrEmpty(text) || graph == null) return;
            _input.value = string.Empty;
            Bubble(text, true);

            var content = new JArray();
            if (_pendingToolId != null)
            {
                content.Add(ToolResult(_pendingToolId, _pendingResult ?? "The user did not apply these changes."));
                _pendingToolId = null;
            }
            content.Add(new JObject { ["type"] = "text", ["text"] = text });
            _messages.Add(new JObject { ["role"] = "user", ["content"] = content });
            Request(graph);
        }

        void Request(NarrativeGraph graph)
        {
            var body = new JObject
            {
                ["model"] = EditorPrefs.GetString(ModelPref, "claude-sonnet-4-5"),
                ["max_tokens"] = 4096,
                ["system"] = SystemPrompt(graph),
                ["messages"] = _messages,
                ["tools"] = new JArray { EditTool() },
            };
            var request = new UnityWebRequest(Endpoint, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString())),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("content-type", "application/json");
            request.SetRequestHeader("x-api-key", EditorPrefs.GetString(KeyPref, string.Empty));
            request.SetRequestHeader("anthropic-version", "2023-06-01");

            SetBusy(true);
            var thinking = Note(StoryText.T("Pensando…", "Thinking…"));
            request.SendWebRequest().completed += _ =>
            {
                thinking.RemoveFromHierarchy();
                SetBusy(false);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Note(StoryText.T($"No se pudo contactar: {request.error}. {request.downloadHandler.text}", $"Could not reach the service: {request.error}. {request.downloadHandler.text}"), true);
                    _messages.RemoveAt(_messages.Count - 1);
                }
                else OnAnswer(JObject.Parse(request.downloadHandler.text));
                request.Dispose();
            };
        }

        void OnAnswer(JObject answer)
        {
            var content = answer["content"] as JArray ?? new JArray();
            _messages.Add(new JObject { ["role"] = "assistant", ["content"] = content });
            foreach (var block in content)
            {
                var type = (string)block["type"];
                if (type == "text") Bubble((string)block["text"], false);
                else if (type == "tool_use" && (string)block["name"] == "edit_chapter")
                    Proposal((string)block["id"], block["input"] as JObject);
            }
        }

        void Proposal(string toolId, JObject input)
        {
            _pendingToolId = toolId;
            _pendingResult = null;
            var ops = input?["ops"] as JArray ?? new JArray();
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
                var graph = Graph?.Invoke();
                var result = StoryBatchEdit.Apply(graph, new JObject { ["ops"] = ops }.ToString());
                _pendingResult = result.Applied ? "Applied:\n" + result : result.ToString();
                Note(result.Applied ? StoryText.T("Cambios aplicados (Ctrl+Z los deshace).", "Changes applied (Ctrl+Z undoes them).") : result.ToString(), !result.Applied);
                if (result.Applied) Changed?.Invoke();
                apply.SetEnabled(false);
                discard.SetEnabled(false);
            }) { text = StoryText.T("Aplicar", "Apply") };
            apply.AddToClassList("story-button");
            apply.AddToClassList("is-primary");
            discard = new Button(() =>
            {
                _pendingResult = "The user discarded these changes.";
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
                "add" => $"añadir {(string)op["type"]} «{(string)op["title"]}» tras {(string)op["after"]}",
                "set" => $"cambiar {(string)op["step"]}",
                "connect" => $"conectar {(string)op["from"]} → {(string)op["to"]}",
                "disconnect" => $"desconectar {(string)op["from"]}",
                "section" => $"{(string)op["step"]} abre la sección «{(string)op["value"]}»",
                "delete" => $"borrar {(string)op["step"]}",
                _ => o,
            };
        }

        static JObject ToolResult(string id, string text)
            => new() { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = text };

        static string SystemPrompt(NarrativeGraph graph)
            => "You help a game writer work on a chapter of a narrative graph in Unity. Answer in the language the user writes in. " +
               "Steps are numbered by section ('2.3'). To change the chapter, call edit_chapter once with all the operations; the user reviews and applies them. " +
               "Use only step types and fields from the manual.\n\n" +
               NarrativeGraphText.Manual() + "\n\n" + NarrativeGraphText.Chapter(graph);

        static JObject EditTool() => JObject.Parse(@"{
            ""name"": ""edit_chapter"",
            ""description"": ""Proposes several changes to the chapter, applied together or not at all. Steps are named by number ('2.3'), id, or the id of a step added earlier in the same batch. Operations: add {id, type, title, after, port, fields:[{name,value}]}, set {step, fields}, connect {from, to, port}, disconnect {from, port}, section {step, value}, delete {step}."",
            ""input_schema"": { ""type"": ""object"", ""properties"": { ""ops"": { ""type"": ""array"", ""items"": { ""type"": ""object"" } } }, ""required"": [""ops""] }
        }");

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

        void SetBusy(bool busy)
        {
            _busy = busy;
            _send.SetEnabled(!busy);
        }

        void ScrollToEnd() => _log.schedule.Execute(() => _log.scrollOffset = new Vector2(0, float.MaxValue));
    }
}
