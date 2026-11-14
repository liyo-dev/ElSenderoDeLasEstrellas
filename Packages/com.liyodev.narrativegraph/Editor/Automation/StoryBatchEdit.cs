using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// Several changes to a graph described as data and applied all together, or none if any of
    /// them is wrong. Steps are named by their number ("2.3"), their id, or the "id" given to a
    /// step added earlier in the same batch. Meant for scripts, tools and assistants.
    ///
    /// { "ops": [
    ///   { "op": "add", "id": "greet", "type": "SetFlagNode", "title": "Greets", "after": "2.3",
    ///     "fields": [ { "name": "flagKey", "value": "GREETED" } ] },
    ///   { "op": "set", "step": "2.4", "fields": [ { "name": "displayTitle", "value": "New title" } ] },
    ///   { "op": "connect", "from": "greet", "to": "2.4", "port": "" },
    ///   { "op": "disconnect", "from": "2.3", "port": "No" },
    ///   { "op": "section", "step": "greet", "value": "Arrival" },
    ///   { "op": "delete", "step": "2.5" } ] }
    public static class StoryBatchEdit
    {
        [Serializable] public sealed class Field { public string name; public string value; }

        [Serializable]
        public sealed class Op
        {
            public string op;
            public string id;
            public string type;
            public string title;
            public string after;
            public string step;
            public string from;
            public string to;
            public string port;
            public string value;
            public List<Field> fields = new();
        }

        [Serializable] public sealed class Batch { public List<Op> ops = new(); }

        public sealed class Result
        {
            public bool Applied;
            public readonly List<string> Errors = new();
            public readonly List<string> Done = new();
            public override string ToString() => Applied ? string.Join("\n", Done) : "Not applied:\n" + string.Join("\n", Errors);
        }

        public static Result Apply(NarrativeGraph graph, string json)
        {
            var result = new Result();
            Batch batch;
            try { batch = JsonUtility.FromJson<Batch>(json); }
            catch (Exception e) { result.Errors.Add("Invalid JSON: " + e.Message); return result; }
            if (graph == null) { result.Errors.Add("No graph."); return result; }
            if (batch?.ops == null || batch.ops.Count == 0) { result.Errors.Add("No operations."); return result; }

            // Check everything first; nothing changes unless all operations are valid.
            var structure = StoryStructure.Build(graph);
            var planned = new Dictionary<string, Type>();
            for (int i = 0; i < batch.ops.Count; i++)
                Check(graph, structure, batch.ops[i], i + 1, planned, result.Errors);
            if (result.Errors.Count > 0) return result;

            Undo.RecordObject(graph, "Batch edit");
            var created = new Dictionary<string, NarrativeNode>();
            foreach (var op in batch.ops)
            {
                try { Run(graph, structure, op, created, result.Done); }
                catch (Exception e)
                {
                    result.Errors.Add($"{op.op}: {e.Message}");
                    Undo.PerformUndo();
                    return result;
                }
            }
            EditorUtility.SetDirty(graph);
            result.Applied = true;
            return result;
        }

        static void Check(NarrativeGraph graph, StoryStructure s, Op op, int n, Dictionary<string, Type> planned, List<string> errors)
        {
            string where = $"#{n} {op.op}";
            bool Known(string reference) => !string.IsNullOrEmpty(reference) && (planned.ContainsKey(reference) || Find(graph, s, reference, null) != null);
            Type TypeOf(string reference)
                => planned.TryGetValue(reference ?? string.Empty, out var t) ? t : Find(graph, s, reference, null)?.GetType();

            switch (op.op)
            {
                case "add":
                    var type = StepType(op.type);
                    if (type == null) { errors.Add($"{where}: unknown step type '{op.type}'."); return; }
                    if (!string.IsNullOrEmpty(op.id))
                    {
                        if (planned.ContainsKey(op.id)) errors.Add($"{where}: id '{op.id}' used twice.");
                        planned[op.id] = type;
                    }
                    if (!string.IsNullOrEmpty(op.after) && !Known(op.after)) errors.Add($"{where}: step '{op.after}' not found.");
                    CheckFields(type, op.fields, where, errors);
                    break;
                case "set":
                    if (!Known(op.step)) { errors.Add($"{where}: step '{op.step}' not found."); return; }
                    CheckFields(TypeOf(op.step), op.fields, where, errors);
                    break;
                case "connect":
                    if (!Known(op.from)) errors.Add($"{where}: step '{op.from}' not found.");
                    if (!Known(op.to)) errors.Add($"{where}: step '{op.to}' not found.");
                    CheckPort(TypeOf(op.from), op.port, where, errors);
                    break;
                case "disconnect":
                    if (!Known(op.from)) errors.Add($"{where}: step '{op.from}' not found.");
                    break;
                case "delete":
                case "section":
                    if (!Known(op.step)) errors.Add($"{where}: step '{op.step}' not found.");
                    break;
                default:
                    errors.Add($"{where}: unknown operation (use add, set, connect, disconnect, section or delete).");
                    break;
            }
        }

        static void Run(NarrativeGraph graph, StoryStructure s, Op op, Dictionary<string, NarrativeNode> created, List<string> done)
        {
            switch (op.op)
            {
                case "add":
                {
                    var node = (NarrativeNode)Activator.CreateInstance(StepType(op.type));
                    node.displayTitle = op.title ?? string.Empty;
                    node.outputs = new List<string>();
                    var ports = node.GetOutputPorts();
                    if (ports != null) foreach (var _ in ports) node.outputs.Add(string.Empty);
                    var previous = Find(graph, s, op.after, created);
                    node.position = previous != null ? previous.position + new Vector2(StoryLayout.CardWidth + StoryLayout.ColumnGap, 0f) : Vector2.zero;
                    SetFields(node, op.fields);
                    graph.nodes.Add(node);
                    if (!string.IsNullOrEmpty(op.id)) created[op.id] = node;
                    if (previous != null) Connect(previous, node, op.port);
                    if (string.IsNullOrEmpty(graph.startNodeGuid)) graph.startNodeGuid = node.guid;
                    done.Add($"added {node.GetType().Name} '{node.displayTitle}' ({node.guid})");
                    break;
                }
                case "set":
                {
                    var node = Find(graph, s, op.step, created);
                    SetFields(node, op.fields);
                    done.Add($"changed '{node.displayTitle}'");
                    break;
                }
                case "connect":
                {
                    var from = Find(graph, s, op.from, created);
                    var to = Find(graph, s, op.to, created);
                    Connect(from, to, op.port);
                    done.Add($"connected '{from.displayTitle}' → '{to.displayTitle}'");
                    break;
                }
                case "disconnect":
                {
                    var from = Find(graph, s, op.from, created);
                    int index = PortIndex(from, op.port);
                    if (from.HasNamedOutputs) { if (index >= 0 && index < from.outputs.Count) from.outputs[index] = string.Empty; }
                    else from.outputs.Clear();
                    done.Add($"disconnected '{from.displayTitle}'");
                    break;
                }
                case "section":
                {
                    var node = Find(graph, s, op.step, created);
                    node.section = op.value ?? string.Empty;
                    done.Add($"'{node.displayTitle}' opens section '{node.section}'");
                    break;
                }
                case "delete":
                {
                    var node = Find(graph, s, op.step, created);
                    foreach (var other in graph.nodes)
                    {
                        if (other?.outputs == null) continue;
                        if (other.HasNamedOutputs)
                        {
                            for (int i = 0; i < other.outputs.Count; i++) if (other.outputs[i] == node.guid) other.outputs[i] = string.Empty;
                        }
                        else other.outputs.RemoveAll(g => g == node.guid);
                    }
                    graph.nodes.Remove(node);
                    if (graph.startNodeGuid == node.guid) graph.startNodeGuid = null;
                    done.Add($"deleted '{node.displayTitle}'");
                    break;
                }
            }
        }

        static NarrativeNode Find(NarrativeGraph graph, StoryStructure s, string reference, Dictionary<string, NarrativeNode> created)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            if (created != null && created.TryGetValue(reference, out var made)) return made;
            var byGuid = graph.FindNode(reference);
            if (byGuid != null) return byGuid;
            return s.StoryOrder.FirstOrDefault(n => s.Number(n) == reference);
        }

        static Type StepType(string name)
            => string.IsNullOrEmpty(name) ? null
             : NarrativeNodeCatalog.All.Select(e => e.type).FirstOrDefault(t => t.Name == name || t.FullName == name);

        static void Connect(NarrativeNode from, NarrativeNode to, string port)
        {
            var names = from.GetOutputPorts();
            if (names == null)
            {
                if (!from.outputs.Contains(to.guid)) from.outputs.Add(to.guid);
                return;
            }
            while (from.outputs.Count < names.Length) from.outputs.Add(string.Empty);
            int index = PortIndex(from, port);
            if (index < 0) index = from.outputs.FindIndex(string.IsNullOrEmpty);
            if (index < 0) throw new InvalidOperationException($"'{from.displayTitle}' has no free way out.");
            from.outputs[index] = to.guid;
        }

        static int PortIndex(NarrativeNode node, string port)
        {
            var names = node.GetOutputPorts();
            if (names == null || string.IsNullOrEmpty(port)) return -1;
            return Array.FindIndex(names, n => string.Equals(n, port, StringComparison.OrdinalIgnoreCase));
        }

        static void CheckPort(Type type, string port, string where, List<string> errors)
        {
            if (type == null || string.IsNullOrEmpty(port)) return;
            NarrativeNode sample;
            try { sample = (NarrativeNode)Activator.CreateInstance(type); } catch (Exception) { return; }
            var names = sample.GetOutputPorts();
            if (names != null && !names.Any(n => string.Equals(n, port, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"{where}: '{type.Name}' has no way out called '{port}' (it has {string.Join(", ", names)}).");
        }

        static void CheckFields(Type type, List<Field> fields, string where, List<string> errors)
        {
            if (type == null || fields == null) return;
            foreach (var f in fields)
            {
                var info = type.GetField(f.name ?? string.Empty, BindingFlags.Instance | BindingFlags.Public);
                if (info == null) { errors.Add($"{where}: '{type.Name}' has no field '{f.name}'."); continue; }
                if (!TryConvert(info.FieldType, f.value, out _, out var why)) errors.Add($"{where}: field '{f.name}': {why}");
            }
        }

        static void SetFields(NarrativeNode node, List<Field> fields)
        {
            if (fields == null) return;
            foreach (var f in fields)
            {
                var info = node.GetType().GetField(f.name, BindingFlags.Instance | BindingFlags.Public);
                TryConvert(info.FieldType, f.value, out var value, out _);
                info.SetValue(node, value);
            }
        }

        static bool TryConvert(Type type, string text, out object value, out string why)
        {
            value = null;
            why = null;
            text ??= string.Empty;
            if (type == typeof(string)) { value = text; return true; }
            if (type == typeof(bool))
            {
                if (bool.TryParse(text, out var b)) { value = b; return true; }
                why = "expected true or false"; return false;
            }
            if (type == typeof(int))
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; }
                why = "expected a whole number"; return false;
            }
            if (type == typeof(float))
            {
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fl)) { value = fl; return true; }
                why = "expected a number"; return false;
            }
            if (type.IsEnum)
            {
                if (Enum.TryParse(type, text, true, out var e)) { value = e; return true; }
                why = $"expected one of {string.Join(", ", Enum.GetNames(type))}"; return false;
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                if (text.Length == 0) return true;
                var asset = AssetDatabase.LoadAssetAtPath(text, type);
                if (asset == null)
                {
                    var guid = AssetDatabase.FindAssets($"{text} t:{type.Name}")
                        .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == text);
                    if (guid != null) asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);
                }
                if (asset != null) { value = asset; return true; }
                why = $"no {type.Name} asset called '{text}'"; return false;
            }
            why = $"{type.Name} fields cannot be set from text yet"; return false;
        }
    }
}
