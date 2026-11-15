using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Algo que el juego sabe hacer y que la historia le pide con el paso «Pedir al juego»
/// (<see cref="GameStepNode"/>). Se registra con un id en <see cref="NarrativeGameSteps"/>: las
/// peticiones con datos propios, con <see cref="NarrativeRequests.Register{T,TRun}"/>; sin código,
/// con un <see cref="NarrativeGameStepHook"/> en la escena que lanza eventos del Inspector.
/// </summary>
public interface INarrativeGameStep
{
    /// <summary>
    /// Empieza. Al acabar se llama a <see cref="NarrativeGameStepCall.Finish(int)"/>: en el acto o
    /// mucho después (al terminar una animación, un minijuego…).
    /// </summary>
    void Begin(NarrativeGameStepCall call);

    /// <summary>
    /// La historia ya no espera (se ha agotado el tiempo, se para el capítulo, se carga partida):
    /// soltar lo que se estuviera haciendo. Después de esto, Finish no hace nada.
    /// </summary>
    void Cancel(NarrativeGameStepCall call);
}

/// <summary>
/// Una petición en curso: los datos que pone el grafo y la forma de avisar de que ha terminado
/// (y por qué salida).
/// </summary>
public sealed class NarrativeGameStepCall
{
    Action<int> _finish;
    Action _abandon;
    readonly string[] _outputs;
    readonly Func<int, bool> _isConnected;

    internal NarrativeGameStepCall(NarrativeRequest request, NarrativeNode step, NarrativeContext context, string[] outputs,
                                   Func<int, bool> isConnected, Action<int> finish, Action abandon)
    {
        Request = request;
        Step = step;
        Context = context;
        Id = request?.Id ?? string.Empty;
        _outputs = outputs is { Length: > 0 } ? outputs : new[] { "Hecho" };
        _isConnected = isConnected;
        _finish = finish;
        _abandon = abandon;
    }

    /// <summary>Id con el que se pidió.</summary>
    public string Id { get; }

    /// <summary>Los datos de la petición, tal y como están en el grafo.</summary>
    public NarrativeRequest Request { get; }

    /// <summary>El paso del grafo que la pide.</summary>
    public NarrativeNode Step { get; }

    /// <summary>El contexto del grafo que la pide (señales, runner…).</summary>
    public NarrativeContext Context { get; }

    /// <summary>Texto libre de una petición sin código (<see cref="NarrativeRequestValues"/>).</summary>
    public string Text => (Request as NarrativeRequestValues)?.text ?? string.Empty;

    /// <summary>Número libre de una petición sin código.</summary>
    public float Number => (Request as NarrativeRequestValues)?.number ?? 0f;

    /// <summary>Asset de una petición sin código, o null.</summary>
    public UnityEngine.Object Asset => (Request as NarrativeRequestValues)?.asset;

    /// <summary>Nombres de las salidas, en orden.</summary>
    public IReadOnlyList<string> Outputs => _outputs;

    /// <summary>true si la salida lleva a algún paso (p. ej. para decidir qué hacer al perder).</summary>
    public bool IsExitConnected(int output) => _isConnected != null && _isConnected(output);

    /// <summary>true en cuanto ha terminado o se ha cancelado.</summary>
    public bool IsOver { get; private set; }

    /// <summary>Se lanza una vez, al terminar, abandonarse o cancelarse.</summary>
    public event Action Ended;

    /// <summary>Termina por la primera salida.</summary>
    public void Finish() => Finish(0);

    /// <summary>Termina por la salida indicada (se ajusta a las que hay). Solo cuenta la primera vez.</summary>
    public void Finish(int output)
    {
        if (IsOver) return;
        var finish = _finish;
        End();
        finish?.Invoke(Mathf.Clamp(output, 0, _outputs.Length - 1));
    }

    /// <summary>Termina por la salida con ese nombre; si no existe, por la primera (y lo avisa).</summary>
    public void Finish(string outputName)
    {
        int index = Array.IndexOf(_outputs, outputName);
        if (index < 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[Pedir al juego '{Id}'] No hay ninguna salida «{outputName}» (hay: {string.Join(", ", _outputs)}). Sigue por «{_outputs[0]}».");
#endif
            index = 0;
        }
        Finish(index);
    }

    /// <summary>
    /// Quien lo atendía ya no puede terminarlo (se ha apagado, ha fallado): la historia no se queda
    /// esperando y sigue por la salida de reserva del paso.
    /// </summary>
    public void Abandon()
    {
        if (IsOver) return;
        var abandon = _abandon;
        End();
        abandon?.Invoke();
    }

    /// <summary>La historia ya no espera: lo que llegue después se ignora.</summary>
    internal void Close()
    {
        if (IsOver) return;
        End();
    }

    void End()
    {
        IsOver = true;
        _finish = null;
        _abandon = null;
        var ended = Ended;
        Ended = null;
        ended?.Invoke();
    }
}
