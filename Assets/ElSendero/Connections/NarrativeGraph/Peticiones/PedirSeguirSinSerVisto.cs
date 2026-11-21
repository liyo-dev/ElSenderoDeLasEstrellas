using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Game.NPC.States;
using Sendero.Core.Feedback;
using Acompanante = PedirSeguirSinSerVisto.Acompanante;
using Liyodev.NarrativeGraph;

/// Un NPC recorre una ruta hasta una marca y el jugador le sigue sin que le vea.
///
/// Mientras dura, el grupo va agachado (<see cref="SigiloDelGrupo"/>); con 'velocidadDelJugador'
/// mayor que 0, además, con ese tope de velocidad.
/// El NPC vigila con la vista (un cono por delante que tapan el escenario y los
/// <see cref="Escondite"/>) y con el oído (de cerca, si el jugador no está escondido). En los
/// puntos de 'paradasParaMirar' se para, hace un gesto y mira hacia atrás unos segundos. Si nota
/// algo, se para, hace el gesto de haber notado algo y mira hacia donde lo notó, con un aviso
/// sobre la cabeza ('iconoDeAlerta') que se va apagando durante 'segundosDeAlerta': es el tiempo
/// del jugador para esconderse. Si al apagarse alguno le ve, le ha descubierto; si no, siguen la
/// marcha. Con 'distanciaParaEsperar' mayor que 0, si el jugador se queda muy atrás, le espera
/// sin llamarle. Sale por «Llega» cuando el NPC está en la marca y el jugador cerca (o el jugador
/// llega antes a la marca, o se acaba el tiempo), y por «Le ve» si le descubre.
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

    [System.Serializable]
    public sealed class EsperaEnParada
    {
        [Tooltip("Parada (de 'paradasParaMirar') en la que se quedan mirando atrás hasta que el jugador llega.")]
        public string enLaParada = "";

        [Tooltip("Marca (SpawnAnchor) a la que tiene que llegar el jugador para que sigan (p. ej. un escondite al otro lado de un paso vigilado).")]
        public string hastaQueElJugadorLlegueA = "";

        [Tooltip("Metros alrededor de la marca que cuentan como haber llegado.")]
        public float radio = 3f;
    }

    [Tooltip("Paradas en las que se quedan mirando atrás hasta que el jugador llega a un sitio: no basta con esperar a que se vayan.")]
    public List<EsperaEnParada> esperas = new();

    [Tooltip("El NPC al que se sigue no se gira en las paradas (p. ej. va entre las varas de un carro): miran atrás sus acompañantes.")]
    public bool elQueSeSigueNoSeVuelve;

    [Min(0.5f)]
    [Tooltip("Segundos que se queda mirando atrás en cada parada.")]
    public float segundosMirando = 3f;

    [Min(0.5f)]
    [Tooltip("Velocidad del NPC, en m/s.")]
    public float velocidad = 2.2f;

    [Min(0f)]
    [Tooltip("Velocidad máxima del jugador agachado, en m/s. 0 = la de siempre: solo cambia la postura.")]
    public float velocidadDelJugador = 0f;

    [Min(1f)]
    [Tooltip("Hasta dónde ve el NPC, en metros.")]
    public float alcanceDeLaVista = 14f;

    [Range(20f, 220f)]
    [Tooltip("Abertura del cono de la vista, en grados.")]
    public float anguloDeLaVista = 110f;

    [Min(0f)]
    [Tooltip("A menos de esto, en metros, el NPC oye al jugador si no está escondido.")]
    public float distanciaDeOido = 4f;

    [Min(0.5f)]
    [Tooltip("Segundos que dura el aviso cuando notan algo: el tiempo del jugador para esconderse. Si al acabar le ven, le han descubierto.")]
    public float segundosDeAlerta = 3f;

    [Tooltip("Icono que sale sobre la cabeza de quien nota algo y se va apagando mientras dura el aviso. Vacío = sin icono (solo el gesto).")]
    public GameObject iconoDeAlerta;

    [Min(0f)]
    [Tooltip("Si el jugador se queda más atrás que esto, en metros, el NPC se para a esperarle. 0 = no le esperan.")]
    public float distanciaParaEsperar = 0f;

    [Min(1f)]
    [Tooltip("En la marca, el tramo acaba cuando el jugador está a esta distancia del NPC, en metros.")]
    public float radioDeLlegada = 25f;

    [Min(0f)]
    [Tooltip("Si el jugador llega a esta distancia de la marca sin que le descubran, el tramo acaba por «Llega» aunque el NPC no haya llegado (se le pone en la marca si está lejos). 0 = solo acaba cuando llega el NPC.")]
    public float radioDelJugadorEnLaMarca = 6f;

    [Min(10f)]
    [Tooltip("Tope de segundos del recorrido. Si se agota, el tramo acaba por «Llega» y los NPCs se ponen en la marca si están lejos.")]
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
        => $"Seguir sin ser visto \"{step?.DisplayTitle}\" (guid {step?.Guid}): ¿le descubre '{npcId}'{(acompanantes.Count > 0 ? " o quien le acompaña" : "")}? Salidas: Llega / Le ve.";

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
        if (velocidadDelJugador > 0f && velocidadDelJugador < velocidad) warnings.Add("El jugador va más despacio que el NPC al que sigue: si se queda atrás no podrá alcanzarle.");
        foreach (var p in paradasParaMirar)
            if (!string.IsNullOrWhiteSpace(p) && !puntosDePaso.Contains(p))
                warnings.Add($"La parada '{p}' no está entre los puntos de paso.");
        foreach (var e in esperas)
            if (e != null && !paradasParaMirar.Contains(e.enLaParada))
                warnings.Add($"La espera en '{e.enLaParada}' no es en una parada para mirar.");
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

    private enum Fase { Andando, Esperando, Parada, Alerta }

    // Distancia a la que se dejan atrás los puntos de paso y se da por llegado a la marca.
    private const float RadioDePunto = 1.5f;
    private const float RadioDeMarca = 0.9f;
    private const float IntervaloDeVigilancia = 0.1f;
    private const float IntervaloDeRefresco = 0.5f;
    private const float IntervaloDeFormacion = 0.25f;
    private const float SegundosDelGesto = 0.8f;
    // Tras un aviso que acaba sin descubrirle, unos segundos sin volver a notar nada: si no, el
    // oído encadenaría un aviso tras otro con el jugador agachado cerca.
    private const float SegundosTrasLaAlerta = 1.5f;
    private const string HijoDelAviso = "_AvisoDeSigilo";
    private const float SegundosAlDescubrirle = 0.8f;
    private const float Fundido = 0.5f;
    // Si el que va delante no se mueve en este tiempo y no está en una parada, se le vuelve a
    // mandar andar. Es la red que evita que el tramo se quede parado con los NPCs en la marca.
    private const float SegundosSinAvanzar = 3f;
    // Un acompañante va algo más rápido que el NPC para recuperar su sitio tras una ronda.
    private const float VentajaDelAcompanante = 1.2f;
    // Por debajo de esto, en metros, el acompañante ya está en su sitio y se queda quieto.
    private const float HolguraDeFormacion = 0.8f;
    // Al empezar, el NPC toma distancia antes de vigilar: suele salir de una cinemática con el
    // jugador al lado.
    private const float SegundosDeVentaja = 4f;
    // A menos de esto del jugador no se recoloca a un NPC de golpe (se notaría el salto).
    private const float AlcanceParaNoTeletransportar = 18f;
    // Si el camino del NavMesh entre dos puntos sale más largo que esto respecto a la recta (más
    // la holgura), o sube más que 'SubidaMaxima' sobre los dos extremos, es un rodeo que no haría
    // un carro (p. ej. por un monte): el NPC va por la recta, a tramos cortos (INC-1402).
    private const float RodeoMaximo = 1.6f;
    private const float HolguraDeRodeo = 6f;
    private const float SubidaMaxima = 3.5f;
    private const float TramoRecto = 6f;
    private static NavMeshPath s_camino;
    private static readonly Vector3[] s_esquinas = new Vector3[64];

    /// Lo que el nodo lleva de cada NPC que mueve (el que se sigue y sus acompañantes).
    private sealed class Movido
    {
        public NPCBehaviourManagerV2 Npc;
        public NavMeshAgent Agente;
        public ControlDelNodo Control;
        public float VelocidadOriginal;
        public bool UpdatePositionOriginal;
        public UnityEngine.AI.ObstacleAvoidanceType EvitacionOriginal;
        public int PrioridadOriginal;
        public bool RotacionManualPrevia;
        public bool Preparado;
        public Acompanante Datos;
        public List<Vector3> Ronda;
        public int PasoDeRonda;
        public bool EnRonda => Ronda != null;
        public Game.NPC.Common.NPCAlertIconController Aviso;
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
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Sigilo:{StepGuid}] arranca: '{R.npcId}' en {npc.transform.position:F1}, destino '{R.anchorId}' {destino:F1}, jugador {jugador.position:F1}.");
#endif
        // La historia puede tenerlos retirados («no está» hasta que anochece): el seguimiento los
        // necesita en escena, así que vuelven antes de colocarlos en la salida.
        Presentar(R.npcId);
        foreach (var a in R.acompanantes) if (a != null) Presentar(a.npcId);

        var otros = BuscarAcompanantes();

        // El grupo se agacha ya al empezar: así el jugador ve que el tramo ha tomado el control
        // aunque la colocación en la salida tarde un fundido. Se suelta en Soltar().
        SigiloDelGrupo.Empezar(this, R.velocidadDelJugador);
        _enSigilo = true;

        // La capa de la historia los enciende en su siguiente revisión (BringOnStage ya lo ha
        // pedido): hasta que estén activos su agente no existe y la colocación en la salida
        // necesita el NavMesh. Se espera a que la escena los tenga encendidos.
        float finDeEspera = Time.time + 2f;
        while (npc != null && !npc.gameObject.activeInHierarchy && Time.time < finDeEspera)
            yield return null;

        yield return Co_PonerEnLaSalida(npc, otros, jugador);
        if (jugador == null) jugador = PlayerService.PlayerTransform;
        if (jugador == null || npc == null)
        {
            SinEscena(jugador != null);
            yield break;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Sigilo:{StepGuid}] en la salida; tomo el NPC (agente={(npc.Agent != null ? (npc.Agent.enabled ? (npc.Agent.isOnNavMesh ? "en malla" : "fuera de malla") : "apagado") : "sin agente")}).");
