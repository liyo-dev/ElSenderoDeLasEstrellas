using System;
using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Luces de escena que van con la canción, como en una función de teatro: en cada tiempo dan un golpe
    /// de intensidad que se apaga enseguida, y por tramos de la canción cambian de paleta. Dentro de un tramo,
    /// cada luz toma un color de la paleta y van rotando en cada compás (o en cada tiempo, si el tramo lo
    /// pide). Un tramo con intensidad 0 deja la escena a oscuras (por ejemplo, solo con el cañón de seguimiento).
    /// Lee el segundo de la canción del reloj: al reintentar, se pone sola donde toca.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeatLightRig : MonoBehaviour
    {
        [Serializable]
        public struct Section
        {
            [Tooltip("Segundo de la canción desde el que vale este tramo (hasta el siguiente).")]
            public float fromSeconds;
            [Tooltip("Colores del tramo; cada luz toma uno y rotan.")]
            public Color[] colors;
            [Tooltip("Cuánto de la intensidad base (0 = apagadas).")]
            [Range(0f, 3f)] public float intensity;
            [Tooltip("Rotan en cada tiempo en lugar de en cada compás.")]
            public bool changeEveryBeat;
        }

        [Tooltip("Reloj que sigue la canción. Vacío: el de la escena.")]
        [SerializeField] private TempoClock clock;
        [SerializeField] private Light[] lights = Array.Empty<Light>();
        [Tooltip("Tramos en orden de segundo.")]
        [SerializeField] private Section[] sections = Array.Empty<Section>();
        [SerializeField, Min(0f)] private float baseIntensity = 6f;
        [Tooltip("Golpe de cada tiempo, sobre la intensidad del tramo (0,5 = la mitad más).")]
        [SerializeField, Min(0f)] private float beatKick = 0.5f;
        [Tooltip("Segundos que tarda en apagarse el golpe de cada tiempo.")]
        [SerializeField, Min(0.01f)] private float kickDecay = 0.2f;
        [Tooltip("Segundos que tarda una luz en pasar de un color al siguiente.")]
        [SerializeField, Min(0.01f)] private float colorBlendSeconds = 0.12f;

        private Color[] _actual = Array.Empty<Color>();

        private void OnEnable()
        {
            if (clock == null) clock = TempoClock.Current;
            if (_actual.Length != lights.Length) _actual = new Color[lights.Length];
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) _actual[i] = lights[i].color;
        }

        private void LateUpdate()
        {
            if (clock == null || !clock.IsRunning || sections.Length == 0) return;
            var score = clock.Score;
            if (score == null || score.BeatCount < 2) return;
            double t = clock.SongTime;

            int s = 0;
            for (int i = 1; i < sections.Length; i++) if (t >= sections[i].fromSeconds) s = i;
            var tramo = sections[s];

            int beat = Mathf.Clamp(score.BeatIndexAt(t), 0, score.BeatCount - 1);
            float since = Mathf.Max(0f, (float)(t - score.BeatTime(beat)));
            int measure = Mathf.Max(0, beat - score.FirstDownbeatIndex) / Mathf.Max(1, score.BeatsPerMeasure);
            int unit = tramo.changeEveryBeat ? beat : measure;
            float golpe = 1f + beatKick * Mathf.Exp(-since / kickDecay);
            float intensidad = baseIntensity * tramo.intensity * golpe;
            float mezcla = 1f - Mathf.Exp(-Time.deltaTime / colorBlendSeconds);

            for (int i = 0; i < lights.Length; i++)
            {
                var luz = lights[i];
                if (luz == null) continue;
                if (tramo.colors != null && tramo.colors.Length > 0)
                {
                    _actual[i] = Color.Lerp(_actual[i], tramo.colors[(unit + i) % tramo.colors.Length], mezcla);
                    luz.color = _actual[i];
                }
                luz.intensity = intensidad;
                bool encendida = intensidad > 0.01f;
                if (luz.enabled != encendida) luz.enabled = encendida;
            }
        }
    }
}
