using System;
using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Quién lleva el jugador y qué cuerpo representa a cada personaje del grupo en este momento.
/// Es la puerta única para el resto del juego: los sistemas preguntan aquí en lugar de mirar
/// `ActiveCharacterSwapper`, `PlayerParty` o el NPC de Will cada uno por su lado. Hoy hay un solo
/// cuerpo jugable (el de `_WILL`, que se disfraza del personaje al mando) y los demás son NPCs;
/// cuando cada personaje tenga su cuerpo propio solo cambia esta clase. Ver INC-717.
///
/// También es el dueño del nombre de cada personaje: el que lleva su `NPCPartyConfig.displayName`
/// y con el que `PlayerParty` lo encuentra.
public static class ControlDelJugador
{
    public const int Personajes = 3;

    private const string NombreDeWill = "Will";
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
        return MiembroDe(personaje) != null;
    }

    /// El miembro del grupo que pone cuerpo a Estela o a Liam, lo lleve la IA o esté oculto porque
    /// el jugador va con ese personaje. Will no es miembro del grupo: null. Null también si no se
    /// ha unido.
    public static NPCPartyMember MiembroDe(Slot personaje)
    {
        if (personaje == Slot.Will || !PlayerParty.HasInstance) return null;
        return PlayerParty.Instance.GetMemberByName(Nombre(personaje));
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
        NPCPartyMember npc = personaje == Slot.Will
            ? (swapper != null ? swapper.WillNpcInstance : null)
            : MiembroDe(personaje);
        if (npc == null || RepresentadoPorElJugador(npc)) return null;
        return npc;
    }

    /// El NPC está oculto porque el jugador lleva a su personaje (su cuerpo lo pone el del
    /// jugador). Quien encienda renderers, mueva o despierte a un compañero lo pregunta antes:
    /// la visibilidad de ese NPC es del relevo de personaje (`ActiveCharacterSwapper`).
    public static bool RepresentadoPorElJugador(NPCPartyMember npc)
    {
        var swapper = ActiveCharacterSwapper.Instance;
        return npc != null && swapper != null && swapper.HiddenNpc == npc;
    }

    /// Qué personaje es un cuerpo del grupo (el del jugador o el NPC de un compañero, por la ficha
    /// de su componente `Personaje`).
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

    /// Nombre del personaje: el de su `NPCPartyConfig.displayName` y el que se muestra.
    public static string Nombre(Slot personaje) => personaje switch
    {
        Slot.Liam => NombreDeLiam,
        Slot.Estela => NombreDeEstela,
        _ => NombreDeWill
    };

    /// Qué personaje lleva ese nombre (sin distinguir mayúsculas). False si no es del grupo.
    public static bool SlotDeNombre(string nombre, out Slot personaje)
    {
        personaje = Slot.Will;
        if (string.IsNullOrEmpty(nombre)) return false;
        for (int i = 0; i < Personajes; i++)
        {
            var slot = (Slot)i;
            if (!string.Equals(nombre, Nombre(slot), StringComparison.OrdinalIgnoreCase)) continue;
            personaje = slot;
            return true;
        }
        return false;
    }
}
