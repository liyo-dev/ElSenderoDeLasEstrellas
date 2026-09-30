using System;
using System.Collections.Generic;

/// Un jefe que cambia de forma de pelear según le queda vida. Lo implementa su IA (p. ej.
/// ImpDemonAI) y lo leen quienes tienen que contarlo: la barra de vida marca dónde empieza cada
/// fase y Eldran explica qué ha cambiado.
public interface IJefeConFases
{
    /// Fase actual, empezando en 0.
    int Fase { get; }

    /// Porcentaje de vida (0..1) al que empieza cada fase a partir de la segunda, de mayor a menor.
    IReadOnlyList<float> UmbralesDeFase { get; }

    /// Se dispara al entrar en una fase nueva (con su número).
    event Action<int> AlCambiarDeFase;
}
