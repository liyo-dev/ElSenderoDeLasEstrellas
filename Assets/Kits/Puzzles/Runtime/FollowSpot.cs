using System;
using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// El cañón de seguimiento de un teatro: una luz de foco alta que sigue a quien va al mando (su
    /// posición la da el juego con <see cref="Target"/>), con un poco de retraso, como si la moviera un
    /// técnico desde la cabina. Sin objetivo, se queda donde está.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class FollowSpot : MonoBehaviour
    {
        /// <summary>Dónde está a quien sigue (los pies). Lo pone el juego; null si no hay nadie.</summary>
        public static Func<Vector3?> Target;

        [Tooltip("Desde dónde alumbra, respecto a quien sigue (alto y por detrás).")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -7f);
        [Tooltip("Altura sobre los pies a la que apunta.")]
        [SerializeField, Min(0f)] private float aimHeight = 1f;
        [Tooltip("Segundos que tarda en alcanzar a quien sigue.")]
        [SerializeField, Min(0.01f)] private float lag = 0.12f;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Target = null;
#endif

        private Vector3 _apunta;
        private bool _hay;
        private Transform _soporte;
        private Light _luz;
        private Vector3? _marca;
        private float _radio;
        private float _anguloOriginal, _anguloInteriorOriginal, _intensityOriginal;
        private static readonly Quaternion Cenital = Quaternion.Euler(90f, 0f, 0f);

        private void Awake()
        {
            _soporte = transform;
            _luz = GetComponent<Light>();
            _anguloOriginal = _luz.spotAngle;
            _anguloInteriorOriginal = _luz.innerSpotAngle;
            _intensityOriginal = _luz.intensity;
        }

        /// <summary>Atenúa o aviva el cañón sin perder su intensidad de montaje. 1 = tal cual está en la escena.</summary>
        public void SetBrightness(float multiplier)
        {
            _luz.intensity = _intensityOriginal * Mathf.Max(0f, multiplier);
        }

        /// <summary>Ilumina una marca de escenario desde arriba. Sin marca vuelve a seguir al intérprete.</summary>
        public void SetStageMark(Vector3? mark, float radius = 0f)
        {
            if (_marca.HasValue != mark.HasValue || !Mathf.Approximately(_radio, radius))
            {
                float altura = Mathf.Max(1f, offset.y);
                float angulo = 2f * Mathf.Atan(radius / altura) * Mathf.Rad2Deg;
                _luz.spotAngle = mark.HasValue ? angulo : _anguloOriginal;
                _luz.innerSpotAngle = mark.HasValue ? angulo * 0.74f : _anguloInteriorOriginal;
                _radio = radius;
                _hay = false;
            }
            _marca = mark;
        }

        private void LateUpdate()
        {
            if (_marca.HasValue)
            {
                _soporte.SetPositionAndRotation(_marca.Value + Vector3.up * Mathf.Max(1f, offset.y), Cenital);
                return;
            }
            var donde = Target != null ? Target() : null;
            if (!donde.HasValue) return;
            Vector3 objetivo = donde.Value + Vector3.up * aimHeight;
            float k = _hay ? 1f - Mathf.Exp(-Time.deltaTime / lag) : 1f;
            _apunta = Vector3.Lerp(_apunta, objetivo, k);
            _hay = true;
            Vector3 posicion = _apunta - Vector3.up * aimHeight + offset;
            _soporte.SetPositionAndRotation(posicion, Quaternion.LookRotation(_apunta - posicion, Vector3.up));
        }
    }
}
