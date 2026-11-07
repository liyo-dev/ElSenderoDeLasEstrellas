using UnityEngine;

/// <summary>
/// Lo que juega el jugador en una ronda de un <see cref="ConcursoPorRondas"/>: una estrategia por
/// tipo de ronda (<see cref="EquilibrioEnRonda"/>, <see cref="RitmoEnRonda"/>,
/// <see cref="MantenerEnRonda"/>). El concurso le da una zona de la pantalla donde pintarse, la
/// avanza cada fotograma hasta que acaba y luego le pregunta lo bien que ha salido.
/// </summary>
public interface IPruebaDeRonda
{
    /// <summary>Texto que enseña los controles de la ronda (ya traducido).</summary>
    string Instruccion { get; }

    /// <summary>Empieza: se pinta dentro de <paramref name="zona"/> con el estilo de los menús.</summary>
    void Empezar(RectTransform zona, EstiloDeMenuSencillo estilo);

    /// <summary>Avanza la ronda. True cuando ha terminado.</summary>
    bool Actualizar(float dt);

    /// <summary>Lo bien que ha salido, de 0 a 1.</summary>
    float Rendimiento { get; }

    /// <summary>Se acaba (terminada o cortada): quita lo que pintó.</summary>
    void Terminar();
}
