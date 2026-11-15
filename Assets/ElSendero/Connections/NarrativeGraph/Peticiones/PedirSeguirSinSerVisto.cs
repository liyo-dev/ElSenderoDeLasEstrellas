using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Game.NPC.States;
using Sendero.Core.Feedback;
using Acompanante = PedirSeguirSinSerVisto.Acompanante;

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
/// Con 'acompanantes', otros NPCs van con él (por delante o por detrás) y vigilan igual. En una
/// parada, un acompañante puede dar una ronda por unas marcas y volver; el NPC al que se sigue le
/// espera. Si cualquiera de ellos descubre al jugador, el tramo sale por «Le ve».
///
/// Con 'salidaDelNpc' y 'salidaDelJugador' (y la salida de cada acompañante), al empezar pone a
/// cada uno en su marca si está a más de 'distanciaParaRecolocar' de ella (al repetir el tramo
/// después de perder), tapando el cambio con un fundido.
[Serializable]
[NarrativeNodeInfo("NPCs", "Seguir sin ser visto", "Un NPC (y quien le acompañe) camina por una ruta hasta una marca y el jugador le sigue a escondidas, agachado. Miran atrás en algunos puntos, un acompañante puede dar una ronda en una parada, y se giran si oyen o ven algo. Sale por «Llega» si llegan sin verle y por «Le ve» si le descubren.")]
public sealed class PedirSeguirSinSerVisto : NarrativeRequest, INarrativeStateEffect
{
    static readonly string[] Salidas = { "Llega", "Le ve" };

    /// Otro NPC que va con el que se sigue y también vigila.
    [Serializable]
    public sealed class Acompanante
    {
        [NarrativeKey(NarrativeKeyKind.Actor)]
        [Tooltip("persistenceId del NPC que acompaña.")]
        public string npcId = "";

        [Tooltip("Metros por delante (positivo) o por detrás (negativo) del NPC al que se sigue.")]
        public float adelanto = 3f;

        [NarrativeKey(NarrativeKeyKind.Anchor)]
        [Tooltip("Marca donde empieza. Vacío = donde esté.")]
        public string salida = "";

        [Tooltip("Rondas que da en algunas paradas: va por sus marcas y vuelve.")]
        public List<Ronda> rondas = new();
    }

    /// En una parada, un acompañante recorre unas marcas y vuelve junto al NPC al que se sigue.
    [Serializable]
    public sealed class Ronda
    {
        [NarrativeKey(NarrativeKeyKind.Anchor)]
        [Tooltip("Parada (de 'paradasParaMirar') en la que da la ronda.")]
        public string enLaParada = "";

        [NarrativeKey(NarrativeKeyKind.Anchor)]
        [Tooltip("Marcas por las que pasa, en orden.")]
        public List<string> marcas = new();
    }

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

    [Tooltip("NPCs que van con el que se sigue y también vigilan.")]
    public List<Acompanante> acompanantes = new();

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

    public override string[] Exits => Salidas;

    public override string DescribeDecision(NarrativeNode step)
        => $"Seguir sin ser visto \"{step?.displayTitle}\" (guid {step?.guid}): ¿le descubre '{npcId}'{(acompanantes.Count > 0 ? " o quien le acompaña" : "")}? Salidas: Llega / Le ve.";

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Actor(npcId, "va delante");
        foreach (var a in acompanantes) if (a != null && !string.IsNullOrWhiteSpace(a.npcId)) d.Actor(a.npcId, "acompaña");
        if (!string.IsNullOrWhiteSpace(anchorId)) d.Anchor(anchorId);
    }

    /// El actor acaba en la marca de destino (y sus acompañantes con él).
    public void Project(INarrativeStateWriter state)
    {
        if (string.IsNullOrWhiteSpace(anchorId)) return;
        if (!string.IsNullOrWhiteSpace(npcId))
            state.PlaceActor(npcId, NarrativeLocation.World(anchorId));
        foreach (var a in acompanantes)
            if (a != null && !string.IsNullOrWhiteSpace(a.npcId))
                state.PlaceActor(a.npcId, NarrativeLocation.World(anchorId));
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(npcId)) warnings.Add("Falta el NPC al que se sigue.");
        if (string.IsNullOrWhiteSpace(anchorId)) warnings.Add("Falta la marca a la que va el NPC.");
        if (velocidadDelJugador >= velocidad) warnings.Add("El jugador debe ir más despacio que el NPC al que sigue.");
        foreach (var p in paradasParaMirar)
            if (!string.IsNullOrWhiteSpace(p) && !puntosDePaso.Contains(p))
                warnings.Add($"La parada '{p}' no está entre los puntos de paso.");
        foreach (var a in acompanantes)
        {
            if (a == null) continue;
            if (string.IsNullOrWhiteSpace(a.npcId)) warnings.Add("Hay un acompañante sin NPC.");
            if (a.npcId == npcId) warnings.Add($"El acompañante '{a.npcId}' es el mismo NPC al que se sigue.");
            foreach (var r in a.rondas)
                if (r != null && !paradasParaMirar.Contains(r.enLaParada))
                    warnings.Add($"La ronda de '{a.npcId}' es en '{r.enLaParada}', que no es una parada para mirar.");
        }
    }
}

