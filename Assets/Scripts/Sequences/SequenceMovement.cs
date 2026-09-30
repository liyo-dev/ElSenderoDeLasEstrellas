using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;

/// El movimiento de actores durante una secuencia, en UN solo sitio.
///
/// Esto es literalmente la razón de ser del sistema. Antes cada sequencer escribía su propio
/// helper de movimiento (Co_RunTo en Oliver, Co_SimpleWalkTo en el Mago Oscuro...) y cada uno
/// volvía a tropezar con lo mismo. INC-209 documenta los cuatro defectos que tenía el de Oliver:
/// pasar m/s en crudo a SetMovementSpeed() (que espera 0-1 y satura), no rotar durante el
/// trayecto, moverse a una velocidad distinta de la del propio agente, e ir en línea recta con el
/// NavMeshAgent apagado.
///
/// Aquí se hace como lo hace el resto del juego (CinematicState.MoveToPositionSequence): el
/// NavMeshAgent conduce, y el animator se limita a LEER su velocidad real ya normalizada. Es la
/// versión que Raúl aprobó el 16 sep 2026 ("así es como quiero que caminen, como lo hace el player
/// cuando lo controlo yo"). Cualquier arreglo futuro de "los NPCs andan raro" se hace en este
/// archivo y lo heredan todas las secuencias.
public static class SequenceMovement
{
    /// Tolerancia de llegada sobre el stoppingDistance del agente. Mismo criterio que
    /// CinematicState.HasReachedDestination().
    public const float ArrivalTolerance = 0.25f;

    /// Coloca a un actor en una posición del mundo al instante (detrás de un corte o fuera de
    /// cámara). Con NavMeshAgent usa Warp: mover el transform a pelo deja al agente creyendo que
    /// sigue donde estaba, y el siguiente movimiento lo devuelve allí. Si en ese punto no hay
    /// NavMesh (lo alto de una colina, un tejado) el Warp falla; entonces se apaga el agente y se
    /// coloca el transform, porque ahí el actor necesita estar, no navegar.
    public static void PlaceAt(SequenceActor actor, Vector3 position, Quaternion rotation)
    {
        if (actor?.Transform == null) return;

        actor.StopMovement();

        var agent = actor.Agent;
        bool colocado = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh) colocado = agent.Warp(position);

        if (!colocado)
        {
            if (agent != null && agent.enabled) agent.enabled = false;
            actor.Transform.position = position;
        }

        actor.Transform.rotation = rotation;
        actor.SyncRotation();
    }

    /// Lleva a un actor hasta una posición del mundo.
    ///
    /// speedOverride: 0 (lo normal) = usar la velocidad ya configurada en su NavMeshAgent, que es
    /// con la que se mueve en cualquier otra parte del juego y con la que está calibrado el blend
    /// tree. Un valor > 0 la fuerza durante el trayecto y la restaura al terminar — solo tiene
    /// sentido si se ha verificado que el clip de carrera aguanta esa velocidad sin patinar.
    public static IEnumerator MoveTo(SequenceActor actor, Vector3 destination,
        float speedOverride, float timeout)
    {
        if (actor == null || actor.Transform == null) yield break;

        var agent = actor.Agent;
        var anim = actor.NpcAnimator;

        // Sin agente utilizable no hay camino canónico posible. Se coloca en el punto y se avisa,
        // en vez de caer en un Lerp a mano que reintroduciría el patinaje de INC-209.
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[SequenceMovement] '{actor.Id}' no tiene un NavMeshAgent utilizable " +
                "(sin componente, desactivado, o fuera del NavMesh), así que va andando en línea " +
                "recta en vez de navegando. Si esto sale en varios actores de la misma escena, lo " +
                "que hay que mirar es el bakeado del NavMesh, no el beat.");
