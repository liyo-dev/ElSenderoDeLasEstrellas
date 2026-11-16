using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Une el kit de puzles rítmicos con el juego: quién pesa en un plato, a quién alcanza una marea,
/// quién queda detrás de una puerta y quién es el jugador en un tramo lateral. El kit no sabe nada
/// de personajes ni de quién lleva el mando; eso se lo dice este enchufe. Ver TDD § 31.
/// </summary>
public static class EnchufeDePuzzlesDelJuego
{
    private static SidePlaneZone _zonaLateral;

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

    private static Transform RaizDelJugador(Collider col)
        => ControlDelJugador.EsElJugador(col) ? ControlDelJugador.CuerpoDelJugador?.transform : null;

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

    private static void AlEntrarEnElLateral(SidePlaneZone zona)
    {
        if (zona == null) return;
        QuitarCamaraLateral();
        _zonaLateral = zona;
        CamaraLateral.Poner(zona);
    }

    private static void AlSalirDelLateral(SidePlaneZone zona)
    {
        if (zona == null || zona != _zonaLateral) return;
        QuitarCamaraLateral();
    }

    private static void QuitarCamaraLateral()
    {
        if (_zonaLateral == null) return;
        var camara = _zonaLateral.GetComponent<CamaraLateral>();
        if (camara != null) Object.Destroy(camara);
        _zonaLateral = null;
    }

    // Después de que los kits hayan limpiado sus estáticos (SubsystemRegistration): si se enchufara
    // antes, el reset del kit se llevaría por delante lo que se acaba de enganchar.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Enchufar()
    {
        WeightPlate.ResolveWeightBody = CuerpoQuePesa;
        PulseTide.IsTarget = EsElJugador;
        BeatGate.QuedaAlguienDetras = QuedaAlguienDetras;
        SidePlaneZone.ResolvePlayerRoot = RaizDelJugador;
        SidePlaneZone.ZoneEntered += AlEntrarEnElLateral;
        SidePlaneZone.ZoneExited += AlSalirDelLateral;
    }
}
