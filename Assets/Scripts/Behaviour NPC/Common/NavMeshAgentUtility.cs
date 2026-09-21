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

            // No se encontró NavMesh cerca: habilitar igual para no romper el flujo existente.
            // El código llamante debe seguir comprobando agent.isOnNavMesh tras esta llamada.
            agent.enabled = true;
            return agent.isOnNavMesh;
        }

        public static void SafeSetStopped(NavMeshAgent agent, bool stopped)
        {
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = stopped;
        }

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

        public static float ComputeSpeedFactor(NavMeshAgent agent)
        {
            if (agent == null || !agent.isOnNavMesh)
                return 0f;

            if (agent.speed <= 0.01f)
                return 0f;

            float vel = agent.velocity.magnitude;

            // Usar desiredVelocity solo cuando el agente ya está en movimiento real.
            // Evita mostrar animación de caminar cuando el agente está bloqueado
            // (desiredVelocity > 0 pero velocity ≈ 0).
            float refSpeed = vel >= 0.05f ? Mathf.Max(vel, agent.desiredVelocity.magnitude) : vel;

            return Mathf.Clamp01(refSpeed / agent.speed);
        }

        // FIX 4 sep 2026 (petición de Raúl: "cuando eldran camina... antes hacia la animación
        // correcta ahora los npcs... cuando les tengo que seguir del punto A al punto B están
        // haciendo una animación de andar que no es la que toca, deben hacer la que hagan los
        // personajes principales"): el Animator Controller genérico de los NPCs (NPC_NoWeapon,
        // usado por Eldran y compañía) resultó ser LITERALMENTE el mismo blend tree "Free
        // Locomotion" y los mismos clips que usa el propio Invector@BasicLocomotion.controller de
        // los personajes jugables (mismos guids de WalkFWD_RM en el umbral 0.5 y MoveFWD_Normal_RM
        // en el umbral 1) — no son sistemas de animación distintos como se sospechaba al principio.
        // El problema es de CALIBRACIÓN: ComputeSpeedFactor (arriba) devuelve
        // velocidad_actual/agent.speed, así que en cuanto un NavMeshAgent alcanza su velocidad
        // configurada -algo casi inmediato al hacer SetDestination en una secuencia de "sígueme",
        // ver CinematicState.MoveToPositionSequence/MoveToAction/LeadPlayerToAnchorSequence- el
        // valor llega a ~1.0, que en el blend tree cae en el tramo de MoveFWD_Normal (el mismo
        // clip que el jugador solo enseña esprintando), no en WalkFWD (umbral 0.5, lo que el
        // jugador enseña al caminar con normalidad). De ahí que estos NPCs parecieran "trotar" en
        // vez de caminar. Este helper satura el resultado al tramo de caminar del blend tree, para
        // usar en las secuencias donde el NPC debe caminar con paso normal (nunca correr) sin
        // tocar ComputeSpeedFactor en sí -otros llamadores (p.ej. combate/persecución) sí pueden
        // querer el rango completo 0-1 para mostrar una marcha más rápida-.
        // ESTADO 16 sep 2026: este tope YA NO SE USA en ningún sitio del proyecto. Raúl pidió
        // ese día que todos los NPCs se movieran como Oliver ("Oliver va genial y me parece más
        // limpio"), y eso significa rango completo 0-1: la rampa Idle→Walk→Run que da la
        // aceleración real del agente, en vez de un salto de 0 a 0.5 que se queda clavado ahí.
        // Todos los llamadores de CinematicState pasaron a ComputeSpeedFactor.
        //
        // Se conserva a propósito, no es código muerto por descuido: es la vuelta atrás si algún
        // día reaparece el "trotan en vez de caminar" del 4 sep en alguna escena concreta. En ese
        // caso se cambia SOLO esa llamada, no todas.
        public const float WalkGaitThreshold = 0.5f;

        public static float ComputeWalkGaitSpeedFactor(NavMeshAgent agent)
        {
            float raw = ComputeSpeedFactor(agent);
            return raw > 0f ? Mathf.Min(raw, WalkGaitThreshold) : 0f;
        }

        // FIX 5 sep 2026 (incidencia saltitos/animación mal en escolta de Eldran): variante que
        // calcula el factor contra una velocidad de referencia FIJA en vez de agent.speed. Hace
        // falta cuando el propio llamador cambia agent.speed dinámicamente frame a frame (ver
        // CinematicState.LeadPlayerToAnchorSequence, que reduce agent.speed progresivamente
        // cuando el jugador se queda atrás) — en ese caso velocidad_actual/agent.speed da casi
        // siempre ~1.0 (el NavMeshAgent converge su velocidad real al valor de agent.speed casi
        // al instante), así que ComputeWalkGaitSpeedFactor(agent) se queda pegado en
        // WalkGaitThreshold SIEMPRE, sin reflejar que el NPC está yendo mucho más despacio en
        // términos absolutos. Confirmado con logs: agent.speed bajando de 3.5 a 1.1 y el factor
        // recortado se quedaba fijo en 0.5 todo el tiempo. Usar la velocidad base (constante,
        // capturada una vez al iniciar la secuencia) como referencia soluciona esto: ahora el
        // factor sí baja cuando el NPC va más despacio de lo normal.
        public static float ComputeWalkGaitSpeedFactor(NavMeshAgent agent, float referenceSpeed)
        {
            if (agent == null || !agent.isOnNavMesh) return 0f;
            if (referenceSpeed <= 0.01f) return 0f;

            float vel = agent.velocity.magnitude;
            float refSpeed = vel >= 0.05f ? Mathf.Max(vel, agent.desiredVelocity.magnitude) : vel;

            // FIX 5 sep 2026 (v2 -- incidencia "otra animación"/pose de andar distinta durante
            // TODA la escolta, no solo al pararse): la versión anterior de este método normalizaba
            // refSpeed contra referenceSpeed COMPLETO (el 100% de la velocidad base del escolta).
            // Eso arregló los "saltitos", pero introdujo un problema nuevo: en
            // LeadPlayerToAnchorSequence, agent.speed se reduce CONTINUAMENTE con un Lerp según lo
            // lejos que esté el jugador (100% cuando está pegado, hasta 20% cuando está a
            // _escortMaxDist) -- y ESO ES LO NORMAL EN CUALQUIER ESCOLTA, no un caso raro, porque
            // el jugador casi nunca camina pegado del todo al NPC. Con el 100% de referenceSpeed
            // como divisor, en cuanto el jugador se quedaba a una distancia media/grande (la
            // mayor parte del tiempo) el factor se quedaba muy por debajo de WalkGaitThreshold
            // (p.ej. ~0.2 en vez de 0.5), y el blend tree mostraba una mezcla débil entre Idle y
            // Walk -- piernas casi sin zancada, brazos pegados al cuerpo -- que Raúl describió como
            // "otra animación"/pose distinta, confirmado visualmente comparando con Will (que sí
            // llega a un blend confiado). Ver claude/incidencia-eldran-... para el video de
            // comparación.
            //
            // Fix: solo exigimos que refSpeed supere una FRACCIÓN pequeña (25%) de referenceSpeed
            // para considerar que el NPC "está caminando de verdad" y mostrar el paso completo
            // (WalkGaitThreshold) -- ese 25% ya cubre el mínimo real de la Lerp de ritmo de la
            // escolta (20%), así que durante el ritmo normal (20%-100%) el blend se queda
            // confiadamente en WalkGaitThreshold, igual que como camina Will. Por debajo de ese
            // 25% (parándose de verdad, llegando al anchor, esperando al jugador) SÍ interpolamos
            // hacia Idle de forma gradual -- conserva el objetivo original del fix de "saltitos":
            // que nunca salte de golpe de caminar a Idle en un solo frame.
            float walkConfidenceFloor = Mathf.Max(0.05f, referenceSpeed * 0.25f);
            float raw = Mathf.Clamp01(refSpeed / walkConfidenceFloor);
            return raw > 0f ? Mathf.Min(raw, WalkGaitThreshold) : 0f;
        }

        // FIX 9 sept 2026 (incidencia "tirones" en Estela/Liam siguiendo al jugador, reportado
        // en contraste directo con el guardia -LeadPlayerToAnchorSequence-, que sí anima limpio
        // desde el fix de arriba): mismo diagnóstico que ComputeWalkGaitSpeedFactor(agent,
        // referenceSpeed) -- FollowPlayerState reasigna agent.speed cada frame (salto discreto
        // entre velocidad de caminar y una "velocidad de catch-up" dinámica que además varía con
        // _smoothedPlayerSpeed), así que usar agent.speed como divisor de ComputeSpeedFactor()
        // produce saltos en el factor de animación aunque la velocidad real del NavMeshAgent no
        // haya cambiado todavía (necesita tiempo para acelerar). A diferencia de
        // ComputeWalkGaitSpeedFactor, aquí NO se satura a WalkGaitThreshold: el compañero sí debe
        // llegar a mostrar la animación de correr al alcanzar al jugador, así que el llamador debe
        // pasar una referencia ya SUAVIZADA (no agent.speed en crudo) para obtener un resultado
        // estable en todo el rango 0-1.
        public static float ComputeSpeedFactor(NavMeshAgent agent, float referenceSpeed)
        {
            if (agent == null || !agent.isOnNavMesh) return 0f;
            if (referenceSpeed <= 0.01f) return 0f;

            float vel = agent.velocity.magnitude;
            float refSpeed = vel >= 0.05f ? Mathf.Max(vel, agent.desiredVelocity.magnitude) : vel;

            return Mathf.Clamp01(refSpeed / referenceSpeed);
        }
    }
}
