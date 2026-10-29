using Game.NPC;
using UnityEngine;

/// <summary>
/// Los actores del grafo en El Sendero: NPCs por su persistenceId (<see cref="NPCRegistry"/>),
/// su icono de «tiene algo para ti» (<see cref="NarrativeActor"/>) y su colocación en marcas
/// (<see cref="SpawnAnchor"/>, con <see cref="SequenceActor"/> para moverlos).
/// </summary>
public sealed class NpcNarrativeAdapter : INarrativeActorDirectory, INarrativeAttentionMarkers, INarrativeActorPlacement
{
    // ── INarrativeActorDirectory ─────────────────────────────────────────────────────────────

    public Transform FindActor(string actorId)
    {
        var npc = FindNpc(actorId);
        return npc != null ? npc.transform : null;
    }

    static NPCBehaviourManagerV2 FindNpc(string actorId)
    {
        if (string.IsNullOrEmpty(actorId) || !NPCRegistry.HasInstance) return null;
        return NPCRegistry.Instance.GetNPCByID(actorId);
    }

    // ── INarrativeAttentionMarkers ───────────────────────────────────────────────────────────

    public void ShowMarker(string actorId, GameObject markerOverride)
    {
        var actor = FindNarrativeActor(actorId);
        if (actor != null) actor.ShowQuestIcon(markerOverride);
    }

    public void HideMarker(string actorId)
    {
        var actor = FindNarrativeActor(actorId);
        if (actor != null) actor.HideQuestIcon();
    }

    static NarrativeActor FindNarrativeActor(string actorId)
    {
        var npc = FindNpc(actorId);
        var actor = npc != null ? npc.GetComponent<NarrativeActor>() : null;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (actor == null && !string.IsNullOrEmpty(actorId))
            Debug.LogWarning($"[NpcNarrativeAdapter] '{actorId}' no tiene NarrativeActor: no se puede mostrar ni ocultar su icono.");
#endif
        return actor;
    }

    // ── INarrativeActorPlacement ─────────────────────────────────────────────────────────────

    public bool TryPlace(string actorId, string markId, bool applyLocationSettings, bool makeVisible)
    {
        var mark = string.IsNullOrWhiteSpace(markId) ? null : SpawnAnchor.FindById(markId);
        if (mark == null || !SequenceActor.TryResolve(actorId, out var actor)) return false;

        if (applyLocationSettings && actorId == SequenceActor.PlayerId)
            TeleportService.TeleportToAnchor(actor.Transform.gameObject, markId, false);
        else
            SequenceMovement.PlaceAt(actor, mark.transform.position, mark.GetCharacterRotation());
        if (makeVisible) actor.SetRenderingVisible(true);
        actor.ForzarEnElSuelo();
        return true;
    }
}
