using UnityEngine;

/// Objeto sencillo de los servicios básicos: un id y un nombre para enseñar.
[CreateAssetMenu(menuName = "Narrative Graph/Objeto básico", fileName = "Objeto")]
public sealed class NarrativeBasicItem : NarrativeItemAsset
{
    public string id = "OBJETO";
    public string displayName = "Objeto";

    public override string ItemId => id;
}
