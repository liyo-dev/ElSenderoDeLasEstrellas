using UnityEngine;

/// <summary>
/// Utilidad estática para seleccionar el objetivo de combate.
/// El jugador es el objetivo por defecto. Estela y Liam son soporte y nunca reciben daño. Un jefe
/// puede decidir ir un rato a por un aliado que le provoca (SenueloDeCombate, p. ej. el Gólem
/// con sus «dos objetivos»); eso lo gestiona la IA del jefe, no este proveedor.
/// </summary>
public static class CombatTargetProvider
{
    /// <summary>
    /// Devuelve siempre al jugador como objetivo primario.
    /// Los enemigos persiguen y atacan únicamente al jugador en todo momento.
    /// </summary>
    public static Transform GetNearestTarget(Vector3 fromPosition)
    {
        if (PlayerService.TryGetPlayer(out var playerGO) && playerGO != null)
            return playerGO.transform;
        return null;
    }
}
