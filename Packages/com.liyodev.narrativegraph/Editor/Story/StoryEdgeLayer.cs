using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Draws every connection of the canvas (and the one being dragged) in world space.
    public sealed class StoryEdgeLayer : VisualElement
    {
        List<StoryLayout.Route> _routes = new();
        HashSet<string> _onPath = new();
        NarrativeNode _selected;
        Vector2? _dragFrom, _dragTo;

        public StoryEdgeLayer()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.width = 1; style.height = 1;
            style.overflow = Overflow.Visible;
            generateVisualContent += Draw;
        }

        public static string Key(NarrativeNode from, NarrativeNode to) => from.guid + ">" + to.guid;

        public void SetRoutes(List<StoryLayout.Route> routes) { _routes = routes ?? new List<StoryLayout.Route>(); MarkDirtyRepaint(); }
        public void SetPath(HashSet<string> edgeKeys) { _onPath = edgeKeys ?? new HashSet<string>(); MarkDirtyRepaint(); }
        public void SetSelected(NarrativeNode node) { _selected = node; MarkDirtyRepaint(); }

        public void SetDrag(Vector2? from, Vector2? to)
        {
            _dragFrom = from; _dragTo = to;
            MarkDirtyRepaint();
        }

        public Color ColorOf(StoryLayout.Route r)
        {
            if (_onPath.Contains(Key(r.From, r.To))) return StoryColors.Accent;
            if (_selected != null && (r.From == _selected || r.To == _selected)) return StoryColors.LineNear;
            return StoryColors.Line;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            foreach (var r in _routes)
            {
                if (r.Points == null || r.Points.Count < 2) continue;
                bool lit = _onPath.Contains(Key(r.From, r.To));
                var color = ColorOf(r);
                // Con un camino marcado, el resto de líneas se apaga.
                if (!lit && _onPath.Count > 0) color.a *= 0.35f;
                p.strokeColor = color;
                p.lineWidth = lit ? 3.2f : 1.6f;
                if (r.Parallel) DrawDashed(p, r.Points);
                else DrawRounded(p, r.Points, 8f);
                DrawArrow(p, r.Points, color);
            }

            if (_dragFrom.HasValue && _dragTo.HasValue)
            {
                p.strokeColor = StoryColors.Accent;
                p.lineWidth = 2f;
                DrawDashed(p, new List<Vector2> { _dragFrom.Value, _dragTo.Value });
            }
        }

        static void DrawRounded(Painter2D p, List<Vector2> pts, float radius)
        {
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Count - 1; i++)
            {
                float r = Mathf.Min(radius, Vector2.Distance(pts[i - 1], pts[i]) * 0.5f, Vector2.Distance(pts[i], pts[i + 1]) * 0.5f);
                if (r < 0.5f) p.LineTo(pts[i]);
                else p.ArcTo(pts[i], pts[i + 1], r);
            }
            p.LineTo(EndBeforeArrow(pts));
            p.Stroke();
        }

        static void DrawDashed(Painter2D p, List<Vector2> pts)
        {
            const float dash = 7f, gap = 5f;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1];
                var b = i == pts.Count - 1 ? EndBeforeArrow(pts) : pts[i];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                var dir = (b - a) / len;
                for (float t = 0f; t < len; t += dash + gap)
                {
                    p.BeginPath();
                    p.MoveTo(a + dir * t);
                    p.LineTo(a + dir * Mathf.Min(t + dash, len));
                    p.Stroke();
                }
            }
        }

        static Vector2 EndBeforeArrow(List<Vector2> pts)
        {
            var end = pts[^1];
            var dir = (end - pts[^2]).normalized;
            return end - dir * 4f;
        }

        static void DrawArrow(Painter2D p, List<Vector2> pts, Color color)
        {
            var tip = pts[^1];
            var dir = (tip - pts[^2]).normalized;
            if (dir == Vector2.zero) return;
            var side = new Vector2(-dir.y, dir.x);
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(tip);
            p.LineTo(tip - dir * 9f + side * 4.5f);
            p.LineTo(tip - dir * 9f - side * 4.5f);
            p.ClosePath();
            p.Fill();
        }
    }
}
