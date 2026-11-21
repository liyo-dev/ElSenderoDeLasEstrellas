using System;
using UnityEngine;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Espera a que entre un objeto concreto en el inventario del jugador (INarrativeInventory), p. ej.
    /// al recogerlo o comprarlo.
    /// </summary>
    [Serializable]
    [NarrativeNodeInfo("Objetos", "Esperar objeto", "Espera a que el jugador consiga un objeto.")]
    [SavePoint("Seguro guardar mientras espera item")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class WaitForItemAddedNode : NarrativeNode, INarrativeStateEffect
    {
        [Header("Item Configuration")]
        [Tooltip("El objeto que debe entrar en el inventario")]
        [SerializeField] private NarrativeItemAsset itemToWaitFor;
        public NarrativeItemAsset ItemToWaitFor { get => itemToWaitFor; set => itemToWaitFor = value; }

        [Tooltip("Cantidad mínima que debe añadirse de una sola vez (0 = cualquier cantidad)")]
        [Min(0)] [SerializeField] private int minimumAmount = 0;

        [Header("Options")]
        [Tooltip("Si está activo, se completa inmediatamente si el objeto ya está en el inventario")]
        [SerializeField] private bool completeIfAlreadyOwned = false;
        public bool CompleteIfAlreadyOwned { get => completeIfAlreadyOwned; set => completeIfAlreadyOwned = value; }

        [Tooltip("Activar logs de debug")]
        [SerializeField] private bool debugLogs = false;

        [NonSerialized] private Action<NarrativeItemAsset, int, int> _itemAddedHandler;
        [NonSerialized] private INarrativeInventory _inventory;

        public override void Describe(INarrativeNodeDescriber d) => d.Item(itemToWaitFor, minimumAmount, "espera");

        void Log(string msg)
        {
            if (debugLogs)
                NarrativeLog.Info($"[WaitForItemAdded:{Guid}] {msg}");
        }

        public void Project(INarrativeStateWriter state)
        {
            if (itemToWaitFor != null) state.EnsureItem(itemToWaitFor.ItemId, minimumAmount > 0 ? minimumAmount : 1);
        }

        public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
        {
            if (itemToWaitFor == null)
            {
                Debug.LogWarning($"[WaitForItemAdded:{Guid}] No se configuró itemToWaitFor, avanzando inmediatamente");
                onReadyToAdvance?.Invoke();
                return;
            }

            if (!NarrativeServices.TryGet(out INarrativeInventory inventory, nameof(WaitForItemAddedNode)) || !inventory.IsAvailable)
            {
                Debug.LogWarning($"[WaitForItemAdded:{Guid}] No hay inventario del jugador, avanzando inmediatamente");
                onReadyToAdvance?.Invoke();
                return;
            }

            if (completeIfAlreadyOwned)
            {
                int currentCount = inventory.Count(itemToWaitFor);
                if (currentCount > 0)
                {
                    Log($"El jugador ya tiene {currentCount}x {itemToWaitFor.name}, completando inmediatamente");
                    onReadyToAdvance?.Invoke();
                    return;
                }
            }

            _itemAddedHandler = (addedItem, addedAmount, newTotal) =>
            {
                if (addedItem == null || addedItem.ItemId != itemToWaitFor.ItemId)
                    return;

                if (minimumAmount > 0 && addedAmount < minimumAmount)
                {
                    Log($"Se añadió {addedAmount}x {addedItem.name} pero se requieren al menos {minimumAmount}, esperando más...");
                    return;
                }

                Log($"¡Objeto añadido! {addedAmount}x {addedItem.name} (total: {newTotal}) → completando nodo");
                Cleanup();
                onReadyToAdvance?.Invoke();
            };

            if (!inventory.WatchItemAdded(_itemAddedHandler))
            {
                _itemAddedHandler = null;
                Debug.LogWarning($"[WaitForItemAdded:{Guid}] No se puede vigilar el inventario, avanzando inmediatamente");
                onReadyToAdvance?.Invoke();
                return;
            }
            _inventory = inventory;
            Log($"Esperando a que se añada {itemToWaitFor.name}" + (minimumAmount > 0 ? $" (mínimo {minimumAmount})" : ""));
        }

        public override void Exit(NarrativeContext ctx)
        {
            Cleanup();
        }

        void Cleanup()
        {
            if (_itemAddedHandler != null && _inventory != null)
            {
                _inventory.UnwatchItemAdded(_itemAddedHandler);
                Log("Desuscrito del inventario");
            }
            _itemAddedHandler = null;
            _inventory = null;
        }
    }
}
