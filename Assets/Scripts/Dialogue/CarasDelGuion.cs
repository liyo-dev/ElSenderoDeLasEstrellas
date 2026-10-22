using System;
using System.Collections.Generic;

/// El vocabulario de caras del juego para el guion horneado.
///
/// El asset horneado guarda la **palabra** de la cara («alegre»), no el enum. Así el asset no
/// depende del arte de este juego: quien use el sistema en otro proyecto traduce la palabra a
/// la cara que tenga. Aquí es donde esa traducción se hace, porque las caras las pone
/// `NPCEmotionController`, que es arte de este juego (los meshes de ojos y boca del rig).
///
/// Nota: esto es la costura de caras del plan de extracción (TDD § 25.3, costura 7). Cuando el
/// sistema se empaquete, esta clase pasa a ser la implementación de una interfaz, y el
/// horneador deja de conocerla para preguntar a un proveedor.
public static class CarasDelGuion
{
    /// La palabra canónica de cada cara: la que se guarda en el asset horneado.
    private static readonly Dictionary<NPCEmotion, string> s_canonicas = new()
    {
        { NPCEmotion.Neutral, "neutral" },
        { NPCEmotion.Happy, "alegre" },
        { NPCEmotion.Sad, "triste" },
        { NPCEmotion.Angry, "enfado" },
        { NPCEmotion.Surprised, "sorpresa" },
        { NPCEmotion.Scared, "miedo" },
        { NPCEmotion.Thinking, "pensativo" },
        { NPCEmotion.Tired, "cansado" },
        { NPCEmotion.Smirk, "picaro" },
        { NPCEmotion.Worried, "preocupado" },
        { NPCEmotion.Determined, "decidido" },
        { NPCEmotion.Relieved, "aliviado" },
        { NPCEmotion.Confused, "confuso" },
        { NPCEmotion.Excited, "ilusionado" },
        { NPCEmotion.Annoyed, "molesto" },
        { NPCEmotion.Grateful, "agradecido" },
    };

    /// Las palabras que el juego entiende, con sus sinónimos.
    private static readonly Dictionary<string, NPCEmotion> s_porPalabra = new(StringComparer.OrdinalIgnoreCase)
    {
        { "neutral", NPCEmotion.Neutral },
        { "alegre", NPCEmotion.Happy }, { "feliz", NPCEmotion.Happy },
        { "triste", NPCEmotion.Sad },
        { "enfado", NPCEmotion.Angry }, { "enfadado", NPCEmotion.Angry },
        { "sorpresa", NPCEmotion.Surprised },
        { "miedo", NPCEmotion.Scared },
        { "pensativo", NPCEmotion.Thinking },
        { "cansado", NPCEmotion.Tired },
        { "picaro", NPCEmotion.Smirk },
        { "preocupado", NPCEmotion.Worried },
        { "decidido", NPCEmotion.Determined },
        { "aliviado", NPCEmotion.Relieved },
        { "confuso", NPCEmotion.Confused },
        { "ilusionado", NPCEmotion.Excited },
        { "molesto", NPCEmotion.Annoyed },
        { "agradecido", NPCEmotion.Grateful },
    };

    /// Traduce una palabra a la cara del juego. Acepta la palabra en castellano y el nombre del
    /// enum («alegre», «happy»).
    public static bool Intentar(string palabra, out NPCEmotion emocion)
    {
        emocion = NPCEmotion.None;
        if (string.IsNullOrWhiteSpace(palabra)) return false;
        palabra = palabra.Trim();
        if (s_porPalabra.TryGetValue(palabra, out emocion)) return true;
        if (Enum.TryParse(palabra, true, out emocion) && emocion != NPCEmotion.None) return true;
        emocion = NPCEmotion.None;
        return false;
    }

    /// La palabra canónica de una cara: la que se guarda en el asset horneado.
    public static string Canonica(NPCEmotion emocion)
        => s_canonicas.TryGetValue(emocion, out var palabra) ? palabra : null;

    /// Todas las palabras que el juego entiende, para los avisos del horneador.
    public static IEnumerable<string> Palabras => s_porPalabra.Keys;
}
