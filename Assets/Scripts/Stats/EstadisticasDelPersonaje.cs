using System;
using System.Collections.Generic;
using UnityEngine;

/// Las estadísticas del personaje que lleva el jugador: vida, magia, ataque y defensa (INC-470).
///
///  - Inicial: con lo que empieza el personaje, de su ficha (FichaDePersonaje, en el componente
///    Personaje del cuerpo). Ver INC-483.
///  - Base: lo que tiene por sí mismo. Sube al ganar combates (PasoPremioDeEstadisticas) y con la
///    Sintonía Estelar, siempre con <see cref="Sumar"/>. La vida y la magia son de cada personaje y
///    las lleva SaludDelGrupo (único dueño: <see cref="Sumar"/> le pasa lo que suben, para los
///    tres); aquí no se escriben. El ataque y la defensa son de cada personaje: los de Will en el
///    preset (ataque, defensa), los de Estela y Liam en su entrada de companionHealth; sin guardar,
///    los de su ficha (cada uno el suyo: no hay base compartida).
///  - Bonos: lo que suman las fuentes registradas (IFuenteDeBonos: la ropa del cuerpo del jugador).
///    No se guardan; las vuelve a registrar quien las da. SaludDelGrupo escucha
///    <see cref="Cambiadas"/> para sumar los de vida y magia al personaje que va al mando.
///  - Total = Base + Bonos. Es lo que se usa en juego: vida y magia máximas, daño de los
///    hechizos (ataque, ver CombateDelPersonaje) y daño recibido (defensa).
public static class EstadisticasDelPersonaje
{
    /// Valores de partida cuando el cuerpo no tiene ficha (escenas de prueba sin Personaje).
    public const float AtaqueInicial = FormulasDeCombate.AtaqueDeReferencia;
    public const float DefensaInicial = 5f;

    private static readonly List<IFuenteDeBonos> _fuentes = new();

    /// Ha cambiado la base o algún bono.
    public static event Action Cambiadas;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _fuentes.Clear();
        Cambiadas = null;
    }
