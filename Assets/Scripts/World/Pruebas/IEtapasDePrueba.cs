using System.Collections;

/// <summary>
/// Lo que acompaña a una <see cref="PruebaDelJugador"/> y necesita su momento en cada etapa: colocar
/// a alguien con la pantalla tapada, decir unas frases antes de la salida o al ganar, soltar lo que
/// tenga al acabar. La prueba busca estos componentes en su mismo objeto y espera a cada uno, en el
/// orden en que están, antes de seguir. Así la prueba no sabe nada de quién habla ni de quién se
/// coloca, y lo que acompaña no sabe nada de las reglas de la prueba.
/// </summary>
public interface IEtapasDePrueba
{
    /// <summary>Con la pantalla tapada, antes de cada intento (el jugador ya está en la salida).</summary>
    IEnumerator Colocar(PruebaDelJugador prueba);

    /// <summary>Con la pantalla ya destapada y el jugador quieto en la salida, antes de la cuenta atrás.</summary>
    IEnumerator Presentar(PruebaDelJugador prueba);

    /// <summary>Recién ganada, antes de avisar al resto del juego.</summary>
    IEnumerator Celebrar(PruebaDelJugador prueba);

    /// <summary>Recién perdida, antes del fundido del reintento.</summary>
    IEnumerator Lamentar(PruebaDelJugador prueba);

    /// <summary>La prueba se acaba o se apaga: soltar todo lo que se tenga sujeto.</summary>
    void Soltar(PruebaDelJugador prueba);
}
