using UnityEngine;

/// <summary>
/// Zona (trigger) donde el jugador puede usar ciertas habilidades aunque su preset todavía no las
/// tenga: volar en una prueba antes de haberlo aprendido, por ejemplo. Usa los permisos externos de
/// <see cref="PlayerActionManager"/> (los bloqueos externos siguen mandando), no toca el preset ni
/// la partida guardada, y se retira sola al salir o al desactivarse. Pareja de
/// <see cref="ZonaQueBloqueaHabilidades"/>.
/// </summary>
public sealed class ZonaQueConcedeHabilidades : ZonaDelJugador
{
    [SerializeField] private PlayerAbility[] habilidadesConcedidas = System.Array.Empty<PlayerAbility>();

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
