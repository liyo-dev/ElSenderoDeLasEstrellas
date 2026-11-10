using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor.Story
{
    /// Every change the story editor makes to a graph goes through here, with undo and dirtying.
    public sealed class StoryGraphEditing
    {
        public NarrativeGraph Graph { get; }
        public event Action Changed;

        public StoryGraphEditing(NarrativeGraph graph) { Graph = graph; }

        void Begin(string undoName) => Undo.RecordObject(Graph, undoName);

        void End()
        {
            EditorUtility.SetDirty(Graph);
            Changed?.Invoke();
        }

        public NarrativeNode CreateStep(Type type, Vector2 position, string section)
        {
            if (Graph == null || type == null) return null;
            Begin(StoryText.T("Añadir paso", "Add step"));
            var node = (NarrativeNode)Activator.CreateInstance(type);
            node.position = position;
            node.section = section ?? string.Empty;
            node.outputs ??= new List<string>();
            var ports = node.GetOutputPorts();
            if (ports != null)
                for (int i = 0; i < ports.Length; i++) node.outputs.Add(string.Empty);

            Graph.nodes.Add(node);
            bool noStart = string.IsNullOrEmpty(Graph.startNodeGuid) || Graph.FindNode(Graph.startNodeGuid) == null;
            if (noStart && !node.IsAnnotation) Graph.startNodeGuid = node.guid;
            End();
            return node;
        }

        /// Connects two steps. For a step with named outputs, <paramref name="port"/> picks the
        /// output (-1 = the first free one). Returns false when nothing could be connected.
        public bool Connect(NarrativeNode from, NarrativeNode to, int port = -1)
        {
            if (from == null || to == null || from == to || from.IsAnnotation || to.IsAnnotation) return false;
            from.outputs ??= new List<string>();
            var names = from.GetOutputPorts();

            if (names != null)
            {
                while (from.outputs.Count < names.Length) from.outputs.Add(string.Empty);
                if (port < 0) port = from.outputs.FindIndex(g => string.IsNullOrEmpty(g));
                if (port < 0 || port >= names.Length) return false;
                if (from.outputs[port] == to.guid) return false;
                Begin(StoryText.T("Conectar pasos", "Connect steps"));
                from.outputs[port] = to.guid;
                End();
                return true;
            }

            if (from.outputs.Contains(to.guid)) return false;
            Begin(StoryText.T("Conectar pasos", "Connect steps"));
            int empty = from.outputs.FindIndex(g => string.IsNullOrEmpty(g));
            if (empty >= 0) from.outputs[empty] = to.guid;
            else from.outputs.Add(to.guid);
            End();
            return true;
        }

        public void Disconnect(NarrativeNode from, int outputIndex)
        {
            if (from?.outputs == null || outputIndex < 0 || outputIndex >= from.outputs.Count) return;
            Begin(StoryText.T("Desconectar", "Disconnect"));
            if (from.HasNamedOutputs) from.outputs[outputIndex] = string.Empty;
            else from.outputs.RemoveAt(outputIndex);
            End();
        }

        /// Puts <paramref name="step"/> in the middle of an existing connection. With
        /// <paramref name="exit"/>, a chain goes in: the connection enters at step and leaves at exit.
        public void InsertBetween(NarrativeNode from, int outputIndex, NarrativeNode step, NarrativeNode exit = null)
        {
            var target = from?.GetOutputGuid(outputIndex);
            if (target == null || step == null) return;
            exit ??= step;
            Begin(StoryText.T("Insertar paso", "Insert step"));
            from.outputs[outputIndex] = step.guid;
            exit.outputs ??= new List<string>();
            if (exit.HasNamedOutputs)
            {
                while (exit.outputs.Count < exit.GetOutputPorts().Length) exit.outputs.Add(string.Empty);
                int free = exit.outputs.FindIndex(string.IsNullOrEmpty);
                exit.outputs[free < 0 ? 0 : free] = target;
            }
            else if (!exit.outputs.Contains(target)) exit.outputs.Add(target);
            End();
        }

        /// Adds a copy of a template's steps, its first step at <paramref name="position"/>.
        /// Returns the copies (empty if the template had none).
        public List<NarrativeNode> AddTemplate(StoryTemplate template, Vector2 position, string section)
        {
            var copies = template != null ? template.Instantiate() : new List<NarrativeNode>();
            if (Graph == null || copies.Count == 0) return copies;
            Begin(StoryText.T("Añadir plantilla", "Add template"));
            var entry = StoryTemplate.Entry(copies);
            var offset = position - entry.position;
            foreach (var copy in copies)
            {
                copy.position += offset;
                copy.section = copy == entry ? section ?? string.Empty : string.Empty;
                Graph.nodes.Add(copy);
            }
            bool noStart = string.IsNullOrEmpty(Graph.startNodeGuid) || Graph.FindNode(Graph.startNodeGuid) == null;
            if (noStart && !entry.IsAnnotation) Graph.startNodeGuid = entry.guid;
            End();
            return copies;
        }

        public void Delete(IEnumerable<NarrativeNode> steps)
        {
            var doomed = new HashSet<string>();
            foreach (var s in steps) if (s != null) doomed.Add(s.guid);
            if (doomed.Count == 0) return;

            Begin(StoryText.T("Borrar paso", "Delete step"));
            foreach (var n in Graph.nodes)
            {
                if (n?.outputs == null || doomed.Contains(n.guid)) continue;
                if (n.HasNamedOutputs)
                {
                    for (int i = 0; i < n.outputs.Count; i++)
                        if (doomed.Contains(n.outputs[i])) n.outputs[i] = string.Empty;
                }
                else n.outputs.RemoveAll(doomed.Contains);
            }
            Graph.nodes.RemoveAll(n => n != null && doomed.Contains(n.guid));
            if (doomed.Contains(Graph.startNodeGuid)) Graph.startNodeGuid = null;
            End();
        }

        public void Move(IReadOnlyDictionary<NarrativeNode, Vector2> positions)
        {
            if (positions == null || positions.Count == 0) return;
            Begin(StoryText.T("Mover pasos", "Move steps"));
            foreach (var pair in positions) pair.Key.position = pair.Value;
            End();
        }

        public void SetStart(NarrativeNode step)
        {
            if (step == null || step.IsAnnotation) return;
            Begin(StoryText.T("Marcar inicio", "Set start"));
            Graph.startNodeGuid = step.guid;
            End();
        }

        public void SetSection(IEnumerable<NarrativeNode> steps, string section)
        {
            Begin(StoryText.T("Cambiar sección", "Change section"));
            foreach (var s in steps) if (s != null) s.section = section ?? string.Empty;
            End();
        }

        /// Renames a section on the steps that open it; the steps that inherit it follow.
        public void RenameSection(string oldName, string newName)
        {
            newName = newName?.Trim() ?? string.Empty;
            if (Graph == null || newName.Length == 0 || newName == oldName) return;
            Begin(StoryText.T("Renombrar sección", "Rename section"));
            foreach (var n in Graph.nodes)
                if (n != null && StoryStructure.OwnSection(n) == oldName) n.section = newName;
            End();
        }

        /// The caller already changed a field through a SerializedObject: only notify.
        public void NotifyFieldChanged() => End();
    }
}