#endif

    // El jugador lleva CombateDelPersonaje (su ataque y su defensa en combate) desde que se registra.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
        if (PlayerService.HasInstance && PlayerService.Player != null) AlRegistrarJugador(PlayerService.Player);
    }

    private static void AlRegistrarJugador(GameObject jugador)
    {
        if (jugador != null && jugador.GetComponent<CombateDelPersonaje>() == null)
            jugador.AddComponent<CombateDelPersonaje>();
    }

    public static Estadisticas Bonos
    {
        get
        {
            var suma = default(Estadisticas);
            for (int i = 0; i < _fuentes.Count; i++)
                if (_fuentes[i] != null) suma += _fuentes[i].Bonos;
            return suma;
        }
    }

    public static Estadisticas Base
    {
        get
        {
            var p = GameBootService.IsAvailable ? UnlockService.GetActivePreset() : null;
            // Sin partida cargada (escenas de prueba, primeros frames): valores iniciales, para
            // que los hechizos no hagan 0 de daño.
            var activo = ControlDelJugador.Activo;
            if (p == null) { var ini = InicialesDe(activo); return new Estadisticas(0f, 0f, ini.ataque, ini.defensa); }
            var (ataque, defensa) = AtaqueYDefensa(p, activo);
            // La vida y la magia del que va al mando, de SaludDelGrupo; sin grupo en la escena, las
            // de Will guardadas en el preset.
            float vida = p.maxHP, magia = p.maxMP;
            var grupo = SaludDelGrupo.Instance;
            if (grupo != null && grupo.EstaListo)
            {
                vida = grupo.VidaMaxBase(activo);
                magia = grupo.ManaMaxBase(activo);
            }
            return new Estadisticas(vida, magia, ataque, defensa);
        }
    }

    public static Estadisticas Total => Base + Bonos;

    /// Suma a la base de los tres personajes (un premio de combate, un don de Sintonía): el ataque
    /// y la defensa aquí, la vida y la magia por SaludDelGrupo. Devuelve el total de antes y el de
    /// después del que va al mando, para contarlo en el informe.
    public static (Estadisticas antes, Estadisticas despues) Sumar(Estadisticas delta)
    {
        var p = UnlockService.GetActivePreset();
        if (p == null) return (default, default);

        var antes = Total;
        if (delta.ataque != 0f || delta.defensa != 0f)
            for (int i = 0; i < ControlDelJugador.Personajes; i++)
            {
                var s = (PartyControlManager.CharacterSlot)i;
                var (ataque, defensa) = AtaqueYDefensa(p, s);
                PonerAtaqueYDefensa(p, s, ataque + delta.ataque, defensa + delta.defensa);
            }
        if (delta.vida != 0f || delta.magia != 0f)
        {
            var grupo = SaludDelGrupo.Instance;
            bool hecho = grupo != null && grupo.AumentarMaximosDelGrupo(delta.vida, delta.magia);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (!hecho) Debug.LogWarning($"[EstadisticasDelPersonaje] Sin SaludDelGrupo lista en la escena: no se suben vida {delta.vida} ni magia {delta.magia}.");
#endif
        }
        var despues = Total;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[EstadisticasDelPersonaje] +({delta}): {antes} → {despues}");
#endif
        Cambiadas?.Invoke();
        return (antes, despues);
    }

    // El ataque y la defensa se leen al vuelo (Total). La vida y la magia máximas las mueve
    // SaludDelGrupo al oír Cambiadas.
    public static void RegistrarFuente(IFuenteDeBonos fuente)
    {
        if (fuente == null || _fuentes.Contains(fuente)) return;
        _fuentes.Add(fuente);
        Cambiadas?.Invoke();
    }

    public static void QuitarFuente(IFuenteDeBonos fuente)
    {
        if (fuente == null || !_fuentes.Remove(fuente)) return;
        Cambiadas?.Invoke();
    }

    /// Una fuente ya registrada ha cambiado lo que suma (se cambia de pieza): llamar a esto justo
    /// después del cambio; quien escucha vuelve a leer <see cref="Bonos"/>.
    public static void AvisarCambioDeBonos() => Cambiadas?.Invoke();

    /// Con lo que empieza el personaje que lleva el jugador.
    public static Estadisticas Iniciales => InicialesDe(ControlDelJugador.Activo);

    /// Con lo que empieza ese personaje: la ficha de su cuerpo (o la del proyecto si no está en
    /// escena), o los valores por defecto si no la hay.
    public static Estadisticas InicialesDe(PartyControlManager.CharacterSlot personaje)
    {
        var ficha = FichaDePersonaje.De(personaje);
        return ficha != null ? ficha.EstadisticasIniciales : new Estadisticas(0f, 0f, AtaqueInicial, DefensaInicial);
    }

    /// El ataque y la defensa de base de ese personaje. Will, los del preset (ataque, defensa);
    /// Estela y Liam, los de su entrada. Lo que no esté guardado (a 0) es el de su ficha.
    public static (float ataque, float defensa) AtaqueYDefensa(PlayerPresetSO p, PartyControlManager.CharacterSlot personaje)
    {
        float ataque, defensa;
        if (personaje == PartyControlManager.CharacterSlot.Will) { ataque = p.ataque; defensa = p.defensa; }
        else
        {
            var e = p.EstadoDe(personaje, crear: false);
            ataque = e != null ? e.ataque : 0f;
            defensa = e != null ? e.defensa : 0f;
        }
        if (ataque > 0f && defensa > 0f) return (ataque, defensa);
        var ini = InicialesDe(personaje);
        return (ataque > 0f ? ataque : ini.ataque, defensa > 0f ? defensa : ini.defensa);
    }

    private static void PonerAtaqueYDefensa(PlayerPresetSO p, PartyControlManager.CharacterSlot personaje, float ataque, float defensa)
    {
        if (personaje == PartyControlManager.CharacterSlot.Will)
        {
            p.ataque = ataque;
            p.defensa = defensa;
            return;
        }
        var e = p.EstadoDe(personaje, crear: true);
        e.ataque = ataque;
        e.defensa = defensa;
    }
}
