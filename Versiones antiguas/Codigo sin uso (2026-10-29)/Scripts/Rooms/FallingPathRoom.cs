using UnityEngine;

public class FallingPathRoom : MonoBehaviour
{
    public TorchInteract endTorch;  // antorcha del final
    public RoomGoal roomGoal;
    public DoorGate exitDoor;

    void Awake()
    {
        if (endTorch) endTorch.onTorchToggled += OnTorch;
    }

    void OnDestroy()
    {
        if (endTorch) endTorch.onTorchToggled -= OnTorch;
    }

    void OnTorch(bool lit)
    {
        if (lit)
        {
            roomGoal?.MarkCleared();
            if (exitDoor) exitDoor.Open();
            enabled = false;
        }
    }
}