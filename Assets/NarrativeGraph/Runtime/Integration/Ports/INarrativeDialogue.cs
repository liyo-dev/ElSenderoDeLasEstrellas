using System;
using UnityEngine;

/// <summary>Caja de diálogo que avanza el jugador.</summary>
public interface INarrativeDialoguePlayer
{
    /// <summary>true si hay un diálogo abierto ahora.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Abre el diálogo. <paramref name="speaker"/>: con quién se habla (cámara y animaciones), o null.
    /// <paramref name="onClosed"/> se llama al cerrarse. false si no se ha podido abrir; entonces
    /// <paramref name="onClosed"/> no se llama.
    /// </summary>
    bool TryPlay(NarrativeDialogueAsset dialogue, Transform speaker, Action onClosed);
}

/// <summary>Pregunta con dos respuestas.</summary>
public interface INarrativeChoicePresenter
{
    /// <summary>Enseña la pregunta y llama a una de las dos acciones según la respuesta. false si no se puede enseñar.</summary>
    bool TryShowChoice(string prompt, string optionA, string optionB, Action onA, Action onB);
}
