using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// A step on the canvas: number, type, the name the author gave it and one line of what it
    /// does. Named outputs are chips that can be dragged; on hover, one handle per side lets a
    /// connection start from wherever is closest to its destination.
    public sealed class StepCard : VisualElement, INarrativeNodeHost
    {
        public NarrativeNode Model { get; }

        /// (card, output port or -1, handle position in world space)
        public event Action<StepCard, int, PointerDownEvent> LinkStarted;

        readonly Label _number;
        readonly Label _type;
        readonly VisualElement _warning;
        readonly Label _title;
        readonly Label _summary;
        readonly VisualElement _outputs;

        public StepCard(NarrativeNode model)
        {
            Model = model;
            AddToClassList("story-card");
            if (model.IsAnnotation) AddToClassList("story-card--note");
            pickingMode = PickingMode.Position;

            var header = new VisualElement();
            header.AddToClassList("story-card__header");
            Add(header);

            var breakpoint = new VisualElement { tooltip = StoryText.T("Aquí se pausa al jugar", "Play pauses here") };
            breakpoint.AddToClassList("story-card__breakpoint");
            header.Add(breakpoint);

            _number = new Label();
            _number.AddToClassList("story-card__number");
            header.Add(_number);

            _type = new Label();
            _type.AddToClassList("story-card__type");
            header.Add(_type);

            _warning = new VisualElement();
            _warning.AddToClassList("story-card__warning");
            header.Add(_warning);

            _title = new Label();
            _title.AddToClassList("story-card__title");
            Add(_title);

            _summary = new Label();
            _summary.AddToClassList("story-card__summary");
            Add(_summary);

            _outputs = new VisualElement();
            _outputs.AddToClassList("story-card__outputs");
            Add(_outputs);

            if (!model.IsAnnotation)
            {
                AddHandle("story-card__handle--top");
                AddHandle("story-card__handle--right");
                AddHandle("story-card__handle--bottom");
                AddHandle("story-card__handle--left");
            }

            Refresh(null);
        }

        void AddHandle(string modifier)
        {
            var h = new VisualElement();
            h.AddToClassList("story-card__handle");
            h.AddToClassList(modifier);
            h.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                LinkStarted?.Invoke(this, -1, e);
                e.StopPropagation();
            });
            Add(h);
        }

        public void Refresh(StoryStructure structure)
        {
            var entry = NarrativeNodeCatalog.Get(Model.GetType());
            _number.text = structure != null ? structure.Number(Model) : string.Empty;
            _number.style.display = string.IsNullOrEmpty(_number.text) ? DisplayStyle.None : DisplayStyle.Flex;
            // En la cabecera, el nombre corto del tipo; el completo, al pasar el ratón.
            var typeName = entry?.title ?? Model.GetType().Name;
            int paren = typeName.IndexOf(" (", StringComparison.Ordinal);
            var shortName = paren > 0 ? typeName.Substring(0, paren) : typeName;
            _type.text = (Model is CheckpointNode ? "◆ " : string.Empty) + shortName.ToUpperInvariant();
            _type.tooltip = typeName;
            EnableInClassList("is-checkpoint", Model is CheckpointNode);
            _type.style.color = StoryColors.Category(entry?.category);

            _title.text = string.IsNullOrWhiteSpace(Model.displayTitle) ? entry?.title ?? Model.GetType().Name : Model.displayTitle;
            _summary.text = StepSummary.For(Model);
            _summary.style.display = string.IsNullOrEmpty(_summary.text) ? DisplayStyle.None : DisplayStyle.Flex;

            var warnings = StepSummary.Warnings(Model);
            _warning.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _warning.tooltip = string.Join("\n", warnings);
            EnableInClassList("has-warning", warnings.Count > 0);

            RebuildOutputs();
            style.left = Model.position.x;
            style.top = Model.position.y;
            style.width = StoryLayout.CardWidth;
            style.height = StoryLayout.HeightOf(Model);
        }

        void RebuildOutputs()
        {
            _outputs.Clear();
            var names = Model.GetOutputPorts();
            _outputs.style.display = names != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (names == null) return;
            for (int i = 0; i < names.Length; i++)
            {
                int port = i;
                var chip = new Label(names[i]);
                chip.AddToClassList("story-card__output");
                chip.EnableInClassList("is-connected", Model.GetOutputGuid(i) != null);
                chip.tooltip = StoryText.T("Arrastra para conectar esta salida", "Drag to connect this output");
                chip.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    LinkStarted?.Invoke(this, port, e);
                    e.StopPropagation();
                });
                _outputs.Add(chip);
            }
        }

        public void SetPosition(Vector2 position)
        {
            style.left = position.x;
            style.top = position.y;
        }

        public Rect WorldRect => new Rect(new Vector2(resolvedStyle.left, resolvedStyle.top),
                                          new Vector2(StoryLayout.CardWidth, StoryLayout.HeightOf(Model)));
    }

    /// The editor palette. One accent for the story's own path, the selection and the main
    /// action; step types only tint their label, muted so the accent stays the loudest thing.
    public static class StoryColors
    {
        public static readonly Color Accent = new Color32(0xFF, 0x7A, 0x45, 0xFF);
        public static readonly Color Line = new Color32(0x57, 0x52, 0x4B, 0xFF);
        public static readonly Color LineNear = new Color32(0x9A, 0x93, 0x89, 0xFF);
        public static readonly Color Ok = new Color32(0xA8, 0xD8, 0xB0, 0xFF);
        public static readonly Color Warning = new Color32(0xF0, 0xB2, 0x4A, 0xFF);

        public static Color Category(string category)
        {
            var c = NarrativeNodeCatalog.ColorOf(category ?? "Otros");
            Color.RGBToHSV(c, out var h, out var s, out _);
            return Color.HSVToRGB(h, Mathf.Min(s, 0.45f), 0.86f);
        }
    }
}
