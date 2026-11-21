using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// Objeto sencillo de los servicios básicos: un id y un nombre para enseñar.
    [CreateAssetMenu(menuName = "Narrative Graph/Objeto básico", fileName = "Objeto")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class NarrativeBasicItem : NarrativeItemAsset
    {
        public string id = "OBJETO";
        public string displayName = "Objeto";

        public override string ItemId => id;
    }
}
