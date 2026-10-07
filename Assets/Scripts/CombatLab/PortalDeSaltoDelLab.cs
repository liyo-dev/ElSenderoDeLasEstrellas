using UnityEngine;

/// Lleva al jugador (y al grupo) a otro punto del laboratorio, con el teletransporte del juego.
public sealed class PortalDeSaltoDelLab : PortalDelLab
{
    [Tooltip("Dónde aparece el jugador.")]
    [SerializeField] private Transform destino;

    protected override void Cruzar(GameObject jugador)
    {
        if (destino != null) ZonaDeJefesDelLab.Llevar(jugador, destino);
    }
}
