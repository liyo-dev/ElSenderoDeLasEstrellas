using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// The graph as plain text: a chapter read top to bottom, and a manual of every step type.
    /// Meant for people and for any tool or assistant that works with the graph.
    public static class NarrativeGraphText
    {
        /// The chapter in story order: sections, numbered steps, what each does and where it leads.
        public static string Chapter(NarrativeGraph graph)
        {
            if (graph == null) return string.Empty;
            var s = StoryStructure.Build(graph);
            var sb = new StringBuilder();
            sb.AppendLine($"# {graph.name}");
            sb.AppendLine();
            var start = graph.FindNode(graph.startNodeGuid);
            sb.AppendLine($"Start: {(start != null ? s.Number(start) : "none")}");
            foreach (var section in s.Sections)
            {
                sb.AppendLine();
                sb.AppendLine($"## {section.Index + 1}. {StoryStructure.DisplayName(section.Name)}");
                foreach (var node in section.Steps)
                {
                    sb.AppendLine();
                    string title = string.IsNullOrWhiteSpace(node.displayTitle) ? "(untitled)" : node.displayTitle;
                    sb.AppendLine($"### {s.Number(node)} {title}");
                    sb.AppendLine($"- type: {node.GetType().Name} ({NarrativeNodeCatalog.TitleOf(node.GetType())})");
                    sb.AppendLine($"- id: {node.guid}");
                    var summary = StepSummary.For(node, 400);
                    if (!string.IsNullOrWhiteSpace(summary)) sb.AppendLine($"- does: {summary}");
                    foreach (var field in EditableFields(node.GetType()))
                    {
                        var value = Format(field.GetValue(node));
                        if (!string.IsNullOrEmpty(value)) sb.AppendLine($"- {field.Name}: {value}");
                    }
                    var ports = node.GetOutputPorts();
                    if (node.outputs != null)
                        for (int i = 0; i < node.outputs.Count; i++)
                        {
                            var target = graph.FindNode(node.outputs[i]);
                            string label = ports != null && i < ports.Length ? ports[i] : "next";
                            sb.AppendLine($"- {label} → {(target != null ? s.Number(target) : "nothing")}");
                        }
                    foreach (var w in StepSummary.Warnings(node)) sb.AppendLine($"- warning: {w}");
                }
            }
            return sb.ToString();
        }

        /// Every step type the project has, with its fields and ways out.
        public static string Manual()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Step types");
            sb.AppendLine();
            sb.AppendLine("Each step does one thing when the story reaches it. Fields are set per step; ways out are the named outputs (a step without them simply continues, and several connections from it run in parallel).");
            foreach (var group in NarrativeNodeCatalog.All.Where(e => !e.obsolete).GroupBy(e => e.category).OrderBy(g => NarrativeNodeCatalog.CategoryOrder(g.Key)))
            {
                sb.AppendLine();
                sb.AppendLine($"## {group.Key}");
                foreach (var entry in group.OrderBy(e => e.title))
                {
                    sb.AppendLine();
                    sb.AppendLine($"### {entry.title} (`{entry.type.Name}`)");
                    if (!string.IsNullOrWhiteSpace(entry.description)) sb.AppendLine(entry.description);
                    foreach (var field in EditableFields(entry.type))
                    {
                        var tip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
                        sb.AppendLine($"- `{field.Name}` ({TypeName(field.FieldType)}){(string.IsNullOrEmpty(tip) ? string.Empty : ": " + tip)}");
                    }
                    try
                    {
                        var sample = (NarrativeNode)Activator.CreateInstance(entry.type);
                        var ports = sample.GetOutputPorts();
                        if (ports != null) sb.AppendLine($"- ways out: {string.Join(" / ", ports)}");
                    }
                    catch (Exception) { }
                }
            }
            return sb.ToString();
        }

        /// Public, serialized, visible fields a person can edit on a step.
        public static FieldInfo[] EditableFields(Type type)
            => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                   .Where(f => f.DeclaringType != typeof(NarrativeNode))
                   .Where(f => f.GetCustomAttribute<HideInInspector>() == null && f.GetCustomAttribute<NonSerializedAttribute>() == null)
                   .ToArray();

        static string Format(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case string str: return str;
                case UnityEngine.Object obj: return obj != null ? obj.name : string.Empty;
                case System.Collections.IEnumerable list:
                    var items = list.Cast<object>().Select(o => o is UnityEngine.Object u ? (u != null ? u.name : "") : JsonOrString(o)).Where(x => !string.IsNullOrEmpty(x)).ToList();
                    return items.Count == 0 ? string.Empty : "[" + string.Join("; ", items) + "]";
                default: return JsonOrString(value);
            }
        }

        static string JsonOrString(object o)
        {
            if (o == null) return string.Empty;
            var t = o.GetType();
            if (t.IsPrimitive || t.IsEnum || o is string) return o.ToString();
            var json = JsonUtility.ToJson(o);
            return string.IsNullOrEmpty(json) || json == "{}" ? o.ToString() : json;
        }

        static string TypeName(Type t)
        {
            if (t == typeof(string)) return "text";
            if (t == typeof(int)) return "whole number";
            if (t == typeof(float)) return "number";
            if (t == typeof(bool)) return "yes/no";
            if (t.IsEnum) return string.Join(" | ", Enum.GetNames(t));
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return t.Name + " asset";
            if (t.IsGenericType) return "list of " + TypeName(t.GetGenericArguments()[0]);
            return t.Name;
        }

        [MenuItem("Tools/Narrative Graph/Guardar el manual de pasos")]
        static void SaveManual()
        {
            const string path = "Assets/STEP_TYPES.md";
            System.IO.File.WriteAllText(path, Manual(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
        }
    }
}
