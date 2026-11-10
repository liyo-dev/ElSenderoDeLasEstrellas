using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Game.NPC.States;
using Sendero.Core.Feedback;

/// Un NPC recorre una ruta hasta una marca y el jugador le sigue sin que le vea.
///
/// Mientras dura, el grupo va agachado y más despacio que el NPC (<see cref="SigiloDelGrupo"/>).
/// El NPC vigila con la vista (un cono por delante que tapan el escenario y los
/// <see cref="Escondite"/>) y con el oído (de cerca, si el jugador no está escondido). En los
/// puntos de 'paradasParaMirar' se para, hace un gesto y mira hacia atrás unos segundos. Si nota
/// algo, se para y se vuelve hacia el jugador; si le sigue viendo 'segundosHastaVerle', le ha
/// visto. El aviso es el giro: no dice nada. Si el jugador se queda muy atrás, le espera sin
/// llamarle. Sale por «Llega» cuando el NPC está en la marca y el jugador cerca, y por «Le ve»
/// si le descubre.
///
/// Con 'salidaDelNpc' y 'salidaDelJugador', al empezar pone a cada uno en su marca si está a más
/// de 'distanciaParaRecolocar' de ella (al repetir el tramo después de perder), tapando el cambio
/// con un fundido.
[Serializable]
[NarrativeNodeInfo("NPCs", "Seguir sin ser visto", "Un NPC camina por una ruta hasta una marca y el jugador le sigue a escondidas, agachado. El NPC mira atrás en algunos puntos y se gira si oye o ve algo. Sale por «Llega» si llega sin verle y por «Le ve» si le descubre.")]
public sealed class SeguirSinSerVistoNode : NarrativeNode, INarrativeStateEffect
{
    private static readonly string[] Puertos = { "Llega", "Le ve" };
    private const int PuertoLlega = 0;
    private const int PuertoLeVe = 1;

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC al que se sigue.")]
    public string npcId = "";

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Marca (SpawnAnchor) a la que va el NPC.")]
    public string anchorId = "";

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Marcas por las que pasa antes de llegar, en orden.")]
    public List<string> puntosDePaso = new();

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Puntos de paso (de la lista de arriba) en los que se para a mirar hacia atrás.")]
    public List<string> paradasParaMirar = new();

    [Min(0.5f)]
    [Tooltip("Segundos que se queda mirando atrás en cada parada.")]
    public float segundosMirando = 3f;

    [Min(0.5f)]
    [Tooltip("Velocidad del NPC, en m/s.")]
    public float velocidad = 2.2f;

    [Min(0.5f)]
    [Tooltip("Velocidad máxima del jugador agachado, en m/s. Siempre menor que la del NPC.")]
    public float velocidadDelJugador = 1.7f;

    [Min(1f)]
    [Tooltip("Hasta dónde ve el NPC, en metros.")]
    public float alcanceDeLaVista = 14f;

    [Range(20f, 220f)]
    [Tooltip("Abertura del cono de la vista, en grados.")]
    public float anguloDeLaVista = 110f;

    [Min(0f)]
    [Tooltip("A menos de esto, en metros, el NPC oye al jugador si no está escondido.")]
    public float distanciaDeOido = 4f;

    [Min(0.1f)]
    [Tooltip("Segundos seguidos viendo al jugador hasta que le descubre.")]
    public float segundosHastaVerle = 1f;

    [Min(5f)]
    [Tooltip("Si el jugador se queda más atrás que esto, en metros, el NPC se para a esperarle.")]
    public float distanciaParaEsperar = 30f;

    [Min(1f)]
    [Tooltip("En la marca, el tramo acaba cuando el jugador está a esta distancia del NPC, en metros.")]
    public float radioDeLlegada = 25f;

    [Min(10f)]
    [Tooltip("Tope de segundos del recorrido. Si se agota, el NPC se queda donde esté y el tramo acaba por «Llega».")]
    public float duracionMaxima = 420f;

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Marca donde empieza el NPC. Vacío = donde esté.")]
    public string salidaDelNpc = "";

