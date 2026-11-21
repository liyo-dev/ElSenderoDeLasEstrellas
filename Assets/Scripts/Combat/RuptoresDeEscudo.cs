using UnityEngine;

/// <summary>
/// Qué elemento rompe el escudo de qué elemento. Solo cuentan los escudos elementales de las
/// personas (NPCs); las criaturas no llevan escudo, solo debilidad de daño (AfinidadElemental).
/// Regla: el escudo lo rompe la magia de su mismo elemento, que es también su color (el del
/// hechizo en vuelo). Lo que no rompe, rebota. Ver INC-1357.
/// </summary>
public static class RuptoresDeEscudo
{
    /// <summary>Si un hechizo de 'ataque' rompe un escudo de 'escudo'.</summary>
    public static bool Rompe(MagicElement ataque, MagicElement escudo) => ataque == escudo;

    /// <summary>
    /// Color con el que se tiñe el escudo de ese elemento: el cuerpo de la paleta de sus hechizos,
    /// con Mente (lavanda claro) y Oscuro (ciruela oscuro) bien separados para no confundirlos.
    /// </summary>
    public static Color ColorDe(MagicElement elemento)
    {
        switch (elemento)
        {
            case MagicElement.Storm: return new Color(0.40f, 0.95f, 0.72f, 1f);
            case MagicElement.Fire: return new Color(1.00f, 0.45f, 0.10f, 1f);
            case MagicElement.Light: return new Color(1.00f, 0.82f, 0.25f, 1f);
            case MagicElement.Mind: return new Color(0.70f, 0.60f, 1.00f, 1f);
            default: return new Color(0.42f, 0.06f, 0.36f, 1f);
        }
    }
}
