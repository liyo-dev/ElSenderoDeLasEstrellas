using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────
// CONGELADO (12 sept 2026) — uno de los 7 micro-componentes de quest legacy.
// Sustituto: SignalEmitter (o InteractableToNarrativeEvent) emitiendo una señal
// + StartQuestNode del grafo decidiendo qué questId arrancar. Sigue vivo en
// MainWorld_old.unity. Ver claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md § 2.
// ───────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class QuestStarterOnInteract : MonoBehaviour
{
    [SerializeField] private string questIdToStart = string.Empty;
    [SerializeField] private bool onlyOnce = true;
    [SerializeField] private GameObject targetToHide;

    bool used;

    void Awake()
    {
        if (!targetToHide)
            targetToHide = gameObject;
    }

    void OnEnable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged += HandleQuestsChanged;

        EvaluateVisibility();
    }

    void OnDisable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged -= HandleQuestsChanged;
    }

    void HandleQuestsChanged()
    {
        EvaluateVisibility();
    }

    void EvaluateVisibility()
    {
        if (!onlyOnce) return;
        if (QuestManager.Instance == null) return;
        if (string.IsNullOrEmpty(questIdToStart)) return;

        var state = QuestManager.Instance.GetState(questIdToStart);
        if (state != QuestState.Inactive)
        {
            used = true;
            if (targetToHide && targetToHide.activeSelf)
                targetToHide.SetActive(false);
        }
    }

    // Llama a este método desde el OnFinished del Interactable (o desde un UnityEvent del propio diálogo)
    public void StartQuestNow()
    {
        if (used) return;

        QuestManager.Instance?.StartQuest(questIdToStart);
        used = onlyOnce;

        if (used && targetToHide)
            targetToHide.SetActive(false);
    }
}
