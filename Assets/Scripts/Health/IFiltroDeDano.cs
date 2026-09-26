using UnityEngine;

/// Algo que decide, antes de que se aplique, qué pasa con un golpe que recibe un Damageable del
/// mismo GameObject. Damageable los consulta en orden en cada TakeDamage:
///  - devuelve la cantidad (igual o cambiada) → se aplica como daño;
///  - devuelve 0 → el golpe no hace nada;
///  - devuelve un número negativo → en vez de daño, cura ese valor (en positivo).
/// Así cualquier regla de «cuándo se le puede hacer daño» (un jefe que solo es vulnerable con el
/// aro encendido, una armadura, un escudo) vive en su propio componente sin tocar Damageable.
/// Ver INC-469.
public interface IFiltroDeDano
{
    float Filtrar(float cantidad, GameObject instigador);
}

/// Algo que dice si su dueño está expuesto (se le puede hacer daño) ahora mismo. Lo usa
/// SoloDanoCuandoExpuesto. Ejemplo: RuneCollar, expuesto mientras el aro brilla.
public interface IExpuestoAlDano
{
    bool Expuesto { get; }
}
