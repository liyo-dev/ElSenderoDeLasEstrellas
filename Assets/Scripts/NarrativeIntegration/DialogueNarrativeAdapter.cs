using System;
using UnityEngine;

/// <summary>Diálogos y preguntas del grafo, en la caja de <see cref="DialogueManager"/>.</summary>
public sealed class DialogueNarrativeAdapter : INarrativeDialoguePlayer, INarrativeChoicePresenter
{
    public bool IsOpen => DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;

    public bool TryPlay(NarrativeDialogueAsset dialogue, Transform speaker, Action onClosed)
    {
        var dm = DialogueManager.Instance;
        if (dm == null || dialogue is not DialogueAsset asset) return false;

        if (speaker != null) dm.StartDialogue(asset, speaker, onClosed);
        else dm.StartDialogue(asset, onClosed);
        return true;
    }

    public bool TryShowChoice(string prompt, string optionA, string optionB, Action onA, Action onB)
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return false;
        dm.ShowWithChoices(prompt, optionA, optionB, onA, onB);
        return true;
    }
}
