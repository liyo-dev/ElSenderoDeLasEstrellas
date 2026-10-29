using System.Collections.Generic;
using UnityEngine;

public class EnemyGateWatcher : MonoBehaviour
{
    public List<EnemyMarker> enemies = new();
    public RoomGoal roomGoal;
    public DoorGate exitDoor;

    void Awake()
    {
        foreach (var e in enemies)
            if (e) e.onEnemyGone += OnGone;
        Check();
    }

    void OnDestroy()
    {
        foreach (var e in enemies)
            if (e) e.onEnemyGone -= OnGone;
    }

    void OnGone(EnemyMarker _)
    {
        Check();
    }

    void Check()
    {
        enemies.RemoveAll(e => e == null);
        if (enemies.Count == 0)
        {
            roomGoal?.MarkCleared();
            if (exitDoor) exitDoor.Open();
            enabled = false;
        }
    }
}