using System;
using UnityEngine;

/// <summary>
/// Comprueba que el jugador tenga un objeto (INarrativeInventory), opcionalmente lo consume y
/// actualiza una quest o un paso. Si falta el objeto puede seguir por otra salida.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Objetos", "Requiere objeto", "Comprueba/consume un objeto del inventario; salida alternativa si falta.")]
public sealed class RequireInventoryItemNode : NarrativeNode
{
    [Header("Item requerido")]
    public NarrativeItemAsset item;
    [Min(1)] public int requiredAmount = 1;
    public bool consumeOnSuccess = true;
    public bool logWarnings = true;

    [Header("Quest")]
    [Tooltip("Quest a actualizar cuando el jugador entrega el item.")]
    [NarrativeKey(NarrativeKeyKind.Quest)]
    public string questId;
    [Tooltip("Step a marcar como completado (opcional).")]
    public int questStepIndex = -1;
    [Tooltip("Iniciar la quest si está inactiva antes de marcar el paso.")]
    public bool autoStartQuestIfInactive = true;
    [Tooltip("Si se activa, en lugar de marcar un paso se completará toda la quest.")]
    public bool completeQuestInstead = false;

    [Header("Flujo si falta el item")]
    [Tooltip("Cuando es true y falta el item, se salta a la salida alternateOutputIndex (p.ej. para diálogos distintos).")]
    public bool useAlternateOutputOnMissing = true;
    [Tooltip("Índice de la salida a usar cuando falta el item (por defecto 1).")]
    public int alternateOutputIndex = 1;

    /// Decide por índice desde Enter() (ForceJumpToOutput) aunque no tenga puertos con nombre.
    public override int ProjectionPort => ProjectionStops;

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Item(item, requiredAmount, consumeOnSuccess ? "requiere y consume" : "requiere");
        if (!string.IsNullOrEmpty(questId)) d.Quest(questId, "Quest");
    }

    public override string DescribeDecision()
        => $"Requiere objeto \"{displayTitle}\" (guid {guid}): necesita '{(item != null ? item.ItemId : "<sin objeto>")}' x{requiredAmount}.";

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (item == null || requiredAmount <= 0)
        {
            if (logWarnings)
                Debug.LogWarning("[RequireInventoryItemNode] Item o cantidad inválida.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (!NarrativeServices.TryGet(out INarrativeInventory inventory, nameof(RequireInventoryItemNode)) || !inventory.IsAvailable)
        {
            if (logWarnings)
                Debug.LogWarning("[RequireInventoryItemNode] No hay inventario del jugador.");
            HandleMissing(ctx, onReadyToAdvance);
            return;
        }

        if (inventory.Has(item, requiredAmount))
        {
            if (consumeOnSuccess)
            {
                if (!inventory.TryConsume(item, requiredAmount))
                {
                    if (logWarnings)
                        Debug.LogWarning("[RequireInventoryItemNode] No se pudo consumir el item pese a que el conteo era suficiente.");
                }
            }

            ApplyQuestProgress(ctx);
            onReadyToAdvance?.Invoke();
        }
        else
        {
            HandleMissing(ctx, onReadyToAdvance);
        }
    }

    void HandleMissing(NarrativeContext ctx, Action readyCallback)
    {
        if (useAlternateOutputOnMissing && ctx != null && ctx.Runner != null && outputs != null)
        {
            if (alternateOutputIndex >= 0 && alternateOutputIndex < outputs.Count)
            {
                ctx.Runner.ForceJumpToOutput(this, alternateOutputIndex);
                return;
            }
        }

        readyCallback?.Invoke();
    }

    void ApplyQuestProgress(NarrativeContext ctx)
    {
        if (string.IsNullOrEmpty(questId))
            return;

        var signals = ctx?.Signals;
        if (signals == null)
        {
            if (logWarnings)
                Debug.LogWarning("[RequireInventoryItemNode] Sin proveedor de señales: no se actualiza el progreso de la misión.");
            return;
        }

        if (completeQuestInstead)
        {
            signals.CompleteQuest(questId);
            return;
        }

        if (questStepIndex >= 0)
        {
            if (autoStartQuestIfInactive && signals.GetQuestState(questId) == NarrativeQuestState.NotStarted)
                signals.StartQuest(questId, null);

            signals.CompleteQuestStep(questId, questStepIndex);
            return;
        }

        if (autoStartQuestIfInactive)
            signals.StartQuest(questId, null);
    }
}
