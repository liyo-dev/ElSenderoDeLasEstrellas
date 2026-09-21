using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Beats que actúan sobre un actor: gesto, cara, hacia dónde mira, movimiento y el candado que lo
// saca de su comportamiento ambiental.

/// Reproduce una animación puntual sobre un actor.
///
/// Funciona igual con NPCs y con el jugador: los dos Animator Controllers comparten casi todo el
/// vocabulario (Laugh01, Cheer01/02, Angry01/02, HandClap01, TakeDamage_2, Dizzy_NoWeapon,
/// Beg01, HeadNod01...), y SequenceActor.PlayGesture() se encarga de enrutar a NPCSimpleAnimator o
/// a PlayerDialogueAnimator según corresponda.
[Serializable]
public class GestureBeat : SequenceBeat
{
    [Tooltip("ID del actor: 'Player' para Will, o el Persistence ID del NPC (p. ej. 'NPC_Oliver').")]
    public string actorId;

    [Tooltip("Nombre exacto del estado del Animator (p. ej. 'Cheer02', 'Laugh01', 'TakeDamage_2').")]
    public string gesture;

    [Tooltip("Cuántas veces reproducirlo seguidas. 2 sirve, por ejemplo, para que un saludo se lea " +
             "bien cuando la escena acaba de empezar y el primer ciclo se pierde con la transición.")]
    public int repeats = 1;

    [Tooltip("Segundos entre repeticiones y antes de dar el beat por terminado. Si es 0, el beat " +
             "no espera nada: lanza el gesto y sigue — que es justo lo que se quiere cuando va " +
             "dentro de un Parallel, reaccionando mientras otro habla.")]
    public float holdSeconds = 0f;

    [Tooltip("Al acabar, devolver al actor a su pose normal. Hace falta para los gestos de CUERPO " +
             "ENTERO que no tienen transición de salida propia en el Animator Controller: el caso " +
             "conocido es 'Dizzy_NoWeapon', que vive en la capa base y se queda puesto para siempre " +
             "(los gestos siguientes van en UpperBody y no lo limpian). Con esto marcado, el actor " +
             "vuelve a su locomoción normal al terminar el beat. Requiere holdSeconds > 0, para que " +
             "el gesto llegue a verse antes de cerrarse.")]
    public bool returnToNormalAfter = false;

    public override string Describe()
        => $"Gesto: {actorId} → {gesture}" + (repeats > 1 ? $" ×{repeats}" : "");

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor == null) yield break;

        int times = Mathf.Max(1, repeats);
        for (int i = 0; i < times; i++)
        {
            actor.PlayGesture(gesture);
            if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);
        }

        if (returnToNormalAfter) actor.ReturnToNormalPose();
    }
}

/// Cambia la cara de un actor (meshes de ojos y boca, vía NPCEmotionController).
[Serializable]
/// Mantener una pose, o soltarla.
///
/// Existe porque un GestureBeat es un DISPARO: reproduce el clip y, al acabar, el animador manda
/// al personaje a idle. Para saludar está bien; para volar, sostener un hechizo o quedarse en el
/// aire es justo lo contrario. Antes se apañaba repitiendo el gesto, y salían las dos caras del
/// mismo fallo: reinicio del clip desde el fotograma 0 (el tirón) o un idle colado entre
/// repeticiones — con el caso extremo de un idle de pie a veinte metros de altura.
///
/// La pose dura hasta que otro gesto la pise, hasta otra pose, o hasta un `soltar`.
public class PoseBeat : SequenceBeat
{
    [Tooltip("Quién la sostiene.")]
    public string actorId;

    [Tooltip("Nombre exacto del estado del Animator. Se deja vacío cuando 'soltar' está marcado.")]
    public string pose;

    [Tooltip("Marcado, suelta la pose que tuviera en vez de poner una nueva.")]
    public bool soltar = false;

    [Tooltip("Al soltar, devolver al personaje a su pose normal. Desmarcado lo deja como esté, " +
             "que es lo que hace falta si lo siguiente es otro gesto: así no se ve un idle de un " +
             "fotograma entre los dos.")]
    public bool volverAIdle = true;

    public override string Describe()
        => soltar ? $"Soltar pose: {actorId}" : $"Pose: {actorId} → {pose}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor == null) yield break;

        if (soltar) actor.ReleasePose(volverAIdle);
        else actor.HoldPose(pose);

        yield break;
    }
}

