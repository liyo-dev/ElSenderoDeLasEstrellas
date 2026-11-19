using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Kits.WorldWrap
{
    /// <summary>
    /// Mundo que da la vuelta: un rectángulo de lado <see cref="Size"/> alrededor de
    /// <see cref="Center"/> con los bordes cosidos (este↔oeste y norte↔sur). Cuando lo que vigila
    /// (<see cref="TargetProvider"/>, normalmente el jugador) pasa de un borde, mueve un lado entero
    /// hacia el contrario lo que lo lleva (<see cref="MoverResolver"/>: el propio objetivo o, por
    /// ejemplo, el barco en el que va) y lo que le acompaña:
    /// <list type="bullet">
    /// <item>lo que diga <see cref="PassengersProvider"/> (p. ej. el grupo que le sigue);</item>
    /// <item>los <see cref="WorldWrapPassenger"/> de la escena (siempre o si están cerca, según cada uno);</item>
    /// <item>los cuerpos con Rigidbody a menos de <c>carryRadius</c> (proyectiles, barcos, objetos que flotan).</item>
    /// </list>
    /// Cada uno se mueve con su Rigidbody, su NavMeshAgent, sus estelas y sus partículas en espacio
    /// mundo; después avisa con <see cref="ObjectShifted"/> (uno por objeto movido) y
    /// <see cref="Shifted"/> (una vez) para lo demás que deba acompañarle (cámaras, nubes…).
    /// Lleva la cuenta de lo desplazado en <see cref="TotalShift"/> y la publica como vector global de
    /// shader, para que un agua o un cielo dibujados en coordenadas del mundo no salten al cruzar.
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

        [Tooltip("Lo más que puede avanzar el objetivo entre dos pasos de física (m) para contar como que cruza el borde. Un salto mayor (colocarlo en otra escena o en un escenario de cinemática fuera del mundo) no se cose.")]
        [SerializeField, Min(1f)] private float maxStepAcrossEdge = 200f;

        [Tooltip("Radio alrededor del objetivo (m) en el que los cuerpos con Rigidbody cruzan con él (proyectiles, barcos, cosas que flotan). 0 = ninguno.")]
        [SerializeField, Min(0f)] private float carryRadius = 60f;

        [Tooltip("Capas de los cuerpos que cruzan con el objetivo.")]
        [SerializeField] private LayerMask carryLayers = ~0;

        [Tooltip("Nombre del vector global de shader con el desplazamiento acumulado (xyz). Un shader en coordenadas del mundo usa (posición − este vector) para que su dibujo siga igual al cruzar. Vacío = no se publica.")]
        [SerializeField] private string shaderShiftProperty = "_WorldWrapShift";

        /// <summary>Lo que se vigila (normalmente el jugador). Lo pone quien usa el kit.</summary>
        public Func<Transform> TargetProvider;

        /// <summary>Qué se mueve cuando el objetivo cruza (el objetivo o lo que lo lleva). Sin él, el objetivo.</summary>
        public Func<Transform, Transform> MoverResolver;

        /// <summary>
        /// Añade a la lista lo que debe cruzar con el objetivo además de lo que lo lleva (p. ej. los
        /// compañeros que le siguen). Recibe el objetivo antes de moverse.
        /// </summary>
        public Action<Transform, List<Transform>> PassengersProvider;

        /// <summary>El mundo cíclico activo de las escenas cargadas.</summary>
        public static WorldWrapper Current { get; private set; }

        /// <summary>El mundo se ha desplazado este vector para quien acompaña al objetivo.</summary>
        public static event Action<Vector3> Shifted;

        /// <summary>Un objeto concreto ha cruzado con este desplazamiento (para avisar a cámaras que lo siguen).</summary>
        public static event Action<Transform, Vector3> ObjectShifted;

        /// <summary>Suma de todos los desplazamientos desde que se activó el mundo.</summary>
        public static Vector3 TotalShift { get; private set; }

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

        public float CarryRadius
        {
            get => carryRadius;
            set => carryRadius = Mathf.Max(0f, value);
        }

        private const int MaxColliders = 128;
        private static readonly Collider[] s_colliders = new Collider[MaxColliders];
        private static readonly List<Transform> s_candidates = new List<Transform>(32);
        private static readonly HashSet<Transform> s_candidateSet = new HashSet<Transform>();
        private static readonly List<Transform> s_moved = new List<Transform>(32);
        private static readonly List<Rigidbody> s_bodies = new List<Rigidbody>(16);
        private static readonly List<TrailRenderer> s_trails = new List<TrailRenderer>(8);
        private static readonly List<ParticleSystem> s_particleSystems = new List<ParticleSystem>(16);
        private static readonly List<(Rigidbody body, RigidbodyInterpolation mode)> s_interpolationToRestore =
            new List<(Rigidbody, RigidbodyInterpolation)>(8);
        private static Vector3[] s_points = new Vector3[0];
        private static ParticleSystem.Particle[] s_particles = new ParticleSystem.Particle[256];

        private int _shaderShiftId = -1;
        private Transform _lastTarget;
        private Vector3 _lastPosition;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Shifted = null;
            ObjectShifted = null;
            TotalShift = Vector3.zero;
            s_interpolationToRestore.Clear();
        }
