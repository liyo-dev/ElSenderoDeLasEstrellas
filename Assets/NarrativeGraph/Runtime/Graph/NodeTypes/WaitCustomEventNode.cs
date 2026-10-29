using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[NarrativeNodeInfo("Señales", "Esperar señal", "Espera una señal custom (sticky: si ya se disparó, pasa de largo).")]
[SavePoint("Seguro guardar mientras espera eventos")]
public sealed class WaitCustomEventNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Signal)]
    public string eventKey;

    [Tooltip("Si está marcado (por defecto), una señal ocurrida ANTES de llegar a este nodo también cuenta (comportamiento sticky). Si se desmarca, se descarta cualquier emisión previa y solo avanza si la señal ocurre mientras este nodo está esperando.")]
    public bool acceptEarlierEvent = true;

    public override void Describe(INarrativeNodeDescriber d) => d.Signal(eventKey, SignalRole.Escucha);

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) warnings.Add("eventKey vacío");
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (!acceptEarlierEvent)
        {
            ctx?.Signals?.ClearPendingCustom(eventKey);
        }

        // Usar GUID del nodo para hacer el flag específico a esta instancia
        var eventReceivedKey = $"__event_{guid}_{eventKey}_received";
        if (acceptEarlierEvent && ctx.Blackboard.Get<bool>(eventReceivedKey, false))
        {
            // El flag se consume al leerlo: si no se limpia, un nodo dentro de un bucle de reintento
            // avanzaría sin esperar el evento real en la segunda vuelta. Ver INC-448.
            ctx.Blackboard.Set(eventReceivedKey, false);
    #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[WaitCustom:{guid}] Evento '{eventKey}' ya fue recibido previamente → avanzando inmediatamente (flag consumido)");
#endif
            ready?.Invoke();
            return;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[WaitCustom:{guid}] Suscribiéndose a '{eventKey}' (acceptEarlier={acceptEarlierEvent})...");
#endif
        void Handler()
        {
            ctx.Signals.OffCustom(eventKey, Handler);
            // Marcar el evento como recibido en el blackboard (específico a este nodo)
            ctx.Blackboard.Set(eventReceivedKey, true);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[WaitCustom:{guid}] ✅ Recibido '{eventKey}' → avanzando");
#endif
            ready?.Invoke();
        }
        ctx.Signals.OnCustom(eventKey, Handler);
    }

}