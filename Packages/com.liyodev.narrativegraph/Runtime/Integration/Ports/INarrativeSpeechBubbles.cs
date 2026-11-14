using System;
using UnityEngine;

/// <summary>Frase corta sobre un personaje.</summary>
public struct NarrativeBubbleLine
{
    /// <summary>Clave de localización del texto (y de su voz, si el juego las asocia).</summary>
    public string TextKey;
    /// <summary>Texto por defecto si la clave no está traducida.</summary>
    public string Text;
    /// <summary>Sobre quién aparece.</summary>
    public Transform Anchor;
    /// <summary>Segundos en pantalla (nunca menos de lo que se tarda en leerla).</summary>
    public float Duration;
    /// <summary>Se queda hasta que otra frase la sustituya.</summary>
    public bool UntilHidden;
    /// <summary>Nombre de quien habla, si la presentación lo enseña.</summary>
    public string SpeakerName;
    /// <summary>Gesto o animación del personaje al decirla (vacío = ninguno).</summary>
    public string Gesture;
    public bool Emphasis;
}

/// <summary>Frase en curso, para cortarla si el grafo abandona el nodo antes de que termine.</summary>
public interface INarrativeLineHandle
{
    /// <summary>Deja de esperarla: calla su voz y deja de atender el botón de saltar.</summary>
    void Cancel();
}

/// <summary>Bocadillos sobre los personajes.</summary>
public interface INarrativeSpeechBubbles
{
    /// <summary>false si ahora no se pueden enseñar (falta la interfaz en pantalla).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Dice la frase. Con <paramref name="onHidden"/>, quien llama la está esperando: el botón de
    /// saltar del juego puede cortarla y <paramref name="onHidden"/> se llama una vez al esconderse
    /// (sola o saltada). Devuelve null si no había nada que decir; entonces <paramref name="onHidden"/>
    /// no se llama.
    /// </summary>
    INarrativeLineHandle Say(NarrativeBubbleLine line, Action onHidden);
}
