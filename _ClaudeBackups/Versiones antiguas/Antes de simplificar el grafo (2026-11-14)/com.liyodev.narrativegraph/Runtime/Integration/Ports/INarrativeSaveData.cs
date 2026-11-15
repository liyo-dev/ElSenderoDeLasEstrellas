using System.Collections.Generic;

/// <summary>Estado guardado del blackboard de un grafo, por la etiqueta con la que lo registra el hub.</summary>
public readonly struct NarrativeBlackboardRecord
{
    public readonly string GraphLabel;
    public readonly List<SimpleBlackboard.Entry> Entries;

    public NarrativeBlackboardRecord(string graphLabel, List<SimpleBlackboard.Entry> entries)
    {
        GraphLabel = graphLabel;
        Entries = entries;
    }
}

/// <summary>
/// La partida en curso vista por el grafo: si hay una cargada y qué blackboards guardó. Lo usa
/// <see cref="NarrativeGraphStarter"/> para continuar cada grafo donde se quedó.
/// </summary>
public interface INarrativeSaveData
{
    /// <summary>true cuando el juego ya tiene los datos de una partida en memoria.</summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Cambia cada vez que se carga una partida (arranque, continuar, recarga de pruebas). Sirve para
    /// restaurar los blackboards una sola vez por carga y no pisar el estado vivo al cambiar de escena.
    /// </summary>
    int LoadCount { get; }

    /// <summary>Blackboards guardados de la partida cargada; vacío si no guardó ninguno y null si no hay partida.</summary>
    IReadOnlyList<NarrativeBlackboardRecord> GetSavedBlackboards();
}
