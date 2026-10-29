using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca uno o varios pasos de una quest como completados y, opcionalmente, cierra la quest.
/// Útil cuando un NPC otorga una recompensa que también debe avanzar la misión.
/// Los pasos se indican por id de condición (recomendado) o por índice.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Quests", "Completar pasos de quest", "Marca pasos como hechos y, opcionalmente, cierra la quest.")]
public sealed class CompleteQuestStepsNode : NarrativeNode, INarrativeStateEffect, INarrativeStandingLine
{
    [Header("Después de completar los pasos")]
    [NarrativeKey(NarrativeKeyKind.Actor)]
    public string standingActorId;
    [Tooltip("Frase del actor cuando se le habla después de este encargo.")]
    public NarrativeDialogueAsset standingDialogue;
    private string StandingFlag => "STEPS_COMPLETED_" + guid;
    public string StandingActorId => standingActorId;
    public NarrativeDialogueAsset StandingDialogue => standingDialogue;
    public bool IsStandingLineActive(INarrativeSignals signals) => NarrativeFlags.Has(StandingFlag);
    [Header("Quest")]
    [Tooltip("ID de la quest que se actualizará.")]
    [NarrativeKey(NarrativeKeyKind.Quest)]
    public string questId;

    [Header("Completar Steps por Condition ID (Recomendado)")]
    [Tooltip("Lista de Step Condition IDs que se marcarán como completados. Ej: 'QUEST_PUENTE_STEP_00'")]
    [NarrativeKey(NarrativeKeyKind.QuestStep, "questId")]
    public List<string> stepConditionIds = new();

    [Header("Completar Steps por Índice (si no hay Condition IDs)")]
    [Tooltip("Lista de índices de pasos que se marcarán como completados. Solo se usa si stepConditionIds está vacío.")]
    public List<int> steps = new();

    [Header("Opciones")]
    [Tooltip("Cuando está activo, completará toda la quest tras procesar los pasos.")]
    public bool completeQuest;

    [Tooltip("Muestra advertencias cuando los datos son inválidos.")]
    public bool logWarnings = true;

    public override void Describe(INarrativeNodeDescriber d)
        => d.Quest(questId, completeQuest ? "Completa pasos y CIERRA" : "Completa pasos", stepConditionIds);

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            warnings.Add("questId vacío");
            return;
        }
        bool porCondicion = stepConditionIds != null && stepConditionIds.Count > 0;
        bool porIndice = steps != null && steps.Count > 0;
        if (!porCondicion && !porIndice && !completeQuest)
            warnings.Add("No marca ningún paso ni cierra la quest");
        if (!porCondicion && porIndice && steps.Exists(step => step < 0))
            warnings.Add("Hay índices de paso negativos");
    }

    public void Project(INarrativeStateWriter state)
    {
        if (string.IsNullOrWhiteSpace(questId)) return;
        bool porCondicion = stepConditionIds != null && stepConditionIds.Count > 0;
        state.CompleteQuestSteps(questId, porCondicion ? stepConditionIds : null, porCondicion ? null : steps);
        if (completeQuest) state.CompleteQuest(questId);
        if (standingDialogue != null) state.SetFlag(StandingFlag, true);
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            if (logWarnings)
                Debug.LogWarning("[CompleteQuestStepsNode] questId vacío.");
            ready?.Invoke();
            return;
        }

        bool anyAction = false;
        bool warnedNoSteps = false;

        // Prioridad 1: por id de condición (no depende del orden de los pasos)
        if (stepConditionIds != null && stepConditionIds.Count > 0)
        {
            var processed = new HashSet<string>();
            foreach (var conditionId in stepConditionIds)
            {
                if (string.IsNullOrWhiteSpace(conditionId))
                {
                    if (logWarnings)
                        Debug.LogWarning($"[CompleteQuestStepsNode] Step Condition ID vacío para quest '{questId}'.");
                    continue;
                }

                if (!processed.Add(conditionId))
                    continue; // evitar repetir llamadas para el mismo ID

                // Siempre por ctx.Signals: el grafo no conoce el sistema de misiones del juego.
                if (ctx.Signals != null)
                {
                    ctx.Signals.CompleteQuestStepByConditionId(questId, conditionId);
                    anyAction = true;
                    NarrativeLog.Info($"[CompleteQuestStepsNode] ✅ Completando step con conditionId '{conditionId}' en quest '{questId}'");
                }
                else
                {
                    if (logWarnings)
                        Debug.LogWarning($"[CompleteQuestStepsNode] ctx.Signals es null - No se puede completar step '{conditionId}'");
                }
            }
        }
        // Prioridad 2: por índice, solo si no hay ids de condición
        else if (steps != null && steps.Count > 0)
        {
            var processed = new HashSet<int>();
            foreach (var stepIndex in steps)
            {
                if (stepIndex < 0)
                {
                    if (logWarnings)
                        Debug.LogWarning($"[CompleteQuestStepsNode] Índice de paso inválido ({stepIndex}) para quest '{questId}'.");
                    continue;
                }
                if (!processed.Add(stepIndex))
                    continue; // evitar repetir llamadas para el mismo índice

                ctx.Signals?.CompleteQuestStep(questId, stepIndex);
                anyAction = true;
                NarrativeLog.Info($"[CompleteQuestStepsNode] ✅ Completando step índice {stepIndex} en quest '{questId}'");
            }
        }
        else if (!completeQuest && logWarnings)
        {
            Debug.LogWarning($"[CompleteQuestStepsNode] No hay pasos configurados (ni por conditionId ni por índice) para quest '{questId}' y 'completeQuest' está desactivado.");
            warnedNoSteps = true;
        }

        if (completeQuest)
        {
            ctx.Signals?.CompleteQuest(questId);
            anyAction = true;
            NarrativeLog.Info($"[CompleteQuestStepsNode] ✅ Completando quest '{questId}'");
        }
        else if (!anyAction && logWarnings && !warnedNoSteps)
        {
            Debug.LogWarning($"[CompleteQuestStepsNode] Nada que hacer para quest '{questId}'.");
        }

        if (anyAction && standingDialogue != null) NarrativeFlags.Set(StandingFlag, true);
        ready?.Invoke();
    }
}
