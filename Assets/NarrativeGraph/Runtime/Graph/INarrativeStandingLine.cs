/// <summary>
/// Lo implementa un nodo que deja a un actor con algo que decir mientras dura una situación de la
/// historia (p. ej. una misión que ha encargado y sigue en curso).
///
/// Se consulta cuando el jugador habla con ese actor y ningún nodo del grafo está esperando esa
/// conversación (<see cref="NarrativeStandingLines"/>). Es estado derivado: el nodo no registra
/// nada al ejecutarse, sino que dice si su frase vale ahora (<see cref="IsStandingLineActive"/>),
/// así que funciona igual al cargar partida o al arrancar desde un nodo intermedio.
///
/// Un tipo de nodo nuevo que quiera dejar una frase así implementa esta interfaz y ya está: nadie
/// tiene que conocerlo por su nombre.
/// </summary>
public interface INarrativeStandingLine
{
    /// <summary>Actor que dice la frase (persistenceId). Vacío = el nodo no deja frase.</summary>
    string StandingActorId { get; }

    /// <summary>Qué dice. Null = el nodo no deja frase.</summary>
    DialogueAsset StandingDialogue { get; }

    /// <summary>True si la frase vale en este momento de la partida.</summary>
    bool IsStandingLineActive(INarrativeSignals signals);
}
