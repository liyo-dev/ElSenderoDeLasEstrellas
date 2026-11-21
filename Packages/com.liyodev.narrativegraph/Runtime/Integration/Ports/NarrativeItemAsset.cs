using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Objeto de inventario que el grafo puede dar, pedir o esperar. El juego hace que su propio asset
    /// de objeto herede de aquí.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public abstract class NarrativeItemAsset : ScriptableObject
    {
        /// <summary>Id estable del objeto (el que se guarda en la partida).</summary>
        public abstract string ItemId { get; }
    }
}
