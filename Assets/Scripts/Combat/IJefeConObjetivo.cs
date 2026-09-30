using System;
using UnityEngine;

/// Un jefe que puede cambiar de objetivo (ir a por el jugador o a por un aliado que le provoca).
/// Lo implementa su IA y lo lee la guía de combate para avisar. Ver INC-489.
public interface IJefeConObjetivo
{
    Transform Objetivo { get; }
    event Action<Transform> AlCambiarDeObjetivo;
}
