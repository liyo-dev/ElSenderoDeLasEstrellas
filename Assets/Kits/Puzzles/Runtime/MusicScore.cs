using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Partitura de una canción para un nivel que va a su compás: los tiempos medidos en la toma
    /// (una canción real no lleva un BPM exacto) y las marcas de lo que pasa en cada golpe, cada una
    /// con su voz (qué cae, qué se lanza, qué se abre). El reloj (<see cref="TempoClock"/>) sigue los
    /// tiempos; quien reacciona a las marcas (<see cref="ScoreCueTrigger"/>, <see cref="CueDrop"/>)
    /// las lee por su voz.
    /// </summary>
    [CreateAssetMenu(menuName = "Kits/Puzzles/Music Score", fileName = "MusicScore")]
    public sealed class MusicScore : ScriptableObject
    {
        [Serializable]
        public struct Cue
        {
            [Tooltip("Segundo de la canción en el que suena el golpe.")]
            public float time;
            [Tooltip("Qué es (la voz de la partitura): cada nivel decide qué hace cada voz.")]
            public string voice;
            [Tooltip("Compás (desde 1), solo para leerlo.")]
            public int measure;
            [Tooltip("Tiempo dentro del compás (desde 1; 2,5 = el «y» del 2), solo para leerlo.")]
            public float beat;
        }

        [Tooltip("La canción.")]
        [SerializeField] private AudioClip clip;
        [Tooltip("Segundo de cada tiempo de la canción, en orden.")]
        [SerializeField] private float[] beatTimes = Array.Empty<float>();
        [Tooltip("Índice del primer tiempo que es el 1 de un compás.")]
        [SerializeField, Min(0)] private int firstDownbeatIndex;
        [Tooltip("Tiempos por compás.")]
        [SerializeField, Min(1)] private int beatsPerMeasure = 4;
        [Tooltip("Las marcas, en orden de tiempo.")]
        [SerializeField] private Cue[] cues = Array.Empty<Cue>();

        public AudioClip Clip => clip;
        public int BeatCount => beatTimes.Length;
        public int FirstDownbeatIndex => firstDownbeatIndex;
        public int BeatsPerMeasure => beatsPerMeasure;
        public IReadOnlyList<Cue> Cues => cues;

        public float BeatTime(int index)
        {
            if (beatTimes.Length == 0) return 0f;
            return beatTimes[Mathf.Clamp(index, 0, beatTimes.Length - 1)];
        }

        /// <summary>El último tiempo que ya ha sonado en ese segundo (-1 antes del primero).</summary>
        public int BeatIndexAt(double seconds)
        {
            int lo = 0, hi = beatTimes.Length - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (beatTimes[mid] <= seconds) { found = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return found;
        }

        /// <summary>Segundos entre ese tiempo y el siguiente (el último repite el anterior).</summary>
        public float BeatLength(int index)
        {
            if (beatTimes.Length < 2) return 0.5f;
            int i = Mathf.Clamp(index, 0, beatTimes.Length - 2);
            return Mathf.Max(0.05f, beatTimes[i + 1] - beatTimes[i]);
        }

        /// <summary>
        /// Añade a la lista, en orden, los segundos de las marcas de esas voces que caen entre
        /// <paramref name="from"/> y <paramref name="to"/>. Sin voces, valen todas.
        /// </summary>
        public void CollectCueTimes(IReadOnlyList<string> voices, float from, float to, List<float> into)
        {
            for (int i = 0; i < cues.Length; i++)
            {
                var c = cues[i];
                if (c.time < from || c.time > to) continue;
                if (!HasVoice(voices, c.voice)) continue;
                into.Add(c.time);
            }
        }

        private static bool HasVoice(IReadOnlyList<string> voices, string voice)
        {
            if (voices == null || voices.Count == 0) return true;
            for (int i = 0; i < voices.Count; i++)
                if (string.Equals(voices[i], voice, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>Rellena la partitura desde una herramienta de editor (importar una medición).</summary>
        public void SetData(AudioClip songClip, float[] beats, int firstDownbeat, int measureBeats, Cue[] allCues)
        {
            clip = songClip;
            beatTimes = beats ?? Array.Empty<float>();
            firstDownbeatIndex = Mathf.Max(0, firstDownbeat);
            beatsPerMeasure = Mathf.Max(1, measureBeats);
            cues = allCues ?? Array.Empty<Cue>();
            Array.Sort(cues, (a, b) => a.time.CompareTo(b.time));
        }
#endif
    }
}
