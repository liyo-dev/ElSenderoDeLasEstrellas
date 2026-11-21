using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    [Serializable]
    [NarrativeNodeInfo("Notas", "Nota", "Comentario libre en el grafo. No se ejecuta.")]
    [NarrativeNoWorldEffect]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public class GraphNoteNode : NarrativeNode
    {
        [TextArea(3, 12)] [SerializeField] private string note;
        public string Note { get => note; set => note = value; }
#pragma warning disable 0414 // solo los usa el editor por SerializedProperty o se conservan por los grafos guardados
        [SerializeField] private Color accent = new Color(1f, 0.93f, 0.55f);
#pragma warning restore 0414

        public override bool IsAnnotation => true;

        public override void Describe(INarrativeNodeDescriber d) => d.Line(note ?? "", NarrativeTextTone.Note);

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            // Las notas no bloquean la ejecución narrativa.
            onReadyToAdvance?.Invoke();
        }
    }
}
