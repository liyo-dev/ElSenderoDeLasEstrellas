using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug helper: press K to add the configured ItemData to the player's Inventory
/// and (optionally) force spawn a popup via any CollectiblePopupQueue in scene.
/// </summary>
public class DebugInventoryAdder : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;
    // By default don't force spawn because Inventory.Add already triggers OnItemAdded.
    public bool alsoForcePopup = false;

    void Update()
    {
        // FIX (5 sept 2026, AGENTS.md y TRACKER INC-F6): UnityEngine.Input.* lanza
        // InvalidOperationException con el nuevo Input System activo en exclusiva
        // (ProjectSettings.activeInputHandler: 1) - mismo patron ya corregido en
        // CinematicSequencerBase.cs.
        if (Keyboard.current != null && Keyboard.current[Key.K].wasPressedThisFrame)
        {
            if (item == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[DebugInventoryAdder] No ItemData assigned.");
#endif
                return;
            }

            if (PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true))
            {
                inv.Add(item, amount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[DebugInventoryAdder] Added {amount} {item.displayName} to Inventory '{inv.gameObject.name}'");
#endif
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[DebugInventoryAdder] Inventory not found via PlayerService.");
#endif
            }

            if (alsoForcePopup)
            {
#if UNITY_2023_1_OR_NEWER
                var queue = ServiceLocator.Get<CollectiblePopupQueue>(false);
#else
                var queue = ServiceLocator.Get<CollectiblePopupQueue>(false);
#endif
                if (queue != null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("[DebugInventoryAdder] Forcing TestSpawn on CollectiblePopupQueue");
#endif
                    queue.TestSpawn(item, amount);
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[DebugInventoryAdder] No CollectiblePopupQueue found in scene.");
#endif
                }
            }
        }
    }
}
