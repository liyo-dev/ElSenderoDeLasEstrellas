/// <summary>Traduce las claves de texto que llevan los nodos.</summary>
public interface INarrativeTextLocalizer
{
    /// <summary>Texto de la clave en el idioma actual, o <paramref name="fallback"/> si no existe.</summary>
    string Localize(string key, string fallback);
}

/// <summary>Resuelve el texto de un nodo: clave traducida si la hay, si no el texto por defecto.</summary>
public static class NarrativeText
{
    /// <summary>
    /// Sin clave devuelve <paramref name="fallback"/>. Sin traductor registrado también: un juego sin
    /// localización es válido y sus nodos enseñan el texto escrito en el grafo.
    /// </summary>
    public static string Resolve(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        var localizer = NarrativeServices.Get<INarrativeTextLocalizer>();
        return localizer?.Localize(key, fallback) ?? fallback;
    }
}
