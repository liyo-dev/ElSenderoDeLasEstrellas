/// <summary>
/// Las situaciones de juego de El Sendero que se publican en <c>Kits.Core.GameContext</c>.
/// El kit no las conoce: son textos que ponemos aquí. Quien es dueño de una situación la abre y la
/// cierra con su clave; quien cede ante ella la consulta (ver TDD § 32).
/// </summary>
public static class Situaciones
{
    /// Hay al menos un NPC en combate con el jugador (<see cref="ActiveCombatRegistry"/>).
    public const string Combate = "combate";

    /// Hay al menos una cinemática en marcha (<see cref="CinematicSequencerBase"/>).
    public const string Cinematica = "cinematica";
}
