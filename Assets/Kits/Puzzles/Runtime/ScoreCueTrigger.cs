using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Kits.Puzzles
{
    /// <summary>
    /// Avisa de las marcas de una partitura: un poco antes (el aviso, para que se vea venir) y en
    /// el golpe. Sirve para cualquier cosa que reacciona a la música sin saber de ella: una boca que
    /// se abre, una puerta, un sonido. Lee el segundo de la canción del reloj, así que al reintentar
    /// (la canción vuelve atrás) se recoloca solo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreCueTrigger : MonoBehaviour
    {
        [Tooltip("Reloj que sigue la canción. Vacío: el de la escena.")]
        [SerializeField] private TempoClock clock;
        [Tooltip("Voces de la partitura a las que reacciona. Vacío: a todas.")]
        [SerializeField] private string[] voices = Array.Empty<string>();
        [Tooltip("Solo las marcas desde este segundo de la canción.")]
        [SerializeField, Min(0f)] private float fromSeconds;
        [Tooltip("Solo las marcas hasta este segundo de la canción.")]
        [SerializeField, Min(0f)] private float toSeconds = 100000f;
        [Tooltip("Segundos de aviso antes del golpe.")]
        [SerializeField, Min(0f)] private float warningSeconds = 0.2f;

        [Tooltip("Un poco antes del golpe.")]
        public UnityEvent onWarning = new UnityEvent();
        [Tooltip("En el golpe.")]
        public UnityEvent onCue = new UnityEvent();

        /// <summary>Aviso de una marca: (número de la marca en este oyente, segundo del golpe).</summary>
        public event Action<int, float> Warned;
        /// <summary>Golpe de una marca: (número de la marca en este oyente, segundo del golpe).</summary>
        public event Action<int, float> Hit;

        private readonly List<float> _tiempos = new List<float>(32);
        private int _siguienteAviso, _siguienteGolpe;
        private double _ultimoSegundo = -1d;

        public IReadOnlyList<float> CueTimes => _tiempos;

        private void OnEnable()
        {
            if (clock == null) clock = TempoClock.Current;
            Rebuild();
        }

        /// <summary>Vuelve a leer las marcas de la partitura (si se cambia el reloj o la ventana).</summary>
        public void Rebuild()
        {
            _tiempos.Clear();
            if (clock != null && clock.Score != null)
                clock.Score.CollectCueTimes(voices, fromSeconds, toSeconds, _tiempos);
            _ultimoSegundo = -1d;
            _siguienteAviso = _siguienteGolpe = 0;
        }

        private void Update()
        {
            if (clock == null || !clock.IsRunning || !clock.FollowsSong) return;
            double t = clock.SongTime;
            if (t < _ultimoSegundo - 0.05d || _ultimoSegundo < 0d) Recolocar(t);
            _ultimoSegundo = t;

            while (_siguienteAviso < _tiempos.Count && t >= _tiempos[_siguienteAviso] - warningSeconds)
            {
                onWarning.Invoke();
                Warned?.Invoke(_siguienteAviso, _tiempos[_siguienteAviso]);
                _siguienteAviso++;
            }
            while (_siguienteGolpe < _tiempos.Count && t >= _tiempos[_siguienteGolpe])
            {
                onCue.Invoke();
                Hit?.Invoke(_siguienteGolpe, _tiempos[_siguienteGolpe]);
                _siguienteGolpe++;
            }
        }

        /// <summary>Tras un salto de la canción, lo ya pasado no se dispara.</summary>
        private void Recolocar(double t)
        {
            _siguienteAviso = 0;
            while (_siguienteAviso < _tiempos.Count && _tiempos[_siguienteAviso] - warningSeconds < t) _siguienteAviso++;
            _siguienteGolpe = 0;
            while (_siguienteGolpe < _tiempos.Count && _tiempos[_siguienteGolpe] < t) _siguienteGolpe++;
        }
    }
}
