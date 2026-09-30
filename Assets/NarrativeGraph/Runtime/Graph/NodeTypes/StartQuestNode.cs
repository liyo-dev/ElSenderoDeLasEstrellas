using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[NarrativeNodeInfo("Quests", "Iniciar quest", "Arranca la quest en el QuestManager (aparece en el diario). Si se indica quién la encarga, ese personaje dice su frase de «mientras tanto» cuando se le habla con la quest en curso.")]
public sealed class StartQuestNode : NarrativeNode, INarrativeStateEffect, INarrativeStandingLine
{
    [NarrativeKey(NarrativeKeyKind.Quest)]
    public string questId;

    [Header("Mientras está en curso")]
    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("Personaje que encarga la quest. Si el jugador le habla mientras la quest sigue en curso y el grafo no está esperando esa conversación, dice el diálogo de abajo.")]
    public string giverId;

    [Tooltip("Lo que dice quien encarga la quest si se le habla mientras sigue en curso (p. ej. una pista de qué hay que hacer).")]
    public DialogueAsset inProgressDialogue;

    [Tooltip("Márcalo solo si nadie encarga la quest (se la propone el propio jugador). Si no, toda quest necesita quién la encarga y su diálogo de mientras tanto: ningún personaje se queda sin decir nada.")]
    public bool noGiver;

    public string StandingActorId => giverId;
    public DialogueAsset StandingDialogue => inProgressDialogue;

    public bool IsStandingLineActive(INarrativeSignals signals)
    {
        if (signals == null || string.IsNullOrWhiteSpace(questId)) return false;
        var state = signals.GetQuestState(questId);
        return state == NarrativeQuestState.Active || state == NarrativeQuestState.StepsReady;
    }

    public override void CollectWarnings(List<string> warnings)
    {
        bool hayQuien = !string.IsNullOrWhiteSpace(giverId);
        bool hayDialogo = inProgressDialogue != null;

        if (noGiver)
        {
            if (hayQuien || hayDialogo)
                warnings.Add("Marcada «nadie la encarga» pero tiene personaje o diálogo de mientras tanto");
            return;
        }
        if (!hayQuien && !hayDialogo)
            warnings.Add("Nadie dice nada mientras está en curso: pon quién la encarga y su diálogo, o marca «No Giver» si es una quest propia del jugador");
        else if (!hayQuien)
            warnings.Add("Tiene diálogo de mientras tanto pero no quién lo dice");
        else if (!hayDialogo)
            warnings.Add("Quien la encarga no tiene diálogo de mientras tanto");
    }

    public void Project(INarrativeStateWriter state)
    {
        if (!string.IsNullOrWhiteSpace(questId)) state.StartQuest(questId);
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        // Clave por nodo: el mismo nodo no vuelve a iniciar la quest si se repasa por él.
        var questStartedKey = $"__quest_{guid}_{questId}_started";
        if (ctx.Blackboard.Get<bool>(questStartedKey, false))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[StartQuestNode:{guid}] Quest {questId} ya fue iniciada previamente por este nodo → saltando inicio");
#endif
            ready?.Invoke();
            return;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[StartQuestNode:{guid}] Start {questId}");
#endif
        ctx.Signals.StartQuest(questId, null);
        ctx.Blackboard.Set(questStartedKey, true);

        ready?.Invoke();
    }
}
