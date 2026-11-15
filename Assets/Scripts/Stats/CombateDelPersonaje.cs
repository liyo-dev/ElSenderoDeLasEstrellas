using UnityEngine;

/// Lo que las estadísticas del personaje hacen en combate: su ataque multiplica el daño de sus
/// hechizos (IPoderDeAtaque) y su defensa reduce el daño que recibe (IFiltroDeDano, lo consulta
/// PlayerHealthSystem). EstadisticasDelPersonaje lo pone en el jugador al registrarse. Solo cuenta
/// mientras su cuerpo es el que lleva el jugador: con la IA, el cuerpo sigue sus propias reglas
/// de compañero. Ver INC-470.
[DisallowMultipleComponent]
public sealed class CombateDelPersonaje : MonoBehaviour, IPoderDeAtaque, IFiltroDeDano
{
    public float Aplicar(float danoBase) =>
        ControlDelJugador.EsElJugador(this)
            ? FormulasDeCombate.DanoConAtaque(danoBase, EstadisticasDelPersonaje.Total.ataque)
            : danoBase;

    public float Filtrar(float cantidad, GameObject instigador) =>
        ControlDelJugador.EsElJugador(this)
            ? FormulasDeCombate.DanoTrasDefensa(cantidad, EstadisticasDelPersonaje.Total.defensa)
            : cantidad;
}
