using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro global de SpawnAnchor con lookup O(1) por id.
/// Se mantiene automáticamente desde SpawnAnchor.OnEnable/OnDisable.
/// </summary>
public static class AnchorRegistry
{
    private static readonly Dictionary<string, SpawnAnchor> _byId = new();

    public static void Register(SpawnAnchor anchor)
    {
        if (!anchor || string.IsNullOrEmpty(anchor.anchorId)) return;
        if (_byId.TryGetValue(anchor.anchorId, out var existing) && existing && existing != anchor)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[AnchorRegistry] Duplicado de anchorId '{anchor.anchorId}'. Reemplazando referencia.");
#endif
        }
        _byId[anchor.anchorId] = anchor;
    }

    public static void Unregister(SpawnAnchor anchor)
    {
        if (!anchor || string.IsNullOrEmpty(anchor.anchorId)) return;
        if (_byId.TryGetValue(anchor.anchorId, out var existing) && existing == anchor)
        {
            _byId.Remove(anchor.anchorId);
        }
    }

    public static bool TryGet(string id, out SpawnAnchor anchor)
        => _byId.TryGetValue(id, out anchor);

    public static SpawnAnchor Get(string id)
    {
        _byId.TryGetValue(id, out var a);
        return a;
    }

    public static IReadOnlyDictionary<string, SpawnAnchor> All => _byId;
}