public sealed class SeguirSinSerVistoEnCurso : NarrativeRequestRun<PedirSeguirSinSerVisto>
{
    private const int PuertoLlega = 0;
    private const float RadioParaBuscarMalla = 8f;
    private const int PuertoLeVe = 1;

    private enum Fase { Andando, Esperando, Parada, Sospecha }

    // Distancia a la que se dejan atrás los puntos de paso y se da por llegado a la marca.
    private const float RadioDePunto = 1.5f;
    private const float RadioDeMarca = 0.9f;
    private const float IntervaloDeVigilancia = 0.1f;
    private const float IntervaloDeRefresco = 0.5f;
    private const float IntervaloDeFormacion = 0.25f;
    private const float SegundosDelGesto = 0.8f;
    private const float SegundosMinimosDeSospecha = 1.2f;
    private const float SegundosAlDescubrirle = 0.8f;
    private const float Fundido = 0.5f;
    // Un acompañante va algo más rápido que el NPC para recuperar su sitio tras una ronda.
    private const float VentajaDelAcompanante = 1.2f;
    // Por debajo de esto, en metros, el acompañante ya está en su sitio y se queda quieto.
    private const float HolguraDeFormacion = 0.8f;
    // Al empezar, el NPC toma distancia antes de vigilar: suele salir de una cinemática con el
    // jugador al lado.
    private const float SegundosDeVentaja = 4f;

    /// Lo que el nodo lleva de cada NPC que mueve (el que se sigue y sus acompañantes).
    private sealed class Movido
    {
        public NPCBehaviourManagerV2 Npc;
        public NavMeshAgent Agente;
        public ControlDelNodo Control;
        public float VelocidadOriginal;
        public bool UpdatePositionOriginal;
        public bool RotacionManualPrevia;
        public bool Preparado;
        public Acompanante Datos;
        public List<Vector3> Ronda;
        public int PasoDeRonda;
        public bool EnRonda => Ronda != null;
    }

    private Coroutine _rutina;
    private Movido _lider;
    private List<Movido> _acompanantes;
    private bool _enSigilo;
    private bool _conMusica;

    protected override void Start()
    {
        var npc = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(R.npcId) : null;
        var marca = string.IsNullOrWhiteSpace(R.anchorId) ? null : SpawnAnchor.FindById(R.anchorId);
        var jugador = PlayerService.PlayerTransform;
        if (npc == null || marca == null || jugador == null || Runner == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[Sigilo:{StepGuid}] No se puede empezar: NPC '{R.npcId}'={(npc != null ? "ok" : "NO")}, " +
                $"marca '{R.anchorId}'={(marca != null ? "ok" : "NO")}, jugador={(jugador != null ? "ok" : "NO")}. Sigue por «Llega» para no bloquear.");
#endif
            Finish(PuertoLlega);
            return;
        }

