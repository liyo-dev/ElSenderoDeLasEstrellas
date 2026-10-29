using UnityEngine;

/// Diálogo del juego. Hereda de NarrativeDialogueAsset para que el grafo narrativo lo guarde y lo
/// reproduzca sin conocer el formato de las líneas.
[CreateAssetMenu(menuName = "El Sendero/Diálogos/Dialogue", fileName = "DG_")]
public class DialogueAsset : NarrativeDialogueAsset
{
    public DialogueLine[] lines;

    [Min(0), Tooltip("Máximo de líneas visuales por página. Cero conserva la presentación completa de cada frase.")]
    public int maxLinesPerPage;

    [Header("Cinematografía")]
    [Tooltip("Activa el modo de conversación grupal: cámara elevada y alejada que encuadra a todos los personajes.")]
    public bool isGroupConversation = false;

    public override int LineCount => lines != null ? lines.Length : 0;

    public override string Summary => base.Summary + (isGroupConversation ? " · grupal" : "");

    /// Cada línea con quién la dice, para la tarjeta del editor del grafo.
    public override void DescribeLines(INarrativeNodeDescriber d)
    {
        int count = LineCount;
        for (int i = 0; i < count; i++)
        {
            var line = lines[i];
            d.DialogueLine(line.speakerNameId, line.isPlayerSpeaking, line.textId, line.text,
                line.emotion != default ? "emoción: " + line.emotion : null);
        }
    }
}
