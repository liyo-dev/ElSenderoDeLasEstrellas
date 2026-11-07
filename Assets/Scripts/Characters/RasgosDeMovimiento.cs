using System;
using UnityEngine;

/// <summary>
/// Lo que cambia en la forma de moverse de un personaje por lo que lleva puesto: si puede planear
/// y cuánto rebota de más en lanzadores y trampolines. Lo dan los atuendos de escena
/// (<see cref="AtuendoDeEscena"/>) y lo consultan los sistemas de movimiento
/// (<see cref="PlaneoDelJugador"/>, <see cref="LanzadorDeSalto"/>) con <see cref="AlMando"/>.
/// </summary>
[Serializable]
public struct RasgosDeMovimiento
{
    [Tooltip("Puede planear manteniendo el botón de saltar mientras cae.")]
    public bool planear;

    [Tooltip("Rebote de más en lanzadores y trampolines: 0,5 = sale un 50 % más rápido hacia arriba.")]
    [Min(0f)] public float reboteExtra;

    /// <summary>Por cuánto se multiplica el impulso de un lanzador o un trampolín.</summary>
    public float MultiplicadorDeRebote => 1f + Mathf.Max(0f, reboteExtra);

    /// <summary>Junta dos juegos de rasgos: planea si alguno lo da y los rebotes extra se suman.</summary>
    public static RasgosDeMovimiento Sumar(RasgosDeMovimiento a, RasgosDeMovimiento b) => new RasgosDeMovimiento
    {
        planear = a.planear || b.planear,
        reboteExtra = Mathf.Max(0f, a.reboteExtra) + Mathf.Max(0f, b.reboteExtra)
    };

    /// <summary>Los rasgos del personaje al mando (los que le dan sus atuendos puestos).</summary>
    public static RasgosDeMovimiento AlMando
    {
        get
        {
            var registro = CharacterAppearanceRegistry.Instance;
            return registro != null ? registro.RasgosDe(ControlDelJugador.Activo) : default;
        }
    }
}
