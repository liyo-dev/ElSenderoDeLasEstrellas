using System;

/// <summary>
/// Estado de una quest tal y como lo ve el grafo. Es deliberadamente más rico que
/// QuestState (Inactive/Active/Completed): distingue "activa con todos los pasos hechos
/// pero sin entregar", que es el estado que decide si un NPC dice "¿ya lo tienes?" o
/// "¡gracias!" (BranchQuestStateNode).
/// </summary>
public enum NarrativeQuestState
{
    NotStarted = 0,
    Active = 1,
    StepsReady = 2,
    Completed = 3
}

public interface INarrativeSignals
{
    // QUEST
    void OfferQuest(string questId, object npcContext);
    bool IsQuestCompleted(string questId);
    void OnQuestCompleted(string questId, Action cb);
    void OffQuestCompleted(string questId, Action cb);
    void StartQuest(string questId, object npcContext);
    void CompleteQuest(string questId);
    void CompleteQuestStep(string questId, int stepIndex);
    void CompleteQuestStepByConditionId(string questId, string stepConditionId);

    /// <summary>Estado detallado de la quest (ver NarrativeQuestState).</summary>
    NarrativeQuestState GetQuestState(string questId);

    // BATTLE
    void OnBattleWon(object arena, Action cb);
    void OffBattleWon(object arena, Action cb);

    // CUSTOM EVENTS
    void RaiseCustom(string key);
    void RaiseCustom(string key, string context);
    void OnCustom(string key, Action cb);
    void OffCustom(string key, Action cb);

    /// <summary>True si la clave se disparó alguna vez en la partida actual (durable, no se consume).</summary>
    bool HasEverRaised(string key);

    /// <summary>
    /// Descarta una señal pendiente (sticky) sin consumirla. Para nodos que esperan un hecho
    /// "en vivo" (p. ej. hablar con un NPC) y no quieren heredar una interacción anterior.
    /// </summary>
    void ClearPendingCustom(string key);
}
