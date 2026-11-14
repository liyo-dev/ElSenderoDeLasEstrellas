using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Un paso en el lienzo: cabecera del color de su tipo (icono, número, tipo), la foto del paso
    /// si la hay y se quieren fotos (StoryCardImages), el título y una línea de lo que hace, con
    /// el retrato de su personaje. Las salidas con nombre se arrastran; al pasar el ratón, un
    /// asa por lado para empezar una conexión desde el lado más cercano a su destino.
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
        readonly Image _photo;
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

            _photo = new Image { scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            _photo.AddToClassList("story-card__photo");
            Add(_photo);

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
            var entry = NarrativeNodeCatalog.Get(Model.GetType());
            _number.text = structure != null ? structure.Number(Model) : string.Empty;
            _number.style.display = string.IsNullOrEmpty(_number.text) ? DisplayStyle.None : DisplayStyle.Flex;
            // En la cabecera, el nombre corto del tipo; el completo, al pasar el ratón.
            var typeName = entry?.title ?? Model.GetType().Name;
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

            bool photo = !Model.IsAnnotation && StoryCardImages.ShowPhotos && StoryCardImages.HasPhoto(Model);
            _photo.image = photo ? StoryCardImages.PhotoOf(Model) : null;
            _photo.style.display = photo ? DisplayStyle.Flex : DisplayStyle.None;
            var face = StoryCardImages.ShowPortraits && !Model.IsAnnotation ? StoryCardImages.PortraitOf(StoryCardImages.ActorOf(Model)) : null;
            _face.image = face;
            _face.style.display = face != null ? DisplayStyle.Flex : DisplayStyle.None;

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

        /// Un dibujo sencillo por familia de pasos, en blanco sobre el cuadrado de su color.
        void DrawIcon(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = _icon.contentRect;
            float w = r.width, h = r.height;
            if (w <= 0) return;
            Vector2 P(float x, float y) => new Vector2(x * w, y * h);
            p.strokeColor = Color.white;
            p.fillColor = Color.white;
            p.lineWidth = 1.6f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            switch (_icon.userData as string)
            {
                case "Diálogo":
                    p.BeginPath(); p.MoveTo(P(.2f, .25f)); p.LineTo(P(.8f, .25f)); p.LineTo(P(.8f, .62f)); p.LineTo(P(.45f, .62f)); p.LineTo(P(.28f, .8f)); p.LineTo(P(.3f, .62f)); p.LineTo(P(.2f, .62f)); p.ClosePath(); p.Stroke();
                    break;
                case "Combate":
                    p.BeginPath(); p.MoveTo(P(.22f, .22f)); p.LineTo(P(.78f, .78f)); p.MoveTo(P(.78f, .22f)); p.LineTo(P(.22f, .78f)); p.Stroke();
                    break;
                case "Quests":
                    p.BeginPath(); p.MoveTo(P(.5f, .2f)); p.LineTo(P(.5f, .6f)); p.Stroke();
                    p.BeginPath(); p.Arc(P(.5f, .78f), w * .07f, 0, 360); p.Fill();
                    break;
                case "NPCs":
                case "Grupo":
                    p.BeginPath(); p.Arc(P(.5f, .35f), w * .14f, 0, 360); p.Fill();
                    p.BeginPath(); p.MoveTo(P(.25f, .82f)); p.BezierCurveTo(P(.25f, .55f), P(.75f, .55f), P(.75f, .82f)); p.Stroke();
                    break;
                case "Cinemáticas":
                case "Audio y cine":
                    p.BeginPath(); p.MoveTo(P(.33f, .24f)); p.LineTo(P(.78f, .5f)); p.LineTo(P(.33f, .76f)); p.ClosePath(); p.Fill();
                    break;
                case "Objetos":
                    p.BeginPath(); p.MoveTo(P(.22f, .38f)); p.LineTo(P(.78f, .38f)); p.LineTo(P(.72f, .8f)); p.LineTo(P(.28f, .8f)); p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.38f, .38f)); p.LineTo(P(.38f, .24f)); p.LineTo(P(.62f, .24f)); p.LineTo(P(.62f, .38f)); p.Stroke();
                    break;
                case "Mundo":
                    p.BeginPath(); p.MoveTo(P(.5f, .18f)); p.LineTo(P(.82f, .5f)); p.LineTo(P(.5f, .82f)); p.LineTo(P(.18f, .5f)); p.ClosePath(); p.Stroke();
                    break;
                case "Señales":
                    p.BeginPath(); p.MoveTo(P(.15f, .5f)); p.LineTo(P(.32f, .5f)); p.LineTo(P(.42f, .25f)); p.LineTo(P(.58f, .75f)); p.LineTo(P(.68f, .5f)); p.LineTo(P(.85f, .5f)); p.Stroke();
                    break;
                case "Jugador":
                    p.BeginPath();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.PI * 2 * i / 10f - Mathf.PI / 2, rad = i % 2 == 0 ? .36f : .16f;
                        var v = P(.5f + Mathf.Cos(a) * rad, .52f + Mathf.Sin(a) * rad);
                        if (i == 0) p.MoveTo(v); else p.LineTo(v);
                    }
                    p.ClosePath(); p.Fill();
                    break;
                case "Flujo":
                    p.BeginPath(); p.MoveTo(P(.2f, .5f)); p.LineTo(P(.75f, .5f)); p.MoveTo(P(.55f, .3f)); p.LineTo(P(.78f, .5f)); p.LineTo(P(.55f, .7f)); p.Stroke();
                    break;
                case "Notas":
                    p.BeginPath(); p.MoveTo(P(.25f, .32f)); p.LineTo(P(.75f, .32f)); p.MoveTo(P(.25f, .5f)); p.LineTo(P(.75f, .5f)); p.MoveTo(P(.25f, .68f)); p.LineTo(P(.6f, .68f)); p.Stroke();
                    break;
                default:
                    p.BeginPath(); p.Arc(P(.5f, .5f), w * .16f, 0, 360); p.Fill();
                    break;
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

    /// Colores del editor. Un acento (oro de estrella) para el camino de la historia, la
    /// selección y la acción principal; cada tipo de paso, su color en la cabecera.
    public static class StoryColors
    {
        public static readonly Color Accent = new Color32(0xF2, 0xB5, 0x44, 0xFF);
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
