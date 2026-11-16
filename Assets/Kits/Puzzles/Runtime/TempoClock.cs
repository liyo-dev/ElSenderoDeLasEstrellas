using System;
using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Reloj de tiempos y compases de un ritmo: dice en qué tiempo y en qué compás va y avisa de
    /// cada uno. Lo que se mueve a su compás (losas, rejillas, velas, mareas) se engancha a sus
    /// avisos y no necesita saber nada más.
    ///
    /// Cuenta con el reloj del juego, así que el ritmo se para cuando el juego se para (un menú, un
    /// fundido de reintento) y no sigue corriendo por debajo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TempoClock : MonoBehaviour
    {
        public static TempoClock Current { get; private set; }

        [Header("Ritmo")]
        [Tooltip("Pulsos por minuto del tramo. Marca lo rápido que late: a 120, un tiempo cada 0,5 s y un compás 4/4 cada 2 s.")]
        [SerializeField, Min(20f)] private float bpm = 120f;
        [Tooltip("Tiempos por compás (4 en un 4/4). El tiempo 0 de cada compás es el fuerte.")]
        [SerializeField, Min(1)] private int beatsPerMeasure = 4;

        [Header("Arranque")]
        [Tooltip("Empieza a contar al activarse.")]
        [SerializeField] private bool startOnEnable = true;

        /// <summary>Un tiempo: (compás, tiempo dentro del compás 0..n-1).</summary>
        public event Action<int, int> Beat;
        /// <summary>El tiempo fuerte de un compás (el 1).</summary>
        public event Action<int> Downbeat;
        /// <summary>El último tiempo del compás: el aviso de que llega el fuerte.</summary>
        public event Action<int> Upbeat;
        /// <summary>Cambia el ritmo en caliente y conserva el compás.</summary>
        public event Action<float> TempoChanged;

        public float Bpm => bpm;
        public int BeatsPerMeasure => beatsPerMeasure;
        public bool IsRunning { get; private set; }
        public int CurrentMeasure { get; private set; }
        public int CurrentBeatInMeasure { get; private set; }
        /// <summary>Cuánto se ha andado del tiempo actual, de 0 a 1.</summary>
        public float BeatPhase01 { get; private set; }
        /// <summary>Cuánto se ha andado del compás actual, de 0 a 1.</summary>
        public float MeasurePhase01 =>
            (CurrentBeatInMeasure + BeatPhase01) / Mathf.Max(1, beatsPerMeasure);
        public float SecondsPerBeat => 60f / Mathf.Max(1f, bpm);
        public float SecondsPerMeasure => SecondsPerBeat * Mathf.Max(1, beatsPerMeasure);
        /// <summary>Tiempos que se han contado desde el principio.</summary>
        public int TotalBeats { get; private set; }
        /// <summary>Segundos que faltan para el tiempo siguiente (lo que se tarda en cambiar de estado).</summary>
        public float SecondsToNextBeat => (1f - BeatPhase01) * SecondsPerBeat;

        private double _inicio;
        private int _compasDeSalida;
        private int _ultimoTiempoContado = -1;
        private bool _avisoDeSitio;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
        }
#endif

        private void Awake()
        {
            _avisoDeSitio = Current != null && Current != this;
            if (Current == null) Current = this;
        }

        private void OnEnable()
        {
            Current = this;
            if (startOnEnable) StartPulse();
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            IsRunning = false;
        }

        /// <summary>Empieza a contar desde el compás que se diga.</summary>
        public void StartPulse(int startMeasure = 0)
        {
            _compasDeSalida = Mathf.Max(0, startMeasure);
            _inicio = Time.time;
            _ultimoTiempoContado = -1;
            CurrentMeasure = _compasDeSalida;
            CurrentBeatInMeasure = 0;
            BeatPhase01 = 0f;
            TotalBeats = 0;
            IsRunning = true;
        }

        /// <summary>Para el reloj sin perder en qué compás iba.</summary>
        public void StopPulse() => IsRunning = false;

        /// <summary>Cambia el ritmo sin saltar: se conserva el tiempo en el que iba.</summary>
        public void SetBpm(float newBpm)
        {
            newBpm = Mathf.Max(20f, newBpm);
            if (Mathf.Approximately(newBpm, bpm)) return;
            // Los tiempos andados se miden con el tempo viejo; el origen se recoloca con el nuevo.
            double tiempos = (Time.time - _inicio) / SecondsPerBeat;
            bpm = newBpm;
            _inicio = Time.time - tiempos * SecondsPerBeat;
            TempoChanged?.Invoke(bpm);
        }

        private void Update()
        {
            if (!IsRunning) return;

            double tiempos = (Time.time - _inicio) / SecondsPerBeat;
            int total = Mathf.FloorToInt((float)tiempos);
            BeatPhase01 = (float)(tiempos - total);

            if (total == _ultimoTiempoContado) return;
            _ultimoTiempoContado = total;
            TotalBeats = total;
            int tiempoDelCompas = total % Mathf.Max(1, beatsPerMeasure);
            CurrentMeasure = _compasDeSalida + total / Mathf.Max(1, beatsPerMeasure);
            CurrentBeatInMeasure = tiempoDelCompas;

            if (tiempoDelCompas == 0) Downbeat?.Invoke(CurrentMeasure);
            if (tiempoDelCompas == beatsPerMeasure - 1) Upbeat?.Invoke(CurrentMeasure);
            Beat?.Invoke(CurrentMeasure, tiempoDelCompas);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            bpm = Mathf.Max(20f, bpm);
            beatsPerMeasure = Mathf.Max(1, beatsPerMeasure);
        }

        private void OnDrawGizmosSelected()
        {
            if (_avisoDeSitio)
                Debug.LogWarning($"[TempoClock] {name}: ya hay otro reloj activo en la escena.", this);
        }
#endif
    }
}
