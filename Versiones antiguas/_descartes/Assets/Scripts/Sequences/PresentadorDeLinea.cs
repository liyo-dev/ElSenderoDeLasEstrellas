using System;
using UnityEngine;

/// El camino único por el que sale el texto de una cinemática.
///
/// Hay dos maneras de escribir una frase (los beats de diálogo y el reproductor de guiones
/// horneados) y las dos tienen que verse y oírse igual. Aquí se decide: se resuelve la clave de
/// localización, se elige con qué estilo se presenta, se enseña el bocadillo y se busca su voz.
/// Lo que cada una hace después (paginar, gesticular, esperar a que termine) es distinto, y se
/// queda en su sitio.
///
/// ── Por qué hay dos enums de presentación ────────────────────────────────────────────────────
/// `PresentacionDeTexto` (el de la UI) tiene tres valores y se guarda en los assets de
/// secuencia. `PresentacionDeLinea` (el de una frase) tiene esos tres más «heredar de la
/// secuencia», y también se guarda en los assets. Sus números no pueden ser los mismos, así
/// que la conversión vive aquí y en ningún otro sitio: restar uno al valor de la frase era
/// exactamente el tipo de conversión que se cuelga sin que nadie se entere.
public static class PresentadorDeLinea
{
    /// Traduce una clave de texto. Sin localización, devuelve la propia clave.
    public static string Texto(string clave)
        => string.IsNullOrEmpty(clave) ? string.Empty
           : (LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, clave) : clave);

    /// Traduce la clave del nombre del hablante. Si no hay nombre, null.
    public static string NombreDelHablante(string clave)
        => string.IsNullOrEmpty(clave) ? null : Texto(clave);

    /// El estilo con el que se presenta la frase: el que fija la frase, o el de la secuencia
    /// si la frase no lo fija.
    public static PresentacionDeTexto Estilo(PresentacionDeLinea deLaFrase,
        PresentacionDeTexto deLaSecuencia)
        => deLaFrase == PresentacionDeLinea.HeredarDeLaSecuencia
           ? deLaSecuencia
           : (PresentacionDeTexto)((int)deLaFrase - 1);

    /// La voz de la frase, si la hay.
    public static bool Voz(string clave, out AudioClip clip)
    {
        clip = null;
        return !string.IsNullOrEmpty(clave) && VoiceLines.TryGet(clave, out clip);
    }

    /// Suelta la voz de la frase, si la hay.
    public static void SonarLaVoz(string clave)
    {
        if (!Voz(clave, out var clip)) return;
        AudioService.Instance?.PlayVoice(clip);
    }

    /// Enseña la frase en el bocadillo (o en el subtítulo, según el estilo) y devuelve el turno
    /// del bocadillo, para poder esconderlo con su número.
    public static int Mostrar(Transform ancla, string texto, float duracion,
        PresentacionDeTexto presentacion, string nombreDelHablante, Color colorDelNombre,
        Vector3? desplazamiento, Action alTerminar = null, string gesto = null)
    {
        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo == null || ancla == null) return -1;
        return bocadillo.Show(ancla, texto, duracion, alTerminar, gesto,
            speakerName: nombreDelHablante, worldOffset: desplazamiento,
            presentacion: presentacion, colorDelNombre: colorDelNombre);
    }
}

/// Cómo se presenta una frase dentro de una cinemática.
///
/// Hereda de la secuencia: no se dice cómo, y manda lo que diga la definición. Los otros tres
/// valores son los estilos de la UI (bocadillo, subtítulo, subtítulo grande). Los números están
/// guardados en los assets de secuencia, así que no se pueden cambiar sin migrarlos.
public enum PresentacionDeLinea { HeredarDeLaSecuencia, Bocadillo, Subtitulo, SubtituloGrande }
