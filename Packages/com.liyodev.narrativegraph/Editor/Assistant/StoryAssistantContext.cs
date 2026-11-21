using System;
using System.Collections.Generic;
using System.Linq;
using Liyodev.Assistant;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// El contexto del grafo para el chat compartido y su servidor MCP: el manual de pasos, el
    /// capítulo activo, el informe en vivo y el aplicador de operaciones (StoryBatchEdit). Está
    /// siempre registrado (AssistantContexts lo encuentra solo), aunque el editor del grafo esté
    /// cerrado: entonces trabaja sobre el último capítulo abierto o el que se elija desde fuera.
    public sealed class StoryAssistantContext : IAssistantContext, IAssistantContextSelection
    {
        public const string ContextId = "narrative-graph";

        public StoryAssistantContext()
        {
            StoryGraphWindow.ActiveChanged -= NotifyChanged;
            StoryGraphWindow.ActiveChanged += NotifyChanged;
        }

        static NarrativeGraph Graph => StoryGraphWindow.ActiveGraph;

        public string Id => ContextId;
        public string Name => StoryText.T("Historia", "Story");
        public string TargetName => Graph != null ? Graph.name : string.Empty;
        public bool HasTarget => Graph != null;
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
            var graph = Graph;
            if (graph == null)
                return "# Capítulos del proyecto (ninguno elegido)\nNo propongas cambios: elige un capítulo (context_select) o ábrelo en el editor.\n"
                       + string.Join("\n", AllGraphs().Select(g => "- " + g.name));
            return NarrativeGraphText.Chapter(graph) + "\n\n" + StoryLiveReport.Build(graph, StoryGraphWindow.ActiveStep).ToText();
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
            var graph = Graph;
            if (graph == null)
            {
                result.Errors.Add(StoryText.T("No hay ningún capítulo elegido.", "No chapter is selected."));
                return result;
            }

            var applied = StoryBatchEdit.Apply(graph, json);
            result.Applied = applied.Applied;
            result.Done.AddRange(applied.Done);
            result.Errors.AddRange(applied.Errors);
            if (applied.Applied)
            {
                AssetDatabase.SaveAssetIfDirty(graph);
                NarrativeProjectIndex.InvalidateGraphs();
                StoryGraphWindow.GraphChangedOutside(graph);
            }
            return result;
        }

        public IReadOnlyList<AssistantTarget> Targets()
            => AllGraphs().Select(g => new AssistantTarget { id = g.name, name = g.name }).ToList();

        public bool Select(string id)
        {
            var graph = AllGraphs().FirstOrDefault(g => string.Equals(g.name, id, StringComparison.OrdinalIgnoreCase));
            if (graph == null) return false;
            StoryGraphWindow.SetActiveGraph(graph);
            return true;
        }

        static IEnumerable<NarrativeGraph> AllGraphs()
            => AssetDatabase.FindAssets("t:" + nameof(NarrativeGraph))
                .Select(g => AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(g => g != null)
                .OrderBy(g => g.name, StringComparer.OrdinalIgnoreCase);

        public string QuickActionLabel => null;
        public string QuickActionPrompt() => null;

        public void NotifyChanged() => Changed?.Invoke();
    }
}