#endif

        private void OnEnable()
        {
            Current = this;
            TotalShift = Vector3.zero;
            _shaderShiftId = string.IsNullOrEmpty(shaderShiftProperty) ? -1 : Shader.PropertyToID(shaderShiftProperty);
            PublishShift();
        }

        private void OnDisable()
        {
            RestoreInterpolation();
            if (Current != this) return;
            Current = null;
            TotalShift = Vector3.zero;
            PublishShift();
        }

        private void FixedUpdate()
        {
            RestoreInterpolation();

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

        /// <summary>Distancia en X y Z de <paramref name="position"/> al borde más cercano (negativa si está fuera).</summary>
        public float DistanceToEdge(Vector3 position)
        {
            float dx = size.x * 0.5f - Mathf.Abs(position.x - center.x);
            float dz = size.y * 0.5f - Mathf.Abs(position.z - center.y);
            return Mathf.Min(dx, dz);
        }

        private void Shift(Transform target, Vector3 delta)
        {
            Transform mover = MoverResolver != null ? MoverResolver(target) : null;
            if (mover == null) mover = target;

            CollectCandidates(target, mover);

            // Un hijo de algo que ya cruza viaja con su padre: moverlo otra vez lo dejaría el doble de lejos.
            s_moved.Clear();
            for (int i = 0; i < s_candidates.Count; i++)
            {
                Transform t = s_candidates[i];
                if (!HasCandidateAncestor(t)) s_moved.Add(t);
            }
            for (int i = 0; i < s_moved.Count; i++)
            {
                Move(s_moved[i], delta);
                ShiftAttachedEffects(s_moved[i], delta);
            }
            Physics.SyncTransforms();

            TotalShift += delta;
            PublishShift();

            for (int i = 0; i < s_moved.Count; i++)
                ObjectShifted?.Invoke(s_moved[i], delta);
            Shifted?.Invoke(delta);

            s_candidates.Clear();
            s_candidateSet.Clear();
            s_moved.Clear();
        }

        private void CollectCandidates(Transform target, Transform mover)
        {
            s_candidates.Clear();
            s_candidateSet.Clear();
            AddCandidate(mover);
            if (mover != target && !target.IsChildOf(mover)) AddCandidate(target);

            Vector3 origin = target.position;
            int before = s_candidates.Count;
            PassengersProvider?.Invoke(target, s_candidates);
            // Lo que ha añadido el proveedor pasa por el mismo filtro que lo demás.
            for (int i = s_candidates.Count - 1; i >= before; i--)
            {
                Transform t = s_candidates[i];
                s_candidates.RemoveAt(i);
                AddCandidate(t);
            }

            var passengers = WorldWrapPassenger.All;
            for (int i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                if (p == null) continue;
                if (p.AlwaysCarry || (p.transform.position - origin).sqrMagnitude <= p.Radius * p.Radius)
                    AddCandidate(p.transform);
            }

            if (carryRadius <= 0f) return;
            int n = Physics.OverlapSphereNonAlloc(origin, carryRadius, s_colliders, carryLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                Rigidbody body = s_colliders[i].attachedRigidbody;
                if (body != null) AddCandidate(body.transform);
                s_colliders[i] = null;
            }
        }

        private static void AddCandidate(Transform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy || t.gameObject.isStatic) return;
            if (s_candidateSet.Add(t)) s_candidates.Add(t);
        }

        private static bool HasCandidateAncestor(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (s_candidateSet.Contains(p)) return true;
            return false;
        }

        private void PublishShift()
        {
            if (_shaderShiftId != -1) Shader.SetGlobalVector(_shaderShiftId, TotalShift);
        }

        /// <summary>
        /// Mueve un objeto sin arrastrar nada por el camino: su Rigidbody (y los de sus hijos, sin la
        /// interpolación de un paso que lo pintaría a medio camino) y su NavMeshAgent.
        /// </summary>
        public static void Move(Transform t, Vector3 delta)
        {
            Vector3 destination = t.position + delta;
            if (t.TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh)
                agent.Warp(destination);

            t.GetComponentsInChildren(s_bodies);
            for (int i = 0; i < s_bodies.Count; i++)
            {
                Rigidbody body = s_bodies[i];
                if (body.interpolation != RigidbodyInterpolation.None)
                {
                    s_interpolationToRestore.Add((body, body.interpolation));
                    body.interpolation = RigidbodyInterpolation.None;
                }
                if (body.transform == t) body.position = destination;
            }
            s_bodies.Clear();
            t.position = destination;
        }

        /// <summary>Devuelve la interpolación a los cuerpos movidos en el paso de física anterior.</summary>
        private static void RestoreInterpolation()
        {
            for (int i = 0; i < s_interpolationToRestore.Count; i++)
            {
                var (body, mode) = s_interpolationToRestore[i];
                if (body != null) body.interpolation = mode;
            }
            s_interpolationToRestore.Clear();
        }

        /// <summary>
        /// Corre las estelas y las partículas en espacio mundo de <paramref name="root"/> y sus hijos,
        /// para que no dejen un rastro de un lado del mundo al otro.
        /// </summary>
        public static void ShiftAttachedEffects(Transform root, Vector3 delta)
        {
            root.GetComponentsInChildren(s_trails);
            for (int t = 0; t < s_trails.Count; t++)
            {
                TrailRenderer trail = s_trails[t];
                int n = trail.positionCount;
                if (n == 0) continue;
                // SetPositions toma el array entero: tiene que medir justo lo que la estela.
                if (s_points.Length != n) s_points = new Vector3[n];
                n = trail.GetPositions(s_points);
                for (int i = 0; i < n; i++) s_points[i] += delta;
                trail.SetPositions(s_points);
            }
            s_trails.Clear();

            root.GetComponentsInChildren(s_particleSystems);
            for (int p = 0; p < s_particleSystems.Count; p++)
            {
                ParticleSystem ps = s_particleSystems[p];
                if (ps.main.simulationSpace != ParticleSystemSimulationSpace.World) continue;
                int count = ps.particleCount;
                if (count == 0) continue;
                if (s_particles.Length < count) s_particles = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];
                int n = ps.GetParticles(s_particles, count);
                for (int i = 0; i < n; i++) s_particles[i].position += delta;
                ps.SetParticles(s_particles, n);
            }
            s_particleSystems.Clear();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float y = transform.position.y;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(center.x, y, center.y), new Vector3(size.x, 0f, size.y));
        }
#endif
    }
}
