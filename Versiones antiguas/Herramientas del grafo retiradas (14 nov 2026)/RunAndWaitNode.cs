using System;
using System.Collections.Generic;
using UnityEngine;

/// Asks a system of the game to do something and waits until it says it has finished: a
/// cinematic, a minigame, a fight, a puzzle... The step raises "start" and goes on when the
/// system raises "done" (or "failed", through the second way out). Any system can answer: by
/// code through the signals, or in the scene with a NarrativeSignalReactor.
[Serializable]
[NarrativeNodeInfo("Cinemáticas", "Lanzar y esperar", "Pide a un sistema del juego que empiece algo (cinemática, minijuego, combate…) y espera a que avise de que ha terminado.")]
[UnsafeForSave("Esperando a que termine")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
public sealed class RunAndWaitNode : NarrativeNode, INarrativeStateEffect
{
    [Tooltip("What it is, for people: 'Farewell at the gate', 'Fishing minigame'...")]
    public string label;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Signal raised to start it. Example: FAREWELL_START")]
    public string startSignal;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Signal the system raises when it finishes. Example: FAREWELL_DONE")]
    public string doneSignal;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Optional: signal raised if it goes wrong or is cancelled (second way out).")]
    public string failedSignal;

    static readonly string[] Ports = { "Hecho", "Fallo" };
    public override string[] GetOutputPorts() => Ports;

    /// When rebuilding the story, the normal ending is taken.
    public override int ProjectionPort => 0;

    public void Project(INarrativeStateWriter state)
    {
        state.MarkSignalRaised(startSignal);
        state.MarkSignalRaised(doneSignal);
    }

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Line(string.IsNullOrEmpty(label) ? "(sin nombre)" : label, NarrativeTextTone.Strong);
        d.Signal(startSignal, SignalRole.Emite, "empieza");
        d.Signal(doneSignal, SignalRole.Escucha, "hecho");
        if (!string.IsNullOrEmpty(failedSignal)) d.Signal(failedSignal, SignalRole.Escucha, "fallo");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(startSignal) || string.IsNullOrWhiteSpace(doneSignal))
            warnings.Add("Faltan la señal de empezar o la de terminado");
    }

    [NonSerialized] INarrativeSignals _signals;
    [NonSerialized] Action _onDone;
    [NonSerialized] Action _onFailed;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (string.IsNullOrWhiteSpace(startSignal) || string.IsNullOrWhiteSpace(doneSignal) || ctx?.Signals == null)
        {
            Debug.LogWarning($"[RunAndWait:{guid}] '{label}': missing signals or signal system, going on through 'Hecho'.");
            AdvanceThrough(ctx, ready, 0);
            return;
        }

        _signals = ctx.Signals;
        // A "done" left over from an earlier run must not count.
        _signals.ClearPendingCustom(doneSignal);
        if (!string.IsNullOrWhiteSpace(failedSignal)) _signals.ClearPendingCustom(failedSignal);

        _onDone = () => { Unsubscribe(); AdvanceThrough(ctx, ready, 0); };
        _signals.OnCustom(doneSignal, _onDone);
        if (!string.IsNullOrWhiteSpace(failedSignal))
        {
            _onFailed = () => { Unsubscribe(); AdvanceThrough(ctx, ready, 1); };
            _signals.OnCustom(failedSignal, _onFailed);
        }
        _signals.RaiseCustom(startSignal, $"[RunAndWait] {label}");
    }

    public override void Exit(NarrativeContext ctx) => Unsubscribe();

    void Unsubscribe()
    {
        if (_signals == null) return;
        if (_onDone != null) _signals.OffCustom(doneSignal, _onDone);
        if (_onFailed != null) _signals.OffCustom(failedSignal, _onFailed);
        _onDone = null;
        _onFailed = null;
    }
}
