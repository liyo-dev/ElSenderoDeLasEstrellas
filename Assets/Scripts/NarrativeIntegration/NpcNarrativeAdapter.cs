using System.Collections.Generic;
using Game.NPC;
using UnityEngine;

/// <summary>
/// Los actores del grafo en El Sendero: NPCs por su persistenceId (<see cref="NPCRegistry"/>),
/// su icono de «tiene algo para ti» (<see cref="NarrativeActor"/>) y su colocación en marcas
/// (<see cref="SpawnAnchor"/>, con <see cref="SequenceActor"/> para moverlos).
/// </summary>
public sealed class NpcNarrativeAdapter : INarrativeActorDirectory, INarrativeAttentionMarkers, INarrativeActorPlacement, INarrativeActorPresence
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

    // ── INarrativeActorPresence ──────────────────────────────────────────────────────────────

    // Más cerca que esto, el jugador vería al actor desaparecer, aparecer o saltar de sitio.
    const float DistanciaSinTestigos = 25f;

    // Actores retirados por una agenda: se apagan y se guardan aquí para poder devolverlos.
    readonly Dictionary<string, GameObject> _retirados = new();

    public bool IsPresent(string actorId) => BuscarSinAviso(actorId) != null;

    public bool TrySetPresent(string actorId, bool present)
    {
        if (string.IsNullOrEmpty(actorId)) return true;
        if (!present)
        {
            var npc = BuscarSinAviso(actorId);
            if (npc == null) return true;
            if (CercaDelJugador(npc.transform.position)) return false;
            _retirados[actorId] = npc.gameObject;
            npc.gameObject.SetActive(false);
            return true;
        }

        if (!_retirados.TryGetValue(actorId, out var go)) return true;
        if (go == null) { _retirados.Remove(actorId); return true; }
        if (CercaDelJugador(go.transform.position)) return false;
        go.SetActive(true);
        _retirados.Remove(actorId);
        return true;
    }

    public bool TryPlaceUnseen(string actorId, string markId)
    {
        var mark = string.IsNullOrWhiteSpace(markId) ? null : SpawnAnchor.FindById(markId);
        if (mark == null) return true; // Sin marca no hay nada que hacer; no se reintenta.
        if (CercaDelJugador(mark.transform.position)) return false;

        if (_retirados.TryGetValue(actorId, out var go) && go != null)
        {
            go.SetActive(true);
            _retirados.Remove(actorId);
        }
        else
        {
            var npc = BuscarSinAviso(actorId);
            if (npc == null) return true;
            if (CercaDelJugador(npc.transform.position)) return false;
        }
        return TryPlace(actorId, markId, false, true);
    }

    static NPCBehaviourManagerV2 BuscarSinAviso(string actorId) =>
        !string.IsNullOrEmpty(actorId) && NPCRegistry.HasInstance && NPCRegistry.Instance.TryGetNPCByID(actorId, out var npc) && npc.isActiveAndEnabled
            ? npc : null;

    static bool CercaDelJugador(Vector3 posicion) =>
        PlayerService.TryGetPlayer(out GameObject jugador) && jugador != null
        && (jugador.transform.position - posicion).sqrMagnitude < DistanciaSinTestigos * DistanciaSinTestigos;
}
