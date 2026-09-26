using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Game.NPC.States;

/// Un NPC lleva al jugador andando hasta un SpawnAnchor mientras el jugador tiene el control.
///
/// El NPC camina delante. Si el jugador se queda atrás, se para, le espera y le dice la frase de
/// 'llamadaKey' en un bocadillo cada pocos segundos; sigue cuando llega. El nodo avanza cuando el NPC ha llegado a la marca y el
/// jugador está a 'radioLlegada' de él. El paseo lo hace CinematicState.LeadPlayerToAnchorSequence,
/// la misma escolta que usa el guardia del castillo.
[Serializable]
[NarrativeNodeInfo("NPCs", "Guiar al jugador", "Un NPC camina hasta una marca (SpawnAnchor) y el jugador le sigue con el control. Si el jugador se queda atrás, el NPC le espera y le llama. Avanza cuando los dos están en la marca.")]
public sealed class GuiarJugadorNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC que guía (p. ej. NPC_Eldran).")]
    public string npcId = "NPC_Eldran";

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("anchorId del SpawnAnchor al que lleva al jugador.")]
    public string anchorId;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Marcas (SpawnAnchor) por las que pasa antes de ir a 'anchorId', en orden. Sirven para que vaya por el camino que se quiere y no por el atajo que encuentre el NavMesh. Vacío = va directo.")]
    public List<string> puntosDePaso = new();

    [NarrativeKey(NarrativeKeyKind.LocKey)]
    [Tooltip("Lo que dice el NPC, en bocadillo, cuando vuelve a por el jugador. Vacío = no dice nada.")]
    public string llamadaKey = "EVT_ELDRAN_GUIA_LLAMADA";

    [Min(0.5f)]
    [Tooltip("Segundos que dura el bocadillo de la llamada.")]
    public float duracionLlamada = 2.2f;

    [Min(2f)]
    [Tooltip("Si el jugador se queda más atrás que esto, en metros, el NPC se para a esperarle.")]
    public float distanciaMaxima = 9f;

    [Min(1f)]
    [Tooltip("Cuando el jugador llega a esta distancia, en metros, el NPC sigue andando.")]
    public float distanciaReanudar = 3f;

    [Min(1f)]
    [Tooltip("Cuando el NPC ya está en la marca, el nodo avanza en cuanto el jugador llega a esta distancia de él, en metros.")]
    public float radioLlegada = 4f;

    [Min(1f)]
    [Tooltip("Velocidad, en m/s, a la que anda el NPC mientras guía. Si su velocidad normal es mayor, se usa esa.")]
    public float velocidad = 2.6f;

    [Min(10f)]
    [Tooltip("Tope de segundos del paseo. Si se agota, el NPC se queda donde esté y se sigue esperando a que llegue el jugador.")]
    public float duracionMaxima = 240f;

    [NonSerialized] private Coroutine _rutina;
    [NonSerialized] private NarrativeRunner _runner;
    [NonSerialized] private UnityEngine.AI.NavMeshAgent _agenteTocado;
    [NonSerialized] private float _velocidadOriginal;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var npc = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(npcId) : null;
        var anchor = string.IsNullOrWhiteSpace(anchorId) ? null : SpawnAnchor.FindById(anchorId);
        var player = PlayerService.PlayerTransform;

        if (npc == null || anchor == null || player == null || ctx?.Runner == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[GuiarJugadorNode:{guid}] No se puede guiar: NPC '{npcId}'={(npc != null ? "ok" : "NO")}, " +
                $"marca '{anchorId}'={(anchor != null ? "ok" : "NO")}, jugador={(player != null ? "ok" : "NO")}. Se avanza para no bloquear.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        _runner = ctx.Runner;
        _rutina = _runner.StartCoroutine(Co_Guiar(npc, anchor.transform.position, player, onReadyToAdvance));
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_rutina != null && _runner != null) _runner.StopCoroutine(_rutina);
        _rutina = null;
        _runner = null;
        DevolverVelocidad();
    }

    private IEnumerator Co_Guiar(NPCBehaviourManagerV2 npc, Vector3 destino, Transform player, Action onReadyToAdvance)
    {
        var agent = npc.Agent;
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            yield return null;
        }

        var anim = npc.SimpleAnimator;
        if (anim != null)
        {
            anim.AllowManualRotation = false;
            anim.EnableAutoRotation();
        }

        // La marca puede quedar a un palmo del NavMesh (un escalón, una raíz): se usa el punto
        // navegable más cercano para que el NPC pueda terminar el camino.
        if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var navHit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            destino = navHit.position;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
            Debug.LogWarning($"[GuiarJugadorNode:{guid}] La marca '{anchorId}' no tiene NavMesh a menos de 10 m: " +
                $"'{npcId}' no podrá llegar. Hay que ampliar el NavMesh hasta ahí o mover la marca.");
