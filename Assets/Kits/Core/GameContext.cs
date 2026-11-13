using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kits.Core
{
    /// <summary>
    /// Registro único de las situaciones de juego que están en curso (combate, cinemática, menú…).
    ///
    /// Cada situación es un texto que pone el juego; el kit no sabe cuáles existen. Quien es dueño
    /// de una situación la abre con <see cref="Enter"/> y la cierra con <see cref="Exit"/>, siempre
    /// con su propia clave (como se pide el tiempo o la cámara a sus árbitros): la situación dura
    /// mientras quede al menos una clave, así que dos combates a la vez no se pisan al acabar uno.
    ///
    /// Los demás sistemas no preguntan a cada dueño por separado: consultan <see cref="Has"/> o
    /// escuchan <see cref="Changed"/>. Qué hace cada sistema ante cada situación (callarse,
    /// esperar, seguir) lo decide el propio sistema con datos, no este registro.
    ///
    /// Una clave que es un <see cref="UnityEngine.Object"/> destruido se descarta sola, para que
    /// una escena que se descarga no deje una situación abierta para siempre.
    /// </summary>
    public static class GameContext
    {
        static readonly Dictionary<string, HashSet<object>> s_owners = new();
        static readonly List<object> s_dead = new();

        /// <summary>Una situación se abre (true) o se cierra del todo (false).</summary>
        public static event Action<string, bool> Changed;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_owners.Clear();
            s_dead.Clear();
            Changed = null;
        }
#endif

        /// <summary>True mientras alguna clave tenga abierta la situación.</summary>
        public static bool Has(string situation)
        {
            if (string.IsNullOrEmpty(situation) || !s_owners.TryGetValue(situation, out var owners) || owners.Count == 0)
                return false;

            DiscardDead(situation, owners);
            return owners.Count > 0;
        }

        /// <summary>La clave <paramref name="owner"/> abre la situación. Repetirla no hace nada.</summary>
        public static void Enter(string situation, object owner)
        {
            if (string.IsNullOrEmpty(situation) || owner == null) return;

            if (!s_owners.TryGetValue(situation, out var owners))
            {
                owners = new HashSet<object>();
                s_owners[situation] = owners;
            }

            bool wasClosed = owners.Count == 0;
            if (owners.Add(owner) && wasClosed) Changed?.Invoke(situation, true);
        }

        /// <summary>La clave <paramref name="owner"/> deja de tener abierta la situación. Llamarlo de más no hace nada.</summary>
        public static void Exit(string situation, object owner)
        {
            if (string.IsNullOrEmpty(situation) || owner == null) return;
            if (!s_owners.TryGetValue(situation, out var owners) || !owners.Remove(owner)) return;
            if (owners.Count == 0) Changed?.Invoke(situation, false);
        }

        static void DiscardDead(string situation, HashSet<object> owners)
        {
            s_dead.Clear();
            foreach (var owner in owners)
                if (owner is UnityEngine.Object o && o == null) s_dead.Add(owner);

            if (s_dead.Count == 0) return;

            for (int i = 0; i < s_dead.Count; i++) owners.Remove(s_dead[i]);
            s_dead.Clear();
            if (owners.Count == 0) Changed?.Invoke(situation, false);
        }
    }
}
