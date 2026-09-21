using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro global de <see cref="NpcSpawnPoint"/> con lookup O(1) por id.
/// Se mantiene automáticamente desde NpcSpawnPoint.OnEnable/OnDisable.
///
/// Copia deliberada de <see cref="AnchorRegistry"/> — mismo patrón, mismo comportamiento ante
/// duplicados — para que no haya dos formas distintas de hacer lo mismo en el proyecto.
/// </summary>
public static class NpcSpawnRegistry
{
    private static readonly Dictionary<string, NpcSpawnPoint> _byId = new();

    public static void Register(NpcSpawnPoint point)
    {
        if (!point || string.IsNullOrEmpty(point.spawnId)) return;
        if (_byId.TryGetValue(point.spawnId, out var existing) && existing && existing != point)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[NpcSpawnRegistry] Duplicado de spawnId '{point.spawnId}'. Reemplazando referencia.");
#endif
        }
        _byId[point.spawnId] = point;
    }

    public static void Unregister(NpcSpawnPoint point)
    {
        if (!point || string.IsNullOrEmpty(point.spawnId)) return;
        if (_byId.TryGetValue(point.spawnId, out var existing) && existing == point)
            _byId.Remove(point.spawnId);
    }

    public static bool TryGet(string id, out NpcSpawnPoint point)
        => _byId.TryGetValue(id, out point);

    public static NpcSpawnPoint Get(string id)
    {
        _byId.TryGetValue(id, out var p);
        return p;
    }

    public static IReadOnlyDictionary<string, NpcSpawnPoint> All => _byId;
}
