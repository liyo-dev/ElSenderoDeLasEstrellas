using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Game.NPC.States;

/// Un NPC lleva al jugador andando hasta un SpawnAnchor mientras el jugador tiene el control.
///
/// El NPC camina delante. Si el jugador se queda atrás, se para, le espera y le dice la frase de
/// 'llamadaKey' en un bocadillo cada pocos segundos; sigue cuando llega. Mientras el jugador va
/// cerca, no puede ir más rápido que el NPC (así no le adelanta, INC-536), y si hay 'charla', la
/// van diciendo en bocadillos por el camino (INC-537). El nodo avanza cuando el NPC ha llegado a
/// la marca, el jugador está a 'radioLlegada' de él y la charla ha terminado. Ya en la marca, el
/// NPC se vuelve hacia el jugador y le sigue con la mirada mientras llega y terminan la charla. El
/// paseo lo hace CinematicState.LeadPlayerToAnchorSequence; la llamada y la charla salen por
/// PresentadorDeLinea. Al acabar, el animador del NPC queda con la rotación como la tenía antes.
[Serializable]
[NarrativeNodeInfo("NPCs", "Guiar al jugador", "Un NPC camina hasta una marca (SpawnAnchor) y el jugador le sigue con el control, sin poder adelantarle. Si el jugador se queda atrás, el NPC le espera y le llama. Por el camino pueden ir charlando (bocadillos). Avanza cuando los dos están en la marca y la charla ha terminado.")]
public sealed class GuiarJugadorNode : NarrativeNode, INarrativeStateEffect
{
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC que guía.")]
    public string npcId = "";

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("anchorId del SpawnAnchor al que lleva al jugador.")]
    public string anchorId;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Marcas (SpawnAnchor) por las que pasa antes de ir a 'anchorId', en orden. Sirven para que vaya por el camino que se quiere y no por el atajo que encuentre el NavMesh. Vacío = va directo.")]
    public List<string> puntosDePaso = new();

    [NarrativeKey(NarrativeKeyKind.LocKey)]
    [Tooltip("Lo que dice el NPC, en bocadillo, cuando vuelve a por el jugador. Vacío = no dice nada.")]
    public string llamadaKey = "";

    [Min(0.5f)]
    [Tooltip("Segundos que dura el bocadillo de la llamada.")]
    public float duracionLlamada = 2.2f;

    [Min(2f)]
    [Tooltip("Si el jugador se queda más atrás que esto, en metros, el NPC se para a esperarle.")]
    public float distanciaMaxima = 9f;

    [Min(1f)]
    [Tooltip("Cuando el jugador llega a esta distancia, en metros, el NPC sigue andando.")]
    public float distanciaReanudar = 3f;

    [Min(0.1f)]
    [Tooltip("Espacio que el jugador conserva detrás del guía mientras camina, en metros.")]
    public float separacionDelGuia = 1.2f;

    [Min(1f)]
    [Tooltip("Cuando el NPC ya está en la marca, el nodo avanza en cuanto el jugador llega a esta distancia de él, en metros.")]
    public float radioLlegada = 4f;

    [Min(1f)]
    [Tooltip("Velocidad, en m/s, a la que anda el NPC mientras guía. Si su velocidad normal es mayor, se usa esa.")]
    public float velocidad = 3f;

    [Min(10f)]
    [Tooltip("Tope de segundos del paseo. Si se agota, el NPC se queda donde esté y se sigue esperando a que llegue el jugador.")]
    public float duracionMaxima = 240f;

    [Tooltip("Lo que se cuentan por el camino: un diálogo normal (hablante y texto por línea) que sale en bocadillos sobre quien habla mientras el NPC guía. Se pausa si el NPC se detiene a esperar al jugador o hay un menú abierto. Si llegan antes de acabarlo, lo terminan allí. Vacío = van en silencio.")]
    public DialogueAsset charla;

    // Mientras sigue al NPC, el jugador va como mucho a la velocidad del NPC. No le adelanta
    // porque TopeDeVelocidadDelJugador conserva la separación detrás del guía (INC-736).
    private const float FraccionVelocidadJugador = 1f;
    [NonSerialized] private Coroutine _rutina;
    [NonSerialized] private NarrativeRunner _runner;
    [NonSerialized] private UnityEngine.AI.NavMeshAgent _agenteTocado;
    [NonSerialized] private float _velocidadOriginal;
    [NonSerialized] private Coroutine _rutinaCharla;
    [NonSerialized] private CharlaEnBocadillos _charla;
    [NonSerialized] private bool _topePuesto;
    [NonSerialized] private Vector3 _direccionGuia;
    [NonSerialized] private NPCBehaviourManagerV2 _npcEscortado;
    [NonSerialized] private NPCSimpleAnimator _animTocado;
    [NonSerialized] private bool _rotacionManualPrevia;
    [NonSerialized] private bool _updatePositionOriginal;

    /// El actor acaba en la marca de destino.
    public void Project(INarrativeStateWriter state)
    {
        if (!string.IsNullOrWhiteSpace(npcId) && !string.IsNullOrWhiteSpace(anchorId))
            state.PlaceActor(npcId, NarrativeLocation.World(anchorId));
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var npc = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(npcId) : null;
        var anchor = string.IsNullOrWhiteSpace(anchorId) ? null : SpawnAnchor.FindById(anchorId);
        var player = PlayerService.PlayerTransform;

        if (npc == null || anchor == null || player == null || ctx?.Runner == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
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
        if (_rutinaCharla != null && _runner != null) _runner.StopCoroutine(_rutinaCharla);
        _rutina = null;
        _rutinaCharla = null;
        _runner = null;
        _charla?.Callar();
        _charla = null;

        if (_npcEscortado != null)
        {
            if (_npcEscortado.Brain?.CurrentState is CinematicState cs)
                cs.CancelSequence();
            if (_animTocado != null) _animTocado.AllowManualRotation = _rotacionManualPrevia;
            if (_agenteTocado != null) _agenteTocado.updatePosition = _updatePositionOriginal;
            _npcEscortado = null;
            _animTocado = null;
        }

        DevolverVelocidad();
        QuitarTope();
    }

    private IEnumerator Co_Guiar(NPCBehaviourManagerV2 npc, Vector3 destino, Transform player, Action onReadyToAdvance)
    {
        var agent = npc.Agent;
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            yield return null;
        }

        _npcEscortado = npc;

        var anim = npc.SimpleAnimator;
        if (anim != null)
        {
            _animTocado = anim;
            _rotacionManualPrevia = anim.AllowManualRotation;
            anim.AllowManualRotation = false;
            anim.EnableAutoRotation();
        }

        // La marca puede quedar a un palmo del NavMesh (un escalón, una raíz): se usa el punto
        // navegable más cercano para que el NPC pueda terminar el camino.
        if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var navHit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            destino = navHit.position;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
            Debug.LogWarning($"[GuiarJugadorNode:{guid}] La marca '{anchorId}' no tiene NavMesh a menos de 10 m: " +
                $"'{npcId}' no podrá llegar. Hay que ampliar el NavMesh hasta ahí o mover la marca.");
#endif

        // El agente tiene que mover el cuerpo: si otro estado lo dejó desenganchado
        // (updatePosition = false), el agente andaría solo por dentro y el NPC se quedaría quieto.
        if (agent != null && agent.enabled)
        {
            _updatePositionOriginal = agent.updatePosition;
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

        if (charla != null)
        {
            _charla = new CharlaEnBocadillos(charla, npc);
            _rutinaCharla = _runner.StartCoroutine(_charla.Co_Hablar(
                () => player != null && !paseo.WaitingForRetrievedDialogue && !MenuManager.AnyOpen(),
                pausaInicial: 1f));
        }

        while (!paseo.IsCompleted)
        {
            LimitarJugador(npc, player, agent);
            if (paseo.WaitingForRetrievedDialogue)
            {
                yield return Co_Llamar(npc, destino);
                paseo.AcknowledgePlayerRetrieved(npc.Context);
            }
            yield return null;
        }

        float velocidadGuia = agent != null ? agent.speed : velocidad;
        DevolverVelocidad();
        QuitarTope();

        // Ya en la marca, espera a que el jugador llegue hasta él mirándole.
        if (anim != null) anim.EnableAutoRotation();
        float radio2 = radioLlegada * radioLlegada;
        while (player != null && (player.position - npc.transform.position).sqrMagnitude > radio2)
        {
            LimitarJugador(npc, player, velocidadGuia);
            MirarAlJugador(npc, player);
            yield return null;
        }
        QuitarTope();

        // Si llegan antes de acabar la charla, la terminan allí, cara a cara.
        while (_charla != null && !_charla.Terminada)
        {
            MirarAlJugador(npc, player);
            yield return null;
        }
        MirarAlJugador(npc, player);
        _rutinaCharla = null;
        _charla = null;

        if (anim != null) anim.AllowManualRotation = _rotacionManualPrevia;
        _npcEscortado = null;
        _animTocado = null;
        _rutina = null;
        onReadyToAdvance?.Invoke();
    }

    /// Gira al NPC hacia el jugador mientras está parado; si se está moviendo, manda la marcha.
    private static void MirarAlJugador(NPCBehaviourManagerV2 npc, Transform player)
    {
        if (player == null || npc == null) return;
        var agent = npc.Agent;
        if (agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.04f) return;
        npc.SimpleAnimator?.FaceTarget(player.position);
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
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.LogWarning($"[GuiarJugadorNode:{guid}] El punto de paso '{id}' no está en la escena: se lo salta.");
#endif
                    continue;
                }
                Vector3 p = marca.transform.position;
                if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                    p = hit.position;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                else
                    Debug.LogWarning($"[GuiarJugadorNode:{guid}] El punto de paso '{id}' no tiene NavMesh a menos de 4 m: muévelo al camino.");
#endif
                ruta.Add(p);
            }
        }
        ruta.Add(destino);
        return ruta;
    }

    private void LimitarJugador(NPCBehaviourManagerV2 npc, Transform player, UnityEngine.AI.NavMeshAgent agent)
    {
        bool cerca = player != null && (player.position - npc.transform.position).sqrMagnitude <= distanciaMaxima * distanciaMaxima;
        if (player == null) { QuitarTope(); return; }
        Vector3 direccion = agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath
            ? agent.steeringTarget - npc.transform.position : _direccionGuia;
        direccion.y = 0f;
        if (direccion.sqrMagnitude > 0.01f) _direccionGuia = direccion.normalized;
        if (_direccionGuia.sqrMagnitude < 0.01f) _direccionGuia = npc.transform.forward;
        float avance = agent != null ? Mathf.Max(0f, Vector3.Dot(agent.velocity, _direccionGuia)) : 0f;
        float tope = cerca ? (agent != null ? agent.speed : velocidad) * FraccionVelocidadJugador : float.PositiveInfinity;
        TopeDeVelocidadDelJugador.PonerSeguimiento(this, tope,
            npc.transform, _direccionGuia, avance, separacionDelGuia);
        _topePuesto = true;
    }

    /// Cerca del NPC, el jugador no va más rápido que él (no le adelanta). Lejos, va a su paso
    /// normal para poder alcanzarle.
    private void LimitarJugador(NPCBehaviourManagerV2 npc, Transform player, float velocidadNpc)
    {
        bool cerca = player != null &&
                     (player.position - npc.transform.position).sqrMagnitude <= distanciaMaxima * distanciaMaxima;
        // En el destino se permite acercarse por cualquier lado para cerrar el paseo.
        if (cerca) TopeDeVelocidadDelJugador.Poner(this, velocidadNpc * FraccionVelocidadJugador);
        else TopeDeVelocidadDelJugador.Quitar(this);
        _topePuesto = cerca;
    }

    private void QuitarTope()
    {
        if (!_topePuesto) return;
        TopeDeVelocidadDelJugador.Quitar(this);
        _topePuesto = false;
        _direccionGuia = Vector3.zero;
    }

    private void DevolverVelocidad()
    {
        if (_agenteTocado != null) _agenteTocado.speed = _velocidadOriginal;
        _agenteTocado = null;
    }

    private IEnumerator Co_Llamar(NPCBehaviourManagerV2 npc, Vector3 destino)
    {
        if (string.IsNullOrEmpty(llamadaKey) || !PresentadorDeLinea.Disponible) yield break;

        // Si el jugador va por delante, camino del destino, «¡Por aquí!» no tiene sentido.
        var player = PlayerService.PlayerTransform;
        if (player != null &&
            (player.position - destino).sqrMagnitude < (npc.transform.position - destino).sqrMagnitude)
            yield break;

        if (player != null) npc.SimpleAnimator?.FaceTarget(player.position);

        bool terminado = false;
        var dicha = PresentadorDeLinea.Decir(new FraseADecir
        {
            clave = llamadaKey,
            ancla = npc.transform,
            duracion = duracionLlamada,
            alTerminar = () => terminado = true,
        });
        if (!dicha.Dicha) yield break;
        // La llamada termina cuando se esconde sola o cuando otra frase la pisa (entonces su aviso
        // no llega).
        while (!terminado && PresentadorDeLinea.TurnoActual == dicha.Turno) yield return null;
    }
}
