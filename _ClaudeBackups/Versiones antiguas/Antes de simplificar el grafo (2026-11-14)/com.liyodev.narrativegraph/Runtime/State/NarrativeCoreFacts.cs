/// Fact categories that belong to the core nodes. Games add their own categories next to these.
public static class NarrativeCoreFacts
{
    /// Additively loaded scene. Key: scene name. Value: Loaded, or null when unloaded.
    public const string Scene = "Scene";
    public const string Loaded = "Loaded";

    /// Story counter. Key: counter name. Value: "+n" to add, "=n" to set.
    public const string Counter = "Counter";

    /// La pieza «Personaje desde aquí» que vale para un personaje. Key: id del actor. Value: guid
    /// de la pieza, o null cuando vuelve a lo de siempre.
    public const string ActorState = "ActorState";

    /// La pieza «lo de siempre» de un personaje (vale cuando no hay otra). Key: id del actor. Value: guid.
    public const string ActorUsual = "ActorUsual";

    public static string CounterAdd(int amount) => "+" + amount;
    public static string CounterSet(int value) => "=" + value;

    /// Applies a counter fact to a current value.
    public static int ApplyCounter(int current, string change)
    {
        if (string.IsNullOrEmpty(change)) return current;
        if (!int.TryParse(change.Substring(1), out var n)) return current;
        return change[0] == '=' ? n : current + n;
    }
}
