using UnityEngine;

/// <summary>
/// Muestra un icono flotante sobre este GameObject (mismo componente que ya usan los NPCs para
/// su icono de cabeza, <see cref="Game.NPC.Common.NPCAlertIconController"/>) mientras la quest
/// indicada está activa y el jugador todavía no ha interactuado con este objeto. Pensado para
/// colocarse junto a <see cref="QuestObjectiveMarker"/> (mismo objeto, mismo questId) para que un
/// objetivo de quest en el mundo tenga icono de cabeza Y marcador de minimapa a la vez — no es
/// específico de la caja de Eldran, cualquier objeto interactuable de una quest puede reutilizarlo.
///
/// A diferencia de <see cref="QuestObjectiveMarker"/> (que se oculta al completarse un PASO de la
/// quest vía <c>QuestManager.MarkStepDone</c>), este componente se oculta directamente al primer
/// <see cref="Interactable.OnInteract"/> del propio objeto — necesario para quests como
/// ELDRAN_MISSION2 que no marcan pasos intermedios (la quest queda "Active" de principio a fin,
/// ver claude/incidencia-caja-eldran-sin-icono-minimapa-2026-09-12.md).
///
/// Ver claude/propuesta-mejora-secuencia-entrega-objetos-npc-2026-09-15.md en el Project.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class WorldQuestIcon : MonoBehaviour
{
    [SerializeField] private string questId;
    [SerializeField] private GameObject iconPrefab;

    private Game.NPC.Common.NPCAlertIconController _iconController;
    private Interactable _interactable;
    private bool _consumed;
    private bool _visible;

    private void Start()
    {
        _interactable = GetComponent<Interactable>();
        if (_interactable != null)
            _interactable.OnInteract.AddListener(OnInteracted);

        // Hijo dedicado, mismo patrón que QuestObjectiveMarker con su MinimapMarker: evita
        // colisionar con cualquier otro icono/controlador que pueda vivir en este GameObject.
        var child = new GameObject("_WorldQuestIcon").transform;
        child.SetParent(transform, false);
        _iconController = child.gameObject.AddComponent<Game.NPC.Common.NPCAlertIconController>();

        if (QuestManager.Instance != null)
            Subscribe();
        else
            GameBootService.OnProfileReady += OnProfileReady;

        Refresh();
    }

    private void OnDestroy()
    {
        if (_interactable != null)
            _interactable.OnInteract.RemoveListener(OnInteracted);

        Unsubscribe();
        GameBootService.OnProfileReady -= OnProfileReady;
    }

    private void OnProfileReady()
    {
        GameBootService.OnProfileReady -= OnProfileReady;
        Subscribe();
        Refresh();
    }

    private void Subscribe()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestStarted   += OnQuestEvent;
        QuestManager.Instance.OnQuestCompleted += OnQuestEvent;
        QuestManager.Instance.OnQuestsChanged  += Refresh;
    }

    private void Unsubscribe()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestStarted   -= OnQuestEvent;
        QuestManager.Instance.OnQuestCompleted -= OnQuestEvent;
        QuestManager.Instance.OnQuestsChanged  -= Refresh;
    }

    private void OnQuestEvent(string id)
    {
        if (id == questId) Refresh();
    }

    private void OnInteracted(GameObject who)
    {
        // El jugador ya cogió/usó el objeto: el icono de "búscame" deja de tener sentido a
        // partir de aquí, sin importar en qué estado quede la quest después.
        _consumed = true;
        Refresh();
    }

    private void Refresh()
    {
        bool shouldShow = EvalVisibility();
        if (shouldShow == _visible) return;
        _visible = shouldShow;

        if (_iconController == null) return;
        if (shouldShow) _iconController.ShowPersistentIcon(iconPrefab);
        else _iconController.HideAlertIcon();
    }

    private bool EvalVisibility()
    {
        if (_consumed || iconPrefab == null || string.IsNullOrEmpty(questId) || QuestManager.Instance == null)
            return false;

        return QuestManager.Instance.GetState(questId) == QuestState.Active;
    }
}
