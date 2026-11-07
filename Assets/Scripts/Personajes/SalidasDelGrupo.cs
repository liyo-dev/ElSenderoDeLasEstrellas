using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Sacar a un compañero del grupo un rato por historia (se lo llevan, se queda esperando en otro
/// sitio) y devolverlo después. Sale del equipo (<see cref="NPCPartyMember.LeaveParty"/>: no se le
/// puede llevar, no sigue al jugador) y, si se pide, se retira de la escena hasta que vuelva; al
/// volver se reactiva y se une otra vez. Si iba al mando, el mando pasa antes a Will. Will no sale
/// nunca del grupo. Lo usa el nodo del grafo <c>GrupoNode</c>.
/// </summary>
public static class SalidasDelGrupo
{
    private static readonly Dictionary<Slot, NPCPartyMember> s_fuera = new Dictionary<Slot, NPCPartyMember>();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_fuera.Clear();
#endif

    /// <summary>El personaje está fuera del grupo por <see cref="Sacar"/>.</summary>
    public static bool EstaFuera(Slot personaje) => s_fuera.ContainsKey(personaje);

    /// <summary>
    /// Lo saca del grupo. <paramref name="retirarDeLaEscena"/>: además deja de estar en la escena
    /// (inactivo) hasta que vuelva. False si ahora no se puede (es Will, no se le puede quitar el mando).
    /// </summary>
    public static bool Sacar(Slot personaje, bool retirarDeLaEscena)
    {
        if (personaje == Slot.Will) return false;
        if (ControlDelJugador.Activo == personaje && !ControlDelJugador.PasarElMandoA(Slot.Will)) return false;
        var miembro = ControlDelJugador.MiembroDe(personaje);
        if (miembro == null) return s_fuera.ContainsKey(personaje);
        // Recién devuelto el mando, el NPC puede seguir oculto por el relevo: se espera.
        if (ControlDelJugador.RepresentadoPorElJugador(miembro)) return false;

        miembro.LeaveParty();
        if (retirarDeLaEscena) miembro.gameObject.SetActive(false);
        s_fuera[personaje] = miembro;
        return true;
    }

    /// <summary>
    /// Lo une al grupo la primera vez: el compañero ya está en la escena (lo pone el roster) pero
    /// todavía no es del grupo. Si estaba fuera por <see cref="Sacar"/>, lo devuelve. False si
    /// ahora no se puede (aún no está en la escena o no se puede unir; se reintenta).
    /// </summary>
    public static bool Unir(Slot personaje)
    {
        if (personaje == Slot.Will || ControlDelJugador.MiembroDe(personaje) != null) return true;
        if (s_fuera.ContainsKey(personaje)) return Devolver(personaje);
        var miembro = FueraDelGrupo(personaje);
        if (miembro == null) return false;
        if (!miembro.gameObject.activeSelf) miembro.gameObject.SetActive(true);
        return miembro.IsInParty || miembro.JoinParty();
    }

    /// <summary>
    /// El compañero de ese personaje que está en la escena sin ser del grupo. Puede haber otros NPCs
    /// con su nombre (los de un interior abierto en aditivo, como la torre de Liam): manda el de su
    /// id de siempre («_LIAM», «_ESTELA»), luego uno de la escena activa y, si no, el primero.
    /// </summary>
    private static NPCPartyMember FueraDelGrupo(Slot personaje)
    {
        string nombre = ControlDelJugador.Nombre(personaje);
        string id = "_" + nombre.ToUpperInvariant();
        var activa = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        NPCPartyMember deLaEscena = null, primero = null;
        foreach (var m in Object.FindObjectsByType<NPCPartyMember>(FindObjectsInactive.Include))
        {
            if (m == null || !string.Equals(m.DisplayName, nombre, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (m.NPCManager != null && m.NPCManager.PersistenceId == id) return m;
            if (deLaEscena == null && m.gameObject.scene == activa) deLaEscena = m;
            if (primero == null) primero = m;
        }
        return deLaEscena ?? primero;
    }

    /// <summary>Lo devuelve al grupo (y a la escena). False si ahora no se puede unir (se reintenta).</summary>
    public static bool Devolver(Slot personaje)
    {
        if (!s_fuera.TryGetValue(personaje, out var miembro) || miembro == null)
        {
            s_fuera.Remove(personaje);
            return true;
        }
        if (!miembro.gameObject.activeSelf) miembro.gameObject.SetActive(true);
        if (!miembro.IsInParty && !miembro.JoinParty()) return false;
        s_fuera.Remove(personaje);
        return true;
    }
}
