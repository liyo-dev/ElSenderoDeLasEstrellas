using System;
using UnityEngine;

/// El camino único por el que se dice una frase en pantalla.
///
/// Todo lo que pone una frase en el bocadillo o en el subtítulo pasa por aquí: los beats de
/// diálogo y el reproductor de guiones de las cinemáticas, los nodos del grafo narrativo
/// (bocadillo, guiar al jugador), las charlas por el camino y los comentarios en reposo del grupo.
/// Así todas se ven y se oyen igual: se resuelve la clave de localización, se enseña el texto en
/// `SpeechBubbleUI` y se suelta su voz, si la tiene. Lo que cada uno hace después (paginar,
/// gesticular, esperar a que termine) es distinto y se queda en su sitio.
///
/// Hay dos maneras de usarlo:
/// - `Decir`: la frase completa (texto, voz y duración). Es lo que necesita casi todo el mundo.
/// - `Mostrar` + `SonarLaVoz`: por separado, para quien ya pagina y temporiza por su cuenta
///   (los beats de diálogo, el guion horneado).
public static class PresentadorDeLinea
{
    /// Margen que queda el texto en pantalla después de que termine su voz.
    public const float MargenTrasLaVoz = 0.3f;

    /// Hay dónde enseñar frases (la UI de Start está cargada).
    public static bool Disponible => SpeechBubbleUI.Instance != null;

    /// Turno de la frase que hay (o ha habido) en pantalla; -1 sin UI. Ver `SpeechBubbleUI.Turno`.
    public static int TurnoActual => SpeechBubbleUI.Instance != null ? SpeechBubbleUI.Instance.Turno : -1;

    /// Hay una frase en pantalla, sea de quien sea.
    public static bool HayAlgoEnPantalla => SpeechBubbleUI.Instance != null && SpeechBubbleUI.Instance.Mostrando;

    // ── Texto y voz ──────────────────────────────────────────────────────────────────────────

    /// Traduce una clave de texto. Sin localización, devuelve la propia clave.
    public static string Texto(string clave) => Texto(clave, clave);

    /// Traduce una clave de texto; si no hay clave o no está traducida, `porDefecto`.
    public static string Texto(string clave, string porDefecto)
    {
        if (string.IsNullOrEmpty(clave)) return porDefecto ?? string.Empty;
        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(clave, porDefecto ?? clave)
            : porDefecto ?? clave;
    }

    /// Traduce la clave del nombre del hablante. Si no hay nombre, null.
    public static string NombreDelHablante(string clave)
        => string.IsNullOrEmpty(clave) ? null : Texto(clave);

    /// La voz de la frase, si la hay.
    public static bool Voz(string clave, out AudioClip clip)
    {
        clip = null;
        return !string.IsNullOrEmpty(clave) && VoiceLines.TryGet(clave, out clip);
    }

    /// Suelta la voz de la frase, si la hay.
    public static void SonarLaVoz(string clave) => SonarLaVoz(clave, out _);

    /// Suelta la voz de la frase, si la hay, y dice cuál ha sonado (para poder callarla).
    public static bool SonarLaVoz(string clave, out AudioClip clip)
    {
        var audio = AudioService.Instance;
        if (audio == null || !Voz(clave, out clip)) { clip = null; return false; }
        audio.PlayVoice(clip);
        return true;
    }

    /// Calla esa voz si es la que está sonando (no la de otra frase que haya entrado después).
    public static void CallarLaVoz(AudioClip clip)
    {
        var audio = AudioService.Instance;
        if (clip != null && audio != null && audio.IsVoicePlaying(clip)) audio.StopVoice();
    }

