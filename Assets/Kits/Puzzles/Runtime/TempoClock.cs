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
    ///
    /// Con una partitura (<see cref="MusicScore"/>) sigue a la canción en lugar de un BPM fijo: toca
    /// la canción y cada tiempo es el que se midió en la toma, aunque el tempo se mueva un poco.
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

        [Header("Canción (opcional)")]
        [Tooltip("Partitura con los tiempos medidos de la canción. Con ella el reloj sigue a la música y el BPM de arriba no se usa.")]
        [SerializeField] private MusicScore score;
        [Tooltip("Fuente que toca la canción de la partitura. Vacía: se añade una a este objeto.")]
        [SerializeField] private AudioSource source;
        [Tooltip("Segundos que se suman al retardo de la salida de audio (el del búfer se mide solo). Positivo: lo que va a la música llega más tarde.")]
        [SerializeField, Range(-0.2f, 0.3f)] private float extraLatency;

        /// <summary>Un tiempo: (compás, tiempo dentro del compás 0..n-1).</summary>
        public event Action<int, int> Beat;
        /// <summary>El tiempo fuerte de un compás (el 1).</summary>
        public event Action<int> Downbeat;
        /// <summary>El último tiempo del compás: el aviso de que llega el fuerte.</summary>
        public event Action<int> Upbeat;
        /// <summary>Cambia el ritmo en caliente y conserva el compás.</summary>
        public event Action<float> TempoChanged;

        public float Bpm => FollowsSong ? 60f / score.BeatLength(Mathf.Max(0, TotalBeats)) : bpm;
        public int BeatsPerMeasure => FollowsSong ? score.BeatsPerMeasure : beatsPerMeasure;
        /// <summary>La partitura que sigue (null si va a un BPM fijo).</summary>
        public MusicScore Score => score;
        /// <summary>Fuente que suena la canción (null si el reloj va sin música).</summary>
        public AudioSource Source => source;
        /// <summary>Sigue a una canción medida en lugar de a un BPM fijo.</summary>
        public bool FollowsSong => score != null && score.BeatCount > 1;
        /// <summary>Segundo de la canción en el que va (solo con partitura).</summary>
        /// <summary>
        /// Segundo de la canción que se está oyendo (solo con partitura): el que lee la fuente menos
        /// lo que tarda el audio en salir por los altavoces.
        /// </summary>
        public double SongTime { get; private set; }
        /// <summary>Retardo de la salida de audio que se descuenta, en segundos.</summary>
        public float OutputLatency => _latencia;
        public bool IsRunning { get; private set; }
        public int CurrentMeasure { get; private set; }
        public int CurrentBeatInMeasure { get; private set; }
        /// <summary>Cuánto se ha andado del tiempo actual, de 0 a 1.</summary>
        public float BeatPhase01 { get; private set; }
        /// <summary>Cuánto se ha andado del compás actual, de 0 a 1.</summary>
        public float MeasurePhase01 =>
            (CurrentBeatInMeasure + BeatPhase01) / Mathf.Max(1, beatsPerMeasure);
        public float SecondsPerBeat => FollowsSong ? score.BeatLength(Mathf.Max(0, TotalBeats)) : 60f / Mathf.Max(1f, bpm);
        public float SecondsPerMeasure => SecondsPerBeat * Mathf.Max(1, BeatsPerMeasure);
        /// <summary>Tiempos que se han contado desde el principio.</summary>
        public int TotalBeats { get; private set; }
        /// <summary>Segundos que faltan para el tiempo siguiente (lo que se tarda en cambiar de estado).</summary>
        public float SecondsToNextBeat => (1f - BeatPhase01) * SecondsPerBeat;

        private double _inicio;
        private int _compasDeSalida;
        private int _ultimoTiempoContado = -1;
        private bool _avisoDeSitio;

        // Con partitura: la fuente avanza a saltos (bloques de audio); entre salto y salto se suma el
        // tiempo real para que el pulso no tiemble. Si la canción se acaba, sigue con el reloj del juego.
        private int _ultimaMuestra = -1;
        private double _segundoDeLaMuestra;
        private double _realDeLaMuestra;
        private double _origenSinCancion;
        private float _latencia;
        private float _segundoPendiente = -1f;
        private bool _pausadaPorElJuego;

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
            // La canción se carga ya: si se carga al darle a tocar, empieza tarde y el reloj se adelanta.
            if (FollowsSong && score.Clip != null) score.Clip.LoadAudioData();
            if (startOnEnable) StartPulse();
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            IsRunning = false;
            if (source != null) source.Stop();
        }

        /// <summary>Empieza a contar desde el compás que se diga.</summary>
        public void StartPulse(int startMeasure = 0)
        {
            if (FollowsSong)
            {
                int tiempo = score.FirstDownbeatIndex + Mathf.Max(0, startMeasure) * score.BeatsPerMeasure;
                StartAtSeconds(startMeasure <= 0 ? 0f : score.BeatTime(tiempo));
                return;
            }
            _compasDeSalida = Mathf.Max(0, startMeasure);
            _inicio = Time.time;
            _ultimoTiempoContado = -1;
            CurrentMeasure = _compasDeSalida;
            CurrentBeatInMeasure = 0;
            BeatPhase01 = 0f;
            TotalBeats = 0;
            IsRunning = true;
        }

        /// <summary>
        /// Con partitura: toca la canción desde ese segundo y cuenta los tiempos medidos desde ahí.
        /// Sin partitura no hace nada (el reloj de BPM fijo empieza con <see cref="StartPulse"/>).
        /// </summary>
        public void StartAtSeconds(float seconds)
        {
            if (!FollowsSong) return;
            seconds = Mathf.Max(0f, seconds);
            var clip = score.Clip;
            if (clip != null)
            {
                if (source == null)
                {
                    source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                }
                source.clip = clip;
                source.loop = false;
                if (source.isPlaying) source.Stop();
                _segundoPendiente = seconds;
                // Sin cargar del todo, la fuente ignora el segundo pedido: se espera a que esté.
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                else TocarDesde(seconds);
            }
            _ultimaMuestra = -1;
            _segundoDeLaMuestra = seconds;
            _realDeLaMuestra = Time.unscaledTimeAsDouble;
            _latencia = MedirLatencia();
            _origenSinCancion = Time.timeAsDouble - seconds;
            _pausadaPorElJuego = false;
            SongTime = clip != null ? seconds - _latencia : seconds;

            int indice = score.BeatIndexAt(seconds);
            _ultimoTiempoContado = indice;
            ContarTiempo(indice, avisar: false);
            BeatPhase01 = 0f;
            IsRunning = true;
        }

        /// <summary>Para el reloj sin perder en qué compás iba (con canción, la pausa).</summary>
        public void StopPulse()
        {
            IsRunning = false;
            if (source != null && source.isPlaying) source.Pause();
        }

        /// <summary>Cambia el ritmo sin saltar: se conserva el tiempo en el que iba.
        /// Con partitura no se usa: manda la canción.</summary>
        public void SetBpm(float newBpm)
        {
            if (FollowsSong) return;
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
            if (FollowsSong)
            {
                SeguirLaCancion();
                return;
            }

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

        private void TocarDesde(float seconds)
        {
            _segundoPendiente = -1f;
            source.time = Mathf.Min(seconds, Mathf.Max(0f, source.clip.length - 0.01f));
            source.Play();
            _ultimaMuestra = -1;
            _segundoDeLaMuestra = seconds;
            _realDeLaMuestra = Time.unscaledTimeAsDouble;
        }

        /// <summary>Lo que tarda en oírse una muestra: los búferes de la mezcla más el ajuste del Inspector.</summary>
        private float MedirLatencia()
        {
            AudioSettings.GetDSPBufferSize(out int largo, out int cuantos);
            int frecuencia = AudioSettings.outputSampleRate;
            float buferes = frecuencia > 0 ? (float)largo * cuantos / frecuencia : 0f;
            return Mathf.Max(0f, buferes + extraLatency);
        }

        private void SeguirLaCancion()
        {
            // El juego en pausa (menú, fundido con tiempo parado) para también la canción.
            bool juegoParado = Time.timeScale <= 0f;
            if (source != null && source.clip != null)
            {
                if (juegoParado)
                {
                    if (source.isPlaying) { source.Pause(); _pausadaPorElJuego = true; }
                    return;
                }
                if (_pausadaPorElJuego)
                {
                    source.UnPause();
                    _pausadaPorElJuego = false;
                    _ultimaMuestra = -1;
                }
            }
            else if (juegoParado) return;

            double ahora = Time.unscaledTimeAsDouble;
            if (_segundoPendiente >= 0f && source != null && source.clip != null)
            {
                // Esperando a que se cargue la canción: el reloj no anda hasta que suena.
                if (source.clip.loadState != AudioDataLoadState.Loaded) return;
                TocarDesde(_segundoPendiente);
                return;
            }
            if (source != null && source.clip != null && source.isPlaying)
            {
                int muestra = source.timeSamples;
                if (muestra != _ultimaMuestra)
                {
                    _ultimaMuestra = muestra;
                    _segundoDeLaMuestra = (double)muestra / source.clip.frequency;
                    _realDeLaMuestra = ahora;
                }
                // Entre bloques de audio se avanza con el tiempo real, sin pasarse de un bloque.
                SongTime = _segundoDeLaMuestra + System.Math.Min(ahora - _realDeLaMuestra, 0.1) - _latencia;
                _origenSinCancion = Time.timeAsDouble - SongTime;
            }
            else
            {
                SongTime = Time.timeAsDouble - _origenSinCancion;
            }

            int indice = score.BeatIndexAt(SongTime);
            if (indice >= 0)
            {
                double inicio = score.BeatTime(indice);
                BeatPhase01 = Mathf.Clamp01((float)((SongTime - inicio) / score.BeatLength(indice)));
            }
            else BeatPhase01 = 0f;

            if (indice == _ultimoTiempoContado) return;
            _ultimoTiempoContado = indice;
            ContarTiempo(indice, avisar: indice >= 0);
        }

        /// <summary>Coloca el compás y el tiempo de un índice de la partitura y, si se pide, avisa.</summary>
        private void ContarTiempo(int indice, bool avisar)
        {
            int porCompas = Mathf.Max(1, score.BeatsPerMeasure);
            int relativo = indice - score.FirstDownbeatIndex;
            int compas = relativo >= 0 ? relativo / porCompas : (relativo - porCompas + 1) / porCompas;
            int tiempoDelCompas = relativo - compas * porCompas;
            TotalBeats = Mathf.Max(0, indice);
            CurrentMeasure = compas;
            CurrentBeatInMeasure = tiempoDelCompas;
            if (!avisar) return;
            if (tiempoDelCompas == 0) Downbeat?.Invoke(compas);
            if (tiempoDelCompas == porCompas - 1) Upbeat?.Invoke(compas);
            Beat?.Invoke(compas, tiempoDelCompas);
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
