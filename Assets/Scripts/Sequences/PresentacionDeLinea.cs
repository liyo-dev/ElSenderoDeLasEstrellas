/// Cómo se presenta una frase dentro de una cinemática.
///
/// Hereda de la secuencia: no se dice cómo, y manda lo que diga la definición. Los otros tres
/// valores son los estilos de la UI (bocadillo, subtítulo, subtítulo grande). Los números están
/// guardados en los assets de secuencia, así que no se pueden cambiar sin migrarlos.
///
/// ── Por qué hay dos enums de presentación ────────────────────────────────────────────────────
/// `PresentacionDeTexto` (el de la UI) tiene tres valores y se guarda en los assets de
/// secuencia. `PresentacionDeLinea` (el de una frase) tiene esos tres más «heredar de la
/// secuencia», y también se guarda en los assets. Sus números no pueden ser los mismos, así
/// que la conversión vive aquí (`Resolver`) y en ningún otro sitio: restar uno al valor de la
/// frase a mano es el tipo de conversión que se cuela sin que nadie se entere.
public enum PresentacionDeLinea { HeredarDeLaSecuencia, Bocadillo, Subtitulo, SubtituloGrande }

public static class PresentacionDeLineaExtensiones
{
    /// El estilo con el que se presenta la frase: el que fija la frase, o el de la secuencia
    /// si la frase no lo fija.
    public static PresentacionDeTexto Resolver(this PresentacionDeLinea deLaFrase,
        PresentacionDeTexto deLaSecuencia)
        => deLaFrase == PresentacionDeLinea.HeredarDeLaSecuencia
           ? deLaSecuencia
           : (PresentacionDeTexto)((int)deLaFrase - 1);
}