#endif
            // ANTES SE TELETRANSPORTABA, y es lo que hacía que en el prólogo del 19 sep los diez
            // aldeanos "no huyeran": aparecían en el puente de un fotograma al siguiente. En una
            // cinemática eso nunca es aceptable — un personaje que se mueve sin andar se lee como
            // un fallo, no como una elipsis. Se cae al mismo paseo a mano que usa WalkPathBeat, que
            // no necesita NavMesh y alimenta la animación con la velocidad real (ver allí por qué
            // eso no reintroduce el patinaje de INC-209).
            yield return WalkPathBeat.AndarHasta(actor, destination,
                speedOverride > 0.1f ? speedOverride : 2.2f, timeout);
            yield break;
        }

        // El destino puede caer fuera del NavMesh (p. ej. un punto calculado alrededor de otro
        // actor). Proyectarlo antes, o remainingDistance nunca converge y el actor se come el
        // timeout entero empujando contra el borde.
        Vector3 target = destination;
        if (NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas))
        {
            target = hit.position;
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
        {
            Debug.LogWarning($"[SequenceMovement] El destino de '{actor.Id}' ({destination}) no cae " +
                "sobre el NavMesh ni a 2 m de él. Revisa la marca de posición o el bakeado de la zona.");
        }
#endif

        // Guarda velocidad y obstacle avoidance en el propio actor y aplica los de la secuencia
        // (obstacle avoidance desactivado, mismo criterio que CinematicState: el jugador y los
        // miembros del grupo no deben bloquear al actor, pero la geometría bakeada sí). Vive en el
        // actor y no en variables locales para que la limpieza del SequencePlayer pueda
        // restaurarlo también si la cinemática se salta a mitad de este beat.
        actor.BeginAgentOverride(speedOverride);

        try
        {
            // Salir de cualquier pose de interacción/diálogo antes de echar a andar: si no, un
            // bocadillo anterior puede dejar la capa UpperBody congelada todo el trayecto.
            anim?.SetBattleMode(false);
            anim?.SetTalking(false);
            anim?.EndInteraction();
            anim?.TransitionToLocomotion();

            NavMeshAgentUtility.SetDestination(agent, target);

            float elapsed = 0f;
            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;

                if (agent == null || !agent.enabled || !agent.isOnNavMesh) break;

                if (!agent.pathPending &&
                    agent.remainingDistance <= agent.stoppingDistance + ArrivalTolerance)
                    break;

                // El animator LEE la velocidad real del agente, ya normalizada 0-1. Nada de m/s.
                anim?.SetMovementSpeed(NavMeshAgentUtility.FactorDeLocomocion(agent));

                // Rotación continua hacia la dirección real de avance, para que nunca se le vea
                // caminar de lado o de espaldas cuando la ruta gira.
                if (agent.velocity.sqrMagnitude > 0.01f)
                    GirarHaciaAvance(actor, anim, agent.velocity);

                yield return null;
            }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (elapsed >= timeout)
                Debug.LogWarning($"[SequenceMovement] '{actor.Id}' no llegó a su destino en {timeout}s. " +
                    "La secuencia sigue desde donde esté. Suele significar que no hay ruta de NavMesh " +
                    "hasta ahí, o que el destino queda al otro lado de un obstáculo.");
#endif
        }
        finally
        {
            // Restaurar SIEMPRE lo que se tocó del agente. Si el skip interrumpe la corrutina sin
            // llegar aquí, la limpieza del SequencePlayer repite estas dos llamadas (las dos son
            // idempotentes).
            actor.EndAgentOverride();
            actor.StopMovement();
        }
    }

    /// Grados por segundo a los que un personaje se gira hacia donde anda. A 300 un giro de 90°
    /// tarda tres décimas: se lee como cambiar de rumbo, no como un salto de rotación.
    public const float GiroAlAndar = 300f;

    /// Gira al actor hacia donde avanza ESCRIBIENDO la rotación, y sincroniza el objetivo del
    /// animador para que no lo arrastre de vuelta. No vale `NPCSimpleAnimator.FaceDirection`: solo
    /// propone la rotación, y no llega a aplicarse si el animador tiene la rotación automática
    /// apagada (un diálogo o una cinemática anterior la dejan así): el actor anda de espaldas.
    /// Ver INC-313 e INC-479. Lo usan los dos caminos de movimiento (NavMesh y WalkPathBeat).
    public static void GirarHaciaAvance(SequenceActor actor, NPCSimpleAnimator anim, Vector3 direccion)
    {
        direccion.y = 0f;
        if (actor?.Transform == null || direccion.sqrMagnitude < 0.0001f) return;

        actor.Transform.rotation = Quaternion.RotateTowards(
            actor.Transform.rotation,
            Quaternion.LookRotation(direccion.normalized, Vector3.up),
            GiroAlAndar * Time.deltaTime);
        anim?.SyncTargetRotation();
    }

    /// Punto de parada alrededor de otro actor: a 'distance' metros de él, en el ángulo indicado
    /// respecto a la dirección a la que mira (0 = justo enfrente, encarados; 90 = a su derecha;
    /// 180 = a su espalda).
    public static Vector3 PointAround(Transform other, float distance, float angleDegrees)
    {
        if (other == null) return Vector3.zero;
        Vector3 dir = Quaternion.AngleAxis(angleDegrees, Vector3.up) * other.forward;
        return other.position + dir * distance;
    }
}