#endif
        _lider = Tomar(npc, null);
        _acompanantes = new List<Movido>(otros.Count);
        foreach (var (datos, m) in otros) _acompanantes.Add(Tomar(m, datos));
        yield return null;

        if (R.musica != null && NarrativeServices.TryGet(out INarrativeAudio audio, nameof(SeguirSinSerVistoEnCurso)))
        {
            audio.PlayMusic(R.musica, R.fundidoDeMusica);
            _conMusica = true;
        }

        yield return null;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Sigilo:{StepGuid}] sigilo activo (agachado). En malla: {EnLaMalla(_lider.Agente)}.");
#endif
        if (!EnLaMalla(_lider.Agente))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[Sigilo:{StepGuid}] '{R.npcId}' no tiene NavMeshAgent sobre el NavMesh. Sigue por «Llega».");
#endif
            Terminar(PuertoLlega);
            yield break;
        }

        Preparar(_lider, R.velocidad);
        foreach (var a in _acompanantes) Preparar(a, R.velocidad * VentajaDelAcompanante, esquiva: true);

        var ruta = Ruta(destino, out var paradas, out var idsDeRuta);
        int tramo = 0;
        bool[] paradaHecha = new bool[ruta.Count];
        Andar(_lider, ruta[tramo]);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Sigilo:{StepGuid}] ruta de {ruta.Count} puntos; primer destino {ruta[0]:F1}.");
