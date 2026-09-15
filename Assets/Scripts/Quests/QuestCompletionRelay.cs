using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────
// CONGELADO (12 sept 2026) — uno de los 7 micro-componentes de quest legacy.
// Sustituto: WaitQuestCompleteNode del grafo (mismo hecho — "quest completada" —
// ya resuelto ahí sin UnityEvent cableado a mano). Sigue vivo en MainWorld_old.
// Ver claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md § 2.
// ───────────────────────────────────────────────────────────────────────────
public class QuestCompletionRelay : MonoBehaviour
{
    [SerializeField] private string questId;
    [SerializeField] private UnityEngine.Events.UnityEvent onCompleted;
    bool fired;

    void OnEnable()
    {
        if (QuestManager.Instance) QuestManager.Instance.OnQuestsChanged += Check;
        Check();
    }
    void OnDisable()
    {
        if (QuestManager.Instance) QuestManager.Instance.OnQuestsChanged -= Check;
    }

    void Check()
    {
        if (fired || QuestManager.Instance == null) return;
        if (QuestManager.Instance.GetState(questId) == QuestState.Completed)
        {
            fired = true;
            onCompleted?.Invoke();
        }
    }
}