using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// «Pedir al juego»: el paso con el que la historia le pide al juego que haga algo (una
/// cinemática, un combate, un minijuego, cambiar la hora…) y sigue cuando el juego avisa, por la
/// salida que diga. El grafo no sabe qué pasa dentro: solo emite la petición
/// (<see cref="NarrativeRequest"/>, con sus datos) y sabe lo que ella declara que deja hecho.
///
/// El juego atiende cada petición por su id (<see cref="NarrativeGameSteps"/>): las peticiones
/// con datos propios, con un manejador registrado en <see cref="NarrativeRequests"/>; sin código,
/// con un <see cref="NarrativeGameStepHook"/> en la escena.
///
/// La guarda: la historia nunca se queda colgada por él. Si nadie atiende el id, si tarda más del
/// tiempo máximo o si quien lo atendía lo deja a medias, sigue por la salida de reserva y lo deja
/// dicho en la consola.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Otros", "Pedir al juego", "Le pide al juego algo que sabe hacer (por su id) y sigue cuando avisa. Si nadie lo atiende, la historia no se cuelga.")]
[UnsafeForSave("Esperando al juego")]
public sealed class GameStepNode : NarrativeNode, INarrativeStateEffect, INarrativeRouteEffect
{
    public enum IfNobodyAnswers
    {
        /// Sigue en el acto por la salida de reserva.
        GoOn,
        /// Espera a que alguien se apunte con ese id (p. ej. al cargarse su escena).
        WaitForIt,
    }

    const string FallbackExitName = "Sin respuesta";

    [SerializeReference]
    [Tooltip("Lo que se le pide al juego, con sus datos.")]
    public NarrativeRequest request = new NarrativeRequestValues();

    [Tooltip("Si nadie atiende el id al llegar: seguir por la salida de reserva, o esperar a que aparezca.")]
    public IfNobodyAnswers ifNobodyAnswers = IfNobodyAnswers.GoOn;

    [Tooltip("Segundos de juego como mucho esperando (a que aparezca y a que termine); 0 = sin límite. Al agotarse se cancela y sigue por la salida de reserva.")]
    [Min(0f)] public float timeoutSeconds;

    /// <summary>Un paso que pide <paramref name="request"/> (para montajes por código).</summary>
    public static GameStepNode For(NarrativeRequest request) => new() { request = request };

    /// <summary>El tipo de petición: es lo que el editor enseña como tipo del paso.</summary>
    public override Type KindType => request != null && request is not NarrativeRequestValues ? request.GetType() : GetType();

    public override IEnumerable<object> FieldOwners()
    {
        yield return this;
        if (request != null) yield return request;
    }

    NarrativeRequestValues Values => request as NarrativeRequestValues;
    bool SeparateFallback => Values?.separateFallbackExit ?? false;
    bool WaitUntilFinished => Values?.waitUntilFinished ?? true;
    string RequestId => request?.Id;

    // ── Puertos ─────────────────────────────────────────────────────────────

    string[] GameExits()
    {
        var exits = request?.Exits;
        return exits is { Length: > 0 } ? exits : new[] { "Hecho" };
    }

    /// Por dónde sigue cuando el juego no responde: «Sin respuesta» si la petición la pide, o la primera.
    int FallbackPort => SeparateFallback ? GameExits().Length : 0;

    public override string[] GetOutputPorts()
    {
        var exits = GameExits();
        if (!SeparateFallback) return exits;
        var ports = new string[exits.Length + 1];
        exits.CopyTo(ports, 0);
        ports[exits.Length] = FallbackExitName;
        return ports;
    }

    /// Al rehacer la historia sin jugarla, lo que diga la petición (por defecto, su salida normal).
    public override int ProjectionPort => request?.ProjectionExit ?? 0;

    public override string DescribeDecision() => request?.DescribeDecision(this) ?? base.DescribeDecision();

    // ── Lo que cuenta a las herramientas ────────────────────────────────────

    public void Project(INarrativeStateWriter state) => (request as INarrativeStateEffect)?.Project(state);

    public void ProjectRoute(INarrativeStateWriter state, int chosenPort) => (request as INarrativeRouteEffect)?.ProjectRoute(state, chosenPort);

    public override void Describe(INarrativeNodeDescriber d)
    {
        if (request == null) { d.Line("(sin petición)", NarrativeTextTone.Strong); return; }
        request.Describe(d);
        if (ifNobodyAnswers == IfNobodyAnswers.WaitForIt) d.Tag("espera a que aparezca");
        if (timeoutSeconds > 0f) d.Tag($"máx. {timeoutSeconds:0.#} s");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (request == null) { warnings.Add("No pide nada al juego"); return; }
        request.CollectWarnings(warnings);
        if (ifNobodyAnswers == IfNobodyAnswers.WaitForIt && timeoutSeconds <= 0f)
            warnings.Add("Espera sin límite a que alguien lo atienda: si nunca aparece, la historia se queda aquí (pon un tiempo máximo)");
    }

