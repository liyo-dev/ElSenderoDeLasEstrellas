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
    /// Hay una conversación abierta en la caja de diálogo (<see cref="DialogueManager"/>).
    public const string Dialogo = "dialogo";

    /// El grupo va en sigilo, siguiendo a alguien sin que le vea (<see cref="SigiloDelGrupo"/>).
    public const string Sigilo = "sigilo";
    /// Se juega una prueba que no para (una huida a ritmo, como el Templo del Coro): no se abren los
    /// menús rápidos, que un toque sin querer en mitad de un cambio de personaje deja vendido al jugador.
    public const string PruebaSinPausa = "prueba_sin_pausa";
}
