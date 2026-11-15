using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// El comodín del grafo: para lo que ningún otro paso cubre. Pide al juego algo que ya sabe hacer
/// (un INarrativeGameStep apuntado con un id en NarrativeGameSteps: un NarrativeGameStepHook de la
/// escena o un registro por código) y sigue cuando el juego avisa, por la salida que diga.
///
/// La guarda: la historia nunca se queda colgada por él. Si nadie atiende el id, si tarda más del
/// tiempo máximo o si quien lo atendía desaparece a medias, sigue por la salida de reserva
/// («Sin respuesta», o la primera) y lo deja dicho en la consola.
[Serializable]
[NarrativeNodeInfo("Otros", "Paso del juego", "Para lo que ningún otro paso cubre: pide al juego algo que ya sabe hacer (por su id) y sigue cuando avisa. Si nadie lo atiende, la historia no se cuelga.")]
[UnsafeForSave("Esperando a un sistema del juego")]
public sealed class GameStepNode : NarrativeNode
{
    public enum IfNobodyAnswers
    {
        /// Sigue en el acto por la salida de reserva.
        GoOn,
        /// Espera a que alguien se apunte con ese id (p. ej. al cargarse su escena).
        WaitForIt,
    }

    const string DefaultExitName = "Hecho";
    const string FallbackExitName = "Sin respuesta";

    [Tooltip("Qué es, para personas: «Abrir la compuerta del molino», «Minijuego de pesca»…")]
    public string label;

    [NarrativeKey(NarrativeKeyKind.GameStep)]
    [Tooltip("Id del paso en el juego: el de un NarrativeGameStepHook de la escena o el que registra el código con NarrativeGameSteps.Register.")]
    public string stepId;

    [Header("Datos para el juego (opcionales)")]
    [Tooltip("Texto libre que recibe el juego (un nombre, una clave…).")]
    public string text;

    [Tooltip("Número libre que recibe el juego.")]
    public float number;

    [Tooltip("Asset que recibe el juego (un ScriptableObject suyo, un prefab…).")]
    public UnityEngine.Object asset;

    [Header("Salidas")]
    [Tooltip("Salidas por las que puede terminar el juego, en orden. Vacío = una sola, «Hecho».")]
    public List<string> exitNames = new() { DefaultExitName };

    [Tooltip("Espera a que el juego avise de que ha terminado. Si no, lo pide y sigue en el acto por la primera salida.")]
    public bool waitUntilFinished = true;

    [Header("Guarda")]
    [Tooltip("Si nadie atiende el id al llegar: seguir por la salida de reserva, o esperar a que aparezca.")]
    public IfNobodyAnswers ifNobodyAnswers = IfNobodyAnswers.GoOn;

    [Tooltip("Segundos de juego como mucho esperando (a que aparezca y a que termine); 0 = sin límite. Al agotarse se cancela y sigue por la salida de reserva.")]
    [Min(0f)] public float timeoutSeconds;

    [Tooltip("Pone la salida «Sin respuesta» para la reserva. Si no, la reserva es la primera salida.")]
    public bool separateFallbackExit = true;

    // ── Puertos ─────────────────────────────────────────────────────────────

    /// Las salidas que puede elegir el juego (sin la de reserva).
    string[] GameExits()
    {
        int count = exitNames?.Count ?? 0;
        if (count == 0) return new[] { DefaultExitName };
        var names = new string[count];
        for (int i = 0; i < count; i++) names[i] = string.IsNullOrWhiteSpace(exitNames[i]) ? $"Salida {i + 1}" : exitNames[i];
        return names;
    }

    int GameExitCount => Mathf.Max(1, exitNames?.Count ?? 0);

    /// Por dónde sigue cuando el juego no responde.
    int FallbackPort => separateFallbackExit ? GameExitCount : 0;

    public override string[] GetOutputPorts()
    {
        var exits = GameExits();
        if (!separateFallbackExit) return exits;
        var ports = new string[exits.Length + 1];
        exits.CopyTo(ports, 0);
        ports[exits.Length] = FallbackExitName;
        return ports;
    }

    /// Al rehacer la historia sin jugarla se supone que el juego terminó por su salida normal.
    public override int ProjectionPort => 0;

    // ── Lo que cuenta a las herramientas ────────────────────────────────────

    public override void Describe(INarrativeNodeDescriber d)
    {
        string what = !string.IsNullOrEmpty(label) ? label : !string.IsNullOrEmpty(stepId) ? stepId : "(sin id)";
        d.Line(what, NarrativeTextTone.Strong);
        d.Field("Pide al juego", string.IsNullOrEmpty(stepId) ? "—" : stepId);
        if (!string.IsNullOrEmpty(text)) d.Field("Texto", text);
        if (number != 0f) d.Field("Número", number.ToString("0.##"));
        if (asset != null) d.Asset(asset, "dato del paso del juego");
        d.Tag(waitUntilFinished ? "espera a que termine" : "no espera");
        if (ifNobodyAnswers == IfNobodyAnswers.WaitForIt) d.Tag("espera a que aparezca");
        if (timeoutSeconds > 0f) d.Tag($"máx. {timeoutSeconds:0.#} s");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(stepId)) warnings.Add("Falta el id del paso del juego");
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

        if (string.IsNullOrWhiteSpace(stepId)) { Leave(FallbackPort, "no tiene id"); return; }
        if (NarrativeGameSteps.TryGet(stepId, out var handler)) { Begin(handler); return; }
        if (ifNobodyAnswers == IfNobodyAnswers.GoOn) { Leave(FallbackPort, "nadie lo atiende"); return; }

        _waitingForHandler = true;
        NarrativeGameSteps.Registered += OnRegistered;
        StartTimeout();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Paso del juego «{Name}»] Nadie lo atiende aún: espera a que aparezca «{stepId}».");
#endif
    }

    public override void Exit(NarrativeContext ctx) => Cleanup();

    void OnRegistered(string id)
    {
        if (!_active || id != stepId || !NarrativeGameSteps.TryGet(id, out var handler)) return;
        StopWaitingForHandler();
        Begin(handler);
    }

    void Begin(INarrativeGameStep handler)
    {
        _handler = handler;
        NarrativeGameStepCall call = null;
        call = new NarrativeGameStepCall(stepId, text, number, asset, GameExits(),
            port => OnCallOver(call, port, null),
            () => OnCallOver(call, FallbackPort, "quien lo atendía lo ha dejado a medias"));

        // Sin esperar, lo que haga el juego después ya no es cosa de la historia: no se cancela al salir.
        if (waitUntilFinished) { _call = call; StartTimeout(); }

        try { handler.Begin(call); }
        catch (Exception e)
        {
            Debug.LogException(e);
            call.Close();
            if (waitUntilFinished) { _call = null; Leave(FallbackPort, "ha fallado al empezar"); return; }
        }
        if (!waitUntilFinished) Leave(0, null);
    }

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
            Debug.LogWarning($"[Paso del juego «{Name}»] {problem}: la historia sigue por «{ports[Mathf.Clamp(port, 0, ports.Length - 1)]}».");
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
            call.Close();
            try { handler?.Cancel(call); }
            catch (Exception e) { Debug.LogException(e); }
        }
        _ctx = null;
        _ready = null;
    }

    string Name => !string.IsNullOrEmpty(label) ? label : stepId;
}
