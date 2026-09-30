using UnityEngine;

/// Marca un cuerpo como uno de los personajes del grupo y dice cuál es (su ficha). Lo lleva el
/// cuerpo de cada personaje, lo mueva el jugador o la IA. Ver INC-483.
[DisallowMultipleComponent]
public sealed class Personaje : MonoBehaviour
{
    [SerializeField] private FichaDePersonaje ficha;

    public FichaDePersonaje Ficha => ficha;
}
