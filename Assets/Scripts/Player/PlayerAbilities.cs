using UnityEngine;

/// Las habilidades de un personaje (nadar, saltar, magia, volar, esprintar, escudo). Las de Will
/// van en el preset (abilities); las de Estela y Liam salen de su ficha más lo que aprenden en la
/// partida (ver HabilidadesDelPersonaje).
[System.Serializable]
public class PlayerAbilities
{
    [Tooltip("Permite nadar (Swimming)")] public bool swim;
    [Tooltip("Permite saltar")] public bool jump;
    [Tooltip("Permite usar magia (casts)")] public bool magic = false;
    [Tooltip("Permite volar")] public bool fly;
    [Tooltip("Permite usar sprint")] public bool sprint;
    [Tooltip("Permite usar el escudo de proteccion contra magia enemiga")] public bool shield;

    /// <summary>true si la habilidad está desbloqueada.</summary>
    public bool Tiene(AbilityKey habilidad)
    {
        switch (habilidad)
        {
            case AbilityKey.Swim: return swim;
            case AbilityKey.Jump: return jump;
            case AbilityKey.Magic: return magic;
            case AbilityKey.Fly: return fly;
            case AbilityKey.Sprint: return sprint;
            case AbilityKey.Shield: return shield;
            default: return false;
        }
    }

    public PlayerAbilities Copia() => (PlayerAbilities)MemberwiseClone();

    /// <summary>Añade las que tiene <paramref name="otras"/> (no quita ninguna).</summary>
    public void Sumar(PlayerAbilities otras)
    {
        if (otras == null) return;
        swim |= otras.swim; jump |= otras.jump; magic |= otras.magic;
        fly |= otras.fly; sprint |= otras.sprint; shield |= otras.shield;
    }

    /// <summary>Activa o desactiva una habilidad.</summary>
    public void Poner(AbilityKey habilidad, bool valor)
    {
        switch (habilidad)
        {
            case AbilityKey.Swim: swim = valor; break;
            case AbilityKey.Jump: jump = valor; break;
            case AbilityKey.Magic: magic = valor; break;
            case AbilityKey.Fly: fly = valor; break;
            case AbilityKey.Sprint: sprint = valor; break;
            case AbilityKey.Shield: shield = valor; break;
        }
    }
}

