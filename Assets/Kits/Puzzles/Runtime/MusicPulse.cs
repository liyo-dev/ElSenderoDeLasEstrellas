using System;
using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Reloj de tiempos y compases de una música: dice en qué tiempo y en qué compás va y avisa de
    /// cada uno. Lo que se mueve al ritmo (losas, rejillas, velas, mareas) se engancha a sus avisos
    /// y no necesita saber nada de la canción.
    ///
    /// Si hay una pista sonando, el reloj se lee de la propia pista (no se desfasa nunca). Si no la
    /// hay —una maqueta— cuenta con el reloj de audio y puede llevar un clic audible para oír el
    /// pulso mientras se monta el nivel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicPulse : MonoBehaviour
    {
        public static MusicPulse Current { get; private set; }

        [Header("Ritmo")]
        [Tooltip("Pulsos por minuto de la música.")]
        [SerializeField, Min(20f)] private float bpm = 120f;
        [Tooltip("Tiempos por compás (4 en un 4/4). El tiempo 0 de cada compás es el fuerte.")]
        [SerializeField, Min(1)] private int beatsPerMeasure = 4;

        [Header("Arranque")]
        [Tooltip("Empieza a contar al activarse.")]
        [SerializeField] private bool startOnEnable = true;

        [Header("Pista (opcional)")]
        [Tooltip("Música que manda: el reloj se lee de ella. Sin pista, el reloj cuenta solo.")]
        [SerializeField] private AudioSource musicSource;

        [Header("Clic de maqueta")]
        [Tooltip("Suena un clic en cada tiempo para oír el pulso mientras se monta el nivel.")]
        [SerializeField] private bool clickInBlockout = true;
        [SerializeField] private AudioSource clickSource;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.35f;
        [Tooltip("Tono del clic en el tiempo fuerte (el 1 de cada compás).")]
        [SerializeField] private float downbeatPitch = 1.45f;
        [Tooltip("Tono del clic en el resto de tiempos.")]
        [SerializeField] private float beatPitch = 1f;

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

        private double _inicio;
        private double _segundos;
        private int _compasDeSalida;
        private int _ultimoTiempoContado = -1;
        private AudioClip _clicFuerte, _clicFlojo;
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
            if (clickInBlockout) PrepararClics();
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
            _inicio = Ahora();
            _segundos = 0;
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
            double tiempos = _segundos / SecondsPerBeat;
            bpm = newBpm;
            _segundos = tiempos * SecondsPerBeat;
            _inicio = Ahora() - _segundos;
            TempoChanged?.Invoke(bpm);
        }

        private void Update()
        {
            if (!IsRunning) return;

            _segundos = Ahora() - _inicio;
            double tiempos = _segundos / SecondsPerBeat;
            int total = Mathf.FloorToInt((float)tiempos);
            BeatPhase01 = (float)(tiempos - total);

            if (total != _ultimoTiempoContado)
            {
                _ultimoTiempoContado = total;
                TotalBeats = total;
                int tiempo = total % Mathf.Max(1, beatsPerMeasure);
                CurrentMeasure = _compasDeSalida + total / Mathf.Max(1, beatsPerMeasure);
                CurrentBeatInMeasure = tiempo;

                if (tiempo == 0) Downbeat?.Invoke(CurrentMeasure);
                if (tiempo == beatsPerMeasure - 1) Upbeat?.Invoke(CurrentMeasure);
                Beat?.Invoke(CurrentMeasure, tiempo);
                if (clickInBlockout) SonarClic(tiempo == 0);
            }
        }

        // ── Lo de dentro ─────────────────────────────────────────────────────────────────────

        /// <summary>Si hay pista sonando manda ella; si no, el reloj de audio.</summary>
        private double Ahora()
        {
            if (musicSource != null && musicSource.isPlaying && musicSource.clip != null)
            {
                double porMuestra = 1.0 / Mathf.Max(1, musicSource.clip.frequency);
                return musicSource.timeSamples * porMuestra;
            }
            return AudioSettings.dspTime;
        }

        private void PrepararClics()
        {
            _clicFuerte = ClipDeClic("ClicFuerte", 1180f);
            _clicFlojo = ClipDeClic("ClicFlojo", 760f);
            if (clickSource == null)
            {
                clickSource = gameObject.AddComponent<AudioSource>();
                clickSource.playOnAwake = false;
                clickSource.spatialBlend = 0f;
            }
        }

        /// <summary>Un chasquido corto con caída, para oír los tiempos sin música.</summary>
        private static AudioClip ClipDeClic(string nombre, float hz)
        {
            const int frecuencia = 44100;
            int muestras = Mathf.RoundToInt(frecuencia * 0.05f);
            var datos = new float[muestras];
            for (int i = 0; i < muestras; i++)
            {
                float t = i / (float)frecuencia;
                float caida = 1f - i / (float)muestras;
                datos[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * caida * caida * 0.5f;
            }
            var clip = AudioClip.Create(nombre, muestras, 1, frecuencia, false);
            clip.SetData(datos, 0);
            return clip;
        }

        private void SonarClic(bool fuerte)
        {
            if (clickSource == null) return;
            var clip = fuerte ? _clicFuerte : _clicFlojo;
            if (clip == null) return;
            clickSource.pitch = fuerte ? downbeatPitch : beatPitch;
            clickSource.PlayOneShot(clip, clickVolume);
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
                Debug.LogWarning($"[MusicPulse] {name}: ya hay otro pulso activo en la escena.", this);
        }
#endif
    }
}
