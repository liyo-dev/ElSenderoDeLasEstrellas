using System;
using UnityEngine;
using UnityEngine.AI;

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

    [Header("Secuencia de datos (opcional)")]
    [Tooltip("Si se asigna (con signalIn/signalDone iguales a los suyos), y si ninguna escena tiene un SequencePlayer para esta secuencia, se monta uno en vivo con los " +
             "ajustes (cámara, audio, transiciones) del SequencePlayer 'plantillaDeAjustes'. Así una " +
             "secuencia nueva no obliga a tocar la escena.")]
    public SequenceDefinition secuencia;

    [Tooltip("Nombre del GameObject del SequencePlayer ya montado del que copiar los ajustes.")]
    public string plantillaDeAjustes = "SEQ_PerasEldran";

    [Tooltip("Actor que se coloca más cerca del jugador ANTES de empezar, fuera de plano, si está a " +
             "más de 'distanciaMaxima' (para que un «viene hacia ti» no dure medio minuto). Vacío = nada.")]
    [NarrativeKey(NarrativeKeyKind.Actor)]
    public string acercarActor;
    public float distanciaMaxima = 24f;
    public float distanciaDeAparicion = 12f;

    static readonly string[] Ports = { "Hecho", "Fallo" };
    public override string[] GetOutputPorts() => Ports;

    [NonSerialized] private INarrativeSignals _signals;
    [NonSerialized] private Action _onDone;
    [NonSerialized] private Action _onFailed;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (secuencia != null)
        {
            if (signalIn != secuencia.signalIn || signalDone != secuencia.signalOut)
                Debug.LogWarning($"[PlayCinematicNode:{guid}] Las señales del nodo ({signalIn}/{signalDone}) no " +
                                 $"coinciden con las de '{secuencia.name}' ({secuencia.signalIn}/{secuencia.signalOut}).");
            MontarSiFalta();
            AcercarActor();
        }

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

    /// Si ninguna escena escucha esta secuencia, monta un SequencePlayer en vivo con los ajustes
    /// de la plantilla. La copia se destruye sola un rato después de terminar (el SequencePlayer
    /// retiene a los NPCs unos segundos más tras la secuencia; ver _idleGraceAfterSequence).
    private void MontarSiFalta()
    {
        SequencePlayer molde = null;
        foreach (var sp in UnityEngine.Object.FindObjectsByType<SequencePlayer>())
        {
            if (sp.Definition == secuencia) return;
            if (sp.name == plantillaDeAjustes) molde = sp;
        }
        if (molde == null)
        {
            Debug.LogError($"[PlayCinematicNode:{guid}] '{secuencia.name}' no está montada en la escena y no " +
                           $"encuentro la plantilla '{plantillaDeAjustes}' para montarla en vivo.");
            return;
        }
        var copia = SequencePlayer.CrearCopiaPara(secuencia, molde);
        if (copia != null) copia.RegisterCleanup(() => UnityEngine.Object.Destroy(copia.gameObject, 6f));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PlayCinematicNode] '{secuencia.name}' montada en vivo con los ajustes de '{molde.name}'.");
#endif
    }

    private void AcercarActor()
    {
        if (string.IsNullOrEmpty(acercarActor)) return;
        if (!SequenceActor.TryResolve(acercarActor, out var actor) || actor.Transform == null) return;
        if (!SequenceActor.TryResolve(SequenceActor.PlayerId, out var jugador) || jugador.Transform == null) return;

        Vector3 pj = jugador.Transform.position;
        Vector3 hacia = actor.Transform.position - pj; hacia.y = 0f;
        if (hacia.magnitude <= distanciaMaxima) return;
        if (!NavMesh.SamplePosition(pj + hacia.normalized * distanciaDeAparicion, out var hit, 6f, NavMesh.AllAreas)) return;

        if (actor.Agent != null && actor.Agent.enabled && actor.Agent.isOnNavMesh) actor.Agent.Warp(hit.position);
        else actor.Transform.position = hit.position;
        actor.Face(pj);
    }

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