    /// Lo que se tarda en leer el texto entero con esa presentación (todas sus páginas).
    public static float TiempoDeLectura(string texto, PresentacionDeTexto presentacion = PresentacionDeTexto.Bocadillo)
    {
        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo == null || string.IsNullOrWhiteSpace(texto)) return 0f;
        float total = 0f;
        foreach (var pagina in bocadillo.Paginar(texto, presentacion)) total += bocadillo.TiempoDeLectura(pagina);
        return total;
    }

    // ── Decir una frase entera ───────────────────────────────────────────────────────────────

    /// Dice una frase: la traduce, la enseña y suelta su voz. La frase dura lo que pide
    /// `FraseADecir.duracion`, pero nunca menos de lo que se tarda en leerla ni de lo que dura su voz
    /// (salvo `hastaQueSeEsconda`). Devuelve el turno y la duración final, para esperarla,
    /// esconderla o callar su voz; `LineaDicha.Nada` si no se ha podido decir.
    public static LineaDicha Decir(in FraseADecir frase)
    {
        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo == null) return LineaDicha.Nada;

        // Un bocadillo con pico necesita a quién señalar; fijo en pantalla o en subtítulo, no.
        bool necesitaAncla = frase.presentacion == PresentacionDeTexto.Bocadillo && !frase.fijoEnPantalla;
        if (necesitaAncla && frase.ancla == null) return LineaDicha.Nada;

        string texto = Texto(frase.clave, frase.textoPorDefecto);
        if (string.IsNullOrWhiteSpace(texto)) return LineaDicha.Nada;

        AudioClip voz = null;
        if (!frase.sinVoz) SonarLaVoz(frase.clave, out voz);

        float duracion = 0f;
        if (!frase.hastaQueSeEsconda)
        {
            duracion = Mathf.Max(frase.duracion, TiempoDeLectura(texto, frase.presentacion));
            if (voz != null) duracion = Mathf.Max(duracion, voz.length + MargenTrasLaVoz);
        }

        int turno = Ensenar(bocadillo, frase.ancla, texto, duracion, frase.presentacion,
            frase.nombreDelHablante, frase.colorDelNombre, frase.desplazamiento, frase.alTerminar,
            frase.gesto, frase.enfasis, frase.fijoEnPantalla);
        return new LineaDicha(turno, duracion, voz);
    }

    // ── Enseñar un texto ya preparado ────────────────────────────────────────────────────────

    /// Enseña el texto tal cual (ya traducido y paginado por quien llama) durante `duracion`
    /// segundos, sin voz, y devuelve el turno del bocadillo para poder esconderlo con su número.
    /// -1 si no hay UI o falta el ancla de un bocadillo con pico.
    public static int Mostrar(Transform ancla, string texto, float duracion,
        PresentacionDeTexto presentacion, string nombreDelHablante, Color colorDelNombre,
        Vector3? desplazamiento, Action alTerminar = null, string gesto = null,
        bool fijoEnPantalla = false)
    {
        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo == null) return -1;
        bool necesitaAncla = presentacion == PresentacionDeTexto.Bocadillo && !fijoEnPantalla;
        if (necesitaAncla && ancla == null) return -1;
        return Ensenar(bocadillo, ancla, texto, duracion, presentacion, nombreDelHablante,
            colorDelNombre, desplazamiento, alTerminar, gesto, false, fijoEnPantalla);
    }

    // ── Quitar la frase ──────────────────────────────────────────────────────────────────────

    /// Esconde la frase de ese turno, si sigue en pantalla (no la que otro haya puesto después).
    public static void Esconder(int turno) => SpeechBubbleUI.Instance?.Hide(turno);

    /// Da por terminada ya la frase en pantalla: avisa a quien la esperaba y la esconde.
    public static void SaltarLaActual() => SpeechBubbleUI.Instance?.SkipCurrent();

    private static int Ensenar(SpeechBubbleUI bocadillo, Transform ancla, string texto, float duracion,
        PresentacionDeTexto presentacion, string nombreDelHablante, Color colorDelNombre,
        Vector3? desplazamiento, Action alTerminar, string gesto, bool enfasis, bool fijoEnPantalla)
        => bocadillo.Show(ancla, texto, duracion, alTerminar, gesto, enfasis,
            speakerName: nombreDelHablante, worldOffset: desplazamiento,
            fijoEnPantalla: fijoEnPantalla, presentacion: presentacion, colorDelNombre: colorDelNombre);
}

/// Una frase para `PresentadorDeLinea.Decir`. Todos los campos valen por defecto: basta con la
/// clave (o el texto) y el ancla.
public struct FraseADecir
{
    /// Clave de localización del texto y de la voz.
    public string clave;
    /// Texto si no hay clave o la clave no está traducida.
    public string textoPorDefecto;
    /// Sobre quién sale el bocadillo. Puede ser null si la frase va fija en pantalla o en subtítulo.
    public Transform ancla;
    /// Segundos mínimos en pantalla (la frase siempre dura al menos lo que se tarda en leerla y lo
    /// que dura su voz).
    public float duracion;
    /// La frase se queda en pantalla hasta que alguien la esconda; `alTerminar` no llega solo.
    public bool hastaQueSeEsconda;
    /// No suelta la voz aunque la haya.
    public bool sinVoz;
    /// Bocadillo, subtítulo o subtítulo grande.
    public PresentacionDeTexto presentacion;
    /// Quieta en un lado de la pantalla, con el nombre de quien habla: una voz que llega de
    /// alguien que no está a la vista (o un texto suelto sin hablante).
    public bool fijoEnPantalla;
    /// Nombre que se enseña encima (subtítulo o bocadillo fijo). Ya traducido.
    public string nombreDelHablante;
    public Color colorDelNombre;
    /// Altura del bocadillo sobre el ancla, si no vale la de siempre.
    public Vector3? desplazamiento;
    /// Gesto (estado del Animator) que hace quien habla mientras lo dice.
    public string gesto;
    /// Bocadillo de énfasis (solo con el sprite antiguo del prefab).
    public bool enfasis;
    /// Se llama cuando la frase termina sola o se salta. No llega si otra frase la pisa.
    public Action alTerminar;
}

/// Lo que ha quedado de decir una frase: su turno en pantalla, cuánto dura y la voz que ha sonado.
public readonly struct LineaDicha
{
    public static readonly LineaDicha Nada = new(-1, 0f, null);

    /// Turno del bocadillo (ver `PresentadorDeLinea.Esconder`). -1 si no se ha dicho.
    public readonly int Turno;
    /// Segundos que va a estar en pantalla. 0 si se queda hasta que se esconda o no se ha dicho.
    public readonly float Duracion;
    /// La voz que ha sonado, para callarla con `PresentadorDeLinea.CallarLaVoz`. Null sin voz.
    public readonly AudioClip Voz;

    public bool Dicha => Turno > 0;

    public LineaDicha(int turno, float duracion, AudioClip voz)
    {
        Turno = turno;
        Duracion = duracion;
        Voz = voz;
    }
}
