using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Reproduce una secuencia (SequenceDefinition) sin que tenga que estar montada en la escena (INC-445).
///
/// Hasta ahora cada secuencia nueva necesitaba un GameObject con SequencePlayer + SequenceStage en
/// su escena, y MainWorld está guardada en binario: había que montarlo a mano en el Editor. Este
/// nodo busca si ya hay un SequencePlayer para esa definición; si no, crea uno en vivo copiando los
/// ajustes de otro que sí esté montado (cámara, audio, transiciones) — ver
/// SequencePlayer.CrearCopiaPara. Luego emite su señal de entrada y espera la de salida, como
/// PlayCinematicNode.
///
/// Opcional: si un actor tiene que llegar andando hasta el jugador y está lejísimos (Eldran en la
/// plaza y Will en el mercado), se le acerca ANTES de empezar, fuera de cámara, para que el paseo
/// dure unos segundos y no medio minuto.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Cinemática", "Secuencia (sin montar en escena)", "Reproduce un SequenceDefinition; si la escena no lo tiene montado, lo monta en vivo.")]
public sealed class ReproducirSecuenciaNode : NarrativeNode
{
    public SequenceDefinition secuencia;

    [Tooltip("Nombre del GameObject de un SequencePlayer ya montado en la escena del que copiar los " +
             "ajustes (cámara, audio, transiciones). Vacío = el primero que encuentre.")]
    public string plantilla = "SEQ_PerasEldran";

    [Header("Acercar a un actor antes de empezar (opcional)")]
    [NarrativeKey(NarrativeKeyKind.Actor)]
    public string actorAAcercar;
    [Tooltip("Si está más lejos del jugador que esto…")]
    public float distanciaMaxima = 24f;
    [Tooltip("…se le pone a esta distancia, en su misma dirección, sobre el NavMesh.")]
    public float distanciaDeAparicion = 12f;

    [NonSerialized] private INarrativeSignals _signals;
    [NonSerialized] private Action _onDone;
    [NonSerialized] private string _signalDone;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (secuencia == null || string.IsNullOrWhiteSpace(secuencia.signalIn) || string.IsNullOrWhiteSpace(secuencia.signalOut))
        {
            Debug.LogWarning($"[ReproducirSecuenciaNode:{guid}] Sin secuencia o sin señales → sigo.");
            ready?.Invoke();
            return;
        }
        if (ctx?.Signals == null) { ready?.Invoke(); return; }

        // ¿Ya hay alguien escuchando esta secuencia?
        SequencePlayer jugadorDeSecuencia = null, molde = null;
        foreach (var sp in UnityEngine.Object.FindObjectsByType<SequencePlayer>())
        {
            if (sp.Definition == secuencia) jugadorDeSecuencia = sp;
            if (molde == null && (string.IsNullOrEmpty(plantilla) || sp.name == plantilla)) molde = sp;
        }
        if (jugadorDeSecuencia == null)
        {
            if (molde == null)
                foreach (var sp in UnityEngine.Object.FindObjectsByType<SequencePlayer>()) { molde = sp; break; }
            if (molde == null)
            {
                Debug.LogError($"[ReproducirSecuenciaNode:{guid}] No hay ningún SequencePlayer en escena del que copiar ajustes: no puedo reproducir '{secuencia.name}'.");
                ready?.Invoke();
                return;
            }
            jugadorDeSecuencia = SequencePlayer.CrearCopiaPara(secuencia, molde);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ReproducirSecuenciaNode] '{secuencia.name}' montada en vivo con los ajustes de '{molde.name}'.");
#endif
        }

        AcercarActor();

        _signals = ctx.Signals;
        _signalDone = secuencia.signalOut;
        _signals.ClearPendingCustom(_signalDone);
        _onDone = () =>
        {
            Soltar();
            ready?.Invoke();
        };
        _signals.OnCustom(_signalDone, _onDone);
        _signals.RaiseCustom(secuencia.signalIn, $"[ReproducirSecuenciaNode] {secuencia.name}");
    }

    public override void Exit(NarrativeContext ctx) => Soltar();

    private void Soltar()
    {
        if (_signals != null && _onDone != null)
        {
            try { _signals.OffCustom(_signalDone, _onDone); } catch { }
        }
        _signals = null;
        _onDone = null;
    }

    private void AcercarActor()
    {
        if (string.IsNullOrEmpty(actorAAcercar)) return;
        if (!SequenceActor.TryResolve(actorAAcercar, out var actor) || actor.Transform == null) return;
        if (!SequenceActor.TryResolve(SequenceActor.PlayerId, out var jugador) || jugador.Transform == null) return;

        Vector3 pj = jugador.Transform.position;
        Vector3 hacia = actor.Transform.position - pj; hacia.y = 0f;
        if (hacia.magnitude <= distanciaMaxima) return;

        Vector3 punto = pj + hacia.normalized * distanciaDeAparicion;
        if (!NavMesh.SamplePosition(punto, out var hit, 6f, NavMesh.AllAreas)) return;

        if (actor.Agent != null && actor.Agent.enabled && actor.Agent.isOnNavMesh) actor.Agent.Warp(hit.position);
        else actor.Transform.position = hit.position;
        actor.Face(pj);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ReproducirSecuenciaNode] '{actorAAcercar}' estaba a {hacia.magnitude:0} m: acercado a {distanciaDeAparicion:0} m antes de empezar.");
#endif
    }
}
