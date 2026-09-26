using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC.Common
{
    public static class NavMeshAgentUtility
    {
        const int DefaultSampleAttempts = 8;

        public static bool EnsureAgentOnNavMesh(NavMeshAgent agent, Vector3 origin, float searchRadius)
        {
            if (agent == null) return false;
            if (agent.isOnNavMesh) return true;

            if (NavMesh.SamplePosition(origin, out var hit, Mathf.Max(1f, searchRadius), NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                return true;
            }
            return false;
        }

        public static bool TryGetRandomPoint(Vector3 origin, float radius, out Vector3 result, int attempts = DefaultSampleAttempts)
        {
            for (int i = 0; i < attempts; i++)
            {
                var randomPoint = origin + Random.insideUnitSphere * radius;
                if (NavMesh.SamplePosition(randomPoint, out var hit, radius, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = origin;
            return false;
        }

        /// <summary>
        /// Habilita un NavMeshAgent evitando el error de consola "Failed to create agent because
        /// there is no valid NavMesh": Unity comprueba transform.position EN EL INSTANTE en que se
        /// pone agent.enabled = true (para colocar el agente sobre la malla), antes de que el código
        /// llamante pueda hacer Warp/SetDestination. Si en ese instante el transform está fuera del
        /// NavMesh (tras un salto, una animación de muerte, o mientras el agente estuvo desactivado),
        /// el error se loggea aunque el código lo corrija justo después con Warp — por eso hay que
        /// recolocar el transform en un punto válido ANTES de habilitar, no después.
        /// Si el agente ya está habilitado, no hace nada (no-op seguro).
        /// </summary>
        /// <param name="agent">Agente a habilitar.</param>
        /// <param name="t">Transform del mismo GameObject (se recoloca si hace falta).</param>
        /// <param name="desiredPosition">Posición cerca de la cual buscar un punto válido de NavMesh (normalmente transform.position o el punto de destino/aterrizaje).</param>
        /// <param name="searchRadius">Radio de búsqueda en NavMesh.SamplePosition.</param>
        /// <returns>True si el agente quedó habilitado y sobre el NavMesh.</returns>
        public static bool SafeEnable(NavMeshAgent agent, Transform t, Vector3 desiredPosition, float searchRadius = 5f)
        {
            if (agent == null) return false;
            if (agent.enabled) return true;

            if (NavMesh.SamplePosition(desiredPosition, out var hit, Mathf.Max(1f, searchRadius), NavMesh.AllAreas))
            {
                if (t != null) t.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
                return true;
            }

            // Sin NavMesh cerca no se habilita: Unity escribiría «Failed to create agent» en
            // cada intento (y lo reintenta cada vez que se rehace el NavMesh). Se queda apagado
            // hasta que un intento posterior encuentre malla (INC-448).
            return false;
        }

        public static void SafeSetStopped(NavMeshAgent agent, bool stopped)
        {
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = stopped;
        }

        /// Velocidad mínima, en m/s, a la que va un NPC cuando anda. Por debajo, el paso del blend
        /// tree se queda tan corto que parece que no mueve las piernas. Ver INC-464.
        public const float VelocidadMinimaAndando = 2.0f;

        public static void HardStop(NavMeshAgent agent)
        {
            if (agent == null)
                return;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            // Limpiar velocidad residual. ResetPath() ya cancela el path y desiredVelocity;
            // NO llamar SetDestination aquí: en Unity 6 puede resetear isStopped internamente
            // antes de que LateUpdate lo lea, produciendo falsos warnings del safety check.
            agent.velocity = Vector3.zero;

            // FIX INC-NPCS-EN-ARBOLES (14 ago 2026): este reseteo de nextPosition solo tiene
            // sentido con el agente sobre el NavMesh (limpiar residuo de un path real). Si se
            // llama con el agente fuera de malla (p.ej. mientras SeekShelterState lo mueve a mano
            // bajo la copa de un árbol, ver NPCStateBase.BeginManualApproach), asignar aquí una
            // posición fuera de malla hace que el NavMeshAgent la reproyecte sobre el NavMesh en
            // cuanto pueda — y si algo reactiva agent.updatePosition después, arrastra el
            // transform de vuelta a ese punto proyectado (el NPC se "escupe" del árbol). No hay
            // nada que limpiar aquí si ya está fuera de malla: se deja tal cual.
            if (agent.isOnNavMesh)
                agent.nextPosition = agent.transform.position;
        }

        public static void SetDestination(NavMeshAgent agent, Vector3 destination, float stoppingDistance = -1f)
        {
            if (agent == null) return;
            if (!agent.isOnNavMesh) return;

            agent.isStopped = false;
            if (stoppingDistance >= 0f)
                agent.stoppingDistance = stoppingDistance;
            agent.SetDestination(destination);
        }

        // ── Animación de andar: UN solo criterio para todos los NPCs (INC-466) ─────────────────
        //
        // Raúl, 26 sep 2026, viendo a Oliver deslizarse sin mover las piernas junto a Eldran y
        // Will: «¿ves cómo se mueven Eldran y Will, que dan como saltitos al andar? Así debe ser
        // siempre, y si no, con la animación de andar sin saltitos, pero que ande».
        //
        // Hasta hoy cada sistema traducía la velocidad del agente a su manera (velocidad/agent.speed,
        // velocidad/una referencia suavizada, velocidad/referencia al 25 %, dos tramos con la
        // velocidad de andar y correr del animador...), y dos de ellos podían escribir a la vez en
        // el mismo NPC. Con cualquiera de esas cuentas, un NPC que anda más despacio que «su»
        // referencia caía entre Idle (0) y Andar (0,5) del blend tree: se desliza con las piernas
        // casi quietas. Ahora todos llaman aquí, y la cuenta usa la velocidad REAL, en m/s:
        //   - parado (menos de VelocidadParado): 0, idle;
        //   - moviéndose: nunca menos de 0,5, la animación de andar;
        //   - de VelocidadTrote en adelante: 1, el trote con saltitos de Will y Eldran.
        // Entre VelocidadAndar y VelocidadTrote pasa de uno a otro sin saltos.

        /// Por debajo de esto, en m/s, el NPC está quieto.
        public const float VelocidadParado = 0.15f;
        /// Hasta esto, en m/s, anda (0,5 en el blend tree).
        public const float VelocidadAndar = 1.0f;
        /// Desde esto, en m/s, trota con saltitos (1 en el blend tree).
        public const float VelocidadTrote = 1.8f;

        /// Valor de InputMagnitude (blend tree «Free Locomotion») para lo que el agente se mueve.
        public static float FactorDeLocomocion(NavMeshAgent agent)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return 0f;
            float vel = agent.velocity.magnitude;
            // Mientras acelera, lo que quiere andar adelanta la animación; si está bloqueado
            // (quiere andar pero no se mueve), se queda quieto.
            if (vel >= 0.05f) vel = Mathf.Max(vel, agent.desiredVelocity.magnitude);
            return FactorDeLocomocion(vel);
        }

        /// Valor de InputMagnitude para una velocidad en m/s. Ver el comentario de arriba.
        public static float FactorDeLocomocion(float velocidad)
        {
            if (velocidad < VelocidadParado) return 0f;
            if (velocidad <= VelocidadAndar) return 0.5f;
            return Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(VelocidadAndar, VelocidadTrote, velocidad));
        }
    }
}