[Serializable]
public class EmotionBeat : SequenceBeat
{
    [Tooltip("ID del actor: 'Player' o el Persistence ID del NPC.")]
    public string actorId;

    [Tooltip("Emoción a poner. 'None' no cambia nada (no tiene sentido aquí, pero no rompe).")]
    public NPCEmotion emotion = NPCEmotion.Neutral;

    [Tooltip("Si es > 0, la cara vuelve a Neutral pasados esos segundos. 0 = se queda puesta hasta " +
             "que otro beat la cambie.")]
    public float revertAfter = 0f;

    public override string Describe() => $"Cara: {actorId} → {emotion}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor == null) yield break;

        actor.SetEmotion(emotion);

        if (revertAfter > 0f)
        {
            yield return new WaitForSeconds(revertAfter);
            actor.SetEmotion(NPCEmotion.Neutral);
        }
    }
}

/// Orienta a un actor: hacia otro actor, hacia una marca de posición, o justo al contrario.
[Serializable]
public class FaceBeat : SequenceBeat
{
    [Tooltip("Quién gira.")]
    public string actorId;

    [Tooltip("Hacia qué actor mira. Si se deja vacío, se usa 'markName'.")]
    public string targetActorId;

    [Tooltip("Hacia qué marca de posición del SequenceStage mira. Solo se usa si 'targetActorId' " +
             "está vacío.")]
    public string markName;

    [Tooltip("Si está marcado, mira justo al lado contrario (darle la espalda).")]
    public bool lookAway = false;

    [Tooltip("Si está marcado, el otro también gira hacia este — para dejarlos encarados con un " +
             "solo beat. Solo aplica cuando el objetivo es un actor.")]
    public bool mutual = false;

    [Tooltip("Segundos que tarda en girarse. 0 = instantáneo, que es lo correcto cuando el giro " +
             "ocurre con la pantalla cubierta o fuera de plano. En cámara vale la pena poner 0,3–0,5: " +
             "un personaje que cambia de orientación de golpe delante del espectador se lee como un " +
             "fallo. El tiempo es REAL, para que no se estire con la cámara lenta.")]
    public float turnDuration = 0f;

    public override string Describe()
        => $"Mirar: {actorId} → {(string.IsNullOrEmpty(targetActorId) ? markName : targetActorId)}"
           + (lookAway ? " (de espaldas)" : "") + (mutual ? " (mutuo)" : "");

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor?.Transform == null) yield break;

        SequenceActor targetActor = string.IsNullOrEmpty(targetActorId) ? null : ctx.GetActor(targetActorId);
        Vector3 point;

        if (targetActor?.Transform != null)
        {
            point = targetActor.Transform.position;
        }
        else
        {
            var mark = ctx.Stage != null ? ctx.Stage.GetMark(markName) : null;
            if (mark == null) yield break; // el aviso ya lo ha dado el stage
            point = mark.position;
        }

        if (lookAway) point = actor.Transform.position + (actor.Transform.position - point);

        if (mutual && targetActor?.Transform != null && actor.Transform != null)
            targetActor.Face(actor.Transform.position);

        if (turnDuration <= 0f)
        {
            actor.Face(point);
            yield break;
        }

        yield return Co_TurnSmoothly(actor.Transform, point, turnDuration);

        // Sin esto, ApplySmoothRotation arrastra al NPC de vuelta a la rotación que tuviera
        // apuntada el animador en cuanto termina el giro. Ver SequenceActor.Face (INC-299).
        actor.SyncRotation();
    }

    /// Gira poco a poco en lugar de encarar de golpe. Solo toca el eje vertical: inclinar a un
    /// personaje para mirar algo que está más alto o más bajo lo deja torcido.
    private static IEnumerator Co_TurnSmoothly(Transform who, Vector3 lookAtPoint, float duration)
    {
        Vector3 dir = lookAtPoint - who.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;

        Quaternion from = who.rotation;
        Quaternion to = Quaternion.LookRotation(dir.normalized, Vector3.up);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (who == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            who.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        if (who != null) who.rotation = to;
    }
}

/// Mueve a un actor. Siempre por el camino canónico (ver SequenceMovement): el NavMeshAgent
/// conduce y el animator lee su velocidad real. Nunca a mano.
[Serializable]
public class MoveToBeat : SequenceBeat
{
    [Tooltip("Quién se mueve.")]
    public string actorId;

    [Tooltip("Ir hasta este actor, parándose a 'stopDistance' y en el ángulo indicado. Si se deja " +
             "vacío, se usa 'markName'.")]
    public string towardsActorId;

