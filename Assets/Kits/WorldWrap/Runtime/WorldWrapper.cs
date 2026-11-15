using System;
using UnityEngine;

namespace Kits.WorldWrap
{
    /// <summary>
    /// Mundo que da la vuelta: un rectángulo de lado <see cref="Size"/> alrededor de
    /// <see cref="Center"/> con los bordes cosidos (este↔oeste y norte↔sur). Cuando lo que vigila
    /// (<see cref="TargetProvider"/>, normalmente el jugador) pasa de un borde, mueve un lado entero
    /// hacia el contrario lo que lo lleva (<see cref="MoverResolver"/>: el propio objetivo o, por
    /// ejemplo, el barco en el que va), corre con él sus estelas y partículas en espacio mundo y
    /// avisa con <see cref="Shifted"/> para que se mueva lo demás que le acompaña.
    /// La costura tiene que quedar en mar abierto, lejos de cualquier tierra: el kit no duplica nada.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class WorldWrapper : MonoBehaviour
    {
        [Tooltip("Centro del mundo en X y Z (m).")]
        [SerializeField] private Vector2 center;

        [Tooltip("Lado del mundo en X y Z (m). Al pasar de la mitad se aparece por el lado contrario.")]
        [SerializeField] private Vector2 size = new Vector2(3000f, 3000f);

        /// <summary>Lo que se vigila (normalmente el jugador). Lo pone quien usa el kit.</summary>
        public Func<Transform> TargetProvider;

        /// <summary>Qué se mueve cuando el objetivo cruza (el objetivo o lo que lo lleva). Sin él, el objetivo.</summary>
        public Func<Transform, Transform> MoverResolver;

        /// <summary>El mundo cíclico activo de las escenas cargadas.</summary>
        public static WorldWrapper Current { get; private set; }

        /// <summary>El mundo se ha desplazado este vector para quien acompaña al objetivo.</summary>
        public static event Action<Vector3> Shifted;

        public Vector2 Center
        {
            get => center;
            set => center = value;
        }

        public Vector2 Size
        {
            get => size;
            set => size = new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y));
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Shifted = null; }
#endif

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        [Tooltip("Lo más que puede avanzar el objetivo entre dos pasos de física (m) para contar como que cruza el borde. Un salto mayor (colocarlo en otra escena o en un escenario de cinemática fuera del mundo) no se cose.")]
        [SerializeField, Min(1f)] private float maxStepAcrossEdge = 200f;

        private Transform _lastTarget;
        private Vector3 _lastPosition;

        private void FixedUpdate()
        {
            Transform target = TargetProvider?.Invoke();
            if (target == null)
            {
                _lastTarget = null;
                return;
            }
            Vector3 position = target.position;
            bool crossedByMoving = target == _lastTarget
                && IsInside(_lastPosition)
                && (position - _lastPosition).sqrMagnitude <= maxStepAcrossEdge * maxStepAcrossEdge;
            _lastTarget = target;
            _lastPosition = position;
            if (!crossedByMoving) return;
            Vector3 delta = OffsetFor(position);
            if (delta.x == 0f && delta.z == 0f) return;
            Shift(target, delta);
            _lastPosition = target.position;
        }

        /// <summary>Si <paramref name="position"/> está dentro del rectángulo del mundo.</summary>
        public bool IsInside(Vector3 position)
        {
            Vector3 offset = OffsetFor(position);
            return offset.x == 0f && offset.z == 0f;
        }

        /// <summary>Desplazamiento que lleva <paramref name="position"/> dentro del rectángulo del mundo.</summary>
        public Vector3 OffsetFor(Vector3 position)
        {
            float dx = -size.x * Mathf.Round((position.x - center.x) / size.x);
            float dz = -size.y * Mathf.Round((position.z - center.y) / size.y);
            return new Vector3(dx, 0f, dz);
        }

        /// <summary>La misma posición dentro del rectángulo del mundo (para guardar o comparar).</summary>
        public Vector3 Wrap(Vector3 position) => position + OffsetFor(position);

        private void Shift(Transform target, Vector3 delta)
        {
            Transform mover = MoverResolver != null ? MoverResolver(target) : null;
            if (mover == null) mover = target;
            Move(mover, delta);
            ShiftAttachedEffects(mover, delta);
            Physics.SyncTransforms();
            Shifted?.Invoke(delta);
        }

        /// <summary>Mueve un objeto (y su Rigidbody, si tiene) sin arrastrar nada por el camino.</summary>
        public static void Move(Transform t, Vector3 delta)
        {
            if (t.TryGetComponent(out Rigidbody rb)) rb.position += delta;
            t.position += delta;
        }

        /// <summary>
        /// Corre las estelas y las partículas en espacio mundo de <paramref name="root"/> y sus hijos,
        /// para que no dejen un rastro de un lado del mundo al otro.
        /// </summary>
        public static void ShiftAttachedEffects(Transform root, Vector3 delta)
        {
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>())
            {
                int n = trail.positionCount;
                if (n == 0) continue;
                var points = new Vector3[n];
                trail.GetPositions(points);
                for (int i = 0; i < n; i++) points[i] += delta;
                trail.SetPositions(points);
            }
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.main.simulationSpace != ParticleSystemSimulationSpace.World) continue;
                int count = ps.particleCount;
                if (count == 0) continue;
                var particles = new ParticleSystem.Particle[count];
                int n = ps.GetParticles(particles);
                for (int i = 0; i < n; i++) particles[i].position += delta;
                ps.SetParticles(particles, n);
            }
        }
    }
}
