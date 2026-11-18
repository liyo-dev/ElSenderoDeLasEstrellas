using System.Collections.Generic;
using Game.NPC;
using UnityEngine;

/// <summary>
/// Los actores del grafo en El Sendero: NPCs por su persistenceId (<see cref="NPCRegistry"/>) y
/// actores sin IA por su id (<see cref="NarrativeActor.TryGetSinIA"/>), su icono de «tiene algo
/// para ti» (<see cref="NarrativeActor"/>) y la colocación y presencia de los NPCs en marcas
/// (<see cref="SpawnAnchor"/>, con <see cref="SequenceActor"/> para moverlos).
/// </summary>
public sealed class NpcNarrativeAdapter : INarrativeActorDirectory, INarrativeAttentionMarkers, INarrativeActorPlacement, INarrativeActorPresence
{
    // ── INarrativeActorDirectory ─────────────────────────────────────────────────────────────

    public Transform FindActor(string actorId)
    {
        // El jugador no es un NPC del registro: se busca con los mismos ids que acepta
        // SequenceActor («Player», «Actor_Will», «will»).
        if (EsElJugador(actorId)) return Game.NPC.Common.PlayerLocator.ResolvePlayer();
        if (NarrativeActor.TryGetSinIA(actorId, out var sinIA)) return sinIA.transform;
        var npc = FindNpc(actorId);
        return npc != null ? npc.transform : null;
    }

