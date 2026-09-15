
using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────
// CONGELADO (12 sept 2026) — uno de los 7 micro-componentes de quest legacy.
// Sustituto: SignalEmitter/InteractableToNarrativeEvent + CompleteQuestStepsNode
// del grafo. Sigue vivo en MainWorld_old.unity.
// Ver claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md § 2.
// ───────────────────────────────────────────────────────────────────────────
public class QuestProgressOnEvent : MonoBehaviour
{
    [SerializeField] string questId;
    [SerializeField] int stepIndex;

    public void Mark()
    {
        QuestManager.Instance?.MarkStepDone(questId, stepIndex);
    }
}
