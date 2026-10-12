using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Quién lleva el jugador y qué cuerpo representa a cada personaje del grupo en este momento.
/// Es la puerta única para el resto del juego: los sistemas nuevos preguntan aquí en lugar de
/// mirar `ActiveCharacterSwapper`, `PlayerParty` o el NPC de Will cada uno por su lado. Hoy hay
/// un solo cuerpo jugable (el de `_WILL`, que se disfraza del personaje al mando) y los demás son
/// NPCs; cuando cada personaje tenga su cuerpo propio solo cambia esta clase. Ver INC-717.
public static class ControlDelJugador
{
    public const int Personajes = 3;

    private const string NombreDeLiam = "Liam";
    private const string NombreDeEstela = "Estela";

    /// El personaje que lleva el jugador.
    public static Slot Activo =>
        PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : Slot.Will;

    /// El cuerpo que mueve el jugador (el registrado en PlayerService).
    public static GameObject CuerpoDelJugador => PlayerService.Player;

    /// El personaje está en el grupo (Will siempre; Estela y Liam cuando se han unido).
    public static bool EnElGrupo(Slot personaje)
    {
        if (personaje == Slot.Will) return true;
        if (!PlayerParty.HasInstance) return false;
        return PlayerParty.Instance.GetMemberByName(Nombre(personaje)) != null;
    }

    /// El cuerpo que representa ahora a ese personaje: el del jugador si es el activo, su NPC si
    /// lo lleva la IA. Null si no está en escena.
    public static GameObject CuerpoDe(Slot personaje)
    {
        if (personaje == Activo) return CuerpoDelJugador;
        var npc = NpcDe(personaje);
        return npc != null ? npc.gameObject : null;
    }

    /// El NPC que representa a ese personaje cuando lo lleva la IA (el de Will solo existe
    /// mientras otro va al mando). Null si es el activo o no está en escena.
    public static NPCPartyMember NpcDe(Slot personaje)
    {
        if (personaje == Activo) return null;
        var swapper = ActiveCharacterSwapper.Instance;
        NPCPartyMember npc;
        if (personaje == Slot.Will)
            npc = swapper != null ? swapper.WillNpcInstance : null;
        else
            npc = PlayerParty.HasInstance ? PlayerParty.Instance.GetMemberByName(Nombre(personaje)) : null;
        if (npc == null || (swapper != null && swapper.HiddenNpc == npc)) return null;
        return npc;
    }

    /// Qué personaje es un cuerpo del grupo (el del jugador o el NPC de un compañero).
    public static bool PersonajeDe(GameObject cuerpo, out Slot personaje)
    {
        personaje = Slot.Will;
        if (cuerpo == null) return false;
        var jugador = CuerpoDelJugador;
        if (jugador != null && (cuerpo == jugador || cuerpo.transform.IsChildOf(jugador.transform)
                                || jugador.transform.IsChildOf(cuerpo.transform)))
        {
            personaje = Activo;
            return true;
        }
        var ficha = cuerpo.GetComponentInParent<Personaje>();
        if (ficha == null || ficha.Ficha == null) return false;
        personaje = ficha.Ficha.Personaje;
        return true;
    }

    /// Pasa el mando a otro personaje aunque el cambio esté bloqueado por el modo de acción
    /// (lo usa el relevo al caer el que va al mando). Respeta los momentos «solo Will».
    public static bool PasarElMandoA(Slot personaje)
    {
        var pcm = PartyControlManager.Instance;
        if (pcm == null || pcm.IsSwitchingLocked) return false;
        if (personaje == pcm.ActiveSlot) return true;
        if (!EnElGrupo(personaje)) return false;
        var swapper = ActiveCharacterSwapper.Instance;
        if (swapper == null || !swapper.IsReady) return false;
        pcm.ForceSwitch(personaje);
        return pcm.ActiveSlot == personaje;
    }

    public static string Nombre(Slot personaje) => personaje switch
    {
        Slot.Liam => NombreDeLiam,
        Slot.Estela => NombreDeEstela,
        _ => "Will"
    };
}
