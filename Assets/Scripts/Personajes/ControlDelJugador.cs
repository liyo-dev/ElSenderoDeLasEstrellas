using System;
using Game.NPC;
using UnityEngine;
using UnityEngine.AI;
using Slot = PartyControlManager.CharacterSlot;

/// Quién lleva el jugador y qué cuerpo tiene cada personaje del grupo. Es la puerta única para el
/// resto del juego: los sistemas preguntan aquí en lugar de mirar `PlayerParty` o los cuerpos cada
/// uno por su lado. Ver INC-717.
///
/// Cada personaje (Will, Estela y Liam) tiene su cuerpo propio y permanente, con su apariencia, su
/// ficha y su estado; lo mueve el jugador o la IA según su <see cref="PilotoDelCuerpo"/>. Cambiar de
/// personaje es cambiar de piloto (<see cref="CambiarElMando"/>): el cuerpo que sale pasa a la IA,
/// el que entra toma el mando y se registra en PlayerService (que avisa con OnPlayerRegistered), y
/// la cámara pasa a seguirle.
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

    // ─── ¿Es el jugador? ──────────────────────────────────────────────────

    /// Ese objeto (un colisionador, quien da un golpe, quien entra en una zona) es del cuerpo que
    /// lleva el jugador. Es la pregunta única para «¿es el jugador?»: tener un componente del
    /// jugador no basta, porque cada personaje del grupo lleva los suyos aunque lo mueva la IA.
    public static bool EsElJugador(Component desde) => desde != null && EsDelCuerpo(desde.transform, CuerpoDelJugador);

    /// <inheritdoc cref="EsElJugador(Component)"/>
    public static bool EsElJugador(GameObject desde) => desde != null && EsDelCuerpo(desde.transform, CuerpoDelJugador);

    /// El componente T del cuerpo que lleva el jugador, si <paramref name="desde"/> es de ese cuerpo.
    /// False (y null) si es de otro personaje, de un NPC o de cualquier otra cosa.
    public static bool DelJugador<T>(Component desde, out T componente) where T : Component
    {
        componente = null;
        var cuerpo = CuerpoDelJugador;
        if (desde == null || !EsDelCuerpo(desde.transform, cuerpo)) return false;
        componente = cuerpo.GetComponentInChildren<T>(true);
        return componente != null;
    }

    /// <inheritdoc cref="DelJugador{T}(Component, out T)"/>
    public static bool DelJugador<T>(GameObject desde, out T componente) where T : Component
    {
        componente = null;
        return desde != null && DelJugador(desde.transform, out componente);
    }

    private static bool EsDelCuerpo(Transform t, GameObject cuerpo)
    {
        if (cuerpo == null) return false;
        var raiz = cuerpo.transform;
        return t == raiz || t.IsChildOf(raiz);
    }

    // ─── Cuerpos y grupo ──────────────────────────────────────────────────

    /// El personaje está en el grupo (Will siempre; Estela y Liam cuando se han unido).
    public static bool EnElGrupo(Slot personaje)
    {
        if (personaje == Slot.Will) return true;
        return MiembroDe(personaje) != null;
    }

    /// El miembro de PlayerParty de Estela o de Liam (lo lleve la IA o el jugador). Will no es
    /// miembro del grupo: null. Null también si no se ha unido.
    public static NPCPartyMember MiembroDe(Slot personaje)
    {
        if (personaje == Slot.Will || !PlayerParty.HasInstance) return null;
        return PlayerParty.Instance.GetMemberByName(Nombre(personaje));
    }

    /// El piloto del cuerpo propio de ese personaje, lo lleve quien lo lleve. Null si no está en
    /// escena. Si hay más de un cuerpo suyo (un interior abierto en aditivo trae el suyo), manda el
    /// del miembro del grupo, luego el que lleva el jugador y, si no, el primero.
    public static PilotoDelCuerpo PilotoDe(Slot personaje)
    {
        if (personaje != Slot.Will)
        {
            var delGrupo = PilotoDelCuerpo.De(MiembroDe(personaje));
            if (delGrupo != null) return delGrupo;
        }
        PilotoDelCuerpo primero = null;
        var cuerpos = PilotoDelCuerpo.Cuerpos;
        for (int i = 0; i < cuerpos.Count; i++)
        {
            var c = cuerpos[i];
            if (c == null || !PersonajeDelCuerpo(c, out var p) || p != personaje) continue;
            if (c.LoLlevaElJugador) return c;
            if (primero == null) primero = c;
        }
        return primero;
    }

    /// El cuerpo de ese personaje: el del jugador si es el activo, el que lleva la IA si no. Null si
    /// no está en escena (o, Estela y Liam, si aún no son del grupo y no van al mando).
    public static GameObject CuerpoDe(Slot personaje)
    {
        if (personaje == Activo) return CuerpoDelJugador;
        var npc = NpcDe(personaje);
        return npc != null ? npc.gameObject : null;
    }

    /// El compañero que pone la IA en el cuerpo de ese personaje. Null si lo lleva el jugador, si
    /// no está en escena o, Estela y Liam, si no se han unido al grupo.
    public static NPCPartyMember NpcDe(Slot personaje)
    {
        if (personaje == Activo) return null;
        NPCPartyMember npc;
        if (personaje == Slot.Will)
        {
            var piloto = PilotoDe(Slot.Will);
            npc = piloto != null ? piloto.GetComponent<NPCPartyMember>() : null;
        }
        else npc = MiembroDe(personaje);
        if (npc == null || RepresentadoPorElJugador(npc)) return null;
        return npc;
    }

    /// Ese compañero lo lleva ahora el jugador (es el cuerpo al mando). Quien vaya a mover,
    /// despertar o poner a seguir a un compañero lo pregunta antes.
    public static bool RepresentadoPorElJugador(NPCPartyMember npc)
    {
        var piloto = PilotoDelCuerpo.De(npc);
        return piloto != null && piloto.LoLlevaElJugador;
    }

    /// Qué personaje del grupo pone ese compañero (Will siempre que sea su cuerpo; Estela o Liam si
    /// son el miembro del grupo). False si no es de ninguno de los tres.
    public static bool PersonajeDelNpc(NPCPartyMember npc, out Slot personaje)
    {
        personaje = Slot.Will;
        if (npc == null) return false;
        var piloto = PilotoDelCuerpo.De(npc);
        if (piloto == null || !PersonajeDelCuerpo(piloto, out personaje)) return false;
        return personaje == Slot.Will || MiembroDe(personaje) == npc;
    }

    /// Qué personaje es un cuerpo del grupo (por la ficha de su componente `Personaje`).
    public static bool PersonajeDe(GameObject cuerpo, out Slot personaje)
    {
        personaje = Slot.Will;
        if (cuerpo == null) return false;
        var ficha = cuerpo.GetComponentInParent<Personaje>();
        if (ficha == null || ficha.Ficha == null) return false;
        personaje = ficha.Ficha.Personaje;
        return true;
    }

    private static bool PersonajeDelCuerpo(PilotoDelCuerpo piloto, out Slot personaje)
    {
        personaje = Slot.Will;
        var ficha = piloto.GetComponent<Personaje>();
        if (ficha == null || ficha.Ficha == null) return false;
        personaje = ficha.Ficha.Personaje;
        return true;
    }

    // ─── Cambiar de personaje ─────────────────────────────────────────────

    /// Pasa el mando a otro personaje aunque el cambio esté bloqueado por el modo de acción
    /// (lo usa el relevo al caer el que va al mando). Respeta los momentos «solo Will».
    public static bool PasarElMandoA(Slot personaje)
    {
        var pcm = PartyControlManager.Instance;
        if (pcm == null || pcm.IsSwitchingLocked) return false;
        if (personaje == pcm.ActiveSlot) return true;
        if (!EnElGrupo(personaje) || PilotoDe(personaje) == null) return false;
        pcm.ForceSwitch(personaje);
        return pcm.ActiveSlot == personaje;
    }

    /// Se puede pasar el mando a ese personaje: tiene cuerpo en escena.
    public static bool TieneCuerpo(Slot personaje) => PilotoDe(personaje) != null;

    /// Cambia de piloto: el cuerpo de <paramref name="de"/> pasa a la IA y el de
    /// <paramref name="a"/> toma el mando, se registra como jugador y recibe la cámara. Lo llama
    /// PartyControlManager con el personaje activo ya cambiado. False si <paramref name="a"/> no
    /// tiene cuerpo en escena.
    public static bool CambiarElMando(Slot de, Slot a)
    {
        var entra = PilotoDe(a);
        if (entra == null) return false;
        var sale = de != a ? PilotoDe(de) : null;

        sale?.DarALaIA();
        entra.TomarElMando();
        PlayerService.RegisterPlayer(entra.gameObject);
        DarLaCamara(entra.transform);

        if (sale != null)
        {
            SoltarInvitadosDe(de, entra);
            DevolverALaIA(sale.GetComponent<NPCPartyMember>());
        }
        return true;
    }

    /// Distancia a partir de la cual el relevo no pone al que sale en el sitio del que entra, sino
    /// detrás del jugador (INC-1369).
    private const float DistanciaMaximaParaRelevo = 6f;
    private const float DistanciaDetrasDelJugador = 1.5f;

    /// Cambio de personaje con el grupo en «Siguiendo» y sin pareja (INC-1369): el que entra aparece
    /// donde estaba el jugador, con su mismo giro y velocidad, y el cuerpo que deja el jugador pasa a
    /// la IA justo donde estaba el que entra. Si el que entra estaba a más de 6 m, el que sale queda
    /// 1,5 m detrás del jugador, sobre la malla de navegación.
    public static bool CambiarElMandoEnElSitio(Slot de, Slot a)
    {
        var entra = PilotoDe(a);
        if (entra == null) return false;
        var sale = de != a ? PilotoDe(de) : null;
        if (sale == null) return CambiarElMando(de, a);

        // Las poses y la velocidad se leen primero: al pasar a la IA el cuerpo que sale se congela.
        var posSale = sale.transform.position;
        var rotSale = sale.transform.rotation;
        var posEntra = entra.transform.position;
        var rotEntra = entra.transform.rotation;
        var rbSale = sale.GetComponent<Rigidbody>();
        var velocidad = rbSale != null && !rbSale.isKinematic ? rbSale.linearVelocity : Vector3.zero;

        CambiarElMando(de, a);

        // El que entra toma la pose y la velocidad del jugador.
        Colocar(entra.gameObject, posSale, rotSale);
        var rbEntra = entra.GetComponent<Rigidbody>();
        if (rbEntra != null && !rbEntra.isKinematic) rbEntra.linearVelocity = velocidad;

        // El que sale ocupa el sitio del que entra; si estaba lejos, queda detrás del jugador.
        var destino = posEntra;
        var giro = rotEntra;
        if (Vector3.Distance(posEntra, posSale) > DistanciaMaximaParaRelevo)
        {
            giro = rotSale;
            destino = posSale - (rotSale * Vector3.forward) * DistanciaDetrasDelJugador;
            if (NavMesh.SamplePosition(destino, out var enMalla, 3f, NavMesh.AllAreas)) destino = enMalla.position;
        }
        Colocar(sale.gameObject, destino, giro);
        return true;
    }

    /// Pone un cuerpo en esa pose. Si tiene agente de navegación dentro de la malla, lo lleva con
    /// Warp; el Rigidbody se sincroniza para que la física no lo devuelva a su sitio.
    private static void Colocar(GameObject cuerpo, Vector3 pos, Quaternion rot)
    {
        var agente = cuerpo.GetComponent<NavMeshAgent>();
        if (agente != null && agente.isActiveAndEnabled && agente.isOnNavMesh) agente.Warp(pos);
        cuerpo.transform.SetPositionAndRotation(pos, rot);
        var rb = cuerpo.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; rb.rotation = rot; }
    }

    /// Al cargar una escena o una partida, cada cuerpo con el piloto que le toca: el del personaje
    /// activo, el jugador; los demás, la IA.
    public static void RepartirPilotos()
    {
        var entra = PilotoDe(Activo);
        var cuerpos = PilotoDelCuerpo.Cuerpos;
        for (int i = 0; i < cuerpos.Count; i++)
        {
            var c = cuerpos[i];
            if (c == null || c == entra || !c.LoLlevaElJugador) continue;
            c.DarALaIA();
            DevolverALaIA(c.GetComponent<NPCPartyMember>());
        }
        if (entra == null) return;

        entra.TomarElMando();
        if (CuerpoDelJugador != entra.gameObject) PlayerService.RegisterPlayer(entra.gameObject);
        DarLaCamara(entra.transform);
    }

    // La cámara del jugador sigue al cuerpo que lleva.
    private static void DarLaCamara(Transform cuerpo)
    {
        if (!ServiceLocator.TryGet(out vThirdPersonCamera camara) || camara == null) return;
        if (camara.currentTarget != cuerpo) camara.SetMainTarget(cuerpo);
    }

    // Un compañero que se unió mientras el jugador llevaba a Estela o a Liam (no del trío) es su
    // compañero: al dejar ese personaje se queda con él en vez de seguir al nuevo.
    private static void SoltarInvitadosDe(Slot de, PilotoDelCuerpo entra)
    {
        if (de == Slot.Will || !PlayerParty.HasInstance) return;
        var party = PlayerParty.Instance;
        var miembros = party.Members;
        for (int i = miembros.Count - 1; i >= 0; i--)
        {
            var m = miembros[i];
            if (m == null || PilotoDelCuerpo.De(m) == entra) continue;
            if (SlotDeNombre(m.DisplayName, out _)) continue;
            if (m._joinedForSlot == de) party.RemoveMember(m);
        }
    }

    // El cuerpo que deja el jugador vuelve a lo que hace el grupo: al combate si lo hay; si no,
    // siguiendo o anclado según el modo del grupo.
    private static void DevolverALaIA(NPCPartyMember npc)
    {
        if (npc == null) return;
        var enemigo = EnemigoEnCombate();
        if (enemigo != null) npc.OnPlayerEnteredCombat(enemigo);
        else PartyControlManager.PonerEnModoDelGrupo(npc);
    }

    private static Transform EnemigoEnCombate()
    {
        foreach (var go in ActiveCombatRegistry.GetAllInCombat())
        {
            if (go == null || go.GetComponent<NPCPartyMember>() != null) continue;
            return go.transform;
        }
        return null;
    }

    // ─── Nombres ──────────────────────────────────────────────────────────

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
