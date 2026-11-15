using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Narrative Graph/Grafo", fileName = "NarrativeGraph")]
public class NarrativeGraph : ScriptableObject
{
    public string startNodeGuid;
    [SerializeReference] public List<NarrativeNode> nodes = new();
    [Tooltip("Escenas en las que aplica este grafo. Vacío = todas.")]
    public List<string> applicableSceneNames = new();

    private void OnValidate()
    {
        // Quita nodos nulos (tipos de nodo borrados o renombrados que ya no se pueden deserializar).
        nodes?.RemoveAll(n => n == null);
    }

    public NarrativeNode FindNode(string guid)
        => nodes.Find(n => n != null && n.guid == guid);

    public NarrativeNode FindNodeByTitle(string title)
        => nodes.Find(n => n != null && n.displayTitle == title);
}