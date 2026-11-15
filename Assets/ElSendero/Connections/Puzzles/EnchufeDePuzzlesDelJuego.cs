using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Une el kit de puzles rítmicos con el juego: quién pesa en un plato y a quién alcanza una marea.
/// El kit no sabe nada de personajes ni de quién lleva el mando; eso se lo dice este enchufe.
/// Ver TDD § 31.
/// </summary>
public static class EnchufeDePuzzlesDelJuego
{
    /// <summary>
    /// Un cuerpo del grupo pesa mientras esté encima, lo lleve el jugador o la IA: se cuenta por el
    /// piloto del cuerpo, no por la etiqueta del collider, que cambia al cambiar de personaje.
    /// </summary>
    private static Transform CuerpoQuePesa(Collider col)
    {
        var piloto = PilotoDelCuerpo.De(col);
        return piloto != null ? piloto.transform : null;
    }

    private static bool EsElJugador(Collider col) => ControlDelJugador.EsElJugador(col);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Enchufar()
    {
        WeightPlate.ResolveWeightBody = CuerpoQuePesa;
        PulseTide.IsTarget = EsElJugador;
    }
}
