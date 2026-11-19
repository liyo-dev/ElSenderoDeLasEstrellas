using System.Collections.Generic;
using UnityEngine;

namespace Kits.WorldWrap
{
    /// <summary>
    /// Marca algo que cruza la costura del <see cref="WorldWrapper"/> junto con lo que este vigila:
    /// siempre (<see cref="AlwaysCarry"/>, para lo que va pegado a él aunque esté lejos, como un
    /// efecto que le sigue) o solo si está a menos de <see cref="Radius"/> en el momento de cruzar
    /// (acompañantes sin Rigidbody, efectos sueltos cerca). Se mueve con su Rigidbody, su
    /// NavMeshAgent, sus estelas y sus partículas en espacio mundo.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldWrapPassenger : MonoBehaviour
    {
        [Tooltip("Cruza siempre con el objetivo, esté donde esté.")]
        [SerializeField] private bool alwaysCarry;

        [Tooltip("Si no cruza siempre: distancia al objetivo (m) por debajo de la cual cruza con él.")]
        [SerializeField, Min(0f)] private float radius = 150f;

        private static readonly List<WorldWrapPassenger> s_all = new List<WorldWrapPassenger>(16);

        /// <summary>Los pasajeros activos de las escenas cargadas.</summary>
        public static IReadOnlyList<WorldWrapPassenger> All => s_all;

        public bool AlwaysCarry
        {
            get => alwaysCarry;
            set => alwaysCarry = value;
        }

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0f, value);
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_all.Clear(); }
#endif

        private void OnEnable() => s_all.Add(this);

        private void OnDisable() => s_all.Remove(this);
    }
}
