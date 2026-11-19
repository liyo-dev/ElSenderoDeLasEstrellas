using System.Collections.Generic;
using Game.NPC;
using Kits.WorldWrap;
using Unity.Cinemachine;
using UnityEngine;

/// El mundo cíclico de El Sendero: vigila al jugador que va al mando y, si va en barco, mueve el
/// barco con él (y con el barco, la tripulación sentada en sus puestos). Con él cruzan también los
/// compañeros del grupo que estén cerca (volando, nadando o anclados en el agua), y al cruzar se
/// vacía el rastro que siguen y se avisa a las cámaras de Cinemachine que siguen a lo movido para
/// que no recorran el mapa de un lado a otro. Ver INC-1197, INC-1303
[RequireComponent(typeof(WorldWrapper))]
public class MundoCiclicoDelJuego : MonoBehaviour
{
    [Tooltip("Distancia al jugador (m) por debajo de la cual un compañero del grupo cruza con él.")]
    [SerializeField, Min(0f)] private float radioDelGrupo = 200f;

    private WorldWrapper _mundo;
    private bool _jugadorAvisado;

    private void Awake() => _mundo = GetComponent<WorldWrapper>();

    private void OnEnable()
    {
        _mundo.TargetProvider = Jugador;
        _mundo.MoverResolver = LoQueLeLleva;
        _mundo.PassengersProvider = Companeros;
        WorldWrapper.ObjectShifted += AvisarALasCamaras;
        WorldWrapper.Shifted += AlCruzar;
    }

    private void OnDisable()
    {
        _mundo.TargetProvider = null;
        _mundo.MoverResolver = null;
        _mundo.PassengersProvider = null;
        WorldWrapper.ObjectShifted -= AvisarALasCamaras;
        WorldWrapper.Shifted -= AlCruzar;
    }

    private static Transform Jugador()
        => PlayerService.TryGetPlayer(out var jugador, allowSceneLookup: false) && jugador != null ? jugador.transform : null;

    private static Transform LoQueLeLleva(Transform jugador)
    {
        var barco = jugador.GetComponentInParent<BarcoNavegable>();
        return barco != null ? barco.transform : jugador;
    }

    private void Companeros(Transform jugador, List<Transform> pasajeros)
    {
        float radio2 = radioDelGrupo * radioDelGrupo;
        Vector3 origen = jugador.position;
        if (PlayerParty.HasInstance)
        {
            var miembros = PlayerParty.Instance.Members;
            for (int i = 0; i < miembros.Count; i++)
                AnadirSiCerca(miembros[i], origen, radio2, pasajeros);
        }
        AnadirSiCerca(ControlDelJugador.NpcDe(PartyControlManager.CharacterSlot.Will), origen, radio2, pasajeros);
    }

    private static void AnadirSiCerca(NPCPartyMember miembro, Vector3 origen, float radio2, List<Transform> pasajeros)
    {
        if (miembro == null || ControlDelJugador.RepresentadoPorElJugador(miembro)) return;
        if ((miembro.NPCManager?.IsInCinematic ?? false)) return;
        if ((miembro.transform.position - origen).sqrMagnitude > radio2) return;
        pasajeros.Add(miembro.transform);
    }

    private void AvisarALasCamaras(Transform movido, Vector3 desplazamiento)
    {
        CinemachineCore.OnTargetObjectWarped(movido, desplazamiento);
        if (movido == Jugador()) _jugadorAvisado = true;
    }

    private void AlCruzar(Vector3 desplazamiento)
    {
        // Si ha cruzado dentro de lo que le lleva (el barco), las cámaras que le siguen a él aún no lo saben.
        var jugador = Jugador();
        if (!_jugadorAvisado && jugador != null) CinemachineCore.OnTargetObjectWarped(jugador, desplazamiento);
        _jugadorAvisado = false;

        if (!PlayerParty.HasInstance) return;
        var grupo = PlayerParty.Instance;
        grupo.LimpiarRastro();
        var miembros = grupo.Members;
        for (int i = 0; i < miembros.Count; i++)
            if (miembros[i] != null) miembros[i].ReiniciarRastro();
    }
}
