using UnityEngine;

/// Empieza la pelea contra un jefe en la arena de la zona de jefes del laboratorio.
public sealed class PortalDeJefeDelLab : PortalDelLab
{
    [SerializeField] private ZonaDeJefesDelLab zona;
    [Tooltip("El encuentro del juego: enemigo, nombre, radio de la arena, premios y guía.")]
    [SerializeField] private BattleEncounterSO encuentro;
    [Tooltip("Id de la batalla en el juego (Demon_1, Golem_1…): decide la música.")]
    [SerializeField] private string idDeBatalla;

    protected override void Cruzar(GameObject jugador)
    {
        if (zona != null) zona.Empezar(encuentro, idDeBatalla);
    }
}
