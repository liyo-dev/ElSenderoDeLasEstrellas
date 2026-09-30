using System;
using UnityEngine;

[Serializable]
[NarrativeNodeInfo("Señales", "Esperar señal", "Espera una señal custom (sticky: si ya se disparó, pasa de largo).")]
[SavePoint("Seguro guardar mientras espera eventos")]
public sealed class WaitCustomEventNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Signal)]
    public string eventKey;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        // Usar GUID del nodo para hacer el flag específico a esta instancia
        var eventReceivedKey = $"__event_{guid}_{eventKey}_received";
        if (ctx.Blackboard.Get<bool>(eventReceivedKey, false))
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
        Debug.Log($"[WaitCustom:{guid}] Suscribiéndose a '{eventKey}'...");
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