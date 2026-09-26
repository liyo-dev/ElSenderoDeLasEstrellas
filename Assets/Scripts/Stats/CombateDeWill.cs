using UnityEngine;

/// Lo que las estadísticas de Will hacen en combate: su ataque multiplica el daño de sus
/// hechizos (IPoderDeAtaque) y su defensa reduce el daño que recibe (IFiltroDeDano, lo consulta
/// PlayerHealthSystem). EstadisticasDeWill lo pone en el jugador al registrarse. Ver INC-470.
[DisallowMultipleComponent]
public sealed class CombateDeWill : MonoBehaviour, IPoderDeAtaque, IFiltroDeDano
{
    public float Aplicar(float danoBase) =>
        FormulasDeCombate.DanoConAtaque(danoBase, EstadisticasDeWill.Total.ataque);

    public float Filtrar(float cantidad, GameObject instigador) =>
        FormulasDeCombate.DanoTrasDefensa(cantidad, EstadisticasDeWill.Total.defensa);
}
