using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NarrativeGraphEditor.Story
{
    /// A group of steps saved to be reused: "a shop visit", "a boss fight with its cinematic"...
    /// Made from a selection in the editor and offered again in "What happens now?".
    [CreateAssetMenu(menuName = "Narrative Graph/Step template", fileName = "NewStepTemplate")]
    public sealed class StoryTemplate : ScriptableObject
    {
        public const string DefaultFolder = "Assets/NarrativeTemplates";

        [TextArea] public string description;
        [SerializeReference] public List<NarrativeNode> steps = new();

        public string Title => name;

        /// Every template in the project, by name.
        public static List<StoryTemplate> All()
            => AssetDatabase.FindAssets("t:" + nameof(StoryTemplate))
                .Select(g => AssetDatabase.LoadAssetAtPath<StoryTemplate>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null && t.steps != null && t.steps.Count > 0)
                .OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// Saves a copy of <paramref name="selection"/> as a new template. Connections that leave the
        /// selection are dropped; positions are kept relative to the first step.
        public static StoryTemplate Create(string title, IReadOnlyCollection<NarrativeNode> selection)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var template = FromSelection(selection);
            if (template == null) return null;

            if (!AssetDatabase.IsValidFolder(DefaultFolder))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(DefaultFolder));
            var path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/{SafeFileName(title)}.asset");
            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();
            return template;
        }

        /// An unsaved template with a copy of <paramref name="selection"/> (used to duplicate steps).
        public static StoryTemplate FromSelection(IReadOnlyCollection<NarrativeNode> selection)
        {
            if (selection == null || selection.Count == 0) return null;
            var inside = new HashSet<string>(selection.Select(n => n.guid));
            var origin = new Vector2(selection.Min(n => n.position.x), selection.Min(n => n.position.y));
            var template = CreateInstance<StoryTemplate>();
            foreach (var node in selection.OrderBy(n => n.position.y).ThenBy(n => n.position.x))
            {
                var copy = Clone(node);
                copy.guid = node.guid;
                copy.position = node.position - origin;
                copy.section = string.Empty;
                KeepOutputsInside(copy, inside);
                template.steps.Add(copy);
            }
            return template;
        }

        /// Fresh copies of the template's steps, with new ids and their connections remapped.
        public List<NarrativeNode> Instantiate()
        {
            var remap = new Dictionary<string, string>();
            var copies = new List<NarrativeNode>();
            foreach (var step in steps)
            {
                if (step == null) continue;
                var copy = Clone(step);
                copy.guid = Guid.NewGuid().ToString();
                remap[step.guid] = copy.guid;
                copies.Add(copy);
            }
            foreach (var copy in copies)
            {
                if (copy.outputs == null) continue;
                for (int i = 0; i < copy.outputs.Count; i++)
                    copy.outputs[i] = !string.IsNullOrEmpty(copy.outputs[i]) && remap.TryGetValue(copy.outputs[i], out var g) ? g : string.Empty;
                if (!copy.HasNamedOutputs) copy.outputs.RemoveAll(string.IsNullOrEmpty);
            }
            return copies;
        }

        /// The step the template starts with: the first one nothing inside it leads to.
        public static NarrativeNode Entry(IReadOnlyList<NarrativeNode> copies)
        {
            var targets = new HashSet<string>(copies.Where(c => c.outputs != null).SelectMany(c => c.outputs));
            return copies.FirstOrDefault(c => !c.IsAnnotation && !targets.Contains(c.guid)) ?? copies.FirstOrDefault();
        }

        /// The step the template ends with: the last one with a free way out.
        public static NarrativeNode Exit(IReadOnlyList<NarrativeNode> copies)
            => copies.LastOrDefault(c => !c.IsAnnotation && (c.outputs == null || c.outputs.Count == 0
                                                             || (c.HasNamedOutputs && c.outputs.Any(string.IsNullOrEmpty))))
               ?? copies.LastOrDefault();

        static NarrativeNode Clone(NarrativeNode source)
        {
            var copy = (NarrativeNode)Activator.CreateInstance(source.GetType());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
            copy.outputs = source.outputs != null ? new List<string>(source.outputs) : new List<string>();
            return copy;
        }

        static void KeepOutputsInside(NarrativeNode node, HashSet<string> inside)
        {
            if (node.outputs == null) return;
            if (node.HasNamedOutputs)
            {
                for (int i = 0; i < node.outputs.Count; i++)
                    if (!inside.Contains(node.outputs[i])) node.outputs[i] = string.Empty;
            }
            else node.outputs.RemoveAll(g => !inside.Contains(g));
        }

        static string SafeFileName(string title)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(title.Trim().Select(c => invalid.Contains(c) ? '-' : c).ToArray());
            return string.IsNullOrEmpty(clean) ? "Template" : clean;
        }
    }
}
