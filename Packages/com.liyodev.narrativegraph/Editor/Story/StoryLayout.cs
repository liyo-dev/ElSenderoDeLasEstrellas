using System.Collections.Generic;
using UnityEngine;

namespace NarrativeGraphEditor.Story
{
    /// Geometry of the story canvas: card sizes, section frames, automatic layout and the route
    /// every connection takes. A connection leaves each card through the side facing the other
    /// card; connections sharing a side are spread along it, and corners are rounded.
    public static class StoryLayout
    {
        public const float CardWidth = 220f;
        public const float CardHeight = 112f;
        public const float CardHeightWithOutputs = 142f;
        public const float PhotoHeight = 112f;
        public const float ColumnGap = 60f;
        public const float RowGap = 50f;
        public const float SectionGap = 110f;
        public const float SectionHeader = 44f;
        public const int StepsPerRow = 8;

        public static float HeightOf(NarrativeNode node)
            => (node != null && node.HasNamedOutputs ? CardHeightWithOutputs : CardHeight)
               + (node != null && !node.IsAnnotation && StoryCardImages.ShowPhotos && StoryCardImages.HasPhoto(node) ? PhotoHeight : 0f);

        public static Rect RectOf(NarrativeNode node) => new Rect(node.position, new Vector2(CardWidth, HeightOf(node)));

        public static Dictionary<StoryStructure.Section, Rect> SectionFrames(StoryStructure structure)
        {
            var frames = new Dictionary<StoryStructure.Section, Rect>();
            foreach (var section in structure.Sections)
            {
                if (section.Steps.Count == 0) continue;
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                foreach (var n in section.Steps)
                {
                    var r = RectOf(n);
                    x0 = Mathf.Min(x0, r.xMin); y0 = Mathf.Min(y0, r.yMin);
                    x1 = Mathf.Max(x1, r.xMax); y1 = Mathf.Max(y1, r.yMax);
                }
                frames[section] = Rect.MinMaxRect(x0, y0 - SectionHeader, x1, y1 + 20f);
            }
            return frames;
        }

        /// Sections one under another; inside a section, steps flow left to right by depth and
        /// wrap every StepsPerRow columns; steps at the same depth (branches) stack downwards.
        public static Dictionary<NarrativeNode, Vector2> AutoLayout(NarrativeGraph graph, StoryStructure structure)
        {
            var result = new Dictionary<NarrativeNode, Vector2>();
            var byGuid = new Dictionary<string, NarrativeNode>();
            foreach (var n in graph.nodes) if (n != null) byGuid[n.guid] = n;

            float y = 0f;
            foreach (var section in structure.Sections)
            {
                var members = new HashSet<NarrativeNode>(section.Steps);
                var depth = new Dictionary<NarrativeNode, int>();
                foreach (var step in section.Steps)
                {
                    if (!depth.ContainsKey(step)) depth[step] = 0;
                    if (step.outputs == null) continue;
                    foreach (var g in step.outputs)
                    {
                        if (string.IsNullOrEmpty(g) || !byGuid.TryGetValue(g, out var next) || !members.Contains(next)) continue;
                        // Only forward in story order: a connection back is a loop, not a layer.
                        if (section.Steps.IndexOf(next) <= section.Steps.IndexOf(step)) continue;
                        int d = depth[step] + 1;
                        if (!depth.TryGetValue(next, out var cur) || cur < d) depth[next] = d;
                    }
                }

                var cells = new SortedDictionary<int, List<NarrativeNode>>();
                foreach (var step in section.Steps)
                {
                    int d = depth[step];
                    if (!cells.TryGetValue(d, out var list)) cells[d] = list = new List<NarrativeNode>();
                    list.Add(step);
                }

                y += SectionHeader;
                var rowHeights = new Dictionary<int, float>();
                foreach (var pair in cells)
                {
                    int row = pair.Key / StepsPerRow;
                    float h = 0f;
                    foreach (var n in pair.Value) h += HeightOf(n) + RowGap;
                    rowHeights[row] = Mathf.Max(rowHeights.TryGetValue(row, out var cur) ? cur : 0f, h);
                }

                var rowTop = new Dictionary<int, float>();
                float top = y;
                int maxRow = 0;
                foreach (var r in rowHeights.Keys) maxRow = Mathf.Max(maxRow, r);
                for (int r = 0; r <= maxRow; r++)
                {
                    rowTop[r] = top;
                    top += (rowHeights.TryGetValue(r, out var h) ? h : 0f) + RowGap;
                }

                foreach (var pair in cells)
                {
                    int row = pair.Key / StepsPerRow, col = pair.Key % StepsPerRow;
                    float cy = rowTop[row];
                    foreach (var n in pair.Value)
                    {
                        result[n] = new Vector2(col * (CardWidth + ColumnGap), cy);
                        cy += HeightOf(n) + RowGap;
                    }
                }
                y = top + SectionGap - RowGap;
            }

            float noteY = y;
            foreach (var n in graph.nodes)
            {
                if (n == null || !n.IsAnnotation) continue;
                result[n] = new Vector2(0f, noteY);
                noteY += 180f;
            }
            return result;
        }

