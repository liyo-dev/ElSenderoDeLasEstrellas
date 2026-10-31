using UnityEngine;

/// <summary>Busca en la escena a los actores por su id persistente (el que usan las claves de actor del grafo).</summary>
public interface INarrativeActorDirectory
{
    /// <summary>Transform del actor, o null si ahora no está en ninguna escena cargada.</summary>
    Transform FindActor(string actorId);
}

/// <summary>
/// Marca de «tiene algo para ti» sobre un actor (icono de cabeza, minimapa…) mientras el grafo
/// espera que el jugador vaya a hablar con él.
/// </summary>
public interface INarrativeAttentionMarkers
{
    /// <summary>Enseña la marca. <paramref name="markerOverride"/>: prefab propio del nodo; null = el del actor.</summary>
    void ShowMarker(string actorId, GameObject markerOverride);

    void HideMarker(string actorId);
}

/// <summary>Coloca a un actor en una marca con nombre del mundo, al instante.</summary>
public interface INarrativeActorPlacement
{
    /// <summary>
    /// Pone al actor en la marca, mirando hacia donde mira ella. <paramref name="applyLocationSettings"/>:
    /// si el actor es el jugador, aplicar también lo que la marca dicte del lugar (entorno, interior…).
    /// <paramref name="makeVisible"/>: enseñarlo aunque estuviera oculto. false si el actor o la marca no existen.
    /// </summary>
    bool TryPlace(string actorId, string markId, bool applyLocationSettings, bool makeVisible);
}

/// <summary>
/// Que un actor esté o no en el mundo, y cambiarlo de sitio, sin que el jugador lo vea. Lo usan
/// las agendas (<see cref="AgendaDeActores"/>): si ahora se notaría, devuelve false y se vuelve a
/// intentar más tarde.
/// </summary>
public interface INarrativeActorPresence
{
    /// <summary>true si el actor está ahora en una escena cargada.</summary>
    bool IsPresent(string actorId);

    /// <summary>Lo retira o lo devuelve. false = ahora no (el jugador lo vería).</summary>
    bool TrySetPresent(string actorId, bool present);

    /// <summary>Lo pone en una marca (devolviéndolo si estaba retirado). false = ahora no.</summary>
    bool TryPlaceUnseen(string actorId, string markId);
}
