/// <summary>
/// Quien pone voz a los personajes con nombre que acompañan una prueba (en el juego, el grupo:
/// <c>GrupoEnLaPrueba</c>). La prueba le pide una frase por el nombre del hablante sin saber quién
/// es ni dónde está; va en el mismo objeto que la prueba.
/// </summary>
public interface IHablantesDePrueba
{
    /// <summary>Dice la frase si conoce a ese hablante; si no, <see cref="LineaDicha.Nada"/>.</summary>
    LineaDicha Decir(string hablante, string clave);
}
