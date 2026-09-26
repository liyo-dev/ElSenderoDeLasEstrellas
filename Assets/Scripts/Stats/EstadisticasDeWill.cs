using System;
using System.Collections.Generic;
using UnityEngine;

/// Las estadísticas de Will: vida, magia, ataque y defensa (INC-470).
///
///  - Base: lo que Will tiene por sí mismo. Sube al ganar combates (PasoPremioDeEstadisticas) y
///    se guarda con la partida. La vida y la magia de base son su vida y su magia máximas de
///    siempre (PlayerHealthSystem / ManaPool, y maxHP / maxMP en el preset): así todo lo que ya
///    las tocaba (desbloquear la magia, un punto de guardado, cargar partida) sigue igual. El
///    ataque y la defensa se guardan en el preset (ataque, defensa).
///  - Bonos: lo que suman las fuentes registradas (IFuenteDeBonos: equipo...). No se guardan; las
///    vuelve a registrar quien las da.
///  - Total = Base + Bonos. Es lo que se usa en juego: vida y magia máximas, daño de los
///    hechizos (ataque, ver CombateDeWill) y daño recibido (defensa).
public static class EstadisticasDeWill
{
    /// Con lo que empieza Will (también en partidas de antes de este sistema).
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

    // El jugador lleva CombateDeWill (su ataque y su defensa en combate) desde que se registra.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
        if (PlayerService.HasInstance && PlayerService.Player != null) AlRegistrarJugador(PlayerService.Player);
    }

    private static void AlRegistrarJugador(GameObject jugador)
    {
        if (jugador != null && jugador.GetComponent<CombateDeWill>() == null)
            jugador.AddComponent<CombateDeWill>();
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
            if (p == null) return new Estadisticas(0f, 0f, AtaqueInicial, DefensaInicial);
            AsegurarAtaqueYDefensa(p);
            var bonos = Bonos;
            float vida = PlayerService.TryGetComponent(out PlayerHealthSystem salud) ? salud.MaxHealth - bonos.vida : p.maxHP;
            float magia = PlayerService.TryGetComponent(out ManaPool mana) ? mana.Max - bonos.magia : p.maxMP;
            return new Estadisticas(vida, magia, p.ataque, p.defensa);
        }
    }

    public static Estadisticas Total => Base + Bonos;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EstadisticasDeWill] +({delta}): {antes} → {despues}");
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

    private static void AsegurarAtaqueYDefensa(PlayerPresetSO p)
    {
        if (p.ataque <= 0f) p.ataque = AtaqueInicial;
        if (p.defensa <= 0f) p.defensa = DefensaInicial;
    }
}
