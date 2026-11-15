using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Thin handle between two panels: dragging it changes the width of one of them. The width is
    /// remembered per user.
    public sealed class StorySplitter : VisualElement
    {
        readonly VisualElement _panel;
        readonly bool _panelIsLeft;
        readonly string _pref;
        readonly float _min, _max;
        float _startX, _startWidth;
        bool _dragging;

        public StorySplitter(VisualElement panel, bool panelIsLeft, string prefKey, float defaultWidth, float min, float max)
        {
            _panel = panel;
            _panelIsLeft = panelIsLeft;
            _pref = prefKey;
            _min = min;
            _max = max;
            AddToClassList("story-splitter");
            tooltip = StoryText.T("Arrastra para cambiar el ancho (doble clic: tamaño inicial)", "Drag to resize (double-click: default size)");
            SetWidth(EditorPrefs.GetFloat(_pref, defaultWidth));

            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (e.clickCount == 2) { SetWidth(defaultWidth); Save(); e.StopPropagation(); return; }
                _dragging = true;
                _startX = e.position.x;
                _startWidth = _panel.resolvedStyle.width;
                this.CapturePointer(e.pointerId);
                AddToClassList("is-dragging");
                e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging) return;
                float delta = e.position.x - _startX;
                SetWidth(_startWidth + (_panelIsLeft ? delta : -delta));
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (!_dragging) return;
                _dragging = false;
                if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
                RemoveFromClassList("is-dragging");
                Save();
            });
        }

        void SetWidth(float width) => _panel.style.width = Mathf.Clamp(width, _min, _max);

        void Save() => EditorPrefs.SetFloat(_pref, _panel.resolvedStyle.width);
    }
}
