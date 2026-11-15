using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Algo que el juego ya sabe hacer y que la historia puede pedir con el paso «Paso del juego»
/// (<see cref="GameStepNode"/>): lo que ningún otro paso del grafo cubre. Se registra con un id en
/// <see cref="NarrativeGameSteps"/>; sin código, con un <see cref="NarrativeGameStepHook"/> en la
/// escena que lanza eventos del Inspector.
/// </summary>
public interface INarrativeGameStep
{
    /// <summary>
    /// Empieza el paso. Al acabar se llama a <see cref="NarrativeGameStepCall.Finish(int)"/>: en el
    /// acto o mucho después (al terminar una animación, un minijuego…).
    /// </summary>
    void Begin(NarrativeGameStepCall call);

    /// <summary>
    /// La historia ya no espera (se ha agotado el tiempo, se para el capítulo, se carga partida):
    /// soltar lo que se estuviera haciendo. Después de esto, Finish no hace nada.
    /// </summary>
    void Cancel(NarrativeGameStepCall call);
}

/// <summary>
/// Una petición en curso a un paso del juego: los datos que pone el grafo y la forma de avisar
/// de que ha terminado (y por qué salida).
/// </summary>
public sealed class NarrativeGameStepCall
{
    Action<int> _finish;
    Action _abandon;
    readonly string[] _outputs;

    internal NarrativeGameStepCall(string id, string text, float number, UnityEngine.Object asset, string[] outputs, Action<int> finish, Action abandon)
    {
        _abandon = abandon;
        Id = id;
        Text = text ?? string.Empty;
        Number = number;
        Asset = asset;
        _outputs = outputs is { Length: > 0 } ? outputs : new[] { "Hecho" };
        _finish = finish;
    }

    /// <summary>Id con el que se pidió el paso.</summary>
    public string Id { get; }

    /// <summary>Texto libre que pone el grafo (un nombre, una clave…).</summary>
    public string Text { get; }

    /// <summary>Número libre que pone el grafo.</summary>
    public float Number { get; }

    /// <summary>Asset que pone el grafo (un ScriptableObject del juego, un prefab…), o null.</summary>
    public UnityEngine.Object Asset { get; }

    /// <summary>Nombres de las salidas del paso, en orden.</summary>
    public IReadOnlyList<string> Outputs => _outputs;

    /// <summary>true en cuanto ha terminado o se ha cancelado.</summary>
    public bool IsOver { get; private set; }

    /// <summary>Termina por la primera salida.</summary>
    public void Finish() => Finish(0);

    /// <summary>Termina por la salida indicada (se ajusta a las que hay). Solo cuenta la primera vez.</summary>
    public void Finish(int output)
    {
        if (IsOver) return;
        IsOver = true;
        var finish = _finish;
        _finish = null;
        _abandon = null;
        finish?.Invoke(Mathf.Clamp(output, 0, _outputs.Length - 1));
    }

    /// <summary>
    /// Quien lo atendía ya no puede terminarlo (se ha apagado, ha fallado): la historia no se queda
    /// esperando y sigue por la salida de reserva del paso.
    /// </summary>
    public void Abandon()
    {
        if (IsOver) return;
        IsOver = true;
        var abandon = _abandon;
        _finish = null;
        _abandon = null;
        abandon?.Invoke();
    }

    /// <summary>Termina por la salida con ese nombre; si no existe, por la primera (y lo avisa).</summary>
    public void Finish(string outputName)
    {
        int index = Array.IndexOf(_outputs, outputName);
        if (index < 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[Paso del juego '{Id}'] No hay ninguna salida «{outputName}» (hay: {string.Join(", ", _outputs)}). Sigue por «{_outputs[0]}».");
#endif
            index = 0;
        }
        Finish(index);
    }

    /// <summary>La historia ya no espera: lo que llegue después se ignora.</summary>
    internal void Close()
    {
        IsOver = true;
        _finish = null;
        _abandon = null;
    }
}
