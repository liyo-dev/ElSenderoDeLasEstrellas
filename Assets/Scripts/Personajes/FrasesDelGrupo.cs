using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Frases dichas por un personaje del grupo, lo lleve el jugador o la IA: en bocadillo sobre su
/// cuerpo si está cerca del jugador y, si no, en subtítulo con su nombre (todo por
/// <see cref="PresentadorDeLinea"/>).
/// </summary>
public static class FrasesDelGrupo
{
    /// <summary>Metros al jugador hasta los que la frase sale en bocadillo sobre quien habla.</summary>
    public const float DistanciaParaBocadillo = 14f;

    public static LineaDicha Decir(Slot hablante, string clave, float distanciaParaBocadillo = DistanciaParaBocadillo)
    {
        if (string.IsNullOrEmpty(clave)) return LineaDicha.Nada;
        var cuerpo = ControlDelJugador.CuerpoDe(hablante);
        var jugador = PlayerService.Player;
        bool cerca = cuerpo != null && jugador != null &&
                     (cuerpo.transform.position - jugador.transform.position).sqrMagnitude <= distanciaParaBocadillo * distanciaParaBocadillo;
        return PresentadorDeLinea.Decir(new FraseADecir
        {
            clave = clave,
            ancla = cerca ? cuerpo.transform : null,
            presentacion = cerca ? PresentacionDeTexto.Bocadillo : PresentacionDeTexto.Subtitulo,
            nombreDelHablante = cerca ? null : ControlDelJugador.Nombre(hablante),
        });
    }

    /// <summary>Igual, con el personaje por su nombre («Will», «Estela», «Liam»). Nada si no es del grupo.</summary>
    public static LineaDicha Decir(string nombre, string clave, float distanciaParaBocadillo = DistanciaParaBocadillo)
        => ControlDelJugador.SlotDeNombre(nombre, out var hablante) ? Decir(hablante, clave, distanciaParaBocadillo) : LineaDicha.Nada;
}