#endif

        var anim = npc.SimpleAnimator;
        var fase = Fase.Andando;
        float limite = Time.time + R.duracionMaxima;
        float finDeFase = 0f;
        float inicioDeFase = Time.time;
        Movido quienAvisa = null;
        Vector3 dondeLoNoto = Vector3.zero;
        float siguienteVigilancia = Time.time + SegundosDeVentaja;
        float siguienteRefresco = 0f;
        float siguienteFormacion = 0f;
        bool ve = false, oye = false;
        bool hayEspera = false;
        Vector3 esperaEn = Vector3.zero;
        float radioDeEspera = 0f;
        Movido quienNota = _lider;
        Vector3 haciaAtras = -npc.transform.forward;
        Vector3 ultimaPosicion = npc.transform.position;
        float siguienteControlDeAvance = Time.time + SegundosSinAvanzar;
        int reintentosDeAndar = 0;

        while (Time.time < limite)
        {
            if (!SiguenEnEscena(npc, ref jugador))
            {
                SinEscena(jugador != null);
                yield break;
            }
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

            if (fase != Fase.Alerta && (ve || oye))
            {
                // Alguien ha notado algo: se paran, hace el gesto y mira hacia donde lo notó, con el
                // aviso sobre la cabeza apagándose. Ese rato es el del jugador para esconderse.
                fase = Fase.Alerta;
                inicioDeFase = Time.time;
                finDeFase = Time.time + R.segundosDeAlerta;
                quienAvisa = quienNota;
                dondeLoNoto = jugador.position;
                Parar(_lider);
                foreach (var a in _acompanantes) { a.Ronda = null; Parar(a); }
                quienAvisa.Npc.SimpleAnimator?.PlaySenseSomething();
                MostrarAviso(quienAvisa);
            }

            if (fase != Fase.Alerta && Time.time >= siguienteFormacion)
            {
                siguienteFormacion = Time.time + IntervaloDeFormacion;
                foreach (var a in _acompanantes) MoverAcompanante(a);
            }

            // Red para que la historia nunca se quede parada: si el jugador ya está en la marca sin
            // que le hayan descubierto, el tramo se da por hecho aunque los NPCs se hayan quedado
            // atrás o perdidos (INC-1404).
            if (fase != Fase.Alerta && R.radioDelJugadorEnLaMarca > 0f
                && Plano(jugador.position, destino) <= R.radioDelJugadorEnLaMarca)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[Sigilo:{StepGuid}] el jugador ha llegado a la marca '{R.anchorId}' (el NPC, a {Plano(npc.transform.position, destino):0} m). Sigue por «Llega».");
#endif
                PonerEnLaMarca(destino, jugador);
                Terminar(PuertoLlega);
                yield break;
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
                            hayEspera = BuscarEspera(idsDeRuta[tramo], out esperaEn, out radioDeEspera);
                            fase = Fase.Parada;
                            inicioDeFase = Time.time;
                            finDeFase = Time.time + SegundosDelGesto + R.segundosMirando;
                            break;
                        }
                        tramo++;
                        Andar(_lider, ruta[tramo]);
                    }
                    else if (R.distanciaParaEsperar > 0f && distanciaAlJugador > R.distanciaParaEsperar)
                    {
                        Parar(_lider);
                        fase = Fase.Esperando;
                    }
                    else if (SinCamino(_lider))
                    {
                        Andar(_lider, ruta[tramo]);
                    }
                    else if (Time.time >= siguienteControlDeAvance)
                    {
                        siguienteControlDeAvance = Time.time + SegundosSinAvanzar;
                        if (Plano(npc.transform.position, ultimaPosicion) < 0.05f)
                        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                            Debug.LogWarning($"[Sigilo:{StepGuid}] '{R.npcId}' lleva {SegundosSinAvanzar:0} s parado sin estar en una parada; se le vuelve a mandar andar (intento {reintentosDeAndar + 1}).");
#endif
                            if (reintentosDeAndar++ >= 2)
                            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                                Debug.LogWarning($"[Sigilo:{StepGuid}] '{R.npcId}' no consigue andar hacia {ruta[tramo]:F1}. Sigue por «Llega» para no bloquear la historia.");
#endif
                                Terminar(PuertoLlega);
                                yield break;
                            }
                            Andar(_lider, ruta[tramo]);
                        }
                        else
                        {
                            reintentosDeAndar = 0;
                        }
                        ultimaPosicion = npc.transform.position;
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
                        ultimaPosicion = npc.transform.position;
                        siguienteControlDeAvance = Time.time + SegundosSinAvanzar;
                        reintentosDeAndar = 0;
                    }
                    else
                    {
                        // Se ha parado a esperar: que se le note. Se gira hacia el jugador, sin
                        // llamarle (el diseño no quiere voces), para que no parezca congelado.
                        if (!R.elQueSeSigueNoSeVuelve) _lider.Npc.SimpleAnimator?.FaceTarget(jugador.position);
                        else
                            foreach (var a in _acompanantes)
                                if (!a.EnRonda && a.Npc != null) a.Npc.SimpleAnimator?.FaceTarget(jugador.position);
                    }
                    break;
                }
                case Fase.Parada:
                {
                    // Primero el gesto, de cara al camino; luego se vuelve y mira atrás. Quien no
                    // está de ronda mira atrás con él.
                    if (Time.time - inicioDeFase >= SegundosDelGesto)
                    {
                        if (!R.elQueSeSigueNoSeVuelve) anim?.FaceDirection(haciaAtras);
                        foreach (var a in _acompanantes)
                            if (!a.EnRonda && a.Npc != null) a.Npc.SimpleAnimator?.FaceDirection(haciaAtras);
                    }
                    bool esperanAlJugador = hayEspera && Plano(jugador.position, esperaEn) > radioDeEspera;
                    if (Time.time >= finDeFase && !AlgunoDeRonda() && !esperanAlJugador)
                    {
                        hayEspera = false;
                        tramo++;
                        Andar(_lider, ruta[tramo]);
                        fase = Fase.Andando;
                        ultimaPosicion = npc.transform.position;
                        siguienteControlDeAvance = Time.time + SegundosSinAvanzar;
                        reintentosDeAndar = 0;
                    }
                    break;
                }
                case Fase.Alerta:
                {
                    if (quienAvisa?.Npc == null) quienAvisa = _lider;
                    if (Time.time - inicioDeFase >= SegundosDelGesto)
                        quienAvisa.Npc.SimpleAnimator?.FaceTarget(dondeLoNoto);
                    if (Time.time < finDeFase) break;

                    QuitarAviso(quienAvisa);
                    if (AlgunoLeVe(jugador, ref quienAvisa) || AlgunoLeOye(jugador, ref quienAvisa))
                    {
                        yield return Co_Descubierto(quienAvisa.Npc, jugador);
                        if (!SiguenEnEscena(npc, ref jugador))
                        {
                            SinEscena(jugador != null);
                            yield break;
                        }
                        Terminar(PuertoLeVe);
                        yield break;
                    }
                    {
                        // No le ven: siguen la marcha.
                        quienAvisa.Npc.SimpleAnimator?.PlaySearching();
                        ve = false; oye = false;
                        siguienteVigilancia = Time.time + SegundosTrasLaAlerta;
                        bool enLaMarca = tramo == ruta.Count - 1 && Plano(npc.transform.position, ruta[tramo]) < RadioDeMarca;
                        if (enLaMarca) fase = Fase.Esperando;
                        else
                        {
                            Andar(_lider, ruta[tramo]);
                            fase = Fase.Andando;
                            ultimaPosicion = npc.transform.position;
                            siguienteControlDeAvance = Time.time + SegundosSinAvanzar;
                            reintentosDeAndar = 0;
                        }
                    }
                    break;
                }
            }
            yield return null;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[Sigilo:{StepGuid}] se agotan los {R.duracionMaxima:0} s del recorrido con '{R.npcId}' a {Plano(npc.transform.position, destino):0} m de la marca. Sigue por «Llega».");
