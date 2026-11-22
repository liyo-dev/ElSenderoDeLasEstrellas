using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Une el kit de puzles rítmicos con el juego: quién pesa en un plato, a quién alcanza una marea,
/// quién queda detrás de una puerta, a quién golpea lo que cae al compás, quién es el jugador en un tramo lateral y la presencia que
/// acecha a quien lleva un objeto (<see cref="PresenciaQueAcechaEnElJuego"/>). El kit no sabe nada
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

    /// <summary>Lo que cae al compás (CueDrop) golpea al grupo que pille debajo y lo aparta.</summary>
    // Lo que cae apuntando apunta a quien lleva el jugador.
    private static Vector3? PosicionDelJugador()
    {
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        return cuerpo != null ? cuerpo.transform.position : (Vector3?)null;
    }

    // Por la mezcla del juego (bus de efectos), en 2D: el golpe va con la música, no con la distancia.
    private static void SonidoDeLoQueCae(AudioClip sonido, Vector3 donde, float volumen)
        => AudioService.Instance?.PlaySFX(sonido, volumen);

    // Alto 0: hasta 2,2 m (alcanza aunque se salte); con alto, solo hasta ahí (algo bajo que se salta).
    private static void GolpeDeLoQueCae(Vector3 centro, float radio, float dano, float alto)
        => DanoEnZona.GolpearEnCilindro(centro, radio, alto > 0f ? alto : 2.2f, 0.3f, dano, 5f);

    private static Transform CuerpoDeQuienVaAlMando() => ControlDelJugador.CuerpoDelJugador?.transform;

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
        CueDrop.Strike = GolpeDeLoQueCae;
        CueDrop.PlaySound = SonidoDeLoQueCae;
        CueDrop.TargetPosition = PosicionDelJugador;
        FollowSpot.Target = PosicionDelJugador;
        BeatSway.Rider = CuerpoDeQuienVaAlMando;
        BeatGate.QuedaAlguienDetras = QuedaAlguienDetras;
        SidePlaneZone.ResolvePlayerRoot = RaizDelJugador;
        SidePlaneZone.ZoneEntered += AlEntrarEnElLateral;
        SidePlaneZone.ZoneExited += AlSalirDelLateral;
        PresenciaQueAcechaEnElJuego.Enchufar();
    }
}
