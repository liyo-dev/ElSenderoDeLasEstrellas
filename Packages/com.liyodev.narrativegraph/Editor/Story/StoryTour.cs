using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Recorrido guiado por el editor: una burbuja por zona, con la zona resaltada. Sale solo la
    /// primera vez que se abre el editor y se puede repetir desde ⚙ Ajustes.
    public sealed class StoryTour : VisualElement
    {
        public sealed class Stop
        {
            public Func<VisualElement> Target;
            public string Title;
            public string Text;
            public Action OnShow;
        }

        const string SeenPref = "NarrativeGraph.Tour.Seen";
        public static bool Seen { get => EditorPrefs.GetBool(SeenPref, false); set => EditorPrefs.SetBool(SeenPref, value); }

        readonly List<Stop> _stops;
        readonly VisualElement _frame;
        readonly VisualElement _bubble;
        readonly Label _count;
        readonly Label _title;
        readonly Label _text;
        readonly Button _back;
        readonly Button _next;
        int _index;

        public StoryTour(List<Stop> stops)
        {
            _stops = stops;
            AddToClassList("story-tour");
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;

            _frame = new VisualElement { pickingMode = PickingMode.Ignore };
            _frame.AddToClassList("story-tour__frame");
            Add(_frame);

            _bubble = new VisualElement();
            _bubble.AddToClassList("story-tour__bubble");
            _count = new Label();
            _count.AddToClassList("story-kicker");
            _bubble.Add(_count);
            _title = new Label();
            _title.AddToClassList("story-tour__title");
            _bubble.Add(_title);
            _text = new Label();
            _text.AddToClassList("story-tour__text");
            _bubble.Add(_text);
            var buttons = new VisualElement();
            buttons.AddToClassList("story-tour__buttons");
            var skip = new Button(Finish) { text = StoryText.T("Saltar", "Skip") };
            skip.AddToClassList("story-button");
            buttons.Add(skip);
            buttons.Add(new VisualElement { style = { flexGrow = 1 } });
            _back = new Button(() => Go(_index - 1)) { text = StoryText.T("Anterior", "Back") };
            _back.AddToClassList("story-button");
            buttons.Add(_back);
            _next = new Button(() => Go(_index + 1));
            _next.AddToClassList("story-button--primary");
            buttons.Add(_next);
            _bubble.Add(buttons);
            Add(_bubble);

            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) Finish();
                else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.Return) Go(_index + 1);
                else if (e.keyCode == KeyCode.LeftArrow) Go(_index - 1);
                e.StopPropagation();
            });
            RegisterCallback<GeometryChangedEvent>(_ => Place());
            focusable = true;
        }

        public void Begin()
        {
            Go(0);
            schedule.Execute(() => Focus()).StartingIn(50);
        }

        void Go(int index)
        {
            if (index >= _stops.Count) { Finish(); return; }
            _index = Mathf.Max(0, index);
            var stop = _stops[_index];
            stop.OnShow?.Invoke();
            _count.text = StoryText.T($"RECORRIDO · {_index + 1} DE {_stops.Count}", $"TOUR · {_index + 1} OF {_stops.Count}");
            _title.text = stop.Title;
            _text.text = stop.Text;
            _back.SetEnabled(_index > 0);
            _next.text = _index == _stops.Count - 1 ? StoryText.T("Empezar", "Start") : StoryText.T("Siguiente", "Next");
            // Se coloca cuando el panel que se acaba de enseñar ya tiene su tamaño.
            schedule.Execute(Place).StartingIn(30);
        }

        void Place()
        {
            if (_stops.Count == 0 || parent == null) return;
            var target = _stops[_index].Target?.Invoke();
            var area = worldBound;
            const float bubbleWidth = 320f;
            _bubble.style.width = bubbleWidth;

            if (target == null || target.panel == null || target.worldBound.width <= 1f)
            {
                _frame.style.display = DisplayStyle.None;
                _bubble.style.left = (area.width - bubbleWidth) * 0.5f;
                _bubble.style.top = area.height * 0.32f;
                return;
            }

            var r = target.worldBound;
            r.position -= area.position;
            _frame.style.display = DisplayStyle.Flex;
            _frame.style.left = r.x - 4; _frame.style.top = r.y - 4;
            _frame.style.width = r.width + 8; _frame.style.height = r.height + 8;

            float bubbleHeight = Mathf.Max(_bubble.resolvedStyle.height, 140f);
            // A la derecha si cabe; si no, a la izquierda; si es ancho, debajo o encima.
            float x, y;
            if (r.width < area.width * 0.45f && r.xMax + 16 + bubbleWidth < area.width) { x = r.xMax + 16; y = r.y; }
            else if (r.width < area.width * 0.45f && r.x - 16 - bubbleWidth > 0) { x = r.x - 16 - bubbleWidth; y = r.y; }
            else
            {
                x = Mathf.Clamp(r.center.x - bubbleWidth * 0.5f, 8, area.width - bubbleWidth - 8);
                y = r.yMax + 16 + bubbleHeight < area.height ? r.yMax + 16 : Mathf.Max(8, r.y - 16 - bubbleHeight);
            }
            _bubble.style.left = x;
            _bubble.style.top = Mathf.Clamp(y, 8, Mathf.Max(8, area.height - bubbleHeight - 8));
        }

        void Finish()
        {
            Seen = true;
            RemoveFromHierarchy();
        }

        /// El manual de usuario en PDF, en la página indicada (si el visor lo permite).
        public static void OpenManual(int page = 1)
        {
            var pdf = Path.GetFullPath(NarrativeGraphPaths.Manual);
            if (pdf == null || !File.Exists(pdf))
            {
                EditorUtility.DisplayDialog(StoryText.T("Manual", "Manual"), StoryText.T("No encuentro el manual (Documentation/Manual de usuario.pdf).", "The manual was not found (Documentation/Manual de usuario.pdf)."), "OK");
                return;
            }
            Application.OpenURL("file:///" + pdf.Replace('\\', '/') + (page > 1 ? "#page=" + page : string.Empty));
        }
    }
}
