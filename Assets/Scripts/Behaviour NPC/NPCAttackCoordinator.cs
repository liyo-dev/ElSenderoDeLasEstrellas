using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Turnos de ataque sobre un mismo objetivo: limita cuántos NPCs pueden estar atacándolo a la
    /// vez, para que un grupo no descargue todo en el mismo instante. Las reservas caducan solas (no
    /// dependen de que cada NPC libere en todos sus caminos de salida). Lo usan el cerebro clásico y
    /// el de puntuación.
    /// </summary>
    public static class NPCAttackCoordinator
    {
        private class Reservation
        {
            public Object owner;
            public float expiresAt;
        }

        private static readonly Dictionary<Transform, List<Reservation>> _reservationsByTarget = new();

        // Cubre preparación + disparo + pausa tras atacar, con margen. Quien ataca más rato renueva.
        private const float ReservationLifetime = 1.5f;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _reservationsByTarget.Clear();
#endif

        /// <summary>
        /// Intenta reservar un turno de ataque sobre <paramref name="target"/>. Devuelve true si hay
        /// hueco (o si ya tenía reserva, que se renueva) y false si ya hay maxConcurrent atacándolo.
        /// </summary>
        public static bool TryReserve(Transform target, Object requester, int maxConcurrent)
        {
            if (target == null) return true;

            if (!_reservationsByTarget.TryGetValue(target, out var list))
            {
                list = new List<Reservation>();
                _reservationsByTarget[target] = list;
            }

            Purge(list);

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].owner != requester) continue;
                list[i].expiresAt = Time.time + ReservationLifetime;
                return true;
            }

            if (list.Count >= Mathf.Max(1, maxConcurrent))
                return false;

            list.Add(new Reservation { owner = requester, expiresAt = Time.time + ReservationLifetime });
            return true;
        }

        /// <summary>¿Tiene ahora mismo ese NPC un turno sobre ese objetivo?</summary>
        public static bool HasTurn(Transform target, Object requester)
        {
            if (target == null || !_reservationsByTarget.TryGetValue(target, out var list)) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].owner == requester && Time.time < list[i].expiresAt) return true;
            return false;
        }

        /// <summary>Cuántos tienen turno ahora mismo sobre ese objetivo.</summary>
        public static int TurnCount(Transform target)
        {
            if (target == null || !_reservationsByTarget.TryGetValue(target, out var list)) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].owner != null && Time.time < list[i].expiresAt) n++;
            return n;
        }

        /// <summary>¿Hay alguien con turno sobre ese objetivo? (para saber quién presiona).</summary>
        public static Object TurnHolder(Transform target)
        {
            if (target == null || !_reservationsByTarget.TryGetValue(target, out var list)) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i].owner != null && Time.time < list[i].expiresAt) return list[i].owner;
            return null;
        }

        /// <summary>Libera la reserva antes de que caduque (para que el siguiente no espere).</summary>
        public static void Release(Transform target, Object requester)
        {
            if (target == null || !_reservationsByTarget.TryGetValue(target, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].owner == requester) list.RemoveAt(i);
        }

        private static void Purge(List<Reservation> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].owner == null || Time.time >= list[i].expiresAt) list.RemoveAt(i);
        }
    }
}
