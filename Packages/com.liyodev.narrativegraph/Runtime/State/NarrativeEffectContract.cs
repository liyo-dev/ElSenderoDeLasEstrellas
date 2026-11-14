using System;
using System.Collections.Generic;

/// The rule that keeps the state projection honest: every concrete node type declares what it
/// leaves changed, or declares that it changes nothing.
public static class NarrativeEffectContract
{
    public static bool IsDeclared(Type nodeType)
    {
        if (nodeType == null) return false;
        return typeof(INarrativeStateEffect).IsAssignableFrom(nodeType)
               || typeof(INarrativeRouteEffect).IsAssignableFrom(nodeType)
               || Attribute.IsDefined(nodeType, typeof(NarrativeNoWorldEffectAttribute), false);
    }

    /// Node types that must declare their effect: concrete, top-level and not obsolete.
    public static bool AppliesTo(Type nodeType)
    {
        return nodeType != null
               && typeof(NarrativeNode).IsAssignableFrom(nodeType)
               && !nodeType.IsAbstract
               && !nodeType.IsNested
               && !Attribute.IsDefined(nodeType, typeof(ObsoleteAttribute), false);
    }

    public static List<Type> FindUndeclared(IEnumerable<Type> nodeTypes)
    {
        var result = new List<Type>();
        if (nodeTypes == null) return result;
        foreach (var type in nodeTypes)
            if (AppliesTo(type) && !IsDeclared(type)) result.Add(type);
        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result;
    }
}
