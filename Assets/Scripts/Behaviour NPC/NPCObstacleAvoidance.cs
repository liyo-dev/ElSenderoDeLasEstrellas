using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Esquiva de obstáculos "físicos" para NPCs con NavMeshAgent, pensada específicamente para el
/// caso de los árboles del Bosque Prohibido (INC-001 / INC-173): los 568 <c>Tree02_a01</c> del
/// bosque NUNCA se marcaron "Navigation Static" (el NavMesh horneado no sabe que existen) y
/// dependen de <c>NavMeshObstacle</c> + Carve para que los agentes los rodeen — pero todo el
/// trabajo de ajustar Carve/tamaños de cápsula en la sesión del 4 sep 2026 se revirtió a
/// petición de Raúl (ver <c>incidencia-navmesh-error-damagesequence-sueno-por-distancia-2026-09-04.md</c>),
/// así que hoy los árboles vuelven a tener cápsulas por defecto (0,5m) que no cubren ni de lejos
/// el tronco real, y cualquier NPC que camine "a través" del punto donde está el árbol se queda
/// empujando contra su collider físico en vez de rodearlo.
///
/// Esta clase NO toca NavMesh, Carve, ni el bakeado — es un desvío puramente en runtime, barato
/// (SphereCast/CheckSphere throttled a unas 6-7 veces por segundo, sin allocs) que se añade como
/// componente adicional junto al <c>NavMeshAgent</c> de cualquier NPC (arañas, miembros del
/// party, NPCs de <c>NPCBehaviourManagerV2</c>...). No sustituye a <c>agent.SetDestination</c> —
/// que sigue llamándose exactamente igual desde cada script de comportamiento (Spider1AI,
/// FollowPlayerState, WanderState, etc.) — solo corrige la VELOCIDAD del agente ese frame,
/// desviándola lateralmente cuando detecta un obstáculo físico justo delante, y dejando que el
/// propio NavMeshAgent retome el rumbo normal en cuanto el camino queda libre.
///
/// Distinción personaje vs. escenario: los árboles/props NO tienen capa propia (viven en
/// "Default", igual que los personajes — ver AGENTS.md § 2), así que se descartan los impactos
/// contra jugador/NPCs comprobando <c>NPCSimpleAnimator</c> en la raíz del objeto golpeado, el
/// mismo criterio ya usado en <c>PlayerParty.cs</c>, <c>NPCCombatBrain.cs</c> y
/// <c>DialogueCinematicController.cs</c> para el mismo problema.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCObstacleAvoidance : MonoBehaviour
{
    [Header("Detección")]
    [Tooltip("Capas físicas a comprobar como posible obstáculo. Por defecto, todas (los props viven en Default, igual que los personajes) — se filtran por NPCSimpleAnimator, no por capa.")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Tooltip("Distancia de anticipación del SphereCast, por delante del agente en su dirección de movimiento actual.")]
    [SerializeField, Min(0.3f)] private float lookaheadDistance = 2.2f;

    [Tooltip("Radio del SphereCast. -1 = usar el radio real del NavMeshAgent + un pequeño margen (recomendado, así no hay que ajustarlo a mano por especie).")]
    [SerializeField] private float castRadius = -1f;

    [Tooltip("Cada cuántos segundos se re-evalúa la detección. No hace falta cada frame — el desvío se mantiene aplicado entre comprobaciones.")]
    [SerializeField, Min(0.05f)] private float checkInterval = 0.15f;

    [Header("Esquiva")]
    [Tooltip("Cuánto pesa el desvío lateral frente a la dirección deseada original (1 = mismo peso). Valores altos esquivan más brusco.")]
    [SerializeField, Min(0.1f)] private float avoidWeight = 1.1f;

    [Tooltip("Velocidad de suavizado al aplicar/soltar el desvío (más alto = reacciona más rápido).")]
    [SerializeField, Min(0.5f)] private float steerLerp = 8f;

    [Tooltip("Red de seguridad: si el NPC lleva más de este tiempo esquivando sin parar (p.ej. encajado entre dos obstáculos en una zona con mobiliario apretado), se suelta el desvío en vez de arriesgarse a un bloqueo permanente.")]
    [SerializeField, Min(0.3f)] private float maxContinuousAvoidSeconds = 1.5f;

    private NavMeshAgent _agent;
    private float _effectiveCastRadius;
    private float _timer;
    private bool _avoiding;
    private Vector3 _avoidLateral; // unitario, hacia el lado elegido para rodear el obstáculo
    private int _effectiveObstacleLayers; // obstacleLayers sin la capa "Floor" (ver FIX 6 sept 2026 en Awake)
    private float _avoidingSince = -1f; // Time.time en que empezó a esquivar sin interrupción, -1 = no esquivando

    /// Está desviando al agente ahora mismo para rodear algo que tiene delante.
    public bool Esquivando => _avoiding;

    /// El último objeto de escenario que le hizo esquivar (para diagnósticos).
    public Collider UltimoObstaculo { get; private set; }

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _effectiveCastRadius = castRadius > 0f ? castRadius : (_agent.radius + 0.15f);

        // FIX (6 sept 2026): excluir siempre la capa "Floor" (el suelo/terreno de todo el
        // proyecto, ver AGENTS.md § 2 y el resto de raycasts de suelo ya existentes) del set de
        // obstáculos. Sin esto, cualquier ondulación/pendiente del terreno que el SphereCast
        // rozara a la altura del pecho del NPC se interpretaba como un obstáculo real y
        // disparaba la esquiva lateral — reportado por Raúl como "saltitos, como si hubiera
        // pequeños montículos" en NPCs que nunca antes tuvieron ese problema (Eldran/Liam/
        // Estela siguiendo a Will, arañas del bosque) justo tras añadir este componente
        // (INC-173). El terreno/suelo nunca debe tratarse como obstáculo a esquivar.
        int floorLayer = LayerMask.NameToLayer("Floor");
        _effectiveObstacleLayers = floorLayer >= 0 ? (obstacleLayers & ~(1 << floorLayer)) : (int)obstacleLayers;

        // Desincroniza el timer entre NPCs: sin esto, todos los agentes instanciados el mismo
        // frame (p.ej. las 59 arañas del bosque al cargar la escena) harían su SphereCast en el
        // mismo frame exacto cada checkInterval, en vez de repartirse en el tiempo.
        _timer = Random.Range(0f, checkInterval);
    }

    void Update()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _agent.isStopped)
        {
            _avoiding = false;
            _avoidingSince = -1f;
            return;
        }

        Vector3 desired = _agent.desiredVelocity;
        if (desired.sqrMagnitude < 0.01f)
        {
            _avoiding = false;
            _avoidingSince = -1f;
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = checkInterval;
            EvaluateObstacle(desired);
        }

        // FIX (6 sept 2026): red de seguridad contra bloqueo permanente — ver comentario del campo
        // maxContinuousAvoidSeconds. Reportado por Raúl como "barrera invisible" bloqueando el avance
        // en la plaza de MainWorld (mobiliario urbano apretado: bancos + farolas), justo tras activar
        // el componente por primera vez en los prefabs reales de party (Eldran/_LIAM/_ESTELA).
        if (_avoiding && _avoidingSince > 0f && Time.time - _avoidingSince > maxContinuousAvoidSeconds)
        {
            _avoiding = false;
            _avoidingSince = -1f;
        }

        if (_avoiding)
        {
            Vector3 desiredDir = desired.normalized;
            Vector3 blended = (desiredDir + _avoidLateral * avoidWeight).normalized;
            Vector3 targetVelocity = blended * desired.magnitude;
            _agent.velocity = Vector3.Lerp(_agent.velocity, targetVelocity, Time.deltaTime * steerLerp);
        }
    }

    private void EvaluateObstacle(Vector3 desired)
    {
        Vector3 dir = desired.normalized;
        float halfHeight = _agent.height * 0.5f;
        // Se adelanta el origen un poco más allá del propio radio del agente para no auto-golpear
        // su propio collider con el SphereCast (que sí solapa el punto de partida si se lanza
        // desde el centro exacto del NPC).
        Vector3 origin = transform.position + Vector3.up * halfHeight + dir * (_agent.radius + 0.05f);

        if (Physics.SphereCast(origin, _effectiveCastRadius, dir, out RaycastHit hit, lookaheadDistance, _effectiveObstacleLayers, QueryTriggerInteraction.Ignore)
            && hit.transform.root.GetComponent<NPCSimpleAnimator>() == null) // descarta jugador/NPCs, solo escenario
        {
            Vector3 lateral = Vector3.Cross(Vector3.up, dir).normalized;

            // FIX (6 sept 2026): comprobar los DOS lados, no solo uno. Antes solo se comprobaba
            // "rightBlocked" y, si estaba bloqueado, se esquivaba hacia el lado contrario SIN
            // comprobar que ese lado estuviera realmente libre — en una zona con mobiliario urbano
            // apretado (bancos + farolas de la plaza de MainWorld) un NPC podía quedar encajado entre
            // dos obstáculos y el componente lo forzaba una y otra vez contra un lado que tampoco
            // estaba libre, sin poder escapar nunca. Un NPC de party (collider real, misma capa que
            // el jugador) atascado así en un paso estrecho se siente exactamente como una "barrera
            // invisible" bloqueando el avance — reportado por Raúl justo tras activar el componente
            // por primera vez en los prefabs reales.
            float probeOffset = _effectiveCastRadius * 2f;
            bool rightBlocked = Physics.CheckSphere(origin + lateral * probeOffset, _effectiveCastRadius * 0.6f, _effectiveObstacleLayers, QueryTriggerInteraction.Ignore);
            bool leftBlocked = Physics.CheckSphere(origin - lateral * probeOffset, _effectiveCastRadius * 0.6f, _effectiveObstacleLayers, QueryTriggerInteraction.Ignore);

            if (rightBlocked && leftBlocked)
            {
                // Encajado entre dos obstáculos: forzar un lateral aquí solo empeoraría el atasco.
                // Mejor no esquivar este ciclo (el NavMeshAgent puede recalcular su propio camino)
                // que empujar al NPC contra una pared que ya sabemos que está ahí.
                _avoiding = false;
                _avoidingSince = -1f;
                return;
            }

            if (!_avoiding) _avoidingSince = Time.time;
            UltimoObstaculo = hit.collider;
            _avoidLateral = rightBlocked ? -lateral : lateral;
            _avoiding = true;
        }
        else
        {
            _avoiding = false;
            _avoidingSince = -1f;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_agent == null) return;

        Vector3 dir = Application.isPlaying ? _agent.desiredVelocity.normalized : transform.forward;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;

        float halfHeight = _agent.height * 0.5f;
        Vector3 origin = transform.position + Vector3.up * halfHeight + dir * (_agent.radius + 0.05f);
        float radius = castRadius > 0f ? castRadius : (_agent.radius + 0.15f);

        Gizmos.color = _avoiding ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(origin + dir * lookaheadDistance, radius);
        Gizmos.DrawLine(origin, origin + dir * lookaheadDistance);

        if (_avoiding)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + _avoidLateral * 2f);
        }
    }
#endif
}
