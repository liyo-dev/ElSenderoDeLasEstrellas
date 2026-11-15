using System;

/// <summary>
/// Las habilidades que se desbloquean, una por cada campo de <see cref="PlayerAbilities"/>. Los
/// números van fijos porque se guardan en los grafos y en los assets (el 2 era escalar, retirado).
/// </summary>
public enum AbilityKey
{
    Swim = 0,
    Jump = 1,
    Magic = 3,
    Fly = 4,
    Sprint = 5,
    Shield = 6
}
