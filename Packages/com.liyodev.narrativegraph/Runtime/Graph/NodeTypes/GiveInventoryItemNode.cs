using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Añade un objeto al inventario del jugador (INarrativeInventory). Es idempotente: el mismo nodo
    /// no vuelve a dar el objeto si se repasa por él.
    /// </summary>
    [Serializable]
    [NarrativeNodeInfo("Objetos", "Dar objeto", "Añade un objeto al inventario del jugador.")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class GiveInventoryItemNode : NarrativeNode, INarrativeStateEffect
    {
        [Header("Item")]
        [SerializeField] private NarrativeItemAsset item;
        public NarrativeItemAsset Item { get => item; set => item = value; }
        [Min(1)] [SerializeField] private int amount = 1;
        public int Amount { get => amount; set => amount = value; }
        [Tooltip("Cuando está activo, loguea advertencias si no se encuentra inventario o item.")]
        [SerializeField] private bool logWarnings = true;
        [Tooltip("Sin efecto: el nodo ya es idempotente (lo recuerda en el blackboard). Se conserva por los grafos guardados.")]
        [SerializeField] private bool onlyOnce = true;
        public bool OnlyOnce { get => onlyOnce; set => onlyOnce = value; }

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
            string key = $"INV_GIVEN:{graphName}:{Guid}:{item.ItemId}";
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
}