    [NarrativeKey(NarrativeKeyKind.Anchor)]
    [Tooltip("Marca donde empieza el jugador. Vacío = donde esté.")]
    public string salidaDelJugador = "";

    [Min(1f)]
    [Tooltip("Al empezar, se pone en su salida a quien esté más lejos que esto de ella, en metros (al repetir el tramo tras perder). Más cerca, sigue desde donde está.")]
    public float distanciaParaRecolocar = 3f;

    [Tooltip("Música mientras dura el tramo. Al acabar suena la del lugar. Vacío = no cambia.")]
    public AudioClip musica;

    [Min(0f)]
    public float fundidoDeMusica = 1.5f;

    private enum Fase { Andando, Esperando, Parada, Sospecha }

    // Distancia a la que se dejan atrás los puntos de paso y se da por llegado a la marca.
    private const float RadioDePunto = 1.5f;
    private const float RadioDeMarca = 0.9f;
    private const float IntervaloDeVigilancia = 0.1f;
    private const float IntervaloDeRefresco = 0.5f;
    private const float SegundosDelGesto = 0.8f;
    private const float SegundosMinimosDeSospecha = 1.2f;
    private const float SegundosAlDescubrirle = 0.8f;
    private const float Fundido = 0.5f;
    // Al empezar, el NPC toma distancia antes de vigilar: suele salir de una cinemática con el
    // jugador al lado.
    private const float SegundosDeVentaja = 4f;

    [NonSerialized] private Coroutine _rutina;
    [NonSerialized] private NarrativeRunner _runner;
    [NonSerialized] private NPCBehaviourManagerV2 _npc;
    [NonSerialized] private ControlDelNodo _control;
    [NonSerialized] private NavMeshAgent _agente;
    [NonSerialized] private float _velocidadOriginal;
    [NonSerialized] private bool _updatePositionOriginal;
    [NonSerialized] private bool _rotacionManualPrevia;
    [NonSerialized] private bool _enSigilo;
    [NonSerialized] private bool _conMusica;

    public override string[] GetOutputPorts() => Puertos;

    /// La proyección supone que llega sin que le vean.
    public override int ProjectionPort => PuertoLlega;

    public override string DescribeDecision()
        => $"Seguir sin ser visto \"{displayTitle}\" (guid {guid}): ¿le descubre '{npcId}'? Salidas: Llega / Le ve.";

