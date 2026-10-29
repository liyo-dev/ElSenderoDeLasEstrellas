using System;
using UnityEngine;

/// <summary>
/// Añade un objeto al inventario del jugador (INarrativeInventory). Es idempotente: el mismo nodo
/// no vuelve a dar el objeto si se repasa por él.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Objetos", "Dar objeto", "Añade un objeto al inventario del jugador.")]
public sealed class GiveInventoryItemNode : NarrativeNode, INarrativeStateEffect
{
    [Header("Item")]
    public NarrativeItemAsset item;
    [Min(1)] public int amount = 1;
    [Tooltip("Cuando está activo, loguea advertencias si no se encuentra inventario o item.")]
    public bool logWarnings = true;
    [Tooltip("Sin efecto: el nodo ya es idempotente (lo recuerda en el blackboard). Se conserva por los grafos guardados.")]
    public bool onlyOnce = true;

    public override void Describe(INarrativeNodeDescriber d) => d.Item(item, amount, "da");

    public void Project(INarrativeStateWriter state)
    {
        if (item != null && amount > 0) state.AddItem(item.ItemId, amount);
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (item == null || amount <= 0)
        {
            if (logWarnings)
                Debug.LogWarning("[GiveInventoryItemNode] Item o cantidad inválida.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (!NarrativeServices.TryGet(out INarrativeInventory inventory, nameof(GiveInventoryItemNode)) || !inventory.IsAvailable)
        {
            if (logWarnings)
                Debug.LogWarning("[GiveInventoryItemNode] No hay inventario del jugador.");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Clave estable por grafo + nodo (+ id del objeto por claridad): el nodo no da dos veces.
        string graphName = ctx?.Graph != null ? ctx.Graph.name : "<no-graph>";
        string key = $"INV_GIVEN:{graphName}:{guid}:{item.ItemId}";
        if (ctx != null && ctx.Blackboard != null)
        {
            if (ctx.Blackboard.Get<bool>(key, false))
            {
                if (logWarnings)
                    NarrativeLog.Info($"[GiveInventoryItemNode] Ítem '{item.ItemId}' ya entregado por este nodo. Omitiendo duplicado.");
                onReadyToAdvance?.Invoke();
                return;
            }
        }

        inventory.Add(item, amount);

        if (ctx != null && ctx.Blackboard != null)
        {
            ctx.Blackboard.Set(key, true);
        }
        onReadyToAdvance?.Invoke();
    }
}
