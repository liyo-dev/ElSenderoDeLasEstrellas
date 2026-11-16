using System;
using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Zona que pone la cámara de lado y sujeta el movimiento a un plano: el tramo se juega de
    /// izquierda a derecha, como un pasillo en dos dimensiones. El kit solo dice dónde está el plano
    /// y quién entra; la cámara y la sujeción las pone el juego con <see cref="ResolvePlayerRoot"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SidePlaneZone : MonoBehaviour
    {
        /// <summary>¿Este collider es del jugador? Devuelve su raíz, o null.</summary>
        public static Func<Collider, Transform> ResolvePlayerRoot;

        /// <summary>El jugador acaba de entrar en el tramo lateral.</summary>
        public static event Action<SidePlaneZone> ZoneEntered;
        /// <summary>El jugador ha salido del tramo lateral.</summary>
        public static event Action<SidePlaneZone> ZoneExited;

        [Header("El plano")]
        [Tooltip("Eje que se bloquea: el jugador se mueve en el plano perpendicular a este.")]
        [SerializeField] private Vector3 planeNormal = Vector3.forward;

        [Header("Cámara")]
        [Tooltip("Distancia de la cámara al jugador, por delante de la normal.")]
        [SerializeField, Min(2f)] private float cameraDistance = 16f;
        [Tooltip("Altura de la cámara sobre los pies del jugador.")]
        [SerializeField, Min(0f)] private float cameraHeight = 3.5f;
        [Tooltip("Cuánto se adelanta la cámara en la dirección de avance (m).")]
        [SerializeField, Min(0f)] private float lookAhead = 4f;
        [Tooltip("Velocidad con la que la cámara alcanza su sitio (1/s).")]
        [SerializeField, Min(0.1f)] private float cameraFollow = 8f;

        public Vector3 PlaneNormal => planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.forward;
        public Vector3 PlanePoint => transform.position;
        public float CameraDistance => cameraDistance;
        public float CameraHeight => cameraHeight;
        public float LookAhead => lookAhead;
        public float CameraFollow => cameraFollow;

        private void Awake()
        {
            var zona = GetComponent<Collider>();
            if (zona != null) zona.isTrigger = true;
        }

        private void OnTriggerEnter(Collider otro)
        {
            if (ResolvePlayerRoot == null || ResolvePlayerRoot(otro) == null) return;
            ZoneEntered?.Invoke(this);
        }

        private void OnTriggerExit(Collider otro)
        {
            if (ResolvePlayerRoot == null || ResolvePlayerRoot(otro) == null) return;
            ZoneExited?.Invoke(this);
        }

        private void OnDisable() => ZoneExited?.Invoke(this);

        /// <summary>Lleva un punto til punto al plano del tramo (anula lo que se salga por la normal).</summary>
        public Vector3 EnElPlano(Vector3 punto)
        {
            Vector3 normal = PlaneNormal;
            return punto - normal * Vector3.Dot(punto - PlanePoint, normal);
        }

        /// <summary>Cuánto se ha salido del plano (m).</summary>
        public float FueraDelPlano(Vector3 punto)
            => Mathf.Abs(Vector3.Dot(punto - PlanePoint, PlaneNormal));

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ResolvePlayerRoot = null;
            ZoneEntered = null;
            ZoneExited = null;
        }
#endif
    }
}
