using UnityEngine;

/// <summary>
/// Zona (trigger) donde ciertas habilidades no se pueden usar mientras el jugador está dentro.
/// Uso principal: zona sin vuelo en las pruebas de plataformeo (si entra volando, el vuelo se
/// corta y cae). Usa los bloqueos externos de <see cref="PlayerActionManager"/>, así que no
/// toca los permisos del preset y se libera sola al salir o al desactivarse.
/// </summary>
public sealed class ZonaQueBloqueaHabilidades : ZonaDelJugador
{
    [SerializeField] private PlayerAbility[] habilidadesBloqueadas = { PlayerAbility.Fly };
    [Tooltip("Aviso al entrar si se estaba usando una habilidad bloqueada (p. ej. volando).")]
    [SerializeField] private string claveDeAviso = "ZONA_SIN_VUELO_AVISO";
    [SerializeField, Min(0.5f)] private float duracionDelAviso = 3f;

    protected override void AlEntrar(PlayerActionManager acciones)
    {
        for (int i = 0; i < habilidadesBloqueadas.Length; i++)
            acciones.AnadirBloqueo(habilidadesBloqueadas[i], this);

        if (System.Array.IndexOf(habilidadesBloqueadas, PlayerAbility.Fly) >= 0)
        {
            var vuelo = acciones.GetComponentInChildren<PlayerFlyingController>();
            if (vuelo != null && vuelo.IsFlying)
            {
                vuelo.TerminarVuelo();
                if (!string.IsNullOrEmpty(claveDeAviso))
                    HudToastService.Instance?.Show(claveDeAviso, duracionDelAviso);
            }
        }
    }

    protected override void AlSalir(PlayerActionManager acciones)
    {
        for (int i = 0; i < habilidadesBloqueadas.Length; i++)
            acciones.QuitarBloqueo(habilidadesBloqueadas[i], this);
    }
}
