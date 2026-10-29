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
///    tres); aquí no se escriben. El ataque y la defensa se guardan en el preset (ataque, defensa)
///    y de momento los comparten los tres.
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
            if (p == null) { var ini = Iniciales; return new Estadisticas(0f, 0f, ini.ataque, ini.defensa); }
            AsegurarAtaqueYDefensa(p);
            // La vida y la magia del que va al mando, de SaludDelGrupo; sin grupo en la escena, las
            // de Will guardadas en el preset.
            float vida = p.maxHP, magia = p.maxMP;
            var grupo = SaludDelGrupo.Instance;
            if (grupo != null && grupo.EstaListo)
            {
                var activo = ControlDelJugador.Activo;
                vida = grupo.VidaMaxBase(activo);
                magia = grupo.ManaMaxBase(activo);
            }
            return new Estadisticas(vida, magia, p.ataque, p.defensa);
        }
    }

    public static Estadisticas Total => Base + Bonos;

    /// Calcula la sustitución de una prenda sin cambiar el equipo ni las fuentes registradas.
    public static Estadisticas TotalSiSeEquipa(WardrobeItemSO pieza)
    {
        var total = Total;
        if (pieza == null) return total;
        var actuales = PlayerService.TryGetComponent(out ModularAutoBuilder constructor)
            ? constructor.BonosDeCategoria(pieza.Category) : default;
        return total - actuales + pieza.Bonos;
    }

    /// Suma a la base (un premio de combate, un don de Sintonía): el ataque y la defensa aquí, la
    /// vida y la magia a los tres personajes por SaludDelGrupo. Devuelve el total de antes y el de
    /// después del que va al mando, para contarlo en el informe.
    public static (Estadisticas antes, Estadisticas despues) Sumar(Estadisticas delta)
    {
        var p = UnlockService.GetActivePreset();
        if (p == null) return (default, default);

        var antes = Total;
        p.ataque += delta.ataque;
        p.defensa += delta.defensa;
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

    /// Con lo que empieza el personaje que lleva el jugador: su ficha, o los valores por defecto si
    /// el cuerpo no tiene.
    public static Estadisticas Iniciales =>
        PlayerService.TryGetComponent(out Personaje personaje, allowSceneLookup: false) && personaje.Ficha != null
            ? personaje.Ficha.EstadisticasIniciales
            : new Estadisticas(0f, 0f, AtaqueInicial, DefensaInicial);

    // El ataque y la defensa sin inicializar (0: partidas de antes de INC-470) toman los de la ficha.
    private static void AsegurarAtaqueYDefensa(PlayerPresetSO p)
    {
        if (p.ataque > 0f && p.defensa > 0f) return;
        var ini = Iniciales;
        if (p.ataque <= 0f) p.ataque = ini.ataque;
        if (p.defensa <= 0f) p.defensa = ini.defensa;
    }
}
