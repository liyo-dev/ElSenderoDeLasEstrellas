using System;

/// <summary>
/// Fork explícito: todas las aristas que salen de este nodo corren EN PARALELO.
///
/// Mecánicamente es lo mismo que sacar varias aristas de cualquier nodo legacy (el runner ya
/// lo trataba como fork), pero al tener un nodo dedicado se ve en el grafo que la
/// paralelización es intencionada y no un despiste. El editor marca con un aviso cualquier
/// otro nodo de puerto único con más de una arista.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Flujo", "Fork (en paralelo)", "Todas las salidas arrancan a la vez y siguen en paralelo.")]
[SavePoint("Instantáneo")]
public sealed class ForkNode : NarrativeNode
{
    public override void Enter(NarrativeContext ctx, Action ready) => ready?.Invoke();
}
