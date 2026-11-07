using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Espera varias señales a la vez y sigue por la salida de la primera que llega; las demás se
/// dejan de escuchar. Sirve para decisiones que se toman jugando (hacer una cosa u otra): cada
/// forma de resolver la situación emite su señal y el grafo sigue por la rama de la que ocurra antes.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Señales", "Esperar la primera de varias señales", "Escucha varias señales y sigue por la salida de la primera que llega; las demás se dejan de escuchar.")]
[SavePoint("Seguro guardar mientras espera eventos")]
public sealed class WaitFirstSignalNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Una señal por salida, en el orden de las salidas.")]
    public List<string> eventKeys = new();

    [Tooltip("Si está marcado, una señal emitida antes de llegar al nodo también cuenta (sticky). Si se desmarca, solo cuentan las que llegan mientras espera.")]
    public bool acceptEarlierEvent = true;

    [NonSerialized] private INarrativeSignals _signals;
    [NonSerialized] private readonly List<(string key, Action handler)> _escuchas = new();
    [NonSerialized] private bool _resuelto;

    public override string[] GetOutputPorts()
    {
        if (eventKeys == null || eventKeys.Count == 0) return new[] { "—" };
        var puertos = new string[eventKeys.Count];
        for (int i = 0; i < puertos.Length; i++) puertos[i] = string.IsNullOrEmpty(eventKeys[i]) ? "(vacía)" : eventKeys[i];
        return puertos;
    }

    public override void Describe(INarrativeNodeDescriber d)
    {
        if (eventKeys == null) return;
        foreach (var k in eventKeys) d.Signal(k, SignalRole.Escucha);
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (eventKeys == null || eventKeys.Count < 2) warnings.Add("Hacen falta al menos dos señales");
        else if (eventKeys.Exists(string.IsNullOrWhiteSpace)) warnings.Add("Hay una señal vacía");
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        _resuelto = false;
        _signals = ctx?.Signals;
        if (_signals == null || eventKeys == null || eventKeys.Count == 0)
        {
            AdvanceThrough(ctx, ready, 0);
            return;
        }
        for (int i = 0; i < eventKeys.Count; i++)
        {
            string key = eventKeys[i];
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!acceptEarlierEvent) _signals.ClearPendingCustom(key);
            int puerto = i;
            Action handler = null;
            handler = () =>
            {
                if (_resuelto) return;
                _resuelto = true;
                Callar();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log($"[WaitFirstSignal:{guid}] ✅ Primera en llegar: '{key}' → salida {puerto}");
#endif
                AdvanceThrough(ctx, ready, puerto);
            };
            _escuchas.Add((key, handler));
        }
        // Se suscribe después de preparar todas: una señal pendiente puede resolverse en el acto.
        foreach (var (key, handler) in _escuchas.ToArray())
        {
            if (_resuelto) break;
            _signals.OnCustom(key, handler);
        }
    }

    public override void Exit(NarrativeContext ctx) => Callar();

    private void Callar()
    {
        if (_signals != null)
            foreach (var (key, handler) in _escuchas)
                try { _signals.OffCustom(key, handler); } catch { }
        _escuchas.Clear();
    }
}