#endif

        // El agente tiene que mover el cuerpo: si otro estado lo dejó desenganchado
        // (updatePosition = false), el agente andaría solo por dentro y el NPC se quedaría quieto.
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.nextPosition = npc.transform.position;
            agent.updatePosition = true;
        }

        if (agent != null)
        {
            _agenteTocado = agent;
            _velocidadOriginal = agent.speed;
            agent.speed = Mathf.Max(agent.speed, velocidad);
        }
        var paseo = new LeadPlayerToAnchorSequence(Ruta(destino), player, duracionMaxima, distanciaMaxima, distanciaReanudar);
        npc.StartCinematicSequence(paseo);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnóstico de INC-461 (Eldran no echa a andar). Se quita al cerrar la incidencia.
        float siguienteDiag = 0f;
        _diagRutaAnterior = null;
        _diagEsquivando = false;
#endif
        while (!paseo.IsCompleted)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.unscaledTime >= siguienteDiag)
            {
                siguienteDiag = Time.unscaledTime + 3f;
                Diagnostico(npc, destino, "andando");
            }
            DiagnosticoRuta(npc, destino);
#endif
            if (paseo.WaitingForRetrievedDialogue)
            {
                yield return Co_Llamar(npc, destino);
                paseo.AcknowledgePlayerRetrieved(npc.Context);
            }
            yield return null;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Diagnostico(npc, destino, "paseo terminado");
