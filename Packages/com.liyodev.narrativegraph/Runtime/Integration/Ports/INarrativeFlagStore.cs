using System.Collections.Generic;

/// <summary>
/// Dónde guarda el juego los flags narrativos (hechos persistentes de la historia que se
/// consultan, como «la puerta ya está abierta»). Los nodos no lo usan directamente: pasan por
/// <see cref="NarrativeFlags"/>, que avisa de los cambios.
/// </summary>
public interface INarrativeFlagStore
{
    /// <summary>false si ahora no hay partida donde guardar (p. ej. Play sin la escena de arranque).</summary>
    bool IsAvailable { get; }

    bool Has(string flag);

    void Set(string flag, bool value);

    /// <summary>Todos los flags activos, sin prefijos de almacenamiento.</summary>
    IEnumerable<string> All();
}
