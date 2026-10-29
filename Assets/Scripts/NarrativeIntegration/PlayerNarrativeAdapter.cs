using System;
using System.Collections.Generic;
using Sendero.UI;
using UnityEngine;

/// <summary>
/// El jugador para el grafo: su control (<see cref="PlayerActionManager"/> en modo Cinematic y el
/// HUD con <see cref="PlayerHUDV2"/>) y el inventario del grupo (<see cref="Inventory"/>).
/// </summary>
public sealed class PlayerNarrativeAdapter : INarrativePlayerControl, INarrativeInventory
{
    // ── INarrativePlayerControl ──────────────────────────────────────────────────────────────

    public void LockPlayer(object owner)
    {
        var pam = FindActionManager();
        if (pam != null) pam.PushMode(ActionMode.Cinematic);
        PlayerHUDV2.Instance?.HideHUD(owner);
    }

    public void UnlockPlayer(object owner)
    {
        var pam = FindActionManager();
        if (pam != null) pam.PopMode(ActionMode.Cinematic);
        PlayerHUDV2.Instance?.ShowHUD(owner);
    }

    static PlayerActionManager FindActionManager()
        => PlayerService.TryGetPlayer(out var playerGo, false) ? playerGo.GetComponent<PlayerActionManager>() : null;

    // ── INarrativeInventory ──────────────────────────────────────────────────────────────────

    readonly Dictionary<Action<NarrativeItemAsset, int, int>, (Inventory inventory, Action<ItemData, int, int> relay)> _watches = new();

    static bool TryGetInventory(out Inventory inventory)
        => PlayerService.TryGetComponent(out inventory, includeInactive: true, allowSceneLookup: true) && inventory != null;

    public bool IsAvailable => TryGetInventory(out _);

    public void Add(NarrativeItemAsset item, int amount)
    {
        if (item is ItemData data && TryGetInventory(out var inventory)) inventory.Add(data, amount);
    }

    public bool Has(NarrativeItemAsset item, int amount)
        => item is ItemData data && TryGetInventory(out var inventory) && inventory.HasItem(data, amount);

    public int Count(NarrativeItemAsset item)
        => item != null && TryGetInventory(out var inventory) ? inventory.Count(item.ItemId) : 0;

    public bool TryConsume(NarrativeItemAsset item, int amount)
        => item is ItemData data && TryGetInventory(out var inventory) && inventory.TryConsume(data, amount);

    public bool WatchItemAdded(Action<NarrativeItemAsset, int, int> onAdded)
    {
        if (onAdded == null || !TryGetInventory(out var inventory)) return false;
        UnwatchItemAdded(onAdded);
        Action<ItemData, int, int> relay = (item, added, total) => onAdded(item, added, total);
        inventory.OnItemAdded += relay;
        _watches[onAdded] = (inventory, relay);
        return true;
    }

    public void UnwatchItemAdded(Action<NarrativeItemAsset, int, int> onAdded)
    {
        if (onAdded == null || !_watches.TryGetValue(onAdded, out var watch)) return;
        if (watch.inventory != null) watch.inventory.OnItemAdded -= watch.relay;
        _watches.Remove(onAdded);
    }
}
