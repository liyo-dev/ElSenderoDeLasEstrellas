// StartNode.cs
using System;

namespace Liyodev.NarrativeGraph
{
    [Serializable]
    [NarrativeNodeInfo("Flujo", "Inicio", "Punto de entrada del grafo.")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class StartNode : NarrativeNode
    {
        public override bool IsGraphEntry => true;

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            onReadyToAdvance?.Invoke(); // pasa inmediatamente al siguiente
        }
    }
}