#endif

        if (anim != null) anim.AllowManualRotation = true;
        DevolverVelocidad();

        // Ya en la marca, espera a que el jugador llegue hasta él.
        float radio2 = radioLlegada * radioLlegada;
        while (player != null && (player.position - npc.transform.position).sqrMagnitude > radio2)
            yield return null;

        if (player != null && anim != null) anim.FaceTarget(player.position);

        _rutina = null;
        onReadyToAdvance?.Invoke();
    }

    /// Los puntos de paso que existen, sobre el NavMesh, y el destino al final.
    private List<Vector3> Ruta(Vector3 destino)
    {
        var ruta = new List<Vector3>();
        if (puntosDePaso != null)
        {
            foreach (var id in puntosDePaso)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                var marca = SpawnAnchor.FindById(id);
                if (marca == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[GuiarJugadorNode:{guid}] El punto de paso '{id}' no está en la escena: se lo salta.");
#endif
                    continue;
                }
                Vector3 p = marca.transform.position;
                if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                    p = hit.position;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else
                    Debug.LogWarning($"[GuiarJugadorNode:{guid}] El punto de paso '{id}' no tiene NavMesh a menos de 4 m: muévelo al camino.");
#endif
                ruta.Add(p);
            }
        }
        ruta.Add(destino);
        return ruta;
    }

    private void DevolverVelocidad()
    {
        if (_agenteTocado != null) _agenteTocado.speed = _velocidadOriginal;
        _agenteTocado = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Diagnostico(NPCBehaviourManagerV2 npc, Vector3 destino, string momento)
    {
        var a = npc.Agent;
        string estado = npc.Brain?.CurrentState?.GetType().Name ?? "?";
        string ag = a == null ? "sin agente" :
            $"enabled={a.enabled} onNavMesh={(a.enabled && a.isOnNavMesh)} " +
            (a.enabled && a.isOnNavMesh
                ? $"isStopped={a.isStopped} updatePosition={a.updatePosition} speed={a.speed:F2} vel={a.velocity.magnitude:F2} " +
                  $"hasPath={a.hasPath} pending={a.pathPending} status={a.pathStatus} restante={a.remainingDistance:F1} " +
                  $"destinoAgente={a.destination} posInterna={a.nextPosition}"
                : "");
        var jugador = PlayerService.PlayerTransform;
        string jug = jugador == null ? "jugador=?" :
            $"jugador={jugador.position} aNPC={Vector3.Distance(jugador.position, npc.transform.position):F1} " +
            $"aMarca={Vector3.Distance(jugador.position, destino):F1}";
        Debug.Log($"[GuiarJugadorNode:DIAG] {momento}: '{npcId}' estado={estado} pos={npc.transform.position} " +
                  $"marca={destino} dist={Vector3.Distance(npc.transform.position, destino):F1} | {jug} | {ag}");
    }

    // Diagnóstico de INC-465 («da un rodeo»): cada ruta nueva que calcula el agente sale entera en
    // la consola (esquinas, largo frente a línea recta) y dibujada en la vista Scene durante dos
    // minutos. Y cada vez que NPCObstacleAvoidance le desvía, qué objeto lo ha provocado.
    [NonSerialized] private float _diagRutaSiguiente;
    [NonSerialized] private Vector3[] _diagRutaAnterior;
    [NonSerialized] private bool _diagEsquivando;

    private void DiagnosticoRuta(NPCBehaviourManagerV2 npc, Vector3 destino)
    {
        var a = npc.Agent;
        if (a == null || !a.enabled || !a.isOnNavMesh) return;

        var esquiva = npc.GetComponent<NPCObstacleAvoidance>();
        bool esquivando = esquiva != null && esquiva.Esquivando;
        if (esquivando && !_diagEsquivando)
        {
            var col = esquiva.UltimoObstaculo;
            string quien = col == null ? "?" : $"'{RutaDe(col.transform)}' ({col.GetType().Name}, layer '{LayerMask.LayerToName(col.gameObject.layer)}')";
            Debug.Log($"[GuiarJugadorNode:ESQUIVA] '{npcId}' en {npc.transform.position} se desvía de su ruta para no chocar con {quien}.");
        }
        _diagEsquivando = esquivando;

        if (Time.unscaledTime < _diagRutaSiguiente || a.pathPending || !a.hasPath) return;
        _diagRutaSiguiente = Time.unscaledTime + 0.5f;

        var c = a.path.corners;
        if (c.Length < 2) return;
        // Es la misma ruta si lo que queda por delante es la cola de la anterior: el agente va
        // quitando las esquinas que pasa, y la primera es siempre su propia posición.
        bool nueva = !EsColaDe(c, _diagRutaAnterior);
        _diagRutaAnterior = c;
        if (!nueva) return;

        float largo = 0f;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < c.Length; i++)
        {
            if (i > 0)
            {
                largo += Vector3.Distance(c[i - 1], c[i]);
                Debug.DrawLine(c[i - 1] + Vector3.up * 0.2f, c[i] + Vector3.up * 0.2f, Color.magenta, 120f);
                sb.Append(" → ");
            }
            sb.Append($"({c[i].x:F1}, {c[i].z:F1})");
        }
        float recto = Vector3.Distance(npc.transform.position, destino);
        float ratio = recto > 0.1f ? largo / recto : 1f;
        string aviso = ratio > 1.3f ? "  ⚠ RODEO" : "";
        Debug.Log($"[GuiarJugadorNode:RUTA] '{npcId}' ruta nueva: {c.Length} esquinas, {largo:F1} m para {recto:F1} m en línea recta (x{ratio:F2}){aviso}. {sb}");
    }

    private static bool EsColaDe(Vector3[] actual, Vector3[] anterior)
    {
        if (anterior == null || actual.Length > anterior.Length) return false;
        int desfase = anterior.Length - actual.Length;
        for (int k = 1; k < actual.Length; k++)
            if ((actual[k] - anterior[desfase + k]).sqrMagnitude > 0.09f) return false;
        return true;
    }

    private static string RutaDe(Transform t)
    {
        var partes = new System.Collections.Generic.List<string>();
        for (var x = t; x != null && partes.Count < 4; x = x.parent) partes.Add(x.name);
        partes.Reverse();
        return string.Join("/", partes);
    }
#endif

    private IEnumerator Co_Llamar(NPCBehaviourManagerV2 npc, Vector3 destino)
    {
        var ui = SpeechBubbleUI.Instance;
        if (ui == null || string.IsNullOrEmpty(llamadaKey)) yield break;

        // Si el jugador va por delante, camino del destino, «¡Por aquí!» no tiene sentido.
        var player = PlayerService.PlayerTransform;
        if (player != null &&
            (player.position - destino).sqrMagnitude < (npc.transform.position - destino).sqrMagnitude)
            yield break;

        if (player != null) npc.SimpleAnimator?.FaceTarget(player.position);

        string texto = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(llamadaKey, llamadaKey)
            : llamadaKey;

        bool terminado = false;
        ui.Show(npc.transform, texto, duracionLlamada, () => terminado = true);
        while (!terminado) yield return null;
    }
}