    static bool EsElJugador(string actorId)
        => actorId == SequenceActor.PlayerId || actorId == "Actor_Will" || actorId == "will";

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
        if (NarrativeActor.TryGetSinIA(actorId, out var sinIA)) return sinIA;
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
        {
            QuedarseEnLaMarca(actor.Manager);
            SequenceMovement.PlaceAt(actor, mark.transform.position, mark.GetCharacterRotation());
        }
        if (makeVisible) actor.SetRenderingVisible(true);
        actor.ForzarEnElSuelo();
        return true;
    }

    /// Un acompañante que no es del trío (Will, Estela, Liam) y que la historia coloca en una marca
    /// se queda en ella: sale del grupo y deja de seguir al jugador. Si no, el seguimiento lo
    /// arrastra fuera de la marca y, al ser del grupo, no se le puede hablar (Interactable), así que
    /// una charla que espere el grafo con él no llegaría nunca. Ver INC-904.
    static void QuedarseEnLaMarca(NPCBehaviourManagerV2 npc)
    {
        if (npc == null || ControlDelJugador.PersonajeDe(npc.gameObject, out _)) return;
        var miembro = npc.GetComponent<NPCPartyMember>();
        if (miembro == null) return;
        if (miembro.IsInParty) miembro.LeaveParty();
        if (miembro.EstaSiguiendo) npc.ForceIdle();
    }

    // ── INarrativeActorPresence ──────────────────────────────────────────────────────────────

    // Más cerca que esto, el jugador vería al actor desaparecer, aparecer o saltar de sitio.
    const float DistanciaSinTestigos = 25f;

    // Actores retirados por una agenda: se apagan y se guardan aquí para poder devolverlos.
    readonly Dictionary<string, GameObject> _retirados = new();

    public NpcNarrativeAdapter() => RepartoDeEscena.Tomado += TraerParaLaEscena;

    /// Una cinemática toma al personaje: si la historia lo tenía retirado, vuelve ya, esté donde
    /// esté el jugador. La cinemática manda en la pantalla y lo coloca ella.
    void TraerParaLaEscena(string actorId)
    {
        if (!_retirados.TryGetValue(actorId, out var go)) return;
        _retirados.Remove(actorId);
        if (go != null) go.SetActive(true);
    }

    public bool IsPresent(string actorId) => BuscarSinAviso(actorId) != null;

    public bool TrySetPresent(string actorId, bool present)
    {
        if (string.IsNullOrEmpty(actorId)) return true;
        // Mientras una cinemática lo tiene, es suyo: la historia lo hará al terminar.
        if (RepartoDeEscena.EstaTomado(actorId)) return false;
        if (!present)
        {
            var npc = BuscarSinAviso(actorId);
            if (npc == null) return true;
            // Un NPC peleando o haciendo una cinemática no se retira: lo necesitan en escena y, si
            // desapareciera, la pelea o el tramo seguirían contando con él (ver PedirCombateContraNpc).
            // Se reintenta en la siguiente revisión, cuando ya no esté ocupado.
            if (npc.IsInCombatOrAlert || npc.IsInCinematic) return false;
            // Un actor que ya no se ve (una escena lo ha hecho desaparecer) se puede retirar aunque
            // el jugador esté al lado: nadie le ve irse (INC-1031).
            if (CercaDelJugador(npc.transform.position) && SeVe(npc)) return false;
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

    /// La historia lo necesita ya (un combate que empieza, una charla en curso): vuelve a escena
    /// aunque el jugador lo vea aparecer. Aquí no vale esperar a que nadie mire.
    public bool BringOnStage(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return true;
        // Mientras una cinemática lo tiene, es suya: la historia lo hará al terminar.
        if (RepartoDeEscena.EstaTomado(actorId)) return false;

        if (_retirados.TryGetValue(actorId, out var retirado))
        {
            _retirados.Remove(actorId);
            if (retirado != null) retirado.SetActive(true);
        }

        // También puede haberlo apagado otra cosa (un tramo, el reparto de la escena): la historia
        // lo está pidiendo en el acto, así que se enciende igual.
        var npc = BuscarAunqueNoEsté(actorId);
        if (npc == null) return false;
        if (!npc.gameObject.activeSelf) npc.gameObject.SetActive(true);
        return npc.gameObject.activeInHierarchy;
    }

    public bool TryPlaceUnseen(string actorId, string markId)
    {
        if (string.IsNullOrWhiteSpace(markId)) return true; // Sin marca no hay nada que hacer.
        if (RepartoDeEscena.EstaTomado(actorId)) return false; // Ver TrySetPresent.
        // La marca o el actor pueden no estar aún (la agenda empieza antes de cargar el mundo):
        // se reintenta hasta que estén.
        var mark = SpawnAnchor.FindById(markId);
        if (mark == null) return false;

        // Quien sigue al jugador siempre está cerca de él y nunca se le podría mover sin testigos:
        // deja de seguirle ya y se le lleva a la marca cuando el jugador se aleje.
        var npc = _retirados.TryGetValue(actorId, out var retirado) && retirado != null ? null : BuscarSinAviso(actorId);
        if (npc != null) QuedarseEnLaMarca(npc);

        bool marcaALaVista = CercaDelJugador(mark.transform.position);

        if (_retirados.TryGetValue(actorId, out var go) && go != null)
        {
            if (marcaALaVista) return false;
            go.SetActive(true);
            _retirados.Remove(actorId);
        }
        else
        {
            if (npc == null) return false;
            // Si el jugador lo vería saltar de sitio, va andando: así no se queda plantado donde
            // estaba mientras dice que se va a otro sitio. Ver INC-1121.
            if (marcaALaVista || CercaDelJugador(npc.transform.position)) return IrAndando(npc, mark);
        }
        return TryPlace(actorId, markId, false, true);
    }

    static bool IrAndando(NPCBehaviourManagerV2 npc, SpawnAnchor mark)
    {
        Vector3 destino = mark.transform.position;
        float metros = Vector3.Distance(npc.transform.position, destino);
        if (metros < 0.5f) return true;
        Quaternion giro = mark.GetCharacterRotation();
        npc.MoveToPosition(destino, maxDuration: metros / 1.2f + 10f, turn: true,
            onComplete: () => { if (npc != null) npc.transform.rotation = giro; });
        return true;
    }

    static NPCBehaviourManagerV2 BuscarSinAviso(string actorId) =>
        !string.IsNullOrEmpty(actorId) && NPCRegistry.HasInstance && NPCRegistry.Instance.TryGetNPCByID(actorId, out var npc) && npc.isActiveAndEnabled
            ? npc : null;

    /// El NPC aunque esté apagado (la historia puede tenerlo retirado): el registro lo conserva.
    static NPCBehaviourManagerV2 BuscarAunqueNoEsté(string actorId) =>
        !string.IsNullOrEmpty(actorId) && NPCRegistry.HasInstance && NPCRegistry.Instance.TryGetNPCByID(actorId, out var npc)
            ? npc : null;

    static bool SeVe(NPCBehaviourManagerV2 npc)
    {
        foreach (var r in npc.GetComponentsInChildren<Renderer>(false))
            if (r.enabled && !r.forceRenderingOff) return true;
        return false;
    }

    static bool CercaDelJugador(Vector3 posicion) =>
        PlayerService.TryGetPlayer(out GameObject jugador) && jugador != null
        && (jugador.transform.position - posicion).sqrMagnitude < DistanciaSinTestigos * DistanciaSinTestigos;
}
