using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Pinta los pictogramas de StoryPictograms en las tarjetas, de dos maneras: la banda, rellena
    /// a dos tonos del color de la familia y con un eco grande del mismo dibujo, girado y
    /// recortado por el borde derecho; y el icono de la cabecera, en línea blanca sobre el
    /// cuadrado de color. Son trazados, no texturas: se ven nítidos a cualquier zoom. Si el
    /// proyecto asigna una textura en NarrativeIconSet, la banda pinta esa en su lugar.
    public static class StoryCardArt
    {
        const float EchoDegrees = -12f;

        /// La banda de la tarjeta, de borde a borde de r.
        public static void DrawBand(Painter2D p, Rect r, string category, Color color)
        {
            if (r.width < 8f || r.height < 8f) return;
            var pic = StoryPictograms.Of(category);
            var card = StoryColors.Card;

            p.fillColor = Color.Lerp(card, color, 0.20f);
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
            p.Fill();

            var echo = new Frame(new Vector2(r.xMax - r.height * 0.62f, r.center.y + r.height * 0.16f), r.height * 1.5f, EchoDegrees);
            Paint(p, pic, echo, Color.Lerp(card, color, 0.40f), Color.Lerp(card, color, 0.31f));

            float size = r.height * 0.72f;
            var center = new Vector2(r.xMin + r.height * 0.3f + size * 0.5f, r.center.y);
            var shadow = Color.Lerp(card, color, 0.06f);
            Paint(p, pic, new Frame(center + new Vector2(0f, Mathf.Max(1f, size * 0.045f)), size, 0f), shadow, shadow);
            Paint(p, pic, new Frame(center, size, 0f), Color.Lerp(color, Color.white, 0.18f), Color.Lerp(color, Color.black, 0.25f));
        }

        /// El icono de la cabecera: el mismo pictograma en línea blanca sobre el color de la familia.
        public static void DrawIcon(Painter2D p, Rect r, string category, Color background)
        {
            if (r.width < 4f || r.height < 4f) return;
            float size = Mathf.Min(r.width, r.height) * 0.74f;
            Outline(p, StoryPictograms.Of(category), new Frame(r.center, size, 0f), Color.white, background, size * 0.115f);
        }

        // ── Pintado ─────────────────────────────────────────────────────────

        /// Coloca la rejilla de 24 del pictograma: centro, lado y giro.
        readonly struct Frame
        {
            readonly Vector2 _center;
            readonly float _cos, _sin;
            public readonly float scale, degrees;

            public Frame(Vector2 center, float size, float degrees)
            {
                _center = center;
                scale = size / 24f;
                this.degrees = degrees;
                float a = degrees * Mathf.Deg2Rad;
                _cos = Mathf.Cos(a);
                _sin = Mathf.Sin(a);
            }

            public Vector2 P(Vector2 v)
            {
                float x = (v.x - 12f) * scale, y = (v.y - 12f) * scale;
                return _center + new Vector2(x * _cos - y * _sin, x * _sin + y * _cos);
            }
        }

        static void Paint(Painter2D p, StoryPictograms.Pictogram pic, Frame f, Color light, Color dark)
        {
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            foreach (var s in pic.shapes)
            {
                var c = s.tone == StoryPictograms.Tone.Light ? light : dark;
                switch (s.kind)
                {
                    case StoryPictograms.Kind.Area:
                        p.fillColor = c;
                        p.BeginPath(); Trace(p, s.path, f); p.Fill();
                        break;
                    case StoryPictograms.Kind.Line:
                        p.strokeColor = c;
                        p.lineWidth = s.width * f.scale;
                        p.BeginPath(); Trace(p, s.path, f); p.Stroke();
                        break;
                    case StoryPictograms.Kind.Circle:
                    case StoryPictograms.Kind.Dot:
                        p.fillColor = c;
                        p.BeginPath(); p.Arc(f.P(s.center), s.radius * f.scale, 0f, 360f); p.ClosePath(); p.Fill();
                        break;
                    case StoryPictograms.Kind.Arc:
                        p.strokeColor = c;
                        p.lineWidth = s.width * f.scale;
                        p.BeginPath(); p.Arc(f.P(s.center), s.radius * f.scale, s.from + f.degrees, s.to + f.degrees); p.Stroke();
                        break;
                }
            }
        }

        /// En línea: cada superficie se rellena con el color de fondo antes de perfilarla, así
        /// tapa los trazos de lo que tiene detrás, como en el dibujo relleno.
        static void Outline(Painter2D p, StoryPictograms.Pictogram pic, Frame f, Color ink, Color paper, float width)
        {
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.lineWidth = width;
            p.strokeColor = ink;
            foreach (var s in pic.shapes)
            {
                switch (s.kind)
                {
                    case StoryPictograms.Kind.Area:
                        p.fillColor = paper;
                        p.BeginPath(); Trace(p, s.path, f); p.Fill();
                        p.BeginPath(); Trace(p, s.path, f); p.Stroke();
                        break;
                    case StoryPictograms.Kind.Line:
                        p.BeginPath(); Trace(p, s.path, f); p.Stroke();
                        break;
                    case StoryPictograms.Kind.Circle:
                        p.fillColor = paper;
                        p.BeginPath(); p.Arc(f.P(s.center), s.radius * f.scale, 0f, 360f); p.ClosePath(); p.Fill();
                        p.BeginPath(); p.Arc(f.P(s.center), s.radius * f.scale, 0f, 360f); p.ClosePath(); p.Stroke();
                        break;
                    case StoryPictograms.Kind.Dot:
                        p.fillColor = ink;
                        p.BeginPath(); p.Arc(f.P(s.center), Mathf.Max(width * 0.6f, s.radius * f.scale), 0f, 360f); p.ClosePath(); p.Fill();
                        break;
                    case StoryPictograms.Kind.Arc:
                        p.BeginPath(); p.Arc(f.P(s.center), s.radius * f.scale, s.from + f.degrees, s.to + f.degrees); p.Stroke();
                        break;
                }
            }
        }

        static void Trace(Painter2D p, StoryPictograms.Command[] path, Frame f)
        {
            foreach (var c in path)
            {
                switch (c.op)
                {
                    case 'M': p.MoveTo(f.P(c.a)); break;
                    case 'L': p.LineTo(f.P(c.a)); break;
                    case 'Q': p.QuadraticCurveTo(f.P(c.a), f.P(c.b)); break;
                    case 'Z': p.ClosePath(); break;
                }
            }
        }
    }
}
