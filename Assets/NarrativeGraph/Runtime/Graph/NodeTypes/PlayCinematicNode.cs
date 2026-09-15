using System;
using UnityEngine;

/// <summary>
/// Lanza una cinemática (XxxSequencer : CinematicSequencerBase) y espera a que termine, todo en
/// un solo nodo. Sustituye al par RaiseCustomEventNode("X_START") + WaitCustomEventNode("X_DONE")
/// que había que casar a mano y que dejaba el grafo mudo sobre qué cinemática era.
///
/// El sequencer sigue siendo una caja negra en C# (ver TDD § 10, patrón "obra de teatro"): este
/// nodo solo conoce sus señales de entrada/salida.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Cinemáticas", "Reproducir cinemática", "Emite la señal de entrada de un Sequencer y espera su señal de fin (Hecho) o de fallo.")]
[UnsafeForSave("Cinemática en curso")]
public sealed class PlayCinematicNode : NarrativeNode
{
    [Tooltip("Nombre legible (solo para el editor): 'Despertar de la estrella', 'Taberna'...")]
    public string cinematicName;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Señal que escucha el Sequencer (_signalIn). Ej: AWAKEN_START")]
    public string signalIn;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Señal que emite el Sequencer al terminar bien (_signalOut). Ej: AWAKEN_DONE")]
    public string signalDone;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Opcional: señal que emite el Sequencer si la secuencia falla/aborta (salida 'Fallo'). Ej: AWAKEN_FAILED")]
    public string signalFailed;

    static readonly string[] Ports = { "Hecho", "Fallo" };
    public override string[] GetOutputPorts() => Ports;

    [NonSerialized] private INarrativeSignals _signals;
    [NonSerialized] private Action _onDone;
    [NonSerialized] private Action _onFailed;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (string.IsNullOrWhiteSpace(signalIn) || string.IsNullOrWhiteSpace(signalDone))
        {
            Debug.LogWarning($"[PlayCinematicNode:{guid}] '{cinematicName}': faltan signalIn/signalDone → avanzando por 'Hecho'.");
            AdvanceThrough(ctx, ready, 0);
            return;
        }
        if (ctx?.Signals == null)
        {
            Debug.LogWarning($"[PlayCinematicNode:{guid}] Sin proveedor de señales → avanzando por 'Hecho'.");
            AdvanceThrough(ctx, ready, 0);
            return;
        }

        _signals = ctx.Signals;

        // Las señales de fin son "en vivo": una DONE/FAILED de una reproducción anterior no vale.
        _signals.ClearPendingCustom(signalDone);
        if (!string.IsNullOrWhiteSpace(signalFailed)) _signals.ClearPendingCustom(signalFailed);

        _onDone = () =>
        {
            Unsubscribe();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PlayCinematicNode:{guid}] ✅ '{cinematicName}' terminada ({signalDone})");
#endif
            AdvanceThrough(ctx, ready, 0);
        };
        _signals.OnCustom(signalDone, _onDone);

        if (!string.IsNullOrWhiteSpace(signalFailed))
        {
            _onFailed = () =>
            {
                Unsubscribe();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[PlayCinematicNode:{guid}] ⚠ '{cinematicName}' falló ({signalFailed})");
#endif
                AdvanceThrough(ctx, ready, 1);
            };
            _signals.OnCustom(signalFailed, _onFailed);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PlayCinematicNode:{guid}] ▶ '{cinematicName}': emitiendo {signalIn}, esperando {signalDone}");
#endif
        _signals.RaiseCustom(signalIn, $"[PlayCinematicNode] {cinematicName}");
    }

    public override void Exit(NarrativeContext ctx) => Unsubscribe();

    private void Unsubscribe()
    {
        if (_signals != null)
        {
            try
            {
                if (_onDone != null) _signals.OffCustom(signalDone, _onDone);
                if (_onFailed != null && !string.IsNullOrWhiteSpace(signalFailed)) _signals.OffCustom(signalFailed, _onFailed);
            }
            catch { }
        }
        _signals = null;
        _onDone = null;
        _onFailed = null;
    }
}