    /// El actor acaba en la marca de destino.
    public void Project(INarrativeStateWriter state)
    {
        if (!string.IsNullOrWhiteSpace(npcId) && !string.IsNullOrWhiteSpace(anchorId))
            state.PlaceActor(npcId, NarrativeLocation.World(anchorId));
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(npcId)) warnings.Add("Falta el NPC al que se sigue.");
        if (string.IsNullOrWhiteSpace(anchorId)) warnings.Add("Falta la marca a la que va el NPC.");
        if (velocidadDelJugador >= velocidad) warnings.Add("El jugador debe ir más despacio que el NPC al que sigue.");
        foreach (var p in paradasParaMirar)
            if (!string.IsNullOrWhiteSpace(p) && !puntosDePaso.Contains(p))
                warnings.Add($"La parada '{p}' no está entre los puntos de paso.");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var npc = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(npcId) : null;
        var marca = string.IsNullOrWhiteSpace(anchorId) ? null : SpawnAnchor.FindById(anchorId);
        var jugador = PlayerService.PlayerTransform;
        if (npc == null || marca == null || jugador == null || ctx?.Runner == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[SeguirSinSerVistoNode:{guid}] No se puede empezar: NPC '{npcId}'={(npc != null ? "ok" : "NO")}, " +
                $"marca '{anchorId}'={(marca != null ? "ok" : "NO")}, jugador={(jugador != null ? "ok" : "NO")}. Sigue por «Llega» para no bloquear.");
#endif
            AdvanceThrough(ctx, onReadyToAdvance, PuertoLlega);
            return;
        }

        _runner = ctx.Runner;
        _rutina = _runner.StartCoroutine(Co_Seguir(ctx, npc, marca.transform.position, jugador, onReadyToAdvance));
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_rutina != null && _runner != null) _runner.StopCoroutine(_rutina);
        _rutina = null;
        Soltar();
    }

    private IEnumerator Co_Seguir(NarrativeContext ctx, NPCBehaviourManagerV2 npc, Vector3 destino, Transform jugador, Action onReadyToAdvance)
    {
        yield return Co_PonerEnLaSalida(npc, jugador);

        _npc = npc;
        _agente = npc.Agent;
        if (_agente != null && !_agente.enabled)
        {
            _agente.enabled = true;
            yield return null;
        }

        var anim = npc.SimpleAnimator;
        if (anim != null)
        {
            _rotacionManualPrevia = anim.AllowManualRotation;
            anim.AllowManualRotation = false;
            anim.EnableAutoRotation();
        }

        if (musica != null && NarrativeServices.TryGet(out INarrativeAudio audio, nameof(SeguirSinSerVistoNode)))
        {
            audio.PlayMusic(musica, fundidoDeMusica);
            _conMusica = true;
        }

        SigiloDelGrupo.Empezar(this, Mathf.Min(velocidadDelJugador, velocidad * 0.85f));
        _enSigilo = true;

        _control = new ControlDelNodo();
        npc.StartCinematicSequence(_control);
        yield return null;

        if (_agente == null || !_agente.enabled || !_agente.isOnNavMesh)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[SeguirSinSerVistoNode:{guid}] '{npcId}' no tiene NavMeshAgent sobre el NavMesh. Sigue por «Llega».");
#endif
            Terminar(ctx, onReadyToAdvance, PuertoLlega);
            yield break;
        }

        _velocidadOriginal = _agente.speed;
        _updatePositionOriginal = _agente.updatePosition;
        _agente.nextPosition = npc.transform.position;
        _agente.updatePosition = true;
        _agente.speed = velocidad;

        var ruta = Ruta(destino, out var paradas);
        int tramo = 0;
        bool[] paradaHecha = new bool[ruta.Count];
        Andar(ruta[tramo]);

        var fase = Fase.Andando;
        float limite = Time.time + duracionMaxima;
        float finDeFase = 0f;
        float inicioDeFase = Time.time;
        float sospecha = 0f;
        float siguienteVigilancia = Time.time + SegundosDeVentaja;
        float siguienteRefresco = 0f;
        bool ve = false, oye = false;
        Vector3 haciaAtras = -npc.transform.forward;

        while (Time.time < limite)
        {
            float dt = Time.deltaTime;
            if (Time.time >= siguienteRefresco)
            {
                siguienteRefresco = Time.time + IntervaloDeRefresco;
                SigiloDelGrupo.Refrescar();
            }
            if (Time.time >= siguienteVigilancia)
            {
                siguienteVigilancia = Time.time + IntervaloDeVigilancia;
                ve = VistaDelVigia.VeAlJugador(npc.transform, jugador, alcanceDeLaVista, anguloDeLaVista);
                oye = VistaDelVigia.OyeAlJugador(npc.transform, jugador, distanciaDeOido);
            }

            if (fase != Fase.Sospecha && (ve || oye))
            {
                fase = Fase.Sospecha;
                inicioDeFase = Time.time;
                Parar();
                anim?.PlaySenseSomething();
            }

            float distanciaAlJugador = Vector3.Distance(jugador.position, npc.transform.position);
            switch (fase)
            {
                case Fase.Andando:
                {
                    if (Plano(npc.transform.position, ruta[tramo]) < (tramo == ruta.Count - 1 ? RadioDeMarca : RadioDePunto))
                    {
                        if (tramo == ruta.Count - 1)
                        {
                            Parar();
                            fase = Fase.Esperando;
                            break;
                        }
                        if (paradas[tramo] && !paradaHecha[tramo])
                        {
                            paradaHecha[tramo] = true;
                            haciaAtras = -npc.transform.forward;
                            Parar();
                            anim?.PlaySenseSomething();
                            fase = Fase.Parada;
                            inicioDeFase = Time.time;
                            finDeFase = Time.time + SegundosDelGesto + segundosMirando;
                            break;
                        }
                        tramo++;
                        Andar(ruta[tramo]);
                    }
                    else if (distanciaAlJugador > distanciaParaEsperar)
                    {
                        Parar();
                        fase = Fase.Esperando;
                    }
                    break;
                }
                case Fase.Esperando:
                {
                    bool enLaMarca = tramo == ruta.Count - 1 && Plano(npc.transform.position, ruta[tramo]) < RadioDeMarca;
                    if (enLaMarca)
                    {
                        if (distanciaAlJugador <= radioDeLlegada)
                        {
                            Terminar(ctx, onReadyToAdvance, PuertoLlega);
                            yield break;
                        }
                    }
                    else if (distanciaAlJugador < distanciaParaEsperar * 0.8f)
                    {
                        Andar(ruta[tramo]);
                        fase = Fase.Andando;
                    }
                    break;
                }
                case Fase.Parada:
                {
                    // Primero el gesto, de cara al camino; luego se vuelve y mira atrás.
                    if (Time.time - inicioDeFase >= SegundosDelGesto)
                        anim?.FaceDirection(haciaAtras);
                    if (Time.time >= finDeFase)
                    {
                        tramo++;
                        Andar(ruta[tramo]);
                        fase = Fase.Andando;
                    }
                    break;
                }
                case Fase.Sospecha:
                {
                    anim?.FaceTarget(jugador.position);
                    if (ve) sospecha += dt / segundosHastaVerle;
                    else if (!oye) sospecha = Mathf.Max(0f, sospecha - dt / (segundosHastaVerle * 2f));
                    if (sospecha >= 1f)
                    {
                        yield return Co_Descubierto(npc, jugador);
                        Terminar(ctx, onReadyToAdvance, PuertoLeVe);
                        yield break;
                    }
                    if (sospecha <= 0f && !ve && !oye && Time.time - inicioDeFase >= SegundosMinimosDeSospecha)
                    {
                        anim?.PlaySearching();
                        bool enLaMarca = tramo == ruta.Count - 1 && Plano(npc.transform.position, ruta[tramo]) < RadioDeMarca;
                        if (enLaMarca) fase = Fase.Esperando;
                        else { Andar(ruta[tramo]); fase = Fase.Andando; }
                    }
                    break;
                }
            }
            yield return null;
        }

        Parar();
        Terminar(ctx, onReadyToAdvance, PuertoLlega);
    }

    // Le ha visto: se queda mirándole un momento antes de seguir el grafo.
    private IEnumerator Co_Descubierto(NPCBehaviourManagerV2 npc, Transform jugador)
    {
        Parar();
        npc.SimpleAnimator?.SetFaceEmotion(NPCEmotion.Angry);
        float fin = Time.time + SegundosAlDescubrirle;
        while (Time.time < fin)
        {
            npc.SimpleAnimator?.FaceTarget(jugador.position);
            yield return null;
        }
    }

    // Al repetir el tramo, pone a cada uno en su salida si está lejos de ella.
    private IEnumerator Co_PonerEnLaSalida(NPCBehaviourManagerV2 npc, Transform jugador)
    {
        var salidaNpc = string.IsNullOrWhiteSpace(salidaDelNpc) ? null : SpawnAnchor.FindById(salidaDelNpc);
        var salidaJugador = string.IsNullOrWhiteSpace(salidaDelJugador) ? null : SpawnAnchor.FindById(salidaDelJugador);
        bool moverNpc = salidaNpc != null && Plano(npc.transform.position, salidaNpc.transform.position) > distanciaParaRecolocar;
        bool moverJugador = salidaJugador != null && Plano(jugador.position, salidaJugador.transform.position) > distanciaParaRecolocar;
        if (!moverNpc && !moverJugador) yield break;

        bool tapada = FeedbackService.IsScreenFaded;
        if (!tapada) yield return FeedbackService.ScreenFadeAsync(Color.black, Fundido, fadeIn: true);

        if (moverNpc)
        {
            var agente = npc.Agent;
            var t = salidaNpc.transform;
            if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Warp(t.position);
            npc.transform.SetPositionAndRotation(t.position, t.rotation);
            npc.SimpleAnimator?.SyncTargetRotation();
        }
        if (moverJugador)
            TeleportService.TeleportToAnchor(jugador.gameObject, salidaDelJugador, useTransition: false);

        yield return null;
        yield return FeedbackService.ScreenFadeAsync(Color.black, Fundido, fadeIn: false);
    }

    private void Andar(Vector3 hacia)
    {
        if (_agente == null || !_agente.enabled || !_agente.isOnNavMesh) return;
        _agente.isStopped = false;
        _agente.SetDestination(hacia);
        _npc?.SimpleAnimator?.TransitionToLocomotion();
    }

    private void Parar()
    {
        if (_agente == null || !_agente.enabled || !_agente.isOnNavMesh) return;
        _agente.isStopped = true;
        _agente.ResetPath();
        _npc?.SimpleAnimator?.StopMovement();
    }

    private void Terminar(NarrativeContext ctx, Action onReadyToAdvance, int puerto)
    {
        _rutina = null;
        Soltar();
        AdvanceThrough(ctx, onReadyToAdvance, puerto);
    }

    // Devuelve al NPC, al grupo y la música como estaban. Se puede llamar más de una vez.
    private void Soltar()
    {
        if (_enSigilo)
        {
            SigiloDelGrupo.Terminar(this);
            _enSigilo = false;
        }
        if (_conMusica)
        {
            if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(SeguirSinSerVistoNode)))
                audio.RequestLocationMusic(fundidoDeMusica);
            _conMusica = false;
        }
        if (_control != null)
        {
            _control.Acabar();
            _control = null;
        }
        if (_agente != null)
        {
            _agente.speed = _velocidadOriginal;
            _agente.updatePosition = _updatePositionOriginal;
            _agente = null;
        }
        if (_npc != null)
        {
            var anim = _npc.SimpleAnimator;
            if (anim != null)
            {
                anim.AllowManualRotation = _rotacionManualPrevia;
                anim.SetFaceEmotion(NPCEmotion.Neutral);
            }
            _npc = null;
        }
    }

    // Puntos de paso que existen, sobre el NavMesh, y el destino al final; 'paradas' marca en
    // cuáles se mira atrás.
    private List<Vector3> Ruta(Vector3 destino, out List<bool> paradas)
    {
        var ruta = new List<Vector3>();
        paradas = new List<bool>();
        foreach (var id in puntosDePaso)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            var marca = SpawnAnchor.FindById(id);
            if (marca == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[SeguirSinSerVistoNode:{guid}] El punto de paso '{id}' no está en la escena: se lo salta.");
#endif
                continue;
            }
            ruta.Add(SobreElNavMesh(marca.transform.position));
            paradas.Add(paradasParaMirar.Contains(id));
        }
        ruta.Add(SobreElNavMesh(destino));
        paradas.Add(false);
        return ruta;
    }

    private static Vector3 SobreElNavMesh(Vector3 p)
        => NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas) ? hit.position : p;

    private static float Plano(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// Tiene al NPC en su estado de cinemática mientras el nodo lo mueve.
    private sealed class ControlDelNodo : CinematicSequence
    {
        public void Acabar() => IsCompleted = true;
        public override void Update(Game.NPC.Common.NPCStateContext context) { }
    }
}
