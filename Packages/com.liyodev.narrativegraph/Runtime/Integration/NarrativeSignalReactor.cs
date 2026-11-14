using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// Connects the story with anything in the scene, without code. Each entry listens for a
/// signal and runs its events (play a Timeline, open a door, start a minigame...). To tell the
/// story something finished, call Raise("SOMETHING_DONE") from an event, an animation or a
/// Timeline signal.
[AddComponentMenu("Narrative Graph/Signal Reactor")]
public sealed class NarrativeSignalReactor : MonoBehaviour
{
    [Serializable]
    public sealed class Reaction
    {
        [NarrativeKey(NarrativeKeyKind.Signal)]
        [Tooltip("Signal to listen for. Example: FAREWELL_START")]
        public string signal;
        public UnityEvent onSignal = new();
    }

    public List<Reaction> reactions = new();

    readonly List<(string key, Action handler)> _listening = new();

    void OnEnable()
    {
        var signals = DefaultNarrativeSignals.EnsureInstance();
        foreach (var reaction in reactions)
        {
            if (reaction == null || string.IsNullOrWhiteSpace(reaction.signal)) continue;
            var captured = reaction;
            Action handler = null;
            handler = () =>
            {
                captured.onSignal.Invoke();
                // Listening again: the same signal may come back later in the story.
                signals.OffCustom(captured.signal, handler);
                signals.OnCustom(captured.signal, handler);
            };
            signals.OnCustom(reaction.signal, handler);
            _listening.Add((reaction.signal, handler));
        }
    }

    void OnDisable()
    {
        var signals = DefaultNarrativeSignals.Instance;
        if (signals != null) foreach (var (key, handler) in _listening) signals.OffCustom(key, handler);
        _listening.Clear();
    }

    /// Raises a signal for the story. Meant to be called from events: Raise("FAREWELL_DONE").
    public void Raise(string signal)
    {
        if (!string.IsNullOrWhiteSpace(signal)) DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signal, $"[Reactor] {name}");
    }
}
