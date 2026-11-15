using System;
using System.Collections.Generic;

/// Story counters: whole numbers that steps add to and read ("visits to the shop", "clues found").
/// They are saved with the game through <see cref="INarrativeCounterStore"/>, or, if the game has
/// none, as entries of the flag store ("#name=3").
public static class NarrativeCounters
{
    const string FlagPrefix = "#";

    public static event Action<string> Changed;

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;
#endif

    public static int Get(string counter)
    {
        if (string.IsNullOrWhiteSpace(counter)) return 0;
        var store = NarrativeServices.Get<INarrativeCounterStore>();
        if (store != null) return store.IsAvailable ? store.Get(counter) : 0;
        return TryFindInFlags(counter, out _, out var value) ? value : 0;
    }

    public static void Set(string counter, int value)
    {
        if (string.IsNullOrWhiteSpace(counter)) return;
        int before = Get(counter);
        var store = NarrativeServices.Get<INarrativeCounterStore>();
        if (store != null)
        {
            if (!store.IsAvailable) return;
            store.Set(counter, value);
        }
        else
        {
            if (TryFindInFlags(counter, out var old, out _)) NarrativeFlags.Set(old, false);
            if (value != 0) NarrativeFlags.Set(FlagKey(counter, value), true);
        }
        if (before != value) Changed?.Invoke(counter);
    }

    public static void Add(string counter, int amount) => Set(counter, Get(counter) + amount);

    /// Every counter with a value, for tools.
    public static IEnumerable<KeyValuePair<string, int>> All()
    {
        foreach (var flag in NarrativeFlags.All())
            if (TryParse(flag, out var name, out var value))
                yield return new KeyValuePair<string, int>(name, value);
    }

    static string FlagKey(string counter, int value) => $"{FlagPrefix}{counter}={value}";

    static bool TryFindInFlags(string counter, out string flag, out int value)
    {
        foreach (var f in NarrativeFlags.All())
            if (TryParse(f, out var name, out value) && name == counter) { flag = f; return true; }
        flag = null;
        value = 0;
        return false;
    }

    static bool TryParse(string flag, out string name, out int value)
    {
        name = null;
        value = 0;
        if (flag == null || !flag.StartsWith(FlagPrefix, StringComparison.Ordinal)) return false;
        int eq = flag.LastIndexOf('=');
        if (eq <= FlagPrefix.Length) return false;
        name = flag.Substring(FlagPrefix.Length, eq - FlagPrefix.Length);
        return int.TryParse(flag.Substring(eq + 1), out value);
    }
}
