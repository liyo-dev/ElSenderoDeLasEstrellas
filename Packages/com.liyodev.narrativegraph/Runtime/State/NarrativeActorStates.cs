using System;
using System.Collections.Generic;

/// En qué está cada personaje según la historia: la pieza «Personaje desde aquí» que vale ahora
/// (<see cref="ActorStateNode"/>). Dos capas por personaje: «lo de siempre» y «desde aquí», que
/// manda mientras exista. Se guarda con la partida como entradas del almacén de datos de la
/// historia («@actor:Eldran=guid», «@usual:Eldran=guid»), igual que los contadores.
public static class NarrativeActorStates
{
    const string CurrentTag = "@actor:";
    const string UsualTag = "@usual:";

    public static event Action<string> Changed;

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;
#endif

    /// La pieza que vale ahora para el personaje (guid), o null: sigue con lo suyo.
    public static string Active(string actorId) => Get(CurrentTag, actorId) ?? Get(UsualTag, actorId);

    /// Pone en su capa lo que dice la pieza.
    public static void Apply(ActorStateNode node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.actorId)) return;
        var (category, value) = node.Fact;
        SetFromFact(category, node.actorId, value);
    }

    /// Aplica un hecho de las categorías <see cref="NarrativeCoreFacts.ActorState"/> o
    /// <see cref="NarrativeCoreFacts.ActorUsual"/> (lo usan los escritores de estado).
    public static void SetFromFact(string category, string actorId, string guid)
    {
        var tag = TagOf(category);
        if (tag == null || string.IsNullOrWhiteSpace(actorId)) return;
        var before = Active(actorId);
        if (TryFind(tag, actorId, out var old, out _)) NarrativeFlags.Set(old, false);
        if (!string.IsNullOrEmpty(guid)) NarrativeFlags.Set(tag + actorId + "=" + guid, true);
        if (before != Active(actorId)) Changed?.Invoke(actorId);
    }

    /// Lo mismo sobre una lista de datos guardados (los de una partida que aún no se juega).
    /// <paramref name="key"/> convierte el nombre al formato con que la lista los guarda.
    public static void SetFromFact(List<string> flags, Func<string, string> key, string category, string actorId, string guid)
    {
        var tag = TagOf(category);
        if (flags == null || tag == null || string.IsNullOrWhiteSpace(actorId)) return;
        var prefix = key(tag + actorId + "=");
        flags.RemoveAll(f => f != null && f.StartsWith(prefix, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(guid)) flags.Add(prefix + guid);
    }

    /// Los personajes con alguna pieza que valga.
    public static IEnumerable<string> Actors()
    {
        var seen = new HashSet<string>();
        foreach (var flag in NarrativeFlags.All())
            if (TryParse(flag, out _, out var actor, out _) && seen.Add(actor)) yield return actor;
    }

    static string TagOf(string category)
        => category == NarrativeCoreFacts.ActorState ? CurrentTag : category == NarrativeCoreFacts.ActorUsual ? UsualTag : null;

    static string Get(string tag, string actorId)
        => !string.IsNullOrWhiteSpace(actorId) && TryFind(tag, actorId, out _, out var guid) ? guid : null;

    static bool TryFind(string tag, string actorId, out string flag, out string guid)
    {
        foreach (var f in NarrativeFlags.All())
            if (TryParse(f, out var t, out var a, out guid) && t == tag && a == actorId) { flag = f; return true; }
        flag = null;
        guid = null;
        return false;
    }

    static bool TryParse(string flag, out string tag, out string actor, out string guid)
    {
        tag = actor = guid = null;
        if (flag == null) return false;
        if (flag.StartsWith(CurrentTag, StringComparison.Ordinal)) tag = CurrentTag;
        else if (flag.StartsWith(UsualTag, StringComparison.Ordinal)) tag = UsualTag;
        else return false;
        int eq = flag.IndexOf('=', tag.Length);
        if (eq <= tag.Length) return false;
        actor = flag.Substring(tag.Length, eq - tag.Length);
        guid = flag.Substring(eq + 1);
        return true;
    }
}
