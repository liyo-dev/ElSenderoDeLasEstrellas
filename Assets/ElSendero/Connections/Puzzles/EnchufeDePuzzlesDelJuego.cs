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

    /// <summary>
    /// ¿Queda alguien del grupo por pasar la puerta? Cuenta a los que están del lado de donde se
    /// viene (el contrario al que se sale) y cerca de ella: un compañero aparcado lejos no la deja
    /// abierta para siempre.
    /// </summary>
    private static bool QuedaAlguienDetras(Vector3 punto, Vector3 salida)
    {
        const float Cerca = 18f;
        int cuantos = DanoEnZona.CuerposDelGrupo(out var cuerpos);
        for (int i = 0; i < cuantos; i++)
        {
            Vector3 desde = cuerpos[i].Posicion - punto;
            if (desde.sqrMagnitude > Cerca * Cerca) continue;
            if (Vector3.Dot(desde, salida) < 0f) return true;
        }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Enchufar()
    {
        WeightPlate.ResolveWeightBody = CuerpoQuePesa;
        PulseTide.IsTarget = EsElJugador;
        BeatGate.QuedaAlguienDetras = QuedaAlguienDetras;
    }
}
