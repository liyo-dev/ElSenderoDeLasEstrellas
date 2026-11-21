using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Nodo que emite un evento custom al sistema de señales narrativas.
    /// Útil para activar condiciones custom en NPCs u otros sistemas que escuchen eventos.
    /// Ejemplo: EVT_MOUNTAIN, EVT_BOSS_DEFEATED, etc.
    /// </summary>
    [Serializable]
    [NarrativeNodeInfo("Señales", "Emitir señal", "Emite una señal custom que escuchan nodos, sequencers y triggers.")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class RaiseCustomEventNode : NarrativeNode, INarrativeStateEffect
    {
        [Tooltip("Clave del evento a emitir (ej: EVT_MOUNTAIN, EVT_BOSS_DEFEATED)")]
        [NarrativeKey(NarrativeKeyKind.Signal)]
        [SerializeField] private string eventKey;
        public string EventKey { get => eventKey; set => eventKey = value; }

        public override void Describe(INarrativeNodeDescriber d) => d.Signal(eventKey, SignalRole.Emite);

        public void Project(INarrativeStateWriter state) => state.MarkSignalRaised(eventKey);

        public override void CollectWarnings(List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(eventKey)) warnings.Add("eventKey vacío");
        }

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            if (string.IsNullOrWhiteSpace(eventKey))
            {
                Debug.LogWarning("[RaiseCustomEventNode] eventKey está vacío. No se emitió ningún evento.");
            }
            else
            {
                NarrativeLog.Info($"[RaiseCustomEventNode] Emitiendo evento: '{eventKey}'");
                ctx.Signals?.RaiseCustom(eventKey, $"[Nodo] {eventKey}");
            }

            onReadyToAdvance?.Invoke();
        }
    }
}
