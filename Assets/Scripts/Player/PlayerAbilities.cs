// filepath: c:\Users\luarb\dev\unity\Alex\Assets\Scripts\Player\PlayerAbilities.cs
using UnityEngine;

/// Clase simple que representa las abilities del jugador.
/// Separada en su propio archivo para evitar dependencias de clases anidadas entre archivos.
[System.Serializable]
public class PlayerAbilities
{
    [Tooltip("Permite nadar (Swimming)")] public bool swim;
    [Tooltip("Permite saltar")] public bool jump;
    [Tooltip("Permite trepar / escalar")] public bool climb;
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
            case AbilityKey.Climb: return climb;
            case AbilityKey.Magic: return magic;
            case AbilityKey.Fly: return fly;
            case AbilityKey.Sprint: return sprint;
            case AbilityKey.Shield: return shield;
            default: return false;
        }
    }

    /// <summary>Activa o desactiva una habilidad.</summary>
    public void Poner(AbilityKey habilidad, bool valor)
    {
        switch (habilidad)
        {
            case AbilityKey.Swim: swim = valor; break;
            case AbilityKey.Jump: jump = valor; break;
            case AbilityKey.Climb: climb = valor; break;
            case AbilityKey.Magic: magic = valor; break;
            case AbilityKey.Fly: fly = valor; break;
            case AbilityKey.Sprint: sprint = valor; break;
            case AbilityKey.Shield: shield = valor; break;
        }
    }
}

