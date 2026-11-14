using System;
using System.Collections.Generic;
using UnityEngine;

/// Diálogo sencillo de los servicios básicos: una lista de frases con quién las dice. Si tu juego
/// tiene su propio sistema de diálogo, usa sus assets y su conexión en lugar de este.
[CreateAssetMenu(menuName = "Narrative Graph/Diálogo básico", fileName = "Dialogo")]
public sealed class NarrativeBasicDialogue : NarrativeDialogueAsset
{
    [Serializable]
    public struct Line
    {
        public string speaker;
        [TextArea(2, 5)] public string text;
    }

    public List<Line> lines = new();

    public override int LineCount => lines?.Count ?? 0;

    public override void DescribeLines(INarrativeNodeDescriber d)
    {
        if (lines == null) return;
        foreach (var l in lines)
            d.Line(string.IsNullOrEmpty(l.speaker) ? $"«{l.text}»" : $"{l.speaker}: «{l.text}»");
    }
}
