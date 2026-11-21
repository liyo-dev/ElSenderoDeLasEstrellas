using Kits.Core;
using UnityEngine;

/// <summary>
/// Orden de prioridad de las poses de medio cuerpo (capa UpperBody) en El Sendero, sobre
/// <see cref="LayerClaims"/>. Llevar algo, interactuar, combatir y las cinemáticas mandan sobre las
/// poses de ambiente (frío, viento, lluvia, gestos de reposo): quien pone una pose que manda la
/// reclama con <see cref="Reclamar"/> y la suelta con <see cref="Soltar"/>; las poses de ambiente
/// preguntan <see cref="AmbientePermitido"/> (los gestos, <see cref="CapaLibrePara"/>) y, si no, se retiran con su fundido sin tocar la capa,
/// y vuelven cuando queda libre. Vale igual para el jugador, los compañeros y los NPCs.
/// </summary>
public static class PosesDeMedioCuerpo
{
    public const string NombreDeLaCapa = "UpperBody";

    /// Poses de ambiente: frío, viento, lluvia.
    public const int Ambiente = 0;
    /// Gestos de reposo y numeritos sueltos.
    public const int GestoDeReposo = 10;
    /// Interactuar con algo (comer, beber, usar un punto del mundo).
    public const int Interaccion = 100;
    /// Llevar algo en brazos (cajas, un compañero).
    public const int Llevar = 200;
    /// Pose de combate y hechizos.
    public const int Combate = 300;
    /// Cinemáticas.
    public const int Cinematica = 400;

    /// Índice de la capa de medio cuerpo de este Animator (-1 si no tiene).
    public static int Capa(Animator animador)
        => animador != null ? animador.GetLayerIndex(NombreDeLaCapa) : -1;

    /// <paramref name="dueño"/> coge la capa de medio cuerpo de <paramref name="animador"/> con esa prioridad.
    public static void Reclamar(Animator animador, object dueño, int prioridad)
    {
        int capa = Capa(animador);
        if (capa > 0) LayerClaims.Claim(animador, capa, dueño, prioridad);
    }

    /// <paramref name="dueño"/> suelta la capa de medio cuerpo de <paramref name="animador"/>.
    public static void Soltar(Animator animador, object dueño)
    {
        if (animador == null) { LayerClaims.ReleaseAll(dueño); return; }
        int capa = Capa(animador);
        if (capa > 0) LayerClaims.Release(animador, capa, dueño);
    }

    /// True si nadie tiene cogida la capa <paramref name="capa"/> por encima de <paramref name="prioridad"/>.
    public static bool CapaLibrePara(Animator animador, int capa, int prioridad)
        => animador != null && capa > 0 && !LayerClaims.IsHeldAbove(animador, capa, prioridad);

    /// True si una pose de ambiente puede usar ahora la capa de medio cuerpo: nadie la tiene cogida
    /// por encima y no hay combate ni cinemática en curso.
    public static bool AmbientePermitido(Animator animador, int capa)
        => CapaLibrePara(animador, capa, Ambiente)
           && !GameContext.Has(Situaciones.Combate) && !GameContext.Has(Situaciones.Cinematica);
}
