using System;
using Liyodev.Assistant;
using Newtonsoft.Json.Linq;

namespace NarrativeGraphEditor.Story
{
    /// El contexto del grafo para el chat compartido: su manual, el capítulo abierto, el informe
    /// en vivo y el aplicador de operaciones que ya existía (StoryBatchEdit).
    public sealed class StoryAssistantContext : IAssistantContext
    {
        private readonly Func<NarrativeGraph> _graph;
        private readonly Func<NarrativeNode> _step;

        public StoryAssistantContext(Func<NarrativeGraph> graph, Func<NarrativeNode> step)
        {
            _graph = graph;
            _step = step;
        }

        public string Id => "narrative-graph";
        public string Name => StoryText.T("Historia", "Story");
        public string TargetName => _graph()?.name ?? string.Empty;
        public bool HasTarget => _graph() != null;
        public string Placeholder => StoryText.T("¿Qué quieres hacer en este capítulo?", "What do you want to do in this chapter?");

        public string Intro => StoryText.T(
            "Escribe lo que quieres y el asistente lee el capítulo, te contesta y te propone cambios que tú aplicas. Conecta tu IA (tu cuenta de ChatGPT, Google o Claude, sin clave) o usa una clave de API.",
            "Write what you want: the assistant reads the chapter, answers and proposes changes you apply. Connect your AI (your ChatGPT, Google or Claude account, no key) or use an API key.");

        public event Action Changed;

        public string RolePrompt() =>
            "You help a game writer work on a chapter of a narrative graph in Unity. Answer in the language the user writes in, briefly.\n" +
            "Steps are numbered by section ('2.3'). To propose changes, end your answer with ONE fenced json block {\"ops\":[...]}; the user reviews it and applies it or not. " +
            "Operations: add {id, type, title, after, port, fields:[{name,value}]}, set {step, fields}, connect {from, to, port}, disconnect {from, port}, section {step, value}, delete {step}. " +
            "Every operation is an object whose \"op\" key names it, for example: {\"ops\":[{\"op\":\"set\",\"step\":\"2.4\",\"fields\":[{\"name\":\"displayTitle\",\"value\":\"New title\"}]}]}. " +
            "Steps are named by number, id, or the id of a step added earlier in the same block. Field values are text. Use only step types and fields from the manual.\n\n" +
            "When asked why something is missing or wrong in the game, use the story state report below: where the story is, what it waits for and what does not match.";

        public string Manual() => NarrativeGraphText.Manual();

        public string State()
        {
            var graph = _graph();
            if (graph == null) return string.Empty;
            return NarrativeGraphText.Chapter(graph) + "\n\n" + StoryLiveReport.Build(graph, _step != null ? _step() : null).ToText();
        }

        public string DescribeOp(JObject op)
        {
            if (op == null) return "?";
            string o = (string)op["op"];
            return o switch
            {
                "add" => StoryText.T(
                    $"añadir {(string)op["type"]} «{(string)op["title"]}» tras {(string)op["after"]}",
                    $"add {(string)op["type"]} “{(string)op["title"]}” after {(string)op["after"]}"),
                "set" => StoryText.T($"cambiar {(string)op["step"]}", $"change {(string)op["step"]}"),
                "connect" => StoryText.T($"conectar {(string)op["from"]} → {(string)op["to"]}", $"connect {(string)op["from"]} → {(string)op["to"]}"),
                "disconnect" => StoryText.T($"desconectar {(string)op["from"]}", $"disconnect {(string)op["from"]}"),
                "section" => StoryText.T(
                    $"{(string)op["step"]} abre la sección «{(string)op["value"]}»",
                    $"{(string)op["step"]} opens section “{(string)op["value"]}”"),
                "delete" => StoryText.T($"borrar {(string)op["step"]}", $"delete {(string)op["step"]}"),
                _ => o,
            };
        }

        public AssistantApplyResult Apply(string json)
        {
            var result = new AssistantApplyResult();
            var graph = _graph();
            if (graph == null)
            {
                result.Errors.Add(StoryText.T("No hay ningún capítulo abierto.", "No chapter is open."));
                return result;
            }

            var applied = StoryBatchEdit.Apply(graph, json);
            result.Applied = applied.Applied;
            result.Done.AddRange(applied.Done);
            result.Errors.AddRange(applied.Errors);
            return result;
        }

        public string QuickActionLabel => null;
        public string QuickActionPrompt() => null;

        public void NotifyChanged() => Changed?.Invoke();
    }
}