        // ── Routing ──────────────────────────────────────────────────────────

        public sealed class Route
        {
            public NarrativeNode From;
            public NarrativeNode To;
            public int OutputIndex;
            public string Label;
            public bool Parallel;
            public List<Vector2> Points;
            public Vector2 LabelAt;
            public Vector2 InsertAt;
            public float Length;
        }

        enum Side { Left, Right, Top, Bottom }

        sealed class Spec
        {
            public Route Route;
            public Rect A, B;
            public bool Vertical;
            public Side SideA, SideB;
            public float AlongA, AlongB;
            public float OtherA, OtherB;
        }

        public static List<Route> Routes(NarrativeGraph graph, StoryStructure structure)
        {
            var byGuid = new Dictionary<string, NarrativeNode>();
            foreach (var n in graph.nodes) if (n != null) byGuid[n.guid] = n;
            var frames = SectionFrames(structure);

            var specs = new List<Spec>();
            foreach (var from in graph.nodes)
            {
                if (from?.outputs == null) continue;
                var names = from.GetOutputPorts();
                int connected = 0;
                foreach (var g in from.outputs) if (!string.IsNullOrEmpty(g)) connected++;
                bool parallel = names == null && connected > 1;

                for (int i = 0; i < from.outputs.Count; i++)
                {
                    var g = from.outputs[i];
                    if (string.IsNullOrEmpty(g) || !byGuid.TryGetValue(g, out var to) || to == from) continue;
                    var route = new Route
                    {
                        From = from, To = to, OutputIndex = i, Parallel = parallel,
                        Label = names != null && i < names.Length ? names[i] : null,
                    };
                    specs.Add(MakeSpec(route, RectOf(from), RectOf(to)));
                }
            }

            SpreadSharedSides(specs);

            var routes = new List<Route>(specs.Count);
            foreach (var s in specs)
            {
                var p0 = PointOn(s.A, s.SideA, s.AlongA);
                var p3 = PointOn(s.B, s.SideB, s.AlongB);
                var pts = new List<Vector2> { p0 };
                if (!s.Vertical)
                {
                    if (Mathf.Abs(p0.y - p3.y) > 0.5f)
                    {
                        float mx = (p0.x + p3.x) * 0.5f;
                        pts.Add(new Vector2(mx, p0.y));
                        pts.Add(new Vector2(mx, p3.y));
                    }
                }
                else if (Mathf.Abs(p0.x - p3.x) > 0.5f)
                {
                    float my = (p0.y + p3.y) * 0.5f;
                    if (structure.SectionOf.TryGetValue(s.Route.From.guid, out var sa) && structure.SectionOf.TryGetValue(s.Route.To.guid, out var sb)
                        && sa != sb && frames.TryGetValue(sa, out var fa) && frames.TryGetValue(sb, out var fb))
                    {
                        if (fa.yMax <= fb.yMin) my = (fa.yMax + fb.yMin) * 0.5f;
                        else if (fb.yMax <= fa.yMin) my = (fb.yMax + fa.yMin) * 0.5f;
                    }
                    pts.Add(new Vector2(p0.x, my));
                    pts.Add(new Vector2(p3.x, my));
                }
                pts.Add(p3);

                var r = s.Route;
                r.Points = pts;
                r.Length = Length(pts);
                r.LabelAt = Along(pts, r.Label != null ? 0.36f : 0.5f);
                r.InsertAt = Along(pts, r.Label != null ? 0.68f : 0.5f);
                routes.Add(r);
            }
            return routes;
        }

