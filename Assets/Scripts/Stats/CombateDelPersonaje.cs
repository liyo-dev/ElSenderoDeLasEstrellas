using UnityEngine;

/// Lo que las estadísticas del personaje hacen en combate: su ataque multiplica el daño de sus
/// hechizos (IPoderDeAtaque) y su defensa reduce el daño que recibe (IFiltroDeDano, lo consulta
/// PlayerHealthSystem). EstadisticasDelPersonaje lo pone en el jugador al registrarse. Ver INC-470.
[DisallowMultipleComponent]
public sealed class CombateDelPersonaje : MonoBehaviour, IPoderDeAtaque, IFiltroDeDano
{
    public float Aplicar(float danoBase) =>
        FormulasDeCombate.DanoConAtaque(danoBase, EstadisticasDelPersonaje.Total.ataque);

    public float Filtrar(float cantidad, GameObject instigador) =>
        FormulasDeCombate.DanoTrasDefensa(cantidad, EstadisticasDelPersonaje.Total.defensa);
}
