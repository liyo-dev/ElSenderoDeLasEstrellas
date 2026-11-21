using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// Aviso de «Calculando…» con un círculo que gira, en el panel que lo contiene: tapándolo entero
    /// (<c>cover</c>) o como una píldora arriba que no oculta lo que hay. Lo que haya que hacer se
    /// hace un momento después de enseñarlo, para que llegue a pintarse antes de que el editor se
    /// quede ocupado.
    public sealed class StoryBusy : VisualElement
    {
        const long PaintDelayMs = 30;

        readonly VisualElement _ring;
        readonly Label _text;
        IVisualElementScheduledItem _spin;
        float _angle;
        int _depth;

        public StoryBusy(bool cover)
        {
            AddToClassList("story-busy");
            AddToClassList(cover ? "story-busy--cover" : "story-busy--pill");
            // Tapando, también para los clics; la píldora deja usar el panel.
            pickingMode = cover ? PickingMode.Position : PickingMode.Ignore;
            var box = new VisualElement();
            box.AddToClassList("story-busy__box");
            _ring = new VisualElement();
            _ring.AddToClassList("story-busy__ring");
            box.Add(_ring);
            _text = new Label();
            _text.AddToClassList("story-busy__text");
            box.Add(_text);
            Add(box);
            style.display = DisplayStyle.None;
        }

        public bool Visible => style.display == DisplayStyle.Flex;

        /// Enseña el aviso. Se puede pedir varias veces: se quita cuando se ha quitado tantas como se pidió.
        public void Show(string text = null)
        {
            _depth++;
            _text.text = string.IsNullOrEmpty(text) ? StoryText.T("Calculando…", "Working…") : text;
            style.display = DisplayStyle.Flex;
            BringToFront();
            _spin ??= schedule.Execute(() =>
            {
                _angle = (_angle + 24f) % 360f;
                _ring.style.rotate = new Rotate(new Angle(_angle, AngleUnit.Degree));
            }).Every(40);
            _spin.Resume();
        }

        public void Hide()
        {
            _depth = Mathf.Max(0, _depth - 1);
            if (_depth > 0) return;
            style.display = DisplayStyle.None;
            _spin?.Pause();
        }

        /// Enseña el aviso, hace <paramref name="work"/> cuando ya se ve y lo quita al acabar.
        public void Run(Action work, string text = null)
        {
            Show(text);
            schedule.Execute(() =>
            {
                try { work?.Invoke(); }
                finally { Hide(); }
            }).StartingIn(PaintDelayMs);
        }
    }
}
