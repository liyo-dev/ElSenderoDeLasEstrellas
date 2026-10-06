using System.Collections;
using UnityEngine;
using System.Collections.Generic;
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
    /// Sin lente válida se conserva el actor: una elipsis necesita probar que está fuera.
    public static bool EnEncuadre(SequenceActor actor, SequenceContext ctx)
    {
        var camara = ctx?.Player?.CachedCamera;
        if (camara == null || !camara.isActiveAndEnabled) camara = SolYLunaEnElCielo.CamaraActual();
        if (camara == null || !camara.isActiveAndEnabled || actor?.Transform == null) return true;
        // Una envolvente completa incluye cabeza, pies y bordes que todavía entran en plano.
        var cuerpo = new Bounds(actor.Transform.position + Vector3.up * actor.HeadTopHeight * 0.5f,
            new Vector3(actor.SafeRadius * 2f, actor.HeadTopHeight, actor.SafeRadius * 2f));
        GeometryUtility.CalculateFrustumPlanes(camara, s_planos);
        return GeometryUtility.TestPlanesAABB(s_planos, cuerpo);
    }

    private static readonly Plane[] s_planos = new Plane[6];

    public static float LimiteDeEspera(SequenceActor actor, Vector3 destino, float velocidad,
        float esperaMaxima)
        => esperaMaxima > 0f ? esperaMaxima
            : Vector3.Distance(actor.Transform.position, destino) / Mathf.Max(0.2f, velocidad) * 1.2f + 1f;

    /// Último recurso: elipsis fuera de cuadro o idle orientado hacia la llegada.
    public static void ResolverAtasco(SequenceActor actor, Vector3 destino, SequenceContext ctx)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        InformeDeRodaje.Aviso($"{actor.Id} atascado: {(EnEncuadre(actor, ctx) ? "se queda quieto en cuadro" : "elipsis a su destino")}");