    [Tooltip("Ir hasta esta marca de posición del SequenceStage. Solo se usa si 'towardsActorId' " +
             "está vacío.")]
    public string markName;

    [Tooltip("A qué distancia del otro actor se para. Solo aplica con 'towardsActorId'.")]
    public float stopDistance = 1.8f;

    [Tooltip("Ángulo (grados) alrededor del otro actor donde pararse, medido desde la dirección a " +
             "la que mira: 0 = justo enfrente (encarados), 90 = a su derecha, 180 = a su espalda. " +
             "Solo aplica con 'towardsActorId'.")]
    public float approachAngle = 0f;

    [Tooltip("Velocidad en m/s. 0 (recomendado) = usar la que tenga su NavMeshAgent, que es con la " +
             "que se mueve en el resto del juego y con la que está calibrada la animación. Subirla " +
             "solo si se ha verificado que el clip aguanta esa velocidad sin patinar.")]
    public float speedOverride = 0f;

    [Tooltip("Segundos máximos de trayecto antes de rendirse y seguir con la secuencia.")]
    public float timeout = 8f;

    [Tooltip("Al llegar, dejar a los dos encarados. Solo aplica con 'towardsActorId'.")]
    public bool faceEachOtherOnArrival = true;

    [Tooltip("Pausa corta al llegar, antes del siguiente beat. Deja que el crossfade a Idle se " +
             "asiente: sin ella, el siguiente gesto puede pedirse en el mismo frame y se ve como si " +
             "el actor siguiera caminando mientras ya está hablando.")]
    public float settleOnArrival = 0.3f;

    public override string Describe()
        => $"Mover: {actorId} → {(string.IsNullOrEmpty(towardsActorId) ? markName : towardsActorId)}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor == null) yield break;

        SequenceActor target = string.IsNullOrEmpty(towardsActorId) ? null : ctx.GetActor(towardsActorId);
        Vector3 destination;

        if (target?.Transform != null)
        {
            destination = SequenceMovement.PointAround(target.Transform, stopDistance, approachAngle);
        }
        else
        {
            var mark = ctx.Stage != null ? ctx.Stage.GetMark(markName) : null;
            if (mark == null) yield break; // el aviso ya lo ha dado el stage
            destination = mark.position;
        }

        yield return SequenceMovement.MoveTo(actor, destination, speedOverride, timeout);

        if (faceEachOtherOnArrival && target?.Transform != null && actor.Transform != null)
        {
            actor.Face(target.Transform.position);
            target.Face(actor.Transform.position);
        }

        if (settleOnArrival > 0f) yield return new WaitForSeconds(settleOnArrival);
    }
}

/// Retiene o suelta el comportamiento ambiental de un NPC (Wander/Idle).
///
/// Normalmente NO hace falta usarlo: el SequencePlayer retiene automáticamente a todos los actores
/// que aparecen en la secuencia al empezar, y los suelta al terminar. Este beat existe para los
/// casos concretos en que hace falta soltar a alguien antes de tiempo, o retener a un actor que la
/// secuencia no menciona en ningún otro beat.
[Serializable]
public class HoldActorBeat : SequenceBeat
{
    [Tooltip("A quién se retiene o se suelta.")]
    public string actorId;

    [Tooltip("Marcado = retener (sale de Wander/Idle y queda bajo control de la secuencia). " +
             "Desmarcado = soltar (recupera su comportamiento normal).")]
    public bool hold = true;

    public override string Describe() => (hold ? "Retener: " : "Soltar: ") + actorId;

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor == null) yield break;

        if (hold) actor.Hold();
        else actor.Release();

        yield break;
    }
}

/// Manda a un actor a esconderse detrás de lo primero que encuentre — un árbol, una roca, un muro.
///
/// ── Por qué busca la cobertura en vez de ir a un punto colocado ──────────────────────────────
/// Lo obvio sería poner una marca detrás del árbol y mandar al actor allí. Pero eso ata la escena
/// a ESE árbol, y el trabajo de esta semana ha ido justo en la dirección contraria: la cinemática
/// del Despertar ocurre donde esté el jugador cuando salte, no en un punto fijo del mapa. Una
/// marca fija devolvería el problema por la puerta de atrás.
///
/// Así que se busca a la redonda lo que haya, se descarta lo que no sirve (el suelo, los propios
/// personajes, lo que es demasiado bajo para esconderse) y se va al lado contrario a la amenaza.
/// Funciona en un bosque y funciona en un claro.
///
/// Si no encuentra nada — un prado sin un solo árbol a la redonda — el actor simplemente se aparta
/// corriendo en dirección contraria. No es tan bonito, pero es lo que haría cualquiera, y la
/// escena no se queda esperando a un árbol que no existe.
[Serializable]
public class TakeCoverBeat : SequenceBeat
{
    [Tooltip("Quién se esconde.")]
    public string actorId;

