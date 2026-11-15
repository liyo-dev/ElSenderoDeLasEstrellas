using System;

/// <summary>Inventario del jugador.</summary>
public interface INarrativeInventory
{
    /// <summary>false si ahora no hay inventario del jugador (p. ej. sin jugador en escena).</summary>
    bool IsAvailable { get; }

    void Add(NarrativeItemAsset item, int amount);

    bool Has(NarrativeItemAsset item, int amount);

    int Count(NarrativeItemAsset item);

    bool TryConsume(NarrativeItemAsset item, int amount);

    /// <summary>
    /// Avisa cada vez que entra un objeto: (objeto, cantidad añadida, total tras añadir). false si
    /// ahora no hay inventario que vigilar.
    /// </summary>
    bool WatchItemAdded(Action<NarrativeItemAsset, int, int> onAdded);

    void UnwatchItemAdded(Action<NarrativeItemAsset, int, int> onAdded);
}
