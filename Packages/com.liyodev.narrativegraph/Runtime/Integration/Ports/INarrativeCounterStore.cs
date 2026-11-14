/// Where the game keeps story counters ("times the player talked to the innkeeper"). Optional:
/// without it, <see cref="NarrativeCounters"/> keeps them inside the flag store.
public interface INarrativeCounterStore
{
    bool IsAvailable { get; }
    int Get(string counter);
    void Set(string counter, int value);
}