        _rutina = Runner.StartCoroutine(Co_Seguir(npc, marca.transform.position, jugador));
    }

    protected override void Stop()
    {
        if (_rutina != null && Runner != null) Runner.StopCoroutine(_rutina);
        _rutina = null;
        Soltar();
    }

    private IEnumerator Co_Seguir(NPCBehaviourManagerV2 npc, Vector3 destino, Transform jugador)
    {
        var otros = BuscarAcompanantes();
        yield return Co_PonerEnLaSalida(npc, otros, jugador);

        _lider = Tomar(npc, null);
        _acompanantes = new List<Movido>(otros.Count);
        foreach (var (datos, m) in otros) _acompanantes.Add(Tomar(m, datos));
        yield return null;

        if (R.musica != null && NarrativeServices.TryGet(out INarrativeAudio audio, nameof(SeguirSinSerVistoEnCurso)))
        {
            audio.PlayMusic(R.musica, R.fundidoDeMusica);
            _conMusica = true;
        }

        SigiloDelGrupo.Empezar(this, Mathf.Min(R.velocidadDelJugador, R.velocidad * 0.85f));
        _enSigilo = true;
        yield return null;

        if (!EnLaMalla(_lider.Agente))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[Sigilo:{StepGuid}] '{R.npcId}' no tiene NavMeshAgent sobre el NavMesh. Sigue por «Llega».");
#endif
            Terminar(PuertoLlega);
            yield break;
        }

        Preparar(_lider, R.velocidad);
        foreach (var a in _acompanantes) Preparar(a, R.velocidad * VentajaDelAcompanante);

        var ruta = Ruta(destino, out var paradas, out var idsDeRuta);
        int tramo = 0;
        bool[] paradaHecha = new bool[ruta.Count];
        Andar(_lider, ruta[tramo]);

        var anim = npc.SimpleAnimator;
        var fase = Fase.Andando;
        float limite = Time.time + R.duracionMaxima;
        float finDeFase = 0f;
        float inicioDeFase = Time.time;
        float sospecha = 0f;
        float siguienteVigilancia = Time.time + SegundosDeVentaja;
        float siguienteRefresco = 0f;
        float siguienteFormacion = 0f;
        bool ve = false, oye = false;
        Movido quienNota = _lider;
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
                ve = false; oye = false;
                Vigilar(_lider, jugador, ref ve, ref oye, ref quienNota);
                foreach (var a in _acompanantes) Vigilar(a, jugador, ref ve, ref oye, ref quienNota);
            }

            if (fase != Fase.Sospecha && (ve || oye))
            {
                fase = Fase.Sospecha;
                inicioDeFase = Time.time;
                Parar(_lider);
                foreach (var a in _acompanantes) { a.Ronda = null; Parar(a); }
                quienNota.Npc.SimpleAnimator?.PlaySenseSomething();
            }

            if (fase != Fase.Sospecha && Time.time >= siguienteFormacion)
            {
                siguienteFormacion = Time.time + IntervaloDeFormacion;
                foreach (var a in _acompanantes) MoverAcompanante(a);
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
                            Parar(_lider);
                            fase = Fase.Esperando;
                            break;
                        }
                        if (paradas[tramo] && !paradaHecha[tramo])
                        {
                            paradaHecha[tramo] = true;
                            haciaAtras = -npc.transform.forward;
                            Parar(_lider);
                            anim?.PlaySenseSomething();
                            EmpezarRondas(idsDeRuta[tramo]);
                            fase = Fase.Parada;
                            inicioDeFase = Time.time;
                            finDeFase = Time.time + SegundosDelGesto + R.segundosMirando;
                            break;
                        }
                        tramo++;
                        Andar(_lider, ruta[tramo]);
                    }
                    else if (distanciaAlJugador > R.distanciaParaEsperar)
                    {
                        Parar(_lider);
                        fase = Fase.Esperando;
                    }
                    break;
                }
                case Fase.Esperando:
                {
                    bool enLaMarca = tramo == ruta.Count - 1 && Plano(npc.transform.position, ruta[tramo]) < RadioDeMarca;
                    if (enLaMarca)
                    {
                        if (distanciaAlJugador <= R.radioDeLlegada)
                        {
                            Terminar(PuertoLlega);
                            yield break;
                        }
                    }
                    else if (distanciaAlJugador < R.distanciaParaEsperar * 0.8f)
                    {
                        Andar(_lider, ruta[tramo]);
                        fase = Fase.Andando;
                    }
                    break;
                }
                case Fase.Parada:
                {
                    // Primero el gesto, de cara al camino; luego se vuelve y mira atrás. Quien no
                    // está de ronda mira atrás con él.
                    if (Time.time - inicioDeFase >= SegundosDelGesto)
                    {
                        anim?.FaceDirection(haciaAtras);
                        foreach (var a in _acompanantes)
                            if (!a.EnRonda) a.Npc.SimpleAnimator?.FaceDirection(haciaAtras);
                    }
                    if (Time.time >= finDeFase && !AlgunoDeRonda())
                    {
                        tramo++;
                        Andar(_lider, ruta[tramo]);
                        fase = Fase.Andando;
                    }
                    break;
                }
                case Fase.Sospecha:
                {
                    quienNota.Npc.SimpleAnimator?.FaceTarget(jugador.position);
                    if (ve) sospecha += dt / R.segundosHastaVerle;
                    else if (!oye) sospecha = Mathf.Max(0f, sospecha - dt / (R.segundosHastaVerle * 2f));
                    if (sospecha >= 1f)
                    {
                        yield return Co_Descubierto(quienNota.Npc, jugador);
                        Terminar(PuertoLeVe);
                        yield break;
                    }
                    if (sospecha <= 0f && !ve && !oye && Time.time - inicioDeFase >= SegundosMinimosDeSospecha)
                    {
                        quienNota.Npc.SimpleAnimator?.PlaySearching();
                        bool enLaMarca = tramo == ruta.Count - 1 && Plano(npc.transform.position, ruta[tramo]) < RadioDeMarca;
                        if (enLaMarca) fase = Fase.Esperando;
                        else { Andar(_lider, ruta[tramo]); fase = Fase.Andando; }
                    }
                    break;
                }
            }
            yield return null;
        }

        Parar(_lider);
        Terminar(PuertoLlega);
    }

    // Quien vigila mejor: el primero que ve; si nadie ve, el primero que oye.
    private void Vigilar(Movido m, Transform jugador, ref bool ve, ref bool oye, ref Movido quienNota)
    {
        if (m?.Npc == null) return;
        var t = m.Npc.transform;
        bool v = VistaDelVigia.VeAlJugador(t, jugador, R.alcanceDeLaVista, R.anguloDeLaVista);
        bool o = VistaDelVigia.OyeAlJugador(t, jugador, R.distanciaDeOido);
        if (v && !ve) quienNota = m;
        else if (o && !ve && !oye) quienNota = m;
        ve |= v;
        oye |= o;
    }

    private void EmpezarRondas(string parada)
    {
        if (string.IsNullOrEmpty(parada)) return;
        foreach (var a in _acompanantes)
        {
            foreach (var r in a.Datos.rondas)
            {
                if (r == null || r.enLaParada != parada) continue;
                var puntos = new List<Vector3>(r.marcas.Count);
                foreach (var id in r.marcas)
                {
                    var marca = string.IsNullOrWhiteSpace(id) ? null : SpawnAnchor.FindById(id);
                    if (marca != null) puntos.Add(SobreElNavMesh(marca.transform.position));
                }
                if (puntos.Count == 0) continue;
                a.Ronda = puntos;
                a.PasoDeRonda = 0;
                Andar(a, puntos[0]);
                break;
            }
        }
    }

    private bool AlgunoDeRonda()
    {
        foreach (var a in _acompanantes) if (a.EnRonda) return true;
        return false;
    }

    // De ronda, sigue sus marcas; si no, va a su sitio respecto al NPC al que se sigue.
    private void MoverAcompanante(Movido a)
    {
        if (!EnLaMalla(a.Agente)) return;
        if (a.EnRonda)
        {
            if (Plano(a.Npc.transform.position, a.Ronda[a.PasoDeRonda]) < RadioDePunto)
            {
                a.PasoDeRonda++;
                if (a.PasoDeRonda >= a.Ronda.Count) a.Ronda = null;
                else Andar(a, a.Ronda[a.PasoDeRonda]);
            }
            if (a.EnRonda) return;
        }

        var lider = _lider.Npc.transform;
        Vector3 sitio = SobreElNavMesh(lider.position + lider.forward * a.Datos.adelanto);
        if (Plano(a.Npc.transform.position, sitio) > HolguraDeFormacion) Andar(a, sitio);
        else Parar(a);
    }

    // Le ha visto: se queda mirándole un momento antes de seguir el grafo.
    private IEnumerator Co_Descubierto(NPCBehaviourManagerV2 quien, Transform jugador)
    {
        Parar(_lider);
        foreach (var a in _acompanantes) Parar(a);
        quien.SimpleAnimator?.SetFaceEmotion(NPCEmotion.Angry);
        float fin = Time.time + SegundosAlDescubrirle;
        while (Time.time < fin)
        {
            quien.SimpleAnimator?.FaceTarget(jugador.position);
            foreach (var a in _acompanantes) a.Npc.SimpleAnimator?.FaceTarget(jugador.position);
            if (_lider.Npc != quien) _lider.Npc.SimpleAnimator?.FaceTarget(jugador.position);
            yield return null;
        }
    }

    private List<(Acompanante, NPCBehaviourManagerV2)> BuscarAcompanantes()
    {
        var lista = new List<(Acompanante, NPCBehaviourManagerV2)>();
        if (!NPCRegistry.HasInstance) return lista;
        foreach (var a in R.acompanantes)
        {
            if (a == null || string.IsNullOrWhiteSpace(a.npcId)) continue;
            var m = NPCRegistry.Instance.GetNPCByID(a.npcId);
            if (m != null) lista.Add((a, m));
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            else Debug.LogWarning($"[Sigilo:{StepGuid}] El acompañante '{a.npcId}' no está en la escena: va sin él.");
#endif
        }
        return lista;
    }

    // Toma el control de un NPC: agente sobre la malla, estado de cinemática y giro automático.
    private static Movido Tomar(NPCBehaviourManagerV2 npc, Acompanante datos)
    {
        var m = new Movido { Npc = npc, Agente = npc.Agent, Datos = datos };
        // El NPC puede llegar aquí con el agente apagado o fuera de la malla (una cinemática lo deja
        // donde acaba su camino, o apareció lejos de la malla): se le busca suelo caminable cerca
        // antes de empezar, en vez de dar el tramo por hecho sin jugarlo (INC-1037).
        if (m.Agente != null && !EnLaMalla(m.Agente))
        {
            if (!m.Agente.enabled)
                Game.NPC.Common.NavMeshAgentUtility.SafeEnable(m.Agente, npc.transform, npc.transform.position, RadioParaBuscarMalla);
            else if (NavMesh.SamplePosition(npc.transform.position, out var suelo, RadioParaBuscarMalla, NavMesh.AllAreas))
                m.Agente.Warp(suelo.position);
        }

        var anim = npc.SimpleAnimator;
        if (anim != null)
        {
            m.RotacionManualPrevia = anim.AllowManualRotation;
            anim.AllowManualRotation = false;
            anim.EnableAutoRotation();
        }

        m.Control = new ControlDelNodo();
        npc.StartCinematicSequence(m.Control);
        return m;
    }

    private static void Preparar(Movido m, float rapidez)
    {
        if (!EnLaMalla(m.Agente)) return;
        m.VelocidadOriginal = m.Agente.speed;
        m.UpdatePositionOriginal = m.Agente.updatePosition;
        m.Agente.nextPosition = m.Npc.transform.position;
        m.Agente.updatePosition = true;
        m.Agente.speed = rapidez;
        m.Preparado = true;
    }

    // Al repetir el tramo, pone a cada uno en su salida si está lejos de ella.
    private IEnumerator Co_PonerEnLaSalida(NPCBehaviourManagerV2 npc, List<(Acompanante, NPCBehaviourManagerV2)> otros, Transform jugador)
    {
        var mover = new List<(NPCBehaviourManagerV2, Transform)>();
        var salidaNpc = string.IsNullOrWhiteSpace(R.salidaDelNpc) ? null : SpawnAnchor.FindById(R.salidaDelNpc);
        if (salidaNpc != null && Plano(npc.transform.position, salidaNpc.transform.position) > R.distanciaParaRecolocar)
            mover.Add((npc, salidaNpc.transform));
        foreach (var (datos, m) in otros)
        {
            var s = string.IsNullOrWhiteSpace(datos.salida) ? null : SpawnAnchor.FindById(datos.salida);
            if (s != null && Plano(m.transform.position, s.transform.position) > R.distanciaParaRecolocar)
                mover.Add((m, s.transform));
        }
        var salidaJugador = string.IsNullOrWhiteSpace(R.salidaDelJugador) ? null : SpawnAnchor.FindById(R.salidaDelJugador);
        bool moverJugador = salidaJugador != null && Plano(jugador.position, salidaJugador.transform.position) > R.distanciaParaRecolocar;
        if (mover.Count == 0 && !moverJugador) yield break;

        bool tapada = FeedbackService.IsScreenFaded;
        if (!tapada) yield return FeedbackService.ScreenFadeAsync(Color.black, Fundido, fadeIn: true);

        foreach (var (m, t) in mover)
        {
            var agente = m.Agent;
            if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Warp(t.position);
            m.transform.SetPositionAndRotation(t.position, t.rotation);
            m.SimpleAnimator?.SyncTargetRotation();
        }
        if (moverJugador)
            TeleportService.TeleportToAnchor(jugador.gameObject, R.salidaDelJugador, useTransition: false);

        yield return null;
        yield return FeedbackService.ScreenFadeAsync(Color.black, Fundido, fadeIn: false);
    }

    private static void Andar(Movido m, Vector3 hacia)
    {
        if (m == null || !EnLaMalla(m.Agente)) return;
        bool estabaQuieto = m.Agente.isStopped || !m.Agente.hasPath;
        m.Agente.isStopped = false;
        m.Agente.SetDestination(hacia);
        if (estabaQuieto) m.Npc.SimpleAnimator?.TransitionToLocomotion();
    }

    private static void Parar(Movido m)
    {
        if (m == null || !EnLaMalla(m.Agente)) return;
        if (m.Agente.isStopped && !m.Agente.hasPath) return;
        m.Agente.isStopped = true;
        m.Agente.ResetPath();
        m.Npc.SimpleAnimator?.StopMovement();
    }

    private static bool EnLaMalla(NavMeshAgent agente) => agente != null && agente.enabled && agente.isOnNavMesh;

    private void Terminar(int puerto)
    {
        _rutina = null;
        Finish(puerto);
    }

    // Devuelve a los NPCs, al grupo y la música como estaban. Se puede llamar más de una vez.
    private void Soltar()
    {
        if (_enSigilo)
        {
            SigiloDelGrupo.Terminar(this);
            _enSigilo = false;
        }
        if (_conMusica)
        {
            if (NarrativeServices.TryGet(out INarrativeAudio audio, nameof(SeguirSinSerVistoEnCurso)))
                audio.RequestLocationMusic(R.fundidoDeMusica);
            _conMusica = false;
        }
        Devolver(_lider);
        _lider = null;
        if (_acompanantes != null)
        {
            foreach (var a in _acompanantes) Devolver(a);
            _acompanantes = null;
        }
    }

    private static void Devolver(Movido m)
    {
        if (m == null) return;
        m.Control?.Acabar();
        m.Control = null;
        if (m.Agente != null && m.Preparado)
        {
            m.Agente.speed = m.VelocidadOriginal;
            m.Agente.updatePosition = m.UpdatePositionOriginal;
        }
        m.Agente = null;
        if (m.Npc != null)
        {
            var anim = m.Npc.SimpleAnimator;
            if (anim != null)
            {
                anim.AllowManualRotation = m.RotacionManualPrevia;
                anim.SetFaceEmotion(NPCEmotion.Neutral);
            }
            m.Npc = null;
        }
    }

    // Puntos de paso que existen, sobre el NavMesh, y el destino al final; 'paradas' marca en
    // cuáles se mira atrás e 'ids' guarda el nombre de cada uno (para las rondas).
    private List<Vector3> Ruta(Vector3 destino, out List<bool> paradas, out List<string> ids)
    {
        var ruta = new List<Vector3>();
        paradas = new List<bool>();
        ids = new List<string>();
        foreach (var id in R.puntosDePaso)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            var marca = SpawnAnchor.FindById(id);
            if (marca == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[Sigilo:{StepGuid}] El punto de paso '{id}' no está en la escena: se lo salta.");
#endif
                continue;
            }
            ruta.Add(SobreElNavMesh(marca.transform.position));
            paradas.Add(R.paradasParaMirar.Contains(id));
            ids.Add(id);
        }
        ruta.Add(SobreElNavMesh(destino));
        paradas.Add(false);
        ids.Add(R.anchorId);
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