    // ── Ejecución ───────────────────────────────────────────────────────────

    [NonSerialized] NarrativeContext _ctx;
    [NonSerialized] Action _ready;
    [NonSerialized] bool _active;
    [NonSerialized] bool _waitingForHandler;
    [NonSerialized] INarrativeGameStep _handler;
    [NonSerialized] NarrativeGameStepCall _call;
    [NonSerialized] Coroutine _timeout;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        Cleanup();
        _ctx = ctx;
        _ready = ready;
        _active = true;

        var id = RequestId;
        if (string.IsNullOrWhiteSpace(id)) { Leave(FallbackPort, "no tiene id"); return; }
        if (NarrativeGameSteps.TryGet(id, out var handler)) { Begin(handler); return; }
        if (ifNobodyAnswers == IfNobodyAnswers.GoOn) { Leave(FallbackPort, "nadie lo atiende"); return; }

        _waitingForHandler = true;
        NarrativeGameSteps.Registered += OnRegistered;
        StartTimeout();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Pedir al juego «{Name}»] Nadie lo atiende aún: espera a que aparezca «{id}».");
#endif
    }

    public override void Exit(NarrativeContext ctx) => Cleanup();

    void OnRegistered(string id)
    {
        if (!_active || id != RequestId || !NarrativeGameSteps.TryGet(id, out var handler)) return;
        StopWaitingForHandler();
        Begin(handler);
    }

    void Begin(INarrativeGameStep handler)
    {
        _handler = handler;
        bool wait = WaitUntilFinished;
        NarrativeGameStepCall call = null;
        call = new NarrativeGameStepCall(request, this, _ctx, GameExits(), IsPortConnected,
            port => OnCallOver(call, port, null),
            () => OnCallOver(call, FallbackPort, "quien lo atendía lo ha dejado a medias"));

        // Sin esperar, lo que haga el juego después ya no es cosa de la historia: no se cancela al salir.
        if (wait) { _call = call; StartTimeout(); }

        try { handler.Begin(call); }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (wait) { _call = null; call.Close(); Leave(FallbackPort, "ha fallado al empezar"); return; }
        }
        if (!wait) Leave(0, null);
    }

    bool IsPortConnected(int port) => !string.IsNullOrEmpty(GetOutputGuid(port));

    void OnCallOver(NarrativeGameStepCall call, int port, string problem)
    {
        if (!_active || call != _call) return;
        _call = null;
        Leave(port, problem);
    }

    /// Sale del paso por <paramref name="port"/>; con <paramref name="problem"/>, lo avisa.
    void Leave(int port, string problem)
    {
        if (!_active) return;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (problem != null)
        {
            var ports = GetOutputPorts();
            Debug.LogWarning($"[Pedir al juego «{Name}»] {problem}: la historia sigue por «{ports[Mathf.Clamp(port, 0, ports.Length - 1)]}».");
        }
#endif
        var ctx = _ctx;
        var ready = _ready;
        Cleanup();
        AdvanceThrough(ctx, ready, port);
    }

    void StartTimeout()
    {
        if (timeoutSeconds <= 0f || _timeout != null || _ctx?.Runner == null) return;
        _timeout = _ctx.Runner.StartCoroutine(TimeoutRoutine());
    }

    IEnumerator TimeoutRoutine()
    {
        yield return new WaitForSeconds(timeoutSeconds);
        _timeout = null;
        if (_active) Leave(FallbackPort, $"no ha terminado en {timeoutSeconds:0.#} s");
    }

    void StopWaitingForHandler()
    {
        if (!_waitingForHandler) return;
        _waitingForHandler = false;
        NarrativeGameSteps.Registered -= OnRegistered;
    }

    /// Suelta todo lo que tenga el paso: la espera, el tiempo y, si el juego no había terminado, le
    /// avisa de que la historia ya no espera.
    void Cleanup()
    {
        _active = false;
        StopWaitingForHandler();
        if (_timeout != null)
        {
            if (_ctx?.Runner != null) _ctx.Runner.StopCoroutine(_timeout);
            _timeout = null;
        }
        var call = _call;
        var handler = _handler;
        _call = null;
        _handler = null;
        if (call != null && !call.IsOver)
        {
            try { handler?.Cancel(call); }
            catch (Exception e) { Debug.LogException(e); }
            call.Close();
        }
        _ctx = null;
        _ready = null;
    }

    string Name => !string.IsNullOrEmpty(displayTitle) ? displayTitle : RequestId;
}
