
using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────
// CONGELADO (12 sept 2026) — uno de los 7 micro-componentes de quest legacy.
// Sustituto: SignalEmitter(trigger=PhysicsTrigger) + CompleteQuestStepsNode del
// grafo. Sigue vivo en MainWorld_old.unity.
// Ver claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md § 2.
// ───────────────────────────────────────────────────────────────────────────
public class QuestProgressOnTrigger : MonoBehaviour
{
    [SerializeField] string questId;
    [SerializeField] int stepIndex;
    [SerializeField] bool once = true;
    bool used;

    void OnTriggerEnter(Collider other)
    {
        if (used || !other.CompareTag("Player")) return;
        QuestManager.Instance?.MarkStepDone(questId, stepIndex);
        if (once) used = true;
    }
}
