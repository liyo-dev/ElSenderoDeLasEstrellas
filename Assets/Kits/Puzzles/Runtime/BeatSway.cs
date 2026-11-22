using System;
using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Plataforma que va de lado a lado al compás: su desplazamiento sale del segundo de la canción
    /// (un vaivén suave que tarda <see cref="beatsPerSwing"/> tiempos de un extremo al otro), así que al
    /// reintentar se pone sola donde toca. Lleva consigo a quien esté encima (<see cref="Rider"/>, que pone
    /// el juego), para que no se le escurra debajo de los pies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeatSway : MonoBehaviour
    {
        /// <summary>Quien puede ir encima (los pies en su posición). Lo pone el juego; null si no hay nadie.</summary>
        public static Func<Transform> Rider;

        [Tooltip("Reloj que sigue la canción. Vacío: el de la escena.")]
        [SerializeField] private TempoClock clock;
        [Tooltip("Cuánto se aparta del centro a cada lado (en ejes del padre).")]
        [SerializeField] private Vector3 amplitude = new Vector3(2.5f, 0f, 0f);
        [Tooltip("Tiempos de la canción que tarda en ir de un extremo al otro.")]
        [SerializeField, Min(1)] private int beatsPerSwing = 4;
        [Tooltip("Lo que pisa (para saber si alguien va encima). Vacío: el colisionador de este objeto.")]
        [SerializeField] private Collider surface;

        private Vector3 _base;
        private Transform _jinete;
        private Rigidbody _cuerpoDelJinete;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Rider = null;
#endif

        private void Awake()
        {
            _base = transform.localPosition;
            if (surface == null) surface = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            if (clock == null) clock = TempoClock.Current;
        }

        private void FixedUpdate()
        {
            if (clock == null || !clock.IsRunning) return;
            var score = clock.Score;
            if (score == null || score.BeatCount < 2) return;
            double t = clock.SongTime;
            int beat = Mathf.Clamp(score.BeatIndexAt(t), 0, score.BeatCount - 2);
            float fase = Mathf.Clamp01((float)(t - score.BeatTime(beat)) / Mathf.Max(0.01f, score.BeatLength(beat)));
            float s = (beat + fase) / beatsPerSwing;
            Vector3 antes = transform.position;
            transform.localPosition = _base + amplitude * Mathf.Sin(s * Mathf.PI);
            Vector3 movido = transform.position - antes;
            if (movido.sqrMagnitude > 0f) Llevar(movido);
        }

        /// <summary>Mueve con ella a quien tiene los pies encima.</summary>
        private void Llevar(Vector3 movido)
        {
            if (surface == null || !surface.enabled || Rider == null) return;
            var jinete = Rider();
            if (jinete == null) return;
            if (jinete != _jinete)
            {
                _jinete = jinete;
                _cuerpoDelJinete = jinete.GetComponent<Rigidbody>();
            }
            Bounds caja = surface.bounds;
            Vector3 pies = jinete.position;
            if (pies.x < caja.min.x - 0.1f || pies.x > caja.max.x + 0.1f || pies.z < caja.min.z - 0.1f || pies.z > caja.max.z + 0.1f) return;
            if (pies.y < caja.max.y - 0.3f || pies.y > caja.max.y + 0.4f) return;
            if (_cuerpoDelJinete != null) _cuerpoDelJinete.position += movido;
            else jinete.position += movido;
        }
    }
}
