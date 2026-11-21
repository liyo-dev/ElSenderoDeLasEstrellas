using System.Collections.Generic;
using UnityEngine;

namespace Kits.Core
{
    /// <summary>
    /// Quién usa cada capa de un Animator y con qué prioridad.
    ///
    /// Los sistemas que ponen una pose que debe mandar (llevar algo, interactuar, combatir, una
    /// cinemática) reclaman la capa con <see cref="Claim"/> y la sueltan con <see cref="Release"/>,
    /// siempre con su propia clave. Los sistemas de menos prioridad (poses de ambiente, gestos de
    /// reposo) consultan <see cref="IsHeldAbove"/> antes de tocar la capa y, si está cogida por
    /// encima de ellos, se retiran sin escribir ni su estado ni su peso; vuelven solos cuando la
    /// capa queda libre. El kit no sabe qué prioridades existen: los números los pone el juego.
    ///
    /// Una clave o un Animator destruidos se descartan solos, para que un objeto que desaparece no
    /// deje una capa cogida para siempre. Consultar no reserva memoria.
    /// </summary>
    public static class LayerClaims
    {
        struct Entry
        {
            public Animator animator;
            public int layer;
            public object owner;
            public int priority;
        }

        static readonly List<Entry> s_entries = new();

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_entries.Clear();
#endif

        /// <summary>La clave <paramref name="owner"/> coge la capa con esa prioridad (repetirla la actualiza).</summary>
        public static void Claim(Animator animator, int layer, object owner, int priority)
        {
            if (animator == null || owner == null || layer < 0) return;
            for (int i = 0; i < s_entries.Count; i++)
            {
                var e = s_entries[i];
                if (e.animator == animator && e.layer == layer && ReferenceEquals(e.owner, owner))
                {
                    e.priority = priority;
                    s_entries[i] = e;
                    return;
                }
            }
            s_entries.Add(new Entry { animator = animator, layer = layer, owner = owner, priority = priority });
        }

        /// <summary>La clave <paramref name="owner"/> suelta la capa. Llamarlo de más no hace nada.</summary>
        public static void Release(Animator animator, int layer, object owner)
        {
            if (owner == null) return;
            for (int i = s_entries.Count - 1; i >= 0; i--)
            {
                var e = s_entries[i];
                if (e.layer == layer && ReferenceEquals(e.owner, owner) && (e.animator == animator || e.animator == null))
                    s_entries.RemoveAt(i);
            }
        }

        /// <summary>La clave <paramref name="owner"/> suelta todo lo que tenga cogido, en cualquier Animator.</summary>
        public static void ReleaseAll(object owner)
        {
            if (owner == null) return;
            for (int i = s_entries.Count - 1; i >= 0; i--)
                if (ReferenceEquals(s_entries[i].owner, owner)) s_entries.RemoveAt(i);
        }

        /// <summary>Prioridad más alta con la que está cogida la capa; <c>int.MinValue</c> si nadie la tiene.</summary>
        public static int Highest(Animator animator, int layer)
        {
            int best = int.MinValue;
            if (animator == null) return best;
            for (int i = s_entries.Count - 1; i >= 0; i--)
            {
                var e = s_entries[i];
                if (e.animator == null || (e.owner is Object o && o == null))
                {
                    s_entries.RemoveAt(i);
                    continue;
                }
                if (e.animator == animator && e.layer == layer && e.priority > best) best = e.priority;
            }
            return best;
        }

        /// <summary>True si alguien tiene la capa cogida con una prioridad mayor que <paramref name="priority"/>.</summary>
        public static bool IsHeldAbove(Animator animator, int layer, int priority)
            => Highest(animator, layer) > priority;
    }
}
