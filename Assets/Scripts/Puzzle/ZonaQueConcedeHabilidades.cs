using UnityEngine;

/// <summary>
/// Zona (trigger) donde el jugador puede usar ciertas habilidades aunque su preset todavía no las
/// tenga: trepar por la pared de una prueba de escalada, por ejemplo. Usa los permisos externos de
/// <see cref="PlayerActionManager"/> (los bloqueos externos siguen mandando), no toca el preset ni
/// la partida guardada, y se retira sola al salir o al desactivarse. Pareja de
/// <see cref="ZonaQueBloqueaHabilidades"/>.
/// </summary>
public sealed class ZonaQueConcedeHabilidades : ZonaDelJugador
{
    [SerializeField] private PlayerAbility[] habilidadesConcedidas = { PlayerAbility.Climb };

    protected override void AlEntrar(PlayerActionManager acciones)
    {
        for (int i = 0; i < habilidadesConcedidas.Length; i++)
            acciones.AnadirPermiso(habilidadesConcedidas[i], this);
    }

    protected override void AlSalir(PlayerActionManager acciones)
    {
        for (int i = 0; i < habilidadesConcedidas.Length; i++)
            acciones.QuitarPermiso(habilidadesConcedidas[i], this);
    }
}
