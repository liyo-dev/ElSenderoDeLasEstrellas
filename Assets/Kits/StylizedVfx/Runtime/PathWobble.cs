using UnityEngine;

namespace Kits.StylizedVfx
{
    /// Forma del vaivén de una pieza que viaja.
    public enum WobbleMode
    {
        None = 0,
        /// De lado a lado en línea quebrada.
        Zigzag = 1,
        /// De lado a lado en curva suave.
        Serpentine = 2,
        /// A botes hacia arriba.
        Hop = 3,
        /// En círculo alrededor de la línea de la marcha (temblor si la amplitud es pequeña).
        Corkscrew = 4,
    }

    /// Mueve su pieza alrededor de la posición con la que se montó, en el plano perpendicular a la
    /// marcha (el eje Z del padre): el proyectil sigue recto y lo que se ve va en zigzag, a botes o en
    /// espiral. Las estelas en espacio de mundo dibujan el recorrido. El vaivén crece desde cero al
    /// encenderse, así que siempre sale de la mano.
    [DisallowMultipleComponent]
    public class PathWobble : MonoBehaviour
    {
        [Tooltip("Forma del vaivén.")]
        [SerializeField] private WobbleMode mode = WobbleMode.Serpentine;

        [Tooltip("Metros que se aparta como mucho de la línea de la marcha.")]
        [SerializeField, Min(0f)] private float amplitude = 0.25f;

        [Tooltip("Vaivenes por segundo.")]
        [SerializeField, Min(0f)] private float frequency = 3f;

        [Tooltip("Segundos que tarda en llegar a toda la amplitud.")]
        [SerializeField, Min(0f)] private float rampSeconds = 0.15f;

        [Tooltip("Empezar cada vez en un punto distinto del vaivén (varias copias a la vez no van a la par).")]
        [SerializeField] private bool randomPhase;

        [Tooltip("Usar tiempo sin escalar.")]
        [SerializeField] private bool unscaledTime;

        private Vector3 _base;
        private bool _hasBase;
        private float _time;
        private float _phase;

        public void Configure(WobbleMode wobbleMode, float metres, float perSecond, bool randomStart)
        {
            mode = wobbleMode;
            amplitude = metres;
            frequency = perSecond;
            randomPhase = randomStart;
        }

        private void Awake()
        {
            _base = transform.localPosition;
            _hasBase = true;
        }

        private void OnEnable()
        {
            if (!_hasBase) { _base = transform.localPosition; _hasBase = true; }
            _time = 0f;
            _phase = randomPhase ? Random.value * Mathf.PI * 2f : 0f;
            transform.localPosition = _base;
        }

        private void OnDisable()
        {
            if (_hasBase) transform.localPosition = _base;
        }

        private void Update()
        {
            if (mode == WobbleMode.None || amplitude <= 0f) return;
            _time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float a = amplitude * (rampSeconds > 0f ? Mathf.Clamp01(_time / rampSeconds) : 1f);
            float w = _time * frequency * Mathf.PI * 2f + _phase;
            Vector3 offset;
            switch (mode)
            {
                case WobbleMode.Zigzag:
                    offset = new Vector3(a * (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(w)), 0f, 0f);
                    break;
                case WobbleMode.Serpentine:
                    offset = new Vector3(a * Mathf.Sin(w), 0f, 0f);
                    break;
                case WobbleMode.Hop:
                    offset = new Vector3(0f, a * Mathf.Abs(Mathf.Sin(w * 0.5f)), 0f);
                    break;
                default:
                    offset = new Vector3(a * Mathf.Cos(w), a * Mathf.Sin(w), 0f);
                    break;
            }
            transform.localPosition = _base + offset;
        }
    }
}
