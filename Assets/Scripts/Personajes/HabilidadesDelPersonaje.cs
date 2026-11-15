using Slot = PartyControlManager.CharacterSlot;

/// Las habilidades de cada personaje del grupo (nadar, saltar, magia, volar, esprintar, escudo),
/// cada uno las suyas (INC-717):
///  - Will: las del preset (abilities), que pone la partida nueva y va ganando con la historia.
///  - Estela y Liam: las de su ficha (FichaDePersonaje.HabilidadesIniciales) más las que aprenden
///    en la partida, guardadas en su entrada de companionHealth (habilidadesAprendidas).
/// Una partida guardada sin habilidades de Estela o Liam les da las de su ficha.
public static class HabilidadesDelPersonaje
{
    /// Las habilidades de ese personaje. Para Will es el objeto del preset; para Estela y Liam, una
    /// copia hecha al vuelo (cambiarla no cambia nada: para eso, <see cref="Aprender"/>).
    public static PlayerAbilities De(PlayerPresetSO preset, Slot personaje)
    {
        if (personaje == Slot.Will)
        {
            if (preset == null) return new PlayerAbilities();
            return preset.abilities ??= new PlayerAbilities();
        }
        var ficha = FichaDePersonaje.De(personaje);
        var habilidades = ficha != null ? ficha.HabilidadesIniciales : FichaDePersonaje.HabilidadesPorDefecto();
        var entrada = preset != null ? preset.EstadoDe(personaje, crear: false) : null;
        if (entrada != null) habilidades.Sumar(entrada.habilidadesAprendidas);
        return habilidades;
    }

    /// Ese personaje aprende la habilidad. Devuelve true si antes no la tenía. A Estela y Liam se
    /// les apunta aunque su ficha ya la traiga: así no depende de que la ficha esté cargada.
    public static bool Aprender(PlayerPresetSO preset, Slot personaje, AbilityKey habilidad)
    {
        if (preset == null) return false;
        bool laTenia = De(preset, personaje).Tiene(habilidad);
        if (personaje == Slot.Will)
        {
            if (!laTenia) preset.abilities.Poner(habilidad, true);
            return !laTenia;
        }
        var entrada = preset.EstadoDe(personaje, crear: true);
        entrada.habilidadesAprendidas ??= new PlayerAbilities();
        entrada.habilidadesAprendidas.Poner(habilidad, true);
        return !laTenia;
    }
}
