using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NarrativeGraphEditor.Story
{
    /// Un pictograma por categoría de paso, dibujado sobre una rejilla de 24 × 24. Es el único
    /// origen de los dibujos del editor: la banda de la tarjeta lo pinta relleno a dos tonos y la
    /// cabecera lo pinta en línea (StoryCardArt). Los trazados se escriben como en SVG
    /// (M, L, Q y Z, en coordenadas absolutas) para poder revisarlos fuera de Unity.
    public static class StoryPictograms
    {
        public enum Tone { Light, Dark }
        public enum Kind { Area, Line, Circle, Arc, Dot }

        public struct Command
        {
            public char op;
            public Vector2 a, b;
        }

        /// Una pieza del pictograma. Area: superficie cerrada. Line: trazo abierto (en la banda,
        /// con su grosor; en la cabecera, con el de la línea). Circle: superficie redonda.
        /// Arc: arco abierto. Dot: punto relleno en los dos modos.
        public sealed class Shape
        {
            public Kind kind;
            public Tone tone;
            public Command[] path;
            public Vector2 center;
            public float radius, from, to, width;
        }

        public sealed class Pictogram
        {
            public readonly Shape[] shapes;
            public Pictogram(params Shape[] shapes) { this.shapes = shapes; }
        }

        const Tone L = Tone.Light, D = Tone.Dark;

        static readonly Dictionary<string, Pictogram> s_byCategory = new()
        {
            // Una rama: un paso que se abre en dos.
            ["Flujo"] = new Pictogram(
                Line(D, 2.4f, "M 8 12 L 11 12 L 15.5 7 M 11 12 L 15.5 17"),
                Circle(L, 5, 12, 3.2f),
                Circle(L, 18.5f, 6.5f, 3.2f),
                Circle(D, 18.5f, 17.5f, 3.2f)),

            // Un banderín: la misión que empieza o se cumple.
            ["Quests"] = new Pictogram(
                Line(D, 2.6f, "M 5 21 L 5 3.5"),
                Area(L, "M 6 4 L 20 4 L 16 8.75 L 20 13.5 L 6 13.5 Z")),

            // Dos bocadillos: quien habla y quien contesta.
            ["Diálogo"] = new Pictogram(
                Area(D, "M 12.5 8.5 L 18.5 8.5 Q 21.5 8.5 21.5 11.5 L 21.5 15 Q 21.5 18 18.5 18 L 18 18 L 19 21.5 L 14 18 L 12.5 18 Q 9.5 18 9.5 15 L 9.5 11.5 Q 9.5 8.5 12.5 8.5 Z"),
                Area(L, "M 5.5 3 L 13.5 3 Q 16.5 3 16.5 6 L 16.5 10.5 Q 16.5 13.5 13.5 13.5 L 10 13.5 L 5 17.5 L 6 13.5 L 5.5 13.5 Q 2.5 13.5 2.5 10.5 L 2.5 6 Q 2.5 3 5.5 3 Z")),

            // Dos personas: los personajes que no lleva el jugador.
            ["NPCs"] = new Pictogram(
                Circle(D, 16, 7.5f, 3.3f),
                Area(D, "M 10.5 19 Q 10.5 13 16 13 Q 21.5 13 21.5 19 Z"),
                Circle(L, 9, 9, 4),
                Area(L, "M 2 21.5 Q 2 15 9 15 Q 16 15 16 21.5 Z")),

            // Tres personas: el grupo que acompaña al jugador.
            ["Grupo"] = new Pictogram(
                Circle(D, 5.5f, 9, 2.8f),
                Area(D, "M 1 19 Q 1 14 5.5 14 Q 10 14 10 19 Z"),
                Circle(D, 18.5f, 9, 2.8f),
                Area(D, "M 14 19 Q 14 14 18.5 14 Q 23 14 23 19 Z"),
                Circle(L, 12, 7.5f, 3.6f),
                Area(L, "M 6 21 Q 6 14.5 12 14.5 Q 18 14.5 18 21 Z")),

            // Ondas a los dos lados: algo que se avisa o se espera.
            ["Señales"] = new Pictogram(
                Arc(D, 2.2f, 12, 12, 9.5f, -45, 45),
                Arc(D, 2.2f, 12, 12, 9.5f, 135, 225),
                Arc(L, 2.2f, 12, 12, 5.5f, -45, 45),
                Arc(L, 2.2f, 12, 12, 5.5f, 135, 225),
                Circle(L, 12, 12, 2.4f)),

            // Una claqueta con su botón de reproducir.
            ["Cinemáticas"] = new Pictogram(
                Area(D, "M 3 8.8 L 19.8 4.6 L 20.6 7.6 L 3.8 11.8 Z"),
                Area(L, "M 5 10 L 19 10 Q 21 10 21 12 L 21 19 Q 21 21 19 21 L 5 21 Q 3 21 3 19 L 3 12 Q 3 10 5 10 Z"),
                Area(D, "M 10 12.8 L 15.5 15.5 L 10 18.2 Z")),

            // Un altavoz con sus ondas: música, sonido y cámara.
            ["Audio y cine"] = new Pictogram(
                Arc(D, 2.2f, 12, 12, 8.5f, -45, 45),
                Arc(D, 2.2f, 12, 12, 4.5f, -45, 45),
                Area(L, "M 3 9 L 7 9 L 12 4.5 L 12 19.5 L 7 15 L 3 15 Z")),

            // Un cofre: lo que se da, se quita o se guarda.
            ["Objetos"] = new Pictogram(
                Area(D, "M 4.5 11 L 19.5 11 Q 21 11 21 12.5 L 21 19.5 Q 21 21 19.5 21 L 4.5 21 Q 3 21 3 19.5 L 3 12.5 Q 3 11 4.5 11 Z"),
                Area(L, "M 3 11 L 3 8 Q 3 4 7 4 L 17 4 Q 21 4 21 8 L 21 11 Z"),
                Area(L, "M 11 9 L 13 9 Q 14 9 14 10 L 14 14 Q 14 15 13 15 L 11 15 Q 10 15 10 14 L 10 10 Q 10 9 11 9 Z")),

            // Una persona con la marca de «es el jugador» encima.
            ["Jugador"] = new Pictogram(
                Area(D, "M 8.5 1.5 L 15.5 1.5 L 12 5.6 Z"),
                Circle(L, 12, 11, 3.8f),
                Area(L, "M 5 22 Q 5 16.5 12 16.5 Q 19 16.5 19 22 Z")),

            // Una chincheta de mapa: lugares, escenas y lo que cambia en ellos.
            ["Mundo"] = new Pictogram(
                Area(L, "M 12 22 Q 4.5 14.5 4.5 9.5 Q 4.5 2.5 12 2.5 Q 19.5 2.5 19.5 9.5 Q 19.5 14.5 12 22 Z"),
                Circle(D, 12, 9.5f, 3)),

            // Una hoja con la esquina doblada.
            ["Notas"] = new Pictogram(
                Area(L, "M 5 3 L 15 3 L 19 7 L 19 21 L 5 21 Z"),
                Area(D, "M 15 3 L 15 7 L 19 7 Z"),
                Line(D, 1.8f, "M 8 11 L 16 11 M 8 14.5 L 16 14.5 M 8 18 L 13 18")),

            // Una espada.
            ["Combate"] = new Pictogram(
                Area(L, "M 5.5 16.5 L 17.5 4.5 L 20 4 L 19.5 6.5 L 7.5 18.5 Z"),
                Line(D, 2.6f, "M 4 15 L 9 20"),
                Line(D, 2.6f, "M 6.5 17.5 L 3.5 20.5"),
                Dot(D, 2.8f, 21.2f, 1.5f)),

            // Un bloque: pasos que no son de ninguna familia.
            ["Otros"] = new Pictogram(
                Area(D, "M 4 7.5 L 12 12 L 12 21 L 4 16.5 Z"),
                Area(L, "M 12 12 L 20 7.5 L 20 16.5 L 12 21 Z"),
                Area(L, "M 12 3 L 20 7.5 L 12 12 L 4 7.5 Z")),
        };

        /// El pictograma de la categoría; las que no tienen uno propio usan el de «Otros».
        public static Pictogram Of(string category)
            => category != null && s_byCategory.TryGetValue(category, out var p) ? p : s_byCategory["Otros"];

        public static IEnumerable<string> Categories => s_byCategory.Keys;

        // ── Piezas ───────────────────────────────────────────────────────────

        static Shape Area(Tone tone, string path) => new Shape { kind = Kind.Area, tone = tone, path = Parse(path) };
        static Shape Line(Tone tone, float width, string path) => new Shape { kind = Kind.Line, tone = tone, width = width, path = Parse(path) };
        static Shape Circle(Tone tone, float cx, float cy, float r) => new Shape { kind = Kind.Circle, tone = tone, center = new Vector2(cx, cy), radius = r };
        static Shape Dot(Tone tone, float cx, float cy, float r) => new Shape { kind = Kind.Dot, tone = tone, center = new Vector2(cx, cy), radius = r };
        static Shape Arc(Tone tone, float width, float cx, float cy, float r, float from, float to)
            => new Shape { kind = Kind.Arc, tone = tone, width = width, center = new Vector2(cx, cy), radius = r, from = from, to = to };

        /// Lee un trazado con M x y, L x y, Q cx cy x y y Z.
        static Command[] Parse(string path)
        {
            var parts = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<Command>();
            int i = 0;
            float F() => float.Parse(parts[i++], CultureInfo.InvariantCulture);
            while (i < parts.Length)
            {
                char op = parts[i++][0];
                switch (op)
                {
                    case 'M':
                    case 'L': result.Add(new Command { op = op, a = new Vector2(F(), F()) }); break;
                    case 'Q': result.Add(new Command { op = op, a = new Vector2(F(), F()), b = new Vector2(F(), F()) }); break;
                    case 'Z': result.Add(new Command { op = op }); break;
                    default: throw new FormatException("Trazado de pictograma no válido: " + path);
                }
            }
            return result.ToArray();
        }
    }
}