        static Spec MakeSpec(Route route, Rect a, Rect b)
        {
            var s = new Spec { Route = route, A = a, B = b };
            bool yOverlap = a.yMin < b.yMax && b.yMin < a.yMax;
            bool xOverlap = a.xMin < b.xMax && b.xMin < a.xMax;

            if (yOverlap && !xOverlap)
            {
                s.Vertical = false;
                s.SideA = b.x > a.x ? Side.Right : Side.Left;
                float c = (Mathf.Max(a.yMin, b.yMin) + Mathf.Min(a.yMax, b.yMax)) * 0.5f;
                s.AlongA = s.AlongB = c;
            }
            else if (xOverlap && !yOverlap)
            {
                s.Vertical = true;
                s.SideA = b.y > a.y ? Side.Bottom : Side.Top;
                float c = (Mathf.Max(a.xMin, b.xMin) + Mathf.Min(a.xMax, b.xMax)) * 0.5f;
                s.AlongA = s.AlongB = c;
            }
            else
            {
                float gx = b.x > a.x ? b.xMin - a.xMax : a.xMin - b.xMax;
                float gy = b.y > a.y ? b.yMin - a.yMax : a.yMin - b.yMax;
                s.Vertical = gy >= 60f || gy > gx;
                if (s.Vertical)
                {
                    s.SideA = b.y > a.y ? Side.Bottom : Side.Top;
                    s.AlongA = a.center.x; s.AlongB = b.center.x;
                }
                else
                {
                    s.SideA = b.x > a.x ? Side.Right : Side.Left;
                    s.AlongA = a.center.y; s.AlongB = b.center.y;
                }
            }
            s.SideB = Opposite(s.SideA);
            s.OtherA = s.Vertical ? b.center.x : b.center.y;
            s.OtherB = s.Vertical ? a.center.x : a.center.y;
            return s;
        }

        static void SpreadSharedSides(List<Spec> specs)
        {
            var groups = new Dictionary<(NarrativeNode, Side), List<(Spec spec, bool isA)>>();
            foreach (var s in specs)
            {
                Add(groups, (s.Route.From, s.SideA), (s, true));
                Add(groups, (s.Route.To, s.SideB), (s, false));
            }
            foreach (var list in groups.Values)
            {
                if (list.Count < 2) continue;
                list.Sort((p, q) => (p.isA ? p.spec.OtherA : p.spec.OtherB).CompareTo(q.isA ? q.spec.OtherA : q.spec.OtherB));
                for (int i = 0; i < list.Count; i++)
                {
                    var (spec, isA) = list[i];
                    var rect = isA ? spec.A : spec.B;
                    float t = (i + 1f) / (list.Count + 1f);
                    float v = spec.Vertical ? rect.xMin + rect.width * t : rect.yMin + rect.height * t;
                    if (isA) spec.AlongA = v; else spec.AlongB = v;
                }
            }
        }

        static void Add(Dictionary<(NarrativeNode, Side), List<(Spec, bool)>> groups, (NarrativeNode, Side) key, (Spec, bool) item)
        {
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<(Spec, bool)>();
            list.Add(item);
        }

        static Side Opposite(Side s) => s switch
        {
            Side.Left => Side.Right,
            Side.Right => Side.Left,
            Side.Top => Side.Bottom,
            _ => Side.Top,
        };

        static Vector2 PointOn(Rect r, Side side, float along) => side switch
        {
            Side.Left => new Vector2(r.xMin, along),
            Side.Right => new Vector2(r.xMax, along),
            Side.Top => new Vector2(along, r.yMin),
            _ => new Vector2(along, r.yMax),
        };

        public static float Length(List<Vector2> pts)
        {
            float total = 0f;
            for (int i = 1; i < pts.Count; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
            return total;
        }

        public static Vector2 Along(List<Vector2> pts, float fraction)
        {
            float target = Length(pts) * fraction;
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Vector2.Distance(pts[i - 1], pts[i]);
                if (target <= seg || i == pts.Count - 1)
                    return seg > 0f ? Vector2.Lerp(pts[i - 1], pts[i], Mathf.Clamp01(target / seg)) : pts[i];
                target -= seg;
            }
            return pts[0];
        }
    }
}
