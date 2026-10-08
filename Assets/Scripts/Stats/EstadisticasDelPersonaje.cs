using System;
using System.Collections.Generic;
using UnityEngine;

/// Las estadísticas del personaje que lleva el jugador: vida, magia, ataque y defensa (INC-470).
///
///  - Inicial: con lo que empieza el personaje, de su ficha (FichaDePersonaje, en el componente
///    Personaje del cuerpo). Ver INC-483.
///  - Base: lo que tiene por sí mismo. Sube al ganar combates (PasoPremioDeEstadisticas) y se
///    guarda con la partida. La vida y la magia de base son la vida y la magia máximas del cuerpo
///    (PlayerHealthSystem / ManaPool, y maxHP / maxMP en el preset); el ataque y la defensa se
///    guardan en el preset (ataque, defensa). Mientras haya un solo cuerpo jugable, el preset
///    guarda una sola base: la comparten los tres personajes.
///  - Bonos: lo que suman las fuentes registradas (IFuenteDeBonos: equipo...). No se guardan; las
///    vuelve a registrar quien las da.
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
            var bonos = Bonos;
            float vida = PlayerService.TryGetComponent(out PlayerHealthSystem salud) ? salud.MaxHealth - bonos.vida : p.maxHP;
            float magia = PlayerService.TryGetComponent(out ManaPool mana) ? mana.Max - bonos.magia : p.maxMP;
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

    /// Suma a la base (un premio de combate, por ejemplo) y lo aplica al jugador. Devuelve el
    /// total de antes y el de después, para contarlo en el informe.
    public static (Estadisticas antes, Estadisticas despues) Sumar(Estadisticas delta)
    {
        var p = UnlockService.GetActivePreset();
        if (p == null) return (default, default);

        var antes = Total;
        p.ataque += delta.ataque;
        p.defensa += delta.defensa;
        CambiarMaximos(p, delta.vida, delta.magia);
        var despues = Total;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[EstadisticasDelPersonaje] +({delta}): {antes} → {despues}");
#endif
        Cambiadas?.Invoke();
        return (antes, despues);
    }

    public static void RegistrarFuente(IFuenteDeBonos fuente)
    {
        if (fuente == null || _fuentes.Contains(fuente)) return;
        _fuentes.Add(fuente);
        AplicarBonosAlJugador(fuente.Bonos);
    }

    public static void QuitarFuente(IFuenteDeBonos fuente)
    {
        if (fuente == null || !_fuentes.Remove(fuente)) return;
        var b = fuente.Bonos;
        AplicarBonosAlJugador(new Estadisticas(-b.vida, -b.magia, -b.ataque, -b.defensa));
    }

    /// Una fuente ya registrada va a cambiar lo que suma (se cambia de pieza): llamar a esto con
    /// la diferencia (nuevo - viejo) justo después del cambio.
    public static void AvisarCambioDeBonos(Estadisticas diferencia) => AplicarBonosAlJugador(diferencia);

    // El ataque y la defensa se leen al vuelo (Total); la vida y la magia máximas son de los
    // componentes del jugador y hay que moverlas. El preset no se toca: guarda solo la base.
    private static void AplicarBonosAlJugador(Estadisticas diferencia)
    {
        if (PlayerService.TryGetComponent(out PlayerHealthSystem salud) && diferencia.vida != 0f)
        {
            salud.SetMaxHealth(Mathf.Max(1f, salud.MaxHealth + diferencia.vida));
            if (diferencia.vida > 0f) salud.Heal(diferencia.vida);
        }
        if (PlayerService.TryGetComponent(out ManaPool mana) && diferencia.magia != 0f)
            mana.Init(Mathf.Max(0f, mana.Max + diferencia.magia), mana.Current + Mathf.Max(0f, diferencia.magia));
        Cambiadas?.Invoke();
    }

    private static void CambiarMaximos(PlayerPresetSO p, float masVida, float masMagia)
    {
        if (PlayerService.TryGetComponent(out PlayerHealthSystem salud))
        {
            if (masVida != 0f)
            {
                salud.SetMaxHealth(Mathf.Max(1f, salud.MaxHealth + masVida));
                if (masVida > 0f) salud.Heal(masVida);   // lo que sube el máximo se gana también de vida
            }
            p.maxHP = salud.MaxHealth - Bonos.vida;
            p.currentHP = salud.CurrentHealth;
        }
        else
        {
            p.maxHP = Mathf.Max(1f, p.maxHP + masVida);
            p.currentHP = Mathf.Clamp(p.currentHP + Mathf.Max(0f, masVida), 0f, p.maxHP);
        }

        if (PlayerService.TryGetComponent(out ManaPool mana))
        {
            if (masMagia != 0f)
                mana.Init(Mathf.Max(0f, mana.Max + masMagia), mana.Current + Mathf.Max(0f, masMagia));
            p.maxMP = mana.Max - Bonos.magia;
            p.currentMP = mana.Current;
        }
        else
        {
            p.maxMP = Mathf.Max(0f, p.maxMP + masMagia);
            p.currentMP = Mathf.Clamp(p.currentMP + Mathf.Max(0f, masMagia), 0f, p.maxMP);
        }
    }

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