    [Tooltip("De qué se esconde. Se usa para elegir el lado del árbol y para saber hacia dónde " +
             "mirar al llegar. Normalmente 'Player' o el id del objeto peligroso.")]
    public string awayFromActorId;

    [Tooltip("A qué distancia se busca algo detrás de lo que esconderse. Más de 15 m queda raro: " +
             "el actor se va corriendo medio campo y la escena se para a esperarle.")]
    public float searchRadius = 14f;

    [Tooltip("Altura mínima, en metros, para que algo cuente como escondite. Por debajo de esto no " +
             "tapa a nadie — un arbusto o un bordillo no valen.")]
    public float minCoverHeight = 1.6f;

    [Tooltip("Velocidad a la que va, en m/s. 0 = su velocidad normal. Por encima de 4 el clip de " +
             "correr empieza a patinar, así que conviene no pasarse aunque el personaje vaya " +
             "presa del pánico.")]
    public float speed = 4f;

    [Tooltip("Segundos máximos antes de rendirse y seguir con la escena. Sin esto, un escondite al " +
             "que no se puede llegar dejaría la secuencia colgada.")]
    public float timeout = 5f;

    [Tooltip("Al llegar, girarse hacia la amenaza y asomarse. Es lo que convierte 'huir' en " +
             "'ponerse a cubierto y seguir mirando'.")]
    public bool peekAfterArriving = true;

    // Buffer pre-alocado: CLAUDE.md § 2 exige OverlapSphereNonAlloc, nunca la versión que reserva.
    private static readonly Collider[] s_candidates = new Collider[48];

    public override string Describe()
        => $"A cubierto: {actorId}" + (string.IsNullOrEmpty(awayFromActorId) ? "" : $" (huyendo de {awayFromActorId})");

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor?.Transform == null) yield break;

        var threat = string.IsNullOrEmpty(awayFromActorId) ? null : ctx.GetActor(awayFromActorId);
        Vector3 threatPos = threat?.Transform != null
            ? threat.Transform.position
            : actor.Transform.position + actor.Transform.forward;

        Vector3 destination = FindCoverSpot(actor, threatPos, out string what);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TakeCoverBeat] {actorId} se pone a cubierto {what}.");
#endif

        yield return SequenceMovement.MoveTo(actor, destination, speed, timeout);

        if (peekAfterArriving) actor.Face(threatPos);
    }

    /// Busca el mejor sitio donde esconderse y devuelve el punto al que ir.
    private Vector3 FindCoverSpot(SequenceActor actor, Vector3 threatPos, out string what)
    {
        Vector3 from = actor.Transform.position;

        int count = Physics.OverlapSphereNonAlloc(from, searchRadius, s_candidates, ~0,
            QueryTriggerInteraction.Ignore);

        Collider best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var c = s_candidates[i];
            if (c == null) continue;

            // Los personajes no son cobertura: ni el propio actor, ni la amenaza, ni nadie.
            if (c.transform.root.GetComponent<NPCSimpleAnimator>() != null) continue;

            Bounds b = c.bounds;

            // Demasiado bajo para tapar a nadie.
            if (b.size.y < minCoverHeight) continue;

            // Demasiado ancho: eso no es un árbol, es el terreno o el suelo de una casa. Esconderse
            // "detrás" de algo así no significa nada.
            float footprint = Mathf.Max(b.size.x, b.size.z);
            if (footprint > 18f) continue;

            Vector3 centre = b.center;
            centre.y = from.y;

            float toActor = Vector3.Distance(from, centre);
            float toThreat = Vector3.Distance(threatPos, centre);

            // Se quiere cerca del actor (para que llegue rápido) y lejos de la amenaza. Si el
            // escondite está más cerca del peligro que el propio actor, no es un escondite.
            if (toThreat < toActor * 0.6f) continue;

            float score = toActor - toThreat * 0.25f;
            if (score < bestScore) { bestScore = score; best = c; }
        }

        if (best == null)
        {
            // Sin nada a la redonda: apartarse corriendo, y ya.
            what = "apartándose (no hay nada detrás de lo que esconderse a la redonda)";
            Vector3 away = from - threatPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = actor.Transform.right;
            return Project(from + away.normalized * 6f);
        }

        Bounds cover = best.bounds;
        Vector3 coverCentre = cover.center;
        coverCentre.y = from.y;

        Vector3 behind = coverCentre - threatPos;
        behind.y = 0f;
        if (behind.sqrMagnitude < 0.01f) behind = coverCentre - from;
        behind.y = 0f;
        if (behind.sqrMagnitude < 0.01f) behind = Vector3.forward;

        float radius = Mathf.Max(cover.size.x, cover.size.z) * 0.5f;
        what = $"detrás de '{best.transform.root.name}'";

        return Project(coverCentre + behind.normalized * (radius + 0.9f));
    }

    /// Pega el punto al NavMesh. Un destino en el aire o dentro del tronco dejaría al agente
    /// intentando llegar a un sitio imposible hasta agotar el tiempo.
    private static Vector3 Project(Vector3 point)
        => NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : point;
}


