using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace NarrativeGraphEditor.Story
{
    /// Sonda de tiempos del editor de la historia: apunta en <see cref="FilePath"/> cada operación
    /// que pasa de <see cref="Threshold"/> ms, con lo que tardó cada una de sus partes, para saber
    /// qué hace esperar al pulsar algo. No escribe en la consola. Ver INC-1210.
    public static class StoryTiming
    {
        public const double Threshold = 100;
        public const string FilePath = "Logs/NarrativeGraph/tiempos.txt";

        internal sealed class Frame
        {
            public string Name;
            public long Start;
            public readonly List<(string text, double ms)> Parts = new();
        }

        static readonly Stack<Frame> s_open = new();

        public readonly struct Scope : IDisposable
        {
            readonly Frame _frame;
            internal Scope(Frame frame) { _frame = frame; }
            public void Dispose() { if (_frame != null) End(_frame); }
        }

        /// Mide lo que haya dentro del <c>using</c>. Si está dentro de otra medida, cuenta como parte suya.
        public static Scope Measure(string name)
        {
            var frame = new Frame { Name = name, Start = Stopwatch.GetTimestamp() };
            s_open.Push(frame);
            return new Scope(frame);
        }

        /// Marca de tiempo para <see cref="Since"/> y <see cref="UntilPainted"/>.
        public static long Now() => Stopwatch.GetTimestamp();

        /// Milisegundos desde una marca.
        public static double Since(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

        /// Apunta lo que tarda, desde <paramref name="start"/>, en llegar el siguiente fotograma del
        /// panel: incluye montar el diseño y pintarlo, que no pasa por ninguna medida.
        public static void UntilPainted(VisualElement element, string name, long start)
        {
            if (element == null) return;
            element.schedule.Execute(() =>
            {
                double ms = Since(start);
                if (ms >= Threshold) Write($"{name}: hasta verse {ms:0} ms");
            });
        }

        static void End(Frame frame)
        {
            // Las partes se cierran siempre antes que quien las contiene (using); si no, se descartan.
            while (s_open.Count > 0 && s_open.Peek() != frame) s_open.Pop();
            if (s_open.Count > 0) s_open.Pop();

            double ms = Since(frame.Start);
            string text = Describe(frame, ms);
            if (s_open.Count > 0)
            {
                if (ms >= 1) s_open.Peek().Parts.Add((text, ms));
                return;
            }
            if (ms >= Threshold) Write(text);
        }

        static string Describe(Frame frame, double ms)
        {
            var text = $"{frame.Name} {ms:0} ms";
            if (frame.Parts.Count == 0) return text;
            var parts = frame.Parts.OrderByDescending(p => p.ms).Take(8).Select(p => p.text);
            return text + " [" + string.Join(" · ", parts) + "]";
        }

        public static void Write(string line)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var mode = EditorApplication.isPlaying ? " (en Play)" : string.Empty;
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff}{mode}  {line}{Environment.NewLine}");
            }
            catch (Exception) { }
        }
    }
}
