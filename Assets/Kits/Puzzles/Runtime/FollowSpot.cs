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

        private void LateUpdate()
        {
            var donde = Target != null ? Target() : null;
            if (!donde.HasValue) return;
            Vector3 objetivo = donde.Value + Vector3.up * aimHeight;
            float k = _hay ? 1f - Mathf.Exp(-Time.deltaTime / lag) : 1f;
            _apunta = Vector3.Lerp(_apunta, objetivo, k);
            _hay = true;
            transform.position = _apunta - Vector3.up * aimHeight + offset;
            transform.rotation = Quaternion.LookRotation(_apunta - transform.position, Vector3.up);
        }
    }
}