#endif
        Parar(_lider);
        PonerEnLaMarca(destino, jugador);
        Terminar(PuertoLlega);
    }

    // Pone en la marca a los NPCs del tramo que estén lejos de ella y fuera del alcance del
    // jugador, para que lo que venga después los encuentre donde la historia cree que están.
    private void PonerEnLaMarca(Vector3 destino, Transform jugador)
    {
        Colocar(_lider, destino, jugador);
        if (_acompanantes == null) return;
        for (int i = 0; i < _acompanantes.Count; i++)
        {
            var a = _acompanantes[i];
            var lider = _lider?.Npc != null ? _lider.Npc.transform : null;
            Vector3 sitio = lider != null ? destino - lider.forward * Mathf.Max(1.5f, Mathf.Abs(a.Datos.adelanto)) : destino;
            Colocar(a, SobreElNavMesh(sitio), jugador);
        }
    }

    private void Colocar(Movido m, Vector3 donde, Transform jugador)
    {
        if (m?.Npc == null) return;
        var t = m.Npc.transform;
        if (Plano(t.position, donde) <= R.distanciaParaRecolocar) return;
        // Delante del jugador no se teletransporta a nadie: solo si ni el sitio de ahora ni la marca
        // están a la vista de cerca.
        if (jugador != null && (Plano(jugador.position, t.position) < AlcanceParaNoTeletransportar
                                || Plano(jugador.position, donde) < AlcanceParaNoTeletransportar * 0.5f))
            return;
        Parar(m);
        if (m.Agente != null && m.Agente.enabled && m.Agente.isOnNavMesh) m.Agente.Warp(donde);
        t.position = donde;
        m.Npc.SimpleAnimator?.SyncTargetRotation();
    }

    // La escena puede cambiar con el tramo en marcha (salir al menú, cargar partida, cambiar de
    // personaje): si el cuerpo del jugador es otro, se sigue con el nuevo; si ya no hay jugador o
    // falta el NPC al que se sigue, no se puede seguir. Ver INC-1331.
    private bool SiguenEnEscena(NPCBehaviourManagerV2 npc, ref Transform jugador)
    {
        if (jugador == null) jugador = PlayerService.PlayerTransform;
        return jugador != null && npc != null && _lider != null && _lider.Npc != null;
    }

    // Sin jugador (la escena se está yendo), se suelta todo y el paso espera a que el grafo lo
    // cancele o lo repita al cargar. Con jugador pero sin el NPC, sigue por «Llega» para no
    // bloquear la historia.
    private void SinEscena(bool hayJugador)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[Sigilo:{StepGuid}] {(hayJugador ? $"'{R.npcId}' ya no está en la escena: sigue por «Llega»." : "El jugador ya no está en la escena: se suelta el tramo.")}");
