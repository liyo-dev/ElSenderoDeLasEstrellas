using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Los dibujos de las tarjetas, en el hueco de la antigua foto. Son líneas, no capturas: no
    /// escriben en disco y se adaptan al tamaño de la tarjeta. Cada categoría tiene el suyo; si el
    /// proyecto asigna una textura en NarrativeIconSet, la tarjeta pinta esa en su lugar.
    public static class StoryCardArt
    {
        public static void Draw(Painter2D p, Rect r, string category, Color color)
        {
            if (r.width < 8f || r.height < 8f) return;

            float scale = Mathf.Min(r.width / 100f, r.height / 50f);
            Vector2 origin = new Vector2(r.center.x - 50f * scale, r.center.y - 25f * scale);
            Vector2 P(float x, float y) => origin + new Vector2(x, y) * scale;
            Func<float, float, Vector2> map = P;

            p.strokeColor = Color.Lerp(color, Color.white, 0.45f);
            p.fillColor = p.strokeColor;
            p.lineWidth = 2.2f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            switch (category)
            {
                case "Flujo": Flow(p, map); break;
                case "Quests": Quests(p, map); break;
                case "Diálogo": Dialogo(p, map); break;
                case "NPCs": Npcs(p, map); break;
                case "Grupo": Grupo(p, map); break;
                case "Señales": Senales(p, map); break;
                case "Cinemáticas": Cinematicas(p, map); break;
                case "Audio y cine": Audio(p, map); break;
                case "Objetos": Objetos(p, map); break;
                case "Jugador": Jugador(p, map); break;
                case "Mundo": Mundo(p, map); break;
                case "Notas": Notas(p, map); break;
                case "Combate": Combate(p, map); break;
                default: Star(p, map, 50f, 25f, 16f, 7); break;
            }

            // Unas chispas alrededor, como en un cuaderno.
            p.strokeColor = new Color(p.strokeColor.r, p.strokeColor.g, p.strokeColor.b, 0.55f);
            Sparkle(p, map, 26f, 13f, 2.2f, 91);
            Sparkle(p, map, 74f, 37f, 2.0f, 97);
            Dot(p, map, 72f, 14f, 1.3f, 101);
            Dot(p, map, 28f, 37f, 1.1f, 103);
        }

        // ── Dibujos ──────────────────────────────────────────────────────────

        static void Flow(Painter2D p, Func<float, float, Vector2> P)
        {
            Ellipse(p, P, 50f, 25f, 15f, 10f, 10, -55f, 235f);
            float a = 235f * Mathf.Deg2Rad;
            float ex = 50f + Mathf.Cos(a) * 15f, ey = 25f + Mathf.Sin(a) * 10f;
            float tx = -Mathf.Sin(a) * 15f, ty = Mathf.Cos(a) * 10f;
            float len = Mathf.Sqrt(tx * tx + ty * ty);
            tx /= len; ty /= len;
            float nx = -ty, ny = tx;
            Line(p, P, ex, ey, ex - tx * 5f + nx * 3f, ey - ty * 5f + ny * 3f, 14);
            Line(p, P, ex, ey, ex - tx * 5f - nx * 3f, ey - ty * 5f - ny * 3f, 15);
        }

        static void Quests(Painter2D p, Func<float, float, Vector2> P)
        {
            Ellipse(p, P, 42f, 25f, 12f, 12f, 20);
            Ellipse(p, P, 42f, 25f, 7f, 7f, 21);
            Polyline(p, P, new[]
            {
                (54f, 22f), (60f, 18f), (66f, 22f), (72f, 17f), (77f, 23f),
                (71f, 29f), (65f, 25f), (59f, 30f), (54f, 26f),
            }, 22);
            Dot(p, P, 77f, 23f, 1.4f, 23);
        }

        static void Dialogo(Painter2D p, Func<float, float, Vector2> P)
        {
            Ellipse(p, P, 42f, 22f, 15f, 9f, 30);
            Line(p, P, 33f, 29f, 29f, 36f, 31);
            Line(p, P, 29f, 36f, 40f, 29f, 32);
            Ellipse(p, P, 63f, 32f, 11f, 7f, 33);
            Line(p, P, 57f, 37f, 55f, 43f, 34);
            Line(p, P, 55f, 43f, 62f, 38f, 35);
            Dot(p, P, 28f, 39f, 1.2f, 36);
            Dot(p, P, 33f, 43f, 1.0f, 37);
        }

        static void Npcs(Painter2D p, Func<float, float, Vector2> P)
        {
            Head(p, P, 40f, 20f, 7f, 40);
            Shoulders(p, P, 40f, 31f, 13f, 9f, 41);
            Head(p, P, 60f, 22f, 6f, 42);
            Shoulders(p, P, 60f, 32f, 11f, 8f, 43);
        }

        static void Grupo(Painter2D p, Func<float, float, Vector2> P)
        {
            Head(p, P, 40f, 20f, 6f, 50);
            Shoulders(p, P, 40f, 31f, 11f, 8f, 51);
            Head(p, P, 50f, 17f, 7f, 52);
            Shoulders(p, P, 50f, 29f, 13f, 9f, 53);
            Head(p, P, 60f, 20f, 6f, 54);
            Shoulders(p, P, 60f, 31f, 11f, 8f, 55);
        }

        static void Senales(Painter2D p, Func<float, float, Vector2> P)
        {
            Line(p, P, 50f, 40f, 50f, 18f, 60);
            Ellipse(p, P, 50f, 16f, 3f, 3f, 61);
            Ellipse(p, P, 50f, 16f, 8f, 8f, 62, -55f, 55f);
            Ellipse(p, P, 50f, 16f, 13f, 13f, 63, -55f, 55f);
            Ellipse(p, P, 50f, 16f, 18f, 18f, 64, -55f, 55f);
            Line(p, P, 43f, 40f, 57f, 40f, 65);
        }

        static void Cinematicas(Painter2D p, Func<float, float, Vector2> P)
        {
            Rect(p, P, 36f, 20f, 28f, 16f, 70);
            Ellipse(p, P, 43f, 15f, 5f, 5f, 71);
            Ellipse(p, P, 57f, 15f, 5f, 5f, 72);
            Ellipse(p, P, 64f, 28f, 5f, 5f, 73);
            Line(p, P, 50f, 36f, 44f, 44f, 74);
            Line(p, P, 50f, 36f, 56f, 44f, 75);
            Line(p, P, 44f, 44f, 56f, 44f, 76);
            Dot(p, P, 64f, 28f, 1.4f, 77);
        }

        static void Audio(Painter2D p, Func<float, float, Vector2> P)
        {
            Ellipse(p, P, 41f, 34f, 5f, 4f, 80);
            Line(p, P, 46f, 34f, 46f, 14f, 81);
            Bezier(p, P, 46f, 14f, 53f, 16f, 56f, 21f, 82);
            Ellipse(p, P, 63f, 25f, 6f, 6f, 83, -55f, 55f);
            Ellipse(p, P, 63f, 25f, 11f, 11f, 84, -55f, 55f);
        }

        static void Objetos(Painter2D p, Func<float, float, Vector2> P)
        {
            Rect(p, P, 36f, 27f, 28f, 13f, 90);
            Ellipse(p, P, 50f, 27f, 14f, 10f, 91, 180f, 360f);
            Line(p, P, 36f, 27f, 64f, 27f, 92);
            Rect(p, P, 47f, 31f, 6f, 7f, 93);
            Dot(p, P, 50f, 34f, 1.2f, 94);
        }

        static void Jugador(Painter2D p, Func<float, float, Vector2> P)
        {
            Head(p, P, 50f, 16f, 7f, 100);
            Shoulders(p, P, 50f, 28f, 15f, 10f, 101);
            Star(p, P, 50f, 29f, 3.5f, 102);
        }

        static void Mundo(Painter2D p, Func<float, float, Vector2> P)
        {
            Ellipse(p, P, 50f, 25f, 15f, 15f, 110);
            Ellipse(p, P, 50f, 25f, 7f, 15f, 111);
            Ellipse(p, P, 50f, 25f, 15f, 5f, 112);
            Line(p, P, 35f, 25f, 65f, 25f, 113);
        }

        static void Notas(Painter2D p, Func<float, float, Vector2> P)
        {
            Rect(p, P, 38f, 10f, 24f, 30f, 120);
            Line(p, P, 56f, 10f, 62f, 16f, 121);
            Line(p, P, 56f, 10f, 56f, 16f, 122);
            Line(p, P, 56f, 16f, 62f, 16f, 123);
            Line(p, P, 42f, 22f, 58f, 22f, 124);
            Line(p, P, 42f, 28f, 58f, 28f, 125);
            Line(p, P, 42f, 34f, 52f, 34f, 126);
            Line(p, P, 64f, 36f, 72f, 20f, 127);
            Line(p, P, 64f, 36f, 66f, 39f, 128);
        }

        static void Combate(Painter2D p, Func<float, float, Vector2> P)
        {
            Line(p, P, 32f, 38f, 66f, 12f, 130);
            Line(p, P, 34f, 40f, 68f, 14f, 131);
            Line(p, P, 66f, 12f, 70f, 10f, 132);
            Line(p, P, 70f, 10f, 36f, 40f, 133);
            Line(p, P, 58f, 18f, 68f, 26f, 134);
            Line(p, P, 66f, 12f, 72f, 6f, 135);
            Dot(p, P, 73f, 5f, 2f, 136);
        }

        // ── Piezas ───────────────────────────────────────────────────────────

        static void Head(Painter2D p, Func<float, float, Vector2> P, float cx, float cy, float r, int seed)
        {
            Ellipse(p, P, cx, cy, r, r, seed);
            Dot(p, P, cx - r * 0.35f, cy - r * 0.1f, Mathf.Max(1f, r * 0.12f), seed + 1);
            Dot(p, P, cx + r * 0.35f, cy - r * 0.1f, Mathf.Max(1f, r * 0.12f), seed + 2);
        }

        static void Shoulders(Painter2D p, Func<float, float, Vector2> P, float cx, float cy, float rx, float ry, int seed)
            => Ellipse(p, P, cx, cy, rx, ry, seed, 0f, 180f);

        static void Line(Painter2D p, Func<float, float, Vector2> P,
            float x1, float y1, float x2, float y2, int seed)
        {
            p.BeginPath();
            p.MoveTo(J(P(x1, y1), seed));
            p.LineTo(J(P(x2, y2), seed + 1));
            p.Stroke();
        }

        static void Bezier(Painter2D p, Func<float, float, Vector2> P,
            float x1, float y1, float cx, float cy, float x2, float y2, int seed)
        {
            p.BeginPath();
            p.MoveTo(J(P(x1, y1), seed));
            p.BezierCurveTo(J(P(cx, cy), seed + 1),
                            J(P(cx, cy), seed + 2),
                            J(P(x2, y2), seed + 3));
            p.Stroke();
        }

        static void Ellipse(Painter2D p, Func<float, float, Vector2> P,
            float cx, float cy, float rx, float ry, int seed, float start = 0f, float end = 360f)
        {
            int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(end - start) / 8f), 8, 48);
            p.BeginPath();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)steps) * Mathf.Deg2Rad;
                var v = J(P(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry), seed + i);
                if (i == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.Stroke();
        }

        static void Rect(Painter2D p, Func<float, float, Vector2> P,
            float x, float y, float w, float h, int seed)
        {
            Line(p, P, x, y, x + w, y, seed);
            Line(p, P, x + w, y, x + w, y + h, seed + 10);
            Line(p, P, x + w, y + h, x, y + h, seed + 20);
            Line(p, P, x, y + h, x, y, seed + 30);
        }

        static void Polyline(Painter2D p, Func<float, float, Vector2> P, (float x, float y)[] points, int seed)
        {
            if (points == null || points.Length < 2) return;
            p.BeginPath();
            for (int i = 0; i < points.Length; i++)
            {
                var v = J(P(points[i].x, points[i].y), seed + i);
                if (i == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.Stroke();
        }

        static void Star(Painter2D p, Func<float, float, Vector2> P, float cx, float cy, float size, int seed)
        {
            p.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? size : size * 0.45f;
                var v = J(P(cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius), seed + i);
                if (i == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.ClosePath();
            p.Stroke();
        }

        static void Sparkle(Painter2D p, Func<float, float, Vector2> P, float cx, float cy, float size, int seed)
        {
            Line(p, P, cx - size, cy, cx + size, cy, seed);
            Line(p, P, cx, cy - size, cx, cy + size, seed + 1);
        }

        static void Dot(Painter2D p, Func<float, float, Vector2> P, float cx, float cy, float radius, int seed)
        {
            p.BeginPath();
            p.Arc(J(P(cx, cy), seed), radius, 0f, 360f);
            p.Fill();
        }

        /// Un temblor pequeño y determinista: parece dibujado a mano, pero no cambia entre repintados.
        static Vector2 J(Vector2 v, int seed)
        {
            float a = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            float b = Mathf.Sin(seed * 39.346f + 11.135f) * 24634.6345f;
            return v + new Vector2((a - Mathf.Floor(a)) - 0.5f, (b - Mathf.Floor(b)) - 0.5f) * 2.2f;
        }
    }
}
