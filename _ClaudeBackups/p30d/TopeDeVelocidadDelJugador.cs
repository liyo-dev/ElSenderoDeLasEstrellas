using System.Collections.Generic;
using Invector.vCharacterController;
using UnityEngine;

/// Tope de velocidad del jugador pedido por otros sistemas (p. ej. al seguir a un NPC, para no
/// adelantarle). Cada sistema pone y quita su propio tope; manda el más bajo de los que haya.
/// Lo aplica el motor del jugador (vThirdPersonMotor.topeDeVelocidad), que también ajusta la
/// animación de andar a esa velocidad. Ver INC-536.
public static class TopeDeVelocidadDelJugador
{
    private static readonly Dictionary<object, float> Topes = new();
    private static vThirdPersonMotor _motor;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Topes.Clear();
        _motor = null;
        PlayerService.OnPlayerRegistered -= AlCambiarJugador;
    }
#endif

    /// Pone (o cambia) el tope de 'quien', en m/s.
    public static void Poner(object quien, float metrosPorSegundo)
    {
        if (quien == null) return;
        if (Topes.Count == 0) PlayerService.OnPlayerRegistered += AlCambiarJugador;
        Topes[quien] = Mathf.Max(0.1f, metrosPorSegundo);
        Aplicar();
    }

    /// Quita el tope de 'quien'. No hace nada si no tenía.
    public static void Quitar(object quien)
    {
        if (quien == null || !Topes.Remove(quien)) return;
        if (Topes.Count == 0) PlayerService.OnPlayerRegistered -= AlCambiarJugador;
        Aplicar();
    }

    private static void AlCambiarJugador(GameObject _) => Aplicar();

    private static void Aplicar()
    {
        float tope = float.PositiveInfinity;
        foreach (var v in Topes.Values) tope = Mathf.Min(tope, v);

        PlayerService.TryGetComponent<vThirdPersonMotor>(out var motor);
        if (_motor != null && _motor != motor) _motor.topeDeVelocidad = float.PositiveInfinity;
        _motor = motor;
        if (_motor != null) _motor.topeDeVelocidad = tope;
    }
}
