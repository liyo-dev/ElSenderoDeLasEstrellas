using System;
using System.Collections.Generic;
using UnityEngine;

/// Pide una cinemática al sistema de cinemáticas y espera a que termine. El grafo solo emite la
/// señal de entrada y espera la de fin: la escena la monta y la dirige el SequencePlayer que la
/// escucha (coge a su reparto esté donde esté, lo coloca y al acabar devuelve el control).
[Serializable]
[NarrativeNodeInfo("Cinemáticas", "Reproducir cinemática", "Emite la señal de entrada de una cinemática y espera su señal de fin (Hecho) o de fallo.")]
public sealed class PedirCinematica : NarrativeRequest, INarrativeStateEffect
{
    static readonly string[] Salidas = { "Hecho", "Fallo" };

    [Tooltip("Nombre legible (solo para el editor): 'Despertar de la estrella', 'Taberna'...")]
    public string cinematicName;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Señal que escucha el SequencePlayer para empezar. Ej: AWAKEN_START")]
    public string signalIn;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Señal que emite el SequencePlayer al terminar bien. Ej: AWAKEN_DONE")]
    public string signalDone;

    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Opcional: señal que emite si la secuencia falla o se aborta (salida 'Fallo'). Ej: AWAKEN_FAILED")]
    public string signalFailed;

    [Header("Secuencia de datos (opcional)")]
    [Tooltip("Si se asigna (con signalIn/signalDone iguales a los suyos) y ninguna escena tiene un SequencePlayer para esta secuencia, se monta uno en vivo con los " +
             "ajustes (cámara, audio, transiciones) del SequencePlayer 'plantillaDeAjustes'. Así una " +
             "secuencia nueva no obliga a tocar la escena.")]
    public SequenceDefinition secuencia;

    [Tooltip("Nombre del GameObject del SequencePlayer ya montado del que copiar los ajustes.")]
    public string plantillaDeAjustes = "";

    public override string[] Exits => Salidas;

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Line(string.IsNullOrEmpty(cinematicName) ? "(sin nombre)" : cinematicName, NarrativeTextTone.Strong);
        d.Signal(signalIn, SignalRole.Emite, "entra");
        d.Signal(signalDone, SignalRole.Escucha, "hecho");
        if (!string.IsNullOrEmpty(signalFailed)) d.Signal(signalFailed, SignalRole.Escucha, "fallo");
        if (secuencia != null) d.Asset(secuencia, "secuencia");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrEmpty(signalIn) || string.IsNullOrEmpty(signalDone)) warnings.Add("Faltan señales de entrada/fin");
        if (secuencia != null && (signalIn != secuencia.signalIn || signalDone != secuencia.signalOut))
            warnings.Add($"Las señales no coinciden con las de '{secuencia.name}' ({secuencia.signalIn}/{secuencia.signalOut})");
    }

    /// Lo que deja cambiado la secuencia, con sus marcas ligadas a ella (o a la plantilla, si se
    /// monta en vivo y usa su escenario).
    public void Project(INarrativeStateWriter state)
    {
        state.MarkSignalRaised(signalIn);
        state.MarkSignalRaised(signalDone);
        if (secuencia != null) secuencia.Project(new ScopedNarrativeStateWriter(state, secuencia.name, plantillaDeAjustes));
    }
}

/// Lanza la cinemática pedida: emite su señal de entrada y espera la de fin o la de fallo.
public sealed class CinematicaEnCurso : NarrativeRequestRun<PedirCinematica>
{
    INarrativeSignals _signals;
    Action _onDone;
    Action _onFailed;

    protected override void Start()
    {
        if (R.secuencia != null) MontarSiFalta();

        _signals = Ctx?.Signals;
        if (string.IsNullOrWhiteSpace(R.signalIn) || string.IsNullOrWhiteSpace(R.signalDone) || _signals == null)
        {
            Debug.LogWarning($"[Cinemática:{StepGuid}] '{R.cinematicName}': faltan señales o proveedor de señales → sigue por 'Hecho'.");
            Finish(0);
            return;
        }

        // Las señales de fin son "en vivo": una DONE/FAILED de una reproducción anterior no vale.
        _signals.ClearPendingCustom(R.signalDone);
        if (!string.IsNullOrWhiteSpace(R.signalFailed)) _signals.ClearPendingCustom(R.signalFailed);

        _onDone = () => Finish(0);
        _signals.OnCustom(R.signalDone, _onDone);
        if (!string.IsNullOrWhiteSpace(R.signalFailed))
        {
            _onFailed = () => Finish(1);
            _signals.OnCustom(R.signalFailed, _onFailed);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[Cinemática:{StepGuid}] '{R.cinematicName}': emite {R.signalIn}, espera {R.signalDone}");
#endif
        _signals.RaiseCustom(R.signalIn, $"[Cinemática] {R.cinematicName}");
    }

    protected override void Stop()
    {
        if (_signals != null)
        {
            if (_onDone != null) _signals.OffCustom(R.signalDone, _onDone);
            if (_onFailed != null) _signals.OffCustom(R.signalFailed, _onFailed);
        }
        _signals = null;
        _onDone = null;
        _onFailed = null;
    }

    /// Si ninguna escena escucha esta secuencia, monta un SequencePlayer en vivo con los ajustes
    /// de la plantilla. La copia se destruye sola un rato después de terminar (el SequencePlayer
    /// retiene a los NPCs unos segundos más tras la secuencia; ver _idleGraceAfterSequence).
    void MontarSiFalta()
    {
        SequencePlayer molde = null;
        foreach (var sp in UnityEngine.Object.FindObjectsByType<SequencePlayer>())
        {
            if (sp.Definition == R.secuencia) return;
            if (sp.name == R.plantillaDeAjustes) molde = sp;
        }
        if (molde == null)
        {
            Debug.LogError($"[Cinemática:{StepGuid}] '{R.secuencia.name}' no está montada en la escena y no " +
                           $"encuentro la plantilla '{R.plantillaDeAjustes}' para montarla en vivo.");
            return;
        }
        var copia = SequencePlayer.CrearCopiaPara(R.secuencia, molde);
        if (copia != null) copia.RegisterCleanup(() => UnityEngine.Object.Destroy(copia.gameObject, 6f));
    }
}