#endif
        if (hayJugador)
        {
            Terminar(PuertoLlega);
            return;
        }
        _rutina = null;
        Soltar();
    }

    // Al acabar el aviso: si alguno ve al jugador, le ha descubierto ('quien' pasa a ser ese).
    private bool AlgunoLeVe(Transform jugador, ref Movido quien)
    {
        if (LeVe(quien, jugador)) return true;
        if (LeVe(_lider, jugador)) { quien = _lider; return true; }
        foreach (var a in _acompanantes)
            if (LeVe(a, jugador)) { quien = a; return true; }
        return false;
    }

    // Al acabar el aviso, quien sigue a tiro de oído y sin esconderse también queda descubierto:
    // plantarse al lado del vigía no puede salir gratis aunque esté de espaldas (INC-1406).
    private bool AlgunoLeOye(Transform jugador, ref Movido quien)
    {
        if (OyeA(quien, jugador)) return true;
        if (OyeA(_lider, jugador)) { quien = _lider; return true; }
        foreach (var a in _acompanantes)
            if (OyeA(a, jugador)) { quien = a; return true; }
        return false;
    }

    private bool OyeA(Movido m, Transform jugador)
        => m?.Npc != null && VistaDelVigia.OyeAlJugador(m.Npc.transform, jugador, R.distanciaDeOido);

    private bool LeVe(Movido m, Transform jugador)
        => m?.Npc != null && VistaDelVigia.VeAlJugador(m.Npc.transform, jugador, R.alcanceDeLaVista, R.anguloDeLaVista);

    // El aviso sobre la cabeza, con el controlador de iconos de siempre en un hijo propio del NPC
    // (como los iconos de misión) para no pisarse con el del raíz.
    private void MostrarAviso(Movido m)
    {
        if (m?.Npc == null || R.iconoDeAlerta == null) return;
        if (m.Aviso == null)
        {
            var npc = m.Npc.transform;
            var hijo = npc.Find(HijoDelAviso);
            if (hijo == null)
            {
                hijo = new GameObject(HijoDelAviso).transform;
                hijo.SetParent(npc, false);
            }
            if (!hijo.TryGetComponent(out m.Aviso))
                m.Aviso = hijo.gameObject.AddComponent<Game.NPC.Common.NPCAlertIconController>();
            m.Aviso.SeguirA(npc);
        }
        m.Aviso.ShowFadingIcon(R.iconoDeAlerta, R.segundosDeAlerta);
    }

    private static void QuitarAviso(Movido m)
    {
        if (m?.Aviso != null) m.Aviso.HideAlertIconImmediate();
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
            else if (SinCamino(a)) Andar(a, a.Ronda[a.PasoDeRonda]);
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
            if (quien == null || jugador == null || _lider?.Npc == null) yield break;
            Vector3 donde = jugador.position;
            quien.SimpleAnimator?.FaceTarget(donde);
            foreach (var a in _acompanantes) if (a.Npc != null) a.Npc.SimpleAnimator?.FaceTarget(donde);
            if (_lider.Npc != quien) _lider.Npc.SimpleAnimator?.FaceTarget(donde);
            yield return null;
        }
    }

    // Devuelve a escena al NPC si la historia lo tenía retirado («no está»): el seguimiento empieza
    // ya y la colocación en la salida va tras un fundido, así que se le puede devolver.
    private static void Presentar(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (NarrativeServices.TryGet(out INarrativeActorPresence presencia, nameof(SeguirSinSerVistoEnCurso)))
            presencia.BringOnStage(id);
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
        // donde acaba su camino, o apareció lejos de la malla). Si está encendido pero fuera de la
        // malla, Warp no basta: hay que apagarlo, mover el transform y volver a encenderlo para que
        // Unity lo enganche sobre el NavMesh (INC-1037).
        if (m.Agente != null && !EnLaMalla(m.Agente))
        {
            if (m.Agente.enabled) m.Agente.enabled = false;
            Game.NPC.Common.NavMeshAgentUtility.SafeEnable(m.Agente, npc.transform, npc.transform.position, RadioParaBuscarMalla);
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

    private static void Preparar(Movido m, float rapidez, bool esquiva = false)
    {
        if (!EnLaMalla(m.Agente)) return;
        m.VelocidadOriginal = m.Agente.speed;
        m.UpdatePositionOriginal = m.Agente.updatePosition;
        // El que va delante y sus acompañantes no deben esquivarse entre sí: en un puente o un
        // camino estrecho, el vigía se planta en la marca y el de atrás se queda bloqueado sin
        // llegar nunca (los dos acaban parados uno junto al otro). Ver INC-1037.
        m.EvitacionOriginal = m.Agente.obstacleAvoidanceType;
        // Solo esquivan los acompañantes, y con menos prioridad que el que va delante: así rodean a
        // este y lo que lleva (un carro sólido, por ejemplo) sin que ninguno se quede bloqueado.
        m.PrioridadOriginal = m.Agente.avoidancePriority;
        m.Agente.obstacleAvoidanceType = esquiva
            ? UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance
            : UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
        if (esquiva) m.Agente.avoidancePriority = Mathf.Max(m.Agente.avoidancePriority, 70);
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
        if (!tapada)
        {
            Fundir(fadeIn: true);
            yield return new WaitForSecondsRealtime(Fundido);
        }

        foreach (var (m, t) in mover)
        {
            // La marca puede estar bajo un puente o sobre una rampa: se coloca en el NavMesh que
            // hay cerca, no en la altura del terreno que hubiera debajo (INC-1037).
            Vector3 destino = SobreElNavMesh(t.position, RadioParaBuscarMalla);
            var agente = m.Agent;
            if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Warp(destino);
            m.transform.SetPositionAndRotation(destino, t.rotation);
            m.SimpleAnimator?.SyncTargetRotation();
        }
        if (moverJugador)
            TeleportService.TeleportToAnchor(jugador.gameObject, R.salidaDelJugador, useTransition: false);

        yield return null;
        Fundir(fadeIn: false);
        yield return new WaitForSecondsRealtime(Fundido);
    }

    // Lanza el fundido sin esperarlo: si el fundido se quedara colgado (otra cinemática, un menú),
    // el seguimiento no puede quedarse parado con los NPCs en la marca.
    private void Fundir(bool fadeIn)
    {
        if (Runner == null) return;
        var rutina = FeedbackService.ScreenFadeAsync(Color.black, Fundido, fadeIn);
        if (rutina != null) Runner.StartCoroutine(rutina);
    }

    private static void Andar(Movido m, Vector3 hacia)
    {
        if (m == null || !EnLaMalla(m.Agente)) return;
        bool estabaQuieto = m.Agente.isStopped || !m.Agente.hasPath;
        m.Agente.isStopped = false;
        bool ok = m.Agente.SetDestination(SinRodeos(m.Agente.transform.position, hacia));
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (!ok) Debug.LogWarning($"[Sigilo] SetDestination falló hacia {hacia:F1}; el NPC no puede llegar.");
#endif
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

    // El destino al que se manda andar: el de verdad si el NavMesh va casi recto, o un punto a
    // pocos metros por la recta si el camino da un rodeo o sube un monte.
    private static Vector3 SinRodeos(Vector3 desde, Vector3 hacia)
    {
        float recta = Plano(desde, hacia);
        if (recta <= TramoRecto) return hacia;
        s_camino ??= new NavMeshPath();
        if (!NavMesh.CalculatePath(desde, hacia, NavMesh.AllAreas, s_camino)) return hacia;
        int n = s_camino.GetCornersNonAlloc(s_esquinas);
        if (n < 2) return hacia;
        float largo = 0f, techo = Mathf.Max(desde.y, hacia.y), alto = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) largo += Vector3.Distance(s_esquinas[i - 1], s_esquinas[i]);
            if (s_esquinas[i].y > alto) alto = s_esquinas[i].y;
        }
        bool rodeo = s_camino.status != NavMeshPathStatus.PathComplete
                     || largo > recta * RodeoMaximo + HolguraDeRodeo
                     || alto > techo + SubidaMaxima;
        if (!rodeo) return hacia;
        Vector3 dir = hacia - desde;
        dir.y = 0f;
        return SobreElNavMesh(desde + dir.normalized * TramoRecto);
    }

    // El agente ha llegado al destino que se le dio (el de verdad o un tramo de la recta).
    private static bool SinCamino(Movido m)
        => EnLaMalla(m?.Agente) && !m.Agente.pathPending && (!m.Agente.hasPath || m.Agente.remainingDistance <= Mathf.Max(0.4f, m.Agente.stoppingDistance + 0.1f));

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
        QuitarAviso(m);
        m.Aviso = null;
        m.Control?.Acabar();
        m.Control = null;
        if (m.Agente != null && m.Preparado)
        {
            m.Agente.speed = m.VelocidadOriginal;
            m.Agente.updatePosition = m.UpdatePositionOriginal;
            m.Agente.obstacleAvoidanceType = m.EvitacionOriginal;
            m.Agente.avoidancePriority = m.PrioridadOriginal;
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

    /// La espera de esta parada, si la tiene: dónde tiene que llegar el jugador para que sigan.
    private bool BuscarEspera(string parada, out Vector3 donde, out float radio)
    {
        donde = Vector3.zero;
        radio = 0f;
        foreach (var e in R.esperas)
        {
            if (e == null || e.enLaParada != parada || string.IsNullOrWhiteSpace(e.hastaQueElJugadorLlegueA)) continue;
            var marca = SpawnAnchor.FindById(e.hastaQueElJugadorLlegueA);
            if (marca == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[Sigilo:{StepGuid}] La marca de espera '{e.hastaQueElJugadorLlegueA}' no está en la escena: no se espera al jugador.");
#endif
                continue;
            }
            donde = marca.transform.position;
            radio = Mathf.Max(0.5f, e.radio);
            return true;
        }
        return false;
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

    private static Vector3 SobreElNavMesh(Vector3 p, float radio = 4f)
        => NavMesh.SamplePosition(p, out var hit, radio, NavMesh.AllAreas) ? hit.position : p;

    private static float Plano(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// Tiene al NPC en su estado de cinemática mientras el nodo lo mueve.
    private sealed class ControlDelNodo : CinematicSequence
    {
        public void Acabar() => IsCompleted = true;
        // El estado de cinemática deja el ritmo de las piernas en manos de la secuencia
        // (AllowManualMovement): se le pasa el del agente para que ande o corra según va, en vez
        // de deslizarse con las piernas quietas.
        public override void Update(Game.NPC.Common.NPCStateContext context)
        {
            var anim = context?.Animator;
            if (anim == null) return;
            var agente = context.Agent;
            bool anda = agente != null && agente.enabled && agente.isOnNavMesh && !agente.isStopped
                        && (agente.hasPath || agente.pathPending);
            anim.SetMovementSpeed(anda ? Game.NPC.Common.NavMeshAgentUtility.FactorDeLocomocion(agente) : 0f);
        }
    }
}
