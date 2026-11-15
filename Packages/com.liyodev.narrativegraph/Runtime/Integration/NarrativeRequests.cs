using System;
using System.Collections.Generic;

/// <summary>
/// Lo que hace el juego cuando el grafo le pide algo con una petición <typeparamref name="T"/>:
/// una ejecución por cada vez que se pide (pueden coincidir varias en ramas en paralelo).
///
/// <see cref="Start"/> empieza; al acabar se llama a <see cref="Finish"/> con la salida. Antes de
/// que la historia siga se llama a <see cref="Stop"/>, también si la historia deja de esperar
/// (se agota el tiempo, se para el capítulo, se carga partida): ahí se suelta todo lo que se
/// hubiera cogido (suscripciones, corrutinas, bloqueos).
/// </summary>
public abstract class NarrativeRequestRun<T> where T : NarrativeRequest
{
    bool _over;

    /// <summary>Los datos de la petición, tal y como están en el grafo.</summary>
    protected T R { get; private set; }

    /// <summary>La llamada en curso (salidas, si cada salida está conectada…).</summary>
    protected NarrativeGameStepCall Call { get; private set; }

    /// <summary>El contexto del grafo que la pide (señales, runner para corrutinas…).</summary>
    protected NarrativeContext Ctx => Call?.Context;

    /// <summary>El runner que la pide: sirve para lanzar corrutinas.</summary>
    protected NarrativeRunner Runner => Call?.Context?.Runner;

    /// <summary>Guid del paso que la pide (para los avisos).</summary>
    protected string StepGuid => Call?.Step?.guid;

    /// <summary>Título del paso que la pide (para los avisos).</summary>
    protected string StepTitle => Call?.Step?.displayTitle;

    internal void Begin(T request, NarrativeGameStepCall call)
    {
        R = request;
        Call = call;
        Start();
    }

    internal void Cancel()
    {
        if (_over) return;
        _over = true;
        Stop();
    }

    /// <summary>Empieza lo que pide la petición.</summary>
    protected abstract void Start();

    /// <summary>Suelta lo que tuviera cogido. Puede llamarse con la ejecución ya parada.</summary>
    protected virtual void Stop() {}

    /// <summary>Termina por la salida indicada. Solo cuenta la primera vez.</summary>
    protected void Finish(int exit = 0)
    {
        if (_over) return;
        _over = true;
        Stop();
        Call.Finish(exit);
    }

    /// <summary>true si la salida <paramref name="exit"/> lleva a algún paso.</summary>
    protected bool IsExitConnected(int exit) => Call != null && Call.IsExitConnected(exit);
}

/// <summary>
/// Registro de los manejadores de las peticiones del juego. El juego apunta aquí cada tipo de
/// petición con su ejecución (al arrancar, una vez); el grafo no conoce a ninguno.
/// </summary>
public static class NarrativeRequests
{
    /// <summary>Atiende las peticiones <typeparamref name="T"/> con una <typeparamref name="TRun"/> nueva cada vez.</summary>
    public static void Register<T, TRun>() where T : NarrativeRequest where TRun : NarrativeRequestRun<T>, new()
        => NarrativeGameSteps.Register(typeof(T).FullName, new Handler<T, TRun>());

    sealed class Handler<T, TRun> : INarrativeGameStep where T : NarrativeRequest where TRun : NarrativeRequestRun<T>, new()
    {
        readonly Dictionary<NarrativeGameStepCall, TRun> _runs = new();

        public void Begin(NarrativeGameStepCall call)
        {
            if (call.Request is not T request)
            {
                call.Abandon();
                return;
            }
            var run = new TRun();
            _runs[call] = run;
            call.Ended += () => _runs.Remove(call);
            run.Begin(request, call);
        }

        public void Cancel(NarrativeGameStepCall call)
        {
            if (_runs.TryGetValue(call, out var run))
            {
                _runs.Remove(call);
                run.Cancel();
            }
        }

        public override string ToString() => $"{typeof(TRun).Name} ({typeof(T).Name})";
    }
}
