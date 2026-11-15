using UnityEngine;

namespace Kits.WorldWrap
{
    /// <summary>
    /// Techo de altura de una escena: hasta dónde se puede subir volando. No es un muro: en la franja
    /// suave de debajo del techo la subida se va frenando, y por encima del techo se empuja hacia
    /// abajo. Quien mueve al personaje en vertical pregunta a <see cref="Current"/> con
    /// <see cref="Limit"/>; sin techo en la escena, no hay límite.
    /// </summary>
    [DisallowMultipleComponent]
    public class AltitudeCeiling : MonoBehaviour
    {
        [Tooltip("Altura del techo en coordenadas del mundo (m).")]
        [SerializeField] private float height = 300f;

        [Tooltip("Franja bajo el techo donde la subida se va frenando hasta pararse (m).")]
        [SerializeField, Min(0.1f)] private float softBand = 30f;

        [Tooltip("Velocidad de bajada por cada metro que se está por encima del techo (1/s).")]
        [SerializeField, Min(0f)] private float pushBack = 1.5f;

        /// <summary>El techo activo de las escenas cargadas (el último que se ha activado).</summary>
        public static AltitudeCeiling Current { get; private set; }

        public float Height
        {
            get => height;
            set => height = value;
        }

        public float SoftBand
        {
            get => softBand;
            set => softBand = Mathf.Max(0.1f, value);
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; }
#endif

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// Velocidad vertical permitida a la altura <paramref name="y"/> para la pedida
        /// <paramref name="verticalSpeed"/>: frena la subida al acercarse al techo y, por encima,
        /// devuelve como mucho una bajada proporcional a lo que se ha pasado.
        /// </summary>
        public float Limit(float y, float verticalSpeed)
        {
            float margin = height - y;
            if (margin < 0f) return Mathf.Min(verticalSpeed, margin * pushBack);
            if (verticalSpeed <= 0f) return verticalSpeed;
            return verticalSpeed * Mathf.Clamp01(margin / softBand);
        }
    }
}
