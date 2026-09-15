using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────
// CONGELADO (12 sept 2026) — uno de los 7 micro-componentes de quest legacy.
// Sustituto: SignalEmitter(trigger=OnInteract) o InteractableToNarrativeEvent +
// CompleteQuestStepsNode/RequireInventoryItemNode del grafo. Sigue vivo en
// MainWorld_old.unity.
// Ver claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md § 2.
// ───────────────────────────────────────────────────────────────────────────
/// <summary>
/// Marca un paso de misión cuando el objeto se recoge mediante un Interactable.
/// Se autoconecta al UnityEvent del Interactable para evitar configurar listeners manualmente.
/// </summary>
public class SimpleQuestPickup : MonoBehaviour
{
    [SerializeField] string questId;
    [SerializeField] int stepIndex;
    [SerializeField] bool completeOnPickup = false;
    [SerializeField] bool debugLogs = false;

    Interactable _interactable;

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
    }

    void OnEnable()
    {
        if (_interactable != null)
            _interactable.OnInteract.AddListener(OnInteract);
    }

    void OnDisable()
    {
        if (_interactable != null)
            _interactable.OnInteract.RemoveListener(OnInteract);
    }

    void OnInteract(GameObject _)
    {
        Pick();
    }

    public void Pick()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;
        var state = qm.GetState(questId);
        if (debugLogs)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SimpleQuestPickup] Pick quest={questId} state={state}");
            #endif
        }

        if (!completeOnPickup)
            return;

        if (state == QuestState.Active)
            qm.MarkStepDone(questId, stepIndex);
    }
}
