// StartNode.cs
using System;

[Serializable]
[NarrativeNodeInfo("Flujo", "Inicio", "Punto de entrada del grafo.")]
public sealed class StartNode : NarrativeNode
{
    public override bool IsGraphEntry => true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        onReadyToAdvance?.Invoke(); // pasa inmediatamente al siguiente
    }
}