#endif
        actor.StopMovement();
        if (!EnEncuadre(actor, ctx)) PlaceAt(actor, destino, actor.Transform.rotation, ctx);
        else actor.Face(destino);
    }

    /// Una muestra por segundo mide avance hacia el objetivo, no velocidad solicitada.
    public sealed class Vigilante
    {
        private float tiempo;
        private float distancia;
        private bool avisado;
        public Vigilante(Vector3 origen, Vector3 destino) { distancia = Vector3.Distance(origen, destino); }
        public bool Atascado(SequenceActor actor, Vector3 destino, string causa)
        {
            tiempo += Time.deltaTime;
            if (tiempo < 1f) return false;
            float actual = Vector3.Distance(actor.Transform.position, destino);
            bool atascado = distancia - actual < 0.15f;
            distancia = actual;
            tiempo = 0f;
            if (atascado && !avisado)
            {
                avisado = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[SequenceMovement] '{actor.Id}' sin progreso hacia {destino}: {causa}. Se recupera el tramo.");
                InformeDeRodaje.Aviso($"{actor.Id} sin avanzar ({causa})");
#endif
            }
            return atascado;
        }
    }
    /// Tolerancia de llegada sobre el stoppingDistance del agente. Mismo criterio que
    /// CinematicState.HasReachedDestination().
    public const float ArrivalTolerance = 0.25f;

    /// Coloca a un actor en una posición del mundo al instante (detrás de un corte o fuera de
    /// cámara). Con NavMeshAgent usa Warp: mover el transform a pelo deja al agente creyendo que
    /// sigue donde estaba, y el siguiente movimiento lo devuelve allí. Si en ese punto no hay
    /// NavMesh (lo alto de una colina, un tejado) el Warp falla; entonces se apaga el agente y se
    /// coloca el transform, porque ahí el actor necesita estar, no navegar.
    public static void PlaceAt(SequenceActor actor, Vector3 position, Quaternion rotation,
        SequenceContext ctx = null)
    {
        if (actor?.Transform == null) return;
        Vector3 puntoSolicitado = position;
        if (!ReservarLlegada(ctx, actor, position, out position)) return;

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
        if ((position - puntoSolicitado).sqrMagnitude > 0.01f) actor.Face(puntoSolicitado);
        actor.DestinoReservado = null;
    }

    /// Lleva a un actor hasta una posición del mundo.
    ///
    /// speedOverride: 0 (lo normal) = usar la velocidad ya configurada en su NavMeshAgent, que es
    /// con la que se mueve en cualquier otra parte del juego y con la que está calibrado el blend
    /// tree. Un valor > 0 la fuerza durante el trayecto y la restaura al terminar — solo tiene
    /// sentido si se ha verificado que el clip de carrera aguanta esa velocidad sin patinar.
    public static IEnumerator MoveTo(SequenceActor actor, Vector3 destination,
        float speedOverride, float timeout, bool esquivar = true, SequenceContext ctx = null,
        bool elipsis = false, float esperaMaxima = 0f)
    {
        if (actor == null || actor.Transform == null) yield break;
        Vector3 puntoSolicitado = destination;
        if (!ReservarLlegada(ctx, actor, destination, out destination)) yield break;

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
                speedOverride > 0.1f ? speedOverride : 2.2f, timeout, esquivar, ctx, elipsis, esperaMaxima);
            if ((actor.Transform.position - destination).sqrMagnitude < 0.01f
                && (destination - puntoSolicitado).sqrMagnitude > 0.01f) actor.Face(puntoSolicitado);
            actor.DestinoReservado = null;
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

        if (!ReservarLlegada(ctx, actor, target, out target))
        {
            actor.DestinoReservado = null;
            yield break;
        }
        // El actor conserva los ajustes originales para restaurarlos también al saltar.
        actor.BeginAgentOverride(speedOverride);
        ctx?.Player?.RegisterCleanup(() => { actor.EndAgentOverride(); actor.StopMovement(); actor.DestinoReservado = null; });

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
            float limite = LimiteDeEspera(actor, target, agent.speed, esperaMaxima);
            var vigilante = new Vigilante(actor.Transform.position, target);
            int intentos = 0;
            Vector3 anterior = actor.Transform.position;
            while (true)
            {
                elapsed += Time.deltaTime;

                if (agent == null || !agent.enabled || !agent.isOnNavMesh) break;

                // Un prop sin carve tampoco se atraviesa: el montaje debe habilitar su recorte.
                Vector3 avance = agent.desiredVelocity;
                if (avance.sqrMagnitude > 0.001f && Obstaculo(actor, actor.Transform.position,
                    actor.Transform.position + avance.normalized * (avance.magnitude * Time.deltaTime + 0.15f), out _))
                {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.LogWarning($"[SequenceMovement] '{actor.Id}' encuentra geometría fuera de su ruta: revisa NavMeshObstacle/carve.");
#endif
                    agent.isStopped = true;
                }

                if (!agent.pathPending &&
                    agent.hasPath &&
                    agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                    break;

                if (elipsis && elapsed >= limite && !EnEncuadre(actor, ctx))
                {
                    PlaceAt(actor, target, actor.Transform.rotation, ctx);
                    break;
                }
                if (vigilante.Atascado(actor, target, "ruta incompleta, geometría o bloqueo del agente"))
                {
                    if (++intentos > 2) { ResolverAtasco(actor, target, ctx); break; }
                    if (intentos == 2) agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                    agent.ResetPath();
                    agent.isStopped = false;
                    NavMeshAgentUtility.SetDestination(agent, target);
                }
                if (!elipsis && elapsed >= timeout) { ResolverAtasco(actor, target, ctx); break; }

                // El animator LEE la velocidad real del agente, ya normalizada 0-1. Nada de m/s.
                float real = Vector3.Distance(anterior, actor.Transform.position) / Mathf.Max(Time.deltaTime, 0.0001f);
                anterior = actor.Transform.position;
                anim?.SetMovementSpeed(real < 0.05f ? 0f : NavMeshAgentUtility.FactorDeLocomocion(agent));

                // Rotación continua hacia la dirección real de avance, para que nunca se le vea
                // caminar de lado o de espaldas cuando la ruta gira.
                if (agent.velocity.sqrMagnitude > 0.01f)
                    GirarHaciaAvance(actor, anim, agent.velocity);

                yield return null;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.pathPending
                && agent.remainingDistance <= agent.stoppingDistance + 0.05f
                && (target - puntoSolicitado).sqrMagnitude > 0.01f) actor.Face(puntoSolicitado);

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
            actor.DestinoReservado = null;
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

    private static readonly RaycastHit[] s_barrido = new RaycastHit[64];
    private static readonly Collider[] s_ocupantes = new Collider[64];
    private static readonly Collider[] s_geometria = new Collider[64];
    private static readonly Dictionary<Collider, bool> s_personajes = new();

    public static void LimpiarCache() => s_personajes.Clear();
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => LimpiarCache();
#endif

    public static bool EsPersonaje(Collider collider)
    {
        if (collider == null) return false;
        if (!s_personajes.TryGetValue(collider, out bool personaje))
        {
            personaje = collider.transform.root.GetComponent<NPCSimpleAnimator>() != null;
            s_personajes[collider] = personaje;
        }
        return personaje;
    }

    /// Elige primero el lado más cercano y reserva antes de ceder el frame a otro movimiento.
    public static bool ReservarLlegada(SequenceContext ctx, SequenceActor actor, Vector3 destino,
        out Vector3 libre)
    {
        libre = destino;
        if (ctx == null || actor.IsDynamic) return true;
        ctx.Player?.RegisterCleanup(() => actor.DestinoReservado = null);
        Vector3 linea = destino - actor.Transform.position;
        linea.y = 0f;
        Vector3 lado = linea.sqrMagnitude > 0.001f
            ? Vector3.Cross(Vector3.up, linea.normalized) : actor.Transform.right;
        if (Vector3.Dot(actor.Transform.position - destino, lado) < 0f) lado = -lado;
        for (int anillo = 0; anillo < 5; anillo++)
        {
            int cantidad = anillo == 0 ? 1 : 16;
            float radio = anillo == 1 ? 0.9f : 1.3f + Mathf.Max(0, anillo - 2) * 0.5f;
            for (int i = 0; i < cantidad; i++)
            {
                float angulo = i == 0 ? 0f : i == 1 ? 180f : (i % 2 == 0 ? 1f : -1f) * ((i / 2) * 22.5f);
                Vector3 candidato = anillo == 0 ? destino
                    : destino + Quaternion.AngleAxis(angulo, Vector3.up) * lado * radio;
                if (actor.Agent != null && actor.Agent.enabled && actor.Agent.isOnNavMesh)
                {
                    if (!NavMesh.SamplePosition(candidato, out var nav, 0.3f, actor.Agent.areaMask)) continue;
                    candidato = nav.position;
                }
                if (!LlegadaLibre(ctx, actor, candidato)) continue;
                libre = candidato;
                actor.DestinoReservado = libre;
                return true;
            }
        }
        return false;
    }

    private static bool LlegadaLibre(SequenceContext ctx, SequenceActor actor, Vector3 punto)
    {
        foreach (var otro in ctx.PersonajesVisibles)
        {
            if (otro?.Transform == null || otro.Transform == actor.Transform || otro.IsDynamic
                || !otro.Transform.gameObject.activeInHierarchy) continue;
            float separacion = actor.SafeRadius + otro.SafeRadius + 0.35f;
            Vector3 d = punto - otro.Transform.position;
            if (Mathf.Abs(d.y) < Mathf.Max(actor.HeadTopHeight, otro.HeadTopHeight))
            {
                d.y = 0f;
                if (d.sqrMagnitude < separacion * separacion) return false;
            }
            if (!otro.DestinoReservado.HasValue) continue;
            d = punto - otro.DestinoReservado.Value;
            if (Mathf.Abs(d.y) > Mathf.Max(actor.HeadTopHeight, otro.HeadTopHeight)) continue;
            d.y = 0f;
            if (d.sqrMagnitude < separacion * separacion) return false;
        }
        int n = Physics.OverlapSphereNonAlloc(punto + Vector3.up * 0.7f,
            actor.SafeRadius + 1f, s_ocupantes, ~0, QueryTriggerInteraction.Collide);
        if (n == s_ocupantes.Length) return false;
        for (int i = 0; i < n; i++)
        {
            var c = s_ocupantes[i];
            if (c == null || c.transform.root == actor.Transform.root || !EsPersonaje(c)) continue;
            Vector3 d = punto - c.transform.root.position;
            if (Mathf.Abs(d.y) > actor.HeadTopHeight) continue;
            d.y = 0f;
            float r = actor.SafeRadius + Mathf.Max(c.bounds.extents.x, c.bounds.extents.z) + 0.25f;
            if (d.sqrMagnitude < r * r) return false;
        }
        float radioActor = Mathf.Max(0.15f, actor.SafeRadius);
        int geometrias = Physics.OverlapCapsuleNonAlloc(punto + Vector3.up * (radioActor + 0.31f),
            punto + Vector3.up * Mathf.Max(radioActor + 0.31f, actor.HeadTopHeight - radioActor),
            radioActor, s_geometria, ~0, QueryTriggerInteraction.Ignore);
        if (geometrias == s_geometria.Length) return false;
        for (int i = 0; i < geometrias; i++)
        {
            var c = s_geometria[i];
            if (c == null || c.transform.root == actor.Transform.root || EsPersonaje(c) || c is TerrainCollider) continue;
            if (c.bounds.max.y > punto.y + 0.3f) return false;
        }
        return true;
    }

    /// Barre todo el cuerpo; el suelo bajo los pies y los personajes no son props.
    public static bool Obstaculo(SequenceActor actor, Vector3 desde, Vector3 hasta, out Bounds bounds)
    {
        bounds = default;
        Vector3 delta = hasta - desde;
        float largo = delta.magnitude;
        if (largo < 0.0001f) return false;
        float radio = Mathf.Max(0.15f, actor.SafeRadius);
        Vector3 abajo = desde + Vector3.up * (radio + 0.31f);
        Vector3 arriba = desde + Vector3.up * Mathf.Max(radio + 0.31f, actor.HeadTopHeight - radio);
        int n = Physics.CapsuleCastNonAlloc(abajo, arriba, radio, delta / largo,
            s_barrido, largo, ~0, QueryTriggerInteraction.Ignore);
        float masCerca = float.PositiveInfinity;
        bool encontrado = false;
        for (int i = 0; i < n; i++)
        {
            var hit = s_barrido[i];
            var c = hit.collider;
            if (c == null || c.transform.root == actor.Transform.root || EsPersonaje(c)) continue;
            if (hit.normal.y > 0.65f && hit.point.y <= desde.y + 0.3f) continue;
            if (c.bounds.max.y <= Mathf.Min(desde.y, hasta.y) + 0.3f) continue;
            if (hit.distance >= masCerca) continue;
            masCerca = hit.distance; bounds = c.bounds; encontrado = true;
        }
        return encontrado || n == s_barrido.Length;
    }

    /// Usa esquinas de NavMesh si hay ruta completa y comprueba también los props sin carve.
    public static List<Vector3> CalcularRuta(SequenceActor actor, Vector3 destino, bool esquivar)
    {
        var ruta = new List<Vector3>();
        Vector3 desde = actor.Transform.position;
        if (!esquivar) { ruta.Add(destino); return ruta; }
        var nav = new NavMeshPath();
        int areas = actor.Agent != null ? actor.Agent.areaMask : NavMesh.AllAreas;
        if (NavMesh.SamplePosition(desde, out var inicio, 0.4f, areas)
            && NavMesh.SamplePosition(destino, out var fin, 0.4f, areas)
            && NavMesh.CalculatePath(inicio.position, fin.position, areas, nav)
            && nav.status == NavMeshPathStatus.PathComplete)
        {
            var esquinas = nav.corners;
            for (int i = 1; i < esquinas.Length; i++)
            {
                if (!InsertarRodeo(actor, desde, esquinas[i], ruta)) { ruta.Clear(); return ruta; }
                desde = esquinas[i];
            }
            if (!InsertarRodeo(actor, desde, destino, ruta)) ruta.Clear();
        }
        else if (!InsertarRodeo(actor, desde, destino, ruta)) ruta.Clear();
        return SuavizarRuta(actor, actor.Transform.position, ruta);
    }

    private static bool InsertarRodeo(SequenceActor actor, Vector3 desde, Vector3 hasta, List<Vector3> ruta)
    {
        if (!Obstaculo(actor, desde, hasta, out var b)) { ruta.Add(hasta); return true; }
        if (b.size == Vector3.zero) return false;
        b.Expand((actor.SafeRadius + 0.5f) * 2f);
        Vector3[] esquinas = {
            new(b.min.x, desde.y, b.min.z), new(b.min.x, desde.y, b.max.z),
            new(b.max.x, desde.y, b.max.z), new(b.max.x, desde.y, b.min.z)
        };
        float mejor = float.PositiveInfinity;
        int entrada = -1, salida = -1;
        // Un punto tangente o dos esquinas consecutivas rodean la caja sin cortar su centro.
        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 4; j++)
        {
            if (i != j && (i + 1) % 4 != j && (j + 1) % 4 != i) continue;
            if (Obstaculo(actor, desde, esquinas[i], out _)
                || Obstaculo(actor, esquinas[i], esquinas[j], out _)
                || Obstaculo(actor, esquinas[j], hasta, out _)) continue;
            float coste = Vector3.Distance(desde, esquinas[i]) + Vector3.Distance(esquinas[i], esquinas[j])
                + Vector3.Distance(esquinas[j], hasta);
            if (coste >= mejor) continue;
            mejor = coste; entrada = i; salida = j;
        }
        if (entrada < 0) return false;
        ruta.Add(esquinas[entrada]);
        if (salida != entrada) ruta.Add(esquinas[salida]);
        ruta.Add(hasta);
        return true;
    }

    private static List<Vector3> SuavizarRuta(SequenceActor actor, Vector3 inicio, List<Vector3> ruta)
    {
        var suave = new List<Vector3>();
        for (int i = 0; i < ruta.Count; i++)
        {
            if (i == ruta.Count - 1) { suave.Add(ruta[i]); break; }
            Vector3 antes = i == 0 ? inicio : ruta[i - 1];
            Vector3 esquina = ruta[i];
            float radio = Mathf.Min(0.45f, Vector3.Distance(antes, esquina) * 0.25f,
                Vector3.Distance(esquina, ruta[i + 1]) * 0.25f);
            Vector3 a = Vector3.MoveTowards(esquina, antes, radio);
            Vector3 b = Vector3.MoveTowards(esquina, ruta[i + 1], radio);
            Vector3 previo = suave.Count > 0 ? suave[suave.Count - 1] : inicio;
            bool limpia = !Obstaculo(actor, previo, a, out _);
            previo = a;
            for (int k = 1; k <= 4 && limpia; k++)
            {
                float t = k / 4f;
                Vector3 p = Vector3.Lerp(Vector3.Lerp(a, esquina, t), Vector3.Lerp(esquina, b, t), t);
                limpia = !Obstaculo(actor, previo, p, out _); previo = p;
            }
            if (!limpia) { suave.Add(esquina); continue; }
            suave.Add(a);
            for (int k = 1; k <= 4; k++)
            {
                float t = k / 4f;
                suave.Add(Vector3.Lerp(Vector3.Lerp(a, esquina, t), Vector3.Lerp(esquina, b, t), t));
            }
        }
        return suave;
    }
}
