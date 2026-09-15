using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────
// CONGELADO (12 sept 2026) — uno de los 7 micro-componentes de quest legacy.
// Sustituto: BranchQuestStateNode del grafo decidiendo, + el propio grafo
// desactivando el GO (hoy no hay nodo directo "desactivar GO por condición" —
// se resuelve con ActivateGameObjectNode, pero ese está Obsolete; de momento
// sin equivalente 1:1 en el grafo — usar con criterio si hace falta ahora
// mismo). Sigue vivo en MainWorld_old.unity.
// Ver claude/catalogo-sistemas-legacy-vs-grafo-nuevo-2026-09-12.md § 2.
// ───────────────────────────────────────────────────────────────────────────
/// <summary>
/// Destruye o desactiva un objeto cuando se cumple el requisito de quest configurado.
/// Suscribe a QuestManager.OnQuestsChanged para reaccionar automáticamente.
/// </summary>
public class QuestObjectDisabler : MonoBehaviour
{
    [SerializeField] private QuestRequirement questRequirement;
    [SerializeField] private GameObject target;
    [SerializeField] private bool destroyObject = true;
    [SerializeField] private bool debugLogs;

    private bool _consumed;
    private bool _hasCheckedAfterDelay;

    void OnEnable()
    {
        var qm = QuestManager.Instance;
        if (qm)
            qm.OnQuestsChanged += Check;

        Check();

        if (!_consumed && !_hasCheckedAfterDelay)
            StartCoroutine(SingleDelayedCheck());
    }

    void OnDisable()
    {
        var qm = QuestManager.Instance;
        if (qm)
            qm.OnQuestsChanged -= Check;
    }

    private void Check()
    {
        if (_consumed) return;
        if (!questRequirement.IsConfigured) return;

        if (!questRequirement.IsSatisfied()) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLogs)
            Debug.Log($"[QuestObjectDisabler:{name}] Condición cumplida ({questRequirement.DebugDescription()}) → {(destroyObject ? "destruyendo" : "desactivando")}.");
#endif

        var go = target ? target : gameObject;
        if (go == null) return;

        _consumed = true;
        if (destroyObject)
            Destroy(go);
        else
            go.SetActive(false);
    }

    public void ForceCheck()
    {
        _consumed = false;
        Check();
    }

    private System.Collections.IEnumerator SingleDelayedCheck()
    {
        yield return null;
        if (!_consumed)
        {
            Check();
            _hasCheckedAfterDelay = true;
        }
    }
}
