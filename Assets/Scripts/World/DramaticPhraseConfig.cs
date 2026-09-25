using UnityEngine;

public enum DramaticTextStyle
{
    Memory,   // Grisáceo, italic — recuerdos lejanos
    Dark,     // Rojizo/oscuro — presencia amenazante
    Urgent,   // Blanco puro — llamada de urgencia
    Epic      // Dorado — momento grandioso
}

public enum DramaticTextBackground
{
    None,
    SemiBlack,  // Alpha ~0.65
    FullBlack,  // Alpha 1.0
    Dream,      // Azul índigo profundo con pulso suave — para secuencias de sueño
    // Añadido 30/08/2026 (visión de la Voz en MagoOscuroFinalBattleSequencer, Fase C): mismo
    // concepto que Dream (nebulosa + chispas + degradado + shimmer en el texto) pero con un
    // blanco cálido en vez del azul índigo oscuro — pedido explícito de Raúl ("el fondo es
    // blanco"). El degradado de texto se sobreescribe automáticamente a un par de tonos oscuros
    // legibles sobre blanco (ver DramaticTextOverlayUI._dreamGradientLeftOnWhite/RightOnWhite) —
    // los tonos claros normales de Dream serían casi invisibles aquí.
    DreamWhite
}

public enum DramaticEntryAnimation
{
    ScaleUp,        // Pequeño → tamaño normal
    FadeIn,         // Opacidad 0 → 1
    TypeWriter,     // Carácter a carácter
    Instant,        // Sin animación
    SlideFromLeft,  // Entra deslizándose desde el borde izquierdo
    SlideFromRight, // Entra deslizándose desde el borde derecho
    KingdomHearts,  // Letra a letra: fade+escala por carácter, zoom suave del contenedor
    // Añadido 11/09/2026: para "Will, ¡DESPIERTA!" — KingdomHearts se lee como el título de una
    // novela/videojuego (pedido explícito de Raúl: no quiere ese efecto aquí). Shake aparece de
    // golpe (fade casi instantáneo) y sacude el texto con un temblor que decae — más parecido a
    // que alguien te esté zarandeando para despertarte que a una presentación épica.
    // SUPERSEDIDO 12/09/2026: probado en juego, Raúl reporta que "no funciona" — se deja el valor
    // por compatibilidad (no se borra ningún dato serializado) pero ya no se usa en "Will,
    // ¡DESPIERTA!"; ver LetterFlyIn más abajo.
    Shake,
    // Añadido 12/09/2026 (pedido explícito de Raúl, sustituye a Shake en "Will, ¡DESPIERTA!"):
    // letra a letra, cada carácter "vuela" desde un punto de origen compartido (grande, como
    // viniendo hacia el espectador desde el fondo de la pantalla) hasta su posición final en el
    // texto — ver DramaticTextOverlayUI.LetterFlyInRoutine / _letterFlyInOrigin.
    LetterFlyIn
}

public enum DramaticExitAnimation
{
    FadeOut,      // Opacidad 1 → 0
    ScaleUp,      // Crece y desvanece (épico que se disuelve)
    Instant,      // Corte directo
    SlideToLeft,  // Sale deslizándose por el borde izquierdo
    SlideToRight, // Sale deslizándose por el borde derecho
    CircleIris    // El fondo se abre en un círculo que crece y deja ver la escena (ver DramaticTextOverlayUI.CircleIrisExit)
}

[System.Serializable]
public struct DramaticPhrase
{
    [Tooltip("Texto de la frase. Usa textId para localización.")]
    [TextArea(1, 3)]
    public string text;

    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    public string textId;

    [Tooltip("Estilo visual de la frase.")]
    public DramaticTextStyle style;

    [Tooltip("Fondo de pantalla durante esta frase.")]
    public DramaticTextBackground background;

    [Tooltip("Cómo aparece el texto en pantalla.")]
    public DramaticEntryAnimation entryAnim;

    [Tooltip("Cómo desaparece el texto de pantalla.")]
    public DramaticExitAnimation exitAnim;

    [Tooltip("Segundos que la frase permanece visible (ignorado si waitForAudio es true).")]
    [Min(0.1f)]
    public float duration;

    [Tooltip("Clip de voz que suena al mostrar la frase.")]
    public AudioClip voiceClip;

    [Tooltip("Si true, la frase dura exactamente lo que dure el clip de audio.")]
    public bool waitForAudio;

    [Tooltip("Posición inicial del texto en el canvas (píxeles UI). El texto empieza aquí.")]
    public Vector2 positionOffset;

    [Tooltip("Si true, el texto se desplaza desde positionOffset hasta moveTo durante el tiempo que está visible.")]
    public bool useMovement;

    [Tooltip("Posición final del texto en el canvas (píxeles UI). Solo se usa si useMovement es true.")]
    public Vector2 moveTo;

    [Tooltip("Si true, esta frase ignora el degradado + shimmer del modo sueño aunque el config tenga dreamMode activado. " +
             "Úsalo para frases que deben mantener su color sólido (ej: una amenaza en rojo antes de entrar al sueño).")]
    public bool disableDreamTextEffect;
}

[CreateAssetMenu(menuName = "El Sendero/UI/Dramatic Text Config", fileName = "DramaticText_")]
public class DramaticPhraseConfig : ScriptableObject
{
    [Tooltip("Frases que se mostrarán en secuencia.")]
    public DramaticPhrase[] phrases;

    [Tooltip("Pausa entre frases consecutivas (segundos).")]
    [Min(0f)]
    public float pauseBetween = 0.15f;

    [Tooltip("Si true, activa el overlay de chispas flotantes (modo sueño) durante toda la secuencia.")]
    public bool dreamMode = false;
}