/// Coloca a un actor en una marca, de golpe, sin caminar.
///
/// MoveToBeat es para que el espectador VEA a alguien ir de un sitio a otro. Esto es lo contrario:
/// es el equivalente de mover a un actor entre toma y toma. En una secuencia montada por cortes
/// — el prólogo salta de la panadería al río, del río al portón — hacer que un anciano cruce
/// andando el pueblo entre dos frases no es fidelidad, es tiempo muerto; y además obligaría a
/// bakear un NavMesh en una maqueta que nadie pisa.
///
/// Se usa SIEMPRE detrás de un corte de plano, nunca a la vista.
[Serializable]
public class PlaceAtMarkBeat : SequenceBeat
{
    [Tooltip("Quién se coloca.")]
    public string actorId;

    [Tooltip("La marca del SequenceStage donde se coloca.")]
    public string markName;

    [Tooltip("Además, dejarlo mirando hacia esta otra marca. Vacío = conserva la orientación de " +
             "la marca donde se coloca.")]
    public string faceTowardsMark;

    [Tooltip("O mirando hacia este actor. Tiene preferencia sobre 'faceTowardsMark'.")]
    public string faceTowardsActor;

    public override string Describe() => $"Colocar: {actorId} en '{markName}'";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var actor = ctx.GetActor(actorId);
        if (actor?.Transform == null) yield break;

        var mark = ctx.Stage != null ? ctx.Stage.GetMark(markName) : null;
        if (mark == null) yield break; // el aviso ya lo ha dado el stage

        actor.StopMovement();

        // Con NavMeshAgent hay que usar Warp: mover el transform a pelo deja al agente creyendo
        // que sigue donde estaba, y el siguiente movimiento lo teletransporta de vuelta.
        var agent = actor.Agent;
        // Warp falla (devuelve false) cuando en la marca no hay NavMesh, y entonces el actor se
        // queda donde estaba sin que nadie lo diga. Eso pasa en cuanto una marca sale del suelo
        // caminable — lo alto de una colina, un tejado, un saliente —, que es justo donde se pone a
        // alguien para que se le vea desde abajo. Si el Warp no puede, se apaga el agente y se
        // coloca el transform a pelo: un actor de cinemática no necesita navegar, necesita estar
        // donde dice la marca.
        bool colocado = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh) colocado = agent.Warp(mark.position);

        if (!colocado)
        {
            if (agent != null && agent.enabled) agent.enabled = false;
            actor.Transform.position = mark.position;
        }

        // OJO: las marcas se crean con `new GameObject(nombre)` y solo se les pone la posición,
        // así que su rotación es la identidad — el +Z del mundo. Colocar sin `faceTowards` deja al
        // actor mirando al norte del mundo, diga lo que diga la escena (INC-293). Se conserva
        // porque una marca CON rotación puesta a mano sí es una orientación deliberada, pero lo
        // normal es dar también un `faceTowardsActor` o `faceTowardsMark`.
        actor.Transform.rotation = mark.rotation;
        actor.SyncRotation();

        Transform objetivo = null;
        if (!string.IsNullOrWhiteSpace(faceTowardsActor))
            objetivo = ctx.GetActor(faceTowardsActor)?.Transform;
        if (objetivo == null && !string.IsNullOrWhiteSpace(faceTowardsMark) && ctx.Stage != null)
            objetivo = ctx.Stage.GetMark(faceTowardsMark);

        if (objetivo != null) actor.Face(objetivo.position);

        yield break;
    }
}
