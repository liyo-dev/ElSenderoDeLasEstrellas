using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Un paso en el lienzo: cabecera del color de su tipo (icono, número, tipo), el título y una
    /// línea de lo que hace, con el retrato de su personaje. Las salidas con nombre se arrastran; al
    /// pasar el ratón, un asa por lado para empezar una conexión desde el lado más cercano a su destino.
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
        readonly VisualElement _header;
        readonly VisualElement _icon;
        readonly VisualElement _art;
        readonly VisualElement _artDrawing;
        readonly Image _face;
        Color _color;

        public StepCard(NarrativeNode model)
        {
            Model = model;
            AddToClassList("story-card");
            if (model.IsAnnotation) AddToClassList("story-card--note");
            pickingMode = PickingMode.Position;

            var header = _header = new VisualElement();
            header.AddToClassList("story-card__header");
            Add(header);

            _icon = new VisualElement();
            _icon.AddToClassList("story-card__icon");
            _icon.generateVisualContent += DrawIcon;
            header.Add(_icon);

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

            // La banda recorta su dibujo: el eco del pictograma se sale por el borde a propósito.
            _art = new VisualElement { pickingMode = PickingMode.Ignore };
            _art.AddToClassList("story-card__art");
            _artDrawing = new VisualElement { pickingMode = PickingMode.Ignore };
            _artDrawing.AddToClassList("story-card__art-drawing");
            _artDrawing.generateVisualContent += DrawCardArt;
            _art.Add(_artDrawing);
            Add(_art);

            var body = new VisualElement();
            body.AddToClassList("story-card__body");
            Add(body);

            _title = new Label();
            _title.AddToClassList("story-card__title");
            body.Add(_title);

            var line = new VisualElement();
            line.AddToClassList("story-card__line");
            body.Add(line);
            _face = new Image { scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            _face.AddToClassList("story-card__face");
            line.Add(_face);

            _summary = new Label();
            _summary.AddToClassList("story-card__summary");
            line.Add(_summary);

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
            var entry = NarrativeNodeCatalog.Get(Model.KindType);
            _number.text = structure != null ? structure.Number(Model) : string.Empty;
            _number.style.display = string.IsNullOrEmpty(_number.text) ? DisplayStyle.None : DisplayStyle.Flex;
            // En la cabecera, el nombre corto del tipo; el completo, al pasar el ratón.
            var typeName = entry?.title ?? Model.KindType.Name;
            int paren = typeName.IndexOf(" (", StringComparison.Ordinal);
            var shortName = paren > 0 ? typeName.Substring(0, paren) : typeName;
            _type.text = (string.IsNullOrEmpty(Model.Badge) ? string.Empty : Model.Badge + " ") + shortName.ToUpperInvariant();
            _type.tooltip = typeName;
            _color = NarrativeNodeCatalog.ColorOf(entry?.category ?? "Otros");
            _type.style.color = Color.Lerp(_color, Color.white, 0.55f);
            _header.style.backgroundColor = new Color(_color.r, _color.g, _color.b, 0.24f);
            _header.style.borderBottomColor = _color;
            _icon.style.backgroundColor = _color;
            _icon.userData = entry?.category;
            _icon.MarkDirtyRepaint();

            bool art = StoryCardImages.ShowCardArt && !Model.IsAnnotation;
            _art.style.display = art ? DisplayStyle.Flex : DisplayStyle.None;
            if (art)
            {
                var custom = StoryCardImages.CardArtOf(Model);
                _art.style.backgroundImage = custom != null ? new StyleBackground(custom) : new StyleBackground(StyleKeyword.None);
                _artDrawing.MarkDirtyRepaint();
            }

            var face = StoryCardImages.ShowPortraits && !Model.IsAnnotation ? StoryCardImages.PortraitOf(StoryCardImages.ActorOf(Model)) : null;
            var customFace = StoryCardImages.ShowPortraits && !Model.IsAnnotation ? StoryCardImages.ActorArtOf(StoryCardImages.ActorOf(Model)) : null;
            if (customFace != null) face = customFace;
            _face.image = face;
            _face.style.display = face != null ? DisplayStyle.Flex : DisplayStyle.None;

            _title.text = string.IsNullOrWhiteSpace(Model.displayTitle) ? entry?.title ?? Model.KindType.Name : Model.displayTitle;
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

        /// El pictograma de la familia, en línea blanca sobre el cuadrado de su color.
        void DrawIcon(MeshGenerationContext ctx)
            => StoryCardArt.DrawIcon(ctx.painter2D, _icon.contentRect, _icon.userData as string, _color);

        /// La banda con el pictograma de la familia; si hay una textura propia, la pinta el
        /// fondo y aquí no se dibuja nada.
        void DrawCardArt(MeshGenerationContext ctx)
        {
            if (!StoryCardImages.ShowCardArt || Model.IsAnnotation) return;
            if (StoryCardImages.CardArtOf(Model) != null) return;
            StoryCardArt.DrawBand(ctx.painter2D, _artDrawing.contentRect, NarrativeNodeCatalog.CategoryOf(Model.KindType), _color);
        }

        public void SetPosition(Vector2 position)
        {
            style.left = position.x;
            style.top = position.y;
        }

        public Rect WorldRect => new Rect(new Vector2(resolvedStyle.left, resolvedStyle.top),
                                          new Vector2(StoryLayout.CardWidth, StoryLayout.HeightOf(Model)));
    }

    /// Colores del editor. Un acento (oro de estrella) para el camino de la historia, la
    /// selección y la acción principal; cada tipo de paso, su color en la cabecera.
    public static class StoryColors
    {
        public static readonly Color Accent = new Color32(0x3D, 0x8B, 0xFF, 0xFF);
        /// El fondo de las tarjetas (--card en StoryEditor.uss).
        public static readonly Color Card = new Color32(0x1E, 0x24, 0x30, 0xFF);
        public static readonly Color Line = new Color32(0x4A, 0x54, 0x68, 0xFF);
        public static readonly Color LineNear = new Color32(0x8C, 0x9A, 0xB3, 0xFF);
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
