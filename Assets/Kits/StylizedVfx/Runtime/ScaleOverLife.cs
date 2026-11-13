using UnityEngine;

namespace Kits.StylizedVfx
{
    /// Escala su propia pieza con una curva desde que se enciende: destellos que saltan y se van,
    /// ondas que se abren, columnas que crecen hacia arriba, pinchos que salen del suelo. Multiplica
    /// la escala con la que se montó; cada eje se puede dejar quieto. Va en un hijo, no en la raíz
    /// del efecto, para no pelearse con quien escale el efecto entero.
    [DisallowMultipleComponent]
    public class ScaleOverLife : MonoBehaviour
    {
        [Tooltip("Multiplicador de la escala según el tiempo (eje X de 0 a «duration»).")]
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Segundos que dura la curva. Pasado ese tiempo se queda en su último valor.")]
        [SerializeField] private float duration = 0.4f;

        [Tooltip("Segundos de espera antes de empezar (la pieza se queda en el primer valor).")]
        [SerializeField] private float delay;

        [Tooltip("Ejes que siguen la curva (1) y ejes que se quedan como se montaron (0).")]
        [SerializeField] private Vector3 axes = Vector3.one;

        [Tooltip("Usar tiempo sin escalar.")]
        [SerializeField] private bool unscaledTime = true;

        private Vector3 _baseScale;
        private float _time;
        private bool _done;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _time = 0f;
            _done = false;
            if (Application.isPlaying) Apply(0f);
        }

        private void Update()
        {
            if (_done) return;
            _time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Max(0f, _time - delay);
            if (t >= duration) _done = true;
            Apply(Mathf.Min(t, duration));
        }

        private void Apply(float t)
        {
            float k = curve.Evaluate(duration > 0f ? t / duration : 1f);
            transform.localScale = new Vector3(
                _baseScale.x * Mathf.Lerp(1f, k, axes.x),
                _baseScale.y * Mathf.Lerp(1f, k, axes.y),
                _baseScale.z * Mathf.Lerp(1f, k, axes.z));
        }

        /// Monta la pieza desde código (constructores de editor). La curva va de 0 a 1 en el tiempo.
        public void Configure(AnimationCurve scaleCurve, float seconds, float waitSeconds, Vector3 scaledAxes)
        {
            curve = scaleCurve;
            duration = seconds;
            delay = waitSeconds;
            axes = scaledAxes;
        }
    }
}
