using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Diálogos/Dialogue", fileName = "DG_")]
public class DialogueAsset : ScriptableObject
{
    public DialogueLine[] lines;

    [Min(0), Tooltip("Máximo de líneas visuales por página. Cero conserva la presentación completa de cada frase.")]
    public int maxLinesPerPage;

    [Header("Cinematografía")]
    [Tooltip("Activa el modo de conversación grupal: cámara elevada y alejada que encuadra a todos los personajes.")]
    public bool isGroupConversation = false;
}
