/// <summary>
/// Cómo enseña lo que hace un personaje que mueve otro sistema: un corredor con guion
/// (<see cref="CorredorDeRuta"/>) o un actor de una cinemática sin animador del juego (los del pack
/// Sweet_Land, ver <see cref="SequenceActor.Desplazarse"/>). Cada modelo trae sus propios parámetros
/// de animación; quien lo mueve solo cuenta qué está haciendo.
/// </summary>
public interface IAnimacionDeCorredor
{
    /// <summary>
    /// Velocidad por el suelo, en metros por segundo (0 = parado). Con <paramref name="alInstante"/>
    /// la animación pasa a esa velocidad sin fundido (al colocar o rebobinar, y en el storyboard).
    /// </summary>
    void Avanzar(float metrosPorSegundo, bool alInstante = false);

    /// <summary>Empieza un salto.</summary>
    void Saltar();

    /// <summary>Empieza o deja de trepar.</summary>
    void Trepar(bool trepando);
}
